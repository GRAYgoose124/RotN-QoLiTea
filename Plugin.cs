using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RiftOfTheNecroManager;
using Shared.TrackSelection;
using QoLiTea.Features.FieldOpacity;
using QoLiTea.Features.LazyCustomTracks;
using QoLiTea.Features.RandomSong;
using QoLiTea.Features.TrackSets;
using QoLiTea.Features.WorkshopAutoScan;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QoLiTea;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : RiftPlugin
{
    internal static new ManualLogSource Logger;
    internal static Plugin Instance;

    internal static readonly Setting<bool> Enabled = new(
        "General",
        "Enabled",
        true,
        "Master toggle — all QoLiTea features off when false.");

    internal static readonly Setting<bool> RandomSongEnabled = new(
        "RandomSong",
        "RandomSongEnabled",
        true,
        "Jukebox random song on the official/custom title list.");

    // Not R (remix CycleMode) or F6 (other mods).
    internal static readonly Setting<KeyCode> RandomSongKey = new(
        "RandomSong",
        "RandomKey",
        KeyCode.J,
        "Title list: jukebox-scroll to a random playable song and start (skip loadout).");

    internal static readonly Setting<bool> LazyCustomTracksEnabled = new(
        "LazyCustomTracks",
        "LazyCustomTracksEnabled",
        true,
        "Custom Music: disk cache + background reconcile + select hydrate (was LazyCustomTracks mod).");

    internal static readonly Setting<bool> RunClearTrackListCache = new(
        "LazyCustomTracks",
        "RunClearTrackListCache",
        false,
        "One-shot: delete track-list-cache.json; reconcile now if Custom Music is open.");

    internal static readonly Setting<bool> WorkshopAutoScanEnabled = new(
        "WorkshopAutoScan",
        "WorkshopAutoScanEnabled",
        true,
        "Custom Music: show new Workshop publishes since last visit; toggle to subscribe.");

    internal static readonly Setting<bool> SkipBootIntroEnabled = new(
        "SkipBootIntro",
        "SkipBootIntroEnabled",
        true,
        "Skip splash media, forced boot calibration, intro cinematic, and title screen — real main menu after load.");

    internal static readonly Setting<bool> FieldOpacityEnabled = new(
        "FieldOpacity",
        "FieldOpacityEnabled",
        true,
        "Scale lane/field tile opacity (0–100). Tiles only — not enemies or strings.");

    // NecroManager Setting<int> does not bind — use string, parse in code.
    internal static readonly Setting<string> FieldOpacity = new(
        "FieldOpacity",
        "FieldOpacity",
        "100",
        "Tile opacity percent (0 = invisible, 100 = stock). Takes effect on next stock tile alpha pass.");

    internal static readonly Setting<bool> BulkUnsubscriberEnabled = new(
        "TrackSets",
        "BulkUnsubscriberEnabled",
        true,
        "Cap-trim and No-Impossible unsub tools (NecroManager one-shots).");

    // NecroManager Setting<int> does not bind — use string, parse in code.
    internal static readonly Setting<string> MaxSubscribedTracks = new(
        "TrackSets",
        "MaxSubscribedTracks",
        "1000",
        "Bulk Unsubscriber keeps at most this many workshop subscriptions.");

    internal static readonly Setting<bool> RunBulkUnsubscriber = new(
        "TrackSets",
        "RunBulkUnsubscriber",
        false,
        "One-shot: open Bulk Unsubscriber confirm (then resets to false).");

    internal static readonly Setting<bool> RunUnsubNoImpossible = new(
        "TrackSets",
        "RunUnsubNoImpossible",
        false,
        "One-shot: unsub non-favorite tracks lacking Impossible (confirm; resets).");

    internal static readonly Setting<bool> SetSubscriberEnabled = new(
        "TrackSets",
        "SetSubscriberEnabled",
        true,
        "Set Subscriber: re-subscribe saved unsub sets (additive).");

    internal static readonly Setting<bool> OpenSetSubscriber = new(
        "TrackSets",
        "OpenSetSubscriber",
        false,
        "One-shot: open Set Subscriber picker (then resets to false).");

    /// <summary>Master + feature gate for Custom Music lazy load.</summary>
    internal static bool IsLazyCustomTracksActive => Enabled && LazyCustomTracksEnabled;

    /// <summary>Master + feature gate for random song.</summary>
    internal static bool IsRandomSongActive => Enabled && RandomSongEnabled;

    /// <summary>Master + feature gate for Workshop autoscan overlay.</summary>
    internal static bool IsWorkshopAutoScanActive => Enabled && WorkshopAutoScanEnabled;

    /// <summary>Master + feature gate for boot splash/intro skip.</summary>
    internal static bool IsSkipBootIntroActive => Enabled && SkipBootIntroEnabled;

    /// <summary>Master + field tile opacity.</summary>
    internal static bool IsFieldOpacityActive => Enabled && FieldOpacityEnabled;

    internal static int FieldOpacityPercent => FieldOpacityPolicy.ParsePercent(FieldOpacity.Entry.Value);

    /// <summary>Master + Bulk Unsubscriber / No-Impossible tools.</summary>
    internal static bool IsBulkUnsubscriberActive => Enabled && BulkUnsubscriberEnabled;

    /// <summary>Master + Set Subscriber.</summary>
    internal static bool IsSetSubscriberActive => Enabled && SetSubscriberEnabled;

    internal static int MaxSubscribedTracksValue
    {
        get
        {
            if (!int.TryParse(MaxSubscribedTracks.Entry.Value, out int n) || n < 0)
                return 1000;
            return n;
        }
    }

    private RandomSongDriver _randomSong;
    private bool _ready;
    private bool _keyHeld;
    private float _nextMissLogTime;

    protected override void OnInit()
    {
        Instance = this;
        Logger = base.Logger;
        _randomSong = new RandomSongDriver(this);

        TrackListCache.LoadFromDisk();
        LiveDeltaService.ResetBaselineFromCache();
        WorkshopSeenStore.LoadFromDisk();
        TrackSetStore.LoadFromDisk();

        base.OnInit();
        _ready = true;

        var update = AccessTools.Method(typeof(TrackSelectionSceneController), nameof(TrackSelectionSceneController.Update));
        var patchInfo = update != null ? Harmony.GetPatchInfo(update) : null;
        var postfixCount = patchInfo?.Postfixes?.Count ?? 0;
        Logger.LogInfo(
            $"{MyPluginInfo.PLUGIN_GUID} ready — RandomSong={RandomSongEnabled.Entry.Value} key={RandomSongKey.Entry.Value}; LazyCustomTracks={LazyCustomTracksEnabled.Entry.Value}; WorkshopAutoScan={WorkshopAutoScanEnabled.Entry.Value}; SkipBootIntro={SkipBootIntroEnabled.Entry.Value}; FieldOpacity={FieldOpacityEnabled.Entry.Value}/{FieldOpacityPercent}; TrackSets cap={MaxSubscribedTracksValue} (TrackSelection.Update postfixes={postfixCount})");
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
        if (!_ready || _randomSong == null || !IsRandomSongActive)
            return;

        if (_randomSong.IsRunning)
            return;

        KeyCode keyCode = RandomSongKey;
        var down = IsKeyDown(keyCode);
        var pressed = down && !_keyHeld;
        _keyHeld = down;
        if (!pressed)
            return;

        Logger.LogInfo($"QoLiTea: {keyCode} edge detected");

        if (TrackListGate.TryGetCustom(out var custom))
        {
            if (custom.InputDisabled)
            {
                Logger.LogWarning("QoLiTea: custom list InputDisabled; ignoring.");
                return;
            }

            if (!_randomSong.TryStartCustom(custom))
                Logger.LogWarning("QoLiTea: custom random could not start.");
            return;
        }

        if (TrackListGate.TryGetOfficial(out var official))
        {
            if (official.InputDisabled)
            {
                Logger.LogWarning("QoLiTea: official list InputDisabled; ignoring.");
                return;
            }

            if (!_randomSong.TryStartOfficial(official))
                Logger.LogWarning("QoLiTea: official random could not start.");
            return;
        }

        if (Time.unscaledTime >= _nextMissLogTime)
        {
            _nextMissLogTime = Time.unscaledTime + 2f;
            Logger.LogWarning("QoLiTea: J ignored (no active title list).");
        }
    }

    private void Update()
    {
        // Backup path if Harmony title-list patches miss.
        TryRandomSongFromTitleList();
        if (_ready)
        {
            TrackSetsOneShot.Tick();
            LazyCustomTracksOneShot.Tick();
        }
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
