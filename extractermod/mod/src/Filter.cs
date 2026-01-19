using System;
using System.Collections.Generic;
using System.Linq;

namespace BlueprintDumper
{
    public static class Filter
    {
        private static readonly string[] ItemKeywords = { "Item", "Weapon", "Armor", "Consumable", "Equipment", "Shield", "Usable", "Accessory" };

        public static List<object> EquipmentForDb(List<object> all)
        {
            return all.Where(o =>
            {
                var t = o?.GetType();
                if (t == null) return false;
                var name = t.Name;
                var ns = t.Namespace ?? "";
                return ItemKeywords.Any(k => name.Contains(k)) || (ns.Contains(".Items") && name.StartsWith("Blueprint"));
            }).ToList();
        }
    }
}
