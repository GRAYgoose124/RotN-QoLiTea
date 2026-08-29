using System;
using System.Collections.Generic;

namespace QoLiTea.Features.ResultsDivergence;

internal static class RunSessionStore
{
    private static readonly List<HitDivergenceSample> LiveHits = new();

    internal static IReadOnlyList<HitDivergenceSample> LastHits { get; set; } =
        Array.Empty<HitDivergenceSample>();

    internal static float LastTotalBeats { get; set; }

    internal static void BeginStage()
    {
        LiveHits.Clear();
        LastHits = Array.Empty<HitDivergenceSample>();
        LastTotalBeats = 0f;
    }

    internal static void AppendLive(HitDivergenceSample sample)
        => LiveHits.Add(sample);

    internal static IReadOnlyList<HitDivergenceSample> SnapshotLiveOrEmpty()
        => LiveHits.Count > 0 ? LiveHits.ToArray() : Array.Empty<HitDivergenceSample>();

    internal static void Clear()
    {
        LiveHits.Clear();
        LastHits = Array.Empty<HitDivergenceSample>();
        LastTotalBeats = 0f;
    }
}
