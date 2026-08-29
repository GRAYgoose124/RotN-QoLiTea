using System;
using System.Collections.Generic;

namespace QoLiTea.Features.ResultsDivergence;

/// <summary>Rating-tier magnitudes for fullscreen timing guide lines.</summary>
public static class DivergenceTimingBins
{
    /// <summary>
    /// Unique signed magnitudes (distance from perfect center) for each minimum rating percent.
    /// </summary>
    public static IReadOnlyList<float> SignedMagnitudesFromMinimumPercents(IEnumerable<int> minimumPercents)
    {
        var seen = new SortedSet<float>();
        if (minimumPercents == null)
            return Array.Empty<float>();

        foreach (int pct in minimumPercents)
        {
            if (pct <= 0 || pct >= 100)
                continue;
            seen.Add(100f - pct);
        }

        var list = new List<float>(seen.Count);
        foreach (float mag in seen)
            list.Add(mag);
        return list;
    }

    /// <summary>Signed distance from center for true-perfect / super-crit threshold.</summary>
    public static float SuperCritMagnitude(int truePerfectMinimumPercent)
        => Math.Max(0f, 100f - truePerfectMinimumPercent);
}
