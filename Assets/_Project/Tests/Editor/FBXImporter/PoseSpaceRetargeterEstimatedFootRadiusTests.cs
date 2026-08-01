using Fbx2Vmd.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterEstimatedFootRadiusTests
    {
        private static readonly Type[] EstimatedFootRadiusParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType()
        };

        [Test]
        public void Given_FiniteFootAndRendererY_When_CalculatingEstimatedFootRadius_Then_UsesLowestFootDistance()
        {
            bool success = TryCalculateEstimatedFootRadius(
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
            bool success = TryCalculateEstimatedFootRadius(
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
            bool success = TryCalculateEstimatedFootRadius(
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
            bool success = TryCalculateEstimatedFootRadius(
                leftFootY: float.NaN,
                rightFootY: float.NaN,
                rendererMinY: 0.03f,
                out float estimatedRadius);

            Assert.That(success, Is.False);
            Assert.That(float.IsNaN(estimatedRadius), Is.True);
        }

        private static bool TryCalculateEstimatedFootRadius(
            float leftFootY,
            float rightFootY,
            float rendererMinY,
            out float estimatedRadius)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateEstimatedFootRadius",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EstimatedFootRadiusParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for estimated foot radius calculation.");

            object[] args =
            {
                leftFootY,
                rightFootY,
                rendererMinY,
                float.NaN
            };

            bool success = (bool)method.Invoke(null, args);
            estimatedRadius = (float)args[3];
            return success;
        }
    }
}
