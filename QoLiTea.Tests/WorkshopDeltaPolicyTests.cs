using QoLiTea.Features.LazyCustomTracks;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace QoLiTea.Tests;

public class WorkshopDeltaPolicyTests
{
    [Fact]
    public void Does_not_remove_subscribed_tracks_whose_install_path_is_unresolved()
    {
        // After quit-stage, Steam often still lists the sub but TryResolveLocalPath fails briefly.
        var cachedWorkshop = new[] { "ws111", "ws222", "ws333" };
        var subscribed = new[] { "ws111", "ws222", "ws333" };
        var resolved = new[] { "ws111" }; // only one path ready

        var toRemove = WorkshopDeltaPolicy.WorkshopLevelIdsToRemove(
            cachedWorkshopLevelIds: cachedWorkshop,
            subscribedLevelIds: subscribed,
            enumerationSucceeded: true);

        Assert.Empty(toRemove);
    }

    [Fact]
    public void Removes_only_tracks_absent_from_subscribed_list()
    {
        var cachedWorkshop = new[] { "ws111", "ws222", "ws333" };
        var subscribed = new[] { "ws111", "ws333" }; // ws222 unsubscribed

        var toRemove = WorkshopDeltaPolicy.WorkshopLevelIdsToRemove(
            cachedWorkshopLevelIds: cachedWorkshop,
            subscribedLevelIds: subscribed,
            enumerationSucceeded: true);

        Assert.Equal(new[] { "ws222" }, toRemove.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Skips_all_removals_when_subscription_enumeration_fails()
    {
        var toRemove = WorkshopDeltaPolicy.WorkshopLevelIdsToRemove(
            cachedWorkshopLevelIds: new[] { "ws111", "ws222" },
            subscribedLevelIds: System.Array.Empty<string>(),
            enumerationSucceeded: false);

        Assert.Empty(toRemove);
    }

    [Fact]
    public void Skips_removals_when_subscribed_list_is_empty_but_cache_has_workshop_tracks()
    {
        // Steam can briefly report 0 subs during scene transitions; do not wipe.
        var toRemove = WorkshopDeltaPolicy.WorkshopLevelIdsToRemove(
            cachedWorkshopLevelIds: new[] { "ws111", "ws222" },
            subscribedLevelIds: System.Array.Empty<string>(),
            enumerationSucceeded: true);

        Assert.Empty(toRemove);
    }

    [Fact]
    public void Adds_only_resolved_new_subscriptions()
    {
        var known = new HashSet<string> { "ws111" };
        var subscribed = new[] { "ws111", "ws222", "ws333" };
        var resolved = new[] { "ws111", "ws222" }; // ws333 still downloading

        var toAdd = WorkshopDeltaPolicy.WorkshopLevelIdsToAdd(
            knownLevelIds: known,
            subscribedLevelIds: subscribed,
            resolvedLevelIds: resolved);

        Assert.Equal(new[] { "ws222" }, toAdd.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Awaiting_install_lists_subscribed_unresolved_missing_from_cache()
    {
        var awaiting = WorkshopDeltaPolicy.WorkshopLevelIdsAwaitingInstall(
            subscribedLevelIds: new[] { "ws111", "ws222", "ws333" },
            resolvedLevelIds: new[] { "ws111" },
            alreadyPresentLevelIds: new[] { "ws111" });

        Assert.Equal(new[] { "ws222", "ws333" }, awaiting.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void Awaiting_install_empty_when_everything_is_resolved_or_cached()
    {
        var awaiting = WorkshopDeltaPolicy.WorkshopLevelIdsAwaitingInstall(
            subscribedLevelIds: new[] { "ws111", "ws222" },
            resolvedLevelIds: new[] { "ws111", "ws222" },
            alreadyPresentLevelIds: new[] { "ws111" });

        Assert.Empty(awaiting);
    }
}
