using Microsoft.Extensions.Configuration;
using Neo4j.Driver;
using Pathdle.Generator;
using Pathdle.Generator.Corpus;
using Pathdle.Generator.Generation;
using Pathdle.Generator.Publish;

var config = EnvBootstrap.Load();
if (args.Length == 0)
{
    PrintHelp();
    return 1;
}

var command = args[0].ToLowerInvariant();
try
{
    return command switch
    {
        "ingest-corpus" => await IngestAsync(config, args.Skip(1).ToArray()),
        "generate-daily" => await GenerateAsync(config, args.Skip(1).ToArray()),
        "diagnose-links" => await DiagnoseLinksAsync(args.Skip(1).ToArray()),
        "help" or "--help" or "-h" => PrintHelp(),
        _ => Unknown(command)
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 2;
}

static int PrintHelp()
{
    Console.WriteLine(
        """
        Pathdle.Generator

          ingest-corpus [--max-articles=8000] [--seeds=path]
                        [--scout-pages=6] [--induce-pages=0]
          generate-daily [--date=YYYY-MM-DD] [--today] [--dry-run]
          diagnose-links [--title=Albert_Einstein] [--expect=Physics]

        induce-pages=0 means unlimited MediaWiki pagination (keep-filtered early-stop still applies).

        Env (root .env): Neo4j__*, ConnectionStrings__Postgres, Pathdle__CorpusVersion
        """);
    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command '{command}'.");
    PrintHelp();
    return 1;
}

static async Task<int> IngestAsync(IConfiguration config, string[] args)
{
    var maxArticles = GetIntArg(args, "--max-articles", 8000);
    var scoutPages = GetIntArg(args, "--scout-pages", 6);
    var inducePages = GetIntArg(args, "--induce-pages", 0);
    var seedsArg = GetStringArg(args, "--seeds");
    var corpusVersion = config["Pathdle:CorpusVersion"] ?? "wiki-crawl-mvp-v1";
    var root = EnvBootstrap.FindRepoRoot();
    var seedsPath = seedsArg
        ?? Path.Combine(root, "apps", "generator", "data", "seed_articles.txt");

    if (!File.Exists(seedsPath))
    {
        throw new FileNotFoundException("Seed articles file not found.", seedsPath);
    }

    await using var store = CreateNeo4j(config);
    using var wiki = new MediaWikiClient();
    var ingestor = new CorpusIngestor(wiki, store);
    await ingestor.IngestAsync(
        seedsPath,
        corpusVersion,
        maxArticles,
        CancellationToken.None,
        scoutPages,
        inducePages);
    return 0;
}

static async Task<int> GenerateAsync(IConfiguration config, string[] args)
{
    var today = args.Any(a => a is "--today");
    var dryRun = args.Any(a => a is "--dry-run");
    var dateArg = GetStringArg(args, "--date");
    var pinnedDate = dateArg is not null;
    var floorDate = pinnedDate
        ? DateOnly.Parse(dateArg!)
        : today
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    var corpusVersion = config["Pathdle:CorpusVersion"] ?? "wiki-crawl-mvp-v1";
    var pg = config.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

    DateOnly puzzleDate;
    if (pinnedDate)
    {
        puzzleDate = floorDate;
    }
    else
    {
        puzzleDate = await PuzzlePublisher.FindNextFreeDateAsync(pg, floorDate, CancellationToken.None);
        if (puzzleDate != floorDate)
        {
            Console.WriteLine(
                $"Next free date: {puzzleDate:yyyy-MM-dd} (floor was {floorDate:yyyy-MM-dd}).");
        }
    }

    var runNonce = Guid.NewGuid().ToString("N")[..12];

    await using var store = CreateNeo4j(config);
    var meta = await store.GetLatestMetaAsync(CancellationToken.None);
    if (meta is null)
    {
        throw new InvalidOperationException("No CorpusMeta in Neo4j. Run ingest-corpus first.");
    }

    Console.WriteLine(
        $"Corpus {meta.Value.Version}: {meta.Value.Articles} articles, {meta.Value.Edges} edges");
    Console.WriteLine(
        $"Generating for {puzzleDate:yyyy-MM-dd} (corpusVersion={corpusVersion}, seed={runNonce})…");

    var graph = await store.LoadGraphAsync(CancellationToken.None);
    var generated = PuzzleGenerator.Generate(graph, puzzleDate, corpusVersion, runNonce);

    Console.WriteLine(
        $"Board: {generated.Puzzle.StartArticleId} → {generated.Puzzle.TargetArticleId}, "
        + $"{generated.Puzzle.Nodes.Count} nodes, {generated.Puzzle.Edges.Count} edges, "
        + $"optimal={generated.Puzzle.OptimalLength}, difficulty={generated.Difficulty.Score}");

    if (dryRun)
    {
        Console.WriteLine("Dry-run: not publishing to Postgres.");
        return 0;
    }

    var published = await PuzzlePublisher.TryPublishAsync(pg, generated.Puzzle, CancellationToken.None);
    return published ? 0 : 0; // idempotent skip is success
}

static async Task<int> DiagnoseLinksAsync(string[] args)
{
    var title = GetStringArg(args, "--title") ?? "Albert_Einstein";
    var expect = GetStringArg(args, "--expect") ?? "Physics";
    using var wiki = new MediaWikiClient();

    Console.WriteLine($"Fetching raw links for {title} (5 pages)…");
    var raw = await wiki.GetMainNamespaceLinksAsync(title, 5, CancellationToken.None);
    Console.WriteLine($"  raw count={raw.Count}, has {expect}={raw.Contains(expect)}");

    var keep = new HashSet<string>(StringComparer.Ordinal) { title, expect, "Mathematics", "Nintendo" };
    Console.WriteLine($"Fetching keep-filtered links for {title}…");
    var filtered = await wiki.GetMainNamespaceLinksAsync(title, 5, CancellationToken.None, keep);
    Console.WriteLine($"  filtered count={filtered.Count}: {string.Join(", ", filtered)}");

    Console.WriteLine("Batch fetch (same keep)…");
    var batch = await wiki.GetOutboundLinksBatchAsync([title], CancellationToken.None, 5, keep);
    Console.WriteLine(
        $"  batch[{title}] count={batch[title].Count}: {string.Join(", ", batch[title])}");

    return raw.Contains(expect) ? 0 : 2;
}

static Neo4jCorpusStore CreateNeo4j(IConfiguration config)
{
    var uri = config["Neo4j:Uri"] ?? "bolt://localhost:7687";
    var user = config["Neo4j:Username"] ?? "neo4j";
    var password = config["Neo4j:Password"]
        ?? throw new InvalidOperationException("Neo4j:Password is required.");
    var database = config["Neo4j:Database"] ?? "neo4j";
    var driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
    return new Neo4jCorpusStore(driver, database);
}

static int GetIntArg(string[] args, string name, int fallback)
{
    var raw = GetStringArg(args, name);
    return raw is not null && int.TryParse(raw, out var n) ? n : fallback;
}

static string? GetStringArg(string[] args, string name)
{
    foreach (var a in args)
    {
        if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
        {
            return a[(name.Length + 1)..];
        }
    }

    return null;
}
