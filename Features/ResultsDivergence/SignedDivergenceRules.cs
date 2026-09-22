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

    /// <summary>
    /// True when a miss has real early/late timing (not a synthetic ±100 fallback).
    /// Timeout / enemy-attack misses use <paramref name="wasPlayerInput"/> false and a synthetic
    /// after-window beat — that must not count as timing.
    /// </summary>
    public static bool MissHasTiming(
        float ratingPercent,
        float inputBeat,
        float targetBeat,
        bool wasPlayerInput = true)
    {
        if (!wasPlayerInput)
            return false;
        return HasUsableRatingPercent(ratingPercent) || HasBeatTiming(inputBeat, targetBeat);
    }

    /// <summary>Timed misses → dots; untimed misses → full-height verticals.</summary>
    public static PlotMarkerKind MarkerForMiss(
        float ratingPercent,
        float inputBeat,
        float targetBeat,
        bool wasPlayerInput = true)
        => MissHasTiming(ratingPercent, inputBeat, targetBeat, wasPlayerInput)
            ? PlotMarkerKind.Dot
            : PlotMarkerKind.VerticalLine;

    /// <summary>Signed Y for a miss marker; untimed verticals sit on the center line.</summary>
    public static float SignedForMiss(
        float ratingPercent,
        bool wasEarly,
        float inputBeat,
        float targetBeat,
        bool wasPlayerInput = true)
    {
        if (!MissHasTiming(ratingPercent, inputBeat, targetBeat, wasPlayerInput))
            return 0f;
        return ForFailure(ratingPercent, wasEarly, inputBeat, targetBeat);
    }

    public static bool HasUsableRatingPercent(float ratingPercent)
        => float.IsFinite(ratingPercent) && ratingPercent > 0f && ratingPercent < 100f;

    public static bool HasBeatTiming(float inputBeat, float targetBeat)
        => float.IsFinite(inputBeat) && float.IsFinite(targetBeat) && inputBeat != targetBeat;
}
