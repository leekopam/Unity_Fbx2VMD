using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// Humanoid 본 구조를 기반으로 MC2 신체 콜라이더를 자동 생성한다.
    /// 스키닝 버텍스 분포로 반경을 피팅하고, 대칭 모드로 좌우를 자동 복제한다.
    /// </summary>
    public static class BodyColliderBuilder
    {
        public const string AutoNamePrefix = "MC2Col_";

        public class BuildResult
        {
            public List<ColliderComponent> colliders = new List<ColliderComponent>();
            public Dictionary<ColliderCategory, List<ColliderComponent>> byCategory =
                new Dictionary<ColliderCategory, List<ColliderComponent>>();
            public List<string> warnings = new List<string>();
        }

        // 콜라이더 슬롯 정의: (카테고리, 시작 본, 끝 본, 캡슐 여부, 좌우 대칭 대상)
        // maxR = 피팅 반경의 상한(상반신 길이 비율). 머리카락 등 체형 외 웨이트로 반경이 과대해지는 것을 방지.
        struct Slot
        {
            public ColliderCategory category;
            public HumanBodyBones start;
            public HumanBodyBones end;
            public bool capsule;
            public bool paired; // 좌우 쌍 중 왼쪽만 만들고 대칭 복제할지
            public string name;
            public float maxR;
        }

        static readonly Slot[] Slots =
        {
            new Slot { category = ColliderCategory.Head,      start = HumanBodyBones.Head,          end = HumanBodyBones.Neck,           capsule = false, paired = false, name = "Head",       maxR = 0.28f },
            new Slot { category = ColliderCategory.Neck,      start = HumanBodyBones.Neck,          end = HumanBodyBones.Head,           capsule = true,  paired = false, name = "Neck",       maxR = 0.12f },
            new Slot { category = ColliderCategory.Chest,     start = HumanBodyBones.Chest,         end = HumanBodyBones.UpperChest,     capsule = true,  paired = false, name = "Chest",      maxR = 0.40f },
            new Slot { category = ColliderCategory.Waist,     start = HumanBodyBones.Spine,         end = HumanBodyBones.Chest,          capsule = true,  paired = false, name = "Waist",      maxR = 0.24f },
            new Slot { category = ColliderCategory.Hips,      start = HumanBodyBones.Hips,          end = HumanBodyBones.Spine,          capsule = false, paired = false, name = "Hips",       maxR = 0.40f },
            new Slot { category = ColliderCategory.Shoulders, start = HumanBodyBones.LeftShoulder,  end = HumanBodyBones.LeftUpperArm,   capsule = true,  paired = true,  name = "Shoulder_L", maxR = 0.12f },
            new Slot { category = ColliderCategory.Arms,      start = HumanBodyBones.LeftUpperArm,  end = HumanBodyBones.LeftLowerArm,   capsule = true,  paired = true,  name = "UpperArm_L", maxR = 0.10f },
            new Slot { category = ColliderCategory.Arms,      start = HumanBodyBones.LeftLowerArm,  end = HumanBodyBones.LeftHand,       capsule = true,  paired = true,  name = "LowerArm_L", maxR = 0.10f },
            new Slot { category = ColliderCategory.Hands,     start = HumanBodyBones.LeftHand,      end = HumanBodyBones.LeftMiddleProximal, capsule = false, paired = true, name = "Hand_L",   maxR = 0.10f },
            new Slot { category = ColliderCategory.Legs,      start = HumanBodyBones.LeftUpperLeg,  end = HumanBodyBones.LeftLowerLeg,   capsule = true,  paired = true,  name = "UpperLeg_L", maxR = 0.16f },
            new Slot { category = ColliderCategory.Legs,      start = HumanBodyBones.LeftLowerLeg,  end = HumanBodyBones.LeftFoot,       capsule = true,  paired = true,  name = "LowerLeg_L", maxR = 0.14f },
            new Slot { category = ColliderCategory.Feet,      start = HumanBodyBones.LeftFoot,      end = HumanBodyBones.LeftToes,       capsule = false, paired = true,  name = "Foot_L",     maxR = 0.16f },
        };

        /// <summary>
        /// Animator의 Humanoid 본에 콜라이더를 생성한다.
        /// </summary>
        /// <param name="animator">Humanoid Avatar를 가진 Animator</param>
        /// <param name="boneVerts">본별 월드 버텍스(반경 피팅용). null이면 기본 비율 사용</param>
        /// <param name="useSymmetry">좌우 쌍을 AutomaticHumanBody 대칭으로 처리할지</param>
        /// <param name="includeLegs">다리·발 콜라이더 포함 여부 (장발용)</param>
        /// <param name="radiusScale">피팅 반경에 곱하는 안전 계수</param>
        public static BuildResult Build(
            Animator animator,
            Dictionary<Transform, List<Vector3>> boneVerts = null,
            bool useSymmetry = true,
            bool includeLegs = true,
            float radiusScale = 1.15f)
        {
            if (animator == null || !animator.isHuman)
            {
                var fail = new BuildResult();
                fail.warnings.Add("Humanoid Animator가 없어 콜라이더를 생성할 수 없습니다.");
                return fail;
            }
            return Build(
                animator.GetBoneTransform, MeasureTorso(animator),
                boneVerts, useSymmetry, includeLegs, radiusScale);
        }

        /// <summary>
        /// 본 해석 함수 기반 생성 — Humanoid 없이도 테스트·폴백 가능한 내부 진입점.
        /// </summary>
        /// <param name="boneResolver">HumanBodyBones → Transform 해석 함수</param>
        /// <param name="torsoLength">머리~골반 기준 길이</param>
        public static BuildResult Build(
            System.Func<HumanBodyBones, Transform> boneResolver,
            float torsoLength,
            Dictionary<Transform, List<Vector3>> boneVerts = null,
            bool useSymmetry = true,
            bool includeLegs = true,
            float radiusScale = 1.15f)
        {
            var result = new BuildResult();
            bool mirrorOk = useSymmetry; // AutomaticHumanBody는 Animator가 같은 부모 위에 있어야 함

            foreach (var slot in Slots)
            {
                if (!includeLegs && (slot.category == ColliderCategory.Legs || slot.category == ColliderCategory.Feet))
                    continue;

                Transform startBone = boneResolver(slot.start);
                if (startBone == null)
                {
                    result.warnings.Add($"본이 없어 건너뜀: {slot.start}");
                    continue;
                }
                Transform endBone = boneResolver(slot.end); // 없을 수 있음(UpperChest/Toes)
                WarnIfNonUniformScale(startBone, slot.name, result);

                ColliderComponent col = slot.capsule && endBone != null
                    ? CreateCapsule(startBone, endBone, slot.name, boneVerts, torsoLength, radiusScale, slot.maxR)
                    : CreateSphere(startBone, endBone, slot.name, boneVerts, torsoLength, radiusScale, slot.maxR);

                if (col == null)
                    continue;

                if (slot.paired && mirrorOk)
                    col.symmetryMode = ColliderSymmetryMode.AutomaticHumanBody;
                else if (slot.paired)
                    CreateMirrorIfPossible(col, slot, boneResolver, boneVerts, torsoLength, radiusScale, result);

                // MC2 공식 요구사항: 스크립트로 파라미터 변경 후 UpdateParameters를 호출해야
                // 데이터 검증과 시메트리 해석이 즉시 반영된다.
                col.UpdateParameters();
                result.colliders.Add(col);
                if (!result.byCategory.TryGetValue(slot.category, out var list))
                    result.byCategory[slot.category] = list = new List<ColliderComponent>();
                list.Add(col);
            }
            return result;
        }

        /// <summary>
        /// 생성된 콜라이더 GameObject를 전부 제거한다 (접두사 기준, 재실행 안전).
        /// </summary>
        public static void ClearGenerated(Transform root)
        {
            if (root == null)
                return;
            var doomed = new List<GameObject>();
            foreach (var col in root.GetComponentsInChildren<ColliderComponent>(true))
            {
                if (col.name.StartsWith(AutoNamePrefix))
                    doomed.Add(col.gameObject);
            }
            foreach (var go in doomed)
            {
                if (Application.isPlaying)
                    Object.Destroy(go);
                else
                    Object.DestroyImmediate(go);
            }
        }

        // MC2 콜라이더는 균일 스케일 전제 — 비균일 스케일 본에 붙으면 판정이 깨짐
        static void WarnIfNonUniformScale(Transform bone, string slotName, BuildResult result)
        {
            Vector3 s = bone.lossyScale;
            float avg = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            if (avg <= 0f)
                return;
            if (Mathf.Abs(s.x - s.y) > avg * 0.02f || Mathf.Abs(s.y - s.z) > avg * 0.02f)
            {
                result.warnings.Add(
                    $"본 '{bone.name}'({slotName})의 스케일이 비균일({s.x:F2},{s.y:F2},{s.z:F2}) — 콜라이더 판정이 정확하지 않을 수 있음");
            }
        }

        static MagicaCapsuleCollider CreateCapsule(
            Transform bone, Transform child, string slotName,
            Dictionary<Transform, List<Vector3>> boneVerts, float torsoLength, float radiusScale, float maxRRatio)
        {
            var go = new GameObject(AutoNamePrefix + slotName);
            go.transform.SetParent(bone, false);

            var col = go.AddComponent<MagicaCapsuleCollider>();

            // 본→자식 방향을 본 로컬축으로 변환해 캡슐 축 결정
            Vector3 localChild = bone.InverseTransformPoint(child.position);
            SetCapsuleAxis(col, localChild);

            float length = localChild.magnitude;
            float radius;
            if (boneVerts != null && boneVerts.TryGetValue(bone, out var verts) && verts.Count > 0)
            {
                // 월드 공간 선분으로 반경 피팅
                radius = ColliderMath.FitCapsuleRadius(bone.position, child.position, verts) * radiusScale;
            }
            else
            {
                radius = Mathf.Max(torsoLength * 0.06f, 0.02f); // fallback: 상반신 비율
            }
            float cap = maxRRatio > 0f ? torsoLength * maxRRatio : torsoLength * 0.5f;
            radius = Mathf.Clamp(radius, 0.01f, cap);
            col.SetSize(radius, radius, length);
            col.center = bone.worldToLocalMatrix.MultiplyPoint(
                Vector3.Lerp(bone.position, child.position, 0.5f));
            return col;
        }

        static MagicaSphereCollider CreateSphere(
            Transform bone, Transform nextBone, string slotName,
            Dictionary<Transform, List<Vector3>> boneVerts, float torsoLength, float radiusScale, float maxRRatio)
        {
            var go = new GameObject(AutoNamePrefix + slotName);
            go.transform.SetParent(bone, false);
            var col = go.AddComponent<MagicaSphereCollider>();

            float radius;
            if (boneVerts != null && boneVerts.TryGetValue(bone, out var verts) && verts.Count > 0)
            {
                radius = ColliderMath.FitSphereRadius(bone.position, verts) * radiusScale;
            }
            else
            {
                radius = torsoLength * 0.12f; // fallback
            }
            float cap = maxRRatio > 0f ? torsoLength * maxRRatio : torsoLength * 0.6f;
            radius = Mathf.Clamp(radius, 0.015f, cap);
            col.SetSize(radius);

            // 손·발은 관절 끝 방향으로 중심을 약간 이동해 커버 범위 확보
            if (nextBone != null)
            {
                Vector3 mid = Vector3.Lerp(bone.position, nextBone.position, 0.4f);
                col.center = bone.worldToLocalMatrix.MultiplyPoint(mid);
            }
            return col;
        }

        static void SetCapsuleAxis(MagicaCapsuleCollider col, Vector3 localDir)
        {
            Vector3 abs = new Vector3(Mathf.Abs(localDir.x), Mathf.Abs(localDir.y), Mathf.Abs(localDir.z));
            if (abs.x >= abs.y && abs.x >= abs.z)
            {
                col.direction = MagicaCapsuleCollider.Direction.X;
                col.reverseDirection = localDir.x < 0f;
            }
            else if (abs.y >= abs.z)
            {
                col.direction = MagicaCapsuleCollider.Direction.Y;
                col.reverseDirection = localDir.y < 0f;
            }
            else
            {
                col.direction = MagicaCapsuleCollider.Direction.Z;
                col.reverseDirection = localDir.z < 0f;
            }
        }

        static void CreateMirrorIfPossible(
            ColliderComponent src, Slot slot, System.Func<HumanBodyBones, Transform> boneResolver,
            Dictionary<Transform, List<Vector3>> boneVerts, float torsoLength, float radiusScale,
            BuildResult result)
        {
            // 대칭 비활성 시 오른쪽 본에 직접 생성
            HumanBodyBones rStart = MirrorBone(slot.start);
            HumanBodyBones rEnd = MirrorBone(slot.end);
            Transform rBone = boneResolver(rStart);
            Transform rEndBone = boneResolver(rEnd);
            if (rBone == null)
                return;

            string rName = slot.name.Replace("_L", "_R");
            ColliderComponent col = slot.capsule && rEndBone != null
                ? CreateCapsule(rBone, rEndBone, rName, boneVerts, torsoLength, radiusScale, slot.maxR)
                : CreateSphere(rBone, rEndBone, rName, boneVerts, torsoLength, radiusScale, slot.maxR);
            if (col == null)
                return;
            col.UpdateParameters();
            result.colliders.Add(col);
            if (!result.byCategory.TryGetValue(slot.category, out var list))
                result.byCategory[slot.category] = list = new List<ColliderComponent>();
            list.Add(col);
        }

        static HumanBodyBones MirrorBone(HumanBodyBones bone)
        {
            switch (bone)
            {
                case HumanBodyBones.LeftShoulder: return HumanBodyBones.RightShoulder;
                case HumanBodyBones.LeftUpperArm: return HumanBodyBones.RightUpperArm;
                case HumanBodyBones.LeftLowerArm: return HumanBodyBones.RightLowerArm;
                case HumanBodyBones.LeftHand: return HumanBodyBones.RightHand;
                case HumanBodyBones.LeftUpperLeg: return HumanBodyBones.RightUpperLeg;
                case HumanBodyBones.LeftLowerLeg: return HumanBodyBones.RightLowerLeg;
                case HumanBodyBones.LeftFoot: return HumanBodyBones.RightFoot;
                case HumanBodyBones.LeftToes: return HumanBodyBones.RightToes;
                case HumanBodyBones.LeftMiddleProximal: return HumanBodyBones.RightMiddleProximal;
                default: return bone;
            }
        }

        /// <summary>머리~골반 거리를 측정해 체형 기준 길이로 반환.</summary>
        public static float MeasureTorso(Animator animator)
        {
            return MeasureTorso(
                animator.GetBoneTransform(HumanBodyBones.Hips),
                animator.GetBoneTransform(HumanBodyBones.Head));
        }

        /// <summary>머리~골반 거리를 측정해 체형 기준 길이로 반환 (해석 함수 폴백용).</summary>
        public static float MeasureTorso(Transform hips, Transform head)
        {
            if (hips == null || head == null)
                return 0.5f; // 합리적 기본값
            return Mathf.Max(Vector3.Distance(hips.position, head.position), 0.05f);
        }

        /// <summary>
        /// Humanoid Avatar에 매핑된 전체 신체 본 집합을 반환한다.
        /// 머리카락 탐지 시 신체 본 제외에 사용.
        /// </summary>
        public static HashSet<Transform> CollectBodyBoneSet(Animator animator)
        {
            var set = new HashSet<Transform>();
            if (animator == null || !animator.isHuman)
                return set;
            foreach (HumanBodyBones b in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (b == HumanBodyBones.LastBone)
                    continue;
                var t = animator.GetBoneTransform(b);
                if (t != null)
                    set.Add(t);
            }
            return set;
        }

        /// <summary>
        /// 스키닝 메시에서 본별 지배 웨이트 버텍스(월드 좌표)를 수집한다.
        /// 콜라이더 반경 피팅에 사용.
        /// </summary>
        public static Dictionary<Transform, List<Vector3>> GatherBoneVertices(
            IList<SkinnedMeshRenderer> renderers)
        {
            var map = new Dictionary<Transform, List<Vector3>>();
            if (renderers == null)
                return map;

            foreach (var smr in renderers)
            {
                if (smr == null || smr.sharedMesh == null)
                    continue;
                var mesh = smr.sharedMesh;
                var verts = mesh.vertices;
                var boneWeights = mesh.boneWeights;
                var bones = smr.bones;
                if (boneWeights == null || boneWeights.Length != verts.Length)
                    continue;

                Transform root = smr.transform;
                for (int i = 0; i < verts.Length; i++)
                {
                    var bw = boneWeights[i];
                    int bi = DominantBoneIndex(bw);
                    if (bi < 0 || bi >= bones.Length || bones[bi] == null)
                        continue;
                    if (!map.TryGetValue(bones[bi], out var list))
                        map[bones[bi]] = list = new List<Vector3>();
                    list.Add(root.TransformPoint(verts[i]));
                }
            }
            return map;
        }

        static int DominantBoneIndex(BoneWeight w)
        {
            int best = w.boneIndex0;
            float bestW = w.weight0;
            if (w.weight1 > bestW) { best = w.boneIndex1; bestW = w.weight1; }
            if (w.weight2 > bestW) { best = w.boneIndex2; bestW = w.weight2; }
            if (w.weight3 > bestW) { best = w.boneIndex3; }
            return best;
        }
    }
}
