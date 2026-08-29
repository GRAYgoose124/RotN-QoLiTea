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

    public static (float x0, float x1) SpanBandX(PracticeSpan span, float totalBeats)
        => (BeatToX(span.StartBeat, totalBeats), BeatToX(span.EndBeat, totalBeats));

    private static float Clamp01(float v)
        => v < 0f ? 0f : (v > 1f ? 1f : v);
}
