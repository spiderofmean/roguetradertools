# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This project is building a website for Warhammer 40,000: Rogue Trader game data. The data has been extracted using the BlueprintDumper mod tool and contains comprehensive item, weapon, armor, and equipment information.

## Data Source

All game data is in `2026-01-18-185750-manual/`, extracted via BlueprintDumper mod (not raw binary parsing).

### Key Files

| File | Description |
|------|-------------|
| `index.json` | Master index of all 157,014 blueprints with GUID, name, type, namespace |
| `items_flat.jsonl` | Flat JSONL format of all items for easy processing |
| `item_type_histogram.json` | Distribution of item types (985 weapons, 157 armors, etc.) |
| `item_namespace_histogram.json` | Distribution by C# namespace |
| `database_report.json` | Extraction statistics |

### Directory Structure

```
2026-01-18-185750-manual/
├── Kingmaker/
│   ├── Blueprints/Items/
│   │   ├── Weapons/BlueprintItemWeapon/    # 985 weapons
│   │   ├── Armors/BlueprintItemArmor/      # 157 armors
│   │   ├── Equipment/                       # Rings, necks, gloves, etc.
│   │   ├── Shields/                         # 32 shields
│   │   └── ...
│   └── Visual/
└── Warhammer/
    └── SpaceCombat/Blueprints/             # Starship weapons, void shields, etc.
```

Individual blueprints are stored as `.jbp` (JSON Blueprint) files with full property data.

### Item Categories

- **Ground Weapons**: BlueprintItemWeapon (985) - bolters, lasguns, melee weapons
- **Armor**: BlueprintItemArmor (157) - carapace, power armor, etc.
- **Equipment**: Rings (141), Necks (77), Gloves (72), Feet (67), Head (81), Shoulders (72)
- **Shields**: BlueprintItemShield (32)
- **Starship**: BlueprintStarshipWeapon (116), VoidShieldGenerator (43), PlasmaDrives (30)

### Blueprint Format (.jbp)

Each `.jbp` file contains:
- `$type`: Full C# type name
- `guid`: Unique identifier
- `name`: Display name
- `namespace`: C# namespace
- `data`: Full property data including stats, descriptions, abilities
