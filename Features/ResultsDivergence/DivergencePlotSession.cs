using UnityEngine;
using UnityEngine.UI;

namespace QoLiTea.Features.ResultsDivergence;

public enum PlotDisplayMode
{
    Hidden,
    Docked,
    Fullscreen,
}

/// <summary>Per-results-screen plot instance, layout mode, and runtime controls.</summary>
internal static class DivergencePlotSession
{
    internal static RawImage ActivePlot { get; private set; }
    internal static PlotDisplayMode DisplayMode { get; private set; } = PlotDisplayMode.Docked;
    internal static PlotRenderContext Context { get; private set; }

    internal static void Reset()
    {
        ActivePlot = null;
        DisplayMode = PlotDisplayMode.Docked;
        Context = null;
    }

    internal static void Register(RawImage plot, PlotRenderContext context)
    {
        ActivePlot = plot;
        Context = context;
        DisplayMode = PlotDisplayMode.Docked;
        if (plot == null)
            return;

        plot.gameObject.SetActive(true);
        ApplyCurrentOpacity();
    }

    internal static void Hide()
    {
        DisplayMode = PlotDisplayMode.Hidden;
        if (ActivePlot != null)
            ActivePlot.gameObject.SetActive(false);
    }

    internal static void ApplyTapAction(PlotTapAction action)
    {
        if (Context == null || ActivePlot == null)
            return;

        switch (action)
        {
            case PlotTapAction.CloseOrOpen:
                if (DisplayMode == PlotDisplayMode.Hidden)
                    SetMode(PlotDisplayMode.Docked);
                else
                    Hide();
                break;
            case PlotTapAction.ToggleFullscreen:
                if (DisplayMode == PlotDisplayMode.Fullscreen)
                    SetMode(PlotDisplayMode.Docked);
                else
                    SetMode(PlotDisplayMode.Fullscreen);
                break;
        }
    }

    internal static void SetMode(PlotDisplayMode mode)
    {
        if (ActivePlot == null || Context == null)
            return;

        DisplayMode = mode;
        ActivePlot.gameObject.SetActive(mode != PlotDisplayMode.Hidden);
        DivergencePlotRenderer.ApplyLayout(ActivePlot.gameObject, mode);
        DivergencePlotRenderer.RefreshTexture(ActivePlot, Context, mode);
        ApplyCurrentOpacity();
    }

    internal static void DestroyIfAny()
    {
        if (ActivePlot != null)
            Object.Destroy(ActivePlot.gameObject);
        Reset();
    }

    private static void ApplyCurrentOpacity()
    {
        if (ActivePlot == null || Context == null)
            return;

        int opacity = DisplayMode == PlotDisplayMode.Fullscreen ? 100 : Context.OpacityPercent;
        DivergencePlotRenderer.ApplyOpacity(ActivePlot, opacity);
    }
}
