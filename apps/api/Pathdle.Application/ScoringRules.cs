namespace Pathdle.Application;

/// <summary>
/// Pathdle scoring: points are a cost. Lower score is better.
/// </summary>
public static class ScoringRules
{
    /// <summary>Successful link discovery (shared group edge exists).</summary>
    public const int SuccessfulLinkCost = 100;

    /// <summary>Failed link attempt (no shared group). No line is kept.</summary>
    public const int FailedLinkCost = 200;

    /// <summary>Reveal all neighbors from one article (first paid reveal only).</summary>
    public const int RevealOutboundCost = 75;

    /// <summary>Maximum number of paid hint reveals per game.</summary>
    public const int MaxHintsPerGame = 3;
}
