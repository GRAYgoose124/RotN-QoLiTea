using HarmonyLib;
using Shared.TrackSelection;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>
/// Kick Workshop AutoScan when Custom Music opens; flush on destroy.
/// </summary>
[HarmonyPatch]
public static class WorkshopAutoScanPatches
{
    private static CustomTracksSelectionSceneController _openedFor;

    [HarmonyPostfix]
    [HarmonyPatch(
        typeof(CustomTracksSelectionSceneController),
        nameof(CustomTracksSelectionSceneController.UpdateTrackList))]
    public static void UpdateTrackListPostfix(CustomTracksSelectionSceneController __instance)
    {
        if (!Plugin.IsWorkshopAutoScanActive || __instance == null)
            return;

        if (ReferenceEquals(_openedFor, __instance))
            return;

        _openedFor = __instance;
        WorkshopAutoScanController.OnCustomMusicOpened(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CustomTracksSelectionSceneController), "OnDestroy")]
    public static void OnDestroyPrefix(CustomTracksSelectionSceneController __instance)
    {
        if (ReferenceEquals(_openedFor, __instance))
            _openedFor = null;

        WorkshopAutoScanController.OnCustomMusicClosed(__instance);
    }
}
