using System.Collections.Generic;
using RhythmRift;
using UnityEngine;
using UnityEngine.UI;

namespace QoLiTea.Features.ResultsDivergence;

internal static class DivergencePlotRenderer
{
    private const int TexW = 512;
    private const int TexH = 128;
    private const string RootName = "QoLiTeaDivergencePlot";

    /// <summary>Full width, docked to bottom and sides; same vertical band as the old centered plot.</summary>
    private static readonly Vector2 AnchorMin = new(0f, 0.02f);
    private static readonly Vector2 AnchorMax = new(1f, 0.27f);

    internal static RawImage Build(
        Transform parent,
        IReadOnlyList<HitDivergenceSample> hits,
        IReadOnlyList<PracticeSpan> spans,
        float totalBeats,
        int opacityPercent,
        AccuracyBar stockColorSource = null)
    {
        if (parent == null)
            return null;

        var existing = parent.Find(RootName);
        if (existing != null)
            Object.Destroy(existing.gameObject);

        var go = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = AnchorMin;
        rt.anchorMax = AnchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var raw = go.GetComponent<RawImage>();
        raw.texture = RenderTexture(hits, spans, totalBeats, stockColorSource);
        raw.raycastTarget = false;
        ApplyOpacity(raw, opacityPercent);
        return raw;
    }

    internal static void ApplyOpacity(RawImage raw, int opacityPercent)
    {
        if (raw == null)
            return;

        float a = ResultsDivergencePolicy.ToAlpha(opacityPercent);
        var c = raw.color;
        c.a = a;
        raw.color = c;
    }

    private static Texture2D RenderTexture(
        IReadOnlyList<HitDivergenceSample> hits,
        IReadOnlyList<PracticeSpan> spans,
        float totalBeats,
        AccuracyBar stockColorSource)
    {
        var tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "QoLiTeaDivergencePlotTex",
        };

        var pixels = new Color[TexW * TexH];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = DivergencePlotColors.Background;

        if (spans != null)
        {
            foreach (var span in spans)
            {
                var (x0, x1) = DivergencePlotLayout.SpanBandX(span, totalBeats);
                int px0 = Mathf.Clamp(Mathf.FloorToInt(x0 * (TexW - 1)), 0, TexW - 1);
                int px1 = Mathf.Clamp(Mathf.CeilToInt(x1 * (TexW - 1)), 0, TexW - 1);
                for (int x = px0; x <= px1; x++)
                {
                    for (int y = 0; y < TexH; y++)
                        Blend(pixels, x, y, DivergencePlotColors.SpanBand);
                }
            }
        }

        int centerY = TexH / 2;
        for (int x = 0; x < TexW; x++)
        {
            pixels[centerY * TexW + x] = DivergencePlotColors.CenterLine;
            if (centerY + 1 < TexH)
                pixels[(centerY + 1) * TexW + x] = DivergencePlotColors.CenterLine;
        }

        if (hits != null)
        {
            foreach (var hit in hits)
            {
                float nx = DivergencePlotLayout.BeatToX(hit.TargetBeat, totalBeats);
                float ny = DivergencePlotLayout.DivergenceToY(hit.SignedDivergence);
                int px = Mathf.Clamp(Mathf.RoundToInt(nx * (TexW - 1)), 0, TexW - 1);
                int py = Mathf.Clamp(Mathf.RoundToInt(ny * (TexH - 1)), 0, TexH - 1);
                Color c = DivergencePlotColors.ForRating(hit.Rating, stockColorSource);
                StampDot(pixels, px, py, c);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static void StampDot(Color[] pixels, int cx, int cy, Color c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            int x = cx + dx;
            int y = cy + dy;
            if (x < 0 || x >= TexW || y < 0 || y >= TexH)
                continue;
            pixels[y * TexW + x] = c;
        }
    }

    private static void Blend(Color[] pixels, int x, int y, Color over)
    {
        int i = y * TexW + x;
        Color under = pixels[i];
        float a = over.a;
        pixels[i] = new Color(
            over.r * a + under.r * (1f - a),
            over.g * a + under.g * (1f - a),
            over.b * a + under.b * (1f - a),
            Mathf.Max(under.a, over.a));
    }
}
