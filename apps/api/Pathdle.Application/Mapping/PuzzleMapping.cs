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
        new(node.Id, node.Title, node.X, node.Y, node.Kind.ToString().ToLowerInvariant());

    public static EdgeDto ToEdgeDto(PuzzleEdge edge) => new(edge.From, edge.To);

    public static GameStateDto ToGameStateDto(PlayerGame game, DailyPuzzle puzzle)
    {
        var reachedTarget = GraphPathFinderReached(game, puzzle);

        return new GameStateDto(
            game.Id,
            puzzle.Id,
            game.Status.ToString().ToLowerInvariant(),
            puzzle.StartArticleId,
            puzzle.TargetArticleId,
            puzzle.Nodes.Select(ToNodeDto).ToList(),
            game.DiscoveredEdges.Select(ToEdgeDto).ToList(),
            game.HintEdges.Select(ToEdgeDto).ToList(),
            game.PlayerPath,
            game.RevealedArticleIds,
            game.ConnectionCount,
            game.Score,
            reachedTarget);
    }

    private static bool GraphPathFinderReached(PlayerGame game, DailyPuzzle puzzle) =>
        Services.GraphPathFinder.FindShortestPath(
            puzzle.StartArticleId,
            puzzle.TargetArticleId,
            game.DiscoveredEdges) is not null;
}
