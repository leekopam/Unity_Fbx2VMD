namespace Fbx2Vmd.FBXImporter
{
    internal static class YybReferenceVideoFrameMetricAttacher
    {
        internal static void Attach(
            VisualComparisonFrameRoleDiagnosticsData diagnostics,
            ReferenceVideoClipCoverageData coverage,
            string projectRoot)
        {
            if (diagnostics == null ||
                coverage == null ||
                diagnostics.reference_mp4_current_clip_duration_seconds <= 0f)
            {
                return;
            }

            diagnostics.referenceMp4CurrentClipRows.Clear();
            diagnostics.referenceMp4CurrentClipRows.AddRange(coverage.Rows);
            if (coverage.SampleCount <= 0)
            {
                return;
            }

            float sumUpperLimbSpan = 0f;
            float sumLowerLimbSpan = 0f;
            int limbSpanSampleCount = 0;
            foreach (ReferenceMp4FrameMetricRow row in coverage.Rows)
            {
                string framePath = VisualComparisonArtifactPathResolver.ResolveProjectRelative(
                    row.framePath,
                    projectRoot);
                if (YybScreenshotDiagnosticAnalyzer.TryAnalyzeCandidateScreenshotFrame(
                        framePath,
                        out var imageMetric,
                        out _) &&
                    VisualComparisonFrameGeometryCalculator.IsFiniteMetric(imageMetric.UpperLimbSpanRatio) &&
                    VisualComparisonFrameGeometryCalculator.IsFiniteMetric(imageMetric.LowerLimbSpanRatio))
                {
                    row.upperLimbSpanRatio = imageMetric.UpperLimbSpanRatio;
                    row.lowerLimbSpanRatio = imageMetric.LowerLimbSpanRatio;
                    row.silhouetteSpanProfile = imageMetric.SilhouetteSpanProfile;
                    row.silhouetteEndpointProfile = imageMetric.SilhouetteEndpointProfile;
                    row.imageSpaceKeypointProfile = imageMetric.ImageSpaceKeypointProfile;
                    row.hasNonHairBrightPixels = imageMetric.HasNonHairBrightPixels;
                    row.nonHairBBoxHeightRatio = imageMetric.NonHairBBoxHeightRatio;
                    row.nonHairBBoxWidthRatio = imageMetric.NonHairBBoxWidthRatio;
                    row.nonHairCenterXRatio = imageMetric.NonHairCenterX;
                    row.nonHairBottomGapRatio = imageMetric.NonHairBottomGapRatio;
                    row.nonHairImageSpaceKeypointProfile = imageMetric.NonHairImageSpaceKeypointProfile;
                    sumUpperLimbSpan += imageMetric.UpperLimbSpanRatio;
                    sumLowerLimbSpan += imageMetric.LowerLimbSpanRatio;
                    limbSpanSampleCount++;
                }
            }

            if (limbSpanSampleCount > 0)
            {
                diagnostics.reference_mp4_current_clip_avg_upper_limb_span_ratio =
                    sumUpperLimbSpan / limbSpanSampleCount;
                diagnostics.reference_mp4_current_clip_avg_lower_limb_span_ratio =
                    sumLowerLimbSpan / limbSpanSampleCount;
            }
        }
    }
}
