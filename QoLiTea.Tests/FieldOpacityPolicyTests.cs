using QoLiTea.Features.FieldOpacity;
using Xunit;

namespace QoLiTea.Tests;

public class FieldOpacityPolicyTests
{
    [Fact]
    public void Gate_requires_master_and_feature()
    {
        Assert.False(FieldOpacityPolicy.ShouldScale(false, true));
        Assert.False(FieldOpacityPolicy.ShouldScale(true, false));
        Assert.True(FieldOpacityPolicy.ShouldScale(true, true));
    }

    [Theory]
    [InlineData("100", 100)]
    [InlineData("0", 0)]
    [InlineData("50", 50)]
    [InlineData(" 75 ", 75)]
    [InlineData("150", 100)]
    [InlineData("-5", 0)]
    [InlineData("", 100)]
    [InlineData("nope", 100)]
    public void ParsePercent_clamps_or_defaults(string raw, int expected)
    {
        Assert.Equal(expected, FieldOpacityPolicy.ParsePercent(raw));
    }

    [Fact]
    public void ParsePercent_null_defaults_to_stock()
    {
        Assert.Equal(100, FieldOpacityPolicy.ParsePercent(null!));
    }

    [Fact]
    public void ScaleAlpha_half_opacity()
    {
        Assert.Equal(0.5f, FieldOpacityPolicy.ScaleAlpha(1f, 50), 3);
    }

    [Fact]
    public void ScaleAlpha_keeps_stock_zero()
    {
        Assert.Equal(0f, FieldOpacityPolicy.ScaleAlpha(0f, 50), 3);
    }

    [Fact]
    public void ScaleAlpha_identity_at_100()
    {
        Assert.Equal(0.85f, FieldOpacityPolicy.ScaleAlpha(0.85f, 100), 3);
    }
}
