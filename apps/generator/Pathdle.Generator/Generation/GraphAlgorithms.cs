using System.Security.Cryptography;
using System.Text;
using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

internal sealed class GenerationOptions
{
    public int MinLength { get; init; } = 4;
    public int MaxLength { get; init; } = 6;
    public int MinBoardNodes { get; init; } = 40;
    public int MaxBoardNodes { get; init; } = 75;
    public int MaxAttempts { get; init; } = 250;
    public int MinScore { get; init; } = 38;
    public int MaxScore { get; init; } = 90;
    public int MaxAltShortest { get; init; } = 4;
    public int MinOutDegreeStart { get; init; } = 2;
    public int MaxOutDegreeStart { get; init; } = 140;
    public int MinInDegreeTarget { get; init; } = 15;
    public int MaxInDegreeTarget { get; init; } = 400;
    public int MinOutDegreeTarget { get; init; } = 2;
    public int TrapDepth { get; init; } = 2;
    public int TrapsPerMidNode { get; init; } = 3;
}

internal sealed record PathCandidate(
    string StartId,
    string TargetId,
    IReadOnlyList<string> OptimalPath,
    int OptimalLength,
    int AltShortestCount,
    double MidPathBranchAvg,
    double HubPenalty);

internal sealed record BuiltBoard(
    PathCandidate Path,
    HashSet<string> NodeIds,
    HashSet<(string From, string To)> Edges,
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
    public static Dictionary<string, int> BfsDistances(
        CorpusGraph graph,
        string start)
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

    public static int CountShortestPaths(
        CorpusGraph graph,
        string start,
        string target,
        int maxWays = int.MaxValue)
    {
        var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 0 };
        var ways = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 1 };
        var q = new Queue<string>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            var du = dist[u];
            if (dist.TryGetValue(target, out var dt) && du > dt) continue;
            foreach (var v in graph.Neighbors(u))
            {
                if (!dist.ContainsKey(v))
                {
                    dist[v] = du + 1;
                    ways[v] = ways[u];
                    q.Enqueue(v);
                }
                else if (dist[v] == du + 1)
                {
                    ways[v] += ways[u];
                }

                if (v == target && ways[v] > maxWays)
                {
                    return ways[v];
                }
            }
        }

        return ways.TryGetValue(target, out var w) ? w : 0;
    }

    public static HashSet<string> NodesOnAnyShortestPath(
        CorpusGraph graph,
        string start,
        string target)
    {
        var forward = BfsDistances(graph, start);
        if (!forward.ContainsKey(target)) return [];

        // Reverse BFS on inverted edges.
        var inbound = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (from, outs) in graph.Outbound)
        {
            foreach (var to in outs)
            {
                if (!inbound.TryGetValue(to, out var list))
                {
                    list = [];
                    inbound[to] = list;
                }

                list.Add(from);
            }
        }

        var backward = new Dictionary<string, int>(StringComparer.Ordinal) { [target] = 0 };
        var q = new Queue<string>();
        q.Enqueue(target);
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            if (!inbound.TryGetValue(u, out var preds)) continue;
            foreach (var p in preds)
            {
                if (backward.ContainsKey(p)) continue;
                backward[p] = backward[u] + 1;
                q.Enqueue(p);
            }
        }

        var targetDist = forward[target];
        var onPath = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in forward.Keys)
        {
            if (backward.TryGetValue(id, out var b)
                && forward[id] + b == targetDist)
            {
                onPath.Add(id);
            }
        }

        return onPath;
    }
}
