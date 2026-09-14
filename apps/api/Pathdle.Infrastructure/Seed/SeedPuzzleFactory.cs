using Pathdle.Application.Models;

namespace Pathdle.Infrastructure.Seed;

/// <summary>
/// Hand-authored frozen puzzle for local MVP development (no Neo4j).
/// Optimal: Albert_Einstein → Physics → Mathematics → Nintendo (3 connections).
/// </summary>
public static class SeedPuzzleFactory
{
    public static readonly Guid PuzzleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static DailyPuzzle CreateForDate(DateOnly puzzleDate)
    {
        var nodes = new List<PuzzleNode>
        {
            N("Albert_Einstein", "Albert Einstein", 0.12, 0.48, NodeKind.Start),
            N("Physics", "Physics", 0.32, 0.28),
            N("Mathematics", "Mathematics", 0.55, 0.35),
            N("Nintendo", "Nintendo", 0.88, 0.52, NodeKind.Target),
            N("Germany", "Germany", 0.22, 0.72),
            N("Japan", "Japan", 0.62, 0.78),
            N("Relativity", "Relativity", 0.28, 0.12),
            N("Quantum_mechanics", "Quantum mechanics", 0.45, 0.15),
            N("Geometry", "Geometry", 0.58, 0.18),
            N("Science", "Science", 0.40, 0.48),
            N("Computer_science", "Computer science", 0.68, 0.42),
            N("Video_game", "Video game", 0.78, 0.30),
            N("Berlin", "Berlin", 0.10, 0.85),
            N("Kyoto", "Kyoto", 0.72, 0.90),
            N("Shigeru_Miyamoto", "Shigeru Miyamoto", 0.90, 0.70),
            N("Philosophy", "Philosophy", 0.48, 0.62),
        };

        var edges = new List<PuzzleEdge>
        {
            E("Albert_Einstein", "Physics"),
            E("Albert_Einstein", "Relativity"),
            E("Albert_Einstein", "Germany"),
            E("Albert_Einstein", "Science"),
            E("Physics", "Mathematics"),
            E("Physics", "Quantum_mechanics"),
            E("Physics", "Relativity"),
            E("Physics", "Science"),
            E("Mathematics", "Geometry"),
            E("Mathematics", "Computer_science"),
            E("Mathematics", "Nintendo"),
            E("Germany", "Berlin"),
            E("Germany", "Japan"),
            E("Japan", "Kyoto"),
            E("Japan", "Nintendo"),
            E("Relativity", "Physics"),
            E("Quantum_mechanics", "Physics"),
            E("Science", "Physics"),
            E("Science", "Mathematics"),
            E("Science", "Philosophy"),
            E("Computer_science", "Mathematics"),
            E("Computer_science", "Video_game"),
            E("Video_game", "Nintendo"),
            E("Video_game", "Shigeru_Miyamoto"),
            E("Shigeru_Miyamoto", "Nintendo"),
            E("Geometry", "Mathematics"),
            E("Berlin", "Germany"),
            E("Kyoto", "Japan"),
            E("Philosophy", "Science"),
            E("Nintendo", "Video_game"),
        };

        var optimalPath = new[] { "Albert_Einstein", "Physics", "Mathematics", "Nintendo" };

        return new DailyPuzzle
        {
            Id = PuzzleId,
            PuzzleDate = puzzleDate,
            GraphVersion = $"{puzzleDate:yyyy-MM-dd}.seed",
            StartArticleId = "Albert_Einstein",
            TargetArticleId = "Nintendo",
            Nodes = nodes,
            Edges = edges,
            OptimalPath = optimalPath,
            OptimalLength = optimalPath.Length - 1,
            GeneratorSeed = "seed-mvp-v1",
            CorpusVersion = "hand-authored-mvp",
            Difficulty = new { note = "hand-authored seed" },
            PublishedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static PuzzleNode N(string id, string title, double x, double y, NodeKind kind = NodeKind.Normal) =>
        new(id, title, x, y, kind);

    private static PuzzleEdge E(string from, string to) => new(from, to);
}
