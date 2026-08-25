using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonRunOptionsCopier
    {
        public static void Copy(
            VisualComparisonRunOptions source,
            VisualComparisonRunOptions destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.fbxFileName = source.fbxFileName;
            destination.durationSeconds = source.durationSeconds;
            destination.targetFrameCount = source.targetFrameCount;
            destination.enableFingerCloseups = source.enableFingerCloseups;
            destination.enableRecorderParentFrameIkOffsetsWhenCenterParented = source.enableRecorderParentFrameIkOffsetsWhenCenterParented;
            destination.mmdIkDeltaGuardLimitOverrideVmd = source.mmdIkDeltaGuardLimitOverrideVmd;
            destination.mmdIkDeltaGuardRecoveryTriggerVmd = source.mmdIkDeltaGuardRecoveryTriggerVmd;
            destination.mmdIkDeltaGuardRecoveryDebtThresholdVmd = source.mmdIkDeltaGuardRecoveryDebtThresholdVmd;
            destination.mmdIkDeltaGuardRecoveryHoldFrames = source.mmdIkDeltaGuardRecoveryHoldFrames;
            destination.enableFinalIkFootGroundingRuntimeOverride = source.enableFinalIkFootGroundingRuntimeOverride;
            destination.enableManualAnimatorFootLocalRotationRuntimeOverride = source.enableManualAnimatorFootLocalRotationRuntimeOverride;
            destination.disableManualAnimatorFootLocalRotationRuntimeOverride = source.disableManualAnimatorFootLocalRotationRuntimeOverride;
            destination.enableManualAnimatorFullBodyPoseRuntimeOverride = source.enableManualAnimatorFullBodyPoseRuntimeOverride;
            destination.disableManualAnimatorFullBodyPoseRuntimeOverride = source.disableManualAnimatorFullBodyPoseRuntimeOverride;
            destination.manualAnimatorFullBodyPoseReferenceWeight = source.manualAnimatorFullBodyPoseReferenceWeight;
            destination.manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride = source.manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride;
            destination.manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride = source.manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride;
            destination.manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride = source.manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride;
            destination.manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride = source.manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride;
            destination.manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride = source.manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride;
            destination.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride = source.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride;
            destination.manualAnimatorFullBodyPoseReferenceFrameGateStart = source.manualAnimatorFullBodyPoseReferenceFrameGateStart;
            destination.manualAnimatorFullBodyPoseReferenceFrameGateEnd = source.manualAnimatorFullBodyPoseReferenceFrameGateEnd;
            destination.enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride = source.enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride;
            destination.setHumanPoseRightLegTwistOutputReferenceWeight = source.setHumanPoseRightLegTwistOutputReferenceWeight;
            destination.setHumanPoseRightLegTwistOutputReferenceMaxDelta = source.setHumanPoseRightLegTwistOutputReferenceMaxDelta;
            destination.enableManualAnimatorBodyRotationRuntimeOverride = source.enableManualAnimatorBodyRotationRuntimeOverride;
            destination.disableManualAnimatorBodyRotationRuntimeOverride = source.disableManualAnimatorBodyRotationRuntimeOverride;
            destination.manualAnimatorBodyRotationReferenceWeight = source.manualAnimatorBodyRotationReferenceWeight;
            destination.enableManualAnimatorHandLocalRotationRuntimeOverride = source.enableManualAnimatorHandLocalRotationRuntimeOverride;
            destination.enableManualAnimatorThumbLocalRotationRuntimeOverride = source.enableManualAnimatorThumbLocalRotationRuntimeOverride;
            destination.enableManualAnimatorHandPalmFrameRuntimeOverride = source.enableManualAnimatorHandPalmFrameRuntimeOverride;
            destination.manualAnimatorHandPalmFrameWeight = source.manualAnimatorHandPalmFrameWeight;
            destination.overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings = source.overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings;
            destination.enableRetargetPoseVisualSpikeSmoothingRuntimeOverride = source.enableRetargetPoseVisualSpikeSmoothingRuntimeOverride;
            destination.retargetPoseVisualSpikeCurrentWeight = source.retargetPoseVisualSpikeCurrentWeight;
            destination.retargetPoseVisualSpikeForearmStretchClampMaxOffset = source.retargetPoseVisualSpikeForearmStretchClampMaxOffset;
            destination.enableRetargetArmStretchClampRuntimeOverride = source.enableRetargetArmStretchClampRuntimeOverride;
            destination.retargetArmStretchMuscleLimit = source.retargetArmStretchMuscleLimit;
            destination.enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = source.enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            destination.enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = source.enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            destination.disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride = source.disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride;
            destination.disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride = source.disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride;
            destination.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride = source.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            destination.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride = source.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            destination.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride = source.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            destination.enablePostSetHumanPoseRightEndpointPositionRuntimeOverride = source.enablePostSetHumanPoseRightEndpointPositionRuntimeOverride;
            destination.enablePreSetHumanPoseRightEndpointPositionRuntimeOverride = source.enablePreSetHumanPoseRightEndpointPositionRuntimeOverride;
            destination.enableManualAnimatorBipedIkFootPositionRuntimeOverride = source.enableManualAnimatorBipedIkFootPositionRuntimeOverride;
            destination.enableManualAnimatorHipsLocalPositionRuntimeOverride = source.enableManualAnimatorHipsLocalPositionRuntimeOverride;
            destination.enableManualAnimatorBodyPositionXzRuntimeOverride = source.enableManualAnimatorBodyPositionXzRuntimeOverride;
            destination.enableRetargetBodyPositionXzRootMotionRuntimeOverride = source.enableRetargetBodyPositionXzRootMotionRuntimeOverride;
            destination.disableTargetHumanoidBonePositionLockRuntimeOverride = source.disableTargetHumanoidBonePositionLockRuntimeOverride;
            destination.manualAnimatorLowerBodySegmentDirectionReferenceWeight = source.manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            destination.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = source.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = source.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = source.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = source.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            destination.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = source.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = source.manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            destination.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = source.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            destination.postSetHumanPoseRightEndpointPositionReferenceWeight = source.postSetHumanPoseRightEndpointPositionReferenceWeight;
            destination.postSetHumanPoseRightEndpointPositionReferenceMaxOffset = source.postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            destination.postSetHumanPoseRightEndpointPositionReferencePositiveZScale = source.postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            destination.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = source.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            destination.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = source.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            destination.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = source.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            destination.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = source.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            destination.preSetHumanPoseRightEndpointPositionReferenceWeight = source.preSetHumanPoseRightEndpointPositionReferenceWeight;
            destination.preSetHumanPoseRightEndpointPositionReferenceMaxOffset = source.preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            destination.preSetHumanPoseRightEndpointPositionReferencePositiveZScale = source.preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            destination.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = source.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            destination.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = source.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            destination.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = source.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            destination.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = source.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            destination.preSetHumanPoseEndpointPositionUseGhostCurrentBasis = source.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            destination.ShouldInvertPreSetHumanPoseEndpointPositionBodyX = source.ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
            destination.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = source.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
            destination.usePostSetHumanPoseRightFootEvaluatorXzReference = source.usePostSetHumanPoseRightFootEvaluatorXzReference;
            destination.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = source.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            destination.manualAnimatorBipedIkFootPositionReferenceWeight = source.manualAnimatorBipedIkFootPositionReferenceWeight;
            destination.manualAnimatorBipedIkFootPositionReferenceMaxOffset = source.manualAnimatorBipedIkFootPositionReferenceMaxOffset;
            destination.manualAnimatorHipsLocalPositionReferenceWeight = source.manualAnimatorHipsLocalPositionReferenceWeight;
            destination.manualAnimatorHipsLocalPositionReferenceMaxOffset = source.manualAnimatorHipsLocalPositionReferenceMaxOffset;
            destination.manualAnimatorBodyPositionXzReferenceWeight = source.manualAnimatorBodyPositionXzReferenceWeight;
            destination.manualAnimatorBodyPositionXzReferenceMaxOffset = source.manualAnimatorBodyPositionXzReferenceMaxOffset;
            destination.manualAnimatorBodyPositionXzReferenceFrameGateStart = source.manualAnimatorBodyPositionXzReferenceFrameGateStart;
            destination.manualAnimatorBodyPositionXzReferenceFrameGateEnd = source.manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            destination.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = source.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            destination.manualAnimatorBodyPositionXzReferenceAxisXScale = source.manualAnimatorBodyPositionXzReferenceAxisXScale;
            destination.manualAnimatorBodyPositionXzReferenceAxisZScale = source.manualAnimatorBodyPositionXzReferenceAxisZScale;
            destination.enableVmdPlaybackProbeRuntimeOverride = source.enableVmdPlaybackProbeRuntimeOverride;
            destination.applyVmdPlaybackProbeIkTargetsRuntimeOverride = source.applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            destination.vmdPlaybackProbeSourceVmdPath = source.vmdPlaybackProbeSourceVmdPath;
            destination.editorDiagnosticSmokeSegment = source.editorDiagnosticSmokeSegment;
            destination.enableReferenceMmdTimingRuntimeOverride = source.enableReferenceMmdTimingRuntimeOverride;
            destination.diagnosticCaptureWidthOverride = source.diagnosticCaptureWidthOverride;
            destination.diagnosticCaptureHeightOverride = source.diagnosticCaptureHeightOverride;
            destination.diagnosticScreenshotPaddingOverride = source.diagnosticScreenshotPaddingOverride;
            destination.diagnosticScreenshotVerticalViewportCenterOverride = source.diagnosticScreenshotVerticalViewportCenterOverride;
        }
    }
}
