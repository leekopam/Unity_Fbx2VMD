using Fbx2Vmd.Retargeting;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterEstimatedFootRadiusTests
    {
        [Test]
        public void Given_FiniteFootAndRendererY_When_CalculatingEstimatedFootRadius_Then_UsesLowestFootDistance()
        {
            bool success = GroundingStabilizer.TryCalculateEstimatedFootRadius(
                leftFootY: 0.11f,
                rightFootY: 0.08f,
                rendererMinY: 0.03f,
                out float estimatedRadius);

            Assert.That(success, Is.True);
            Assert.That(estimatedRadius, Is.EqualTo(0.05f).Within(0.0001f));
        }

        [Test]
        public void Given_EstimatedRadiusBelowMinimum_When_CalculatingEstimatedFootRadius_Then_ClampsToMinimum()
        {
            bool success = GroundingStabilizer.TryCalculateEstimatedFootRadius(
                leftFootY: 0.03f,
                rightFootY: 0.03f,
                rendererMinY: 0.025f,
                out float estimatedRadius);

            Assert.That(success, Is.True);
            Assert.That(estimatedRadius, Is.EqualTo(0.02f).Within(0.0001f));
        }

        [Test]
        public void Given_EstimatedRadiusAboveMaximum_When_CalculatingEstimatedFootRadius_Then_ClampsToMaximum()
        {
            bool success = GroundingStabilizer.TryCalculateEstimatedFootRadius(
                leftFootY: 0.4f,
                rightFootY: 0.5f,
                rendererMinY: 0f,
                out float estimatedRadius);

            Assert.That(success, Is.True);
            Assert.That(estimatedRadius, Is.EqualTo(0.16f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteFootY_When_CalculatingEstimatedFootRadius_Then_ReturnsFalse()
        {
            bool success = GroundingStabilizer.TryCalculateEstimatedFootRadius(
                leftFootY: float.NaN,
                rightFootY: float.NaN,
                rendererMinY: 0.03f,
                out float estimatedRadius);

            Assert.That(success, Is.False);
            Assert.That(float.IsNaN(estimatedRadius), Is.True);
        }

    }
}
