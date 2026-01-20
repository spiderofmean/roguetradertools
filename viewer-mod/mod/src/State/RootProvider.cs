using System;
using System.Collections.Generic;
using System.Linq;
using ViewerMod.Models;

namespace ViewerMod.State
{
    /// <summary>
    /// Provides entry point roots into the game's object graph.
    /// </summary>
    public static class RootProvider
    {
        /// <summary>
        /// Gets all known root objects and registers them in the handle registry.
        /// </summary>
        public static List<RootEntry> GetRoots(HandleRegistry registry)
        {
            var roots = new List<RootEntry>();

            // Try to get Game.Instance
            TryAddRoot(roots, registry, "Game.Instance", () => GetGameInstance());

            // Try to get ResourcesLibrary
            TryAddRoot(roots, registry, "ResourcesLibrary.BlueprintsByAssetId", () => GetBlueprintsByAssetId());

            // Try to get Player
            TryAddRoot(roots, registry, "Game.Instance.Player", () => GetPlayer());

            // Try to get UI context
            TryAddRoot(roots, registry, "Game.Instance.RootUiContext", () => GetRootUiContext());

            // Try to get State
            TryAddRoot(roots, registry, "Game.Instance.State", () => GetState());

            // Try to get Scene roots
            TryAddSceneRoots(roots, registry);

            return roots;
        }

        private static void TryAddRoot(List<RootEntry> roots, HandleRegistry registry, string name, Func<object> getter)
        {
            try
            {
                var obj = getter();
                if (obj != null)
                {
                    var handleId = registry.Register(obj);
                    var type = obj.GetType();
                    roots.Add(new RootEntry
                    {
                        Name = name,
                        HandleId = handleId.ToString(),
                        Type = type.FullName ?? type.Name,
                        AssemblyName = type.Assembly.GetName().Name
                    });
                }
            }
            catch (Exception ex)
            {
                Entry.Log($"Could not get root '{name}': {ex.Message}");
            }
        }

        private static void TryAddSceneRoots(List<RootEntry> roots, HandleRegistry registry)
        {
            try
            {
                // Get all root game objects in the active scene
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var sceneRoots = scene.GetRootGameObjects();
                
                // Register the array itself
                if (sceneRoots != null && sceneRoots.Length > 0)
                {
                    var handleId = registry.Register(sceneRoots);
                    roots.Add(new RootEntry
                    {
                        Name = $"Scene.{scene.name}.RootGameObjects",
                        HandleId = handleId.ToString(),
                        Type = sceneRoots.GetType().FullName,
                        AssemblyName = sceneRoots.GetType().Assembly.GetName().Name
                    });
                }
            }
            catch (Exception ex)
            {
                Entry.Log($"Could not get scene roots: {ex.Message}");
            }
        }

        private static object GetGameInstance()
        {
            // Try to find Game.Instance via reflection
            var gameType = FindType("Kingmaker.Game");
            if (gameType == null) return null;

            var instanceProp = gameType.GetProperty("Instance", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            
            return instanceProp?.GetValue(null, null);
        }

        private static object GetBlueprintsByAssetId()
        {
            // Try to find ResourcesLibrary.BlueprintsByAssetId
            var libType = FindType("Kingmaker.Blueprints.ResourcesLibrary");
            if (libType == null) return null;

            var field = libType.GetField("BlueprintsByAssetId",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            return field?.GetValue(null);
        }

        private static object GetPlayer()
        {
            var game = GetGameInstance();
            if (game == null) return null;

            var playerProp = game.GetType().GetProperty("Player",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            return playerProp?.GetValue(game, null);
        }

        private static object GetRootUiContext()
        {
            var game = GetGameInstance();
            if (game == null) return null;

            var prop = game.GetType().GetProperty("RootUiContext",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            return prop?.GetValue(game, null);
        }

        private static object GetState()
        {
            var game = GetGameInstance();
            if (game == null) return null;

            var prop = game.GetType().GetProperty("State",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            return prop?.GetValue(game, null);
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName);
                    if (type != null) return type;
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }
            return null;
        }
    }
}
