using System;
using System.Collections.Generic;
using System.Text;
using QoLiTea.Features.LazyCustomTracks;
using UnityEngine;

namespace QoLiTea.Features.TrackSets;

public static class BulkUnsubController
{
    private static UnsubConfirmOverlay _overlay;
    private static bool _busy;

    public static void TryRunFromSetting()
    {
        if (_busy || (_overlay != null && _overlay.IsOpen))
            return;

        if (!Plugin.IsBulkUnsubscriberActive)
            return;

        _busy = true;
        try
        {
            TrackSetStore.LoadFromDisk();
            List<ulong> subscribed = WorkshopSubActions.GetSubscribedFileIds();
            var candidates = new List<BulkUnsubCandidate>(subscribed.Count);
            foreach (ulong fileId in subscribed)
            {
                TrackPlayStats.GetAggregates(fileId, out int runs, out long lastPlayed);
                candidates.Add(new BulkUnsubCandidate(
                    fileId,
                    TrackPlayStats.IsFavorite(fileId),
                    runs,
                    lastPlayed));
            }

            int maxKeep = Plugin.MaxSubscribedTracksValue;
            BulkUnsubSelection sel = BulkUnsubPolicy.SelectRemovals(candidates, maxKeep);

            string setName = TrackSetName.EnsureUnique(
                TrackSetName.FormatBulk(DateTime.UtcNow),
                TrackSetStore.SnapshotNames());

            var body = new StringBuilder();
            body.AppendLine($"Subscribed: {sel.SubscribedCount} · Cap: {sel.MaxKeep}");
            body.AppendLine($"Will unsub: {sel.FileIdsToRemove.Count}");
            body.AppendLine(
                $"Projected: {sel.SubscribedCount - sel.FileIdsToRemove.Count}");
            if (sel.ProtectedFavoriteCount > 0 || sel.ProtectedZeroPlayCount > 0)
            {
                body.AppendLine(
                    $"Protected — favorites: {sel.ProtectedFavoriteCount}, 0-play: {sel.ProtectedZeroPlayCount}");
            }

            if (sel.FileIdsToRemove.Count == 0)
            {
                if (sel.StillOverCap)
                    body.AppendLine("Still over cap (nothing eligible).");
                else
                    body.AppendLine("Already under cap.");
            }
            else if (sel.StillOverCap)
            {
                body.AppendLine("Warn: still over cap after this run.");
            }

            OpenOverlay(
                "Bulk Unsubscriber",
                body.ToString(),
                sel.FileIdsToRemove.Count > 0 ? setName : string.Empty,
                sel.FileIdsToRemove,
                confirmed => ApplyUnsub(confirmed, setName, sel.FileIdsToRemove, "BulkUnsub"));
        }
        catch (Exception e)
        {
            _busy = false;
            Plugin.Logger?.LogError($"BulkUnsub: failed: {e}");
        }
    }

    public static void TryRunNoImpossibleFromSetting()
    {
        if (_busy || (_overlay != null && _overlay.IsOpen))
            return;

        if (!Plugin.IsBulkUnsubscriberActive)
            return;

        _busy = true;
        try
        {
            TrackSetStore.LoadFromDisk();
            List<ulong> subscribed = WorkshopSubActions.GetSubscribedFileIds();
            var candidates = new List<NoImpossibleCandidate>(subscribed.Count);
            foreach (ulong fileId in subscribed)
            {
                candidates.Add(new NoImpossibleCandidate(
                    fileId,
                    TrackPlayStats.IsFavorite(fileId),
                    ImpossiblePresenceResolver.Resolve(fileId)));
            }

            NoImpossibleUnsubSelection sel = NoImpossibleUnsubPolicy.SelectRemovals(candidates);
            string setName = TrackSetName.EnsureUnique(
                TrackSetName.FormatNoImpossible(DateTime.UtcNow),
                TrackSetStore.SnapshotNames());

            var body = new StringBuilder();
            body.AppendLine($"Subscribed: {subscribed.Count}");
            body.AppendLine($"Missing Impossible → unsub: {sel.FileIdsToRemove.Count}");
            body.AppendLine(
                $"Kept Impossible: {sel.KeptHasImpossibleCount} · Skip fav: {sel.SkippedFavoriteCount} · Skip unknown: {sel.SkippedUnknownCount}");
            if (sel.FileIdsToRemove.Count == 0)
                body.AppendLine("Nothing to remove.");

            OpenOverlay(
                "Unsub No-Impossible",
                body.ToString(),
                sel.FileIdsToRemove.Count > 0 ? setName : string.Empty,
                sel.FileIdsToRemove,
                confirmed => ApplyUnsub(confirmed, setName, sel.FileIdsToRemove, "UnsubNoImpossible"));
        }
        catch (Exception e)
        {
            _busy = false;
            Plugin.Logger?.LogError($"UnsubNoImpossible: failed: {e}");
        }
    }

    private static void ApplyUnsub(
        bool confirmed,
        string setName,
        List<ulong> fileIds,
        string logTag)
    {
        try
        {
            if (!confirmed || fileIds == null || fileIds.Count == 0)
                return;

            var (ok, fail) = WorkshopSubActions.UnsubscribeAll(fileIds);
            TrackSetRecord record = TrackSetStore.AppendSet(setName, fileIds);
            Plugin.Logger?.LogInfo(
                $"{logTag}: set={record.Name} ok={ok} fail={fail} count={fileIds.Count}");
            LiveDeltaService.SyncWorkshopSubscriptionsToCache();
        }
        finally
        {
            _busy = false;
        }
    }

    private static void OpenOverlay(
        string title,
        string body,
        string setName,
        List<ulong> fileIds,
        Action<bool> onDone)
    {
        if (_overlay == null)
        {
            var go = new GameObject("QoLiTea_UnsubConfirmOverlay");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _overlay = go.AddComponent<UnsubConfirmOverlay>();
        }

        _overlay.Open(title, body, setName, fileIds, onDone);
    }
}
