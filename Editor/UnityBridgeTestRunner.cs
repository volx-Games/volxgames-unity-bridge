using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace VolxGames.UnityBridge.Editor
{
    [InitializeOnLoad]
    internal static class UnityBridgeTestRunner
    {
        private static readonly object SyncRoot = new object();
        private static readonly TestRunnerCallbacks Callbacks = new TestRunnerCallbacks();
        private static TestRunnerApi api;

        static UnityBridgeTestRunner()
        {
            EditorApplication.delayCall += EnsureApi;
        }

        public static UnityBridgeTestReportSummary RunTests(string argument)
        {
            EnsureApi();
            if (api == null)
            {
                return Invalid("Unity Test Runner API is not available in this project.");
            }

            if (UnityBridgeTestTracker.IsRunActive())
            {
                return Invalid("A Unity test run is already in progress.");
            }

            var request = JsonUtility.FromJson<UnityBridgeRunTestsRequest>(argument ?? string.Empty) ?? new UnityBridgeRunTestsRequest();
            TestMode parsedTestMode;
            string testModeError;
            if (!TryParseTestMode(request.testMode, out parsedTestMode, out testModeError))
            {
                var invalidMode = Invalid(testModeError);
                UnityBridgeTestTracker.MarkRunFinished(invalidMode);
                return invalidMode;
            }

            var filter = new Filter
            {
                testMode = parsedTestMode,
                testNames = CleanStrings(request.testNames),
                groupNames = CleanStrings(request.groupNames),
                categoryNames = CleanStrings(request.categoryNames),
                assemblyNames = CleanStrings(request.assemblyNames)
            };

            var executionSettings = new ExecutionSettings(filter)
            {
                runSynchronously = request.runSynchronously
            };

            if (request.runSynchronously && parsedTestMode != TestMode.EditMode)
            {
                var invalidSynchronousMode = Invalid("runSynchronously is only supported for EditMode test runs.");
                UnityBridgeTestTracker.MarkRunFinished(invalidSynchronousMode);
                return invalidSynchronousMode;
            }

            string resolvedResultFilePath;
            string resultPathError;
            if (!TryResolveResultFilePath(request.resultFilePath, out resolvedResultFilePath, out resultPathError))
            {
                var invalidResultPath = Invalid(resultPathError);
                UnityBridgeTestTracker.MarkRunFinished(invalidResultPath);
                return invalidResultPath;
            }

            var startedAtUtc = DateTime.UtcNow.ToString("O");
            var pendingSummary = new UnityBridgeTestReportSummary
            {
                hasReport = false,
                running = true,
                testMode = NormalizeTestMode(parsedTestMode),
                message = "Unity test run started.",
                resultState = "Running",
                startedAtUtc = startedAtUtc,
                resultFilePath = resolvedResultFilePath ?? string.Empty,
                testNames = new List<string>(filter.testNames ?? Array.Empty<string>()),
                groupNames = new List<string>(filter.groupNames ?? Array.Empty<string>()),
                categoryNames = new List<string>(filter.categoryNames ?? Array.Empty<string>()),
                assemblyNames = new List<string>(filter.assemblyNames ?? Array.Empty<string>())
            };

            UnityBridgeTestTracker.MarkRunStarted(pendingSummary);

            try
            {
                var runId = api.Execute(executionSettings);
                pendingSummary.runId = runId;
                pendingSummary.message = request.runSynchronously
                    ? "Synchronous Unity test run started."
                    : "Asynchronous Unity test run started.";
                Callbacks.SetPendingResultFilePath(runId, resolvedResultFilePath);
                return pendingSummary;
            }
            catch (Exception exception)
            {
                var failed = Invalid(exception.Message);
                failed.testMode = NormalizeTestMode(parsedTestMode);
                failed.startedAtUtc = startedAtUtc;
                failed.finishedAtUtc = DateTime.UtcNow.ToString("O");
                failed.resultFilePath = resolvedResultFilePath ?? string.Empty;
                failed.testNames = pendingSummary.testNames;
                failed.groupNames = pendingSummary.groupNames;
                failed.categoryNames = pendingSummary.categoryNames;
                failed.assemblyNames = pendingSummary.assemblyNames;
                UnityBridgeTestTracker.MarkRunFinished(failed);
                return failed;
            }
        }

        private static void EnsureApi()
        {
            lock (SyncRoot)
            {
                if (api != null)
                {
                    return;
                }

                api = ScriptableObject.CreateInstance<TestRunnerApi>();
                api.RegisterCallbacks(Callbacks, 1000);
            }
        }

        private static bool TryParseTestMode(string value, out TestMode testMode, out string error)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                case "all":
                    testMode = TestMode.EditMode | TestMode.PlayMode;
                    error = null;
                    return true;
                case "editmode":
                case "edit":
                    testMode = TestMode.EditMode;
                    error = null;
                    return true;
                case "playmode":
                case "play":
                    testMode = TestMode.PlayMode;
                    error = null;
                    return true;
                default:
                    testMode = 0;
                    error = "Unknown testMode: " + value;
                    return false;
            }
        }

        private static string NormalizeTestMode(TestMode testMode)
        {
            if (testMode == (TestMode.EditMode | TestMode.PlayMode))
            {
                return "All";
            }

            if (testMode == TestMode.EditMode)
            {
                return "EditMode";
            }

            if (testMode == TestMode.PlayMode)
            {
                return "PlayMode";
            }

            return testMode.ToString();
        }

        private static string[] CleanStrings(List<string> values)
        {
            return values == null
                ? Array.Empty<string>()
                : values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
        }

        private static bool TryResolveResultFilePath(string value, out string resolvedPath, out string error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                resolvedPath = null;
                error = null;
                return true;
            }

            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Directory.GetCurrentDirectory();
            if (!Path.IsPathRooted(value))
            {
                resolvedPath = value.Replace("\\", "/");
                error = null;
                return true;
            }

            var fullPath = Path.GetFullPath(value);
            if (!fullPath.StartsWith(projectRoot, StringComparison.Ordinal))
            {
                resolvedPath = null;
                error = "resultFilePath must be relative to the project or inside the project root.";
                return false;
            }

            resolvedPath = fullPath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("\\", "/");
            error = null;
            return true;
        }

        private static UnityBridgeTestReportSummary Invalid(string message)
        {
            return new UnityBridgeTestReportSummary
            {
                hasReport = false,
                running = false,
                resultState = "InvalidRequest",
                message = message,
                testNames = new List<string>(),
                groupNames = new List<string>(),
                categoryNames = new List<string>(),
                assemblyNames = new List<string>()
            };
        }

        private sealed class TestRunnerCallbacks : IErrorCallbacks
        {
            private string currentRunId;
            private string currentResultFilePath;

            public void SetPendingResultFilePath(string runId, string resultFilePath)
            {
                currentRunId = runId;
                currentResultFilePath = resultFilePath;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var summary = new UnityBridgeTestReportSummary
                {
                    hasReport = result != null,
                    running = false,
                    runId = currentRunId,
                    testMode = UnityBridgeTestTracker.GetCurrentOrLastReport().testMode,
                    message = result == null ? "Unity test run finished without a result." : result.Message,
                    resultState = result == null ? "Unknown" : result.ResultState,
                    startedAtUtc = UnityBridgeTestTracker.GetCurrentOrLastReport().startedAtUtc,
                    finishedAtUtc = DateTime.UtcNow.ToString("O"),
                    durationSeconds = result == null ? 0d : result.Duration,
                    passCount = result == null ? 0 : result.PassCount,
                    failCount = result == null ? 0 : result.FailCount,
                    skipCount = result == null ? 0 : result.SkipCount,
                    inconclusiveCount = result == null ? 0 : result.InconclusiveCount,
                    assertCount = result == null ? 0 : result.AssertCount,
                    startedCount = UnityBridgeTestTracker.GetCurrentOrLastReport().startedCount,
                    finishedCount = UnityBridgeTestTracker.GetCurrentOrLastReport().finishedCount,
                    resultFilePath = currentResultFilePath ?? string.Empty,
                    testNames = UnityBridgeTestTracker.GetCurrentOrLastReport().testNames,
                    groupNames = UnityBridgeTestTracker.GetCurrentOrLastReport().groupNames,
                    categoryNames = UnityBridgeTestTracker.GetCurrentOrLastReport().categoryNames,
                    assemblyNames = UnityBridgeTestTracker.GetCurrentOrLastReport().assemblyNames
                };

                summary.totalCount = summary.passCount + summary.failCount + summary.skipCount + summary.inconclusiveCount;
                if (!string.IsNullOrWhiteSpace(currentResultFilePath) && result != null)
                {
                    EnsureDirectoryExists(currentResultFilePath);
                    TestRunnerApi.SaveResultToFile(result, currentResultFilePath);
                }

                UnityBridgeTestTracker.MarkRunFinished(summary);
                currentRunId = null;
                currentResultFilePath = null;
            }

            public void TestStarted(ITestAdaptor test)
            {
                UnityBridgeTestTracker.MarkTestStarted();
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                UnityBridgeTestTracker.MarkTestFinished();
            }

            public void OnError(string message)
            {
                var current = UnityBridgeTestTracker.GetCurrentOrLastReport();
                var failed = new UnityBridgeTestReportSummary
                {
                    hasReport = false,
                    running = false,
                    runId = currentRunId,
                    testMode = current.testMode,
                    message = message,
                    resultState = "Error",
                    startedAtUtc = current.startedAtUtc,
                    finishedAtUtc = DateTime.UtcNow.ToString("O"),
                    startedCount = current.startedCount,
                    finishedCount = current.finishedCount,
                    resultFilePath = currentResultFilePath ?? string.Empty,
                    testNames = current.testNames,
                    groupNames = current.groupNames,
                    categoryNames = current.categoryNames,
                    assemblyNames = current.assemblyNames
                };

                UnityBridgeTestTracker.MarkRunFinished(failed);
                currentRunId = null;
                currentResultFilePath = null;
            }

            private static void EnsureDirectoryExists(string relativeProjectPath)
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Directory.GetCurrentDirectory();
                var fullPath = Path.Combine(projectRoot, relativeProjectPath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }
    }
}
