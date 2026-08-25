using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class ReferenceVideoDiagnosticsMapperTests
    {
        [Test]
        public void Given_ReferenceDiagnostics_When_Mapping_Then_PopulatesFileAndClipMetrics()
        {
            object diagnostics = Create("VisualComparisonFrameRoleDiagnosticsData");
            Invoke(
                "Initialize",
                diagnostics,
                -2f,
                5f,
                null,
                "analysis.json",
                "metrics.json",
                "contact.png");
            object source = Create("ReferenceVideoDiagnosticsData");
            SetProperty(source, "AnalysisFileExists", true);
            SetProperty(source, "AnalysisSchema", "analysis-v1");
            SetProperty(source, "ExtractedFrameCount", 24);
            SetProperty(source, "VideoWidth", 1920);
            SetProperty(source, "VideoHeight", 1080);
            SetProperty(source, "AverageFrameRate", "30/1");
            SetProperty(source, "StreamDurationSeconds", 10f);
            SetProperty(source, "TotalVideoFrames", 300);
            SetProperty(source, "FrameMetricsFileExists", true);
            SetProperty(source, "FrameMetricsSchema", "metrics-v1");
            SetProperty(source, "FrameMetricsSampleCount", 2);
            SetProperty(source, "AverageBBoxHeightRatio", 0.7f);
            object coverage = Create("ReferenceVideoClipCoverageData");
            SetProperty(coverage, "EndSeconds", 5f);
            SetProperty(coverage, "SampleCount", 2);
            SetProperty(coverage, "SampleSeconds", new[] { 0f, 4f });
            SetProperty(coverage, "FirstSampleSeconds", 0f);
            SetProperty(coverage, "LastSampleSeconds", 4f);
            SetProperty(coverage, "SampleCoverageRatio", 0.8f);
            SetProperty(coverage, "SampleGapSeconds", 1f);
            SetProperty(coverage, "AverageBBoxHeightRatio", 0.75f);
            SetProperty(coverage, "AverageBBoxWidthRatio", 0.4f);
            SetProperty(coverage, "CenterXRangeRatio", 0.1f);
            SetProperty(coverage, "MaxBottomGapRatio", 0.02f);
            SetProperty(coverage, "AverageBrightAreaRatio", 0.3f);

            Invoke("Apply", diagnostics, source, coverage, true, false);

            Assert.That(GetField<string>(diagnostics, "reference_mp4_provenance_evidence_path"), Is.Empty);
            Assert.That(GetField<string>(diagnostics, "reference_mp4_analysis_result_path"), Is.EqualTo("analysis.json"));
            Assert.That(GetField<bool>(diagnostics, "reference_mp4_provenance_evidence_exists"), Is.True);
            Assert.That(GetField<bool>(diagnostics, "reference_mp4_contact_sheet_exists"), Is.False);
            Assert.That(GetField<string>(diagnostics, "reference_mp4_analysis_schema"), Is.EqualTo("analysis-v1"));
            Assert.That(GetField<int>(diagnostics, "reference_mp4_width"), Is.EqualTo(1920));
            Assert.That(GetField<int>(diagnostics, "reference_mp4_frame_metrics_sample_count"), Is.EqualTo(2));
            Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_start_seconds"), Is.EqualTo(0f));
            Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_end_seconds"), Is.EqualTo(5f));
            Assert.That(GetField<int>(diagnostics, "reference_mp4_current_clip_sample_count"), Is.EqualTo(2));
            Assert.That(GetField<float[]>(diagnostics, "reference_mp4_current_clip_sample_seconds"), Is.EqualTo(new[] { 0f, 4f }));
            Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_sample_coverage_ratio"), Is.EqualTo(0.8f));
            Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.EqualTo(0.75f));
            Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_upper_limb_span_ratio"), Is.NaN);
        }

        [Test]
        public void Given_NoClipSamples_When_Mapping_Then_KeepsMetricDefaults()
        {
            object diagnostics = Create("VisualComparisonFrameRoleDiagnosticsData");
            Invoke("Initialize", diagnostics, 3f, 0f, "p", "a", "m", "c");
            object source = Create("ReferenceVideoDiagnosticsData");
            object coverage = Create("ReferenceVideoClipCoverageData");

            Invoke("Apply", diagnostics, source, coverage, false, false);

            Assert.That(GetField<int>(diagnostics, "reference_mp4_current_clip_sample_count"), Is.EqualTo(0));
            Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.NaN);
            Assert.That(GetField<float[]>(diagnostics, "reference_mp4_current_clip_sample_seconds"), Is.Empty);
        }

        private static object Create(string typeName)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter." + typeName,
                throwOnError: true);
            return Activator.CreateInstance(type, nonPublic: true);
        }

        private static void Invoke(string methodName, params object[] arguments)
        {
            Type mapperType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceVideoDiagnosticsMapper",
                throwOnError: false);
            Assert.That(mapperType, Is.Not.Null, "모델 중립 참조 영상 진단 매핑 경계가 필요합니다.");
            MethodInfo method = mapperType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, arguments);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            target.GetType().GetProperty(propertyName).SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(fieldName).GetValue(target);
        }
    }
}
