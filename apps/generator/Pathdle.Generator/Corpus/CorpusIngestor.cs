namespace Pathdle.Generator.Corpus;

internal sealed class CorpusIngestor(
    MediaWikiClient wiki,
    Neo4jCorpusStore store)
{
    /// <summary>
    /// High-signal edges that should survive ingest when both endpoints are kept.
    /// Used as a post-write fidelity report (Wikipedia has it → Neo4j must too).
    /// </summary>
    private static readonly (string From, string To)[] CanaryEdges =
    [
        ("Albert_Einstein", "Physics"),
        ("Albert_Einstein", "Relativity"),
        ("Physics", "Mathematics"),
        ("Mathematics", "Geometry"),
        ("Nintendo", "Video_game"),
        ("Japan", "Tokyo"),
        ("United_States", "Washington,_D.C."),
        ("World_War_II", "Germany"),
        ("Computer_science", "Algorithm"),
        ("Biology", "DNA"),
    ];

    public async Task IngestAsync(
        string seedsPath,
        string corpusVersion,
        int maxArticles,
        CancellationToken ct,
        int scoutPages = 6,
        int inducePages = 0)
    {
        var seeds = (await File.ReadAllLinesAsync(seedsPath, ct))
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith('#'))
            .Select(MediaWikiClient.NormalizeId)
            .Where(id => id is not null && !IsJunkArticle(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(maxArticles)
            .ToList();

        Console.WriteLine(
            $"Building induced corpus from {seeds.Count} curated seeds (max={maxArticles}).");
        Console.WriteLine(
            $"  scoutPages={scoutPages}×500, inducePages={(inducePages <= 0 ? "unlimited" : $"{inducePages}×500")}.");

        await store.EnsureSchemaAsync(ct);
        await store.ClearCorpusAsync(ct);

        var titles = seeds.ToDictionary(
            id => id,
            MediaWikiClient.TitleFromId,
            StringComparer.Ordinal);

        var seedSet = titles.Keys.ToHashSet(StringComparer.Ordinal);
        var scoutLinks = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        // Scout pass: deep enough to catch mid-alphabet neighbors for expansions
        // (Physics is ~page 2 on Einstein; denser scout → better keep set).
        Console.WriteLine($"Scouting outbound links for seeds (up to {scoutPages}×500 each)…");
        foreach (var chunk in seeds.Chunk(8))
        {
            var batch = chunk.ToList();
            Console.WriteLine($"  scout batch ({batch[0]} …) size={batch.Count}");
            var fetched = await wiki.GetOutboundLinksBatchAsync(batch, ct, scoutPages);
            foreach (var (from, links) in fetched)
            {
                scoutLinks[from] = links;
            }

            await Task.Delay(200, ct);
        }

        if (titles.Count < maxArticles)
        {
            var overlapScores = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var links in scoutLinks.Values)
            {
                foreach (var to in links)
                {
                    if (seedSet.Contains(to) || IsJunkArticle(to)) continue;
                    overlapScores[to] = overlapScores.GetValueOrDefault(to) + 1;
                }
            }

            var expansions = overlapScores
                .Where(kv => kv.Value >= 2)
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => kv.Key)
                .Where(id => !titles.ContainsKey(id))
                .Take(maxArticles - titles.Count)
                .ToList();

            Console.WriteLine($"Expanding with {expansions.Count} high-overlap neighbors…");
            foreach (var id in expansions)
            {
                titles[id] = MediaWikiClient.TitleFromId(id);
            }
        }

        var keep = titles.Keys.ToHashSet(StringComparer.Ordinal);

        // Seed edge map with scout intersections so early alphabet hubs aren't empty
        // if the later induce pass partially fails under rate limits.
        var allLinks = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var id in keep)
        {
            allLinks[id] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (from, links) in scoutLinks)
        {
            if (!allLinks.TryGetValue(from, out var set)) continue;
            foreach (var to in links)
            {
                if (keep.Contains(to) && !string.Equals(from, to, StringComparison.Ordinal))
                    set.Add(to);
            }
        }

        // Induce pass: paginate every kept article; retain only keep→keep edges.
        // inducePages=0 → unlimited (alphabetical early-stop still applies with keep filter).
        var induceLabel = inducePages <= 0 ? "unlimited" : $"{inducePages}×500";
        Console.WriteLine(
            $"Inducing keep→keep edges for {keep.Count} articles ({induceLabel} each)…");
        var failed = new List<string>();
        var done = 0;
        foreach (var chunk in keep.OrderBy(x => x, StringComparer.Ordinal).Chunk(8))
        {
            var batch = chunk.ToList();
            var fetched = await wiki.GetOutboundLinksBatchAsync(batch, ct, inducePages, keep);
            foreach (var (from, links) in fetched)
            {
                if (!allLinks.TryGetValue(from, out var set)) continue;
                if (links.Count == 0 && set.Count == 0) failed.Add(from);
                foreach (var to in links)
                {
                    if (!string.Equals(from, to, StringComparison.Ordinal))
                        set.Add(to);
                }
            }

            done += batch.Count;
            if (done % 40 == 0 || done >= keep.Count)
            {
                Console.WriteLine($"  induced {done}/{keep.Count}");
            }

            await Task.Delay(150, ct);
        }

        // Retry every empty / failed article (seeds first — they matter most for canaries).
        var retryIds = allLinks
            .Where(kv => kv.Value.Count == 0)
            .Select(kv => kv.Key)
            .Concat(failed)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => seedSet.Contains(id) ? 0 : 1)
            .ThenBy(x => x, StringComparer.Ordinal)
            .ToList();

        if (retryIds.Count > 0)
        {
            Console.WriteLine($"Retrying {retryIds.Count} articles with empty/failed link fetches…");
            var ri = 0;
            foreach (var id in retryIds)
            {
                ri++;
                try
                {
                    var links = await wiki.GetMainNamespaceLinksAsync(id, inducePages, ct, keep);
                    foreach (var to in links)
                    {
                        if (!string.Equals(id, to, StringComparison.Ordinal))
                            allLinks[id].Add(to);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  warn: retry {id} failed: {ex.Message}");
                }

                if (ri % 25 == 0 || ri == retryIds.Count)
                    Console.WriteLine($"  retry {ri}/{retryIds.Count}");

                await Task.Delay(120, ct);
            }
        }

        var internalEdges = new HashSet<(string From, string To)>();
        foreach (var (from, links) in allLinks)
        {
            foreach (var to in links)
            {
                if (keep.Contains(to))
                    internalEdges.Add((from, to));
            }
        }

        var withOut = allLinks.Count(kv => kv.Value.Count > 0);
        var zeroOut = allLinks.Count - withOut;
        Console.WriteLine(
            $"Writing {titles.Count} articles, {internalEdges.Count} edges to Neo4j "
            + $"(articles with outbound={withOut}, zeroOutbound={zeroOut})…");

        ReportCanaries(keep, internalEdges);

        if (internalEdges.Count < titles.Count * 2)
        {
            Console.WriteLine(
                "  note: sparse induced graph — add more related seeds or raise max-articles.");
        }

        await store.UpsertArticlesAndEdgesAsync(titles, internalEdges, ct);
        await store.WriteCorpusMetaAsync(corpusVersion, titles.Count, internalEdges.Count, ct);
        Console.WriteLine($"Corpus '{corpusVersion}' ready.");
    }

    private static void ReportCanaries(
        HashSet<string> keep,
        HashSet<(string From, string To)> edges)
    {
        Console.WriteLine("Canary edges (both endpoints must be in keep set):");
        var ok = 0;
        var missing = 0;
        var skipped = 0;

        foreach (var (from, to) in CanaryEdges)
        {
            var fromKept = keep.Contains(from);
            var toKept = keep.Contains(to);
            if (!fromKept || !toKept)
            {
                skipped++;
                var missingEnds = new List<string>();
                if (!fromKept) missingEnds.Add(from);
                if (!toKept) missingEnds.Add(to);
                Console.WriteLine(
                    $"  SKIP  {from}→{to} (not in keep: {string.Join(", ", missingEnds)})");
                continue;
            }

            if (edges.Contains((from, to)))
            {
                ok++;
                Console.WriteLine($"  OK    {from}→{to}");
            }
            else
            {
                missing++;
                Console.WriteLine($"  MISS  {from}→{to}  ← Wikipedia-class link absent from Neo4j");
            }
        }

        Console.WriteLine(
            $"  canary summary: ok={ok}, miss={missing}, skip={skipped} (miss = wiki fidelity gap)");
    }

    private static bool IsJunkArticle(string id) =>
        id.EndsWith("_(identifier)", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("List_of_", StringComparison.OrdinalIgnoreCase)
        || id.Equals("ISBN", StringComparison.OrdinalIgnoreCase)
        || id.Equals("DOI", StringComparison.OrdinalIgnoreCase)
        || id.Equals("ISSN", StringComparison.OrdinalIgnoreCase)
        || id.Equals("PMID", StringComparison.OrdinalIgnoreCase);
}
