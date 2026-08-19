namespace QoLiTea.Features.SkipBootIntro;

/// <summary>
/// Whether to replace splash Start, and whether to wait out startup load first.
/// </summary>
public static class BootSkipPolicy
{
    public static bool ShouldReplaceSplashStart(bool masterEnabled, bool featureEnabled)
        => masterEnabled && featureEnabled;

    public static bool ShouldWaitForLoadCover(bool isLoading)
        => isLoading;

    /// <summary>
    /// After Splash→MainMenu hop, Awake may configure settings as SplashScreen (auto-opens Audio).
    /// </summary>
    public static bool NeedsMainMenuSettingsRootRepair(bool skipBootActive, string activatingScene)
        => skipBootActive && activatingScene == "SplashScreen";
}
