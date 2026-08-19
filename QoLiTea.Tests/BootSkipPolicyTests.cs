using QoLiTea.Features.SkipBootIntro;
using Xunit;

namespace QoLiTea.Tests;

public class BootSkipPolicyTests
{
    [Fact]
    public void Master_off_keeps_stock_splash()
    {
        Assert.False(BootSkipPolicy.ShouldReplaceSplashStart(false, true));
    }

    [Fact]
    public void Feature_off_keeps_stock_splash()
    {
        Assert.False(BootSkipPolicy.ShouldReplaceSplashStart(true, false));
    }

    [Fact]
    public void Both_on_replaces_splash_start()
    {
        Assert.True(BootSkipPolicy.ShouldReplaceSplashStart(true, true));
    }

    [Fact]
    public void Wait_only_while_loading()
    {
        Assert.True(BootSkipPolicy.ShouldWaitForLoadCover(true));
        Assert.False(BootSkipPolicy.ShouldWaitForLoadCover(false));
    }

    [Fact]
    public void Repair_only_when_boot_skip_left_settings_on_splash()
    {
        Assert.True(BootSkipPolicy.NeedsMainMenuSettingsRootRepair(true, "SplashScreen"));
        Assert.False(BootSkipPolicy.NeedsMainMenuSettingsRootRepair(false, "SplashScreen"));
        Assert.False(BootSkipPolicy.NeedsMainMenuSettingsRootRepair(true, "MainMenu"));
    }
}
