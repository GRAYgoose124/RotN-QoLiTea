namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Chart-ordered beat range (e.g. vibe chain section).</summary>
public readonly struct ChartBeatSpan
{
    public ChartBeatSpan(float startBeat, float endBeat)
    {
        StartBeat = startBeat;
        EndBeat = endBeat;
    }

    public float StartBeat { get; }
    public float EndBeat { get; }
}
