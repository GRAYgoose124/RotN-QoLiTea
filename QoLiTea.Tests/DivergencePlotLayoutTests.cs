using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class DivergencePlotLayoutTests
{
    [Theory]
    [InlineData(0f, 100f, 0f)]
    [InlineData(50f, 100f, 0.5f)]
    [InlineData(100f, 100f, 1f)]
    public void BeatToX_normalized(float beat, float total, float expected)
        => Assert.Equal(expected, DivergencePlotLayout.BeatToX(beat, total), 3);

    [Fact]
    public void DivergenceToY_center_at_half()
        => Assert.Equal(0.5f, DivergencePlotLayout.DivergenceToY(0f), 3);

    [Fact]
    public void DivergenceToY_early_above_center()
        => Assert.True(DivergencePlotLayout.DivergenceToY(-100f) > 0.5f);

    [Fact]
    public void DivergenceToY_late_below_center()
        => Assert.True(DivergencePlotLayout.DivergenceToY(100f) < 0.5f);

    [Fact]
    public void SpanBandX_maps_beats()
    {
        var span = new PracticeSpan(25f, 75f, 5, 10f);
        var (x0, x1) = DivergencePlotLayout.SpanBandX(span, 100f);
        Assert.Equal(0.25f, x0, 3);
        Assert.Equal(0.75f, x1, 3);
    }
}
