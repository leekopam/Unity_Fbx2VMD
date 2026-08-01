#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Modules.FBXImporter.EditorTools
{
    public static class FbxRuntimePoseClipCompareRunner
    {
        private const string MenuPath = "Machine Spirit/FBX Diagnostics/Run Runtime Pose Clip Compare";
        private const string EvidenceDirectory = "Docs/Workflow/Local/progress/evidence";
        private const string PrimaryFbxPath = "Assets/_Project/FBX/satisfaction_2.fbx";
        private const string FallbackFbxPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string TestPrefabPath = "Assets/Plugins/VMDRecorderSample/Models/TestModel/testPrefab.prefab";
        private const string ManualControllerPath = "Assets/_ManualReference/SampleAnimation/TestAnimator1_Manual.controller";
        private const string FallbackControllerPath = "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller";
        private const string TempVariantDirectory = "Assets/_Project/Temp/FbxImportVariantDiagnostics";
        private const float FrameRate = 30f;
        private const float HighRotationDifferenceThresholdDegrees = 5f;
        public const string VariantFallbackFullMeta = "primary_fbx_with_fallback_full_meta";
        public const string VariantFallbackAnimationScalars = "primary_fbx_with_fallback_animation_scalars";
        public const string VariantFallbackAvatarDefinition = "primary_fbx_with_fallback_avatar_definition";
        public const string VariantFallbackSkeletonOnly = "primary_fbx_with_fallback_skeleton_only";
        public const string RawAssimpVariantRuntimeDefault = "runtime_default";
        public const string RawAssimpVariantPreservePivotsTrue = "preserve_pivots_true";
        public const string RawAssimpVariantWithoutLeftHandedBasis = "without_left_handed_basis";
        public const string RawAssimpVariantNoPostProcess = "no_postprocess";
        private static readonly int[] SampleFrames = { 0, 90, 300, 396, 700, 900 };
        private static readonly FocusBone[] FocusBones =
        {
            new FocusBone("RightUpperArm", HumanBodyBones.RightUpperArm, "Skeleton_RightArm", "右腕"),
            new FocusBone("RightLowerArm", HumanBodyBones.RightLowerArm, "Skeleton_RightForeArm", "右ひじ"),
            new FocusBone("RightHand", HumanBodyBones.RightHand, "Skeleton_RightHand", "右手首"),
            new FocusBone("LeftUpperArm", HumanBodyBones.LeftUpperArm, "Skeleton_LeftArm", "左腕"),
            new FocusBone("LeftLowerArm", HumanBodyBones.LeftLowerArm, "Skeleton_LeftForeArm", "左ひじ"),
            new FocusBone("LeftHand", HumanBodyBones.LeftHand, "Skeleton_LeftHand", "左手首"),
            new FocusBone("Head", HumanBodyBones.Head, "Skeleton_Head", "頭"),
            new FocusBone("Chest", HumanBodyBones.Chest, "Skeleton_Spine1", "上半身2"),
        };

        [MenuItem(MenuPath, false, 2126)]
        public static void RunMenu()
        {
            string reportPath = RunDiagnostic(null);
            Debug.Log($"[FbxRuntimePoseClipCompareRunner] report={reportPath}");
        }

        public static void Run()
        {
            string outputPath = GetArgumentValue("-poseCompareOutput");
            string reportPath = RunDiagnostic(outputPath);
            Debug.Log($"[FbxRuntimePoseClipCompareRunner] report={reportPath}");
            EditorApplication.Exit(0);
        }

        public static string RunDiagnostic(string outputPath)
        {
            AnimationClip primaryClip = LoadFirstAnimationClip(PrimaryFbxPath);
            AnimationClip fallbackClip = LoadFirstAnimationClip(FallbackFbxPath);
            if (primaryClip == null)
            {
                throw new InvalidOperationException($"Primary AnimationClip not found: {PrimaryFbxPath}");
            }

            if (fallbackClip == null)
            {
                throw new InvalidOperationException($"Fallback AnimationClip not found: {FallbackFbxPath}");
            }

            GameObject testPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TestPrefabPath);
            if (testPrefab == null)
            {
                throw new InvalidOperationException($"Target prefab not found: {TestPrefabPath}");
            }

            RuntimeAnimatorController controller =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ManualControllerPath) ??
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException("RuntimeAnimatorController not found for clip sampling.");
            }

            Dictionary<PoseKey, SampledPose> primarySamples = SampleClip(testPrefab, controller, primaryClip);
            Dictionary<PoseKey, SampledPose> fallbackSamples = SampleClip(testPrefab, controller, fallbackClip);
            List<PoseComparisonRow> rows = BuildRows(primarySamples, fallbackSamples);
            ClipComparisonSummary summary = BuildSummaryForTest(
                rows,
                FocusBones.Select(bone => bone.Name),
                HighRotationDifferenceThresholdDegrees,
                topRowLimit: 12);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            List<ImportVariantComparison> importVariants = BuildImportVariantComparisons(
                projectRoot,
                testPrefab,
                controller,
                primarySamples,
                fallbackSamples);
            RuntimeImporterComparison runtimeImporterComparison = BuildRuntimeImporterComparison(
                projectRoot,
                FallbackFbxPath,
                fallbackSamples);
            string reportPath = ResolveOutputPath(projectRoot, outputPath);
            string reportDirectory = Path.GetDirectoryName(reportPath);
            Directory.CreateDirectory(reportDirectory);
            string csvPath = Path.Combine(reportDirectory, "rows.csv");
            WriteRowsCsv(csvPath, rows);
            WriteReportJson(
                reportPath,
                csvPath,
                primaryClip,
                fallbackClip,
                controller,
                summary,
                rows,
                importVariants,
                runtimeImporterComparison,
                reportDirectory,
                projectRoot);
            AssetDatabase.Refresh();
            return MakeProjectRelativePath(projectRoot, reportPath);
        }

        public static ClipComparisonSummary BuildSummaryForTest(
            IReadOnlyList<PoseComparisonRow> rows,
            IEnumerable<string> focusBones,
            float highThresholdDegrees,
            int topRowLimit)
        {
            var frameNumbers = new HashSet<int>();
            var boneNames = new HashSet<string>();
            var topRows = rows
                .OrderByDescending(row => row.RotationDifferenceDegrees)
                .ThenBy(row => row.Bone, StringComparer.Ordinal)
                .ThenBy(row => row.Frame)
                .Take(Math.Max(0, topRowLimit))
                .ToList();

            foreach (PoseComparisonRow row in rows)
            {
                frameNumbers.Add(row.Frame);
                boneNames.Add(row.Bone);
            }

            PoseComparisonRow maxRow = topRows.Count > 0 ? topRows[0] : null;
            var focus = new Dictionary<string, FocusBoneSummary>(StringComparer.Ordinal);
            foreach (string focusBone in focusBones)
            {
                List<PoseComparisonRow> matchingRows = rows
                    .Where(row => string.Equals(row.Bone, focusBone, StringComparison.Ordinal))
                    .OrderByDescending(row => row.RotationDifferenceDegrees)
                    .ThenBy(row => row.Frame)
                    .ToList();
                PoseComparisonRow focusMax = matchingRows.FirstOrDefault();
                focus[focusBone] = new FocusBoneSummary
                {
                    Bone = focusBone,
                    RowCount = matchingRows.Count,
                    MaxRotationDifferenceDegrees = focusMax != null ? focusMax.RotationDifferenceDegrees : 0f,
                    MaxRotationFrame = focusMax != null ? focusMax.Frame : -1,
                    HighRotationDifferenceCount = matchingRows.Count(row => row.RotationDifferenceDegrees >= highThresholdDegrees),
                };
            }

            return new ClipComparisonSummary
            {
                RowCount = rows.Count,
                SampleFrameCount = frameNumbers.Count,
                BoneCount = boneNames.Count,
                HighRotationDifferenceThresholdDegrees = highThresholdDegrees,
                HighRotationDifferenceCount = rows.Count(row => row.RotationDifferenceDegrees >= highThresholdDegrees),
                MaxRotationDifferenceDegrees = maxRow != null ? maxRow.RotationDifferenceDegrees : 0f,
                MaxRotationBone = maxRow != null ? maxRow.Bone : string.Empty,
                MaxRotationFrame = maxRow != null ? maxRow.Frame : -1,
                FocusBones = focus,
                TopRows = topRows,
            };
        }

        public static string BuildImportVariantMetaForTest(
            string primaryMeta,
            string fallbackMeta,
            string variantName,
            string guid = "00000000000000000000000000000000")
        {
            string meta;
            switch (variantName)
            {
                case VariantFallbackFullMeta:
                    meta = fallbackMeta;
                    break;
                case VariantFallbackAnimationScalars:
                    meta = ReplaceScalar(
                        primaryMeta,
                        "animationCompression",
                        ExtractRequiredScalar(fallbackMeta, "animationCompression"));
                    meta = ReplaceScalar(
                        meta,
                        "animationWrapMode",
                        ExtractRequiredScalar(fallbackMeta, "animationWrapMode"));
                    break;
                case VariantFallbackAvatarDefinition:
                    meta = ReplaceYamlBlock(primaryMeta, fallbackMeta, "humanDescription");
                    meta = ReplaceYamlBlock(meta, fallbackMeta, "skeleton");
                    break;
                case VariantFallbackSkeletonOnly:
                    meta = ReplaceYamlBlock(primaryMeta, fallbackMeta, "skeleton");
                    break;
                default:
                    throw new ArgumentException($"Unknown import variant: {variantName}", nameof(variantName));
            }

            return ReplaceGuid(meta, guid);
        }

        public static ImportVariantCorrelationSummary BuildImportVariantCorrelationSummaryForTest(
            IReadOnlyList<ImportVariantComparison> comparisons,
            float closeThresholdDegrees,
            float farThresholdDegrees)
        {
            int primaryLike = 0;
            int fallbackLike = 0;
            int mixedOrNeutral = 0;
            foreach (ImportVariantComparison comparison in comparisons)
            {
                float primaryMax = comparison.ComparisonToPrimary.MaxRotationDifferenceDegrees;
                float fallbackMax = comparison.ComparisonToFallback.MaxRotationDifferenceDegrees;
                if (primaryMax <= closeThresholdDegrees && fallbackMax >= farThresholdDegrees)
                {
                    primaryLike++;
                }
                else if (fallbackMax <= closeThresholdDegrees && primaryMax >= farThresholdDegrees)
                {
                    fallbackLike++;
                }
                else
                {
                    mixedOrNeutral++;
                }
            }

            return new ImportVariantCorrelationSummary
            {
                VariantCount = comparisons.Count,
                PrimaryLikeCount = primaryLike,
                FallbackLikeCount = fallbackLike,
                MixedOrNeutralCount = mixedOrNeutral,
            };
        }

        public static RawAssimpImportVariantSummary BuildRawAssimpImportVariantSummaryForTest(
            IReadOnlyList<RawAssimpImportVariantComparison> comparisons,
            float defaultLikeThresholdDegrees,
            float changedThresholdDegrees)
        {
            int defaultLikeCount = 0;
            int changedCount = 0;
            RawAssimpImportVariantComparison maxChanged = null;
            foreach (RawAssimpImportVariantComparison comparison in comparisons)
            {
                float maxRotation = comparison.ComparisonToDefault != null
                    ? comparison.ComparisonToDefault.MaxRotationDifferenceDegrees
                    : 0f;
                int highCount = comparison.ComparisonToDefault != null
                    ? comparison.ComparisonToDefault.HighRotationDifferenceCount
                    : 0;
                if (maxRotation <= defaultLikeThresholdDegrees)
                {
                    defaultLikeCount++;
                }

                if (maxRotation >= changedThresholdDegrees || highCount > 0)
                {
                    changedCount++;
                    if (maxChanged == null
                        || maxRotation > maxChanged.ComparisonToDefault.MaxRotationDifferenceDegrees)
                    {
                        maxChanged = comparison;
                    }
                }
            }

            return new RawAssimpImportVariantSummary
            {
                VariantCount = comparisons.Count,
                DefaultLikeCount = defaultLikeCount,
                ChangedCount = changedCount,
                MaxChangedVariantName = maxChanged != null ? maxChanged.VariantName : string.Empty,
                MaxChangedRotationDegrees = maxChanged != null
                    ? maxChanged.ComparisonToDefault.MaxRotationDifferenceDegrees
                    : 0f,
            };
        }

        public static string ResolveRuntimeSkeletonBoneNameForTest(string focusBoneName)
        {
            foreach (FocusBone focusBone in FocusBones)
            {
                if (string.Equals(focusBone.Name, focusBoneName, StringComparison.Ordinal))
                {
                    return focusBone.RuntimeSkeletonBoneName;
                }
            }

            return string.Empty;
        }

        public static string ResolveVmdBoneNameForTest(string focusBoneName)
        {
            foreach (FocusBone focusBone in FocusBones)
            {
                if (string.Equals(focusBone.Name, focusBoneName, StringComparison.Ordinal))
                {
                    return focusBone.VmdBoneName;
                }
            }

            return string.Empty;
        }

        public static string BuildRuntimeImporterVmdSampleCsvForTest(IEnumerable<RuntimeImporterVmdSampleRow> rows)
        {
            return BuildRuntimeImporterVmdSampleCsv(rows);
        }

        public static Quaternion ConvertUnityRotationToVmdRotationForTest(Quaternion unityRotation)
        {
            return ConvertUnityRotationToVmdRotation(unityRotation);
        }

        public static Quaternion SampleRawAssimpRotationKeysForTest(
            IReadOnlyList<RawAssimpRotationKey> keys,
            float timeSeconds)
        {
            if (keys == null || keys.Count == 0)
            {
                return Quaternion.identity;
            }

            List<RawAssimpRotationKey> ordered = keys
                .OrderBy(key => key.TimeSeconds)
                .ToList();
            if (timeSeconds <= ordered[0].TimeSeconds)
            {
                return Normalize(ordered[0].Rotation);
            }

            int lastIndex = ordered.Count - 1;
            if (timeSeconds >= ordered[lastIndex].TimeSeconds)
            {
                return Normalize(ordered[lastIndex].Rotation);
            }

            for (int index = 0; index < lastIndex; index++)
            {
                RawAssimpRotationKey left = ordered[index];
                RawAssimpRotationKey right = ordered[index + 1];
                if (timeSeconds < left.TimeSeconds || timeSeconds > right.TimeSeconds)
                {
                    continue;
                }

                float span = Mathf.Max(0.000001f, right.TimeSeconds - left.TimeSeconds);
                float t = Mathf.Clamp01((timeSeconds - left.TimeSeconds) / span);
                return Normalize(Quaternion.SlerpUnclamped(
                    Normalize(left.Rotation),
                    Normalize(right.Rotation),
                    t));
            }

            return Normalize(ordered[lastIndex].Rotation);
        }

        private static List<ImportVariantComparison> BuildImportVariantComparisons(
            string projectRoot,
            GameObject testPrefab,
            RuntimeAnimatorController controller,
            Dictionary<PoseKey, SampledPose> primarySamples,
            Dictionary<PoseKey, SampledPose> fallbackSamples)
        {
            string primaryFbxFullPath = Path.Combine(projectRoot, PrimaryFbxPath.Replace('/', Path.DirectorySeparatorChar));
            string fallbackFbxFullPath = Path.Combine(projectRoot, FallbackFbxPath.Replace('/', Path.DirectorySeparatorChar));
            string primaryMeta = File.ReadAllText(primaryFbxFullPath + ".meta", Encoding.UTF8);
            string fallbackMeta = File.ReadAllText(fallbackFbxFullPath + ".meta", Encoding.UTF8);
            string[] variants =
            {
                VariantFallbackFullMeta,
                VariantFallbackAnimationScalars,
                VariantFallbackAvatarDefinition,
                VariantFallbackSkeletonOnly,
            };

            string tempFullDirectory = Path.Combine(projectRoot, TempVariantDirectory.Replace('/', Path.DirectorySeparatorChar));
            var comparisons = new List<ImportVariantComparison>();
            try
            {
                if (AssetDatabase.IsValidFolder(TempVariantDirectory))
                {
                    AssetDatabase.DeleteAsset(TempVariantDirectory);
                }

                Directory.CreateDirectory(tempFullDirectory);
                foreach (string variant in variants)
                {
                    string variantDirectory = Path.Combine(tempFullDirectory, variant);
                    Directory.CreateDirectory(variantDirectory);
                    string variantAssetPath = $"{TempVariantDirectory}/{variant}/satisfaction_2.fbx";
                    string variantFullPath = Path.Combine(projectRoot, variantAssetPath.Replace('/', Path.DirectorySeparatorChar));
                    File.Copy(primaryFbxFullPath, variantFullPath, overwrite: true);
                    string variantMeta = BuildImportVariantMetaForTest(
                        primaryMeta,
                        fallbackMeta,
                        variant,
                        UnityEditor.GUID.Generate().ToString());
                    File.WriteAllText(variantFullPath + ".meta", variantMeta, Encoding.UTF8);

                    AssetDatabase.ImportAsset(variantAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    AnimationClip variantClip = LoadFirstAnimationClip(variantAssetPath);
                    if (variantClip == null)
                    {
                        throw new InvalidOperationException($"Variant AnimationClip not found: {variantAssetPath}");
                    }

                    Dictionary<PoseKey, SampledPose> variantSamples = SampleClip(testPrefab, controller, variantClip);
                    List<PoseComparisonRow> variantToPrimaryRows = BuildRows(variantSamples, primarySamples);
                    List<PoseComparisonRow> variantToFallbackRows = BuildRows(variantSamples, fallbackSamples);
                    comparisons.Add(new ImportVariantComparison
                    {
                        VariantName = variant,
                        AssetPath = variantAssetPath,
                        ClipName = variantClip.name,
                        ClipLengthSeconds = variantClip.length,
                        ComparisonToPrimary = BuildSummaryForTest(
                            variantToPrimaryRows,
                            FocusBones.Select(bone => bone.Name),
                            HighRotationDifferenceThresholdDegrees,
                            topRowLimit: 6),
                        ComparisonToFallback = BuildSummaryForTest(
                            variantToFallbackRows,
                            FocusBones.Select(bone => bone.Name),
                            HighRotationDifferenceThresholdDegrees,
                            topRowLimit: 6),
                    });
                }
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(TempVariantDirectory))
                {
                    AssetDatabase.DeleteAsset(TempVariantDirectory);
                }

                if (Directory.Exists(tempFullDirectory))
                {
                    Directory.Delete(tempFullDirectory, recursive: true);
                }

                string tempParentDirectory = Path.GetDirectoryName(tempFullDirectory);
                if (
                    !string.IsNullOrEmpty(tempParentDirectory)
                    && Directory.Exists(tempParentDirectory)
                    && !Directory.EnumerateFileSystemEntries(tempParentDirectory).Any())
                {
                    Directory.Delete(tempParentDirectory);
                    string tempParentMeta = tempParentDirectory + ".meta";
                    if (File.Exists(tempParentMeta))
                    {
                        File.Delete(tempParentMeta);
                    }
                }
            }

            return comparisons;
        }

        private static RuntimeImporterComparison BuildRuntimeImporterComparison(
            string projectRoot,
            string assetPath,
            Dictionary<PoseKey, SampledPose> unityImportedSamples)
        {
            Dictionary<PoseKey, SampledPose> runtimeSamples = SampleRuntimeImportedClip(projectRoot, assetPath, out string runtimeClipName);
            List<PoseComparisonRow> rows = BuildRows(unityImportedSamples, runtimeSamples);
            List<RuntimeImporterVmdSampleRow> vmdSampleRows = BuildRuntimeImporterVmdSampleRows(
                runtimeSamples,
                "runtime_importer_local",
                "flip_xz_runtime_importer_local");
            Dictionary<PoseKey, SampledPose> rawAssimpSamples = SampleRawAssimpImportedClip(
                projectRoot,
                assetPath,
                out string rawAssimpAnimationName);
            List<PoseComparisonRow> rawAssimpRows = BuildRows(runtimeSamples, rawAssimpSamples);
            List<RuntimeImporterVmdSampleRow> rawAssimpVmdSampleRows = BuildRuntimeImporterVmdSampleRows(
                rawAssimpSamples,
                "raw_assimp_channel",
                "flip_xz_raw_assimp_channel");
            List<RawAssimpImportVariantComparison> rawAssimpImportVariants =
                BuildRawAssimpImportVariantComparisons(projectRoot, assetPath, rawAssimpSamples);
            return new RuntimeImporterComparison
            {
                AssetPath = assetPath,
                RuntimeClipName = runtimeClipName,
                RowCount = rows.Count,
                Summary = BuildSummaryForTest(
                    rows,
                    FocusBones.Select(bone => bone.Name),
                    HighRotationDifferenceThresholdDegrees,
                    topRowLimit: 12),
                Rows = rows,
                VmdSampleRows = vmdSampleRows,
                RawAssimpAnimationName = rawAssimpAnimationName,
                RawAssimpRowCount = rawAssimpRows.Count,
                RawAssimpSummary = BuildSummaryForTest(
                    rawAssimpRows,
                    FocusBones.Select(bone => bone.Name),
                    HighRotationDifferenceThresholdDegrees,
                    topRowLimit: 12),
                RawAssimpRows = rawAssimpRows,
                RawAssimpVmdSampleRows = rawAssimpVmdSampleRows,
                RawAssimpImportVariantSummary = BuildRawAssimpImportVariantSummaryForTest(
                    rawAssimpImportVariants,
                    defaultLikeThresholdDegrees: 1f,
                    changedThresholdDegrees: HighRotationDifferenceThresholdDegrees),
                RawAssimpImportVariants = rawAssimpImportVariants,
            };
        }

        private static List<RawAssimpImportVariantComparison> BuildRawAssimpImportVariantComparisons(
            string projectRoot,
            string assetPath,
            Dictionary<PoseKey, SampledPose> defaultRawAssimpSamples)
        {
            var comparisons = new List<RawAssimpImportVariantComparison>();
            foreach (RawAssimpImportVariantSpec spec in BuildRawAssimpImportVariantSpecs())
            {
                Dictionary<PoseKey, SampledPose> variantSamples = SampleRawAssimpImportedClip(
                    projectRoot,
                    assetPath,
                    spec.PreservePivots,
                    spec.PostProcessSteps,
                    out string animationName);
                List<PoseComparisonRow> rows = BuildRows(defaultRawAssimpSamples, variantSamples);
                comparisons.Add(new RawAssimpImportVariantComparison
                {
                    VariantName = spec.VariantName,
                    PreservePivots = spec.PreservePivots,
                    PostProcessLabel = spec.PostProcessLabel,
                    AnimationName = animationName,
                    ComparisonToDefault = BuildSummaryForTest(
                        rows,
                        FocusBones.Select(bone => bone.Name),
                        HighRotationDifferenceThresholdDegrees,
                        topRowLimit: 8),
                    Rows = rows,
                    VmdSampleRows = BuildRuntimeImporterVmdSampleRows(
                        variantSamples,
                        "raw_assimp_channel_" + spec.VariantName,
                        "flip_xz_raw_assimp_channel_" + spec.VariantName),
                });
            }

            return comparisons;
        }

        private static List<RawAssimpImportVariantSpec> BuildRawAssimpImportVariantSpecs()
        {
            Assimp.PostProcessSteps runtimeDefault =
                Fbx2Vmd.Modules.FBXImporter.RuntimeFBXImporter.BuildAssimpPostProcessStepsForEditorDiagnostics();
            Assimp.PostProcessSteps withoutLeftHandedBasis =
                runtimeDefault
                & ~Assimp.PostProcessSteps.MakeLeftHanded
                & ~Assimp.PostProcessSteps.FlipWindingOrder;
            return new List<RawAssimpImportVariantSpec>
            {
                new RawAssimpImportVariantSpec(
                    RawAssimpVariantRuntimeDefault,
                    preservePivots: false,
                    postProcessLabel: "runtime_default",
                    postProcessSteps: runtimeDefault),
                new RawAssimpImportVariantSpec(
                    RawAssimpVariantPreservePivotsTrue,
                    preservePivots: true,
                    postProcessLabel: "runtime_default",
                    postProcessSteps: runtimeDefault),
                new RawAssimpImportVariantSpec(
                    RawAssimpVariantWithoutLeftHandedBasis,
                    preservePivots: false,
                    postProcessLabel: "without_left_handed_basis",
                    postProcessSteps: withoutLeftHandedBasis),
                new RawAssimpImportVariantSpec(
                    RawAssimpVariantNoPostProcess,
                    preservePivots: false,
                    postProcessLabel: "no_postprocess",
                    postProcessSteps: (Assimp.PostProcessSteps)0),
            };
        }

        private static List<RuntimeImporterVmdSampleRow> BuildRuntimeImporterVmdSampleRows(
            Dictionary<PoseKey, SampledPose> runtimeSamples,
            string sourceMode,
            string exportSourceMode)
        {
            var rows = new List<RuntimeImporterVmdSampleRow>();
            foreach (SampledPose pose in runtimeSamples.Values)
            {
                FocusBone focus = FocusBones.FirstOrDefault(item => string.Equals(item.Name, pose.Bone, StringComparison.Ordinal));
                if (string.IsNullOrEmpty(focus.Name) || string.IsNullOrEmpty(focus.VmdBoneName))
                {
                    continue;
                }

                rows.Add(new RuntimeImporterVmdSampleRow
                {
                    Frame = pose.Frame,
                    TimeSeconds = pose.TimeSeconds,
                    HumanBone = pose.Bone,
                    VmdBoneName = focus.VmdBoneName,
                    BoneIndex = Array.IndexOf(FocusBones, focus),
                    SourceMode = sourceMode,
                    ExportSourceMode = exportSourceMode,
                    LocalRotation = pose.LocalRotation,
                    ExportVmdRotation = ConvertUnityRotationToVmdRotation(pose.LocalRotation),
                });
            }

            rows.Sort((left, right) =>
            {
                int frameCompare = left.Frame.CompareTo(right.Frame);
                return frameCompare != 0 ? frameCompare : string.Compare(left.VmdBoneName, right.VmdBoneName, StringComparison.Ordinal);
            });
            return rows;
        }

        private static Dictionary<PoseKey, SampledPose> SampleRawAssimpImportedClip(
            string projectRoot,
            string assetPath,
            out string animationName)
        {
            return SampleRawAssimpImportedClip(
                projectRoot,
                assetPath,
                preservePivots: false,
                postProcessSteps: Fbx2Vmd.Modules.FBXImporter.RuntimeFBXImporter.BuildAssimpPostProcessStepsForEditorDiagnostics(),
                out animationName);
        }

        private static Dictionary<PoseKey, SampledPose> SampleRawAssimpImportedClip(
            string projectRoot,
            string assetPath,
            bool preservePivots,
            Assimp.PostProcessSteps postProcessSteps,
            out string animationName)
        {
            animationName = string.Empty;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!Fbx2Vmd.Modules.FBXImporter.AssimpLibraryLoader.IsLoaded)
            {
                Fbx2Vmd.Modules.FBXImporter.AssimpLibraryLoader.LoadLibrary();
            }

            using (var importer = new Assimp.AssimpContext())
            {
                importer.SetConfig(new Assimp.Configs.FBXPreservePivotsConfig(preservePivots));
                Assimp.Scene scene = importer.ImportFile(
                    fullPath,
                    postProcessSteps);
                if (scene == null || !scene.HasAnimations || scene.Animations.Count == 0)
                {
                    return new Dictionary<PoseKey, SampledPose>();
                }

                Assimp.Animation animation = scene.Animations[0];
                animationName = string.IsNullOrWhiteSpace(animation.Name) ? "Animation_0" : animation.Name;
                double ticksPerSecond = animation.TicksPerSecond;
                if (ticksPerSecond <= 1.0)
                {
                    ticksPerSecond = 60.0;
                }

                var channelsByName = animation.NodeAnimationChannels
                    .Where(channel => channel.HasRotationKeys)
                    .ToDictionary(channel => channel.NodeName, channel => channel, StringComparer.Ordinal);
                var samples = new Dictionary<PoseKey, SampledPose>();
                foreach (int frame in SampleFrames)
                {
                    float time = frame / FrameRate;
                    foreach (FocusBone focusBone in FocusBones)
                    {
                        if (!channelsByName.TryGetValue(focusBone.RuntimeSkeletonBoneName, out Assimp.NodeAnimationChannel channel))
                        {
                            continue;
                        }

                        List<RawAssimpRotationKey> keys = channel.RotationKeys
                            .Select(key => new RawAssimpRotationKey(
                                (float)(key.Time / ticksPerSecond),
                                new Quaternion(key.Value.X, key.Value.Y, key.Value.Z, key.Value.W)))
                            .ToList();
                        Quaternion rotation = SampleRawAssimpRotationKeysForTest(keys, time);
                        samples[new PoseKey(focusBone.Name, frame)] = new SampledPose
                        {
                            Bone = focusBone.Name,
                            Frame = frame,
                            TimeSeconds = time,
                            LocalRotation = rotation,
                            LocalEuler = rotation.eulerAngles,
                        };
                    }
                }

                return samples;
            }
        }

        private static Dictionary<PoseKey, SampledPose> SampleRuntimeImportedClip(string projectRoot, string assetPath, out string runtimeClipName)
        {
            runtimeClipName = string.Empty;
            string fullPath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            var importer = new Fbx2Vmd.Modules.FBXImporter.RuntimeFBXImporter();
            GameObject importedRoot = null;
            try
            {
                importedRoot = importer.ImportSynchronouslyForEditorDiagnostics(fullPath);
                if (importedRoot == null)
                {
                    throw new InvalidOperationException($"RuntimeFBXImporter returned null for {assetPath}");
                }

                AnimationClip clip = importer.GetAnimationClips().FirstOrDefault();
                if (clip == null)
                {
                    throw new InvalidOperationException($"RuntimeFBXImporter clip not found for {assetPath}");
                }

                runtimeClipName = clip.name;
                importedRoot.hideFlags = HideFlags.HideAndDontSave;
                importedRoot.SetActive(true);
                var samples = new Dictionary<PoseKey, SampledPose>();
                foreach (int frame in SampleFrames)
                {
                    float time = frame / FrameRate;
                    clip.SampleAnimation(importedRoot, Mathf.Min(time, clip.length));
                    foreach (FocusBone focusBone in FocusBones)
                    {
                        Transform bone = FindChildByName(importedRoot.transform, focusBone.RuntimeSkeletonBoneName);
                        if (bone == null)
                        {
                            continue;
                        }

                        samples[new PoseKey(focusBone.Name, frame)] = new SampledPose
                        {
                            Bone = focusBone.Name,
                            Frame = frame,
                            TimeSeconds = time,
                            LocalRotation = bone.localRotation,
                            LocalEuler = bone.localRotation.eulerAngles,
                        };
                    }
                }

                return samples;
            }
            finally
            {
                if (importedRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(importedRoot);
                }
            }
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Dictionary<PoseKey, SampledPose> SampleClip(
            GameObject sourcePrefab,
            RuntimeAnimatorController controller,
            AnimationClip clip)
        {
            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(sourcePrefab);
                instance.name = $"FbxRuntimePoseClipCompare_{SanitizeName(clip.name)}";
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);

                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                {
                    throw new InvalidOperationException($"{sourcePrefab.name} does not contain a valid humanoid Animator.");
                }

                AnimatorOverrideController overrideController = new AnimatorOverrideController(controller);
                List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(overrides);
                string stateName = clip.name;
                if (overrides.Count > 0 && overrides[0].Key != null)
                {
                    stateName = overrides[0].Key.name;
                    overrideController[overrides[0].Key] = clip;
                }

                animator.runtimeAnimatorController = overrideController;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                animator.speed = 0f;
                animator.Rebind();
                animator.Update(0f);

                int stateHash = Animator.StringToHash(stateName);
                bool hasState = animator.HasState(0, stateHash);
                var samples = new Dictionary<PoseKey, SampledPose>();
                foreach (int frame in SampleFrames)
                {
                    float time = frame / FrameRate;
                    float normalizedTime = Mathf.Clamp01(time / Mathf.Max(0.0001f, clip.length));
                    if (hasState)
                    {
                        animator.Play(stateHash, 0, normalizedTime);
                    }
                    else
                    {
                        animator.Play(0, 0, normalizedTime);
                    }

                    animator.Update(0f);
                    foreach (FocusBone focusBone in FocusBones)
                    {
                        Transform bone = animator.GetBoneTransform(focusBone.HumanBone);
                        if (bone == null)
                        {
                            continue;
                        }

                        samples[new PoseKey(focusBone.Name, frame)] = new SampledPose
                        {
                            Bone = focusBone.Name,
                            Frame = frame,
                            TimeSeconds = time,
                            LocalRotation = bone.localRotation,
                            LocalEuler = bone.localRotation.eulerAngles,
                        };
                    }
                }

                return samples;
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static List<PoseComparisonRow> BuildRows(
            Dictionary<PoseKey, SampledPose> primarySamples,
            Dictionary<PoseKey, SampledPose> fallbackSamples)
        {
            var rows = new List<PoseComparisonRow>();
            foreach (KeyValuePair<PoseKey, SampledPose> entry in primarySamples)
            {
                if (!fallbackSamples.TryGetValue(entry.Key, out SampledPose fallbackPose))
                {
                    continue;
                }

                SampledPose primaryPose = entry.Value;
                rows.Add(new PoseComparisonRow
                {
                    Frame = primaryPose.Frame,
                    TimeSeconds = primaryPose.TimeSeconds,
                    Bone = primaryPose.Bone,
                    RotationDifferenceDegrees = Quaternion.Angle(primaryPose.LocalRotation, fallbackPose.LocalRotation),
                    PrimaryLocalEuler = primaryPose.LocalEuler,
                    FallbackLocalEuler = fallbackPose.LocalEuler,
                });
            }

            rows.Sort((left, right) =>
            {
                int frameCompare = left.Frame.CompareTo(right.Frame);
                return frameCompare != 0 ? frameCompare : string.Compare(left.Bone, right.Bone, StringComparison.Ordinal);
            });
            return rows;
        }

        private static AnimationClip LoadFirstAnimationClip(string assetPath)
        {
            UnityEngine.Object[] representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (UnityEngine.Object asset in representations)
            {
                if (asset is AnimationClip clip && clip.humanMotion)
                {
                    return clip;
                }
            }

            foreach (UnityEngine.Object asset in representations)
            {
                if (asset is AnimationClip clip)
                {
                    return clip;
                }
            }

            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (UnityEngine.Object asset in allAssets)
            {
                if (asset is AnimationClip clip && clip.humanMotion)
                {
                    return clip;
                }
            }

            foreach (UnityEngine.Object asset in allAssets)
            {
                if (asset is AnimationClip clip)
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static string ResolveOutputPath(string projectRoot, string outputPath)
        {
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                return Path.GetFullPath(Path.IsPathRooted(outputPath) ? outputPath : Path.Combine(projectRoot, outputPath));
            }

            string directory = Path.Combine(
                projectRoot,
                EvidenceDirectory,
                $"fbx_runtime_pose_clip_compare_satisfaction2_{DateTime.Now:yyyyMMdd-HHmmss}");
            return Path.Combine(directory, "report.json");
        }

        private static void WriteRowsCsv(string path, IReadOnlyList<PoseComparisonRow> rows)
        {
            var builder = new StringBuilder(rows.Count * 160);
            builder.AppendLine("frame,timeSeconds,bone,rotationDifferenceDegrees,primaryLocalEuler,fallbackLocalEuler");
            foreach (PoseComparisonRow row in rows)
            {
                builder.AppendLine(string.Join(",", new[]
                {
                    row.Frame.ToString(CultureInfo.InvariantCulture),
                    F(row.TimeSeconds),
                    EscapeCsv(row.Bone),
                    F(row.RotationDifferenceDegrees),
                    EscapeCsv(V(row.PrimaryLocalEuler)),
                    EscapeCsv(V(row.FallbackLocalEuler)),
                }));
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteReportJson(
            string reportPath,
            string csvPath,
            AnimationClip primaryClip,
            AnimationClip fallbackClip,
            RuntimeAnimatorController controller,
            ClipComparisonSummary summary,
            IReadOnlyList<PoseComparisonRow> rows,
            IReadOnlyList<ImportVariantComparison> importVariants,
            RuntimeImporterComparison runtimeImporterComparison,
            string reportDirectory,
            string projectRoot)
        {
            bool sourceFbxFilesIdentical = Sha256File(Path.Combine(projectRoot, PrimaryFbxPath.Replace('/', Path.DirectorySeparatorChar)))
                == Sha256File(Path.Combine(projectRoot, FallbackFbxPath.Replace('/', Path.DirectorySeparatorChar)));
            ImportVariantCorrelationSummary variantCorrelation = BuildImportVariantCorrelationSummaryForTest(
                importVariants,
                closeThresholdDegrees: 1f,
                farThresholdDegrees: HighRotationDifferenceThresholdDegrees);
            var builder = new StringBuilder(8192);
            builder.AppendLine("{");
            WriteJsonProperty(builder, 1, "schema", "fbx-runtime-pose-clip-compare-v1", comma: true);
            WriteJsonProperty(builder, 1, "generated_at", DateTime.Now.ToString("O", CultureInfo.InvariantCulture), comma: true);
            WriteJsonProperty(builder, 1, "primary_fbx_path", PrimaryFbxPath, comma: true);
            WriteJsonProperty(builder, 1, "fallback_fbx_path", FallbackFbxPath, comma: true);
            WriteJsonProperty(builder, 1, "source_fbx_files_identical_sha256", sourceFbxFilesIdentical, comma: true);
            WriteJsonProperty(builder, 1, "target_prefab_path", TestPrefabPath, comma: true);
            WriteJsonProperty(builder, 1, "controller_path", AssetDatabase.GetAssetPath(controller), comma: true);
            WriteJsonProperty(builder, 1, "primary_clip_name", primaryClip.name, comma: true);
            WriteJsonProperty(builder, 1, "primary_clip_length_seconds", primaryClip.length, comma: true);
            WriteJsonProperty(builder, 1, "primary_human_motion", primaryClip.humanMotion, comma: true);
            WriteJsonProperty(builder, 1, "fallback_clip_name", fallbackClip.name, comma: true);
            WriteJsonProperty(builder, 1, "fallback_clip_length_seconds", fallbackClip.length, comma: true);
            WriteJsonProperty(builder, 1, "fallback_human_motion", fallbackClip.humanMotion, comma: true);
            WriteJsonProperty(builder, 1, "frame_rate", FrameRate, comma: true);
            WriteJsonArray(builder, 1, "sample_frames", SampleFrames.Select(frame => frame.ToString(CultureInfo.InvariantCulture)), comma: true);
            WriteJsonProperty(builder, 1, "rows_csv_path", MakeProjectRelativePath(projectRoot, csvPath), comma: true);
            builder.AppendLine("  \"summary\": {");
            WriteJsonProperty(builder, 2, "row_count", summary.RowCount, comma: true);
            WriteJsonProperty(builder, 2, "sample_frame_count", summary.SampleFrameCount, comma: true);
            WriteJsonProperty(builder, 2, "bone_count", summary.BoneCount, comma: true);
            WriteJsonProperty(builder, 2, "high_rotation_difference_threshold_degrees", summary.HighRotationDifferenceThresholdDegrees, comma: true);
            WriteJsonProperty(builder, 2, "high_rotation_difference_count", summary.HighRotationDifferenceCount, comma: true);
            WriteJsonProperty(builder, 2, "max_rotation_difference_degrees", summary.MaxRotationDifferenceDegrees, comma: true);
            WriteJsonProperty(builder, 2, "max_rotation_bone", summary.MaxRotationBone, comma: true);
            WriteJsonProperty(builder, 2, "max_rotation_frame", summary.MaxRotationFrame, comma: false);
            builder.AppendLine("  },");
            builder.AppendLine("  \"focus_bones\": [");
            int focusIndex = 0;
            List<FocusBoneSummary> focusSummaries = summary.FocusBones.Values
                .OrderBy(focus => focus.Bone, StringComparer.Ordinal)
                .ToList();
            foreach (FocusBoneSummary focus in focusSummaries)
            {
                builder.AppendLine("    {");
                WriteJsonProperty(builder, 3, "bone", focus.Bone, comma: true);
                WriteJsonProperty(builder, 3, "row_count", focus.RowCount, comma: true);
                WriteJsonProperty(builder, 3, "max_rotation_difference_degrees", focus.MaxRotationDifferenceDegrees, comma: true);
                WriteJsonProperty(builder, 3, "max_rotation_frame", focus.MaxRotationFrame, comma: true);
                WriteJsonProperty(builder, 3, "high_rotation_difference_count", focus.HighRotationDifferenceCount, comma: false);
                builder.Append("    }");
                builder.AppendLine(++focusIndex == focusSummaries.Count ? "" : ",");
            }
            builder.AppendLine("  ],");

            builder.AppendLine("  \"top_rows\": [");
            for (int i = 0; i < summary.TopRows.Count; i++)
            {
                PoseComparisonRow row = summary.TopRows[i];
                builder.AppendLine("    {");
                WriteJsonProperty(builder, 3, "frame", row.Frame, comma: true);
                WriteJsonProperty(builder, 3, "time_seconds", row.TimeSeconds, comma: true);
                WriteJsonProperty(builder, 3, "bone", row.Bone, comma: true);
                WriteJsonProperty(builder, 3, "rotation_difference_degrees", row.RotationDifferenceDegrees, comma: false);
                builder.Append("    }");
                builder.AppendLine(i == summary.TopRows.Count - 1 ? "" : ",");
            }
            builder.AppendLine("  ],");
            builder.AppendLine("  \"import_variant_summary\": {");
            WriteJsonProperty(builder, 2, "variant_count", variantCorrelation.VariantCount, comma: true);
            WriteJsonProperty(builder, 2, "primary_like_count", variantCorrelation.PrimaryLikeCount, comma: true);
            WriteJsonProperty(builder, 2, "fallback_like_count", variantCorrelation.FallbackLikeCount, comma: true);
            WriteJsonProperty(builder, 2, "mixed_or_neutral_count", variantCorrelation.MixedOrNeutralCount, comma: false);
            builder.AppendLine("  },");
            builder.AppendLine("  \"import_variants\": [");
            for (int i = 0; i < importVariants.Count; i++)
            {
                WriteImportVariantJson(builder, importVariants[i], i == importVariants.Count - 1);
            }
            builder.AppendLine("  ],");
            WriteRuntimeImporterComparisonJson(builder, runtimeImporterComparison, reportDirectory, projectRoot);
            builder.AppendLine("}");
            File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
        }

        private static void WriteRuntimeImporterComparisonJson(
            StringBuilder builder,
            RuntimeImporterComparison comparison,
            string reportDirectory,
            string projectRoot)
        {
            string runtimeRowsCsvPath = Path.Combine(reportDirectory, "runtime_importer_rows.csv");
            string runtimeVmdSamplesCsvPath = Path.Combine(reportDirectory, "runtime_importer_vmd_samples.csv");
            string rawAssimpRowsCsvPath = Path.Combine(reportDirectory, "raw_assimp_vs_runtime_importer_rows.csv");
            string rawAssimpVmdSamplesCsvPath = Path.Combine(reportDirectory, "raw_assimp_vmd_samples.csv");
            WriteRowsCsv(runtimeRowsCsvPath, comparison.Rows);
            File.WriteAllText(runtimeVmdSamplesCsvPath, BuildRuntimeImporterVmdSampleCsv(comparison.VmdSampleRows), Encoding.UTF8);
            WriteRowsCsv(rawAssimpRowsCsvPath, comparison.RawAssimpRows);
            File.WriteAllText(rawAssimpVmdSamplesCsvPath, BuildRuntimeImporterVmdSampleCsv(comparison.RawAssimpVmdSampleRows), Encoding.UTF8);

            builder.AppendLine("  \"runtime_importer_comparison\": {");
            WriteJsonProperty(builder, 2, "asset_path", comparison.AssetPath, comma: true);
            WriteJsonProperty(builder, 2, "runtime_clip_name", comparison.RuntimeClipName, comma: true);
            WriteJsonProperty(builder, 2, "rows_csv_path", MakeProjectRelativePath(projectRoot, runtimeRowsCsvPath), comma: true);
            WriteJsonProperty(builder, 2, "vmd_samples_csv_path", MakeProjectRelativePath(projectRoot, runtimeVmdSamplesCsvPath), comma: true);
            WriteJsonProperty(builder, 2, "vmd_sample_count", comparison.VmdSampleRows.Count, comma: true);
            builder.AppendLine("    \"summary\": {");
            WriteSummaryJson(builder, comparison.Summary, 3);
            builder.AppendLine("    },");
            builder.AppendLine("    \"top_rows\": [");
            for (int i = 0; i < comparison.Summary.TopRows.Count; i++)
            {
                PoseComparisonRow row = comparison.Summary.TopRows[i];
                builder.AppendLine("      {");
                WriteJsonProperty(builder, 4, "frame", row.Frame, comma: true);
                WriteJsonProperty(builder, 4, "time_seconds", row.TimeSeconds, comma: true);
                WriteJsonProperty(builder, 4, "bone", row.Bone, comma: true);
                WriteJsonProperty(builder, 4, "rotation_difference_degrees", row.RotationDifferenceDegrees, comma: false);
                builder.Append("      }");
                builder.AppendLine(i == comparison.Summary.TopRows.Count - 1 ? "" : ",");
            }

            builder.AppendLine("    ],");
            builder.AppendLine("    \"raw_assimp_channel_comparison\": {");
            WriteJsonProperty(builder, 3, "animation_name", comparison.RawAssimpAnimationName, comma: true);
            WriteJsonProperty(builder, 3, "rows_csv_path", MakeProjectRelativePath(projectRoot, rawAssimpRowsCsvPath), comma: true);
            WriteJsonProperty(builder, 3, "vmd_samples_csv_path", MakeProjectRelativePath(projectRoot, rawAssimpVmdSamplesCsvPath), comma: true);
            WriteJsonProperty(builder, 3, "vmd_sample_count", comparison.RawAssimpVmdSampleRows.Count, comma: true);
            builder.AppendLine("      \"summary\": {");
            WriteSummaryJson(builder, comparison.RawAssimpSummary, 4);
            builder.AppendLine("      },");
            builder.AppendLine("      \"top_rows\": [");
            for (int i = 0; i < comparison.RawAssimpSummary.TopRows.Count; i++)
            {
                PoseComparisonRow row = comparison.RawAssimpSummary.TopRows[i];
                builder.AppendLine("        {");
                WriteJsonProperty(builder, 5, "frame", row.Frame, comma: true);
                WriteJsonProperty(builder, 5, "time_seconds", row.TimeSeconds, comma: true);
                WriteJsonProperty(builder, 5, "bone", row.Bone, comma: true);
                WriteJsonProperty(builder, 5, "rotation_difference_degrees", row.RotationDifferenceDegrees, comma: false);
                builder.Append("        }");
                builder.AppendLine(i == comparison.RawAssimpSummary.TopRows.Count - 1 ? "" : ",");
            }

            builder.AppendLine("      ],");
            WriteRawAssimpImportVariantsJson(
                builder,
                comparison.RawAssimpImportVariantSummary,
                comparison.RawAssimpImportVariants,
                reportDirectory,
                projectRoot);
            builder.AppendLine("    }");
            builder.AppendLine("  }");
        }

        private static void WriteRawAssimpImportVariantsJson(
            StringBuilder builder,
            RawAssimpImportVariantSummary summary,
            IReadOnlyList<RawAssimpImportVariantComparison> variants,
            string reportDirectory,
            string projectRoot)
        {
            string variantDirectory = Path.Combine(reportDirectory, "raw_assimp_import_variants");
            Directory.CreateDirectory(variantDirectory);
            builder.AppendLine("      \"import_variant_summary\": {");
            WriteJsonProperty(builder, 4, "variant_count", summary.VariantCount, comma: true);
            WriteJsonProperty(builder, 4, "default_like_count", summary.DefaultLikeCount, comma: true);
            WriteJsonProperty(builder, 4, "changed_count", summary.ChangedCount, comma: true);
            WriteJsonProperty(builder, 4, "max_changed_variant_name", summary.MaxChangedVariantName, comma: true);
            WriteJsonProperty(builder, 4, "max_changed_rotation_degrees", summary.MaxChangedRotationDegrees, comma: false);
            builder.AppendLine("      },");
            builder.AppendLine("      \"import_variants\": [");
            for (int i = 0; i < variants.Count; i++)
            {
                RawAssimpImportVariantComparison variant = variants[i];
                string rowsPath = Path.Combine(variantDirectory, variant.VariantName + "_rows.csv");
                string vmdSamplesPath = Path.Combine(variantDirectory, variant.VariantName + "_vmd_samples.csv");
                WriteRowsCsv(rowsPath, variant.Rows);
                File.WriteAllText(vmdSamplesPath, BuildRuntimeImporterVmdSampleCsv(variant.VmdSampleRows), Encoding.UTF8);

                builder.AppendLine("        {");
                WriteJsonProperty(builder, 5, "variant_name", variant.VariantName, comma: true);
                WriteJsonProperty(builder, 5, "preserve_pivots", variant.PreservePivots, comma: true);
                WriteJsonProperty(builder, 5, "post_process_label", variant.PostProcessLabel, comma: true);
                WriteJsonProperty(builder, 5, "animation_name", variant.AnimationName, comma: true);
                WriteJsonProperty(builder, 5, "rows_csv_path", MakeProjectRelativePath(projectRoot, rowsPath), comma: true);
                WriteJsonProperty(builder, 5, "vmd_samples_csv_path", MakeProjectRelativePath(projectRoot, vmdSamplesPath), comma: true);
                WriteJsonProperty(builder, 5, "vmd_sample_count", variant.VmdSampleRows.Count, comma: true);
                builder.AppendLine("          \"comparison_to_default\": {");
                WriteSummaryJson(builder, variant.ComparisonToDefault, 6);
                builder.AppendLine("          }");
                builder.Append("        }");
                builder.AppendLine(i == variants.Count - 1 ? "" : ",");
            }

            builder.AppendLine("      ]");
        }

        private static string BuildRuntimeImporterVmdSampleCsv(IEnumerable<RuntimeImporterVmdSampleRow> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine("frameNumber,boneName,boneIndex,sourceMode,exportSourceMode,ghostVsSourceLocalDeltaDegrees,parentRestBasisCorrectedVsSourceLocalDeltaDegrees,exportVsSourceLocalDeltaDegrees,sourceLocalDeltaX,sourceLocalDeltaY,sourceLocalDeltaZ,sourceLocalDeltaW,exportLocalX,exportLocalY,exportLocalZ,exportLocalW,exportVmdX,exportVmdY,exportVmdZ,exportVmdW,humanBone,timeSeconds");
            foreach (RuntimeImporterVmdSampleRow row in rows)
            {
                builder.Append(row.Frame.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(EscapeCsv(row.VmdBoneName));
                builder.Append(',');
                builder.Append(row.BoneIndex.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(EscapeCsv(string.IsNullOrWhiteSpace(row.SourceMode) ? "runtime_importer_local" : row.SourceMode));
                builder.Append(',');
                builder.Append(EscapeCsv(string.IsNullOrWhiteSpace(row.ExportSourceMode) ? "flip_xz_runtime_importer_local" : row.ExportSourceMode));
                builder.Append(",,,,");
                AppendQuaternion(builder, row.LocalRotation);
                builder.Append(',');
                AppendQuaternion(builder, row.LocalRotation);
                builder.Append(',');
                AppendQuaternion(builder, row.ExportVmdRotation);
                builder.Append(',');
                builder.Append(EscapeCsv(row.HumanBone));
                builder.Append(',');
                builder.Append(F(row.TimeSeconds));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static void AppendQuaternion(StringBuilder builder, Quaternion value)
        {
            builder.Append(F(value.x));
            builder.Append(',');
            builder.Append(F(value.y));
            builder.Append(',');
            builder.Append(F(value.z));
            builder.Append(',');
            builder.Append(F(value.w));
        }

        private static Quaternion ConvertUnityRotationToVmdRotation(Quaternion unityRotation)
        {
            return new Quaternion(-unityRotation.x, unityRotation.y, -unityRotation.z, unityRotation.w);
        }

        private static Quaternion Normalize(Quaternion rotation)
        {
            float magnitude = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);
            if (magnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            float scale = 1f / magnitude;
            return new Quaternion(
                rotation.x * scale,
                rotation.y * scale,
                rotation.z * scale,
                rotation.w * scale);
        }

        private static void WriteImportVariantJson(StringBuilder builder, ImportVariantComparison comparison, bool last)
        {
            builder.AppendLine("    {");
            WriteJsonProperty(builder, 3, "variant_name", comparison.VariantName, comma: true);
            WriteJsonProperty(builder, 3, "asset_path", comparison.AssetPath, comma: true);
            WriteJsonProperty(builder, 3, "clip_name", comparison.ClipName, comma: true);
            WriteJsonProperty(builder, 3, "clip_length_seconds", comparison.ClipLengthSeconds, comma: true);
            builder.AppendLine("      \"comparison_to_primary\": {");
            WriteSummaryJson(builder, comparison.ComparisonToPrimary, 4);
            builder.AppendLine("      },");
            builder.AppendLine("      \"comparison_to_fallback\": {");
            WriteSummaryJson(builder, comparison.ComparisonToFallback, 4);
            builder.AppendLine("      }");
            builder.Append("    }");
            builder.AppendLine(last ? "" : ",");
        }

        private static void WriteSummaryJson(StringBuilder builder, ClipComparisonSummary summary, int indent)
        {
            WriteJsonProperty(builder, indent, "row_count", summary.RowCount, comma: true);
            WriteJsonProperty(builder, indent, "high_rotation_difference_count", summary.HighRotationDifferenceCount, comma: true);
            WriteJsonProperty(builder, indent, "max_rotation_difference_degrees", summary.MaxRotationDifferenceDegrees, comma: true);
            WriteJsonProperty(builder, indent, "max_rotation_bone", summary.MaxRotationBone, comma: true);
            WriteJsonProperty(builder, indent, "max_rotation_frame", summary.MaxRotationFrame, comma: false);
        }

        private static void WriteJsonProperty(StringBuilder builder, int indent, string name, string value, bool comma)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(EscapeJson(name)).Append("\": \"").Append(EscapeJson(value)).Append('"');
            builder.AppendLine(comma ? "," : "");
        }

        private static void WriteJsonProperty(StringBuilder builder, int indent, string name, int value, bool comma)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(EscapeJson(name)).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : "");
        }

        private static void WriteJsonProperty(StringBuilder builder, int indent, string name, float value, bool comma)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(EscapeJson(name)).Append("\": ").Append(F(value));
            builder.AppendLine(comma ? "," : "");
        }

        private static void WriteJsonProperty(StringBuilder builder, int indent, string name, bool value, bool comma)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(EscapeJson(name)).Append("\": ").Append(value ? "true" : "false");
            builder.AppendLine(comma ? "," : "");
        }

        private static void WriteJsonArray(StringBuilder builder, int indent, string name, IEnumerable<string> values, bool comma)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(EscapeJson(name)).Append("\": [");
            builder.Append(string.Join(", ", values));
            builder.Append(']');
            builder.AppendLine(comma ? "," : "");
        }

        private static string Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            }
        }

        private static string ReplaceGuid(string meta, string guid)
        {
            return Regex.Replace(
                meta,
                @"(?m)^guid:\s*[0-9a-fA-F]+",
                "guid: " + guid,
                RegexOptions.CultureInvariant);
        }

        private static string ReplaceScalar(string meta, string key, string value)
        {
            string pattern = @"(?m)^(\s*" + Regex.Escape(key) + @":\s*).*$";
            string replaced = Regex.Replace(
                meta,
                pattern,
                "${1}" + value,
                RegexOptions.CultureInvariant);
            if (string.Equals(replaced, meta, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Scalar key not found in meta: {key}");
            }

            return replaced;
        }

        private static string ExtractRequiredScalar(string meta, string key)
        {
            Match match = Regex.Match(
                meta,
                @"(?m)^\s*" + Regex.Escape(key) + @":\s*(.*?)\s*$",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                throw new InvalidOperationException($"Scalar key not found in source meta: {key}");
            }

            return match.Groups[1].Value;
        }

        private static string ReplaceYamlBlock(string targetMeta, string sourceMeta, string key)
        {
            List<string> targetLines = SplitLines(targetMeta);
            List<string> sourceLines = SplitLines(sourceMeta);
            BlockRange targetRange = FindYamlBlock(targetLines, key);
            BlockRange sourceRange = FindYamlBlock(sourceLines, key);

            var result = new List<string>();
            result.AddRange(targetLines.Take(targetRange.Start));
            result.AddRange(sourceLines.Skip(sourceRange.Start).Take(sourceRange.End - sourceRange.Start));
            result.AddRange(targetLines.Skip(targetRange.End));
            return string.Join("\n", result) + "\n";
        }

        private static BlockRange FindYamlBlock(IReadOnlyList<string> lines, string key)
        {
            for (int index = 0; index < lines.Count; index++)
            {
                string line = lines[index];
                if (!IsYamlKeyLine(line, key))
                {
                    continue;
                }

                int startIndent = CountIndent(line);
                int end = index + 1;
                while (end < lines.Count)
                {
                    string candidate = lines[end];
                    string trimmedCandidate = candidate.TrimStart();
                    if (
                        !string.IsNullOrWhiteSpace(candidate)
                        && CountIndent(candidate) <= startIndent
                        && !trimmedCandidate.StartsWith("-", StringComparison.Ordinal))
                    {
                        break;
                    }

                    end++;
                }

                return new BlockRange(index, end);
            }

            throw new InvalidOperationException($"YAML block not found: {key}");
        }

        private static bool IsYamlKeyLine(string line, string key)
        {
            string trimmed = line.TrimStart();
            return trimmed.StartsWith(key + ":", StringComparison.Ordinal);
        }

        private static int CountIndent(string line)
        {
            int count = 0;
            while (count < line.Length && line[count] == ' ')
            {
                count++;
            }

            return count;
        }

        private static List<string> SplitLines(string text)
        {
            return text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .TrimEnd('\n')
                .Split('\n')
                .ToList();
        }

        private static string GetArgumentValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private static string MakeProjectRelativePath(string projectRoot, string path)
        {
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            string fullRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            if (fullPath.StartsWith(fullRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(fullRoot.Length + 1);
            }

            return fullPath;
        }

        private static string SanitizeName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string F(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string V(Vector3 value)
        {
            return $"{F(value.x)} {F(value.y)} {F(value.z)}";
        }

        private readonly struct FocusBone
        {
            public FocusBone(string name, HumanBodyBones humanBone, string runtimeSkeletonBoneName, string vmdBoneName)
            {
                Name = name;
                HumanBone = humanBone;
                RuntimeSkeletonBoneName = runtimeSkeletonBoneName;
                VmdBoneName = vmdBoneName;
            }

            public string Name { get; }
            public HumanBodyBones HumanBone { get; }
            public string RuntimeSkeletonBoneName { get; }
            public string VmdBoneName { get; }
        }

        private readonly struct PoseKey : IEquatable<PoseKey>
        {
            public PoseKey(string bone, int frame)
            {
                Bone = bone;
                Frame = frame;
            }

            private string Bone { get; }
            private int Frame { get; }

            public bool Equals(PoseKey other)
            {
                return Frame == other.Frame && string.Equals(Bone, other.Bone, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is PoseKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Bone != null ? Bone.GetHashCode() : 0) * 397) ^ Frame;
                }
            }
        }

        private sealed class SampledPose
        {
            public string Bone;
            public int Frame;
            public float TimeSeconds;
            public Quaternion LocalRotation;
            public Vector3 LocalEuler;
        }

        public sealed class PoseComparisonRow
        {
            public int Frame;
            public float TimeSeconds;
            public string Bone;
            public float RotationDifferenceDegrees;
            public Vector3 PrimaryLocalEuler;
            public Vector3 FallbackLocalEuler;
        }

        public sealed class ImportVariantComparison
        {
            public string VariantName;
            public string AssetPath;
            public string ClipName;
            public float ClipLengthSeconds;
            public ClipComparisonSummary ComparisonToPrimary;
            public ClipComparisonSummary ComparisonToFallback;
        }

        public sealed class ImportVariantCorrelationSummary
        {
            public int VariantCount;
            public int PrimaryLikeCount;
            public int FallbackLikeCount;
            public int MixedOrNeutralCount;
        }

        public sealed class RuntimeImporterComparison
        {
            public string AssetPath;
            public string RuntimeClipName;
            public int RowCount;
            public ClipComparisonSummary Summary;
            public List<PoseComparisonRow> Rows;
            public List<RuntimeImporterVmdSampleRow> VmdSampleRows;
            public string RawAssimpAnimationName;
            public int RawAssimpRowCount;
            public ClipComparisonSummary RawAssimpSummary;
            public List<PoseComparisonRow> RawAssimpRows;
            public List<RuntimeImporterVmdSampleRow> RawAssimpVmdSampleRows;
            public RawAssimpImportVariantSummary RawAssimpImportVariantSummary;
            public List<RawAssimpImportVariantComparison> RawAssimpImportVariants;
        }

        public sealed class RawAssimpImportVariantComparison
        {
            public string VariantName;
            public bool PreservePivots;
            public string PostProcessLabel;
            public string AnimationName;
            public ClipComparisonSummary ComparisonToDefault;
            public List<PoseComparisonRow> Rows;
            public List<RuntimeImporterVmdSampleRow> VmdSampleRows;
        }

        public sealed class RawAssimpImportVariantSummary
        {
            public int VariantCount;
            public int DefaultLikeCount;
            public int ChangedCount;
            public string MaxChangedVariantName;
            public float MaxChangedRotationDegrees;
        }

        private sealed class RawAssimpImportVariantSpec
        {
            public RawAssimpImportVariantSpec(
                string variantName,
                bool preservePivots,
                string postProcessLabel,
                Assimp.PostProcessSteps postProcessSteps)
            {
                VariantName = variantName;
                PreservePivots = preservePivots;
                PostProcessLabel = postProcessLabel;
                PostProcessSteps = postProcessSteps;
            }

            public string VariantName { get; }
            public bool PreservePivots { get; }
            public string PostProcessLabel { get; }
            public Assimp.PostProcessSteps PostProcessSteps { get; }
        }

        public sealed class RuntimeImporterVmdSampleRow
        {
            public int Frame;
            public float TimeSeconds;
            public string HumanBone;
            public string VmdBoneName;
            public int BoneIndex;
            public string SourceMode;
            public string ExportSourceMode;
            public Quaternion LocalRotation;
            public Quaternion ExportVmdRotation;
        }

        public sealed class RawAssimpRotationKey
        {
            public RawAssimpRotationKey(float timeSeconds, Quaternion rotation)
            {
                TimeSeconds = timeSeconds;
                Rotation = rotation;
            }

            public float TimeSeconds { get; }
            public Quaternion Rotation { get; }
        }

        public sealed class FocusBoneSummary
        {
            public string Bone;
            public int RowCount;
            public float MaxRotationDifferenceDegrees;
            public int MaxRotationFrame;
            public int HighRotationDifferenceCount;
        }

        public sealed class ClipComparisonSummary
        {
            public int RowCount;
            public int SampleFrameCount;
            public int BoneCount;
            public float HighRotationDifferenceThresholdDegrees;
            public int HighRotationDifferenceCount;
            public float MaxRotationDifferenceDegrees;
            public string MaxRotationBone;
            public int MaxRotationFrame;
            public Dictionary<string, FocusBoneSummary> FocusBones;
            public List<PoseComparisonRow> TopRows;
        }

        private readonly struct BlockRange
        {
            public BlockRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            public int Start { get; }
            public int End { get; }
        }
    }
}
#endif
