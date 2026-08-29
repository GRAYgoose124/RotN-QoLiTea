using HarmonyLib;
using Shared;

namespace QoLiTea.Features.ResultsDivergence;

[HarmonyPatch]
internal static class ResultsDivergenceInputPatches
{
    private static bool _toggleKeyHeld;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScoreResultsView), nameof(ScoreResultsView.Update))]
    private static void UpdatePostfix(ScoreResultsView __instance)
    {
        if (__instance == null || !Plugin.IsResultsDivergencePlotActive)
            return;
        if (DivergencePlotSession.ActivePlot == null)
            return;
        if (__instance.InputDisabled)
            return;

        if (!Plugin.TryConsumeKeyEdge(ref _toggleKeyHeld, Plugin.ResultsDivergenceToggleKey))
            return;

        DivergencePlotSession.ToggleVisibility(Plugin.ResultsDivergencePlotOpacityPercent);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScoreResultsView), nameof(ScoreResultsView.Hide))]
    private static void HidePostfix()
    {
        _toggleKeyHeld = false;
        DivergencePlotSession.Reset();
    }
}
