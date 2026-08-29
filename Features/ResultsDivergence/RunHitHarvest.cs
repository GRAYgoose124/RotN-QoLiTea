using System;
using System.Collections.Generic;

namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Test/DTO row for joining harvest without Unity.</summary>
public readonly struct HarvestTestRow
{
    public HarvestTestRow(
        float beat,
        float ratingPercent,
        bool wasEarly,
        PlotHitRating rating,
        bool include = true,
        float inputBeat = float.NaN,
        float targetBeat = float.NaN)
    {
        Beat = beat;
        RatingPercent = ratingPercent;
        WasEarly = wasEarly;
        Rating = rating;
        Include = include;
        InputBeat = float.IsNaN(inputBeat) ? beat : inputBeat;
        TargetBeat = float.IsNaN(targetBeat) ? beat : targetBeat;
    }

    public float Beat { get; }
    public float RatingPercent { get; }
    public bool WasEarly { get; }
    public PlotHitRating Rating { get; }
    public bool Include { get; }
    public float InputBeat { get; }
    public float TargetBeat { get; }
}

public static class RunHitHarvest
{
    public static IReadOnlyList<HitDivergenceSample> FromTestRows(IReadOnlyList<HarvestTestRow> rows)
    {
        if (rows == null || rows.Count == 0)
            return Array.Empty<HitDivergenceSample>();

        var list = new List<HitDivergenceSample>(rows.Count);
        foreach (var row in rows)
        {
            if (!row.Include)
                continue;

            float signed = row.Rating == PlotHitRating.Miss || row.Rating == PlotHitRating.ComboBreak
                ? SignedDivergenceRules.ForFailure(
                    row.RatingPercent,
                    row.WasEarly,
                    row.InputBeat,
                    row.TargetBeat)
                : SignedDivergenceRules.Compute(row.RatingPercent, row.WasEarly);

            bool isSuperCrit = DivergenceRatingRules.IsSuperCrit(row.Rating, row.RatingPercent, 90);
            list.Add(new HitDivergenceSample(row.Beat, signed, row.Rating, row.RatingPercent, PlotMarkerKind.Dot, isSuperCrit));
        }

        return list;
    }
}
