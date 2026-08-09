
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static class FbxRegressionInventoryExporter
    {
        private const string DefaultFbxAssetDirectory = "Assets/_Project/FBX";
        private const string DefaultMappingAssetPath = "Assets/Resources/BoneMapping_Data.txt";

        private sealed class InventoryRow
        {
            public string GateStatus;
            public string FailureClass;
            public string FailureReason;
            public string AssetPath;
            public string FileName;
            public long FileSizeBytes;
            public string SkeletonNamingFamily;
            public int TransformCount;
            public int NamespaceNameCount;
            public int ClipCount;
            public int HumanMotionClipCount;
            public string ClipNames;
            public string ClipLengthsSeconds;
            public float MaxClipLengthSeconds;
            public int DefaultClipAnimationCount;
            public int CustomClipAnimationCount;
            public string DefaultClipAnimationNames;
            public string DefaultClipAnimationFrameRanges;
            public bool RuntimeAnimationImportSucceeded;
            public int RuntimeAnimationCount;
            public int RuntimeNodeAnimationChannelCount;
            public int RuntimePositionKeyCount;
            public int RuntimeRotationKeyCount;
            public int RuntimeScaleKeyCount;
            public string RuntimeAnimationNames;
            public string RuntimeAnimationLengthsSeconds;
            public float RuntimeMaxAnimationLengthSeconds;
            public string RuntimeAnimationError;
            public string ImporterAnimationType;
            public string ImporterAvatarSetup;
            public bool ImporterOptimizeBones;
            public int MappingCount;
            public int MappingMatchedCount;
            public int ExactMatchCount;
            public int NormalizedMatchCount;
            public int AliasMatchCount;
            public int RequiredMatchedCount;
            public int RequiredTotal;
            public string MissingRequiredBones;
            public int FingerMatchedCount;
            public int FingerTotal;
            public string MissingFingerBones;
            public bool AvatarValid;
            public bool AvatarHuman;
            public string WarningReasons;
        }

        [MenuItem("Machine Spirit/Export FBX Regression Inventory")]
        public static void ExportDefaultInventory()
        {
            try
            {
                string inputAssetDirectory = GetCommandLineValue("-fbxInventoryInputDir", DefaultFbxAssetDirectory).Replace("\\", "/");
                string outputDirectory = GetCommandLineValue("-fbxInventoryOutputDir", BuildDefaultOutputDirectory()).Replace("\\", "/");

                Directory.CreateDirectory(outputDirectory);
                Directory.CreateDirectory(Path.Combine(outputDirectory, "analysis"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "csv"));
                Directory.CreateDirectory(Path.Combine(outputDirectory, "logs"));

                Dictionary<string, string> mapping = LoadBoneMapping(DefaultMappingAssetPath);
                List<InventoryRow> rows = ExportInventory(inputAssetDirectory, mapping);

                string sessionId = Path.GetFileName(outputDirectory.TrimEnd('/', '\\'));
                string csvPath = Path.Combine(outputDirectory, "csv", "inventory.csv");
                string indexPath = Path.Combine(outputDirectory, "index.md");
                string summaryPath = Path.Combine(outputDirectory, "logs", "inventory-summary.txt");
                string resultPath = Path.Combine(outputDirectory, "analysis", "result.json");

                WriteCsv(csvPath, rows);
                WriteIndexV2(indexPath, inputAssetDirectory, mapping.Count, rows, csvPath);
                WriteSummary(summaryPath, rows);
                WriteResultJson(resultPath, inputAssetDirectory, rows);

                Debug.Log($"[FbxRegressionInventory] {rows.Count}행 내보내기 완료: {csvPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FbxRegressionInventory] 내보내기 실패: {ex.Message}\n{ex.StackTrace}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }

        private static List<InventoryRow> ExportInventory(string inputAssetDirectory, Dictionary<string, string> mapping)
        {
            string absoluteInputDirectory = ToAbsoluteProjectPath(inputAssetDirectory);
            if (!Directory.Exists(absoluteInputDirectory))
            {
                throw new DirectoryNotFoundException($"FBX directory not found: {inputAssetDirectory}");
            }

            string[] fbxFiles = Directory.GetFiles(absoluteInputDirectory, "*.fbx", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var rows = new List<InventoryRow>();
            foreach (string filePath in fbxFiles)
            {
                string assetPath = ToAssetPath(filePath);
                rows.Add(AnalyzeFbx(assetPath, filePath, mapping));
            }

            return rows;
        }

        private static InventoryRow AnalyzeFbx(string assetPath, string filePath, Dictionary<string, string> mapping)
        {
            var row = new InventoryRow
            {
                GateStatus = "fail",
                FailureClass = "InputUnsupported",
                FailureReason = "",
                AssetPath = assetPath,
                FileName = Path.GetFileName(filePath),
                FileSizeBytes = new FileInfo(filePath).Length,
                ImporterAnimationType = "",
                ImporterAvatarSetup = "",
                ClipNames = "",
                ClipLengthsSeconds = "",
                DefaultClipAnimationNames = "",
                DefaultClipAnimationFrameRanges = "",
                RuntimeAnimationNames = "",
                RuntimeAnimationLengthsSeconds = "",
                RuntimeAnimationError = "",
                MissingRequiredBones = "",
                MissingFingerBones = "",
                WarningReasons = ""
            };

            GameObject instance = null;
            try
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    row.ImporterAnimationType = importer.animationType.ToString();
                    row.ImporterAvatarSetup = importer.avatarSetup.ToString();
                    row.ImporterOptimizeBones = importer.optimizeBones;

                    ModelImporterClipAnimation[] defaultClips = importer.defaultClipAnimations ?? Array.Empty<ModelImporterClipAnimation>();
                    ModelImporterClipAnimation[] customClips = importer.clipAnimations ?? Array.Empty<ModelImporterClipAnimation>();
                    row.DefaultClipAnimationCount = defaultClips.Length;
                    row.CustomClipAnimationCount = customClips.Length;
                    row.DefaultClipAnimationNames = string.Join("|", defaultClips.Select(clip => clip.name));
                    row.DefaultClipAnimationFrameRanges = string.Join(
                        "|",
                        defaultClips.Select(clip =>
                            $"{clip.name}:{clip.firstFrame.ToString("0.###", CultureInfo.InvariantCulture)}-{clip.lastFrame.ToString("0.###", CultureInfo.InvariantCulture)}"));
                }

                AssimpFBXImporter.AnimationInspectionReport runtimeReport = AssimpFBXImporter.InspectAnimationFile(filePath);
                row.RuntimeAnimationImportSucceeded = runtimeReport.HasImportSucceeded;
                row.RuntimeAnimationCount = runtimeReport.AnimationCount;
                row.RuntimeNodeAnimationChannelCount = runtimeReport.NodeAnimationChannelCount;
                row.RuntimePositionKeyCount = runtimeReport.PositionKeyCount;
                row.RuntimeRotationKeyCount = runtimeReport.RotationKeyCount;
                row.RuntimeScaleKeyCount = runtimeReport.ScaleKeyCount;
                row.RuntimeAnimationNames = runtimeReport.AnimationNames;
                row.RuntimeAnimationLengthsSeconds = runtimeReport.AnimationLengthsSeconds;
                row.RuntimeMaxAnimationLengthSeconds = runtimeReport.MaxAnimationLengthSeconds;
                row.RuntimeAnimationError = runtimeReport.ErrorMessage;

                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                AnimationClip[] clips = assets
                    .OfType<AnimationClip>()
                    .Where(clip => clip != null && !clip.name.StartsWith("__", StringComparison.Ordinal))
                    .OrderBy(clip => clip.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                row.ClipCount = clips.Length;
                row.HumanMotionClipCount = clips.Count(clip => clip.humanMotion);
                row.ClipNames = string.Join("|", clips.Select(clip => clip.name));
                row.ClipLengthsSeconds = string.Join("|", clips.Select(clip => clip.length.ToString("0.###", CultureInfo.InvariantCulture)));
                row.MaxClipLengthSeconds = clips.Length > 0 ? clips.Max(clip => clip.length) : 0f;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    row.FailureReason = "GameObject prefab not loaded from FBX asset.";
                    return row;
                }

                instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = $"{prefab.name}_InventoryProbe";
                instance.hideFlags = HideFlags.HideAndDontSave;

                Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
                row.TransformCount = transforms.Length;
                row.NamespaceNameCount = transforms.Count(t => t.name.Contains(":"));
                row.SkeletonNamingFamily = DetectSkeletonNamingFamily(transforms);

                HumanoidAvatarBuilder.BoneMappingDiagnostic diagnostic = HumanoidAvatarBuilder.AnalyzeMapping(instance, mapping);
                row.MappingCount = diagnostic.MappingCount;
                row.MappingMatchedCount = diagnostic.MatchedCount;
                row.ExactMatchCount = diagnostic.ExactMatchCount;
                row.NormalizedMatchCount = diagnostic.NormalizedMatchCount;
                row.AliasMatchCount = diagnostic.AliasMatchCount;
                row.RequiredMatchedCount = diagnostic.RequiredMatchedCount;
                row.RequiredTotal = diagnostic.RequiredTotal;
                row.MissingRequiredBones = string.Join("|", diagnostic.MissingRequiredBones);
                row.FingerMatchedCount = diagnostic.FingerMatchedCount;
                row.FingerTotal = diagnostic.FingerTotal;
                row.MissingFingerBones = string.Join("|", diagnostic.MissingFingerBones);

                if (diagnostic.RequiredMatchedCount < diagnostic.RequiredTotal)
                {
                    row.FailureClass = "SkeletonMappingFailure";
                    row.FailureReason = $"Missing required bones: {row.MissingRequiredBones}";
                    row.WarningReasons = BuildWarningReasons(row);
                    return row;
                }

                HumanoidAvatarBuilder.SetupHumanoid(instance, mapping);
                Animator animator = instance.GetComponent<Animator>();
                Avatar avatar = animator != null ? animator.avatar : null;
                row.AvatarValid = avatar != null && avatar.isValid;
                row.AvatarHuman = avatar != null && avatar.isHuman;

                if (!row.AvatarValid || !row.AvatarHuman)
                {
                    row.FailureClass = "AvatarBuildFailure";
                    row.FailureReason = $"Avatar invalid or non-human. isValid={row.AvatarValid}, isHuman={row.AvatarHuman}";
                    row.WarningReasons = BuildWarningReasons(row);
                    return row;
                }

                if (row.ClipCount == 0 && row.RuntimeAnimationCount == 0)
                {
                    row.FailureClass = "InputUnsupported";
                    row.FailureReason = "No animation clip imported from Unity and no runtime Assimp animation was found.";
                    row.WarningReasons = BuildWarningReasons(row);
                    return row;
                }

                row.WarningReasons = BuildWarningReasons(row);
                row.GateStatus = string.IsNullOrEmpty(row.WarningReasons) ? "pass" : "warn";
                row.FailureClass = "";
                row.FailureReason = "";
                return row;
            }
            catch (Exception ex)
            {
                row.GateStatus = "fail";
                row.FailureClass = "InputUnsupported";
                row.FailureReason = ex.Message.Replace('\r', ' ').Replace('\n', ' ');
                row.WarningReasons = BuildWarningReasons(row);
                return row;
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static string BuildWarningReasons(InventoryRow row)
        {
            var warnings = new List<string>();
            if (row.HumanMotionClipCount == 0 && row.ClipCount > 0)
            {
                warnings.Add("NoHumanMotionClip");
            }

            if (row.ClipCount == 0 && row.RuntimeAnimationCount > 0)
            {
                warnings.Add("EditorClipMissingRuntimeAssimpAvailable");
                warnings.Add("NoEditorHumanoidReferenceClip");
            }

            if (row.DefaultClipAnimationCount > 0 && row.ClipCount == 0)
            {
                warnings.Add($"DefaultClipNotExposed:{row.DefaultClipAnimationCount}");
            }

            if (!row.RuntimeAnimationImportSucceeded)
            {
                warnings.Add("RuntimeAssimpInspectFailed");
            }

            if (row.FingerMatchedCount < row.FingerTotal)
            {
                warnings.Add($"FingerMappingPartial:{row.FingerMatchedCount}/{row.FingerTotal}");
            }

            if (row.AliasMatchCount > 0)
            {
                warnings.Add($"AliasMappingUsed:{row.AliasMatchCount}");
            }

            if (row.NamespaceNameCount > 0)
            {
                warnings.Add($"NamespacedBones:{row.NamespaceNameCount}");
            }

            return string.Join("|", warnings);
        }

        private static string DetectSkeletonNamingFamily(IEnumerable<Transform> transforms)
        {
            string[] names = transforms.Select(t => t.name).ToArray();
            int mixamo = names.Count(name => name.StartsWith("mixamorig", StringComparison.OrdinalIgnoreCase) || name.Contains(":mixamorig", StringComparison.OrdinalIgnoreCase));
            int skeleton = names.Count(name => name.StartsWith("Skeleton_", StringComparison.OrdinalIgnoreCase));
            int yyb = names.Count(name => name.StartsWith("joint_", StringComparison.OrdinalIgnoreCase) || name.StartsWith("!joint_", StringComparison.OrdinalIgnoreCase));
            int namespaced = names.Count(name => name.Contains(":"));

            int max = Math.Max(Math.Max(mixamo, skeleton), Math.Max(yyb, namespaced));
            if (max == 0)
            {
                return "Generic";
            }

            if (max == mixamo)
            {
                return "Mixamo";
            }

            if (max == skeleton)
            {
                return "Skeleton";
            }

            if (max == yyb)
            {
                return "YYB";
            }

            return "Namespaced";
        }

        private static Dictionary<string, string> LoadBoneMapping(string assetPath)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
            {
                throw new FileNotFoundException($"Bone mapping asset not found: {assetPath}");
            }

            var mapping = new Dictionary<string, string>();
            string[] lines = asset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool insideBoneTemplate = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("m_BoneTemplate:", StringComparison.Ordinal))
                {
                    insideBoneTemplate = true;
                    continue;
                }

                if (!insideBoneTemplate)
                {
                    continue;
                }

                if (trimmedLine.StartsWith("m_", StringComparison.Ordinal))
                {
                    break;
                }

                int colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex <= 0)
                {
                    continue;
                }

                string key = trimmedLine[..colonIndex].Trim();
                string value = trimmedLine[(colonIndex + 1)..].Trim();
                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                {
                    mapping[key] = value;
                }
            }

            if (mapping.Count == 0)
            {
                throw new InvalidDataException($"Bone mapping asset has no m_BoneTemplate data: {assetPath}");
            }

            return mapping;
        }

        private static void WriteCsv(string path, List<InventoryRow> rows)
        {
            string[] headers =
            {
                "gateStatus",
                "failureClass",
                "failureReason",
                "assetPath",
                "fileName",
                "fileSizeBytes",
                "skeletonNamingFamily",
                "transformCount",
                "namespaceNameCount",
                "clipCount",
                "humanMotionClipCount",
                "clipNames",
                "clipLengthsSeconds",
                "maxClipLengthSeconds",
                "defaultClipAnimationCount",
                "customClipAnimationCount",
                "defaultClipAnimationNames",
                "defaultClipAnimationFrameRanges",
                "runtimeAnimationImportSucceeded",
                "runtimeAnimationCount",
                "runtimeNodeAnimationChannelCount",
                "runtimePositionKeyCount",
                "runtimeRotationKeyCount",
                "runtimeScaleKeyCount",
                "runtimeAnimationNames",
                "runtimeAnimationLengthsSeconds",
                "runtimeMaxAnimationLengthSeconds",
                "runtimeAnimationError",
                "importerAnimationType",
                "importerAvatarSetup",
                "importerOptimizeBones",
                "mappingCount",
                "mappingMatchedCount",
                "exactMatchCount",
                "normalizedMatchCount",
                "aliasMatchCount",
                "requiredMatchedCount",
                "requiredTotal",
                "missingRequiredBones",
                "fingerMatchedCount",
                "fingerTotal",
                "missingFingerBones",
                "avatarValid",
                "avatarHuman",
                "warningReasons"
            };

            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

            foreach (InventoryRow row in rows)
            {
                string[] values =
                {
                    row.GateStatus,
                    row.FailureClass,
                    row.FailureReason,
                    row.AssetPath,
                    row.FileName,
                    row.FileSizeBytes.ToString(CultureInfo.InvariantCulture),
                    row.SkeletonNamingFamily,
                    row.TransformCount.ToString(CultureInfo.InvariantCulture),
                    row.NamespaceNameCount.ToString(CultureInfo.InvariantCulture),
                    row.ClipCount.ToString(CultureInfo.InvariantCulture),
                    row.HumanMotionClipCount.ToString(CultureInfo.InvariantCulture),
                    row.ClipNames,
                    row.ClipLengthsSeconds,
                    row.MaxClipLengthSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                    row.DefaultClipAnimationCount.ToString(CultureInfo.InvariantCulture),
                    row.CustomClipAnimationCount.ToString(CultureInfo.InvariantCulture),
                    row.DefaultClipAnimationNames,
                    row.DefaultClipAnimationFrameRanges,
                    row.RuntimeAnimationImportSucceeded.ToString(CultureInfo.InvariantCulture),
                    row.RuntimeAnimationCount.ToString(CultureInfo.InvariantCulture),
                    row.RuntimeNodeAnimationChannelCount.ToString(CultureInfo.InvariantCulture),
                    row.RuntimePositionKeyCount.ToString(CultureInfo.InvariantCulture),
                    row.RuntimeRotationKeyCount.ToString(CultureInfo.InvariantCulture),
                    row.RuntimeScaleKeyCount.ToString(CultureInfo.InvariantCulture),
                    row.RuntimeAnimationNames,
                    row.RuntimeAnimationLengthsSeconds,
                    row.RuntimeMaxAnimationLengthSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                    row.RuntimeAnimationError,
                    row.ImporterAnimationType,
                    row.ImporterAvatarSetup,
                    row.ImporterOptimizeBones.ToString(CultureInfo.InvariantCulture),
                    row.MappingCount.ToString(CultureInfo.InvariantCulture),
                    row.MappingMatchedCount.ToString(CultureInfo.InvariantCulture),
                    row.ExactMatchCount.ToString(CultureInfo.InvariantCulture),
                    row.NormalizedMatchCount.ToString(CultureInfo.InvariantCulture),
                    row.AliasMatchCount.ToString(CultureInfo.InvariantCulture),
                    row.RequiredMatchedCount.ToString(CultureInfo.InvariantCulture),
                    row.RequiredTotal.ToString(CultureInfo.InvariantCulture),
                    row.MissingRequiredBones,
                    row.FingerMatchedCount.ToString(CultureInfo.InvariantCulture),
                    row.FingerTotal.ToString(CultureInfo.InvariantCulture),
                    row.MissingFingerBones,
                    row.AvatarValid.ToString(CultureInfo.InvariantCulture),
                    row.AvatarHuman.ToString(CultureInfo.InvariantCulture),
                    row.WarningReasons
                };
                builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteIndex(string path, string inputAssetDirectory, int mappingCount, List<InventoryRow> rows, string csvPath)
        {
            int passCount = rows.Count(row => row.GateStatus == "pass");
            int warnCount = rows.Count(row => row.GateStatus == "warn");
            int failCount = rows.Count(row => row.GateStatus == "fail");

            var builder = new StringBuilder();
            builder.AppendLine("# FBX 인벤토리와 mapping 진단 세션");
            builder.AppendLine();
            builder.AppendLine($"생성일: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();
            builder.AppendLine($"입력 폴더: `{inputAssetDirectory}`");
            builder.AppendLine();
            builder.AppendLine($"BoneMapping 항목 수: `{mappingCount}`");
            builder.AppendLine();
            builder.AppendLine($"CSV: `{NormalizePathForMarkdown(csvPath)}`");
            builder.AppendLine();
            builder.AppendLine("## 요약");
            builder.AppendLine();
            builder.AppendLine($"- 전체 FBX: {rows.Count}");
            builder.AppendLine($"- pass: {passCount}");
            builder.AppendLine($"- warn: {warnCount}");
            builder.AppendLine($"- fail: {failCount}");
            builder.AppendLine();
            builder.AppendLine("## 파일별 결과");
            builder.AppendLine();
            builder.AppendLine("| 파일 | 상태 | 실패 분류 | 필수 본 | 손가락 본 | Unity 클립 | Runtime 애니메이션 | Skeleton family | 경고 |");
            builder.AppendLine("|---|---|---|---:|---:|---:|---:|---|---|");

            foreach (InventoryRow row in rows)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdown(row.FileName)} | {row.GateStatus} | {EscapeMarkdown(row.FailureClass)} | {row.RequiredMatchedCount}/{row.RequiredTotal} | {row.FingerMatchedCount}/{row.FingerTotal} | {row.ClipCount} | {row.RuntimeAnimationCount} | {EscapeMarkdown(row.SkeletonNamingFamily)} | {EscapeMarkdown(row.WarningReasons)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## 판정 기준");
            builder.AppendLine();
            builder.AppendLine("- `pass`: 필수 본 mapping, Avatar 생성, clip 존재가 모두 통과했고 경고가 없다.");
            builder.AppendLine("- `warn`: 변환 기본 조건은 통과했지만 humanMotion clip 없음, alias 매핑 사용, 손가락 일부 누락 같은 후속 확인 항목이 있다.");
            builder.AppendLine("- `fail`: 입력 미지원, skeleton mapping 실패, Avatar 생성 실패 중 하나다.");
            builder.AppendLine();
            builder.AppendLine("## 다음 작업");
            builder.AppendLine();
            builder.AppendLine("1. fail 항목은 실패 분류별로 원인을 나눈다.");
            builder.AppendLine("2. warn 항목은 DiagnosticOnly 배치 샘플링 전에 위험 frame 우선순위를 정한다.");
            builder.AppendLine("3. 새 alias나 fallback이 필요하면 파일명 분기가 아니라 skeleton naming family와 본 이름 패턴으로 처리한다.");

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteIndexV2(string path, string inputAssetDirectory, int mappingCount, List<InventoryRow> rows, string csvPath)
        {
            int passCount = rows.Count(row => row.GateStatus == "pass");
            int warnCount = rows.Count(row => row.GateStatus == "warn");
            int failCount = rows.Count(row => row.GateStatus == "fail");

            var builder = new StringBuilder();
            builder.AppendLine("# FBX 인벤토리와 mapping 진단 세션");
            builder.AppendLine();
            builder.AppendLine($"생성일: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();
            builder.AppendLine($"입력 폴더: `{inputAssetDirectory}`");
            builder.AppendLine();
            builder.AppendLine($"BoneMapping 항목 수: `{mappingCount}`");
            builder.AppendLine();
            builder.AppendLine($"CSV: `{NormalizePathForMarkdown(csvPath)}`");
            builder.AppendLine();
            builder.AppendLine("## 요약");
            builder.AppendLine();
            builder.AppendLine($"- 전체 FBX: {rows.Count}");
            builder.AppendLine($"- pass: {passCount}");
            builder.AppendLine($"- warn: {warnCount}");
            builder.AppendLine($"- fail: {failCount}");
            builder.AppendLine();
            builder.AppendLine("## 파일별 결과");
            builder.AppendLine();
            builder.AppendLine("| 파일 | 상태 | 실패 분류 | 필수 본 | 손가락 본 | Unity 클립 | Runtime 애니메이션 | Skeleton family | 경고 |");
            builder.AppendLine("|---|---|---|---:|---:|---:|---:|---|---|");

            foreach (InventoryRow row in rows)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdown(row.FileName)} | {row.GateStatus} | {EscapeMarkdown(row.FailureClass)} | {row.RequiredMatchedCount}/{row.RequiredTotal} | {row.FingerMatchedCount}/{row.FingerTotal} | {row.ClipCount} | {row.RuntimeAnimationCount} | {EscapeMarkdown(row.SkeletonNamingFamily)} | {EscapeMarkdown(row.WarningReasons)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## 판정 기준");
            builder.AppendLine();
            builder.AppendLine("- `pass`: 필수 본 mapping, Avatar 생성, Unity clip 또는 runtime Assimp animation 존재가 모두 통과했고 경고가 없다.");
            builder.AppendLine("- `warn`: 기본 변환 조건은 통과했지만 Editor Humanoid 기준 clip 누락, alias mapping, namespace bone처럼 추가 확인이 필요한 항목이다.");
            builder.AppendLine("- `fail`: 입력 미지원, skeleton mapping 실패, Avatar 생성 실패, Unity/Assimp 양쪽 animation 누락 중 하나다.");
            builder.AppendLine();
            builder.AppendLine("## 다음 작업");
            builder.AppendLine();
            builder.AppendLine("1. `warn` 항목은 DiagnosticOnly 배치 샘플링에서 우선순위를 높인다.");
            builder.AppendLine("2. `EditorClipMissingRuntimeAssimpAvailable`은 변환 자체보다 Editor Humanoid 기준 보정 경로가 빠지는 리스크로 본다.");
            builder.AppendLine("3. `AliasMappingUsed`는 파일명이 아니라 skeleton naming family와 bone name 패턴 기준으로 관리한다.");

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteSummary(string path, List<InventoryRow> rows)
        {
            var builder = new StringBuilder();
            foreach (IGrouping<string, InventoryRow> group in rows.GroupBy(row => string.IsNullOrEmpty(row.FailureClass) ? row.GateStatus : row.FailureClass))
            {
                builder.AppendLine($"{group.Key}: {group.Count()}");
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static void WriteResultJson(string path, string inputAssetDirectory, List<InventoryRow> rows)
        {
            List<InventoryRow> warnRows = rows.Where(row => row.GateStatus == "warn").ToList();
            List<InventoryRow> failRows = rows.Where(row => row.GateStatus == "fail").ToList();

            var builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine($"  \"generatedAt\": \"{EscapeJson(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))}\",");
            builder.AppendLine($"  \"inputAssetDirectory\": \"{EscapeJson(inputAssetDirectory)}\",");
            builder.AppendLine($"  \"passed\": {(failRows.Count == 0 ? "true" : "false")},");
            builder.AppendLine($"  \"totalRows\": {rows.Count.ToString(CultureInfo.InvariantCulture)},");
            builder.AppendLine($"  \"passRows\": {rows.Count(row => row.GateStatus == "pass").ToString(CultureInfo.InvariantCulture)},");
            builder.AppendLine($"  \"warnRows\": {warnRows.Count.ToString(CultureInfo.InvariantCulture)},");
            builder.AppendLine($"  \"failRows\": {failRows.Count.ToString(CultureInfo.InvariantCulture)},");
            builder.AppendLine($"  \"warnFiles\": {BuildJsonStringArray(warnRows.Select(row => $"{row.FileName}: {row.WarningReasons}"))},");
            builder.AppendLine($"  \"failFiles\": {BuildJsonStringArray(failRows.Select(row => $"{row.FileName}: {row.FailureClass} {row.FailureReason}"))}");
            builder.AppendLine("}");

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static string EscapeCsv(string value)
        {
            value ??= "";
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private static string EscapeMarkdown(string value)
        {
            return (value ?? "").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
        }

        private static string BuildJsonStringArray(IEnumerable<string> values)
        {
            return "[" + string.Join(", ", values.Select(value => $"\"{EscapeJson(value)}\"")) + "]";
        }

        private static string EscapeJson(string value)
        {
            return (value ?? "")
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string NormalizePathForMarkdown(string path)
        {
            return path.Replace("\\", "/");
        }

        private static string BuildDefaultOutputDirectory()
        {
            string sessionId = $"when-{DateTime.Now:yyyyMMdd-HHmmss}_where-Main_Auto_who-yyb_what-fbx-inventory_why-generalization_how-editor-batch";
            return Path.Combine("Docs", "Workflow", "Local", "ComparisonSessions", sessionId);
        }

        private static string GetCommandLineValue(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return fallback;
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            string normalized = assetPath.Replace("\\", "/");
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && normalized != "Assets")
            {
                return Path.GetFullPath(normalized);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, normalized.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/");
            string normalized = absolutePath.Replace("\\", "/");
            if (!normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Path is outside project: {absolutePath}");
            }

            return normalized[projectRoot.Length..].TrimStart('/');
        }
    }
}
#endif

