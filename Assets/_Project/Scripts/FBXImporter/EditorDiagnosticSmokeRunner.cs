using System;
using System.IO;
using UnityEngine;
using Fbx2Vmd.Recording;
using Fbx2Vmd.Settings;

namespace Fbx2Vmd.FBXImporter
{
    internal static class EditorDiagnosticSmokeRunner
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
            if (!string.Equals(Path.GetExtension(normalizedFileName), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                normalizedFileName += ".fbx";
            }

            string controlledPath = Path.Combine(controlledImportDirectory, normalizedFileName);
            if (fileExists(controlledPath))
            {
                return controlledPath;
            }

            string projectFallbackPath = Path.Combine(dataPath, "_Project", "FBX", normalizedFileName);
            return fileExists(projectFallbackPath) ? projectFallbackPath : controlledPath;
        }

        internal static string BuildOutputBaseName(
            string outputBaseName,
            float durationSeconds,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            Func<string, string> sanitizeFileName)
        {
            string cleanBaseName = sanitizeFileName(
                string.IsNullOrWhiteSpace(outputBaseName) ? VMDOutputNamePolicy.DefaultOutputBaseName : outputBaseName);
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

        internal static string GetSegmentLabel(FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
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
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0f
                ? float.NaN
                : value;
        }

        internal static float NormalizePlaybackSpeedOverride(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? float.NaN
                : Mathf.Max(0.0001f, value);
        }

        internal static float NormalizeDiagnosticScreenshotPaddingOverride(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value <= 0f
                ? float.NaN
                : Mathf.Clamp(value, 0.25f, 2f);
        }

        internal static float NormalizeDiagnosticScreenshotVerticalViewportCenterOverride(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? float.NaN
                : Mathf.Clamp01(value);
        }

        internal static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            bool useKnownReferenceTiming,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            recordingLengthSeconds = clipLengthSeconds;
            targetFrameCount = 0;
            playbackSpeed = 1f;

            if (!useKnownReferenceTiming ||
                recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate) ||
                clipLengthSeconds <= 0f ||
                float.IsNaN(clipLengthSeconds) ||
                float.IsInfinity(clipLengthSeconds))
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(outputBaseName ?? string.Empty);
            if (!string.Equals(cleanBaseName, VMDOutputNamePolicy.SatisfactionReferenceBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int referenceFrameCount = VMDOutputNamePolicy.SatisfactionReferenceMaxMmdFrame + 1;
            float referenceDurationSeconds = referenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            if (clipLengthSeconds + frameToleranceSeconds < referenceDurationSeconds)
            {
                return false;
            }

            recordingLengthSeconds = referenceDurationSeconds;
            targetFrameCount = referenceFrameCount;
            playbackSpeed = Mathf.Max(0.0001f, clipLengthSeconds / referenceDurationSeconds);
            return true;
        }

        internal static bool TryBuildKnownMmdReferenceRecordingPlan(
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

            if (!useKnownReferenceTiming ||
                requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                requestedTargetFrameCount <= 0 ||
                recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate) ||
                !TryBuildKnownMmdReferenceRecordingPlan(
                    outputBaseName,
                    clipLengthSeconds,
                    recordingFrameRate,
                    useKnownReferenceTiming: true,
                    out float referenceRecordingLengthSeconds,
                    out int referenceTargetFrameCount,
                    out float referencePlaybackSpeed))
            {
                return false;
            }

            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            if (requestedDurationSeconds + frameToleranceSeconds < referenceRecordingLengthSeconds ||
                requestedTargetFrameCount < referenceTargetFrameCount)
            {
                return false;
            }

            recordingLengthSeconds = referenceRecordingLengthSeconds;
            targetFrameCount = referenceTargetFrameCount;
            playbackSpeed = referencePlaybackSpeed;
            return true;
        }

        internal static bool ShouldUseKnownMmdReferenceTiming(
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

            string cleanBaseName = Path.GetFileNameWithoutExtension(outputBaseName ?? string.Empty);
            if (!string.Equals(cleanBaseName, VMDOutputNamePolicy.SatisfactionReferenceBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int referenceFrameCount = VMDOutputNamePolicy.SatisfactionReferenceMaxMmdFrame + 1;
            float referenceDurationSeconds = referenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            return requestedDurationSeconds + frameToleranceSeconds >= referenceDurationSeconds &&
                   requestedTargetFrameCount >= referenceFrameCount;
        }
    }
}
