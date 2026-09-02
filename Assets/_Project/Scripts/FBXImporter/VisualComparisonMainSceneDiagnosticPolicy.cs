using System;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonMainSceneDiagnosticPolicy
    {
        private const string MainAutoMode = "MainAuto";
        private const string MainRecordingMode = "MainRecording";
        private const string MainRecordingVmdPlaybackProbeMode = "MainRecordingVmdPlaybackProbe";

        internal static bool IsCandidateMode(string captureMode)
        {
            return string.Equals(captureMode, MainRecordingMode, StringComparison.Ordinal) ||
                string.Equals(captureMode, MainRecordingVmdPlaybackProbeMode, StringComparison.Ordinal) ||
                string.Equals(captureMode, MainAutoMode, StringComparison.Ordinal);
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
            if (string.Equals(captureMode, MainAutoMode, StringComparison.Ordinal))
            {
                return "main_auto_integrated_vertical_solve_metrics";
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

            return "primary Main_Auto result paths after bounded vertical solve promotion; raw metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
        }
    }
}
