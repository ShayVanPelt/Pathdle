using Microsoft.Extensions.Configuration;
using Neo4j.Driver;
using Pathdle.Generator;
using Pathdle.Generator.Corpus;
using Pathdle.Generator.Generation;
using Pathdle.Generator.Persistence;

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
        Pathdle.Generator (group-graph / Wikidata)

          ingest-corpus [--max-entities=2000] [--seeds=path] [--demo]
          generate-daily [--date=YYYY-MM-DD] [--today] [--dry-run]

        --demo writes the hand-authored Dell→Vitamin C group graph to Neo4j (no Wikidata).

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
    var maxEntities = GetIntArg(args, "--max-entities", GetIntArg(args, "--max-articles", 2000));
    var demo = args.Any(a => a is "--demo");
    var seedsArg = GetStringArg(args, "--seeds");
    var corpusVersion = config["Pathdle:CorpusVersion"] ?? "wikidata-groups-v1";
    var root = EnvBootstrap.FindRepoRoot();
    var seedsPath = seedsArg
        ?? Path.Combine(root, "apps", "generator", "data", "seed_entities.txt");

    if (!demo && !File.Exists(seedsPath))
    {
        throw new FileNotFoundException(
            "Seed entities file not found. Pass --demo or create seed_entities.txt with QIDs.",
            seedsPath);
    }

    await using var store = CreateNeo4j(config);
    using var wikidata = new WikidataClient();
    var ingestor = new CorpusIngestor(wikidata, store);
    await ingestor.IngestAsync(
        seedsPath,
        corpusVersion,
        maxEntities,
        CancellationToken.None,
        demo);
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

    var corpusVersion = config["Pathdle:CorpusVersion"] ?? "wikidata-groups-v1";
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
        throw new InvalidOperationException(
            "No CorpusMeta in Neo4j. Run ingest-corpus --demo (or full Wikidata ingest) first.");
    }

    Console.WriteLine(
        $"Corpus {meta.Value.Version}: {meta.Value.Articles} entities, {meta.Value.Edges} edges");
    Console.WriteLine(
        $"Generating for {puzzleDate:yyyy-MM-dd} (corpusVersion={corpusVersion}, seed={runNonce})…");

    var graph = await store.LoadGraphAsync(CancellationToken.None);
    // Demo corpora are tiny; Wikidata co-membership graphs need sparsified defaults.
    var options = graph.Articles.Count < 80
        ? new GenerationOptions
        {
            MinLength = 3,
            MaxLength = 5,
            MinBoardNodes = 24,
            MaxBoardNodes = 40,
            MinScore = 15,
            MaxScore = 98,
            MaxAltShortest = 24,
            MaxAttempts = 400,
            MaxDegreeStart = 32,
            MaxDegreeTarget = 32,
            SparseMaxDegree = 12,
            MinFameStart = 0,
            MinFameTarget = 0,
            MinFameBoard = 0,
        }
        : new GenerationOptions();

    var generated = PuzzleGenerator.Generate(graph, puzzleDate, corpusVersion, runNonce, options);

    Console.WriteLine(
        $"Board: {generated.Puzzle.StartArticleId} → {generated.Puzzle.TargetArticleId}, "
        + $"{generated.Puzzle.Nodes.Count} nodes, {generated.Puzzle.Edges.Count} edges, "
        + $"optimal={generated.Puzzle.OptimalLength}, difficulty={generated.Difficulty.Score}");

    if (dryRun)
    {
        Console.WriteLine("Dry-run: not publishing to Postgres.");
        return 0;
    }

    await PuzzlePublisher.TryPublishAsync(pg, generated.Puzzle, CancellationToken.None);
    return 0;
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
