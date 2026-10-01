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
