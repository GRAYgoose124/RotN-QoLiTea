using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RiftOfTheNecroManager;
using Shared.TrackSelection;
using TeaQoLs.Features.RandomSong;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeaQoLs;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : RiftPlugin
{
    internal static new ManualLogSource Logger;
    internal static Plugin Instance;

    internal static readonly Setting<bool> Enabled = new(
        "General",
        "Enabled",
        true,
        "Master toggle for TeaQoLs features.");

    // Not R (remix CycleMode) or F6 (other mods).
    internal static readonly Setting<KeyCode> RandomSongKey = new(
        "RandomSong",
        "RandomKey",
        KeyCode.J,
        "On the official or custom title list: jukebox-scroll to a random playable song and start (skip loadout).");

    private RandomSongDriver _randomSong;
    private bool _ready;
    private bool _keyHeld;
    private float _nextMissLogTime;

    protected override void OnInit()
    {
        Instance = this;
        Logger = base.Logger;
        _randomSong = new RandomSongDriver(this);
        _ready = true;

        var update = AccessTools.Method(typeof(TrackSelectionSceneController), nameof(TrackSelectionSceneController.Update));
        var patchInfo = update != null ? Harmony.GetPatchInfo(update) : null;
        var postfixCount = patchInfo?.Postfixes?.Count ?? 0;
        Logger.LogInfo(
            $"{MyPluginInfo.PLUGIN_GUID} ready — press {RandomSongKey.Entry.Value} on title list (TrackSelection.Update postfixes={postfixCount})");

        base.OnInit();
    }

    protected override void OnUnload()
    {
        _ready = false;
        _randomSong?.Cancel();
        _randomSong = null;
        Instance = null;
    }

    /// <summary>
    /// Called from title-list Update postfixes (primary) and Plugin.Update (backup).
    /// </summary>
    internal void TryRandomSongFromTitleList()
    {
        if (!_ready || _randomSong == null || !Enabled)
            return;

        if (_randomSong.IsRunning)
            return;

        KeyCode keyCode = RandomSongKey;
        var down = IsKeyDown(keyCode);
        var pressed = down && !_keyHeld;
        _keyHeld = down;
        if (!pressed)
            return;

        Logger.LogInfo($"TeaQoLs: {keyCode} edge detected");

        if (TrackListGate.TryGetCustom(out var custom))
        {
            if (custom.InputDisabled)
            {
                Logger.LogWarning("TeaQoLs: custom list InputDisabled; ignoring.");
                return;
            }

            if (!_randomSong.TryStartCustom(custom))
                Logger.LogWarning("TeaQoLs: custom random could not start.");
            return;
        }

        if (TrackListGate.TryGetOfficial(out var official))
        {
            if (official.InputDisabled)
            {
                Logger.LogWarning("TeaQoLs: official list InputDisabled; ignoring.");
                return;
            }

            if (!_randomSong.TryStartOfficial(official))
                Logger.LogWarning("TeaQoLs: official random could not start.");
            return;
        }

        if (Time.unscaledTime >= _nextMissLogTime)
        {
            _nextMissLogTime = Time.unscaledTime + 2f;
            Logger.LogWarning("TeaQoLs: J ignored (no active title list).");
        }
    }

    private void Update()
    {
        // Backup path if Harmony title-list patches miss.
        TryRandomSongFromTitleList();
    }

    private static bool IsKeyDown(KeyCode keyCode)
    {
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Explicit letter keys — enum arithmetic has been flaky across InputSystem versions.
            if (keyCode == KeyCode.J && keyboard.jKey.isPressed)
                return true;

            if (TryToInputKey(keyCode, out var key))
            {
                var control = keyboard[key];
                if (control != null && control.isPressed)
                    return true;
            }
        }

        try
        {
            if (Input.GetKey(keyCode))
                return true;
        }
        catch
        {
            // Input Manager disabled.
        }

        var legacy = UnityInput.Current;
        return legacy != null && legacy.GetKey(keyCode);
    }

    private static bool TryToInputKey(KeyCode keyCode, out Key key)
    {
        if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
        {
            key = Key.A + (keyCode - KeyCode.A);
            return true;
        }

        if (keyCode >= KeyCode.F1 && keyCode <= KeyCode.F12)
        {
            key = Key.F1 + (keyCode - KeyCode.F1);
            return true;
        }

        if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
        {
            key = keyCode == KeyCode.Alpha0
                ? Key.Digit0
                : Key.Digit1 + (keyCode - KeyCode.Alpha1);
            return true;
        }

        key = keyCode switch
        {
            KeyCode.Space => Key.Space,
            KeyCode.Return => Key.Enter,
            KeyCode.KeypadEnter => Key.NumpadEnter,
            KeyCode.Escape => Key.Escape,
            KeyCode.Tab => Key.Tab,
            KeyCode.BackQuote => Key.Backquote,
            KeyCode.Minus => Key.Minus,
            KeyCode.Equals => Key.Equals,
            KeyCode.LeftBracket => Key.LeftBracket,
            KeyCode.RightBracket => Key.RightBracket,
            KeyCode.Backslash => Key.Backslash,
            KeyCode.Semicolon => Key.Semicolon,
            KeyCode.Quote => Key.Quote,
            KeyCode.Comma => Key.Comma,
            KeyCode.Period => Key.Period,
            KeyCode.Slash => Key.Slash,
            _ => Key.None,
        };
        return key != Key.None;
    }
}
