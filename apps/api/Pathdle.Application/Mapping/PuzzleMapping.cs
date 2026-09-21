using Pathdle.Application.Dtos;
using Pathdle.Application.Models;

namespace Pathdle.Application.Mapping;

public static class PuzzleMapping
{
    public static PublicPuzzleDto ToPublicDto(DailyPuzzle puzzle) =>
        new(
            puzzle.Id,
            puzzle.PuzzleDate,
            puzzle.GraphVersion,
            puzzle.StartArticleId,
            puzzle.TargetArticleId,
            puzzle.Nodes.Select(ToNodeDto).ToList());

    public static PuzzleNodeDto ToNodeDto(PuzzleNode node) =>
        new(
            node.Id,
            node.Title,
            node.X,
            node.Y,
            node.Kind.ToString().ToLowerInvariant(),
            node.Description);

    /// <summary>Discovered edges include the relationship label.</summary>
    public static EdgeDto ToDiscoveredEdgeDto(PuzzleEdge edge) =>
        new(edge.From, edge.To, edge.GroupId, edge.GroupLabel);

    /// <summary>Hint edges never expose group labels.</summary>
    public static EdgeDto ToHintEdgeDto(PuzzleEdge edge) =>
        new(edge.From, edge.To);

    public static GameStateDto ToGameStateDto(PlayerGame game, DailyPuzzle puzzle)
    {
        var reachedTarget = Services.GraphPathFinder.FindShortestPath(
            puzzle.StartArticleId,
            puzzle.TargetArticleId,
            game.DiscoveredEdges) is not null;

        var hintsRemaining = Math.Max(0, ScoringRules.MaxHintsPerGame - game.HintsUsed);

        return new GameStateDto(
            game.Id,
            puzzle.Id,
            game.Status.ToString().ToLowerInvariant(),
            puzzle.StartArticleId,
            puzzle.TargetArticleId,
            puzzle.Nodes.Select(ToNodeDto).ToList(),
            game.DiscoveredEdges.Select(ToDiscoveredEdgeDto).ToList(),
            game.HintEdges.Select(ToHintEdgeDto).ToList(),
            game.PlayerPath,
            game.RevealedArticleIds,
            game.ConnectionCount,
            game.Score,
            game.HintsUsed,
            hintsRemaining,
            reachedTarget);
    }
}
