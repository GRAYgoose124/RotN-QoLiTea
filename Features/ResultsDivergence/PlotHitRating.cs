namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Mirrors <c>Shared.InputRating</c> for pure tests (no game asm), plus combo-break.</summary>
public enum PlotHitRating
{
    Miss = 0,
    Ok = 1,
    Good = 2,
    Great = 3,
    Perfect = 4,
    /// <summary>Timeout / enemy-hit miss recorded with wasPlayerInput=false (no stock rating color).</summary>
    ComboBreak = 5,
}
