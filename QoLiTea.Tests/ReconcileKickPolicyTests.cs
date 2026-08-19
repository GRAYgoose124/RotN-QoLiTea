using QoLiTea.Features.LazyCustomTracks;
using Xunit;

namespace QoLiTea.Tests;

public class ReconcileKickPolicyTests
{
    [Fact]
    public void Warm_cache_does_not_kick_full_reconcile()
    {
        Assert.False(ReconcileKickPolicy.ShouldKickImmediateFullReconcile(hasCache: true));
    }

    [Fact]
    public void Cold_open_kicks_full_reconcile()
    {
        Assert.True(ReconcileKickPolicy.ShouldKickImmediateFullReconcile(hasCache: false));
    }

    [Fact]
    public void Warm_open_uses_delta_sync_not_full_scan()
    {
        Assert.True(ReconcileKickPolicy.ShouldSyncDeltasOnWarmOpen(hasCache: true));
        Assert.False(ReconcileKickPolicy.ShouldSyncDeltasOnWarmOpen(hasCache: false));
    }

    [Fact]
    public void Empty_cache_is_treated_as_cold_open()
    {
        // Callers must pass HasTracks (non-empty), not mere TryGet success on [].
        Assert.True(ReconcileKickPolicy.ShouldKickImmediateFullReconcile(hasCache: false));
        Assert.False(ReconcileKickPolicy.ShouldSyncDeltasOnWarmOpen(hasCache: false));
    }
}
