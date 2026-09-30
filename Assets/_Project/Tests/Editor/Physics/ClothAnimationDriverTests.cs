using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>Phase 4 — 클립/곡 연동 클로스 파라미터 드라이버 검증.</summary>
    public class ClothAnimationDriverTests
    {
        ClothAnimationDriver driver;
        GameObject go;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("driver");
            driver = go.AddComponent<ClothAnimationDriver>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
        }

        [Test]
        public void Given_Curves_When_ApplyCurvesTo_Then_SevenPropsWritten()
        {
            driver.blendWeight = AnimationCurve.Constant(0, 1, 0.3f);
            driver.gravity = AnimationCurve.Constant(0, 1, 2.5f);
            driver.damping = AnimationCurve.Constant(0, 1, 0.33f);
            driver.worldInertia = AnimationCurve.Constant(0, 1, 0.7f);
            driver.localInertia = AnimationCurve.Constant(0, 1, 0.6f);
            driver.windInfluence = AnimationCurve.Constant(0, 1, 0.55f);
            driver.animationPoseRatio = AnimationCurve.Constant(0, 1, 0.4f);

            var s = new ClothSerializeData();
            driver.ApplyCurvesTo(s, 0.5f);

            Assert.That(s.blendWeight, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(s.gravity, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(s.damping.value, Is.EqualTo(0.33f).Within(0.001f));
            Assert.That(s.inertiaConstraint.worldInertia, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(s.inertiaConstraint.localInertia, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(s.wind.influence, Is.EqualTo(0.55f).Within(0.001f));
            Assert.That(s.animationPoseRatio, Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void Given_NullCurve_When_ApplyCurvesTo_Then_PropertyUntouched()
        {
            var s = new ClothSerializeData();
            s.gravity = 9.9f;
            driver.blendWeight = AnimationCurve.Constant(0, 1, 0.5f); // gravity만 미설정
            driver.ApplyCurvesTo(s, 0.5f);
            Assert.That(s.gravity, Is.EqualTo(9.9f).Within(0.001f));
            Assert.That(s.blendWeight, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Given_TimeJumpOverThreshold_When_ShouldReset_Then_True()
        {
            // 60초 클립에서 0.5 → 0.7 점프 = 12초 > 0.25s 임계
            Assert.IsTrue(ClothAnimationDriver.ShouldReset(0.5f, 0.7f, 60f, 0.25f));
            // 같은 비율 점프도 0.1초 클립에선 리셋 불필요
            Assert.IsFalse(ClothAnimationDriver.ShouldReset(0.5f, 0.7f, 0.5f, 0.25f));
        }

        [Test]
        public void Given_BackwardSeek_When_ShouldReset_Then_True()
        {
            Assert.IsTrue(ClothAnimationDriver.ShouldReset(0.8f, 0.1f, 30f, 0.25f));
            // 30초 클립에서 0.005 스텝 = 0.15초 — 임계 미만
            Assert.IsFalse(ClothAnimationDriver.ShouldReset(0.80f, 0.805f, 30f, 0.25f));
        }
    }
}
