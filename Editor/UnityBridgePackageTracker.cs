using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace VolxGames.UnityBridge.Editor
{
    [InitializeOnLoad]
    internal static class UnityBridgePackageTracker
    {
        private static readonly object SyncRoot = new object();
        private static Request activeRequest;
        private static UnityBridgePackageOperationSummary currentOperation;
        private static UnityBridgePackageOperationSummary lastOperation;

        static UnityBridgePackageTracker()
        {
            EditorApplication.update += PollActiveRequest;
        }

        public static bool IsOperationRunning
        {
            get
            {
                lock (SyncRoot)
                {
                    return activeRequest != null && currentOperation != null && currentOperation.running;
                }
            }
        }

        public static UnityBridgePackageOperationSummary StartOperation(string action, Request request, IEnumerable<string> packageIds, IEnumerable<string> removePackageIds)
        {
            lock (SyncRoot)
            {
                if (activeRequest != null && !activeRequest.IsCompleted)
                {
                    return Invalid("A package manager operation is already in progress.");
                }

                activeRequest = request;
                currentOperation = new UnityBridgePackageOperationSummary
                {
                    hasOperation = true,
                    running = true,
                    succeeded = false,
                    operationId = Guid.NewGuid().ToString("N"),
                    action = action,
                    status = "InProgress",
                    message = "Package operation started.",
                    startedAtUtc = DateTime.UtcNow.ToString("O"),
                    packageIds = Clean(packageIds),
                    removePackageIds = Clean(removePackageIds)
                };

                return Clone(currentOperation);
            }
        }

        public static UnityBridgePackageOperationSummary CompleteImmediateOperation(string action, bool succeeded, string message, IEnumerable<string> packageIds, IEnumerable<string> removePackageIds)
        {
            lock (SyncRoot)
            {
                var now = DateTime.UtcNow.ToString("O");
                currentOperation = null;
                activeRequest = null;
                lastOperation = new UnityBridgePackageOperationSummary
                {
                    hasOperation = true,
                    running = false,
                    succeeded = succeeded,
                    operationId = Guid.NewGuid().ToString("N"),
                    action = action,
                    status = succeeded ? "Success" : "Failure",
                    message = message,
                    startedAtUtc = now,
                    finishedAtUtc = now,
                    packageIds = Clean(packageIds),
                    removePackageIds = Clean(removePackageIds),
                    packages = UnityBridgeStateBuilder.BuildPackages()
                };

                return Clone(lastOperation);
            }
        }

        public static UnityBridgePackageOperationSummary GetCurrentOrLastOperation()
        {
            lock (SyncRoot)
            {
                return Clone(currentOperation ?? lastOperation ?? Empty("No package operation has been started through the bridge yet."));
            }
        }

        public static UnityBridgePackageOperationSummary GetLastOperation()
        {
            lock (SyncRoot)
            {
                return Clone(lastOperation ?? Empty("No package operation has been completed through the bridge yet."));
            }
        }

        private static void PollActiveRequest()
        {
            Request requestSnapshot;
            UnityBridgePackageOperationSummary operationSnapshot;

            lock (SyncRoot)
            {
                requestSnapshot = activeRequest;
                operationSnapshot = currentOperation;
            }

            if (requestSnapshot == null || operationSnapshot == null || !requestSnapshot.IsCompleted)
            {
                return;
            }

            var completed = BuildCompletedSummary(requestSnapshot, operationSnapshot);
            lock (SyncRoot)
            {
                activeRequest = null;
                currentOperation = null;
                lastOperation = completed;
            }
        }

        private static UnityBridgePackageOperationSummary BuildCompletedSummary(Request request, UnityBridgePackageOperationSummary operation)
        {
            var summary = Clone(operation);
            summary.running = false;
            summary.finishedAtUtc = DateTime.UtcNow.ToString("O");
            summary.status = request.Status.ToString();
            summary.succeeded = request.Status == StatusCode.Success;
            summary.errorCode = request.Error != null ? (int)request.Error.errorCode : 0;
            summary.errorMessage = request.Error != null ? request.Error.message : string.Empty;
            summary.packages = ResolvePackagesForRequest(request);
            summary.message = BuildMessage(summary);
            return summary;
        }

        private static List<UnityBridgePackageInfo> ResolvePackagesForRequest(Request request)
        {
            switch (request)
            {
                case AddRequest addRequest when addRequest.Status == StatusCode.Success && addRequest.Result != null:
                    return new List<UnityBridgePackageInfo> { ToPackageInfo(addRequest.Result) };
                case EmbedRequest embedRequest when embedRequest.Status == StatusCode.Success && embedRequest.Result != null:
                    return new List<UnityBridgePackageInfo> { ToPackageInfo(embedRequest.Result) };
                case AddAndRemoveRequest addAndRemoveRequest when addAndRemoveRequest.Status == StatusCode.Success && addAndRemoveRequest.Result != null:
                    return addAndRemoveRequest.Result.Select(ToPackageInfo).OrderBy(package => package.name).ToList();
                default:
                    return UnityBridgeStateBuilder.BuildPackages();
            }
        }

        private static string BuildMessage(UnityBridgePackageOperationSummary summary)
        {
            if (summary.succeeded)
            {
                return "Package operation succeeded: " + summary.action;
            }

            if (!string.IsNullOrWhiteSpace(summary.errorMessage))
            {
                return "Package operation failed: " + summary.errorMessage;
            }

            return "Package operation finished with status " + summary.status + ".";
        }

        private static UnityBridgePackageInfo ToPackageInfo(UnityEditor.PackageManager.PackageInfo package)
        {
            return new UnityBridgePackageInfo
            {
                name = package.name,
                displayName = package.displayName,
                version = package.version,
                source = package.source.ToString(),
                resolvedPath = package.resolvedPath,
                isDirectDependency = package.isDirectDependency
            };
        }

        private static List<string> Clean(IEnumerable<string> values)
        {
            return values == null
                ? new List<string>()
                : values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        }

        private static UnityBridgePackageOperationSummary Invalid(string message)
        {
            return new UnityBridgePackageOperationSummary
            {
                hasOperation = false,
                running = false,
                succeeded = false,
                status = "InvalidRequest",
                message = message,
                packageIds = new List<string>(),
                removePackageIds = new List<string>(),
                packages = new List<UnityBridgePackageInfo>()
            };
        }

        private static UnityBridgePackageOperationSummary Empty(string message)
        {
            return new UnityBridgePackageOperationSummary
            {
                hasOperation = false,
                running = false,
                succeeded = false,
                message = message,
                packageIds = new List<string>(),
                removePackageIds = new List<string>(),
                packages = new List<UnityBridgePackageInfo>()
            };
        }

        private static UnityBridgePackageOperationSummary Clone(UnityBridgePackageOperationSummary summary)
        {
            return new UnityBridgePackageOperationSummary
            {
                hasOperation = summary.hasOperation,
                running = summary.running,
                succeeded = summary.succeeded,
                operationId = summary.operationId,
                action = summary.action,
                status = summary.status,
                message = summary.message,
                errorCode = summary.errorCode,
                errorMessage = summary.errorMessage,
                startedAtUtc = summary.startedAtUtc,
                finishedAtUtc = summary.finishedAtUtc,
                packageIds = new List<string>(summary.packageIds ?? new List<string>()),
                removePackageIds = new List<string>(summary.removePackageIds ?? new List<string>()),
                packages = new List<UnityBridgePackageInfo>(summary.packages ?? new List<UnityBridgePackageInfo>())
            };
        }
    }
}
