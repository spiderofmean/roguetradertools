using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BlueprintDumper
{
    public static class BlueprintDatabaseUtil
    {
        public static async Task<List<object>> LoadAllBlueprintsAsync(
            IEnumerable<Assembly> assemblies,
            ReflectionUtil.Ctx reflectionCtx,
            Action<string> log = null)
        {
            if (!ReflectionUtil.TryGetCacheInstance(reflectionCtx, out var cacheInstance, out _))
                return new List<object>();

            var cacheType = cacheInstance.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            // Find the GUID-keyed dictionary (typically m_LoadedBlueprints or similar)
            var guidDict = cacheType.GetMembers(flags)
                .Select(m => new { Member = m, Value = TryGetMemberValue(m, cacheInstance) })
                .Where(x => x.Value is IDictionary dict && CountGuidKeys(dict) > 1000)
                .Select(x => x.Value as IDictionary)
                .FirstOrDefault();

            if (guidDict == null)
            {
                log?.Invoke("Could not find GUID dictionary in cache");
                return new List<object>();
            }

            // Extract all blueprints from the dictionary values
            var results = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var val in guidDict.Values)
            {
                var bp = ReflectionUtil.TryUnwrapBlueprint(val, reflectionCtx.SimpleBlueprint);
                if (bp != null)
                {
                    try
                    {
                        var meta = ReflectionUtil.GetBlueprintMeta(bp);
                        var guid = NormalizeGuid(meta.Guid);
                        if (!string.IsNullOrEmpty(guid))
                            results[guid] = bp;
                    }
                    catch { }
                }
            }

            log?.Invoke($"Found {results.Count} blueprints in cache");

            // Try to force-load remaining blueprints via TryGetBlueprint
            var tryGetMethod = reflectionCtx.ResourcesLibraryType?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "TryGetBlueprint" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));

            if (tryGetMethod != null)
            {
                var allGuids = guidDict.Keys.Cast<object>()
                    .Select(k => NormalizeGuid(k?.ToString()))
                    .Where(IsValidGuid)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                int loaded = 0;
                for (int i = 0; i < allGuids.Count; i++)
                {
                    var guid = allGuids[i];
                    if (results.ContainsKey(guid)) continue;

                    try
                    {
                        var result = tryGetMethod.Invoke(null, new object[] { guid });
                        var bp = ReflectionUtil.TryUnwrapBlueprint(result, reflectionCtx.SimpleBlueprint);
                        if (bp != null)
                        {
                            results[guid] = bp;
                            loaded++;
                        }
                    }
                    catch { }

                    if (i % 1000 == 0)
                    {
                        log?.Invoke($"Force-loading: {i}/{allGuids.Count} ({loaded} new)");
                        await Task.Delay(10);
                    }
                }
                log?.Invoke($"Force-loaded {loaded} additional blueprints");
            }

            return results.Values.ToList();
        }

        private static object TryGetMemberValue(MemberInfo m, object instance)
        {
            try
            {
                if (m is FieldInfo f) return f.GetValue(instance);
                if (m is PropertyInfo p && p.GetIndexParameters().Length == 0) return p.GetValue(instance, null);
            }
            catch { }
            return null;
        }

        private static int CountGuidKeys(IDictionary dict)
        {
            int count = 0;
            foreach (var key in dict.Keys)
            {
                if (IsValidGuid(key?.ToString()) && ++count > 1000) break;
            }
            return count;
        }

        private static string NormalizeGuid(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return null;
            return guid.Trim().Replace("-", "").ToLowerInvariant();
        }

        private static bool IsValidGuid(string guid)
        {
            var n = NormalizeGuid(guid);
            return n != null && n.Length == 32 && n != "00000000000000000000000000000000"
                && n.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        }
    }
}
