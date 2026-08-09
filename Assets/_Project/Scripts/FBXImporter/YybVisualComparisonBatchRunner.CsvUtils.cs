#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static partial class YybVisualComparisonBatchRunner
    {
        private static CaptureResult FromPersistedResult(PersistedCaptureResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new CaptureResult
            {
                jobMode = result.jobMode,
                jobDisplayName = result.jobDisplayName,
                sceneName = result.sceneName,
                comparisonLabel = result.comparisonLabel,
                targetName = result.targetName,
                success = result.success,
                error = result.error,
                vmdPath = result.vmdPath,
                frameCount = result.frameCount,
                fileSizeBytes = result.fileSizeBytes,
                comparisonSessionManifestPath = result.comparisonSessionManifestPath,
                comparisonMetricsCsvPath = result.comparisonMetricsCsvPath,
                comparisonFrameFolderPath = result.comparisonFrameFolderPath,
                comparisonFrameIndexPath = result.comparisonFrameIndexPath,
                comparisonSessionId = result.comparisonSessionId,
                hasFBXVmdPipelineEffectiveSettings = result.hasFBXVmdPipelineEffectiveSettings,
                ShouldUseManualAnimatorFootLocalRotationReference = result.ShouldUseManualAnimatorFootLocalRotationReference,
                manualAnimatorFootLocalRotationReferenceWeight = result.manualAnimatorFootLocalRotationReferenceWeight,
                ShouldUseManualAnimatorFullBodyPoseReference = result.ShouldUseManualAnimatorFullBodyPoseReference,
                manualAnimatorFullBodyPoseReferenceWeight = result.manualAnimatorFullBodyPoseReferenceWeight,
                ShouldExcludeManualAnimatorFullBodyLowerMuscles =
                    result.ShouldExcludeManualAnimatorFullBodyLowerMuscles,
                ShouldApplyManualAnimatorFullBodyLowerMusclesOnly =
                    result.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly,
                ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly =
                    result.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly,
                manualAnimatorFullBodyPoseRightArmMusclesOnly =
                    result.manualAnimatorFullBodyPoseRightArmMusclesOnly,
                manualAnimatorFullBodyPoseLeftArmMusclesOnly =
                    result.manualAnimatorFullBodyPoseLeftArmMusclesOnly,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly =
                    result.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly,
                manualAnimatorFullBodyPoseFrameGateStart =
                    result.manualAnimatorFullBodyPoseFrameGateStart,
                manualAnimatorFullBodyPoseFrameGateEnd =
                    result.manualAnimatorFullBodyPoseFrameGateEnd,
                ShouldUseSetHumanPoseRightLegTwistOutputReference =
                    result.ShouldUseSetHumanPoseRightLegTwistOutputReference,
                setHumanPoseRightLegTwistOutputReferenceWeight =
                    result.setHumanPoseRightLegTwistOutputReferenceWeight,
                setHumanPoseRightLegTwistOutputReferenceMaxDelta =
                    result.setHumanPoseRightLegTwistOutputReferenceMaxDelta,
                ShouldUseManualAnimatorBodyRotationReference = result.ShouldUseManualAnimatorBodyRotationReference,
                manualAnimatorBodyRotationReferenceWeight = result.manualAnimatorBodyRotationReferenceWeight,
                ShouldUseManualAnimatorLowerBodySegmentDirectionReference =
                    result.ShouldUseManualAnimatorLowerBodySegmentDirectionReference,
                manualAnimatorLowerBodySegmentDirectionReferenceWeight =
                    result.manualAnimatorLowerBodySegmentDirectionReferenceWeight,
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle,
                ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                    result.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference,
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle,
                ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference =
                    result.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference,
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd,
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                    result.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight,
                ShouldDisableManualAnimatorFootToToesSegmentDirectionReference =
                    result.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference,
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                    result.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle,
                ShouldUseManualAnimatorFootHipsAlignedResidualYawReference =
                    result.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference,
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight =
                    result.manualAnimatorFootHipsAlignedResidualYawReferenceWeight,
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle =
                    result.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle,
                usePostSetHumanPoseRightEndpointPositionReference =
                    result.usePostSetHumanPoseRightEndpointPositionReference,
                postSetHumanPoseRightEndpointPositionReferenceWeight =
                    result.postSetHumanPoseRightEndpointPositionReferenceWeight,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    result.postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    result.postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    result.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    result.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    result.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition =
                    result.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition,
                usePreSetHumanPoseRightEndpointPositionReference =
                    result.usePreSetHumanPoseRightEndpointPositionReference,
                preSetHumanPoseRightEndpointPositionReferenceWeight =
                    result.preSetHumanPoseRightEndpointPositionReferenceWeight,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    result.preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    result.preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    result.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    result.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart,
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    result.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd,
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition =
                    result.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition,
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                    result.preSetHumanPoseEndpointPositionUseGhostCurrentBasis,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX =
                    result.ShouldInvertPreSetHumanPoseEndpointPositionBodyX,
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ =
                    result.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ,
                usePostSetHumanPoseRightFootEvaluatorXzReference =
                    result.usePostSetHumanPoseRightFootEvaluatorXzReference,
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                    result.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                ShouldUseManualAnimatorBodyPositionXzReference =
                    result.ShouldUseManualAnimatorBodyPositionXzReference,
                manualAnimatorBodyPositionXzReferenceWeight =
                    result.manualAnimatorBodyPositionXzReferenceWeight,
                manualAnimatorBodyPositionXzReferenceMaxOffset =
                    result.manualAnimatorBodyPositionXzReferenceMaxOffset,
                manualAnimatorBodyPositionXzReferenceFrameGateStart =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateStart,
                manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateEnd,
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                    result.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames,
                manualAnimatorBodyPositionXzReferenceAxisXScale =
                    result.manualAnimatorBodyPositionXzReferenceAxisXScale,
                manualAnimatorBodyPositionXzReferenceAxisZScale =
                    result.manualAnimatorBodyPositionXzReferenceAxisZScale,
                enableYybArmSwingLimitCorrection = result.enableYybArmSwingLimitCorrection,
                yybArmSwingLimitWeight = result.yybArmSwingLimitWeight,
                yybArmSwingMaxDownDot = result.yybArmSwingMaxDownDot,
                yybArmSwingMinHandHorizontalRatio = result.yybArmSwingMinHandHorizontalRatio,
                yybArmSwingMaxHandBelowShoulderRatio = result.yybArmSwingMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachLimitWeight = result.yybArmSwingHorizontalReachLimitWeight,
                yybArmSwingMaxHandHorizontalReachRatio = result.yybArmSwingMaxHandHorizontalReachRatio,
                yybArmSwingHorizontalReachMaxHandBelowShoulderRatio =
                    result.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio,
                yybArmSwingHorizontalReachMinElbowAngleAfterApply =
                    result.yybArmSwingHorizontalReachMinElbowAngleAfterApply,
                yybArmSwingRaisedPoseHorizontalReachLimitWeight =
                    result.yybArmSwingRaisedPoseHorizontalReachLimitWeight,
                yybArmSwingRaisedPoseMinUpperArmDownDot = result.yybArmSwingRaisedPoseMinUpperArmDownDot,
                yybArmSwingRaisedPoseMaxHandBelowShoulderRatio =
                    result.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio,
                yybArmSwingRaisedPoseMaxHandHorizontalReachRatio =
                    result.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio,
                enableYybArmSleeveAnchorCorrection = result.enableYybArmSleeveAnchorCorrection,
                enableYybArmVisualTwistCorrection = result.enableYybArmVisualTwistCorrection,
                clampRetargetArmStretchMuscles = result.clampRetargetArmStretchMuscles,
                armStretchMuscleLimit = result.armStretchMuscleLimit
            };
        }

        internal static bool IsFiniteMetric(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static float ResolveFrameTopGapRatio(float bottomGapRatio, float bboxHeightRatio)
        {
            if (!IsFiniteMetric(bottomGapRatio) || !IsFiniteMetric(bboxHeightRatio))
            {
                return float.NaN;
            }

            return Mathf.Max(0f, 1f - bottomGapRatio - bboxHeightRatio);
        }

        internal static bool IsFrameEdgeTouched(float bottomGapRatio, float topGapRatio)
        {
            return (IsFiniteMetric(bottomGapRatio) &&
                    bottomGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance) ||
                   (IsFiniteMetric(topGapRatio) &&
                    topGapRatio <= ReferenceAlignedVisualEvidenceEndpointPixelTolerance);
        }

        internal static int IndexOfHeader(string[] headers, string headerName)
        {
            if (headers == null)
            {
                return -1;
            }

            for (int i = 0; i < headers.Length; i++)
            {
                if (string.Equals(headers[i], headerName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }



        private static SummarySampleOrderingDiagnostic[] BuildSampleOrderingDiagnostics()
        {
            return Results
                .Select(result => BuildSampleOrderingDiagnostic(
                    result.jobMode,
                    result.sceneName,
                    result.comparisonMetricsCsvPath))
                .ToArray();
        }

        private static SummarySampleOrderingDiagnostic BuildSampleOrderingDiagnostic(
            string jobMode,
            string sceneName,
            string metricsCsvPath)
        {
            SummarySampleOrderingDiagnostic diagnostic = new SummarySampleOrderingDiagnostic
            {
                job_mode = jobMode ?? string.Empty,
                scene_name = sceneName ?? string.Empty,
                metrics_csv = metricsCsvPath ?? string.Empty
            };

            string absolutePath = ToAbsoluteProjectPath(metricsCsvPath);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return diagnostic;
            }

            string[] lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return diagnostic;
            }

            string[] headers = SplitSimpleCsvLine(lines[0]);
            Dictionary<string, int> indices = BuildCsvIndexMap(headers);
            List<string[]> rows = new List<string[]>();
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    continue;
                }

                rows.Add(SplitSimpleCsvLine(lines[lineIndex]));
            }

            diagnostic.metric_row_count = rows.Count;
            if (rows.Count == 0)
            {
                return diagnostic;
            }

            string[] first = rows[0];
            string[] finish = rows.LastOrDefault(row =>
                string.Equals(GetCsvString(row, indices, "reason"), "finish", StringComparison.OrdinalIgnoreCase))
                ?? rows[rows.Count - 1];

            diagnostic.first_metric_reason = GetCsvString(first, indices, "reason");
            diagnostic.first_metric_recorder_frame = GetCsvInt(first, indices, "recorderFrame");
            diagnostic.first_metric_engine_frame_count = GetCsvInt(first, indices, "frameCount");
            diagnostic.first_metric_time_since_level_load = GetCsvFloat(first, indices, "timeSinceLevelLoad");
            diagnostic.first_metric_animation_clip_time = GetCsvFloat(first, indices, "animationClipTime");
            diagnostic.first_metric_grounding_vertical_step_last = GetCsvFloat(first, indices, "retargetGroundingVerticalStepLast");
            diagnostic.first_metric_grounding_initial_vertical_step = GetCsvFloat(first, indices, "retargetGroundingInitialVerticalStep");
            diagnostic.first_metric_grounding_step_clamp_count = GetCsvInt(first, indices, "retargetGroundingStepClampCount");
            diagnostic.first_metric_grounding_smoothed_count = GetCsvInt(first, indices, "retargetGroundingSmoothedCount");
            diagnostic.first_metric_grounding_max_step_per_frame = GetCsvFloat(first, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.first_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                first,
                indices,
                diagnostic.first_metric_grounding_vertical_step_last,
                diagnostic.first_metric_grounding_max_step_per_frame);
            diagnostic.first_metric_grounding_vertical_step_at_max_step =
                IsGroundingVerticalStepAtMax(diagnostic.first_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.finish_metric_reason = GetCsvString(finish, indices, "reason");
            diagnostic.finish_metric_recorder_frame = GetCsvInt(finish, indices, "recorderFrame");
            diagnostic.finish_metric_engine_frame_count = GetCsvInt(finish, indices, "frameCount");
            diagnostic.finish_metric_time_since_level_load = GetCsvFloat(finish, indices, "timeSinceLevelLoad");
            diagnostic.finish_metric_animation_clip_time = GetCsvFloat(finish, indices, "animationClipTime");
            diagnostic.finish_metric_grounding_vertical_step_last = GetCsvFloat(finish, indices, "retargetGroundingVerticalStepLast");
            diagnostic.finish_metric_grounding_step_clamp_count = GetCsvInt(finish, indices, "retargetGroundingStepClampCount");
            diagnostic.finish_metric_grounding_smoothed_count = GetCsvInt(finish, indices, "retargetGroundingSmoothedCount");
            diagnostic.finish_metric_grounding_max_step_per_frame = GetCsvFloat(finish, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.finish_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                finish,
                indices,
                diagnostic.finish_metric_grounding_vertical_step_last,
                diagnostic.finish_metric_grounding_max_step_per_frame);
            diagnostic.finish_metric_grounding_vertical_step_at_max_step =
                IsGroundingVerticalStepAtMax(diagnostic.finish_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.recording_metric_recorder_frame_span = CalculateMetricIntSpan(
                diagnostic.first_metric_recorder_frame,
                diagnostic.finish_metric_recorder_frame);
            diagnostic.recording_metric_engine_frame_span = CalculateMetricIntSpan(
                diagnostic.first_metric_engine_frame_count,
                diagnostic.finish_metric_engine_frame_count);
            diagnostic.recording_metric_time_since_level_load_span = CalculateMetricFloatSpan(
                diagnostic.first_metric_time_since_level_load,
                diagnostic.finish_metric_time_since_level_load);
            diagnostic.recording_grounding_step_clamp_delta = CalculateMetricIntSpan(
                diagnostic.first_metric_grounding_step_clamp_count,
                diagnostic.finish_metric_grounding_step_clamp_count);
            diagnostic.recording_grounding_smoothed_delta = CalculateMetricIntSpan(
                diagnostic.first_metric_grounding_smoothed_count,
                diagnostic.finish_metric_grounding_smoothed_count);
            diagnostic.recording_phase_span_role =
                "finish-first recording phase metrics; absolute first engine frame includes scene load/import/prewarm startup offset and can vary between Unity batch runs";
            diagnostic.grounding_step_limit_role =
                "prewarm residual is identified by the first recorder-frame grounding step reaching its configured max; recording clamp/smoothed deltas are finish-first counters inside the captured phase";
            return diagnostic;
        }

        private static float ResolveGroundingStepToMaxRatio(
            string[] row,
            Dictionary<string, int> indices,
            float step,
            float maxStep)
        {
            float reportedRatio = GetCsvFloat(row, indices, "retargetGroundingLastStepToMaxStepRatio");
            if (!float.IsNaN(reportedRatio) && !float.IsInfinity(reportedRatio))
            {
                return reportedRatio;
            }

            if (float.IsNaN(step) ||
                float.IsInfinity(step) ||
                float.IsNaN(maxStep) ||
                float.IsInfinity(maxStep) ||
                maxStep <= 0f)
            {
                return float.NaN;
            }

            return Mathf.Abs(step) / maxStep;
        }

        private static bool IsGroundingVerticalStepAtMax(float stepToMaxRatio)
        {
            return !float.IsNaN(stepToMaxRatio) &&
                !float.IsInfinity(stepToMaxRatio) &&
                stepToMaxRatio >= 0.95f;
        }

        private static int CalculateMetricIntSpan(int first, int finish)
        {
            if (first < 0 || finish < 0)
            {
                return -1;
            }

            return finish - first;
        }

        private static float CalculateMetricFloatSpan(float first, float finish)
        {
            if (float.IsNaN(first) ||
                float.IsNaN(finish) ||
                float.IsInfinity(first) ||
                float.IsInfinity(finish))
            {
                return float.NaN;
            }

            return finish - first;
        }

        internal static string[] SplitSimpleCsvLine(string line)
        {
            return (line ?? string.Empty).Split(',');
        }

        private static Dictionary<string, int> BuildCsvIndexMap(string[] headers)
        {
            Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                if (!indices.ContainsKey(headers[index]))
                {
                    indices.Add(headers[index], index);
                }
            }

            return indices;
        }

        private static string GetCsvString(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            if (row == null ||
                indices == null ||
                string.IsNullOrEmpty(column) ||
                !indices.TryGetValue(column, out int index) ||
                index < 0 ||
                index >= row.Length)
            {
                return string.Empty;
            }

            return row[index] ?? string.Empty;
        }

        private static int GetCsvInt(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return int.TryParse(
                GetCsvString(row, indices, column),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : 0;
        }

        private static float GetCsvFloat(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return float.TryParse(
                GetCsvString(row, indices, column),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : float.NaN;
        }

        private static string ToAbsoluteProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(_projectRoot, normalized);
        }

        private static string FormatQualityFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? "n/a"
                : value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string FormatEnabledWeight(bool enabled, float weight)
        {
            return $"{enabled}/{FormatQualityFloat(weight)}";
        }

        private static string FormatEnabledWeightCap(bool enabled, float weight, float cap)
        {
            return $"{enabled}/{FormatQualityFloat(weight)}/{FormatQualityFloat(cap)}";
        }

        private static string FormatEnabledWeightCapScale(bool enabled, float weight, float cap, float scale)
        {
            return $"{enabled}/{FormatQualityFloat(weight)}/{FormatQualityFloat(cap)}/{FormatQualityFloat(scale)}";
        }

        private static string FormatEnabledWeightCapScaleGate(
            bool enabled,
            float weight,
            float cap,
            float scale,
            float frameGateStart,
            float frameGateEnd)
        {
            return $"{FormatEnabledWeightCapScale(enabled, weight, cap, scale)}/{FormatQualityFloat(frameGateStart)}-{FormatQualityFloat(frameGateEnd)}";
        }

        private static string FormatEnabledWeightCapScaleBlendGate(
            bool enabled,
            float weight,
            float cap,
            float scale,
            float blend,
            float frameGateStart,
            float frameGateEnd)
        {
            return $"{FormatEnabledWeightCapScale(enabled, weight, cap, scale)}/blend:{FormatQualityFloat(blend)}/{FormatQualityFloat(frameGateStart)}-{FormatQualityFloat(frameGateEnd)}";
        }

        private static string FormatEvaluatorXzReferenceSettings(CaptureResult result)
        {
            return result == null
                ? "False/n/a"
                : $"{result.usePostSetHumanPoseRightFootEvaluatorXzReference}/{FormatQualityFloat(result.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude)}";
        }

        private static string FormatArmSwingSettings(CaptureResult result)
        {
            if (result == null)
            {
                return "n/a";
            }

            return
                $"{result.enableYybArmSwingLimitCorrection}/" +
                $"{FormatQualityFloat(result.yybArmSwingLimitWeight)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMaxDownDot)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMinHandHorizontalRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMaxHandBelowShoulderRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingHorizontalReachLimitWeight)}/" +
                $"{FormatQualityFloat(result.yybArmSwingMaxHandHorizontalReachRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingHorizontalReachMaxHandBelowShoulderRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingHorizontalReachMinElbowAngleAfterApply)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseHorizontalReachLimitWeight)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseMinUpperArmDownDot)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseMaxHandBelowShoulderRatio)}/" +
                $"{FormatQualityFloat(result.yybArmSwingRaisedPoseMaxHandHorizontalReachRatio)}";
        }

        private static void CopyLatestSummary(string sourcePath, string relativeTargetPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            string targetPath = Path.Combine(_projectRoot, relativeTargetPath);
            string targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        private static AnimationClip LoadFirstAnimationClip(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.humanMotion)
                {
                    return clip;
                }
            }

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static string ResolveReferenceClipAssetPath(string fbxFileName, Func<string, bool> hasReferenceClip)
        {
            string normalizedFileName = NormalizeFbxFileName(fbxFileName);
            string projectCandidate = Path.Combine(ProjectFbxDirectory, normalizedFileName).Replace('\\', '/');
            if (hasReferenceClip(projectCandidate))
            {
                return projectCandidate;
            }

            string importCandidate = Path.Combine(ImportFbxDirectory, normalizedFileName).Replace('\\', '/');
            if (hasReferenceClip(importCandidate))
            {
                return importCandidate;
            }

            return importCandidate;
        }

        private static string NormalizeFbxFileName(string fbxFileName)
        {
            string name = string.IsNullOrWhiteSpace(fbxFileName) ? DefaultFbxFileName : fbxFileName.Trim();
            return string.Equals(Path.GetExtension(name), ".fbx", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(name)
                : Path.GetFileNameWithoutExtension(name) + ".fbx";
        }

        private static string GetCommandLineValue(string name, string fallbackValue)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return fallbackValue;
        }

        private static float GetCommandLineFloat(string name, float fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallbackValue;
        }

        private static int GetCommandLineInt(string name, int fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallbackValue;
        }

        private static bool GetCommandLineBool(string name, bool fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallbackValue;
            }

            if (bool.TryParse(value, out bool parsedBool))
            {
                return parsedBool;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                ? true
                : string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : fallbackValue;
        }

    }
}
#endif
