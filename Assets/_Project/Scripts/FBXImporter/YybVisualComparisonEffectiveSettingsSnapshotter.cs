namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonEffectiveSettingsSnapshotter
    {
        public static void Capture(YybVisualComparisonCaptureResultData result, FBXVmdPipeline fileManager)
        {
            if (result == null || fileManager == null)
            {
                return;
            }

            result.hasFBXVmdPipelineEffectiveSettings = true;
            result.ShouldUseManualAnimatorFootLocalRotationReference =
                fileManager.ShouldUseManualAnimatorFootLocalRotationReference;
            result.manualAnimatorFootLocalRotationReferenceWeight =
                fileManager.manualAnimatorFootLocalRotationReferenceWeight;
            result.ShouldUseManualAnimatorFullBodyPoseReference =
                fileManager.ShouldUseManualAnimatorFullBodyPoseReference;
            result.manualAnimatorFullBodyPoseReferenceWeight =
                fileManager.manualAnimatorFullBodyPoseReferenceWeight;
            result.ShouldExcludeManualAnimatorFullBodyLowerMuscles =
                fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles;
            result.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly =
                fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly;
            result.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly =
                fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly;
            result.manualAnimatorFullBodyPoseRightArmMusclesOnly =
                fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly;
            result.manualAnimatorFullBodyPoseLeftArmMusclesOnly =
                fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly;
            result.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly =
                fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly;
            result.manualAnimatorFullBodyPoseFrameGateStart =
                fileManager.manualAnimatorFullBodyPoseFrameGateStart;
            result.manualAnimatorFullBodyPoseFrameGateEnd =
                fileManager.manualAnimatorFullBodyPoseFrameGateEnd;
            result.ShouldUseSetHumanPoseRightLegTwistOutputReference =
                fileManager.ShouldUseSetHumanPoseRightLegTwistOutputReference;
            result.setHumanPoseRightLegTwistOutputReferenceWeight =
                fileManager.setHumanPoseRightLegTwistOutputReferenceWeight;
            result.setHumanPoseRightLegTwistOutputReferenceMaxDelta =
                fileManager.setHumanPoseRightLegTwistOutputReferenceMaxDelta;
            result.ShouldUseManualAnimatorBodyRotationReference = fileManager.ShouldUseManualAnimatorBodyRotationReference;
            result.manualAnimatorBodyRotationReferenceWeight = fileManager.manualAnimatorBodyRotationReferenceWeight;
            result.ShouldUseManualAnimatorLowerBodySegmentDirectionReference =
                fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference;
            result.manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight;
            result.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle;
            result.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                fileManager.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference;
            result.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle;
            result.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference =
                fileManager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference;
            result.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle;
            result.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd;
            result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight;
            result.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference =
                fileManager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference;
            result.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                fileManager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle;
            result.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference =
                fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference;
            result.manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight;
            result.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle;
            result.usePostSetHumanPoseRightEndpointPositionReference =
                fileManager.usePostSetHumanPoseRightEndpointPositionReference;
            result.postSetHumanPoseRightEndpointPositionReferenceWeight =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceWeight;
            result.postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            result.postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                fileManager.postSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            result.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            result.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            result.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            result.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition =
                fileManager.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            result.usePreSetHumanPoseRightEndpointPositionReference =
                fileManager.usePreSetHumanPoseRightEndpointPositionReference;
            result.preSetHumanPoseRightEndpointPositionReferenceWeight =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceWeight;
            result.preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceMaxOffset;
            result.preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                fileManager.preSetHumanPoseRightEndpointPositionReferencePositiveZScale;
            result.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight;
            result.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart;
            result.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd;
            result.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition =
                fileManager.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            result.preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                fileManager.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
            result.ShouldInvertPreSetHumanPoseEndpointPositionBodyX =
                fileManager.ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
            result.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ =
                fileManager.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
            result.usePostSetHumanPoseRightFootEvaluatorXzReference =
                fileManager.usePostSetHumanPoseRightFootEvaluatorXzReference;
            result.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                fileManager.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude;
            result.ShouldUseManualAnimatorBodyPositionXzReference =
                fileManager.ShouldUseManualAnimatorBodyPositionXzReference;
            result.manualAnimatorBodyPositionXzReferenceWeight =
                fileManager.manualAnimatorBodyPositionXzReferenceWeight;
            result.manualAnimatorBodyPositionXzReferenceMaxOffset =
                fileManager.manualAnimatorBodyPositionXzReferenceMaxOffset;
            result.manualAnimatorBodyPositionXzReferenceFrameGateStart =
                fileManager.manualAnimatorBodyPositionXzReferenceFrameGateStart;
            result.manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                fileManager.manualAnimatorBodyPositionXzReferenceFrameGateEnd;
            result.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                fileManager.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames;
            result.manualAnimatorBodyPositionXzReferenceAxisXScale =
                fileManager.manualAnimatorBodyPositionXzReferenceAxisXScale;
            result.manualAnimatorBodyPositionXzReferenceAxisZScale =
                fileManager.manualAnimatorBodyPositionXzReferenceAxisZScale;
            result.enableYybArmSwingLimitCorrection = fileManager.enableYybArmSwingLimitCorrection;
            result.yybArmSwingLimitWeight = fileManager.YybArmSwingLimitWeight;
            result.yybArmSwingMaxDownDot = fileManager.YybArmSwingMaxDownDot;
            result.yybArmSwingMinHandHorizontalRatio = fileManager.YybArmSwingMinHandHorizontalRatio;
            result.yybArmSwingMaxHandBelowShoulderRatio = fileManager.YybArmSwingMaxHandBelowShoulderRatio;
            result.yybArmSwingHorizontalReachLimitWeight = fileManager.YybArmSwingHorizontalReachLimitWeight;
            result.yybArmSwingMaxHandHorizontalReachRatio = fileManager.YybArmSwingMaxHandHorizontalReachRatio;
            result.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                fileManager.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio;
            result.yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                fileManager.YybArmSwingHorizontalReachMinElbowAngleAfterApply;
            result.yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                fileManager.YybArmSwingRaisedPoseHorizontalReachLimitWeight;
            result.yybArmSwingRaisedPoseMinUpperArmDownDot =
                fileManager.YybArmSwingRaisedPoseMinUpperArmDownDot;
            result.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                fileManager.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio;
            result.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                fileManager.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio;
            result.enableYybArmSleeveAnchorCorrection = fileManager.enableYybArmSleeveAnchorCorrection;
            result.enableYybArmVisualTwistCorrection = fileManager.enableYybArmVisualTwistCorrection;
            result.clampRetargetArmStretchMuscles = fileManager.clampRetargetArmStretchMuscles;
            result.armStretchMuscleLimit = fileManager.ArmStretchMuscleLimit;
        }

    }
}
