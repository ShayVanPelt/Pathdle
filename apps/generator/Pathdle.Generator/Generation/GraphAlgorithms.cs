using System.Security.Cryptography;
using System.Text;
using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

internal sealed class GenerationOptions
{
    public int MinLength { get; init; } = 4;
    public int MaxLength { get; init; } = 6;
    public int MinBoardNodes { get; init; } = 30;
    public int MaxBoardNodes { get; init; } = 40;
    public int MaxAttempts { get; init; } = 400;
    public int MinScore { get; init; } = 30;
    public int MaxScore { get; init; } = 95;
    /// <summary>Dense co-membership graphs have many shortest paths — allow more than Wikipedia link graphs.</summary>
    public int MaxAltShortest { get; init; } = 16;
    public int MinDegreeStart { get; init; } = 2;
    /// <summary>After rarity sparsification; still allow moderately connected hubs.</summary>
    public int MaxDegreeStart { get; init; } = 24;
    public int MinDegreeTarget { get; init; } = 2;
    public int MaxDegreeTarget { get; init; } = 24;
    public int TrapDepth { get; init; } = 2;
    public int TrapsPerMidNode { get; init; } = 3;
    /// <summary>Keep only this many highest-rarity edges per node before path search.</summary>
    public int SparseMaxDegree { get; init; } = 10;
    /// <summary>Min Wikipedia sitelinks for START (fame).</summary>
    public int MinFameStart { get; init; } = 25;
    /// <summary>Min Wikipedia sitelinks for TARGET.</summary>
    public int MinFameTarget { get; init; } = 25;
    /// <summary>Most board fillers must meet this fame floor.</summary>
    public int MinFameBoard { get; init; } = 12;
}

internal sealed record PathCandidate(
    string StartId,
    string TargetId,
    IReadOnlyList<string> OptimalPath,
    int OptimalLength,
    int AltShortestCount,
    double MidPathBranchAvg,
    double HubPenalty,
    double AvgRarity,
    double AvgFame = 0);

internal sealed record BoardEdge(
    string From,
    string To,
    string GroupId,
    string GroupLabel,
    double Rarity);

internal sealed record BuiltBoard(
    PathCandidate Path,
    HashSet<string> NodeIds,
    HashSet<BoardEdge> Edges,
    HashSet<string> TrapNodeIds,
    int TrapEdgeCount);

internal static class SeededRng
{
    public static Random From(string puzzleDate, string corpusVersion, string runNonce)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{puzzleDate}|{corpusVersion}|{runNonce}"));
        var seed = BitConverter.ToInt32(bytes, 0);
        return new Random(seed);
    }
}

internal static class GraphAlgorithms
{
    public static Dictionary<string, int> BfsDistances(CorpusGraph graph, string start)
    {
        var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 0 };
        var q = new Queue<string>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            var d = dist[u];
            foreach (var v in graph.Neighbors(u))
            {
                if (dist.ContainsKey(v)) continue;
                dist[v] = d + 1;
                q.Enqueue(v);
            }
        }

        return dist;
    }

    public static List<string>? ReconstructShortestPath(
        CorpusGraph graph,
        string start,
        string target)
    {
        if (start == target) return [start];
        var parent = new Dictionary<string, string?>(StringComparer.Ordinal) { [start] = null };
        var q = new Queue<string>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            foreach (var v in graph.Neighbors(u))
            {
                if (parent.ContainsKey(v)) continue;
                parent[v] = u;
                if (v == target)
                {
                    var path = new List<string>();
                    string? cur = target;
                    while (cur is not null)
                    {
                        path.Add(cur);
                        cur = parent[cur];
                    }

                    path.Reverse();
                    return path;
                }

                q.Enqueue(v);
            }
        }

        return null;
    }

    public static int CountShortestPaths(CorpusGraph graph, string start, string target, int maxCount = 8)
    {
        var dist = BfsDistances(graph, start);
        if (!dist.TryGetValue(target, out var targetDist)) return 0;

        var ways = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 1 };
        var ordered = dist.OrderBy(kv => kv.Value).Select(kv => kv.Key);
        foreach (var u in ordered)
        {
            if (!ways.TryGetValue(u, out var wu)) continue;
            foreach (var v in graph.Neighbors(u))
            {
                if (!dist.TryGetValue(v, out var dv) || dv != dist[u] + 1) continue;
                ways[v] = Math.Min(maxCount, ways.GetValueOrDefault(v) + wu);
            }
        }

        return ways.GetValueOrDefault(target);
    }

    public static double MidPathBranchAverage(CorpusGraph graph, IReadOnlyList<string> path)
    {
        if (path.Count <= 2) return 0;
        double sum = 0;
        var mids = 0;
        for (var i = 1; i < path.Count - 1; i++)
        {
            sum += graph.Neighbors(path[i]).Count;
            mids++;
        }

        return mids == 0 ? 0 : sum / mids;
    }

    public static double HubPenalty(CorpusGraph graph, IReadOnlyList<string> path)
    {
        // Degree-based hubbiness (not sitelink fame — famous nodes are desirable).
        var maxDeg = 0;
        foreach (var id in graph.Articles.Keys)
        {
            maxDeg = Math.Max(maxDeg, graph.Neighbors(id).Count);
        }

        if (maxDeg <= 0) return 0;
        return path.Average(id => graph.Neighbors(id).Count / (double)maxDeg);
    }

    public static string UndirectedKey(string a, string b) => CorpusGraph.UndirectedKey(a, b);

    /// <summary>
    /// Co-membership graphs are extremely dense. Keep only the highest-rarity edges
    /// per node so shortest paths of length 4–6 exist for daily puzzles.
    /// </summary>
    public static CorpusGraph SparsifyByRarity(CorpusGraph full, int maxDegreePerNode)
    {
        var best = new Dictionary<string, CorpusEdge>(StringComparer.Ordinal);
        foreach (var id in full.Articles.Keys)
        {
            foreach (var edge in full.EdgesFrom(id)
                         .OrderByDescending(e => e.Rarity)
                         .ThenBy(e => e.GroupId, StringComparer.Ordinal)
                         .Take(Math.Max(2, maxDegreePerNode)))
            {
                var key = UndirectedKey(edge.From, edge.To);
                if (!best.TryGetValue(key, out var prev) || edge.Rarity > prev.Rarity)
                {
                    best[key] = edge;
                }
            }
        }

        var adjacency = full.Articles.Keys.ToDictionary(
            k => k,
            _ => new List<CorpusEdge>(),
            StringComparer.Ordinal);

        foreach (var edge in best.Values)
        {
            if (!adjacency.ContainsKey(edge.From) || !adjacency.ContainsKey(edge.To)) continue;
            adjacency[edge.From].Add(edge);
            adjacency[edge.To].Add(new CorpusEdge(
                edge.To, edge.From, edge.GroupId, edge.GroupLabel, edge.Rarity));
        }

        return new CorpusGraph(full.Articles, adjacency, best.Count);
    }
}
