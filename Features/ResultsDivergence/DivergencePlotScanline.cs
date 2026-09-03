using UnityEngine;
using UnityEngine.UI;

namespace QoLiTea.Features.ResultsDivergence;

internal sealed class DivergencePlotScanline : MonoBehaviour
{
    private RawImage _plot;
    private RectTransform _plotRect;
    private Image _scanline;
    private Text _beatText;
    private Canvas _canvas;
    private Camera _canvasCamera;

    internal static void Attach(RawImage plot)
    {
        if (plot == null)
            return;

        var controller = plot.gameObject.GetComponent<DivergencePlotScanline>();
        if (controller == null)
            controller = plot.gameObject.AddComponent<DivergencePlotScanline>();
        controller.Initialize(plot);
    }

    private void Initialize(RawImage plot)
    {
        _plot = plot;
        _plotRect = plot.rectTransform;
        _canvas = plot.GetComponentInParent<Canvas>();
        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            _canvasCamera = _canvas.worldCamera;

        if (_scanline == null)
            CreateScanline();
        if (_beatText == null)
            CreateBeatText();

        Hide();
    }

    private void OnDestroy()
    {
        if (_scanline != null)
            Destroy(_scanline.gameObject);
        if (_beatText != null)
            Destroy(_beatText.gameObject);
    }

    private void Update()
    {
        if (_plot == null || !_plot.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        // Always track mouse position when plot is visible
        if (Input.mousePresent)
        {
            Vector2 mousePos = Input.mousePosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_plotRect, mousePos, _canvasCamera, out Vector2 local))
            {
                UpdateScanlinePosition(local);
            }
            else
            {
                Hide();
            }
        }
        else
        {
            Hide();
        }
    }

    private const float ScanlineVerticalInsetPx = 6f;

    private void CreateScanline()
    {
        var go = new GameObject("QoLiTeaDivergenceScanline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false);

        _scanline = go.GetComponent<Image>();
        _scanline.raycastTarget = false;
        _scanline.color = new Color(1f, 1f, 1f, 0.9f);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0f, ScanlineVerticalInsetPx);
        rt.offsetMax = new Vector2(0f, -ScanlineVerticalInsetPx);
        rt.sizeDelta = new Vector2(2f, 0f);
    }

    private void CreateBeatText()
    {
        var go = new GameObject("QoLiTeaDivergenceBeatLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(transform, false);

        _beatText = go.GetComponent<Text>();
        _beatText.raycastTarget = false;
        _beatText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _beatText.fontStyle = FontStyle.Bold;
        _beatText.fontSize = 12;
        _beatText.alignment = TextAnchor.MiddleCenter;
        _beatText.color = DivergencePlotColors.AxisLabel;
        _beatText.text = "Beat 0.0";

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(76f, 20f);
        rt.anchoredPosition = Vector2.zero;
    }

    private void UpdateScanlinePosition(Vector2 local)
    {
        float plotWidth = _plotRect.rect.width;
        float plotHeight = _plotRect.rect.height;
        if (plotWidth <= 0f || plotHeight <= 0f)
        {
            Hide();
            return;
        }

        float halfWidth = plotWidth * 0.5f;
        float clampedX = Mathf.Clamp(local.x, -halfWidth, halfWidth);
        float xRatio = Mathf.Clamp01((clampedX + halfWidth) / plotWidth);
        float totalBeats = DivergencePlotSession.Context?.TotalBeats ?? 1f;
        float beat = xRatio * totalBeats;

        _scanline.gameObject.SetActive(true);
        var srt = _scanline.rectTransform;
        srt.offsetMin = new Vector2(0f, ScanlineVerticalInsetPx);
        srt.offsetMax = new Vector2(0f, -ScanlineVerticalInsetPx);
        srt.anchoredPosition = new Vector2(clampedX, 0f);

        _beatText.gameObject.SetActive(true);
        _beatText.text = $"Beat {FormatBeat(beat)}";
        _beatText.rectTransform.anchoredPosition = new Vector2(clampedX, -16f);
    }

    private void Hide()
    {
        if (_scanline != null)
            _scanline.gameObject.SetActive(false);
        if (_beatText != null)
            _beatText.gameObject.SetActive(false);
    }

    private static string FormatBeat(float beat)
    {
        if (beat >= 100f)
            return beat.ToString("F0");
        return beat.ToString("F1");
    }
}
