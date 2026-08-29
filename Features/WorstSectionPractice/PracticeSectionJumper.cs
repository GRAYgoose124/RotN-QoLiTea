using System;
using System.Collections.Generic;
using System.Reflection;
using QoLiTea.Features.ResultsDivergence;
using RhythmRift;
using RhythmRift.Enemies;
using Shared.RhythmEngine;
using Shared.SceneLoading;
using Shared.SceneLoading.Payloads;
using UnityEngine;

namespace QoLiTea.Features.WorstSectionPractice;

/// <summary>
/// Auto practice: one stock practice session (first→last section), mid-run seek between
/// sections with 8-beat warm-up. After stock spawns, cull monsters whose hit beat is
/// outside the current section — do not wipe the board before spawn.
/// </summary>
internal static class PracticeSectionJumper
{
    /// <summary>When true, BeginPlay postfix must not reset live hit harvest.</summary>
    internal static bool SuppressBeginStageClear { get; private set; }

    internal static bool TrySeekToSpan(RRStageController stage, PracticeSpan span)
    {
        if (stage?.BeatmapPlayer == null)
            return false;

        float fadeIn = stage._microRiftMusicFadeInDurationInBeats;
        float warmStart = PracticeSectionBeatMath.WarmStartBeat(span.StartBeat, fadeIn);
        ResolveBeatmapIndex(
            stage,
            warmStart,
            out int beatmapIndex,
            out float skippedBefore,
            out float localBeat);

        float overallEnd = OverallPracticeEndBeat(span.EndBeat);
        ApplyPracticeWindow(stage, warmStart, overallEnd, beatmapIndex, skippedBefore);
        PinOverallPracticeMetadata();

        var player = stage.BeatmapPlayer;
        float startTime = stage.ComputeSkipTime(beatmapIndex, warmStart, skippedBefore);
        float startingBeat = warmStart - skippedBefore;

        if (!RestartMusic(stage, player, startTime, startingBeat, fadeIn))
            return false;

        var beatmaps = stage._beatmaps;
        if (beatmaps == null || beatmapIndex < 0 || beatmapIndex >= beatmaps.Count)
            return false;

        for (int i = 0; i < beatmaps.Count; i++)
            beatmaps[i]?.ResetBeatmap();

        var targetBeatmap = beatmaps[beatmapIndex];
        float chartLocalBeat = localBeat - fadeIn;
        player.SetBeatNumberOffset(warmStart - fadeIn);
        // Skip pre-offset clips so the gap does not dump mid-seek; warm-up events after
        // offset still fire so stock can spawn section monsters.
        player.SetNewBeatmap(targetBeatmap, chartLocalBeat, shouldSkipClipsBeforeOffset: true);
        player.SetEventProcessing(shouldProcessEvents: true, shouldProcessInput: true);

        stage._beatmapsIndex = beatmapIndex + 1;
        stage._areStageBeatmapsCompleted = false;

        // Drop leftovers from the previous section only — stock will spawn the next set.
        CullNonSectionEnemies(stage, span, warmStart);

        Plugin.Logger?.LogInfo(
            $"PracticeSectionJumper: seek section [{span.StartBeat:0.##}, {span.EndBeat:0.##}] warm={warmStart:0.##}");
        return true;
    }

    internal static float OverallPracticeEndBeat(float fallbackEnd)
    {
        if (PracticeSectionQueue.HasStoredSpans && PracticeSectionQueue.StoredSpans.Count > 0)
            return PracticeSectionQueue.StoredSpans[PracticeSectionQueue.StoredSpans.Count - 1].EndBeat;
        return fallbackEnd;
    }

    internal static void PinOverallPracticeMetadata()
    {
        if (!PracticeSectionQueue.HasStoredSpans || PracticeSectionQueue.StoredSpans.Count == 0)
            return;

        var first = PracticeSectionQueue.StoredSpans[0];
        var last = PracticeSectionQueue.StoredSpans[PracticeSectionQueue.StoredSpans.Count - 1];
        SceneLoadData.ModifyActiveMetaDataPracticeModeStatus(true, first.StartBeat, last.EndBeat);
    }

    internal static void ApplyPracticeWindow(
        RRStageController stage,
        float warmStart,
        float overallEndBeat,
        int beatmapIndex,
        float skippedBefore)
    {
        stage._practiceModeStartBeatNumber = warmStart;
        stage._practiceModeEndBeatNumber = overallEndBeat;
        stage._practiceModeStartBeatmapIndex = beatmapIndex;
        stage._practiceModeTotalBeatsSkippedBeforeStartBeatmap = skippedBefore;
        if (stage._stageScenePayload is RhythmRiftScenePayload payload)
            payload.SetPracticeModeBeatRange(warmStart, overallEndBeat);
    }

    /// <summary>
    /// After stock has spawned: remove active / queued monsters that are not part of
    /// <paramref name="span"/> (by <see cref="RREnemy.TargetHitBeatNumber"/>).
    /// </summary>
    internal static void CullNonSectionEnemies(
        RRStageController stage,
        PracticeSpan span,
        float? warmStartOverride = null)
    {
        var enemies = stage?._enemyController;
        if (enemies == null)
            return;

        float fadeIn = stage._microRiftMusicFadeInDurationInBeats;
        float warmStart = warmStartOverride
            ?? PracticeSectionBeatMath.WarmStartBeat(span.StartBeat, fadeIn);

        try
        {
            var queued = enemies._queuedSpawnEnemyData;
            if (queued != null && queued.Count > 0)
            {
                for (int i = queued.Count - 1; i >= 0; i--)
                {
                    if (!PracticeSectionEnemyCull.IsQueuedSpawnKeepable(
                            queued[i].SpawnTrueBeatNumber,
                            warmStart,
                            span.EndBeat))
                        queued.RemoveAt(i);
                }
            }

            enemies._holdNoteEnemySpawnData?.Clear();

            var active = enemies._activeEnemies;
            if (active == null || active.Count == 0)
                return;

            var remove = new List<RREnemy>();
            foreach (var enemy in active)
            {
                if (enemy == null)
                    continue;
                if (PracticeSectionEnemyCull.IsInSection(
                        enemy.TargetHitBeatNumber,
                        span.StartBeat,
                        span.EndBeat))
                    continue;
                remove.Add(enemy);
            }

            foreach (var enemy in remove)
            {
                try
                {
                    enemies.DestroyEnemy(enemy);
                    active.Remove(enemy);
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"PracticeSectionJumper Cull DestroyEnemy: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"PracticeSectionJumper CullNonSectionEnemies: {ex.Message}");
        }
    }

    /// <summary>Immediate full clear — only for last-section CompleteStage unblock.</summary>
    internal static void HardClearBoard(RRStageController stage)
    {
        var enemies = stage?._enemyController;
        if (enemies == null)
            return;

        try
        {
            enemies._queuedSpawnEnemyData?.Clear();
            enemies._holdNoteEnemySpawnData?.Clear();
            enemies.SlayAllExistingEnemies();

            var active = enemies._activeEnemies;
            if (active == null || active.Count == 0)
                return;

            var snapshot = new List<RREnemy>(active);
            foreach (var enemy in snapshot)
            {
                if (enemy == null)
                    continue;
                try
                {
                    enemies.DestroyEnemy(enemy);
                }
                catch (Exception ex)
                {
                    Plugin.Logger?.LogWarning($"PracticeSectionJumper DestroyEnemy: {ex.Message}");
                }
            }

            active.Clear();
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"PracticeSectionJumper HardClearBoard: {ex.Message}");
        }
    }

    private static void ResolveBeatmapIndex(
        RRStageController stage,
        float warmStart,
        out int beatmapIndex,
        out float skippedBeforeBeatmaps,
        out float localBeatInBeatmap)
    {
        float remaining = warmStart;
        beatmapIndex = 0;
        skippedBeforeBeatmaps = 0f;
        localBeatInBeatmap = 0f;

        var beatmaps = stage._beatmaps;
        if (beatmaps == null || beatmaps.Count == 0)
            return;

        for (int i = 0; i < beatmaps.Count; i++)
        {
            if (beatmaps[i].DurationInBeats > remaining)
            {
                beatmapIndex = i;
                localBeatInBeatmap = remaining;
                return;
            }

            remaining -= beatmaps[i].DurationInBeats;
            skippedBeforeBeatmaps += beatmaps[i].DurationInBeats;
        }

        beatmapIndex = beatmaps.Count - 1;
        localBeatInBeatmap = 0f;
    }

    /// <summary>
    /// Last Auto section finished — keep LastSpans for stock practice RetryStage.
    /// Re-pin metadata to first→last so auto-retry restarts the whole Auto set.
    /// </summary>
    internal static void OnLastSectionComplete(RRStageController stage)
    {
        PracticeSectionQueue.DeactivateKeepLast();
        HardClearBoard(stage);
        PinOverallPracticeMetadata();
    }

    internal static void FinishAutoPractice(RRStageController stage)
    {
        PracticeSectionQueue.Clear();
        SceneLoadData.ModifyActiveMetaDataPracticeModeStatus(shouldBePracticeMode: false);

        if (stage == null)
            return;

        stage._isPracticeMode = false;
        if (stage._stageScenePayload is StageScenePayload payload)
            payload.IsPracticeMode = false;
    }

    private static bool RestartMusic(
        RRStageController stage,
        BeatmapPlayer player,
        float startTimeInSeconds,
        float startingBeat,
        float fadeInBeats)
    {
        StopMusic(player);

        var parms = new BeatmapPlayer.PlaybackParameters
        {
            StartTimeInSeconds = startTimeInSeconds,
            StartingBeat = Mathf.Max(startingBeat, 0f),
            FadeInDurationInBeats = fadeInBeats,
            IsDoubleSpeed = stage._isDoubleSpeed,
            IsShopkeeperActive = stage._isShopkeeperActive,
        };

        if (!string.IsNullOrEmpty(stage._customTrackAudioFilePath))
        {
            parms.AudioFilePath = stage._customTrackAudioFilePath;
            parms.AudioVolume = stage._customTrackAudioVolume;
            parms.OffsetInSeconds = stage._customTrackPlaybackOffset;
            parms.LipMap = stage._lipMap;
        }

        SuppressBeginStageClear = true;
        try
        {
            player.BeginPlay(parms);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"PracticeSectionJumper music restart failed: {ex.Message}");
            return false;
        }
        finally
        {
            SuppressBeginStageClear = false;
        }
    }

    private static void StopMusic(BeatmapPlayer player)
    {
        try
        {
            var field = typeof(BeatmapPlayer).GetField(
                "_musicInstance",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return;

            object boxed = field.GetValue(player);
            if (boxed == null)
                return;

            Type musicType = boxed.GetType();
            var isValid = musicType.GetMethod("isValid", Type.EmptyTypes);
            if (isValid == null || !(bool)isValid.Invoke(boxed, null))
                return;

            Type stopModeType = musicType.Assembly.GetType("FMOD.Studio.STOP_MODE")
                ?? Type.GetType("FMOD.Studio.STOP_MODE, FMODUnity");
            object immediate = stopModeType != null
                ? Enum.ToObject(stopModeType, 1)
                : null;

            MethodInfo stop = stopModeType != null
                ? musicType.GetMethod("stop", new[] { stopModeType })
                : null;
            stop?.Invoke(boxed, immediate != null ? new[] { immediate } : null);

            musicType.GetMethod("release", Type.EmptyTypes)?.Invoke(boxed, null);
            musicType.GetMethod("clearHandle", Type.EmptyTypes)?.Invoke(boxed, null);
            field.SetValue(player, boxed);

            player.ReleaseCustomSound();
            player._isPlaying = false;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"PracticeSectionJumper StopMusic: {ex.Message}");
        }
    }
}
