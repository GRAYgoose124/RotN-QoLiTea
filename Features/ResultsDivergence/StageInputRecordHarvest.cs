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

        var rawById = record._rawInputDataByGuid;
        var list = new List<HitDivergenceSample>(record._inputResponsesInChronologicalOrder.Count);

        foreach (var response in record._inputResponsesInChronologicalOrder)
        {
            bool isMiss = response.rating == InputRating.Miss || !response.success;
            // Timeout / enemy-hit misses use wasPlayerInput=false and have no raw row.
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

            // Without a beat we cannot place the dot — live RecordInput postfix covers these.
            if (!hasBeat && !response.wasPlayerInput)
                continue;

            PlotHitRating plotRating = DivergencePlotColors.FromInputRating(
                isMiss ? InputRating.Miss : response.rating,
                response.wasPlayerInput);

            float signed = plotRating == PlotHitRating.Miss || plotRating == PlotHitRating.ComboBreak
                ? SignedDivergenceRules.ForFailure(
                    response.ratingPercent,
                    response.wasEarly,
                    inputBeat,
                    targetBeat)
                : SignedDivergenceRules.Compute(response.ratingPercent, response.wasEarly);

            list.Add(new HitDivergenceSample(targetBeat, signed, plotRating));
        }

        return list;
    }
}
