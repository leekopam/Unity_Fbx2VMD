using System;
using Fbx2Vmd.Settings;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 녹화 시작 시점의 Inspector 설정을 고정하는 불변 스냅샷임.
    /// </summary>
    internal sealed class RecordingPlan
    {
        private readonly float[] _diagnosticSampleTimes;

        public RecordingPlan(
            float clipLengthSeconds,
            float playbackSpeed,
            float startDelaySeconds,
            RecordingCaptureResolutionPlan captureResolution,
            bool shouldStartVmdRecording,
            bool enableDiagnostics,
            bool enableFingerCloseups,
            bool useDeterministicCaptureFramerate,
            int retargetPrewarmFrameCount,
            bool useKnownReferenceTiming,
            float[] diagnosticSampleTimes,
            float diagnosticScreenshotPadding,
            float diagnosticScreenshotVerticalViewportCenter)
            : this(
                clipLengthSeconds,
                playbackSpeed,
                startDelaySeconds,
                captureResolution,
                shouldStartVmdRecording,
                enableDiagnostics,
                enableFingerCloseups,
                useDeterministicCaptureFramerate,
                retargetPrewarmFrameCount,
                useKnownReferenceTiming,
                diagnosticSampleTimes,
                diagnosticScreenshotPadding,
                diagnosticScreenshotVerticalViewportCenter,
                FBXVmdPipeline.ResolveRecordingLengthForPlaybackSpeed(clipLengthSeconds, playbackSpeed),
                targetFrameCount: 0)
        {
        }

        private RecordingPlan(
            float clipLengthSeconds,
            float playbackSpeed,
            float startDelaySeconds,
            RecordingCaptureResolutionPlan captureResolution,
            bool shouldStartVmdRecording,
            bool enableDiagnostics,
            bool enableFingerCloseups,
            bool useDeterministicCaptureFramerate,
            int retargetPrewarmFrameCount,
            bool useKnownReferenceTiming,
            float[] diagnosticSampleTimes,
            float diagnosticScreenshotPadding,
            float diagnosticScreenshotVerticalViewportCenter,
            float recordingLengthSeconds,
            int targetFrameCount)
        {
            ClipLengthSeconds = clipLengthSeconds;
            PlaybackSpeed = playbackSpeed;
            StartDelaySeconds = startDelaySeconds;
            CaptureResolution = captureResolution;
            ShouldStartVmdRecording = shouldStartVmdRecording;
            EnableDiagnostics = enableDiagnostics;
            EnableFingerCloseups = enableFingerCloseups;
            UseDeterministicCaptureFramerate = useDeterministicCaptureFramerate;
            RetargetPrewarmFrameCount = retargetPrewarmFrameCount;
            UseKnownReferenceTiming = useKnownReferenceTiming;
            _diagnosticSampleTimes = diagnosticSampleTimes == null ? null : (float[])diagnosticSampleTimes.Clone();
            DiagnosticScreenshotPadding = diagnosticScreenshotPadding;
            DiagnosticScreenshotVerticalViewportCenter = diagnosticScreenshotVerticalViewportCenter;
            RecordingLengthSeconds = recordingLengthSeconds;
            TargetFrameCount = targetFrameCount;
        }

        public float ClipLengthSeconds { get; }
        public float PlaybackSpeed { get; }
        public float StartDelaySeconds { get; }
        public RecordingCaptureResolutionPlan CaptureResolution { get; }
        public bool ShouldStartVmdRecording { get; }
        public bool EnableDiagnostics { get; }
        public bool EnableFingerCloseups { get; }
        public bool UseDeterministicCaptureFramerate { get; }
        public int RetargetPrewarmFrameCount { get; }
        public bool UseKnownReferenceTiming { get; }
        public float DiagnosticScreenshotPadding { get; }
        public float DiagnosticScreenshotVerticalViewportCenter { get; }
        public float RecordingLengthSeconds { get; }
        public int TargetFrameCount { get; }

        public float[] CreateDiagnosticSampleTimesCopy()
        {
            return _diagnosticSampleTimes == null ? null : (float[])_diagnosticSampleTimes.Clone();
        }

        public RecordingPlan WithTiming(float recordingLengthSeconds, int targetFrameCount, float playbackSpeed)
        {
            return new RecordingPlan(
                ClipLengthSeconds,
                playbackSpeed,
                StartDelaySeconds,
                CaptureResolution,
                ShouldStartVmdRecording,
                EnableDiagnostics,
                EnableFingerCloseups,
                UseDeterministicCaptureFramerate,
                RetargetPrewarmFrameCount,
                UseKnownReferenceTiming,
                _diagnosticSampleTimes,
                DiagnosticScreenshotPadding,
                DiagnosticScreenshotVerticalViewportCenter,
                recordingLengthSeconds,
                targetFrameCount);
        }
    }
}
