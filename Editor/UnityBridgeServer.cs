using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace VolxGames.UnityBridge.Editor
{
    [InitializeOnLoad]
    internal static class UnityBridgeServer
    {
        private const string AutoStartPrefKey = "UnityBridge.Unity.Mcp.AutoStart";
        private const string PortPrefKey = "UnityBridge.Unity.Mcp.Port";
        private const string RestartAfterReloadSessionKey = "UnityBridge.Unity.Mcp.RestartAfterReload";
        private const int DefaultPort = 48761;
        private const int PortFallbackCount = 100;
        private const string RiskyActionConfirmation = "I understand this will modify the Unity project.";
        private const int MaxAutoStartRetryCount = 10;
        private const double AutoStartRetryDelaySeconds = 0.75;
        private const double RegistryHeartbeatIntervalSeconds = 10.0d;

        private static readonly object SyncRoot = new object();

        private static HttpListener listener;
        private static Thread listenerThread;
        private static string startedAtUtc;
        private static string lastReloadAtUtc = DateTime.UtcNow.ToString("O");
        private static readonly ConcurrentQueue<PendingCommand> PendingCommands = new ConcurrentQueue<PendingCommand>();
        private static readonly ConcurrentQueue<PendingMainThreadJob> PendingMainThreadJobs = new ConcurrentQueue<PendingMainThreadJob>();
        private static readonly object LogSync = new object();
        private static readonly List<UnityBridgeLogEntry> RecentLogs = new List<UnityBridgeLogEntry>();
        private static readonly object EventSync = new object();
        private static readonly List<UnityBridgeEventEntry> RecentEvents = new List<UnityBridgeEventEntry>();
        private const int MaxLogEntries = 200;
        private const int MaxEventEntries = 200;
        private static readonly TimeSpan DefaultMainThreadTimeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan BuildMainThreadTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan TestMainThreadTimeout = TimeSpan.FromMinutes(30);
        private static int cachedPort = DefaultPort;
        private static bool autoStartScheduled;
        private static int autoStartRetryCount;
        private static double nextAutoStartAttemptTime;
        private static double nextRegistryHeartbeatTime;
        private static string registryFilePath;
        private static volatile bool cachedEditorBusy;

        static UnityBridgeServer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += MarkReload;
            EditorApplication.quitting += Stop;
            EditorApplication.update += UpdateEditorBusyState;
            EditorApplication.update += DrainPendingCommands;
            EditorApplication.update += AutoStartTick;
            EditorApplication.update += RegistryHeartbeatTick;
            CompilationPipeline.compilationStarted += _ =>
            {
                UnityBridgeCompilationTracker.MarkCompilationStarted();
                RecordEvent("compilation", "Compilation started.", string.Empty);
                Debug.Log("[UnityBridge] Compilation started.");
            };
            CompilationPipeline.assemblyCompilationStarted += assemblyPath =>
            {
                UnityBridgeCompilationTracker.MarkAssemblyStarted(Path.GetFileNameWithoutExtension(assemblyPath));
            };
            CompilationPipeline.assemblyCompilationFinished += (assemblyPath, messages) =>
            {
                UnityBridgeCompilationTracker.MarkAssemblyFinished(Path.GetFileNameWithoutExtension(assemblyPath), messages);
            };
            CompilationPipeline.compilationFinished += _ =>
            {
                UnityBridgeCompilationTracker.MarkCompilationFinished();
                RecordEvent("compilation", "Compilation finished.", string.Empty);
                Debug.Log("[UnityBridge] Compilation finished.");
            };
            Application.logMessageReceivedThreaded += CaptureLog;
            Selection.selectionChanged += () =>
            {
                RecordEvent("selection", "Selection changed.", Selection.activeObject != null ? Selection.activeObject.name : string.Empty);
            };
            EditorApplication.playModeStateChanged += change =>
            {
                RecordEvent("playmode", "Play mode state changed.", change.ToString());
            };
            EditorApplication.hierarchyChanged += () =>
            {
                RecordEvent("hierarchy", "Hierarchy changed.", SceneSummary());
            };
            ScheduleAutoStartIfNeeded(0.1d);
        }

        public static bool IsRunning => listener != null && listener.IsListening;

        public static int BoundPort => cachedPort;

        public static int Port
        {
            get => EditorPrefs.GetInt(PortPrefKey, DefaultPort);
            set
            {
                var normalizedPort = NormalizePort(value);
                EditorPrefs.SetInt(PortPrefKey, normalizedPort);
                cachedPort = normalizedPort;
            }
        }

        public static bool AutoStart
        {
            get => EditorPrefs.GetBool(AutoStartPrefKey, true);
            set => EditorPrefs.SetBool(AutoStartPrefKey, value);
        }

        [MenuItem("Tools/Unity Bridge/Start")]
        public static void Start()
        {
            CancelScheduledAutoStart();

            lock (SyncRoot)
            {
                if (IsRunning)
                {
                    return;
                }

                try
                {
                    var requestedPort = NormalizePort(Port);
                    listener = StartListener(requestedPort, out cachedPort);
                    startedAtUtc = DateTime.UtcNow.ToString("O");
                    RegisterBridgeInstance();

                    listenerThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "UnityMcpBridgeListener"
                    };
                    listenerThread.Start();

                    Debug.Log($"[UnityBridge] Listening on http://127.0.0.1:{cachedPort}/");
                    if (cachedPort != requestedPort)
                    {
                        Debug.LogWarning($"[UnityBridge] Preferred port {requestedPort} was unavailable. Using {cachedPort} instead.");
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[UnityBridge] Failed to start: {exception}");
                    Stop();
                }
            }
        }

        [MenuItem("Tools/Unity Bridge/Stop")]
        public static void Stop()
        {
            lock (SyncRoot)
            {
                if (listener == null)
                {
                    return;
                }

                try
                {
                    listener.Stop();
                    listener.Close();
                }
                catch
                {
                    // Ignore shutdown races during assembly reload.
                }

                RemoveBridgeInstance();
                listener = null;
                listenerThread = null;
            }
        }

        private static HttpListener StartListener(int preferredPort, out int boundPort)
        {
            Exception lastException = null;
            var firstPort = NormalizePort(preferredPort);
            var lastPort = Math.Min(65535, firstPort + PortFallbackCount - 1);

            for (var port = firstPort; port <= lastPort; port++)
            {
                var candidate = new HttpListener();
                try
                {
                    candidate.Prefixes.Add($"http://127.0.0.1:{port}/");
                    candidate.Start();
                    boundPort = port;
                    return candidate;
                }
                catch (HttpListenerException exception)
                {
                    lastException = exception;
                    candidate.Close();
                }
                catch (SocketException exception)
                {
                    lastException = exception;
                    candidate.Close();
                }
                catch
                {
                    candidate.Close();
                    throw;
                }
            }

            throw new InvalidOperationException($"No available Unity Bridge port found in range {firstPort}-{lastPort}.", lastException);
        }

        private static int NormalizePort(int port)
        {
            return Math.Min(65535, Math.Max(1, port));
        }

        private static void ListenLoop()
        {
            while (listener != null)
            {
                HttpListenerContext context = null;
                try
                {
                    context = listener.GetContext();
                    var capturedContext = context;
                    ThreadPool.QueueUserWorkItem(_ => HandleContextSafely(capturedContext));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[UnityBridge] Listener error: {exception}");
                    if (context != null)
                    {
                        WriteJson(context.Response, 500, $"{{\"ok\":false,\"message\":{Quote(exception.Message)}}}");
                    }
                }
            }
        }

        private static void HandleContextSafely(HttpListenerContext context)
        {
            try
            {
                HandleContext(context);
            }
            catch (TimeoutException)
            {
                try
                {
                    WriteJson(context.Response, 503, UnityBridgeJson.SerializeObject(BuildBusyResponse(
                        "Unity Editor is busy and did not respond on the main thread in time. Retry after compilation, refresh, or reload finishes.")));
                }
                catch
                {
                    SafeClose(context.Response);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[UnityBridge] Request handling error: {exception}");
                try
                {
                    WriteJson(context.Response, 500, $"{{\"ok\":false,\"message\":{Quote(exception.Message)}}}");
                }
                catch
                {
                    SafeClose(context.Response);
                }
            }
        }

        private static void HandleContext(HttpListenerContext context)
        {
            var request = context.Request;
            switch (request.Url?.AbsolutePath)
            {
                case "/health" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildHealth(cachedPort, IsRunning, startedAtUtc))));
                    return;
                case "/state" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildState(lastReloadAtUtc))));
                    return;
                case "/project" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildProjectSummary())));
                    return;
                case "/runtime" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildRuntimeSummary())));
                    return;
                case "/build" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildBuildSummary())));
                    return;
                case "/build/last" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeBuildTracker.GetLastBuildReport())));
                    return;
                case "/build/player" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.BuildPlayerReport(ReadBody(request)), BuildMainThreadTimeout)));
                    return;
                case "/build/settings" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetBuildSettings(ReadBody(request), lastReloadAtUtc))));
                    return;
                case "/build/target" when request.HttpMethod == "POST":
                    if (!HasRequiredConfirmation(request))
                    {
                        WriteJson(context.Response, 400, $"{{\"ok\":false,\"message\":{Quote($"This endpoint requires x-unity-bridge-confirm: {RiskyActionConfirmation}")}}}");
                        return;
                    }

                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SwitchBuildTarget(ReadBody(request), lastReloadAtUtc), BuildMainThreadTimeout)));
                    return;
                case "/player-settings" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildPlayerSettingsSummary())));
                    return;
                case "/player-settings" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetPlayerSettings(ReadBody(request), lastReloadAtUtc))));
                    return;
                case "/project-tags-layers" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildProjectTagLayerSummary())));
                    return;
                case "/project-tags-layers" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetProjectTagLayers(ReadBody(request)))));
                    return;
                case "/quality-settings" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildQualitySettingsSummary())));
                    return;
                case "/quality-settings" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetQualitySettings(ReadBody(request)))));
                    return;
                case "/audio-settings" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildAudioSettingsSummary())));
                    return;
                case "/audio-settings" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetAudioSettings(ReadBody(request)))));
                    return;
                case "/time-settings" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildTimeSettingsSummary())));
                    return;
                case "/time-settings" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetTimeSettings(ReadBody(request)))));
                    return;
                case "/physics-settings" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildPhysicsSettingsSummary())));
                    return;
                case "/physics-settings" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetPhysicsSettings(ReadBody(request)))));
                    return;
                case "/playerprefs/get" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.PlayerPrefsGet(ReadBody(request)))));
                    return;
                case "/importers/texture" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildTextureImporterSummary(ReadPathRequest(request)))));
                    return;
                case "/importers/texture/set" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeMutations.SetTextureImporter(ReadBody(request)))));
                    return;
                case "/build/scenes" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildProjectSummary().buildScenes)));
                    return;
                case "/build/scenes" when request.HttpMethod == "POST":
                    if (!HasRequiredConfirmation(request))
                    {
                        WriteJson(context.Response, 400, $"{{\"ok\":false,\"message\":{Quote($"This endpoint requires x-unity-bridge-confirm: {RiskyActionConfirmation}")}}}");
                        return;
                    }

                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ExecuteBuildSceneMutation(ReadBody(request), "set"))));
                    return;
                case "/build/scenes/add" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ExecuteBuildSceneMutation(ReadBody(request), "add"))));
                    return;
                case "/build/scenes/remove" when request.HttpMethod == "POST":
                    if (!HasRequiredConfirmation(request))
                    {
                        WriteJson(context.Response, 400, $"{{\"ok\":false,\"message\":{Quote($"This endpoint requires x-unity-bridge-confirm: {RiskyActionConfirmation}")}}}");
                        return;
                    }

                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ExecuteBuildSceneMutation(ReadBody(request), "remove"))));
                    return;
                case "/tests" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeTestTracker.GetCurrentOrLastReport())));
                    return;
                case "/tests/last" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeTestTracker.GetLastReport())));
                    return;
                case "/tests/run" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeTestRunner.RunTests(ReadBody(request)), TestMainThreadTimeout)));
                    return;
                case "/compilation" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeCompilationTracker.GetCurrentOrLastReport())));
                    return;
                case "/compilation/last" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeCompilationTracker.GetLastReport())));
                    return;
                case "/editor/windows" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildEditorWindows())));
                    return;
                case "/inspector/selection" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildInspectorSelection())));
                    return;
                case "/console/selection" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildConsoleSelection())));
                    return;
                case "/context/current" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildCurrentContext(lastReloadAtUtc))));
                    return;
                case "/packages" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildPackages())));
                    return;
                case "/packages/operations" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgePackageTracker.GetCurrentOrLastOperation())));
                    return;
                case "/packages/operations/last" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgePackageTracker.GetLastOperation())));
                    return;
                case "/packages/add" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ExecutePackageMutation(ReadBody(request), "add"))));
                    return;
                case "/packages/remove" when request.HttpMethod == "POST":
                    if (!HasRequiredConfirmation(request))
                    {
                        WriteJson(context.Response, 400, $"{{\"ok\":false,\"message\":{Quote($"This endpoint requires x-unity-bridge-confirm: {RiskyActionConfirmation}")}}}");
                        return;
                    }

                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ExecutePackageMutation(ReadBody(request), "remove"))));
                    return;
                case "/packages/embed" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ExecutePackageMutation(ReadBody(request), "embed"))));
                    return;
                case "/packages/resolve" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ExecutePackageMutation(string.Empty, "resolve"))));
                    return;
                case "/scene-stats" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildSceneStats())));
                    return;
                case "/prefab-stage" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildPrefabStageInfo())));
                    return;
                case "/selection" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildSelection())));
                    return;
                case "/active-object" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildActiveObjectDetails())));
                    return;
                case "/scenes" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildSceneAssets())));
                    return;
                case "/commands" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeCommands.ListAvailableCommands())));
                    return;
                case "/hierarchy" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        UnityBridgeStateBuilder.BuildHierarchy())));
                    return;
                case "/inspect/hierarchy" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        InspectHierarchyObject(ReadPathRequest(request)))));
                    return;
                case "/inspect/asset" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        InspectAsset(ReadPathRequest(request)))));
                    return;
                case "/asset/dependencies" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        AssetDependencies(ReadAssetDependencyRequest(request)))));
                    return;
                case "/asset/resolve" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        ResolveAssetPath(ReadAssetPathRequest(request)))));
                    return;
                case "/search/assets" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        SearchAssets(ReadAssetSearchRequest(request)))));
                    return;
                case "/search/hierarchy" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        SearchHierarchy(ReadHierarchySearchRequest(request)))));
                    return;
                case "/runtime/query" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        QueryRuntimeObjects(ReadRuntimeQueryRequest(request)))));
                    return;
                case "/runtime/inspect" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(ExecuteOnMainThread(() =>
                        InspectRuntimeObject(ReadPathRequest(request)))));
                    return;
                case "/logs" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(GetLogsSnapshot()));
                    return;
                case "/logs/query" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(FilterLogs(ReadLogQueryRequest(request))));
                    return;
                case "/events" when request.HttpMethod == "GET":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(GetEventsSnapshot()));
                    return;
                case "/events/query" when request.HttpMethod == "POST":
                    WriteJson(context.Response, 200, UnityBridgeJson.SerializeObject(FilterEvents(ReadEventQueryRequest(request))));
                    return;
                case "/stream/logs" when request.HttpMethod == "GET":
                    StreamLogs(context);
                    return;
                case "/stream/events" when request.HttpMethod == "GET":
                    StreamEvents(context);
                    return;
                case "/stream/runtime" when request.HttpMethod == "GET":
                    StreamRuntime(context);
                    return;
                case "/command" when request.HttpMethod == "POST":
                    var body = ReadBody(request);
                    var response = ExecuteCommandOnMainThread(body);
                    WriteJson(context.Response, response.ok ? 200 : 400, UnityBridgeJson.SerializeObject(response));
                    return;
                default:
                    WriteJson(context.Response, 404, "{\"ok\":false,\"message\":\"Not found.\"}");
                    return;
            }
        }

        private static UnityBridgeCommandResponse ExecuteCommandOnMainThread(string body)
        {
            var payload = JsonUtility.FromJson<UnityBridgeCommandRequest>(body);
            var pendingCommand = new PendingCommand(payload);
            PendingCommands.Enqueue(pendingCommand);
            var timeout = payload != null && string.Equals(payload.name, "build_player", StringComparison.Ordinal)
                ? BuildMainThreadTimeout
                : payload != null && string.Equals(payload.name, "run_tests", StringComparison.Ordinal)
                ? TestMainThreadTimeout
                : DefaultMainThreadTimeout;

            if (pendingCommand.Complete.WaitOne(timeout))
            {
                return pendingCommand.Response;
            }

            pendingCommand.Cancelled = true;
            return new UnityBridgeCommandResponse
            {
                ok = false,
                busy = IsEditorBusy(),
                message = IsEditorBusy()
                    ? "Unity Editor is busy and did not handle the command on the main thread in time. Retry after compilation, refresh, or reload finishes."
                    : "Command timed out before Unity main thread handled it."
            };
        }

        private static T ExecuteOnMainThread<T>(Func<T> callback)
        {
            return ExecuteOnMainThread(callback, DefaultMainThreadTimeout);
        }

        private static T ExecuteOnMainThread<T>(Func<T> callback, TimeSpan timeout)
        {
            var job = new PendingMainThreadJob(() => callback());
            PendingMainThreadJobs.Enqueue(job);

            if (!job.Complete.WaitOne(timeout))
            {
                job.Cancelled = true;
                throw new TimeoutException("Main thread request timed out.");
            }

            if (job.Exception != null)
            {
                throw job.Exception;
            }

            return (T)job.Result;
        }

        private static bool IsEditorBusy()
        {
            return cachedEditorBusy;
        }

        private static UnityBridgeBusyResponse BuildBusyResponse(string message)
        {
            return new UnityBridgeBusyResponse
            {
                ok = false,
                busy = IsEditorBusy(),
                message = message
            };
        }

        private static string ReadBody(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static bool HasRequiredConfirmation(HttpListenerRequest request)
        {
            var confirm = request.Headers["x-unity-bridge-confirm"];
            return string.Equals(confirm, RiskyActionConfirmation, StringComparison.Ordinal);
        }

        private static string ReadPathRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            var payload = JsonUtility.FromJson<UnityBridgePathRequest>(body);
            return payload != null ? payload.path : null;
        }

        private static UnityBridgeAssetSearchRequest ReadAssetSearchRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            return JsonUtility.FromJson<UnityBridgeAssetSearchRequest>(body);
        }

        private static UnityBridgeHierarchySearchRequest ReadHierarchySearchRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            return JsonUtility.FromJson<UnityBridgeHierarchySearchRequest>(body);
        }

        private static UnityBridgeLogQueryRequest ReadLogQueryRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            return JsonUtility.FromJson<UnityBridgeLogQueryRequest>(body);
        }

        private static UnityBridgeEventQueryRequest ReadEventQueryRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            return JsonUtility.FromJson<UnityBridgeEventQueryRequest>(body);
        }

        private static UnityBridgeRuntimeQueryRequest ReadRuntimeQueryRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            return JsonUtility.FromJson<UnityBridgeRuntimeQueryRequest>(body);
        }

        private static UnityBridgeAssetDependencyRequest ReadAssetDependencyRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            return JsonUtility.FromJson<UnityBridgeAssetDependencyRequest>(body);
        }

        private static UnityBridgeAssetPathRequest ReadAssetPathRequest(HttpListenerRequest request)
        {
            var body = ReadBody(request);
            return JsonUtility.FromJson<UnityBridgeAssetPathRequest>(body);
        }

        private static void WriteJson(HttpListenerResponse response, int statusCode, string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }

        private static void MarkReload()
        {
            lastReloadAtUtc = DateTime.UtcNow.ToString("O");
            ScheduleAutoStartIfNeeded(AutoStartRetryDelaySeconds);
        }

        private static void HandleBeforeAssemblyReload()
        {
            SessionState.SetBool(RestartAfterReloadSessionKey, IsRunning || AutoStart);
            Stop();
        }

        private static void ScheduleAutoStartIfNeeded(double delaySeconds)
        {
            if (!(AutoStart || SessionState.GetBool(RestartAfterReloadSessionKey, false)) || IsRunning)
            {
                return;
            }

            autoStartScheduled = true;
            autoStartRetryCount = 0;
            nextAutoStartAttemptTime = EditorApplication.timeSinceStartup + Math.Max(0.0d, delaySeconds);
        }

        private static void AutoStartTick()
        {
            if (!autoStartScheduled || IsRunning || EditorApplication.timeSinceStartup < nextAutoStartAttemptTime)
            {
                return;
            }

            Start();
            if (IsRunning)
            {
                CancelScheduledAutoStart();
                SessionState.EraseBool(RestartAfterReloadSessionKey);
                return;
            }

            autoStartRetryCount++;
            if (autoStartRetryCount >= MaxAutoStartRetryCount)
            {
                Debug.LogWarning("[UnityBridge] Auto-start retries exhausted. Use Tools/Unity Bridge/Start to retry manually.");
                CancelScheduledAutoStart();
                return;
            }

            nextAutoStartAttemptTime = EditorApplication.timeSinceStartup + AutoStartRetryDelaySeconds;
        }

        private static void CancelScheduledAutoStart()
        {
            autoStartScheduled = false;
            autoStartRetryCount = 0;
            nextAutoStartAttemptTime = 0.0d;
        }

        private static void UpdateEditorBusyState()
        {
            cachedEditorBusy = EditorApplication.isCompiling || EditorApplication.isUpdating;
        }

        private static void RegistryHeartbeatTick()
        {
            if (!IsRunning || EditorApplication.timeSinceStartup < nextRegistryHeartbeatTime)
            {
                return;
            }

            RegisterBridgeInstance();
        }

        private static void RegisterBridgeInstance()
        {
            try
            {
                var projectPath = ProjectRootPath();
                registryFilePath = Path.Combine(RegistryDirectory(), HashPath(projectPath) + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(registryFilePath));
                var now = DateTime.UtcNow.ToString("O");
                var json = "{" +
                           $"\"projectName\":{Quote(Application.productName)}," +
                           $"\"projectPath\":{Quote(projectPath)}," +
                           $"\"url\":{Quote($"http://127.0.0.1:{cachedPort}")}," +
                           $"\"port\":{cachedPort}," +
                           $"\"processId\":{System.Diagnostics.Process.GetCurrentProcess().Id}," +
                           $"\"unityVersion\":{Quote(Application.unityVersion)}," +
                           $"\"startedAtUtc\":{Quote(startedAtUtc)}," +
                           $"\"lastSeenUtc\":{Quote(now)}" +
                           "}";
                var temporaryPath = registryFilePath + ".tmp";
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(registryFilePath))
                {
                    File.Replace(temporaryPath, registryFilePath, null);
                }
                else
                {
                    File.Move(temporaryPath, registryFilePath);
                }
                nextRegistryHeartbeatTime = EditorApplication.timeSinceStartup + RegistryHeartbeatIntervalSeconds;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UnityBridge] Failed to update bridge registry: {exception.Message}");
                nextRegistryHeartbeatTime = EditorApplication.timeSinceStartup + RegistryHeartbeatIntervalSeconds;
            }
        }

        private static void RemoveBridgeInstance()
        {
            if (string.IsNullOrEmpty(registryFilePath))
            {
                return;
            }

            try
            {
                if (File.Exists(registryFilePath))
                {
                    File.Delete(registryFilePath);
                }
            }
            catch
            {
                // Best-effort cleanup; stale entries are ignored by MCP discovery.
            }

            registryFilePath = null;
        }

        private static string RegistryDirectory()
        {
            string applicationDataRoot;
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    applicationDataRoot = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                        "Library",
                        "Application Support");
                    break;
                case RuntimePlatform.WindowsEditor:
                    applicationDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    break;
                default:
                    applicationDataRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                    if (string.IsNullOrEmpty(applicationDataRoot))
                    {
                        applicationDataRoot = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                            ".config");
                    }
                    break;
            }

            return Path.Combine(
                applicationDataRoot,
                "VolxGames",
                "UnityBridge",
                "instances");
        }

        private static string ProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string HashPath(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void DrainPendingCommands()
        {
            while (PendingMainThreadJobs.TryDequeue(out var pendingJob))
            {
                try
                {
                    if (!pendingJob.Cancelled)
                    {
                        pendingJob.Result = pendingJob.Callback();
                    }
                }
                catch (Exception exception)
                {
                    pendingJob.Exception = exception;
                }
                finally
                {
                    pendingJob.Complete.Set();
                }
            }

            while (PendingCommands.TryDequeue(out var pendingCommand))
            {
                try
                {
                    if (!pendingCommand.Cancelled)
                    {
                        pendingCommand.Response = UnityBridgeCommands.Execute(pendingCommand.Request, lastReloadAtUtc);
                    }
                }
                catch (Exception exception)
                {
                    pendingCommand.Response = new UnityBridgeCommandResponse
                    {
                        ok = false,
                        message = exception.Message,
                        state = UnityBridgeStateBuilder.BuildState(lastReloadAtUtc)
                    };
                }
                finally
                {
                    pendingCommand.Complete.Set();
                }
            }
        }

        private static void CaptureLog(string condition, string stackTrace, LogType type)
        {
            lock (LogSync)
            {
                RecentLogs.Add(new UnityBridgeLogEntry
                {
                    level = type.ToString(),
                    message = condition,
                    stackTrace = stackTrace,
                    timestampUtc = DateTime.UtcNow.ToString("O")
                });

                if (RecentLogs.Count > MaxLogEntries)
                {
                    RecentLogs.RemoveRange(0, RecentLogs.Count - MaxLogEntries);
                }
            }
        }

        private static List<UnityBridgeLogEntry> GetLogsSnapshot()
        {
            lock (LogSync)
            {
                return new List<UnityBridgeLogEntry>(RecentLogs);
            }
        }

        private static List<UnityBridgeLogEntry> FilterLogs(UnityBridgeLogQueryRequest request)
        {
            request = request ?? new UnityBridgeLogQueryRequest();

            lock (LogSync)
            {
                IEnumerable<UnityBridgeLogEntry> logs = RecentLogs;
                if (!string.IsNullOrWhiteSpace(request.level))
                {
                    logs = logs.Where(log => string.Equals(log.level, request.level, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(request.query))
                {
                    logs = logs.Where(log =>
                        (log.message != null && log.message.IndexOf(request.query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (log.stackTrace != null && log.stackTrace.IndexOf(request.query, StringComparison.OrdinalIgnoreCase) >= 0));
                }

                var limit = request.limit > 0 ? request.limit : 100;
                return logs.Reverse().Take(limit).Reverse().ToList();
            }
        }

        private static List<UnityBridgeEventEntry> GetEventsSnapshot()
        {
            lock (EventSync)
            {
                return new List<UnityBridgeEventEntry>(RecentEvents);
            }
        }

        private static List<UnityBridgeEventEntry> FilterEvents(UnityBridgeEventQueryRequest request)
        {
            request = request ?? new UnityBridgeEventQueryRequest();

            lock (EventSync)
            {
                IEnumerable<UnityBridgeEventEntry> eventsList = RecentEvents;
                if (!string.IsNullOrWhiteSpace(request.type))
                {
                    eventsList = eventsList.Where(entry => string.Equals(entry.type, request.type, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(request.query))
                {
                    eventsList = eventsList.Where(entry =>
                        (entry.message != null && entry.message.IndexOf(request.query, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (entry.context != null && entry.context.IndexOf(request.query, StringComparison.OrdinalIgnoreCase) >= 0));
                }

                var limit = request.limit > 0 ? request.limit : 100;
                return eventsList.Reverse().Take(limit).Reverse().ToList();
            }
        }

        private static UnityBridgeRuntimeQueryResult QueryRuntimeObjects(UnityBridgeRuntimeQueryRequest request)
        {
            return UnityBridgeStateBuilder.QueryRuntimeObjects(request);
        }

        private static UnityBridgeObjectDetails InspectHierarchyObject(string hierarchyPath)
        {
            return UnityBridgeStateBuilder.BuildHierarchyObjectDetails(hierarchyPath);
        }

        private static UnityBridgeObjectDetails InspectAsset(string assetPath)
        {
            return UnityBridgeStateBuilder.BuildAssetDetails(assetPath);
        }

        private static UnityBridgeObjectDetails InspectRuntimeObject(string hierarchyPath)
        {
            return UnityBridgeStateBuilder.BuildRuntimeObjectDetails(hierarchyPath);
        }

        private static UnityBridgeCommandResponse ExecutePackageMutation(string body, string action)
        {
            switch (action)
            {
                case "add":
                    return UnityBridgeMutations.AddPackage(body, lastReloadAtUtc);
                case "remove":
                    return UnityBridgeMutations.RemovePackage(body, lastReloadAtUtc);
                case "embed":
                    return UnityBridgeMutations.EmbedPackage(body, lastReloadAtUtc);
                case "resolve":
                    return UnityBridgeMutations.ResolvePackages(lastReloadAtUtc);
                default:
                    return new UnityBridgeCommandResponse
                    {
                        ok = false,
                        message = "Unknown package mutation action: " + action,
                        state = UnityBridgeStateBuilder.BuildState(lastReloadAtUtc)
                    };
            }
        }

        private static UnityBridgeCommandResponse ExecuteBuildSceneMutation(string body, string action)
        {
            switch (action)
            {
                case "set":
                    return UnityBridgeMutations.SetBuildScenes(body, lastReloadAtUtc);
                case "add":
                    return UnityBridgeMutations.AddBuildScene(body, lastReloadAtUtc);
                case "remove":
                    return UnityBridgeMutations.RemoveBuildScene(body, lastReloadAtUtc);
                default:
                    return new UnityBridgeCommandResponse
                    {
                        ok = false,
                        message = "Unknown build scene action: " + action,
                        state = UnityBridgeStateBuilder.BuildState(lastReloadAtUtc)
                    };
            }
        }

        private static UnityBridgeAssetDependencyResult AssetDependencies(UnityBridgeAssetDependencyRequest request)
        {
            if (request == null)
            {
                return new UnityBridgeAssetDependencyResult();
            }

            return UnityBridgeStateBuilder.BuildAssetDependencies(request.path, request.recursive);
        }

        private static UnityBridgeAssetPathResult ResolveAssetPath(UnityBridgeAssetPathRequest request)
        {
            if (request == null)
            {
                return new UnityBridgeAssetPathResult();
            }

            return UnityBridgeStateBuilder.ResolveAssetPath(request.path, request.guid);
        }

        private static UnityBridgeAssetSearchResult SearchAssets(UnityBridgeAssetSearchRequest request)
        {
            if (request == null)
            {
                return new UnityBridgeAssetSearchResult();
            }

            return UnityBridgeStateBuilder.SearchAssetsResult(request.query, request.type);
        }

        private static UnityBridgeHierarchySearchResult SearchHierarchy(UnityBridgeHierarchySearchRequest request)
        {
            return UnityBridgeStateBuilder.SearchHierarchy(request);
        }

        private static string Quote(string value)
        {
            return UnityBridgeJson.SerializeObject(value);
        }

        private static void StreamLogs(HttpListenerContext context)
        {
            StreamList(context.Response, "log", () => GetLogsSnapshot().Cast<object>().ToList());
        }

        private static void StreamEvents(HttpListenerContext context)
        {
            StreamList(context.Response, "event", () => GetEventsSnapshot().Cast<object>().ToList());
        }

        private static void StreamRuntime(HttpListenerContext context)
        {
            PrepareEventStream(context.Response);
            var response = context.Response;
            var stream = response.OutputStream;
            string previousPayload = null;
            var heartbeatAt = DateTime.UtcNow;

            try
            {
                while (listener != null && response.OutputStream.CanWrite)
                {
                    var runtime = ExecuteOnMainThread(() => UnityBridgeStateBuilder.BuildRuntimeSummary());
                    var payload = UnityBridgeJson.SerializeObject(runtime);
                    if (!string.Equals(payload, previousPayload, StringComparison.Ordinal))
                    {
                        WriteEvent(stream, "runtime", payload);
                        previousPayload = payload;
                    }

                    if ((DateTime.UtcNow - heartbeatAt).TotalSeconds >= 5)
                    {
                        WriteHeartbeat(stream);
                        heartbeatAt = DateTime.UtcNow;
                    }

                    Thread.Sleep(250);
                }
            }
            catch
            {
                // Client disconnected or listener stopped.
            }
            finally
            {
                SafeClose(response);
            }
        }

        private static void StreamList(HttpListenerResponse response, string eventName, Func<List<object>> snapshotProvider)
        {
            PrepareEventStream(response);
            var stream = response.OutputStream;
            var cursor = snapshotProvider().Count;
            var heartbeatAt = DateTime.UtcNow;

            try
            {
                while (listener != null && response.OutputStream.CanWrite)
                {
                    var snapshot = snapshotProvider();
                    if (snapshot.Count > cursor)
                    {
                        for (var index = cursor; index < snapshot.Count; index++)
                        {
                            WriteEvent(stream, eventName, UnityBridgeJson.SerializeObject(snapshot[index]));
                        }

                        cursor = snapshot.Count;
                    }

                    if ((DateTime.UtcNow - heartbeatAt).TotalSeconds >= 5)
                    {
                        WriteHeartbeat(stream);
                        heartbeatAt = DateTime.UtcNow;
                    }

                    Thread.Sleep(250);
                }
            }
            catch
            {
                // Client disconnected or listener stopped.
            }
            finally
            {
                SafeClose(response);
            }
        }

        private static void PrepareEventStream(HttpListenerResponse response)
        {
            response.StatusCode = 200;
            response.ContentType = "text/event-stream";
            response.ContentEncoding = Encoding.UTF8;
            response.SendChunked = true;
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["Connection"] = "keep-alive";
        }

        private static void WriteEvent(Stream stream, string eventName, string payload)
        {
            var message = "event: " + eventName + "\n" +
                          "data: " + payload + "\n\n";
            var bytes = Encoding.UTF8.GetBytes(message);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private static void WriteHeartbeat(Stream stream)
        {
            var bytes = Encoding.UTF8.GetBytes(": heartbeat\n\n");
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private static void SafeClose(HttpListenerResponse response)
        {
            try
            {
                response.OutputStream.Close();
            }
            catch
            {
            }

            try
            {
                response.Close();
            }
            catch
            {
            }
        }

        private static void RecordEvent(string type, string message, string context)
        {
            lock (EventSync)
            {
                RecentEvents.Add(new UnityBridgeEventEntry
                {
                    type = type,
                    message = message,
                    context = context,
                    timestampUtc = DateTime.UtcNow.ToString("O")
                });

                if (RecentEvents.Count > MaxEventEntries)
                {
                    RecentEvents.RemoveRange(0, RecentEvents.Count - MaxEventEntries);
                }
            }
        }

        private static string SceneSummary()
        {
            try
            {
                return UnityBridgeStateBuilder.BuildState(lastReloadAtUtc).activeScene;
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class PendingCommand
        {
            public PendingCommand(UnityBridgeCommandRequest request)
            {
                Request = request;
                Complete = new ManualResetEvent(false);
            }

            public UnityBridgeCommandRequest Request { get; }

            public ManualResetEvent Complete { get; }

            public UnityBridgeCommandResponse Response { get; set; }

            public bool Cancelled { get; set; }
        }

        private sealed class PendingMainThreadJob
        {
            public PendingMainThreadJob(Func<object> callback)
            {
                Callback = callback;
                Complete = new ManualResetEvent(false);
            }

            public Func<object> Callback { get; }

            public ManualResetEvent Complete { get; }

            public object Result { get; set; }

            public Exception Exception { get; set; }

            public bool Cancelled { get; set; }
        }
    }
}
