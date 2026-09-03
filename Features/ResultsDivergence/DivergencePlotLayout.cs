using System;

namespace QoLiTea.Features.ResultsDivergence;

public static class DivergencePlotLayout
{
    public static float BeatToX(float beat, float totalBeats)
    {
        if (totalBeats <= 0f)
            return 0f;
        return Clamp01(beat / totalBeats);
    }

    public static float DivergenceToY(float signedDivergence)
    {
        float clamped = Math.Max(-100f, Math.Min(100f, signedDivergence));
        return Clamp01(0.5f - clamped / 200f);
    }

    /// <summary>Map signed divergence to a texture row (0 = top, texHeight - 1 = bottom).</summary>
    public static int DivergenceToRow(int texHeight, float signedDivergence)
    {
        if (texHeight <= 0)
            return 0;

        float y = DivergenceToY(signedDivergence);
        int row = (int)Math.Round(y * (texHeight - 1));
        if (row < 0)
            return 0;
        return row >= texHeight ? texHeight - 1 : row;
    }

    public static (float x0, float x1) SpanBandX(PracticeSpan span, float totalBeats)
        => SpanBandX(span.StartBeat, span.EndBeat, totalBeats);

    public static (float x0, float x1) SpanBandX(float startBeat, float endBeat, float totalBeats)
        => (BeatToX(startBeat, totalBeats), BeatToX(endBeat, totalBeats));

    private static float Clamp01(float v)
        => v < 0f ? 0f : (v > 1f ? 1f : v);
}
