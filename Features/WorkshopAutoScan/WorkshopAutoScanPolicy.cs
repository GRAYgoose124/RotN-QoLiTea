using System;
using System.Collections.Generic;
using System.Linq;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>
/// Pure rules for Workshop AutoScan: first-run stamp, candidate filter, seen marks, trim.
/// </summary>
public static class WorkshopAutoScanPolicy
{
    public const int MaxSeenIds = 500;

    /// <summary>Community most-recent dig cap (3 × 30).</summary>
    public const int MaxCommunityPages = 3;

    /// <summary>First successful query: stamp every page id as seen (no overlay).</summary>
    public static List<ulong> StampFirstRun(IEnumerable<ulong> pageFileIds)
    {
        var result = new List<ulong>();
        if (pageFileIds == null)
            return result;

        foreach (ulong id in pageFileIds)
        {
            if (id == 0)
                continue;
            if (!result.Contains(id))
                result.Add(id);
        }

        return result;
    }

    /// <summary>Page results minus already seen minus already subscribed.</summary>
    public static List<ulong> FilterCandidates(
        IEnumerable<ulong> pageFileIds,
        IEnumerable<ulong> seenFileIds,
        IEnumerable<ulong> subscribedFileIds)
    {
        var result = new List<ulong>();
        if (pageFileIds == null)
            return result;

        var seen = new HashSet<ulong>(seenFileIds ?? Array.Empty<ulong>());
        var subscribed = new HashSet<ulong>(subscribedFileIds ?? Array.Empty<ulong>());

        foreach (ulong id in pageFileIds)
        {
            if (id == 0)
                continue;
            if (seen.Contains(id))
                continue;
            if (subscribed.Contains(id))
                continue;
            if (result.Contains(id))
                continue;
            result.Add(id);
        }

        return result;
    }

    /// <summary>Append touched ids not already in seen (order: existing then new).</summary>
    public static List<ulong> MarkSeen(
        IEnumerable<ulong> existingSeen,
        IEnumerable<ulong> touchedFileIds)
    {
        var result = new List<ulong>();
        var set = new HashSet<ulong>();

        foreach (ulong id in existingSeen ?? Array.Empty<ulong>())
        {
            if (id == 0 || !set.Add(id))
                continue;
            result.Add(id);
        }

        foreach (ulong id in touchedFileIds ?? Array.Empty<ulong>())
        {
            if (id == 0 || !set.Add(id))
                continue;
            result.Add(id);
        }

        return result;
    }

    /// <summary>Keep the newest <paramref name="maxCount"/> ids (tail of the list).</summary>
    public static List<ulong> TrimSeen(IEnumerable<ulong> seenFileIds, int maxCount = MaxSeenIds)
    {
        if (maxCount < 0)
            maxCount = 0;

        var list = (seenFileIds ?? Array.Empty<ulong>())
            .Where(id => id != 0)
            .Distinct()
            .ToList();

        if (list.Count <= maxCount)
            return list;

        return list.Skip(list.Count - maxCount).ToList();
    }

    /// <summary>
    /// After finishing community page <paramref name="pageIndex1Based"/>, whether to fetch the next page.
    /// Only when still zero candidates and pages remain (max 3 → 90 items).
    /// </summary>
    public static bool ShouldFetchNextPage(
        int pageIndex1Based,
        int candidateCountSoFar,
        int maxPages = MaxCommunityPages)
    {
        if (maxPages < 1)
            maxPages = 1;
        if (pageIndex1Based < 1)
            return false;
        if (candidateCountSoFar > 0)
            return false;
        return pageIndex1Based < maxPages;
    }

    /// <summary>
    /// Split items into silent auto-sub vs show lists (input order preserved).
    /// </summary>
    public static (
        List<(ulong FileId, ulong OwnerId)> silent,
        List<(ulong FileId, ulong OwnerId)> show)
        PartitionByAutoSubAuthors(
            IEnumerable<(ulong FileId, ulong OwnerId)> items,
            IEnumerable<ulong> autoSubOwnerIds)
    {
        var silent = new List<(ulong FileId, ulong OwnerId)>();
        var show = new List<(ulong FileId, ulong OwnerId)>();
        var owners = new HashSet<ulong>(autoSubOwnerIds ?? Array.Empty<ulong>());

        if (items == null)
            return (silent, show);

        foreach (var item in items)
        {
            if (item.FileId == 0)
                continue;
            if (item.OwnerId != 0 && owners.Contains(item.OwnerId))
                silent.Add(item);
            else
                show.Add(item);
        }

        return (silent, show);
    }
}
