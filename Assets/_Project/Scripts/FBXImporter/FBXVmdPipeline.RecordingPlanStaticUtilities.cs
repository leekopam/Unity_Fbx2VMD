using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Fbx2Vmd.Recording;

namespace Fbx2Vmd.FBXImporter
{
    public partial class FBXVmdPipeline
    {
        internal static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            return TryBuildKnownMmdReferenceRecordingPlan(
                outputBaseName,
                clipLengthSeconds,
                recordingFrameRate,
                useKnownReferenceTiming: true,
                out recordingLengthSeconds,
                out targetFrameCount,
                out playbackSpeed);
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

            if (!useKnownReferenceTiming)
            {
                return false;
            }

            if (recordingFrameRate <= 0f ||
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

        internal static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            return TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
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

        internal static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
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

            if (!TryBuildKnownMmdReferenceRecordingPlan(
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

        private static bool ShouldUseKnownMmdReferenceTimingForEditorSmoke(
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
            bool coversFullReferenceDuration =
                requestedDurationSeconds + frameToleranceSeconds >= referenceDurationSeconds;
            bool coversFullReferenceFrames = requestedTargetFrameCount >= referenceFrameCount;

            return coversFullReferenceDuration && coversFullReferenceFrames;
        }

        internal static float ResolveVmdRecordingPlaybackSpeed(float configuredPlaybackSpeed)
        {
            if (configuredPlaybackSpeed <= 0f ||
                float.IsNaN(configuredPlaybackSpeed) ||
                float.IsInfinity(configuredPlaybackSpeed))
            {
                return 1f;
            }

            return Mathf.Max(0.0001f, configuredPlaybackSpeed);
        }

        internal static float ResolveRecordingLengthForPlaybackSpeed(float clipLengthSeconds, float playbackSpeed)
        {
            if (clipLengthSeconds <= 0f ||
                float.IsNaN(clipLengthSeconds) ||
                float.IsInfinity(clipLengthSeconds))
            {
                return 0f;
            }

            return clipLengthSeconds / ResolveVmdRecordingPlaybackSpeed(playbackSpeed);
        }

        /// <summary>
        /// 녹화 전 지연을 계획값으로만 수행해 실행 중 Inspector 변경을 무시함.
        /// </summary>
        private static IEnumerator WaitForRecordingStartDelay(RecordingPlan recordingPlan)
        {
            if (recordingPlan.StartDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(recordingPlan.StartDelaySeconds);
            }
        }

        /// <summary>
        /// 계획에 고정된 진단 옵션과 캡처 해상도를 녹화기에 적용함.
        /// </summary>
        private static void ConfigureRecordingDiagnostics(HumanoidSampleCode recorderController, RecordingPlan recordingPlan)
        {
            recorderController.SetRecordingDiagnostics(
                recordingPlan.EnableDiagnostics,
                recordingPlan.EnableDiagnostics && recordingPlan.EnableFingerCloseups,
                recordingPlan.EnableDiagnostics && recordingPlan.UseDeterministicCaptureFramerate,
                recordingPlan.CreateDiagnosticSampleTimesCopy(),
                recordingPlan.CaptureResolution.Width,
                recordingPlan.CaptureResolution.Height,
                recordingPlan.DiagnosticScreenshotPadding,
                recordingPlan.DiagnosticScreenshotVerticalViewportCenter);
        }

        /// <summary>
        /// 기준 타이밍에 필요한 고정 프레임 녹화 옵션을 켬.
        /// </summary>
        private static void EnableReferenceTimingFrameCapture(HumanoidSampleCode recorderController)
        {
            if (recorderController.vmdRecorder == null)
            {
                return;
            }

            recorderController.vmdRecorder.UseCaptureFramerateDuringRecording = true;
            recorderController.vmdRecorder.DropLateFrameBacklogWhenNotUsingCaptureFramerate = false;
        }
    }
}
