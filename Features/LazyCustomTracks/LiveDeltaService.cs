using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shared.TrackData;
using Shared.TrackSelection;
using Shared.UGC.Local;
using Shared.UGC.Steam;
using Steamworks;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// Incremental add/remove of Workshop + local tracks while the Custom Music menu is open.
/// </summary>
public static class LiveDeltaService
{
    private static string _lastLocalFingerprint;
    private static HashSet<string> _lastKnownLevelIds = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> _awaitingWorkshopInstall =
        new HashSet<string>(StringComparer.Ordinal);

    public static bool HasAwaitingWorkshopInstalls => _awaitingWorkshopInstall.Count > 0;

    public static void ResetBaselineFromCache()
    {
        _lastLocalFingerprint = TrackListCache.ComputeLocalFingerprint();
        _lastKnownLevelIds = new HashSet<string>(
            TrackListCache.Snapshot().Where(t => t != null).Select(t => t.LevelId),
            StringComparer.Ordinal);
    }

    public static void ClearAwaitingWorkshopInstalls() => _awaitingWorkshopInstall.Clear();

    public static bool ApplyDeltas(CustomTracksSelectionSceneController controller)
    {
        if (controller == null)
            return false;

        bool changed = false;
        try
        {
            changed |= ApplyLocalDeltas();
            changed |= ApplyWorkshopDeltas();
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogError($"LiveDeltaService: delta failed: {e}");
            return false;
        }

        if (!changed)
            return false;

        // One disk write for the whole delta batch — not a full rewrite per +1 track.
        TrackListCache.SaveToDisk();

        var snapshot = TrackListCache.Snapshot();
        controller._customTrackMetadatas = snapshot;
        controller.FilterTrackMetadata(shouldRefreshTrackSelectionOptionGroup: true);
        ResetBaselineFromCache();
        Plugin.Logger?.LogInfo($"LiveDeltaService: applied deltas ({snapshot.Count} tracks)");
        return true;
    }

    public static bool LocalFingerprintChanged()
    {
        string current = TrackListCache.ComputeLocalFingerprint();
        if (string.Equals(current, _lastLocalFingerprint, StringComparison.Ordinal))
            return false;
        return true;
    }

    private static bool ApplyLocalDeltas()
    {
        string basePath = null;
        string prefix = null;
        try
        {
            basePath = LocalUgcTrackProvider.BasePath;
            prefix = LocalUgcTrackProvider.Instance?.LevelIdPrefix;
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
        {
            // Folder missing/unreadable (can happen briefly around scene changes) —
            // do not wipe cached locals; reconcile will correct if truly gone.
            Plugin.Logger?.LogWarning("LiveDeltaService: local CustomTracks folder missing — skip local removals");
            return false;
        }

        if (string.IsNullOrEmpty(prefix))
            prefix = "local_"; // fallback; real prefix comes from provider when available

        var expectedLocal = new HashSet<string>(StringComparer.Ordinal);
        var folderByLevelId = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string dir in Directory.GetDirectories(basePath))
        {
            string name = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(name))
                continue;
            string levelId = prefix + name;
            expectedLocal.Add(levelId);
            folderByLevelId[levelId] = dir;
        }

        bool changed = RemoveMissingLocals(expectedLocal);

        // Locals are few; always re-read info.json so difficulty add/remove (Easy/Medium)
        // shows in folder filters without RunClearTrackListCache. Fingerprint is folder
        // mtime only — editing files inside often does not bump it on Windows.
        foreach (string levelId in expectedLocal)
        {
            if (!folderByLevelId.TryGetValue(levelId, out string folder))
                continue;

            var track = TrackHydrator.TryHydrateLocalFolder(folder, levelId);
            if (track == null)
                continue;

            bool isNew = !_lastKnownLevelIds.Contains(levelId)
                         && !TrackListCache.Snapshot().Any(t => t != null && t.LevelId == levelId);
            string before = TrackListCache.DifficultySignatureFor(levelId);
            string after = TrackListCache.FormatDifficultySignature(track);
            if (!isNew && string.Equals(before, after, StringComparison.Ordinal))
                continue;

            TrackListCache.Upsert(track, saveDisk: false);
            changed = true;
            Plugin.Logger?.LogInfo(
                isNew
                    ? $"LiveDeltaService: +local {levelId}"
                    : $"LiveDeltaService: refresh local {levelId}");
        }

        _lastLocalFingerprint = TrackListCache.ComputeLocalFingerprint();
        return changed;
    }

    private static bool RemoveMissingLocals(HashSet<string> expectedLocal)
    {
        bool changed = false;
        foreach (var track in TrackListCache.Snapshot())
        {
            if (track == null || string.IsNullOrEmpty(track.LevelId))
                continue;

            bool isLocal = track.Category == TrackCategory.UgcLocal
                           || (track is CachedTrackMetadata stub && stub.Dto.Kind == "local");
            if (!isLocal)
                continue;

            if (expectedLocal.Contains(track.LevelId))
                continue;

            if (TrackListCache.Remove(track.LevelId, saveDisk: false))
            {
                changed = true;
                Plugin.Logger?.LogInfo($"LiveDeltaService: -local {track.LevelId}");
            }
        }

        return changed;
    }

    private static bool ApplyWorkshopDeltas()
    {
        if (!SteamWorkshopUgcTrackProvider.Available || SteamWorkshopUgcTrackProvider.Instance == null)
            return false;

        var subscribed = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        bool enumerationSucceeded;
        try
        {
            uint count = SteamUGC.GetNumSubscribedItems();
            var ids = count == 0 ? Array.Empty<PublishedFileId_t>() : new PublishedFileId_t[count];
            uint written = count == 0 ? 0u : SteamUGC.GetSubscribedItems(ids, count);
            for (uint i = 0; i < written; i++)
            {
                var fileId = ids[i];
                string levelId = SteamWorkshopUgcTrackProvider.FileIdToLevelId(fileId);
                if (string.IsNullOrEmpty(levelId))
                    continue;

                subscribed.Add(levelId);

                if (SteamWorkshopUgcTrackMetadata.TryResolveLocalPath(fileId, out string pchFolder)
                    && !string.IsNullOrEmpty(pchFolder))
                {
                    resolved.Add(levelId);
                }
            }

            enumerationSucceeded = true;
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"LiveDeltaService: workshop enumerate failed: {e.Message}");
            return false;
        }

        var cachedWorkshopIds = TrackListCache.Snapshot()
            .Where(IsWorkshopTrack)
            .Select(t => t.LevelId);

        bool changed = false;
        foreach (string levelId in WorkshopDeltaPolicy.WorkshopLevelIdsToRemove(
                     cachedWorkshopIds, subscribed, enumerationSucceeded))
        {
            if (TrackListCache.Remove(levelId, saveDisk: false))
            {
                changed = true;
                Plugin.Logger?.LogInfo($"LiveDeltaService: -workshop {levelId}");
            }
        }

        foreach (string levelId in WorkshopDeltaPolicy.WorkshopLevelIdsToAdd(
                     _lastKnownLevelIds, subscribed, resolved))
        {
            if (TrackListCache.Snapshot().Any(t => t != null && t.LevelId == levelId))
                continue;

            var track = TrackHydrator.TryHydrate(levelId);
            if (track == null)
                continue;

            TrackListCache.Upsert(track, saveDisk: false);
            changed = true;
            _awaitingWorkshopInstall.Remove(levelId);
            Plugin.Logger?.LogInfo($"LiveDeltaService: +workshop {levelId}");
        }

        var presentIds = TrackListCache.Snapshot()
            .Where(t => t != null)
            .Select(t => t.LevelId);
        var awaiting = WorkshopDeltaPolicy.WorkshopLevelIdsAwaitingInstall(
            subscribed, resolved, presentIds);
        _awaitingWorkshopInstall.Clear();
        foreach (string levelId in awaiting)
            _awaitingWorkshopInstall.Add(levelId);

        if (_awaitingWorkshopInstall.Count > 0)
        {
            Plugin.Logger?.LogInfo(
                $"LiveDeltaService: waiting for {_awaitingWorkshopInstall.Count} workshop install(s) to resolve");
        }

        return changed;
    }

    /// <summary>
    /// After Steam subscribe/unsubscribe outside the Custom Music menu (e.g. Set Subscriber).
    /// Updates cache from current Steam subscription set; refreshes open menu if any.
    /// </summary>
    public static void SyncWorkshopSubscriptionsToCache()
    {
        if (!Plugin.IsLazyCustomTracksActive)
            return;

        bool changed = ApplyWorkshopDeltas();
        if (changed)
            TrackListCache.SaveToDisk();
        ResetBaselineFromCache();
        LazyTrackListPatches.RefreshOpenCustomMusicIfAny();
    }

    private static bool IsWorkshopTrack(ITrackMetadata track)
    {
        if (track == null || string.IsNullOrEmpty(track.LevelId))
            return false;

        return track.Category == TrackCategory.UgcRemote
               || track is SteamWorkshopUgcTrackMetadata
               || (track is CachedTrackMetadata stub && stub.Dto.Kind == "workshop");
    }
}
