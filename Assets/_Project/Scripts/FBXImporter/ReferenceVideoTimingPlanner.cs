#if UNITY_EDITOR
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceVideoTimingPlanner
    {
        public static ReferenceVideoTimingPlan Build(
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            bool enabled,
            float knownReferenceDurationSeconds)
        {
            float safeClipLength = Mathf.Max(0f, referenceClipLengthSeconds);
            float safeDuration = Mathf.Max(0.1f, requestedDurationSeconds);
            float defaultStart = VisualComparisonCaptureSegmentPlanner.CalculateStartTime(
                safeClipLength,
                safeDuration,
                segment);
            ReferenceVideoTimingPlan plan = new ReferenceVideoTimingPlan
            {
                Enabled = false,
                HasCandidateTimingOverride = false,
                ReferenceVideoStartSeconds = defaultStart,
                CandidateClipStartSeconds = defaultStart,
                CandidateClipSecondsPerReferenceSecond = 1f,
                ReferenceDurationSeconds = safeClipLength
            };

            if (!enabled ||
                safeClipLength <= 0f ||
                knownReferenceDurationSeconds <= 0f ||
                float.IsNaN(knownReferenceDurationSeconds) ||
                float.IsInfinity(knownReferenceDurationSeconds))
            {
                return plan;
            }

            float referenceStart = VisualComparisonCaptureSegmentPlanner.CalculateStartTime(
                knownReferenceDurationSeconds,
                safeDuration,
                segment);
            float candidateScale = Mathf.Max(
                0.0001f,
                safeClipLength / knownReferenceDurationSeconds);
            float candidateStart = referenceStart * candidateScale;
            float maxCandidateStart = Mathf.Max(
                0f,
                safeClipLength - (safeDuration * candidateScale));

            plan.Enabled = true;
            plan.HasCandidateTimingOverride = true;
            plan.ReferenceVideoStartSeconds = referenceStart;
            plan.CandidateClipStartSeconds = Mathf.Clamp(candidateStart, 0f, maxCandidateStart);
            plan.CandidateClipSecondsPerReferenceSecond = candidateScale;
            plan.ReferenceDurationSeconds = knownReferenceDurationSeconds;
            return plan;
        }
    }
}
#endif
