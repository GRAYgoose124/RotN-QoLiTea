using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Networking;

namespace QoLiTea.Features.WorkshopAutoScan;

/// <summary>
/// Keyboard/gamepad picker (Rift has no mouse). Display-only IMGUI; input in Update.
/// Up/Down move · Confirm toggles select · A=auto-sub author · Cancel closes (subscribes selected then).
/// </summary>
public sealed class WorkshopAutoScanOverlay : MonoBehaviour
{
    private List<WorkshopRecentItem> _items = new List<WorkshopRecentItem>();
    private readonly HashSet<ulong> _touched = new HashSet<ulong>();
    private readonly HashSet<ulong> _pendingSubscribe = new HashSet<ulong>();
    private readonly Dictionary<ulong, Texture2D> _previewCache = new Dictionary<ulong, Texture2D>();
    private readonly Dictionary<ulong, string> _descCache = new Dictionary<ulong, string>();
    private int _selectedIndex;
    private float _scrollY;
    private Action _onClosed;
    private bool _open;
    private bool _flushed;
    private bool _subsApplied;
    private float _nextNavTime;
    private Texture2D _previewTex;
    private ulong _previewFileId;
    private Coroutine _previewLoad;
    private ulong _descFileId;
    private string _descText = string.Empty;
    private bool _descLoading;
    private Coroutine _descLoad;
    private string _statusLine = string.Empty;
    private float _statusUntil;
    private static Texture2D _panelBg;
    private static Texture2D _rowBg;
    private static Texture2D _previewPlaceholder;
    private static GUIStyle _descStyle;

    private const float PanelWidth = 780f;
    private const float PanelHeight = 480f;
    private const float ListWidth = 500f;
    private const float PreviewSize = 220f;
    private const float RowHeight = 28f;
    private const float HeaderHeight = 56f;
    private const float FooterHeight = 52f;
    /// <summary>IMGUI layout + chrome scale.</summary>
    private const float UiScale = 2.5f;
    /// <summary>Description font vs default IMGUI label (chrome uses UiScale).</summary>
    private const float DescScale = 1.75f;
    private const float NavRepeatSeconds = 0.14f;
    private const float StickDeadzone = 0.55f;

    public bool IsOpen => _open;

    public void Open(List<WorkshopRecentItem> items, Action onClosed)
    {
        EnsureTextures();
        _items = items ?? new List<WorkshopRecentItem>();
        _onClosed = onClosed;
        _touched.Clear();
        _pendingSubscribe.Clear();
        _selectedIndex = 0;
        _scrollY = 0f;
        _flushed = false;
        _subsApplied = false;
        _open = true;
        _nextNavTime = 0f;
        _previewTex = null;
        _previewFileId = 0;
        _descFileId = 0;
        _descText = string.Empty;
        _descLoading = false;
        _statusLine = string.Empty;

        if (_items.Count > 0)
        {
            MarkFocusedSeen();
            RequestPreviewForSelection();
            RequestDescriptionForSelection();
        }
    }

    public void FlushSeen()
    {
        if (_flushed)
            return;

        if (_touched.Count > 0)
            WorkshopSeenStore.MarkTouchedAndSave(_touched);
        else
            WorkshopSeenStore.EnsureInitialized();

        _flushed = true;
    }

    public void Close()
    {
        if (!_open)
            return;

        ApplyPendingSubscriptions();
        FlushSeen();
        StopPreviewLoad();
        StopDescLoad();
        _open = false;
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    private void ApplyPendingSubscriptions()
    {
        if (_subsApplied)
            return;
        _subsApplied = true;

        int ok = 0;
        int fail = 0;
        foreach (ulong fileId in _pendingSubscribe)
        {
            if (WorkshopRecentQuery.TrySubscribe(fileId))
            {
                ok++;
                Plugin.Logger?.LogInfo($"WorkshopAutoScan: subscribed {fileId}");
            }
            else
            {
                fail++;
                Plugin.Logger?.LogWarning($"WorkshopAutoScan: subscribe failed for {fileId}");
            }
        }

        if (_pendingSubscribe.Count > 0)
            Plugin.Logger?.LogInfo($"WorkshopAutoScan: close — subscribed {ok}, failed {fail}");
    }

    private void Update()
    {
        if (!_open)
            return;

        if (WasCancelPressed())
        {
            Close();
            return;
        }

        if (_items.Count == 0)
            return;

        if (WasAuthorAutoSubPressed())
        {
            ApplyAuthorAutoSub();
            return;
        }

        if (WasConfirmPressed())
        {
            TogglePendingSelected();
            return;
        }

        int delta = ReadNavDelta();
        if (delta == 0)
            return;

        float now = Time.unscaledTime;
        if (now < _nextNavTime)
            return;

        _nextNavTime = now + NavRepeatSeconds;
        MoveSelection(delta);
    }

    private void MoveSelection(int delta)
    {
        int next = Mathf.Clamp(_selectedIndex + delta, 0, _items.Count - 1);
        if (next == _selectedIndex)
            return;

        _selectedIndex = next;
        MarkFocusedSeen();
        EnsureSelectedVisible();
        RequestPreviewForSelection();
        RequestDescriptionForSelection();
    }

    private void MarkFocusedSeen()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
            return;
        _touched.Add(_items[_selectedIndex].FileId);
    }

    private void EnsureSelectedVisible()
    {
        float listHeight = PanelHeight - HeaderHeight - FooterHeight;
        float rowTop = _selectedIndex * RowHeight;
        float rowBottom = rowTop + RowHeight;

        if (rowTop < _scrollY)
            _scrollY = rowTop;
        else if (rowBottom > _scrollY + listHeight)
            _scrollY = rowBottom - listHeight;

        float maxScroll = Mathf.Max(0f, _items.Count * RowHeight - listHeight);
        _scrollY = Mathf.Clamp(_scrollY, 0f, maxScroll);
    }

    private void TogglePendingSelected()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
            return;

        var item = _items[_selectedIndex];
        _touched.Add(item.FileId);

        if (!_pendingSubscribe.Add(item.FileId))
            _pendingSubscribe.Remove(item.FileId);
    }

    private void ApplyAuthorAutoSub()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
            return;

        var focused = _items[_selectedIndex];
        if (focused.OwnerId == 0)
            return;

        WorkshopAuthorStore.AddOwnerAndSave(focused.OwnerId);

        int count = 0;
        foreach (var item in _items)
        {
            if (item.OwnerId != focused.OwnerId)
                continue;
            _pendingSubscribe.Add(item.FileId);
            _touched.Add(item.FileId);
            count++;
        }

        string name = string.IsNullOrEmpty(focused.Author) ? focused.OwnerId.ToString() : focused.Author;
        _statusLine = $"auto-sub {name} ({count} in list)";
        _statusUntil = Time.unscaledTime + 2.5f;
        Plugin.Logger?.LogInfo(
            $"WorkshopAutoScan: author auto-sub OwnerId={focused.OwnerId} pending={count}");
    }

    private void RequestPreviewForSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
        {
            _previewTex = null;
            _previewFileId = 0;
            return;
        }

        var item = _items[_selectedIndex];
        if (_previewFileId == item.FileId && _previewTex != null)
            return;

        _previewFileId = item.FileId;

        if (_previewCache.TryGetValue(item.FileId, out Texture2D cached))
        {
            _previewTex = cached;
            return;
        }

        _previewTex = null;
        StopPreviewLoad();
        if (string.IsNullOrEmpty(item.PreviewUrl))
            return;

        _previewLoad = StartCoroutine(LoadPreviewCoroutine(item.FileId, item.PreviewUrl));
    }

    private void RequestDescriptionForSelection()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
        {
            _descFileId = 0;
            _descText = string.Empty;
            _descLoading = false;
            return;
        }

        var item = _items[_selectedIndex];
        if (_descFileId == item.FileId && (!_descLoading || _descCache.ContainsKey(item.FileId)))
        {
            if (_descCache.TryGetValue(item.FileId, out string cached))
                _descText = cached;
            return;
        }

        _descFileId = item.FileId;

        if (!string.IsNullOrEmpty(item.Description))
        {
            string stripped = WorkshopDescriptionText.StripBbCode(item.Description);
            _descCache[item.FileId] = stripped;
            _descText = stripped;
            _descLoading = false;
            return;
        }

        if (_descCache.TryGetValue(item.FileId, out string hit))
        {
            _descText = hit;
            _descLoading = false;
            return;
        }

        _descText = string.Empty;
        _descLoading = true;
        StopDescLoad();
        _descLoad = StartCoroutine(LoadDescriptionCoroutine(item.FileId));
    }

    private void StopPreviewLoad()
    {
        if (_previewLoad == null)
            return;
        StopCoroutine(_previewLoad);
        _previewLoad = null;
    }

    private void StopDescLoad()
    {
        if (_descLoad == null)
            return;
        StopCoroutine(_descLoad);
        _descLoad = null;
    }

    private IEnumerator LoadPreviewCoroutine(ulong fileId, string url)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            yield return req.SendWebRequest();

            if (_previewFileId != fileId)
                yield break;

            if (req.result != UnityWebRequest.Result.Success)
            {
                Plugin.Logger?.LogWarning($"WorkshopAutoScan: preview fetch failed ({fileId}): {req.error}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null)
                yield break;

            _previewCache[fileId] = tex;
            if (_previewFileId == fileId)
                _previewTex = tex;
        }

        _previewLoad = null;
    }

    private IEnumerator LoadDescriptionCoroutine(ulong fileId)
    {
        Task<string> task = WorkshopRecentQuery.FetchDescriptionAsync(fileId);
        while (!task.IsCompleted)
            yield return null;

        if (_descFileId != fileId)
        {
            _descLoad = null;
            yield break;
        }

        string raw = string.Empty;
        try
        {
            raw = task.Status == TaskStatus.RanToCompletion ? (task.Result ?? string.Empty) : string.Empty;
        }
        catch (Exception e)
        {
            Plugin.Logger?.LogWarning($"WorkshopAutoScan: desc await failed ({fileId}): {e.Message}");
        }

        string stripped = WorkshopDescriptionText.StripBbCode(raw);
        _descCache[fileId] = stripped;

        // Stash on item for this session.
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].FileId == fileId)
            {
                _items[i].Description = raw;
                break;
            }
        }

        if (_descFileId == fileId)
        {
            _descText = stripped;
            _descLoading = false;
        }

        _descLoad = null;
    }

    private void OnGUI()
    {
        if (!_open)
            return;

        EnsureTextures();
        EnsureDescStyle();

        float scaledW = PanelWidth * UiScale;
        float scaledH = PanelHeight * UiScale;
        float originX = (Screen.width - scaledW) * 0.5f;
        float originY = (Screen.height - scaledH) * 0.5f;

        Matrix4x4 prevMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(
            new Vector3(originX, originY, 0f),
            Quaternion.identity,
            new Vector3(UiScale, UiScale, 1f));

        var panel = new Rect(0f, 0f, PanelWidth, PanelHeight);

        GUI.DrawTexture(panel, _panelBg);
        GUI.Box(panel, GUIContent.none);

        GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 8f, ListWidth, PanelHeight - 16f));
        GUILayout.Label("New Workshop tracks");
        GUILayout.Label("Up/Down  ·  Confirm=select  ·  A=auto-sub author  ·  Cancel=close");
        GUILayout.Space(4);

        float listHeight = PanelHeight - HeaderHeight - FooterHeight;
        var view = GUILayoutUtility.GetRect(ListWidth - 8f, listHeight);
        GUI.BeginGroup(view);
        GUI.BeginGroup(new Rect(0f, -_scrollY, view.width, _items.Count * RowHeight + 4f));

        for (int i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var row = new Rect(0f, i * RowHeight, view.width, RowHeight);

            if (i == _selectedIndex)
                GUI.DrawTexture(row, _rowBg);

            bool pending = _pendingSubscribe.Contains(item.FileId);
            string mark = pending ? "[X]" : "[ ]";
            string author = string.IsNullOrEmpty(item.Author) ? "?" : item.Author;
            string title = string.IsNullOrEmpty(item.Title) ? $"#{item.FileId}" : item.Title;
            string prefix = $"  {mark}  {title}  —  ";
            GUIStyle labelStyle = GUI.skin.label;
            Vector2 prefixSize = labelStyle.CalcSize(new GUIContent(prefix));
            GUI.Label(row, prefix);
            var authorRect = new Rect(
                row.x + prefixSize.x,
                row.y,
                Mathf.Max(8f, row.width - prefixSize.x),
                row.height);
            DrawAuthorLabel(authorRect, author, WorkshopAuthorStore.Contains(item.OwnerId), labelStyle);
        }

        GUI.EndGroup();
        GUI.EndGroup();

        GUILayout.FlexibleSpace();
        int shown = _items.Count == 0 ? 0 : _selectedIndex + 1;
        string footer = $"{shown}/{_items.Count}  ·  {_pendingSubscribe.Count} selected (sub on close)";
        if (!string.IsNullOrEmpty(_statusLine) && Time.unscaledTime < _statusUntil)
            footer = _statusLine;
        GUILayout.Label(footer);
        GUILayout.EndArea();

        // Right column: author → preview (description drawn unscaled below).
        float previewX = panel.x + ListWidth + 24f;
        float rightTop = panel.y + 12f;
        float rightWidth = PreviewSize;
        string authorLabel = "?";
        bool authorAutoSub = false;
        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
        {
            var sel = _items[_selectedIndex];
            authorLabel = string.IsNullOrEmpty(sel.Author) ? "?" : sel.Author;
            authorAutoSub = WorkshopAuthorStore.Contains(sel.OwnerId);
        }

        DrawAuthorLabel(
            new Rect(previewX, rightTop, rightWidth, 22f),
            authorLabel,
            authorAutoSub,
            GUI.skin.label);
        float previewY = rightTop + 26f;
        var previewRect = new Rect(previewX, previewY, PreviewSize, PreviewSize);
        GUI.DrawTexture(previewRect, _previewPlaceholder);
        if (_previewTex != null)
            GUI.DrawTexture(previewRect, _previewTex, ScaleMode.ScaleToFit);
        else
            GUI.Label(previewRect, "\n\n  (no preview)");

        float descY = previewY + PreviewSize + 8f;
        float descBottom = panel.y + PanelHeight - 12f;
        float descHeight = Mathf.Max(40f, descBottom - descY);

        GUI.matrix = prevMatrix;

        string descDraw;
        if (_descLoading && string.IsNullOrEmpty(_descText))
            descDraw = "(loading…)";
        else if (string.IsNullOrEmpty(_descText))
            descDraw = "(no description)";
        else
            descDraw = _descText;

        // Description area follows UiScale layout; font at DescScale only.
        var descRect = new Rect(
            originX + previewX * UiScale,
            originY + descY * UiScale,
            rightWidth * UiScale,
            descHeight * UiScale);

        GUI.Label(descRect, descDraw, _descStyle);
    }

    private void OnDestroy()
    {
        if (_open)
        {
            ApplyPendingSubscriptions();
            FlushSeen();
        }

        StopPreviewLoad();
        StopDescLoad();

        foreach (var kv in _previewCache)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _previewCache.Clear();
        _previewTex = null;
    }

    private static void EnsureTextures()
    {
        if (_panelBg == null)
            _panelBg = MakeTex(new Color(0.07f, 0.08f, 0.12f, 0.96f));
        if (_rowBg == null)
            _rowBg = MakeTex(new Color(0.25f, 0.45f, 0.75f, 0.85f));
        if (_previewPlaceholder == null)
            _previewPlaceholder = MakeTex(new Color(0.05f, 0.05f, 0.07f, 0.95f));
    }

    /// <summary>Must run from OnGUI — touches GUI.skin.</summary>
    private static void EnsureDescStyle()
    {
        if (_descStyle != null)
            return;

        _descStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
        int baseSize = _descStyle.fontSize > 0 ? _descStyle.fontSize : 13;
        _descStyle.fontSize = Mathf.RoundToInt(baseSize * DescScale);
    }

    private static readonly Color AutoSubGold = new Color(1f, 0.84f, 0.2f, 1f);
    private static readonly Color AutoSubPurple = new Color(0.62f, 0.28f, 0.92f, 1f);
    private const float AutoSubHueSpeed = 0.55f;
    private const float AutoSubPulseSpeed = 2.4f;
    private const float AutoSubOutlinePx = 1.25f;

    /// <summary>
    /// Autosub authors: rainbow-cycling outline + gold↔purple pulsing fill.
    /// </summary>
    private static void DrawAuthorLabel(Rect rect, string text, bool autoSub, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!autoSub)
        {
            GUI.Label(rect, text, style);
            return;
        }

        Color prev = GUI.color;
        float t = Time.unscaledTime;
        float hueBase = Mathf.Repeat(t * AutoSubHueSpeed, 1f);
        float pulse = (Mathf.Sin(t * AutoSubPulseSpeed) + 1f) * 0.5f;
        Color fill = Color.Lerp(AutoSubGold, AutoSubPurple, pulse);

        // 8-way outline; each offset rides a different hue so the ring is a cycling gradient.
        for (int i = 0; i < 8; i++)
        {
            float ang = i * (Mathf.PI * 0.25f);
            float hx = Mathf.Cos(ang) * AutoSubOutlinePx;
            float hy = Mathf.Sin(ang) * AutoSubOutlinePx;
            float hue = Mathf.Repeat(hueBase + i / 8f, 1f);
            GUI.color = Color.HSVToRGB(hue, 0.95f, 1f);
            GUI.Label(new Rect(rect.x + hx, rect.y + hy, rect.width, rect.height), text, style);
        }

        GUI.color = fill;
        GUI.Label(rect, text, style);
        GUI.color = prev;
    }

    private static Texture2D MakeTex(Color color)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply(false, true);
        return tex;
    }

    private static bool WasCancelPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            return true;

        var pad = Gamepad.current;
        if (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame))
            return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
                return true;
            if (Input.GetButtonDown("Cancel"))
                return true;
        }
        catch
        {
            // Input Manager may be disabled.
        }

        return false;
    }

    private static bool WasConfirmPressed()
    {
        var kb = Keyboard.current;
        if (kb != null &&
            (kb.enterKey.wasPressedThisFrame ||
             kb.numpadEnterKey.wasPressedThisFrame ||
             kb.spaceKey.wasPressedThisFrame))
            return true;

        var pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
                return true;
            if (Input.GetButtonDown("Submit"))
                return true;
        }
        catch
        {
            // Input Manager may be disabled.
        }

        return false;
    }

    private static bool WasAuthorAutoSubPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.aKey.wasPressedThisFrame)
            return true;

        var pad = Gamepad.current;
        if (pad != null && pad.buttonWest.wasPressedThisFrame)
            return true;

        try
        {
            if (Input.GetKeyDown(KeyCode.A))
                return true;
        }
        catch
        {
            // Input Manager may be disabled.
        }

        return false;
    }

    private int ReadNavDelta()
    {
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (WasPressedOrHeld(kb.upArrowKey) || WasPressedOrHeld(kb.wKey))
                return -1;
            if (WasPressedOrHeld(kb.downArrowKey) || WasPressedOrHeld(kb.sKey))
                return 1;
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (WasPressedOrHeld(pad.dpad.up) || StickUp(pad))
                return -1;
            if (WasPressedOrHeld(pad.dpad.down) || StickDown(pad))
                return 1;
        }

        try
        {
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
                return -1;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
                return 1;
        }
        catch
        {
            // Input Manager may be disabled.
        }

        return 0;
    }

    private static bool WasPressedOrHeld(ButtonControl button)
    {
        return button != null && (button.wasPressedThisFrame || button.isPressed);
    }

    private static bool StickUp(Gamepad pad)
    {
        return pad.leftStick.y.ReadValue() > StickDeadzone;
    }

    private static bool StickDown(Gamepad pad)
    {
        return pad.leftStick.y.ReadValue() < -StickDeadzone;
    }
}
