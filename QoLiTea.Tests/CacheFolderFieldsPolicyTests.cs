using QoLiTea.Features.LazyCustomTracks;
using Xunit;

namespace QoLiTea.Tests;

public class CacheFolderFieldsPolicyTests
{
    [Fact]
    public void Old_cache_without_persisted_details_needs_backfill()
    {
        Assert.True(CacheFolderFieldsPolicy.NeedsFolderBackfill(
            trackCount: 40,
            anyTrackHasPersistedDetails: false));
    }

    [Fact]
    public void Cache_with_persisted_details_does_not_need_backfill()
    {
        Assert.False(CacheFolderFieldsPolicy.NeedsFolderBackfill(
            trackCount: 40,
            anyTrackHasPersistedDetails: true));
    }

    [Fact]
    public void Empty_cache_is_cold_open_not_backfill()
    {
        Assert.False(CacheFolderFieldsPolicy.NeedsFolderBackfill(
            trackCount: 0,
            anyTrackHasPersistedDetails: false));
    }

    [Fact]
    public void Persisted_flag_or_detail_rows_count_as_folder_fields()
    {
        Assert.False(CacheFolderFieldsPolicy.HasPersistedDetails(persistedFlag: false, detailCount: 0));
        Assert.True(CacheFolderFieldsPolicy.HasPersistedDetails(persistedFlag: true, detailCount: 0));
        Assert.True(CacheFolderFieldsPolicy.HasPersistedDetails(persistedFlag: false, detailCount: 2));
    }

    [Fact]
    public void Intensity_change_changes_difficulty_signature()
    {
        string a = CacheFolderFieldsPolicy.FormatDifficultySignature(new[] { (1, "5", "120") });
        string b = CacheFolderFieldsPolicy.FormatDifficultySignature(new[] { (1, "8", "120") });
        Assert.NotEqual(a, b);
    }
}
