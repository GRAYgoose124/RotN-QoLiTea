using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class DivergencePlotEnhancementTests
{
    [Theory]
    [InlineData(1, PlotTapAction.CloseOrOpen)]
    [InlineData(2, PlotTapAction.ToggleFullscreen)]
    [InlineData(3, PlotTapAction.None)]
    public void Tap_policy_maps_counts(int taps, PlotTapAction expected)
        => Assert.Equal(expected, DivergencePlotTapPolicy.Resolve(taps));

    [Fact]
    public void Super_crit_requires_perfect_and_threshold()
    {
        Assert.False(DivergenceRatingRules.IsSuperCrit(PlotHitRating.Great, 95f, 90));
        Assert.False(DivergenceRatingRules.IsSuperCrit(PlotHitRating.Perfect, 89f, 90));
        Assert.True(DivergenceRatingRules.IsSuperCrit(PlotHitRating.Perfect, 90f, 90));
        Assert.True(DivergenceRatingRules.IsSuperCrit(PlotHitRating.Perfect, 100f, 90));
    }

    [Fact]
    public void Timing_bins_dedupe_magnitudes()
    {
        var mags = DivergenceTimingBins.SignedMagnitudesFromMinimumPercents(new[] { 40, 60, 80, 90, 80 });
        Assert.Equal(new[] { 10f, 20f, 40f, 60f }, mags);
    }

    [Theory]
    [InlineData(90, 10f)]
    [InlineData(100, 0f)]
    public void Super_crit_bin_magnitude(int truePerfectMin, float expected)
        => Assert.Equal(expected, DivergenceTimingBins.SuperCritMagnitude(truePerfectMin), 3);
}
