using System.Collections.Generic;
using QoLiTea.Features.RandomSong;
using Xunit;

namespace QoLiTea.Tests;

public class RandomSongRulesTests
{
    [Fact]
    public void IsEligibleRow_rejects_folders_locked_and_closed_children()
    {
        Assert.False(RandomSongRules.IsEligibleRow(
            isFolder: true, isPlayableOptionType: true, isFillerOrTutorial: false,
            isLocked: false, isInsideClosedFolder: false, hasSelectedDifficulty: true));
        Assert.False(RandomSongRules.IsEligibleRow(
            isFolder: false, isPlayableOptionType: false, isFillerOrTutorial: false,
            isLocked: false, isInsideClosedFolder: false, hasSelectedDifficulty: true));
        Assert.False(RandomSongRules.IsEligibleRow(
            isFolder: false, isPlayableOptionType: true, isFillerOrTutorial: false,
            isLocked: true, isInsideClosedFolder: false, hasSelectedDifficulty: true));
        Assert.False(RandomSongRules.IsEligibleRow(
            isFolder: false, isPlayableOptionType: true, isFillerOrTutorial: false,
            isLocked: false, isInsideClosedFolder: true, hasSelectedDifficulty: true));
        Assert.False(RandomSongRules.IsEligibleRow(
            isFolder: false, isPlayableOptionType: true, isFillerOrTutorial: false,
            isLocked: false, isInsideClosedFolder: false, hasSelectedDifficulty: false));
        Assert.True(RandomSongRules.IsEligibleRow(
            isFolder: false, isPlayableOptionType: true, isFillerOrTutorial: false,
            isLocked: false, isInsideClosedFolder: false, hasSelectedDifficulty: true));
    }

    [Fact]
    public void Placeholder_and_disabled_folder_ids_are_detected()
    {
        Assert.True(RandomSongRules.IsPlaceholderLevelId("Placeholder_Browse_Workshop"));
        Assert.False(RandomSongRules.IsPlaceholderLevelId("ws123"));
        Assert.True(RandomSongRules.IsDisabledDifficultyFolderId("folder_TimeAddedDescending_disabledTrack"));
        Assert.False(RandomSongRules.IsDisabledDifficultyFolderId("folder_artist_foo"));
    }

    [Fact]
    public void HasPlayableDifficulty_rejects_null_diff_and_empty_path()
    {
        Assert.False(RandomSongRules.HasPlayableDifficulty(getDifficultyNonNull: false, beatmapFilePath: null));
        Assert.True(RandomSongRules.HasPlayableDifficulty(getDifficultyNonNull: true, beatmapFilePath: null)); // stub
        Assert.False(RandomSongRules.HasPlayableDifficulty(getDifficultyNonNull: true, beatmapFilePath: ""));
        Assert.True(RandomSongRules.HasPlayableDifficulty(getDifficultyNonNull: true, beatmapFilePath: "x.json"));
    }

    [Fact]
    public void CollectEligibleIndices_keeps_only_true_flags()
    {
        var eligible = new[] { false, true, false, true, true };
        Assert.Equal(new[] { 1, 3, 4 }, RandomSongRules.CollectEligibleIndices(eligible));
    }

    [Fact]
    public void PickTargetIndex_prefers_not_current_when_others_exist()
    {
        var eligible = new List<int> { 2, 5, 9 };
        var pick = RandomSongRules.PickTargetIndex(eligible, currentIndex: 5, nextExclusive: n =>
        {
            Assert.Equal(2, n);
            return 1;
        });
        Assert.Equal(9, pick);
    }

    [Fact]
    public void PickTargetIndex_skips_excluded_indices_when_others_remain()
    {
        var eligible = new List<int> { 2, 5, 9, 12 };
        var excluded = new HashSet<int> { 5, 9 };
        var pick = RandomSongRules.PickTargetIndex(
            eligible,
            currentIndex: 2,
            nextExclusive: n =>
            {
                Assert.Equal(1, n); // only 12 left (2 is current)
                return 0;
            },
            excludedIndices: excluded);
        Assert.Equal(12, pick);
    }

    [Fact]
    public void PickTargetIndex_falls_back_when_exclusions_empty_the_non_current_pool()
    {
        var eligible = new List<int> { 2, 5, 9 };
        var excluded = new HashSet<int> { 5, 9 };
        // All non-current excluded → fall back to non-current-only (ignore exclusions).
        var pick = RandomSongRules.PickTargetIndex(
            eligible,
            currentIndex: 2,
            nextExclusive: n =>
            {
                Assert.Equal(2, n);
                return 0;
            },
            excludedIndices: excluded);
        Assert.Equal(5, pick);
    }

    [Fact]
    public void CapScrollForTheater_preserves_direction_and_clamps_track_steps()
    {
        var plan = new RandomSongRules.ScrollPlan(direction: 1, visualSteps: 16, trackSteps: 400);
        var capped = RandomSongRules.CapScrollForTheater(plan, maxTrackSteps: 56);
        Assert.Equal(1, capped.Direction);
        Assert.Equal(56, capped.TrackSteps);
        Assert.Equal(RandomSongRules.MaxVisualJukeboxSteps, capped.VisualSteps);
    }

    [Fact]
    public void CapScrollForTheater_leaves_short_plans_unchanged()
    {
        var plan = new RandomSongRules.ScrollPlan(direction: -1, visualSteps: 10, trackSteps: 40);
        var capped = RandomSongRules.CapScrollForTheater(plan, maxTrackSteps: 56);
        Assert.Equal(-1, capped.Direction);
        Assert.Equal(40, capped.TrackSteps);
        Assert.Equal(10, capped.VisualSteps);
    }

    [Fact]
    public void RecentHistoryCap_is_min_of_pool_minus_one_and_32()
    {
        Assert.Equal(0, RandomSongRules.RecentHistoryCap(eligibleCount: 1));
        Assert.Equal(4, RandomSongRules.RecentHistoryCap(eligibleCount: 5));
        Assert.Equal(32, RandomSongRules.RecentHistoryCap(eligibleCount: 1000));
    }

    [Fact]
    public void ChooseDirectionOnRing_picks_shortest_wrap()
    {
        Assert.Equal(1, RandomSongRules.ChooseDirectionOnRing(fromPos: 0, toPos: 2, ringCount: 10));
        Assert.Equal(-1, RandomSongRules.ChooseDirectionOnRing(fromPos: 0, toPos: 8, ringCount: 10));
        Assert.Equal(1, RandomSongRules.ChooseDirectionOnRing(fromPos: 0, toPos: 5, ringCount: 10));
    }

    [Fact]
    public void CountRingSteps_matches_direction()
    {
        Assert.Equal(2, RandomSongRules.CountRingSteps(0, 2, 10, direction: 1));
        Assert.Equal(2, RandomSongRules.CountRingSteps(0, 8, 10, direction: -1));
        Assert.Equal(0, RandomSongRules.CountRingSteps(3, 3, 10, direction: 1));
    }

    [Fact]
    public void SnapToNavigable_uses_folder_header_when_inside_closed()
    {
        Assert.Equal(4, RandomSongRules.SnapToNavigable(7, folderIndexIfInsideClosed: 4));
        Assert.Equal(7, RandomSongRules.SnapToNavigable(7, folderIndexIfInsideClosed: -1));
    }

    [Fact]
    public void EnsureMinSteps_adds_full_loops()
    {
        Assert.Equal(2, RandomSongRules.EnsureMinSteps(rawSteps: 2, ringCount: 10, minSteps: 2));
        Assert.Equal(12, RandomSongRules.EnsureMinSteps(rawSteps: 2, ringCount: 10, minSteps: 8));
    }

    [Fact]
    public void TrackHoldSeconds_starts_fast_and_ends_slow()
    {
        const float budget = 2.6f;
        var first = RandomSongRules.TrackHoldSeconds(0, 10, budget);
        var last = RandomSongRules.TrackHoldSeconds(9, 10, budget);
        Assert.True(first < last);
        Assert.Equal(RandomSongRules.DeadStopHoldSeconds, last, precision: 5);
    }

    [Fact]
    public void EaseInQuad_is_slow_then_steep()
    {
        Assert.Equal(0f, RandomSongRules.EaseInQuad(0f));
        Assert.Equal(1f, RandomSongRules.EaseInQuad(1f));
        Assert.True(RandomSongRules.EaseInQuad(0.5f) < 0.5f);
    }

    [Fact]
    public void EaseInCubic_brakes_between_quad_and_quint()
    {
        Assert.True(RandomSongRules.EaseInCubic(0.5f) < 0.5f);
        Assert.True(RandomSongRules.EaseInQuint(0.5f) < RandomSongRules.EaseInCubic(0.5f));
        Assert.True(RandomSongRules.EaseInCubic(0.85f) > RandomSongRules.EaseInCubic(0.5f));
    }

    [Fact]
    public void TargetScrollSeconds_scales_with_ring_but_stays_bounded()
    {
        var small = RandomSongRules.TargetScrollSeconds(ringCount: 8, visualSteps: 8);
        var large = RandomSongRules.TargetScrollSeconds(ringCount: 105, visualSteps: 16);
        Assert.True(small < large);
        Assert.InRange(small, RandomSongRules.MinScrollSeconds, RandomSongRules.MaxScrollSeconds);
        Assert.InRange(large, RandomSongRules.MinScrollSeconds, RandomSongRules.MaxScrollSeconds);
    }

    [Fact]
    public void TrackTiming_early_is_fast_last_is_dead_stop_and_scales_with_step_count()
    {
        const float budget = 2.6f;
        var holdFew0 = RandomSongRules.TrackHoldSeconds(0, 10, budget);
        var holdFewLast = RandomSongRules.TrackHoldSeconds(9, 10, budget);
        Assert.True(holdFew0 < holdFewLast);
        Assert.Equal(RandomSongRules.DeadStopHoldSeconds, holdFewLast, precision: 5);

        var holdMany0 = RandomSongRules.TrackHoldSeconds(0, 40, budget);
        Assert.True(holdMany0 < holdFew0);

        var moveLast = RandomSongRules.TrackMoveSeconds(39, 40, budget);
        Assert.Equal(RandomSongRules.DeadStopMoveSeconds, moveLast, precision: 5);
    }

    [Fact]
    public void PlanScrollToTarget_adds_loops_for_min_steps_and_lands_on_target()
    {
        var plan = RandomSongRules.PlanScrollToTarget(
            fromPos: 0, toPos: 3, ringCount: 10, minSteps: 8);
        Assert.Equal(1, plan.Direction);
        Assert.Equal(13, plan.TrackSteps); // 3 + one full loop of 10
        Assert.Equal(13, plan.VisualSteps); // small ring: visual == tracks
        Assert.Equal(3, RandomSongRules.LandRingPos(0, plan.Direction, plan.TrackSteps, 10));
    }

    [Fact]
    public void PlanScrollToTarget_prefers_wrap_direction_when_shorter()
    {
        var plan = RandomSongRules.PlanScrollToTarget(
            fromPos: 0, toPos: 8, ringCount: 10, minSteps: 2);
        Assert.Equal(-1, plan.Direction);
        Assert.Equal(2, plan.TrackSteps);
    }

    [Fact]
    public void PlanScrollToTarget_caps_visual_ticks_on_huge_rings()
    {
        var plan = RandomSongRules.PlanScrollToTarget(
            fromPos: 0, toPos: 400, ringCount: 1000, minSteps: 8);
        Assert.Equal(1, plan.Direction);
        Assert.Equal(400, plan.TrackSteps);
        Assert.Equal(RandomSongRules.MaxVisualJukeboxSteps, plan.VisualSteps);
        Assert.Equal(400, RandomSongRules.LandRingPos(0, plan.Direction, plan.TrackSteps, 1000));
    }

    [Fact]
    public void TracksForVisualStep_sums_to_total_and_settles_singles()
    {
        const int visual = 16;
        const int tracks = 400;
        var sum = 0;
        for (var i = 0; i < visual; i++)
            sum += RandomSongRules.TracksForVisualStep(i, visual, tracks);

        Assert.Equal(tracks, sum);
        Assert.Equal(1, RandomSongRules.TracksForVisualStep(visual - 1, visual, tracks));
        Assert.Equal(1, RandomSongRules.TracksForVisualStep(visual - 2, visual, tracks));
        Assert.True(RandomSongRules.TracksForVisualStep(0, visual, tracks) > 1);
    }

    [Fact]
    public void MicroSteps_split_large_jumps_evenly_and_sum_to_jump()
    {
        Assert.Equal(1, RandomSongRules.MicroStepCount(1));
        Assert.Equal(8, RandomSongRules.MicroStepCount(40));

        const int jump = 30;
        var micros = RandomSongRules.MicroStepCount(jump);
        var sum = 0;
        for (var i = 0; i < micros; i++)
            sum += RandomSongRules.TracksForMicroStep(i, micros, jump);
        Assert.Equal(jump, sum);

        var move = RandomSongRules.MicroMoveSeconds(micros, tickMoveSeconds: 0.04f);
        Assert.True(move >= RandomSongRules.MicroMoveMinSeconds);
    }

    [Fact]
    public void MaxTrackStepsPerRoll_is_animation_budget_not_pick_bias()
    {
        // Cap is for theatrical scroll only — far picks still keep their true target.
        Assert.True(RandomSongRules.MaxTrackStepsPerRoll < 200);
        Assert.True(RandomSongRules.MaxTrackStepsPerRoll >= RandomSongRules.DefaultMinJukeboxSteps);
        var far = RandomSongRules.PlanScrollToTarget(
            fromPos: 0, toPos: 400, ringCount: 1000, minSteps: 8);
        Assert.True(far.TrackSteps > RandomSongRules.MaxTrackStepsPerRoll);
        var theater = RandomSongRules.CapScrollForTheater(far, RandomSongRules.MaxTrackStepsPerRoll);
        Assert.Equal(RandomSongRules.MaxTrackStepsPerRoll, theater.TrackSteps);
        Assert.Equal(far.Direction, theater.Direction);
    }
}
