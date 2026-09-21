namespace Pathdle.Application.Dtos;

public sealed record PublicPuzzleDto(
    Guid Id,
    DateOnly PuzzleDate,
    string GraphVersion,
    string StartArticleId,
    string TargetArticleId,
    IReadOnlyList<PuzzleNodeDto> Nodes);

public sealed record PuzzleNodeDto(
    string Id,
    string Title,
    double X,
    double Y,
    string Kind,
    string? Description = null);

public sealed record GameStateDto(
    Guid GameId,
    Guid PuzzleId,
    string Status,
    string StartArticleId,
    string TargetArticleId,
    IReadOnlyList<PuzzleNodeDto> Nodes,
    IReadOnlyList<EdgeDto> DiscoveredEdges,
    IReadOnlyList<EdgeDto> HintEdges,
    IReadOnlyList<string> PlayerPath,
    IReadOnlyList<string> RevealedArticleIds,
    int ConnectionCount,
    int Score,
    int HintsUsed,
    int HintsRemaining,
    bool ReachedTarget);

/// <summary>
/// Client edge. Group fields are set on discovered edges only — never on hints.
/// </summary>
public sealed record EdgeDto(
    string From,
    string To,
    string? GroupId = null,
    string? GroupLabel = null);

public sealed record StartGameRequest(Guid? PuzzleId);

public sealed record AttemptRequest(string FromId, string ToId);

public sealed record AttemptResponse(
    bool Success,
    string FromId,
    string ToId,
    string? GroupId,
    string? GroupLabel,
    int PointsAdded,
    int ConnectionCount,
    int Score,
    int HintsUsed,
    int HintsRemaining,
    IReadOnlyList<EdgeDto> DiscoveredEdges,
    IReadOnlyList<EdgeDto> HintEdges,
    IReadOnlyList<string> PlayerPath,
    IReadOnlyList<string> RevealedArticleIds,
    bool ReachedTarget,
    string Message);

public sealed record RevealRequest(string ArticleId);

public sealed record RevealResponse(
    string ArticleId,
    IReadOnlyList<string> NeighborIds,
    IReadOnlyList<EdgeDto> HintEdgesAdded,
    int PointsAdded,
    int ConnectionCount,
    int Score,
    int HintsUsed,
    int HintsRemaining,
    IReadOnlyList<EdgeDto> DiscoveredEdges,
    IReadOnlyList<EdgeDto> HintEdges,
    IReadOnlyList<string> PlayerPath,
    IReadOnlyList<string> RevealedArticleIds,
    bool ReachedTarget,
    string Message);

public sealed record CompleteGameResponse(
    Guid GameId,
    string Status,
    IReadOnlyList<string> PlayerPath,
    int PlayerConnectionCount,
    IReadOnlyList<string> OptimalPath,
    int OptimalLength,
    double Efficiency,
    int Score);
