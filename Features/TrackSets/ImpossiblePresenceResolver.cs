using System;
using System.Collections.Generic;
using System.Linq;
using Shared;
using Shared.TrackData;
using Shared.UGC.Steam;
using Steamworks;
using QoLiTea.Features.LazyCustomTracks;

namespace QoLiTea.Features.TrackSets;

/// <summary>Resolve Impossible chart presence from LazyCustomTracks cache / metadata.</summary>
public static class ImpossiblePresenceResolver
{
    public static ImpossiblePresence Resolve(ulong fileId)
    {
        if (fileId == 0)
            return ImpossiblePresence.Unknown;

        string levelId = SteamWorkshopUgcTrackProvider.FileIdToLevelId(new PublishedFileId_t(fileId));
        try
        {
            foreach (ITrackMetadata track in TrackListCache.Snapshot())
            {
                if (track == null || !string.Equals(track.LevelId, levelId, StringComparison.Ordinal))
                    continue;

                return PresenceFromMetadata(track);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"ImpossiblePresenceResolver: cache lookup failed ({fileId}): {e.Message}");
        }

        return ImpossiblePresence.Unknown;
    }

    public static ImpossiblePresence PresenceFromMetadata(ITrackMetadata track)
    {
        if (track == null)
            return ImpossiblePresence.Unknown;

        List<Difficulty> diffs = track.Difficulties?.ToList();
        if (diffs == null || diffs.Count == 0)
            return ImpossiblePresence.Unknown;

        if (track.GetDifficulty(Difficulty.Impossible) != null)
            return ImpossiblePresence.Has;

        return ImpossiblePresence.Missing;
    }
}
