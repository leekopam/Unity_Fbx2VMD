using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 감지된 머리카락 본 체인 하나.
    /// </summary>
    public class HairChain
    {
        public HairPart part = HairPart.Unknown;
        // 체인의 루트 본 (물리 고정점의 시작)
        public Transform rootBone;
        // 체인에 속한 전체 본 (루트 포함, 깊이순)
        public List<Transform> bones = new List<Transform>();
        // 루트에서 체인 끝까지의 월드 누적 길이 (미터)
        public float worldLength;

        public int Depth => bones.Count;
    }

    /// <summary>
    /// Head 본 하위 계층에서 머리카락 본 체인을 탐지한다.
    /// Humanoid 매핑 본과 얼굴 본은 제외한다.
    /// </summary>
    public static class HairChainDetector
    {
        public class Result
        {
            // 감지된 체인 목록
            public List<HairChain> chains = new List<HairChain>();
            // 매핑도 명명 규칙도 아닌 미분류 본 (사용자 검토용)
            public List<Transform> skipped = new List<Transform>();
        }

        /// <summary>
        /// headBone 하위를 탐색해 머리카락 체인을 반환한다.
        /// </summary>
        /// <param name="headBone">기준 머리 본</param>
        /// <param name="isBodyBone">true를 반환하는 본은 신체 본으로 간주해 탐색에서 제외</param>
        /// <param name="explicitRoots">사용자가 직접 지정한 체인 루트. 지정 시 자동 탐지보다 우선</param>
        /// <param name="includeUnknownBones">이름 규칙에 안 맞는 미매핑 본도 머리카락 후보로 포함할지</param>
        public static Result Detect(
            Transform headBone,
            Func<Transform, bool> isBodyBone,
            IList<Transform> explicitRoots = null,
            bool includeUnknownBones = false)
        {
            var result = new Result();
            if (headBone == null)
                return result;

            // 명시 지정이 있으면 그 루트들만 사용
            if (explicitRoots != null && explicitRoots.Count > 0)
            {
                foreach (var root in explicitRoots)
                {
                    if (root == null)
                        continue;
                    var chain = BuildChain(root, headBone, isBodyBone);
                    if (chain != null)
                        result.chains.Add(chain);
                }
                foreach (var chain in result.chains)
                    chain.worldLength = MeasureChainLength(chain);
                return result;
            }

            foreach (Transform child in headBone)
                Visit(child, headBone, isBodyBone, includeUnknownBones, result);

            // 체인 길이 측정
            foreach (var chain in result.chains)
                chain.worldLength = MeasureChainLength(chain);

            return result;
        }

        static void Visit(Transform bone, Transform headBone, Func<Transform, bool> isBodyBone,
            bool includeUnknown, Result result)
        {
            if (bone == null)
                return;
            if (isBodyBone != null && isBodyBone(bone))
                return;
            if (HairBoneClassifier.IsExcluded(bone.name))
                return;

            if (HairBoneClassifier.IsHairBoneName(bone.name))
            {
                var chain = BuildChain(bone, headBone, isBodyBone);
                if (chain != null)
                    result.chains.Add(chain);
                return;
            }

            // 미분류 본: 자식 중 머리카락이 있으면 계속 내려가고, 없으면 skipped 기록
            bool hasHairDescendant = false;
            foreach (Transform child in bone)
            {
                int before = result.chains.Count;
                Visit(child, headBone, isBodyBone, includeUnknown, result);
                if (result.chains.Count > before)
                    hasHairDescendant = true;
            }

            if (!hasHairDescendant && includeUnknown && bone.childCount > 0)
            {
                // 자식 전체가 미분류인 비신체 체인 — 머리카락 후보로 수용
                var chain = BuildChain(bone, headBone, isBodyBone);
                if (chain != null && chain.bones.Count >= 2)
                    result.chains.Add(chain);
                else
                    result.skipped.Add(bone);
            }
            else if (!hasHairDescendant)
            {
                result.skipped.Add(bone);
            }
        }

        /// <summary>
        /// 루트에서 시작해 유효 자식 체인을 수집한다. 자식 2개 미만 체인은 물리 대상이 아니므로 null.
        /// </summary>
        static HairChain BuildChain(Transform root, Transform headBone, Func<Transform, bool> isBodyBone)
        {
            var chain = new HairChain { rootBone = root };
            CollectBones(root, isBodyBone, chain.bones);
            if (chain.bones.Count < 2)
                return null;

            chain.part = HairBoneClassifier.Classify(root.name);
            if (chain.part == HairPart.Unknown)
            {
                // 이름 미분류 시 머리 기준 위치로 분류
                Vector3 local = headBone.InverseTransformPoint(root.position);
                chain.part = HairBoneClassifier.ClassifyByPosition(local);
            }
            return chain;
        }

        static void CollectBones(Transform bone, Func<Transform, bool> isBodyBone, List<Transform> output)
        {
            if (bone == null || (isBodyBone != null && isBodyBone(bone)))
                return;
            if (HairBoneClassifier.IsExcluded(bone.name))
                return;
            output.Add(bone);
            foreach (Transform child in bone)
                CollectBones(child, isBodyBone, output);
        }

        /// <summary>
        /// 체인 끝 본까지의 월드 경로 길이. 다중 자식이면 가장 긴 경로.
        /// </summary>
        static float MeasureChainLength(HairChain chain)
        {
            return LongestPath(chain.rootBone);
        }

        static float LongestPath(Transform bone)
        {
            float best = 0f;
            foreach (Transform child in bone)
            {
                float d = Vector3.Distance(bone.position, child.position) + LongestPath(child);
                if (d > best)
                    best = d;
            }
            return best;
        }
    }
}
