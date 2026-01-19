using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BlueprintDumper
{
    public static class ReflectionUtil
    {
        public sealed class Ctx
        {
            public Type SimpleBlueprint;
            public Type BlueprintsCacheType;
            public Type ResourcesLibraryType;
            public MemberInfo CacheAccessor;
            public MemberInfo BlueprintCollectionMember;
        }

        public static Ctx CreateContext(IEnumerable<Assembly> assemblies)
        {
            Type simple = null, cache = null, resourcesLibrary = null;
            MemberInfo cacheAccessor = null;

            foreach (var asm in assemblies)
            {
                foreach (var t in GetTypesSafe(asm))
                {
                    if (simple == null && t.Name == "SimpleBlueprint")
                        simple = t;
                    if (cache == null && t.Name.EndsWith("BlueprintsCache", StringComparison.OrdinalIgnoreCase))
                        cache = t;
                    if (t.Name == "ResourcesLibrary")
                        resourcesLibrary = t;
                }
            }

            if (simple == null) throw new Exception("Could not find SimpleBlueprint type");
            if (cache == null) throw new Exception("Could not find BlueprintsCache type");

            if (resourcesLibrary != null)
            {
                var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                cacheAccessor = resourcesLibrary.GetProperties(flags)
                    .FirstOrDefault(p => p.Name.Contains("BlueprintsCache") && cache.IsAssignableFrom(p.PropertyType))
                    ?? (MemberInfo)resourcesLibrary.GetFields(flags)
                    .FirstOrDefault(f => f.Name.Contains("BlueprintsCache") && cache.IsAssignableFrom(f.FieldType));
            }

            return new Ctx
            {
                SimpleBlueprint = simple,
                BlueprintsCacheType = cache,
                ResourcesLibraryType = resourcesLibrary,
                CacheAccessor = cacheAccessor,
                BlueprintCollectionMember = FindBlueprintCollectionMember(cache)
            };
        }

        public static bool TryGetAllBlueprints(Ctx ctx, out List<object> blueprints, out string reason)
        {
            blueprints = null;
            reason = null;

            if (!TryGetCacheInstance(ctx, out var cacheInstance, out reason))
                return false;

            var member = ctx.BlueprintCollectionMember ?? FindBlueprintCollectionMember(cacheInstance.GetType());
            if (member == null) { reason = "No blueprint collection member found"; return false; }

            object raw;
            try { raw = GetMemberValue(member, cacheInstance); }
            catch { reason = "Failed to read blueprint collection"; return false; }

            if (raw == null) { reason = "Blueprint collection is null"; return false; }

            var results = new List<object>();
            try
            {
                if (raw is IDictionary dict)
                    foreach (var val in dict.Values)
                    {
                        var bp = TryUnwrapBlueprint(val, ctx.SimpleBlueprint);
                        if (bp != null) results.Add(bp);
                    }
                else if (raw is IEnumerable e)
                    foreach (var o in e)
                    {
                        var bp = TryUnwrapBlueprint(o, ctx.SimpleBlueprint);
                        if (bp != null) results.Add(bp);
                    }
            }
            catch (InvalidOperationException)
            {
                reason = "Collection being modified";
                return false;
            }

            if (results.Count == 0) { reason = "Blueprint collection empty"; return false; }
            blueprints = results;
            return true;
        }

        public static object TryUnwrapBlueprint(object maybeEntry, Type simpleBlueprintType)
        {
            if (maybeEntry == null) return null;
            if (simpleBlueprintType?.IsInstanceOfType(maybeEntry) == true) return maybeEntry;

            var t = maybeEntry.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var name in new[] { "Blueprint", "m_Blueprint", "Value" })
            {
                var prop = t.GetProperty(name, flags);
                if (prop?.GetIndexParameters().Length == 0)
                    try { var v = prop.GetValue(maybeEntry, null); if (v != null) return v; } catch { }

                var field = t.GetField(name, flags);
                if (field != null)
                    try { var v = field.GetValue(maybeEntry); if (v != null) return v; } catch { }
            }
            return null;
        }

        public static (string Guid, string Name, string Type, string Namespace, string FullType) GetBlueprintMeta(object bp)
        {
            var t = bp.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            var name = t.GetProperty("name")?.GetValue(bp) as string
                       ?? t.GetProperty("Name")?.GetValue(bp) as string
                       ?? "unnamed";

            var guidObj = t.GetProperty("AssetGuid")?.GetValue(bp)
                          ?? t.GetField("AssetGuid", flags)?.GetValue(bp);

            return (guidObj?.ToString() ?? "unknown-guid", name, t.Name, t.Namespace ?? "", t.FullName ?? t.Name);
        }

        public static bool TryGetCacheInstance(Ctx ctx, out object cacheInstance, out string reason)
        {
            cacheInstance = null;
            reason = null;

            if (ctx.CacheAccessor != null)
            {
                try
                {
                    cacheInstance = GetMemberValue(ctx.CacheAccessor, null);
                    if (cacheInstance != null) return true;
                    reason = "Cache accessor returned null";
                    return false;
                }
                catch (Exception ex)
                {
                    reason = "Cache accessor threw: " + ex.GetType().Name;
                    return false;
                }
            }

            var t = ctx.BlueprintsCacheType;
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            
            var instProp = t.GetProperties(flags).FirstOrDefault(p => p.Name == "Instance" && t.IsAssignableFrom(p.PropertyType));
            if (instProp != null) { cacheInstance = instProp.GetValue(null, null); if (cacheInstance != null) return true; }

            var instField = t.GetFields(flags).FirstOrDefault(f => f.Name.IndexOf("instance", StringComparison.OrdinalIgnoreCase) >= 0);
            if (instField != null) { cacheInstance = instField.GetValue(null); if (cacheInstance != null) return true; }

            reason = "Could not acquire cache instance";
            return false;
        }

        public static object GetMemberValue(MemberInfo member, object instance) =>
            member is PropertyInfo p ? p.GetValue(instance, null) :
            member is FieldInfo f ? f.GetValue(instance) :
            throw new NotSupportedException();

        private static IEnumerable<Type> GetTypesSafe(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
            catch { return Enumerable.Empty<Type>(); }
        }

        private static MemberInfo FindBlueprintCollectionMember(Type cacheType)
        {
            if (cacheType == null) return null;

            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            return cacheType.GetMembers(flags)
                .Where(m => m is FieldInfo || m is PropertyInfo)
                .Where(m =>
                {
                    var memberType = m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
                    return typeof(IEnumerable).IsAssignableFrom(memberType);
                })
                .OrderByDescending(m =>
                {
                    var name = m.Name.ToLowerInvariant();
                    if (name.Contains("loadedblueprints")) return 20;
                    if (name.Contains("blueprint")) return 10;
                    return 0;
                })
                .FirstOrDefault();
        }
    }
}
