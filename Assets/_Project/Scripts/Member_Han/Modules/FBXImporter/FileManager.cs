using UnityEngine;
using System.IO;
using System.Collections.Generic;
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

        #region Private 필드
        private IFileBrowserService _fileBrowserService;
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
#if UNITY_EDITOR
            _fileBrowserService = new EditorFileBrowserService();
#else
            _fileBrowserService = new StubFileBrowserService();
#endif
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
                
                SaveAndConfigureFBX(sourcePath);
            }
        }
        #endregion

        #region 파일 처리 로직
        private void SaveAndConfigureFBX(string sourcePath)
        {
            try 
            {
                string fileName = Path.GetFileName(sourcePath);
                string targetDir = Path.Combine(Application.dataPath, "Resources", IMPORT_FBX_FOLDER);
                
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                    Debug.Log($"폴더 생성: {targetDir}");
                }
                
                string targetPath = Path.Combine(targetDir, fileName);
                File.Copy(sourcePath, targetPath, true);
                
                Debug.Log($"파일 복사 완료: {targetPath}");
                
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
                ConfigureImportSettings(targetPath);
#endif
            }
            catch (System.Exception e)
            {
                Debug.LogError($"파일 처리 실패: {e.Message}");
            }
        }
        #endregion

#if UNITY_EDITOR
        #region 에디터 전용 - Import Settings
        private void ConfigureImportSettings(string absolutePath)
        {
            // Assets 폴더 기준 상대 경로로 변환
            string assetPath = "Assets" + absolutePath.Substring(Application.dataPath.Length).Replace("\\", "/");
            
            UnityEditor.ModelImporter importer = UnityEditor.AssetImporter.GetAtPath(assetPath) as UnityEditor.ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"ModelImporter를 찾을 수 없습니다: {assetPath}");
                return;
            }

            bool changed = false;
            
            // 1. Animation Type을 Humanoid로 설정
            if (importer.animationType != UnityEditor.ModelImporterAnimationType.Human)
            {
                importer.animationType = UnityEditor.ModelImporterAnimationType.Human;
                Debug.Log($"Animation Type → Humanoid");
                changed = true;
            }
            
            // 2. Strip Bones(Optimize Bones)를 false로 설정
            if (importer.optimizeBones)
            {
                importer.optimizeBones = false;
                Debug.Log($"Strip Bones → false");
                changed = true;
            }
            
            // 3. BoneMapping_Data.txt 적용
            if (ApplyBoneMapping(importer, assetPath))
            {
                changed = true;
            }
            
            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log($"Import Settings 적용 complete");
            }
            else
            {
                Debug.Log("Import Settings 이미 올바르게 설정됨");
            }
        }

        private bool ApplyBoneMapping(UnityEditor.ModelImporter importer, string assetPath)
        {
            Dictionary<string, string> boneMapping = LoadBoneMappingFromResources();
            if (boneMapping == null || boneMapping.Count == 0)
            {
                Debug.LogWarning("BoneMapping_Data.txt 로드 실패. 기본 매핑 사용.");
                return false;
            }

            GameObject tempRoot = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (tempRoot == null)
            {
                Debug.LogWarning($"FBX GameObject 로드 실패: {assetPath}");
                return false;
            }

            // Skeleton Bones 수집
            List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
            CollectSkeletonBones(tempRoot.transform, skeletonBones);

            // Human Bones 구성
            List<HumanBone> humanBones = new List<HumanBone>();
            foreach (var mapping in boneMapping)
            {
                string humanBoneName = mapping.Key;
                string modelBoneName = mapping.Value;

                if (IsBonePresent(tempRoot.transform, modelBoneName))
                {
                    HumanBone hb = new HumanBone
                    {
                        humanName = humanBoneName,
                        boneName = modelBoneName,
                        limit = new HumanLimit { useDefaultValues = true }
                    };
                    humanBones.Add(hb);
                }
            }

            if (humanBones.Count == 0)
            {
                Debug.LogWarning("매핑된 본이 없습니다. 기본 매핑 사용.");
                return false;
            }

            // HumanDescription 구성
            HumanDescription humanDesc = new HumanDescription
            {
                skeleton = skeletonBones.ToArray(),
                human = humanBones.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0,
                hasTranslationDoF = false
            };

            importer.humanDescription = humanDesc;
            Debug.Log($"BoneMapping_Data.txt 적용 완료 (매핑된 본: {humanBones.Count}개)");
            
            return true;
        }

        private Dictionary<string, string> LoadBoneMappingFromResources()
        {
            string assetPath = $"Assets/Resources/{BONE_MAPPING_FILE}";
            TextAsset htFile = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            
            if (htFile == null)
            {
                assetPath = $"Assets/Resources/{BONE_MAPPING_FALLBACK}";
                htFile = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            }

            if (htFile == null)
            {
                Debug.LogError($"BoneMapping_Data 텍스트 파일을 찾을 수 없습니다: Assets/Resources/{BONE_MAPPING_FILE}");
                return null;
            }

            Dictionary<string, string> mapping = new Dictionary<string, string>();
            
            using (System.IO.StringReader reader = new System.IO.StringReader(htFile.text))
            {
                string line;
                bool readingBones = false;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("m_BoneTemplate:"))
                    {
                        readingBones = true;
                        continue;
                    }
                    
                    if (readingBones && line.Contains(":"))
                    {
                        var parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();
                            if (!string.IsNullOrEmpty(value))
                            {
                                mapping[key] = value;
                            }
                        }
                    }
                }
            }
            
            Debug.Log($"BoneMapping_Data 로드 완료: {mapping.Count}개 본 매핑");
            return mapping;
        }

        private void CollectSkeletonBones(Transform current, List<SkeletonBone> bones)
        {
            SkeletonBone bone = new SkeletonBone
            {
                name = current.name,
                position = current.localPosition,
                rotation = current.localRotation,
                scale = current.localScale
            };
            bones.Add(bone);

            foreach (Transform child in current)
            {
                CollectSkeletonBones(child, bones);
            }
        }

        private bool IsBonePresent(Transform root, string boneName)
        {
            if (root.name == boneName) return true;
            foreach (Transform child in root)
            {
                if (IsBonePresent(child, boneName)) return true;
            }
            return false;
        }
        #endregion
#endif
    }
}
