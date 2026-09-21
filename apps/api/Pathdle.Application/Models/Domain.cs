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
    NodeKind Kind,
    string? Description = null);

/// <summary>
/// Undirected playable edge justified by a shared group.
/// Stored once with an arbitrary endpoint order; lookups use <see cref="DailyPuzzle.UndirectedEdgeKey"/>.
/// </summary>
public sealed record PuzzleEdge(
    string From,
    string To,
    string? GroupId = null,
    string? GroupLabel = null);

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
    /// <summary>Optional difficulty metrics jsonb payload (generator).</summary>
    public object? Difficulty { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    public HashSet<string> EdgeKeySet =>
        Edges.Select(e => UndirectedEdgeKey(e.From, e.To)).ToHashSet(StringComparer.Ordinal);

    public PuzzleEdge? FindEdge(string a, string b)
    {
        var key = UndirectedEdgeKey(a, b);
        return Edges.FirstOrDefault(e =>
            string.Equals(UndirectedEdgeKey(e.From, e.To), key, StringComparison.Ordinal));
    }

    /// <summary>Canonical undirected key (lexicographic endpoint order).</summary>
    public static string UndirectedEdgeKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
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
    /// Visual-only neighbor hints from reveals (no group labels). Not path until confirmed by drag.
    /// </summary>
    public List<PuzzleEdge> HintEdges { get; set; } = [];
    public List<AttemptedEdge> AttemptedEdges { get; set; } = [];
    public List<string> PlayerPath { get; set; } = [];
    /// <summary>Article ids for which neighbors have been revealed (paid once).</summary>
    public List<string> RevealedArticleIds { get; set; } = [];
    /// <summary>Number of paid hint reveals used (max 3 per game).</summary>
    public int HintsUsed { get; set; }
    public int Score { get; set; }
    public int ConnectionCount { get; set; }
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record AttemptedEdge(string From, string To, bool Success, DateTimeOffset AttemptedAt);
