using Fbx2Vmd.Retargeting;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterLowestFootBottomTests
    {
        [Test]
        public void Given_FiniteFootHeight_When_CalculatingFootBottom_Then_SubtractsRadius()
        {
            bool resolved = GroundingStabilizer.TryCalculateFootBottomY(
                footY: 0.6f,
                footRadius: 0.08f,
                out float footBottomY);

            Assert.That(resolved, Is.True);
            Assert.That(footBottomY, Is.EqualTo(0.52f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteFootBottom_When_CalculatingFootBottom_Then_ReturnsFalse()
        {
            bool resolved = GroundingStabilizer.TryCalculateFootBottomY(
                footY: float.PositiveInfinity,
                footRadius: 0.08f,
                out float footBottomY);

            Assert.That(resolved, Is.False);
            Assert.That(footBottomY, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_FiniteFootHeights_When_CalculatingLowestFootBottom_Then_UsesLowerFootMinusRadius()
        {
            bool resolved = GroundingStabilizer.TryCalculateLowestFootBottomY(
                leftFootY: 0.4f,
                rightFootY: 0.9f,
                footRadius: 0.08f,
                out float lowestFootBottomY);

            Assert.That(resolved, Is.True);
            Assert.That(lowestFootBottomY, Is.EqualTo(0.32f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteFootBottom_When_CalculatingLowestFootBottom_Then_ReturnsFalse()
        {
            bool resolved = GroundingStabilizer.TryCalculateLowestFootBottomY(
                leftFootY: float.PositiveInfinity,
                rightFootY: 0.9f,
                footRadius: 0.08f,
                out float lowestFootBottomY);

            Assert.That(resolved, Is.False);
            Assert.That(lowestFootBottomY, Is.EqualTo(0f).Within(0.0001f));
        }

    }
}
