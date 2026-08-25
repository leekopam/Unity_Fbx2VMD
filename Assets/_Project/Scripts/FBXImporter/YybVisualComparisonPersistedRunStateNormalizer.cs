using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonPersistedRunStateNormalizer
    {
        internal static YybVisualComparisonRunStateData Normalize(
            YybVisualComparisonRunStateData state,
            YybVisualComparisonRunOptions defaults)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            state.fbxFileName = string.IsNullOrWhiteSpace(state.fbxFileName) ? defaults.fbxFileName : state.fbxFileName;
            state.durationSeconds = Mathf.Max(0.1f, state.durationSeconds);
            state.targetFrameCount = Mathf.Max(1, state.targetFrameCount);
            state.mmdIkDeltaGuardLimitOverrideVmd = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeLimit(state.mmdIkDeltaGuardLimitOverrideVmd);
            state.mmdIkDeltaGuardRecoveryTriggerVmd = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeLimit(state.mmdIkDeltaGuardRecoveryTriggerVmd);
            state.mmdIkDeltaGuardRecoveryDebtThresholdVmd = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeLimit(state.mmdIkDeltaGuardRecoveryDebtThresholdVmd);
            state.mmdIkDeltaGuardRecoveryHoldFrames = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeRecoveryHoldFrames(state.mmdIkDeltaGuardRecoveryHoldFrames);
            state.manualAnimatorFullBodyPoseReferenceWeight = float.IsNaN(state.manualAnimatorFullBodyPoseReferenceWeight) || float.IsInfinity(state.manualAnimatorFullBodyPoseReferenceWeight) ? defaults.manualAnimatorFullBodyPoseReferenceWeight : Mathf.Clamp01(state.manualAnimatorFullBodyPoseReferenceWeight);
            state.manualAnimatorFullBodyPoseReferenceFrameGateStart = Mathf.Max( 0f, VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorFullBodyPoseReferenceFrameGateStart, defaults.manualAnimatorFullBodyPoseReferenceFrameGateStart));
            state.manualAnimatorFullBodyPoseReferenceFrameGateEnd = Mathf.Max( 0f, VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorFullBodyPoseReferenceFrameGateEnd, defaults.manualAnimatorFullBodyPoseReferenceFrameGateEnd));
            state.setHumanPoseRightLegTwistOutputReferenceWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.setHumanPoseRightLegTwistOutputReferenceWeight, defaults.setHumanPoseRightLegTwistOutputReferenceWeight));
            state.setHumanPoseRightLegTwistOutputReferenceMaxDelta = Mathf.Max(0f, VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.setHumanPoseRightLegTwistOutputReferenceMaxDelta, defaults.setHumanPoseRightLegTwistOutputReferenceMaxDelta));
            state.manualAnimatorBodyRotationReferenceWeight = float.IsNaN(state.manualAnimatorBodyRotationReferenceWeight) || float.IsInfinity(state.manualAnimatorBodyRotationReferenceWeight) ? defaults.manualAnimatorBodyRotationReferenceWeight : Mathf.Clamp01(state.manualAnimatorBodyRotationReferenceWeight);
            state.manualAnimatorHandPalmFrameWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorHandPalmFrameWeight, defaults.manualAnimatorHandPalmFrameWeight));
            state.retargetPoseVisualSpikeCurrentWeight = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.retargetPoseVisualSpikeCurrentWeight, defaults.retargetPoseVisualSpikeCurrentWeight), 0.1f, 1f);
            state.retargetPoseVisualSpikeForearmStretchClampMaxOffset = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.retargetPoseVisualSpikeForearmStretchClampMaxOffset, defaults.retargetPoseVisualSpikeForearmStretchClampMaxOffset));
            state.retargetArmStretchMuscleLimit = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.retargetArmStretchMuscleLimit, defaults.retargetArmStretchMuscleLimit), 0f, defaults.retargetArmStretchMuscleLimit);
            state.yybArmSwingLimitWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingLimitWeight, defaults.yybArmSwingLimitWeight));
            state.yybArmSwingMaxDownDot = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingMaxDownDot, defaults.yybArmSwingMaxDownDot));
            state.yybArmSwingMinHandHorizontalRatio = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingMinHandHorizontalRatio, defaults.yybArmSwingMinHandHorizontalRatio), 0f, 1.5f);
            state.yybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingMaxHandBelowShoulderRatio, defaults.yybArmSwingMaxHandBelowShoulderRatio), 0f, 1.5f);
            state.yybArmSwingHorizontalReachLimitWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingHorizontalReachLimitWeight, defaults.yybArmSwingHorizontalReachLimitWeight));
            state.yybArmSwingMaxHandHorizontalReachRatio = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingMaxHandHorizontalReachRatio, defaults.yybArmSwingMaxHandHorizontalReachRatio), 0f, 1.5f);
            state.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio, defaults.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio), 0f, 1.5f);
            state.yybArmSwingHorizontalReachMinElbowAngleAfterApply = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingHorizontalReachMinElbowAngleAfterApply, defaults.yybArmSwingHorizontalReachMinElbowAngleAfterApply), 0f, 180f);
            state.yybArmSwingRaisedPoseHorizontalReachLimitWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingRaisedPoseHorizontalReachLimitWeight, defaults.yybArmSwingRaisedPoseHorizontalReachLimitWeight));
            state.yybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingRaisedPoseMinUpperArmDownDot, defaults.yybArmSwingRaisedPoseMinUpperArmDownDot));
            state.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio, defaults.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio), 0f, 1.5f);
            state.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio, defaults.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio), 0f, 1.5f);
            state.yybArmDirectionUpperArmWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmDirectionUpperArmWeight, defaults.yybArmDirectionUpperArmWeight));
            state.yybArmDirectionForearmWeight = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmDirectionForearmWeight, defaults.yybArmDirectionForearmWeight));
            state.yybArmDirectionUpperArmMaxDegrees = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmDirectionUpperArmMaxDegrees, defaults.yybArmDirectionUpperArmMaxDegrees), 0f, 120f);
            state.yybArmDirectionForearmMaxDegrees = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.yybArmDirectionForearmMaxDegrees, defaults.yybArmDirectionForearmMaxDegrees), 0f, 120f);
            state.yybArmDirectionLeftSideWeightScale = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmDirectionLeftSideWeightScale, defaults.yybArmDirectionLeftSideWeightScale));
            state.yybArmDirectionRightSideWeightScale = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmDirectionRightSideWeightScale, defaults.yybArmDirectionRightSideWeightScale));
            state.yybArmSleeveAnchorInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmSleeveAnchorInfluence, defaults.yybArmSleeveAnchorInfluence));
            state.yybArmShoulderCapAnchorInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmShoulderCapAnchorInfluence, defaults.yybArmShoulderCapAnchorInfluence));
            state.yybArmSleeveAnchorMaxDegrees = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmSleeveAnchorMaxDegrees, defaults.yybArmSleeveAnchorMaxDegrees), 0f, 120f);
            state.yybArmVisualUpperArmInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmVisualUpperArmInfluence, defaults.yybArmVisualUpperArmInfluence));
            state.yybArmVisualForearmInfluence = Mathf.Clamp01(VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmVisualForearmInfluence, defaults.yybArmVisualForearmInfluence));
            state.yybArmVisualUpperArmMaxDegrees = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmVisualUpperArmMaxDegrees, defaults.yybArmVisualUpperArmMaxDegrees), 0f, 120f);
            state.yybArmVisualForearmMaxDegrees = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybArmVisualForearmMaxDegrees, defaults.yybArmVisualForearmMaxDegrees), 0f, 120f);
            state.yybRightSleeveSilhouetteLocalOffsetX = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybRightSleeveSilhouetteLocalOffsetX, defaults.yybRightSleeveSilhouetteLocalOffsetX), -0.2f, 0.2f);
            state.yybRightSleeveSilhouetteLocalOffsetFrameGateStart = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybRightSleeveSilhouetteLocalOffsetFrameGateStart, defaults.yybRightSleeveSilhouetteLocalOffsetFrameGateStart), 0f, 6000f);
            state.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = Mathf.Clamp( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, defaults.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd), 0f, 6000f);
            state.manualAnimatorLowerBodySegmentDirectionReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorLowerBodySegmentDirectionReferenceWeight, defaults.manualAnimatorLowerBodySegmentDirectionReferenceWeight);
            state.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle, defaults.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
            state.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle, defaults.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            state.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle, defaults.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            state.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle, defaults.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle, defaults.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
            state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale, defaults.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale));
            state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight, defaults.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight));
            state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart, defaults.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd, defaults.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight, defaults.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight));
            state.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle, defaults.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            state.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorFootHipsAlignedResidualYawReferenceWeight, defaults.manualAnimatorFootHipsAlignedResidualYawReferenceWeight);
            state.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle, defaults.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            state.postSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.postSetHumanPoseRightEndpointPositionReferenceWeight, defaults.postSetHumanPoseRightEndpointPositionReferenceWeight));
            state.postSetHumanPoseRightEndpointPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.postSetHumanPoseRightEndpointPositionReferenceMaxOffset, defaults.postSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            state.postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.postSetHumanPoseRightEndpointPositionReferencePositiveZScale, defaults.postSetHumanPoseRightEndpointPositionReferencePositiveZScale));
            state.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, defaults.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight));
            state.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart, defaults.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            state.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, defaults.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            state.preSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.preSetHumanPoseRightEndpointPositionReferenceWeight, defaults.preSetHumanPoseRightEndpointPositionReferenceWeight));
            state.preSetHumanPoseRightEndpointPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.preSetHumanPoseRightEndpointPositionReferenceMaxOffset, defaults.preSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            state.preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.preSetHumanPoseRightEndpointPositionReferencePositiveZScale, defaults.preSetHumanPoseRightEndpointPositionReferencePositiveZScale));
            state.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight, defaults.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight));
            state.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart, defaults.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            state.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd, defaults.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            state.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude, defaults.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            state.manualAnimatorBipedIkFootPositionReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorBipedIkFootPositionReferenceWeight, defaults.manualAnimatorBipedIkFootPositionReferenceWeight);
            state.manualAnimatorBipedIkFootPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorBipedIkFootPositionReferenceMaxOffset, defaults.manualAnimatorBipedIkFootPositionReferenceMaxOffset);
            state.manualAnimatorHipsLocalPositionReferenceWeight = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorHipsLocalPositionReferenceWeight, defaults.manualAnimatorHipsLocalPositionReferenceWeight);
            state.manualAnimatorHipsLocalPositionReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorHipsLocalPositionReferenceMaxOffset, defaults.manualAnimatorHipsLocalPositionReferenceMaxOffset);
            state.manualAnimatorBodyPositionXzReferenceWeight = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorBodyPositionXzReferenceWeight, defaults.manualAnimatorBodyPositionXzReferenceWeight));
            state.manualAnimatorBodyPositionXzReferenceMaxOffset = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorBodyPositionXzReferenceMaxOffset, defaults.manualAnimatorBodyPositionXzReferenceMaxOffset);
            state.manualAnimatorBodyPositionXzReferenceFrameGateStart = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorBodyPositionXzReferenceFrameGateStart, defaults.manualAnimatorBodyPositionXzReferenceFrameGateStart);
            state.manualAnimatorBodyPositionXzReferenceFrameGateEnd = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorBodyPositionXzReferenceFrameGateEnd, defaults.manualAnimatorBodyPositionXzReferenceFrameGateEnd);
            state.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = VisualComparisonRuntimeValueNormalizer.NormalizePositive( state.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames, defaults.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames);
            state.manualAnimatorBodyPositionXzReferenceAxisXScale = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorBodyPositionXzReferenceAxisXScale, defaults.manualAnimatorBodyPositionXzReferenceAxisXScale));
            state.manualAnimatorBodyPositionXzReferenceAxisZScale = Mathf.Clamp01( VisualComparisonRuntimeValueNormalizer.NormalizeFinite( state.manualAnimatorBodyPositionXzReferenceAxisZScale, defaults.manualAnimatorBodyPositionXzReferenceAxisZScale));
            state.applyVmdPlaybackProbeIkTargetsRuntimeOverride = state.enableVmdPlaybackProbeRuntimeOverride && state.applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            state.vmdPlaybackProbeSourceVmdPath = state.vmdPlaybackProbeSourceVmdPath ?? string.Empty;
            state.diagnosticCaptureWidthOverride = VisualComparisonScreenshotOverridePolicy.NormalizeCaptureDimension(state.diagnosticCaptureWidthOverride, defaults.diagnosticCaptureWidthOverride);
            state.diagnosticCaptureHeightOverride = VisualComparisonScreenshotOverridePolicy.NormalizeCaptureDimension(state.diagnosticCaptureHeightOverride, defaults.diagnosticCaptureHeightOverride);
            state.diagnosticScreenshotPaddingOverride = VisualComparisonScreenshotOverridePolicy.NormalizePadding(state.diagnosticScreenshotPaddingOverride, defaults.diagnosticScreenshotPaddingOverride);
            state.diagnosticScreenshotVerticalViewportCenterOverride = VisualComparisonScreenshotOverridePolicy.NormalizeVerticalViewportCenter(state.diagnosticScreenshotVerticalViewportCenterOverride, defaults.diagnosticScreenshotVerticalViewportCenterOverride);
            return state;
        }
    }
}
