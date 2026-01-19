# Blueprint Dumper  Install & Usage

Exports item blueprints (weapons, armor, consumables, etc.) from Rogue Trader to JSON files.

## What It Does

- Runs automatically when the main menu loads
- Force-loads all blueprints from the game database (not just cached ones)
- Falls back to cache if database loading fails
- Press **F10** anytime to trigger a manual dump

### Output

- `.jbp` files  one JSON file per blueprint
- `index.json`  list of all exported blueprints with paths
- `run.log`  progress and errors

## Requirements

- Warhammer 40,000: Rogue Trader with mod manager enabled
- For building: Windows, .NET SDK

## Installation

Build and deploy:

```powershell
.\scripts\pack.ps1 -Configuration Release
```

This copies the DLL and manifest to the game's mod folder.

## Enable the Mod

1. Launch Rogue Trader
2. Open Mod Manager (Ctrl+M)
3. Enable **Blueprint Dumper**
4. Restart if prompted

## Usage

1. Start the game and reach the main menu
2. The mod dumps automatically after a few seconds
3. Press **F10** for a manual dump anytime
4. Find output at:
   ```
   %LocalAppData%\Owlcat Games\Warhammer 40000 Rogue Trader\BlueprintDumps\<timestamp>\
   ```

### Output Structure

```
<timestamp>/
  run.log
  index.json
  Kingmaker/Blueprints/Items/Weapons/
    Some_Weapon_<guid>.jbp
  ...
```

## Configuration

Edit `mod/src/Filter.cs` to change which blueprints are exported, then rebuild:

```powershell
.\scripts\pack.ps1 -Configuration Release
```

## Troubleshooting

### No dump folder

- Confirm the mod is enabled in Mod Manager
- Check `Player.log` for "BlueprintDumper" messages

### Empty or incomplete dump

- Check `run.log` for errors
- Try pressing F10 after loading a save

### Build fails

Update `RogueTraderInstallDir` in `mod/BlueprintDumper.csproj` to your game install path:

```xml
<RogueTraderInstallDir>C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40,000 Rogue Trader</RogueTraderInstallDir>
```

### Mod not in Mod Manager

Search `GameLogFull.txt` for `Mods path:` to find where the game expects mods.

## Uninstall

1. Disable in Mod Manager
2. Delete `%LocalAppData%\Owlcat Games\Warhammer 40000 Rogue Trader\UnityModManager\BlueprintDumper\`

Dump outputs are stored separately in `BlueprintDumps\`.

## Notes

- Read-only: does not modify saves
- May briefly affect performance during dump
