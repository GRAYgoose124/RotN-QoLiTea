using HarmonyLib;
using Shared;
using UnityEngine;

namespace QoLiTea.Features.ResultsDivergence;

[HarmonyPatch]
internal static class ResultsDivergenceInputPatches
{
    private const float MultiTapWindowSeconds = 0.35f;

    private static bool _toggleKeyHeld;
    private static bool _escapeHeld;

    private static int _pendingGTaps;
    private static float _gTapExpire;

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

        FlushPendingGTaps();

        if (Plugin.TryConsumeKeyEdge(ref _escapeHeld, KeyCode.Escape))
        {
            DivergencePlotSession.Hide();
            return;
        }

        if (!Plugin.TryConsumeKeyEdge(ref _toggleKeyHeld, Plugin.ResultsDivergenceToggleKey))
            return;

        QueueGTap();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ScoreResultsView), nameof(ScoreResultsView.Hide))]
    private static void HidePostfix()
    {
        _toggleKeyHeld = false;
        _escapeHeld = false;
        _pendingGTaps = 0;
        _gTapExpire = 0f;
        DivergencePlotSession.Reset();
    }

    private static void QueueGTap()
    {
        float now = Time.unscaledTime;
        if (now > _gTapExpire)
            _pendingGTaps = 0;

        _pendingGTaps++;
        _gTapExpire = now + MultiTapWindowSeconds;
    }

    private static void FlushPendingGTaps()
    {
        if (_pendingGTaps <= 0)
            return;
        if (Time.unscaledTime < _gTapExpire)
            return;

        int taps = _pendingGTaps;
        _pendingGTaps = 0;
        DivergencePlotSession.ApplyTapAction(DivergencePlotTapPolicy.Resolve(taps));
    }
}
