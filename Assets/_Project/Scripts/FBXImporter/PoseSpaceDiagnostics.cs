using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
        private sealed class PoseSpaceDiagnostics
        {
            private readonly PoseSpaceRetargeter _retargeter;

            public PoseSpaceDiagnostics(PoseSpaceRetargeter retargeter)
            {
                _retargeter = retargeter;
            }

            public void ResetRetargetPoseStageDiagnostics()
            {
                _retargeter.ResetRetargetPoseStageDiagnostics();
            }

            public RetargetEndpointStageWorldPositions CaptureEndpointStageWorldPositions(Animator animator)
            {
                return PoseSpaceRetargeter.CaptureEndpointStageWorldPositions(animator);
            }

            public void CapturePoseInputDiagnostics(HumanPose pose)
            {
                _retargeter.CapturePoseInputDiagnostics(pose);
            }

            public void CaptureAfterEditorMuscleReferenceDiagnostics(HumanPose pose)
            {
                _retargeter.CaptureAfterEditorMuscleReferenceDiagnostics(pose);
            }

            public void CaptureAfterClampPoseMusclesDiagnostics(HumanPose pose)
            {
                _retargeter.CaptureAfterClampPoseMusclesDiagnostics(pose);
            }

            public void CaptureAfterAnatomicalArmGuardDiagnostics(HumanPose pose)
            {
                _retargeter.CaptureAfterAnatomicalArmGuardDiagnostics(pose);
            }

            public void CaptureAfterVisualSpikeSmoothingDiagnostics(HumanPose pose)
            {
                _retargeter.CaptureAfterVisualSpikeSmoothingDiagnostics(pose);
            }

            public void CaptureSetHumanPoseInputDiagnostics(HumanPose pose)
            {
                _retargeter.CaptureSetHumanPoseInputDiagnostics(pose);
            }

            public void CaptureSetHumanPoseOutputDiagnostics()
            {
                _retargeter.CaptureSetHumanPoseOutputDiagnostics();
            }

            public void CaptureRetargetEndpointStageAttributionDiagnostics()
            {
                _retargeter.CaptureRetargetEndpointStageAttributionDiagnostics();
            }
        }
    }
}
