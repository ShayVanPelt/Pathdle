using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

internal static class TrapBuilder
{
    public static BuiltBoard Build(
        CorpusGraph graph,
        PathCandidate path,
        Random rng,
        GenerationOptions opt)
    {
        var nodes = new HashSet<string>(path.OptimalPath, StringComparer.Ordinal);
        var edges = new HashSet<(string From, string To)>();
        var traps = new HashSet<string>(StringComparer.Ordinal);
        var trapEdges = 0;

        // Optimal path edges.
        for (var i = 0; i < path.OptimalPath.Count - 1; i++)
        {
            edges.Add((path.OptimalPath[i], path.OptimalPath[i + 1]));
        }

        var shortestSet = GraphAlgorithms.NodesOnAnyShortestPath(
            graph,
            path.StartId,
            path.TargetId);

        var midNodes = path.OptimalPath.Skip(1).Take(path.OptimalPath.Count - 2).ToList();
        foreach (var mid in midNodes)
        {
            if (nodes.Count >= opt.MaxBoardNodes) break;

            var candidates = graph.Neighbors(mid)
                .Where(n => !shortestSet.Contains(n) && !nodes.Contains(n))
                .Select(n => graph.Articles[n])
                .OrderByDescending(a => a.DegreeIn)
                .ThenByDescending(a => a.Popularity)
                .Take(16)
                .Select(a => a.Id)
                .OrderBy(_ => rng.Next())
                .Take(opt.TrapsPerMidNode + 2)
                .ToList();

            foreach (var trapRoot in candidates)
            {
                if (nodes.Count >= opt.MaxBoardNodes) break;
                if (!TryAddNode(graph, nodes, edges, path, trapRoot)) continue;
                traps.Add(trapRoot);
                trapEdges++;
                ExpandTrap(graph, trapRoot, nodes, edges, traps, ref trapEdges, path, opt, rng);
            }
        }

        // Light thematic distractors from START / TARGET neighborhoods.
        foreach (var anchor in new[] { path.StartId, path.TargetId })
        {
            if (nodes.Count >= opt.MaxBoardNodes) break;
            var extras = graph.Neighbors(anchor)
                .Where(n => !nodes.Contains(n) && !shortestSet.Contains(n))
                .OrderByDescending(n => graph.Articles[n].DegreeIn)
                .Take(6)
                .ToList();
            foreach (var n in extras)
            {
                if (nodes.Count >= opt.MaxBoardNodes) break;
                if (!TryAddNode(graph, nodes, edges, path, n)) continue;
                traps.Add(n);
                trapEdges++;
            }
        }

        // Grow toward min board size without creating START→TARGET shortcuts.
        var growFrom = path.OptimalPath.ToList();
        var gi = 0;
        while (nodes.Count < opt.MinBoardNodes && gi < 800)
        {
            gi++;
            var src = growFrom[rng.Next(growFrom.Count)];
            var nbrs = graph.Neighbors(src).Where(n => !nodes.Contains(n)).ToList();
            if (nbrs.Count == 0) continue;
            var pick = nbrs[rng.Next(nbrs.Count)];
            if (!TryAddNode(graph, nodes, edges, path, pick)) continue;
            traps.Add(pick);
            trapEdges++;
            growFrom.Add(pick);
        }

        return new BuiltBoard(path, nodes, edges, traps, trapEdges);
    }

    private static void ExpandTrap(
        CorpusGraph graph,
        string trapRoot,
        HashSet<string> nodes,
        HashSet<(string From, string To)> edges,
        HashSet<string> traps,
        ref int trapEdges,
        PathCandidate path,
        GenerationOptions opt,
        Random rng)
    {
        var queue = new Queue<(string Id, int Depth)>();
        queue.Enqueue((trapRoot, 0));

        while (queue.Count > 0 && nodes.Count < opt.MaxBoardNodes)
        {
            var (id, depth) = queue.Dequeue();
            if (depth >= opt.TrapDepth) continue;

            var next = graph.Neighbors(id)
                .Where(n => !nodes.Contains(n))
                .OrderByDescending(n => graph.Articles[n].DegreeIn)
                .Take(4)
                .OrderBy(_ => rng.Next())
                .Take(2)
                .ToList();

            foreach (var n in next)
            {
                if (nodes.Count >= opt.MaxBoardNodes) break;
                if (!TryAddNode(graph, nodes, edges, path, n)) continue;
                traps.Add(n);
                trapEdges++;
                queue.Enqueue((n, depth + 1));
            }
        }
    }

    /// <summary>
    /// Add a node plus all induced corpus edges to the current board, but only if
    /// the shortest START→TARGET length stays equal to the sampled optimal length.
    /// </summary>
    private static bool TryAddNode(
        CorpusGraph graph,
        HashSet<string> nodes,
        HashSet<(string From, string To)> edges,
        PathCandidate path,
        string candidate)
    {
        if (nodes.Contains(candidate)) return false;

        var proposedEdges = new List<(string From, string To)>();
        foreach (var from in nodes)
        {
            foreach (var to in graph.Neighbors(from))
            {
                if (to == candidate) proposedEdges.Add((from, to));
            }
        }

        foreach (var to in graph.Neighbors(candidate))
        {
            if (nodes.Contains(to)) proposedEdges.Add((candidate, to));
        }

        // Tentatively apply, then verify shortest path length.
        nodes.Add(candidate);
        foreach (var e in proposedEdges) edges.Add(e);

        var len = ShortestOnBoard(edges, path.StartId, path.TargetId);
        if (len == path.OptimalLength) return true;

        // Rollback.
        nodes.Remove(candidate);
        foreach (var e in proposedEdges) edges.Remove(e);
        return false;
    }

    private static int? ShortestOnBoard(
        HashSet<(string From, string To)> edges,
        string start,
        string target)
    {
        var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (from, to) in edges)
        {
            if (!adj.TryGetValue(from, out var list))
            {
                list = [];
                adj[from] = list;
            }

            list.Add(to);
        }

        var dist = new Dictionary<string, int>(StringComparer.Ordinal) { [start] = 0 };
        var q = new Queue<string>();
        q.Enqueue(start);
        while (q.Count > 0)
        {
            var u = q.Dequeue();
            if (u == target) return dist[u];
            if (!adj.TryGetValue(u, out var outs)) continue;
            foreach (var v in outs)
            {
                if (dist.ContainsKey(v)) continue;
                dist[v] = dist[u] + 1;
                q.Enqueue(v);
            }
        }

        return null;
    }
}
