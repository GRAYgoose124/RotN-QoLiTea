using Shared;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>Per-difficulty numbers needed by stock Custom Music folder grouping.</summary>
public sealed class CachedDifficultyDto
{
    public Difficulty Difficulty { get; set; }
    public float? Intensity { get; set; }
    public float? BeatsPerMinute { get; set; }
    public float? BeatCount { get; set; }
}
