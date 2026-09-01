using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using static Tests.Editor.FBXImporter.YybVisualComparisonFrameRoleDiagnosticsTestSupport;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonScreenshotBBoxNormalizedDiagnosticsTests
    {
        [Test]
        public void Given_BBoxNormalizedScreenshotDiagnostics_When_CheckingTestOwnership_Then_DedicatedFixtureOwnsContracts()
        {
            string[] integrationContractNames =
            {
                "Given_CandidateAndReferenceFrameImages_When_FramingDiffers_Then_SeparatesBBoxNormalizedKeypointResidual",
                "Given_CandidateTopRowHasSparseSilhouettePixels_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesRobustCapCenterKeypoints",
                "Given_CandidateAndReferenceFrameImages_When_NormalizedShapeDiffers_Then_RecordsMaxBBoxNormalizedKeypointAttribution",
                "Given_MaxBBoxNormalizedAttributionTouchesFrameEdge_When_BuildingDiagnostics_Then_RecordsClipContext",
                "Given_TimeMatchedSamplesIncludeFrameEdgeTouch_When_BuildingDiagnostics_Then_RecordsCropSafeKeypointAggregate",
                "Given_FrameEdgeTouchOnlyAffectsVerticalCap_When_BuildingDiagnostics_Then_RecordsKeypointLocalCropSafeAggregate"
            };

            foreach (string contractName in integrationContractNames)
            {
                MethodInfo legacyMethod = typeof(MmdExportSafetyDefaultsTests).GetMethod(
                    contractName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(
                    legacyMethod,
                    Is.Null,
                    $"bbox 정규화 이미지 진단 계약 '{contractName}'은 거대 안전 기본값 fixture가 아니라 전용 fixture가 소유해야 합니다.");
            }
        }

        [Test]
        public void Given_CandidateAndReferenceFrameImages_When_FramingDiffers_Then_SeparatesBBoxNormalizedKeypointResidual()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonFramingNormalizedKeypoints_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.32,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontA, new RectInt(2, 1, 6, 8));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

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

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta"), Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_avg_image_space_keypoint_l1_delta_removed_by_bbox_normalization"), Is.EqualTo(0.08f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_image_space_keypoint_l1_delta_removed_by_bbox_normalization"), Is.EqualTo(0.1f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_bbox_normalized_image_space_keypoint_basis"), Does.Contain("bbox-normalized"));
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
        public void Given_CandidateTopRowHasSparseSilhouettePixels_When_BuildingSummaryFrameRoleDiagnostics_Then_UsesRobustCapCenterKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonRobustCapKeypoints_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.32,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontA, new RectInt(3, 1, 4, 7), new RectInt(3, 8, 1, 1));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

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

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_image_space_keypoint_basis"), Does.Contain("bbox centerline"));
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
        public void Given_CandidateAndReferenceFrameImages_When_NormalizedShapeDiffers_Then_RecordsMaxBBoxNormalizedKeypointAttribution()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonNormalizedKeypointAttribution_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 0.8,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.32,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontA, new RectInt(3, 1, 4, 4), new RectInt(4, 5, 2, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

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

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_left"));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_reference_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_candidate_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_recorder_frame"), Is.EqualTo(0));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_x_delta"), Is.EqualTo(0.25f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_y_delta"), Is.EqualTo(0f).Within(0.00001f));
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
        public void Given_MaxBBoxNormalizedAttributionTouchesFrameEdge_When_BuildingDiagnostics_Then_RecordsClipContext()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonNormalizedKeypointCropContext_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refA = Path.Combine(frameFolder, "ref-a.png");
            string frontA = Path.Combine(frameFolder, "front-a.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 1.0,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.0,\n" +
                    "  \"avgBrightAreaRatio\": 0.4,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refA.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refA, new RectInt(3, 0, 4, 10));
                WriteFixturePng(frontA, new RectInt(3, 1, 4, 4), new RectInt(4, 5, 2, 4));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"fixture,Main_Auto,start,0,front,{frontA}\n");

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

                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_touches_frame_edge"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_touches_frame_edge"), Is.False);
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_reference_bottom_gap"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_keypoint_candidate_bottom_gap"), Is.EqualTo(0.1f).Within(0.00001f));
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
        public void Given_TimeMatchedSamplesIncludeFrameEdgeTouch_When_BuildingDiagnostics_Then_RecordsCropSafeKeypointAggregate()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonCropSafeKeypoints_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refEdge = Path.Combine(frameFolder, "ref-edge.png");
            string refSafe = Path.Combine(frameFolder, "ref-safe.png");
            string frontEdge = Path.Combine(frameFolder, "front-edge.png");
            string frontSafe = Path.Combine(frameFolder, "front-safe.png");
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
                    "  \"avgBBoxHeightRatio\": 0.9,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.36,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refEdge.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }},\n" +
                    $"    {{ \"seconds\": 1.0, \"framePath\": \"{refSafe.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.32 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refEdge, new RectInt(3, 0, 4, 10));
                WriteFixturePng(frontEdge, new RectInt(3, 1, 4, 4), new RectInt(4, 5, 2, 4));
                WriteFixturePng(refSafe, new RectInt(3, 1, 4, 8));
                WriteFixturePng(frontSafe, new RectInt(3, 1, 4, 8));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"edge,Main_Auto,start,0,front,{frontEdge}\n" +
                    $"safe,Main_Auto,middle,30,front,{frontSafe}\n");

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

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_avg_bbox_width_ratio_abs_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_avg_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_basis"), Does.Contain("edge-touch"));
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
        public void Given_FrameEdgeTouchOnlyAffectsVerticalCap_When_BuildingDiagnostics_Then_RecordsKeypointLocalCropSafeAggregate()
        {
            string root = Path.Combine(Path.GetTempPath(), "VisualComparisonKeypointLocalCropSafe_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refEdge = Path.Combine(frameFolder, "ref-edge.png");
            string frontEdge = Path.Combine(frameFolder, "front-edge.png");
            string indexPath = Path.Combine(frameFolder, "index.csv");

            try
            {
                File.WriteAllText(provenancePath, "fixture provenance");
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-analysis-fixture-v1\",\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"video\": { \"width\": 10, \"height\": 10, \"avg_frame_rate\": \"30/1\", \"stream_duration\": \"3.0\", \"nb_frames\": \"90\" }\n" +
                    "}\n");
                File.WriteAllText(
                    frameMetricsPath,
                    "{\n" +
                    "  \"schema\": \"ref-mp4-frame-metrics-fixture-v1\",\n" +
                    "  \"sampleCount\": 1,\n" +
                    "  \"extractedFrameCount\": 1,\n" +
                    "  \"avgBBoxHeightRatio\": 1.0,\n" +
                    "  \"avgBBoxWidthRatio\": 0.4,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.0,\n" +
                    "  \"avgBrightAreaRatio\": 0.4,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refEdge.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePng(refEdge, new RectInt(3, 0, 4, 10));
                WriteFixturePng(frontEdge, new RectInt(3, 0, 4, 5), new RectInt(4, 5, 2, 5));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"edge,Main_Auto,start,0,front,{frontEdge}\n");

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

                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_crop_safe_sample_count"), Is.EqualTo(0));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count"), Is.EqualTo(6));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_keypoint_local_crop_safe_basis"), Does.Contain("keypoint-local"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

    }
}
