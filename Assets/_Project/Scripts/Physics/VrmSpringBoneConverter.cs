using System.Collections.Generic;
using MagicaCloth2;
using UnityEngine;
using VRM;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// UniVRM 0.x VRMSpringBone을 MC2 BoneCloth로 변환한다 (C-2 정책).
    /// 제작자 튜닝(강성·감쇠·중력·콜라이더)을 MC2 파라미터로 이식하고
    /// 원본 스프링본은 비활성화해 이중 시뮬레이션을 막는다.
    /// </summary>
    public static class VrmSpringBoneConverter
    {
        /// <summary>변환 콜라이더 이름 접두사 — ClearGenerated 정리 대상에 포함.</summary>
        public const string ColliderNamePrefix = BodyColliderBuilder.AutoNamePrefix + "Vrm_";

        /// <summary>
        /// VRM stiffnessForce(힘 계수, 통상 0.5~4)를 MC2 복원 강성(0~1)으로
        /// 환산하는 근사 상수. 의미가 1:1이 아니므로 실측 비교(Phase 4)에서 보정한다.
        /// </summary>
        public const float StiffnessToMc2Scale = 0.25f;

        /// <summary>변환 결과 — 생성물과 미지원 필드 기록.</summary>
        public class Result
        {
            public readonly List<MagicaCloth> cloths = new List<MagicaCloth>();
            public readonly List<ColliderComponent> colliders = new List<ColliderComponent>();
            public readonly List<VRMSpringBone> disabledSprings = new List<VRMSpringBone>();
            public readonly List<string> unsupported = new List<string>();
            /// <summary>그룹별 변환 콜라이더 캐시 — 공유 그룹의 중복 생성 방지.</summary>
            public readonly Dictionary<VRMSpringBoneColliderGroup, List<ColliderComponent>>
                collidersByGroup = new Dictionary<VRMSpringBoneColliderGroup, List<ColliderComponent>>();
        }

        /// <summary>
        /// VRM 스프링 파라미터를 MC2 값형 필드에 적용한다 (순수 매핑 — 본/콜라이더
        /// 참조를 다루지 않아 EditMode에서 검증 가능).
        /// </summary>
        /// <param name="unit">모델 스케일 단위 (torso/0.6m) — 거리 파라미터 스케일용</param>
        public static void ApplyParams(ClothSerializeData s, float stiffnessForce,
            float dragForce, float gravityPower, Vector3 gravityDir, float hitRadius,
            float unit = 1f)
        {
            // 복원 강성 — 깊이 커브 없이 균일값 (VRM은 체인 단위 스칼라)
            var rest = s.angleRestorationConstraint;
            rest.useAngleRestoration = true;
            rest.stiffness.SetValue(Mathf.Clamp01(stiffnessForce * StiffnessToMc2Scale));

            // VRM dragForce(프레임당 운동량 손실률) → MC2 damping
            s.damping.SetValue(Mathf.Clamp01(dragForce));

            // VRM의 gravityDir*gravityPower는 가속도 벡터 — MC2는 크기+방향 분리
            s.gravity = gravityPower;
            s.gravityDirection = gravityDir.sqrMagnitude > 1e-6f
                ? gravityDir.normalized
                : Vector3.down;

            // 파티클 충돌 반경
            s.radius.SetValue(Mathf.Max(hitRadius, 0.001f));

            // 프로젝트 공통 안전값 — 텔레포트 후 폭주 방지, 낮은 마찰
            var inertia = s.inertiaConstraint;
            inertia.teleportMode = InertiaConstraint.TeleportMode.Keep;
            inertia.teleportDistance = 0.5f * unit;
            inertia.teleportRotation = 90f;
            s.colliderCollisionConstraint.friction = 0.05f;
        }

        /// <summary>모델 루트 하위의 모든 VRMSpringBone을 MC2 클로스로 변환한다.</summary>
        /// <param name="setupRoot">생성 클로스를 매달 부모 (CharacterPhysicsSetup.transform)</param>
        /// <param name="modelRoot">VRMSpringBone 검색 루트 (Animator 루트)</param>
        /// <param name="unit">모델 스케일 단위 — 거리 파라미터 스케일용</param>
        /// <param name="report">변환 기록 (null 허용)</param>
        public static Result ConvertAll(Transform setupRoot, Transform modelRoot,
            float unit, List<string> report)
        {
            var result = new Result();
            if (setupRoot == null || modelRoot == null)
                return result;
            int index = 0;
            foreach (var spring in modelRoot.GetComponentsInChildren<VRMSpringBone>(true))
            {
                if (spring == null)
                    continue;
                if (!spring.isActiveAndEnabled)
                {
                    // 제작자가 끈 스프링(컴포넌트/오브젝트)은 물리 없음이 의도 —
                    // MC2로 부활시키지 않는다. 단 직전 변환 실행이 끈 스프링도 이
                    // 경로로 오므로, 재실행 전 호출부가 원본을 복원해야 한다
                    // (CharacterPhysicsSetup.ClearGenerated 참조).
                    report?.Add($"VRM 스프링 '{spring.name}' — 비활성 상태라 건너뜀");
                    continue;
                }
                if (Convert(setupRoot, spring, index, unit, result, report) != null)
                    index++;
            }
            return result;
        }

        /// <summary>VRMSpringBone 1개를 BoneCloth 1개로 변환하고 원본을 비활성화한다.</summary>
        static MagicaCloth Convert(Transform setupRoot, VRMSpringBone spring, int index,
            float unit, Result result, List<string> report)
        {
            // MC2 rootBones는 Fixed 앵커 — 리프 단독 루트는 전 입자 고정인
            // 죽은 체인이 되므로 변환 대상에서 제외한다.
            var roots = new List<Transform>();
            var leafRoots = new HashSet<Transform>();
            if (spring.RootBones != null)
                foreach (var rb in spring.RootBones)
                {
                    if (rb == null || roots.Contains(rb) || leafRoots.Contains(rb))
                        continue;
                    if (rb.childCount == 0)
                        leafRoots.Add(rb);
                    else
                        roots.Add(rb);
                }
            if (roots.Count == 0)
            {
                report?.Add($"VRM 스프링 '{spring.name}' — 변환 가능한 루트본이 " +
                    $"없어 건너뜀 (리프 루트 {leafRoots.Count}개)");
                return null;
            }

            var go = new GameObject($"{CharacterPhysicsSetup.ClothNamePrefix}Vrm_{index}");
            go.transform.SetParent(setupRoot, false);
            var cloth = go.AddComponent<MagicaCloth>();
            var sdata = cloth.SerializeData;
            sdata.clothType = ClothProcess.ClothType.BoneCloth;
            sdata.rootBones.Clear();
            sdata.rootBones.AddRange(roots);
            ApplyParams(sdata, spring.m_stiffnessForce, spring.m_dragForce,
                spring.m_gravityPower, spring.m_gravityDir, spring.m_hitRadius, unit);

            // 그룹별 콜라이더 → MC2 SphereCollider — 같은 그룹을 공유하는
            // 스프링이 있으므로 그룹당 1회만 생성해 컴포넌트를 재사용한다.
            var clothColliders = sdata.colliderCollisionConstraint.colliderList;
            clothColliders.Clear();
            bool colliderCapped = false;
            if (spring.ColliderGroups != null)
                foreach (var group in spring.ColliderGroups)
                {
                    if (group == null || !group.isActiveAndEnabled)
                        continue;
                    foreach (var col in ConvertColliderGroup(group, result))
                    {
                        if (clothColliders.Count >= 32)
                        {
                            colliderCapped = true;
                            break;
                        }
                        if (!clothColliders.Contains(col))
                            clothColliders.Add(col);
                    }
                }
            if (colliderCapped)
                result.unsupported.Add($"{spring.name}: 콜라이더 32개 상한 초과 — 잘림");

            spring.enabled = false; // 삭제가 아닌 비활성 — 호출부가 disabledSprings로 복원 책임
            result.cloths.Add(cloth);
            result.disabledSprings.Add(spring);
            // 미지원·근사 필드 기록 — Phase 4 검증 때 판정 재료로 쓴다
            if (leafRoots.Count > 0)
                result.unsupported.Add(
                    $"{spring.name}: 리프 루트본 {leafRoots.Count}개 미변환 (MC2 고정 앵커)");
            result.unsupported.Add(
                $"{spring.name}: 루트본 첫 세그먼트 고정 — VRM은 루트본도 시뮬레이션함(근사)");
            if (spring.m_center != null)
                result.unsupported.Add(
                    $"{spring.name}: m_center 미지원 — 월드 기준 시뮬레이션으로 대체");
            if (spring.m_updateType != VRMSpringBone.SpringBoneUpdateType.LateUpdate)
                result.unsupported.Add(
                    $"{spring.name}: m_updateType={spring.m_updateType} — MC2 갱신 주기와 불일치");
            if (!string.IsNullOrEmpty(spring.m_comment))
                result.unsupported.Add($"{spring.name}: m_comment: {spring.m_comment}");
            // hitRadius는 모델 로컬 단위 — unit≠1 모델에서 스케일 적용 여부는
            // Phase 4 네이티브↔변환 비교로 판정한다 (헤어 경로는 0.02f*unit 기준).
            if (Mathf.Abs(unit - 1f) > 0.01f)
                result.unsupported.Add(
                    $"{spring.name}: hitRadius unit 스케일 미적용 — Phase 4 캘리브레이션 필요");
            report?.Add($"VRM→MC2 변환: {go.name} (체인 루트 {roots.Count}개, " +
                $"콜라이더 {clothColliders.Count}개)");
            return cloth;
        }

        /// <summary>그룹의 SphereCollider[]를 MC2 SphereCollider로 복사한다 (그룹당 1회).</summary>
        static IEnumerable<ColliderComponent> ConvertColliderGroup(
            VRMSpringBoneColliderGroup group, Result result)
        {
            if (result.collidersByGroup.TryGetValue(group, out var existing))
                return existing;
            var created = new List<ColliderComponent>();
            result.collidersByGroup.Add(group, created);
            if (group.Colliders == null)
                return created;
            for (int i = 0; i < group.Colliders.Length; i++)
            {
                var sc = group.Colliders[i];
                if (sc == null)
                    continue;
                var go = new GameObject(ColliderNamePrefix + result.colliders.Count);
                go.transform.SetParent(group.transform, false);
                var col = go.AddComponent<MagicaSphereCollider>();
                col.SetSize(sc.Radius);
                col.center = sc.Offset;
                // 스크립트 변경 후 UpdateParameters — 검증·시메트리 해석 즉시 반영
                col.UpdateParameters();
                result.colliders.Add(col);
                created.Add(col);
            }
            return created;
        }
    }
}
