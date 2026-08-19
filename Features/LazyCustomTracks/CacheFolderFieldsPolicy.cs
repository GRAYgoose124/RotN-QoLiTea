using System.Collections.Generic;
using System.Linq;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// Stock Custom Music folders treat non-finite intensity/BPM as the WTF/"Unknowns" bucket.
/// Lazy cache must persist per-difficulty numbers or one-shot backfill via full reconcile.
/// </summary>
public static class CacheFolderFieldsPolicy
{
    public static bool NeedsFolderBackfill(int trackCount, bool anyTrackHasPersistedDetails)
    {
        if (trackCount <= 0)
            return false;
        return !anyTrackHasPersistedDetails;
    }

    public static bool HasPersistedDetails(bool persistedFlag, int detailCount)
        => persistedFlag || detailCount > 0;

    public static string FormatDifficultySignature(
        IEnumerable<(int Difficulty, string Intensity, string Bpm)> rows)
    {
        if (rows == null)
            return "";

        return string.Join(",", rows.Select(r => $"{r.Difficulty}:{r.Intensity}:{r.Bpm}"));
    }
}
