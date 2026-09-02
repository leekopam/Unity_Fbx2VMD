#if UNITY_EDITOR
namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonCaptureJobAdvancePolicy
    {
        internal static bool CanStartNextJob(
            bool isRunActive,
            bool hasActiveJob,
            bool isActiveJobFinished)
        {
            return isRunActive && (!hasActiveJob || isActiveJobFinished);
        }
    }
}
#endif
