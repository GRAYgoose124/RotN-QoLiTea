using System;
using System.Collections.Generic;
using RhythmRift;
using Shared.RhythmEngine;

namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Activated vibe-power windows (player-triggered), not chart vibe-chain spawns.</summary>
internal static class VibeActivationHarvest
{
    internal static IReadOnlyList<ChartBeatSpan> Snapshot()
        => RunSessionStore.SnapshotVibeSpans();

    internal static IReadOnlyList<ChartBeatSpan> FromRecord(StageInputRecord record, float totalBeats)
    {
        var live = RunSessionStore.SnapshotVibeSpans();
        if (live.Count > 0)
            return live;

        var starts = record?._vibePowerActivationBeatNumbers;
        if (starts == null || starts.Count == 0)
            return Array.Empty<ChartBeatSpan>();

        // Fallback when live hooks missed: point markers only (no reliable end beat on record).
        var spans = new List<ChartBeatSpan>(starts.Count);
        foreach (float start in starts)
            spans.Add(new ChartBeatSpan(start, Math.Min(start + 4f, totalBeats)));
        return spans;
    }
}
