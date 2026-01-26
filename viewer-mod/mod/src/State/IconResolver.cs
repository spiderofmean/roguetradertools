using System;
using System.Reflection;
using UnityEngine;

namespace ViewerMod.State
{
    public static class IconResolver
    {
        private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // Derived via probing: player-facing BlueprintItem* uses one of these.
        private static readonly string[] IconMemberCandidates =
        {
            "m_Icon",
            "Icon",
        };

        public sealed class IconResolution
        {
            public UnityEngine.Object Icon { get; set; }
            public string Path { get; set; }
        }

        public static IconResolution FindIconResolution(object blueprint)
        {
            if (blueprint == null) return null;

            for (var i = 0; i < IconMemberCandidates.Length; i++)
            {
                var memberName = IconMemberCandidates[i];
                if (!TryGetMemberValueByName(blueprint, memberName, out var raw)) continue;

                if (TryResolveToSpriteOrTextureWithPath(raw, depth: 3, out var icon, out var suffix))
                {
                    return new IconResolution
                    {
                        Icon = icon,
                        Path = string.IsNullOrEmpty(suffix) ? $"root.{memberName}" : $"root.{memberName}{suffix}"
                    };
                }
            }

            return null;
        }

        private static bool TryGetMemberValueByName(object instance, string name, out object value)
        {
            value = null;
            if (instance == null || string.IsNullOrEmpty(name)) return false;

            for (var current = instance.GetType(); current != null; current = current.BaseType)
            {
                try
                {
                    var field = current.GetField(name, All);
                    if (field != null && !field.IsStatic)
                    {
                        value = field.GetValue(instance);
                        return true;
                    }

                    var prop = current.GetProperty(name, All);
                    if (prop != null && prop.GetIndexParameters().Length == 0)
                    {
                        var getter = prop.GetGetMethod(true);
                        if (getter != null && !getter.IsStatic)
                        {
                            value = prop.GetValue(instance, null);
                            return true;
                        }
                    }
                }
                catch
                {
                    // Best-effort.
                }
            }

            return false;
        }

        private static bool TryResolveToSpriteOrTextureWithPath(object value, int depth, out UnityEngine.Object icon, out string suffixPath)
        {
            icon = null;
            suffixPath = null;
            if (value == null || depth < 0) return false;

            if (value is Sprite sprite)
            {
                icon = sprite;
                return true;
            }

            if (value is Texture2D tex2d)
            {
                icon = tex2d;
                return true;
            }

            if (value is Texture tex)
            {
                icon = tex;
                return true;
            }

            try
            {
                var t = value.GetType();

                // Wrapper/link object pattern
                var load = t.GetMethod("Load", All, null, Type.EmptyTypes, null);
                if (load != null)
                {
                    var loaded = load.Invoke(value, null);
                    if (TryResolveToSpriteOrTextureWithPath(loaded, depth - 1, out icon, out var sub))
                    {
                        suffixPath = ".Load()" + (sub ?? "");
                        return true;
                    }
                }

                foreach (var propName in new[] { "Sprite", "sprite", "Icon", "icon", "Texture", "texture" })
                {
                    var prop = t.GetProperty(propName, All);
                    if (prop == null) continue;
                    if (prop.GetIndexParameters().Length > 0) continue;
                    var getter = prop.GetGetMethod(true);
                    if (getter == null) continue;

                    var v = prop.GetValue(value, null);
                    if (TryResolveToSpriteOrTextureWithPath(v, depth - 1, out icon, out var sub))
                    {
                        suffixPath = "." + propName + (sub ?? "");
                        return true;
                    }
                }

                foreach (var fieldName in new[] { "m_Sprite", "Sprite", "sprite", "m_Icon", "Icon", "icon", "m_Texture", "Texture", "texture" })
                {
                    var field = t.GetField(fieldName, All);
                    if (field == null) continue;
                    if (field.IsStatic) continue;

                    var v = field.GetValue(value);
                    if (TryResolveToSpriteOrTextureWithPath(v, depth - 1, out icon, out var sub))
                    {
                        suffixPath = "." + fieldName + (sub ?? "");
                        return true;
                    }
                }
            }
            catch
            {
                // Best-effort.
            }

            return false;
        }
    }
}
