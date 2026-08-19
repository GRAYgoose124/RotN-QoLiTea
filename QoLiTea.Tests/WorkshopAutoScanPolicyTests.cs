using System.Collections.Generic;
using System.Linq;
using QoLiTea.Features.WorkshopAutoScan;
using Xunit;

namespace QoLiTea.Tests;

public class WorkshopAutoScanPolicyTests
{
    [Fact]
    public void First_run_stamp_marks_all_page_ids_and_yields_no_candidates()
    {
        var page = new ulong[] { 10, 20, 30 };
        var stamped = WorkshopAutoScanPolicy.StampFirstRun(page);

        Assert.Equal(new ulong[] { 10, 20, 30 }, stamped.ToArray());

        var candidates = WorkshopAutoScanPolicy.FilterCandidates(
            pageFileIds: page,
            seenFileIds: stamped,
            subscribedFileIds: System.Array.Empty<ulong>());

        Assert.Empty(candidates);
    }

    [Fact]
    public void Filter_drops_seen_and_subscribed()
    {
        var page = new ulong[] { 1, 2, 3, 4 };
        var seen = new ulong[] { 1 };
        var subscribed = new ulong[] { 3 };

        var candidates = WorkshopAutoScanPolicy.FilterCandidates(page, seen, subscribed);

        Assert.Equal(new ulong[] { 2, 4 }, candidates.ToArray());
    }

    [Fact]
    public void Mark_seen_only_adds_touched_ids()
    {
        var existing = new List<ulong> { 1, 2 };
        var updated = WorkshopAutoScanPolicy.MarkSeen(existing, touchedFileIds: new ulong[] { 2, 3 });

        Assert.Equal(new ulong[] { 1, 2, 3 }, updated.ToArray());
    }

    [Fact]
    public void Trim_keeps_newest_max_seen_ids()
    {
        var seen = Enumerable.Range(1, 600).Select(i => (ulong)i).ToList();
        var trimmed = WorkshopAutoScanPolicy.TrimSeen(seen, maxCount: 500);

        Assert.Equal(500, trimmed.Count);
        Assert.Equal(101u, trimmed[0]);
        Assert.Equal(600u, trimmed[^1]);
    }

    [Fact]
    public void Mark_seen_then_trim_preserves_cap()
    {
        var existing = Enumerable.Range(1, 500).Select(i => (ulong)i).ToList();
        var updated = WorkshopAutoScanPolicy.MarkSeen(existing, new ulong[] { 999 });
        var trimmed = WorkshopAutoScanPolicy.TrimSeen(updated, 500);

        Assert.Equal(500, trimmed.Count);
        Assert.DoesNotContain(1u, trimmed);
        Assert.Contains(999u, trimmed);
    }

    [Fact]
    public void Should_fetch_next_page_only_when_zero_candidates_and_pages_remain()
    {
        Assert.True(WorkshopAutoScanPolicy.ShouldFetchNextPage(pageIndex1Based: 1, candidateCountSoFar: 0));
        Assert.False(WorkshopAutoScanPolicy.ShouldFetchNextPage(pageIndex1Based: 1, candidateCountSoFar: 2));
        Assert.True(WorkshopAutoScanPolicy.ShouldFetchNextPage(pageIndex1Based: 2, candidateCountSoFar: 0));
        Assert.False(WorkshopAutoScanPolicy.ShouldFetchNextPage(pageIndex1Based: 3, candidateCountSoFar: 0));
    }

    [Fact]
    public void Partition_moves_auto_sub_owner_items_to_silent()
    {
        var items = new (ulong FileId, ulong OwnerId)[]
        {
            (1, 10),
            (2, 20),
            (3, 10),
        };
        var (silent, show) = WorkshopAutoScanPolicy.PartitionByAutoSubAuthors(items, new ulong[] { 10 });

        Assert.Equal(new ulong[] { 1, 3 }, silent.Select(i => i.FileId).ToArray());
        Assert.Equal(new ulong[] { 2 }, show.Select(i => i.FileId).ToArray());
    }
}
