using System;
using System.Collections.Generic;

namespace QoLiTea.Features.TrackSets;

/// <summary>One subscribed workshop track's stats for bulk-unsub ranking.</summary>
public readonly struct BulkUnsubCandidate
{
    public readonly ulong FileId;
    public readonly bool IsFavorite;
    public readonly int TotalRuns;
    public readonly long LastPlayedUnix;

    public BulkUnsubCandidate(ulong fileId, bool isFavorite, int totalRuns, long lastPlayedUnix)
    {
        FileId = fileId;
        IsFavorite = isFavorite;
        TotalRuns = totalRuns;
        LastPlayedUnix = lastPlayedUnix;
    }
}

/// <summary>Result of selecting tracks to unsub to reach a subscribed-count cap.</summary>
public sealed class BulkUnsubSelection
{
    public List<ulong> FileIdsToRemove { get; } = new List<ulong>();
    public int SubscribedCount { get; set; }
    public int MaxKeep { get; set; }
    public bool StillOverCap { get; set; }
    public int ProtectedFavoriteCount { get; set; }
    public int ProtectedZeroPlayCount { get; set; }
}

/// <summary>
/// Cap trim: drop least-recently-played, then fewest plays.
/// Never removes favorites or 0-play tracks.
/// </summary>
public static class BulkUnsubPolicy
{
    public static BulkUnsubSelection SelectRemovals(
        IReadOnlyList<BulkUnsubCandidate> candidates,
        int maxKeep)
    {
        var result = new BulkUnsubSelection
        {
            MaxKeep = maxKeep < 0 ? 0 : maxKeep,
        };

        if (candidates == null || candidates.Count == 0)
            return result;

        result.SubscribedCount = candidates.Count;

        var eligible = new List<BulkUnsubCandidate>();
        foreach (BulkUnsubCandidate c in candidates)
        {
            if (c.FileId == 0)
                continue;
            if (c.IsFavorite)
            {
                result.ProtectedFavoriteCount++;
                continue;
            }

            if (c.TotalRuns <= 0)
            {
                result.ProtectedZeroPlayCount++;
                continue;
            }

            eligible.Add(c);
        }

        if (result.SubscribedCount <= result.MaxKeep)
            return result;

        int need = result.SubscribedCount - result.MaxKeep;
        eligible.Sort(CompareEligible);

        int take = Math.Min(need, eligible.Count);
        for (int i = 0; i < take; i++)
            result.FileIdsToRemove.Add(eligible[i].FileId);

        int remaining = result.SubscribedCount - result.FileIdsToRemove.Count;
        result.StillOverCap = remaining > result.MaxKeep;
        return result;
    }

    private static int CompareEligible(BulkUnsubCandidate a, BulkUnsubCandidate b)
    {
        int byPlayed = a.LastPlayedUnix.CompareTo(b.LastPlayedUnix);
        if (byPlayed != 0)
            return byPlayed;

        int byRuns = a.TotalRuns.CompareTo(b.TotalRuns);
        if (byRuns != 0)
            return byRuns;

        return a.FileId.CompareTo(b.FileId);
    }
}
