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
        
        #region Ghost Retargeting 지원
        
        /// <summary>
        /// Ghost GameObject용 Humanoid Avatar 자동 생성
        /// BoneMapping 파일 없이 키워드 기반으로 본을 자동 매핑
        /// </summary>
        public static Avatar BuildGhostAvatar(GameObject ghostRoot)
        {
            Debug.Log($"[BuildGhostAvatar] Ghost Avatar 생성 시작: {ghostRoot.name}");
            
            // 1. Auto-Mapping으로 HumanDescription 생성
            HumanDescription description = AutoMapBones(ghostRoot);
            
            // 2. Avatar 빌드
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(ghostRoot, description);
            
            if (avatar.isValid)
            {
                Debug.Log("[BuildGhostAvatar] ✅ Ghost Avatar 생성 성공");
            }
            else
            {
                Debug.LogError("[BuildGhostAvatar] ❌ Ghost Avatar 생성 실패. 본 매핑 확인 필요.");
            }
            
            return avatar;
        }
        
        /// <summary>
        /// 키워드 기반 자동 본 매핑
        /// Mixamo(mixamorig:Hips), Skeleton_(Skeleton_Hips) 등 다양한 형식 지원
        /// </summary>
        private static HumanDescription AutoMapBones(GameObject root)
        {
            // 매핑 규칙: [Humanoid 표준 이름] -> [검색 키워드 배열]
            Dictionary<string, string[]> mappingRules = new Dictionary<string, string[]>
            {
                // 핵심 본 (필수)
                { "Hips", new[] { "Hips", "Pelvis", "hip", "pelvis" } },
                { "Spine", new[] { "Spine", "spine1", "spine" } },
                { "Chest", new[] { "Chest", "Spine2", "chest", "spine2" } },
                { "Neck", new[] { "Neck", "neck" } },
                { "Head", new[] { "Head", "head" } },
                
                // 왼쪽 다리
                { "LeftUpperLeg", new[] { "LeftUpLeg", "L_Thigh", "LeftThigh", "LeftLeg", "L_UpLeg" } },
                { "LeftLowerLeg", new[] { "LeftLeg", "L_Calf", "LeftShin", "L_Leg", "LeftKnee" } },
                { "LeftFoot", new[] { "LeftFoot", "L_Foot" } },
                { "LeftToes", new[] { "LeftToe", "L_Toe", "LeftFoot_End" } },
                
                // 오른쪽 다리
                { "RightUpperLeg", new[] { "RightUpLeg", "R_Thigh", "RightThigh", "RightLeg", "R_UpLeg" } },
                { "RightLowerLeg", new[] { "RightLeg", "R_Calf", "RightShin", "R_Leg", "RightKnee" } },
                { "RightFoot", new[] { "RightFoot", "R_Foot" } },
                { "RightToes", new[] { "RightToe", "R_Toe", "RightFoot_End" } },
                
                // 왼쪽 팔
                { "LeftShoulder", new[] { "LeftShoulder", "L_Shoulder" } },
                { "LeftUpperArm", new[] { "LeftArm", "L_UpperArm", "LeftUpperArm", "L_Arm" } },
                { "LeftLowerArm", new[] { "LeftForeArm", "L_Forearm", "LeftElbow", "L_ForeArm" } },
                { "LeftHand", new[] { "LeftHand", "L_Hand" } },
                
                // 오른쪽 팔
                { "RightShoulder", new[] { "RightShoulder", "R_Shoulder" } },
                { "RightUpperArm", new[] { "RightArm", "R_UpperArm", "RightUpperArm", "R_Arm" } },
                { "RightLowerArm", new[] { "RightForeArm", "R_Forearm", "RightElbow", "R_ForeArm" } },
                { "RightHand", new[] { "RightHand", "R_Hand" } }
            };
            
            // 모든 Transform 수집
            Transform[] allBones = root.GetComponentsInChildren<Transform>();
            Debug.Log($"[AutoMapBones] 전체 본 개수: {allBones.Length}");
            
            // 매핑 결과 저장
            List<HumanBone> humanBones = new List<HumanBone>();
            int successCount = 0;
            int failCount = 0;
            
            foreach (var rule in mappingRules)
            {
                string humanName = rule.Key;
                string[] keywords = rule.Value;
                
                // 키워드로 본 찾기
                Transform found = FindBoneByKeywords(allBones, keywords);
                if (found != null)
                {
                    HumanBone hb = new HumanBone();
                    hb.humanName = humanName;
                    hb.boneName = found.name;
                    hb.limit.useDefaultValues = true;
                    humanBones.Add(hb);
                    
                    successCount++;
                    Debug.Log($"[AutoMap] ✅ {humanName} -> {found.name}");
                }
                else
                {
                    failCount++;
                    // 필수 본(Hips, Spine 등)이 없으면 Warning
                    if (humanName == "Hips" || humanName == "Spine" || humanName == "Head")
                    {
                        Debug.LogWarning($"[AutoMap] ⚠️ 필수 본 매핑 실패: {humanName}");
                    }
                }
            }
            
            Debug.Log($"[AutoMapBones] 매핑 결과: 성공 {successCount}개 / 실패 {failCount}개");
            
            // SkeletonBone 수집 (기존 메서드 재사용)
            List<SkeletonBone> skeletonBonesList = new List<SkeletonBone>();
            CollectSkeleton(root.transform, skeletonBonesList);
            SkeletonBone[] skeletonBones = skeletonBonesList.ToArray();
            
            // HumanDescription 생성
            HumanDescription description = new HumanDescription();
            description.human = humanBones.ToArray();
            description.skeleton = skeletonBones;
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
        
        /// <summary>
        /// 키워드 배열로 본 찾기 (대소문자 무시, 부분 일치)
        /// </summary>
        private static Transform FindBoneByKeywords(Transform[] bones, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                foreach (Transform bone in bones)
                {
                    // 정확히 일치하는 경우 우선 반환
                    if (bone.name.Equals(keyword, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return bone;
                    }
                }
                
                // 부분 일치 검색 (접두사/접미사 포함)
                foreach (Transform bone in bones)
                {
                    if (bone.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return bone;
                    }
                }
            }
            return null;
        }
        
        #endregion
        #endregion
    }
}
