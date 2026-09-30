using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class NamePatternBoneResolverTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        Transform Bone(string name, Transform parent)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>영어+일본어 혼용 명명의 임의 계층을 만든다.</summary>
        Transform BuildEnglishRig()
        {
            var root = new GameObject("Model");
            created.Add(root);
            var hips = Bone("Hips", root.transform);
            var spine = Bone("Spine", hips);
            var chest = Bone("Chest", spine);
            var neck = Bone("Neck", chest);
            Bone("Head", neck);
            var lArm = Bone("LeftUpperArm", chest);
            var lFore = Bone("LeftLowerArm", lArm);
            Bone("LeftHand", lFore);
            var rArm = Bone("RightUpperArm", chest);
            Bone("RightHand", rArm);
            var lLeg = Bone("LeftUpLeg", hips);
            var lShin = Bone("LeftLowerLeg", lLeg);
            Bone("LeftFoot", lShin);
            return root.transform;
        }

        [Test]
        public void Given_EnglishNames_When_Resolve_Then_CoreBonesMapped()
        {
            var root = BuildEnglishRig();
            var map = NamePatternBoneResolver.Resolve(root);

            Assert.That(map[HumanBodyBones.Head].name, Is.EqualTo("Head"));
            Assert.That(map[HumanBodyBones.Hips].name, Is.EqualTo("Hips"));
            Assert.That(map[HumanBodyBones.LeftUpperArm].name, Is.EqualTo("LeftUpperArm"));
            Assert.That(map[HumanBodyBones.RightHand].name, Is.EqualTo("RightHand"));
            Assert.That(map[HumanBodyBones.LeftFoot].name, Is.EqualTo("LeftFoot"));
        }

        [Test]
        public void Given_MmdJapaneseNames_When_Resolve_Then_CoreBonesMapped()
        {
            var root = new GameObject("Root");
            created.Add(root);
            var hips = Bone("腰", root.transform);
            var chest = Bone("上半身", hips);
            var neck = Bone("首", chest);
            Bone("頭", neck);
            var lArm = Bone("左腕", chest);
            var lElbow = Bone("左ひじ", lArm);
            Bone("左手首", lElbow);
            var rArm = Bone("右腕", chest);
            Bone("右手首", rArm);
            var lLeg = Bone("左足", hips);
            var lKnee = Bone("左ひざ", lLeg);
            Bone("左足首", lKnee);

            var map = NamePatternBoneResolver.Resolve(root.transform);

            Assert.That(map[HumanBodyBones.Hips].name, Is.EqualTo("腰"));
            Assert.That(map[HumanBodyBones.Head].name, Is.EqualTo("頭"));
            Assert.That(map[HumanBodyBones.LeftUpperArm].name, Is.EqualTo("左腕"));
            Assert.That(map[HumanBodyBones.LeftLowerArm].name, Is.EqualTo("左ひじ"));
            Assert.That(map[HumanBodyBones.LeftUpperLeg].name, Is.EqualTo("左足"));
            Assert.That(map[HumanBodyBones.LeftFoot].name, Is.EqualTo("左足首"));
        }

        [Test]
        public void Given_ForearmName_When_Resolve_Then_LongestMatchWins()
        {
            var root = new GameObject("Root");
            created.Add(root);
            var arm = Bone("forearm_L", root.transform);

            var map = NamePatternBoneResolver.Resolve(root.transform);

            // 'forearm'이 'arm'보다 길어 LowerArm으로 매핑되어야 함
            Assert.That(map[HumanBodyBones.LeftLowerArm], Is.EqualTo(arm));
            Assert.That(map.ContainsKey(HumanBodyBones.LeftUpperArm), Is.False);
        }

        [Test]
        public void Given_HairBone_When_Resolve_Then_NotMapped()
        {
            var root = new GameObject("Root");
            created.Add(root);
            Bone("joint_hidariHairA1", root.transform);
            Bone("Hair_Tail_01", root.transform);

            var map = NamePatternBoneResolver.Resolve(root.transform);

            Assert.That(map.Count, Is.EqualTo(0));
        }

        [Test]
        public void Given_SideTokens_When_DetectSide_Then_Correct()
        {
            Assert.That(NamePatternBoneResolver.DetectSide("arm_L"),
                Is.EqualTo(NamePatternBoneResolver.Side.Left));
            Assert.That(NamePatternBoneResolver.DetectSide("arm.R"),
                Is.EqualTo(NamePatternBoneResolver.Side.Right));
            Assert.That(NamePatternBoneResolver.DetectSide("hidariUde"),
                Is.EqualTo(NamePatternBoneResolver.Side.Left));
            Assert.That(NamePatternBoneResolver.DetectSide("spine"),
                Is.EqualTo(NamePatternBoneResolver.Side.None));
        }

        [Test]
        public void Given_CompactSideSuffix_When_DetectSide_Then_Correct()
        {
            // "ArmL"/"LArm" 같은 무구분자 표기 — 대문자 L/R만 인정해 'collar' 등 오탐 방지
            Assert.That(NamePatternBoneResolver.DetectSide("ArmL"),
                Is.EqualTo(NamePatternBoneResolver.Side.Left));
            Assert.That(NamePatternBoneResolver.DetectSide("UpperArmR"),
                Is.EqualTo(NamePatternBoneResolver.Side.Right));
            Assert.That(NamePatternBoneResolver.DetectSide("LArm"),
                Is.EqualTo(NamePatternBoneResolver.Side.Left));
            Assert.That(NamePatternBoneResolver.DetectSide("RFoot"),
                Is.EqualTo(NamePatternBoneResolver.Side.Right));
            Assert.That(NamePatternBoneResolver.DetectSide("collar"),
                Is.EqualTo(NamePatternBoneResolver.Side.None));
            Assert.That(NamePatternBoneResolver.DetectSide("shoulder"),
                Is.EqualTo(NamePatternBoneResolver.Side.None));
        }

        [Test]
        public void Given_CompactSidedBones_When_Resolve_Then_Mapped()
        {
            var root = new GameObject("Root");
            created.Add(root);
            var lArm = Bone("UpperArmL", root.transform);
            var rArm = Bone("UpperArmR", root.transform);

            var map = NamePatternBoneResolver.Resolve(root.transform);

            Assert.That(map[HumanBodyBones.LeftUpperArm], Is.EqualTo(lArm));
            Assert.That(map[HumanBodyBones.RightUpperArm], Is.EqualTo(rArm));
        }

        [Test]
        public void Given_SpacedKeyName_When_Resolve_Then_NormalizedKeyMatches()
        {
            // 키 "upper body2"는 정규화 이름에 맞아야 함 (구분자 포함 키의 사후 매칭)
            var root = new GameObject("Root");
            created.Add(root);
            var chest2 = Bone("UpperBody2", root.transform);

            var map = NamePatternBoneResolver.Resolve(root.transform);

            Assert.That(map[HumanBodyBones.UpperChest], Is.EqualTo(chest2));
        }

        [Test]
        public void Given_MmdCenterHips_When_Resolve_Then_HipsMapped()
        {
            // PronamaChan/Unity-chan 계열 리그의 hips 본명(center/lower_body) 커버
            var root = new GameObject("Root");
            created.Add(root);
            var center = Bone("center", root.transform);
            var lowerBody = Bone("lower_body", center);
            Bone("head", lowerBody);

            var map = NamePatternBoneResolver.Resolve(root.transform);

            Assert.That(map[HumanBodyBones.Hips], Is.EqualTo(center));
            Assert.That(map[HumanBodyBones.Head].name, Is.EqualTo("head"));
        }

        [Test]
        public void Given_Chain_When_CollectWithMinDepth_Then_SkipsShallowBones()
        {
            // 루트 → a(깊이1) → b(깊이2) → c(깊이3)
            var root = new GameObject("root");
            created.Add(root);
            var a = Bone("a", root.transform);
            var b = Bone("b", a);
            Bone("c", b);

            var outList = new List<Transform>();
            PhysicsValidationProbe.CollectChainBones(root.transform, 2, outList);

            Assert.That(outList.Count, Is.EqualTo(2));
            Assert.That(outList[0].name, Is.EqualTo("b"));
            Assert.That(outList[1].name, Is.EqualTo("c"));
        }
    }
}
