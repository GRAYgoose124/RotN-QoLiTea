namespace QoLiTea.Features.ResultsDivergence;

public enum PlotTapAction
{
    None,
    /// <summary>Visible → hide; hidden → docked.</summary>
    CloseOrOpen,
    /// <summary>Docked/hidden → fullscreen; fullscreen → docked.</summary>
    ToggleFullscreen,
}

public static class DivergencePlotTapPolicy
{
    public static PlotTapAction Resolve(int tapCount)
        => tapCount switch
        {
            2 => PlotTapAction.ToggleFullscreen,
            1 => PlotTapAction.CloseOrOpen,
            _ => PlotTapAction.None,
        };
}
