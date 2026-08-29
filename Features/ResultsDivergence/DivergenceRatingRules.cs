namespace QoLiTea.Features.ResultsDivergence;

public static class DivergenceRatingRules
{
    /// <summary>
    /// Stock "super crit" / true perfect — <see cref="InputRatingsDefinition.IsRatingPercentTruePerfect"/>.
    /// </summary>
    public static bool IsSuperCrit(PlotHitRating rating, float ratingPercent, int truePerfectMinimum)
        => rating == PlotHitRating.Perfect
           && ratingPercent >= truePerfectMinimum;
}
