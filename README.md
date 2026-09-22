# QoLiTea

Quality-of-life bundle for [Rift of the NecroDancer](https://store.steampowered.com/app/2073250) (BepInEx 5).

Requires **[Rift of the NecroManager](https://github.com/96-LB/RiftOfTheNecroManager)**. Config lives in `BepInEx/config/rotn.dimethyltea.QoLiTea.cfg` and in NecroManager’s Mods menu.

## What’s in it

**Jukebox (J)** — On the official or Custom Music list, pick a random playable track, scroll to it, and start at your current difficulty. Optionally open the loadout instead of auto-playing (`RandomSongOpenLoadout`).

**Lazy Custom Tracks** — Custom Music opens from a disk cache and refreshes in the background, so big Workshop libraries don’t stall the menu. Workshop installs and local folder changes update the open list without a full rescan. Clear cache via NecroManager `RunClearTrackListCache` if something looks stuck.

**Workshop AutoScan** — Opening Custom Music scans recent Workshop publishes. New charts show in an overlay (keyboard/gamepad): select what to sub, **A** to auto-sub an author forever. Turn off `WorkshopAutoScanAutoOpen` to scan silently and open the stash with **N** (toasts when there’s nothing new).

**Skip Boot Intro** — After load, skip splash / forced calibration / intro / press-any-key and land on the real main menu.

**Field Opacity** — Fade lane tiles only (`0`–`100`). Enemies and strings stay stock. Off by default.

**Track sets (NecroManager)** — Cap Workshop subs (`RunBulkUnsubscriber`), drop non-favorites without Impossible (`RunUnsubNoImpossible`), or re-sub archived sets (`OpenSetSubscriber`). Removals are archived so you can restore them.

**Results divergence plot** — Timing scatter on the results screen (center = perfect; early/late marked). Gold bands for vibe windows; misses and overhits as vertical marks. **G** toggles; double-tap **G** for fullscreen. Doesn’t replace the stock histogram.

**Practice worst sections** — Results **Auto** option that chains stock practice across your worst spans (off by default).

## Hotkeys

| Where | Key | Does |
|-------|-----|------|
| Title list | **J** | Random song |
| Custom Music | **N** | Open stashed Workshop AutoScan list |
| Results | **G** | Toggle plot (double-tap = fullscreen) |

## Settings cheat sheet

Most toggles are on by default. Notable defaults / one-shots:

| Setting | Default | Notes |
|---------|---------|--------|
| `Enabled` | on | Master kill switch |
| `RandomSongOpenLoadout` | off | Land on loadout instead of auto-start |
| `WorkshopAutoScanAutoOpen` | on | Off → use **N** after scan |
| `FieldOpacityEnabled` | off | Then set `FieldOpacity` percent |
| `WorstSectionPracticeEnabled` | off | Adds **Auto** on results |
| `MaxSubscribedTracks` | 1000 | Bulk Unsubscriber cap |
| `RunClearTrackListCache` / `RunBulkUnsubscriber` / `RunUnsubNoImpossible` / `OpenSetSubscriber` | off | NecroManager one-shots (flip once) |

## Build from source

Needs a Rift install with BepInEx 5, NecroManager in `BepInEx/plugins`, and the .NET SDK.

1. Point `GameManaged` in `QoLiTea.csproj` (or `-p:GameManaged=…`) at your game’s `Managed` folder.
2. Put a publicized `Assembly-CSharp` at `lib/RiftReadable.dll`.
3. Point `GamePlugins` at `BepInEx/plugins` so the build can reference NecroManager.
4. `dotnet build -c Release` → copy `bin/Release/netstandard2.1/QoLiTea.dll` into plugins.

Optional: `dotnet test QoLiTea.Tests/QoLiTea.Tests.csproj`
