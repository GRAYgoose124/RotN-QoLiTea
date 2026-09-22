namespace QoLiTea.Features.ResultsDivergence;

/// <summary>
/// Empty/partial ProcessHitData swings: true overhit vs miss-style vertical (open windows / mismatch).
/// </summary>
public static class ProcessHitClassification
{
    public enum EmptySwingKind
    {
        /// <summary>No notes involved — stock errant / overhit (pink ComboBreak vertical).</summary>
        TrueOverhit,
        /// <summary>Empty or partial swing while notes were attackable — miss-style vert red.</summary>
        MissVertical,
    }

    /// <summary>
    /// Classify an empty <c>hitDatas</c> swing.
    /// </summary>
    public static EmptySwingKind ClassifyEmptySwing(
        int positionsToAttackCount,
        bool anyEnemyHitWindowOpen)
    {
        if (anyEnemyHitWindowOpen || positionsToAttackCount > 0)
            return EmptySwingKind.MissVertical;
        return EmptySwingKind.TrueOverhit;
    }

    /// <summary>
    /// True when stock would set the partial-miss flag (hit count ≠ attack positions).
    /// </summary>
    public static bool IsPartialMiss(int hitDataCount, int positionsToAttackCount)
        => hitDataCount > 0
           && positionsToAttackCount > 0
           && hitDataCount != positionsToAttackCount;
}
