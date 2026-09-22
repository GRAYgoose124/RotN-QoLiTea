namespace QoLiTea.Features.ResultsDivergence;

public readonly struct HitDivergenceSample
{
    public HitDivergenceSample(
        float targetBeat,
        float signedDivergence,
        PlotHitRating rating,
        float ratingPercent = 0f,
        PlotMarkerKind marker = PlotMarkerKind.Dot,
        bool isSuperCrit = false,
        string enemyDisplayName = null,
        int enemyTypeId = 0)
    {
        TargetBeat = targetBeat;
        SignedDivergence = signedDivergence;
        Rating = rating;
        RatingPercent = ratingPercent;
        Marker = marker;
        IsSuperCrit = isSuperCrit;
        EnemyDisplayName = enemyDisplayName ?? string.Empty;
        EnemyTypeId = enemyTypeId;
    }

    public float TargetBeat { get; }
    public float SignedDivergence { get; }
    public PlotHitRating Rating { get; }
    public float RatingPercent { get; }
    public PlotMarkerKind Marker { get; }
    public bool IsSuperCrit { get; }
    public string EnemyDisplayName { get; }
    public int EnemyTypeId { get; }

    public bool IsPerfect => Rating == PlotHitRating.Perfect;
    public bool IsBad => Rating != PlotHitRating.Perfect;

    public HitDivergenceSample WithEnemy(string displayName, int typeId)
        => new(
            TargetBeat,
            SignedDivergence,
            Rating,
            RatingPercent,
            Marker,
            IsSuperCrit,
            displayName,
            typeId);
}
