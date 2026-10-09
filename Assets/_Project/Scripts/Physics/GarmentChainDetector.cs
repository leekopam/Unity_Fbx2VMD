using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 신체 측 부착점(허리/가슴/목/어깨)에 매달린 비신체 체인을 토폴로지+스키닝으로 탐지한다.
    /// 이름 패턴이 실패하는 일반 FBX/VRM 모델용 폴백 — 이름 경로에 미채택됐고
    /// 스키닝 버텍스를 실제로 갖는 체인만 장식물(Accessory) 후보로 채택한다.
    /// 주의: LimbKeyword는 부분 매칭이라 armband/wristband 류의 사지명 장식물은 차단된다 —
    /// 신체 본을 못 품은 사지 보조본(ArmM 등)은 이 정규식만이 거를 수 있어 보수적으로 유지한다.
    /// </summary>
    public static class GarmentChainDetector
    {
        /// <summary>구동 보조/기구 본 — 물리 대상이 아닌 체인을 걸러낸다.</summary>
        static readonly Regex MechanismKeyword = new Regex(
            @"twist|helper|ctrl|ctl_|_ctl|aim|target|pole|locator|driver|_ik$|^ik_|_ik_",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 사지·골격 본 명명 — 장식물이 아니라 팔/다리/엉덩이 계열의 골격 연장.
        /// 서브트리 신체 본 검사와 병용한다 (어느 하나만으로는 못 걸러내는 경우가 있다).
        /// </summary>
        static readonly Regex LimbKeyword = new Regex(
            @"arm|leg|hand|foot|shoulder|elbow|wrist|knee|ankle|thigh|calf|hip|腕|脚|足|膝|肩|手|肘|腿",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// 몸쪽 부착이 정상인 장식물 명명 — 신체 제외 패턴보다 우선한다.
        /// necklace/earring 류는 이름 경로(Classify→IsExcluded)에서도 제외돼 양쪽 경로가
        /// 모두 막히는 데드존이 되므로, 신체측 폴백에서는 이 화이트리스트로 살린다.
        /// </summary>
        static readonly Regex GarmentWhitelist = new Regex(
            @"necklace|choker|earring|ear_?cuff|ear_?ring|brooch|pendant|scarf|" +
            @"목걸이|초커|귀걸이|브로치|펜던트|스카프|목도리",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>장식물 경로용 제외 판정 — 신체 제외 패턴이지만 장식물 명명은 살린다.</summary>
        static bool IsExcludedForGarment(string name) =>
            HairBoneClassifier.IsExcluded(name) && !GarmentWhitelist.IsMatch(name);

        public class Result
        {
            /// <summary>채택된 체인 루트 목록 (HairChainDetector 명시 루트로 넘긴다)</summary>
            public List<Transform> roots = new List<Transform>();
            /// <summary>체인 본에 스키닝된 버텍스 수 (roots와 같은 순서)</summary>
            public List<int> skinnedVertexCounts = new List<int>();
            /// <summary>채택/스킵 사유 — 리포트에 그대로 남긴다</summary>
            public List<string> report = new List<string>();
        }

        /// <summary>
        /// 부착점 본들의 직속 자식에서 장식물 후보 체인을 수집한다.
        /// </summary>
        /// <param name="attachmentBones">장식물이 매달릴 수 있는 신체 본 (허리~목~어깨)</param>
        /// <param name="isClaimed">신체 본·헤어 체인·이름매칭 장식물·스커트·네이티브 소유 본이면 true</param>
        /// <param name="boneVerts">본별 지배 버텍스 맵 (BodyColliderBuilder.GatherBoneVertices 결과)</param>
        /// <param name="minChainBones">이 본 수 미만 체인은 물리 대상에서 제외</param>
        /// <param name="minSkinnedVerts">이 버텍스 수 미만이면 메시와 무관한 보조 본으로 간주해 제외</param>
        public static Result Detect(
            IList<Transform> attachmentBones,
            Func<Transform, bool> isClaimed,
            Func<Transform, bool> isBodyBone,
            IDictionary<Transform, List<Vector3>> boneVerts,
            int minChainBones = 2,
            int minSkinnedVerts = 4)
        {
            var result = new Result();
            if (attachmentBones == null)
                return result;

            var adopted = new HashSet<Transform>(); // 이 탐지기가 채택한 체인의 본 — 중첩 루트 방지
            foreach (var attach in attachmentBones)
            {
                if (attach == null)
                    continue;
                foreach (Transform child in attach)
                {
                    if (!IsCandidate(child, isClaimed, isBodyBone, adopted))
                        continue;
                    var bones = CollectChain(child, isClaimed, adopted);
                    if (bones.Count < minChainBones)
                    {
                        result.report.Add($"장식물 후보 스킵(본 {bones.Count}개): {child.name}");
                        continue;
                    }
                    int verts = CountSkinnedVerts(bones, boneVerts);
                    if (verts < minSkinnedVerts)
                    {
                        result.report.Add(
                            $"장식물 후보 스킵(스키닝 버텍스 {verts}개): {child.name}");
                        continue;
                    }
                    foreach (var b in bones)
                        adopted.Add(b);
                    result.roots.Add(child);
                    result.skinnedVertexCounts.Add(verts);
                    result.report.Add(
                        $"장식물 체인 채택(토폴로지): {child.name} — 본 {bones.Count}개, 스키닝 버텍스 {verts}개");
                }
            }
            return result;
        }

        /// <summary>체인 루트 후보 적격성 — 신체/기구/사지/타 경로 담당 본을 걸러낸다.</summary>
        static bool IsCandidate(
            Transform t, Func<Transform, bool> isClaimed,
            Func<Transform, bool> isBodyBone, HashSet<Transform> adopted)
        {
            if (t == null || t.childCount == 0)
                return false;
            if (isClaimed != null && isClaimed(t))
                return false;
            if (adopted.Contains(t))
                return false;
            if (t.name.StartsWith(CharacterPhysicsSetup.ClothNamePrefix))
                return false;
            if (MechanismKeyword.IsMatch(t.name) || IsExcludedForGarment(t.name))
                return false;
            // 사지 명명의 본은 장식물이 아니라 골격 연장이다 (예: joint_RightArmM)
            if (LimbKeyword.IsMatch(t.name))
                return false;
            // 서브트리에 신체 본이 있으면 골격 연장이다 (예: 어깨 보조본 → 팔 체인)
            if (ContainsBodyBone(t, isBodyBone))
                return false;
            // 스커트 본은 스커트 경로가 담당
            if (SkirtClothBuilder.SkirtBoneRegex.IsMatch(t.name))
                return false;
            // 이름으로 채택된 루트는 isClaimed에서 이미 걸러진다. 분류가 되는 이름이라도
            // 이름 경로는 Accessory 루트만 채택하므로 몸측 tail_*/cape_* 류는 여기서 받는다.
            return true;
        }

        /// <summary>서브트리 어딘가에 신체 본이 있으면 골격 연장으로 간주한다.</summary>
        static bool ContainsBodyBone(Transform root, Func<Transform, bool> isBodyBone)
        {
            if (isBodyBone == null)
                return false;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && isBodyBone(t))
                    return true;
            return false;
        }

        /// <summary>
        /// 루트 서브트리에서 유효 본을 수집한다.
        /// 기구/제외/다른 경로 소유 본은 그 가지를 끊는다 (HairChainDetector.CollectBones와 동일 규칙).
        /// </summary>
        static List<Transform> CollectChain(
            Transform root, Func<Transform, bool> isClaimed, HashSet<Transform> adopted)
        {
            var bones = new List<Transform>();
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var b = stack.Pop();
                if (b == null)
                    continue;
                if (b != root &&
                    ((isClaimed != null && isClaimed(b)) || adopted.Contains(b)))
                    continue;
                if (MechanismKeyword.IsMatch(b.name) || IsExcludedForGarment(b.name)
                    || LimbKeyword.IsMatch(b.name)
                    || b.name.StartsWith(CharacterPhysicsSetup.ClothNamePrefix)
                    || SkirtClothBuilder.SkirtBoneRegex.IsMatch(b.name))
                    continue;
                bones.Add(b);
                foreach (Transform child in b)
                    stack.Push(child);
            }
            return bones;
        }

        static int CountSkinnedVerts(
            List<Transform> bones, IDictionary<Transform, List<Vector3>> boneVerts)
        {
            if (boneVerts == null)
                return 0;
            int total = 0;
            foreach (var b in bones)
                if (boneVerts.TryGetValue(b, out var verts) && verts != null)
                    total += verts.Count;
            return total;
        }
    }
}
