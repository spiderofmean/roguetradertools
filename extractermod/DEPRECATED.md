# DEPRECATED

**This mod has been superseded by [viewer-mod](../viewer-mod/) and is no longer maintained.**

## Why Deprecated?

The viewer-mod provides all functionality of this mod plus significant enhancements:

### extractermod provided:
- One-time blueprint dump to JSON files (`.jbp` format)
- Manual trigger via F10 hotkey
- Icon extraction to separate files
- Equipment filtering
- Output to `%LocalAppData%\...\BlueprintDumps\`

### viewer-mod provides:
- ✅ **All of the above** via `scripts/extract-blueprints.js`
- ✅ Live HTTP API access (no game restart needed)
- ✅ Real-time blueprint browsing via web UI
- ✅ MCP server for AI agent integration
- ✅ On-demand icon retrieval
- ✅ NDJSON streaming format (more efficient)
- ✅ Live game state inspection

## Migration Path

1. **Undeploy this mod**:
   ```powershell
   cd extractermod
   .\scripts\undeploy.ps1
   ```

2. **Install viewer-mod**:
   ```powershell
   cd ../viewer-mod
   # Follow viewer-mod/README.md for setup
   .\scripts\build.ps1
   .\scripts\deploy.ps1
   ```

3. **Extract blueprints** (same as before):
   ```powershell
   # Start the game and load a save
   node scripts/extract-blueprints.js
   ```
   Output: `viewer-mod/blueprint-dump/equipment.jsonl`

## Functionality Comparison

| Feature | extractermod | viewer-mod |
|---------|-------------|------------|
| Blueprint extraction | ✅ Manual dump | ✅ Live API + script |
| Equipment filtering | ✅ | ✅ |
| Icon extraction | ✅ Files | ✅ HTTP endpoint |
| Format | `.jbp` (one per item) | NDJSON (streaming) |
| Trigger | F10 or auto | HTTP API (always on) |
| Live access | ❌ | ✅ |
| Web UI | ❌ | ✅ |
| MCP integration | ❌ | ✅ |

## No Functional Gaps

The viewer-mod's `BlueprintService.cs` uses the **same filtering logic** as extractermod's `Filter.cs`:
- Same equipment keywords: Item, Weapon, Armor, etc.
- Same namespace detection: `.Items` with `Blueprint` prefix
- Same dumping depth: 3 levels (configurable)

The extraction script `extract-blueprints.js`:
- Fetches all blueprint GUIDs in batches
- Filters to equipment types
- Downloads icons via HTTP
- Outputs to NDJSON format (easier to process than individual `.jbp` files)

## Timeline

- **Created**: January 2026
- **Deprecated**: January 25, 2026 (superseded by viewer-mod)
- **Status**: No longer maintained, undeploy recommended

## Questions?

See [viewer-mod documentation](../viewer-mod/README.md) or [viewer-mod spec](../viewer-mod/spec/README.md).
