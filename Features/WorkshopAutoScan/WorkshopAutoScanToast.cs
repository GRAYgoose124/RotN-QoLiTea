using UnityEngine;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>Short-lived IMGUI toast on Custom Music (e.g. hotkey with empty stash).</summary>
public sealed class WorkshopAutoScanToast : MonoBehaviour
{
    private const float DefaultSeconds = 2.2f;
    private const float UiScale = 2.5f;
    private const float PanelWidth = 280f;
    private const float PanelHeight = 48f;

    private static WorkshopAutoScanToast _instance;
    private static Texture2D _panelBg;

    private string _text = string.Empty;
    private float _until;

    public static void Show(string text, float seconds = DefaultSeconds)
    {
        if (string.IsNullOrEmpty(text))
            return;

        EnsureInstance();
        _instance._text = text;
        _instance._until = Time.unscaledTime + Mathf.Max(0.4f, seconds);
    }

    public static void Hide()
    {
        if (_instance == null)
            return;
        _instance._text = string.Empty;
        _instance._until = 0f;
    }

    public static void DestroyInstance()
    {
        if (_instance == null)
            return;
        Object.Destroy(_instance.gameObject);
        _instance = null;
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
            return;

        var go = new GameObject("QoLiTea_WorkshopAutoScanToast");
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<WorkshopAutoScanToast>();
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_text) || Time.unscaledTime >= _until)
            return;

        EnsurePanelBg();

        float scaledW = PanelWidth * UiScale;
        float scaledH = PanelHeight * UiScale;
        float originX = (Screen.width - scaledW) * 0.5f;
        float originY = Screen.height - scaledH - 48f * UiScale;

        Matrix4x4 prev = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(
            new Vector3(originX, originY, 0f),
            Quaternion.identity,
            new Vector3(UiScale, UiScale, 1f));

        var panel = new Rect(0f, 0f, PanelWidth, PanelHeight);
        GUI.DrawTexture(panel, _panelBg);
        GUI.Box(panel, GUIContent.none);
        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
        };
        GUI.Label(panel, _text, labelStyle);

        GUI.matrix = prev;
    }

    private static void EnsurePanelBg()
    {
        if (_panelBg != null)
            return;
        _panelBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _panelBg.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.12f, 0.92f));
        _panelBg.Apply(false, true);
    }
}
