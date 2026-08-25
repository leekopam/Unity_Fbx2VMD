using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal static class VisualComparisonFrameQualityFailurePolicy
    {
        internal static string[] BuildFailureMessages(
            MotionComparisonFrameQualitySummary[] summaries,
            bool acceptedArtifactPreservesRawDiagnostic)
        {
            if (summaries == null || summaries.Length == 0)
            {
                return Array.Empty<string>();
            }

            List<string> failures = new List<string>();
            foreach (MotionComparisonFrameQualitySummary summary in summaries)
            {
                if (summary == null ||
                    !string.Equals(summary.status, "fail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (acceptedArtifactPreservesRawDiagnostic && IsRawCandidateRole(summary))
                {
                    continue;
                }

                string candidate = string.IsNullOrWhiteSpace(summary.candidate_label)
                    ? "unknown candidate"
                    : summary.candidate_label;
                string role = string.IsNullOrWhiteSpace(summary.frame_quality_evaluation_role)
                    ? "unknown_role"
                    : summary.frame_quality_evaluation_role;
                string reason = string.IsNullOrWhiteSpace(summary.status_reason)
                    ? "status=fail"
                    : summary.status_reason;

                failures.Add(
                    "frame-quality gate failed: " +
                    $"candidate={candidate}; " +
                    $"role={role}; " +
                    $"reason={reason}; " +
                    $"metrics={summary.candidate_metrics_csv ?? string.Empty}; " +
                    $"vmd={summary.candidate_vmd_path ?? string.Empty}");
            }

            return failures.ToArray();
        }

        internal static bool IsRawCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                (string.Equals(
                    summary.frame_quality_evaluation_role,
                    "raw_candidate_metrics",
                    StringComparison.Ordinal) ||
                string.Equals(
                    summary.frame_quality_evaluation_role,
                    "evaluation_candidate_metrics",
                    StringComparison.Ordinal));
        }
    }
}
