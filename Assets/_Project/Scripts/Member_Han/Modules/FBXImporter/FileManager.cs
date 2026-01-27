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
        private const string IMPORT_FBX_FOLDER = "Import_FBX";
        private const string FBX_EXTENSION = "fbx";
        private const string BONE_MAPPING_FILE = "BoneMapping_Data.txt";
        

        #region Public 필드
        [Header("FBX 임포트 설정")]
        [Tooltip("체크 시 선택한 FBX 파일을 Import_FBX 폴더에 복사하여 저장")]
        public bool saveToImportFolder = false;
        
        [Header("Ghost Retargeting 설정")]
        [Tooltip("애니메이션을 적용할 대상 캐릭터 (Humanoid Avatar 필요)")]
        public GameObject targetCharacter;
        
        [Header("디버그 설정 (RuntimeRetargeter에 적용됨)")]
        [Tooltip("본 매핑 관련 디버그 로그 출력")]
        public bool showBoneMappingLog = false;
        [Tooltip("런타임 애니메이션 디버그 로그 출력")]
        public bool showRuntimeAnimationLog = false;
        
        [Tooltip("Ghost 모델 보이기 (디버깅용)")]
        public bool showGhostModel = false;

        [Header("Golden Hand Settings")]
        [Tooltip("Finger Stretch Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float FingerStretchScale = 1.0f;
        [Tooltip("Finger Spread Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float FingerSpreadScale = 1.0f;
        
        [Space(5)]
        [Tooltip("Thumb Stretch Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float ThumbStretchScale = 1.0f;
        [Tooltip("Thumb Spread Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float ThumbSpreadScale = 1.0f;

        [Header("Final Tuning")]
        [Tooltip("높이 보정 (미터 단위). 0.02 = 2cm 올림")]
        [Range(-0.5f, 0.5f)] public float HeightOffset = 0.02f; 

        [Tooltip("보폭 비율 (1.0 = 자동, 미끄러지면 조절)")]
        [Range(0.8f, 1.2f)] public float MovementScaleMultiplier = 1.0f;

        [Header("Thumb Digital Orthopedics (Offset)")]
        [Tooltip("Rotation Offset (X: Press, Y: Angle, Z: Roll). Default: (-10, -30, 0)")]
        public Vector3 ThumbRotationOffset = new Vector3(-10f, -30f, 0f);
        
        [Tooltip("Muscle Offset (Stretch). Default: -0.1")]
        [Range(-0.5f, 0.5f)] public float ThumbStretchOffset = -0.1f;

        [Header("Smart Curve (Dynamics)")]
        public bool EnableSmartCurve = true;
        [Tooltip("Standard Finger Dampen Strength (0.1 ~ 0.5)")]
        [Range(0.0f, 1.0f)] public float SmartCurveStrength = 0.5f; // Mapped to Dampen
        public float StretchThreshold = 0.7f; // Keeping this for logic stability

        public bool EnableThumbSmartCurve = true;
        [Range(0.0f, 1.0f)] public float ThumbSmartCurveStrength = 0.5f; // Mapped to Dampen
        
        [Space(10)]

        #endregion

        #region Private 필드
        private IFileBrowserService _fileBrowserService;
        private RuntimeFBXImporter _fbxImporter;
        #endregion

        #region Unity 생명주기
        private void Awake()
        {
            InitializeServices(); // Verified Clean Build
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
                
                // 1. 파일 복사 Check
                if (saveToImportFolder)
                {
                    string targetDir = Path.Combine(Application.dataPath, "Resources", IMPORT_FBX_FOLDER);
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                    
                    string potentialTargetPath = Path.Combine(targetDir, fileName);
                    bool isNewFile = Path.GetFullPath(sourcePath) != Path.GetFullPath(potentialTargetPath);
                    
                    if (isNewFile)
                    {
                        File.Copy(sourcePath, potentialTargetPath, true);
                        targetPath = potentialTargetPath;
                    }
                    else
                    {
                        targetPath = potentialTargetPath;
                    }

#if UNITY_EDITOR
                    if (isNewFile || File.Exists(targetPath))
                    {
                         // 에디터 환경이면 ImportSettings 적용 (선택 사항)
                         ConfigureImportSettings(targetPath);
                    }
#endif
                }
                
                // 2. Assimp-net을 사용하여 FBX 로드
                Debug.Log($"[FileManager] FBX 로드 시작: {targetPath}");
                GameObject importedModel = await _fbxImporter.ImportAsync(targetPath);
                
                if (importedModel != null)
                {
                    // Ghost Renderer 제어
                    foreach (var renderer in importedModel.GetComponentsInChildren<Renderer>())
                    {
                        renderer.enabled = showGhostModel; // Ghost 모델 보이기/숨기기
                    }

                    // 1. Ghost Container Pattern (스케일 방어막)
                    // Legacy Animation이 실행되면 자식(importedModel)의 Scale은 무조건 1.0으로 강제 원복됩니다.
                    // 이를 방어하기 위해 부모(Container)에서 0.01로 눌러버리는 구조가 필수적입니다.
                    Debug.Log($"[System] 🛡️ Activating Ghost Container... (Scale Lock: 0.01)");
                    
                    // A. 컨테이너 생성
                    GameObject ghostContainer = new GameObject($"GhostContainer_{importedModel.name}");
                    ghostContainer.transform.position = Vector3.zero;
                    ghostContainer.transform.rotation = Quaternion.identity;
                    
                    // B. 컨테이너 스케일 고정 (0.01)
                    ghostContainer.transform.localScale = Vector3.one * 0.01f;
                    
                    // C. 모델 종속시키기
                    importedModel.transform.SetParent(ghostContainer.transform, false);

                    // 2. 매핑 데이터 로드 & Ghost 아바타 생성
                    // *중요*: Avatar는 자식(importedModel)을 기준으로 굽습니다.
                    var boneMapping = LoadBoneMappingRuntime();
                    HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);

                    // 3. Ghost에서 애니메이션 클립 추출
                    AnimationClip targetClip = null;
                    var animComp = importedModel.GetComponent<Animation>();
                    
                    if (animComp != null && animComp.clip != null)
                    {
                        targetClip = animComp.clip;
    
                        if (targetClip.length > 1000f)
                        {
                            Debug.LogError("[Critical] 애니메이션 시간이 비정상적으로 깁니다. RuntimeFBXImporter의 timeScale을 확인하세요.");
                        }
                    }
                    else
                    {
                        Debug.LogError("[Critical] Ghost에 Animation 컴포넌트가 없거나 클립이 없습니다!");
                        return;
                    }


                    // 3. Target 찾기
                    GameObject targetObject = GameObject.Find("testPrefab");
                    if (targetObject == null)
                    {
                        Debug.LogError("[FileManager] 'testPrefab'을 찾을 수 없습니다. 리타겟팅을 중단합니다.");
                    }
                    {
                        // [RESET] 완전 초기화 (원점, 회전 0)
                        targetObject.transform.position = Vector3.zero; 
                        targetObject.transform.rotation = Quaternion.identity; 

                        // 중요: Animator의 Root Motion 옵션은 끕니다. 
                        // 우리가 수동으로 제어하기 때문입니다.
                        Animator anim = targetObject.GetComponent<Animator>();
                        if (anim != null) anim.applyRootMotion = false; 

                        // [STAGE 28] IKControl 간섭 제거
                        // 외부 IK 스크립트가 있다면 제거하여 물리 충돌을 방지합니다.
                        var ikControl = targetObject.GetComponent<IKControl>();
                        if (ikControl != null)
                        {
                            Debug.Log("[FileManager] ⚠️ Hostile IKControl detected. Destroying...");
                            Destroy(ikControl);
                        }

                        // --------------------------------------------------------
                        // 포즈 공간 리타겟터 부착 및 초기화 (STAGE 28)
                        // --------------------------------------------------------
                        var retargeter = importedModel.AddComponent<PoseSpaceRetargeter>();
                        
                        // FileManager 자신(this)을 넘겨서 설정을 공유함
                        retargeter.Initialize(importedModel, targetObject, boneMapping, targetClip, this);
                        
                        // 녹화기 연결 및 자동 시작 명령
                        var recorderController = targetObject.GetComponent<HumanoidSampleCode>();
                        if (recorderController != null)
                        {
                            float clipLen = targetClip.length;
                            // FBX 클립 길이와 이름을 안전하게 전달
                            recorderController.StartAutoRecording(clipLen, targetClip.name);
                        }
                        else
                        {
                            Debug.LogError("[FileManager] 'testPrefab'에 HumanoidSampleCode 컴포넌트가 없습니다!");
                        }
                    }
                        
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
        
        /// <summary>
        /// 런타임에서도 BoneMapping_Data.txt를 읽어오는 함수
        /// </summary>
        private Dictionary<string, string> LoadBoneMappingRuntime()
        {
            var mapping = new Dictionary<string, string>();
            
            // Resources 폴더에서 로드 (확장자 .txt 제외)
            // 파일명: "BoneMapping_Data.txt" -> 로드명: "BoneMapping_Data"
            string loadName = Path.GetFileNameWithoutExtension(BONE_MAPPING_FILE);
            TextAsset mappingAsset = Resources.Load<TextAsset>(loadName);

            if (mappingAsset != null)
            {
                Debug.Log($"[FileManager] BoneMapping 로드 성공 (Resources/{loadName})");
                string[] lines = mappingAsset.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
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
                        if (trimmedLine.StartsWith("m_")) break; // 섹션 종료
                        
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
            }
            else
            {
                Debug.LogError($"[FileManager] ❌ BoneMapping 로드 실패! 경로: Resources/{loadName}. (빌드에 포함되었는지 확인하세요)");
            }

            return mapping;
        }

        private void SetupGhostRetargeting(GameObject ghostObject, AnimationClip ghostClip, GameObject targetPrefab)
        {
            Debug.Log("[FileManager] Native AnimatorOverride Setup...");
             Animator targetAnimator = targetPrefab.GetComponent<Animator>();
            if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null) return;

            AnimatorOverrideController overrideController = new AnimatorOverrideController(targetAnimator.runtimeAnimatorController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            if (overrides.Count > 0)
            {
                overrideController[overrides[0].Key] = ghostClip;
            }
            
            targetAnimator.runtimeAnimatorController = overrideController;
            
            var sampleCode = targetPrefab.GetComponent<HumanoidSampleCode>();
            if (sampleCode != null)
            {
                sampleCode.StartProcessing(ghostClip);
            }
            else
            {
                targetAnimator.enabled = true;
                targetAnimator.Play("satisfaction_2_FBX", 0, 0f);
            }
             
            if (ghostObject != null) Destroy(ghostObject);
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

            string relativePath = "Assets" + standardizedFilePath[standardizedDataPath.Length..];
            
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
            Debug.Log($"  - 현재 Optimize Bones: {importer.optimizeBones}");
            
            // ===== 3단계: Rig 설정 (Humanoid, Strip Bones = False) =====
            Debug.Log($"[3단계] Rig 설정 적용 중...");
            
            // 3-1. Animation Import 활성화 (반드시 true)
            importer.importAnimation = true;
            importer.animationCompression = UnityEditor.ModelImporterAnimationCompression.Off;
            
            // 3-2. Animation Type = Humanoid
            importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
            
            // 3-3. Avatar Definition = "Create From This Model" (중요)
            // Avatar를 모델에서 생성하도록 명시적 설정 (AnimationClip 보존을 위해 필수)
            importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;
            // 3-4. Strip Bones 해제 (Optimize Game Objects 아님)
            importer.optimizeBones = false;
            try 
            {
                // SerializedObject를 사용하여 "optimizeBones" 또는 관련 속성 해제
                UnityEditor.SerializedObject serializedImporter = new UnityEditor.SerializedObject(importer);
                serializedImporter.Update();

                // 가능한 내부 속성명들 (m_OptimizeBones 등)
                string[] propNames = new string[] { "m_OptimizeBones", "optimizeBones" };
                bool found = false;

                foreach (string name in propNames)
                {
                    UnityEditor.SerializedProperty prop = serializedImporter.FindProperty(name);
                    if (prop != null && prop.propertyType == UnityEditor.SerializedPropertyType.Boolean)
                    {
                        prop.boolValue = false;
                        found = true;
                        Debug.Log($"[Strip Bones Fix] Successfully disabled '{name}' via SerializedObject");
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
            catch (System.Exception e)
            {
                Debug.LogError($"[Strip Bones Fix] Error: {e.Message}");
            }
            
            // AnimationCompression 추가 설정
            importer.animationWrapMode = WrapMode.ClampForever; // Once: 재생 후 초기화, ClampForever: 마지막 프레임 유지
            
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
                        HumanBone bone = new HumanBone
                        {
                            humanName = kvp.Key,
                            boneName = kvp.Value
                        };
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
                            SkeletonBone skelBone = new SkeletonBone
                            {
                                name = transform.name,
                                position = transform.localPosition,
                                rotation = transform.localRotation,
                                scale = transform.localScale
                            };
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
                
                UnityEditor.ModelImporterClipAnimation[] clipAnimations = new UnityEditor.ModelImporterClipAnimation[] {
                    new UnityEditor.ModelImporterClipAnimation
                    {
                        name = "Take 001",
                        takeName = "Take 001",
                        firstFrame = 0,
                        lastFrame = 100
                    }
                };
                
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
                    HumanBone bone = new HumanBone
                    {
                        humanName = kvp.Key,      // Unity Humanoid bone name (e.g., "Hips")
                        boneName = kvp.Value      // Actual bone name in FBX (e.g., "Skeleton_Hips")
                    };
                    
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
                        string key = trimmedLine[..colonIndex].Trim();
                        string value = trimmedLine[(colonIndex + 1)..].Trim();
                        
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

        // [헬퍼 함수] 힙 높이 측정 (STAGE 32)
        private float GetHipsHeight(GameObject model)
        {
            Animator anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            
            // Hips 찾기 (이름 기반)
            Transform hips = model.transform.Find("Hips"); 
            if (hips == null) hips = model.transform.Find("mixamorig:Hips");
            if (hips == null) 
            {
                // 깊이 탐색
                foreach(var t in model.GetComponentsInChildren<Transform>()) 
                    if(t.name.Contains("Hips") || t.name.Contains("Pelvis")) { hips = t; break; }
            }

            return (hips != null) ? hips.position.y : 0f;
        }
    }
}
