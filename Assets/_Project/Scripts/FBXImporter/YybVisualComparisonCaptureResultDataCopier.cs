using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonCaptureResultDataCopier
    {
        public static void Copy(
            YybVisualComparisonCaptureResultData source,
            YybVisualComparisonCaptureResultData destination)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.jobMode = source.jobMode;
            destination.jobDisplayName = source.jobDisplayName;
            destination.sceneName = source.sceneName;
            destination.comparisonLabel = source.comparisonLabel;
            destination.targetName = source.targetName;
            destination.success = source.success;
            destination.error = source.error;
            destination.vmdPath = source.vmdPath;
            destination.frameCount = source.frameCount;
            destination.fileSizeBytes = source.fileSizeBytes;
            destination.comparisonSessionManifestPath = source.comparisonSessionManifestPath;
            destination.comparisonMetricsCsvPath = source.comparisonMetricsCsvPath;
            destination.comparisonFrameFolderPath = source.comparisonFrameFolderPath;
            destination.comparisonFrameIndexPath = source.comparisonFrameIndexPath;
            destination.comparisonSessionId = source.comparisonSessionId;
            destination.hasFBXVmdPipelineEffectiveSettings = source.hasFBXVmdPipelineEffectiveSettings;
            destination.ShouldUseManualAnimatorFootLocalRotationReference = source.ShouldUseManualAnimatorFootLocalRotationReference;
            destination.manualAnimatorFootLocalRotationReferenceWeight = source.manualAnimatorFootLocalRotationReferenceWeight;
            destination.ShouldUseManualAnimatorFullBodyPoseReference = source.ShouldUseManualAnimatorFullBodyPoseReference;
            destination.manualAnimatorFullBodyPoseReferenceWeight = source.manualAnimatorFullBodyPoseReferenceWeight;
            destination.ShouldExcludeManualAnimatorFullBodyLowerMuscles = source.ShouldExcludeManualAnimatorFullBodyLowerMuscles;
            destination.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly = source.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly;
            destination.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = source.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly;
            destination.manualAnimatorFullBodyPoseRightArmMusclesOnly = source.manualAnimatorFullBodyPoseRightArmMusclesOnly;
            destination.manualAnimatorFullBodyPoseLeftArmMusclesOnly = source.manualAnimatorFullBodyPoseLeftArmMusclesOnly;
            destination.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly = source.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly;
            destination.manualAnimatorFullBodyPoseFrameGateStart = source.manualAnimatorFullBodyPoseFrameGateStart;
            destination.manualAnimatorFullBodyPoseFrameGateEnd = source.manualAnimatorFullBodyPoseFrameGateEnd;
            destination.ShouldUseSetHumanPoseRightLegTwistOutputReference = source.ShouldUseSetHumanPoseRightLegTwistOutputReference;
            destination.setHumanPoseRightLegTwistOutputReferenceWeight = source.setHumanPoseRightLegTwistOutputReferenceWeight;
            destination.setHumanPoseRightLegTwistOutputReferenceMaxDelta = source.setHumanPoseRightLegTwistOutputReferenceMaxDelta;
            destination.ShouldUseManualAnimatorBodyRotationReference = source.ShouldUseManualAnimatorBodyRotationReference;
            destination.manualAnimatorBodyRotationReferenceWeight = source.manualAnimatorBodyRotationReferenceWeight;
            destination.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = source.ShouldUseManualAnimatorLowerBodySegmentDirectionReference;
            destination.manualAnimatorLowerBodySegmentDirectionReferenceWeight = source.manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            destination.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = source.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            destination.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference = source.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference;
            destination.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = source.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            destination.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference = source.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference;
            destination.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = source.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = source.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            destination.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = source.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            destination.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = source.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference;
            destination.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = source.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            destination.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = source.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference;
            destination.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = source.manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            destination.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = source.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            destination.usePostSetHumanPoseRightEndpointPositionReference = source.usePostSetHumanPoseRightEndpointPositionReference;
            destination.postSetHumanPoseRightEndpointPositionReferenceWeight = source.postSetHumanPoseRightEndpointPositionReferenceWeight;
            destination.postSetHumanPoseRightEndpointPositionReferenceMaxOffset = source.postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            destination.postSetHumanPoseRightEndpointPositionReferencePositiveZScale = source.postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            destination.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = source.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            destination.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = source.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            destination.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = source.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            destination.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = source.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            destination.usePreSetHumanPoseRightEndpointPositionReference = source.usePreSetHumanPoseRightEndpointPositionReference;
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
            destination.ShouldUseManualAnimatorBodyPositionXzReference = source.ShouldUseManualAnimatorBodyPositionXzReference;
            destination.manualAnimatorBodyPositionXzReferenceWeight = source.manualAnimatorBodyPositionXzReferenceWeight;
            destination.manualAnimatorBodyPositionXzReferenceMaxOffset = source.manualAnimatorBodyPositionXzReferenceMaxOffset;
            destination.manualAnimatorBodyPositionXzReferenceFrameGateStart = source.manualAnimatorBodyPositionXzReferenceFrameGateStart;
            destination.manualAnimatorBodyPositionXzReferenceFrameGateEnd = source.manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            destination.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = source.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            destination.manualAnimatorBodyPositionXzReferenceAxisXScale = source.manualAnimatorBodyPositionXzReferenceAxisXScale;
            destination.manualAnimatorBodyPositionXzReferenceAxisZScale = source.manualAnimatorBodyPositionXzReferenceAxisZScale;
            destination.enableYybArmSwingLimitCorrection = source.enableYybArmSwingLimitCorrection;
            destination.yybArmSwingLimitWeight = source.yybArmSwingLimitWeight;
            destination.yybArmSwingMaxDownDot = source.yybArmSwingMaxDownDot;
            destination.yybArmSwingMinHandHorizontalRatio = source.yybArmSwingMinHandHorizontalRatio;
            destination.yybArmSwingMaxHandBelowShoulderRatio = source.yybArmSwingMaxHandBelowShoulderRatio;
            destination.yybArmSwingHorizontalReachLimitWeight = source.yybArmSwingHorizontalReachLimitWeight;
            destination.yybArmSwingMaxHandHorizontalReachRatio = source.yybArmSwingMaxHandHorizontalReachRatio;
            destination.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = source.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            destination.yybArmSwingHorizontalReachMinElbowAngleAfterApply = source.yybArmSwingHorizontalReachMinElbowAngleAfterApply;
            destination.yybArmSwingRaisedPoseHorizontalReachLimitWeight = source.yybArmSwingRaisedPoseHorizontalReachLimitWeight;
            destination.yybArmSwingRaisedPoseMinUpperArmDownDot = source.yybArmSwingRaisedPoseMinUpperArmDownDot;
            destination.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = source.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            destination.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = source.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            destination.enableYybArmSleeveAnchorCorrection = source.enableYybArmSleeveAnchorCorrection;
            destination.enableYybArmVisualTwistCorrection = source.enableYybArmVisualTwistCorrection;
            destination.clampRetargetArmStretchMuscles = source.clampRetargetArmStretchMuscles;
            destination.armStretchMuscleLimit = source.armStretchMuscleLimit;
        }
    }
}
