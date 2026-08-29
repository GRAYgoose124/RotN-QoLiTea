namespace QoLiTea.Features.WorstSectionPractice;

/// <summary>Stock practice warm-up math (see RRStageController.ProcessPracticeModePayload).</summary>
internal static class PracticeSectionBeatMath
{
    internal const float WarmupBeats = 8f;

    internal static float WarmStartBeat(float sectionStartBeat, float fadeInBeats)
    {
        if (sectionStartBeat - fadeInBeats < 0f)
            return 0f;
        if (sectionStartBeat - WarmupBeats < fadeInBeats)
            return fadeInBeats;
        return sectionStartBeat - WarmupBeats;
    }
}
