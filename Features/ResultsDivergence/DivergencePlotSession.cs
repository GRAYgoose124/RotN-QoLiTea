using UnityEngine;
using UnityEngine.UI;

namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Per-results-screen plot instance + runtime G-toggle visibility.</summary>
internal static class DivergencePlotSession
{
    internal static RawImage ActivePlot { get; private set; }
    internal static bool IsVisible { get; private set; } = true;

    internal static void Reset()
    {
        ActivePlot = null;
        IsVisible = true;
    }

    internal static void Register(RawImage plot, int opacityPercent)
    {
        ActivePlot = plot;
        IsVisible = true;
        if (plot == null)
            return;

        plot.gameObject.SetActive(true);
        DivergencePlotRenderer.ApplyOpacity(plot, opacityPercent);
    }

    internal static void ToggleVisibility(int opacityPercent)
    {
        if (ActivePlot == null)
            return;

        IsVisible = !IsVisible;
        ActivePlot.gameObject.SetActive(IsVisible);
        if (IsVisible)
            DivergencePlotRenderer.ApplyOpacity(ActivePlot, opacityPercent);
    }

    internal static void DestroyIfAny()
    {
        if (ActivePlot != null)
            Object.Destroy(ActivePlot.gameObject);
        Reset();
    }
}
