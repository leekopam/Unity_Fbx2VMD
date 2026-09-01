using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using static Tests.Editor.FBXImporter.VisualComparisonFrameRoleDiagnosticsTestSupport;

namespace Tests.Editor.FBXImporter
{
    public class YybScreenshotNonHairDiagnosticsTests
    {
        [Test]
        public void Given_YybNonHairScreenshotDiagnostics_When_CheckingTestOwnership_Then_DedicatedFixtureOwnsContracts()
        {
            Type legacyFixtureType = typeof(MmdExportSafetyDefaultsTests);
            string[] movedContractNames =
            {
                "Given_HairLikeSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints",
                "Given_DarkTealHairShadowExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints",
                "Given_NonHairSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_RecordsNonHairMaxAttribution",
                "Given_NonHairFrameEdgeTouchStillLeavesMiddleBandResidual_When_BuildingDiagnostics_Then_RecordsNonHairKeypointLocalCropSafeAggregate"
            };

            foreach (string movedContractName in movedContractNames)
            {
                MethodInfo legacyMethod = legacyFixtureType.GetMethod(
                    movedContractName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(
                    legacyMethod,
                    Is.Null,
                    $"YYB non-hair 이미지 진단 계약 '{movedContractName}'은 거대 안전 기본값 fixture가 아니라 전용 fixture가 소유해야 합니다.");
            }
        }

        [Test]
        public void Given_HairLikeSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybNonHairKeypointAggregate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-hair.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-hair.png");
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
                    "  \"avgBBoxWidthRatio\": 0.6,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.34,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.34 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(0, 210, 210, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(0, 210, 210, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 2), new Color32(0, 210, 210, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"hair,Main_Auto,start,0,front,{candidateFrame}\n");

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
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_right"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis"), Does.Contain("cyan/teal hair-like"));
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
        public void Given_DarkTealHairShadowExtendsCandidateBand_When_BuildingDiagnostics_Then_SeparatesNonHairBBoxNormalizedKeypoints()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybDarkHairShadowKeypointAggregate_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-dark-hair.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-dark-hair.png");
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
                    "  \"avgBBoxWidthRatio\": 0.6,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.34,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.34 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(25, 52, 54, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(25, 52, 54, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 2), new Color32(25, 52, 54, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"darkhair,Main_Auto,start,0,front,{candidateFrame}\n");

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
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_right"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(10));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_avg_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_bbox_normalized_image_space_keypoint_basis"), Does.Contain("dark teal hair-shadow"));
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
        public void Given_NonHairSilhouetteExtendsCandidateBand_When_BuildingDiagnostics_Then_RecordsNonHairMaxAttribution()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybNonHairMaxAttribution_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-nonhair.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-nonhair.png");
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
                    "  \"avgBBoxWidthRatio\": 0.6,\n" +
                    "  \"centerXRangeRatio\": 0.0,\n" +
                    "  \"maxBottomGapRatio\": 0.1,\n" +
                    "  \"avgBrightAreaRatio\": 0.34,\n" +
                    "  \"rows\": [\n" +
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 0.8, \"bboxWidthRatio\": 0.6, \"centerXRatio\": 0.6, \"bottomGapRatio\": 0.1, \"brightAreaRatio\": 0.34 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(255, 255, 255, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 1, 4, 8), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 1, 1, 1), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 2), new Color32(255, 255, 255, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"nonhair,Main_Auto,start,0,front,{candidateFrame}\n");

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

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_2_right"));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_index"), Is.EqualTo(7));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_seconds"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_recorder_frame"), Is.EqualTo(0));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_x_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_y_delta"), Is.EqualTo(0f).Within(0.00001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_candidate_x"), Is.GreaterThan(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_reference_x")));
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_touches_frame_edge"), Is.False);
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_touches_frame_edge"), Is.False);
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
        public void Given_NonHairFrameEdgeTouchStillLeavesMiddleBandResidual_When_BuildingDiagnostics_Then_RecordsNonHairKeypointLocalCropSafeAggregate()
        {
            string root = Path.Combine(Path.GetTempPath(), "YybNonHairKeypointLocalCropSafe_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string provenancePath = Path.Combine(root, "provenance.md");
            string resultPath = Path.Combine(root, "result.json");
            string frameMetricsPath = Path.Combine(root, "frame-metrics.json");
            string contactSheetPath = Path.Combine(root, "contact-sheet.png");
            string frameFolder = Path.Combine(root, "frames");
            Directory.CreateDirectory(frameFolder);
            string refFrame = Path.Combine(frameFolder, "ref-nonhair-edge.png");
            string candidateFrame = Path.Combine(frameFolder, "candidate-nonhair-edge.png");
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
                    $"    {{ \"seconds\": 0.0, \"framePath\": \"{refFrame.Replace("\\", "\\\\")}\", \"bboxHeightRatio\": 1.0, \"bboxWidthRatio\": 0.4, \"centerXRatio\": 0.5, \"bottomGapRatio\": 0.0, \"brightAreaRatio\": 0.4 }}\n" +
                    "  ]\n" +
                    "}\n");
                File.WriteAllBytes(contactSheetPath, new byte[] { 0x89, 0x50, 0x4e, 0x47 });
                WriteFixturePngWithColor(
                    refFrame,
                    new FixturePngFill(new RectInt(3, 0, 4, 10), new Color32(255, 255, 255, 255)));
                WriteFixturePngWithColor(
                    candidateFrame,
                    new FixturePngFill(new RectInt(3, 0, 4, 10), new Color32(255, 255, 255, 255)),
                    new FixturePngFill(new RectInt(8, 5, 1, 3), new Color32(255, 255, 255, 255)));
                File.WriteAllText(
                    indexPath,
                    "label,scene,reason,recorderFrame,view,path\n" +
                    $"nonhair-edge,Main_Auto,start,0,front,{candidateFrame}\n");

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

                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_reference_touches_frame_edge"), Is.True);
                Assert.That(GetField<bool>(diagnostics, "candidate_vs_reference_time_matched_non_hair_max_bbox_normalized_keypoint_candidate_touches_frame_edge"), Is.True);
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_sample_count"), Is.EqualTo(1));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_count"), Is.EqualTo(4));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_bbox_normalized_image_space_keypoint_excluded_count"), Is.EqualTo(6));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_l1_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<int>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_index"), Is.GreaterThanOrEqualTo(0));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_x_delta"), Is.GreaterThan(0.2f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_y_delta"), Is.LessThan(0.001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_candidate_x"), Is.Not.EqualTo(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_reference_x")).Within(0.001f));
                Assert.That(GetField<float>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_required_x_reduction_to_threshold"), Is.GreaterThan(0f));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_max_bbox_normalized_image_space_keypoint_label"), Is.EqualTo("band_1_right"));
                Assert.That(GetField<string>(diagnostics, "candidate_vs_reference_time_matched_non_hair_keypoint_local_crop_safe_basis"), Does.Contain("non-hair"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        private struct FixturePngFill
        {
            public FixturePngFill(RectInt rect, Color32 color)
            {
                Rect = rect;
                Color = color;
            }

            public RectInt Rect;
            public Color32 Color;
        }

        private static void WriteFixturePngWithColor(string path, params FixturePngFill[] fills)
        {
            var texture = new Texture2D(10, 10, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Color32[] pixels = new Color32[100];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(0, 0, 0, 255);
                }

                foreach (FixturePngFill fill in fills)
                {
                    for (int y = fill.Rect.yMin; y < fill.Rect.yMax; y++)
                    {
                        for (int x = fill.Rect.xMin; x < fill.Rect.xMax; x++)
                        {
                            pixels[(y * 10) + x] = fill.Color;
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
