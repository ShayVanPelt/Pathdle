using Pathdle.Application.Models;

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
    string Kind);

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
    bool ReachedTarget);

public sealed record EdgeDto(string From, string To);

public sealed record StartGameRequest(Guid? PuzzleId);

public sealed record AttemptRequest(string FromId, string ToId);

public sealed record AttemptResponse(
    bool Success,
    string FromId,
    string ToId,
    int PointsAdded,
    int ConnectionCount,
    int Score,
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
