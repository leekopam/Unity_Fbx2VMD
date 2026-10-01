using System.Collections.Generic;
using System.Linq;
using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class SkirtBoneClothBuilderTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
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

        /// <summary>허리 주변에 스커트 체인 루트 N개를 둘레 방향으로 배치한 리그.</summary>
        (Transform root, Dictionary<Transform, int> depths, List<Transform> roots)
            BuildRingRig(int chainCount)
        {
            var root = new GameObject("Model");
            created.Add(root);
            var hips = Bone("Hips", root.transform, Vector3.up);
            var roots = new List<Transform>();
            for (int i = 0; i < chainCount; i++)
            {
                float ang = i * Mathf.PI * 2f / chainCount;
                var r = Bone($"スカート_{i}_0", hips,
                    new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.1f);
                Bone($"スカート_{i}_1", r, Vector3.down * 0.1f); // 깊이1
                roots.Add(r);
            }
            var depths = SkirtClothBuilder.CollectSkirtBoneDepths(root.transform);
            return (root.transform, depths, roots);
        }

        [Test]
        public void Given_ScrambledRoots_When_Ordered_Then_RingOrder()
        {
            var (root, depths, roots) = BuildRingRig(8);
            // 수집 순서와 무관하게 각도 순(인접=이웃 체인)으로 정렬돼야 한다
            var ordered = SkirtBoneClothBuilder.CollectChainRootsOrdered(root, depths);

            Assert.AreEqual(8, ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                // 각도 정렬 결과는 단조 증가 순환 — 인덱스 i와 i+1의 각도 차가 360/N 이하
                var inv = root.worldToLocalMatrix;
                float a0 = AngleOf(inv.MultiplyPoint3x4(ordered[i].position));
                float a1 = AngleOf(inv.MultiplyPoint3x4(ordered[(i + 1) % ordered.Count].position));
                float diff = Mathf.Repeat(a1 - a0, Mathf.PI * 2f);
                Assert.That(diff, Is.LessThan(Mathf.PI / 2f),
                    $"루트 {i}→{i+1} 각도 차 {Mathf.Rad2Deg * diff:F0}도 — 링 이탈 의심");
            }
        }

        static float AngleOf(Vector3 p) => Mathf.Atan2(p.z, p.x);

        [Test]
        public void Given_TwoRoots_When_CanBuildLoop_Then_False()
        {
            var (root, depths, roots) = BuildRingRig(2);
            var ordered = SkirtBoneClothBuilder.CollectChainRootsOrdered(root, depths);
            Assert.IsFalse(SkirtBoneClothBuilder.CanBuildLoop(ordered),
                "루트 2개는 링 면 불성립");
        }

        [Test]
        public void Given_RingRoots_When_Create_Then_SequentialLoopBoneCloth()
        {
            var (root, depths, roots) = BuildRingRig(6);
            var ordered = SkirtBoneClothBuilder.CollectChainRootsOrdered(root, depths);

            var cloth = SkirtBoneClothBuilder.Create(
                root, ordered, new List<ColliderComponent>(), true, 0.008f);

            Assert.IsNotNull(cloth);
            created.Add(cloth.gameObject);
            var sdata = cloth.SerializeData;
            Assert.AreEqual(ClothProcess.ClothType.BoneCloth, sdata.clothType);
            Assert.AreEqual(RenderSetupData.BoneConnectionMode.SequentialLoopMesh,
                sdata.connectionMode);
            Assert.AreEqual(ordered.Count, sdata.rootBones.Count);
            CollectionAssert.AreEqual(ordered, sdata.rootBones,
                "rootBones는 정렬된 루프 순서로 등록돼야 한다");
            Assert.IsTrue(sdata.motionConstraint.useBackstop);
            Assert.That(sdata.motionConstraint.backstopDistance.value,
                Is.EqualTo(0.008f).Within(0.0001f));
        }

        [Test]
        public void Given_NotEnoughRoots_When_Create_Then_Null()
        {
            var (root, depths, roots) = BuildRingRig(2);
            var ordered = SkirtBoneClothBuilder.CollectChainRootsOrdered(root, depths);

            var cloth = SkirtBoneClothBuilder.Create(
                root, ordered, new List<ColliderComponent>(), false, 0f);

            Assert.IsNull(cloth);
        }
    }
}
