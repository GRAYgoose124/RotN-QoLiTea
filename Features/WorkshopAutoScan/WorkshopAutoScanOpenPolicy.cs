namespace QoLiTea.Features.WorkshopAutoScan;

public enum WorkshopAutoScanHotkeyAction
{
    Ignore,
    OpenStash,
    ToastNoNewItems,
    ToastScanning,
}

/// <summary>
/// Pure gates for optional auto-open, hotkey stash open, and row select/seen prefix.
/// </summary>
public static class WorkshopAutoScanOpenPolicy
{
    public const string ToastNoNewItems = "No new items";
    public const string ToastScanning = "Scanning…";

    public static bool ShouldAutoOpenOverlay(bool autoOpenEnabled, int showItemCount)
        => autoOpenEnabled && showItemCount > 0;

    public static WorkshopAutoScanHotkeyAction ResolveHotkeyAction(
        bool featureActive,
        bool customMusicOpen,
        bool overlayOpen,
        bool scanInFlight,
        int stashCount)
    {
        if (!featureActive || !customMusicOpen || overlayOpen)
            return WorkshopAutoScanHotkeyAction.Ignore;
        if (scanInFlight)
            return WorkshopAutoScanHotkeyAction.ToastScanning;
        if (stashCount <= 0)
            return WorkshopAutoScanHotkeyAction.ToastNoNewItems;
        return WorkshopAutoScanHotkeyAction.OpenStash;
    }

    public static bool ShouldOpenStashOnHotkey(
        bool featureActive,
        bool customMusicOpen,
        bool overlayOpen,
        bool scanInFlight,
        int stashCount)
        => ResolveHotkeyAction(
               featureActive, customMusicOpen, overlayOpen, scanInFlight, stashCount)
           == WorkshopAutoScanHotkeyAction.OpenStash;

    /// <summary>Select mark + optional session-seen S, including trailing spaces before title.</summary>
    public static string FormatSelectSeenPrefix(bool pendingSubscribe, bool seenThisSession)
    {
        string mark = pendingSubscribe ? "[X]" : "[ ]";
        return seenThisSession ? $"  {mark} S  " : $"  {mark}  ";
    }
}
