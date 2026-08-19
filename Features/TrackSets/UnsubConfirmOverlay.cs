using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace QoLiTea.Features.TrackSets;

/// <summary>Confirm / Cancel preview for a pending unsub batch.</summary>
public sealed class UnsubConfirmOverlay : MonoBehaviour
{
    private string _title = string.Empty;
    private string _body = string.Empty;
    private string _setName = string.Empty;
    private List<ulong> _fileIds = new List<ulong>();
    private Action<bool> _onDone;
    private bool _open;
    private bool _finished;
    private static Texture2D _panelBg;

    private const float PanelWidth = 520f;
    private const float PanelHeight = 220f;
    private const float UiScale = 3f;

    public bool IsOpen => _open;

    public void Open(
        string title,
        string body,
        string setName,
        List<ulong> fileIds,
        Action<bool> onDone)
    {
        EnsureTextures();
        _title = title ?? string.Empty;
        _body = body ?? string.Empty;
        _setName = setName ?? string.Empty;
        _fileIds = fileIds ?? new List<ulong>();
        _onDone = onDone;
        _finished = false;
        _open = true;
    }

    public void Close(bool confirmed)
    {
        if (!_open || _finished)
            return;

        _finished = true;
        _open = false;
        var cb = _onDone;
        _onDone = null;
        cb?.Invoke(confirmed);
    }

    private void Update()
    {
        if (!_open)
            return;

        if (WasCancelPressed())
        {
            Close(false);
            return;
        }

        if (WasConfirmPressed())
            Close(true);
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
        var rect = new Rect(x, y, w, h);

        GUI.DrawTexture(rect, _panelBg);
        GUILayout.BeginArea(new Rect(x + 16f, y + 12f, w - 32f, h - 24f));
        GUILayout.Label(_title);
        GUILayout.Space(8f);
        GUILayout.Label(_body);
        if (!string.IsNullOrEmpty(_setName))
            GUILayout.Label("Set: " + _setName);
        GUILayout.Label($"Tracks: {_fileIds.Count}");
        GUILayout.FlexibleSpace();
        GUILayout.Label("Confirm = run · Cancel = abort");
        GUILayout.EndArea();

        GUI.matrix = prev;
    }

    private void OnDestroy()
    {
        if (_open && !_finished)
            Close(false);
    }

    private static void EnsureTextures()
    {
        if (_panelBg != null)
            return;
        _panelBg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _panelBg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.12f, 0.94f));
        _panelBg.Apply();
    }

    private static bool WasConfirmPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame ||
                           kb.spaceKey.wasPressedThisFrame))
            return true;

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame)
                return true;
        }

        return false;
    }

    private static bool WasCancelPressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame))
            return true;

        var gp = Gamepad.current;
        if (gp != null && gp.buttonEast.wasPressedThisFrame)
            return true;

        return false;
    }
}
