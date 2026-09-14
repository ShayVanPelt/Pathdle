using Pathdle.Application.Models;
using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

internal static class LayoutBuilder
{
    public static IReadOnlyList<PuzzleNode> Layout(
        CorpusGraph graph,
        BuiltBoard board,
        Random rng)
    {
        var path = board.Path.OptimalPath;
        var positions = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);

        for (var i = 0; i < path.Count; i++)
        {
            var t = path.Count == 1 ? 0.5 : (double)i / (path.Count - 1);
            var x = 0.08 + t * 0.84;
            var y = 0.42 + Math.Sin(t * Math.PI) * 0.08 + (rng.NextDouble() - 0.5) * 0.04;
            positions[path[i]] = (Clamp(x), Clamp(y));
        }

        // Place traps near a random path parent they connect from, else orbit mid path.
        foreach (var id in board.NodeIds)
        {
            if (positions.ContainsKey(id)) continue;

            string? parent = null;
            foreach (var (from, to) in board.Edges)
            {
                if (to == id && positions.ContainsKey(from))
                {
                    parent = from;
                    break;
                }
            }

            parent ??= path[Math.Clamp(path.Count / 2, 0, path.Count - 1)];
            var (px, py) = positions[parent];
            var angle = rng.NextDouble() * Math.PI * 2;
            var radius = 0.06 + rng.NextDouble() * 0.12;
            positions[id] = (
                Clamp(px + Math.Cos(angle) * radius),
                Clamp(py + Math.Sin(angle) * radius * 0.85));
        }

        // Mild separation.
        for (var iter = 0; iter < 40; iter++)
        {
            var ids = positions.Keys.ToList();
            for (var i = 0; i < ids.Count; i++)
            {
                for (var j = i + 1; j < ids.Count; j++)
                {
                    var a = positions[ids[i]];
                    var b = positions[ids[j]];
                    var dx = a.X - b.X;
                    var dy = a.Y - b.Y;
                    var d2 = dx * dx + dy * dy;
                    if (d2 is < 1e-8 or > 0.012) continue;
                    var d = Math.Sqrt(d2);
                    var push = (0.11 - d) * 0.08;
                    var ux = dx / d * push;
                    var uy = dy / d * push;
                    if (!path.Contains(ids[i]))
                    {
                        positions[ids[i]] = (Clamp(a.X + ux), Clamp(a.Y + uy));
                    }

                    if (!path.Contains(ids[j]))
                    {
                        positions[ids[j]] = (Clamp(b.X - ux), Clamp(b.Y - uy));
                    }
                }
            }
        }

        return board.NodeIds
            .Select(id =>
            {
                var art = graph.Articles[id];
                var (x, y) = positions[id];
                var kind = id == board.Path.StartId
                    ? NodeKind.Start
                    : id == board.Path.TargetId
                        ? NodeKind.Target
                        : NodeKind.Normal;
                return new PuzzleNode(id, art.Title, x, y, kind);
            })
            .OrderBy(n => n.Kind == NodeKind.Start ? 0 : n.Kind == NodeKind.Target ? 2 : 1)
            .ThenBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double Clamp(double v) => Math.Clamp(v, 0.03, 0.97);
}
