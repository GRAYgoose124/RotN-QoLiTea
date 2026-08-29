using QoLiTea.Features.WorstSectionPractice;
using Xunit;

namespace QoLiTea.Tests;

public class PracticeSectionEnemyCullTests
{
    [Theory]
    [InlineData(100f, 100f, 120f, true)]
    [InlineData(120f, 100f, 120f, true)]
    [InlineData(99.9f, 100f, 120f, false)]
    [InlineData(120.1f, 100f, 120f, false)]
    public void IsInSection_by_hit_beat(float hit, float start, float end, bool expected)
        => Assert.Equal(expected, PracticeSectionEnemyCull.IsInSection(hit, start, end));

    [Theory]
    [InlineData(92f, 92f, 120f, true)]
    [InlineData(91f, 92f, 120f, false)]
    [InlineData(120f, 92f, 120f, true)]
    [InlineData(121f, 92f, 120f, false)]
    public void Queued_spawn_keepable_window(float spawn, float warm, float end, bool expected)
        => Assert.Equal(expected, PracticeSectionEnemyCull.IsQueuedSpawnKeepable(spawn, warm, end));
}
