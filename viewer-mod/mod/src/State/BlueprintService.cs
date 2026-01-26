using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Newtonsoft.Json;

namespace ViewerMod.State
{
    public class BlueprintService
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly object FieldCacheLock = new object();
        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();
        private MethodInfo _tryGet;
        private object _cache;
        private IDictionary _dict;
        private string[] _guids;
        private bool _initialized;

        private static readonly string[] EquipmentKeywords =
        {
            "Item",
            "Weapon",
            "Armor",
            "Consumable",
            "Equipment",
            "Shield",
            "Usable",
            "Accessory",
        };

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
                    catch { return Array.Empty<Type>(); }
                })
                .ToArray();

            var resourcesLibraryType = types.FirstOrDefault(t => t.Name == "ResourcesLibrary")
                ?? throw new Exception("ResourcesLibrary type not found");

            _tryGet = resourcesLibraryType
                .GetMethods(All)
                .FirstOrDefault(m => m.Name == "TryGetBlueprint" &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(string))
                ?? throw new Exception("ResourcesLibrary.TryGetBlueprint(string) not found");

            // Prefer ResourcesLibrary.BlueprintsCache (most reliable)
            var blueprintsCacheProp = resourcesLibraryType.GetProperty("BlueprintsCache", All);
            if (blueprintsCacheProp != null)
            {
                _cache = blueprintsCacheProp.GetValue(null);
            }
            else
            {
                var blueprintsCacheField = resourcesLibraryType.GetField("BlueprintsCache", All);
                if (blueprintsCacheField != null)
                    _cache = blueprintsCacheField.GetValue(null);
            }

            if (_cache == null)
            {
                // Fallback: locate BlueprintsCache type and try common singleton patterns
                var cacheType = types.FirstOrDefault(t => t.Name.EndsWith("BlueprintsCache"))
                    ?? throw new Exception("BlueprintsCache type not found");

                var instanceProp = cacheType.GetProperty("Instance", All);
                if (instanceProp != null)
                    _cache = instanceProp.GetValue(null);

                if (_cache == null)
                {
                    var instanceField = cacheType.GetFields(All)
                        .FirstOrDefault(f => f.Name.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (instanceField != null)
                        _cache = instanceField.GetValue(null);
                }
            }

            if (_cache == null)
                throw new Exception("Could not locate BlueprintsCache instance");

            _dict = _cache.GetType().GetMembers(All)
                .Select(m =>
                {
                    if (m is FieldInfo f) return f.GetValue(f.IsStatic ? null : _cache);
                    if (m is PropertyInfo p && p.GetIndexParameters().Length == 0) return p.GetValue(p.GetGetMethod(true)?.IsStatic == true ? null : _cache);
                    return null;
                })
                .OfType<IDictionary>()
                .OrderByDescending(d => d.Count)
                .FirstOrDefault(d => d.Count > 100)
                ?? throw new Exception("No suitable blueprint GUID dictionary found on cache");

            // Snapshot GUID keys once. Hydration can mutate the underlying cache/dict.
            _guids = _dict.Keys.Cast<object>()
                .Select(k => k?.ToString()?.Replace("-", "")?.ToLowerInvariant())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        public List<BlueprintMeta> GetAllBlueprints()
        {
            Initialize();

            // Performance note:
            // Hydrating every blueprint here (TryGetBlueprint for ~150k GUIDs) is expensive and duplicates
            // work done again during per-blueprint detail calls.
            // For dumping, the GUID list is sufficient and is already complete from the cache dictionary.
            return _guids.Select(guid => new BlueprintMeta { Guid = guid, Name = "", Type = "", Namespace = "" }).ToList();
        }

        public object GetBlueprintRange(int start, int count)
        {
            Initialize();

            if (start < 0) start = 0;
            if (count < 0) count = 0;
            if (start > _guids.Length) start = _guids.Length;
            if (start + count > _guids.Length) count = _guids.Length - start;

            var blueprints = new List<object>(count);
            for (var i = 0; i < count; i++)
            {
                var guid = _guids[start + i];
                var bp = GetBlueprintByGuid(guid);
                if (bp != null) blueprints.Add(bp);
            }

            return new { total = _guids.Length, start, count, blueprints };
        }

        public void WriteEquipmentBlueprintsNdjson(int start, int count, Stream output)
        {
            Initialize();

            if (start < 0) start = 0;
            if (count < 0) count = 0;
            if (start > _guids.Length) start = _guids.Length;
            if (start + count > _guids.Length) count = _guids.Length - start;

            var encoding = new UTF8Encoding(false);
            using (var writer = new StreamWriter(output, encoding, 64 * 1024, leaveOpen: true))
            {
                for (var i = 0; i < count; i++)
                {
                    var guid = _guids[start + i];
                    var result = _tryGet.Invoke(null, new object[] { guid });
                    var bp = result?.GetType().GetProperty("Blueprint", All)?.GetValue(result) ?? result;
                    if (bp == null) continue;

                    var t = bp.GetType();
                    if (!IsEquipmentBlueprint(t)) continue;

                    var name = (t.GetProperty("name") ?? t.GetProperty("Name"))?.GetValue(bp) as string;
                    var lineObj = new
                    {
                        meta = new BlueprintMeta { Guid = guid, Name = name ?? "", Type = t.Name, Namespace = t.Namespace ?? "" },
                        data = Dump(bp, 3, 200, new HashSet<object>(ReferenceEqualityComparer.Instance))
                    };

                    writer.Write(JsonConvert.SerializeObject(lineObj, Formatting.None));
                    writer.Write('\n');
                }
            }
        }

        private static bool IsEquipmentBlueprint(Type type)
        {
            if (type == null) return false;

            var name = type.Name ?? "";
            var ns = type.Namespace ?? "";

            for (var i = 0; i < EquipmentKeywords.Length; i++)
            {
                if (name.IndexOf(EquipmentKeywords[i], StringComparison.Ordinal) >= 0) return true;
            }

            return ns.IndexOf(".Items", StringComparison.OrdinalIgnoreCase) >= 0 && name.StartsWith("Blueprint", StringComparison.Ordinal);
        }

        public object GetBlueprintByGuid(string guid)
        {
            Initialize();

            var normalized = guid.Replace("-", "").ToLowerInvariant();
            var result = _tryGet.Invoke(null, new object[] { normalized });
            var bp = result?.GetType().GetProperty("Blueprint", All)?.GetValue(result) ?? result;
            if (bp == null) return null;

            var t = bp.GetType();
            var name = (t.GetProperty("name") ?? t.GetProperty("Name"))?.GetValue(bp) as string;
            // Match extractor-mod defaults more closely: depth=3.
            return new
            {
                meta = new BlueprintMeta { Guid = normalized, Name = name ?? "", Type = t.Name, Namespace = t.Namespace ?? "" },
                data = Dump(bp, 3, 200, new HashSet<object>(ReferenceEqualityComparer.Instance))
            };
        }

        public object GetBlueprintObjectByGuid(string guid, out BlueprintMeta meta)
        {
            Initialize();

            meta = null;
            if (string.IsNullOrEmpty(guid)) return null;

            var normalized = guid.Replace("-", "").ToLowerInvariant();
            var result = _tryGet.Invoke(null, new object[] { normalized });
            var bp = result?.GetType().GetProperty("Blueprint", All)?.GetValue(result) ?? result;
            if (bp == null) return null;

            var t = bp.GetType();
            var name = (t.GetProperty("name") ?? t.GetProperty("Name"))?.GetValue(bp) as string;
            meta = new BlueprintMeta { Guid = normalized, Name = name ?? "", Type = t.Name, Namespace = t.Namespace ?? "" };
            return bp;
        }

        private object Dump(object obj, int depth, int maxCollection, HashSet<object> visited)
        {
            if (obj == null || depth < 0) return null;

            // Avoid re-dumping the same object graph nodes.
            if (!(obj is string) && !obj.GetType().IsValueType)
            {
                if (visited.Contains(obj)) return null;
                visited.Add(obj);
            }

            var t = obj.GetType();
            if (t.IsPrimitive || obj is string || obj is decimal) return obj;
            if (t.IsEnum || obj is Guid) return obj.ToString();

            if (obj is IDictionary dict)
                return dict.Keys.Cast<object>().Take(maxCollection).Select(k => new { key = Dump(k, depth - 1, maxCollection, visited), value = Dump(dict[k], depth - 1, maxCollection, visited) }).ToList();

            if (obj is IEnumerable seq && !(obj is string))
                return seq.Cast<object>().Take(maxCollection).Select(x => Dump(x, depth - 1, maxCollection, visited)).ToList();

            var result = new Dictionary<string, object> { ["$type"] = t.FullName };

            FieldInfo[] fields;
            lock (FieldCacheLock)
            {
                if (!FieldCache.TryGetValue(t, out fields))
                {
                    fields = t.GetFields(All).Where(f => !f.IsStatic).Take(100).ToArray();
                    FieldCache[t] = fields;
                }
            }

            foreach (var f in fields)
            {
                try { var v = f.GetValue(obj); if (v != null) result[f.Name] = Dump(v, depth - 1, maxCollection, visited); } catch { }
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

    public class BlueprintMeta
    {
        public string Guid { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Namespace { get; set; }
    }
}
