using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;
using Fbx2Vmd.FileSystem;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBXVmdPipeline의 FBX 파일 선택 및 임포트 진입점을 담당하는 컴패니언 컨트롤러입니다.
    /// 파일 다이얼로그, Import_FBX 폴더 로드, SharedSettings 연동을 캡슐화합니다.
    /// </summary>
    public class FBXImportController
    {
        internal const string BONE_MAPPING_FILE = "BoneMapping_Data.txt";

        private readonly FBXVmdPipeline _pipeline;
        private readonly IFileBrowserService _fileBrowserService;
        private readonly Func<string, Task<GameObject>> _importModelAsync;

        public FBXImportController(
            FBXVmdPipeline pipeline,
            IFileBrowserService fileBrowserService,
            Func<string, Task<GameObject>> importModelAsync)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _fileBrowserService = fileBrowserService ?? throw new ArgumentNullException(nameof(fileBrowserService));
            _importModelAsync = importModelAsync ?? throw new ArgumentNullException(nameof(importModelAsync));
        }

        internal string CopyToControlledImportFolder(string sourcePath)
        {
            string targetDir = GetControlledImportDirectory();
            Directory.CreateDirectory(targetDir);

            string safeFileName = SanitizeFileName(Path.GetFileName(sourcePath));
            string targetPath = Path.Combine(targetDir, safeFileName);

            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, targetPath, true);
            }

            return targetPath;
        }

        internal static string GetControlledImportDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "Resources", FBXVmdPipeline.IMPORT_FBX_FOLDER);
#else
            return Path.Combine(Application.persistentDataPath, FBXVmdPipeline.IMPORT_FBX_FOLDER);
#endif
        }

        internal static string SanitizeFileName(string fileName)
        {
            string safeName = string.IsNullOrWhiteSpace(fileName) ? "motion.fbx" : fileName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return safeName;
        }

        internal static bool TryValidateSourcePath(string sourcePath, out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                errorMessage = $"FBX 파일을 찾을 수 없습니다: {sourcePath}";
                return false;
            }

            if (!string.Equals(Path.GetExtension(sourcePath), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "FBX 파일만 선택할 수 있습니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        internal async Task<FBXModelImportResult> ImportRuntimeModelAsync(
            string sourcePath,
            bool shouldRecordVmdAfterImport)
        {
            if (!TryValidateSourcePath(sourcePath, out string sourceValidationError))
            {
                return FBXModelImportResult.Fail(sourceValidationError);
            }

            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.Selected,
                $"선택됨: {Path.GetFileName(sourcePath)}",
                0.05f);
            string targetPath = CopyToControlledImportFolder(sourcePath);
            string outputBaseName = Path.GetFileNameWithoutExtension(targetPath);
            if (shouldRecordVmdAfterImport)
            {
                Debug.Log($"[Recording] 자동 VMD 출력명 고정됨. VMD={outputBaseName}.vmd, 입력 FBX={Path.GetFileName(sourcePath)}");
            }
            else
            {
                Debug.Log($"[FBXImport] Unity 촬영 전용 모드 선택됨. 출력={outputBaseName}, VMD 자동 녹화=생략");
            }

            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.Copied,
                $"복제 완료: {Path.GetFileName(targetPath)}",
                0.15f);

#if UNITY_EDITOR
            ConfigureEditorImportSettingsIfNeeded(sourcePath, targetPath);
#endif

            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.LoadingFbx,
                "FBX 로드 중",
                0.25f);
            GameObject importedModel = await _importModelAsync(targetPath);
            if (importedModel == null)
            {
                return FBXModelImportResult.Fail("FBX 로드에 실패했습니다.");
            }

            return FBXModelImportResult.Succeed(importedModel, targetPath, outputBaseName);
        }

        internal static Dictionary<string, string> LoadBoneMappingRuntime()
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>();
            string loadName = Path.GetFileNameWithoutExtension(BONE_MAPPING_FILE);
            TextAsset mappingAsset = Resources.Load<TextAsset>(loadName);

            if (mappingAsset == null)
            {
                Debug.LogWarning($"[FBXImport] BoneMapping 로드 실패: Resources/{loadName}.txt (자동 본 매핑으로 폴백합니다.)");
                return mapping;
            }

            Debug.Log($"[FBXImport] BoneMapping 로드 성공 (Resources/{loadName})");
            string[] lines = mappingAsset.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool insideBoneTemplate = false;
            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("m_BoneTemplate:"))
                {
                    insideBoneTemplate = true;
                    continue;
                }

                if (!insideBoneTemplate)
                {
                    continue;
                }

                if (trimmedLine.StartsWith("m_"))
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

            return mapping;
        }

        internal static bool ValidateGhostAvatar(GameObject importedModel)
        {
            Animator ghostAnimator = importedModel.GetComponent<Animator>();
            if (ghostAnimator == null || ghostAnimator.avatar == null)
            {
                Debug.LogError("[FBXImport] Ghost Animator 또는 Avatar가 없습니다.");
                return false;
            }

            if (!ghostAnimator.avatar.isValid || !ghostAnimator.avatar.isHuman)
            {
                Debug.LogError($"[FBXImport] Ghost Avatar가 유효하지 않습니다. valid={ghostAnimator.avatar.isValid}, human={ghostAnimator.avatar.isHuman}");
                return false;
            }

            return true;
        }

        internal static bool TryPrepareRuntimeAvatar(
            GameObject importedModel,
            out Dictionary<string, string> boneMapping,
            out string errorMessage)
        {
            boneMapping = LoadBoneMappingRuntime();
            if (boneMapping == null)
            {
                boneMapping = new Dictionary<string, string>();
            }

            // BoneMapping_Data.txt는 특정 리그에 종속될 수 있으므로, 실패 시 자동 매핑으로 폴백함.
            HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);
            if (!ValidateGhostAvatar(importedModel))
            {
                Debug.LogWarning("[FBXImport] Ghost Humanoid Avatar 생성 실패함. 자동 본 매핑으로 재시도함.");
                boneMapping = HumanoidAvatarBuilder.BuildAutoMapping(importedModel);
                HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);

                if (!ValidateGhostAvatar(importedModel))
                {
                    errorMessage = "Ghost Humanoid Avatar 생성에 실패했습니다.";
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        internal static AnimationClip ExtractPrimaryClip(Animation ghostAnimation, bool shouldLogRuntimeAnimation)
        {
            if (ghostAnimation == null || ghostAnimation.clip == null)
            {
                return null;
            }

            AnimationClip targetClip = ghostAnimation.clip;
            if (targetClip.length <= 0f || float.IsNaN(targetClip.length) || float.IsInfinity(targetClip.length))
            {
                Debug.LogError($"[FBXImport] 애니메이션 길이가 올바르지 않습니다: {targetClip.length}");
                return null;
            }

            if (targetClip.length > 1000f)
            {
                Debug.LogWarning("[FBXImport] 애니메이션 길이가 비정상적으로 깁니다. Assimp timeScale을 확인하세요.");
            }

            if (shouldLogRuntimeAnimation)
            {
                Debug.Log($"[FBXImport] Clip: {targetClip.name}, Length: {targetClip.length:F3}s, FrameRate: {targetClip.frameRate}");
            }

            return targetClip;
        }

#if UNITY_EDITOR
        internal void ConfigureEditorImportSettingsIfNeeded(string sourcePath, string targetPath)
        {
            if (ShouldConfigureEditorImportSettings(sourcePath, targetPath, Application.dataPath))
            {
                ConfigureImportSettings(targetPath);
                return;
            }

            Debug.Log($"[FBXImport] 제어된 Import_FBX 가져오기 설정 유지됨. 경로={targetPath}");
        }

        internal static bool ShouldConfigureEditorImportSettings(string sourcePath, string targetPath, string dataPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return true;
            }

            if (!PathsEqual(sourcePath, targetPath))
            {
                return true;
            }

            string targetRelativePath = ToAssetRelativePath(targetPath, dataPath);
            return !IsControlledImportAssetPath(targetRelativePath);
        }

        internal static bool IsControlledImportAssetPath(string relativePath)
        {
            return !string.IsNullOrEmpty(relativePath)
                && relativePath.Replace("\\", "/").StartsWith($"Assets/Resources/{FBXVmdPipeline.IMPORT_FBX_FOLDER}/", StringComparison.OrdinalIgnoreCase);
        }

        internal static string ToAssetRelativePath(string filePath, string dataPath)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(dataPath))
            {
                return "";
            }

            string standardizedFilePath = filePath.Replace("\\", "/");
            string standardizedDataPath = dataPath.Replace("\\", "/");

            if (!standardizedFilePath.StartsWith(standardizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return "Assets" + standardizedFilePath[standardizedDataPath.Length..];
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void ConfigureImportSettings(string filePath)
        {
            string standardizedFilePath = filePath.Replace("\\", "/");
            string standardizedDataPath = Application.dataPath.Replace("\\", "/");

            if (!standardizedFilePath.StartsWith(standardizedDataPath))
            {
                Debug.LogError($"파일 경로가 Assets 폴더 내에 있지 않습니다: {filePath}");
                return;
            }

            string relativePath = "Assets" + standardizedFilePath[standardizedDataPath.Length..];

            Debug.Log($"[1단계] FBX Import 시작: {relativePath}");
            UnityEditor.AssetDatabase.ImportAsset(relativePath, UnityEditor.ImportAssetOptions.ForceUpdate);

            Debug.Log("[2단계] FBX 정보 가져오기");
            UnityEditor.ModelImporter importer = UnityEditor.AssetImporter.GetAtPath(relativePath) as UnityEditor.ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[2단계 실패] ModelImporter를 가져올 수 없습니다: {relativePath}");
                return;
            }

            Debug.Log("[2단계 완료] ModelImporter 정보:");
            Debug.Log($"  - 현재 Animation Type: {importer.animationType}");
            Debug.Log($"  - 현재 Import Animation: {importer.importAnimation}");
            Debug.Log($"  - 현재 Optimize Bones: {importer.optimizeBones}");

            Debug.Log("[3단계] Rig 설정 적용 중...");
            importer.importAnimation = true;
            importer.animationCompression = UnityEditor.ModelImporterAnimationCompression.Off;
            importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
            importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeBones = false;

            try
            {
                UnityEditor.SerializedObject serializedImporter = new UnityEditor.SerializedObject(importer);
                serializedImporter.Update();

                string[] propertyNames = { "m_OptimizeBones", "optimizeBones" };
                bool found = false;
                foreach (string propertyName in propertyNames)
                {
                    UnityEditor.SerializedProperty property = serializedImporter.FindProperty(propertyName);
                    if (property != null && property.propertyType == UnityEditor.SerializedPropertyType.Boolean)
                    {
                        property.boolValue = false;
                        found = true;
                        Debug.Log($"[Strip Bones Fix] SerializedObject를 통해 '{propertyName}' 비활성화 성공");
                    }
                }

                if (found)
                {
                    serializedImporter.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning("[Strip Bones Fix] 'optimizeBones' 관련 속성을 찾지 못했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Strip Bones Fix] 오류: {e.Message}");
            }

            importer.animationWrapMode = WrapMode.ClampForever;
            importer.importBlendShapes = true;
            importer.importVisibility = true;
            importer.importCameras = false;
            importer.importLights = false;

            Debug.Log("[3단계] Bone Mapping 적용 시작");
            string mappingFilePath = Path.Combine(Application.dataPath, "Resources", BONE_MAPPING_FILE);
            if (File.Exists(mappingFilePath))
            {
                Dictionary<string, string> mapping = ParseBoneMappingFile(mappingFilePath);
                if (mapping.Count > 0)
                {
                    Debug.Log($"[3단계] Bone Mapping 파일 파싱 완료: {mapping.Count}개 매핑");

                    HumanDescription description = importer.humanDescription;
                    List<HumanBone> humanBones = new List<HumanBone>();
                    foreach (KeyValuePair<string, string> pair in mapping)
                    {
                        HumanBone bone = new HumanBone
                        {
                            humanName = HumanoidAvatarBuilder.NormalizeHumanBoneName(pair.Key),
                            boneName = pair.Value
                        };
                        bone.limit.useDefaultValues = true;
                        humanBones.Add(bone);
                    }

                    description.human = humanBones.ToArray();

                    List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
                    IEnumerable<Transform> allTransforms = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativePath)
                        .OfType<Transform>();
                    foreach (Transform transform in allTransforms)
                    {
                        if (transform != null)
                        {
                            skeletonBones.Add(new SkeletonBone
                            {
                                name = transform.name,
                                position = transform.localPosition,
                                rotation = transform.localRotation,
                                scale = transform.localScale
                            });
                        }
                    }

                    if (skeletonBones.Count > 0)
                    {
                        description.skeleton = skeletonBones.ToArray();
                        Debug.Log($"[3단계] Skeleton 배열 설정: {skeletonBones.Count}개 본");
                    }

                    importer.humanDescription = description;
                    Debug.Log($"[3단계] Bone Mapping 적용: {humanBones.Count}개 본");
                }
                else
                {
                    Debug.LogWarning("[3단계] Bone Mapping 데이터가 비어있습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"[3단계] Bone Mapping 파일을 찾을 수 없습니다: {mappingFilePath}");
            }

            Debug.Log("[3단계] Animation Clip 추출 시작");
            if (importer.defaultClipAnimations != null && importer.defaultClipAnimations.Length > 0)
            {
                importer.clipAnimations = Array.Empty<UnityEditor.ModelImporterClipAnimation>();
                Debug.Log($"[3단계] Animation Clip 추출: {importer.defaultClipAnimations.Length}개");

                foreach (UnityEditor.ModelImporterClipAnimation clip in importer.defaultClipAnimations)
                {
                    Debug.Log($"  - Clip: {clip.name} (Start: {clip.firstFrame}, End: {clip.lastFrame})");
                }
            }
            else
            {
                Debug.LogWarning("[3단계] defaultClipAnimations가 비어있습니다. 자동 경로에서는 임의 Take 001을 만들지 않습니다.");
                importer.clipAnimations = Array.Empty<UnityEditor.ModelImporterClipAnimation>();
            }

            Debug.Log("[3단계 완료] 최종 설정:");
            Debug.Log("  - Animation Type: Humanoid");
            Debug.Log($"  - Import Animation: {importer.importAnimation}");
            Debug.Log($"  - Optimize Game Objects (Strip Bones): {importer.optimizeGameObjects}");
            Debug.Log("  - Bone Mapping: 적용 완료");
            Debug.Log($"  - Animation Clips: {(importer.clipAnimations != null ? importer.clipAnimations.Length : 0)}개");

            UnityEditor.AssetDatabase.WriteImportSettingsIfDirty(relativePath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.ImportAsset(relativePath, UnityEditor.ImportAssetOptions.ForceUpdate);

            Debug.Log("[3단계] 최종 Reimport 완료");
            Debug.Log("===========================================");
        }

        private static Dictionary<string, string> ParseBoneMappingFile(string path)
        {
            Dictionary<string, string> mapping = new Dictionary<string, string>();
            string[] lines = File.ReadAllLines(path);
            bool insideBoneTemplate = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("m_BoneTemplate:"))
                {
                    insideBoneTemplate = true;
                    continue;
                }

                if (insideBoneTemplate)
                {
                    if (trimmedLine.StartsWith("m_"))
                    {
                        break;
                    }

                    int colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string key = trimmedLine[..colonIndex].Trim();
                        string value = trimmedLine[(colonIndex + 1)..].Trim();
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            mapping[key] = value;
                        }
                    }
                }
            }

            return mapping;
        }
#endif

        public void ImportFromDialog()
        {
            TryImportFromDialog();
        }

        internal bool TryImportFromDialog()
        {
            _pipeline.EnsureServicesInitialized();

            if (_pipeline._isProcessing)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return true;
            }

            _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Idle, "FBX 파일 선택 대기", 0f);
            string[] paths = _fileBrowserService.OpenFilePanel(
                "Import FBX", "", FBXVmdPipeline.FBX_EXTENSION, false);
            if (paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Cancelled, "파일 선택이 취소됨.", 0f);
                return true;
            }

            string sourcePath = paths[0];
            Debug.Log($"[FBXImport] 파일 선택됨. 경로={sourcePath}");
            _pipeline.ProcessFBXAsync(sourcePath);
            return true;
        }

        public void LoadFromImportFolder()
        {
            TryLoadFromImportFolder();
        }

        internal bool TryLoadFromImportFolder()
        {
            if (_pipeline._isProcessing)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return true;
            }

            string targetDir = GetControlledImportDirectory();
            if (!Directory.Exists(targetDir))
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Failed, $"Import_FBX 폴더가 없습니다: {targetDir}", 0f);
                return true;
            }

            string[] fbxFiles = Directory.GetFiles(targetDir, "*.fbx", SearchOption.TopDirectoryOnly);
            if (fbxFiles.Length == 0)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Failed, "Import_FBX 폴더에 FBX 파일이 없습니다", 0f);
                return true;
            }

            string selectedFile = fbxFiles[0];
            _pipeline.ProcessFBXAsync(selectedFile);
            return true;
        }

        public bool TryImportFromSharedSettings(string sourcePath)
        {
            if (_pipeline._isProcessing)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            _pipeline.ProcessFBXAsync(sourcePath.Trim());
            return true;
        }
    }
}
