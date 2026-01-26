#!/usr/bin/env python3
"""Static site generator v2 - Single JSON file with compression."""

import argparse
import json
import gzip
from pathlib import Path
from collections import defaultdict


def get_latest_extraction(extractions_dir: Path) -> Path:
    """Find the most recent extraction directory by sorting folder names."""
    if not extractions_dir.exists():
        raise FileNotFoundError(f"Extractions directory not found: {extractions_dir}")

    # Get all directories, sort by name (timestamps sort correctly alphabetically)
    dirs = sorted([d for d in extractions_dir.iterdir() if d.is_dir()], reverse=True)

    if not dirs:
        raise FileNotFoundError(f"No extraction directories found in: {extractions_dir}")

    return dirs[0]


def parse_args():
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="Generate static site from extraction data")
    parser.add_argument(
        "--extraction-dir",
        type=Path,
        help="Path to extraction directory. If not specified, uses latest in ../extractions/"
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("website"),
        help="Output directory for generated files (default: website)"
    )
    return parser.parse_args()


# These will be set in main() based on arguments
DATA_DIR = None
OUTPUT_DIR = Path("website")

# Category configuration with paths
CATEGORIES = {
    "all": {"title": "All Items", "path": None, "icon": "📦"},
    "weapons": {
        "title": "Weapons",
        "path": "Kingmaker/Blueprints/Items/Weapons/BlueprintItemWeapon",
        "icon": "⚔️",
    },
    "armor": {
        "title": "Armor",
        "path": "Kingmaker/Blueprints/Items/Armors/BlueprintItemArmor",
        "icon": "🛡️",
    },
    "shields": {
        "title": "Shields",
        "path": "Kingmaker/Blueprints/Items/Shields/BlueprintItemShield",
        "icon": "🔰",
    },
    "helmets": {
        "title": "Helmets",
        "path": "Kingmaker/Blueprints/Items/Equipment/BlueprintItemEquipmentHead",
        "icon": "🪖",
    },
    "gloves": {
        "title": "Gloves",
        "path": "Kingmaker/Blueprints/Items/Equipment/BlueprintItemEquipmentGloves",
        "icon": "🧤",
    },
    "boots": {
        "title": "Boots",
        "path": "Kingmaker/Blueprints/Items/Equipment/BlueprintItemEquipmentFeet",
        "icon": "👢",
    },
    "cloaks": {
        "title": "Cloaks",
        "path": "Kingmaker/Blueprints/Items/Equipment/BlueprintItemEquipmentShoulders",
        "icon": "🧥",
    },
    "rings": {
        "title": "Rings",
        "path": "Kingmaker/Blueprints/Items/Equipment/BlueprintItemEquipmentRing",
        "icon": "💍",
    },
    "amulets": {
        "title": "Amulets",
        "path": "Kingmaker/Blueprints/Items/Equipment/BlueprintItemEquipmentNeck",
        "icon": "📿",
    },
    "usables": {
        "title": "Consumables",
        "path": "Kingmaker/Blueprints/Items/Equipment/BlueprintItemEquipmentUsable",
        "icon": "🧪",
    },
    "starship-weapons": {
        "title": "Starship Weapons",
        "path": "Warhammer/SpaceCombat/Blueprints/BlueprintStarshipWeapon",
        "icon": "🚀",
    },
    "void-shields": {
        "title": "Void Shields",
        "path": "Warhammer/SpaceCombat/Blueprints/BlueprintItemVoidShieldGenerator",
        "icon": "🛸",
    },
    "plasma-drives": {
        "title": "Plasma Drives",
        "path": "Warhammer/SpaceCombat/Blueprints/BlueprintItemPlasmaDrives",
        "icon": "⚡",
    },
    "auger-arrays": {
        "title": "Auger Arrays",
        "path": "Warhammer/SpaceCombat/Blueprints/BlueprintItemAugerArray",
        "icon": "📡",
    },
}


def load_items_from_path(category_path: str) -> list:
    """Load all items from a category directory."""
    items = []
    full_path = DATA_DIR / category_path
    if not full_path.exists():
        print(f"  Warning: Path does not exist: {full_path}")
        return items

    for jbp_file in full_path.glob("*.jbp"):
        try:
            with open(jbp_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
                items.append(data)
        except Exception as e:
            print(f"  Error loading {jbp_file}: {e}")

    return items


def get_item_name(item: dict) -> str:
    """Get the display name for an item, trying multiple sources."""
    data = item.get("data", {})

    # Try data.Name first (capitalized, usually the display name)
    name = data.get("Name", "")
    if name:
        return name

    # Try top-level name
    name = item.get("name", "")
    if name:
        return name

    # Fall back to data.name (internal code name)
    name = data.get("name", "")
    if name:
        # Clean up internal names like "Bolter_VoidCrypt_Cutscene" -> "Bolter VoidCrypt Cutscene"
        return name.replace("_", " ")

    return ""


def is_valid_item(item: dict, extracted: dict) -> bool:
    """Check if an item should be included (not a template/prototype)."""
    # Must have a name
    if not extracted.get("name"):
        return False

    # For weapons, filter out 0-0 damage items (templates)
    if extracted.get("category") == "weapons":
        if extracted.get("damageMin", 0) == 0 and extracted.get("damageMax", 0) == 0:
            # Exception: some melee weapons might legitimately have 0 base damage
            if not extracted.get("isMelee"):
                return False

    return True


def clean_extracted_data(extracted: dict) -> dict:
    """Remove null/placeholder values from extracted data."""
    cleaned = {}
    for key, value in extracted.items():
        # Skip empty strings
        if value == "":
            continue
        # Skip -1 values (used for N/A, e.g., ammo on melee weapons)
        if value == -1:
            continue
        # Skip 0 for optional numeric fields (but keep for some fields like damageMin)
        if value == 0 and key in ("dodgePenetration", "rateOfFire", "recoil", "shieldStrength", "detectionRadius"):
            continue
        # Skip False for boolean fields
        if value is False and key in ("isRanged", "isMelee"):
            continue
        # Skip empty lists
        if isinstance(value, list) and len(value) == 0:
            continue
        cleaned[key] = value
    return cleaned


def extract_item_data(item: dict, category: str) -> dict:
    """Extract relevant fields from item for the JSON database."""
    data = item.get("data", {})

    # Common fields
    extracted = {
        "id": item.get("guid", ""),
        "name": get_item_name(item),
        "category": category,
        "type": item.get("$type", "").split(".")[-1],
        "rarity": data.get("Rarity", "Common"),
        "description": data.get("Description", "") or "",
        "flavorText": data.get("FlavorText", "") or "",
    }

    # Weapon-specific fields
    if "Weapon" in item.get("$type", "") and "Starship" not in item.get("$type", ""):
        extracted.update({
            "damageMin": data.get("WarhammerDamage", 0),
            "damageMax": data.get("WarhammerMaxDamage", data.get("WarhammerDamage", 0)),
            "penetration": data.get("WarhammerPenetration", 0),
            "dodgePenetration": data.get("DodgePenetration", 0),
            "range": data.get("WarhammerMaxDistance", data.get("AttackRange", 0)),
            "ammo": data.get("WarhammerMaxAmmo", 0),
            "rateOfFire": data.get("RateOfFire", 0),
            "recoil": data.get("WarhammerRecoil", 0),
            "family": data.get("Family", ""),
            "holdingType": data.get("HoldingType", ""),
            "isRanged": data.get("IsRanged", False),
            "isMelee": data.get("IsMelee", False),
            "damageType": data.get("m_DamageType", {}).get("Type", "") if isinstance(data.get("m_DamageType"), dict) else "",
        })

        # Extract weapon abilities
        abilities = []
        wa = data.get("WeaponAbilities", {})
        if isinstance(wa, dict) and "items" in wa:
            for ab in wa.get("items", []):
                if isinstance(ab, dict) and not ab.get("IsNone", True):
                    ab_type = ab.get("Type", "")
                    ap = ab.get("AP", 0)
                    if ab_type:
                        abilities.append({"type": ab_type, "ap": ap})
        extracted["abilities"] = abilities

    # Armor-specific fields
    elif "Armor" in item.get("$type", "") and "Plating" not in item.get("$type", ""):
        extracted.update({
            "damageAbsorption": data.get("DamageAbsorption", 0),
            "damageDeflection": data.get("DamageDeflection", 0),
            "armorCategory": data.get("Category", ""),
        })

    # Starship weapon fields
    elif "StarshipWeapon" in item.get("$type", ""):
        slots = data.get("AllowedSlots", {})
        slot_list = slots.get("items", []) if isinstance(slots, dict) else []
        extracted.update({
            "weaponType": data.get("WeaponType", ""),
            "damageInstances": data.get("DamageInstances", 1),
            "allowedSlots": slot_list,
        })

    # Void shield fields
    elif "VoidShield" in item.get("$type", ""):
        extracted.update({
            "shieldStrength": data.get("ShieldStrengthBonus", 0),
        })

    # Plasma drive fields
    elif "PlasmaDrives" in item.get("$type", ""):
        extracted.update({
            "speed": data.get("Speed", 0),
            "maneuverability": data.get("Maneuverability", 0),
        })

    # Auger array fields
    elif "AugerArray" in item.get("$type", ""):
        extracted.update({
            "detectionRadius": data.get("DetectionRadiusBonus", 0),
        })

    return clean_extracted_data(extracted)


def main():
    global DATA_DIR, OUTPUT_DIR

    args = parse_args()

    # Determine extraction directory
    if args.extraction_dir:
        DATA_DIR = args.extraction_dir
    else:
        # Find the latest extraction directory
        script_dir = Path(__file__).parent
        extractions_dir = script_dir / ".." / "extractions"
        DATA_DIR = get_latest_extraction(extractions_dir.resolve())

    OUTPUT_DIR = args.output_dir

    print(f"Using extraction: {DATA_DIR.name}")
    print("Generating static site v2 (SPA with compressed JSON)...")

    # Ensure output directory exists
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    # Load all items
    all_items = []
    counts = {"all": 0}

    for cat_id, cat_config in CATEGORIES.items():
        if cat_id == "all":
            continue

        path = cat_config.get("path")
        if not path:
            continue

        print(f"Loading {cat_id}...")
        raw_items = load_items_from_path(path)

        valid_count = 0
        for item in raw_items:
            extracted = extract_item_data(item, cat_id)
            if is_valid_item(item, extracted):
                all_items.append(extracted)
                valid_count += 1

        print(f"  Found {len(raw_items)} items, {valid_count} valid")
        counts[cat_id] = valid_count

    counts["all"] = len(all_items)

    # Sort by name
    all_items.sort(key=lambda x: x.get("name", "").lower())

    # Create the database object
    database = {
        "items": all_items,
        "counts": counts,
        "categories": {k: {"title": v["title"], "icon": v["icon"]} for k, v in CATEGORIES.items()},
    }

    # Write uncompressed JSON
    json_path = OUTPUT_DIR / "items.json"
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(database, f, separators=(',', ':'))  # Minified

    uncompressed_size = json_path.stat().st_size
    print(f"\nUncompressed JSON: {uncompressed_size:,} bytes ({uncompressed_size/1024/1024:.2f} MB)")

    # Write gzip compressed (maximum compression level 9)
    gz_path = OUTPUT_DIR / "items.json.gz"
    with gzip.open(gz_path, 'wt', encoding='utf-8', compresslevel=9) as f:
        json.dump(database, f, separators=(',', ':'))

    gz_size = gz_path.stat().st_size
    print(f"Gzip compressed:   {gz_size:,} bytes ({gz_size/1024:.2f} KB) - {gz_size/uncompressed_size*100:.1f}% of original")

    # Try brotli if available
    try:
        import brotli
        br_path = OUTPUT_DIR / "items.json.br"
        json_bytes = json.dumps(database, separators=(',', ':')).encode('utf-8')
        compressed = brotli.compress(json_bytes, quality=11)  # Max quality
        with open(br_path, 'wb') as f:
            f.write(compressed)
        br_size = br_path.stat().st_size
        print(f"Brotli compressed: {br_size:,} bytes ({br_size/1024:.2f} KB) - {br_size/uncompressed_size*100:.1f}% of original")
    except ImportError:
        print("Brotli not available (pip install brotli for better compression)")

    print(f"\nTotal items: {len(all_items):,}")
    print("\nCategory counts:")
    for cat_id, count in sorted(counts.items(), key=lambda x: -x[1]):
        print(f"  {cat_id}: {count}")


if __name__ == "__main__":
    main()
