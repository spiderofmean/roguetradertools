# Blueprint Dumper for Rogue Trader

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
