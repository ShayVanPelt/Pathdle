using Pathdle.Application.Abstractions;
using Pathdle.Application.Dtos;
using Pathdle.Application.Mapping;
using Pathdle.Application.Models;

namespace Pathdle.Application.Services;

public sealed class GameService(
    IPuzzleRepository puzzleRepository,
    IGameRepository gameRepository,
    IClock clock)
{
    public async Task<PublicPuzzleDto?> GetTodayPuzzleAsync(CancellationToken cancellationToken = default)
    {
        var puzzle = await puzzleRepository.GetByDateAsync(clock.UtcToday, cancellationToken);
        return puzzle is null ? null : PuzzleMapping.ToPublicDto(puzzle);
    }

    public async Task<PublicPuzzleDto?> GetPuzzleByDateAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var puzzle = await puzzleRepository.GetByDateAsync(date, cancellationToken);
        return puzzle is null ? null : PuzzleMapping.ToPublicDto(puzzle);
    }

    public async Task<GameStateDto> StartGameAsync(
        string playerKey,
        Guid? puzzleId,
        CancellationToken cancellationToken = default)
    {
        ValidatePlayerKey(playerKey);

        var puzzle = puzzleId is Guid id
            ? await puzzleRepository.GetByIdAsync(id, cancellationToken)
            : await puzzleRepository.GetByDateAsync(clock.UtcToday, cancellationToken);

        if (puzzle is null)
        {
            throw new NotFoundException("Puzzle not found.");
        }

        var existing = await gameRepository.GetByPuzzleAndPlayerAsync(puzzle.Id, playerKey, cancellationToken);
        if (existing is not null)
        {
            return PuzzleMapping.ToGameStateDto(existing, puzzle);
        }

        var game = new PlayerGame
        {
            Id = Guid.NewGuid(),
            DailyPuzzleId = puzzle.Id,
            PlayerKey = playerKey,
            PlayerPath = [puzzle.StartArticleId],
            StartedAt = clock.UtcNow
        };

        await gameRepository.AddAsync(game, cancellationToken);
        return PuzzleMapping.ToGameStateDto(game, puzzle);
    }

    public async Task<GameStateDto> GetGameAsync(
        Guid gameId,
        string playerKey,
        CancellationToken cancellationToken = default)
    {
        ValidatePlayerKey(playerKey);
        var (game, puzzle) = await LoadOwnedGameAsync(gameId, playerKey, cancellationToken);
        return PuzzleMapping.ToGameStateDto(game, puzzle);
    }

    public async Task<AttemptResponse> AttemptConnectionAsync(
        Guid gameId,
        string playerKey,
        AttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePlayerKey(playerKey);

        if (string.IsNullOrWhiteSpace(request.FromId) || string.IsNullOrWhiteSpace(request.ToId))
        {
            throw new ValidationException("fromId and toId are required.");
        }

        if (string.Equals(request.FromId, request.ToId, StringComparison.Ordinal))
        {
            throw new ValidationException("Cannot connect an article to itself.");
        }

        var (game, puzzle) = await LoadOwnedGameAsync(gameId, playerKey, cancellationToken);
        if (game.Status != GameStatus.Active)
        {
            throw new ConflictException("Game is not active.");
        }

        EnsureNodeExists(puzzle, request.FromId);
        EnsureNodeExists(puzzle, request.ToId);
        EnsureChartedSource(game, puzzle, request.FromId);

        if (IsDiscovered(game, request.FromId, request.ToId))
        {
            var existing = FindDiscovered(game, request.FromId, request.ToId);
            return BuildAttemptResponse(
                game,
                puzzle,
                success: true,
                request.FromId,
                request.ToId,
                existing?.GroupId,
                existing?.GroupLabel,
                pointsAdded: 0,
                "Link already on your chart.");
        }

        var boardEdge = puzzle.FindEdge(request.FromId, request.ToId);
        var success = boardEdge is not null;
        game.AttemptedEdges.Add(new AttemptedEdge(request.FromId, request.ToId, success, clock.UtcNow));

        if (!success)
        {
            game.Score += ScoringRules.FailedLinkCost;
            await gameRepository.UpdateAsync(game, cancellationToken);
            return BuildAttemptResponse(
                game,
                puzzle,
                success: false,
                request.FromId,
                request.ToId,
                null,
                null,
                ScoringRules.FailedLinkCost,
                $"No shared connection. +{ScoringRules.FailedLinkCost} points.");
        }

        var confirmed = new PuzzleEdge(
            request.FromId,
            request.ToId,
            boardEdge!.GroupId,
            boardEdge.GroupLabel);
        game.DiscoveredEdges.Add(confirmed);
        game.ConnectionCount += 1;
        game.Score += ScoringRules.SuccessfulLinkCost;
        // Clear dashed hints from the node you just linked from (confirmed path replaces the fan).
        game.HintEdges.RemoveAll(h =>
            string.Equals(h.From, request.FromId, StringComparison.Ordinal));
        AppendVisit(game, request.ToId);

        await gameRepository.UpdateAsync(game, cancellationToken);

        return BuildAttemptResponse(
            game,
            puzzle,
            success: true,
            request.FromId,
            request.ToId,
            confirmed.GroupId,
            confirmed.GroupLabel,
            ScoringRules.SuccessfulLinkCost,
            string.IsNullOrEmpty(confirmed.GroupLabel)
                ? $"Link found. +{ScoringRules.SuccessfulLinkCost} points."
                : $"Link found — {confirmed.GroupLabel}. +{ScoringRules.SuccessfulLinkCost} points.");
    }

    public async Task<RevealResponse> RevealOutboundAsync(
        Guid gameId,
        string playerKey,
        RevealRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePlayerKey(playerKey);

        if (string.IsNullOrWhiteSpace(request.ArticleId))
        {
            throw new ValidationException("articleId is required.");
        }

        var (game, puzzle) = await LoadOwnedGameAsync(gameId, playerKey, cancellationToken);
        if (game.Status != GameStatus.Active)
        {
            throw new ConflictException("Game is not active.");
        }

        EnsureNodeExists(puzzle, request.ArticleId);
        EnsureChartedSource(game, puzzle, request.ArticleId);

        var alreadyRevealed = game.RevealedArticleIds.Any(id =>
            string.Equals(id, request.ArticleId, StringComparison.Ordinal));

        var neighborIds = NeighborsOf(puzzle, request.ArticleId);

        if (alreadyRevealed)
        {
            // Free re-show: restore dashed hints that were cleared after a confirm.
            var hintsAdded = EnsureHintEdges(game, request.ArticleId, neighborIds);
            if (hintsAdded.Count > 0)
            {
                await gameRepository.UpdateAsync(game, cancellationToken);
            }

            return BuildRevealResponse(
                game,
                puzzle,
                request.ArticleId,
                neighborIds,
                hintsAdded,
                pointsAdded: 0,
                "Connections already revealed — showing hints again (free).");
        }

        if (game.HintsUsed >= ScoringRules.MaxHintsPerGame)
        {
            throw new ConflictException(
                $"No hints left ({ScoringRules.MaxHintsPerGame} per puzzle).");
        }

        var paidHints = EnsureHintEdges(game, request.ArticleId, neighborIds);
        game.RevealedArticleIds.Add(request.ArticleId);
        game.HintsUsed += 1;
        game.Score += ScoringRules.RevealOutboundCost;

        await gameRepository.UpdateAsync(game, cancellationToken);

        return BuildRevealResponse(
            game,
            puzzle,
            request.ArticleId,
            neighborIds,
            paidHints,
            ScoringRules.RevealOutboundCost,
            neighborIds.Count == 0
                ? $"No connections on this board. +{ScoringRules.RevealOutboundCost} points."
                : $"Hinted {neighborIds.Count} connection(s) — drag to confirm. +{ScoringRules.RevealOutboundCost} points.");
    }

    public async Task<CompleteGameResponse> CompleteGameAsync(
        Guid gameId,
        string playerKey,
        CancellationToken cancellationToken = default)
    {
        ValidatePlayerKey(playerKey);
        var (game, puzzle) = await LoadOwnedGameAsync(gameId, playerKey, cancellationToken);

        if (game.Status == GameStatus.Completed)
        {
            return ToCompleteResponse(game, puzzle);
        }

        if (game.Status != GameStatus.Active)
        {
            throw new ConflictException("Game is not active.");
        }

        var path = GraphPathFinder.FindShortestPath(
            puzzle.StartArticleId,
            puzzle.TargetArticleId,
            game.DiscoveredEdges);

        if (path is null)
        {
            throw new ConflictException("Target has not been reached yet.");
        }

        game.PlayerPath = path.ToList();
        game.Status = GameStatus.Completed;
        game.CompletedAt = clock.UtcNow;
        await gameRepository.UpdateAsync(game, cancellationToken);

        return ToCompleteResponse(game, puzzle);
    }

    private static List<string> NeighborsOf(DailyPuzzle puzzle, string articleId)
    {
        var neighbors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in puzzle.Edges)
        {
            if (string.Equals(e.From, articleId, StringComparison.Ordinal))
            {
                neighbors.Add(e.To);
            }
            else if (string.Equals(e.To, articleId, StringComparison.Ordinal))
            {
                neighbors.Add(e.From);
            }
        }

        return neighbors.ToList();
    }

    /// <summary>
    /// Adds unlabeled hint edges from articleId to each undiscovered neighbor. Returns newly added hints.
    /// </summary>
    private static List<PuzzleEdge> EnsureHintEdges(
        PlayerGame game,
        string articleId,
        IReadOnlyList<string> neighborIds)
    {
        var hintsAdded = new List<PuzzleEdge>();
        foreach (var neighbor in neighborIds)
        {
            if (IsDiscovered(game, articleId, neighbor))
            {
                continue;
            }

            var alreadyHinted = game.HintEdges.Any(h =>
                string.Equals(h.From, articleId, StringComparison.Ordinal)
                && string.Equals(h.To, neighbor, StringComparison.Ordinal));
            if (alreadyHinted)
            {
                continue;
            }

            // Hints never carry group labels.
            var hint = new PuzzleEdge(articleId, neighbor);
            game.HintEdges.Add(hint);
            hintsAdded.Add(hint);
        }

        return hintsAdded;
    }

    private static bool IsDiscovered(PlayerGame game, string a, string b)
    {
        var key = DailyPuzzle.UndirectedEdgeKey(a, b);
        return game.DiscoveredEdges.Any(e =>
            string.Equals(DailyPuzzle.UndirectedEdgeKey(e.From, e.To), key, StringComparison.Ordinal));
    }

    private static PuzzleEdge? FindDiscovered(PlayerGame game, string a, string b)
    {
        var key = DailyPuzzle.UndirectedEdgeKey(a, b);
        return game.DiscoveredEdges.FirstOrDefault(e =>
            string.Equals(DailyPuzzle.UndirectedEdgeKey(e.From, e.To), key, StringComparison.Ordinal));
    }

    private static void AppendVisit(PlayerGame game, string articleId)
    {
        if (game.PlayerPath.Any(id => string.Equals(id, articleId, StringComparison.Ordinal)))
        {
            return;
        }

        game.PlayerPath.Add(articleId);
    }

    private static AttemptResponse BuildAttemptResponse(
        PlayerGame game,
        DailyPuzzle puzzle,
        bool success,
        string fromId,
        string toId,
        string? groupId,
        string? groupLabel,
        int pointsAdded,
        string message) =>
        new(
            success,
            fromId,
            toId,
            groupId,
            groupLabel,
            pointsAdded,
            game.ConnectionCount,
            game.Score,
            game.HintsUsed,
            HintsRemaining(game),
            game.DiscoveredEdges.Select(PuzzleMapping.ToDiscoveredEdgeDto).ToList(),
            game.HintEdges.Select(PuzzleMapping.ToHintEdgeDto).ToList(),
            game.PlayerPath,
            game.RevealedArticleIds,
            HasReachedTarget(game, puzzle),
            message);

    private static RevealResponse BuildRevealResponse(
        PlayerGame game,
        DailyPuzzle puzzle,
        string articleId,
        IReadOnlyList<string> neighborIds,
        IReadOnlyList<PuzzleEdge> hintsAdded,
        int pointsAdded,
        string message) =>
        new(
            articleId,
            neighborIds,
            hintsAdded.Select(PuzzleMapping.ToHintEdgeDto).ToList(),
            pointsAdded,
            game.ConnectionCount,
            game.Score,
            game.HintsUsed,
            HintsRemaining(game),
            game.DiscoveredEdges.Select(PuzzleMapping.ToDiscoveredEdgeDto).ToList(),
            game.HintEdges.Select(PuzzleMapping.ToHintEdgeDto).ToList(),
            game.PlayerPath,
            game.RevealedArticleIds,
            HasReachedTarget(game, puzzle),
            message);

    private static int HintsRemaining(PlayerGame game) =>
        Math.Max(0, ScoringRules.MaxHintsPerGame - game.HintsUsed);

    private async Task<(PlayerGame Game, DailyPuzzle Puzzle)> LoadOwnedGameAsync(
        Guid gameId,
        string playerKey,
        CancellationToken cancellationToken)
    {
        var game = await gameRepository.GetByIdAsync(gameId, cancellationToken)
            ?? throw new NotFoundException("Game not found.");

        if (!string.Equals(game.PlayerKey, playerKey, StringComparison.Ordinal))
        {
            throw new ForbiddenException("Game does not belong to this player.");
        }

        var puzzle = await puzzleRepository.GetByIdAsync(game.DailyPuzzleId, cancellationToken)
            ?? throw new NotFoundException("Puzzle not found for game.");

        return (game, puzzle);
    }

    private static CompleteGameResponse ToCompleteResponse(PlayerGame game, DailyPuzzle puzzle)
    {
        var playerConnections = Math.Max(0, game.PlayerPath.Count - 1);
        var efficiency = playerConnections == 0
            ? 0
            : (double)puzzle.OptimalLength / playerConnections;

        return new CompleteGameResponse(
            game.Id,
            game.Status.ToString().ToLowerInvariant(),
            game.PlayerPath,
            playerConnections,
            puzzle.OptimalPath,
            puzzle.OptimalLength,
            Math.Round(efficiency, 3),
            game.Score);
    }

    private static bool HasReachedTarget(PlayerGame game, DailyPuzzle puzzle) =>
        GraphPathFinder.FindShortestPath(
            puzzle.StartArticleId,
            puzzle.TargetArticleId,
            game.DiscoveredEdges) is not null;

    private static void EnsureChartedSource(
        PlayerGame game,
        DailyPuzzle puzzle,
        string articleId)
    {
        var isReachable = GraphPathFinder.FindShortestPath(
            puzzle.StartArticleId,
            articleId,
            game.DiscoveredEdges) is not null;

        if (!isReachable)
        {
            throw new ConflictException(
                "Chart a path to this article before exploring from it.");
        }
    }

    private static void EnsureNodeExists(DailyPuzzle puzzle, string articleId)
    {
        if (!puzzle.Nodes.Any(n => string.Equals(n.Id, articleId, StringComparison.Ordinal)))
        {
            throw new ValidationException($"Unknown article on this board: {articleId}");
        }
    }

    private static void ValidatePlayerKey(string playerKey)
    {
        if (string.IsNullOrWhiteSpace(playerKey))
        {
            throw new ValidationException("X-Player-Key header is required.");
        }

        if (playerKey.Length > 128)
        {
            throw new ValidationException("X-Player-Key is too long.");
        }
    }
}

public class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string message) : AppException(message, StatusCodes.NotFound);
public sealed class ValidationException(string message) : AppException(message, StatusCodes.BadRequest);
public sealed class ConflictException(string message) : AppException(message, StatusCodes.Conflict);
public sealed class ForbiddenException(string message) : AppException(message, StatusCodes.Forbidden);

public static class StatusCodes
{
    public const int BadRequest = 400;
    public const int Forbidden = 403;
    public const int NotFound = 404;
    public const int Conflict = 409;
}
