using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class ProcessHitClassificationTests
{
    [Fact]
    public void Empty_with_open_window_or_attack_positions_is_miss_vertical()
    {
        Assert.Equal(
            ProcessHitClassification.EmptySwingKind.MissVertical,
            ProcessHitClassification.ClassifyEmptySwing(positionsToAttackCount: 0, anyEnemyHitWindowOpen: true));
        Assert.Equal(
            ProcessHitClassification.EmptySwingKind.MissVertical,
            ProcessHitClassification.ClassifyEmptySwing(positionsToAttackCount: 2, anyEnemyHitWindowOpen: false));
    }

    [Fact]
    public void Empty_with_no_window_and_no_positions_is_true_overhit()
    {
        Assert.Equal(
            ProcessHitClassification.EmptySwingKind.TrueOverhit,
            ProcessHitClassification.ClassifyEmptySwing(positionsToAttackCount: 0, anyEnemyHitWindowOpen: false));
    }

    [Fact]
    public void Partial_miss_when_hit_count_mismatches_positions()
    {
        Assert.True(ProcessHitClassification.IsPartialMiss(hitDataCount: 1, positionsToAttackCount: 2));
        Assert.False(ProcessHitClassification.IsPartialMiss(hitDataCount: 2, positionsToAttackCount: 2));
        Assert.False(ProcessHitClassification.IsPartialMiss(hitDataCount: 0, positionsToAttackCount: 2));
    }
}
