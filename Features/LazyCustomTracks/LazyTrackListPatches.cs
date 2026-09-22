using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using Shared.TrackData;
using Shared.TrackSelection;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// Unblocks Custom Music Start via disk/memory cache.
/// Cold open: one full stock reconcile. Warm open / Workshop changes: live deltas only.
/// </summary>
[HarmonyPatch]
public static class LazyTrackListPatches
{
    private static bool _passthrough;
    private static CustomTracksSelectionSceneController _activeController;
    private static CustomTracksSelectionSceneController _pendingUiRefresh;
    private static CustomTracksSelectionSceneController _reconcileOwner;
    private static bool _pendingStoreCache;
    private static bool _opened;
    private static bool _liveDeltaPending;
    private static bool _warmDeltaPending;
    private static bool _cacheDirty;
    private static float _nextAwaitingWorkshopPollRealtime;

    private const float AwaitingWorkshopPollSeconds = 0.5f;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.UpdateTrackList))]
    public static bool UpdateTrackListPrefix(
        CustomTracksSelectionSceneController __instance,
        bool fetchRemote,
        ref Task __result)
    {
        if (_passthrough || !Plugin.IsLazyCustomTracksActive)
            return true;

        // Live dirty path: stock Update cleared _trackQueryNeeded into UpdateTrackList —
        // treat as delta, never full scan.
        if (_opened && _liveDeltaPending)
        {
            _liveDeltaPending = false;
            __result = Task.CompletedTask;
            if (LiveDeltaService.ApplyDeltas(__instance))
                _cacheDirty = true;
            return false;
        }

        // Empty list is not usable cache — must full-reconcile (TryGet is true for []).
        bool hasCache = TrackListCache.TryGet(out List<ITrackMetadata> cached)
            && cached != null
            && cached.Count > 0;
        bool needsFolderBackfill = TrackListCache.NeedsFolderFieldBackfill;

        if (hasCache)
        {
            __instance._customTrackMetadatas = cached;
            Plugin.Logger.LogInfo(
                needsFolderBackfill
                    ? $"LazyCustomTracks: serving {cached.Count} cached tracks — folder fields missing, full reconcile"
                    : $"LazyCustomTracks: serving {cached.Count} cached tracks (delta sync, no full scan)");
        }
        else
        {
            __instance._customTrackMetadatas ??= new List<ITrackMetadata>();
            Plugin.Logger.LogInfo("LazyCustomTracks: cold open — empty list, reconcile starting");
        }

        __result = Task.CompletedTask;
        _opened = true;
        _activeController = __instance;
        LiveDeltaService.ResetBaselineFromCache();

        if (ReconcileKickPolicy.ShouldKickImmediateFullReconcile(hasCache, needsFolderBackfill))
        {
            KickReconcile(__instance, fetchRemote);
        }
        else if (ReconcileKickPolicy.ShouldSyncDeltasOnWarmOpen(hasCache, needsFolderBackfill))
        {
            // Next Update: cheap +1/-1 Workshop/local sync (not QueryAllTracks).
            _warmDeltaPending = true;
        }

        return false;
    }

    private static void KickReconcile(CustomTracksSelectionSceneController instance, bool fetchRemote)
    {
        Task existing = instance._trackQueryTask;
        if (existing != null && !existing.IsCompleted)
        {
            Plugin.Logger.LogInfo("LazyCustomTracks: reconcile already running");
            return;
        }

        _reconcileOwner = instance;
        _passthrough = true;
        Task realTask;
        try
        {
            realTask = instance.UpdateTrackList(fetchRemote);
        }
        catch (Exception e)
        {
            _passthrough = false;
            _reconcileOwner = null;
            Plugin.Logger.LogError($"LazyCustomTracks: failed to start reconcile UpdateTrackList: {e}");
            return;
        }
        _passthrough = false;

        instance._trackQueryTask = realTask;
        var owner = instance;
        realTask.ContinueWith(t =>
        {
            if (!_opened || !ReferenceEquals(_reconcileOwner, owner) || owner == null)
            {
                Plugin.Logger.LogInfo("LazyCustomTracks: discard reconcile (menu closed or superseded)");
                return;
            }

            if (t.IsFaulted)
            {
                Plugin.Logger.LogError($"LazyCustomTracks: reconcile failed: {t.Exception}");
                return;
            }

            _pendingStoreCache = true;
            _pendingUiRefresh = owner;
        }, TaskContinuationOptions.ExecuteSynchronously);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.Update))]
    public static void UpdatePostfix(CustomTracksSelectionSceneController __instance)
    {
        _activeController = __instance;

        if (_opened && Plugin.IsLazyCustomTracksActive && _warmDeltaPending)
        {
            _warmDeltaPending = false;
            if (LiveDeltaService.ApplyDeltas(__instance))
            {
                _cacheDirty = true;
                Plugin.Logger.LogInfo("LazyCustomTracks: warm-open delta sync applied");
            }
        }

        // Workshop subscribe often fires before the install path exists — keep polling
        // (workshop-only: skip local info.json re-read that hitchs the menu).
        if (_opened && Plugin.IsLazyCustomTracksActive
            && LiveDeltaService.HasAwaitingWorkshopInstalls
            && UnityEngine.Time.realtimeSinceStartup >= _nextAwaitingWorkshopPollRealtime)
        {
            _nextAwaitingWorkshopPollRealtime =
                UnityEngine.Time.realtimeSinceStartup + AwaitingWorkshopPollSeconds;
            if (LiveDeltaService.PollAwaitingWorkshopInstalls(__instance))
                _cacheDirty = true;
        }

        // Local folder changes while open → live delta (no full loader).
        if (_opened && Plugin.IsLazyCustomTracksActive
            && LiveDeltaService.LocalFingerprintChanged())
        {
            if (LiveDeltaService.ApplyDeltas(__instance))
                _cacheDirty = true;
        }

        if (_pendingUiRefresh == null)
            _pendingUiRefresh = null;

        if (_pendingUiRefresh == null && !_pendingStoreCache)
            return;

        if (_pendingUiRefresh != null && _pendingUiRefresh != __instance)
            return;

        if (!_opened || __instance == null)
        {
            _pendingStoreCache = false;
            _pendingUiRefresh = null;
            return;
        }

        try
        {
            if (_pendingStoreCache && __instance._customTrackMetadatas != null)
            {
                var fresh = __instance._customTrackMetadatas;
                int cachedCount = TrackListCache.Snapshot().Count;

                if (!CacheReconcilePolicy.ShouldAcceptReconcile(cachedCount, fresh.Count))
                {
                    Plugin.Logger.LogWarning(
                        $"LazyCustomTracks: ignoring empty reconcile ({fresh.Count}) over cache ({cachedCount})");
                    if (TrackListCache.TryGet(out var cached))
                        __instance._customTrackMetadatas = cached;
                }
                else if (TrackListCache.DivergesFrom(fresh))
                {
                    TrackListCache.Store(fresh, saveDisk: true);
                    _cacheDirty = false;
                    Plugin.Logger.LogInfo(
                        $"LazyCustomTracks: reconcile diverged — cache replaced ({fresh.Count} tracks)");
                }
                else
                {
                    Plugin.Logger.LogInfo("LazyCustomTracks: reconcile matched cache — disk unchanged");
                }

                _pendingStoreCache = false;
                LiveDeltaService.ResetBaselineFromCache();
            }

            if (_pendingUiRefresh == __instance)
            {
                __instance.FilterTrackMetadata(shouldRefreshTrackSelectionOptionGroup: true);
                Plugin.Logger.LogInfo(
                    $"LazyCustomTracks: UI refreshed ({__instance._customTrackMetadatas?.Count ?? 0} tracks)");
                _pendingUiRefresh = null;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"LazyCustomTracks: UI refresh failed: {e}");
            _pendingUiRefresh = null;
            _pendingStoreCache = false;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.Update))]
    public static void UpdatePrefix(CustomTracksSelectionSceneController __instance)
    {
        if (!Plugin.IsLazyCustomTracksActive || !_opened)
            return;

        if (__instance._trackQueryNeeded)
        {
            __instance._trackQueryNeeded = false;
            _liveDeltaPending = false;
            // Always run deltas on the scheduled update; may only arm awaiting-install poll.
            if (LiveDeltaService.ApplyDeltas(__instance))
                _cacheDirty = false; // ApplyDeltas already saved
            if (LiveDeltaService.HasAwaitingWorkshopInstalls)
            {
                _nextAwaitingWorkshopPollRealtime = 0f;
                Plugin.Logger.LogInfo("LazyCustomTracks: workshop update scheduled — polling installs");
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.FlagTrackQueryNeeded))]
    public static void FlagTrackQueryNeededPostfix()
    {
        if (!_opened)
            return;

        _liveDeltaPending = true;

        // Don't wait for next stock Update path — subscription callbacks can arrive mid-frame.
        if (_activeController != null)
        {
            if (LiveDeltaService.ApplyDeltas(_activeController))
                _cacheDirty = false;
            if (LiveDeltaService.HasAwaitingWorkshopInstalls)
                _nextAwaitingWorkshopPollRealtime = 0f;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.HandleTrackMetadataReSort))]
    public static bool HandleTrackMetadataReSortPrefix(CustomTracksSelectionSceneController __instance)
    {
        bool modEnabled = Plugin.IsLazyCustomTracksActive;
        bool isActive = __instance != null && ReferenceEquals(__instance, _activeController);
        return ReSortGatePolicy.ShouldRunStockReSort(modEnabled, _opened, isActive);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), "OnDestroy")]
    public static void OnDestroyPrefix(CustomTracksSelectionSceneController __instance)
    {
        try
        {
            if (__instance._customTrackMetadatas != null && __instance._customTrackMetadatas.Count > 0)
            {
                if (_cacheDirty || TrackListCache.DivergesFrom(__instance._customTrackMetadatas))
                {
                    TrackListCache.Store(__instance._customTrackMetadatas, saveDisk: true);
                    _cacheDirty = false;
                }
            }
            else if (_cacheDirty)
            {
                TrackListCache.SaveToDisk();
                _cacheDirty = false;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogError($"LazyCustomTracks: close save failed: {e}");
        }

        if (_activeController == __instance)
            _activeController = null;
        if (_pendingUiRefresh == __instance)
            _pendingUiRefresh = null;
        if (ReferenceEquals(_reconcileOwner, __instance))
            _reconcileOwner = null;

        _opened = false;
        _liveDeltaPending = false;
        _warmDeltaPending = false;
        _pendingStoreCache = false;
        LiveDeltaService.ClearAwaitingWorkshopInstalls();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.HandleTrackSelected))]
    public static void HandleTrackSelectedPrefix(CustomTracksSelectionSceneController __instance, int trackIndex)
    {
        TryHydrateDisplayed(__instance, trackIndex);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.HandleTrackSubmitted))]
    public static void HandleTrackSubmittedPrefix(CustomTracksSelectionSceneController __instance, string levelId)
    {
        TryHydrateByLevelId(__instance, levelId);
    }

    private static void TryHydrateDisplayed(CustomTracksSelectionSceneController controller, int trackIndex)
    {
        try
        {
            var displayed = controller._displayedTrackMetaDatas;
            if (displayed == null || trackIndex < 0 || trackIndex >= displayed.Length)
                return;

            var current = displayed[trackIndex];
            if (current == null)
                return;

            TryHydrateByLevelId(controller, current.LevelId);
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"LazyCustomTracks: select hydrate failed: {e.Message}");
        }
    }

    private static void TryHydrateByLevelId(CustomTracksSelectionSceneController controller, string levelId)
    {
        if (string.IsNullOrEmpty(levelId) || controller == null)
            return;

        try
        {
            var list = controller._customTrackMetadatas;
            if (list == null)
                return;

            int idx = list.FindIndex(t => t != null && t.LevelId == levelId);
            if (idx < 0)
                return;

            // Always re-read from disk/provider. Skipping non-stubs left Hard/Impossible
            // stuck after info.json gained Easy/Medium (hydrate ran once, then never again).
            var hydrated = TrackHydrator.TryHydrate(levelId);
            if (hydrated == null)
            {
                Plugin.Logger?.LogWarning($"LazyCustomTracks: could not hydrate {levelId}");
                return;
            }

            list[idx] = hydrated;
            TrackListCache.Upsert(hydrated, saveDisk: false);
            _cacheDirty = true;

            var displayed = controller._displayedTrackMetaDatas;
            if (displayed != null)
            {
                for (int i = 0; i < displayed.Length; i++)
                {
                    if (displayed[i] != null && displayed[i].LevelId == levelId)
                        displayed[i] = hydrated;
                }
            }

            Plugin.Logger?.LogInfo($"LazyCustomTracks: hydrated {levelId} for play");
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"LazyCustomTracks: hydrate by levelId failed: {e.Message}");
        }
    }

  internal static void RefreshOpenCustomMusicIfAny()
    {
        if (_activeController == null || !_opened || !Plugin.IsLazyCustomTracksActive)
            return;

        if (LiveDeltaService.ApplyDeltas(_activeController))
        {
            _cacheDirty = true;
            Plugin.Logger.LogInfo("LazyCustomTracks: subscription sync applied to open menu");
        }
    }

    /// <summary>One-shot clear: wipe cache; full reconcile if Custom Music is open.</summary>
    public static void ApplyClearCacheAndRefresh()
    {
        if (!Plugin.IsLazyCustomTracksActive)
            return;

        TrackListCache.Clear();
        LiveDeltaService.ClearAwaitingWorkshopInstalls();
        LiveDeltaService.ResetBaselineFromCache();
        _cacheDirty = false;

        if (_activeController != null && _opened)
        {
            _activeController._customTrackMetadatas = new List<ITrackMetadata>();
            _activeController.FilterTrackMetadata(shouldRefreshTrackSelectionOptionGroup: true);
            KickReconcile(_activeController, fetchRemote: true);
            Plugin.Logger.LogInfo("LazyCustomTracks: cache cleared — reconcile started");
        }
        else
        {
            Plugin.Logger.LogInfo("LazyCustomTracks: cache cleared (next Custom Music open is cold)");
        }
    }
}
