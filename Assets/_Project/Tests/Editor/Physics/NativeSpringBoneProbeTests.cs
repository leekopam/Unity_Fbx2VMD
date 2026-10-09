using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;
using UnityEngine;
using VRM;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// 네이티브 스프링본 소유권 프로브 — VRM 캐릭터의 기존 물리와
    /// MC2 이중 시뮬레이션을 막는 가드의 단위 검증.
    /// </summary>
    public class NativeSpringBoneProbeTests
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

        [Test]
        public void Given_VrmSpringBone_When_Collecting_Then_OwnsRootSubtree()
        {
            var root = new GameObject("VrmModel");
            created.Add(root);
            var chest = Bone("Chest", root.transform);
            var tie1 = Bone("tie_01", chest);
            var tie2 = Bone("tie_02", tie1);
            var tie3 = Bone("tie_03", tie2);
            var other = Bone("hair_01", chest);

            var spring = root.AddComponent<VRMSpringBone>();
            spring.RootBones = new List<Transform> { tie1 };

            var owned = NativeSpringBoneProbe.CollectOwnedBones(root.transform);

            Assert.That(owned.Contains(tie1), Is.True);
            Assert.That(owned.Contains(tie2), Is.True, "루트의 하위 체인도 소유입니다.");
            Assert.That(owned.Contains(tie3), Is.True);
            Assert.That(owned.Contains(other), Is.False, "무관한 체인은 소유가 아닙니다.");
            Assert.That(owned.Contains(chest), Is.False);
        }

        [Test]
        public void Given_NoSpringBone_When_Collecting_Then_Empty()
        {
            var root = new GameObject("PlainModel");
            created.Add(root);
            Bone("anything", root.transform);

            var owned = NativeSpringBoneProbe.CollectOwnedBones(root.transform);

            Assert.That(owned.Count, Is.EqualTo(0));
        }

        [Test]
        public void Given_EmptyRootBones_When_Collecting_Then_Empty()
        {
            var root = new GameObject("VrmModel");
            created.Add(root);
            Bone("bone", root.transform);
            root.AddComponent<VRMSpringBone>(); // RootBones 기본 빈 리스트

            var owned = NativeSpringBoneProbe.CollectOwnedBones(root.transform);

            Assert.That(owned.Count, Is.EqualTo(0));
        }
    }
}
