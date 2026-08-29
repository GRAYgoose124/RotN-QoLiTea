using System;
using System.Collections.Generic;
using RhythmRift;
using Shared.RhythmEngine;

namespace QoLiTea.Features.ResultsDivergence;

internal sealed class PlotRenderContext
{
    internal PlotRenderContext(
        IReadOnlyList<HitDivergenceSample> hits,
        IReadOnlyList<PracticeSpan> worstSpans,
        IReadOnlyList<ChartBeatSpan> vibeSpans,
        float totalBeats,
        int opacityPercent,
        IReadOnlyList<float> timingBinMagnitudes,
        float superCritBinMagnitude,
        AccuracyBar stockColorSource)
    {
        Hits = hits ?? Array.Empty<HitDivergenceSample>();
        WorstSpans = worstSpans ?? Array.Empty<PracticeSpan>();
        VibeSpans = vibeSpans ?? Array.Empty<ChartBeatSpan>();
        TotalBeats = totalBeats;
        OpacityPercent = opacityPercent;
        TimingBinMagnitudes = timingBinMagnitudes ?? Array.Empty<float>();
        SuperCritBinMagnitude = superCritBinMagnitude;
        StockColorSource = stockColorSource;
    }

    internal IReadOnlyList<HitDivergenceSample> Hits { get; }
    internal IReadOnlyList<PracticeSpan> WorstSpans { get; }
    internal IReadOnlyList<ChartBeatSpan> VibeSpans { get; }
    internal float TotalBeats { get; }
    internal int OpacityPercent { get; }
    internal IReadOnlyList<float> TimingBinMagnitudes { get; }
    internal float SuperCritBinMagnitude { get; }
    internal AccuracyBar StockColorSource { get; }
}
