using System;

namespace QoLiTea.Features.ResultsDivergence;

public static class SignedDivergenceRules
{
    public static float Compute(float ratingPercent, bool wasEarly)
    {
        float mag = 100f - ratingPercent;
        return (wasEarly ? -1f : 1f) * mag;
    }

    public static float MissDivergence(bool? wasEarly)
        => wasEarly == false ? 100f : -100f;

    /// <summary>
    /// Miss / combo-break / overhit: prefer stock <paramref name="ratingPercent"/>, else beat delta, else ±100.
    /// </summary>
    public static float ForFailure(float ratingPercent, bool wasEarly, float inputBeat, float targetBeat)
    {
        if (HasUsableRatingPercent(ratingPercent))
            return Compute(ratingPercent, wasEarly);

        if (HasBeatTiming(inputBeat, targetBeat))
            return FromBeatDelta(inputBeat, targetBeat);

        return MissDivergence(wasEarly);
    }

    public static float FromBeatDelta(float inputBeat, float targetBeat)
    {
        float beatDelta = inputBeat - targetBeat;
        bool early = beatDelta < 0f;
        float mag = Math.Min(100f, Math.Abs(beatDelta) * 100f);
        return (early ? -1f : 1f) * mag;
    }

    private static bool HasUsableRatingPercent(float ratingPercent)
        => float.IsFinite(ratingPercent) && ratingPercent > 0f && ratingPercent < 100f;

    private static bool HasBeatTiming(float inputBeat, float targetBeat)
        => float.IsFinite(inputBeat) && float.IsFinite(targetBeat) && inputBeat != targetBeat;
}
