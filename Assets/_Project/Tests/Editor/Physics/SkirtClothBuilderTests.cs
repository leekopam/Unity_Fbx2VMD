using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class SkirtClothBuilderTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly List<Mesh> meshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            foreach (var m in meshes)
                if (m != null) Object.DestroyImmediate(m);
            meshes.Clear();
        }

        Transform Bone(string name, Transform parent)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>허리 아래 스커트 체인 2개(각 깊이 0→1→2) + 비스커트 다리 본을 만든다.</summary>
        (Transform root, Dictionary<Transform, int> depths, Transform[] chain0, Transform[] chain1, Transform leg)
            BuildSkirtRig()
        {
            var root = new GameObject("Model");
            created.Add(root);
            var hips = Bone("Hips", root.transform);
            var c0a = Bone("スカート_0_0", hips);
            var c0b = Bone("スカート_0_1", c0a);
            var c0c = Bone("スカート_0_2", c0b);
            var c1a = Bone("skirt_1_0", hips);
            var c1b = Bone("skirt_1_1", c1a);
            var leg = Bone("UpperLegL", hips);
            var depths = SkirtClothBuilder.CollectSkirtBoneDepths(root.transform);
            return (root.transform, depths,
                new[] { c0a, c0b, c0c }, new[] { c1a, c1b }, leg);
        }

        [Test]
        public void Given_ChainHierarchy_When_CollectDepths_Then_DepthFollowsChain()
        {
            var (root, depths, chain0, chain1, leg) = BuildSkirtRig();
            Assert.AreEqual(5, depths.Count);
            Assert.AreEqual(0, depths[chain0[0]]);
            Assert.AreEqual(1, depths[chain0[1]]);
            Assert.AreEqual(2, depths[chain0[2]]);
            Assert.AreEqual(0, depths[chain1[0]]);
            Assert.AreEqual(1, depths[chain1[1]]);
            Assert.IsFalse(depths.ContainsKey(leg), "비스커트 본은 제외");
        }

        /// <summary>버텍스 4개 메시: 각각 루트 본/깊은 본/다리 본/혼합 가중치로 바인딩.</summary>
        SkinnedMeshRenderer MakeSmr(Transform[] bones, BoneWeight[] weights, int vertexCount)
        {
            var go = new GameObject("SkirtMesh");
            created.Add(go);
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            meshes.Add(mesh);
            mesh.vertices = new Vector3[vertexCount];
            mesh.triangles = new int[0];
            mesh.boneWeights = weights;
            mesh.bindposes = new Matrix4x4[bones.Length];
            smr.bones = bones;
            smr.sharedMesh = mesh;
            return smr;
        }

        static BoneWeight W(int bone, float weight)
        {
            return new BoneWeight { boneIndex0 = bone, weight0 = weight };
        }

        [Test]
        public void Given_SkinnedVerts_When_BuildAttributes_Then_DepthDecidesFixedMove()
        {
            var (root, depths, chain0, chain1, leg) = BuildSkirtRig();
            // bones[]: 0=스커트 루트, 1=스커트 깊이2, 2=다리
            var bones = new[] { chain0[0], chain0[2], leg };
            var weights = new[]
            {
                W(0, 1f),            // 루트 본 → Fixed
                W(1, 1f),            // 깊이 2 → Move
                W(2, 1f),            // 다리 → Invalid
                new BoneWeight        // 스커트 0.4 + 다리 0.6 → 임계 미만 → Invalid
                {
                    boneIndex0 = 1, weight0 = 0.4f,
                    boneIndex1 = 2, weight1 = 0.6f,
                },
            };
            var smr = MakeSmr(bones, weights, 4);

            var attrs = SkirtClothBuilder.BuildVertexAttributes(
                smr, depths, 0.5f, 0, out int fixedCount, out int moveCount);

            Assert.AreEqual(4, attrs.Length, "배열 길이는 메시 버텍스 수와 동일 (MC2 계약)");
            Assert.IsTrue(attrs[0].IsFixed());
            Assert.IsTrue(attrs[1].IsMove());
            Assert.IsTrue(attrs[2].IsInvalid());
            Assert.IsTrue(attrs[3].IsInvalid(), "스커트 가중치 0.4 < 0.5 — 본 스키닝 유지");
            Assert.AreEqual(1, fixedCount);
            Assert.AreEqual(1, moveCount);
        }

        [Test]
        public void Given_DeeperFixedDepth_When_BuildAttributes_Then_RootAndChildFixed()
        {
            var (root, depths, chain0, chain1, leg) = BuildSkirtRig();
            var bones = new[] { chain0[0], chain0[1], chain0[2] };
            var weights = new[] { W(0, 1f), W(1, 1f), W(2, 1f) };
            var smr = MakeSmr(bones, weights, 3);

            var attrs = SkirtClothBuilder.BuildVertexAttributes(
                smr, depths, 0.5f, 1, out int fixedCount, out int moveCount);

            Assert.IsTrue(attrs[0].IsFixed());
            Assert.IsTrue(attrs[1].IsFixed(), "fixedChainDepth=1 → 깊이1까지 고정");
            Assert.IsTrue(attrs[2].IsMove());
            Assert.AreEqual(2, fixedCount);
            Assert.AreEqual(1, moveCount);
        }

        [Test]
        public void Given_NoSkirtVerts_When_Create_Then_NoCloth()
        {
            var (root, depths, chain0, chain1, leg) = BuildSkirtRig();
            var bones = new[] { leg };
            var smr = MakeSmr(bones, new[] { W(0, 1f) }, 1);

            var result = SkirtClothBuilder.Create(
                root, new[] { smr }, depths, new List<ColliderComponent>(), 0.6f, 0.5f, 0);

            Assert.IsNull(result.cloth);
            Assert.AreEqual(0, result.moveVertexCount + result.fixedVertexCount);
        }

        [Test]
        public void Given_SkirtVerts_When_Create_Then_MeshClothWithAttributes()
        {
            var (root, depths, chain0, chain1, leg) = BuildSkirtRig();
            var bones = new[] { chain0[0], chain0[2] };
            var weights = new[] { W(0, 1f), W(1, 1f) };
            var smr = MakeSmr(bones, weights, 2);

            var result = SkirtClothBuilder.Create(
                root, new[] { smr }, depths, new List<ColliderComponent>(), 0.6f, 0.5f, 0);

            Assert.IsNotNull(result.cloth);
            created.Add(result.cloth.gameObject);
            var sdata = result.cloth.SerializeData;
            Assert.AreEqual(ClothProcess.ClothType.MeshCloth, sdata.clothType);
            Assert.AreEqual(1, sdata.sourceRenderers.Count);
            Assert.AreEqual(1, result.cloth.GetSerializeData2().vertexAttributeList.Count);
            Assert.AreEqual(2, result.cloth.GetSerializeData2().vertexAttributeList[0].Length);
            Assert.IsTrue(sdata.angleLimitConstraint.useAngleLimit);
            Assert.IsTrue(sdata.angleRestorationConstraint.useAngleRestoration);
            Assert.AreEqual(1.0f, sdata.triangleBendingConstraint.stiffness);
        }

        /// <summary>혼합 삼각형도 추출되고 무효 버텍스가 Fixed로 격상되는지 검증.</summary>
        [Test]
        public void Given_MixedMesh_When_Extract_Then_MixedTrisMovedWithFixedBoundary()
        {
            var (root, depths, chain0, chain1, leg) = BuildSkirtRig();
            var bones = new[] { chain0[0], chain0[2], leg };
            // v0,v1=스커트, v2=다리(무효), v3=스커트 — 두 삼각형 모두 스커트 버텍스를
            // 1개 이상 포함하므로 전부 추출되고, v2는 추출본에서 Fixed로 격상된다
            var weights = new[] { W(0, 1f), W(1, 1f), W(2, 1f), W(1, 1f) };
            var smr = MakeSmr(bones, weights, 4);
            smr.transform.SetParent(root, false); // 추출 GO는 원본 부모 아래로 생성됨
            var mesh = smr.sharedMesh;
            mesh.vertices = new[] { Vector3.zero, Vector3.one, Vector3.up, Vector3.right };
            mesh.triangles = new[] { 0, 1, 2, 0, 1, 3 };

            var attrs = SkirtClothBuilder.BuildVertexAttributes(
                smr, depths, 0.5f, 0, out _, out _);
            Assert.IsTrue(attrs[2].IsInvalid(), "v2는 스커트 무가중 — 무효");

            var extracted = SkirtClothBuilder.ExtractSkirtRenderer(
                smr, attrs, out var remapped);

            Assert.IsNotNull(extracted);
            var newSmr = extracted.GetComponent<SkinnedMeshRenderer>();
            Assert.AreEqual(4, newSmr.sharedMesh.vertexCount, "추출 메시는 경계 버텍스 포함");
            Assert.AreEqual(6, newSmr.sharedMesh.triangles.Length, "두 삼각형 모두 추출");
            Assert.AreEqual(0, smr.sharedMesh.triangles.Length, "원본에는 삼각형이 남지 않음");
            Assert.AreEqual(4, smr.sharedMesh.vertexCount, "원본 버텍스 수는 유지 (삼각형만 제거)");
            CollectionAssert.AreEqual(mesh.bindposes, newSmr.sharedMesh.bindposes);
            for (int i = 0; i < remapped.Length; i++)
                Assert.IsFalse(remapped[i].IsInvalid(), "추출 메시는 전부 클로스 대상");
            // 무효 버텍스(v2, remap 인덱스 2)는 Fixed로 격상
            Assert.AreEqual(MagicaCloth2.VertexAttribute.Flag_Fixed, remapped[2].Value,
                "혼합 삼각형의 무효 버텍스는 Fixed 격상");

            // 정리 시 원본 메시 복원
            SkirtClothBuilder.CleanupExtractions(root);
            Assert.AreEqual(mesh, smr.sharedMesh, "CleanupExtractions가 원본 메시를 복원");
        }
    }
}
