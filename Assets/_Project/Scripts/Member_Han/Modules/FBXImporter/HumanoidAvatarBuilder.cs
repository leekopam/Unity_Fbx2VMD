using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace Member_Han.Modules.FBXImporter
{
    /// <summary>
    /// Humanoid Avatar 빌드를 담당하는 유틸리티 클래스
    /// FBX 모델에 Humanoid Avatar를 자동으로 설정
    /// </summary>
    public class HumanoidAvatarBuilder
    {
        #region 상수
        private const string MAPPING_FILE_PATH = "BoneMapping_Data";
        private const string CHEST_BONE_NAME = "Chest";
        
        // HumanDescription 기본값
        private const float DEFAULT_ARM_TWIST = 0.5f;
        private const float DEFAULT_LEG_TWIST = 0.5f;
        private const float DEFAULT_ARM_STRETCH = 0.05f;
        private const float DEFAULT_LEG_STRETCH = 0.05f;
        private const float DEFAULT_FEET_SPACING = 0f;
        #endregion

        #region 공개 API
        public static void SetupHumanoid(GameObject characterRoot)
        {
            // 1. 본 매핑 데이터 로드
            Dictionary<string, string> boneMapping = LoadBoneMapping(MAPPING_FILE_PATH);
            if (boneMapping == null || boneMapping.Count == 0)
            {
                Debug.LogError("본 매핑 데이터 로드 실패");
                return;
            }

            // 2. HumanDescription 구성
            HumanDescription description = BuildHumanDescription(characterRoot, boneMapping);

            // 3. 회전 정보 저장 (T-Pose 적용을 위해)
            Dictionary<Transform, Quaternion> originalRotations = new Dictionary<Transform, Quaternion>();
            StoreRotations(characterRoot.transform, originalRotations);

            // 4. Avatar 빌드
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(characterRoot, description);
            
            if (avatar.isValid)
            {
                Debug.Log("Avatar 빌드 성공");
                ApplyAvatarToAnimator(characterRoot, avatar);
            }
            else
            {
                Debug.LogError("Avatar 빌드 실패. 설정을 확인하세요.");
            }

            // 5. 원본 회전 정보 복원
            RestoreRotations(originalRotations);
        }
        #endregion

        #region HumanDescription 구성
        private static HumanDescription BuildHumanDescription(GameObject characterRoot, Dictionary<string, string> boneMapping)
        {
            HumanDescription description = new HumanDescription();
            
            // Skeleton Bones 수집
            List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
            CollectSkeleton(characterRoot.transform, skeletonBones);
            description.skeleton = skeletonBones.ToArray();

            // Human Bones 매핑
            List<HumanBone> humanBones = new List<HumanBone>();
            foreach (var mapping in boneMapping)
            {
                string humanBoneName = mapping.Key;
                string modelBoneName = mapping.Value;

                if (IsBonePresent(characterRoot.transform, modelBoneName))
                {
                    HumanBone hb = new HumanBone();
                    hb.humanName = humanBoneName;
                    hb.boneName = modelBoneName;
                    hb.limit.useDefaultValues = true;
                    humanBones.Add(hb);
                }
                else
                {
                    // Chest 본이 누락된 경우 경고
                    if (humanBoneName == CHEST_BONE_NAME)
                    {
                        Debug.LogWarning("Chest 본이 누락되었습니다. 폴백 또는 무시 중...");
                    }
                }
            }
            description.human = humanBones.ToArray();

            // 기본 파라미터 설정
            description.upperArmTwist = DEFAULT_ARM_TWIST;
            description.lowerArmTwist = DEFAULT_ARM_TWIST;
            description.upperLegTwist = DEFAULT_LEG_TWIST;
            description.lowerLegTwist = DEFAULT_LEG_TWIST;
            description.armStretch = DEFAULT_ARM_STRETCH;
            description.legStretch = DEFAULT_LEG_STRETCH;
            description.feetSpacing = DEFAULT_FEET_SPACING;
            description.hasTranslationDoF = false;

            return description;
        }
        #endregion

        #region 본 매핑 로드
        private static Dictionary<string, string> LoadBoneMapping(string resourcePath)
        {
            TextAsset htFile = Resources.Load<TextAsset>(resourcePath);
            if (htFile == null) return null;

            Dictionary<string, string> mapping = new Dictionary<string, string>();
            
            using (StringReader reader = new StringReader(htFile.text))
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
            return mapping;
        }
        #endregion

        #region 스켈레톤 수집
        private static void CollectSkeleton(Transform current, List<SkeletonBone> skeletonBones)
        {
            SkeletonBone bone = new SkeletonBone();
            bone.name = current.name;
            bone.position = current.localPosition;
            bone.rotation = current.localRotation;
            bone.scale = current.localScale;
            
            skeletonBones.Add(bone);

            foreach (Transform child in current)
            {
                CollectSkeleton(child, skeletonBones);
            }
        }

        private static bool IsBonePresent(Transform root, string boneName)
        {
            if (root.name == boneName) return true;
            foreach (Transform child in root)
            {
                if (IsBonePresent(child, boneName)) return true;
            }
            return false;
        }
        #endregion

        #region 회전 저장/복원
        private static void StoreRotations(Transform t, Dictionary<Transform, Quaternion> dict)
        {
            dict[t] = t.localRotation;
            foreach (Transform child in t) StoreRotations(child, dict);
        }

        private static void RestoreRotations(Dictionary<Transform, Quaternion> dict)
        {
            foreach (var kvp in dict)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.localRotation = kvp.Value;
                }
            }
        }
        #endregion

        #region Animator 적용
        private static void ApplyAvatarToAnimator(GameObject characterRoot, Avatar avatar)
        {
            Animator animator = characterRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = characterRoot.AddComponent<Animator>();
            }
            animator.avatar = avatar;
        }
        #endregion
    }
}
