using System;
using Shared.TrackData;
using Shared.UGC.Local;
using Shared.UGC.Steam;
using Steamworks;

namespace QoLiTea.Features.LazyCustomTracks;

public static class TrackHydrator
{
    public static ITrackMetadata TryHydrate(string levelId)
    {
        if (string.IsNullOrEmpty(levelId))
            return null;

        try
        {
            if (SteamWorkshopUgcTrackProvider.Available)
            {
                var fileId = SteamWorkshopUgcTrackProvider.LevelIdToFileId(levelId);
                if (fileId.HasValue)
                {
                    var workshop = SteamWorkshopUgcTrackProvider.Instance.GetTrackByLevelIdSync(levelId);
                    if (workshop != null)
                        return workshop;

                    return SteamWorkshopUgcTrackMetadata.FromLocalFileSync(fileId.Value);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"TrackHydrator: workshop hydrate failed for {levelId}: {e.Message}");
        }

        try
        {
            var local = LocalUgcTrackProvider.Instance;
            if (local != null)
            {
                var track = local.GetTrackByLevelIdSync(levelId);
                if (track != null)
                    return track;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"TrackHydrator: local hydrate failed for {levelId}: {e.Message}");
        }

        try
        {
            if (!string.IsNullOrEmpty(levelId) && LocalUgcTrackProvider.Instance != null)
            {
                string path;
                if (LocalUgcTrackProvider.Instance.Resolve(levelId, out path) && !string.IsNullOrEmpty(path))
                {
                    return LocalTrackMetadata.FromPathSync(path, levelId, new LocalTrackMetadata.Options
                    {
                        HasLeaderboard = true,
                        Category = TrackCategory.UgcLocal,
                        UseFileSystemTimestamp = true,
                    });
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"TrackHydrator: FromPathSync failed for {levelId}: {e.Message}");
        }

        return null;
    }

    public static ITrackMetadata TryHydrateWorkshop(PublishedFileId_t fileId)
    {
        try
        {
            var levelId = SteamWorkshopUgcTrackProvider.FileIdToLevelId(fileId);
            if (!string.IsNullOrEmpty(levelId))
            {
                var viaProvider = SteamWorkshopUgcTrackProvider.Instance.GetTrackByLevelIdSync(levelId);
                if (viaProvider != null)
                    return viaProvider;
            }

            return SteamWorkshopUgcTrackMetadata.FromLocalFileSync(fileId);
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"TrackHydrator: workshop file hydrate failed ({fileId}): {e.Message}");
            return null;
        }
    }

    public static ITrackMetadata TryHydrateLocalFolder(string folderPath, string levelId)
    {
        try
        {
            return LocalTrackMetadata.FromPathSync(folderPath, levelId, new LocalTrackMetadata.Options
            {
                HasLeaderboard = true,
                Category = TrackCategory.UgcLocal,
                UseFileSystemTimestamp = true,
            });
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"TrackHydrator: local folder hydrate failed ({folderPath}): {e.Message}");
            return null;
        }
    }
}
