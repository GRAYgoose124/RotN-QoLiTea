namespace QoLiTea.Features.WorstSectionPractice;

/// <summary>Whether a monster belongs to the Auto practice section (by hit beat).</summary>
public static class PracticeSectionEnemyCull
{
    public static bool IsInSection(float targetHitBeat, float sectionStartBeat, float sectionEndBeat)
        => targetHitBeat >= sectionStartBeat && targetHitBeat <= sectionEndBeat;

    /// <summary>
    /// Queued spawn rows only expose spawn beat (not hit beat). Keep rows that can still
    /// become in-section hits: spawn at/after warm-start and at/before section end.
    /// </summary>
    public static bool IsQueuedSpawnKeepable(
        float spawnTrueBeat,
        float warmStartBeat,
        float sectionEndBeat)
        => spawnTrueBeat >= warmStartBeat && spawnTrueBeat <= sectionEndBeat;
}
