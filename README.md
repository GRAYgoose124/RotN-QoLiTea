# QoLiTea

Rift of the NecroDancer BepInEx 5 quality-of-life bundle.

Plugin GUID: `rotn.dimethyltea.QoLiTea` (config: `BepInEx/config/rotn.dimethyltea.QoLiTea.cfg`).

## Features

### Random song

On the **official** or **Custom Music** title list, press the random key (default **J**):

1. If every folder is closed, opens them all so the pool is the full viewable list (skips promo / tutorial / locked / filler).
2. Scrolls to that track (speed scales with list size; hard brake to a dead stop on the pick).
3. Starts the stage with the difficulty you already had selected - skips the loadout screen.

### Lazy Custom Tracks

Speeds up the Custom Music menu with a persistent disk cache and background refresh. (For when loading takes a while and you have many workshop items.)

1. Restores the last display list across restarts, including per-difficulty intensity/BPM so stock list folders do not dump tracks into Unknowns.
2. Serves cache immediately on open; reconciles in the background; writes disk only if the result diverges. Old caches missing folder fields get one full reconcile.
3. Workshop install/unsub and local folder add/remove update the open list without a full rescan. Warm-open also re-reads local `info.json` so new difficulties appear in Easy/Medium folders.
4. Select/submit sync-hydrates from disk (not only cold stubs), so local `info.json` difficulty edits show after reopening the track — no full restart needed.

**Clear cache:** NecroManager `RunClearTrackListCache` (one-shot) deletes `track-list-cache.json` and kicks a full reconcile if Custom Music is already open.

Cache file: `BepInEx/config/rotn.dimethyltea.QoLiTea/track-list-cache.json`.

### Workshop AutoScan

On **Custom Music** open, checks recent Workshop publishes (community most-recent).

1. Page 1 (30). If nothing new after seen/subscribed filter, digs pages 2–3 (stops when uniques appear; max 90).
2. Authors you marked with **A** are silent-subscribed and skipped in the overlay.
3. Overlay lists the rest (title + author). **Up/Down** move | **Confirm** toggles select | **A** auto-sub that author (all their rows in this list + future scans) | **Cancel** closes and subscribes the selected set. Keyboard/gamepad only (no mouse).
4. Focused row: author + thumbnail on the right, workshop description underneath (lazy fetch). Focusing marks seen. Already-subscribed items are skipped. Lazy Custom Tracks picks up installs via live deltas.

Seen: `BepInEx/config/rotn.dimethyltea.QoLiTea/workshop-autoscan-seen.json`  
Auto-sub authors: `BepInEx/config/rotn.dimethyltea.QoLiTea/workshop-autoscan-authors.json`.

### Skip Boot Intro

On game start, after startup loading finishes, jumps past splash logos/video, forced boot calibration, intro cinematic, and the press-any-key title screen - straight to the real main menu. Splash still covers load time if the game is still loading.

### Field Opacity

Lane/field **tiles** only: set opacity `0`–`100` (`100` = stock opaque, `0` = invisible). Enemies, arrows, and guitar strings stay stock. Change takes effect on the next stock tile alpha pass (usually stage fade-in / column changes).

### Bulk Unsubscriber / Set Subscriber

NecroManager one-shots under **TrackSets**.

**Bulk Unsubscriber** (`RunBulkUnsubscriber`): trims workshop subs to `MaxSubscribedTracks` (default **1000**). Drops least-recently-played first, then fewest plays. Never unsubs favorites or 0-play tracks. Preview Confirm/Cancel; always archives removals into a set (`unsub-yyyy-MM-dd-HHmm`).

**Unsub No-Impossible** (`RunUnsubNoImpossible`): unsubs non-favorite tracks that lack an Impossible chart (from Lazy Custom Tracks cache / metadata). Skips favorites and unknown difficulty data. Archives to `unsub-noimp-…`.

**Set Subscriber** (`OpenSetSubscriber`): multi-select saved sets; Enter subscribes the union (additive only). Sets file: `BepInEx/config/rotn.dimethyltea.QoLiTea/track-sets.json`. Updates the Lazy Custom Tracks cache from Steam after unsub/subscribe.

### Results divergence plot

On the **results screen**, draws a scatter plot of signed timing divergence from on-beat perfect (center line = perfect; early/late above/below), using stock rating-percent language. Shaded bands mark detected worst sections. Full width along the bottom of the results canvas (same height band as before). Default **80%** opacity (**20%** transparent); tune via `PlotOpacity`. Press **G** (configurable) on the results screen to toggle the plot on/off without leaving results. Does not replace the stock results histogram.

### Practice worst sections

When worst sections are found and stock Practice is available, adds an **Auto** results menu option (registered in the scrollable option list, after Retry). One stock practice window from the **start of the first** worst section through the **end of the last**. At each section boundary, mid-run **FMOD seek + chart skip** with 8-beat warm-up prep and enemy clear (no full scene reload between sections). After the last section, stock `CompleteStage` runs normally.

## Settings

| Key | Default | Meaning |
|-----|---------|---------|
| **Enabled** | true | Master toggle - all features off when false. |
| **RandomSongEnabled** | true | Jukebox random song. |
| **RandomKey** | J | Hotkey for random song (title list only). |
| **LazyCustomTracksEnabled** | true | Custom Music lazy load / cache. |
| **RunClearTrackListCache** | false | One-shot: clear track list cache. |
| **WorkshopAutoScanEnabled** | true | Custom Music: new Workshop publish overlay. |
| **SkipBootIntroEnabled** | true | Skip splash media / forced boot calib / intro cinematic / title screen. |
| **FieldOpacityEnabled** | false | Scale lane/field tile opacity. |
| **FieldOpacity** | 100 | Tile opacity percent (`0` = invisible, `100` = stock). |
| **BulkUnsubscriberEnabled** | true | Cap-trim + No-Impossible unsub tools. |
| **MaxSubscribedTracks** | 1000 | Bulk Unsubscriber subscription cap. |
| **RunBulkUnsubscriber** | false | One-shot: open Bulk Unsubscriber confirm. |
| **RunUnsubNoImpossible** | false | One-shot: unsub tracks without Impossible. |
| **SetSubscriberEnabled** | true | Set Subscriber picker. |
| **OpenSetSubscriber** | false | One-shot: open Set Subscriber. |
| **ResultsDivergencePlotEnabled** | true | Results scatter plot of timing divergence. |
| **ToggleKey** | G | Results screen: toggle plot visibility. |
| **PlotOpacity** | 90 | Plot opacity percent (`80` = 20% transparent). |
| **WorstSectionPracticeEnabled** | true | Detect spans + Auto button / jumper. |

Also available under NecroManager’s in-game Mods settings menu.

## Build from source

Requires a Rift install with **BepInEx 5**, **[Rift of the NecroManager](https://github.com/96-LB/RiftOfTheNecroManager)** in `BepInEx/plugins`, and the [.NET SDK](https://dotnet.microsoft.com/download) (`dotnet` on your PATH).

1. In `QoLiTea.csproj`, set `$(GameManaged)` to your game’s `…/RiftOfTheNecroDancer_Data/Managed` folder (the default path is my Steam install). You can also pass it on the command line: `-p:GameManaged="D:\path\to\Managed"`.
2. Put a publicized `Assembly-CSharp` at `lib/RiftReadable.dll`.

   ```bash
   /a/Projects/rift-mods/scripts/regen_publicized_assembly.sh \
     /a/SteamLibrary/steamapps/common/RiftOfTheNecroDancerOSTVolume1/RiftOfTheNecroDancer_Data/Managed/Assembly-CSharp \
     /a/Projects/rift-mods/mods/QoLiTea/lib
   ```

3. Point `$(GamePlugins)` at your `BepInEx/plugins` folder if needed so the build can reference `RiftOfTheNecroManager.dll` (`Private=false` - not copied beside this mod).
4. Build and install:

```bash
dotnet build -c Release
# then copy bin/Release/netstandard2.1/QoLiTea.dll ; <game>/BepInEx/plugins/
```

Optional: `dotnet test QoLiTea.Tests/QoLiTea.Tests.csproj`