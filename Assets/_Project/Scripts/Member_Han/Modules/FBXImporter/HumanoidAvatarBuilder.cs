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

        // 스마트 매핑 (유지)
        private static Dictionary<string, Transform> SmartMapTransforms(Transform root, Dictionary<string, string> nameData)
        {
            var result = new Dictionary<string, Transform>();
            var allTransforms = root.GetComponentsInChildren<Transform>();
            var modelMap = new Dictionary<string, Transform>();
            
            foreach (var t in allTransforms)
            {
                string cleanName = Regex.Replace(t.name.ToLower(), "[^a-z0-9]", "");
                if (!modelMap.ContainsKey(cleanName)) modelMap[cleanName] = t;
            }

            foreach (var kvp in nameData)
            {
                string cleanTarget = Regex.Replace(kvp.Value.ToLower(), "[^a-z0-9]", "");
                if (modelMap.ContainsKey(cleanTarget)) result[kvp.Key] = modelMap[cleanTarget];
            }
            return result;
        }
    }
}
