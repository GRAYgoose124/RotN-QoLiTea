namespace QoLiTea.Features.ResultsDivergence;

public readonly struct HitDivergenceSample
{
    public HitDivergenceSample(
        float targetBeat,
        float signedDivergence,
        PlotHitRating rating,
        float ratingPercent = 0f,
        PlotMarkerKind marker = PlotMarkerKind.Dot,
        bool isSuperCrit = false)
    {
        TargetBeat = targetBeat;
        SignedDivergence = signedDivergence;
        Rating = rating;
        RatingPercent = ratingPercent;
        Marker = marker;
        IsSuperCrit = isSuperCrit;
    }

    public float TargetBeat { get; }
    public float SignedDivergence { get; }
    public PlotHitRating Rating { get; }
    public float RatingPercent { get; }
    public PlotMarkerKind Marker { get; }
    public bool IsSuperCrit { get; }

    public bool IsPerfect => Rating == PlotHitRating.Perfect;
    public bool IsBad => Rating != PlotHitRating.Perfect;
}
