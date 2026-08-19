using System.Collections.Generic;

namespace QoLiTea.Features.TrackSets;

public enum ImpossiblePresence
{
    Unknown = 0,
    Has = 1,
    Missing = 2,
}

public readonly struct NoImpossibleCandidate
{
    public readonly ulong FileId;
    public readonly bool IsFavorite;
    public readonly ImpossiblePresence Presence;

    public NoImpossibleCandidate(ulong fileId, bool isFavorite, ImpossiblePresence presence)
    {
        FileId = fileId;
        IsFavorite = isFavorite;
        Presence = presence;
    }
}

public sealed class NoImpossibleUnsubSelection
{
    public List<ulong> FileIdsToRemove { get; } = new List<ulong>();
    public int SkippedFavoriteCount { get; set; }
    public int SkippedUnknownCount { get; set; }
    public int KeptHasImpossibleCount { get; set; }
}

/// <summary>
/// Unsub tracks that lack Impossible. Favorites and Unknown presence are skipped.
/// </summary>
public static class NoImpossibleUnsubPolicy
{
    public static NoImpossibleUnsubSelection SelectRemovals(
        IReadOnlyList<NoImpossibleCandidate> candidates)
    {
        var result = new NoImpossibleUnsubSelection();
        if (candidates == null)
            return result;

        foreach (NoImpossibleCandidate c in candidates)
        {
            if (c.FileId == 0)
                continue;

            if (c.IsFavorite)
            {
                result.SkippedFavoriteCount++;
                continue;
            }

            if (c.Presence == ImpossiblePresence.Unknown)
            {
                result.SkippedUnknownCount++;
                continue;
            }

            if (c.Presence == ImpossiblePresence.Has)
            {
                result.KeptHasImpossibleCount++;
                continue;
            }

            result.FileIdsToRemove.Add(c.FileId);
        }

        return result;
    }
}
