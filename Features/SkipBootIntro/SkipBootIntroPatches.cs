using System.Collections;
using HarmonyLib;
using Shared.Analytics;
using Shared.PlayerData;
using Shared.SceneLoading;
using Shared.Title;
using UnityEngine;

namespace QoLiTea.Features.SkipBootIntro;

/// <summary>
/// Replace splash Start: wait out load cover if needed, then MainMenu.
/// Clear first-load title so boot lands on the real menu.
/// </summary>
[HarmonyPatch]
internal static class SkipBootIntroPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SplashScreenController), nameof(SplashScreenController.Start))]
    private static bool SplashStartPrefix(ref IEnumerator __result)
    {
        if (!BootSkipPolicy.ShouldReplaceSplashStart(Plugin.Enabled, Plugin.SkipBootIntroEnabled))
            return true;

        __result = HopToMainMenuAfterLoad();
        return false;
    }

    /// <summary>
    /// Stock shows the press-any-key title while <c>_isFirstLoad</c> (static) is true.
    /// Clear it before Awake so content/menu path runs like a return visit.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(MainMenuManager), "Awake")]
    private static void MainMenuAwakePrefix()
    {
        if (!BootSkipPolicy.ShouldReplaceSplashStart(Plugin.Enabled, Plugin.SkipBootIntroEnabled))
            return;

        MainMenuManager._isFirstLoad = false;
    }

    /// <summary>
    /// Only repair the MainMenu settings instance when Awake configured it as SplashScreen
    /// (stock splash path auto-opens Audio). Does not rewrite other ConfigureSettingsMenu callers.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuManager), "Awake")]
    private static void MainMenuAwakePostfix(MainMenuManager __instance)
    {
        if (!BootSkipPolicy.ShouldReplaceSplashStart(Plugin.Enabled, Plugin.SkipBootIntroEnabled))
            return;

        var menu = __instance._settingsMenu;
        if (menu == null)
            return;

        if (!BootSkipPolicy.NeedsMainMenuSettingsRootRepair(true, menu._activatingScene))
            return;

        menu.ConfigureSettingsMenu("MainMenu");
        ShowSettingsCategoryRoot(menu);
    }

    private static void ShowSettingsCategoryRoot(SettingsMenuManager menu)
    {
        if ((bool)menu._riftAudioSettingsController)
            menu._riftAudioSettingsController.gameObject.SetActive(false);
        if ((bool)menu._riftDisplaySettingsController)
            menu._riftDisplaySettingsController.gameObject.SetActive(false);
        if ((bool)menu._riftGameplaySettingsController)
            menu._riftGameplaySettingsController.gameObject.SetActive(false);
        if ((bool)menu._riftLanguageSettingsController)
            menu._riftLanguageSettingsController.gameObject.SetActive(false);
        if ((bool)menu._riftAccessibilitySettingsController)
            menu._riftAccessibilitySettingsController.gameObject.SetActive(false);
        if ((bool)menu._riftOtherSettingsController)
            menu._riftOtherSettingsController.gameObject.SetActive(false);
        if ((bool)menu._contentParent)
            menu._contentParent.SetActive(true);
    }

    private static IEnumerator HopToMainMenuAfterLoad()
    {
        SceneLoadingController.Instance.FlagLoadingComplete(
            RiftAnalyticsTimeTracker.GameEnvironmentType.Startup);

        if (BootSkipPolicy.ShouldWaitForLoadCover(SceneLoadingController.Instance.IsLoading))
            yield return new WaitUntil(() => !SceneLoadingController.Instance.IsLoading);

        PlayerSaveController.Instance.SetHasSeenSplashScreens(hasSeen: true);
        SceneLoadingController.Instance.GoToScene("MainMenu");
    }
}
