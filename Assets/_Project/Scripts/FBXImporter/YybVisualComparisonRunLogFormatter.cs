using System.Globalization;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonRunLogFormatter
    {
        internal static string BuildStartMessage(
            YybVisualComparisonRunOptions options,
            string segment,
            bool isBatchMode)
        {
            return
                $"[YybVisualComparisonBatchRunner] 시작: fbx={options.fbxFileName}, " +
                $"duration={options.durationSeconds:F2}s, targetFrames={options.targetFrameCount}, " +
                $"{string.Join(", ", BuildSharedFields(options, segment))}, " +
                $"batchMode={isBatchMode}";
        }

        internal static string BuildTraceMessage(
            YybVisualComparisonRunOptions options,
            string segment)
        {
            return
                $"run started fbx={options.fbxFileName} duration={options.durationSeconds:F2}s " +
                string.Join(" ", BuildSharedFields(options, segment));
        }

        private static string[] BuildSharedFields(
            YybVisualComparisonRunOptions options,
            string segment)
        {
            return new[]
            {
                $"fingerCloseups={options.enableFingerCloseups}",
                $"recorderParentIkOffsets={options.enableRecorderParentFrameIkOffsetsWhenCenterParented}",
                $"mmdIkDeltaGuardLimitOverrideVmd={FormatRuntimeOverride(options.mmdIkDeltaGuardLimitOverrideVmd)}",
                $"mmdIkDeltaGuardRecoveryTriggerVmd={FormatRuntimeOverride(options.mmdIkDeltaGuardRecoveryTriggerVmd)}",
                $"mmdIkDeltaGuardRecoveryDebtThresholdVmd={FormatRuntimeOverride(options.mmdIkDeltaGuardRecoveryDebtThresholdVmd)}",
                $"mmdIkDeltaGuardRecoveryHoldFrames={FormatRuntimeOverride(options.mmdIkDeltaGuardRecoveryHoldFrames)}",
                $"finalIkFootGrounding={options.enableFinalIkFootGroundingRuntimeOverride}",
                $"manualFootLocalRotation={options.enableManualAnimatorFootLocalRotationRuntimeOverride}",
                $"manualFullBodyPose={options.enableManualAnimatorFullBodyPoseRuntimeOverride}/{options.manualAnimatorFullBodyPoseReferenceWeight:F2}",
                $"manualBodyRotation={options.enableManualAnimatorBodyRotationRuntimeOverride}/{options.manualAnimatorBodyRotationReferenceWeight:F2}",
                $"manualHandLocalRotation={options.enableManualAnimatorHandLocalRotationRuntimeOverride}",
                $"manualThumbLocalRotation={options.enableManualAnimatorThumbLocalRotationRuntimeOverride}",
                $"manualHandPalmFrame={options.enableManualAnimatorHandPalmFrameRuntimeOverride}/{options.manualAnimatorHandPalmFrameWeight:F2}",
                $"retargetPoseVisualSpikeSmoothing={options.overrideRetargetPoseVisualSpikeSmoothingRuntimeSettings}/{options.enableRetargetPoseVisualSpikeSmoothingRuntimeOverride}/{options.retargetPoseVisualSpikeCurrentWeight:F2}/{options.retargetPoseVisualSpikeForearmStretchClampMaxOffset:F2}",
                $"yybArmSwingLimit={options.enableYybArmSwingLimitRuntimeOverride}/{options.yybArmSwingLimitWeight:F2}",
                $"yybArmDirection={options.enableYybArmDirectionRetargetRuntimeOverride}/{options.yybArmDirectionUpperArmWeight:F2}/{options.yybArmDirectionForearmWeight:F2}",
                $"yybArmSleeveAnchor={options.overrideYybArmSleeveAnchorRuntimeSettings}/{options.enableYybArmSleeveAnchorRuntimeOverride}/{options.yybArmSleeveAnchorInfluence:F2}/{options.yybArmShoulderCapAnchorInfluence:F2}/{options.yybArmSleeveAnchorMaxDegrees:F1}",
                $"yybArmVisualTwist={options.overrideYybArmVisualTwistRuntimeSettings}/{options.enableYybArmVisualTwistRuntimeOverride}/{options.yybArmVisualUpperArmInfluence:F2}/{options.yybArmVisualForearmInfluence:F2}/{options.yybArmVisualUpperArmMaxDegrees:F1}/{options.yybArmVisualForearmMaxDegrees:F1}",
                $"manualHipsLocalPosition={options.enableManualAnimatorHipsLocalPositionRuntimeOverride}/{options.manualAnimatorHipsLocalPositionReferenceWeight:F2}/{options.manualAnimatorHipsLocalPositionReferenceMaxOffset:F3}",
                $"retargetBodyPositionXzRootMotion={options.enableRetargetBodyPositionXzRootMotionRuntimeOverride}",
                $"targetBoneLockDisabled={options.disableTargetHumanoidBonePositionLockRuntimeOverride}",
                $"vmdPlaybackProbe={options.enableVmdPlaybackProbeRuntimeOverride}",
                $"vmdPlaybackProbeApplyIkTargets={options.applyVmdPlaybackProbeIkTargetsRuntimeOverride}",
                $"referenceMmdTiming={options.enableReferenceMmdTimingRuntimeOverride}",
                $"segment={segment}",
                $"diagnosticCapture={FormatRuntimeOverride(options.diagnosticCaptureWidthOverride)}x{FormatRuntimeOverride(options.diagnosticCaptureHeightOverride)}",
                $"diagnosticFraming={FormatDiagnosticFramingOverride(options.diagnosticScreenshotPaddingOverride)}/{FormatDiagnosticFramingOverride(options.diagnosticScreenshotVerticalViewportCenterOverride)}"
            };
        }

        private static string FormatRuntimeOverride(float value)
        {
            return VmdIkDeltaGuardRuntimeOverrideApplier.HasLimit(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : "none";
        }

        private static string FormatRuntimeOverride(int value)
        {
            return value > 0
                ? value.ToString(CultureInfo.InvariantCulture)
                : "none";
        }

        private static string FormatDiagnosticFramingOverride(float value)
        {
            return VisualComparisonScreenshotOverridePolicy.HasFiniteFramingOverride(value)
                ? value.ToString("0.###", CultureInfo.InvariantCulture)
                : "none";
        }
    }
}
