using System.IO;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceVideoFrameRoleDiagnosticsAttacher
    {
        internal static ReferenceVideoClipCoverageData Attach(
            VisualComparisonFrameRoleDiagnosticsData diagnostics,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string provenanceEvidencePath,
            string analysisResultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string projectRoot,
            string canonicalContext,
            string analysisMetricBasis)
        {
            if (diagnostics == null)
            {
                return null;
            }

            ReferenceVideoDiagnosticsMapper.Initialize(
                diagnostics,
                referenceClipStartSeconds,
                requestedDurationSeconds,
                provenanceEvidencePath,
                analysisResultPath,
                frameMetricsPath,
                contactSheetPath);
            diagnostics.reference_mp4_canonical_context = canonicalContext ?? string.Empty;
            diagnostics.reference_mp4_analysis_metric_basis = analysisMetricBasis ?? string.Empty;

            string resolvedResultPath = ResolveProjectRelative(analysisResultPath, projectRoot);
            string resolvedFrameMetricsPath = ResolveProjectRelative(frameMetricsPath, projectRoot);
            string resolvedContactSheetPath = ResolveProjectRelative(contactSheetPath, projectRoot);
            bool provenanceEvidenceExists = File.Exists(
                ResolveProjectRelative(provenanceEvidencePath, projectRoot));
            bool contactSheetExists = File.Exists(resolvedContactSheetPath);
            ReferenceVideoDiagnosticsData referenceVideo = ReferenceVideoDiagnosticsReader.Read(
                resolvedResultPath,
                resolvedFrameMetricsPath);
            ReferenceVideoClipCoverageData coverage = ReferenceVideoClipCoverageCalculator.Calculate(
                referenceVideo.FrameMetricRows,
                diagnostics.reference_mp4_current_clip_start_seconds,
                diagnostics.reference_mp4_current_clip_duration_seconds);
            ReferenceVideoDiagnosticsMapper.Apply(
                diagnostics,
                referenceVideo,
                coverage,
                provenanceEvidenceExists,
                contactSheetExists);
            return coverage;
        }

        private static string ResolveProjectRelative(string path, string projectRoot)
        {
            return VisualComparisonArtifactPathResolver.ResolveProjectRelative(path, projectRoot);
        }
    }
}
