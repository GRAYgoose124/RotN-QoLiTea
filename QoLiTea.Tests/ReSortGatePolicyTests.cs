using QoLiTea.Features.LazyCustomTracks;
using Xunit;

namespace QoLiTea.Tests;

public class ReSortGatePolicyTests
{
    [Fact]
    public void Disabled_mod_always_runs_stock_resort()
    {
        Assert.True(ReSortGatePolicy.ShouldRunStockReSort(
            modEnabled: false, menuOpened: false, isActiveController: false));
    }

    [Fact]
    public void Warm_open_active_menu_allows_user_sort_cycle()
    {
        // Warm opens never set reconcile owner — sort must still work.
        Assert.True(ReSortGatePolicy.ShouldRunStockReSort(
            modEnabled: true, menuOpened: true, isActiveController: true));
    }

    [Fact]
    public void Closed_menu_blocks_late_resort_from_background_scan()
    {
        Assert.False(ReSortGatePolicy.ShouldRunStockReSort(
            modEnabled: true, menuOpened: false, isActiveController: false));
    }

    [Fact]
    public void Stale_destroyed_controller_blocked_even_if_another_menu_is_open()
    {
        Assert.False(ReSortGatePolicy.ShouldRunStockReSort(
            modEnabled: true, menuOpened: true, isActiveController: false));
    }
}
