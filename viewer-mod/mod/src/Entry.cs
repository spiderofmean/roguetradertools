using System;
using UnityEngine;

namespace ViewerMod
{
    /// <summary>
    /// Mod entry point for UnityModManager.
    /// </summary>
    public static class Entry
    {
        private static GameObject _host;

        /// <summary>
        /// Called by UnityModManager when the mod loads.
        /// </summary>
        public static void StartOnce()
        {
            Log("Initializing Viewer Mod...");

            _host = new GameObject("ViewerMod");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;
            _host.AddComponent<ViewerBehaviour>();

            Log("Viewer Mod initialized. Server starting on http://localhost:5000/");
        }

        internal static void Log(string message)
        {
            Debug.Log($"[ViewerMod] {message}");
        }

        internal static void LogError(string message)
        {
            Debug.LogError($"[ViewerMod] {message}");
        }
    }
}
