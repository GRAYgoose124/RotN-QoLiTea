using System.Collections.Generic;
using RhythmRift;
using Shared;
using Shared.RhythmEngine;
using UnityEngine;
using UnityEngine.UI;

namespace QoLiTea.Features.ResultsDivergence;

internal static class DivergencePlotRenderer
{
    private const string RootName = "QoLiTeaDivergencePlot";

    private static readonly Vector2 DockedAnchorMin = new(0f, 0.02f);
    private static readonly Vector2 DockedAnchorMax = new(1f, 0.27f);
    private static readonly Vector2 FullscreenAnchorMin = Vector2.zero;
    private static readonly Vector2 FullscreenAnchorMax = Vector2.one;

    internal static RawImage Build(
        Transform parent,
        PlotRenderContext context,
        PlotDisplayMode mode)
    {
        if (parent == null || context == null)
            return null;

        var existing = parent.Find(RootName);
        if (existing != null)
            Object.Destroy(existing.gameObject);

        var go = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.transform.SetParent(parent, false);
        ApplyLayout(go, mode);

        var raw = go.GetComponent<RawImage>();
        bool fullscreen = mode == PlotDisplayMode.Fullscreen;
        raw.texture = RenderTexture(context, fullscreen);
        raw.raycastTarget = false;
        ApplyOpacity(raw, context.OpacityPercent);
        DivergencePlotScanline.Attach(raw);
        return raw;
    }

    internal static void ApplyLayout(GameObject plotGo, PlotDisplayMode mode)
    {
        if (plotGo == null)
            return;

        var rt = plotGo.GetComponent<RectTransform>();
        if (mode == PlotDisplayMode.Fullscreen)
        {
            rt.anchorMin = FullscreenAnchorMin;
            rt.anchorMax = FullscreenAnchorMax;
        }
        else
        {
            rt.anchorMin = DockedAnchorMin;
            rt.anchorMax = DockedAnchorMax;
        }

        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
    }

    internal static void RefreshTexture(RawImage raw, PlotRenderContext context, PlotDisplayMode mode)
    {
        if (raw == null || context == null)
            return;

        bool fullscreen = mode == PlotDisplayMode.Fullscreen;
        var old = raw.texture;
        raw.texture = RenderTexture(context, fullscreen);
        if (old != null)
            Object.Destroy(old);
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

    private static Texture2D RenderTexture(PlotRenderContext context, bool fullscreen)
    {
        int texW = fullscreen ? 1024 : 512;
        int texH = fullscreen ? 512 : 128;

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
        {
            filterMode = fullscreen ? FilterMode.Point : FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = fullscreen ? "QoLiTeaDivergencePlotTexFull" : "QoLiTeaDivergencePlotTex",
        };

        var pixels = new Color[texW * texH];
        Color bg = fullscreen ? DivergencePlotColors.BackgroundFullscreen : DivergencePlotColors.Background;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = bg;

        DrawSpanBands(pixels, texW, texH, context.VibeSpans, context.TotalBeats, DivergencePlotColors.VibeBand);
        DrawSpanBands(pixels, texW, texH, context.WorstSpans, context.TotalBeats, DivergencePlotColors.SpanBand);

        if (fullscreen)
        {
            DrawTimingBinLines(pixels, texW, texH, context.TimingBinMagnitudes, fullscreen: true);
            DrawSuperCritBinLines(pixels, texW, texH, context.SuperCritBinMagnitude, fullscreen: true);
            DrawCenterLine(pixels, texW, texH, fullscreen: true);
        }
        else
        {
            DrawCenterLine(pixels, texW, texH, fullscreen: false);
        }

        DrawSideMarker(pixels, texW, texH, true, DivergencePlotColors.AxisLabel);
        DrawSideMarker(pixels, texW, texH, false, DivergencePlotColors.AxisLabel);

        foreach (var hit in context.Hits)
        {
            float nx = DivergencePlotLayout.BeatToX(hit.TargetBeat, context.TotalBeats);
            int px = Mathf.Clamp(Mathf.RoundToInt(nx * (texW - 1)), 0, texW - 1);
            Color c = DivergencePlotColors.ForHit(hit, context.StockColorSource);

            if (hit.Marker == PlotMarkerKind.VerticalLine)
            {
                StampVerticalLine(pixels, texW, texH, px, c);
                continue;
            }

            int py = DivergencePlotLayout.DivergenceToRow(texH, hit.SignedDivergence);
            StampDot(pixels, texW, texH, px, py, c);
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static void DrawSpanBands<T>(
        Color[] pixels,
        int texW,
        int texH,
        IReadOnlyList<T> spans,
        float totalBeats,
        Color color)
        where T : struct
    {
        if (spans == null || spans.Count == 0)
            return;

        foreach (var span in spans)
        {
            var band = span is PracticeSpan practice
                ? DivergencePlotLayout.SpanBandX(practice, totalBeats)
                : span is ChartBeatSpan chart
                    ? DivergencePlotLayout.SpanBandX(chart.StartBeat, chart.EndBeat, totalBeats)
                    : default;
            int px0 = Mathf.Clamp(Mathf.FloorToInt(band.x0 * (texW - 1)), 0, texW - 1);
            int px1 = Mathf.Clamp(Mathf.CeilToInt(band.x1 * (texW - 1)), 0, texW - 1);
            for (int x = px0; x <= px1; x++)
            {
                for (int y = 0; y < texH; y++)
                    Blend(pixels, texW, x, y, color);
            }
        }
    }

    private static void DrawTimingBinLines(
        Color[] pixels,
        int texW,
        int texH,
        IReadOnlyList<float> magnitudes,
        bool fullscreen = false)
    {
        if (magnitudes == null)
            return;

        Color line = fullscreen ? DivergencePlotColors.TimingBinLineFullscreen : DivergencePlotColors.TimingBinLine;
        foreach (float mag in magnitudes)
        {
            int pyEarly = DivergencePlotLayout.DivergenceToRow(texH, -mag);
            int pyLate = DivergencePlotLayout.DivergenceToRow(texH, mag);
            DrawHorizontalLine(pixels, texW, texH, pyEarly, line, 1);
            DrawHorizontalLine(pixels, texW, texH, pyLate, line, 1);
        }
    }

    private static void DrawSuperCritBinLines(
        Color[] pixels,
        int texW,
        int texH,
        float magnitude,
        bool fullscreen = false)
    {
        if (magnitude <= 0f)
            return;

        Color line = fullscreen ? DivergencePlotColors.SuperCritBinLineFullscreen : DivergencePlotColors.SuperCritBinLine;
        int pyEarly = DivergencePlotLayout.DivergenceToRow(texH, -magnitude);
        int pyLate = DivergencePlotLayout.DivergenceToRow(texH, magnitude);
        DrawHorizontalLine(pixels, texW, texH, pyEarly, line, 1);
        DrawHorizontalLine(pixels, texW, texH, pyLate, line, 1);
    }

    private static void DrawCenterLine(
        Color[] pixels,
        int texW,
        int texH,
        bool fullscreen)
    {
        Color line = fullscreen ? DivergencePlotColors.CenterLineFullscreen : DivergencePlotColors.CenterLine;
        int pyCenter = DivergencePlotLayout.DivergenceToRow(texH, 0f);
        int thickness = fullscreen ? 1 : 2;
        DrawHorizontalLine(pixels, texW, texH, pyCenter, line, thickness);
    }

    private static void DrawHorizontalLine(Color[] pixels, int texW, int texH, int centerY, Color color, int thickness)
    {
        if (thickness <= 0)
            return;

        int y0 = centerY - (thickness - 1) / 2;
        for (int t = 0; t < thickness; t++)
        {
            int row = y0 + t;
            if (row < 0 || row >= texH)
                continue;
            for (int x = 0; x < texW; x++)
                pixels[row * texW + x] = color;
        }
    }

    private static void DrawSideMarker(Color[] pixels, int texW, int texH, bool early, Color color)
    {
        int x = 8;
        int y = early ? texH - 14 : 6;
        if (early)
            StampLetterE(pixels, texW, texH, x, y, color);
        else
            StampLetterL(pixels, texW, texH, x, y, color);
    }

    private static void StampLetterE(Color[] pixels, int texW, int texH, int x, int y, Color c)
    {
        for (int dy = 0; dy < 9; dy++)
            PlotPixel(pixels, texW, texH, x, y + dy, c);
        for (int dx = 0; dx < 6; dx++)
        {
            PlotPixel(pixels, texW, texH, x + dx, y, c);
            PlotPixel(pixels, texW, texH, x + dx, y + 4, c);
            PlotPixel(pixels, texW, texH, x + dx, y + 8, c);
        }
    }

    private static void StampLetterL(Color[] pixels, int texW, int texH, int x, int y, Color c)
    {
        for (int dy = 0; dy < 9; dy++)
            PlotPixel(pixels, texW, texH, x, y + dy, c);
        for (int dx = 0; dx < 6; dx++)
            PlotPixel(pixels, texW, texH, x + dx, y + 8, c);
    }

    private static void StampDot(Color[] pixels, int texW, int texH, int cx, int cy, Color c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            PlotPixel(pixels, texW, texH, cx + dx, cy + dy, c);
    }

    private static void StampVerticalLine(Color[] pixels, int texW, int texH, int x, Color c)
    {
        for (int y = 2; y < texH - 2; y++)
            PlotPixel(pixels, texW, texH, x, y, c);
    }

    private static void PlotPixel(Color[] pixels, int texW, int texH, int x, int y, Color c)
    {
        if (x < 0 || x >= texW || y < 0 || y >= texH)
            return;
        pixels[y * texW + x] = c;
    }

    private static void Blend(Color[] pixels, int texW, int x, int y, Color over)
    {
        int i = y * texW + x;
        Color under = pixels[i];
        float a = over.a;
        pixels[i] = new Color(
            over.r * a + under.r * (1f - a),
            over.g * a + under.g * (1f - a),
            over.b * a + under.b * (1f - a),
            Mathf.Max(under.a, over.a));
    }
}
