using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Member_Han.Modules.FBXImporter
{
    public static class HumanoidAvatarBuilder
    {
        public static void SetupHumanoid(GameObject targetRoot, Dictionary<string, string> explicitMapping)
        {
            Animator animator = targetRoot.GetComponent<Animator>();
            if (animator == null) animator = targetRoot.AddComponent<Animator>();

            // 수동 교정 없이 순수 데이터로 아바타 생성
            Avatar newAvatar = CreatePureAvatar(targetRoot, explicitMapping);
            
            animator.avatar = newAvatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private static Avatar CreatePureAvatar(GameObject root, Dictionary<string, string> mappingData)
        {
            // 1. 스마트 매핑
            var boneMap = SmartMapTransforms(root.transform, mappingData);
            if (boneMap.Count == 0) Debug.LogError("❌ 매핑 실패!");

            // 2. HumanDescription 생성
            // Assimp의 MakeLeftHanded가 이미 좌표계를 맞췄으므로, 
            // 현재 상태(Bind Pose)를 그대로 신뢰합니다.

            // 3. HumanDescription 생성
            HumanDescription description = new HumanDescription
            {
                // 현재 트랜스폼 상태 그대로 스켈레톤 생성
                skeleton = CreateSkeleton(root.transform), 
                
                human = boneMap.Select(kvp => new HumanBone { 
                    humanName = kvp.Key, 
                    boneName = kvp.Value.name,
                    limit = new HumanLimit { useDefaultValues = true } 
                }).ToArray(),
                
                // 근육 설정 (기본값)
                upperArmTwist = 0.5f, lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                armStretch = 0.05f, legStretch = 0.05f,
                feetSpacing = 0f, hasTranslationDoF = false
            };

            return AvatarBuilder.BuildHumanAvatar(root, description);
        }

        // 트랜스폼 계층구조를 그대로 가져오는 함수
        private static SkeletonBone[] CreateSkeleton(Transform root)
        {
            var bones = new List<SkeletonBone>();
            AddBonesRecursive(root, bones);
            return bones.ToArray();
        }

        private static void AddBonesRecursive(Transform t, List<SkeletonBone> bones)
        {
            bones.Add(new SkeletonBone {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation, // 원본 데이터 그대로 사용
                scale = t.localScale
            });
            foreach (Transform child in t) AddBonesRecursive(child, bones);
        }

        // 스마트 매핑: 정확한 이름 매칭 우선, 실패 시 normalized 매칭 시도
        private static Dictionary<string, Transform> SmartMapTransforms(Transform root, Dictionary<string, string> nameData)
        {
            var result = new Dictionary<string, Transform>();
            var allTransforms = root.GetComponentsInChildren<Transform>();
            
            // 1. 정확한 이름 매칭을 위한 딕셔너리
            var exactNameMap = new Dictionary<string, Transform>();
            // 2. Normalized 이름 매칭을 위한 딕셔너리 (fallback)
            var normalizedMap = new Dictionary<string, Transform>();
            
            foreach (var t in allTransforms)
            {
                // 정확한 이름으로 저장
                if (!exactNameMap.ContainsKey(t.name))
                    exactNameMap[t.name] = t;
                
                // Normalized 이름으로 저장 (기존 로직)
                string cleanName = Regex.Replace(t.name.ToLower(), "[^a-z0-9]", "");
                if (!normalizedMap.ContainsKey(cleanName))
                    normalizedMap[cleanName] = t;
            }

            foreach (var kvp in nameData)
            {
                string humanBoneName = kvp.Key;   // 예: "Hips"
                string targetBoneName = kvp.Value; // 예: "13.joint_HipMaster"
                
                Transform foundBone = null;
                
                // [우선순위 1] 정확한 이름 매칭
                if (exactNameMap.TryGetValue(targetBoneName, out foundBone))
                {
                    result[humanBoneName] = foundBone;
                    continue;
                }
                
                // [우선순위 2] Normalized 매칭 (fallback)
                string cleanTarget = Regex.Replace(targetBoneName.ToLower(), "[^a-z0-9]", "");
                if (normalizedMap.TryGetValue(cleanTarget, out foundBone))
                {
                    result[humanBoneName] = foundBone;
                    Debug.Log($"[AvatarBuilder] ✅ Fallback 매칭 성공: {humanBoneName} -> {foundBone.name} (원본: {targetBoneName})");
                    continue;
                }
                
                // 매칭 실패 로그
                Debug.LogWarning($"[AvatarBuilder] ⚠️ 본 매핑 실패: {humanBoneName} (대상: {targetBoneName})");
            }
            
            // 필수 본 체크 및 로그
            string[] requiredBones = { "Hips", "Spine", "Head", "LeftUpperArm", "RightUpperArm", "LeftUpperLeg", "RightUpperLeg" };
            bool hasCriticalMissing = false;
            foreach (var bone in requiredBones)
            {
                if (!result.ContainsKey(bone))
                {
                    Debug.LogError($"[AvatarBuilder] ❌ 필수 본 누락: {bone}");
                    hasCriticalMissing = true;
                }
            }
            
            // [진단 도구] 매핑 실패 시 실제 계층 구조 덤프
            if (hasCriticalMissing)
            {
                Debug.LogError("========== [진단] FBX 계층 구조 덤프 시작 ==========");
                PrintHierarchy(root, 0);
                Debug.LogError("========== [진단] FBX 계층 구조 덤프 종료 ==========");
            }
            
            Debug.Log($"[AvatarBuilder] 매핑 완료: {result.Count}/{nameData.Count} 본 발견");
            return result;
        }
        
        /// <summary>
        /// 진단용: Transform 계층 구조를 콘솔에 출력
        /// </summary>
        private static void PrintHierarchy(Transform t, int depth)
        {
            string indent = new string(' ', depth * 2);
            Debug.Log($"{indent}└─ {t.name}");
            foreach (Transform child in t)
            {
                PrintHierarchy(child, depth + 1);
            }
        }
    }
}
