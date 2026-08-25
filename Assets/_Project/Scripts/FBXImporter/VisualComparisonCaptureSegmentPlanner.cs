using System;
using System.IO;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class VisualComparisonManualCapturePlan
    {
        public float StartTimeSeconds;
        public float DurationSeconds;
        public int TargetFrameCount;
        public string OutputBaseName;
        public string ComparisonLabel;
    }

    internal static class VisualComparisonCaptureSegmentPlanner
    {
        internal static FBXVmdPipeline.EditorDiagnosticSmokeSegment ResolveSegment(string value)
        {
            if (string.Equals(value, "middle", StringComparison.OrdinalIgnoreCase))
            {
                return FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle;
            }

            if (string.Equals(value, "tail", StringComparison.OrdinalIgnoreCase))
            {
                return FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail;
            }

            return FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head;
        }

        internal static VisualComparisonManualCapturePlan BuildManualCapturePlan(
            string labelSuffix,
            string fbxFileName,
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            float frameRate,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            float clipLength = Math.Max(0.1f, referenceClipLengthSeconds);
            float requestedDuration = Math.Max(0.1f, requestedDurationSeconds);
            float startTime = CalculateStartTime(clipLength, requestedDuration, segment);
            float remainingLength = Math.Max(0.1f, clipLength - startTime);
            float captureDuration = Math.Min(requestedDuration, remainingLength);
            int targetFrameCount = Math.Max(1, (int)Math.Ceiling(captureDuration * frameRate));
            string segmentToken = segment == FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head
                ? string.Empty
                : $"_{GetSegmentLabel(segment)}";
            string outputBaseName =
                $"{labelSuffix}_{Path.GetFileNameWithoutExtension(fbxFileName)}{segmentToken}_{(int)Math.Ceiling(captureDuration)}s_animtime";

            return new VisualComparisonManualCapturePlan
            {
                StartTimeSeconds = startTime,
                DurationSeconds = captureDuration,
                TargetFrameCount = targetFrameCount,
                OutputBaseName = outputBaseName,
                ComparisonLabel = $"manual_{outputBaseName}"
            };
        }

        internal static float CalculateStartTime(
            float referenceClipLengthSeconds,
            float requestedDurationSeconds,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
        {
            float clipLength = Math.Max(0.1f, referenceClipLengthSeconds);
            float safeDuration = Math.Max(0.1f, requestedDurationSeconds);
            switch (segment)
            {
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle:
                    return Math.Max(0f, (clipLength - safeDuration) * 0.5f);
                case FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail:
                    return Math.Max(0f, clipLength - safeDuration);
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
    }
}
