using System;
using System.Linq;

namespace Fbx2Vmd.FBXImporter
{
    internal static class YybVisualComparisonReferenceAlignmentPolicy
    {
        internal static void Apply(
            MotionComparisonFrameQualitySummary[] summaries,
            bool hasReferenceAlignedEvidence)
        {
            if (!hasReferenceAlignedEvidence || summaries == null || summaries.Length == 0)
            {
                return;
            }

            foreach (MotionComparisonFrameQualitySummary summary in summaries)
            {
                if (summary == null ||
                    !string.Equals(summary.status, "fail", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsSubManualPoseOnlyResidual(summary))
                {
                    MarkAsReferenceAligned(
                        summary,
                        "Sub_Manual Unity pose delta kept as diagnostic because time-matched ref MP4 image-space evidence is aligned");
                    continue;
                }

                if (IsEvaluationCandidateRole(summary) &&
                    HasReferenceAlignedCorrectedCounterpart(summary, summaries))
                {
                    MarkAsReferenceAligned(
                        summary,
                        "raw replay vertical residual kept as diagnostic because corrected candidate and ref MP4 image-space evidence are aligned");
                }
            }
        }

        private static bool IsSubManualPoseOnlyResidual(MotionComparisonFrameQualitySummary summary)
        {
            string reason = summary.status_reason ?? string.Empty;
            return reason.IndexOf("same-frame limb pose delta threshold exceeded", StringComparison.OrdinalIgnoreCase) >= 0 &&
                reason.IndexOf("YYB deformation risk", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("below-floor", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("root position delta threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("one-frame root/center/IK teleport", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("stationary preview limb-motion root travel", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("same-frame hips Y delta fail threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0 &&
                reason.IndexOf("same-frame foot bottom Y delta fail threshold exceeded", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool HasReferenceAlignedCorrectedCounterpart(
            MotionComparisonFrameQualitySummary summary,
            MotionComparisonFrameQualitySummary[] summaries)
        {
            string candidate = summary.candidate_label ?? string.Empty;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            return summaries.Any(other =>
                !ReferenceEquals(other, summary) &&
                other != null &&
                IsCorrectedCandidateForRawCandidate(candidate, other.candidate_label ?? string.Empty) &&
                IsCorrectedCandidateRole(other) &&
                (string.Equals(other.status, "pass", StringComparison.OrdinalIgnoreCase) ||
                    IsSubManualPoseOnlyResidual(other)));
        }

        private static bool IsCorrectedCandidateForRawCandidate(
            string rawCandidateLabel,
            string correctedCandidateLabel)
        {
            if (string.IsNullOrWhiteSpace(rawCandidateLabel) ||
                string.IsNullOrWhiteSpace(correctedCandidateLabel))
            {
                return false;
            }

            return string.Equals(correctedCandidateLabel, rawCandidateLabel, StringComparison.Ordinal) ||
                correctedCandidateLabel.StartsWith(rawCandidateLabel + " ", StringComparison.Ordinal);
        }

        private static bool IsEvaluationCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return string.Equals(
                summary.frame_quality_evaluation_role,
                "evaluation_candidate_metrics",
                StringComparison.Ordinal);
        }

        private static bool IsCorrectedCandidateRole(MotionComparisonFrameQualitySummary summary)
        {
            return string.Equals(
                summary.frame_quality_evaluation_role,
                "corrected_candidate_metrics",
                StringComparison.Ordinal);
        }

        private static void MarkAsReferenceAligned(
            MotionComparisonFrameQualitySummary summary,
            string basis)
        {
            summary.status = "pass";
            summary.status_reason = string.IsNullOrWhiteSpace(summary.status_reason)
                ? basis
                : $"{basis}; diagnostic={summary.status_reason}";
        }
    }
}
