using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shared.TrackSelection;
using UnityEngine;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>
/// One scan per Custom Music open: show unseen recent Workshop items (incl. first run).
/// Digs up to 3 community pages when earlier pages have zero uniques; silent-subs auto-sub authors.
/// </summary>
public static class WorkshopAutoScanController
{
    private static int _scanGeneration;
    private static CustomTracksSelectionSceneController _owner;
    private static bool _scanInFlight;
    private static WorkshopAutoScanOverlay _overlay;

    public static bool IsOverlayOpen => _overlay != null && _overlay.IsOpen;

    public static void OnCustomMusicOpened(CustomTracksSelectionSceneController controller)
    {
        if (!Plugin.IsWorkshopAutoScanActive || controller == null)
            return;

        if (_owner == controller && (_scanInFlight || IsOverlayOpen))
            return;

        _owner = controller;
        int gen = ++_scanGeneration;
        _ = RunScanAsync(controller, gen);
    }

    public static void OnCustomMusicClosed(CustomTracksSelectionSceneController controller)
    {
        if (_owner != null && controller != null && !ReferenceEquals(_owner, controller))
            return;

        _scanGeneration++;
        CloseOverlay(flush: true);
        _owner = null;
        _scanInFlight = false;
    }

    public static void CloseOverlay(bool flush)
    {
        if (_overlay == null)
            return;

        if (_owner != null)
            _owner.InputDisabled = false;

        _overlay.Close();
        if (_overlay != null)
        {
            UnityEngine.Object.Destroy(_overlay.gameObject);
            _overlay = null;
        }
    }

    private static async Task RunScanAsync(CustomTracksSelectionSceneController controller, int gen)
    {
        _scanInFlight = true;
        try
        {
            WorkshopSeenStore.LoadFromDisk();
            WorkshopAuthorStore.LoadFromDisk();

            var seen = WorkshopSeenStore.SnapshotSeenFileIds();
            var subscribed = WorkshopRecentQuery.GetSubscribedFileIds();
            var autoSubOwners = WorkshopAuthorStore.SnapshotOwnerIds();

            var accumulated = new List<WorkshopRecentItem>();
            var accumulatedIds = new List<ulong>();
            var seenIdSet = new HashSet<ulong>();
            List<ulong> candidateIds = new List<ulong>();

            for (int page = 1; page <= WorkshopAutoScanPolicy.MaxCommunityPages; page++)
            {
                List<WorkshopRecentItem> pageItems = await WorkshopRecentQuery.QueryRecentPageAsync(page);
                if (gen != _scanGeneration || !ReferenceEquals(_owner, controller))
                    return;

                if (pageItems != null)
                {
                    foreach (var item in pageItems)
                    {
                        if (item == null || item.FileId == 0)
                            continue;
                        if (!seenIdSet.Add(item.FileId))
                            continue;
                        accumulated.Add(item);
                        accumulatedIds.Add(item.FileId);
                    }
                }

                candidateIds = WorkshopAutoScanPolicy.FilterCandidates(
                    accumulatedIds, seen, subscribed);

                if (!WorkshopAutoScanPolicy.ShouldFetchNextPage(page, candidateIds.Count))
                    break;
            }

            if (accumulated.Count == 0)
            {
                Plugin.Logger?.LogInfo("WorkshopAutoScan: empty recent pages (skip)");
                if (!WorkshopSeenStore.Initialized)
                    WorkshopSeenStore.Save(initialized: true, WorkshopSeenStore.SnapshotSeenFileIds());
                return;
            }

            if (candidateIds.Count == 0)
            {
                if (!WorkshopSeenStore.Initialized)
                    WorkshopSeenStore.Save(initialized: true, seen);
                Plugin.Logger?.LogInfo("WorkshopAutoScan: no new workshop items");
                return;
            }

            var candidateSet = new HashSet<ulong>(candidateIds);
            var candidates = accumulated.Where(p => candidateSet.Contains(p.FileId)).ToList();

            var pairs = candidates.Select(c => (c.FileId, c.OwnerId));
            var (silentPairs, showPairs) = WorkshopAutoScanPolicy.PartitionByAutoSubAuthors(
                pairs, autoSubOwners);

            var silentSet = new HashSet<ulong>(silentPairs.Select(p => p.FileId));
            var showSet = new HashSet<ulong>(showPairs.Select(p => p.FileId));

            var silentItems = candidates.Where(c => silentSet.Contains(c.FileId)).ToList();
            var showItems = candidates.Where(c => showSet.Contains(c.FileId)).ToList();

            if (silentItems.Count > 0)
            {
                int ok = 0;
                int fail = 0;
                var silentIds = new List<ulong>();
                foreach (var item in silentItems)
                {
                    silentIds.Add(item.FileId);
                    if (WorkshopRecentQuery.TrySubscribe(item.FileId))
                    {
                        ok++;
                        Plugin.Logger?.LogInfo(
                            $"WorkshopAutoScan: auto-sub author {item.OwnerId} → subscribed {item.FileId}");
                    }
                    else
                    {
                        fail++;
                    }
                }

                WorkshopSeenStore.MarkTouchedAndSave(silentIds);
                Plugin.Logger?.LogInfo(
                    $"WorkshopAutoScan: silent author auto-sub — ok={ok} fail={fail}");
            }

            if (showItems.Count == 0)
            {
                if (!WorkshopSeenStore.Initialized)
                    WorkshopSeenStore.EnsureInitialized();
                Plugin.Logger?.LogInfo("WorkshopAutoScan: no overlay items after author auto-sub");
                return;
            }

            await WorkshopRecentQuery.ResolveAuthorsAsync(showItems);
            if (gen != _scanGeneration || !ReferenceEquals(_owner, controller))
                return;

            if (!WorkshopSeenStore.Initialized)
                Plugin.Logger?.LogInfo(
                    $"WorkshopAutoScan: first run — showing {showItems.Count} item(s) to browse/subscribe");
            else
                Plugin.Logger?.LogInfo($"WorkshopAutoScan: showing {showItems.Count} new item(s)");

            ShowOverlay(controller, showItems);
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"WorkshopAutoScan: scan failed: {e.Message}");
        }
        finally
        {
            if (gen == _scanGeneration)
                _scanInFlight = false;
        }
    }

    private static void ShowOverlay(
        CustomTracksSelectionSceneController controller,
        List<WorkshopRecentItem> items)
    {
        CloseOverlay(flush: false);

        var go = new GameObject("QoLiTea_WorkshopAutoScanOverlay");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _overlay = go.AddComponent<WorkshopAutoScanOverlay>();
        controller.InputDisabled = true;
        _overlay.Open(items, onClosed: () =>
        {
            if (_owner != null)
                _owner.InputDisabled = false;
            if (_overlay != null)
            {
                UnityEngine.Object.Destroy(_overlay.gameObject);
                _overlay = null;
            }
        });
    }
}
