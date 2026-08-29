using QoLiTea.Features.ResultsDivergence;
using QoLiTea.Features.WorstSectionPractice;
using Xunit;

namespace QoLiTea.Tests;

public class ResultsFeaturePolicyTests
{
    [Fact]
    public void Harvest_gate_requires_master_and_either_feature()
    {
        Assert.False(ResultsDivergencePolicy.ShouldHarvest(false, true, true));
        Assert.False(ResultsDivergencePolicy.ShouldHarvest(true, false, false));
        Assert.True(ResultsDivergencePolicy.ShouldHarvest(true, true, false));
        Assert.True(ResultsDivergencePolicy.ShouldHarvest(true, false, true));
        Assert.True(ResultsDivergencePolicy.ShouldHarvest(true, true, true));
    }

    [Fact]
    public void Plot_gate_requires_master_and_feature()
    {
        Assert.False(ResultsDivergencePolicy.ShouldShowPlot(false, true));
        Assert.False(ResultsDivergencePolicy.ShouldShowPlot(true, false));
        Assert.True(ResultsDivergencePolicy.ShouldShowPlot(true, true));
    }

    [Theory]
    [InlineData("80", 80)]
    [InlineData("100", 100)]
    [InlineData("0", 0)]
    [InlineData("", 80)]
    [InlineData("nope", 80)]
    [InlineData("150", 100)]
    public void Plot_opacity_parses_and_clamps(string raw, int expected)
        => Assert.Equal(expected, ResultsDivergencePolicy.ParsePlotOpacityPercent(raw));

    [Theory]
    [InlineData(80, 0.8f)]
    [InlineData(100, 1f)]
    [InlineData(0, 0f)]
    public void Plot_opacity_to_alpha(int percent, float expected)
        => Assert.Equal(expected, ResultsDivergencePolicy.ToAlpha(percent), 3);

    [Fact]
    public void Practice_gate_requires_spans_and_stock_flag()
    {
        Assert.False(WorstSectionPracticePolicy.ShouldOfferPractice(true, true, 0, true));
        Assert.False(WorstSectionPracticePolicy.ShouldOfferPractice(true, true, 2, false));
        Assert.True(WorstSectionPracticePolicy.ShouldOfferPractice(true, true, 2, true));
    }
}
