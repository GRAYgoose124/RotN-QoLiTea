using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class SignedDivergenceRulesTests
{
    [Theory]
    [InlineData(100f, true, 0f)]
    [InlineData(100f, false, 0f)]
    [InlineData(90f, true, -10f)]
    [InlineData(90f, false, 10f)]
    public void Compute_signed_percent(float pct, bool early, float expected)
        => Assert.Equal(expected, SignedDivergenceRules.Compute(pct, early), 3);

    [Fact]
    public void Miss_unknown_early_defaults_negative()
        => Assert.Equal(-100f, SignedDivergenceRules.MissDivergence(null), 3);

    [Fact]
    public void Miss_late_is_positive_extent()
        => Assert.Equal(100f, SignedDivergenceRules.MissDivergence(false), 3);

    [Fact]
    public void Miss_early_is_negative_extent()
        => Assert.Equal(-100f, SignedDivergenceRules.MissDivergence(true), 3);

    [Theory]
    [InlineData(40f, true, 10f, 10.5f, -60f)]
    [InlineData(0f, true, 10f, 10.25f, -25f)]
    [InlineData(0f, false, 10.5f, 10f, 50f)]
    public void ForFailure_uses_rating_or_beats(
        float ratingPercent,
        bool wasEarly,
        float inputBeat,
        float targetBeat,
        float expected)
        => Assert.Equal(
            expected,
            SignedDivergenceRules.ForFailure(ratingPercent, wasEarly, inputBeat, targetBeat),
            3);
}
