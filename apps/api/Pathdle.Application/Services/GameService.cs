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

        var alreadyDiscovered = game.DiscoveredEdges.Any(e =>
            string.Equals(e.From, request.FromId, StringComparison.Ordinal)
            && string.Equals(e.To, request.ToId, StringComparison.Ordinal));

        if (alreadyDiscovered)
        {
            return BuildAttemptResponse(
                game,
                puzzle,
                success: true,
                request.FromId,
                request.ToId,
                pointsAdded: 0,
                "Link already on your chart.");
        }

        var success = puzzle.EdgeKeySet.Contains(DailyPuzzle.EdgeKey(request.FromId, request.ToId));
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
                ScoringRules.FailedLinkCost,
                $"No link that way. +{ScoringRules.FailedLinkCost} points.");
        }

        game.DiscoveredEdges.Add(new PuzzleEdge(request.FromId, request.ToId));
        game.ConnectionCount += 1;
        game.Score += ScoringRules.SuccessfulLinkCost;
        // Clear outbound hint lines from the node you just linked from (confirmed path replaces the blue fan).
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
            ScoringRules.SuccessfulLinkCost,
            $"Link found. +{ScoringRules.SuccessfulLinkCost} points.");
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

        var alreadyRevealed = game.RevealedArticleIds.Any(id =>
            string.Equals(id, request.ArticleId, StringComparison.Ordinal));

        var outbound = puzzle.Edges
            .Where(e => string.Equals(e.From, request.ArticleId, StringComparison.Ordinal))
            .ToList();

        var neighborIds = outbound
            .Select(e => e.To)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (alreadyRevealed)
        {
            return new RevealResponse(
                request.ArticleId,
                neighborIds,
                [],
                0,
                game.ConnectionCount,
                game.Score,
                game.DiscoveredEdges.Select(PuzzleMapping.ToEdgeDto).ToList(),
                game.HintEdges.Select(PuzzleMapping.ToEdgeDto).ToList(),
                game.PlayerPath,
                game.RevealedArticleIds,
                HasReachedTarget(game, puzzle),
                "Outbound links already revealed for this article.");
        }

        // Hints only — do NOT add to discovered path edges. Player must still drag to connect.
        var hintsAdded = new List<PuzzleEdge>();
        foreach (var edge in outbound)
        {
            var alreadyDiscovered = game.DiscoveredEdges.Any(d =>
                string.Equals(d.From, edge.From, StringComparison.Ordinal)
                && string.Equals(d.To, edge.To, StringComparison.Ordinal));
            if (alreadyDiscovered)
            {
                continue;
            }

            var alreadyHinted = game.HintEdges.Any(h =>
                string.Equals(h.From, edge.From, StringComparison.Ordinal)
                && string.Equals(h.To, edge.To, StringComparison.Ordinal));
            if (alreadyHinted)
            {
                continue;
            }

            game.HintEdges.Add(edge);
            hintsAdded.Add(edge);
        }

        game.RevealedArticleIds.Add(request.ArticleId);
        game.Score += ScoringRules.RevealOutboundCost;

        await gameRepository.UpdateAsync(game, cancellationToken);

        return new RevealResponse(
            request.ArticleId,
            neighborIds,
            hintsAdded.Select(PuzzleMapping.ToEdgeDto).ToList(),
            ScoringRules.RevealOutboundCost,
            game.ConnectionCount,
            game.Score,
            game.DiscoveredEdges.Select(PuzzleMapping.ToEdgeDto).ToList(),
            game.HintEdges.Select(PuzzleMapping.ToEdgeDto).ToList(),
            game.PlayerPath,
            game.RevealedArticleIds,
            HasReachedTarget(game, puzzle),
            neighborIds.Count == 0
                ? $"No outbound links on this board. +{ScoringRules.RevealOutboundCost} points."
                : $"Hinted {neighborIds.Count} possible link(s) — drag to confirm. +{ScoringRules.RevealOutboundCost} points.");
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

        // Results use the best route through confirmed links; exploration history is not erased from edges.
        game.PlayerPath = path.ToList();
        game.Status = GameStatus.Completed;
        game.CompletedAt = clock.UtcNow;
        await gameRepository.UpdateAsync(game, cancellationToken);

        return ToCompleteResponse(game, puzzle);
    }

    /// <summary>
    /// Append-only visit list. Never truncates — players may branch from earlier nodes.
    /// </summary>
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
        int pointsAdded,
        string message) =>
        new(
            success,
            fromId,
            toId,
            pointsAdded,
            game.ConnectionCount,
            game.Score,
            game.DiscoveredEdges.Select(PuzzleMapping.ToEdgeDto).ToList(),
            game.HintEdges.Select(PuzzleMapping.ToEdgeDto).ToList(),
            game.PlayerPath,
            game.RevealedArticleIds,
            HasReachedTarget(game, puzzle),
            message);

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
