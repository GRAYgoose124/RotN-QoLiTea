using System;

namespace QoLiTea.Features.ResultsDivergence;

public static class ResultsDivergencePolicy
{
    public static bool ShouldHarvest(bool masterEnabled, bool plotEnabled, bool practiceEnabled)
        => masterEnabled && (plotEnabled || practiceEnabled);

    public static bool ShouldShowPlot(bool masterEnabled, bool featureEnabled)
        => masterEnabled && featureEnabled;

    /// <summary>Clamp to 0–100; empty/garbage → <paramref name="defaultPercent"/>.</summary>
    public static int ParsePlotOpacityPercent(string raw, int defaultPercent = 80)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return defaultPercent;
        if (!int.TryParse(raw.Trim(), out int n))
            return defaultPercent;
        if (n < 0)
            return 0;
        if (n > 100)
            return 100;
        return n;
    }

    public static float ToAlpha(int opacityPercent)
        => Math.Clamp(opacityPercent, 0, 100) / 100f;
}
