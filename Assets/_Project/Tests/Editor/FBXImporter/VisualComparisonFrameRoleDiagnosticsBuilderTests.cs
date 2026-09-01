using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonFrameRoleDiagnosticsBuilderTests
    {
        [Test]
        public void Given_FrameRoleDiagnostics_When_CheckingOwnership_Then_BuilderOwnsComposition()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type builderType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonFrameRoleDiagnosticsBuilder",
                throwOnError: false);
            Assert.That(builderType, Is.Not.Null, "프레임 역할 진단 조립 전용 타입이 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            int runnerOwnedBuildMethodCount = runnerType
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .Count(method => method.Name == "BuildSummaryFrameRoleDiagnostics");

            Assert.That(
                runnerOwnedBuildMethodCount,
                Is.EqualTo(0),
                "배치 실행기는 프레임 역할 진단 조립을 소유하면 안 됩니다.");
        }

        [Test]
        public void Given_TimeMatchedImageDiagnostics_When_CheckingTestOwnership_Then_BuilderFixtureOwnsIntegrationContracts()
        {
            string[] integrationContractNames =
            {
                "Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesTimeMatchedCandidateAndReferenceFraming",
                "Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesBandedImageSpaceLimbSpans",
                "Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesSilhouetteProfileLimbSpans",
                "Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesDeterministicImageSpaceKeypoints"
            };

            foreach (string contractName in integrationContractNames)
            {
                MethodInfo legacyMethod = typeof(MmdExportSafetyDefaultsTests).GetMethod(
                    contractName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(
                    legacyMethod,
                    Is.Null,
                    $"프레임 역할 진단 통합 계약 '{contractName}'은 거대 안전 기본값 fixture가 아니라 builder fixture가 소유해야 합니다.");
            }
        }

        [Test]
        public void Given_FrameCounts_When_BuildingSummaryFrameRoleDiagnostics_Then_SeparatesReferenceTargetFromRecordedBaselines()
        {
            string root = Path.Combine(Path.GetTempPath(), "FrameRoleDiagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 7,\n" +
                    "  \"video\": {\n" +
                    "    \"width\": 1280,\n" +
                    "    \"height\": 720,\n" +
                    "    \"avg_frame_rate\": \"30/1\",\n" +
                    "    \"stream_duration\": \"2.500\",\n" +
                    "    \"nb_frames\": \"75\"\n" +
                    "  }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 4,\n" +
                    "  \"extractedFrameCount\": 7,\n" +
                    "  \"avgBBoxHeightRatio\": 0.42,\n" +
                    "  \"centerXRangeRatio\": 0.24,\n" +
                    "  \"maxBottomGapRatio\": 0.05,\n" +
                    "  \"avgBrightAreaRatio\": 0.12,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.4, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.01, \"brightAreaRatio\": 0.1 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.02, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.5, \"centerXRatio\": 0.4, \"bottomGapRatio\": 0.03, \"brightAreaRatio\": 0.3 },\n" +
                    "    { \"seconds\": 5.0, \"bboxHeightRatio\": 0.9, \"centerXRatio\": 0.9, \"bottomGapRatio\": 0.04, \"brightAreaRatio\": 0.9 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 6001,
                    baselineRecordedFrameCount: 6234,
                    candidateRecordedFrameCount: 5900,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath);

                Assert.That(GetField<int>(diagnostics, "reference_target_frame_count"), Is.EqualTo(6001));
                Assert.That(GetField<int>(diagnostics, "baseline_recorded_frame_count"), Is.EqualTo(6234));
                Assert.That(GetField<int>(diagnostics, "candidate_recorded_frame_count"), Is.EqualTo(5900));
                Assert.That(GetField<int>(diagnostics, "candidate_frame_count_delta_from_reference_target"), Is.EqualTo(-101));
                Assert.That(GetField<string>(diagnostics, "target_frame_count_role"), Does.Contain("ref_mmd_mp4"));
                Assert.That(GetField<string>(diagnostics, "baseline_recorded_frame_count_role"), Does.Contain("Sub_Manual"));
                Assert.That(GetField<string>(diagnostics, "candidate_recorded_frame_count_role"), Does.Contain("Main_Auto"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_provenance_evidence_path"), Is.EqualTo(provenancePath));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_analysis_result_path"), Is.EqualTo(resultPath));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_frame_metrics_path"), Is.EqualTo(frameMetricsPath));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_contact_sheet_path"), Is.EqualTo(contactSheetPath));
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_provenance_evidence_exists"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_analysis_result_exists"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_frame_metrics_exists"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "reference_mp4_contact_sheet_exists"), Is.True);
                Assert.That(GetField<string>(diagnostics, "reference_mp4_canonical_context"), Does.Contain("Sub_Manual"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_canonical_context"), Does.Contain("MMD"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_analysis_schema"), Is.EqualTo("ref-mp4-analysis-fixture-v1"));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_frame_metrics_schema"), Is.EqualTo("ref-mp4-frame-metrics-fixture-v1"));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_width"), Is.EqualTo(1280));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_height"), Is.EqualTo(720));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_avg_frame_rate"), Is.EqualTo("30/1"));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_stream_duration_seconds"), Is.EqualTo(2.5f).Within(0.000001f));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_total_video_frames"), Is.EqualTo(75));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_frame_metrics_sample_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_frame_metrics_extracted_frame_count"), Is.EqualTo(7));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_avg_bbox_height_ratio"), Is.EqualTo(0.42f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_center_x_range_ratio"), Is.EqualTo(0.24f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_max_bottom_gap_ratio"), Is.EqualTo(0.05f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_duration_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_current_clip_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_first_sample_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_last_sample_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_sample_coverage_ratio"), Is.EqualTo(1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_sample_gap_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.EqualTo(0.5f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_center_x_range_ratio"), Is.EqualTo(0.3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_max_bottom_gap_ratio"), Is.EqualTo(0.03f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bright_area_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_current_clip_sample_basis"), Does.Contain("requested duration"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesCandidateFramingToReferenceMp4()
        {
            string root = Path.Combine(Path.GetTempPath(), "CandidateFramingDiagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string rightIgnored = Path.Combine(frameFolder, "right-ignored.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.2,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.4, \"centerXRatio\": 0.1, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.5, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.2, \"brightAreaRatio\": 0.3 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(4, 2, 4, 5));
                WriteFixturePng(rightIgnored, new RectInt(0, 0, 10, 10));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t3,90,front,{frontB}\n" +
                    $"fixture,Main_Auto,t3,90,right,{rightIgnored}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_frame_index_path"), Is.EqualTo(indexPath));
                Assert.That(GetField<bool>(diagnostics, "candidate_screenshot_frame_index_exists"), Is.True);
                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_frame_metrics_view"), Is.EqualTo("front"));
                Assert.That(GetField<int>(diagnostics, "candidate_screenshot_frame_metrics_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<int>(diagnostics, "candidate_screenshot_nonblank_frame_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_bbox_height_ratio"), Is.EqualTo(0.65f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_center_x_range_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_bottom_gap_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_top_gap_ratio"), Is.EqualTo(0.3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_bright_area_ratio"), Is.EqualTo(0.26f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_avg_bbox_height_ratio_delta"), Is.EqualTo(0.15f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_center_x_range_ratio_delta"), Is.EqualTo(0.1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_max_bottom_gap_ratio_delta"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_avg_bright_area_ratio_delta"), Is.EqualTo(0.01f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.EqualTo(0.45f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_avg_bbox_height_ratio_delta"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_center_x_range_ratio_delta"), Is.EqualTo(0.1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_max_bottom_gap_ratio_delta"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_current_clip_avg_bright_area_ratio_delta"), Is.EqualTo(0.01f).Within(0.000001f));
                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_frame_metrics_basis"), Does.Contain("front"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ReportsCandidateTimingCoverageAgainstReferenceSamples()
        {
            string root = Path.Combine(Path.GetTempPath(), "CandidateTimingDiagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string frontC = Path.Combine(frameFolder, "front-c.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 3,\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"avgBBoxHeightRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.2,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.4, \"centerXRatio\": 0.1, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.5, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.2, \"brightAreaRatio\": 0.3 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.6, \"centerXRatio\": 0.3, \"bottomGapRatio\": 0.3, \"brightAreaRatio\": 0.4 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(3, 2, 4, 6));
                WriteFixturePng(frontC, new RectInt(4, 2, 4, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t1.7,51,front,{frontB}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontC}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_screenshot_time_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_first_sample_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_last_sample_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_sample_coverage_ratio"), Is.EqualTo(1f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_sample_gap_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_ref_sample_seconds_gap"), Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_ref_sample_seconds_gap"), Is.EqualTo(0.06666667f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_screenshot_sample_timing_basis"), Does.Contain("recorderFrame"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_TailSegmentStart_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesMatchingReferenceMp4Window()
        {
            string root = Path.Combine(Path.GetTempPath(), "TailReferenceWindowDiagnostics_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string frontC = Path.Combine(frameFolder, "front-c.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 6,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"153.0\", \"nb_frames\": \"4590\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 6,\n" +
                    "  \"extractedFrameCount\": 6,\n" +
                    "  \"avgBBoxHeightRatio\": 0.5,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.3,\n" +
                    "  \"maxBottomGapRatio\": 0.2,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.1, \"bboxWidthRatio\": 0.2, \"centerXRatio\": 0.1, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.2 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.2, \"bboxWidthRatio\": 0.3, \"centerXRatio\": 0.2, \"bottomGapRatio\": 0.2, \"brightAreaRatio\": 0.3 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.3, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.3, \"bottomGapRatio\": 0.3, \"brightAreaRatio\": 0.4 },\n" +
                    "    { \"seconds\": 150.0, \"bboxHeightRatio\": 0.6, \"bboxWidthRatio\": 0.7, \"centerXRatio\": 0.4, \"bottomGapRatio\": 0.01, \"brightAreaRatio\": 0.5 },\n" +
                    "    { \"seconds\": 151.5, \"bboxHeightRatio\": 0.7, \"bboxWidthRatio\": 0.8, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.02, \"brightAreaRatio\": 0.6 },\n" +
                    "    { \"seconds\": 153.0, \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.9, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.03, \"brightAreaRatio\": 0.7 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(3, 2, 4, 6));
                WriteFixturePng(frontC, new RectInt(4, 2, 4, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t1.5,45,front,{frontB}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontC}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnosticsWithReferenceClipStart(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    referenceClipStartSeconds: 150f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_start_seconds"), Is.EqualTo(150f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_end_seconds"), Is.EqualTo(153f).Within(0.000001f));
                Assert.That(GetField<int>(diagnostics, "reference_mp4_current_clip_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_first_sample_seconds"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_last_sample_seconds"), Is.EqualTo(3f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_height_ratio"), Is.EqualTo(0.7f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_center_x_range_ratio"), Is.EqualTo(0.2f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_max_ref_sample_seconds_gap"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_seconds_gap"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<string>(diagnostics, "reference_mp4_current_clip_sample_basis"), Does.Contain("clip start"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_SeparateArtifactRoots_When_Building_Then_ResolvesEachRootIndependently()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "SeparateFrameDiagnosticsRoots_" + Guid.NewGuid().ToString("N"));
            string referenceRoot = Path.Combine(root, "reference");
            string candidateRoot = Path.Combine(root, "candidate");
            Directory.CreateDirectory(referenceRoot);
            Directory.CreateDirectory(candidateRoot);
            string candidateFramePath = Path.Combine(candidateRoot, "front.png");

            try
            {
                File.WriteAllText(Path.Combine(referenceRoot, "provenance.md"), "fixture provenance");
                File.WriteAllText(
                    Path.Combine(referenceRoot, "result.json"),
                    "{\"schema\":\"reference-fixture-v1\",\"extractedFrameCount\":1," +
                    "\"video\":{\"width\":10,\"height\":10,\"avg_frame_rate\":\"30/1\"," +
                    "\"stream_duration\":\"1.0\",\"nb_frames\":\"30\"}}");
                File.WriteAllText(
                    Path.Combine(referenceRoot, "metrics.json"),
                    "{\"schema\":\"metrics-fixture-v1\",\"sampleCount\":1,\"extractedFrameCount\":1," +
                    "\"rows\":[{\"seconds\":0.0,\"bboxHeightRatio\":0.5,\"bboxWidthRatio\":0.4," +
                    "\"centerXRatio\":0.5,\"bottomGapRatio\":0.1,\"brightAreaRatio\":0.2}]}");
                File.WriteAllBytes(
                    Path.Combine(referenceRoot, "contact.png"),
                    new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(candidateFramePath, new RectInt(2, 1, 4, 8));
                File.WriteAllText(
                    Path.Combine(candidateRoot, "index.csv"),
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,candidate,start,0,front,{candidateFramePath}\n");

                object diagnostics = BuildFrameRoleDiagnostics(
                    referenceTargetFrameCount: 30,
                    baselineRecordedFrameCount: 30,
                    candidateRecordedFrameCount: 30,
                    requestedDurationSeconds: 1f,
                    referenceClipStartSeconds: 0f,
                    provenancePath: "provenance.md",
                    resultPath: "result.json",
                    frameMetricsPath: "metrics.json",
                    contactSheetPath: "contact.png",
                    candidateFrameIndexPath: "index.csv",
                    referenceVideoProjectRoot: referenceRoot,
                    candidateFrameProjectRoot: candidateRoot);

                Assert.That(GetField<bool>(diagnostics, "reference_mp4_analysis_result_exists"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "candidate_screenshot_frame_index_exists"), Is.True);
                Assert.That(GetField<int>(diagnostics, "candidate_screenshot_frame_metrics_sample_count"), Is.EqualTo(1));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateScreenshotIndex_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesTimeMatchedCandidateAndReferenceFraming()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateTimeMatchedFraming_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string frontC = Path.Combine(frameFolder, "front-c.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 3,\n" +
                    "  \"extractedFrameCount\": 3,\n" +
                    "  \"avgBBoxHeightRatio\": 0.58,\n" +
                    "  \"avgBBoxWidthRatio\": 0.46,\n" +
                    "  \"centerXRangeRatio\": 0.13,\n" +
                    "  \"maxBottomGapRatio\": 0.22,\n" +
                    "  \"avgBrightAreaRatio\": 0.26,\n" +
                    "  \"rows\": [\n" +
                    "    { \"seconds\": 0.0, \"bboxHeightRatio\": 0.7, \"bboxWidthRatio\": 0.44, \"centerXRatio\": 0.45, \"bottomGapRatio\": 0.08, \"brightAreaRatio\": 0.30 },\n" +
                    "    { \"seconds\": 1.5, \"bboxHeightRatio\": 0.55, \"bboxWidthRatio\": 0.38, \"centerXRatio\": 0.52, \"bottomGapRatio\": 0.22, \"brightAreaRatio\": 0.27 },\n" +
                    "    { \"seconds\": 3.0, \"bboxHeightRatio\": 0.48, \"bboxWidthRatio\": 0.56, \"centerXRatio\": 0.58, \"bottomGapRatio\": 0.18, \"brightAreaRatio\": 0.22 }\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(frontA, new RectInt(2, 1, 4, 8));
                WriteFixturePng(frontB, new RectInt(3, 2, 4, 6));
                WriteFixturePng(frontC, new RectInt(4, 2, 4, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,t1.5,45,front,{frontB}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontC}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_sample_count"), Is.EqualTo(3));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_seconds_gap"), Is.EqualTo(0f).Within(0.000001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bbox_height_ratio_abs_delta"), Is.EqualTo(0.05666667f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_height_ratio_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_bbox_width_ratio"), Is.EqualTo(0.46f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_bbox_width_ratio"), Is.EqualTo(0.4f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bbox_width_ratio_abs_delta"), Is.EqualTo(0.07333333f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_width_ratio_abs_delta"), Is.EqualTo(0.16f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_center_x_ratio_abs_delta"), Is.EqualTo(0.03f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bottom_gap_ratio_abs_delta"), Is.EqualTo(0.02f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bright_area_ratio_abs_delta"), Is.EqualTo(0.02333333f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_framing_metric_basis"), Does.Contain("nearest"));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_limb_span_basis"), Does.Contain("bbox width"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesBandedImageSpaceLimbSpans()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateBandedLimbSpan_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string refB = Path.Combine(frameFolder, "ref-b.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.30 }},\n" +
                    $"    {{ \"seconds\": 3.0, \"framePath\": \"{refB.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.20 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 4), new RectInt(2, 5, 6, 4));
                WriteFixturePng(refB, new RectInt(4, 1, 2, 4), new RectInt(3, 5, 4, 4));
                WriteFixturePng(frontA, new RectInt(2, 1, 5, 4), new RectInt(1, 5, 8, 4));
                WriteFixturePng(frontB, new RectInt(4, 1, 2, 4), new RectInt(2, 5, 5, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontB}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_upper_limb_span_ratio"), Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "reference_mp4_current_clip_avg_lower_limb_span_ratio"), Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_upper_limb_span_ratio"), Is.EqualTo(0.65f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_screenshot_avg_lower_limb_span_ratio"), Is.EqualTo(0.35f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_limb_band_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_upper_limb_span_ratio_abs_delta"), Is.EqualTo(0.15f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_upper_limb_span_ratio_abs_delta"), Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_lower_limb_span_ratio_abs_delta"), Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_lower_limb_span_ratio_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_limb_band_basis"), Does.Contain("silhouette"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesSilhouetteProfileLimbSpans()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateSilhouetteProfile_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string refB = Path.Combine(frameFolder, "ref-b.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.30 }},\n" +
                    $"    {{ \"seconds\": 3.0, \"framePath\": \"{refB.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.20 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 4), new RectInt(2, 5, 6, 4));
                WriteFixturePng(refB, new RectInt(4, 1, 2, 4), new RectInt(3, 5, 4, 4));
                WriteFixturePng(frontA, new RectInt(2, 1, 5, 4), new RectInt(1, 5, 8, 4));
                WriteFixturePng(frontB, new RectInt(4, 1, 2, 4), new RectInt(2, 5, 5, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontB}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_profile_band_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_profile_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_silhouette_profile_l1_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_l1_abs_delta"), Is.EqualTo(0.15f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_profile_band_abs_delta"), Is.EqualTo(0.2f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_silhouette_profile_basis"), Does.Contain("4-band"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_landmark_band_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_silhouette_landmark_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_silhouette_landmark_endpoint_abs_delta"), Is.EqualTo(0.05f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_silhouette_landmark_endpoint_abs_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_silhouette_landmark_basis"), Does.Contain("left/right"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_BuildingSummaryFrameRoleDiagnostics_Then_ComparesDeterministicImageSpaceKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybCandidateImageSpaceKeypoints_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string refB = Path.Combine(frameFolder, "ref-b.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string frontB = Path.Combine(frameFolder, "front-b.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 2,\n" +
                    "  \"extractedFrameCount\": 2,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.5,\n" +
                    "  \"centerXRangeRatio\": 0.1,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.25,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.30 }},\n" +
                    $"    {{ \"seconds\": 3.0, \"framePath\": \"{refB.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.20 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 4), new RectInt(2, 5, 6, 4));
                WriteFixturePng(refB, new RectInt(4, 1, 2, 4), new RectInt(3, 5, 4, 4));
                WriteFixturePng(frontA, new RectInt(2, 1, 5, 4), new RectInt(1, 5, 8, 4));
                WriteFixturePng(frontB, new RectInt(4, 1, 2, 4), new RectInt(2, 5, 5, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n" +
                    $"fixture,Main_Auto,finish,90,front,{frontB}\n");

                object diagnostics = BuildSummaryFrameRoleDiagnostics(
                    referenceTargetFrameCount: 90,
                    baselineRecordedFrameCount: 90,
                    candidateRecordedFrameCount: 90,
                    requestedDurationSeconds: 3f,
                    provenancePath,
                    resultPath,
                    frameMetricsPath,
                    contactSheetPath,
                    indexPath);

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_sample_count"), Is.EqualTo(2));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta"), Is.EqualTo(0.045f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_basis"), Does.Contain("deterministic 2D silhouette keypoints"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        private static object BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath)
        {
            return BuildFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds: 0f,
                provenancePath,
                resultPath,
                frameMetricsPath,
                contactSheetPath,
                candidateFrameIndexPath: string.Empty);
        }

        private static object BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath)
        {
            return BuildFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds: 0f,
                provenancePath,
                resultPath,
                frameMetricsPath,
                contactSheetPath,
                candidateFrameIndexPath);
        }

        private static object BuildSummaryFrameRoleDiagnosticsWithReferenceClipStart(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath)
        {
            return BuildFrameRoleDiagnostics(
                referenceTargetFrameCount,
                baselineRecordedFrameCount,
                candidateRecordedFrameCount,
                requestedDurationSeconds,
                referenceClipStartSeconds,
                provenancePath,
                resultPath,
                frameMetricsPath,
                contactSheetPath,
                candidateFrameIndexPath);
        }

        private static object BuildFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount,
            float requestedDurationSeconds,
            float referenceClipStartSeconds,
            string provenancePath,
            string resultPath,
            string frameMetricsPath,
            string contactSheetPath,
            string candidateFrameIndexPath,
            string referenceVideoProjectRoot = null,
            string candidateFrameProjectRoot = null)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type requestType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonFrameRoleDiagnosticsBuildRequest",
                throwOnError: true);
            object request = Activator.CreateInstance(requestType, nonPublic: true);
            SetProperty(request, "ReferenceTargetFrameCount", referenceTargetFrameCount);
            SetProperty(request, "BaselineRecordedFrameCount", baselineRecordedFrameCount);
            SetProperty(request, "CandidateRecordedFrameCount", candidateRecordedFrameCount);
            SetProperty(request, "RequestedDurationSeconds", requestedDurationSeconds);
            SetProperty(request, "ReferenceClipStartSeconds", referenceClipStartSeconds);
            SetProperty(request, "ReferenceVideoProvenanceEvidencePath", provenancePath);
            SetProperty(request, "ReferenceVideoAnalysisResultPath", resultPath);
            SetProperty(request, "ReferenceVideoFrameMetricsPath", frameMetricsPath);
            SetProperty(request, "ReferenceVideoContactSheetPath", contactSheetPath);
            SetProperty(request, "CandidateFrameIndexPath", candidateFrameIndexPath);
            SetProperty(
                request,
                "ReferenceVideoProjectRoot",
                referenceVideoProjectRoot ?? Directory.GetCurrentDirectory());
            SetProperty(
                request,
                "CandidateFrameProjectRoot",
                candidateFrameProjectRoot ?? Directory.GetCurrentDirectory());
            SetProperty(request, "TargetFrameCountRole", "ref_mmd_mp4 expected frame range for the full satisfaction_2 reference");
            SetProperty(request, "BaselineRecordedFrameCountRole", "Sub_Manual recorded comparison baseline; reported separately and not used as target_frame_count");
            SetProperty(request, "CandidateRecordedFrameCountRole", "Main_Auto candidate capture under test");
            SetProperty(request, "FrameQualityMetricBasis", "Unity pose metrics compare Sub_Manual and Main_Auto rows by recorderFrame; the ref_mmd_mp4 count is only the frame-count target");
            SetProperty(request, "VmdExportMetricBasis", "VMD export spike and floor metrics are evaluated on the Main_Auto candidate VMD");
            SetProperty(request, "ReferenceVideoCanonicalContext", "Ref MP4 is a manually postprocessed MMD render from Sub_Manual testPrefab + satisfaction_2.");
            SetProperty(request, "ReferenceVideoAnalysisMetricBasis", "MP4 analysis supplies visual bbox/framing context.");

            Type builderType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonFrameRoleDiagnosticsBuilder",
                throwOnError: true);
            MethodInfo buildMethod = builderType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);
            return buildMethod.Invoke(null, new[] { request });
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"Expected property '{propertyName}' to exist.");
            property.SetValue(target, value);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            Assert.That(target, Is.Not.Null);
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected field '{fieldName}' to exist.");
            return (T)field.GetValue(target);
        }

        private static void WriteFixturePng(string path, RectInt brightRect)
        {
            WriteFixturePng(path, new[] { brightRect });
        }

        private static void WriteFixturePng(string path, params RectInt[] brightRects)
        {
            var texture = new Texture2D(10, 10, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var pixels = new Color32[100];
                for (int index = 0; index < pixels.Length; index++)
                {
                    pixels[index] = new Color32(0, 0, 0, 255);
                }

                foreach (RectInt brightRect in brightRects)
                {
                    for (int y = brightRect.yMin; y < brightRect.yMax; y++)
                    {
                        for (int x = brightRect.xMin; x < brightRect.xMax; x++)
                        {
                            pixels[(y * 10) + x] = new Color32(255, 255, 255, 255);
                        }
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
