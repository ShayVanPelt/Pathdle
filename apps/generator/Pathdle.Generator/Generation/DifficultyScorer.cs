namespace Pathdle.Generator.Generation;

internal sealed record DifficultyMetrics(
    int OptimalLength,
    int AltShortestCount,
    double MidPathBranchAvg,
    int TrapNodeCount,
    int TrapEdgeCount,
    double HubPenalty,
    int Score,
    string Band)
{
    public object ToPayload() => new
    {
        optimal_length = OptimalLength,
        alt_shortest_count = AltShortestCount,
        mid_path_branch_avg = Math.Round(MidPathBranchAvg, 2),
        trap_node_count = TrapNodeCount,
        trap_edge_count = TrapEdgeCount,
        hub_penalty = Math.Round(HubPenalty, 2),
        score = Score,
        band = Band
    };
}

internal static class DifficultyScorer
{
    public static DifficultyMetrics Score(BuiltBoard board, GenerationOptions opt)
    {
        var p = board.Path;
        var trapNodes = board.TrapNodeIds.Count;
        var trapEdges = board.TrapEdgeCount;

        // Higher score = harder (still a cost metaphor for design banding).
        var score =
            20
            + p.OptimalLength * 8
            + Math.Min(24, (int)(p.MidPathBranchAvg * 4))
            + Math.Min(20, trapNodes / 2)
            + Math.Min(12, trapEdges / 4)
            + (int)(p.HubPenalty * 10)
            - Math.Max(0, p.AltShortestCount - 1) * 6;

        score = Math.Clamp(score, 0, 100);
        var band = score switch
        {
            < 45 => "medium",
            < 70 => "medium-hard",
            _ => "hard"
        };

        return new DifficultyMetrics(
            p.OptimalLength,
            p.AltShortestCount,
            p.MidPathBranchAvg,
            trapNodes,
            trapEdges,
            p.HubPenalty,
            score,
            band);
    }

    public static bool Accepts(DifficultyMetrics metrics, BuiltBoard board, GenerationOptions opt) =>
        metrics.OptimalLength >= opt.MinLength
        && metrics.OptimalLength <= opt.MaxLength
        && board.NodeIds.Count >= opt.MinBoardNodes
        && board.NodeIds.Count <= opt.MaxBoardNodes + 10
        && metrics.TrapNodeCount >= 6
        && metrics.Score >= opt.MinScore
        && metrics.Score <= opt.MaxScore;
}
