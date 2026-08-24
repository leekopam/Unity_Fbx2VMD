#if UNITY_EDITOR
using System;
using System.IO;
using Fbx2Vmd.Recording;
using Fbx2Vmd.Settings;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Editor smoke 진단의 경로, 캡처, 구간과 기준 timing 계획을 계산함.
    /// </summary>
    internal static class FBXEditorDiagnosticPlanner
    {
        internal static bool TryBuildCaptureResolutionOverride(
            int requestedWidth,
            int requestedHeight,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;
            if (requestedWidth <= 0 || requestedHeight <= 0)
            {
                return false;
            }

            RecordingCaptureResolutionPlan plan = RecordingCaptureResolution.CreateCustomPlan(
                requestedWidth,
                requestedHeight);
            width = plan.Width;
            height = plan.Height;
            return true;
        }

        internal static string ResolveFbxPath(
            string fbxFileName,
            string controlledImportDirectory,
            string dataPath,
            Func<string, bool> fileExists)
        {
            string normalizedFileName = Path.GetFileName(fbxFileName.Trim().Replace("\\", "/"));
            if (!string.Equals(
                    Path.GetExtension(normalizedFileName),
                    ".fbx",
                    StringComparison.OrdinalIgnoreCase))
            {
                normalizedFileName += ".fbx";
            }

            string controlledPath = Path.Combine(controlledImportDirectory, normalizedFileName);
            if (fileExists(controlledPath))
            {
                return controlledPath;
            }

            string projectFallbackPath = Path.Combine(
                dataPath,
                "_Project",
                "FBX",
                normalizedFileName);
            if (fileExists(projectFallbackPath))
            {
                return projectFallbackPath;
            }

            return controlledPath;
        }

        internal static string BuildOutputBaseName(
            string outputBaseName,
            float durationSeconds,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            string cleanBaseName = FBXImportController.SanitizeFileName(
                string.IsNullOrWhiteSpace(outputBaseName)
                    ? VMDOutputNamePolicy.DefaultOutputBaseName
                    : outputBaseName);
            int roundedSeconds = Mathf.Max(1, Mathf.CeilToInt(durationSeconds));
            string prefix;
            switch (segment)
            {
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle:
                    prefix = "smoke_middle";
                    break;
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail:
                    prefix = "smoke_tail";
                    break;
                default:
                    prefix = "smoke";
                    break;
            }

            return $"{prefix}_{cleanBaseName}_{roundedSeconds}s";
        }

        internal static float CalculateStartTime(
            AnimationClip clip,
            float requestedDuration,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            if (clip == null)
            {
                return 0f;
            }

            float clipLength = Mathf.Max(0f, clip.length);
            float safeDuration = Mathf.Max(0.1f, requestedDuration);
            switch (segment)
            {
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle:
                    return Mathf.Max(0f, (clipLength - safeDuration) * 0.5f);
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail:
                    return Mathf.Max(0f, clipLength - safeDuration);
                default:
                    return 0f;
            }
        }

        internal static string GetSegmentLabel(
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            switch (segment)
            {
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle:
                    return "middle";
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail:
                    return "tail";
                default:
                    return "head";
            }
        }

        internal static float[] CloneSampleTimes(float[] sampleTimesOverride)
        {
            return sampleTimesOverride != null && sampleTimesOverride.Length > 0
                ? (float[])sampleTimesOverride.Clone()
                : null;
        }

        internal static float NormalizeStartTimeOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return float.NaN;
            }

            return value;
        }

        internal static float NormalizePlaybackSpeedOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return float.NaN;
            }

            return Mathf.Max(0.0001f, value);
        }

        internal static float NormalizeScreenshotPaddingOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return float.NaN;
            }

            return Mathf.Clamp(value, 0.25f, 2f);
        }

        internal static float NormalizeScreenshotVerticalViewportCenterOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return float.NaN;
            }

            return Mathf.Clamp01(value);
        }

        internal static bool TryBuildKnownReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            return TryBuildKnownReferenceRecordingPlan(
                outputBaseName,
                clipLengthSeconds,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                recordingFrameRate,
                useKnownReferenceTiming: true,
                out recordingLengthSeconds,
                out targetFrameCount,
                out playbackSpeed);
        }

        internal static bool TryBuildKnownReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            bool useKnownReferenceTiming,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            recordingLengthSeconds = requestedDurationSeconds;
            targetFrameCount = requestedTargetFrameCount;
            playbackSpeed = 1f;

            if (!useKnownReferenceTiming)
            {
                return false;
            }

            if (requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                requestedTargetFrameCount <= 0 ||
                recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate))
            {
                return false;
            }

            if (!VMDRecordingController.TryBuildKnownMmdReferenceRecordingPlan(
                    outputBaseName,
                    clipLengthSeconds,
                    recordingFrameRate,
                    out float referenceRecordingLengthSeconds,
                    out int referenceTargetFrameCount,
                    out float referencePlaybackSpeed))
            {
                return false;
            }

            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            bool coversFullReferenceDuration =
                requestedDurationSeconds + frameToleranceSeconds >= referenceRecordingLengthSeconds;
            bool coversFullReferenceFrames = requestedTargetFrameCount >= referenceTargetFrameCount;

            if (!coversFullReferenceDuration || !coversFullReferenceFrames)
            {
                return false;
            }

            recordingLengthSeconds = referenceRecordingLengthSeconds;
            targetFrameCount = referenceTargetFrameCount;
            playbackSpeed = referencePlaybackSpeed;
            return true;
        }

        internal static bool ShouldUseKnownReferenceTiming(
            string outputBaseName,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            bool sceneUseKnownReferenceTiming)
        {
            if (sceneUseKnownReferenceTiming)
            {
                return true;
            }

            if (requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                requestedTargetFrameCount <= 0 ||
                recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate))
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(
                outputBaseName ?? string.Empty);
            if (!string.Equals(
                    cleanBaseName,
                    VMDOutputNamePolicy.SatisfactionReferenceBaseName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int referenceFrameCount =
                VMDOutputNamePolicy.SatisfactionReferenceMaxMmdFrame + 1;
            float referenceDurationSeconds = referenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            bool coversFullReferenceDuration =
                requestedDurationSeconds + frameToleranceSeconds >= referenceDurationSeconds;
            bool coversFullReferenceFrames = requestedTargetFrameCount >= referenceFrameCount;

            return coversFullReferenceDuration && coversFullReferenceFrames;
        }
    }
}
#endif
