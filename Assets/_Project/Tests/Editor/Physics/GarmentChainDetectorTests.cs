using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class GarmentChainDetectorTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                Object.DestroyImmediate(go);
            created.Clear();
        }

        Transform Bone(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static Dictionary<Transform, List<Vector3>> Verts(params Transform[] bones)
        {
            var map = new Dictionary<Transform, List<Vector3>>();
            foreach (var b in bones)
                map[b] = new List<Vector3>(new Vector3[5]);
            return map;
        }

        [Test]
        public void Given_UnnamedSkinnedChain_When_Detecting_Then_Adopts()
        {
            var chest = Bone("Chest", null, Vector3.zero);
            var a1 = Bone("bone001", chest, Vector3.down * 0.05f);
            var a2 = Bone("bone002", a1, Vector3.down * 0.05f);
            var a3 = Bone("bone003", a2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, Verts(a1, a2, a3));

            Assert.That(result.roots, Contains.Item(a1));
        }

        [Test]
        public void Given_ChainWithoutSkinning_When_Detecting_Then_Skips()
        {
            var chest = Bone("Chest", null, Vector3.zero);
            var a1 = Bone("bone001", chest, Vector3.down * 0.05f);
            var a2 = Bone("bone002", a1, Vector3.down * 0.05f);
            Bone("bone003", a2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, new Dictionary<Transform, List<Vector3>>());

            Assert.That(result.roots.Count, Is.EqualTo(0),
                "스키닝 버텍스가 없는 체인은 보조 본일 수 있어 채택하지 않습니다.");
            Assert.That(result.report.Exists(r => r.Contains("스키닝")), Is.True);
        }

        [Test]
        public void Given_TwoBoneChainAndLeaf_When_Detecting_Then_AdoptsChainOnly()
        {
            var chest = Bone("Chest", null, Vector3.zero);
            var a1 = Bone("bone001", chest, Vector3.down * 0.05f);
            Bone("bone002", a1, Vector3.down * 0.05f); // 루트+자식1 = 본 2개는 허용

            // 본 1개(자식 없음)는 후보가 아니다
            Bone("leaf_bone", chest, Vector3.down * 0.1f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, Verts(a1));

            Assert.That(result.roots, Contains.Item(a1)); // 본 2개 체인은 채택
            Assert.That(result.roots.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_MechanismChain_When_Detecting_Then_Skips()
        {
            var chest = Bone("Chest", null, Vector3.zero);
            var t1 = Bone("arm_twist_01", chest, Vector3.down * 0.05f);
            var t2 = Bone("arm_twist_02", t1, Vector3.down * 0.05f);
            Bone("arm_twist_03", t2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, Verts(t1, t2));

            Assert.That(result.roots.Count, Is.EqualTo(0),
                "트위스트 등 기구 본은 물리 대상이 아닙니다.");
        }

        [Test]
        public void Given_SkirtNamedChain_When_Detecting_Then_Skips()
        {
            var hips = Bone("Hips", null, Vector3.zero);
            var s1 = Bone("skirt_a_01", hips, Vector3.down * 0.05f);
            var s2 = Bone("skirt_a_02", s1, Vector3.down * 0.05f);
            Bone("skirt_a_03", s2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { hips }, _ => false, _ => false, Verts(s1, s2));

            Assert.That(result.roots.Count, Is.EqualTo(0),
                "스커트 본은 스커트 클로스 경로가 담당합니다.");
        }

        [Test]
        public void Given_ClassifiedButUnclaimedChain_When_Detecting_Then_Adopts()
        {
            // 이름은 분류되지만 이름 경로가 미채택한 체인(몸측 ribbon 등) — 실제 파이프라인에서
            // 이름 경로는 Accessory 루트만 채택하므로 여기서 받아야 사라지지 않는다
            var chest = Bone("Chest", null, Vector3.zero);
            var r1 = Bone("ribbon_01", chest, Vector3.down * 0.05f);
            var r2 = Bone("ribbon_02", r1, Vector3.down * 0.05f);
            Bone("ribbon_03", r2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, Verts(r1, r2));

            Assert.That(result.roots, Contains.Item(r1));
        }

        [Test]
        public void Given_NameClaimedChain_When_Detecting_Then_Skips()
        {
            // 이름 경로가 실제로 채택한 루트(isClaimed=true)는 토폴로지가 다시 잡지 않는다
            var chest = Bone("Chest", null, Vector3.zero);
            var r1 = Bone("ribbon_01", chest, Vector3.down * 0.05f);
            var r2 = Bone("ribbon_02", r1, Vector3.down * 0.05f);
            Bone("ribbon_03", r2, Vector3.down * 0.05f);
            var claimed = new HashSet<Transform> { r1, r2 };

            var result = GarmentChainDetector.Detect(
                new[] { chest }, t => claimed.Contains(t), _ => false, Verts(r1, r2));

            Assert.That(result.roots.Count, Is.EqualTo(0));
        }

        [Test]
        public void Given_ClaimedChain_When_Detecting_Then_Skips()
        {
            var chest = Bone("Chest", null, Vector3.zero);
            var a1 = Bone("bone001", chest, Vector3.down * 0.05f);
            var a2 = Bone("bone002", a1, Vector3.down * 0.05f);
            Bone("bone003", a2, Vector3.down * 0.05f);

            var claimed = new HashSet<Transform> { a1 };
            var result = GarmentChainDetector.Detect(
                new[] { chest }, t => claimed.Contains(t), _ => false, Verts(a1, a2));

            Assert.That(result.roots.Count, Is.EqualTo(0),
                "신체 본·기존 체인·네이티브 소유 본으로 이미 소유된 루트는 건너뜁니다.");
        }

        [Test]
        public void Given_LimbNamedChain_When_Detecting_Then_Skips()
        {
            var chest = Bone("Chest", null, Vector3.zero);
            var a1 = Bone("arm_m_01", chest, Vector3.down * 0.05f);
            var a2 = Bone("arm_m_02", a1, Vector3.down * 0.05f);
            Bone("arm_m_03", a2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, Verts(a1, a2));

            Assert.That(result.roots.Count, Is.EqualTo(0),
                "팔/다리 명명 본은 장식물이 아니라 골격 연장입니다.");
        }

        [Test]
        public void Given_ChainContainingBodyBone_When_Detecting_Then_Skips()
        {
            // 이름이 무관해도 서브트리에 신체 본이 있으면 골격 연장 — 스킵
            var chest = Bone("Chest", null, Vector3.zero);
            var sub = Bone("subsidiary_01", chest, Vector3.down * 0.05f);
            var sub2 = Bone("subsidiary_02", sub, Vector3.down * 0.05f);
            var hand = Bone("HandBone", sub2, Vector3.down * 0.05f);
            Bone("subsidiary_03", hand, Vector3.down * 0.05f);

            var bodySet = new HashSet<Transform> { hand };
            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, t => bodySet.Contains(t), Verts(sub, sub2));

            Assert.That(result.roots.Count, Is.EqualTo(0),
                "서브트리에 신체 본을 포함하는 체인은 골격 연장입니다.");
        }

        [Test]
        public void Given_WhitelistedGarmentName_When_Detecting_Then_Adopts()
        {
            // necklace는 IsExcluded의 'neck' 패턴에 걸리지만 몸쪽 부착이 정상인 장식물 —
            // 이름 경로도 제외하므로 폴백이 화이트리스트로 살려야 한다
            var chest = Bone("Chest", null, Vector3.zero);
            var n1 = Bone("necklace_01", chest, Vector3.down * 0.05f);
            var n2 = Bone("necklace_02", n1, Vector3.down * 0.05f);
            Bone("necklace_03", n2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, Verts(n1, n2));

            Assert.That(result.roots, Contains.Item(n1));
        }

        [Test]
        public void Given_ExcludedFaceName_When_Detecting_Then_Skips()
        {
            // 얼굴 계열 제외는 화이트리스트 없이 유지 — face/jaw 등은 장식물이 아니다
            var neck = Bone("Neck", null, Vector3.zero);
            var f1 = Bone("face_extra_01", neck, Vector3.down * 0.05f);
            var f2 = Bone("face_extra_02", f1, Vector3.down * 0.05f);
            Bone("face_extra_03", f2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { neck }, _ => false, _ => false, Verts(f1, f2));

            Assert.That(result.roots.Count, Is.EqualTo(0));
        }

        [Test]
        public void Given_NestedChains_When_Detecting_Then_OnlyTopRootAdopted()
        {
            var chest = Bone("Chest", null, Vector3.zero);
            var a1 = Bone("node_01", chest, Vector3.down * 0.05f);
            var a2 = Bone("node_02", a1, Vector3.down * 0.05f);
            Bone("node_03", a2, Vector3.down * 0.05f);

            var result = GarmentChainDetector.Detect(
                new[] { chest }, _ => false, _ => false, Verts(a1, a2));

            Assert.That(result.roots.Count, Is.EqualTo(1));
            Assert.That(result.roots[0], Is.EqualTo(a1));
        }
    }
}
