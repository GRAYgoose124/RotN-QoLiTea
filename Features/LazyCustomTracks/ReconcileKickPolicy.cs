namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// When to start stock UpdateTrackList (full UGC info.json scan).
/// Warm cache must never kick that — use live deltas for +1/-1 Workshop/local changes.
/// </summary>
public static class ReconcileKickPolicy
{
    public static bool ShouldKickImmediateFullReconcile(bool hasCache) => !hasCache;

    /// <summary>
    /// Warm opens sync with a cheap subscribed/folder delta pass instead of a full scan.
    /// </summary>
    public static bool ShouldSyncDeltasOnWarmOpen(bool hasCache) => hasCache;
}
