using System;

namespace Fbx2Vmd.FBXImporter
{
    [Serializable]
    internal class YybVisualComparisonRunStateData : YybVisualComparisonRunOptions
    {
        public bool isRunning;
        public bool activeJobFinished;
        public bool advanceAfterPlayStopPending;
        public bool playModeEntryPending;
        public string summarySessionId;
        public string summaryDirectory;
        public string projectRoot;
        public VisualComparisonCaptureJobStateData activeJob;
        public VisualComparisonCaptureJobStateData[] pendingJobs;
        public YybVisualComparisonCaptureResultData[] results;
        public string[] failures;
    }
}
