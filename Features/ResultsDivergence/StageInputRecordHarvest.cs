using System;
using System.Collections.Generic;
using Shared;
using Shared.RhythmEngine;

namespace QoLiTea.Features.ResultsDivergence;

internal static class StageInputRecordHarvest
{
    internal static IReadOnlyList<HitDivergenceSample> FromRecord(StageInputRecord record)
    {
        if (record?._inputResponsesInChronologicalOrder == null)
            return Array.Empty<HitDivergenceSample>();

        int truePerfectMin = DivergenceTimingBinsHarvest.ReadTruePerfectMinimum(record._inputRatingsDefinition);
        var rawById = record._rawInputDataByGuid;
        var list = new List<HitDivergenceSample>(record._inputResponsesInChronologicalOrder.Count);

        foreach (var response in record._inputResponsesInChronologicalOrder)
        {
            bool isMiss = response.rating == InputRating.Miss || !response.success;
            if (!response.wasPlayerInput && !isMiss)
                continue;

            float targetBeat = 0f;
            float inputBeat = 0f;
            bool hasBeat = false;
            if (rawById != null &&
                response.associateRawDataIdentifier != Guid.Empty &&
                rawById.TryGetValue(response.associateRawDataIdentifier, out var raw))
            {
                inputBeat = raw.InputBeatNumber;
                targetBeat = raw.TargetBeatNumber;
                hasBeat = true;
            }

            PlotHitRating plotRating = DivergencePlotColors.FromInputRating(
                isMiss ? InputRating.Miss : response.rating,
                response.wasPlayerInput);

            if (!hasBeat && plotRating == PlotHitRating.ComboBreak && response.wasPlayerInput)
            {
                // Errant overhit — vertical line at best-known beat (live harvest usually has this).
                continue;
            }

            if (!hasBeat && !response.wasPlayerInput)
                continue;

            float signed = plotRating == PlotHitRating.Miss || plotRating == PlotHitRating.ComboBreak
                ? SignedDivergenceRules.ForFailure(
                    response.ratingPercent,
                    response.wasEarly,
                    inputBeat,
                    targetBeat)
                : SignedDivergenceRules.Compute(response.ratingPercent, response.wasEarly);

            bool isSuperCrit = DivergenceRatingRules.IsSuperCrit(
                plotRating,
                response.ratingPercent,
                truePerfectMin);

            list.Add(new HitDivergenceSample(
                targetBeat,
                signed,
                plotRating,
                response.ratingPercent,
                PlotMarkerKind.Dot,
                isSuperCrit));
        }

        return list;
    }
}
