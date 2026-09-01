#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class VisualComparisonFrameRoleDiagnosticsBuildRequest
    {
        internal int ReferenceTargetFrameCount { get; set; }
        internal int BaselineRecordedFrameCount { get; set; }
        internal int CandidateRecordedFrameCount { get; set; }
        internal float RequestedDurationSeconds { get; set; }
        internal float ReferenceClipStartSeconds { get; set; }
        internal string ReferenceVideoProvenanceEvidencePath { get; set; } = string.Empty;
        internal string ReferenceVideoAnalysisResultPath { get; set; } = string.Empty;
        internal string ReferenceVideoFrameMetricsPath { get; set; } = string.Empty;
        internal string ReferenceVideoContactSheetPath { get; set; } = string.Empty;
        internal string CandidateFrameIndexPath { get; set; } = string.Empty;
        internal string ReferenceVideoProjectRoot { get; set; } = string.Empty;
        internal string CandidateFrameProjectRoot { get; set; } = string.Empty;
        internal string TargetFrameCountRole { get; set; } = string.Empty;
        internal string BaselineRecordedFrameCountRole { get; set; } = string.Empty;
        internal string CandidateRecordedFrameCountRole { get; set; } = string.Empty;
        internal string FrameQualityMetricBasis { get; set; } = string.Empty;
        internal string VmdExportMetricBasis { get; set; } = string.Empty;
        internal string ReferenceVideoCanonicalContext { get; set; } = string.Empty;
        internal string ReferenceVideoAnalysisMetricBasis { get; set; } = string.Empty;
    }

    internal static class VisualComparisonFrameRoleDiagnosticsBuilder
    {
        internal static VisualComparisonFrameRoleDiagnosticsData Build(
            VisualComparisonFrameRoleDiagnosticsBuildRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var diagnostics = new VisualComparisonFrameRoleDiagnosticsData
            {
                reference_target_frame_count = Mathf.Max(0, request.ReferenceTargetFrameCount),
                baseline_recorded_frame_count = Mathf.Max(0, request.BaselineRecordedFrameCount),
                candidate_recorded_frame_count = Mathf.Max(0, request.CandidateRecordedFrameCount),
                baseline_frame_count_delta_from_reference_target = request.ReferenceTargetFrameCount > 0
                    ? request.BaselineRecordedFrameCount - request.ReferenceTargetFrameCount
                    : 0,
                candidate_frame_count_delta_from_reference_target = request.ReferenceTargetFrameCount > 0
                    ? request.CandidateRecordedFrameCount - request.ReferenceTargetFrameCount
                    : 0,
                target_frame_count_role = request.TargetFrameCountRole,
                baseline_recorded_frame_count_role = request.BaselineRecordedFrameCountRole,
                candidate_recorded_frame_count_role = request.CandidateRecordedFrameCountRole,
                frame_quality_metric_basis = request.FrameQualityMetricBasis,
                vmd_export_metric_basis = request.VmdExportMetricBasis
            };
            ReferenceVideoClipCoverageData coverage = ReferenceVideoFrameRoleDiagnosticsAttacher.Attach(
                diagnostics,
                request.RequestedDurationSeconds,
                request.ReferenceClipStartSeconds,
                request.ReferenceVideoProvenanceEvidencePath,
                request.ReferenceVideoAnalysisResultPath,
                request.ReferenceVideoFrameMetricsPath,
                request.ReferenceVideoContactSheetPath,
                request.ReferenceVideoProjectRoot,
                request.ReferenceVideoCanonicalContext,
                request.ReferenceVideoAnalysisMetricBasis);
            YybReferenceVideoFrameMetricAttacher.Attach(
                diagnostics,
                coverage,
                request.ReferenceVideoProjectRoot);
            YybScreenshotDiagnosticAnalyzer.AttachCandidateScreenshotFrameDiagnostics(
                diagnostics,
                request.CandidateFrameIndexPath,
                request.CandidateFrameProjectRoot);
            return diagnostics;
        }
    }
}
#endif
