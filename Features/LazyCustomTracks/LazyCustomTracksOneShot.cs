using System;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>Watch NecroManager one-shot bools for Lazy Custom Tracks.</summary>
public static class LazyCustomTracksOneShot
{
    private static bool _clearPrev;
    private static bool _primed;

    public static void Tick()
    {
        try
        {
            TickCore();
        }
        catch (InvalidOperationException)
        {
            // Setting not bound yet.
        }
    }

    private static void TickCore()
    {
        if (!Plugin.IsLazyCustomTracksActive)
        {
            SyncPrev();
            return;
        }

        bool clear = Plugin.RunClearTrackListCache.Entry.Value;

        if (!_primed)
        {
            _clearPrev = clear;
            _primed = true;
            if (clear)
                Plugin.RunClearTrackListCache.Entry.Value = false;
            SyncPrev();
            return;
        }

        if (clear && !_clearPrev)
        {
            Plugin.RunClearTrackListCache.Entry.Value = false;
            LazyTrackListPatches.ApplyClearCacheAndRefresh();
        }

        SyncPrev();
    }

    private static void SyncPrev()
    {
        _clearPrev = Plugin.RunClearTrackListCache.Entry.Value;
    }
}
