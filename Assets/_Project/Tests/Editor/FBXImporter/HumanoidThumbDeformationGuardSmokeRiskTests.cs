using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidThumbDeformationGuardSmokeRiskTests
    {
        [Test]
        public void Given_ManualThumbOverrideSpreadExceedsSceneCap_When_ResolvingVisualLengthLimit_Then_KeepsConfiguredSmokeCap()
        {
            MethodInfo method = typeof(HumanoidThumbDeformationGuard).GetMethod(
                "ResolveManualOverrideMaxSpreadAngle",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "High-risk manual thumb override must not relax the scene smoke-safe spread cap.");

            Assert.That((float)method.Invoke(null, new object[] { 50f, 52f }), Is.EqualTo(50f).Within(0.0001f));
            Assert.That((float)method.Invoke(null, new object[] { 54f, 52f }), Is.EqualTo(52f).Within(0.0001f));
            Assert.That((float)method.Invoke(null, new object[] { 50f, 48f }), Is.EqualTo(48f).Within(0.0001f));
            Assert.That((float)method.Invoke(null, new object[] { -1f, 52f }), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualThumbProjectionRiskExceedsSmokeLimit_When_CheckingPreserveBypass_Then_BypassesManualReferencePreserve()
        {
            MethodInfo method = typeof(HumanoidThumbDeformationGuard).GetMethod(
                "ShouldBypassManualThumbProjectionPreserveForSmokeRisk",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, "Projection correction must bypass manual reference preserve when the current thumb projection already exceeds smoke risk.");

            Assert.That((bool)method.Invoke(null, new object[] { -0.008f, 0.358f, 0.5f }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[] { 0.093f, 0.358f, 0.5f }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { 0.505f, 0.358f, 0.5f }), Is.False);
        }
    }
}
