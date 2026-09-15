using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

internal static class PathSampler
{
    private const int MaxCandidates = 24;
    private const int MaxPairProbes = 120;

    public static PathCandidate? Sample(CorpusGraph graph, Random rng, GenerationOptions opt)
    {
        var starts = graph.Articles.Values
            .Where(a =>
                a.DegreeOut >= opt.MinOutDegreeStart
                && a.DegreeOut <= opt.MaxOutDegreeStart
                && a.DegreeIn >= 1)
            .OrderBy(_ => rng.Next())
            .Take(160)
            .Select(a => a.Id)
            .ToList();

        if (starts.Count == 0) return null;

        var candidates = new List<PathCandidate>(MaxCandidates);
        var probes = 0;

        foreach (var start in starts)
        {
            if (candidates.Count >= MaxCandidates) break;
            if (probes >= MaxPairProbes && candidates.Count > 0) break;

            var dist = GraphAlgorithms.BfsDistances(graph, start);
            var targets = dist
                .Where(kv => kv.Value >= opt.MinLength && kv.Value <= opt.MaxLength)
                .Select(kv => kv.Key)
                .Where(id => !string.Equals(id, start, StringComparison.Ordinal))
                .Where(id =>
                {
                    if (!graph.Articles.TryGetValue(id, out var art)) return false;
                    return art.DegreeIn >= opt.MinInDegreeTarget
                        && art.DegreeIn <= opt.MaxInDegreeTarget
                        && art.DegreeOut >= opt.MinOutDegreeTarget;
                })
                .OrderBy(_ => rng.Next())
                .Take(40)
                .ToList();

            foreach (var target in targets)
            {
                if (candidates.Count >= MaxCandidates) break;
                if (probes >= MaxPairProbes && candidates.Count > 0) break;

                probes++;
                var path = GraphAlgorithms.ReconstructShortestPath(graph, start, target);
                if (path is null) continue;
                var length = path.Count - 1;
                if (length < opt.MinLength || length > opt.MaxLength) continue;

                const int altCountCap = 1024;
                var alt = GraphAlgorithms.CountShortestPaths(graph, start, target, altCountCap);
                if (alt <= 0) continue;

                var mid = path.Skip(1).Take(path.Count - 2).ToList();
                if (mid.Count == 0) continue;

                var branchAvg = mid.Average(id => graph.Neighbors(id).Count);
                if (branchAvg < 1.0) continue;

                var hubPenalty = path.Average(id =>
                {
                    var pop = graph.Articles[id].Popularity;
                    return pop > 400 ? 1.0 : pop > 200 ? 0.5 : 0.0;
                });
                if (hubPenalty > 0.75) continue;

                candidates.Add(new PathCandidate(
                    start,
                    target,
                    path,
                    length,
                    alt,
                    branchAvg,
                    hubPenalty));
            }
        }

        if (candidates.Count == 0) return null;

        // Mid-degree TARGETs sit in a dense subgraph; prefer clearer paths but keep variety.
        var poolSize = Math.Max(1, (candidates.Count + 2) / 3);
        var pool = candidates
            .OrderBy(c => c.AltShortestCount)
            .ThenBy(_ => rng.Next())
            .Take(poolSize)
            .ToList();
        return pool[rng.Next(pool.Count)];
    }
}
