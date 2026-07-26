using System;
using System.Collections.Generic;

namespace TeaQoLs.Features.RandomSong;

/// <summary>
/// Pure random-song pick + scroll pacing (no Unity).
/// </summary>
public static class RandomSongRules
{
    public const int DefaultMinJukeboxSteps = 8;
    /// <summary>Hard cap on UI ticks so a 1000-track custom list still finishes in budget.</summary>
    public const int MaxVisualJukeboxSteps = 16;
    /// <summary>
    /// Max eligible ±1 NavigateTrackList steps per roll. Stock UI desyncs if we jump by more
    /// than ±1 per call; this keeps long lists snappy without teleporting the highlight.
    /// </summary>
    public const int MaxTrackStepsPerRoll = 56;
    /// <summary>Last visual ticks are always single-track for the dead-stop feel.</summary>
    public const int SettleVisualSteps = 3;
    /// <summary>
    /// Within one visual tick, split a large track jump into this many stock lerps
    /// so multi-track advances blur smoothly instead of teleporting.
    /// </summary>
    public const int MaxMicroStepsPerVisualTick = 8;
    /// <summary>Floor for each micro-lerp duration (seconds).</summary>
    public const float MicroMoveMinSeconds = 0.012f;
    /// <summary>Total scroll budget floor (seconds).</summary>
    public const float MinScrollSeconds = 1.3f;
    /// <summary>Total scroll budget ceiling (seconds).</summary>
    public const float MaxScrollSeconds = 2.6f;
    /// <summary>Final track tick move — settle on the pick.</summary>
    public const float DeadStopMoveSeconds = 0.1f;
    /// <summary>Final track tick hold — dead stop on the selected track.</summary>
    public const float DeadStopHoldSeconds = 0.16f;
    /// <summary>Fraction of each non-final step budget spent on the stock mover.</summary>
    public const float MoveBudgetFraction = 0.45f;

    public static float Clamp01(float t)
    {
        if (t <= 0f)
            return 0f;
        if (t >= 1f)
            return 1f;
        return t;
    }

    public static float Lerp(float a, float b, float t)
        => a + (b - a) * t;

    /// <summary>
    /// Progress 0 = first track tick, 1 = last. Ease-in so the roll starts fast and settles.
    /// </summary>
    public static float EaseInQuad(float t)
    {
        t = Clamp01(t);
        return t * t;
    }

    /// <summary>Harder late brake than quad — spends more of the slowdown on the last ticks.</summary>
    public static float EaseInCubic(float t)
    {
        t = Clamp01(t);
        return t * t * t;
    }

    /// <summary>Harder late brake than cubic (kept for comparisons / callers).</summary>
    public static float EaseInQuint(float t)
    {
        t = Clamp01(t);
        return t * t * t * t * t;
    }

    public static bool IsEligibleRow(
        bool isFolder,
        bool isPlayableOptionType,
        bool isFillerOrTutorial,
        bool isLocked,
        bool isInsideClosedFolder,
        bool hasSelectedDifficulty)
    {
        if (isFolder || !isPlayableOptionType || isFillerOrTutorial || isLocked)
            return false;
        if (isInsideClosedFolder || !hasSelectedDifficulty)
            return false;
        return true;
    }

    /// <summary>Workshop browse / editor rows — not playable stages.</summary>
    public static bool IsPlaceholderLevelId(string levelId)
        => !string.IsNullOrEmpty(levelId)
           && levelId.StartsWith("Placeholder_", StringComparison.Ordinal);

    /// <summary>
    /// Stock puts charts missing the current difficulty under a folder whose LevelId
    /// contains <c>_disabledTrack</c>.
    /// </summary>
    public static bool IsDisabledDifficultyFolderId(string levelId)
        => !string.IsNullOrEmpty(levelId)
           && levelId.IndexOf("_disabledTrack", StringComparison.OrdinalIgnoreCase) >= 0;

    /// <summary>
    /// True when GetDifficulty exists and (if a beatmap path is known) it is non-empty.
    /// LazyCustomTracks stubs always have a null path — those still pass when GetDifficulty is non-null.
    /// </summary>
    public static bool HasPlayableDifficulty(bool getDifficultyNonNull, string beatmapFilePath)
    {
        if (!getDifficultyNonNull)
            return false;
        // null path = stub / unknown — allow into pool; resolve must confirm before start.
        if (beatmapFilePath == null)
            return true;
        return beatmapFilePath.Length > 0;
    }

    /// <summary>
    /// Indices stock NavigateTrackList can land on (not children of closed folders).
    /// </summary>
    public static bool IsNavigableRow(bool isInsideClosedFolder)
        => !isInsideClosedFolder;

    public static List<int> CollectEligibleIndices(IReadOnlyList<bool> eligibleByIndex)
    {
        var result = new List<int>();
        if (eligibleByIndex == null)
            return result;

        for (var i = 0; i < eligibleByIndex.Count; i++)
        {
            if (eligibleByIndex[i])
                result.Add(i);
        }

        return result;
    }

    public static List<int> CollectNavigableIndices(IReadOnlyList<bool> navigableByIndex)
        => CollectEligibleIndices(navigableByIndex);

    /// <summary>
    /// Picks a random eligible index. Prefer not current when other choices exist.
    /// </summary>
    public static int? PickTargetIndex(
        IReadOnlyList<int> eligibleIndices,
        int currentIndex,
        Func<int, int> nextExclusive)
    {
        if (eligibleIndices == null || eligibleIndices.Count == 0 || nextExclusive == null)
            return null;

        if (eligibleIndices.Count == 1)
            return eligibleIndices[0];

        var others = new List<int>(eligibleIndices.Count);
        for (var i = 0; i < eligibleIndices.Count; i++)
        {
            var idx = eligibleIndices[i];
            if (idx != currentIndex)
                others.Add(idx);
        }

        IReadOnlyList<int> pool = others.Count > 0 ? others : eligibleIndices;
        var pick = nextExclusive(pool.Count);
        if (pick < 0 || pick >= pool.Count)
            return null;

        return pool[pick];
    }

    /// <summary>
    /// Shortest direction on a navigable ring (+1 / -1). Tie → +1.
    /// </summary>
    public static int ChooseDirectionOnRing(int fromPos, int toPos, int ringCount)
    {
        if (ringCount <= 0 || fromPos == toPos)
            return 1;

        var forward = (toPos - fromPos + ringCount) % ringCount;
        var backward = (fromPos - toPos + ringCount) % ringCount;
        return forward <= backward ? 1 : -1;
    }

    public static int CountRingSteps(int fromPos, int toPos, int ringCount, int direction)
    {
        if (ringCount <= 0 || fromPos == toPos)
            return 0;

        if (direction >= 0)
            return (toPos - fromPos + ringCount) % ringCount;

        return (fromPos - toPos + ringCount) % ringCount;
    }

    public static int IndexOnRing(IReadOnlyList<int> ring, int absoluteIndex)
    {
        if (ring == null)
            return -1;
        for (var i = 0; i < ring.Count; i++)
        {
            if (ring[i] == absoluteIndex)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Snap a selection that sits inside a closed folder onto that folder header for nav math.
    /// </summary>
    public static int SnapToNavigable(int index, int folderIndexIfInsideClosed)
        => folderIndexIfInsideClosed >= 0 ? folderIndexIfInsideClosed : index;

    public static int EnsureMinSteps(int rawSteps, int ringCount, int minSteps)
    {
        if (rawSteps < 0)
            rawSteps = 0;
        if (ringCount <= 1 || minSteps <= rawSteps)
            return rawSteps;

        var need = minSteps - rawSteps;
        var loops = (need + ringCount - 1) / ringCount;
        return rawSteps + loops * ringCount;
    }

    public static int LandRingPos(int fromPos, int direction, int steps, int ringCount)
    {
        if (ringCount <= 0)
            return 0;

        var delta = direction >= 0 ? steps : -steps;
        var landed = (fromPos + delta) % ringCount;
        if (landed < 0)
            landed += ringCount;
        return landed;
    }

    public readonly struct ScrollPlan
    {
        public ScrollPlan(int direction, int visualSteps, int trackSteps)
        {
            Direction = direction >= 0 ? 1 : -1;
            VisualSteps = visualSteps < 0 ? 0 : visualSteps;
            TrackSteps = trackSteps < 0 ? 0 : trackSteps;
        }

        public int Direction { get; }
        /// <summary>UI ticks (move+hold). Capped so huge lists stay snappy.</summary>
        public int VisualSteps { get; }
        /// <summary>Eligible tracks traveled across those ticks (may be ≫ VisualSteps).</summary>
        public int TrackSteps { get; }

        /// <summary>Alias for VisualSteps (older call sites).</summary>
        public int Steps => VisualSteps;
    }

    /// <summary>
    /// Pre-picked target path: shortest ring direction. Pads with full loops only when the
    /// ring is small enough to fit in the visual budget; large pools jump many tracks per tick.
    /// </summary>
    public static ScrollPlan PlanScrollToTarget(
        int fromPos,
        int toPos,
        int ringCount,
        int minSteps,
        int maxVisualSteps = MaxVisualJukeboxSteps)
    {
        if (ringCount <= 0)
            return new ScrollPlan(1, 0, 0);

        if (maxVisualSteps < 1)
            maxVisualSteps = 1;

        var direction = ChooseDirectionOnRing(fromPos, toPos, ringCount);
        var raw = CountRingSteps(fromPos, toPos, ringCount, direction);
        if (raw == 0)
            raw = ringCount > 1 ? ringCount : 0;

        int trackSteps;
        if (ringCount <= maxVisualSteps)
        {
            // Small list: allow full-lap padding so short hops still spin a bit.
            trackSteps = EnsureMinSteps(raw, ringCount, minSteps);
        }
        else
        {
            // Large list: never add an 800-track lap — cover shortest path in capped ticks.
            trackSteps = raw < 1 ? 1 : raw;
            if (trackSteps < minSteps)
                trackSteps = minSteps;
            // Keep congruent to landing on target: trackSteps ≡ raw (mod ringCount).
            if (raw > 0 && trackSteps % ringCount != raw % ringCount)
            {
                // minSteps pad without full laps isn't congruent — stick to shortest path.
                trackSteps = raw;
            }
        }

        var visual = trackSteps;
        if (visual > maxVisualSteps)
            visual = maxVisualSteps;
        if (visual < 1 && trackSteps > 0)
            visual = 1;

        return new ScrollPlan(direction, visual, trackSteps);
    }

    /// <summary>
    /// How many eligible tracks to jump on visual tick <paramref name="visualIndex"/>.
    /// Cruise ticks absorb the distance; the last <see cref="SettleVisualSteps"/> are 1 each.
    /// </summary>
    public static int TracksForVisualStep(
        int visualIndex,
        int visualSteps,
        int totalTrackSteps,
        int settleSteps = SettleVisualSteps)
    {
        if (visualSteps <= 0 || totalTrackSteps <= 0)
            return 0;
        if (visualIndex < 0 || visualIndex >= visualSteps)
            return 0;

        if (visualSteps == 1)
            return totalTrackSteps;

        if (settleSteps < 1)
            settleSteps = 1;
        if (settleSteps >= visualSteps)
            settleSteps = visualSteps - 1;

        // Last settleSteps ticks: 1 track each (dead-stop runway).
        if (visualIndex >= visualSteps - settleSteps)
            return 1;

        var cruiseVisual = visualSteps - settleSteps;
        var cruiseTracks = totalTrackSteps - settleSteps;
        if (cruiseTracks < cruiseVisual)
        {
            // More cruise ticks than tracks — first cruiseTracks ticks move 1, rest idle.
            return visualIndex < cruiseTracks ? 1 : 0;
        }

        var baseJump = cruiseTracks / cruiseVisual;
        var rem = cruiseTracks % cruiseVisual;
        return baseJump + (visualIndex < rem ? 1 : 0);
    }

    /// <summary>
    /// How many stock NavigateTrackList lerps to use for a track jump (smooth blur, capped).
    /// </summary>
    public static int MicroStepCount(int trackJump, int maxMicroSteps = MaxMicroStepsPerVisualTick)
    {
        if (trackJump <= 0)
            return 0;
        if (maxMicroSteps < 1)
            maxMicroSteps = 1;
        return trackJump <= maxMicroSteps ? trackJump : maxMicroSteps;
    }

    /// <summary>Distribute <paramref name="trackJump"/> across micro-lerps (no settle bias).</summary>
    public static int TracksForMicroStep(int microIndex, int microCount, int trackJump)
    {
        if (microCount <= 0 || trackJump <= 0)
            return 0;
        if (microIndex < 0 || microIndex >= microCount)
            return 0;
        if (microCount == 1)
            return trackJump;

        var baseJump = trackJump / microCount;
        var rem = trackJump % microCount;
        return baseJump + (microIndex < rem ? 1 : 0);
    }

    /// <summary>
    /// Duration for one micro-lerp; splits the tick move budget, never below
    /// <see cref="MicroMoveMinSeconds"/>.
    /// </summary>
    public static float MicroMoveSeconds(int microCount, float tickMoveSeconds)
    {
        if (microCount <= 1)
            return Math.Max(MicroMoveMinSeconds, tickMoveSeconds);
        var each = tickMoveSeconds / microCount;
        return each < MicroMoveMinSeconds ? MicroMoveMinSeconds : each;
    }

    /// <summary>
    /// Total move+hold budget for the scroll. Uses visual tick count (not raw track distance)
    /// so a 1000-song list still lands in MaxScrollSeconds.
    /// </summary>
    public static float TargetScrollSeconds(int ringCount, int visualSteps)
    {
        var byRing = Lerp(MinScrollSeconds, MaxScrollSeconds, Clamp01(ringCount / 200f));
        var bySteps = Lerp(MinScrollSeconds, MaxScrollSeconds, Clamp01(visualSteps / 16f));
        var seconds = Math.Max(byRing, bySteps);
        if (seconds < MinScrollSeconds)
            return MinScrollSeconds;
        if (seconds > MaxScrollSeconds)
            return MaxScrollSeconds;
        return seconds;
    }

    public static float StepProgress(int stepIndex, int totalSteps)
    {
        if (totalSteps <= 1)
            return 1f;
        return stepIndex / (float)(totalSteps - 1);
    }

    /// <summary>Relative time weight per tick — fast early, brakes on the last ticks (cubic).</summary>
    public static float StepWeight(int stepIndex, int totalSteps)
    {
        if (totalSteps <= 1)
            return 1f;
        // Higher early floor → snappier start; cubic (not quint) → less grind on the last 2–3.
        return Lerp(0.45f, 1f, EaseInCubic(StepProgress(stepIndex, totalSteps)));
    }

    public static float SumStepWeights(int totalSteps)
    {
        var sum = 0f;
        for (var i = 0; i < totalSteps; i++)
            sum += StepWeight(i, totalSteps);
        return sum <= 0f ? 1f : sum;
    }

    /// <summary>
    /// Weights for cruise ticks only (excludes the final dead-stop tick).
    /// </summary>
    public static float SumCruiseStepWeights(int totalSteps)
    {
        if (totalSteps <= 1)
            return 1f;

        var sum = 0f;
        for (var i = 0; i < totalSteps - 1; i++)
            sum += StepWeight(i, totalSteps);
        return sum <= 0f ? 1f : sum;
    }

    public static float CruiseBudgetSeconds(float targetScrollSeconds)
    {
        var reserved = DeadStopMoveSeconds + DeadStopHoldSeconds;
        var cruise = targetScrollSeconds - reserved;
        return cruise < 0.4f ? 0.4f : cruise;
    }

    public static float TrackHoldSeconds(int stepIndex, int totalSteps, float targetScrollSeconds)
    {
        if (totalSteps <= 1 || stepIndex >= totalSteps - 1)
            return DeadStopHoldSeconds;

        var stepBudget = CruiseBudgetSeconds(targetScrollSeconds)
            * StepWeight(stepIndex, totalSteps)
            / SumCruiseStepWeights(totalSteps);
        return Math.Max(0.008f, stepBudget * (1f - MoveBudgetFraction));
    }

    public static float TrackMoveSeconds(int stepIndex, int totalSteps, float targetScrollSeconds)
    {
        if (totalSteps <= 1 || stepIndex >= totalSteps - 1)
            return DeadStopMoveSeconds;

        var stepBudget = CruiseBudgetSeconds(targetScrollSeconds)
            * StepWeight(stepIndex, totalSteps)
            / SumCruiseStepWeights(totalSteps);
        return Math.Max(0.012f, stepBudget * MoveBudgetFraction);
    }

    public static bool ShouldKeepSteppingToTarget(int currentIndex, int targetIndex, int safeguardRemaining)
        => currentIndex != targetIndex && safeguardRemaining > 0;
}
