using System;
using System.Collections.Generic;

namespace QoLiTea.Features.ResultsDivergence;

internal static class RunSessionStore
{
    private static readonly List<HitDivergenceSample> LiveHits = new();
    private static readonly List<ChartBeatSpan> LiveVibeSpans = new();
    private static float? _openVibeStartBeat;

    internal static IReadOnlyList<HitDivergenceSample> LastHits { get; set; } =
        Array.Empty<HitDivergenceSample>();

    internal static float LastTotalBeats { get; set; }

    internal static int TruePerfectMinimum { get; private set; } = 90;

    internal static void BeginStage(int truePerfectMinimum = 90)
    {
        TruePerfectMinimum = truePerfectMinimum;
        LiveHits.Clear();
        LiveVibeSpans.Clear();
        _openVibeStartBeat = null;
        LastHits = Array.Empty<HitDivergenceSample>();
        LastTotalBeats = 0f;
    }

    internal static void AppendLive(HitDivergenceSample sample)
        => LiveHits.Add(sample);

    /// <summary>Attach enemy display name to the latest live sample at this beat (for vibe captions).</summary>
    internal static void RememberEnemyName(float targetBeat, string displayName)
    {
        if (targetBeat <= 0f || string.IsNullOrEmpty(displayName))
            return;

        for (var i = LiveHits.Count - 1; i >= 0; i--)
        {
            if (Math.Abs(LiveHits[i].TargetBeat - targetBeat) > 0.0001f)
                continue;
            LiveHits[i] = LiveHits[i].WithEnemy(displayName);
            return;
        }
    }

    internal static void OnVibeActivated(float beat)
    {
        if (beat <= 0f)
            return;
        if (_openVibeStartBeat.HasValue)
            return;
        _openVibeStartBeat = beat;
    }

    internal static void OnVibeDeactivated(float beat)
    {
        if (!_openVibeStartBeat.HasValue || beat <= 0f)
            return;

        float start = _openVibeStartBeat.Value;
        float end = Math.Max(beat, start);
        LiveVibeSpans.Add(new ChartBeatSpan(start, end));
        _openVibeStartBeat = null;
    }

    internal static void CloseOpenVibeAt(float beat)
    {
        if (_openVibeStartBeat.HasValue)
            OnVibeDeactivated(beat);
    }

    internal static IReadOnlyList<ChartBeatSpan> SnapshotVibeSpans()
    {
        if (LiveVibeSpans.Count == 0)
            return Array.Empty<ChartBeatSpan>();
        return LiveVibeSpans.ToArray();
    }

    internal static IReadOnlyList<HitDivergenceSample> SnapshotLiveOrEmpty()
        => LiveHits.Count > 0 ? LiveHits.ToArray() : Array.Empty<HitDivergenceSample>();

    internal static void Clear()
    {
        LiveHits.Clear();
        LiveVibeSpans.Clear();
        _openVibeStartBeat = null;
        LastHits = Array.Empty<HitDivergenceSample>();
        LastTotalBeats = 0f;
    }
}
