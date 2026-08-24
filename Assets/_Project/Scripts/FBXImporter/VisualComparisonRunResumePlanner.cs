#if UNITY_EDITOR
namespace Fbx2Vmd.FBXImporter
{
    internal enum VisualComparisonRunResumeAction
    {
        QueueAdvanceAfterPlayStop,
        QueuePlayModeEntry,
        RecoverMissingActiveJob,
        DeferActiveJobStartInPlayMode,
        DeferNextJob,
        DeferActiveJobEntry,
        StartNextJob,
        FinalizeRun
    }

    internal static class VisualComparisonRunResumePlanner
    {
        internal static VisualComparisonRunResumeAction Resolve(
            bool isAdvanceAfterPlayStopPending,
            bool isPlayModeEntryPending,
            bool hasActiveJob,
            bool isPlaying,
            bool isActiveJobFinished,
            bool hasPendingJobs)
        {
            if (isAdvanceAfterPlayStopPending)
            {
                return VisualComparisonRunResumeAction.QueueAdvanceAfterPlayStop;
            }

            if (isPlayModeEntryPending)
            {
                return hasActiveJob
                    ? VisualComparisonRunResumeAction.QueuePlayModeEntry
                    : VisualComparisonRunResumeAction.RecoverMissingActiveJob;
            }

            if (isPlaying)
            {
                return VisualComparisonRunResumeAction.DeferActiveJobStartInPlayMode;
            }

            if (hasActiveJob)
            {
                return isActiveJobFinished
                    ? VisualComparisonRunResumeAction.DeferNextJob
                    : VisualComparisonRunResumeAction.DeferActiveJobEntry;
            }

            return hasPendingJobs
                ? VisualComparisonRunResumeAction.StartNextJob
                : VisualComparisonRunResumeAction.FinalizeRun;
        }
    }
}
#endif
