using Shared;

namespace QoLiTea.Features.ResultsDivergence;

internal static class DivergenceLiveHarvest
{
    internal static void TryAppend(
        float targetBeat,
        float signed,
        PlotHitRating rating,
        float ratingPercent = 0f,
        PlotMarkerKind marker = PlotMarkerKind.Dot)
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
            isSuperCrit));
    }

    internal static void TryAppendFailure(
        float targetBeat,
        float ratingPercent,
        float inputBeat,
        float targetBeatForTiming,
        PlotHitRating rating)
    {
        bool wasEarly = inputBeat < targetBeatForTiming;
        float signed = SignedDivergenceRules.ForFailure(
            ratingPercent,
            wasEarly,
            inputBeat,
            targetBeatForTiming);
        TryAppend(targetBeat, signed, rating, ratingPercent);
    }

    internal static void TryAppendOverhit(float inputBeat)
    {
        TryAppend(
            inputBeat,
            0f,
            PlotHitRating.ComboBreak,
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
        float signed = plotRating == PlotHitRating.Miss || plotRating == PlotHitRating.ComboBreak
            ? SignedDivergenceRules.ForFailure(
                ratingPercent,
                inputBeatNumber < targetBeatNumber,
                inputBeatNumber,
                targetBeatNumber)
            : SignedDivergenceRules.Compute(ratingPercent, inputBeatNumber < targetBeatNumber);

        TryAppend(targetBeatNumber, signed, plotRating, ratingPercent);
    }
}
