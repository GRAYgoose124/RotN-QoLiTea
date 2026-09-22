using QoLiTea.Features.WorkshopAutoScan;
using Xunit;

namespace QoLiTea.Tests;

public class WorkshopAutoScanOpenPolicyTests
{
    [Theory]
    [InlineData(true, 3, true)]
    [InlineData(true, 0, false)]
    [InlineData(false, 3, false)]
    [InlineData(false, 0, false)]
    public void Auto_open_only_when_enabled_and_show_items_exist(
        bool autoOpen, int showCount, bool expected)
    {
        Assert.Equal(expected, WorkshopAutoScanOpenPolicy.ShouldAutoOpenOverlay(autoOpen, showCount));
    }

    [Theory]
    [InlineData(true, true, false, false, 2, WorkshopAutoScanHotkeyAction.OpenStash)]
    [InlineData(false, true, false, false, 2, WorkshopAutoScanHotkeyAction.Ignore)]
    [InlineData(true, false, false, false, 2, WorkshopAutoScanHotkeyAction.Ignore)]
    [InlineData(true, true, true, false, 2, WorkshopAutoScanHotkeyAction.Ignore)]
    [InlineData(true, true, false, true, 2, WorkshopAutoScanHotkeyAction.ToastScanning)]
    [InlineData(true, true, false, false, 0, WorkshopAutoScanHotkeyAction.ToastNoNewItems)]
    public void Hotkey_action_covers_open_ignore_and_toasts(
        bool featureActive,
        bool customMusicOpen,
        bool overlayOpen,
        bool scanInFlight,
        int stashCount,
        WorkshopAutoScanHotkeyAction expected)
    {
        Assert.Equal(
            expected,
            WorkshopAutoScanOpenPolicy.ResolveHotkeyAction(
                featureActive, customMusicOpen, overlayOpen, scanInFlight, stashCount));
        Assert.Equal(
            expected == WorkshopAutoScanHotkeyAction.OpenStash,
            WorkshopAutoScanOpenPolicy.ShouldOpenStashOnHotkey(
                featureActive, customMusicOpen, overlayOpen, scanInFlight, stashCount));
    }

    [Fact]
    public void Toast_messages_are_user_facing_copy()
    {
        Assert.Equal("No new items", WorkshopAutoScanOpenPolicy.ToastNoNewItems);
        Assert.Equal("Scanning…", WorkshopAutoScanOpenPolicy.ToastScanning);
    }

    [Theory]
    [InlineData(false, false, "  [ ]  ")]
    [InlineData(true, false, "  [X]  ")]
    [InlineData(false, true, "  [ ] S  ")]
    [InlineData(true, true, "  [X] S  ")]
    public void Row_prefix_shows_S_only_when_seen_this_session(
        bool pending, bool seen, string expected)
    {
        Assert.Equal(expected, WorkshopAutoScanOpenPolicy.FormatSelectSeenPrefix(pending, seen));
    }
}
