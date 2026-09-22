using System;
using System.Collections.Generic;
using HarmonyLib;
using QoLiTea.Features.WorstSectionPractice;
using RhythmRift;
using Shared;
using Shared.RhythmEngine;
using Shared.SceneLoading.Payloads;
using UnityEngine;

namespace QoLiTea.Features.ResultsDivergence;

[HarmonyPatch]
internal static class ResultsDivergencePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RRStageController), nameof(RRStageController.BeginPlay))]
    private static void BeginPlayPostfix()
    {
        if (PracticeSectionJumper.SuppressBeginStageClear)
            return;
        if (!ResultsDivergencePolicy.ShouldHarvest(
                Plugin.Enabled,
                Plugin.ResultsDivergencePlotEnabled,
                Plugin.WorstSectionPracticeEnabled))
            return;
        var stage = UnityEngine.Object.FindObjectOfType<RRStageController>();
        int truePerfectMin = DivergenceTimingBinsHarvest.ReadTruePerfectMinimum(
            stage?._stageInputRecord?._inputRatingsDefinition);
        RunSessionStore.BeginStage(truePerfectMin);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StageInputRecord), nameof(StageInputRecord.RecordInput))]
    private static void RecordInputPostfix(
        InputRating inputRating,
        float ratingPercent,
        float inputBeatNumber,
        float targetBeatNumber,
        bool wasPlayerInput)
    {
        try
        {
            DivergenceLiveHarvest.TryAppendRecordInput(
                inputRating,
                ratingPercent,
                inputBeatNumber,
                targetBeatNumber,
                wasPlayerInput);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"ResultsDivergence RecordInput harvest failed: {ex.Message}");
        }
    }

    /// <summary>
    /// RR skips <see cref="StageInputRecord.RecordInput"/> for note misses and calls
    /// <see cref="StageInputRecord.RecordErrantInput"/> for empty swings (overhits).
    /// Empty/partial swings with open hit windows are miss-style vert reds, not pink overhits.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RRStageController), nameof(RRStageController.ProcessHitData))]
    private static void ProcessHitDataPostfix(
        RRStageController __instance,
        List<RREnemyController.EnemyHitData> hitDatas,
        List<Unity.Mathematics.int2> positionsToAttack,
        bool isBaneInput,
        bool isDebugInput)
    {
        try
        {
            if (isBaneInput || isDebugInput)
                return;

            int positionCount = positionsToAttack?.Count ?? 0;
            bool anyWindowOpen = __instance._enemyController != null
                && __instance._enemyController.IsAnyEnemyHitWindowOpen();

            if (hitDatas != null)
            {
                foreach (var hit in hitDatas)
                {
                    if (hit.Enemy == null)
                        continue;
                    RunSessionStore.RememberEnemyName(hit.TargetBeat, hit.Enemy.DisplayName);
                }
            }

            if (hitDatas == null || hitDatas.Count == 0)
            {
                float inputBeat = __instance.BeatmapPlayer?.FmodTimeCapsule.TrueBeatNumber ?? 0f;
                if (inputBeat <= 0f)
                    return;

                var kind = ProcessHitClassification.ClassifyEmptySwing(positionCount, anyWindowOpen);
                if (kind == ProcessHitClassification.EmptySwingKind.MissVertical)
                    DivergenceLiveHarvest.TryAppendMissVertical(inputBeat);
                else
                    DivergenceLiveHarvest.TryAppendOverhit(inputBeat);
                return;
            }

            int missHitDataCount = 0;
            foreach (var hit in hitDatas)
            {
                if (hit.InputRating != InputRating.Miss)
                    continue;

                missHitDataCount++;
                DivergenceLiveHarvest.TryAppendFailure(
                    hit.TargetBeat,
                    hit.RatingPercent,
                    hit.InputBeat,
                    hit.TargetBeat,
                    PlotHitRating.Miss,
                    hit.Enemy?.DisplayName,
                    wasPlayerInput: true);
            }

            if (ProcessHitClassification.ShouldAppendExtraMissVertical(
                    hitDatas.Count,
                    missHitDataCount,
                    positionCount,
                    anyWindowOpen))
            {
                float inputBeat = __instance.BeatmapPlayer?.FmodTimeCapsule.TrueBeatNumber ?? 0f;
                if (inputBeat > 0f)
                    DivergenceLiveHarvest.TryAppendMissVertical(inputBeat);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"ResultsDivergence ProcessHitData harvest failed: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StageInputRecord), nameof(StageInputRecord.VibePowerActivated))]
    private static void VibePowerActivatedPostfix(float currentTime, float currentBeatNumber)
    {
        try
        {
            if (!ResultsDivergencePolicy.ShouldHarvest(
                    Plugin.Enabled,
                    Plugin.ResultsDivergencePlotEnabled,
                    Plugin.WorstSectionPracticeEnabled))
                return;
            RunSessionStore.OnVibeActivated(currentBeatNumber);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"ResultsDivergence VibePowerActivated failed: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StageInputRecord), nameof(StageInputRecord.VibePowerDeactivated))]
    private static void VibePowerDeactivatedPostfix()
    {
        try
        {
            if (!ResultsDivergencePolicy.ShouldHarvest(
                    Plugin.Enabled,
                    Plugin.ResultsDivergencePlotEnabled,
                    Plugin.WorstSectionPracticeEnabled))
                return;

            var stage = UnityEngine.Object.FindObjectOfType<RRStageController>();
            float beat = stage?.BeatmapPlayer?.FmodTimeCapsule.TrueBeatNumber ?? 0f;
            if (beat > 0f)
                RunSessionStore.OnVibeDeactivated(beat);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"ResultsDivergence VibePowerDeactivated failed: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScoreResultsView), nameof(ScoreResultsView.Show))]
    private static void ShowPostfix(ScoreResultsView __instance)
    {
        try
        {
            Apply(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"ResultsDivergence Show failed: {ex.Message}");
        }
    }

    private static void Apply(ScoreResultsView view)
    {
        if (view == null)
            return;

        if (!ResultsDivergencePolicy.ShouldHarvest(
                Plugin.Enabled,
                Plugin.ResultsDivergencePlotEnabled,
                Plugin.WorstSectionPracticeEnabled))
        {
            DivergencePlotSession.DestroyIfAny();
            WorstSectionPracticeUi.HideButton(view);
            return;
        }

        var hits = RunSessionStore.SnapshotLiveOrEmpty();
        var stage = UnityEngine.Object.FindObjectOfType<RRStageController>();
        if (hits.Count == 0 && stage?._stageInputRecord != null)
            hits = StageInputRecordHarvest.FromRecord(stage._stageInputRecord);
        if (hits.Count == 0 && RunSessionStore.LastHits != null && RunSessionStore.LastHits.Count > 0)
            hits = RunSessionStore.LastHits;

        if (hits == null || hits.Count == 0)
        {
            DivergencePlotSession.DestroyIfAny();
            WorstSectionPracticeUi.HideButton(view);
            return;
        }

        float totalBeats = GuessTotalBeats(stage, hits);
        RunSessionStore.CloseOpenVibeAt(totalBeats);

        RunSessionStore.LastHits = hits;
        RunSessionStore.LastTotalBeats = totalBeats;

        var spans = WorstSectionDetector.Detect(hits);

        bool showPlot = ResultsDivergencePolicy.ShouldShowPlot(
            Plugin.Enabled,
            Plugin.ResultsDivergencePlotEnabled);

        if (showPlot)
        {
            Transform plotParent = view.transform.Find("Content") ?? view.transform;
            var ratings = view._accuracyBar?._ratingsDefinition
                ?? stage?._stageInputRecord?._inputRatingsDefinition;
            int truePerfectMin = DivergenceTimingBinsHarvest.ReadTruePerfectMinimum(ratings);
            var vibeSpans = VibeActivationHarvest.FromRecord(stage?._stageInputRecord, totalBeats);
            var vibeStats = VibeWindowStats.Compute(vibeSpans, hits);
            var context = new PlotRenderContext(
                hits,
                spans,
                vibeSpans,
                totalBeats,
                Plugin.ResultsDivergencePlotOpacityPercent,
                DivergenceTimingBinsHarvest.FromRatings(ratings),
                DivergenceTimingBins.SuperCritMagnitude(truePerfectMin),
                view._accuracyBar,
                vibeStats);
            var raw = DivergencePlotRenderer.Build(
                plotParent,
                context,
                PlotDisplayMode.Docked);
            DivergencePlotSession.Register(raw, context);
        }
        else
        {
            DivergencePlotSession.DestroyIfAny();
        }

        bool offerPractice = WorstSectionPracticePolicy.ShouldOfferPractice(
            Plugin.Enabled,
            Plugin.WorstSectionPracticeEnabled,
            spans.Count,
            view._shouldShowPracticeMode);

        if (offerPractice)
            WorstSectionPracticeUi.EnsureButton(view, spans);
        else
            WorstSectionPracticeUi.HideButton(view);
    }

    private static float GuessTotalBeats(RRStageController stage, IReadOnlyList<HitDivergenceSample> hits)
    {
        float maxHit = 0f;
        if (hits != null)
        {
            foreach (var h in hits)
            {
                if (h.TargetBeat > maxHit)
                    maxHit = h.TargetBeat;
            }
        }

        float fromPayload = 0f;
        if (stage != null)
        {
            if (stage._practiceModeTotalStageBeats > 0f)
                fromPayload = stage._practiceModeTotalStageBeats;
            else if (stage._stageScenePayload is RhythmRiftScenePayload payload)
            {
                if (payload.FinalInputBeatOverride > 0f)
                    fromPayload = payload.FinalInputBeatOverride;
                else if (payload.TotalBeats > 0f)
                    fromPayload = payload.TotalBeats;
            }
        }

        float total = Math.Max(fromPayload, maxHit);
        return Math.Max(total, 1f);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StageInputRecord), nameof(StageInputRecord.GetStageResultsData))]
    private static void GetStageResultsDataPostfix(StageInputRecord __instance)
    {
        try
        {
            if (!ResultsDivergencePolicy.ShouldHarvest(
                    Plugin.Enabled,
                    Plugin.ResultsDivergencePlotEnabled,
                    Plugin.WorstSectionPracticeEnabled))
                return;

            var live = RunSessionStore.SnapshotLiveOrEmpty();
            if (live.Count > 0)
            {
                RunSessionStore.LastHits = live;
            }
            else if (__instance != null)
            {
                RunSessionStore.LastHits = StageInputRecordHarvest.FromRecord(__instance);
            }

            RunSessionStore.LastTotalBeats = 0f;
        }
        catch (Exception ex)
        {
            Plugin.Logger?.LogWarning($"ResultsDivergence harvest stash failed: {ex.Message}");
        }
    }
}
