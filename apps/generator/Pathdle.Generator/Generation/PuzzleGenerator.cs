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

        var rejectNullPath = 0;
        var rejectScore = 0;
        var rejectShortcut = 0;
        for (var attempt = 1; attempt <= opt.MaxAttempts; attempt++)
        {
            var path = PathSampler.Sample(graph, rng, opt);
            if (path is null)
            {
                rejectNullPath++;
                continue;
            }

            var board = TrapBuilder.Build(graph, path, rng, opt);
            var difficulty = DifficultyScorer.Score(board, opt);
            if (!DifficultyScorer.Accepts(difficulty, board, opt))
            {
                rejectScore++;
                continue;
            }

            // Verify shortest path length still holds on induced edges.
            var induced = ToAdjacency(board.Edges);
            var check = ShortestLength(induced, path.StartId, path.TargetId);
            if (check != path.OptimalLength)
            {
                rejectShortcut++;
                continue;
            }

            var nodes = LayoutBuilder.Layout(graph, board, rng);
            var edges = board.Edges
                .Select(e => new PuzzleEdge(e.From, e.To))
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
                + $"score={difficulty.Score} ({difficulty.Band})");

            return new GeneratedPuzzle(puzzle, difficulty);
        }

        throw new InvalidOperationException(
            $"Failed to generate a quality puzzle after {opt.MaxAttempts} attempts "
            + $"(nullPath={rejectNullPath}, score/size={rejectScore}, shortcut={rejectShortcut}). "
            + "Ingest a larger/denser corpus or relax GenerationOptions.");
    }

    private static Dictionary<string, List<string>> ToAdjacency(
        HashSet<(string From, string To)> edges)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (from, to) in edges)
        {
            if (!map.TryGetValue(from, out var list))
            {
                list = [];
                map[from] = list;
            }

            list.Add(to);
        }

        return map;
    }

    private static int? ShortestLength(
        Dictionary<string, List<string>> adj,
        string start,
        string target)
    {
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
