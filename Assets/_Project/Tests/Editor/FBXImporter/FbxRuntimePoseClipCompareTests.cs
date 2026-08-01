using Fbx2Vmd.Modules.FBXImporter.EditorTools;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class FbxRuntimePoseClipCompareTests
    {
        [Test]
        public void Given_PairedPoseRows_When_BuildingSummary_Then_FocusAndTopResidualsAreReported()
        {
            var rows = new List<FbxRuntimePoseClipCompareRunner.PoseComparisonRow>
            {
                new FbxRuntimePoseClipCompareRunner.PoseComparisonRow
                {
                    Frame = 0,
                    TimeSeconds = 0f,
                    Bone = "RightUpperArm",
                    RotationDifferenceDegrees = 3.5f
                },
                new FbxRuntimePoseClipCompareRunner.PoseComparisonRow
                {
                    Frame = 90,
                    TimeSeconds = 3f,
                    Bone = "RightLowerArm",
                    RotationDifferenceDegrees = 12.25f
                },
                new FbxRuntimePoseClipCompareRunner.PoseComparisonRow
                {
                    Frame = 90,
                    TimeSeconds = 3f,
                    Bone = "RightHand",
                    RotationDifferenceDegrees = 8.5f
                }
            };

            FbxRuntimePoseClipCompareRunner.ClipComparisonSummary summary =
                FbxRuntimePoseClipCompareRunner.BuildSummaryForTest(
                    rows,
                    new[] { "RightUpperArm", "RightLowerArm", "RightHand" },
                    highThresholdDegrees: 5f,
                    topRowLimit: 2);

            Assert.That(summary.RowCount, Is.EqualTo(3));
            Assert.That(summary.BoneCount, Is.EqualTo(3));
            Assert.That(summary.SampleFrameCount, Is.EqualTo(2));
            Assert.That(summary.HighRotationDifferenceCount, Is.EqualTo(2));
            Assert.That(summary.MaxRotationDifferenceDegrees, Is.EqualTo(12.25f).Within(0.0001f));
            Assert.That(summary.MaxRotationBone, Is.EqualTo("RightLowerArm"));
            Assert.That(summary.TopRows, Has.Count.EqualTo(2));
            Assert.That(summary.TopRows[0].Bone, Is.EqualTo("RightLowerArm"));
            Assert.That(summary.FocusBones["RightLowerArm"].MaxRotationDifferenceDegrees, Is.EqualTo(12.25f).Within(0.0001f));
            Assert.That(summary.FocusBones["RightUpperArm"].HighRotationDifferenceCount, Is.EqualTo(0));
        }

        [Test]
        public void Given_PrimaryAndFallbackMeta_When_BuildingImportVariants_Then_TargetSettingsAreIsolated()
        {
            const string primaryMeta = @"fileFormatVersion: 2
guid: 11111111111111111111111111111111
ModelImporter:
  animations:
    animationCompression: 3
    animationWrapMode: 0
  humanDescription:
    primaryHuman: 1
  skeleton:
  - name: PrimaryBone
    rotation: {x: 0, y: 0, z: 0, w: 1}
  animationType: 3
";
            const string fallbackMeta = @"fileFormatVersion: 2
guid: 22222222222222222222222222222222
ModelImporter:
  animations:
    animationCompression: 0
    animationWrapMode: 8
  humanDescription:
    fallbackHuman: 1
  skeleton:
  - name: FallbackBone
    rotation: {x: 0, y: 0, z: 1, w: 0}
  animationType: 3
";

            string scalarVariant = FbxRuntimePoseClipCompareRunner.BuildImportVariantMetaForTest(
                primaryMeta,
                fallbackMeta,
                FbxRuntimePoseClipCompareRunner.VariantFallbackAnimationScalars,
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
            string avatarVariant = FbxRuntimePoseClipCompareRunner.BuildImportVariantMetaForTest(
                primaryMeta,
                fallbackMeta,
                FbxRuntimePoseClipCompareRunner.VariantFallbackAvatarDefinition,
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

            Assert.That(scalarVariant, Does.Contain("guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
            Assert.That(scalarVariant, Does.Contain("animationCompression: 0"));
            Assert.That(scalarVariant, Does.Contain("animationWrapMode: 8"));
            Assert.That(scalarVariant, Does.Contain("primaryHuman: 1"));
            Assert.That(scalarVariant, Does.Contain("PrimaryBone"));
            Assert.That(avatarVariant, Does.Contain("guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
            Assert.That(avatarVariant, Does.Contain("animationCompression: 3"));
            Assert.That(avatarVariant, Does.Contain("fallbackHuman: 1"));
            Assert.That(avatarVariant, Does.Contain("FallbackBone"));
        }

        [Test]
        public void Given_ImportVariantComparisons_When_BuildingCorrelationSummary_Then_LikeCountsAreSeparated()
        {
            var comparisons = new List<FbxRuntimePoseClipCompareRunner.ImportVariantComparison>
            {
                new FbxRuntimePoseClipCompareRunner.ImportVariantComparison
                {
                    ComparisonToPrimary = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        MaxRotationDifferenceDegrees = 0.25f
                    },
                    ComparisonToFallback = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        MaxRotationDifferenceDegrees = 12f
                    }
                },
                new FbxRuntimePoseClipCompareRunner.ImportVariantComparison
                {
                    ComparisonToPrimary = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        MaxRotationDifferenceDegrees = 15f
                    },
                    ComparisonToFallback = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        MaxRotationDifferenceDegrees = 0.5f
                    }
                },
                new FbxRuntimePoseClipCompareRunner.ImportVariantComparison
                {
                    ComparisonToPrimary = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        MaxRotationDifferenceDegrees = 2f
                    },
                    ComparisonToFallback = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        MaxRotationDifferenceDegrees = 3f
                    }
                }
            };

            FbxRuntimePoseClipCompareRunner.ImportVariantCorrelationSummary summary =
                FbxRuntimePoseClipCompareRunner.BuildImportVariantCorrelationSummaryForTest(
                    comparisons,
                    closeThresholdDegrees: 1f,
                    farThresholdDegrees: 5f);

            Assert.That(summary.VariantCount, Is.EqualTo(3));
            Assert.That(summary.PrimaryLikeCount, Is.EqualTo(1));
            Assert.That(summary.FallbackLikeCount, Is.EqualTo(1));
            Assert.That(summary.MixedOrNeutralCount, Is.EqualTo(1));
        }

        [Test]
        public void Given_FocusBoneName_When_ResolvingRuntimeSkeletonBoneName_Then_FbxSkeletonNameIsReturned()
        {
            Assert.That(
                FbxRuntimePoseClipCompareRunner.ResolveRuntimeSkeletonBoneNameForTest("RightLowerArm"),
                Is.EqualTo("Skeleton_RightForeArm"));
            Assert.That(
                FbxRuntimePoseClipCompareRunner.ResolveRuntimeSkeletonBoneNameForTest("Chest"),
                Is.EqualTo("Skeleton_Spine1"));
            Assert.That(
                FbxRuntimePoseClipCompareRunner.ResolveRuntimeSkeletonBoneNameForTest("UnknownBone"),
                Is.EqualTo(string.Empty));
        }

        [Test]
        public void Given_RuntimeImporterSamples_When_BuildingVmdSampleCsv_Then_VmdBoneNamesAndFlipXzRotationAreWritten()
        {
            var rows = new List<FbxRuntimePoseClipCompareRunner.RuntimeImporterVmdSampleRow>
            {
                new FbxRuntimePoseClipCompareRunner.RuntimeImporterVmdSampleRow
                {
                    Frame = 300,
                    TimeSeconds = 10f,
                    HumanBone = "RightLowerArm",
                    VmdBoneName = FbxRuntimePoseClipCompareRunner.ResolveVmdBoneNameForTest("RightLowerArm"),
                    BoneIndex = 7,
                    LocalRotation = new UnityEngine.Quaternion(0.1f, 0.2f, 0.3f, 0.9f),
                    ExportVmdRotation = new UnityEngine.Quaternion(-0.1f, 0.2f, -0.3f, 0.9f),
                }
            };

            string csv = FbxRuntimePoseClipCompareRunner.BuildRuntimeImporterVmdSampleCsvForTest(rows);

            Assert.That(FbxRuntimePoseClipCompareRunner.ResolveVmdBoneNameForTest("RightLowerArm"), Is.EqualTo("右ひじ"));
            Assert.That(csv, Does.Contain("frameNumber,boneName,boneIndex"));
            Assert.That(csv, Does.Contain("300,右ひじ,7"));
            Assert.That(csv, Does.Contain("runtime_importer_local,flip_xz_runtime_importer_local"));
            Assert.That(csv, Does.Contain("-0.1,0.2,-0.3,0.9"));
        }

        [Test]
        public void Given_RawAssimpRotationKeys_When_SamplingBetweenKeys_Then_SlerpedSampleIsWrittenAsVmdCsv()
        {
            var keys = new List<FbxRuntimePoseClipCompareRunner.RawAssimpRotationKey>
            {
                new FbxRuntimePoseClipCompareRunner.RawAssimpRotationKey(0f, Quaternion.identity),
                new FbxRuntimePoseClipCompareRunner.RawAssimpRotationKey(2f, Quaternion.Euler(0f, 90f, 0f))
            };

            Quaternion sample = FbxRuntimePoseClipCompareRunner.SampleRawAssimpRotationKeysForTest(keys, 1f);
            Assert.That(Quaternion.Angle(sample, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.001f));

            var rows = new List<FbxRuntimePoseClipCompareRunner.RuntimeImporterVmdSampleRow>
            {
                new FbxRuntimePoseClipCompareRunner.RuntimeImporterVmdSampleRow
                {
                    Frame = 30,
                    TimeSeconds = 1f,
                    HumanBone = "RightUpperArm",
                    VmdBoneName = FbxRuntimePoseClipCompareRunner.ResolveVmdBoneNameForTest("RightUpperArm"),
                    BoneIndex = 0,
                    SourceMode = "raw_assimp_channel",
                    ExportSourceMode = "flip_xz_raw_assimp_channel",
                    LocalRotation = sample,
                    ExportVmdRotation = FbxRuntimePoseClipCompareRunner.ConvertUnityRotationToVmdRotationForTest(sample),
                }
            };

            string csv = FbxRuntimePoseClipCompareRunner.BuildRuntimeImporterVmdSampleCsvForTest(rows);

            Assert.That(csv, Does.Contain("raw_assimp_channel,flip_xz_raw_assimp_channel"));
            Assert.That(csv, Does.Contain(FbxRuntimePoseClipCompareRunner.ResolveVmdBoneNameForTest("RightUpperArm")));
        }

        [Test]
        public void Given_RawAssimpImportVariants_When_BuildingSummary_Then_DefaultLikeAndChangedVariantsAreSeparated()
        {
            var variants = new List<FbxRuntimePoseClipCompareRunner.RawAssimpImportVariantComparison>
            {
                new FbxRuntimePoseClipCompareRunner.RawAssimpImportVariantComparison
                {
                    VariantName = "runtime_default",
                    PreservePivots = false,
                    PostProcessLabel = "runtime_default",
                    ComparisonToDefault = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        RowCount = 48,
                        HighRotationDifferenceCount = 0,
                        MaxRotationDifferenceDegrees = 0f,
                        MaxRotationBone = "Chest",
                        MaxRotationFrame = 0,
                    },
                },
                new FbxRuntimePoseClipCompareRunner.RawAssimpImportVariantComparison
                {
                    VariantName = "preserve_pivots_true",
                    PreservePivots = true,
                    PostProcessLabel = "runtime_default",
                    ComparisonToDefault = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        RowCount = 48,
                        HighRotationDifferenceCount = 0,
                        MaxRotationDifferenceDegrees = 0.25f,
                        MaxRotationBone = "Head",
                        MaxRotationFrame = 90,
                    },
                },
                new FbxRuntimePoseClipCompareRunner.RawAssimpImportVariantComparison
                {
                    VariantName = "without_left_handed_basis",
                    PreservePivots = false,
                    PostProcessLabel = "without_left_handed_basis",
                    ComparisonToDefault = new FbxRuntimePoseClipCompareRunner.ClipComparisonSummary
                    {
                        RowCount = 48,
                        HighRotationDifferenceCount = 12,
                        MaxRotationDifferenceDegrees = 54f,
                        MaxRotationBone = "RightLowerArm",
                        MaxRotationFrame = 300,
                    },
                },
            };

            FbxRuntimePoseClipCompareRunner.RawAssimpImportVariantSummary summary =
                FbxRuntimePoseClipCompareRunner.BuildRawAssimpImportVariantSummaryForTest(
                    variants,
                    defaultLikeThresholdDegrees: 1f,
                    changedThresholdDegrees: 5f);

            Assert.That(summary.VariantCount, Is.EqualTo(3));
            Assert.That(summary.DefaultLikeCount, Is.EqualTo(2));
            Assert.That(summary.ChangedCount, Is.EqualTo(1));
            Assert.That(summary.MaxChangedVariantName, Is.EqualTo("without_left_handed_basis"));
            Assert.That(summary.MaxChangedRotationDegrees, Is.EqualTo(54f).Within(0.001f));
        }
    }
}
