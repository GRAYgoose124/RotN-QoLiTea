namespace QoLiTea.Features.ResultsDivergence;

public readonly struct HitDivergenceSample
{
    public HitDivergenceSample(float targetBeat, float signedDivergence, PlotHitRating rating)
    {
        TargetBeat = targetBeat;
        SignedDivergence = signedDivergence;
        Rating = rating;
    }

    public float TargetBeat { get; }
    public float SignedDivergence { get; }
    public PlotHitRating Rating { get; }

    public bool IsPerfect => Rating == PlotHitRating.Perfect;
    public bool IsBad => Rating != PlotHitRating.Perfect;
}
