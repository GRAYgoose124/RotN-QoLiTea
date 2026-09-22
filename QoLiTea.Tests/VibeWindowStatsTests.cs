using System.Collections.Generic;
using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class VibeWindowStatsTests
{
    [Fact]
    public void Compute_counts_hits_and_first_last_enemy_names_in_span()
    {
        var spans = new[] { new ChartBeatSpan(10f, 20f) };
        var hits = new List<HitDivergenceSample>
        {
            new(8f, 0f, PlotHitRating.Perfect, enemyDisplayName: "Skip"),
            new(12f, 0.1f, PlotHitRating.Good, enemyDisplayName: "Skeleton"),
            new(15f, -0.1f, PlotHitRating.Great, enemyDisplayName: "Zombie"),
            new(18f, 0f, PlotHitRating.Perfect, enemyDisplayName: "Bat"),
            new(22f, 0f, PlotHitRating.Miss, enemyDisplayName: "After"),
        };

        var stats = VibeWindowStats.Compute(spans, hits);
        Assert.Single(stats);
        Assert.Equal(3, stats[0].HitCount);
        Assert.Equal("Skeleton", stats[0].FirstEnemyName);
        Assert.Equal("Bat", stats[0].LastEnemyName);
    }

    [Fact]
    public void Compute_empty_when_no_spans()
    {
        Assert.Empty(VibeWindowStats.Compute(null, new List<HitDivergenceSample>()));
        Assert.Empty(VibeWindowStats.Compute(System.Array.Empty<ChartBeatSpan>(), new List<HitDivergenceSample>()));
    }
}
