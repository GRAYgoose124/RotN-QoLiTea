namespace QoLiTea.Features.LazyCustomTracks;

/// <summary>
/// Whether stock HandleTrackMetadataReSort may run.
/// Blocks late calls from a background UpdateTrackList after the menu is gone,
/// without requiring a full reconcile owner (warm opens never set one).
/// </summary>
public static class ReSortGatePolicy
{
    public static bool ShouldRunStockReSort(bool modEnabled, bool menuOpened, bool isActiveController)
    {
        if (!modEnabled)
            return true;

        return menuOpened && isActiveController;
    }
}
