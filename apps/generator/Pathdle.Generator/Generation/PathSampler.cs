using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

internal static class PathSampler
{
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

        foreach (var start in starts)
        {
            var dist = GraphAlgorithms.BfsDistances(graph, start);
            var targets = dist
                .Where(kv => kv.Value >= opt.MinLength && kv.Value <= opt.MaxLength)
                .Select(kv => kv.Key)
                .Where(id => !string.Equals(id, start, StringComparison.Ordinal))
                .OrderBy(_ => rng.Next())
                .Take(40)
                .ToList();

            foreach (var target in targets)
            {
                var path = GraphAlgorithms.ReconstructShortestPath(graph, start, target);
                if (path is null) continue;
                var length = path.Count - 1;
                if (length < opt.MinLength || length > opt.MaxLength) continue;

                var alt = GraphAlgorithms.CountShortestPaths(graph, start, target);
                if (alt <= 0 || alt > opt.MaxAltShortest) continue;

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

                return new PathCandidate(
                    start,
                    target,
                    path,
                    length,
                    alt,
                    branchAvg,
                    hubPenalty);
            }
        }

        return null;
    }
}
