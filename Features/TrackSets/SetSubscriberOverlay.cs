using System;
using System.Collections.Generic;
using QoLiTea.Features.LazyCustomTracks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QoLiTea.Features.TrackSets;

/// <summary>Multi-select saved sets; Confirm applies additive subscribe; Cancel aborts.</summary>
public sealed class SetSubscriberOverlay : MonoBehaviour
{
    private List<TrackSetRecord> _sets = new List<TrackSetRecord>();
    private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
    private int _index;
    private float _scrollY;
    private Action _onClosed;
    private bool _open;
    private bool _applied;
    private float _nextNavTime;
    private static Texture2D _panelBg;
    private static Texture2D _rowBg;

    private const float PanelWidth = 560f;
    private const float PanelHeight = 360f;
    private const float RowHeight = 28f;
    private const float UiScale = 3f;
    private const float NavRepeatSeconds = 0.14f;

    public bool IsOpen => _open;

    public void Open(List<TrackSetRecord> sets, Action onClosed)
    {
        EnsureTextures();
        _sets = sets ?? new List<TrackSetRecord>();
        _onClosed = onClosed;
        _selected.Clear();
        _index = 0;
        _scrollY = 0f;
        _applied = false;
        _open = true;
        _nextNavTime = 0f;
    }

    public void Close(bool apply)
    {
        if (!_open)
            return;

        if (apply)
            ApplySubscriptions();

        _open = false;
        var cb = _onClosed;
        _onClosed = null;
        cb?.Invoke();
    }

    private void ApplySubscriptions()
    {
        if (_applied)
            return;
        _applied = true;

        var union = new List<ulong>();
        var seen = new HashSet<ulong>();
        foreach (TrackSetRecord set in _sets)
        {
            if (set == null || string.IsNullOrEmpty(set.Name) || !_selected.Contains(set.Name))
                continue;
            if (set.FileIds == null)
                continue;
            foreach (ulong id in set.FileIds)
            {
                if (id == 0 || !seen.Add(id))
                    continue;
                union.Add(id);
            }
        }

        var already = new HashSet<ulong>(WorkshopSubActions.GetSubscribedFileIds());
        var (ok, fail, skipped) = WorkshopSubActions.SubscribeMissing(union, already);
        Plugin.Logger?.LogInfo(
            $"SetSubscriber: selected={_selected.Count} unique={union.Count} ok={ok} fail={fail} skipped={skipped}");
        LiveDeltaService.SyncWorkshopSubscriptionsToCache();
    }

    private void Update()
    {
        if (!_open)
            return;

        if (WasCancelPressed())
        {
            Close(apply: false);
            return;
        }

        // Enter = apply selected; Space / South = toggle row
        if (WasApplyPressed())
        {
            Close(apply: true);
            return;
        }

        if (WasTogglePressed())
        {
            ToggleFocused();
            return;
        }

        float now = Time.unscaledTime;
        if (now < _nextNavTime)
            return;

        int delta = 0;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed || kb.wKey.isPressed)
                delta = -1;
            else if (kb.downArrowKey.isPressed || kb.sKey.isPressed)
                delta = 1;
        }

        var gp = Gamepad.current;
        if (gp != null && delta == 0)
        {
            if (gp.dpad.up.isPressed || gp.leftStick.y.ReadValue() > 0.55f)
                delta = -1;
            else if (gp.dpad.down.isPressed || gp.leftStick.y.ReadValue() < -0.55f)
                delta = 1;
        }

        if (delta == 0 || _sets.Count == 0)
            return;

        _index = Mathf.Clamp(_index + delta, 0, _sets.Count - 1);
        _nextNavTime = now + NavRepeatSeconds;
        EnsureVisible();
    }

    private void ToggleFocused()
    {
        if (_sets.Count == 0 || _index < 0 || _index >= _sets.Count)
            return;

        TrackSetRecord set = _sets[_index];
        if (set == null || string.IsNullOrEmpty(set.Name))
            return;

        if (!_selected.Add(set.Name))
            _selected.Remove(set.Name);
    }

    private void EnsureVisible()
    {
        float listH = PanelHeight - 80f;
        float top = _index * RowHeight;
        if (top < _scrollY)
            _scrollY = top;
        else if (top + RowHeight > _scrollY + listH)
            _scrollY = top + RowHeight - listH;
    }

    private void OnGUI()
    {
        if (!_open)
            return;

        Matrix4x4 prev = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(UiScale, UiScale, 1f));

        float w = PanelWidth;
        float h = PanelHeight;
        float x = (Screen.width / UiScale - w) * 0.5f;
        float y = (Screen.height / UiScale - h) * 0.5f;

        GUI.DrawTexture(new Rect(x, y, w, h), _panelBg);
        GUILayout.BeginArea(new Rect(x + 12f, y + 10f, w - 24f, h - 20f));
        GUILayout.Label("Set Subscriber");
        if (_sets.Count == 0)
        {
            GUILayout.Space(8f);
            GUILayout.Label("No sets yet. Bulk Unsub / No-Impossible create them.");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Cancel = close");
            GUILayout.EndArea();
            GUI.matrix = prev;
            return;
        }

        GUILayout.Label("Up/Down · Space=toggle · Enter=subscribe selected · Cancel=abort");
        GUILayout.Space(4f);

        float listH = h - 80f;
        var listRect = GUILayoutUtility.GetRect(w - 24f, listH);
        GUI.BeginGroup(listRect);
        for (int i = 0; i < _sets.Count; i++)
        {
            TrackSetRecord set = _sets[i];
            if (set == null)
                continue;

            float rowY = i * RowHeight - _scrollY;
            if (rowY + RowHeight < 0f || rowY > listH)
                continue;

            var row = new Rect(0f, rowY, listRect.width, RowHeight - 2f);
            if (i == _index)
                GUI.DrawTexture(row, _rowBg);

            bool on = !string.IsNullOrEmpty(set.Name) && _selected.Contains(set.Name);
            int count = set.FileIds?.Count ?? 0;
            GUI.Label(row, $"{(on ? "[x]" : "[ ]")} {set.Name}  ({count})");
        }

        GUI.EndGroup();
        GUILayout.Label($"{_selected.Count} set(s) selected");
        GUILayout.EndArea();
        GUI.matrix = prev;
    }

    private void OnDestroy()
    {
        if (_open)
            Close(apply: false);
    }

    private static void EnsureTextures()
    {
        if (_panelBg != null)
            return;
        _panelBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _panelBg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.12f, 0.94f));
        _panelBg.Apply();
        _rowBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _rowBg.SetPixel(0, 0, new Color(0.2f, 0.35f, 0.55f, 0.85f));
        _rowBg.Apply();
    }

    private static bool WasApplyPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            return true;
        return false;
    }

    private static bool WasTogglePressed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
            return true;

        var gp = Gamepad.current;
        return gp != null && gp.buttonSouth.wasPressedThisFrame;
    }

    private static bool WasCancelPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            return true;

        var gp = Gamepad.current;
        return gp != null && gp.buttonEast.wasPressedThisFrame;
    }
}
