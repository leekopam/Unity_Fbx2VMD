#if UNITY_EDITOR
using System;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Editor smoke 진단의 일시 설정, 녹화 override와 완료 알림 상태를 보관함.
    /// </summary>
    internal sealed class FBXEditorDiagnosticSession
    {
        internal readonly struct SettingsSnapshot
        {
            internal SettingsSnapshot(
                bool enableRecordingDiagnostics,
                bool enableDiagnosticFingerCloseups,
                bool useDeterministicCaptureFramerateForDiagnostics,
                float startDelay)
            {
                EnableRecordingDiagnostics = enableRecordingDiagnostics;
                EnableDiagnosticFingerCloseups = enableDiagnosticFingerCloseups;
                UseDeterministicCaptureFramerateForDiagnostics =
                    useDeterministicCaptureFramerateForDiagnostics;
                StartDelay = startDelay;
            }

            internal bool EnableRecordingDiagnostics { get; }
            internal bool EnableDiagnosticFingerCloseups { get; }
            internal bool UseDeterministicCaptureFramerateForDiagnostics { get; }
            internal float StartDelay { get; }
        }

        internal sealed class Plan
        {
            internal float DurationSeconds { get; set; }
            internal int TargetFrameCount { get; set; }
            internal float[] SampleTimesOverride { get; set; }
            internal FBXVmdPipeline.EditorDiagnosticSmokeSegment Segment { get; set; }
            internal string CurrentFbxFileName { get; set; }
            internal bool HasCaptureResolutionOverride { get; set; }
            internal int CaptureWidth { get; set; }
            internal int CaptureHeight { get; set; }
            internal float DiagnosticScreenshotPaddingOverride { get; set; } = float.NaN;
            internal float DiagnosticScreenshotVerticalViewportCenterOverride { get; set; } = float.NaN;
            internal bool UseKnownReferenceTiming { get; set; }
            internal float RecordingStartTimeOverrideSeconds { get; set; } = float.NaN;
            internal float RecordingPlaybackSpeedOverride { get; set; } = float.NaN;
        }

        private SettingsSnapshot _settingsSnapshot;

        internal event Action<string, VmdSaveResult> Finished;

        internal bool IsRecordingOverrideActive { get; private set; }
        internal int TargetFrameCount { get; private set; }
        internal float DurationSeconds { get; private set; }
        internal float[] SampleTimesOverride { get; private set; }
        internal FBXVmdPipeline.EditorDiagnosticSmokeSegment Segment { get; private set; }
        internal string CurrentFbxFileName { get; private set; }
        internal bool HasCaptureResolutionOverride { get; private set; }
        internal int CaptureWidth { get; private set; }
        internal int CaptureHeight { get; private set; }
        internal float DiagnosticScreenshotPaddingOverride { get; private set; } = float.NaN;
        internal float DiagnosticScreenshotVerticalViewportCenterOverride { get; private set; } = float.NaN;
        internal bool UseKnownReferenceTiming { get; private set; }
        internal float RecordingStartTimeOverrideSeconds { get; private set; } = float.NaN;
        internal float RecordingPlaybackSpeedOverride { get; private set; } = float.NaN;
        internal bool HasSettingsSnapshot { get; private set; }

        internal void CaptureSettings(SettingsSnapshot snapshot)
        {
            _settingsSnapshot = snapshot;
            HasSettingsSnapshot = true;
        }

        internal void Begin(Plan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            IsRecordingOverrideActive = true;
            TargetFrameCount = plan.TargetFrameCount;
            DurationSeconds = plan.DurationSeconds;
            SampleTimesOverride = plan.SampleTimesOverride;
            Segment = plan.Segment;
            CurrentFbxFileName = plan.CurrentFbxFileName;
            HasCaptureResolutionOverride = plan.HasCaptureResolutionOverride;
            CaptureWidth = plan.CaptureWidth;
            CaptureHeight = plan.CaptureHeight;
            DiagnosticScreenshotPaddingOverride = plan.DiagnosticScreenshotPaddingOverride;
            DiagnosticScreenshotVerticalViewportCenterOverride =
                plan.DiagnosticScreenshotVerticalViewportCenterOverride;
            UseKnownReferenceTiming = plan.UseKnownReferenceTiming;
            RecordingStartTimeOverrideSeconds = plan.RecordingStartTimeOverrideSeconds;
            RecordingPlaybackSpeedOverride = plan.RecordingPlaybackSpeedOverride;
        }

        internal bool Clear(out SettingsSnapshot settingsSnapshot)
        {
            bool hasSettingsSnapshot = HasSettingsSnapshot;
            settingsSnapshot = _settingsSnapshot;

            IsRecordingOverrideActive = false;
            TargetFrameCount = 0;
            DurationSeconds = 0f;
            SampleTimesOverride = null;
            Segment = FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head;
            CurrentFbxFileName = null;
            HasCaptureResolutionOverride = false;
            CaptureWidth = 0;
            CaptureHeight = 0;
            DiagnosticScreenshotPaddingOverride = float.NaN;
            DiagnosticScreenshotVerticalViewportCenterOverride = float.NaN;
            UseKnownReferenceTiming = false;
            RecordingStartTimeOverrideSeconds = float.NaN;
            RecordingPlaybackSpeedOverride = float.NaN;
            _settingsSnapshot = default(SettingsSnapshot);
            HasSettingsSnapshot = false;

            return hasSettingsSnapshot;
        }

        internal void NotifyFinished(VmdSaveResult result)
        {
            if (!string.IsNullOrEmpty(CurrentFbxFileName))
            {
                Finished?.Invoke(CurrentFbxFileName, result);
            }
        }
    }
}
#endif
