# TeaQoLs

Rift of the NecroDancer BepInEx 5 quality-of-life bag.

Plugin GUID: `rotn.dimethyltea.TeaQoLs` (config: `BepInEx/config/rotn.dimethyltea.TeaQoLs.cfg`).

## Features

### Random song

On the **official** or **Custom Music** title list, press the random key (default **J** — not R; R is remix):

1. If every folder is closed, opens them all so the pool is the full viewable list (skips promo / tutorial / locked / filler).
2. Scrolls to that track (speed scales with list size; hard brake to a dead stop on the pick).
3. Starts the stage with the difficulty you already had selected — skips the loadout screen.

## Settings

| Key | Default | Meaning |
|-----|---------|---------|
| **Enabled** | true | Master toggle. |
| **RandomKey** | J | Hotkey for random song (title list only). Do not use R (stock remix). |

Also available under NecroManager’s in-game Mods settings menu.

## Build from source

Requires a Rift install with **BepInEx 5**, **[Rift of the NecroManager](https://github.com/96-LB/RiftOfTheNecroManager)** in `BepInEx/plugins`, and the [.NET SDK](https://dotnet.microsoft.com/download) (`dotnet` on your PATH).

1. In `TeaQoLs.csproj`, set `$(GameManaged)` to your game’s `…/RiftOfTheNecroDancer_Data/Managed` folder (the default path is my Steam install). You can also pass it on the command line: `-p:GameManaged="D:\path\to\Managed"`.
2. Put a publicized `Assembly-CSharp` at `lib/RiftReadable.dll`.

   ```bash
   A:/Projects/rift-mods/scripts/regen_publicized_assembly.sh \
     /a/SteamLibrary/steamapps/common/RiftOfTheNecroDancerOSTVolume1/RiftOfTheNecroDancer_Data/Managed/Assembly-CSharp \
     A:/Projects/rift-mods/mods/TeaQoLs/lib
   ```

3. Point `$(GamePlugins)` at your `BepInEx/plugins` folder if needed (default is my Steam install) so the build can reference `RiftOfTheNecroManager.dll` (`Private=false` — not copied beside this mod).
4. Build and install:

```bash
dotnet build -c Release
# then copy bin/Release/netstandard2.1/TeaQoLs.dll → <game>/BepInEx/plugins/
```

Optional: `dotnet test TeaQoLs.Tests/TeaQoLs.Tests.csproj`

Until this GUID is listed for NecroManager Automatic version control, use Manual/Disabled VC or per-mod Override + Allowed Versions so the plugin is not deactivated.
