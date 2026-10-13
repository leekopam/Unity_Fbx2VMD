using System.Collections.Generic;
using System.Reflection;
using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;
using VRM;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// VRM 스프링본 → MC2 변환기 — 파라미터 매핑과 클로스 생성의 단위 검증.
    /// 제작자 튜닝이 손실 없이 값형 파라미터로 이식되는지 확인한다.
    /// </summary>
    public class VrmSpringBoneConverterTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                Object.DestroyImmediate(go);
            created.Clear();
        }

        Transform Bone(string name, Transform parent)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ---------- Phase 1: 파라미터 매핑 ----------

        [Test]
        public void Given_StiffSpring_When_ApplyParams_Then_HighRestoration()
        {
            var s = new ClothSerializeData();

            // VRM 뻣뻣한 스프링 (stiffness 4.0 → MC2 상한)
            VrmSpringBoneConverter.ApplyParams(s,
                stiffnessForce: 4.0f, dragForce: 0.8f,
                gravityPower: 0.5f, gravityDir: Vector3.down, hitRadius: 0.05f);

            Assert.That(s.angleRestorationConstraint.useAngleRestoration, Is.True);
            Assert.That(s.angleRestorationConstraint.stiffness.value,
                Is.EqualTo(1.0f), "stiffnessForce 4.0 × 0.25 = 1.0 (클램프)");
            Assert.That(s.damping.value, Is.EqualTo(0.8f));
            Assert.That(s.gravity, Is.EqualTo(0.5f));
            Assert.That((Vector3)s.gravityDirection, Is.EqualTo(Vector3.down));
            Assert.That(s.radius.value, Is.EqualTo(0.05f));
        }

        [Test]
        public void Given_SoftSpring_When_ApplyParams_Then_LowRestoration()
        {
            var s = new ClothSerializeData();

            // VRM 부드러운 스프링
            VrmSpringBoneConverter.ApplyParams(s,
                stiffnessForce: 0.5f, dragForce: 0.1f,
                gravityPower: 0f, gravityDir: Vector3.down, hitRadius: 0.02f);

            Assert.That(s.angleRestorationConstraint.stiffness.value,
                Is.EqualTo(0.125f), "stiffnessForce 0.5 × 0.25");
            Assert.That(s.damping.value, Is.EqualTo(0.1f));
            Assert.That(s.gravity, Is.EqualTo(0f));
        }

        [Test]
        public void Given_ZeroGravityDir_When_ApplyParams_Then_DownFallback()
        {
            var s = new ClothSerializeData();

            VrmSpringBoneConverter.ApplyParams(s,
                stiffnessForce: 1f, dragForce: 0.4f,
                gravityPower: 0f, gravityDir: Vector3.zero, hitRadius: 0.02f);

            Assert.That((Vector3)s.gravityDirection, Is.EqualTo(Vector3.down),
                "영벡터 방향은 정규화 불가 — 아래 방향으로 폴백");
        }

        [Test]
        public void Given_AnyParams_When_ApplyParams_Then_SafetyValuesApplied()
        {
            var s = new ClothSerializeData();

            VrmSpringBoneConverter.ApplyParams(s,
                stiffnessForce: 1f, dragForce: 0.4f,
                gravityPower: 0f, gravityDir: Vector3.down, hitRadius: 0.02f);

            Assert.That(s.inertiaConstraint.teleportMode,
                Is.EqualTo(InertiaConstraint.TeleportMode.Keep));
            Assert.That(s.colliderCollisionConstraint.friction,
                Is.EqualTo(0.05f));
        }

        // ---------- Phase 2: 클로스 변환 ----------

        [Test]
        public void Given_SpringWithChain_When_ConvertAll_Then_BoneClothCreatedAndSpringDisabled()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);

            var chest = Bone("Chest", model.transform);
            var tie1 = Bone("tie_01", chest);
            var tie2 = Bone("tie_02", tie1);
            Bone("tie_03", tie2);

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { tie1 };
            spring.m_stiffnessForce = 2.0f;
            spring.m_dragForce = 0.5f;
            spring.m_hitRadius = 0.03f;

            var report = new List<string>();
            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, report);

            Assert.That(result.cloths.Count, Is.EqualTo(1));
            Assert.That(result.disabledSprings, Contains.Item(spring));
            Assert.That(spring.enabled, Is.False, "원본 스프링은 비활성 (롤백 위해 삭제 아님)");

            var cloth = result.cloths[0];
            Assert.That(cloth.SerializeData.clothType,
                Is.EqualTo(ClothProcess.ClothType.BoneCloth));
            Assert.That(cloth.SerializeData.rootBones, Contains.Item(tie1));
            Assert.That(cloth.SerializeData.damping.value, Is.EqualTo(0.5f));
            Assert.That(cloth.name, Does.StartWith("MC2Cloth_Vrm"));
        }

        [Test]
        public void Given_ColliderGroup_When_ConvertAll_Then_SphereCollidersCopied()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);

            var head = Bone("Head", model.transform);
            var hair = Bone("hair_01", head);
            Bone("hair_02", hair); // 리프 루트는 변환 제외 — 체인 형태 필요

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { hair };

            var groupGo = head.gameObject;
            var group = groupGo.AddComponent<VRMSpringBoneColliderGroup>();
            group.Colliders = new[]
            {
                new VRMSpringBoneColliderGroup.SphereCollider
                    { Offset = new Vector3(0.1f, 0.05f, 0f), Radius = 0.12f },
                new VRMSpringBoneColliderGroup.SphereCollider
                    { Offset = Vector3.zero, Radius = 0.08f },
            };
            spring.ColliderGroups = new[] { group };

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.colliders.Count, Is.EqualTo(2));
            var cloth = result.cloths[0];
            Assert.That(cloth.SerializeData.colliderCollisionConstraint.colliderList,
                Has.Count.EqualTo(2), "변환 콜라이더가 클로스에 매핑");
            foreach (var col in result.colliders)
            {
                Assert.That(col, Is.TypeOf<MagicaSphereCollider>());
                Assert.That(col.name, Does.StartWith("MC2Col_Vrm"));
            }
        }

        [Test]
        public void Given_EmptyRootBones_When_ConvertAll_Then_SkippedAndSpringStays()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform>();

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.cloths.Count, Is.EqualTo(0));
            Assert.That(result.disabledSprings, Is.Empty);
            Assert.That(spring.enabled, Is.True,
                "변환된 게 없으면 원본 스프링을 비활성하지 않는다");
        }

        [Test]
        public void Given_NoSprings_When_ConvertAll_Then_EmptyResult()
        {
            var model = new GameObject("PlainModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);
            Bone("bone", model.transform);

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.cloths.Count, Is.EqualTo(0));
            Assert.That(result.colliders.Count, Is.EqualTo(0));
        }

        // ---------- 리뷰 반영 케이스 ----------

        [Test]
        public void Given_DisabledSpring_When_ConvertAll_Then_SkippedNotRevived()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);
            var chest = Bone("Chest", model.transform);
            var tie = Bone("tie_01", chest);
            Bone("tie_02", tie);

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { tie };
            spring.enabled = false; // 제작자가 끈 스프링 — 물리 없음이 의도

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.cloths.Count, Is.EqualTo(0),
                "꺼진 스프링을 MC2로 부활시키면 제작자 의도 위반");
            Assert.That(result.disabledSprings, Is.Empty);
            Assert.That(spring.enabled, Is.False);
        }

        [Test]
        public void Given_InactiveSpringObject_When_ConvertAll_Then_Skipped()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);
            var chest = Bone("Chest", model.transform);
            var tie = Bone("tie_01", chest);
            Bone("tie_02", tie);

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { tie };
            model.SetActive(false); // 오브젝트 단위 비활성도 시뮬레이션 불가

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.cloths.Count, Is.EqualTo(0));
            Assert.That(result.disabledSprings, Is.Empty);
        }

        [Test]
        public void Given_LeafOnlyRoots_When_ConvertAll_Then_SkippedDeadChain()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);
            var chest = Bone("Chest", model.transform);
            var leaf = Bone("leaf_01", chest); // 자식 없음 — MC2에선 전 입자 고정

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { leaf };

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.cloths.Count, Is.EqualTo(0),
                "리프 단독 루트는 죽은 클로스 — 생성하지 않는다");
            Assert.That(spring.enabled, Is.True, "변환된 게 없으면 원본 유지");
        }

        [Test]
        public void Given_RerunWithoutRestore_When_ConvertAll_Then_SecondRunConvertsNothing()
        {
            // 계약: 재실행 전 호출부(CharacterPhysicsSetup.ClearGenerated)가
            // 비활성 스프링을 복원해야 한다. 복원 없는 2회 실행은 변환하지 않는다 —
            // 물리 소실이 아니라 복원 책임이 호출부에 있음을 문서화한다.
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);
            var chest = Bone("Chest", model.transform);
            var tie = Bone("tie_01", chest);
            Bone("tie_02", tie);

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { tie };

            var first = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());
            Assert.That(first.cloths.Count, Is.EqualTo(1));

            var second = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());
            Assert.That(second.cloths.Count, Is.EqualTo(0),
                "복원 없는 재실행은 추가 변환 없음 — 멱등");

            // 호출부 복원 시나리오 — 스프링 재활성 후 3회 실행은 다시 변환
            spring.enabled = true;
            var third = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());
            Assert.That(third.cloths.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_CenterAndUpdateType_When_ConvertAll_Then_UnsupportedRecorded()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            var center = new GameObject("Center");
            created.Add(model);
            created.Add(setupRoot);
            created.Add(center);
            var chest = Bone("Chest", model.transform);
            var tie = Bone("tie_01", chest);
            Bone("tie_02", tie);

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { tie };
            spring.m_center = center.transform;
            spring.m_updateType = VRMSpringBone.SpringBoneUpdateType.FixedUpdate;

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.unsupported, Has.Some.Contains("m_center"));
            Assert.That(result.unsupported, Has.Some.Contains("m_updateType"));
        }

        [Test]
        public void Given_SharedColliderGroup_When_ConvertAll_Then_CollidersNotDuplicated()
        {
            var model = new GameObject("VrmModel");
            var setupRoot = new GameObject("Setup");
            created.Add(model);
            created.Add(setupRoot);
            var head = Bone("Head", model.transform);
            var hair1 = Bone("hair_01", head);
            var hair2 = Bone("hair_02", head);
            Bone("tip_01", hair1);
            Bone("tip_02", hair2);

            var group = head.gameObject.AddComponent<VRMSpringBoneColliderGroup>();
            group.Colliders = new[]
            {
                new VRMSpringBoneColliderGroup.SphereCollider
                    { Offset = Vector3.zero, Radius = 0.1f },
            };

            var springA = model.AddComponent<VRMSpringBone>();
            springA.RootBones = new List<Transform> { hair1 };
            springA.ColliderGroups = new[] { group };
            var springB = model.AddComponent<VRMSpringBone>();
            springB.RootBones = new List<Transform> { hair2 };
            springB.ColliderGroups = new[] { group }; // 같은 그룹 공유

            var result = VrmSpringBoneConverter.ConvertAll(
                setupRoot.transform, model.transform, 1f, new List<string>());

            Assert.That(result.cloths.Count, Is.EqualTo(2));
            Assert.That(result.colliders.Count, Is.EqualTo(1),
                "공유 그룹은 1회만 변환 — 두 클로스가 같은 콜라이더를 참조");
            Assert.That(result.cloths[0].SerializeData.colliderCollisionConstraint
                .colliderList[0], Is.SameAs(result.colliders[0]));
        }

        [Test]
        public void Given_ConvertedSpring_When_ClearGenerated_Then_SpringRestored()
        {
            var model = new GameObject("VrmModel");
            var setupGo = new GameObject("Setup");
            created.Add(model);
            created.Add(setupGo);
            var head = Bone("Head", model.transform);
            var hair = Bone("hair_01", head);
            Bone("tip_01", hair);

            var spring = model.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { hair };

            var result = VrmSpringBoneConverter.ConvertAll(
                setupGo.transform, model.transform, 1f, null);
            Assert.That(spring.enabled, Is.False);

            // SetupInternal이 채우는 경로와 동일 — 직렬화 필드에 변환분을 싣는다.
            var setup = setupGo.AddComponent<CharacterPhysicsSetup>();
            var field = typeof(CharacterPhysicsSetup).GetField(
                "convertedSprings", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            ((List<VRMSpringBone>)field.GetValue(setup))
                .AddRange(result.disabledSprings);

            setup.ClearGenerated();

            Assert.That(spring.enabled, Is.True,
                "ClearGenerated가 변환으로 끈 스프링을 복원해야 함 — 생략 시 재실행·" +
                "GuardOnly 복귀에서 물리 소실");
        }
    }
}
