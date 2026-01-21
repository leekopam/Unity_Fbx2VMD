using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Member_Han.Modules.FileSystem;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// FBX 파일 선택 및 관리를 담당하는 매니저 클래스
    /// </summary>
    public class FileManager : MonoBehaviour
    {
        #region 상수
        private const string IMPORT_FBX_FOLDER = "Import_FBX";
        private const string FBX_EXTENSION = "fbx";
        private const string BONE_MAPPING_FILE = "BoneMapping_Data.txt";
        private const string BONE_MAPPING_FALLBACK = "BoneMapping_Data.ht";
        #endregion

        #region Public 필드
        [Header("FBX 임포트 설정")]
        [Tooltip("체크 시 선택한 FBX 파일을 Import_FBX 폴더에 복사하여 저장")]
        public bool saveToImportFolder = true;
        #endregion

        #region Private 필드
        private IFileBrowserService _fileBrowserService;
        private RuntimeFBXImporter _fbxImporter;
        #endregion

        #region Unity 생명주기
        private void Awake()
        {
            InitializeServices();
        }
        #endregion

        #region 초기화
        private void InitializeServices()
        {
            // 1. 파일 브라우저 서비스 초기화 (StandaloneFileBrowser 사용)
            _fileBrowserService = new RuntimeFileBrowserService();
            
            // 2. 런타임 FBX 임포터 초기화 (Assimp 사용)
            _fbxImporter = new RuntimeFBXImporter();
        }
        #endregion


        #region 이벤트 핸들러
        public void OnClickImportButton()
        {
            string[] paths = _fileBrowserService.OpenFilePanel("Import FBX", "", FBX_EXTENSION, false);
            if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                string sourcePath = paths[0];
                Debug.Log($"선택된 파일: {sourcePath}");
                
                // 비동기 실행 (Fire-and-forget)
                ProcessFBXAsync(sourcePath);
            }
        }

        /// <summary>
        /// Import_FBX 폴더에 있는 FBX 파일 목록 로드
        /// 에디터와 빌드 환경 모두에서 작동
        /// </summary>
        public void OnClickLoadFromImportFolder()
        {
            string targetDir = Path.Combine(Application.dataPath, "Resources", IMPORT_FBX_FOLDER);
            
            if (!Directory.Exists(targetDir))
            {
                Debug.LogWarning($"Import_FBX 폴더가 존재하지 않습니다: {targetDir}");
                return;
            }

            string[] fbxFiles = Directory.GetFiles(targetDir, "*.fbx", SearchOption.TopDirectoryOnly);
            
            if (fbxFiles.Length == 0)
            {
                Debug.LogWarning("Import_FBX 폴더에 FBX 파일이 없습니다.");
                return;
            }

            // 첫 번째 FBX 파일 사용
            string selectedFile = fbxFiles[0];
            Debug.Log($"Import_FBX 폴더에서 로드: {Path.GetFileName(selectedFile)}");
            
            // 비동기 실행
            ProcessFBXAsync(selectedFile);
        }
        #endregion

        #region 파일 처리 로직
        private async void ProcessFBXAsync(string sourcePath)
        {
            try 
            {
                string fileName = Path.GetFileName(sourcePath);
                string targetPath = sourcePath; // 기본값: 원본 경로 사용
                
                // 1. 파일 복사 (saveToImportFolder 옵션이 켜져있을 경우)
                if (saveToImportFolder)
                {
                    string targetDir = Path.Combine(Application.dataPath, "Resources", IMPORT_FBX_FOLDER);
                    
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                        Debug.Log($"폴더 생성: {targetDir}");
                    }
                    
                    // 같은 파일이 아닐 경우에만 복사
                    string potentialTargetPath = Path.Combine(targetDir, fileName);
                    bool isNewFile = Path.GetFullPath(sourcePath) != Path.GetFullPath(potentialTargetPath);
                    
                    if (isNewFile)
                    {
                        File.Copy(sourcePath, potentialTargetPath, true);
                        targetPath = potentialTargetPath;
                        Debug.Log($"파일 복사 완료: {targetPath}");
                    }
                    else
                    {
                        targetPath = potentialTargetPath;
                    }

#if UNITY_EDITOR
                    // 에디터 환경: 파일 복사 후 Import Settings를 먼저 적용
                    // AssetDatabase.Refresh()를 호출하지 않고, ConfigureImportSettings에서 직접 Import 처리
                    if (isNewFile || File.Exists(targetPath))
                    {
                        ConfigureImportSettings(targetPath);
                    }
#endif
                }
                else
                {
                    Debug.Log($"Import_FBX 폴더 저장 스킵 (원본 경로 사용): {sourcePath}");
                }
                
                // 2. Assimp-net을 사용하여 FBX 로드 (에디터/빌드 공통)
                Debug.Log($"[FileManager] FBX 로드 시작: {targetPath}");
                GameObject importedModel = await _fbxImporter.ImportAsync(targetPath);
                
                if (importedModel != null)
                {
                    Debug.Log($"[FileManager] FBX 로드 성공: {importedModel.name}");
                    
                    // Hierarchy에 생성된 GameObject를 비활성화 (보이지 않게 처리)
                    importedModel.SetActive(false);
                    
                    // 3. Humanoid Avatar 및 BoneMapping 적용
                    HumanoidAvatarBuilder.SetupHumanoid(importedModel);
                    
                    // 4. AnimationClip 가져오기 및 검증
                    AnimationClip[] clips = _fbxImporter.GetAnimationClips();
                    if (clips != null && clips.Length > 0)
                    {
                        Debug.Log($"[FileManager] AnimationClip {clips.Length}개 로드 완료:");
                        foreach (var clip in clips)
                        {
                            Debug.Log($"  - {clip.name} (길이: {clip.length}초, 프레임: {clip.frameRate}fps)");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[FileManager] AnimationClip이 없거나 생성되지 않았습니다.");
                    }
                    
                    // 5. (선택사항) 씬에 배치된 모델의 위치 초기화 등
                    importedModel.transform.position = Vector3.zero;
                }
                else
                {
                    Debug.LogError("[FileManager] FBX 로드 실패");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"파일 처리 실패: {e.Message}\n{e.StackTrace}");
            }
        }
        #endregion

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 전용: 단계별 Import 설정 적용
        /// 1단계: FBX Import (기본 설정)
        /// 2단계: FBX 정보 가져오기
        /// 3단계: Rig 설정 (Humanoid, Strip Bones = False)
        /// 4단계: Bone Mapping 적용
        /// </summary>
        private void ConfigureImportSettings(string filePath)
        {
            // 절대 경로를 "Assets/..." 상대 경로로 변환
            string standardizedFilePath = filePath.Replace("\\", "/");
            string standardizedDataPath = Application.dataPath.Replace("\\", "/");
            
            if (!standardizedFilePath.StartsWith(standardizedDataPath))
            {
                Debug.LogError($"파일 경로가 Assets 폴더 내에 있지 않습니다: {filePath}");
                return;
            }

            string relativePath = "Assets" + standardizedFilePath.Substring(standardizedDataPath.Length);
            
            // ===== 1단계: FBX 파일 Import (기본 설정으로) =====
            Debug.Log($"[1단계] FBX Import 시작: {relativePath}");
            UnityEditor.AssetDatabase.ImportAsset(relativePath, UnityEditor.ImportAssetOptions.ForceUpdate);
            
            // ===== 2단계: FBX 정보 가져오기 =====
            Debug.Log($"[2단계] FBX 정보 가져오기");
            UnityEditor.ModelImporter importer = UnityEditor.AssetImporter.GetAtPath(relativePath) as UnityEditor.ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[2단계 실패] ModelImporter를 가져올 수 없습니다: {relativePath}");
                return;
            }
            
            Debug.Log($"[2단계 완료] ModelImporter 정보:");
            Debug.Log($"  - 현재 Animation Type: {importer.animationType}");
            Debug.Log($"  - 현재 Import Animation: {importer.importAnimation}");
            Debug.Log($"  - 현재 Optimize Game Objects: {importer.optimizeGameObjects}");
            
            // ===== 3단계: Rig 설정 (Humanoid, Strip Bones = False) =====
            Debug.Log($"[3단계] Rig 설정 적용 중...");
            
            // 3-1. Animation Import 활성화 (반드시 true)
            importer.importAnimation = true;
            importer.animationCompression = UnityEditor.ModelImporterAnimationCompression.Off;
            
            // 3-2. Animation Type = Humanoid
            importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
            
            // 3-3. Avatar Definition = "Create From This Model" (중요!)
            // Avatar를 모델에서 생성하도록 명시적 설정 (AnimationClip 보존을 위해 필수!)
            importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
            Debug.Log($"[3단계] Avatar Definition 설정: Create From This Model");
            
            // 3-4. Strip Bones = False (Optimize Game Objects = False)
            importer.optimizeGameObjects = false;
            
            // AnimationCompression 추가 설정
            importer.animationWrapMode = WrapMode.Loop; // 기본 Loop 설정
            
            //3-5. 기타 설정
            importer.importBlendShapes = true;
            importer.importVisibility = true;
            importer.importCameras = false;
            importer.importLights = false;
            
            // ===== 3-6. Bone Mapping 적용 (먼저!) =====
            Debug.Log($"[3단계] Bone Mapping 적용 시작");
            
            string mappingFilePath = Path.Combine(Application.dataPath, "Resources", BONE_MAPPING_FILE);
            if (File.Exists(mappingFilePath))
            {
                var mappingDict = ParseBoneMappingFile(mappingFilePath);
                if (mappingDict != null && mappingDict.Count > 0)
                {
                    Debug.Log($"[3단계] Bone Mapping 파일 파싱 완료: {mappingDict.Count}개 매핑");
                    
                    HumanDescription description = importer.humanDescription;
                    
                    // Human Bones 설정
                    List<HumanBone> humanBones = new List<HumanBone>();
                    foreach (var kvp in mappingDict)
                    {
                        HumanBone bone = new HumanBone();
                        bone.humanName = kvp.Key;
                        bone.boneName = kvp.Value;
                        bone.limit.useDefaultValues = true;
                        humanBones.Add(bone);
                    }
                    description.human = humanBones.ToArray();
                    
                    // 중요! Skeleton 배열도 설정해야 함!
                    // FBX의 모든 Transform을 skeleton으로 등록
                    List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
                    var allTransformPaths = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativePath)
                        .OfType<UnityEngine.Transform>();
                    
                    foreach (var transform in allTransformPaths)
                    {
                        if (transform != null)
                        {
                            SkeletonBone skelBone = new SkeletonBone();
                            skelBone.name = transform.name;
                            skelBone.position = transform.localPosition;
                            skelBone.rotation = transform.localRotation;
                            skelBone.scale = transform.localScale;
                            skeletonBones.Add(skelBone);
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
                    Debug.LogWarning($"[3단계] Bone Mapping 데이터가 비어있습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"[3단계] Bone Mapping 파일을 찾을 수 없습니다: {mappingFilePath}");
            }
            
            // ===== 3-7. Animation Clip 추출 설정 (BoneMapping 다음에!) =====
            // Bone Mapping 적용 후에 clipAnimations 설정
            Debug.Log($"[3단계] Animation Clip 추출 시작");
            
            if (importer.defaultClipAnimations != null && importer.defaultClipAnimations.Length > 0)
            {
                // 기본 클립을 clipAnimations에 명시적으로 할당
                importer.clipAnimations = importer.defaultClipAnimations;
                Debug.Log($"[3단계] Animation Clip 추출: {importer.defaultClipAnimations.Length}개");
                
                foreach (var clip in importer.defaultClipAnimations)
                {
                    Debug.Log($"  - Clip: {clip.name} (Start: {clip.firstFrame}, End: {clip.lastFrame})");
                }
            }
            else
            {
                // 기본 클립이 없으면 Take 001로 생성 시도
                Debug.LogWarning($"[3단계] defaultClipAnimations가 비어있음. 수동으로 클립 생성 시도...");
                
                UnityEditor.ModelImporterClipAnimation[] clipAnimations = new UnityEditor.ModelImporterClipAnimation[1];
                clipAnimations[0] = new UnityEditor.ModelImporterClipAnimation();
                clipAnimations[0].name = "Take 001";
                clipAnimations[0].takeName = "Take 001";
                clipAnimations[0].firstFrame = 0;
                clipAnimations[0].lastFrame = 100;
                
                importer.clipAnimations = clipAnimations;
                Debug.Log($"[3단계] 수동 Animation Clip 생성: Take 001");
            }
            
            Debug.Log($"[3단계 완료] 최종 설정:");
            Debug.Log($"  - Animation Type: Humanoid");
            Debug.Log($"  - Import Animation: {importer.importAnimation}");
            Debug.Log($"  - Optimize Game Objects (Strip Bones): {importer.optimizeGameObjects}");
            Debug.Log($"  - Bone Mapping: 적용 완료");
            Debug.Log($"  - Animation Clips: {(importer.clipAnimations != null ? importer.clipAnimations.Length : 0)}개");
            
            // 최종 저장 및 Reimport (한 번만!)
            UnityEditor.AssetDatabase.WriteImportSettingsIfDirty(relativePath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.ImportAsset(relativePath, UnityEditor.ImportAssetOptions.ForceUpdate);
            
            Debug.Log($"[3단계] 최종 Reimport 완료");
            Debug.Log($"===========================================");
        }

        #region Editor 전용: Bone Mapping 파싱

        /// <summary>
        /// BoneMapping_Data.txt 파일을 읽어서 ModelImporter에 HumanDescription으로 적용
        /// </summary>
        private void ApplyBoneMapping(UnityEditor.ModelImporter importer)
        {
            try
            {
                string mappingFilePath = Path.Combine(Application.dataPath, "Resources", BONE_MAPPING_FILE);
                if (!File.Exists(mappingFilePath))
                {
                    Debug.LogWarning($"Bone Mapping 파일을 찾을 수 없습니다: {mappingFilePath}");
                    return;
                }

                // 파일 읽기 및 파싱
                var mappingDict = ParseBoneMappingFile(mappingFilePath);
                if (mappingDict.Count == 0)
                {
                    Debug.LogWarning("Bone Mapping 데이터가 비어있습니다.");
                    return;
                }

                Debug.Log($"[Editor Only] Bone Mapping 파일 파싱 완료: {mappingDict.Count}개 매핑 발견");

                // 기존 HumanDescription 가져오기 (새로 생성할 경우 기본값 사용)
                HumanDescription description = importer.humanDescription;
                
                // HumanBone 배열 생성
                List<HumanBone> humanBones = new List<HumanBone>();
                foreach (var kvp in mappingDict)
                {
                    HumanBone bone = new HumanBone();
                    bone.humanName = kvp.Key;      // Unity Humanoid bone name (e.g., "Hips")
                    bone.boneName = kvp.Value;     // Actual bone name in FBX (e.g., "Skeleton_Hips")
                    
                    // Limit 설정은 기본값 사용
                    bone.limit.useDefaultValues = true;
                    
                    humanBones.Add(bone);
                }

                // HumanDescription 업데이트
                description.human = humanBones.ToArray();
                
                // Skeleton 설정: 보통은 모든 Transform을 포함하도록 비워두거나, 
                // 필요한 경우 특정 본만 지정할 수 있음 (일단 기본 설정 유지)
                // description.skeleton = ... (필요시 설정)
                
                // 새로운 HumanDescription 적용
                importer.humanDescription = description;
                
                Debug.Log($"[Editor Only] Bone Mapping 적용 완료: {humanBones.Count}개 본 매핑됨");
                
                // 매핑된 본 목록 출력 (디버그용)
                foreach (var bone in humanBones)
                {
                    Debug.Log($"  - {bone.humanName} -> {bone.boneName}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Bone Mapping 적용 중 오류 발생: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// YAML 형식의 BoneMapping_Data.txt 파싱
        /// </summary>
        private Dictionary<string, string> ParseBoneMappingFile(string path)
        {
            var mapping = new Dictionary<string, string>();
            string[] lines = File.ReadAllLines(path);
            bool insideBoneTemplate = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();
                
                // m_BoneTemplate 섹션 시작 확인
                if (trimmedLine.StartsWith("m_BoneTemplate:"))
                {
                    insideBoneTemplate = true;
                    continue;
                }

                // 섹션이 끝나거나 다른 속성이 나오면 중단 (들여쓰기로 판단 가능하지만 간단히 :)
                if (insideBoneTemplate)
                {
                    if (trimmedLine.StartsWith("m_")) break; // 다른 속성 시작
                    
                    // "HumanBoneName: ActualBoneName" 형식 파싱
                    int colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string key = trimmedLine.Substring(0, colonIndex).Trim();
                        string value = trimmedLine.Substring(colonIndex + 1).Trim();
                        
                        // 값이 비어있지 않은 경우에만 추가
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            mapping[key] = value;
                        }
                    }
                }
            }
            return mapping;
        }
        #endregion
#endif

    }
}
