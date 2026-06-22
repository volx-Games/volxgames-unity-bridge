using System.Collections.Generic;

namespace VolxGames.UnityBridge.Editor
{
    internal static class UnityBridgeTestTracker
    {
        private static readonly object SyncRoot = new object();
        private static UnityBridgeTestReportSummary currentRun;
        private static UnityBridgeTestReportSummary lastRun;

        public static void MarkRunStarted(UnityBridgeTestReportSummary report)
        {
            lock (SyncRoot)
            {
                currentRun = report;
            }
        }

        public static void MarkTestStarted()
        {
            lock (SyncRoot)
            {
                if (currentRun == null)
                {
                    return;
                }

                currentRun.startedCount += 1;
            }
        }

        public static void MarkTestFinished()
        {
            lock (SyncRoot)
            {
                if (currentRun == null)
                {
                    return;
                }

                currentRun.finishedCount += 1;
            }
        }

        public static void MarkRunFinished(UnityBridgeTestReportSummary report)
        {
            lock (SyncRoot)
            {
                currentRun = null;
                lastRun = report;
            }
        }

        public static bool IsRunActive()
        {
            lock (SyncRoot)
            {
                return currentRun != null && currentRun.running;
            }
        }

        public static UnityBridgeTestReportSummary GetCurrentOrLastReport()
        {
            lock (SyncRoot)
            {
                return currentRun ?? lastRun ?? Empty("No test run has been started through the bridge yet.");
            }
        }

        public static UnityBridgeTestReportSummary GetLastReport()
        {
            lock (SyncRoot)
            {
                return lastRun ?? Empty("No test run has been completed through the bridge yet.");
            }
        }

        private static UnityBridgeTestReportSummary Empty(string message)
        {
            return new UnityBridgeTestReportSummary
            {
                hasReport = false,
                running = false,
                message = message,
                testNames = new List<string>(),
                groupNames = new List<string>(),
                categoryNames = new List<string>(),
                assemblyNames = new List<string>()
            };
        }
    }
}
