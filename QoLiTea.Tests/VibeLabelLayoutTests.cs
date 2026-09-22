using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class VibeLabelLayoutTests
{
    [Theory]
    [InlineData(0, 0.88f)]
    [InlineData(1, 0.825f)]
    [InlineData(2, 0.77f)]
    [InlineData(3, 0.715f)]
    public void AnchorY_steps_down_by_index(int index, float expectedY)
        => Assert.Equal(
            expectedY,
            VibeLabelLayout.AnchorY(index, baseline: 0.88f, step: 0.055f, rowCount: 4),
            3);

    [Fact]
    public void AnchorY_wraps_after_row_count()
        => Assert.Equal(
            0.88f,
            VibeLabelLayout.AnchorY(4, baseline: 0.88f, step: 0.055f, rowCount: 4),
            3);

    [Fact]
    public void AnchorY_clamps_non_positive_row_count_to_one()
        => Assert.Equal(
            0.88f,
            VibeLabelLayout.AnchorY(7, baseline: 0.88f, step: 0.055f, rowCount: 0),
            3);
}
