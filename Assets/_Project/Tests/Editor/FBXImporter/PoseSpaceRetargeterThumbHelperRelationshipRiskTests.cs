using Fbx2Vmd.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterThumbHelperRelationshipRiskTests
    {
        private static readonly Type[] ThumbHelperRelationshipRiskParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(float).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        [Test]
        public void Given_HelperDistanceAndRotationWithinWarnings_When_CalculatingThumbHelperRelationshipRisk_Then_ReturnsZeroRisks()
        {
            bool calculated = TryCalculateThumbHelperRelationshipRisk(
                currentDistance: 0.102f,
                initialDistance: 0.1f,
                rotationDelta: 10f,
                spreadRisk: 0f,
                projectionRisk: 0f,
                out float helperDistanceRisk,
                out float helperRotationRisk,
                out float webbingRisk);

            Assert.That(calculated, Is.True);
            Assert.That(helperDistanceRisk, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(helperRotationRisk, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(webbingRisk, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Given_HelperDistanceAndRotationWarningsExceeded_When_CalculatingThumbHelperRelationshipRisk_Then_ScalesRisks()
        {
            bool calculated = TryCalculateThumbHelperRelationshipRisk(
                currentDistance: 0.106f,
                initialDistance: 0.1f,
                rotationDelta: 49f,
                spreadRisk: 0.25f,
                projectionRisk: 0.35f,
                out float helperDistanceRisk,
                out float helperRotationRisk,
                out float webbingRisk);

            Assert.That(calculated, Is.True);
            Assert.That(helperDistanceRisk, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(helperRotationRisk, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(webbingRisk, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteInputsWithoutRiskData_When_CalculatingThumbHelperRelationshipRisk_Then_ReturnsFalse()
        {
            bool calculated = TryCalculateThumbHelperRelationshipRisk(
                currentDistance: float.NaN,
                initialDistance: 0.1f,
                rotationDelta: float.NaN,
                spreadRisk: float.NaN,
                projectionRisk: float.NaN,
                out float helperDistanceRisk,
                out float helperRotationRisk,
                out float webbingRisk);

            Assert.That(calculated, Is.False);
            Assert.That(float.IsNaN(helperDistanceRisk), Is.True);
            Assert.That(float.IsNaN(helperRotationRisk), Is.True);
            Assert.That(float.IsNaN(webbingRisk), Is.True);
        }

        private static bool TryCalculateThumbHelperRelationshipRisk(
            float currentDistance,
            float initialDistance,
            float rotationDelta,
            float spreadRisk,
            float projectionRisk,
            out float helperDistanceRisk,
            out float helperRotationRisk,
            out float webbingRisk)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateThumbHelperRelationshipRisk",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ThumbHelperRelationshipRiskParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for thumb helper relationship risk calculation.");

            object[] args =
            {
                currentDistance,
                initialDistance,
                rotationDelta,
                spreadRisk,
                projectionRisk,
                float.NaN,
                float.NaN,
                float.NaN
            };

            bool calculated = (bool)method.Invoke(null, args);
            helperDistanceRisk = (float)args[5];
            helperRotationRisk = (float)args[6];
            webbingRisk = (float)args[7];
            return calculated;
        }
    }
}
