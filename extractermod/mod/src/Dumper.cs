using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BlueprintDumper
{
    public static class Dumper
    {
        private static bool _started;
        private static int _running;
        private static readonly object _logLock = new object();
        private static SynchronizationContext _mainThreadContext;

        public static void StartOnce()
        {
            if (_started) return;
            _started = true;
            
            _mainThreadContext = SynchronizationContext.Current;
            LogToUnity("[BlueprintDumper] Initialized.");

            var host = new GameObject("BlueprintDumper");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<DumperBehaviour>();
            LogToUnity("[BlueprintDumper] Press F10 to dump blueprints.");

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                await RunDumpAsync("auto");
            });
        }

        public static void TriggerDump() => Task.Run(() => RunDumpAsync("manual"));

        private static async Task RunDumpAsync(string reasonTag)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                LogToUnity("[BlueprintDumper] Dump already running.");
                return;
            }

            string outDir = null;
            try
            {
                var stamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + "-" + reasonTag;
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                outDir = Path.Combine(localAppData, "Owlcat Games", "Warhammer 40000 Rogue Trader", "BlueprintDumps", stamp);
                Directory.CreateDirectory(outDir);

                Log($"Blueprint Dumper starting... output: {outDir}", outDir);

                var assemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();
                var ctx = ReflectionUtil.CreateContext(assemblies);

                Log("Loading blueprints from database...", outDir);
                var all = await BlueprintDatabaseUtil.LoadAllBlueprintsAsync(assemblies, ctx, msg => Log(msg, outDir));

                if (all.Count == 0)
                {
                    Log("Database empty, trying cache...", outDir);
                    var deadline = DateTime.UtcNow.AddSeconds(90);
                    while (DateTime.UtcNow < deadline)
                    {
                        if (ReflectionUtil.TryGetAllBlueprints(ctx, out all, out _)) break;
                        await Task.Delay(1000);
                    }
                }

                if (all.Count == 0)
                {
                    Log("FATAL: Could not locate blueprints.", outDir);
                    return;
                }

                Log($"Total blueprints: {all.Count}", outDir);

                var targets = Filter.EquipmentForDb(all);
                Log($"Equipment blueprints: {targets.Count}", outDir);

                var index = new List<object>();
                var flatPath = Path.Combine(outDir, "items_flat.jsonl");
                
                for (int i = 0; i < targets.Count; i++)
                {
                    if (i % 250 == 0) Log($"Processed {i}/{targets.Count}...", outDir);

                    try
                    {
                        var bp = targets[i];
                        var meta = ReflectionUtil.GetBlueprintMeta(bp);
                        var json = JsonUtil.ToFallbackJson(bp, meta);

                        var typeDir = Path.Combine(outDir, NamespaceToDir(meta.Namespace), Sanitize(meta.Type));
                        Directory.CreateDirectory(typeDir);

                        var fileName = $"{Sanitize(meta.Name)}_{meta.Guid}.jbp";
                        var filePath = Path.Combine(typeDir, fileName);
                        File.WriteAllText(filePath, json, new UTF8Encoding(false));

                        index.Add(new { meta.Guid, meta.Name, meta.Type, meta.Namespace, meta.FullType, file = GetRelativePath(outDir, filePath) });

                        try
                        {
                            var flat = ExtractUtil.ExtractFlatRecord(bp);
                            File.AppendAllText(flatPath, ExtractUtil.ToJsonLine(flat) + Environment.NewLine, new UTF8Encoding(false));
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to dump blueprint #{i}: {ex.Message}", outDir);
                    }

                    if (i % 50 == 0) await Task.Delay(1);
                }

                File.WriteAllText(Path.Combine(outDir, "index.json"),
                    Newtonsoft.Json.JsonConvert.SerializeObject(index, Newtonsoft.Json.Formatting.Indented));

                Log("Blueprint Dumper completed.", outDir);
            }
            catch (Exception ex)
            {
                LogToUnity($"[BlueprintDumper] Fatal error: {ex}");
                if (outDir != null)
                    try { File.AppendAllText(Path.Combine(outDir, "run.log"), $"[{DateTime.Now:HH:mm:ss}] FATAL: {ex}{Environment.NewLine}"); } catch { }
            }
            finally
            {
                Interlocked.Exchange(ref _running, 0);
            }
        }

        private static void LogToUnity(string message)
        {
            try
            {
                if (_mainThreadContext != null && SynchronizationContext.Current != _mainThreadContext)
                    _mainThreadContext.Post(_ => Debug.Log(message), null);
                else
                    Debug.Log(message);
            }
            catch { }
        }

        private static void Log(string message, string outDir)
        {
            LogToUnity($"[BlueprintDumper] {message}");
            if (outDir != null)
            {
                lock (_logLock)
                {
                    try { File.AppendAllText(Path.Combine(outDir, "run.log"), $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"); } catch { }
                }
            }
        }

        private static string Sanitize(string s) =>
            string.Join("_", (s ?? "unnamed").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        private static string NamespaceToDir(string ns)
        {
            if (string.IsNullOrWhiteSpace(ns)) return "(no_namespace)";
            var parts = ns.Split('.').Take(4).Select(Sanitize).ToArray();
            return parts.Length > 0 ? Path.Combine(parts) : "(no_namespace)";
        }
            
        private static string GetRelativePath(string relativeTo, string path)
        {
            if (!relativeTo.EndsWith(Path.DirectorySeparatorChar.ToString()))
                relativeTo += Path.DirectorySeparatorChar;
            return Uri.UnescapeDataString(new Uri(relativeTo).MakeRelativeUri(new Uri(path)).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

