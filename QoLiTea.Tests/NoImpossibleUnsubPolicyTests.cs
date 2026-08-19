using System.Linq;
using QoLiTea.Features.TrackSets;
using Xunit;

namespace QoLiTea.Tests;

public class NoImpossibleUnsubPolicyTests
{
    [Fact]
    public void Removes_missing_impossible_only()
    {
        var tracks = new[]
        {
            new NoImpossibleCandidate(1, false, ImpossiblePresence.Missing),
            new NoImpossibleCandidate(2, false, ImpossiblePresence.Has),
            new NoImpossibleCandidate(3, false, ImpossiblePresence.Unknown),
            new NoImpossibleCandidate(4, true, ImpossiblePresence.Missing),
        };

        var sel = NoImpossibleUnsubPolicy.SelectRemovals(tracks);

        Assert.Equal(new ulong[] { 1 }, sel.FileIdsToRemove.ToArray());
        Assert.Equal(1, sel.KeptHasImpossibleCount);
        Assert.Equal(1, sel.SkippedUnknownCount);
        Assert.Equal(1, sel.SkippedFavoriteCount);
    }

    [Fact]
    public void Zero_play_missing_impossible_is_removed()
    {
        // No-Impossible does not shield zero-play (unlike Bulk Unsub).
        var tracks = new[]
        {
            new NoImpossibleCandidate(9, false, ImpossiblePresence.Missing),
        };

        var sel = NoImpossibleUnsubPolicy.SelectRemovals(tracks);

        Assert.Equal(new ulong[] { 9 }, sel.FileIdsToRemove.ToArray());
    }
}
