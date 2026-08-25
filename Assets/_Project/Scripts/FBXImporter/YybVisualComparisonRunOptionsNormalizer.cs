using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonRunOptionsNormalizer
    {
        internal static YybVisualComparisonRunOptions Normalize(
            YybVisualComparisonRunOptions options,
            YybVisualComparisonRunOptions defaults,
            float frameRate)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (defaults == null)
            {
                throw new ArgumentNullException(nameof(defaults));
            }

            if (frameRate <= 0f || float.IsNaN(frameRate) || float.IsInfinity(frameRate))
            {
                throw new ArgumentOutOfRangeException(nameof(frameRate), frameRate, "프레임 속도는 양수여야 합니다.");
            }

            options.fbxFileName = FbxReferenceClipPathResolver.NormalizeFileName(options.fbxFileName, defaults.fbxFileName);
            options.durationSeconds = Mathf.Max(0.1f, options.durationSeconds);
            options.targetFrameCount = Mathf.Max(1, Mathf.CeilToInt(options.durationSeconds * frameRate));
            options.mmdIkDeltaGuardLimitOverrideVmd = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeLimit(options.mmdIkDeltaGuardLimitOverrideVmd);
            options.mmdIkDeltaGuardRecoveryTriggerVmd = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeLimit(options.mmdIkDeltaGuardRecoveryTriggerVmd);
            options.mmdIkDeltaGuardRecoveryDebtThresholdVmd = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeLimit(options.mmdIkDeltaGuardRecoveryDebtThresholdVmd);
            options.mmdIkDeltaGuardRecoveryHoldFrames = VmdIkDeltaGuardRuntimeOverrideApplier.NormalizeRecoveryHoldFrames(options.mmdIkDeltaGuardRecoveryHoldFrames);
            options.manualAnimatorFullBodyPoseReferenceWeight = Mathf.Clamp01(options.manualAnimatorFullBodyPoseReferenceWeight);
            options.manualAnimatorFullBodyPoseReferenceFrameGateStart = Mathf.Max(0f, options.manualAnimatorFullBodyPoseReferenceFrameGateStart);
            options.manualAnimatorFullBodyPoseReferenceFrameGateEnd = Mathf.Max(0f, options.manualAnimatorFullBodyPoseReferenceFrameGateEnd);
            options.setHumanPoseRightLegTwistOutputReferenceWeight = Mathf.Clamp01( options.setHumanPoseRightLegTwistOutputReferenceWeight);
            options.setHumanPoseRightLegTwistOutputReferenceMaxDelta = Mathf.Max(0f, options.setHumanPoseRightLegTwistOutputReferenceMaxDelta);
            options.manualAnimatorBodyRotationReferenceWeight = Mathf.Clamp01(options.manualAnimatorBodyRotationReferenceWeight);
            options.manualAnimatorHandPalmFrameWeight = Mathf.Clamp01(options.manualAnimatorHandPalmFrameWeight);
            options.retargetPoseVisualSpikeCurrentWeight = Mathf.Clamp( options.retargetPoseVisualSpikeCurrentWeight, 0.1f, 1f);
            options.retargetPoseVisualSpikeForearmStretchClampMaxOffset = Mathf.Clamp01(options.retargetPoseVisualSpikeForearmStretchClampMaxOffset);
            options.retargetArmStretchMuscleLimit = Mathf.Clamp( options.retargetArmStretchMuscleLimit, 0f, defaults.retargetArmStretchMuscleLimit);
            options.yybArmSwingLimitWeight = Mathf.Clamp01(options.yybArmSwingLimitWeight);
            options.yybArmSwingMaxDownDot = Mathf.Clamp01(options.yybArmSwingMaxDownDot);
            options.yybArmSwingMinHandHorizontalRatio = Mathf.Clamp(options.yybArmSwingMinHandHorizontalRatio, 0f, 1.5f);
            options.yybArmSwingMaxHandBelowShoulderRatio = Mathf.Clamp( options.yybArmSwingMaxHandBelowShoulderRatio, 0f, 1.5f);
            options.yybArmSwingHorizontalReachLimitWeight = Mathf.Clamp01(options.yybArmSwingHorizontalReachLimitWeight);
            options.yybArmSwingMaxHandHorizontalReachRatio = Mathf.Clamp( options.yybArmSwingMaxHandHorizontalReachRatio, 0f, 1.5f);
            options.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio = Mathf.Clamp( options.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio, 0f, 1.5f);
            options.yybArmSwingHorizontalReachMinElbowAngleAfterApply = Mathf.Clamp( options.yybArmSwingHorizontalReachMinElbowAngleAfterApply, 0f, 180f);
            options.yybArmSwingRaisedPoseHorizontalReachLimitWeight = Mathf.Clamp01( options.yybArmSwingRaisedPoseHorizontalReachLimitWeight);
            options.yybArmSwingRaisedPoseMinUpperArmDownDot = Mathf.Clamp01( options.yybArmSwingRaisedPoseMinUpperArmDownDot);
            options.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp( options.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio, 0f, 1.5f);
            options.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio = Mathf.Clamp( options.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio, 0f, 1.5f);
            options.yybArmDirectionUpperArmWeight = Mathf.Clamp01(options.yybArmDirectionUpperArmWeight);
            options.yybArmDirectionForearmWeight = Mathf.Clamp01(options.yybArmDirectionForearmWeight);
            options.yybArmDirectionUpperArmMaxDegrees = Mathf.Clamp(options.yybArmDirectionUpperArmMaxDegrees, 0f, 120f);
            options.yybArmDirectionForearmMaxDegrees = Mathf.Clamp(options.yybArmDirectionForearmMaxDegrees, 0f, 120f);
            options.yybArmDirectionLeftSideWeightScale = Mathf.Clamp01(options.yybArmDirectionLeftSideWeightScale);
            options.yybArmDirectionRightSideWeightScale = Mathf.Clamp01(options.yybArmDirectionRightSideWeightScale);
            options.yybArmSleeveAnchorInfluence = Mathf.Clamp01(options.yybArmSleeveAnchorInfluence);
            options.yybArmShoulderCapAnchorInfluence = Mathf.Clamp01(options.yybArmShoulderCapAnchorInfluence);
            options.yybArmSleeveAnchorMaxDegrees = Mathf.Clamp(options.yybArmSleeveAnchorMaxDegrees, 0f, 120f);
            options.yybArmVisualUpperArmInfluence = Mathf.Clamp01(options.yybArmVisualUpperArmInfluence);
            options.yybArmVisualForearmInfluence = Mathf.Clamp01(options.yybArmVisualForearmInfluence);
            options.yybArmVisualUpperArmMaxDegrees = Mathf.Clamp(options.yybArmVisualUpperArmMaxDegrees, 0f, 120f);
            options.yybArmVisualForearmMaxDegrees = Mathf.Clamp(options.yybArmVisualForearmMaxDegrees, 0f, 120f);
            options.manualAnimatorLowerBodySegmentDirectionReferenceWeight = Mathf.Clamp01( options.manualAnimatorLowerBodySegmentDirectionReferenceWeight);
            options.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = Mathf.Max( 0f, options.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
            options.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = Mathf.Max( 0f, options.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            options.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = Mathf.Max( 0f, options.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            options.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = Mathf.Max( 0f, options.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = Mathf.Max( 0f, options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
            options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = Mathf.Clamp01( options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale);
            options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = Mathf.Clamp01( options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
            options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = Mathf.Max( 0f, options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = Mathf.Max( 0f, options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = Mathf.Clamp01( options.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
            options.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = Mathf.Max( 0f, options.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            options.manualAnimatorFootHipsAlignedResidualYawReferenceWeight = Mathf.Clamp01( options.manualAnimatorFootHipsAlignedResidualYawReferenceWeight);
            options.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = Mathf.Max( 0f, options.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            options.postSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01( options.postSetHumanPoseRightEndpointPositionReferenceWeight);
            options.postSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max( 0f, options.postSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            options.postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01( options.postSetHumanPoseRightEndpointPositionReferencePositiveZScale);
            options.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01( options.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
            options.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max( 0f, options.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            options.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max( 0f, options.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            options.preSetHumanPoseRightEndpointPositionReferenceWeight = Mathf.Clamp01( options.preSetHumanPoseRightEndpointPositionReferenceWeight);
            options.preSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max( 0f, options.preSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            options.preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01( options.preSetHumanPoseRightEndpointPositionReferencePositiveZScale);
            options.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01( options.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
            options.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max( 0f, options.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            options.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max( 0f, options.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            options.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = Mathf.Max( 0f, options.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
            options.manualAnimatorBipedIkFootPositionReferenceWeight = Mathf.Clamp01( options.manualAnimatorBipedIkFootPositionReferenceWeight);
            options.manualAnimatorBipedIkFootPositionReferenceMaxOffset = Mathf.Max( 0f, options.manualAnimatorBipedIkFootPositionReferenceMaxOffset);
            options.manualAnimatorHipsLocalPositionReferenceWeight = Mathf.Clamp01( options.manualAnimatorHipsLocalPositionReferenceWeight);
            options.manualAnimatorHipsLocalPositionReferenceMaxOffset = Mathf.Max( 0f, options.manualAnimatorHipsLocalPositionReferenceMaxOffset);
            options.manualAnimatorBodyPositionXzReferenceWeight = Mathf.Clamp01( options.manualAnimatorBodyPositionXzReferenceWeight);
            options.manualAnimatorBodyPositionXzReferenceMaxOffset = Mathf.Max( 0f, options.manualAnimatorBodyPositionXzReferenceMaxOffset);
            options.manualAnimatorBodyPositionXzReferenceFrameGateStart = Mathf.Max( 0f, options.manualAnimatorBodyPositionXzReferenceFrameGateStart);
            options.manualAnimatorBodyPositionXzReferenceFrameGateEnd = Mathf.Max( 0f, options.manualAnimatorBodyPositionXzReferenceFrameGateEnd);
            options.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = Mathf.Max( 0f, options.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames);
            options.manualAnimatorBodyPositionXzReferenceAxisXScale = Mathf.Clamp01( options.manualAnimatorBodyPositionXzReferenceAxisXScale);
            options.manualAnimatorBodyPositionXzReferenceAxisZScale = Mathf.Clamp01( options.manualAnimatorBodyPositionXzReferenceAxisZScale);
            options.yybRightSleeveSilhouetteLocalOffsetX = Mathf.Clamp(options.yybRightSleeveSilhouetteLocalOffsetX, -0.2f, 0.2f);
            options.yybRightSleeveSilhouetteLocalOffsetFrameGateStart = Mathf.Clamp(options.yybRightSleeveSilhouetteLocalOffsetFrameGateStart, 0f, 6000f);
            options.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = Mathf.Clamp(options.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd, 0f, 6000f);
            options.applyVmdPlaybackProbeIkTargetsRuntimeOverride = options.enableVmdPlaybackProbeRuntimeOverride && options.applyVmdPlaybackProbeIkTargetsRuntimeOverride;
            options.vmdPlaybackProbeSourceVmdPath = string.Empty;
            options.diagnosticCaptureWidthOverride = VisualComparisonScreenshotOverridePolicy.NormalizeCaptureDimension(options.diagnosticCaptureWidthOverride, defaults.diagnosticCaptureWidthOverride);
            options.diagnosticCaptureHeightOverride = VisualComparisonScreenshotOverridePolicy.NormalizeCaptureDimension(options.diagnosticCaptureHeightOverride, defaults.diagnosticCaptureHeightOverride);
            options.diagnosticScreenshotPaddingOverride = VisualComparisonScreenshotOverridePolicy.NormalizePadding(options.diagnosticScreenshotPaddingOverride, defaults.diagnosticScreenshotPaddingOverride);
            options.diagnosticScreenshotVerticalViewportCenterOverride = VisualComparisonScreenshotOverridePolicy.NormalizeVerticalViewportCenter(options.diagnosticScreenshotVerticalViewportCenterOverride, defaults.diagnosticScreenshotVerticalViewportCenterOverride);
            return options;
        }
    }
}
