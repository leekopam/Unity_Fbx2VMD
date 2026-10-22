using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonMainSceneDiagnosticPolicy
    {
        private const string AutomaticMode = "Automatic";
        private const string LegacyMainAutoMode = "MainAuto";
        private const string MainRecordingMode = "MainRecording";
        private const string MainRecordingVmdPlaybackProbeMode = "MainRecordingVmdPlaybackProbe";

        internal static bool IsAutomaticMode(string captureMode)
        {
            return string.Equals(captureMode, AutomaticMode, StringComparison.Ordinal) ||
                string.Equals(captureMode, LegacyMainAutoMode, StringComparison.Ordinal);
        }

        internal static bool IsCandidateMode(string captureMode)
        {
            return string.Equals(captureMode, MainRecordingMode, StringComparison.Ordinal) ||
                string.Equals(captureMode, MainRecordingVmdPlaybackProbeMode, StringComparison.Ordinal) ||
                IsAutomaticMode(captureMode);
        }

        internal static bool ShouldBuildFrameQualityDiagnostic(
            bool captureSucceeded,
            string metricsCsvPath,
            string vmdPath)
        {
            return captureSucceeded ||
                (!string.IsNullOrWhiteSpace(metricsCsvPath) &&
                    !string.IsNullOrWhiteSpace(vmdPath));
        }

        internal static string ResolveIntegratedVerticalSolveRole(string captureMode)
        {
            if (IsAutomaticMode(captureMode))
            {
                return "automatic_integrated_vertical_solve_metrics";
            }

            if (string.Equals(captureMode, MainRecordingVmdPlaybackProbeMode, StringComparison.Ordinal))
            {
                return "vmd_replay_integrated_vertical_solve_metrics";
            }

            return string.Empty;
        }

        internal static string ResolveIntegratedVerticalSolveBasis(string captureMode)
        {
            if (string.Equals(captureMode, MainRecordingVmdPlaybackProbeMode, StringComparison.Ordinal))
            {
                return "primary VMD replay diagnostic output after bounded vertical solve promotion; raw replay metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
            }

            return "primary automatic-path result paths after bounded vertical solve promotion; raw metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
        }
    }
}
