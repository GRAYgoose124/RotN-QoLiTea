using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class MissMarkerRulesTests
{
    [Fact]
    public void Untimed_miss_uses_vertical_line_at_center()
    {
        Assert.False(SignedDivergenceRules.MissHasTiming(ratingPercent: 0f, inputBeat: 10f, targetBeat: 10f));
        Assert.Equal(
            PlotMarkerKind.VerticalLine,
            SignedDivergenceRules.MarkerForMiss(0f, 10f, 10f));
        Assert.Equal(0f, SignedDivergenceRules.SignedForMiss(0f, wasEarly: true, 10f, 10f));
    }

    [Fact]
    public void Timed_miss_via_rating_percent_uses_dot()
    {
        Assert.True(SignedDivergenceRules.MissHasTiming(ratingPercent: 40f, inputBeat: 10f, targetBeat: 10f));
        Assert.Equal(
            PlotMarkerKind.Dot,
            SignedDivergenceRules.MarkerForMiss(40f, 10f, 10f));
        Assert.Equal(-60f, SignedDivergenceRules.SignedForMiss(40f, wasEarly: true, 10f, 10f));
    }

    [Fact]
    public void Timed_miss_via_beat_delta_uses_dot()
    {
        Assert.True(SignedDivergenceRules.MissHasTiming(0f, inputBeat: 9.5f, targetBeat: 10f));
        Assert.Equal(
            PlotMarkerKind.Dot,
            SignedDivergenceRules.MarkerForMiss(0f, 9.5f, 10f));
        Assert.Equal(-50f, SignedDivergenceRules.SignedForMiss(0f, wasEarly: true, 9.5f, 10f), 3);
    }

    /// <summary>
    /// Stock <c>HandleEnemyAttack</c> records timeout misses with
    /// <c>wasPlayerInput: false</c> and a synthetic input beat at target+afterWindow —
    /// that delta is not a player press and must not count as timing.
    /// </summary>
    [Fact]
    public void Timeout_miss_synthetic_after_window_beat_is_untimed_vertical()
    {
        const float target = 10f;
        const float syntheticInput = 10.25f;

        Assert.False(SignedDivergenceRules.MissHasTiming(
            ratingPercent: 0f, syntheticInput, target, wasPlayerInput: false));
        Assert.Equal(
            PlotMarkerKind.VerticalLine,
            SignedDivergenceRules.MarkerForMiss(0f, syntheticInput, target, wasPlayerInput: false));
        Assert.Equal(
            0f,
            SignedDivergenceRules.SignedForMiss(0f, wasEarly: false, syntheticInput, target, wasPlayerInput: false));
    }

    [Fact]
    public void Same_synthetic_delta_with_player_input_is_timed_dot()
    {
        const float target = 10f;
        const float input = 10.25f;

        Assert.True(SignedDivergenceRules.MissHasTiming(0f, input, target, wasPlayerInput: true));
        Assert.Equal(
            PlotMarkerKind.Dot,
            SignedDivergenceRules.MarkerForMiss(0f, input, target, wasPlayerInput: true));
    }

    [Fact]
    public void Partial_miss_does_not_add_extra_vertical_when_miss_hit_data_exists()
    {
        Assert.False(ProcessHitClassification.ShouldAppendExtraMissVertical(
            hitDataCount: 1,
            missHitDataCount: 1,
            positionsToAttackCount: 2,
            anyEnemyHitWindowOpen: true));
    }

    [Fact]
    public void Partial_miss_without_miss_hit_data_still_allows_extra_vertical()
    {
        // Defensive: if hitDatas were non-miss somehow, keep prior miss-vertical behavior.
        Assert.True(ProcessHitClassification.ShouldAppendExtraMissVertical(
            hitDataCount: 1,
            missHitDataCount: 0,
            positionsToAttackCount: 2,
            anyEnemyHitWindowOpen: true));
    }
}
