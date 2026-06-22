namespace VolxGames.UnityBridge.Editor
{
    internal static class UnityBridgeBuildTracker
    {
        private static readonly object SyncRoot = new object();
        private static UnityBridgeBuildReportSummary lastBuildReport;

        public static bool IsBuildRunning { get; private set; }

        public static string CurrentBuildStartedAtUtc { get; private set; }

        public static void MarkBuildStarted(string startedAtUtc)
        {
            lock (SyncRoot)
            {
                IsBuildRunning = true;
                CurrentBuildStartedAtUtc = startedAtUtc;
            }
        }

        public static void SetLastBuildReport(UnityBridgeBuildReportSummary report)
        {
            lock (SyncRoot)
            {
                lastBuildReport = report;
                IsBuildRunning = false;
                CurrentBuildStartedAtUtc = null;
            }
        }

        public static UnityBridgeBuildReportSummary GetLastBuildReport()
        {
            lock (SyncRoot)
            {
                return lastBuildReport ?? new UnityBridgeBuildReportSummary
                {
                    hasReport = false,
                    message = "No build has been run through the bridge yet."
                };
            }
        }
    }
}
