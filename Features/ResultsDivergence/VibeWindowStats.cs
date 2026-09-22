using System;
using System.Collections.Generic;

namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Per activated-vibe window: hit count + first/last named monster.</summary>
public readonly struct VibeWindowStat
{
    public VibeWindowStat(
        ChartBeatSpan span,
        int hitCount,
        string firstEnemyName,
        string lastEnemyName)
    {
        Span = span;
        HitCount = hitCount;
        FirstEnemyName = firstEnemyName ?? string.Empty;
        LastEnemyName = lastEnemyName ?? string.Empty;
    }

    public ChartBeatSpan Span { get; }
    public int HitCount { get; }
    public string FirstEnemyName { get; }
    public string LastEnemyName { get; }
}

public static class VibeWindowStats
{
    public static IReadOnlyList<VibeWindowStat> Compute(
        IReadOnlyList<ChartBeatSpan> vibeSpans,
        IReadOnlyList<HitDivergenceSample> hits)
    {
        if (vibeSpans == null || vibeSpans.Count == 0)
            return Array.Empty<VibeWindowStat>();

        hits ??= Array.Empty<HitDivergenceSample>();
        var result = new List<VibeWindowStat>(vibeSpans.Count);
        for (var i = 0; i < vibeSpans.Count; i++)
        {
            var span = vibeSpans[i];
            int count = 0;
            string first = null;
            string last = null;
            float firstBeat = float.MaxValue;
            float lastBeat = float.MinValue;

            for (var h = 0; h < hits.Count; h++)
            {
                var hit = hits[h];
                if (hit.TargetBeat < span.StartBeat || hit.TargetBeat > span.EndBeat)
                    continue;

                count++;
                if (hit.TargetBeat < firstBeat)
                {
                    firstBeat = hit.TargetBeat;
                    first = hit.EnemyDisplayName;
                }

                if (hit.TargetBeat >= lastBeat)
                {
                    lastBeat = hit.TargetBeat;
                    last = hit.EnemyDisplayName;
                }
            }

            result.Add(new VibeWindowStat(span, count, first, last));
        }

        return result;
    }
}
