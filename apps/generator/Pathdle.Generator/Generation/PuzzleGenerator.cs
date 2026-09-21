using Pathdle.Application.Models;
using Pathdle.Generator.Corpus;

namespace Pathdle.Generator.Generation;

internal sealed record GeneratedPuzzle(
    DailyPuzzle Puzzle,
    DifficultyMetrics Difficulty);

internal static class PuzzleGenerator
{
    public static GeneratedPuzzle Generate(
        CorpusGraph graph,
        DateOnly puzzleDate,
        string corpusVersion,
        string runNonce,
        GenerationOptions? options = null)
    {
        var opt = options ?? new GenerationOptions();
        var rng = SeededRng.From(puzzleDate.ToString("yyyy-MM-dd"), corpusVersion, runNonce);
        var generatorSeed = $"{puzzleDate:yyyy-MM-dd}:{corpusVersion}:{runNonce}";

        // Full co-membership graphs are too dense for length-4+ shortest paths.
        var sparse = GraphAlgorithms.SparsifyByRarity(graph, opt.SparseMaxDegree);
        Console.WriteLine(
            $"Sparse graph for generation: {sparse.EdgeCount} edges "
            + $"(from {graph.EdgeCount}, maxDegree={opt.SparseMaxDegree}).");

        var rejectNullPath = 0;
        var rejectScore = 0;
        var rejectShortcut = 0;
        for (var attempt = 1; attempt <= opt.MaxAttempts; attempt++)
        {
            var path = PathSampler.Sample(sparse, rng, opt);
            if (path is null)
            {
                rejectNullPath++;
                continue;
            }

            var board = TrapBuilder.Build(sparse, path, rng, opt);
            var difficulty = DifficultyScorer.Score(board, opt);
            if (!DifficultyScorer.Accepts(difficulty, board, opt))
            {
                rejectScore++;
                continue;
            }

            var check = ShortestLength(board.Edges, path.StartId, path.TargetId);
            if (check != path.OptimalLength)
            {
                rejectShortcut++;
                continue;
            }

            var nodes = LayoutBuilder.Layout(graph, board, rng);
            var edges = board.Edges
                .Select(e =>
                {
                    // Canonical endpoint order for storage
                    var (a, b) = string.CompareOrdinal(e.From, e.To) <= 0
                        ? (e.From, e.To)
                        : (e.To, e.From);
                    return new PuzzleEdge(a, b, e.GroupId, e.GroupLabel);
                })
                .OrderBy(e => e.From, StringComparer.Ordinal)
                .ThenBy(e => e.To, StringComparer.Ordinal)
                .ToList();

            var puzzle = new DailyPuzzle
            {
                Id = Guid.CreateVersion7(),
                PuzzleDate = puzzleDate,
                GraphVersion = $"{puzzleDate:yyyy-MM-dd}.{corpusVersion}",
                StartArticleId = path.StartId,
                TargetArticleId = path.TargetId,
                Nodes = nodes,
                Edges = edges,
                OptimalPath = path.OptimalPath,
                OptimalLength = path.OptimalLength,
                GeneratorSeed = generatorSeed,
                CorpusVersion = corpusVersion,
                Difficulty = difficulty.ToPayload(),
                PublishedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };

            Console.WriteLine(
                $"Accepted on attempt {attempt}: {path.StartId} → {path.TargetId} "
                + $"len={path.OptimalLength} nodes={nodes.Count} edges={edges.Count} "
                + $"score={difficulty.Score} ({difficulty.Band}) rarity={difficulty.AvgRarity:F1}");

            return new GeneratedPuzzle(puzzle, difficulty);
        }

        throw new InvalidOperationException(
            $"Failed to generate a quality puzzle after {opt.MaxAttempts} attempts "
            + $"(nullPath={rejectNullPath}, score/size={rejectScore}, shortcut={rejectShortcut}). "
            + "Ingest a denser Wikidata corpus (`ingest-corpus`) or try `--demo`.");
    }

    private static int? ShortestLength(
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
                if (dist.ContainsKey(v)) continue;
                dist[v] = dist[u] + 1;
                q.Enqueue(v);
            }
        }

        return null;
    }
}
