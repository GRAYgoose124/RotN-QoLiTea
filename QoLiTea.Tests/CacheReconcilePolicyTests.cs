using QoLiTea.Features.LazyCustomTracks;
using Xunit;

namespace QoLiTea.Tests;

public class CacheReconcilePolicyTests
{
    [Fact]
    public void Rejects_empty_reconcile_when_cache_has_tracks()
    {
        Assert.False(CacheReconcilePolicy.ShouldAcceptReconcile(
            cachedCount: 40,
            freshCount: 0));
    }

    [Fact]
    public void Accepts_non_empty_reconcile_when_cache_empty()
    {
        Assert.True(CacheReconcilePolicy.ShouldAcceptReconcile(
            cachedCount: 0,
            freshCount: 12));
    }

    [Fact]
    public void Accepts_matching_non_empty_reconcile()
    {
        Assert.True(CacheReconcilePolicy.ShouldAcceptReconcile(
            cachedCount: 12,
            freshCount: 12));
    }

    [Fact]
    public void Accepts_reconcile_that_grows_or_shrinks_but_stays_non_empty()
    {
        Assert.True(CacheReconcilePolicy.ShouldAcceptReconcile(
            cachedCount: 40,
            freshCount: 38));
        Assert.True(CacheReconcilePolicy.ShouldAcceptReconcile(
            cachedCount: 40,
            freshCount: 41));
    }

    [Fact]
    public void Accepts_empty_when_cache_already_empty()
    {
        Assert.True(CacheReconcilePolicy.ShouldAcceptReconcile(
            cachedCount: 0,
            freshCount: 0));
    }
}
