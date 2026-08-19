using System;
using System.Collections.Generic;
using Steamworks;
using Shared.UGC.Steam;
using QoLiTea.Features.WorkshopAutoScan;

namespace QoLiTea.Features.TrackSets;

/// <summary>Steam workshop subscribe/unsubscribe helpers.</summary>
public static class WorkshopSubActions
{
    public static List<ulong> GetSubscribedFileIds() =>
        WorkshopRecentQuery.GetSubscribedFileIds();

    public static bool TrySubscribe(ulong fileId) =>
        WorkshopRecentQuery.TrySubscribe(fileId);

    public static bool TryUnsubscribe(ulong fileId)
    {
        try
        {
            if (!SteamWorkshopUgcTrackProvider.Available || fileId == 0)
                return false;

            SteamAPICall_t call = SteamUGC.UnsubscribeItem(new PublishedFileId_t(fileId));
            return call != SteamAPICall_t.Invalid;
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"WorkshopSubActions: UnsubscribeItem failed ({fileId}): {e.Message}");
            return false;
        }
    }

    public static (int ok, int fail) UnsubscribeAll(IEnumerable<ulong> fileIds)
    {
        int ok = 0;
        int fail = 0;
        if (fileIds == null)
            return (ok, fail);

        foreach (ulong id in fileIds)
        {
            if (TryUnsubscribe(id))
                ok++;
            else
                fail++;
        }

        return (ok, fail);
    }

    public static (int ok, int fail, int skipped) SubscribeMissing(
        IEnumerable<ulong> fileIds,
        HashSet<ulong> alreadySubscribed)
    {
        int ok = 0;
        int fail = 0;
        int skipped = 0;
        var subbed = alreadySubscribed ?? new HashSet<ulong>();

        if (fileIds == null)
            return (ok, fail, skipped);

        foreach (ulong id in fileIds)
        {
            if (id == 0)
                continue;
            if (subbed.Contains(id))
            {
                skipped++;
                continue;
            }

            if (TrySubscribe(id))
            {
                ok++;
                subbed.Add(id);
            }
            else
            {
                fail++;
            }
        }

        return (ok, fail, skipped);
    }
}
