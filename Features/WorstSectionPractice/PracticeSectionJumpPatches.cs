using HarmonyLib;
using RhythmRift;
using Shared.RhythmEngine;
using Shared.SceneLoading;

namespace QoLiTea.Features.WorstSectionPractice;

[HarmonyPatch]
internal static class PracticeSectionJumpPatches
{
    /// <summary>
    /// During Auto: cull out-of-section monsters after stock spawn (esp. warm-up), then
    /// seek to the next section or CompleteStage on the last.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RRStageController), nameof(RRStageController.Update))]
    private static void UpdatePostfix(RRStageController __instance)
    {
        if (__instance == null || !__instance._isPracticeMode || !PracticeSectionQueue.IsActive)
            return;
        if (PracticeSectionQueue.JumpInProgress)
            return;
        if (__instance._areStageBeatmapsCompleted || __instance._hasPlayerBeenDefeated)
            return;

        var player = __instance.BeatmapPlayer;
        if (player == null || !player.IsPlaying())
            return;

        if (!PracticeSectionQueue.TryGetCurrent(out var current))
            return;

        // Stock spawns during warm-up / travel-in; keep only current-section hits on board.
        PracticeSectionJumper.CullNonSectionEnemies(__instance, current);

        FmodTimeCapsule capsule = player.FmodTimeCapsule;
        if (capsule.TrueBeatNumber < current.EndBeat)
            return;

        if (PracticeSectionQueue.TryPeekNext(out var next))
        {
            PracticeSectionQueue.MarkJumpInProgress();
            try
            {
                if (PracticeSectionJumper.TrySeekToSpan(__instance, next))
                    PracticeSectionQueue.TryAdvanceToNext(out _);
            }
            finally
            {
                PracticeSectionQueue.ClearJumpInProgress();
            }

            return;
        }

        PracticeSectionJumper.OnLastSectionComplete(__instance);
        if (!__instance._areStageBeatmapsCompleted && !__instance._hasPlayerBeenDefeated)
            __instance.CompleteStage();
    }

    /// <summary>
    /// Practice auto-retry / cold Auto start: re-arm queue; overall window first→last;
    /// spawn cull start = first section warm-up.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RRStageController), nameof(RRStageController.BeginPlay))]
    private static void BeginPlayPostfix(RRStageController __instance)
    {
        if (__instance == null || PracticeSectionJumper.SuppressBeginStageClear)
            return;

        if (!__instance._isPracticeMode)
        {
            if (PracticeSectionQueue.HasStoredSpans)
                PracticeSectionQueue.Clear();
            return;
        }

        if (!PracticeSectionQueue.TryRestoreLastQueue())
            return;

        if (!PracticeSectionQueue.TryGetCurrent(out var first))
            return;

        var last = PracticeSectionQueue.StoredSpans[PracticeSectionQueue.StoredSpans.Count - 1];
        SceneLoadData.ModifyActiveMetaDataPracticeModeStatus(true, first.StartBeat, last.EndBeat);

        float fadeIn = __instance._microRiftMusicFadeInDurationInBeats;
        float warmStart = PracticeSectionBeatMath.WarmStartBeat(first.StartBeat, fadeIn);
        PracticeSectionJumper.ApplyPracticeWindow(
            __instance,
            warmStart,
            last.EndBeat,
            __instance._practiceModeStartBeatmapIndex,
            __instance._practiceModeTotalBeatsSkippedBeforeStartBeatmap);
    }
}
