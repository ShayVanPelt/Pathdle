using Pathdle.Application.Models;

namespace Pathdle.Application.Services;

public static class GraphPathFinder
{
    /// <summary>
    /// Shortest undirected path from start to target using only the given edges.
    /// </summary>
    public static IReadOnlyList<string>? FindShortestPath(
        string start,
        string target,
        IEnumerable<PuzzleEdge> edges)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Add(string a, string b)
        {
            if (!adjacency.TryGetValue(a, out var list))
            {
                list = [];
                adjacency[a] = list;
            }

            if (!list.Contains(b, StringComparer.Ordinal))
            {
                list.Add(b);
            }
        }

        foreach (var e in edges)
        {
            Add(e.From, e.To);
            Add(e.To, e.From);
        }

        if (string.Equals(start, target, StringComparison.Ordinal))
        {
            return [start];
        }

        var queue = new Queue<string>();
        var previous = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [start] = null
        };

        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var next in neighbors)
            {
                if (previous.ContainsKey(next))
                {
                    continue;
                }

                previous[next] = current;
                if (string.Equals(next, target, StringComparison.Ordinal))
                {
                    return Reconstruct(previous, target);
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> Reconstruct(
        IReadOnlyDictionary<string, string?> previous,
        string target)
    {
        var path = new List<string>();
        string? current = target;
        while (current is not null)
        {
            path.Add(current);
            current = previous[current];
        }

        path.Reverse();
        return path;
    }
}
