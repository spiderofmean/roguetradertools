using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BlueprintDumper
{
    public static class ExtractUtil
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static Dictionary<string, object> ExtractFlatRecord(object blueprint)
        {
            var meta = ReflectionUtil.GetBlueprintMeta(blueprint);
            var record = new Dictionary<string, object>
            {
                ["guid"] = meta.Guid,
                ["name"] = meta.Name,
                ["type"] = meta.FullType,
                ["namespace"] = meta.Namespace
            };

            foreach (var f in blueprint.GetType().GetFields(Flags).Where(f => !f.IsStatic).Take(100))
            {
                try
                {
                    var v = f.GetValue(blueprint);
                    if (v != null && TryCoercePrimitive(v, out var coerced))
                        record[f.Name] = coerced;
                }
                catch { }
            }

            var components = GetComponentTypeNames(blueprint);
            if (components?.Count > 0)
                record["components"] = components;

            return record;
        }

        private static bool TryCoercePrimitive(object v, out object coerced)
        {
            coerced = null;
            var t = v.GetType();
            if (t.IsEnum) { coerced = v.ToString(); return true; }
            if (t.IsPrimitive || v is string || v is decimal) { coerced = v; return true; }
            if (v is Guid g) { coerced = g.ToString(); return true; }
            if (v is DateTime dt) { coerced = dt.ToString("o"); return true; }
            return false;
        }

        private static List<string> GetComponentTypeNames(object blueprint)
        {
            var componentField = blueprint.GetType().GetFields(Flags)
                .FirstOrDefault(f => !f.IsStatic && f.Name.Contains("Component") && typeof(IEnumerable).IsAssignableFrom(f.FieldType));

            if (componentField == null) return null;

            try
            {
                if (componentField.GetValue(blueprint) is IEnumerable e)
                    return e.Cast<object>().Where(c => c != null).Select(c => c.GetType().FullName).Distinct().OrderBy(x => x).Take(200).ToList();
            }
            catch { }
            return null;
        }

        public static string ToJsonLine(object obj) => JsonConvert.SerializeObject(obj, Formatting.None);
    }
}
