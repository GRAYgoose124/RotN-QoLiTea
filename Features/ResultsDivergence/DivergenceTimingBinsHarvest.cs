using System;
using System.Collections.Generic;
using Shared;
using Shared.RhythmEngine;

namespace QoLiTea.Features.ResultsDivergence;

internal static class DivergenceTimingBinsHarvest
{
    internal static IReadOnlyList<float> FromRatings(InputRatingsDefinition ratings)
    {
        if (ratings == null)
            return Array.Empty<float>();

        var mins = new[]
        {
            ratings.GetMinimumPercentForRating(InputRating.Ok),
            ratings.GetMinimumPercentForRating(InputRating.Good),
            ratings.GetMinimumPercentForRating(InputRating.Great),
            ratings.GetMinimumPercentForRating(InputRating.Perfect),
        };
        return DivergenceTimingBins.SignedMagnitudesFromMinimumPercents(mins);
    }

    internal static int ReadTruePerfectMinimum(InputRatingsDefinition ratings)
        => ratings?._truePerfectBonusMinimumValue ?? 90;
}
