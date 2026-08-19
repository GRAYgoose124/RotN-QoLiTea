namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// Guards disk/memory cache against bad full-loader results (e.g. empty after quit-stage).
/// </summary>
public static class CacheReconcilePolicy
{
    /// <summary>
    /// Whether a completed stock UpdateTrackList result should replace the cache / drive UI.
    /// Never accept an empty reconcile when we already have a non-empty cache.
    /// </summary>
    public static bool ShouldAcceptReconcile(int cachedCount, int freshCount)
    {
        if (freshCount < 0)
            freshCount = 0;
        if (cachedCount < 0)
            cachedCount = 0;

        if (freshCount == 0 && cachedCount > 0)
            return false;

        return true;
    }
}
