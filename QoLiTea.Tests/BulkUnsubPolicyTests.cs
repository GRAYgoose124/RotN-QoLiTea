using System.Collections.Generic;
using System.Linq;
using QoLiTea.Features.TrackSets;
using Xunit;

namespace QoLiTea.Tests;

public class BulkUnsubPolicyTests
{
    [Fact]
    public void Under_cap_removes_nothing()
    {
        var tracks = new[]
        {
            new BulkUnsubCandidate(1, false, 5, 100),
            new BulkUnsubCandidate(2, false, 3, 50),
        };

        var sel = BulkUnsubPolicy.SelectRemovals(tracks, maxKeep: 10);

        Assert.Empty(sel.FileIdsToRemove);
        Assert.False(sel.StillOverCap);
        Assert.Equal(2, sel.SubscribedCount);
    }

    [Fact]
    public void Removes_oldest_then_fewest_plays()
    {
        var tracks = new[]
        {
            new BulkUnsubCandidate(1, false, 10, 1000), // recent, many plays
            new BulkUnsubCandidate(2, false, 2, 100),   // oldest
            new BulkUnsubCandidate(3, false, 1, 100),   // same age, fewer plays — first
            new BulkUnsubCandidate(4, false, 5, 500),
        };

        // 4 subscribed, keep 2 → remove 2
        var sel = BulkUnsubPolicy.SelectRemovals(tracks, maxKeep: 2);

        Assert.Equal(new ulong[] { 3, 2 }, sel.FileIdsToRemove.ToArray());
        Assert.False(sel.StillOverCap);
    }

    [Fact]
    public void Never_removes_favorites_or_zero_play()
    {
        var tracks = new[]
        {
            new BulkUnsubCandidate(1, true, 1, 10),   // favorite
            new BulkUnsubCandidate(2, false, 0, 10),  // zero play
            new BulkUnsubCandidate(3, false, 2, 10),  // only eligible
            new BulkUnsubCandidate(4, false, 3, 20),
        };

        // 4 subscribed, keep 1 → need 3 removals but only 2 eligible
        var sel = BulkUnsubPolicy.SelectRemovals(tracks, maxKeep: 1);

        Assert.Equal(new ulong[] { 3, 4 }, sel.FileIdsToRemove.ToArray());
        Assert.Equal(1, sel.ProtectedFavoriteCount);
        Assert.Equal(1, sel.ProtectedZeroPlayCount);
        Assert.True(sel.StillOverCap);
    }

    [Fact]
    public void Favorites_alone_over_cap_removes_nothing_eligible()
    {
        var tracks = new[]
        {
            new BulkUnsubCandidate(1, true, 5, 100),
            new BulkUnsubCandidate(2, true, 5, 50),
        };

        var sel = BulkUnsubPolicy.SelectRemovals(tracks, maxKeep: 1);

        Assert.Empty(sel.FileIdsToRemove);
        Assert.True(sel.StillOverCap);
        Assert.Equal(2, sel.ProtectedFavoriteCount);
    }
}
