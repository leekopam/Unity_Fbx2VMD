#if UNITY_EDITOR
namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonRuntimeOverrideCoordinator
    {
        internal static bool Apply(
            FBXVmdPipeline pipeline,
            YybVisualComparisonRunStateData state,
            float maximumArmStretchMuscleLimit,
            float defaultRightLowerLegToFootAxisXzScale,
            float defaultRightLowerLegToFootBlendWeight,
            float defaultRightLowerLegToFootEndpointBlendWeight)
        {
            if (pipeline == null || state == null)
            {
                return false;
            }

            if (state.enableFinalIkFootGroundingRuntimeOverride)
            {
                FinalIkFootGroundingRuntimeOverrideApplier.Apply(pipeline, true);
            }

            if (state.disableManualAnimatorFootLocalRotationRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyFootLocalRotation(pipeline, false);
            }
            else if (state.enableManualAnimatorFootLocalRotationRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyFootLocalRotation(pipeline, true);
            }

            if (state.disableManualAnimatorFullBodyPoseRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyFullBodyPose(
                    pipeline,
                    false,
                    state.manualAnimatorFullBodyPoseReferenceWeight,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    0f,
                    0f);
            }
            else if (state.enableManualAnimatorFullBodyPoseRuntimeOverride ||
                     state.manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride ||
                     state.manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride ||
                     state.manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride ||
                     state.manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride ||
                     state.manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride ||
                     state.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride ||
                     state.manualAnimatorFullBodyPoseReferenceFrameGateStart > 0f ||
                     state.manualAnimatorFullBodyPoseReferenceFrameGateEnd > 0f)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyFullBodyPose(
                    pipeline,
                    true,
                    state.manualAnimatorFullBodyPoseReferenceWeight,
                    state.manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                    state.manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                    state.manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                    state.manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                    state.manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                    state.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                    state.manualAnimatorFullBodyPoseReferenceFrameGateStart,
                    state.manualAnimatorFullBodyPoseReferenceFrameGateEnd);
            }

            if (state.enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyRightLegTwistOutput(
                    pipeline,
                    true,
                    state.setHumanPoseRightLegTwistOutputReferenceWeight,
                    state.setHumanPoseRightLegTwistOutputReferenceMaxDelta);
            }

            if (state.disableManualAnimatorBodyRotationRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyBodyRotation(
                    pipeline,
                    false,
                    state.manualAnimatorBodyRotationReferenceWeight);
            }
            else if (state.enableManualAnimatorBodyRotationRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyBodyRotation(
                    pipeline,
                    true,
                    state.manualAnimatorBodyRotationReferenceWeight);
            }

            if (state.enableManualAnimatorHandLocalRotationRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyHandLocalRotation(pipeline, true);
            }

            if (state.enableManualAnimatorThumbLocalRotationRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyThumbLocalRotation(pipeline, true);
            }

            if (state.enableManualAnimatorHandPalmFrameRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyHandPalmFrame(
                    pipeline,
                    true,
                    state.manualAnimatorHandPalmFrameWeight);
            }

            if (state.overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings)
            {
                RetargetingRuntimeOverrideApplier.ApplyPoseVisualSpikeSmoothing(
                    pipeline,
                    state.enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                    state.retargetPoseVisualSpikeCurrentWeight,
                    state.retargetPoseVisualSpikeForearmStretchClampMaxOffset);
            }

            if (state.enableRetargetArmStretchClampRuntimeOverride)
            {
                RetargetingRuntimeOverrideApplier.ApplyArmStretchClamp(
                    pipeline,
                    true,
                    state.retargetArmStretchMuscleLimit,
                    maximumArmStretchMuscleLimit);
            }

            if (state.enableYybArmSwingLimitRuntimeOverride)
            {
                YybArmRuntimeOverrideApplier.ApplySwingLimit(
                    pipeline,
                    true,
                    state.yybArmSwingLimitWeight,
                    state.yybArmSwingMaxDownDot,
                    state.yybArmSwingMinHandHorizontalRatio,
                    state.yybArmSwingMaxHandBelowShoulderRatio,
                    state.yybArmSwingHorizontalReachLimitWeight,
                    state.yybArmSwingMaxHandHorizontalReachRatio,
                    state.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                    state.yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    state.yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                    state.yybArmSwingRaisedPoseMinUpperArmDownDot,
                    state.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                    state.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
            }

            if (state.enableYybArmDirectionRetargetRuntimeOverride)
            {
                YybArmRuntimeOverrideApplier.ApplyDirection(
                    pipeline,
                    true,
                    state.yybArmDirectionUpperArmWeight,
                    state.yybArmDirectionForearmWeight,
                    state.yybArmDirectionUpperArmMaxDegrees,
                    state.yybArmDirectionForearmMaxDegrees,
                    state.yybArmDirectionLeftSideWeightScale,
                    state.yybArmDirectionRightSideWeightScale);
            }

            if (state.overrideYybArmSleeveAnchorRuntimeSettings)
            {
                YybArmRuntimeOverrideApplier.ApplySleeveAnchor(
                    pipeline,
                    state.enableYybArmSleeveAnchorRuntimeOverride,
                    state.yybArmSleeveAnchorInfluence,
                    state.yybArmShoulderCapAnchorInfluence,
                    state.yybArmSleeveAnchorMaxDegrees);
            }

            if (state.overrideYybArmVisualTwistRuntimeSettings)
            {
                YybArmRuntimeOverrideApplier.ApplyVisualTwist(
                    pipeline,
                    state.enableYybArmVisualTwistRuntimeOverride,
                    state.yybArmVisualUpperArmInfluence,
                    state.yybArmVisualForearmInfluence,
                    state.yybArmVisualUpperArmMaxDegrees,
                    state.yybArmVisualForearmMaxDegrees);
            }

            ApplyLowerBodySegmentDirection(
                pipeline,
                state,
                defaultRightLowerLegToFootAxisXzScale,
                defaultRightLowerLegToFootBlendWeight,
                defaultRightLowerLegToFootEndpointBlendWeight);

            if (state.disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyFootHipsAlignedResidualYaw(
                    pipeline,
                    false,
                    state.manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                    state.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            }
            else if (state.enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyFootHipsAlignedResidualYaw(
                    pipeline,
                    true,
                    state.manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                    state.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            }

            if (state.enablePostSetHumanPoseRightEndpointPositionRuntimeOverride)
            {
                HumanPoseEndpointRuntimeOverrideApplier.ApplyPostSetReference(
                    pipeline,
                    true,
                    state.postSetHumanPoseRightEndpointPositionReferenceWeight,
                    state.postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    state.postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    state.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    state.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                    state.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                    state.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition,
                    state.usePostSetHumanPoseRightFootEvaluatorXzReference,
                    state.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            }

            if (state.enablePreSetHumanPoseRightEndpointPositionRuntimeOverride)
            {
                HumanPoseEndpointRuntimeOverrideApplier.ApplyPreSetReference(
                    pipeline,
                    true,
                    state.preSetHumanPoseRightEndpointPositionReferenceWeight,
                    state.preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    state.preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    state.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    state.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                    state.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                    state.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition,
                    state.preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                    state.ShouldInvertPreSetHumanPoseEndpointPositionBodyX,
                    state.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ);
            }

            if (state.enableManualAnimatorBipedIkFootPositionRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyBipedIkFootPosition(
                    pipeline,
                    true,
                    state.manualAnimatorBipedIkFootPositionReferenceWeight,
                    state.manualAnimatorBipedIkFootPositionReferenceMaxOffset);
            }

            if (state.enableManualAnimatorHipsLocalPositionRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyHipsLocalPosition(
                    pipeline,
                    true,
                    state.manualAnimatorHipsLocalPositionReferenceWeight,
                    state.manualAnimatorHipsLocalPositionReferenceMaxOffset);
            }

            if (state.enableManualAnimatorBodyPositionXzRuntimeOverride)
            {
                ManualPoseReferenceRuntimeOverrideApplier.ApplyBodyPositionXz(
                    pipeline,
                    true,
                    state.manualAnimatorBodyPositionXzReferenceWeight,
                    state.manualAnimatorBodyPositionXzReferenceMaxOffset,
                    state.manualAnimatorBodyPositionXzReferenceFrameGateStart,
                    state.manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                    state.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                    state.manualAnimatorBodyPositionXzReferenceAxisXScale,
                    state.manualAnimatorBodyPositionXzReferenceAxisZScale);
            }

            if (state.enableYybRightSleeveSilhouetteOffsetRuntimeOverride)
            {
                YybArmRuntimeOverrideApplier.ApplyRightSleeveSilhouetteOffset(
                    pipeline,
                    true,
                    state.yybRightSleeveSilhouetteLocalOffsetX,
                    state.yybRightSleeveSilhouetteLocalOffsetFrameGateStart,
                    state.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd);
            }

            if (state.enableRetargetBodyPositionXzRootMotionRuntimeOverride)
            {
                RetargetingRuntimeOverrideApplier.ApplyBodyPositionXzRootMotion(pipeline, true);
            }

            if (state.disableTargetHumanoidBonePositionLockRuntimeOverride)
            {
                RetargetingRuntimeOverrideApplier.ApplyTargetHumanoidBonePositionLock(pipeline, false);
            }

            return true;
        }

        private static void ApplyLowerBodySegmentDirection(
            FBXVmdPipeline pipeline,
            YybVisualComparisonRunStateData state,
            float defaultRightLowerLegToFootAxisXzScale,
            float defaultRightLowerLegToFootBlendWeight,
            float defaultRightLowerLegToFootEndpointBlendWeight)
        {
            bool hasDetails = ManualLowerBodySegmentDirectionRuntimeOverrideApplier.HasDetails(
                state.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                state.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                state.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                state.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                state.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                defaultRightLowerLegToFootAxisXzScale,
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                defaultRightLowerLegToFootBlendWeight,
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                defaultRightLowerLegToFootEndpointBlendWeight,
                state.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                state.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);

            if (state.disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride ||
                state.enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride)
            {
                ManualLowerBodySegmentDirectionRuntimeOverrideApplier.Apply(
                    pipeline,
                    state.enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride &&
                    !state.disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride,
                    state.manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                    state.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                    state.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                    state.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                    state.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                    state.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    state.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                    state.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                    state.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            }
            else if (hasDetails)
            {
                ManualLowerBodySegmentDirectionRuntimeOverrideApplier.ApplyDetails(
                    pipeline,
                    state.disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                    state.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                    state.disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                    state.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    state.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                    state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                    state.disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                    state.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            }
        }
    }
}
#endif
