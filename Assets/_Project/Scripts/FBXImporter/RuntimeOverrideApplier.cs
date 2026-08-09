
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static partial class YybVisualComparisonBatchRunner
    {
        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd)
        {
            return ApplyMmdIkDeltaGuardRuntimeOverride(
                recorder,
                overrideLimitVmd,
                NoMmdIkDeltaGuardLimitOverrideVmd);
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd)
        {
            return ApplyMmdIkDeltaGuardRuntimeOverride(
                recorder,
                overrideLimitVmd,
                recoveryTriggerVmd,
                NoMmdIkDeltaGuardLimitOverrideVmd);
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd)
        {
            return ApplyMmdIkDeltaGuardRuntimeOverride(
                recorder,
                overrideLimitVmd,
                recoveryTriggerVmd,
                recoveryDebtThresholdVmd,
                NoMmdIkDeltaGuardRecoveryHoldFrames);
        }

        private static bool ApplyMmdIkDeltaGuardRuntimeOverride(
            UnityHumanoidVMDRecorder recorder,
            float overrideLimitVmd,
            float recoveryTriggerVmd,
            float recoveryDebtThresholdVmd,
            int recoveryHoldFrames)
        {
            float normalizedLimit = NormalizeMmdIkDeltaGuardLimitOverride(overrideLimitVmd);
            if (recorder == null || !HasMmdIkDeltaGuardLimitOverride(normalizedLimit))
            {
                return false;
            }

            recorder.ClampMmdIkExportDeltaSpikes = true;
            float normalizedRecoveryTrigger = NormalizeMmdIkDeltaGuardLimitOverride(recoveryTriggerVmd);
            if (HasMmdIkDeltaGuardLimitOverride(normalizedRecoveryTrigger))
            {
                recorder.UseMmdIkExportDeltaRecoveryLimit = true;
                recorder.MmdIkExportDeltaRecoveryLimitPerFrame = normalizedLimit;
                recorder.MmdIkExportDeltaRecoveryTriggerPerFrame = normalizedRecoveryTrigger;
                recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame =
                    NormalizeMmdIkDeltaGuardLimitOverride(recoveryDebtThresholdVmd);
                recorder.MmdIkExportDeltaRecoveryHoldFrames =
                    NormalizeMmdIkDeltaGuardRecoveryHoldFrames(recoveryHoldFrames);
                return true;
            }

            recorder.UseMmdIkExportDeltaRecoveryLimit = false;
            recorder.MmdIkExportDeltaRecoveryDebtThresholdPerFrame = 0f;
            recorder.MmdIkExportDeltaRecoveryHoldFrames = 0;
            recorder.MaxMmdFootIkExportDeltaPerFrame = normalizedLimit;
            recorder.MaxMmdToeIkExportDeltaPerFrame = normalizedLimit;
            return true;
        }

        private static bool ApplyFinalIkFootGroundingRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableFinalIkFootGroundingExperiment = enabled;

            if (!enabled && fileManager.targetCharacter != null)
            {
                GrounderBipedIK grounder = fileManager.targetCharacter.GetComponent<GrounderBipedIK>();
                if (grounder != null)
                {
                    grounder.weight = 0f;
                    grounder.enabled = false;
                }

                BipedIK bipedIk = fileManager.targetCharacter.GetComponent<BipedIK>();
                if (bipedIk != null)
                {
                    bipedIk.fixTransforms = false;
                    bipedIk.enabled = false;
                }
            }

            return true;
        }

        private static bool ApplyVmdPlaybackProbeRuntimeOverride(
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

        private static bool ApplyMainSceneRuntimeOverrides(FBXVmdPipeline fileManager)
        {
            if (fileManager == null)
            {
                return false;
            }

            if (_enableFinalIkFootGroundingRuntimeOverride)
            {
                ApplyFinalIkFootGroundingRuntimeOverride(fileManager, true);
            }

            if (_disableManualAnimatorFootLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorFootLocalRotationRuntimeOverride(fileManager, false);
            }
            else if (_enableManualAnimatorFootLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorFootLocalRotationRuntimeOverride(fileManager, true);
            }

            if (_disableManualAnimatorFullBodyPoseRuntimeOverride)
            {
                ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorFullBodyPoseReferenceWeight,
                    false,
                    false);
            }
            else if (_enableManualAnimatorFullBodyPoseRuntimeOverride ||
                     _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride ||
                     _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride ||
                     _manualAnimatorFullBodyPoseReferenceFrameGateStart > 0f ||
                     _manualAnimatorFullBodyPoseReferenceFrameGateEnd > 0f)
            {
                ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorFullBodyPoseReferenceWeight,
                    _manualAnimatorFullBodyPoseExcludeLowerBodyMusclesRuntimeOverride,
                    _manualAnimatorFullBodyPoseLowerBodyMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseLegTwistMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseRightArmMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseLeftArmMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnlyRuntimeOverride,
                    _manualAnimatorFullBodyPoseReferenceFrameGateStart,
                    _manualAnimatorFullBodyPoseReferenceFrameGateEnd);
            }

            if (_enableSetHumanPoseRightLegTwistOutputReferenceRuntimeOverride)
            {
                ApplySetHumanPoseRightLegTwistOutputRuntimeOverride(
                    fileManager,
                    true,
                    _setHumanPoseRightLegTwistOutputReferenceWeight,
                    _setHumanPoseRightLegTwistOutputReferenceMaxDelta);
            }

            if (_disableManualAnimatorBodyRotationRuntimeOverride)
            {
                ApplyManualAnimatorBodyRotationRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorBodyRotationReferenceWeight);
            }
            else if (_enableManualAnimatorBodyRotationRuntimeOverride)
            {
                ApplyManualAnimatorBodyRotationRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorBodyRotationReferenceWeight);
            }

            if (_enableManualAnimatorHandLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorHandLocalRotationRuntimeOverride(fileManager, true);
            }

            if (_enableManualAnimatorThumbLocalRotationRuntimeOverride)
            {
                ApplyManualAnimatorThumbLocalRotationRuntimeOverride(fileManager, true);
            }

            if (_enableManualAnimatorHandPalmFrameRuntimeOverride)
            {
                ApplyManualAnimatorHandPalmFrameRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorHandPalmFrameWeight);
            }

            if (_overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings)
            {
                ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
                    fileManager,
                    _enableRetargetPoseVisualSpikeSmoothingRuntimeOverride,
                    _retargetPoseVisualSpikeCurrentWeight,
                    _retargetPoseVisualSpikeForearmStretchClampMaxOffset);
            }

            if (_enableRetargetArmStretchClampRuntimeOverride)
            {
                ApplyRetargetArmStretchClampRuntimeOverride(
                    fileManager,
                    true,
                    _retargetArmStretchMuscleLimit);
            }

            if (_enableYybArmSwingLimitRuntimeOverride)
            {
                ApplyYybArmSwingLimitRuntimeOverride(
                    fileManager,
                    true,
                    _yybArmSwingLimitWeight,
                    _yybArmSwingMaxDownDot,
                    _yybArmSwingMinHandHorizontalRatio,
                    _yybArmSwingMaxHandBelowShoulderRatio,
                    _yybArmSwingHorizontalReachLimitWeight,
                    _yybArmSwingMaxHandHorizontalReachRatio,
                    _yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                    _yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                    _yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                    _yybArmSwingRaisedPoseMinUpperArmDownDot,
                    _yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                    _yybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
            }

            if (_enableYybArmDirectionRetargetRuntimeOverride)
            {
                ApplyYybArmDirectionRetargetRuntimeOverride(
                    fileManager,
                    true,
                    _yybArmDirectionUpperArmWeight,
                    _yybArmDirectionForearmWeight,
                    _yybArmDirectionUpperArmMaxDegrees,
                    _yybArmDirectionForearmMaxDegrees,
                    _yybArmDirectionLeftSideWeightScale,
                    _yybArmDirectionRightSideWeightScale);
            }

            if (_overrideYybArmSleeveAnchorRuntimeSettings)
            {
                ApplyYybArmSleeveAnchorRuntimeOverride(
                    fileManager,
                    _enableYybArmSleeveAnchorRuntimeOverride,
                    _yybArmSleeveAnchorInfluence,
                    _yybArmShoulderCapAnchorInfluence,
                    _yybArmSleeveAnchorMaxDegrees);
            }

            if (_overrideYybArmVisualTwistRuntimeSettings)
            {
                ApplyYybArmVisualTwistRuntimeOverride(
                    fileManager,
                    _enableYybArmVisualTwistRuntimeOverride,
                    _yybArmVisualUpperArmInfluence,
                    _yybArmVisualForearmInfluence,
                    _yybArmVisualUpperArmMaxDegrees,
                    _yybArmVisualForearmMaxDegrees);
            }

            if (_disableManualAnimatorLowerBodySegmentDirectionRuntimeOverride)
            {
                ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                    _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                    _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                    _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                    _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                    _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            }
            else if (_enableManualAnimatorLowerBodySegmentDirectionRuntimeOverride)
            {
                ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                    _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride,
                    _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                    _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride,
                    _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                    _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride,
                    _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            }
            else if (HasManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride())
            {
                ApplyManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride(fileManager);
            }

            if (_disableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride)
            {
                ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
                    fileManager,
                    false,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            }
            else if (_enableManualAnimatorFootHipsAlignedResidualYawRuntimeOverride)
            {
                ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                    _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            }

            if (_enablePostSetHumanPoseRightEndpointPositionRuntimeOverride)
            {
                ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                    fileManager,
                    true,
                    _postSetHumanPoseRightEndpointPositionReferenceWeight,
                    _postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    _postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                    _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                    _postSetHumanPoseEndpointPositionUseLeftSide,
                    _usePostSetHumanPoseRightFootEvaluatorXzReference,
                    _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            }

            if (_enablePreSetHumanPoseRightEndpointPositionRuntimeOverride)
            {
                ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
                    fileManager,
                    true,
                    _preSetHumanPoseRightEndpointPositionReferenceWeight,
                    _preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    _preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                    _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                    _preSetHumanPoseEndpointPositionUseLeftSide,
                    _preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                    _preSetHumanPoseEndpointPositionInvertBodyPositionX,
                    _preSetHumanPoseEndpointPositionInvertBodyPositionZ);
            }

            if (_enableManualAnimatorBipedIkFootPositionRuntimeOverride)
            {
                ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorBipedIkFootPositionReferenceWeight,
                    _manualAnimatorBipedIkFootPositionReferenceMaxOffset);
            }

            if (_enableManualAnimatorHipsLocalPositionRuntimeOverride)
            {
                ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorHipsLocalPositionReferenceWeight,
                    _manualAnimatorHipsLocalPositionReferenceMaxOffset);
            }

            if (_enableManualAnimatorBodyPositionXzRuntimeOverride)
            {
                ApplyManualAnimatorBodyPositionXzRuntimeOverride(
                    fileManager,
                    true,
                    _manualAnimatorBodyPositionXzReferenceWeight,
                    _manualAnimatorBodyPositionXzReferenceMaxOffset,
                    _manualAnimatorBodyPositionXzReferenceFrameGateStart,
                    _manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                    _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                    _manualAnimatorBodyPositionXzReferenceAxisXScale,
                    _manualAnimatorBodyPositionXzReferenceAxisZScale);
            }

            if (_enableYybRightSleeveSilhouetteOffsetRuntimeOverride)
            {
                ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
                    fileManager,
                    true,
                    _yybRightSleeveSilhouetteLocalOffsetX,
                    _yybRightSleeveSilhouetteLocalOffsetFrameGateStart,
                    _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd);
            }

            if (_enableRetargetBodyPositionXzRootMotionRuntimeOverride)
            {
                ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(fileManager, true);
            }

            if (_disableTargetHumanoidBonePositionLockRuntimeOverride)
            {
                ApplyTargetHumanoidBonePositionLockRuntimeOverride(fileManager, false);
            }

            return true;
        }

        private static bool ApplyManualAnimatorFootLocalRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseManualAnimatorFootLocalRotationReference = enabled;
            fileManager.manualAnimatorFootLocalRotationReferenceWeight = enabled ? 1f : 0f;
            return true;
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorFullBodyPoseReferenceWeight);
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight)
        {
            return ApplyManualAnimatorFullBodyPoseRuntimeOverride(
                fileManager,
                enabled,
                weight,
                false,
                false);
        }

        private static bool ApplyManualAnimatorFullBodyPoseRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            bool excludeLowerBodyMuscles,
            bool lowerBodyMusclesOnly = false,
            bool legTwistMusclesOnly = false,
            bool rightArmMusclesOnly = false,
            bool leftArmMusclesOnly = false,
            bool rightSleeveChainMusclesOnly = false,
            float frameGateStart = DefaultManualAnimatorFullBodyPoseReferenceFrameGateStart,
            float frameGateEnd = DefaultManualAnimatorFullBodyPoseReferenceFrameGateEnd)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseManualAnimatorFullBodyPoseReference = enabled;
            fileManager.manualAnimatorFullBodyPoseReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.ShouldExcludeManualAnimatorFullBodyLowerMuscles = enabled && excludeLowerBodyMuscles;
            fileManager.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly = enabled && lowerBodyMusclesOnly;
            fileManager.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = enabled && legTwistMusclesOnly;
            fileManager.manualAnimatorFullBodyPoseRightArmMusclesOnly = enabled && rightArmMusclesOnly;
            fileManager.manualAnimatorFullBodyPoseLeftArmMusclesOnly = enabled && leftArmMusclesOnly;
            fileManager.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly =
                enabled && rightSleeveChainMusclesOnly;
            fileManager.manualAnimatorFullBodyPoseFrameGateStart = enabled ? Mathf.Max(0f, frameGateStart) : 0f;
            fileManager.manualAnimatorFullBodyPoseFrameGateEnd = enabled ? Mathf.Max(0f, frameGateEnd) : 0f;
            return true;
        }

        private static bool ApplySetHumanPoseRightLegTwistOutputRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxDelta)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseSetHumanPoseRightLegTwistOutputReference = enabled;
            fileManager.setHumanPoseRightLegTwistOutputReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.setHumanPoseRightLegTwistOutputReferenceMaxDelta = Mathf.Max(0f, maxDelta);
            return true;
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorBodyRotationRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorBodyRotationReferenceWeight);
        }

        private static bool ApplyManualAnimatorBodyRotationRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseManualAnimatorBodyRotationReference = enabled;
            fileManager.manualAnimatorBodyRotationReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            return true;
        }

        private static bool ApplyManualAnimatorHandLocalRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorHandLocalRotationReference = enabled;
            return true;
        }

        private static bool ApplyManualAnimatorThumbLocalRotationRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorThumbLocalRotationReference = enabled;
            return true;
        }

        private static bool ApplyManualAnimatorHandPalmFrameRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorHandPalmFrameReference = enabled;
            fileManager.manualAnimatorHandPalmFrameWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            return true;
        }

        private static bool ApplyRetargetPoseVisualSpikeSmoothingRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float currentWeight,
            float forearmStretchClampMaxOffset)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.smoothRetargetPoseOnVisualStepSpike = enabled;
            fileManager.RetargetPoseVisualSpikeCurrentWeight = Mathf.Clamp(currentWeight, 0.1f, 1f);
            fileManager.RetargetPoseVisualSpikeForearmStretchClampMaxOffset =
                Mathf.Clamp01(forearmStretchClampMaxOffset);
            return true;
        }

        private static bool ApplyRetargetArmStretchClampRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float stretchLimit)
        {
            if (fileManager == null)
            {
                return false;
            }

            SetSerializedBoolean(fileManager, "_shouldEnableAnatomicalArmGuard", true);
            fileManager.clampRetargetArmStretchMuscles = enabled;
            fileManager.targetGuardClampAnatomicalArmMuscles = enabled;
            fileManager.targetGuardClampArmStretchMuscles = enabled;
            fileManager.ArmStretchMuscleLimit = enabled
                ? Mathf.Clamp(stretchLimit, 0f, DefaultRetargetArmStretchMuscleLimit)
                : 0f;
            return true;
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                DefaultYybArmSwingHorizontalReachLimitWeight,
                DefaultYybArmSwingMaxHandHorizontalReachRatio,
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply,
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
                DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                horizontalReachLimitWeight,
                maxHandHorizontalReachRatio,
                DefaultYybArmSwingHorizontalReachMaxHandBelowShoulderRatio);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio,
            float horizontalReachMaxHandBelowShoulderRatio)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                horizontalReachLimitWeight,
                maxHandHorizontalReachRatio,
                horizontalReachMaxHandBelowShoulderRatio,
                DefaultYybArmSwingHorizontalReachMinElbowAngleAfterApply);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio,
            float horizontalReachMaxHandBelowShoulderRatio,
            float horizontalReachMinElbowAngleAfterApply)
        {
            return ApplyYybArmSwingLimitRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxDownDot,
                minHandHorizontalRatio,
                maxHandBelowShoulderRatio,
                horizontalReachLimitWeight,
                maxHandHorizontalReachRatio,
                horizontalReachMaxHandBelowShoulderRatio,
                horizontalReachMinElbowAngleAfterApply,
                DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
                DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
                DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio);
        }

        private static bool ApplyYybArmSwingLimitRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxDownDot,
            float minHandHorizontalRatio,
            float maxHandBelowShoulderRatio,
            float horizontalReachLimitWeight,
            float maxHandHorizontalReachRatio,
            float horizontalReachMaxHandBelowShoulderRatio,
            float horizontalReachMinElbowAngleAfterApply,
            float raisedPoseHorizontalReachLimitWeight = DefaultYybArmSwingRaisedPoseHorizontalReachLimitWeight,
            float raisedPoseMinUpperArmDownDot = DefaultYybArmSwingRaisedPoseMinUpperArmDownDot,
            float raisedPoseMaxHandBelowShoulderRatio = DefaultYybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
            float raisedPoseMaxHandHorizontalReachRatio = DefaultYybArmSwingRaisedPoseMaxHandHorizontalReachRatio)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmSwingLimitCorrection = enabled;
            fileManager.YybArmSwingLimitWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.YybArmSwingMaxDownDot = Mathf.Clamp01(maxDownDot);
            fileManager.YybArmSwingMinHandHorizontalRatio = Mathf.Clamp(minHandHorizontalRatio, 0f, 1.5f);
            fileManager.YybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp(maxHandBelowShoulderRatio, 0f, 1.5f);
            fileManager.YybArmSwingHorizontalReachLimitWeight = enabled
                ? Mathf.Clamp01(horizontalReachLimitWeight)
                : 0f;
            fileManager.YybArmSwingMaxHandHorizontalReachRatio = enabled
                ? Mathf.Clamp(maxHandHorizontalReachRatio, 0f, 1.5f)
                : 0f;
            fileManager.YybArmSwingHorizontalReachMaxHandBelowShoulderRatio = enabled
                ? Mathf.Clamp(horizontalReachMaxHandBelowShoulderRatio, 0f, 1.5f)
                : 0f;
            fileManager.YybArmSwingHorizontalReachMinElbowAngleAfterApply = enabled
                ? Mathf.Clamp(horizontalReachMinElbowAngleAfterApply, 0f, 180f)
                : 0f;
            fileManager.YybArmSwingRaisedPoseHorizontalReachLimitWeight = enabled
                ? Mathf.Clamp01(raisedPoseHorizontalReachLimitWeight)
                : 0f;
            fileManager.YybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01(raisedPoseMinUpperArmDownDot);
            fileManager.YybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp(
                raisedPoseMaxHandBelowShoulderRatio,
                0f,
                1.5f);
            fileManager.YybArmSwingRaisedPoseMaxHandHorizontalReachRatio = enabled
                ? Mathf.Clamp(raisedPoseMaxHandHorizontalReachRatio, 0f, 1.5f)
                : 0f;
            return true;
        }

        private static bool ApplyYybArmDirectionRetargetRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            return ApplyYybArmDirectionRetargetRuntimeOverride(
                fileManager,
                enabled,
                upperArmWeight,
                forearmWeight,
                upperArmMaxDegrees,
                forearmMaxDegrees,
                DefaultYybArmDirectionLeftSideWeightScale,
                DefaultYybArmDirectionRightSideWeightScale);
        }

        private static bool ApplyYybArmDirectionRetargetRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float upperArmWeight,
            float forearmWeight,
            float upperArmMaxDegrees,
            float forearmMaxDegrees,
            float leftSideWeightScale,
            float rightSideWeightScale)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmDirectionRetargetCorrection = enabled;
            fileManager.YybArmDirectionUpperArmWeight = enabled ? Mathf.Clamp01(upperArmWeight) : 0f;
            fileManager.YybArmDirectionForearmWeight = enabled ? Mathf.Clamp01(forearmWeight) : 0f;
            fileManager.YybArmDirectionUpperArmMaxDegrees = Mathf.Clamp(upperArmMaxDegrees, 0f, 120f);
            fileManager.YybArmDirectionForearmMaxDegrees = Mathf.Clamp(forearmMaxDegrees, 0f, 120f);
            fileManager.YybArmDirectionLeftSideWeightScale = enabled ? Mathf.Clamp01(leftSideWeightScale) : 0f;
            fileManager.YybArmDirectionRightSideWeightScale = enabled ? Mathf.Clamp01(rightSideWeightScale) : 0f;
            return true;
        }

        private static bool ApplyYybArmSleeveAnchorRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float sleeveInfluence,
            float shoulderCapInfluence,
            float maxDegrees)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmSleeveAnchorCorrection = enabled;
            fileManager.YybArmSleeveAnchorInfluence = enabled ? Mathf.Clamp01(sleeveInfluence) : 0f;
            fileManager.YybArmShoulderCapAnchorInfluence = enabled ? Mathf.Clamp01(shoulderCapInfluence) : 0f;
            fileManager.YybArmSleeveAnchorMaxDegrees = Mathf.Clamp(maxDegrees, 0f, 120f);
            return true;
        }

        private static bool ApplyYybArmVisualTwistRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float upperArmInfluence,
            float forearmInfluence,
            float upperArmMaxDegrees,
            float forearmMaxDegrees)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.enableYybArmVisualTwistCorrection = enabled;
            fileManager.YybArmVisualUpperArmInfluence = enabled ? Mathf.Clamp01(upperArmInfluence) : 0f;
            fileManager.YybArmVisualForearmInfluence = enabled ? Mathf.Clamp01(forearmInfluence) : 0f;
            fileManager.YybArmVisualUpperArmMaxDegrees = Mathf.Clamp(upperArmMaxDegrees, 0f, 120f);
            fileManager.YybArmVisualForearmMaxDegrees = Mathf.Clamp(forearmMaxDegrees, 0f, 120f);
            return true;
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                leftLowerLegToFootMaxAngle,
                rightLowerLegToFootMaxAngle,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes,
                footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg: false,
                upperLegToLowerLegMaxAngle: DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableLowerLegToFoot: false,
                lowerLegToFootMaxAngle: DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                leftLowerLegToFootMaxAngle: DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootMaxAngle: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootAxisXzScale: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                rightLowerLegToFootBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                rightLowerLegToFootFrameGateStart: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                rightLowerLegToFootFrameGateEnd: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                rightLowerLegToFootEndpointBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes: false,
                footToToesMaxAngle: DefaultManualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg: false,
                upperLegToLowerLegMaxAngle: DefaultManualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                disableLowerLegToFoot: false,
                lowerLegToFootMaxAngle: DefaultManualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                leftLowerLegToFootMaxAngle: DefaultManualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootMaxAngle: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                rightLowerLegToFootAxisXzScale: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                rightLowerLegToFootBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                rightLowerLegToFootFrameGateStart: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                rightLowerLegToFootFrameGateEnd: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                rightLowerLegToFootEndpointBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes: disableFootToToes,
                footToToesMaxAngle: footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            float rightLowerLegToFootAxisXzScale,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                leftLowerLegToFootMaxAngle,
                rightLowerLegToFootMaxAngle,
                rightLowerLegToFootAxisXzScale,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes,
                footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            return ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxAngle,
                disableUpperLegToLowerLeg,
                upperLegToLowerLegMaxAngle,
                disableLowerLegToFoot,
                lowerLegToFootMaxAngle,
                leftLowerLegToFootMaxAngle: 0f,
                rightLowerLegToFootMaxAngle: 0f,
                rightLowerLegToFootAxisXzScale: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                rightLowerLegToFootBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                rightLowerLegToFootFrameGateStart: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                rightLowerLegToFootFrameGateEnd: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                rightLowerLegToFootEndpointBlendWeight: DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                disableFootToToes: disableFootToToes,
                footToToesMaxAngle: footToToesMaxAngle);
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            float rightLowerLegToFootAxisXzScale,
            float rightLowerLegToFootBlendWeight,
            float rightLowerLegToFootFrameGateStart,
            float rightLowerLegToFootFrameGateEnd,
            float rightLowerLegToFootEndpointBlendWeight,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = enabled;
            fileManager.manualAnimatorLowerBodySegmentDirectionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = Mathf.Max(0f, maxAngle);
            fileManager.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                enabled && disableUpperLegToLowerLeg;
            fileManager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, upperLegToLowerLegMaxAngle);
            fileManager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference =
                enabled && disableLowerLegToFoot;
            fileManager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, lowerLegToFootMaxAngle);
            fileManager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, leftLowerLegToFootMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, rightLowerLegToFootMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                Mathf.Clamp01(rightLowerLegToFootAxisXzScale);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                Mathf.Clamp01(rightLowerLegToFootBlendWeight);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                Mathf.Max(0f, rightLowerLegToFootFrameGateStart);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                Mathf.Max(0f, rightLowerLegToFootFrameGateEnd);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                Mathf.Clamp01(rightLowerLegToFootEndpointBlendWeight);
            fileManager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = enabled && disableFootToToes;
            fileManager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = Mathf.Max(0f, footToToesMaxAngle);
            return true;
        }

        private static bool HasManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride()
        {
            return _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride ||
                _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle > 0f ||
                _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride ||
                _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle > 0f ||
                _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle > 0f ||
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle > 0f ||
                Mathf.Abs(
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale -
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale) > 0.0001f ||
                Mathf.Abs(
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight -
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight) > 0.0001f ||
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart > 0f ||
                _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd > 0f ||
                Mathf.Abs(
                    _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight -
                    DefaultManualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight) > 0.0001f ||
                _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride ||
                _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle > 0f;
        }

        private static bool ApplyManualAnimatorLowerBodySegmentDirectionDetailRuntimeOverride(
            FBXVmdPipeline fileManager)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                _disableManualAnimatorUpperLegToLowerLegSegmentDirectionRuntimeOverride;
            fileManager.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            fileManager.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference =
                _disableManualAnimatorLowerLegToFootSegmentDirectionRuntimeOverride;
            fileManager.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            fileManager.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                Mathf.Clamp01(_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                Mathf.Clamp01(_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                Mathf.Max(0f, _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                Mathf.Max(0f, _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            fileManager.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                Mathf.Clamp01(_manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
            fileManager.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference =
                _disableManualAnimatorFootToToesSegmentDirectionRuntimeOverride;
            fileManager.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            return true;
        }

        private static bool ApplyManualAnimatorFootHipsAlignedResidualYawRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxAngle)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = enabled;
            fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = Mathf.Max(0f, maxAngle);
            return true;
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorBipedIkFootPositionReferenceWeight,
                DefaultManualAnimatorBipedIkFootPositionReferenceMaxOffset);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                DefaultPostSetHumanPoseRightEndpointPositionReferencePositiveZScale);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float frameGateStart,
            float frameGateEnd)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                DefaultPostSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                frameGateStart,
                frameGateEnd);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide = false)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                frameGateStart,
                frameGateEnd,
                useLeftSide,
                evaluatorXzReferenceEnabled: false,
                evaluatorXzTargetMagnitude:
                    DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd)
        {
            return ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
                fileManager,
                enabled,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                frameGateStart,
                frameGateEnd,
                useLeftSide: false,
                evaluatorXzReferenceEnabled: false,
                evaluatorXzTargetMagnitude:
                    DefaultPostSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
        }

        private static bool ApplyPostSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide,
            bool evaluatorXzReferenceEnabled,
            float evaluatorXzTargetMagnitude)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.usePostSetHumanPoseRightEndpointPositionReference = enabled;
            fileManager.postSetHumanPoseRightEndpointPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.postSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            fileManager.postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(positiveZScale);
            fileManager.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(toesBlendWeight);
            fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            fileManager.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            fileManager.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = enabled && useLeftSide;
            fileManager.usePostSetHumanPoseRightFootEvaluatorXzReference =
                enabled && evaluatorXzReferenceEnabled;
            fileManager.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                Mathf.Max(0f, evaluatorXzTargetMagnitude);
            return true;
        }

        private static bool ApplyPreSetHumanPoseEndpointPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide,
            bool useGhostCurrentBasis,
            bool invertBodyPositionX,
            bool invertBodyPositionZ)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.usePreSetHumanPoseRightEndpointPositionReference = enabled;
            fileManager.preSetHumanPoseRightEndpointPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.preSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            fileManager.preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(positiveZScale);
            fileManager.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(toesBlendWeight);
            fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            fileManager.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            fileManager.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = enabled && useLeftSide;
            fileManager.preSetHumanPoseEndpointPositionUseGhostCurrentBasis = enabled && useGhostCurrentBasis;
            fileManager.ShouldInvertPreSetHumanPoseEndpointPositionBodyX = enabled && invertBodyPositionX;
            fileManager.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = enabled && invertBodyPositionZ;
            return true;
        }

        private static bool ApplyManualAnimatorBipedIkFootPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useManualAnimatorBipedIkFootPositionReference = enabled;
            fileManager.manualAnimatorBipedIkFootPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorBipedIkFootPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            return true;
        }

        private static bool ApplyManualAnimatorHipsLocalPositionRuntimeOverride(FBXVmdPipeline fileManager, bool enabled)
        {
            return ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
                fileManager,
                enabled,
                DefaultManualAnimatorHipsLocalPositionReferenceWeight,
                DefaultManualAnimatorHipsLocalPositionReferenceMaxOffset);
        }

        private static bool ApplyManualAnimatorHipsLocalPositionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseManualAnimatorHipsLocalPositionReference = enabled;
            fileManager.manualAnimatorHipsLocalPositionWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorHipsLocalPositionMaxOffset = Mathf.Max(0f, maxOffset);
            return true;
        }

        private static bool ApplyManualAnimatorBodyPositionXzRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float weight,
            float maxOffset,
            float frameGateStart,
            float frameGateEnd,
            float frameGateBlendFrames,
            float axisXScale,
            float axisZScale)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseManualAnimatorBodyPositionXzReference = enabled;
            fileManager.manualAnimatorBodyPositionXzReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            fileManager.manualAnimatorBodyPositionXzReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            fileManager.manualAnimatorBodyPositionXzReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            fileManager.manualAnimatorBodyPositionXzReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            fileManager.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = Mathf.Max(0f, frameGateBlendFrames);
            fileManager.manualAnimatorBodyPositionXzReferenceAxisXScale = Mathf.Clamp01(axisXScale);
            fileManager.manualAnimatorBodyPositionXzReferenceAxisZScale = Mathf.Clamp01(axisZScale);
            return true;
        }

        private static bool ApplyYybRightSleeveSilhouetteOffsetRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled,
            float localOffsetX,
            float frameGateStart,
            float frameGateEnd)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.useYybRightSleeveSilhouetteLocalOffsetReference = enabled;
            fileManager.yybRightSleeveSilhouetteLocalOffsetX = Mathf.Clamp(localOffsetX, -0.2f, 0.2f);
            fileManager.yybRightSleeveSilhouetteLocalOffsetFrameGateStart =
                Mathf.Clamp(frameGateStart, 0f, 6000f);
            fileManager.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd =
                Mathf.Clamp(frameGateEnd, 0f, 6000f);
            return true;
        }

        private static bool ApplyTargetHumanoidBonePositionLockRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            SetSerializedBoolean(fileManager, "_shouldLockTargetHumanoidBonePositions", enabled);
            return true;
        }

        private static void SetSerializedBoolean(FBXVmdPipeline fileManager, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(fileManager);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"FBXVmdPipeline 직렬화 bool 필드를 찾을 수 없습니다: {propertyName}");
            }

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool ApplyRetargetBodyPositionXzRootMotionRuntimeOverride(
            FBXVmdPipeline fileManager,
            bool enabled)
        {
            if (fileManager == null)
            {
                return false;
            }

            fileManager.ShouldUseRetargetBodyPositionXZRootMotion = enabled;
            return true;
        }

        private static float NormalizeMmdIkDeltaGuardLimitOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return NoMmdIkDeltaGuardLimitOverrideVmd;
            }

            return value;
        }

        private static float NormalizePositiveFloat(float value, float fallbackValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallbackValue;
            }

            return value;
        }

        private static float NormalizeFiniteFloat(float value, float fallbackValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallbackValue;
            }

            return value;
        }

        private static bool HasMmdIkDeltaGuardLimitOverride(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
        private static int NormalizeMmdIkDeltaGuardRecoveryHoldFrames(int value)
        {
            return value > 0 ? value : 0;
        }
    }
}
#endif
