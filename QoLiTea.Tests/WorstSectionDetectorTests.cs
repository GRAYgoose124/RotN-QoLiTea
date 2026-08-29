using System.Collections.Generic;
using System.Linq;
using QoLiTea.Features.ResultsDivergence;
using Xunit;

namespace QoLiTea.Tests;

public class WorstSectionDetectorTests
{
    private static HitDivergenceSample Hit(float beat, float div, bool perfect) =>
        new(beat, div, perfect ? PlotHitRating.Perfect : PlotHitRating.Good);

    [Fact]
    public void Isolated_miss_yields_no_spans()
    {
        var spans = WorstSectionDetector.Detect(new[] { Hit(10, -100, false) });
        Assert.Empty(spans);
    }

    [Fact]
    public void Short_cluster_below_min_span_yields_no_spans()
    {
        // 3 bad within 4 beats but span length 2 < MinSpanBeats 8
        var hits = new[] { Hit(10, -20, false), Hit(11, -15, false), Hit(12, -10, false) };
        Assert.Empty(WorstSectionDetector.Detect(hits));
    }

    [Fact]
    public void Dense_bad_over_min_span_yields_one_span()
    {
        var hits = Enumerable.Range(10, 9).Select(b => Hit(b, -10, false)).ToArray(); // 10..18
        var spans = WorstSectionDetector.Detect(hits);
        Assert.Single(spans);
        Assert.Equal(10f, spans[0].StartBeat, 3);
        Assert.Equal(18f, spans[0].EndBeat, 3);
        Assert.Equal(9, spans[0].BadHitCount);
    }

    [Fact]
    public void Bleed_through_perfects_keep_one_long_span()
    {
        var hits = new[]
        {
            Hit(10, -20, false), Hit(11, -15, false),
            Hit(12, 0, true), Hit(13, 0, true),
            Hit(14, -25, false), Hit(15, -30, false), Hit(16, -10, false),
            Hit(17, -12, false), Hit(18, -8, false),
        };
        var spans = WorstSectionDetector.Detect(hits);
        Assert.Single(spans);
        Assert.Equal(10f, spans[0].StartBeat, 3);
        Assert.Equal(18f, spans[0].EndBeat, 3);
    }

    [Fact]
    public void Three_consecutive_perfects_split_cluster()
    {
        var hits = new List<HitDivergenceSample>();
        hits.AddRange(Enumerable.Range(10, 9).Select(b => Hit(b, -10, false))); // 10-18
        hits.Add(Hit(19, 0, true));
        hits.Add(Hit(20, 0, true));
        hits.Add(Hit(21, 0, true)); // 3rd perfect splits
        hits.AddRange(Enumerable.Range(22, 9).Select(b => Hit(b, -10, false))); // 22-30
        var spans = WorstSectionDetector.Detect(hits);
        Assert.Equal(2, spans.Count);
        Assert.Equal(10f, spans[0].StartBeat, 3);
        Assert.Equal(22f, spans[1].StartBeat, 3);
    }

    [Fact]
    public void Overlap_keeps_longer_span()
    {
        var hits = Enumerable.Range(10, 11).Select(b => Hit(b, -10, false)).ToList(); // 10-20
        var spans = WorstSectionDetector.Detect(hits);
        Assert.Single(spans);
        Assert.True(spans[0].EndBeat - spans[0].StartBeat >= 8f);
    }

    [Fact]
    public void Output_is_chart_order()
    {
        var hits = new List<HitDivergenceSample>();
        hits.AddRange(Enumerable.Range(50, 9).Select(b => Hit(b, -10, false))); // 50-58
        hits.AddRange(Enumerable.Range(10, 9).Select(b => Hit(b, -10, false))); // 10-18
        var spans = WorstSectionDetector.Detect(hits);
        Assert.Equal(2, spans.Count);
        Assert.Equal(10f, spans[0].StartBeat, 3);
        Assert.Equal(50f, spans[1].StartBeat, 3);
    }
}
