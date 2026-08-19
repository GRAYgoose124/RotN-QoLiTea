using System;
using System.Collections.Generic;

namespace QoLiTea.Features.TrackSets;

/// <summary>Timestamped set names for unsub archives.</summary>
public static class TrackSetName
{
    public const string BulkPrefix = "unsub-";
    public const string NoImpossiblePrefix = "unsub-noimp-";

    public static string FormatBulk(DateTime utc) =>
        BulkPrefix + utc.ToString("yyyy-MM-dd-HHmm");

    public static string FormatNoImpossible(DateTime utc) =>
        NoImpossiblePrefix + utc.ToString("yyyy-MM-dd-HHmm");

    /// <summary>If <paramref name="desired"/> is taken, append -2, -3, …</summary>
    public static string EnsureUnique(string desired, IEnumerable<string> existingNames)
    {
        if (string.IsNullOrEmpty(desired))
            desired = BulkPrefix + "unnamed";

        var taken = new HashSet<string>(StringComparer.Ordinal);
        if (existingNames != null)
        {
            foreach (string name in existingNames)
            {
                if (!string.IsNullOrEmpty(name))
                    taken.Add(name);
            }
        }

        if (!taken.Contains(desired))
            return desired;

        for (int n = 2; n < 10000; n++)
        {
            string candidate = desired + "-" + n;
            if (!taken.Contains(candidate))
                return candidate;
        }

        return desired + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }
}
