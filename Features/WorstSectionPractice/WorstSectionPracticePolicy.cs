namespace QoLiTea.Features.WorstSectionPractice;

public static class WorstSectionPracticePolicy
{
    public static bool ShouldOfferPractice(
        bool masterEnabled,
        bool featureEnabled,
        int spanCount,
        bool stockPracticeAllowed)
        => masterEnabled && featureEnabled && stockPracticeAllowed && spanCount > 0;
}
