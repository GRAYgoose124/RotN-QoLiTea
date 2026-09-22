using System;
using System.Collections;
using System.Collections.Generic;
using Shared.TrackData;
using Shared.TrackSelection;
using Shared.UGC.Local;
using Shared.UGC.Steam;
using UnityEngine;
using static Shared.TrackSelection.BaseTrackSelectionOptionGroup;

namespace QoLiTea.Features.RandomSong;

internal static class TrackListGate
{
    public static bool TryGetOfficial(out TrackSelectionSceneController controller)
    {
        controller = null;
        foreach (var c in UnityEngine.Object.FindObjectsOfType<TrackSelectionSceneController>(true))
        {
            if (c == null || !c.isActiveAndEnabled)
                continue;
            var group = c._infiniteTrackSelectionOptionGroup;
            if (group == null || !group.IsInitialized)
                continue;
            controller = c;
            return true;
        }

        return false;
    }

    public static bool TryGetCustom(out CustomTracksSelectionSceneController controller)
    {
        controller = null;
        foreach (var c in UnityEngine.Object.FindObjectsOfType<CustomTracksSelectionSceneController>(true))
        {
            if (c == null || !c.isActiveAndEnabled)
                continue;
            var group = c._trackSelectionOptionGroup;
            if (group == null || !group.IsInitialized)
                continue;
            controller = c;
            return true;
        }

        return false;
    }
}

/// <summary>
/// Jukebox: true global pick, capped theatrical ±1 scroll, snap via RefreshTrackData when far.
/// </summary>
internal sealed class RandomSongDriver
{
    private readonly MonoBehaviour _host;
    private Coroutine _running;
    private readonly Queue<string> _recentLevelIds = new();
    private readonly HashSet<string> _recentLevelIdSet = new(StringComparer.Ordinal);

    public RandomSongDriver(MonoBehaviour host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public bool IsRunning => _running != null;

    public bool TryStartCustom(CustomTracksSelectionSceneController controller)
    {
        if (IsRunning || controller == null)
            return false;

        Plugin.Logger?.LogInfo("QoLiTea: starting custom jukebox coroutine on controller");
        _running = controller.StartCoroutine(RunCustom(controller));
        return true;
    }

    public bool TryStartOfficial(TrackSelectionSceneController controller)
    {
        if (IsRunning || controller == null)
            return false;

        Plugin.Logger?.LogInfo("QoLiTea: starting official jukebox coroutine on controller");
        _running = controller.StartCoroutine(RunOfficial(controller));
        return true;
    }

    public void Cancel()
    {
        _running = null;
    }

    private void RememberLevelId(string levelId, int eligibleCount)
    {
        if (string.IsNullOrEmpty(levelId))
            return;

        var cap = RandomSongRules.RecentHistoryCap(eligibleCount);
        if (cap <= 0)
            return;

        if (_recentLevelIdSet.Add(levelId))
            _recentLevelIds.Enqueue(levelId);

        while (_recentLevelIds.Count > cap)
        {
            var old = _recentLevelIds.Dequeue();
            _recentLevelIdSet.Remove(old);
        }
    }

    private HashSet<int> BuildExcludedIndices(
        IList<ITrackMetadata> metas,
        List<int> eligibleIndices)
    {
        var excluded = new HashSet<int>();
        if (_recentLevelIdSet.Count == 0 || metas == null || eligibleIndices == null)
            return excluded;

        for (var i = 0; i < eligibleIndices.Count; i++)
        {
            var idx = eligibleIndices[i];
            if (idx < 0 || idx >= metas.Count)
                continue;
            var id = metas[idx]?.LevelId;
            if (!string.IsNullOrEmpty(id) && _recentLevelIdSet.Contains(id))
                excluded.Add(idx);
        }

        return excluded;
    }

    private static void SnapSelectionToIndex(
        BaseTrackSelectionOptionGroup group,
        IList<ITrackMetadata> metas,
        int targetIndex,
        Shared.Difficulty selectedDifficulty)
    {
        if (group == null || metas == null || metas.Count == 0)
            return;
        if (targetIndex < 0 || targetIndex >= metas.Count)
            return;

        var arr = new ITrackMetadata[metas.Count];
        for (var i = 0; i < metas.Count; i++)
            arr[i] = metas[i];

        group.RefreshTrackData(arr, targetIndex, selectedDifficulty);
    }

    private IEnumerator RunOfficial(TrackSelectionSceneController controller)
    {
        var disabledInput = false;
        try
        {
            var group = controller._infiniteTrackSelectionOptionGroup;
            if (group == null || !group.IsInitialized)
            {
                Plugin.Logger?.LogWarning("QoLiTea: official group missing/uninitialized");
                yield break;
            }

            yield return EnsureEligibleAndScroll(
                group,
                controller._selectedDifficulty,
                meta => controller.IsTrackLocked(meta),
                openAllFoldersWhenClosed: true,
                afterLand: (elig, indices) =>
                {
                    disabledInput = false;
                    var metas = group._trackMetaData;
                    var sid = group._selectedTrackIndex;
                    if (metas != null && sid >= 0 && sid < metas.Count)
                        RememberLevelId(metas[sid]?.LevelId, indices?.Count ?? 0);
                    return controller.GoToSelectedStageCoroutine();
                },
                setInputDisabled: v =>
                {
                    controller.InputDisabled = v;
                    disabledInput = v;
                });
        }
        finally
        {
            if (disabledInput && controller != null)
                controller.InputDisabled = false;
            _running = null;
        }
    }

    private IEnumerator RunCustom(CustomTracksSelectionSceneController controller)
    {
        var disabledInput = false;
        try
        {
            var group = controller._trackSelectionOptionGroup;
            if (group == null || !group.IsInitialized)
            {
                Plugin.Logger?.LogWarning("QoLiTea: custom group missing/uninitialized");
                yield break;
            }

            yield return EnsureEligibleAndScroll(
                group,
                controller._selectedDifficulty,
                _ => false,
                // Custom date folders: don't explode the whole Workshop list up front.
                // Empty pool still opens closed folders below.
                openAllFoldersWhenClosed: false,
                afterLand: (elig, indices) =>
                {
                    disabledInput = false;
                    return GoToCustomStageSkippingLoadout(controller, group, elig, indices);
                },
                setInputDisabled: v =>
                {
                    controller.InputDisabled = v;
                    disabledInput = v;
                });
        }
        finally
        {
            if (disabledInput && controller != null)
                controller.InputDisabled = false;
            _running = null;
        }
    }

    /// <summary>
    /// Resolve full UGC metadata (same providers as stock HandleTrackSubmitted), then start
    /// without opening the loadout. If the landed stub lied about difficulty availability,
    /// jump to another eligible track and retry (no long re-scroll).
    /// </summary>
    private IEnumerator GoToCustomStageSkippingLoadout(
        CustomTracksSelectionSceneController controller,
        BaseTrackSelectionOptionGroup group,
        List<bool> eligible,
        List<int> eligibleIndices)
    {
        if (controller == null || group == null)
            yield break;

        var metas = group._trackMetaData;
        if (metas == null || eligibleIndices == null || eligibleIndices.Count == 0)
            yield break;

        var tried = new HashSet<string>(StringComparer.Ordinal);
        const int maxAttempts = 10;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var idx = group._selectedTrackIndex;
            if (idx < 0 || idx >= metas.Count)
                yield break;

            var levelId = metas[idx]?.LevelId;
            if (string.IsNullOrEmpty(levelId) || RandomSongRules.IsPlaceholderLevelId(levelId))
            {
                var nextBad = PickUnusedEligibleIndex(metas, eligibleIndices, tried, idx);
                if (nextBad == null)
                    yield break;
                yield return JumpSelectionToIndex(group, eligible, nextBad.Value);
                yield return WaitHold(group, RandomSongRules.DeadStopHoldSeconds);
                continue;
            }

            tried.Add(levelId);

            controller.HandleTrackSelected(idx);
            yield return null;

            Plugin.Logger?.LogInfo($"QoLiTea: resolving custom track {levelId} (attempt {attempt + 1})");

            ITrackMetadata trackMetadata = null;
            var steamTask = SteamWorkshopUgcTrackProvider.Instance.GetTrackByLevelId(levelId);
            while (steamTask != null && !steamTask.IsCompleted)
                yield return null;

            if (steamTask != null && steamTask.IsFaulted)
                Plugin.Logger?.LogWarning($"QoLiTea: Steam GetTrackByLevelId faulted: {steamTask.Exception?.GetBaseException().Message}");
            else if (steamTask != null)
                trackMetadata = steamTask.Result;

            if (trackMetadata == null)
            {
                var localTask = LocalUgcTrackProvider.Instance.GetTrackByLevelId(levelId);
                while (localTask != null && !localTask.IsCompleted)
                    yield return null;

                if (localTask != null && localTask.IsFaulted)
                    Plugin.Logger?.LogWarning($"QoLiTea: Local GetTrackByLevelId faulted: {localTask.Exception?.GetBaseException().Message}");
                else if (localTask != null)
                    trackMetadata = localTask.Result;
            }

            if (trackMetadata == null && controller._customTrackMetadatas != null)
            {
                for (var i = 0; i < controller._customTrackMetadatas.Count; i++)
                {
                    var candidate = controller._customTrackMetadatas[i];
                    if (candidate != null && candidate.LevelId == levelId)
                    {
                        trackMetadata = candidate;
                        break;
                    }
                }
            }

            if (trackMetadata == null)
            {
                Plugin.Logger?.LogWarning($"QoLiTea: could not resolve custom track {levelId}");
                var nextMissing = PickUnusedEligibleIndex(metas, eligibleIndices, tried, idx);
                if (nextMissing == null)
                    yield break;
                yield return JumpSelectionToIndex(group, eligible, nextMissing.Value);
                yield return WaitHold(group, RandomSongRules.DeadStopHoldSeconds);
                continue;
            }

            controller._submittedTrackMetadata = trackMetadata;
            var difficulty = trackMetadata.GetDifficulty(controller._selectedDifficulty);
            controller._submittedTrackDifficulty = difficulty;

            if (difficulty != null
                && !string.IsNullOrEmpty(difficulty.BeatmapFilePath)
                && !difficulty.BeatCount.HasValue)
            {
                var estTask = controller.EstimateBeatCountForLegacyBeatmap(levelId, difficulty);
                while (estTask != null && !estTask.IsCompleted)
                    yield return null;

                if (estTask != null && !estTask.IsFaulted && estTask.Result != null)
                    controller._submittedTrackDifficulty = estTask.Result;
            }

            if (controller._submittedTrackDifficulty != null
                && !string.IsNullOrEmpty(controller._submittedTrackDifficulty.BeatmapFilePath))
            {
                Plugin.Logger?.LogInfo($"QoLiTea: starting custom stage {levelId}");
                RememberLevelId(levelId, eligibleIndices.Count);
                controller.GoToSelectedStage();
                yield break;
            }

            Plugin.Logger?.LogWarning(
                $"QoLiTea: {levelId} unavailable for {controller._selectedDifficulty} after resolve — trying another");

            var next = PickUnusedEligibleIndex(metas, eligibleIndices, tried, idx);
            if (next == null)
                break;

            yield return JumpSelectionToIndex(group, eligible, next.Value);
            // Dead-stop on the fallback so the song that starts is the one on screen.
            yield return WaitHold(group, RandomSongRules.DeadStopHoldSeconds);
        }

        Plugin.Logger?.LogWarning("QoLiTea: no custom track playable on current difficulty");
    }

    private static int? PickUnusedEligibleIndex(
        IList<ITrackMetadata> metas,
        List<int> eligibleIndices,
        HashSet<string> tried,
        int currentIndex)
    {
        if (metas == null || eligibleIndices == null)
            return null;

        var remaining = new List<int>(eligibleIndices.Count);
        for (var i = 0; i < eligibleIndices.Count; i++)
        {
            var idx = eligibleIndices[i];
            if (idx < 0 || idx >= metas.Count)
                continue;
            var id = metas[idx]?.LevelId;
            if (string.IsNullOrEmpty(id) || tried.Contains(id))
                continue;
            remaining.Add(idx);
        }

        if (remaining.Count == 0)
            return null;

        return RandomSongRules.PickTargetIndex(
            remaining,
            currentIndex,
            n => UnityEngine.Random.Range(0, n));
    }

    private static IEnumerator JumpSelectionToIndex(
        BaseTrackSelectionOptionGroup group,
        List<bool> eligible,
        int targetIndex)
    {
        if (group == null || eligible == null)
            yield break;

        var stockWrapped = group._isWrapped;
        var stockDuration = group._trackMovementDuration;
        group._isWrapped = true;
        try
        {
            var safeguard = Math.Min(eligible.Count, 48) + 4;
            while (RandomSongRules.ShouldKeepSteppingToTarget(
                       group._selectedTrackIndex, targetIndex, safeguard))
            {
                var cur = group._selectedTrackIndex;
                var ring = RandomSongRules.CollectEligibleIndices(eligible);
                var fromPos = RandomSongRules.IndexOnRing(ring, cur);
                var toPos = RandomSongRules.IndexOnRing(ring, targetIndex);
                if (fromPos < 0 || toPos < 0)
                {
                    yield return AdvanceEligibleTracks(
                        group, eligible, 1, 1, RandomSongRules.MicroMoveMinSeconds);
                }
                else
                {
                    var dir = RandomSongRules.ChooseDirectionOnRing(fromPos, toPos, ring.Count);
                    var remaining = RandomSongRules.CountRingSteps(fromPos, toPos, ring.Count, dir);
                    var burst = Math.Min(remaining, 40);
                    if (burst < 1)
                        burst = 1;

                    // Smooth micro-blur toward the fallback pick.
                    var micros = RandomSongRules.MicroStepCount(burst);
                    var microMove = RandomSongRules.MicroMoveSeconds(micros, 0.05f);
                    for (var m = 0; m < micros; m++)
                    {
                        var part = RandomSongRules.TracksForMicroStep(m, micros, burst);
                        if (part <= 0)
                            continue;
                        yield return AdvanceEligibleTracks(group, eligible, dir, part, microMove);
                    }
                }

                safeguard--;
                if (group._selectedTrackIndex == cur)
                    break;
            }
        }
        finally
        {
            if (group != null)
            {
                group._isWrapped = stockWrapped;
                group._trackMovementDuration = stockDuration;
            }
        }
    }

    private IEnumerator EnsureEligibleAndScroll(
        BaseTrackSelectionOptionGroup group,
        Shared.Difficulty selectedDifficulty,
        Func<ITrackMetadata, bool> isLocked,
        bool openAllFoldersWhenClosed,
        Func<List<bool>, List<int>, IEnumerator> afterLand,
        Action<bool> setInputDisabled)
    {
        var metas = group._trackMetaData;
        if (metas == null || metas.Count == 0)
        {
            Plugin.Logger?.LogWarning("QoLiTea: list empty");
            yield break;
        }

        BuildFlags(metas, group, selectedDifficulty, isLocked, out var eligible, out var navigable);
        var eligibleIndices = RandomSongRules.CollectEligibleIndices(eligible);

        // Official freestyle: if every folder is closed, open them all so the roll
        // has the full viewable track pool (not one random folder).
        if (openAllFoldersWhenClosed
            && CountOpenFolders(metas) == 0
            && CountClosedFolders(metas) > 0)
        {
            Plugin.Logger?.LogInfo("QoLiTea: no folders open; opening all before roll");
            OpenAllClosedFolders(metas, group, selectedDifficulty, isLocked);
            yield return null;
            yield return null;
            BuildFlags(metas, group, selectedDifficulty, isLocked, out eligible, out navigable);
            eligibleIndices = RandomSongRules.CollectEligibleIndices(eligible);
        }
        else if (eligibleIndices.Count == 0)
        {
            Plugin.Logger?.LogInfo("QoLiTea: no viewable playable tracks; opening all closed folders");
            if (!OpenAllClosedFolders(metas, group, selectedDifficulty, isLocked))
            {
                Plugin.Logger?.LogWarning("QoLiTea: no eligible tracks (and no closed folder to open)");
                yield break;
            }

            yield return null;
            yield return null;
            BuildFlags(metas, group, selectedDifficulty, isLocked, out eligible, out navigable);
            eligibleIndices = RandomSongRules.CollectEligibleIndices(eligible);
        }

        if (eligibleIndices.Count == 0)
        {
            Plugin.Logger?.LogWarning("QoLiTea: still no eligible tracks after opening folders");
            yield break;
        }

        var trackRing = eligibleIndices;
        Plugin.Logger?.LogInfo(
            $"QoLiTea: pool eligible={trackRing.Count} navigable={RandomSongRules.CollectNavigableIndices(navigable).Count}/{metas.Count} (sel={group._selectedTrackIndex})");

        setInputDisabled(true);

        // Nudge onto the track ring before planning so fromPos is meaningful.
        if (group._selectedTrackIndex < 0
            || group._selectedTrackIndex >= eligible.Count
            || !eligible[group._selectedTrackIndex])
        {
            var stockWrappedPrep = group._isWrapped;
            group._isWrapped = true;
            try
            {
                yield return AdvanceToNextTrack(
                    group, eligible, direction: 1, moveSeconds: RandomSongRules.DeadStopMoveSeconds);
            }
            finally
            {
                if (group != null)
                    group._isWrapped = stockWrappedPrep;
            }
        }

        var excluded = BuildExcludedIndices(metas, trackRing);
        var target = RandomSongRules.PickTargetIndex(
            trackRing,
            group._selectedTrackIndex,
            n => UnityEngine.Random.Range(0, n),
            excluded);
        if (target == null)
        {
            Plugin.Logger?.LogWarning("QoLiTea: pick failed");
            yield break;
        }

        var fromPos = RandomSongRules.IndexOnRing(trackRing, group._selectedTrackIndex);
        var toPos = RandomSongRules.IndexOnRing(trackRing, target.Value);
        if (fromPos < 0)
            fromPos = 0;
        if (toPos < 0)
        {
            Plugin.Logger?.LogWarning($"QoLiTea: target {target.Value} not on track ring");
            yield break;
        }

        var plan = RandomSongRules.PlanScrollToTarget(
            fromPos, toPos, trackRing.Count, RandomSongRules.DefaultMinJukeboxSteps);

        // Cap theatrical scroll only — keep the true pick and snap after if far.
        var needsSnap = plan.TrackSteps > RandomSongRules.MaxTrackStepsPerRoll;
        plan = RandomSongRules.CapScrollForTheater(plan, RandomSongRules.MaxTrackStepsPerRoll);

        var scrollTarget = target.Value;
        if (needsSnap)
        {
            var theaterLand = RandomSongRules.LandRingPos(
                fromPos, plan.Direction, plan.TrackSteps, trackRing.Count);
            scrollTarget = trackRing[theaterLand];
        }

        var budget = RandomSongRules.TargetScrollSeconds(trackRing.Count, plan.VisualSteps);
        var landName = metas[target.Value]?.TrackName ?? "?";
        Plugin.Logger?.LogInfo(
            $"QoLiTea: pick [{target.Value}] {landName} — visual={plan.VisualSteps} tracks={plan.TrackSteps} dir={plan.Direction} budget={budget:0.00}s snap={needsSnap} (from {group._selectedTrackIndex})");

        yield return ScrollToIndex(
            group, eligible, plan.Direction, plan.VisualSteps, plan.TrackSteps, budget, scrollTarget);

        if (group == null || !group.isActiveAndEnabled)
            yield break;

        if (group._selectedTrackIndex != target.Value)
        {
            Plugin.Logger?.LogInfo(
                $"QoLiTea: snap selection {group._selectedTrackIndex} → pick {target.Value}");
            SnapSelectionToIndex(group, metas, target.Value, selectedDifficulty);
            yield return WaitHold(group, RandomSongRules.DeadStopHoldSeconds);
        }

        if (!IsPlayableSelection(group, metas, selectedDifficulty, isLocked))
        {
            Plugin.Logger?.LogWarning(
                $"QoLiTea: landed non-playable at {group._selectedTrackIndex}; aborting play.");
            yield break;
        }

        var startedName = metas[group._selectedTrackIndex]?.TrackName ?? "?";
        Plugin.Logger?.LogInfo(
            $"QoLiTea: starting stage at {group._selectedTrackIndex} [{startedName}]");
        var cont = afterLand(eligible, eligibleIndices);
        if (cont != null)
            yield return cont;
    }

    private static int CountOpenFolders(IList<ITrackMetadata> metas)
    {
        var n = 0;
        for (var i = 0; i < metas.Count; i++)
        {
            if (metas[i] is FolderTrackMetadata { IsOpen: true })
                n++;
        }

        return n;
    }

    private static int CountClosedFolders(IList<ITrackMetadata> metas)
    {
        var n = 0;
        for (var i = 0; i < metas.Count; i++)
        {
            if (metas[i] is FolderTrackMetadata { IsOpen: false })
                n++;
        }

        return n;
    }

    private static bool OpenAllClosedFolders(
        IList<ITrackMetadata> metas,
        BaseTrackSelectionOptionGroup group,
        Shared.Difficulty selectedDifficulty,
        Func<ITrackMetadata, bool> isLocked)
    {
        var opened = 0;
        for (var i = 0; i < metas.Count; i++)
        {
            if (metas[i] is not FolderTrackMetadata folder || folder.IsOpen)
                continue;
            if (RandomSongRules.IsDisabledDifficultyFolderId(folder.LevelId))
                continue;

            var childPlayable = 0;
            for (var c = 1; c <= folder.NumTracks && i + c < metas.Count; c++)
            {
                var child = metas[i + c];
                if (child == null || child is FolderTrackMetadata)
                    continue;
                if (child.Category == TrackCategory.Filler || child.Category == TrackCategory.Tutorial)
                    continue;
                if (isLocked(child))
                    continue;
                if (child.GetDifficulty(selectedDifficulty) == null)
                    continue;
                if (!IsTrackSelectOptionTypePlayable(group.GetTrackTypeForMetadata(child)))
                    continue;
                childPlayable++;
            }

            if (childPlayable <= 0)
                continue;

            Plugin.Logger?.LogInfo($"QoLiTea: opening folder [{i}] {folder.TrackName}");
            folder.IsOpen = true;
            opened++;
        }

        return opened > 0;
    }

    private static void BuildFlags(
        IList<ITrackMetadata> metas,
        BaseTrackSelectionOptionGroup group,
        Shared.Difficulty selectedDifficulty,
        Func<ITrackMetadata, bool> isLocked,
        out List<bool> eligible,
        out List<bool> navigable)
    {
        eligible = new List<bool>(metas.Count);
        navigable = new List<bool>(metas.Count);
        for (var i = 0; i < metas.Count; i++)
        {
            var meta = metas[i];
            var insideClosed = IsInsideClosedFolder(metas, group, i);
            navigable.Add(RandomSongRules.IsNavigableRow(insideClosed));

            if (meta == null)
            {
                eligible.Add(false);
                continue;
            }

            if (RandomSongRules.IsPlaceholderLevelId(meta.LevelId))
            {
                eligible.Add(false);
                continue;
            }

            var insideDisabled = IsInsideDisabledDifficultyFolder(metas, group, i);
            var isFolder = meta is FolderTrackMetadata || meta.Category == TrackCategory.Folder;
            var diff = meta.GetDifficulty(selectedDifficulty);
            var hasDiff = RandomSongRules.HasPlayableDifficulty(
                diff != null,
                diff?.BeatmapFilePath);

            eligible.Add(RandomSongRules.IsEligibleRow(
                isFolder,
                IsTrackSelectOptionTypePlayable(group.GetTrackTypeForMetadata(meta)),
                meta.Category == TrackCategory.Filler || meta.Category == TrackCategory.Tutorial,
                isLocked(meta) || insideDisabled,
                insideClosed,
                hasDiff));
        }
    }

    private static bool IsInsideClosedFolder(
        IList<ITrackMetadata> metas,
        BaseTrackSelectionOptionGroup group,
        int index)
    {
        var folderIdx = group.GetFolderIndexForTrack(index);
        if (folderIdx < 0 || folderIdx == index)
            return false;
        if (folderIdx >= metas.Count)
            return false;

        return metas[folderIdx] is FolderTrackMetadata { IsOpen: false };
    }

    private static bool IsInsideDisabledDifficultyFolder(
        IList<ITrackMetadata> metas,
        BaseTrackSelectionOptionGroup group,
        int index)
    {
        var folderIdx = group.GetFolderIndexForTrack(index);
        if (folderIdx < 0 || folderIdx >= metas.Count)
            return false;
        if (metas[folderIdx] is not FolderTrackMetadata folder)
            return false;
        return RandomSongRules.IsDisabledDifficultyFolderId(folder.LevelId);
    }

    private static bool IsPlayableSelection(
        BaseTrackSelectionOptionGroup group,
        IList<ITrackMetadata> metas,
        Shared.Difficulty selectedDifficulty,
        Func<ITrackMetadata, bool> isLocked)
    {
        var i = group._selectedTrackIndex;
        if (i < 0 || i >= metas.Count)
            return false;

        var meta = metas[i];
        if (meta == null || RandomSongRules.IsPlaceholderLevelId(meta.LevelId))
            return false;

        if (IsInsideDisabledDifficultyFolder(metas, group, i))
            return false;

        var isFolder = meta is FolderTrackMetadata || meta.Category == TrackCategory.Folder;
        var diff = meta.GetDifficulty(selectedDifficulty);
        return RandomSongRules.IsEligibleRow(
            isFolder,
            IsTrackSelectOptionTypePlayable(group.GetTrackTypeForMetadata(meta)),
            meta.Category == TrackCategory.Filler || meta.Category == TrackCategory.Tutorial,
            isLocked(meta),
            IsInsideClosedFolder(metas, group, i),
            RandomSongRules.HasPlayableDifficulty(diff != null, diff?.BeatmapFilePath));
    }

    /// <summary>
    /// Scroll visual ticks toward a pre-picked target. Large pools jump many eligible
    /// tracks per tick so wall-clock stays near the budget; last ticks are single-track.
    /// </summary>
    private static IEnumerator ScrollToIndex(
        BaseTrackSelectionOptionGroup group,
        List<bool> eligible,
        int direction,
        int visualSteps,
        int trackSteps,
        float targetScrollSeconds,
        int targetIndex)
    {
        if (visualSteps <= 0 || trackSteps <= 0)
            yield break;

        var stockDuration = group._trackMovementDuration;
        // Freestyle lists often have _isWrapped=false when few rows are visible;
        // NavigateTrackList then refuses past the ends ("bounce"). Force wrap for the roll.
        var stockWrapped = group._isWrapped;
        group._isWrapped = true;

        try
        {
            Plugin.Logger?.LogInfo(
                $"QoLiTea: scrolling visual={visualSteps} tracks={trackSteps} (dir {direction}, budget {targetScrollSeconds:0.00}s, wrap forced)");

            for (var i = 0; i < visualSteps; i++)
            {
                if (group == null || !group.isActiveAndEnabled)
                    yield break;

                var jump = RandomSongRules.TracksForVisualStep(i, visualSteps, trackSteps);
                if (jump <= 0)
                    continue;

                var moveSeconds = RandomSongRules.TrackMoveSeconds(i, visualSteps, targetScrollSeconds);
                var holdSeconds = RandomSongRules.TrackHoldSeconds(i, visualSteps, targetScrollSeconds);

                // Large jumps: several short stock lerps (blur) instead of one teleport.
                var micros = RandomSongRules.MicroStepCount(jump);
                var microMove = RandomSongRules.MicroMoveSeconds(micros, moveSeconds);
                for (var m = 0; m < micros; m++)
                {
                    if (group == null || !group.isActiveAndEnabled)
                        yield break;

                    var chunk = RandomSongRules.TracksForMicroStep(m, micros, jump);
                    if (chunk <= 0)
                        continue;

                    group._trackMovementDuration = microMove;
                    yield return AdvanceEligibleTracks(group, eligible, direction, chunk, microMove);
                }

                yield return WaitHold(group, holdSeconds);
            }

            // If a folder skip desynced the ring, crawl to the pick at dead-stop pace.
            var safeguard = Math.Min(eligible.Count, 64) + 4;
            while (RandomSongRules.ShouldKeepSteppingToTarget(
                       group._selectedTrackIndex, targetIndex, safeguard))
            {
                if (group == null || !group.isActiveAndEnabled)
                    yield break;

                var cur = group._selectedTrackIndex;
                group._trackMovementDuration = RandomSongRules.DeadStopMoveSeconds;
                yield return AdvanceEligibleTracks(
                    group, eligible, direction, eligibleCount: 1, RandomSongRules.DeadStopMoveSeconds);
                yield return WaitHold(group, RandomSongRules.DeadStopHoldSeconds);
                safeguard--;
                if (group._selectedTrackIndex == cur)
                    break;
            }

            yield return WaitUntilIdle(group);
        }
        finally
        {
            if (group != null)
            {
                group._trackMovementDuration = stockDuration;
                group._isWrapped = stockWrapped;
            }
        }
    }

    /// <summary>
    /// Advance <paramref name="eligibleCount"/> playable tracks using stock ±1 NavigateTrackList
    /// only. Large multi-deltas desync the highlighted row from _selectedTrackIndex (UI shows
    /// one song, start uses another).
    /// </summary>
    private static IEnumerator AdvanceEligibleTracks(
        BaseTrackSelectionOptionGroup group,
        List<bool> eligible,
        int direction,
        int eligibleCount,
        float moveSeconds)
    {
        if (group == null || eligible == null || eligible.Count == 0 || eligibleCount <= 0)
            yield break;

        var stepDir = direction >= 0 ? 1 : -1;
        var expectedOnes = CountPhysicalStepsForEligibleJump(group, eligible, direction, eligibleCount);
        if (expectedOnes < 1)
            expectedOnes = 1;

        var slice = moveSeconds / expectedOnes;
        // Huge bursts stay snappy; small bursts keep a readable lerp.
        if (expectedOnes <= RandomSongRules.MaxMicroStepsPerVisualTick)
        {
            if (slice < RandomSongRules.MicroMoveMinSeconds)
                slice = RandomSongRules.MicroMoveMinSeconds;
        }
        else if (slice < 0.001f)
        {
            slice = 0.001f;
        }

        for (var e = 0; e < eligibleCount; e++)
        {
            if (group == null || !group.isActiveAndEnabled)
                yield break;

            var physical = CountPhysicalStepsForEligibleJump(group, eligible, direction, 1);
            if (physical <= 0)
                physical = 1;

            for (var s = 0; s < physical; s++)
            {
                if (group == null || !group.isActiveAndEnabled)
                    yield break;

                group._trackMovementDuration = slice;
                yield return WaitUntilIdle(group);
                group.NavigateTrackList(stepDir);
                yield return WaitUntilIdle(group);
            }
        }

        var guard = Math.Min(eligible.Count, 32) + 2;
        var start = group._selectedTrackIndex;
        while (guard-- > 0
               && group != null
               && (group._selectedTrackIndex < 0
                   || group._selectedTrackIndex >= eligible.Count
                   || !eligible[group._selectedTrackIndex]))
        {
            group.NavigateTrackList(stepDir);
            yield return WaitUntilIdle(group);
            if (group._selectedTrackIndex == start)
                break;
        }
    }

    private static int CountPhysicalStepsForEligibleJump(
        BaseTrackSelectionOptionGroup group,
        List<bool> eligible,
        int direction,
        int eligibleCount)
    {
        var cur = group._selectedTrackIndex;
        var physical = 0;
        var stepDir = direction >= 0 ? 1 : -1;
        for (var e = 0; e < eligibleCount; e++)
        {
            var guard = eligible.Count + 2;
            var stepped = 0;
            while (guard-- > 0)
            {
                var next = group.AdjustTrackIndex(cur, stepDir);
                stepped++;
                cur = next;
                if (cur >= 0 && cur < eligible.Count && eligible[cur])
                    break;
                if (stepped > eligible.Count)
                    break;
            }

            physical += Math.Max(1, stepped);
        }

        return physical;
    }

    private static IEnumerator AdvanceToNextTrack(
        BaseTrackSelectionOptionGroup group,
        List<bool> eligible,
        int direction,
        float moveSeconds)
        => AdvanceEligibleTracks(group, eligible, direction, eligibleCount: 1, moveSeconds);

    private static IEnumerator WaitUntilIdle(BaseTrackSelectionOptionGroup group)
    {
        var frames = 0;
        while (group != null && group.IsPerformingMovement && frames++ < 180)
            yield return null;
    }

    private static IEnumerator WaitHold(BaseTrackSelectionOptionGroup group, float holdSeconds)
    {
        var wait = Mathf.Max(0.02f, holdSeconds);
        var end = Time.unscaledTime + wait;
        while (Time.unscaledTime < end)
        {
            if (group == null)
                yield break;
            yield return null;
        }

        yield return WaitUntilIdle(group);
    }
}
