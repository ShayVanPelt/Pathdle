namespace Pathdle.Application;

/// <summary>
/// Pathdle scoring: points are a cost. Lower score is better.
/// </summary>
public static class ScoringRules
{
    /// <summary>Successful link discovery (node → node with a valid directed edge).</summary>
    public const int SuccessfulLinkCost = 100;

    /// <summary>Failed link attempt (no edge in that direction). No line is kept.</summary>
    public const int FailedLinkCost = 100;

    /// <summary>Reveal all outbound neighbors from one article on the board.</summary>
    public const int RevealOutboundCost = 75;
}
