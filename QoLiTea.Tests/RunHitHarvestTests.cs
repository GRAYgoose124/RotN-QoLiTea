using System.Collections.Generic;
using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class RunHitHarvestTests
{
    [Fact]
    public void Joins_perfect_and_miss()
    {
        var rows = new[]
        {
            new HarvestTestRow(10f, 100f, false, PlotHitRating.Perfect),
            new HarvestTestRow(11f, 0f, true, PlotHitRating.Miss),
        };
        var hits = RunHitHarvest.FromTestRows(rows);
        Assert.Equal(2, hits.Count);
        Assert.Equal(0f, hits[0].SignedDivergence, 3);
        Assert.Equal(PlotHitRating.Perfect, hits[0].Rating);
        Assert.Equal(-100f, hits[1].SignedDivergence, 3);
        Assert.Equal(PlotHitRating.Miss, hits[1].Rating);
    }

    [Fact]
    public void Skips_excluded_rows()
    {
        var rows = new[]
        {
            new HarvestTestRow(10f, 90f, true, PlotHitRating.Great, include: false),
            new HarvestTestRow(11f, 90f, false, PlotHitRating.Great),
        };
        var hits = RunHitHarvest.FromTestRows(rows);
        Assert.Single(hits);
        Assert.Equal(10f, hits[0].SignedDivergence, 3);
        Assert.Equal(PlotHitRating.Great, hits[0].Rating);
    }

    [Fact]
    public void Preserves_good_ok_great_ratings()
    {
        var rows = new[]
        {
            new HarvestTestRow(1f, 70f, false, PlotHitRating.Good),
            new HarvestTestRow(2f, 40f, true, PlotHitRating.Ok),
            new HarvestTestRow(3f, 85f, false, PlotHitRating.Great),
        };
        var hits = RunHitHarvest.FromTestRows(rows);
        Assert.Equal(PlotHitRating.Good, hits[0].Rating);
        Assert.Equal(PlotHitRating.Ok, hits[1].Rating);
        Assert.Equal(PlotHitRating.Great, hits[2].Rating);
    }

    [Fact]
    public void Combo_break_row_is_bad_not_perfect()
    {
        var hits = RunHitHarvest.FromTestRows(new[]
        {
            new HarvestTestRow(20f, 0f, true, PlotHitRating.ComboBreak),
        });
        Assert.Equal(PlotHitRating.ComboBreak, hits[0].Rating);
        Assert.True(hits[0].IsBad);
        Assert.False(hits[0].IsPerfect);
        Assert.Equal(-100f, hits[0].SignedDivergence, 3);
    }
}
