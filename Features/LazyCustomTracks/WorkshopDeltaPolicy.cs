using System;
using System.Collections.Generic;
using System.Linq;

namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// Pure rules for Workshop add/remove while the Custom Music menu is open.
/// Removals key off the subscribed set, not resolved install paths — unresolved
/// paths are common right after quitting a stage and must not wipe the list.
/// </summary>
public static class WorkshopDeltaPolicy
{
    public static List<string> WorkshopLevelIdsToRemove(
        IEnumerable<string> cachedWorkshopLevelIds,
        IEnumerable<string> subscribedLevelIds,
        bool enumerationSucceeded)
    {
        var result = new List<string>();
        if (!enumerationSucceeded || cachedWorkshopLevelIds == null)
            return result;

        var subscribed = new HashSet<string>(
            (subscribedLevelIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal);

        var cached = cachedWorkshopLevelIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Steam can briefly report 0 subs during scene transitions; do not wipe.
        if (subscribed.Count == 0 && cached.Count > 0)
            return result;

        foreach (string levelId in cached)
        {
            if (!subscribed.Contains(levelId))
                result.Add(levelId);
        }

        return result;
    }

    public static List<string> WorkshopLevelIdsToAdd(
        ISet<string> knownLevelIds,
        IEnumerable<string> subscribedLevelIds,
        IEnumerable<string> resolvedLevelIds)
    {
        var result = new List<string>();
        if (resolvedLevelIds == null)
            return result;

        var known = knownLevelIds ?? new HashSet<string>(StringComparer.Ordinal);
        // subscribed is informational; only resolved installs can be hydrated.
        _ = subscribedLevelIds;

        foreach (string levelId in resolvedLevelIds)
        {
            if (string.IsNullOrEmpty(levelId))
                continue;
            if (known.Contains(levelId))
                continue;
            result.Add(levelId);
        }

        return result;
    }

    /// <summary>
    /// Subscribed but not yet on disk (and not already in the list). Keep polling until resolved.
    /// </summary>
    public static List<string> WorkshopLevelIdsAwaitingInstall(
        IEnumerable<string> subscribedLevelIds,
        IEnumerable<string> resolvedLevelIds,
        IEnumerable<string> alreadyPresentLevelIds)
    {
        var resolved = new HashSet<string>(
            (resolvedLevelIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal);
        var present = new HashSet<string>(
            (alreadyPresentLevelIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal);

        var result = new List<string>();
        foreach (string levelId in subscribedLevelIds ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(levelId))
                continue;
            if (present.Contains(levelId))
                continue;
            if (resolved.Contains(levelId))
                continue;
            result.Add(levelId);
        }

        return result;
    }
}
