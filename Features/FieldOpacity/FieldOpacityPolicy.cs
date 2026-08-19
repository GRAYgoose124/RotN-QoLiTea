namespace QoLiTea.Features.FieldOpacity;

/// <summary>
/// Config gate + opacity percent parse/scale for lane tile alphas.
/// </summary>
public static class FieldOpacityPolicy
{
    public static bool ShouldScale(bool masterEnabled, bool featureEnabled)
        => masterEnabled && featureEnabled;

    /// <summary>Clamp to 0–100; empty/garbage → 100 (stock).</summary>
    public static int ParsePercent(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 100;
        if (!int.TryParse(raw.Trim(), out int n))
            return 100;
        if (n < 0)
            return 0;
        if (n > 100)
            return 100;
        return n;
    }

    public static float ToFactor(int percent) => percent / 100f;

    public static float ScaleAlpha(float stockAlpha, int percent)
        => stockAlpha * ToFactor(percent);
}
