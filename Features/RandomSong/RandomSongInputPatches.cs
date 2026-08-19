using HarmonyLib;
using Shared.TrackSelection;
using UnityEngine.InputSystem;

namespace QoLiTea.Features.RandomSong;

/// <summary>
/// Drive random-song from stock title-list Update (same place as remix CycleMode).
/// </summary>
[HarmonyPatch(typeof(TrackSelectionSceneController), nameof(TrackSelectionSceneController.Update))]
internal static class RandomSongOfficialInputPatch
{
    private static bool _logged;

    [HarmonyPostfix]
    public static void Postfix(TrackSelectionSceneController __instance)
    {
        if (__instance == null)
            return;

        if (!_logged)
        {
            _logged = true;
            var kb = Keyboard.current;
            Plugin.Logger?.LogInfo(
                $"QoLiTea: official title Update hooked (InputDisabled={__instance.InputDisabled}, Keyboard={(kb != null)})");
        }

        if (__instance.InputDisabled)
            return;

        Plugin.Instance?.TryRandomSongFromTitleList();
    }
}

[HarmonyPatch(typeof(CustomTracksSelectionSceneController), nameof(CustomTracksSelectionSceneController.Update))]
internal static class RandomSongCustomInputPatch
{
    private static bool _logged;

    [HarmonyPostfix]
    public static void Postfix(CustomTracksSelectionSceneController __instance)
    {
        if (__instance == null)
            return;

        if (!_logged)
        {
            _logged = true;
            Plugin.Logger?.LogInfo(
                $"QoLiTea: custom title Update hooked (InputDisabled={__instance.InputDisabled})");
        }

        if (__instance.InputDisabled)
            return;

        Plugin.Instance?.TryRandomSongFromTitleList();
    }
}
