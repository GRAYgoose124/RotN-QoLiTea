using HarmonyLib;
using RhythmRift;

namespace QoLiTea.Features.FieldOpacity;

/// <summary>
/// Scale stock tile alpha by configured opacity. Tiles only — arrows/strings stay stock.
/// </summary>
[HarmonyPatch]
internal static class FieldOpacityPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(RRTileView), nameof(RRTileView.SetAlpha))]
    private static void SetAlphaPrefix(ref float alpha)
    {
        if (!FieldOpacityPolicy.ShouldScale(Plugin.Enabled, Plugin.FieldOpacityEnabled))
            return;

        alpha = FieldOpacityPolicy.ScaleAlpha(alpha, Plugin.FieldOpacityPercent);
    }
}
