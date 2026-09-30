using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class HairBoneClassifierTests
    {
        [Test]
        public void Given_YybStyleNames_When_Detecting_Then_RecognizesHair()
        {
            Assert.That(HairBoneClassifier.IsHairBoneName("joint_hidariHairA1"), Is.True);
            Assert.That(HairBoneClassifier.IsHairBoneName("joint_migiHairB2"), Is.True);
            Assert.That(HairBoneClassifier.IsHairBoneName("joint_hidariHairC3"), Is.True);
        }

        [Test]
        public void Given_CommonEnglishNames_When_Detecting_Then_RecognizesHair()
        {
            Assert.That(HairBoneClassifier.IsHairBoneName("Hair_Front_01"), Is.True);
            Assert.That(HairBoneClassifier.IsHairBoneName("hair_back_2"), Is.True);
            Assert.That(HairBoneClassifier.IsHairBoneName("TwinTail_L_1"), Is.True);
        }

        [Test]
        public void Given_MmdNames_When_Detecting_Then_RecognizesHair()
        {
            Assert.That(HairBoneClassifier.IsHairBoneName("前髪1"), Is.True);
            Assert.That(HairBoneClassifier.IsHairBoneName("ツインテール左"), Is.True);
            Assert.That(HairBoneClassifier.IsHairBoneName("アホ毛"), Is.True);
            Assert.That(HairBoneClassifier.IsHairBoneName("後髪"), Is.True);
        }

        [Test]
        public void Given_BodyOrFaceBones_When_Detecting_Then_Rejects()
        {
            Assert.That(HairBoneClassifier.IsHairBoneName("joint_Head"), Is.False);
            Assert.That(HairBoneClassifier.IsHairBoneName("Eye_L"), Is.False);
            Assert.That(HairBoneClassifier.IsHairBoneName("Neck"), Is.False);
            Assert.That(HairBoneClassifier.IsHairBoneName("UpperArm_L"), Is.False);
            Assert.That(HairBoneClassifier.IsHairBoneName("Skirt_01"), Is.False);
            Assert.That(HairBoneClassifier.IsHairBoneName("胸"), Is.False);
        }

        [Test]
        public void Given_PartNames_When_Classifying_Then_ReturnsCorrectPart()
        {
            Assert.That(HairBoneClassifier.Classify("hair_front_1"), Is.EqualTo(HairPart.Front));
            Assert.That(HairBoneClassifier.Classify("side_hair_L"), Is.EqualTo(HairPart.Side));
            Assert.That(HairBoneClassifier.Classify("backhair_01"), Is.EqualTo(HairPart.Back));
            Assert.That(HairBoneClassifier.Classify("ponytail_1"), Is.EqualTo(HairPart.Tail));
            Assert.That(HairBoneClassifier.Classify("ribbon_L"), Is.EqualTo(HairPart.Accessory));
            Assert.That(HairBoneClassifier.Classify("アホ毛"), Is.EqualTo(HairPart.Ahoge));
        }

        [Test]
        public void Given_YybHairName_When_Classifying_Then_UnknownForPositionFallback()
        {
            // YYB식 이름은 부위 키워드가 없으므로 Unknown → 위치 fallback 대상
            Assert.That(HairBoneClassifier.Classify("joint_hidariHairA1"), Is.EqualTo(HairPart.Unknown));
        }

        [Test]
        public void Given_RootPositions_When_ClassifyingByPosition_Then_InfersPart()
        {
            // 머리 기준 앞쪽 → 앞머리
            Assert.That(HairBoneClassifier.ClassifyByPosition(new Vector3(0f, 0.05f, 0.08f)),
                Is.EqualTo(HairPart.Front));
            // 머리 기준 옆쪽 → 옆머리
            Assert.That(HairBoneClassifier.ClassifyByPosition(new Vector3(0.09f, 0.0f, -0.01f)),
                Is.EqualTo(HairPart.Side));
            // 뒤쪽 → 뒷머리
            Assert.That(HairBoneClassifier.ClassifyByPosition(new Vector3(0f, 0.0f, -0.08f)),
                Is.EqualTo(HairPart.Back));
            // 위쪽+옆쪽 돌출 → 트윈테일 계열
            Assert.That(HairBoneClassifier.ClassifyByPosition(new Vector3(0.06f, 0.08f, -0.02f)),
                Is.EqualTo(HairPart.Tail));
        }
    }
}
