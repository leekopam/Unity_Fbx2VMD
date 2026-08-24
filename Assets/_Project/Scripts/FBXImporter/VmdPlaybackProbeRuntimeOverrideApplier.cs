using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VmdPlaybackProbeRuntimeOverrideApplier
    {
        internal static bool Apply(
            GameObject target,
            string sourceVmdPath,
            UnityHumanoidVMDRecorder recorder,
            bool applyIkTargets)
        {
            if (target == null ||
                string.IsNullOrWhiteSpace(sourceVmdPath) ||
                !File.Exists(sourceVmdPath))
            {
                return false;
            }

            VmdPlaybackProbe probe = target.GetComponent<VmdPlaybackProbe>();
            if (probe == null)
            {
                probe = target.AddComponent<VmdPlaybackProbe>();
            }

            bool useCenterAsParentOfAll = recorder != null && recorder.UseCenterAsParentOfAll;
            bool routeCenterBoneToGroove = recorder != null && recorder.RouteHumanoidCenterToGroove;
            probe.ConfigureRuntimePlayback(
                sourceVmdPath,
                useCenterAsParentOfAll,
                routeCenterBoneToGroove,
                applyIkTargets);
            return probe.PlaybackEnabled && probe.ApplyIkTargets == applyIkTargets;
        }
    }
}
