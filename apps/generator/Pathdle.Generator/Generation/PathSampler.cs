using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

/// <summary>
/// Samples START/TARGET at BFS distance 4–6 on a rarity-sparsified graph,
/// preferring famous (high sitelink) endpoints and rarer group edges.
/// </summary>
internal static class PathSampler
{
    public static PathCandidate? Sample(CorpusGraph graph, Random rng, GenerationOptions opt)
    {
        var starts = graph.Articles.Values
            .Where(a => TitleQuality.HasPlayableTitle(a.Title, a.Id))
            .Where(a => a.Popularity >= opt.MinFameStart || a.IsSeed)
            .Where(a =>
            {
                var deg = graph.Neighbors(a.Id).Count;
                return deg >= opt.MinDegreeStart && deg <= opt.MaxDegreeStart;
            })
            // Prefer seeds and famous entities.
            .OrderByDescending(a => a.IsSeed)
            .ThenByDescending(a => a.Popularity)
            .ThenBy(_ => rng.Next())
            .Take(160)
            .Select(a => a.Id)
            .ToList();

        if (starts.Count == 0) return null;

        var pool = new List<PathCandidate>();
        foreach (var start in starts)
        {
            var dist = GraphAlgorithms.BfsDistances(graph, start);
            var targets = dist
                .Where(kv => kv.Value >= opt.MinLength && kv.Value <= opt.MaxLength)
                .Select(kv => kv.Key)
                .Where(id =>
                {
                    if (!graph.Articles.TryGetValue(id, out var art)) return false;
                    if (!TitleQuality.HasPlayableTitle(art.Title, id)) return false;
                    if (art.Popularity < opt.MinFameTarget && !art.IsSeed) return false;
                    var deg = graph.Neighbors(id).Count;
                    return deg >= opt.MinDegreeTarget && deg <= opt.MaxDegreeTarget;
                })
                .OrderByDescending(id =>
                    graph.Articles.TryGetValue(id, out var a)
                        ? (a.IsSeed ? 1_000_000 : 0) + a.Popularity
                        : 0)
                .ThenBy(_ => rng.Next())
                .Take(40)
                .ToList();

            foreach (var target in targets)
            {
                var shortest = GraphAlgorithms.ReconstructShortestPath(graph, start, target);
                if (shortest is null) continue;
                var length = shortest.Count - 1;
                if (length < opt.MinLength || length > opt.MaxLength) continue;

                if (!PathTitlesOk(graph, shortest)) continue;

                // Mid-path nodes should mostly be recognizable.
                if (!PathFameOk(graph, shortest, opt)) continue;

                var alt = GraphAlgorithms.CountShortestPaths(graph, start, target, opt.MaxAltShortest + 1);
                if (alt > opt.MaxAltShortest) continue;

                var mid = GraphAlgorithms.MidPathBranchAverage(graph, shortest);
                if (mid < 1.0) continue;

                var hub = GraphAlgorithms.HubPenalty(graph, shortest);
                if (hub > 0.98) continue;

                var rarity = AvgPathRarity(graph, shortest);
                var fame = AvgPathFame(graph, shortest);
                pool.Add(new PathCandidate(
                    start, target, shortest, length, alt, mid, hub, rarity, fame));
            }

            if (pool.Count >= 40) break;
        }

        if (pool.Count == 0) return null;

        var ranked = pool
            .OrderByDescending(p => p.AvgFame)
            .ThenByDescending(p => p.AvgRarity)
            .ThenBy(p => p.AltShortestCount)
            .ThenBy(p => p.HubPenalty)
            .ToList();
        var top = ranked.Take(Math.Max(1, ranked.Count / 3)).ToList();
        return top[rng.Next(top.Count)];
    }

    private static bool PathTitlesOk(CorpusGraph graph, IReadOnlyList<string> path)
    {
        var titles = new List<string>();
        foreach (var id in path)
        {
            if (!graph.Articles.TryGetValue(id, out var art)) return false;
            if (!TitleQuality.HasPlayableTitle(art.Title, id)) return false;
            if (TitleQuality.ConflictsWithBoard(art.Title, titles, maxPerFamily: 1)) return false;
            titles.Add(art.Title);
        }

        return true;
    }

    private static bool PathFameOk(CorpusGraph graph, IReadOnlyList<string> path, GenerationOptions opt)
    {
        // Allow at most one obscure mid node; start/target already fame-gated.
        var obscureMids = 0;
        for (var i = 1; i < path.Count - 1; i++)
        {
            if (!graph.Articles.TryGetValue(path[i], out var art)) return false;
            if (art.Popularity < opt.MinFameBoard && !art.IsSeed) obscureMids++;
        }

        return obscureMids <= 1;
    }

    private static double AvgPathRarity(CorpusGraph graph, IReadOnlyList<string> path)
    {
        if (path.Count < 2) return 0;
        double sum = 0;
        var n = 0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var e = graph.FindEdge(path[i], path[i + 1]);
            if (e is null) continue;
            sum += e.Rarity;
            n++;
        }

        return n == 0 ? 0 : sum / n;
    }

    private static double AvgPathFame(CorpusGraph graph, IReadOnlyList<string> path)
    {
        if (path.Count == 0) return 0;
        double sum = 0;
        foreach (var id in path)
        {
            if (graph.Articles.TryGetValue(id, out var a)) sum += a.Popularity;
        }

        return sum / path.Count;
    }
}
