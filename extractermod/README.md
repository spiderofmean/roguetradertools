# ⚠️ DEPRECATED - Blueprint Dumper for Rogue Trader

**This mod has been superseded by [viewer-mod](../viewer-mod/) and is no longer maintained.**

## Migration

The viewer-mod provides all the functionality of this mod plus:
- Live HTTP API access to blueprints (no game restart needed)
- Web UI for browsing game state
- MCP server for AI agent integration
- On-demand icon extraction
- Same extraction script: `viewer-mod/scripts/extract-blueprints.js`

### To migrate:

1. **Remove this mod**: Run `.\scripts\undeploy.ps1` (see below)
2. **Install viewer-mod**: Follow [viewer-mod README](../viewer-mod/README.md)
3. **Extract blueprints**: Run `node viewer-mod/scripts/extract-blueprints.js`

---

## Uninstallation

Run the undeploy script:
```powershell
.\scripts\undeploy.ps1
```

This will:
1. Remove the mod DLL from the game's mods folder
2. List locations of blueprint dumps for manual cleanup

---

<details>
<summary>Original README (for reference)</summary>

A mod that exports item blueprints (weapons, armor, consumables, etc.) from Warhammer 40,000: Rogue Trader to JSON files.

See [INSTALL_AND_USAGE.md](INSTALL_AND_USAGE.md) for detailed instructions.

## Features

- Dumps **all** equipment blueprints, including future-act items
- Runs automatically at the main menu
- Press **F10** to trigger a manual dump
- Falls back to cache if database loading fails

## Quick Start

1. **Build & Deploy**:
   ```powershell
   .\scripts\pack.ps1 -Configuration Release
   ```

2. **Enable**: Launch the game, open Mod Manager (Ctrl+M), enable "Blueprint Dumper".

3. **Output**: Check `%LocalAppData%\Owlcat Games\Warhammer 40000 Rogue Trader\BlueprintDumps\`

## Configuration

Edit `Filter.cs` to change which blueprint types are exported.

## Troubleshooting

- **No output?** Check `run.log` in the dump folder or `Player.log` in the game's LocalLow directory.
- **Build fails?** Update `RogueTraderInstallDir` in `mod/BlueprintDumper.csproj`.

</details>
