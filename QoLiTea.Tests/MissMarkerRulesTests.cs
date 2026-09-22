using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class MissMarkerRulesTests
{
    [Fact]
    public void Untimed_miss_uses_vertical_line_at_center()
    {
        Assert.False(SignedDivergenceRules.MissHasTiming(ratingPercent: 0f, inputBeat: 10f, targetBeat: 10f));
        Assert.Equal(
            PlotMarkerKind.VerticalLine,
            SignedDivergenceRules.MarkerForMiss(0f, 10f, 10f));
        Assert.Equal(0f, SignedDivergenceRules.SignedForMiss(0f, wasEarly: true, 10f, 10f));
    }

    [Fact]
    public void Timed_miss_via_rating_percent_uses_dot()
    {
        Assert.True(SignedDivergenceRules.MissHasTiming(ratingPercent: 40f, inputBeat: 10f, targetBeat: 10f));
        Assert.Equal(
            PlotMarkerKind.Dot,
            SignedDivergenceRules.MarkerForMiss(40f, 10f, 10f));
        Assert.Equal(-60f, SignedDivergenceRules.SignedForMiss(40f, wasEarly: true, 10f, 10f));
    }

    [Fact]
    public void Timed_miss_via_beat_delta_uses_dot()
    {
        Assert.True(SignedDivergenceRules.MissHasTiming(0f, inputBeat: 9.5f, targetBeat: 10f));
        Assert.Equal(
            PlotMarkerKind.Dot,
            SignedDivergenceRules.MarkerForMiss(0f, 9.5f, 10f));
    }
}
