using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;

namespace Tests.Editor.ClothPhysics
{
    public class HairPhysicsParametersTests
    {
        static ClothSerializeData NewLongHairData()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.ApplyLongHair(s, 1.0f);
            return s;
        }

        [Test]
        public void Given_DefaultTuning_When_Apply_Then_TemplateUnchanged()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, HairTuning.Default);
            Assert.That(s.angleRestorationConstraint.stiffness.value, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(s.damping.value, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(s.gravity, Is.EqualTo(5.0f).Within(0.001f));
        }

        [Test]
        public void Given_HighSway_When_Apply_Then_StifferAndDampingWeaker()
        {
            var t = HairTuning.Default;
            t.sway = 2f;
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t);
            // sway 2배 → 강성·감쇠 절반
            Assert.That(s.angleRestorationConstraint.stiffness.value, Is.LessThan(0.15f));
            Assert.That(s.damping.value, Is.LessThan(0.08f));
        }

        [Test]
        public void Given_ZeroSway_When_Apply_Then_NearlyRigid()
        {
            var t = HairTuning.Default;
            t.sway = 0f;
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t);
            Assert.That(s.angleRestorationConstraint.stiffness.value, Is.EqualTo(1f).Within(0.001f));
            Assert.That(s.damping.value, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Given_GravityScale_When_Apply_Then_GravityScaled()
        {
            var t = HairTuning.Default;
            t.gravityScale = 0.5f;
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t);
            Assert.That(s.gravity, Is.EqualTo(2.5f).Within(0.001f));
        }

        [Test]
        public void Given_RadiusScale_When_Apply_Then_RadiusScaled()
        {
            var t = HairTuning.Default;
            t.radiusScale = 1.5f;
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t);
            Assert.That(s.radius.value, Is.EqualTo(0.02f * 1.5f).Within(0.0001f));
        }

        [Test]
        public void Given_InertiaScale_When_Apply_Then_InertiaClamped()
        {
            var t = HairTuning.Default;
            t.inertiaScale = 2f; // 범위 0.5~1.5지만 코드는 안전하게 클램프해야 함
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t);
            Assert.That(s.inertiaConstraint.worldInertia, Is.LessThanOrEqualTo(1f));
            Assert.That(s.inertiaConstraint.localInertia, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void Given_DefaultTuning_When_Apply_Then_SelfCollisionEnabled()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, HairTuning.Default);
            Assert.That(s.selfCollisionConstraint.selfMode,
                Is.EqualTo(SelfCollisionConstraint.SelfCollisionMode.FullMesh));
            Assert.That(s.selfCollisionConstraint.surfaceThickness.value, Is.GreaterThan(0f));
        }

        [Test]
        public void Given_SelfCollisionOff_When_Apply_Then_SelfCollisionDisabled()
        {
            var t = HairTuning.Default;
            t.useSelfCollision = false;
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t);
            Assert.That(s.selfCollisionConstraint.selfMode,
                Is.EqualTo(SelfCollisionConstraint.SelfCollisionMode.None));
        }

        [Test]
        public void Given_Accessory_When_Apply_Then_DampingRisesTowardTip()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Accessory, s, 0.6f, null, false, HairTuning.Default);
            Assert.That(s.damping.useCurve, Is.True,
                "의상 체인은 깊이별 감쇠 램프를 써야 합니다 (팁 과흔들림 방지).");
            Assert.That(s.damping.Evaluate(0f), Is.LessThan(s.damping.Evaluate(1f)),
                "감쇠는 루트보다 끝에서 커야 합니다.");
            Assert.That(s.damping.Evaluate(1f), Is.GreaterThanOrEqualTo(0.4f),
                "끝단 감쇠가 너무 약하면 팁이 계속 출렁입니다.");
        }

        [Test]
        public void Given_AccessoryAndSpringProfile_When_Apply_Then_DampingRampSurvives()
        {
            var spring = HairSpringProfile.Default;
            spring.enabled = true;
            spring.dampingRatio = 0.5f; // 스프링 경로는 damping을 평탄값으로 덮어쓴다
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(
                HairPart.Accessory, s, 0.6f, null, false, HairTuning.Default, true, spring);
            Assert.That(s.damping.useCurve, Is.True,
                "스프링 프로파일의 평탄 damping 덮어쓰기 후에도 의상 램프는 복원돼야 합니다.");
            Assert.That(s.damping.Evaluate(1f), Is.GreaterThan(s.damping.Evaluate(0f)));
        }

        [Test]
        public void Given_HairPart_When_Apply_Then_DampingStaysFlat()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, HairTuning.Default);
            // 머리카락은 의상 램프를 쓰지 않는다 — 끝이 살랑여야 한다
            Assert.That(s.damping.useCurve, Is.False);
        }

        [Test]
        public void Given_TorsoScale_When_Apply_Then_SelfCollisionThicknessScaled()
        {
            var small = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, small, 0.6f, null, true, HairTuning.Default);
            var large = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, large, 1.2f, null, true, HairTuning.Default);
            // torso 0.6→unit 1.0, 1.2→unit 2.0 — 자기 충돌 두께는 체형에 비례해야 한다
            Assert.That(large.selfCollisionConstraint.surfaceThickness.value,
                Is.EqualTo(small.selfCollisionConstraint.surfaceThickness.value * 2f)
                    .Within(0.0005f));
        }
    }
}
