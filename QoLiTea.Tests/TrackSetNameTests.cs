using System;
using System.Collections.Generic;
using QoLiTea.Features.TrackSets;
using Xunit;

namespace QoLiTea.Tests;

public class TrackSetNameTests
{
    [Fact]
    public void Format_bulk_and_noimp_prefixes()
    {
        var utc = new DateTime(2026, 7, 26, 23, 10, 0, DateTimeKind.Utc);
        Assert.Equal("unsub-2026-07-26-2310", TrackSetName.FormatBulk(utc));
        Assert.Equal("unsub-noimp-2026-07-26-2310", TrackSetName.FormatNoImpossible(utc));
    }

    [Fact]
    public void EnsureUnique_appends_suffix_on_collision()
    {
        var existing = new List<string> { "unsub-2026-07-26-2310" };
        Assert.Equal(
            "unsub-2026-07-26-2310-2",
            TrackSetName.EnsureUnique("unsub-2026-07-26-2310", existing));
    }

    [Fact]
    public void EnsureUnique_returns_desired_when_free()
    {
        Assert.Equal("unsub-a", TrackSetName.EnsureUnique("unsub-a", Array.Empty<string>()));
    }
}
