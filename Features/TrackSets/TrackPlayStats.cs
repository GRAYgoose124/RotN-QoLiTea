using System;
using Shared;
using Shared.PlayerData;
using Shared.StorageData;
using Shared.UGC.Steam;
using Steamworks;

namespace QoLiTea.Features.TrackSets;

/// <summary>Favorite / play-count / last-played for workshop levelIds.</summary>
public static class TrackPlayStats
{
    private static readonly Difficulty[] Diffs =
    {
        Difficulty.Easy,
        Difficulty.Medium,
        Difficulty.Hard,
        Difficulty.Impossible,
        Difficulty.BossBattle,
        Difficulty.Challenge,
    };

    public static string LevelIdForFile(ulong fileId) =>
        SteamWorkshopUgcTrackProvider.FileIdToLevelId(new PublishedFileId_t(fileId));

    public static bool IsFavorite(ulong fileId)
    {
        try
        {
            return PlayerDataUtil.IsLevelFavorite(LevelIdForFile(fileId));
        }
        catch
        {
            return false;
        }
    }

    public static void GetAggregates(ulong fileId, out int totalRuns, out long lastPlayedUnix)
    {
        totalRuns = 0;
        lastPlayedUnix = 0;
        string levelId = LevelIdForFile(fileId);
        try
        {
            foreach (Difficulty d in Diffs)
            {
                int runs = PlayerDataUtil.GetAttemptCountForDifficulty(
                    levelId, d, LevelStatsMode.Normal);
                if (runs > 0)
                    totalRuns += runs;

                long played = PlayerDataUtil.GetTimeLastPlayedForDifficulty(
                    levelId, d, LevelStatsMode.Normal);
                if (played > lastPlayedUnix)
                    lastPlayedUnix = played;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"TrackPlayStats: aggregate failed ({fileId}): {e.Message}");
        }
    }
}
