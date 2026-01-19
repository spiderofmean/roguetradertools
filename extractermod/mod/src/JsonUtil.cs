using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BlueprintDumper
{
    public static class JsonUtil
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            MaxDepth = 10,
            Error = (s, e) => e.ErrorContext.Handled = true
        };

        public static string ToFallbackJson(object blueprint, (string Guid, string Name, string Type, string Namespace, string FullType) meta)
        {
            var payload = new Dictionary<string, object>
            {
                ["$type"] = blueprint.GetType().FullName,
                ["guid"] = meta.Guid,
                ["name"] = meta.Name,
                ["namespace"] = meta.Namespace,
                ["data"] = SafeDump(blueprint, 3, 200, new HashSet<object>(ReferenceEqualityComparer.Instance))
            };
            return JsonConvert.SerializeObject(payload, Settings);
        }

        private static object SafeDump(object obj, int depth, int maxCollection, HashSet<object> visited)
        {
            if (obj == null || depth < 0) return null;

            var t = obj.GetType();

            if (t.IsPrimitive || obj is string || obj is decimal) return obj;
            if (t.IsEnum) return obj.ToString();
            if (obj is Guid g) return g.ToString();
            if (obj is DateTime dt) return dt.ToString("o");

            if (!t.IsValueType)
            {
                if (visited.Contains(obj)) return new Dictionary<string, object> { ["$ref"] = t.FullName };
                visited.Add(obj);
            }

            if (obj is IDictionary dict)
            {
                var list = new List<object>();
                int i = 0;
                foreach (var key in dict.Keys)
                {
                    if (i++ >= maxCollection) break;
                    try { list.Add(new Dictionary<string, object> { ["key"] = SafeDump(key, depth - 1, maxCollection, visited), ["value"] = SafeDump(dict[key], depth - 1, maxCollection, visited) }); } catch { }
                }
                return new Dictionary<string, object> { ["$type"] = t.FullName, ["items"] = list };
            }

            if (obj is IEnumerable seq)
            {
                var list = new List<object>();
                int i = 0;
                try { foreach (var item in seq) { if (i++ >= maxCollection) break; list.Add(SafeDump(item, depth - 1, maxCollection, visited)); } } catch { }
                return new Dictionary<string, object> { ["$type"] = t.FullName, ["items"] = list };
            }

            var result = new Dictionary<string, object> { ["$type"] = t.FullName };
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(f => !f.IsStatic).Take(100))
            {
                try { var v = f.GetValue(obj); if (v != null) result[f.Name] = SafeDump(v, depth - 1, maxCollection, visited); } catch { }
            }
            return result;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
