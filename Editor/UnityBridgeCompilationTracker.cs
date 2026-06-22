using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace VolxGames.UnityBridge.Editor
{
    internal static class UnityBridgeCompilationTracker
    {
        private const string CurrentKey = "UnityBridge.Unity.Mcp.Compilation.Current";
        private const string LastKey = "UnityBridge.Unity.Mcp.Compilation.Last";
        private static readonly object SyncRoot = new object();

        public static void MarkCompilationStarted()
        {
            lock (SyncRoot)
            {
                Save(CurrentKey, new UnityBridgeCompilationSummary
                {
                    hasReport = true,
                    running = true,
                    succeeded = false,
                    compilationId = Guid.NewGuid().ToString("N"),
                    message = "Compilation started.",
                    startedAtUtc = DateTime.UtcNow.ToString("O")
                });
            }
}

        public static void MarkAssemblyStarted(string assemblyName)
        {
            lock (SyncRoot)
            {
                var report = Load(CurrentKey) ?? new UnityBridgeCompilationSummary
                {
                    hasReport = true,
                    running = true,
                    succeeded = false,
                    compilationId = Guid.NewGuid().ToString("N"),
                    message = "Compilation started.",
                    startedAtUtc = DateTime.UtcNow.ToString("O")
                };

                if (!string.IsNullOrWhiteSpace(assemblyName) && !report.assemblyNames.Contains(assemblyName))
                {
                    report.assemblyNames.Add(assemblyName);
                }

                report.assemblyCount = report.assemblyNames.Count;
                Save(CurrentKey, report);
            }
        }

        public static void MarkAssemblyFinished(string assemblyName, CompilerMessage[] messages)
        {
            lock (SyncRoot)
            {
                var report = Load(CurrentKey);
                if (report == null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(assemblyName) && !report.completedAssemblyNames.Contains(assemblyName))
                {
                    report.completedAssemblyNames.Add(assemblyName);
                }

                report.completedAssemblyCount = report.completedAssemblyNames.Count;

                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        if (message.type == CompilerMessageType.Error)
                        {
                            report.totalErrors += 1;
                        }
                        else if (message.type == CompilerMessageType.Warning)
                        {
                            report.totalWarnings += 1;
                        }
                    }
                }

                Save(CurrentKey, report);
            }
        }

        public static void MarkCompilationFinished()
        {
            lock (SyncRoot)
            {
                var report = Load(CurrentKey) ?? new UnityBridgeCompilationSummary
                {
                    hasReport = true,
                    compilationId = Guid.NewGuid().ToString("N"),
                    startedAtUtc = DateTime.UtcNow.ToString("O")
                };

                report.running = false;
                report.finishedAtUtc = DateTime.UtcNow.ToString("O");
                report.completedAssemblyCount = Math.Max(report.completedAssemblyCount, report.assemblyCount);
                report.succeeded = report.totalErrors == 0;
                report.message = report.succeeded ? "Compilation finished successfully." : "Compilation finished with errors.";

                if (DateTime.TryParse(report.startedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startedAt) &&
                    DateTime.TryParse(report.finishedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var finishedAt))
                {
                    report.durationSeconds = Math.Max(0d, (finishedAt - startedAt).TotalSeconds);
                }

                Save(LastKey, report);
                SessionState.EraseString(CurrentKey);
            }
        }

        public static UnityBridgeCompilationSummary GetCurrentOrLastReport()
        {
            lock (SyncRoot)
            {
                return Load(CurrentKey) ?? Load(LastKey) ?? Empty("No compilation has been observed through the bridge yet.");
            }
        }

        public static UnityBridgeCompilationSummary GetLastReport()
        {
            lock (SyncRoot)
            {
                return Load(LastKey) ?? Empty("No compilation has finished through the bridge yet.");
            }
        }

        private static UnityBridgeCompilationSummary Empty(string message)
        {
            return new UnityBridgeCompilationSummary
            {
                hasReport = false,
                running = EditorApplication.isCompiling,
                succeeded = false,
                message = message
            };
        }

        private static UnityBridgeCompilationSummary Load(string key)
        {
            var json = SessionState.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var report = JsonUtility.FromJson<UnityBridgeCompilationSummary>(json);
                if (report.assemblyNames == null)
                {
                    report.assemblyNames = new System.Collections.Generic.List<string>();
                }

                if (report.completedAssemblyNames == null)
                {
                    report.completedAssemblyNames = new System.Collections.Generic.List<string>();
                }

                return report;
            }
            catch
            {
                return null;
            }
        }

        private static void Save(string key, UnityBridgeCompilationSummary report)
        {
            SessionState.SetString(key, JsonUtility.ToJson(report));
        }
    }
}
