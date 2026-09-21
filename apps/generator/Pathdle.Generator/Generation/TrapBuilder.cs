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
        var edges = new HashSet<BoardEdge>(BoardEdgeComparer.Instance);
        var traps = new HashSet<string>(StringComparer.Ordinal);
        var trapEdges = 0;

        // Optimal path edges
        for (var i = 0; i < path.OptimalPath.Count - 1; i++)
        {
            var a = path.OptimalPath[i];
            var b = path.OptimalPath[i + 1];
            var e = graph.FindEdge(a, b)
                ?? throw new InvalidOperationException($"Missing corpus edge {a}-{b}");
            edges.Add(ToBoardEdge(e));
        }

        var midNodes = path.OptimalPath.Skip(1).Take(path.OptimalPath.Count - 2).ToList();
        foreach (var mid in midNodes)
        {
            var candidates = graph.EdgesFrom(mid)
                .Where(e => !nodes.Contains(e.To))
                .OrderByDescending(e =>
                    graph.Articles.TryGetValue(e.To, out var a) ? a.Popularity : 0)
                .ThenByDescending(e => e.Rarity)
                .ThenBy(_ => rng.Next())
                .Take(opt.TrapsPerMidNode * 4)
                .ToList();

            var added = 0;
            var obscureAdded = 0;
            foreach (var edge in candidates)
            {
                if (added >= opt.TrapsPerMidNode) break;
                if (!graph.Articles.TryGetValue(edge.To, out var cand)) continue;
                var obscure = cand.Popularity < opt.MinFameBoard && !cand.IsSeed;
                if (obscure && obscureAdded >= 1) continue;

                if (!TryAddNode(graph, nodes, edges, edge.To, path, opt, out var addedEdges))
                {
                    continue;
                }

                traps.Add(edge.To);
                trapEdges += addedEdges;
                added++;
                if (obscure) obscureAdded++;

                ExpandTrap(graph, nodes, edges, traps, edge.To, path, rng, opt, ref trapEdges);
                if (nodes.Count >= opt.MaxBoardNodes) break;
            }

            if (nodes.Count >= opt.MaxBoardNodes) break;
        }

        // Light distractors from START / TARGET neighborhoods
        foreach (var anchor in new[] { path.StartId, path.TargetId })
        {
            if (nodes.Count >= opt.MinBoardNodes) break;
            foreach (var edge in graph.EdgesFrom(anchor)
                         .OrderByDescending(e =>
                             graph.Articles.TryGetValue(e.To, out var a) ? a.Popularity : 0)
                         .ThenByDescending(e => e.Rarity)
                         .Take(8))
            {
                if (nodes.Count >= opt.MaxBoardNodes) break;
                if (nodes.Contains(edge.To)) continue;
                if (!TryAddNode(graph, nodes, edges, edge.To, path, opt, out var addedEdges)) continue;
                traps.Add(edge.To);
                trapEdges += addedEdges;
            }
        }

        // Fill until min board size — prefer famous entities
        var filler = graph.Articles.Values
            .Where(a => !nodes.Contains(a.Id))
            .Where(a => a.Popularity >= opt.MinFameBoard || a.IsSeed)
            .OrderByDescending(a => a.Popularity)
            .ThenBy(_ => rng.Next())
            .Select(a => a.Id)
            .ToList();
        foreach (var id in filler)
        {
            if (nodes.Count >= opt.MinBoardNodes) break;
            if (nodes.Count >= opt.MaxBoardNodes) break;
            var bridge = graph.EdgesFrom(id).FirstOrDefault(e => nodes.Contains(e.To));
            if (bridge is null) continue;
            if (!TryAddNode(graph, nodes, edges, id, path, opt, out var addedEdges)) continue;
            traps.Add(id);
            trapEdges += addedEdges;
        }

        return new BuiltBoard(path, nodes, edges, traps, trapEdges);
    }

    private static void ExpandTrap(
        CorpusGraph graph,
        HashSet<string> nodes,
        HashSet<BoardEdge> edges,
        HashSet<string> traps,
        string root,
        PathCandidate path,
        Random rng,
        GenerationOptions opt,
        ref int trapEdges)
    {
        if (opt.TrapDepth <= 0) return;
        var frontier = new List<string> { root };
        for (var d = 0; d < opt.TrapDepth; d++)
        {
            var next = new List<string>();
            foreach (var u in frontier)
            {
                foreach (var edge in graph.EdgesFrom(u)
                             .OrderByDescending(e =>
                                 graph.Articles.TryGetValue(e.To, out var a) ? a.Popularity : 0)
                             .ThenByDescending(e => e.Rarity)
                             .Take(4))
                {
                    if (nodes.Contains(edge.To)) continue;
                    if (!graph.Articles.TryGetValue(edge.To, out var cand)) continue;
                    if (cand.Popularity < opt.MinFameBoard && !cand.IsSeed) continue;
                    if (!TryAddNode(graph, nodes, edges, edge.To, path, opt, out var added)) continue;
                    traps.Add(edge.To);
                    trapEdges += added;
                    next.Add(edge.To);
                    if (nodes.Count >= 40) return;
                }
            }

            frontier = next.OrderBy(_ => rng.Next()).Take(6).ToList();
            if (frontier.Count == 0) break;
        }
    }

    private static bool TryAddNode(
        CorpusGraph graph,
        HashSet<string> nodes,
        HashSet<BoardEdge> edges,
        string newId,
        PathCandidate path,
        GenerationOptions opt,
        out int addedEdges)
    {
        addedEdges = 0;
        if (!graph.Articles.TryGetValue(newId, out var art)
            || !TitleQuality.HasPlayableTitle(art.Title, newId))
        {
            return false;
        }

        var existingTitles = nodes
            .Select(id => graph.Articles.TryGetValue(id, out var a) ? a.Title : null)
            .Where(t => t is not null)
            .Cast<string>();
        if (TitleQuality.ConflictsWithBoard(art.Title, existingTitles, maxPerFamily: 2))
        {
            return false;
        }

        var snapshotNodes = nodes.ToHashSet(StringComparer.Ordinal);
        var snapshotEdges = edges.ToHashSet(BoardEdgeComparer.Instance);

        nodes.Add(newId);
        foreach (var edge in graph.EdgesFrom(newId))
        {
            if (!nodes.Contains(edge.To)) continue;
            edges.Add(ToBoardEdge(edge));
            addedEdges++;
        }

        var length = ShortestOnBoard(nodes, edges, path.StartId, path.TargetId);
        if (length is null || length < path.OptimalLength)
        {
            nodes.Clear();
            foreach (var n in snapshotNodes) nodes.Add(n);
            edges.Clear();
            foreach (var e in snapshotEdges) edges.Add(e);
            addedEdges = 0;
            return false;
        }

        return true;
    }

    private static int? ShortestOnBoard(
        HashSet<string> nodes,
        HashSet<BoardEdge> edges,
        string start,
        string target)
    {
        var adj = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (!adj.TryGetValue(e.From, out var fl)) { fl = []; adj[e.From] = fl; }
            if (!adj.TryGetValue(e.To, out var tl)) { tl = []; adj[e.To] = tl; }
            fl.Add(e.To);
            tl.Add(e.From);
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
                if (!nodes.Contains(v) || dist.ContainsKey(v)) continue;
                dist[v] = dist[u] + 1;
                q.Enqueue(v);
            }
        }

        return null;
    }

    private static BoardEdge ToBoardEdge(CorpusEdge e) =>
        new(e.From, e.To, e.GroupId, e.GroupLabel, e.Rarity);
}

internal sealed class BoardEdgeComparer : IEqualityComparer<BoardEdge>
{
    public static readonly BoardEdgeComparer Instance = new();

    public bool Equals(BoardEdge? x, BoardEdge? y)
    {
        if (x is null || y is null) return false;
        return GraphAlgorithms.UndirectedKey(x.From, x.To)
            == GraphAlgorithms.UndirectedKey(y.From, y.To);
    }

    public int GetHashCode(BoardEdge obj) =>
        StringComparer.Ordinal.GetHashCode(GraphAlgorithms.UndirectedKey(obj.From, obj.To));
}
