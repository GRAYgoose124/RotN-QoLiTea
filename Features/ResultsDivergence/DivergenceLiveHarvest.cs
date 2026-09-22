using Shared;

namespace QoLiTea.Features.ResultsDivergence;

internal static class DivergenceLiveHarvest
{
    internal static void TryAppend(
        float targetBeat,
        float signed,
        PlotHitRating rating,
        float ratingPercent = 0f,
        PlotMarkerKind marker = PlotMarkerKind.Dot,
        string enemyDisplayName = null)
    {
        if (!ResultsDivergencePolicy.ShouldHarvest(
                Plugin.Enabled,
                Plugin.ResultsDivergencePlotEnabled,
                Plugin.WorstSectionPracticeEnabled))
            return;

        bool isSuperCrit = DivergenceRatingRules.IsSuperCrit(
            rating,
            ratingPercent,
            RunSessionStore.TruePerfectMinimum);

        RunSessionStore.AppendLive(new HitDivergenceSample(
            targetBeat,
            signed,
            rating,
            ratingPercent,
            marker,
            isSuperCrit,
            enemyDisplayName));
    }

    internal static void TryAppendFailure(
        float targetBeat,
        float ratingPercent,
        float inputBeat,
        float targetBeatForTiming,
        PlotHitRating rating,
        string enemyDisplayName = null,
        bool wasPlayerInput = true)
    {
        bool wasEarly = inputBeat < targetBeatForTiming;
        PlotMarkerKind resolvedMarker;
        float signed;
        if (rating == PlotHitRating.Miss || rating == PlotHitRating.ComboBreak)
        {
            resolvedMarker = SignedDivergenceRules.MarkerForMiss(
                ratingPercent, inputBeat, targetBeatForTiming, wasPlayerInput);
            signed = SignedDivergenceRules.SignedForMiss(
                ratingPercent, wasEarly, inputBeat, targetBeatForTiming, wasPlayerInput);
        }
        else
        {
            resolvedMarker = PlotMarkerKind.Dot;
            signed = SignedDivergenceRules.ForFailure(
                ratingPercent,
                wasEarly,
                inputBeat,
                targetBeatForTiming);
        }

        TryAppend(
            targetBeat,
            signed,
            rating,
            ratingPercent,
            resolvedMarker,
            enemyDisplayName);
    }

    internal static void TryAppendOverhit(float inputBeat)
    {
        TryAppend(
            inputBeat,
            0f,
            PlotHitRating.ComboBreak,
            marker: PlotMarkerKind.VerticalLine);
    }

    internal static void TryAppendMissVertical(float inputBeat)
    {
        TryAppend(
            inputBeat,
            0f,
            PlotHitRating.Miss,
            marker: PlotMarkerKind.VerticalLine);
    }

    internal static void TryAppendRecordInput(
        InputRating inputRating,
        float ratingPercent,
        float inputBeatNumber,
        float targetBeatNumber,
        bool wasPlayerInput)
    {
        if (!ResultsDivergencePolicy.ShouldHarvest(
                Plugin.Enabled,
                Plugin.ResultsDivergencePlotEnabled,
                Plugin.WorstSectionPracticeEnabled))
            return;

        bool isMiss = inputRating == InputRating.Miss;
        if (!wasPlayerInput && !isMiss)
            return;

        PlotHitRating plotRating = DivergencePlotColors.FromInputRating(inputRating, wasPlayerInput);
        float signed;
        PlotMarkerKind marker = PlotMarkerKind.Dot;
        if (plotRating == PlotHitRating.Miss || plotRating == PlotHitRating.ComboBreak)
        {
            // Timeout misses (wasPlayerInput=false) carry a synthetic after-window beat —
            // MarkerForMiss treats those as untimed verticals, not aligned edge dots.
            marker = SignedDivergenceRules.MarkerForMiss(
                ratingPercent, inputBeatNumber, targetBeatNumber, wasPlayerInput);
            signed = SignedDivergenceRules.SignedForMiss(
                ratingPercent,
                inputBeatNumber < targetBeatNumber,
                inputBeatNumber,
                targetBeatNumber,
                wasPlayerInput);

            // Plot timeouts as Miss (red vertical), not ComboBreak (pink overhit).
            if (isMiss && !wasPlayerInput)
                plotRating = PlotHitRating.Miss;
        }
        else
        {
            signed = SignedDivergenceRules.Compute(ratingPercent, inputBeatNumber < targetBeatNumber);
        }

        TryAppend(targetBeatNumber, signed, plotRating, ratingPercent, marker);
    }
}
