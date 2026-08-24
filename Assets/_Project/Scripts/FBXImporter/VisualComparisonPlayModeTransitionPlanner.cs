#if UNITY_EDITOR
namespace Fbx2Vmd.FBXImporter
{
    internal enum VisualComparisonPlayModePhase
    {
        EnteredPlayMode,
        EnteredEditMode,
        ExitingPlayMode,
        Other
    }

    internal enum VisualComparisonPlayModeTransitionAction
    {
        Ignore,
        StartActiveJob,
        CleanupOnly,
        QueueAdvanceAfterPlayStop,
        QueuePlayModeEntry,
        ObservePlayModeExit,
        ReportPrematureExit
    }

    internal static class VisualComparisonPlayModeTransitionPlanner
    {
        internal static VisualComparisonPlayModeTransitionAction Resolve(
            VisualComparisonPlayModePhase phase,
            bool isRunActive,
            bool hasActiveJob,
            bool isActiveJobFinished,
            bool isAdvanceAfterPlayStopPending)
        {
            if (!isRunActive)
            {
                return VisualComparisonPlayModeTransitionAction.Ignore;
            }

            switch (phase)
            {
                case VisualComparisonPlayModePhase.EnteredPlayMode:
                    return VisualComparisonPlayModeTransitionAction.StartActiveJob;
                case VisualComparisonPlayModePhase.EnteredEditMode:
                    if (isAdvanceAfterPlayStopPending)
                    {
                        return VisualComparisonPlayModeTransitionAction.QueueAdvanceAfterPlayStop;
                    }

                    return hasActiveJob && !isActiveJobFinished
                        ? VisualComparisonPlayModeTransitionAction.QueuePlayModeEntry
                        : VisualComparisonPlayModeTransitionAction.CleanupOnly;
                case VisualComparisonPlayModePhase.ExitingPlayMode:
                    return hasActiveJob && !isActiveJobFinished
                        ? VisualComparisonPlayModeTransitionAction.ReportPrematureExit
                        : VisualComparisonPlayModeTransitionAction.ObservePlayModeExit;
                default:
                    return VisualComparisonPlayModeTransitionAction.Ignore;
            }
        }
    }
}
#endif
