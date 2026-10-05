using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// 본 스프링 식 머리카락 프로파일의 MC2 변환과 외부 백엔드 어댑터의
    /// 미설치 폴백 동작을 검증한다.
    /// </summary>
    public class HairSpringProfileTests
    {
        static ClothSerializeData NewTunedData()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.ApplyLongHair(s, 1.0f);
            return s;
        }

        [Test]
        public void Given_CurveTypes_When_Evaluate_Then_ExpectedDepthProfile()
        {
            Assert.That(new DepthSpringCurve(SpringCurveType.ConstantOne).Evaluate(0.7f),
                Is.EqualTo(1f));
            Assert.That(new DepthSpringCurve(SpringCurveType.ConstantZero).Evaluate(0.3f),
                Is.EqualTo(0f));
            // 루트 1 → 끝 0 선형
            var fade = new DepthSpringCurve(SpringCurveType.RootOneTailZero);
            Assert.That(fade.Evaluate(0f), Is.EqualTo(1f));
            Assert.That(fade.Evaluate(1f), Is.EqualTo(0f));
            Assert.That(fade.Evaluate(0.5f), Is.EqualTo(0.5f).Within(0.001f));
            // 루트 0 → 끝 1 선형
            var grow = new DepthSpringCurve(SpringCurveType.RootZeroTailOne);
            Assert.That(grow.Evaluate(0f), Is.EqualTo(0f));
            Assert.That(grow.Evaluate(1f), Is.EqualTo(1f));
        }

        [Test]
        public void Given_CurveType_When_Average_Then_MatchesIntegral()
        {
            // RootOneTailZero 선형의 평균은 0.5
            var fade = new DepthSpringCurve(SpringCurveType.RootOneTailZero);
            Assert.That(fade.Average(), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(new DepthSpringCurve(SpringCurveType.ConstantHalf).Average(),
                Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Given_ProfileDisabled_When_Apply_Then_TuningPathUnchanged()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true,
                HairTuning.Default, true, HairSpringProfile.Default);
            // enabled=false → 기존 Apply 결과와 동일해야 한다
            Assert.That(s.angleRestorationConstraint.stiffness.value,
                Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(s.gravity, Is.EqualTo(5.0f).Within(0.001f));
            Assert.That(s.animationPoseRatio, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Given_EnabledProfile_When_Apply_Then_SpringSemanticsMapped()
        {
            var p = HairSpringProfile.Default;
            p.enabled = true;
            p.frequency = 8f;
            p.dampingRatio = 0.8f;
            p.gravity = -3f;

            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true,
                HairTuning.Default, true, p);

            // 진동수 → 복원 강성 근사 (8/10)
            Assert.That(s.angleRestorationConstraint.stiffness.value,
                Is.EqualTo(0.8f).Within(0.001f));
            // 감쇠비 → 속도 감쇠
            Assert.That(s.angleRestorationConstraint.velocityAttenuation,
                Is.EqualTo(0.8f).Within(0.001f));
            // 애니메이션 혼합 평균(RootOneTailZero → 0.5) → animationPoseRatio
            Assert.That(s.animationPoseRatio, Is.EqualTo(0.5f).Within(0.02f));
            // 중력 절대값 덮어쓰기
            Assert.That(s.gravity, Is.EqualTo(-3f).Within(0.001f));
        }

        [Test]
        public void Given_BendAngleCap_When_Apply_Then_AngleLimitToggled()
        {
            var p = HairSpringProfile.Default;
            p.enabled = true;
            p.maxBendAngleDeg = 90f;
            var s = NewTunedData();
            p.ApplyToMc2(s, 1.0f);
            Assert.IsTrue(s.angleLimitConstraint.useAngleLimit);
            Assert.That(s.angleLimitConstraint.limitAngle.value,
                Is.EqualTo(90f).Within(0.001f));

            p.maxBendAngleDeg = 180f;
            p.ApplyToMc2(s, 1.0f);
            Assert.IsFalse(s.angleLimitConstraint.useAngleLimit);
        }

        [Test]
        public void Given_CollisionRadius_When_Apply_Then_ParticleRadiusScaled()
        {
            var p = HairSpringProfile.Default;
            p.enabled = true;
            p.maxCollisionRadius = 0.03f;
            var s = NewTunedData();
            p.ApplyToMc2(s, 2.0f);
            // radius.value = maxCollisionRadius * unit
            Assert.That(s.radius.value, Is.EqualTo(0.06f).Within(0.001f));
        }

        [Test]
        public void Given_NoSpringAsset_When_AdapterUsed_Then_SafeFallback()
        {
            // 이 프로젝트에는 본 스프링 에셋 미설치 — 리플렉션 해석이 실패하고 조용히 폴백.
            // 에셋이 임포트되면 이 테스트는 의미가 없으므로 스킵한다.
            Assume.That(SpringHairBackend.Available, Is.False);
            var host = new GameObject("host");
            try
            {
                Assert.IsNull(SpringHairBackend.Build(
                    host.transform, new System.Collections.Generic.List<HairChain>
                    {
                        new HairChain { rootBone = host.transform, part = HairPart.Tail }
                    },
                    HairSpringProfile.Default, null));
                Assert.IsFalse(SpringHairBackend.ReapplyExisting(
                    host.transform, HairSpringProfile.Default));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Given_DefaultBackend_When_Created_Then_MagicaCloth2()
        {
            var setup = new GameObject("setup").AddComponent<CharacterPhysicsSetup>();
            try
            {
                Assert.That(setup.hairBackend, Is.EqualTo(HairDynamicsBackend.MagicaCloth2));
                Assert.IsFalse(setup.springProfile.enabled);
            }
            finally
            {
                Object.DestroyImmediate(setup.gameObject);
            }
        }
    }
}
