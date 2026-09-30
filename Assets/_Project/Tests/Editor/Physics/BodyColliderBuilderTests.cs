using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class BodyColliderBuilderTests
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly Dictionary<HumanBodyBones, Transform> rig = new Dictionary<HumanBodyBones, Transform>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            rig.Clear();
        }

        Transform Bone(HumanBodyBones id, string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            rig[id] = go.transform;
            return go.transform;
        }

        /// <summary>간소화된 전신 릭을 만든다 (1.6m 체형).</summary>
        Transform BuildRig()
        {
            var root = new GameObject("Root");
            created.Add(root);
            var hips = Bone(HumanBodyBones.Hips, "Hips", root.transform, new Vector3(0, 1.0f, 0));
            var spine = Bone(HumanBodyBones.Spine, "Spine", hips, new Vector3(0, 1.15f, 0));
            var chest = Bone(HumanBodyBones.Chest, "Chest", spine, new Vector3(0, 1.3f, 0));
            var neck = Bone(HumanBodyBones.Neck, "Neck", chest, new Vector3(0, 1.5f, 0));
            Bone(HumanBodyBones.Head, "Head", neck, new Vector3(0, 1.6f, 0));
            Bone(HumanBodyBones.UpperChest, "UpperChest", chest, new Vector3(0, 1.4f, 0));

            var lsh = Bone(HumanBodyBones.LeftShoulder, "LShoulder", chest, new Vector3(-0.1f, 1.48f, 0));
            var lua = Bone(HumanBodyBones.LeftUpperArm, "LUpperArm", lsh, new Vector3(-0.2f, 1.48f, 0));
            var lla = Bone(HumanBodyBones.LeftLowerArm, "LLowerArm", lua, new Vector3(-0.45f, 1.48f, 0));
            Bone(HumanBodyBones.LeftHand, "LHand", lla, new Vector3(-0.7f, 1.48f, 0));

            var rsh = Bone(HumanBodyBones.RightShoulder, "RShoulder", chest, new Vector3(0.1f, 1.48f, 0));
            var rua = Bone(HumanBodyBones.RightUpperArm, "RUpperArm", rsh, new Vector3(0.2f, 1.48f, 0));
            var rla = Bone(HumanBodyBones.RightLowerArm, "RLowerArm", rua, new Vector3(0.45f, 1.48f, 0));
            Bone(HumanBodyBones.RightHand, "RHand", rla, new Vector3(0.7f, 1.48f, 0));

            var lul = Bone(HumanBodyBones.LeftUpperLeg, "LUpperLeg", hips, new Vector3(-0.1f, 0.95f, 0));
            var lll = Bone(HumanBodyBones.LeftLowerLeg, "LLowerLeg", lul, new Vector3(-0.1f, 0.5f, 0));
            Bone(HumanBodyBones.LeftFoot, "LFoot", lll, new Vector3(-0.1f, 0.08f, 0));
            var rul = Bone(HumanBodyBones.RightUpperLeg, "RUpperLeg", hips, new Vector3(0.1f, 0.95f, 0));
            var rll = Bone(HumanBodyBones.RightLowerLeg, "RLowerLeg", rul, new Vector3(0.1f, 0.5f, 0));
            Bone(HumanBodyBones.RightFoot, "RFoot", rll, new Vector3(0.1f, 0.08f, 0));
            return root.transform;
        }

        [Test]
        public void Given_FullRig_When_BuildWithoutSymmetry_Then_CreatesMirroredPairs()
        {
            BuildRig();
            var result = BodyColliderBuilder.Build(
                b => rig.TryGetValue(b, out var t) ? t : null,
                torsoLength: 0.6f,
                boneVerts: null,
                useSymmetry: false,
                includeLegs: true);

            // 5 비대칭(머리/목/가슴/허리/골반) + 7 쌍×2 = 19
            Assert.That(result.colliders.Count, Is.EqualTo(19), result.warnings.Count > 0 ? string.Join(";", result.warnings) : "");
            Assert.That(result.byCategory[ColliderCategory.Arms].Count, Is.EqualTo(4));
            Assert.That(result.byCategory[ColliderCategory.Hands].Count, Is.EqualTo(2));
        }

        [Test]
        public void Given_Rig_When_Build_Then_CapsuleHasPositiveSize()
        {
            BuildRig();
            var result = BodyColliderBuilder.Build(
                b => rig.TryGetValue(b, out var t) ? t : null,
                0.6f, null, false, true);

            foreach (var col in result.colliders)
            {
                var size = col.GetSize();
                Assert.That(size.x, Is.GreaterThan(0f), $"{col.name} 반경이 0입니다.");
            }
        }

        [Test]
        public void Given_RigWithoutLegs_When_Build_Then_SkipsLegs()
        {
            BuildRig();
            var result = BodyColliderBuilder.Build(
                b => rig.TryGetValue(b, out var t) ? t : null,
                0.6f, null, false, includeLegs: false);

            Assert.That(result.byCategory.ContainsKey(ColliderCategory.Legs), Is.False);
            Assert.That(result.byCategory.ContainsKey(ColliderCategory.Feet), Is.False);
        }

        [Test]
        public void Given_BoneVerts_When_Build_Then_RadiusFitsVerts()
        {
            var hips = Bone(HumanBodyBones.Hips, "Hips", null, new Vector3(0, 1f, 0));
            Bone(HumanBodyBones.Spine, "Spine", hips, new Vector3(0, 1.15f, 0));

            // 허리 주변 반경 0.2m 버텍스
            var verts = new List<Vector3>();
            for (int i = 0; i < 10; i++)
                verts.Add(hips.position + Random.onUnitSphere * 0.2f);
            var boneVerts = new Dictionary<Transform, List<Vector3>> { { hips, verts } };

            var result = BodyColliderBuilder.Build(
                b => rig.TryGetValue(b, out var t) ? t : null,
                0.6f, boneVerts, false, false, 1.15f);

            var hipsCol = result.byCategory[ColliderCategory.Hips][0] as MagicaSphereCollider;
            Assert.That(hipsCol, Is.Not.Null);
            float r = hipsCol.GetSize().x;
            Assert.That(r, Is.GreaterThan(0.2f), "버텍스 반경+스케일 이상이어야 합니다.");
            Assert.That(r, Is.LessThan(0.35f));
        }

        [Test]
        public void Given_CreatedColliders_When_Clearing_Then_Removed()
        {
            var root = BuildRig();
            BodyColliderBuilder.Build(
                b => rig.TryGetValue(b, out var t) ? t : null,
                0.6f, null, false, true);
            Assert.That(root.GetComponentsInChildren<ColliderComponent>(true).Length, Is.GreaterThan(0));

            BodyColliderBuilder.ClearGenerated(root);
            Assert.That(root.GetComponentsInChildren<ColliderComponent>(true).Length, Is.EqualTo(0));
        }
    }
}
