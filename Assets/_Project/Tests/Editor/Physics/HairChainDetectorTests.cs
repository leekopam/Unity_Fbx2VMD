using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class HairChainDetectorTests
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

        /// <summary>YYB Miku식 계층: head 직하에 joint_hidariHairA1 → A2 → A3 체인.</summary>
        [Test]
        public void Given_YybHairHierarchy_When_Detecting_Then_FindsChains()
        {
            var head = Bone("joint_Head", null, Vector3.zero);
            var a1 = Bone("joint_hidariHairA1", head, new Vector3(0.1f, 0.1f, 0f));
            var a2 = Bone("joint_hidariHairA2", a1, new Vector3(0f, -0.1f, 0f));
            Bone("joint_hidariHairA3", a2, new Vector3(0f, -0.1f, 0f));

            var result = HairChainDetector.Detect(head, null);

            Assert.That(result.chains.Count, Is.EqualTo(1), "머리카락 체인 1개를 찾아야 합니다.");
            Assert.That(result.chains[0].rootBone, Is.EqualTo(a1));
            Assert.That(result.chains[0].bones.Count, Is.EqualTo(3));
            // 위치상 옆+위 → Tail 부위
            Assert.That(result.chains[0].part, Is.EqualTo(HairPart.Tail));
            Assert.That(result.chains[0].worldLength, Is.GreaterThan(0.15f));
        }

        [Test]
        public void Given_SingleBoneChain_When_Detecting_Then_SkipsTooShort()
        {
            var head = Bone("Head", null, Vector3.zero);
            Bone("hair_stub", head, new Vector3(0f, 0.1f, 0f)); // 자식 없는 단일 본

            var result = HairChainDetector.Detect(head, null);
            Assert.That(result.chains.Count, Is.EqualTo(0), "본 1개짜리 체인은 물리 대상이 아닙니다.");
        }

        [Test]
        public void Given_BodyBonePredicate_When_Detecting_Then_ExcludesBodyBones()
        {
            var head = Bone("Head", null, Vector3.zero);
            var neckThing = Bone("joint_neckAcc", head, Vector3.zero);
            var hair = Bone("hair_back_1", neckThing, new Vector3(0f, 0f, -0.05f));
            Bone("hair_back_2", hair, new Vector3(0f, -0.1f, -0.05f));

            // neckThing이 신체 본으로 표시되면 하위 체인도 잘리지 않음 — 신체 본 자체가 탐색 중단
            var bodySet = new HashSet<Transform> { neckThing };
            var result = HairChainDetector.Detect(head, t => bodySet.Contains(t));

            Assert.That(result.chains.Count, Is.EqualTo(0), "신체 본 하위는 탐색하지 않습니다.");
        }

        [Test]
        public void Given_ExplicitRoots_When_Detecting_Then_UsesThem()
        {
            var head = Bone("Head", null, Vector3.zero);
            var custom = Bone("myhair_1", head, Vector3.zero);
            Bone("myhair_2", custom, new Vector3(0f, -0.1f, 0f));
            var other = Bone("hair_other_1", head, new Vector3(0.05f, 0f, 0f));
            Bone("hair_other_2", other, new Vector3(0f, -0.1f, 0f));

            var result = HairChainDetector.Detect(head, null, new[] { custom });

            Assert.That(result.chains.Count, Is.EqualTo(1), "명시 지정 루트만 사용해야 합니다.");
            Assert.That(result.chains[0].rootBone, Is.EqualTo(custom));
        }

        [Test]
        public void Given_NonHairNonBody_When_Detecting_Then_SkipsWithoutCrash()
        {
            var head = Bone("Head", null, Vector3.zero);
            Bone("random_node", head, new Vector3(0.05f, 0f, 0f)); // 자식 없는 미분류

            var result = HairChainDetector.Detect(head, null);
            Assert.That(result.chains.Count, Is.EqualTo(0));
            Assert.That(result.skipped.Count, Is.EqualTo(1));
        }
    }
}
