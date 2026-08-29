using System;
using System.Collections.Generic;
using System.Linq;

namespace QoLiTea.Features.ResultsDivergence;

public sealed class WorstSectionDetectorOptions
{
    public float GapMergeBeats { get; set; } = 4f;
    public int MaxPerfectBleedThrough { get; set; } = 2;
    public int MinBadHits { get; set; } = 3;
    public float MinSpanBeats { get; set; } = 8f;
    public int MaxSpans { get; set; } = 3;
}

public static class WorstSectionDetector
{
    public static IReadOnlyList<PracticeSpan> Detect(IReadOnlyList<HitDivergenceSample> hits)
        => Detect(hits, new WorstSectionDetectorOptions());

    public static IReadOnlyList<PracticeSpan> Detect(
        IReadOnlyList<HitDivergenceSample> hits,
        WorstSectionDetectorOptions options)
    {
        if (options == null)
            options = new WorstSectionDetectorOptions();
        if (hits == null || hits.Count == 0)
            return Array.Empty<PracticeSpan>();

        var ordered = hits.OrderBy(h => h.TargetBeat).ToList();
        var candidates = new List<PracticeSpan>();

        bool open = false;
        float start = 0f;
        float end = 0f;
        int badCount = 0;
        float sumAbs = 0f;
        int perfectBleed = 0;

        void Close()
        {
            if (!open)
                return;
            float len = end - start;
            if (badCount >= options.MinBadHits && len >= options.MinSpanBeats)
            {
                float mean = badCount > 0 ? sumAbs / badCount : 0f;
                candidates.Add(new PracticeSpan(start, end, badCount, mean));
            }

            open = false;
            badCount = 0;
            sumAbs = 0f;
            perfectBleed = 0;
        }

        foreach (var hit in ordered)
        {
            if (!open)
            {
                if (!hit.IsBad)
                    continue;
                open = true;
                start = hit.TargetBeat;
                end = hit.TargetBeat;
                badCount = 1;
                sumAbs = Math.Abs(hit.SignedDivergence);
                perfectBleed = 0;
                continue;
            }

            float gap = hit.TargetBeat - end;
            if (gap > options.GapMergeBeats)
            {
                Close();
                if (hit.IsBad)
                {
                    open = true;
                    start = hit.TargetBeat;
                    end = hit.TargetBeat;
                    badCount = 1;
                    sumAbs = Math.Abs(hit.SignedDivergence);
                    perfectBleed = 0;
                }

                continue;
            }

            if (hit.IsPerfect)
            {
                perfectBleed++;
                if (perfectBleed > options.MaxPerfectBleedThrough)
                {
                    Close();
                    continue;
                }

                end = hit.TargetBeat;
                continue;
            }

            if (hit.IsBad)
            {
                perfectBleed = 0;
                end = hit.TargetBeat;
                badCount++;
                sumAbs += Math.Abs(hit.SignedDivergence);
            }
        }

        Close();

        var ranked = candidates
            .OrderByDescending(s => s.LengthBeats)
            .ThenByDescending(s => s.MeanAbsDivergence)
            .ToList();

        var picked = new List<PracticeSpan>();
        foreach (var span in ranked)
        {
            if (picked.Count >= options.MaxSpans)
                break;
            bool overlaps = picked.Any(p => SpansOverlap(p, span));
            if (!overlaps)
                picked.Add(span);
        }

        return picked.OrderBy(s => s.StartBeat).ToList();
    }

    private static bool SpansOverlap(PracticeSpan a, PracticeSpan b)
        => a.StartBeat <= b.EndBeat && b.StartBeat <= a.EndBeat;
}
