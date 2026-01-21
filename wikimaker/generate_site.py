#!/usr/bin/env python3
"""Static site generator for Warhammer 40K Rogue Trader item database."""

import json
import os
import html
from pathlib import Path
from collections import defaultdict

DATA_DIR = Path("2026-01-18-185750-manual")
OUTPUT_DIR = Path("website")

# Category configuration
CATEGORIES = {
    "weapons": {
        "title": "Weapons",
        "path": "Kingmaker/Blueprints/Items/Weapons/BlueprintItemWeapon",
        "icon": "⚔️",
        "subcategories": {
            "ranged": {"title": "Ranged Weapons", "filter": lambda d: d.get("data", {}).get("IsRanged", False)},
            "melee": {"title": "Melee Weapons", "filter": lambda d: d.get("data", {}).get("IsMelee", False)},
        }
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
}


def load_items(category_path: str) -> list:
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


def get_rarity_class(rarity: str) -> str:
    """Get CSS class for rarity."""
    rarity_map = {
        "Common": "common",
        "Uncommon": "uncommon",
        "Rare": "rare",
        "VeryRare": "veryrare",
        "Unique": "unique",
    }
    return rarity_map.get(rarity, "common")


def escape(text) -> str:
    """HTML escape text."""
    if text is None:
        return ""
    return html.escape(str(text))


def generate_sidebar(active_category: str = None, counts: dict = None) -> str:
    """Generate sidebar HTML."""
    counts = counts or {}

    sidebar = '''
    <nav class="sidebar">
        <div class="sidebar-header">
            <h1>RT Database</h1>
            <div class="subtitle">Warhammer 40K: Rogue Trader</div>
        </div>
        <div class="search-box">
            <input type="text" class="search-input" placeholder="Search items..." id="search-input">
        </div>
        <div class="nav-section">
            <div class="nav-section-title">Equipment</div>
    '''

    equipment_cats = ["weapons", "armor", "shields", "helmets", "gloves", "boots", "cloaks", "rings", "amulets", "usables"]
    for cat_id in equipment_cats:
        cat = CATEGORIES.get(cat_id, {})
        active = "active" if cat_id == active_category else ""
        count = counts.get(cat_id, 0)
        sidebar += f'''
            <a href="{cat_id}.html" class="nav-item {active}">
                {cat.get("title", cat_id)} <span class="count">{count}</span>
            </a>
        '''

    sidebar += '''
        </div>
        <div class="nav-section">
            <div class="nav-section-title">Starship</div>
    '''

    starship_cats = ["starship-weapons", "void-shields"]
    for cat_id in starship_cats:
        cat = CATEGORIES.get(cat_id, {})
        active = "active" if cat_id == active_category else ""
        count = counts.get(cat_id, 0)
        sidebar += f'''
            <a href="{cat_id}.html" class="nav-item {active}">
                {cat.get("title", cat_id)} <span class="count">{count}</span>
            </a>
        '''

    sidebar += '''
        </div>
    </nav>
    '''
    return sidebar


def generate_weapon_card(item: dict) -> str:
    """Generate HTML card for a weapon."""
    data = item.get("data", {})
    name = escape(item.get("name", "Unknown"))
    guid = item.get("guid", "")
    rarity = data.get("Rarity", "Common")
    rarity_class = get_rarity_class(rarity)

    damage_min = data.get("WarhammerDamage", 0)
    damage_max = data.get("WarhammerMaxDamage", damage_min)
    penetration = data.get("WarhammerPenetration", 0)
    attack_range = data.get("WarhammerMaxDistance", data.get("AttackRange", 0))
    family = data.get("Family", "Unknown")
    attack_type = "Ranged" if data.get("IsRanged") else "Melee" if data.get("IsMelee") else "Unknown"

    return f'''
    <div class="item-card">
        <a href="items/{guid}.html">
            <div class="item-card-header">
                <div class="item-icon rarity-border-{rarity_class}">
                    [IMG]
                </div>
                <div class="item-title-area">
                    <div class="item-name rarity-{rarity_class}">{name}</div>
                    <div class="item-type">{attack_type} • {family}</div>
                </div>
            </div>
            <div class="item-card-body">
                <div class="item-stats">
                    <div class="stat-row">
                        <span class="stat-label">Damage</span>
                        <span class="stat-value">{damage_min}-{damage_max}</span>
                    </div>
                    <div class="stat-row">
                        <span class="stat-label">Penetration</span>
                        <span class="stat-value">{penetration}</span>
                    </div>
                    <div class="stat-row">
                        <span class="stat-label">Range</span>
                        <span class="stat-value">{attack_range}</span>
                    </div>
                    <div class="stat-row">
                        <span class="stat-label">Rarity</span>
                        <span class="stat-value rarity-{rarity_class}">{rarity}</span>
                    </div>
                </div>
            </div>
        </a>
    </div>
    '''


def generate_armor_card(item: dict) -> str:
    """Generate HTML card for armor."""
    data = item.get("data", {})
    name = escape(item.get("name", "Unknown"))
    guid = item.get("guid", "")
    rarity = data.get("Rarity", "Common")
    rarity_class = get_rarity_class(rarity)

    absorption = data.get("DamageAbsorption", 0)
    deflection = data.get("DamageDeflection", 0)
    category = data.get("Category", "Unknown")

    return f'''
    <div class="item-card">
        <a href="items/{guid}.html">
            <div class="item-card-header">
                <div class="item-icon rarity-border-{rarity_class}">
                    [IMG]
                </div>
                <div class="item-title-area">
                    <div class="item-name rarity-{rarity_class}">{name}</div>
                    <div class="item-type">{category} Armor</div>
                </div>
            </div>
            <div class="item-card-body">
                <div class="item-stats">
                    <div class="stat-row">
                        <span class="stat-label">Absorption</span>
                        <span class="stat-value">{absorption}</span>
                    </div>
                    <div class="stat-row">
                        <span class="stat-label">Deflection</span>
                        <span class="stat-value">{deflection}</span>
                    </div>
                    <div class="stat-row">
                        <span class="stat-label">Rarity</span>
                        <span class="stat-value rarity-{rarity_class}">{rarity}</span>
                    </div>
                </div>
            </div>
        </a>
    </div>
    '''


def generate_equipment_card(item: dict, item_type: str = "Equipment") -> str:
    """Generate HTML card for generic equipment."""
    data = item.get("data", {})
    name = escape(item.get("name", "Unknown"))
    guid = item.get("guid", "")
    rarity = data.get("Rarity", "Common")
    rarity_class = get_rarity_class(rarity)

    return f'''
    <div class="item-card">
        <a href="items/{guid}.html">
            <div class="item-card-header">
                <div class="item-icon rarity-border-{rarity_class}">
                    [IMG]
                </div>
                <div class="item-title-area">
                    <div class="item-name rarity-{rarity_class}">{name}</div>
                    <div class="item-type">{item_type}</div>
                </div>
            </div>
            <div class="item-card-body">
                <div class="item-stats">
                    <div class="stat-row">
                        <span class="stat-label">Rarity</span>
                        <span class="stat-value rarity-{rarity_class}">{rarity}</span>
                    </div>
                </div>
            </div>
        </a>
    </div>
    '''


def generate_starship_weapon_card(item: dict) -> str:
    """Generate HTML card for starship weapon."""
    data = item.get("data", {})
    name = escape(item.get("name", "Unknown"))
    guid = item.get("guid", "")
    rarity = data.get("Rarity", "Common")
    rarity_class = get_rarity_class(rarity)

    weapon_type = data.get("WeaponType", "Unknown")
    damage_instances = data.get("DamageInstances", 1)
    slots = data.get("AllowedSlots", {})
    slot_list = slots.get("items", []) if isinstance(slots, dict) else []
    slot_str = ", ".join(slot_list) if slot_list else "Any"

    return f'''
    <div class="item-card">
        <a href="items/{guid}.html">
            <div class="item-card-header">
                <div class="item-icon rarity-border-{rarity_class}">
                    [IMG]
                </div>
                <div class="item-title-area">
                    <div class="item-name rarity-{rarity_class}">{name}</div>
                    <div class="item-type">{weapon_type}</div>
                </div>
            </div>
            <div class="item-card-body">
                <div class="item-stats">
                    <div class="stat-row">
                        <span class="stat-label">Shots</span>
                        <span class="stat-value">{damage_instances}</span>
                    </div>
                    <div class="stat-row">
                        <span class="stat-label">Slot</span>
                        <span class="stat-value">{slot_str}</span>
                    </div>
                    <div class="stat-row">
                        <span class="stat-label">Rarity</span>
                        <span class="stat-value rarity-{rarity_class}">{rarity}</span>
                    </div>
                </div>
            </div>
        </a>
    </div>
    '''


def generate_item_detail_page(item: dict, category_id: str, category_title: str) -> str:
    """Generate detailed item page."""
    data = item.get("data", {})
    name = escape(item.get("name", "Unknown"))
    guid = item.get("guid", "")
    rarity = data.get("Rarity", "Common")
    rarity_class = get_rarity_class(rarity)
    description = escape(data.get("Description", "")) or "No description available."

    # Build stats based on item type
    stats_html = ""

    if "Weapon" in item.get("$type", ""):
        damage_min = data.get("WarhammerDamage", 0)
        damage_max = data.get("WarhammerMaxDamage", damage_min)
        stats_html = f'''
        <div class="stat-box">
            <div class="label">Damage</div>
            <div class="value">{damage_min} - {damage_max}</div>
        </div>
        <div class="stat-box">
            <div class="label">Penetration</div>
            <div class="value">{data.get("WarhammerPenetration", 0)}</div>
        </div>
        <div class="stat-box">
            <div class="label">Range</div>
            <div class="value">{data.get("WarhammerMaxDistance", data.get("AttackRange", 0))}</div>
        </div>
        <div class="stat-box">
            <div class="label">Ammo</div>
            <div class="value">{data.get("WarhammerMaxAmmo", "N/A")}</div>
        </div>
        <div class="stat-box">
            <div class="label">Rate of Fire</div>
            <div class="value">{data.get("RateOfFire", "N/A")}</div>
        </div>
        <div class="stat-box">
            <div class="label">Recoil</div>
            <div class="value">{data.get("WarhammerRecoil", 0)}</div>
        </div>
        <div class="stat-box">
            <div class="label">Family</div>
            <div class="value">{data.get("Family", "Unknown")}</div>
        </div>
        <div class="stat-box">
            <div class="label">Holding</div>
            <div class="value">{data.get("HoldingType", "Unknown")}</div>
        </div>
        '''
    elif "Armor" in item.get("$type", ""):
        stats_html = f'''
        <div class="stat-box">
            <div class="label">Damage Absorption</div>
            <div class="value">{data.get("DamageAbsorption", 0)}</div>
        </div>
        <div class="stat-box">
            <div class="label">Damage Deflection</div>
            <div class="value">{data.get("DamageDeflection", 0)}</div>
        </div>
        <div class="stat-box">
            <div class="label">Category</div>
            <div class="value">{data.get("Category", "Unknown")}</div>
        </div>
        '''
    elif "Starship" in item.get("$type", ""):
        slots = data.get("AllowedSlots", {})
        slot_list = slots.get("items", []) if isinstance(slots, dict) else []
        slot_str = ", ".join(slot_list) if slot_list else "Any"
        stats_html = f'''
        <div class="stat-box">
            <div class="label">Weapon Type</div>
            <div class="value">{data.get("WeaponType", "Unknown")}</div>
        </div>
        <div class="stat-box">
            <div class="label">Damage Instances</div>
            <div class="value">{data.get("DamageInstances", 1)}</div>
        </div>
        <div class="stat-box">
            <div class="label">Allowed Slots</div>
            <div class="value">{slot_str}</div>
        </div>
        '''

    return f'''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{name} - RT Database</title>
    <link rel="stylesheet" href="../css/style.css">
</head>
<body>
    <div class="container">
        {generate_sidebar(category_id)}
        <main class="main-content">
            <div class="breadcrumb">
                <a href="../index.html">Home</a>
                <span>›</span>
                <a href="../{category_id}.html">{category_title}</a>
                <span>›</span>
                {name}
            </div>

            <div class="item-detail">
                <div class="item-detail-header">
                    <div class="item-detail-icon rarity-border-{rarity_class}">
                        [IMG]
                    </div>
                    <div class="item-detail-title">
                        <h1 class="rarity-{rarity_class}">{name}</h1>
                        <div class="item-detail-meta">
                            <span class="rarity-{rarity_class}">{rarity}</span>
                            <span>GUID: {guid}</span>
                        </div>
                    </div>
                </div>

                <div class="item-detail-section">
                    <h2>Statistics</h2>
                    <div class="stats-grid">
                        {stats_html}
                    </div>
                </div>

                <div class="item-detail-section">
                    <h2>Description</h2>
                    <p class="description-text">{description}</p>
                </div>
            </div>
        </main>
    </div>
</body>
</html>
'''


def generate_category_page(category_id: str, category: dict, items: list, counts: dict) -> str:
    """Generate a category listing page."""
    title = category.get("title", category_id)

    # Generate item cards based on category type
    cards_html = ""
    for item in sorted(items, key=lambda x: x.get("name", "")):
        if category_id == "weapons":
            cards_html += generate_weapon_card(item)
        elif category_id == "armor":
            cards_html += generate_armor_card(item)
        elif category_id in ["starship-weapons"]:
            cards_html += generate_starship_weapon_card(item)
        else:
            cards_html += generate_equipment_card(item, title.rstrip('s'))

    return f'''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>{title} - RT Database</title>
    <link rel="stylesheet" href="css/style.css">
</head>
<body>
    <div class="container">
        {generate_sidebar(category_id, counts)}
        <main class="main-content">
            <div class="page-header">
                <h1>{title}</h1>
                <p class="description">Browse all {len(items)} {title.lower()} in Warhammer 40K: Rogue Trader.</p>
            </div>

            <div class="item-grid" id="item-grid">
                {cards_html}
            </div>
        </main>
    </div>
    <script src="js/search.js"></script>
</body>
</html>
'''


def generate_index_page(counts: dict) -> str:
    """Generate the main index page."""
    total = sum(counts.values())

    return f'''<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>RT Database - Warhammer 40K: Rogue Trader</title>
    <link rel="stylesheet" href="css/style.css">
</head>
<body>
    <div class="container">
        {generate_sidebar(None, counts)}
        <main class="main-content">
            <div class="page-header">
                <h1>Warhammer 40K: Rogue Trader Database</h1>
                <p class="description">
                    Browse {total:,} items including weapons, armor, equipment, and starship components
                    from Warhammer 40,000: Rogue Trader.
                </p>
            </div>

            <div class="item-grid">
                <div class="item-card">
                    <a href="weapons.html">
                        <div class="item-card-header">
                            <div class="item-icon">⚔️</div>
                            <div class="item-title-area">
                                <div class="item-name">Weapons</div>
                                <div class="item-type">{counts.get("weapons", 0)} items</div>
                            </div>
                        </div>
                        <div class="item-card-body">
                            <p class="description-text">Ranged and melee weapons including bolters, lasguns, chainswords, and more.</p>
                        </div>
                    </a>
                </div>

                <div class="item-card">
                    <a href="armor.html">
                        <div class="item-card-header">
                            <div class="item-icon">🛡️</div>
                            <div class="item-title-area">
                                <div class="item-name">Armor</div>
                                <div class="item-type">{counts.get("armor", 0)} items</div>
                            </div>
                        </div>
                        <div class="item-card-body">
                            <p class="description-text">Body armor including carapace, power armor, and more.</p>
                        </div>
                    </a>
                </div>

                <div class="item-card">
                    <a href="helmets.html">
                        <div class="item-card-header">
                            <div class="item-icon">🪖</div>
                            <div class="item-title-area">
                                <div class="item-name">Helmets</div>
                                <div class="item-type">{counts.get("helmets", 0)} items</div>
                            </div>
                        </div>
                        <div class="item-card-body">
                            <p class="description-text">Head protection and enhancement gear.</p>
                        </div>
                    </a>
                </div>

                <div class="item-card">
                    <a href="rings.html">
                        <div class="item-card-header">
                            <div class="item-icon">💍</div>
                            <div class="item-title-area">
                                <div class="item-name">Rings</div>
                                <div class="item-type">{counts.get("rings", 0)} items</div>
                            </div>
                        </div>
                        <div class="item-card-body">
                            <p class="description-text">Rings with various bonuses and abilities.</p>
                        </div>
                    </a>
                </div>

                <div class="item-card">
                    <a href="starship-weapons.html">
                        <div class="item-card-header">
                            <div class="item-icon">🚀</div>
                            <div class="item-title-area">
                                <div class="item-name">Starship Weapons</div>
                                <div class="item-type">{counts.get("starship-weapons", 0)} items</div>
                            </div>
                        </div>
                        <div class="item-card-body">
                            <p class="description-text">Macro-cannons, lances, torpedoes and other void weaponry.</p>
                        </div>
                    </a>
                </div>

                <div class="item-card">
                    <a href="void-shields.html">
                        <div class="item-card-header">
                            <div class="item-icon">🛸</div>
                            <div class="item-title-area">
                                <div class="item-name">Void Shields</div>
                                <div class="item-type">{counts.get("void-shields", 0)} items</div>
                            </div>
                        </div>
                        <div class="item-card-body">
                            <p class="description-text">Starship shield generators.</p>
                        </div>
                    </a>
                </div>
            </div>
        </main>
    </div>
</body>
</html>
'''


def main():
    print("Generating static site...")

    # Ensure output directories exist
    (OUTPUT_DIR / "items").mkdir(parents=True, exist_ok=True)
    (OUTPUT_DIR / "css").mkdir(parents=True, exist_ok=True)
    (OUTPUT_DIR / "js").mkdir(parents=True, exist_ok=True)

    # Load all items and count them
    all_items = {}
    counts = {}

    for cat_id, cat_config in CATEGORIES.items():
        print(f"Loading {cat_id}...")
        items = load_items(cat_config["path"])
        all_items[cat_id] = items
        counts[cat_id] = len(items)
        print(f"  Found {len(items)} items")

    # Generate category pages
    for cat_id, cat_config in CATEGORIES.items():
        print(f"Generating {cat_id}.html...")
        items = all_items[cat_id]
        html_content = generate_category_page(cat_id, cat_config, items, counts)

        with open(OUTPUT_DIR / f"{cat_id}.html", 'w', encoding='utf-8') as f:
            f.write(html_content)

        # Generate individual item pages
        for item in items:
            guid = item.get("guid", "")
            if guid:
                detail_html = generate_item_detail_page(item, cat_id, cat_config["title"])
                with open(OUTPUT_DIR / "items" / f"{guid}.html", 'w', encoding='utf-8') as f:
                    f.write(detail_html)

    # Generate index page
    print("Generating index.html...")
    index_html = generate_index_page(counts)
    with open(OUTPUT_DIR / "index.html", 'w', encoding='utf-8') as f:
        f.write(index_html)

    # Generate search JS
    print("Generating search.js...")
    search_js = '''
document.addEventListener('DOMContentLoaded', function() {
    const searchInput = document.getElementById('search-input');
    const itemGrid = document.getElementById('item-grid');

    if (searchInput && itemGrid) {
        searchInput.addEventListener('input', function() {
            const query = this.value.toLowerCase();
            const cards = itemGrid.querySelectorAll('.item-card');

            cards.forEach(card => {
                const name = card.querySelector('.item-name').textContent.toLowerCase();
                if (name.includes(query)) {
                    card.style.display = '';
                } else {
                    card.style.display = 'none';
                }
            });
        });
    }
});
'''
    with open(OUTPUT_DIR / "js" / "search.js", 'w', encoding='utf-8') as f:
        f.write(search_js)

    print(f"\nDone! Generated site in {OUTPUT_DIR}/")
    print(f"Total items: {sum(counts.values()):,}")


if __name__ == "__main__":
    main()
