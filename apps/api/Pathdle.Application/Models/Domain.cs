namespace Pathdle.Application.Models;

public enum NodeKind
{
    Normal,
    Start,
    Target
}

public sealed record PuzzleNode(
    string Id,
    string Title,
    double X,
    double Y,
    NodeKind Kind);

public sealed record PuzzleEdge(string From, string To);

public sealed class DailyPuzzle
{
    public required Guid Id { get; init; }
    public required DateOnly PuzzleDate { get; init; }
    public required string GraphVersion { get; init; }
    public required string StartArticleId { get; init; }
    public required string TargetArticleId { get; init; }
    public required IReadOnlyList<PuzzleNode> Nodes { get; init; }
    public required IReadOnlyList<PuzzleEdge> Edges { get; init; }
    public required IReadOnlyList<string> OptimalPath { get; init; }
    public required int OptimalLength { get; init; }
    public required string GeneratorSeed { get; init; }
    public required string CorpusVersion { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public HashSet<string> EdgeKeySet =>
        Edges.Select(e => EdgeKey(e.From, e.To)).ToHashSet(StringComparer.Ordinal);

    public static string EdgeKey(string from, string to) => $"{from}->{to}";
}

public enum GameStatus
{
    Active,
    Completed,
    Abandoned
}

public sealed class PlayerGame
{
    public required Guid Id { get; init; }
    public required Guid DailyPuzzleId { get; init; }
    public required string PlayerKey { get; init; }
    public GameStatus Status { get; set; } = GameStatus.Active;
    public List<PuzzleEdge> DiscoveredEdges { get; set; } = [];
    /// <summary>
    /// Visual-only outbound hints from reveals. Not part of the player's path until confirmed by a successful drag.
    /// </summary>
    public List<PuzzleEdge> HintEdges { get; set; } = [];
    public List<AttemptedEdge> AttemptedEdges { get; set; } = [];
    public List<string> PlayerPath { get; set; } = [];
    /// <summary>Article ids for which outbound neighbors have been revealed.</summary>
    public List<string> RevealedArticleIds { get; set; } = [];
    public int Score { get; set; }
    public int ConnectionCount { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record AttemptedEdge(string From, string To, bool Success, DateTimeOffset AttemptedAt);
