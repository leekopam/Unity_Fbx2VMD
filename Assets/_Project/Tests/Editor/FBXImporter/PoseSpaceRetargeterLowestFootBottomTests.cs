using Fbx2Vmd.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterLowestFootBottomTests
    {
        private static readonly Type[] LowestFootBottomParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] FootBottomParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType()
        };

        [Test]
        public void Given_FiniteFootHeight_When_CalculatingFootBottom_Then_SubtractsRadius()
        {
            bool resolved = TryCalculateFootBottomY(
                footY: 0.6f,
                footRadius: 0.08f,
                out float footBottomY);

            Assert.That(resolved, Is.True);
            Assert.That(footBottomY, Is.EqualTo(0.52f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteFootBottom_When_CalculatingFootBottom_Then_ReturnsFalse()
        {
            bool resolved = TryCalculateFootBottomY(
                footY: float.PositiveInfinity,
                footRadius: 0.08f,
                out float footBottomY);

            Assert.That(resolved, Is.False);
            Assert.That(footBottomY, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_FiniteFootHeights_When_CalculatingLowestFootBottom_Then_UsesLowerFootMinusRadius()
        {
            bool resolved = TryCalculateLowestFootBottomY(
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
            bool resolved = TryCalculateLowestFootBottomY(
                leftFootY: float.PositiveInfinity,
                rightFootY: 0.9f,
                footRadius: 0.08f,
                out float lowestFootBottomY);

            Assert.That(resolved, Is.False);
            Assert.That(lowestFootBottomY, Is.EqualTo(0f).Within(0.0001f));
        }

        private static bool TryCalculateLowestFootBottomY(
            float leftFootY,
            float rightFootY,
            float footRadius,
            out float lowestFootBottomY)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateLowestFootBottomY",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: LowestFootBottomParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for lowest foot bottom calculation.");

            object[] args =
            {
                leftFootY,
                rightFootY,
                footRadius,
                0f
            };

            bool resolved = (bool)method.Invoke(null, args);
            lowestFootBottomY = (float)args[3];
            return resolved;
        }

        private static bool TryCalculateFootBottomY(
            float footY,
            float footRadius,
            out float footBottomY)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateFootBottomY",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FootBottomParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for single foot bottom calculation.");

            object[] args =
            {
                footY,
                footRadius,
                0f
            };

            bool resolved = (bool)method.Invoke(null, args);
            footBottomY = (float)args[2];
            return resolved;
        }
    }
}
