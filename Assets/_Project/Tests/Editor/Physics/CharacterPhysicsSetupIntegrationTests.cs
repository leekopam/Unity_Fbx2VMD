using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// CharacterPhysicsSetup의 비 Humanoid(Generic) 경로 통합 검증.
    /// 이름 패턴 폴백으로 콜라이더+BoneCloth가 생성되어야 한다.
    /// </summary>
    public class CharacterPhysicsSetupIntegrationTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        Transform Bone(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go.transform;
        }

        /// <summary>Avatar 없는 Generic 릭 + 머리카락/장식물/스커트 본.</summary>
        GameObject BuildGenericModel()
        {
            var root = new GameObject("GenericModel");
            created.Add(root);
            root.AddComponent<Animator>(); // Avatar null → isHuman=false

            var hips = Bone("Hips", root.transform, new Vector3(0, 0.9f, 0));
            var spine = Bone("Spine", hips, new Vector3(0, 1.05f, 0));
            var chest = Bone("Chest", spine, new Vector3(0, 1.2f, 0));
            var neck = Bone("Neck", chest, new Vector3(0, 1.4f, 0));
            var head = Bone("Head", neck, new Vector3(0, 1.5f, 0));

            // 머리카락 체인 (머리 하위)
            var hair1 = Bone("hair_front_01", head, new Vector3(0, 1.55f, 0.05f));
            var hair2 = Bone("hair_front_02", hair1, new Vector3(0, 1.5f, 0.1f));
            Bone("hair_front_03", hair2, new Vector3(0, 1.45f, 0.1f));

            // 장식물 체인 (가슴 하위 — 머리 외 부착점)
            var acc1 = Bone("ribbon_chain_01", chest, new Vector3(0.05f, 1.25f, -0.05f));
            var acc2 = Bone("ribbon_chain_02", acc1, new Vector3(0.06f, 1.15f, -0.08f));
            Bone("ribbon_chain_03", acc2, new Vector3(0.07f, 1.05f, -0.1f));

            // 스커트 본 (경고 대상)
            var sk1 = Bone("skirt_01", hips, new Vector3(0.1f, 0.9f, 0));
            Bone("skirt_02", sk1, new Vector3(0.12f, 0.7f, 0));
            return root;
        }

        [Test]
        public void Given_GenericRig_When_Setup_Then_FallbackResolvesCollidersAndCloths()
        {
            var root = BuildGenericModel();
            var setup = root.AddComponent<CharacterPhysicsSetup>();
            setup.Setup();

            Assert.That(setup.generatedColliders.Count, Is.GreaterThanOrEqualTo(4),
                "폴백 해석으로 최소 상체 콜라이더가 생성되어야 합니다.\n" + setup.lastReport);
            Assert.That(setup.generatedCloths.Count, Is.GreaterThanOrEqualTo(2),
                "머리카락+장식물 클로스가 생성되어야 합니다.\n" + setup.lastReport);
            Assert.That(setup.generatedParts, Contains.Item(HairPart.Front));
            Assert.That(setup.generatedParts, Contains.Item(HairPart.Accessory));
            Assert.That(setup.lastReport, Does.Contain("폴백"));
            // 중첩된 장식물 본은 하나의 체인으로 묶여야 한다 (중복 루트 금지)
            Assert.That(setup.lastReport, Does.Contain("Accessory: 체인 1개"));
        }

        [Test]
        public void Given_GenericRig_When_Setup_Then_SkirtWarningReported()
        {
            var root = BuildGenericModel();
            var setup = root.AddComponent<CharacterPhysicsSetup>();
            setup.Setup();

            Assert.That(setup.lastReport, Does.Contain("스커트 본"));
        }

        [Test]
        public void Given_GenericRig_When_SetupTwice_Then_Idempotent()
        {
            var root = BuildGenericModel();
            var setup = root.AddComponent<CharacterPhysicsSetup>();
            setup.Setup();
            int firstColliders = setup.generatedColliders.Count;
            int firstCloths = setup.generatedCloths.Count;

            setup.Setup();

            Assert.That(setup.generatedColliders.Count, Is.EqualTo(firstColliders));
            Assert.That(setup.generatedCloths.Count, Is.EqualTo(firstCloths));
        }

        /// <summary>
        /// 완전성 판정 회귀: 스커트 본이 있는데 MeshCloth가 없으면 불완전으로
        /// 판정해야 한다 — 씬 직렬화 헤어 클로스만 유효한 플레이에서 스킵돼
        /// 런타임 전용 스커트가 영구 누락되는 사례의 재발 방지.
        /// </summary>
        [Test]
        public void Given_SkirtBonesButNoMeshCloth_Then_SetupIsIncomplete()
        {
            var root = BuildGenericModel();
            var setup = root.AddComponent<CharacterPhysicsSetup>();
            var m = typeof(CharacterPhysicsSetup).GetMethod("IsSetupComplete",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null);

            Assert.That((bool)m.Invoke(setup, null), Is.False,
                "스커트 본 존재 + MeshCloth 부재 = 불완전으로 판정해야 합니다.");

            setup.autoSkirtCloth = false;
            Assert.That((bool)m.Invoke(setup, null), Is.True,
                "autoSkirtCloth 꺼짐이면 스커트 없어도 완전입니다.");
        }

        const string PronamaChanPrefabPath =
            "Assets/Plugins/VMDRecorderSample/Models/PronamaChan/PronamaChan.prefab";

        /// <summary>
        /// Phase 3 실모델 검증: PronamaChan은 Avatar 없는 Generic 리그라
        /// 이름 패턴 폴백 경로(center→Hips, *_L/*_R 측면)를 전부 통과해야 한다.
        /// </summary>
        [Test]
        public void Given_PronamaChanPrefab_When_Setup_Then_FallbackResolvesRealModel()
        {
            var prefab = UnityEditor.AssetDatabase
                .LoadAssetAtPath<GameObject>(PronamaChanPrefabPath);
            if (prefab == null)
                Assert.Ignore($"로컬 fixture가 없습니다: {PronamaChanPrefabPath}");

            var inst = Object.Instantiate(prefab);
            created.Add(inst);
            var animator = inst.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.isHuman, Is.False,
                "PronamaChan은 Generic 리그여야 폴백 경로 검증이 됩니다.");

            var map = NamePatternBoneResolver.Resolve(animator.transform);
            Assert.That(map.ContainsKey(HumanBodyBones.Head), Is.True,
                "이름 패턴으로 Head를 해석해야 합니다.");
            Assert.That(map.ContainsKey(HumanBodyBones.Hips), Is.True,
                "이름 패턴으로 Hips(center/lower_body)를 해석해야 합니다.");
            Assert.That(map.ContainsKey(HumanBodyBones.LeftUpperArm), Is.True,
                "arm_L 계열 이름으로 좌측 상완을 해석해야 합니다.");

            var setup = inst.AddComponent<CharacterPhysicsSetup>();
            setup.Setup();

            Assert.That(setup.generatedColliders.Count, Is.GreaterThanOrEqualTo(4),
                "실모델에서 최소 상체 콜라이더가 생성되어야 합니다.\n" + setup.lastReport);
            Assert.That(setup.generatedCloths.Count, Is.GreaterThanOrEqualTo(1),
                "실모델 머리카락/장식물 체인으로 클로스가 생성되어야 합니다.\n" + setup.lastReport);
        }
    }
}
