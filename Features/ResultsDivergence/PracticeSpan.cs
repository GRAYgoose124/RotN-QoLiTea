namespace QoLiTea.Features.ResultsDivergence;

public readonly struct PracticeSpan
{
    public PracticeSpan(float startBeat, float endBeat, int badHitCount, float meanAbsDivergence)
    {
        StartBeat = startBeat;
        EndBeat = endBeat;
        BadHitCount = badHitCount;
        MeanAbsDivergence = meanAbsDivergence;
    }

    public float StartBeat { get; }
    public float EndBeat { get; }
    public int BadHitCount { get; }
    public float MeanAbsDivergence { get; }

    public float LengthBeats => EndBeat - StartBeat;
}
