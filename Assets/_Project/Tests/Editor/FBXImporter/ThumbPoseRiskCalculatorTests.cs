using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class ThumbPoseRiskCalculatorTests
    {
        [Test]
        public void Given_ValueWithinWarning_When_CalculatingAboveThresholdRisk_Then_ReturnsZero()
        {
            Assert.That(CalculateAboveThreshold(0.003f, 0.003f, 0.008f), Is.EqualTo(0f));
        }

        [Test]
        public void Given_ValueBetweenWarningAndFullRisk_When_CalculatingAboveThresholdRisk_Then_ScalesLinearly()
        {
            Assert.That(CalculateAboveThreshold(0.006f, 0.003f, 0.008f), Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void Given_NonIncreasingThresholds_When_CalculatingAboveThresholdRisk_Then_UsesStepRisk()
        {
            Assert.That(CalculateAboveThreshold(10f, 10f, 10f), Is.EqualTo(0f));
            Assert.That(CalculateAboveThreshold(11f, 10f, 10f), Is.EqualTo(1f));
        }

        [Test]
        public void Given_ValueOutsideAllowedRange_When_CalculatingRangeRisk_Then_UsesNearestBoundaryDistance()
        {
            Assert.That(CalculateOutsideRange(0.4f, 0.3f, 0.5f, 0.2f), Is.EqualTo(0f));
            Assert.That(CalculateOutsideRange(0.2f, 0.3f, 0.5f, 0.2f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(CalculateOutsideRange(0.7f, 0.3f, 0.5f, 0.2f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_FiniteAndNonFiniteRisks_When_FindingMaximum_Then_IgnoresNonFiniteValues()
        {
            Assert.That(FindMaximumFinite(float.NaN, 0.25f, 0.75f), Is.EqualTo(0.75f));
            Assert.That(float.IsNaN(FindMaximumFinite(float.NaN, float.PositiveInfinity)), Is.True);
        }

        [Test]
        public void Given_HelperValuesWithinWarnings_When_CalculatingRelationshipRisk_Then_ReturnsZeroRisks()
        {
            bool calculated = TryCalculateHelperRelationshipRisk(
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
        public void Given_HelperWarningsExceeded_When_CalculatingRelationshipRisk_Then_ScalesRisks()
        {
            bool calculated = TryCalculateHelperRelationshipRisk(
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
        public void Given_NoFiniteHelperRisk_When_CalculatingRelationshipRisk_Then_ReturnsFalse()
        {
            bool calculated = TryCalculateHelperRelationshipRisk(
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

        private static float CalculateAboveThreshold(float value, float warningThreshold, float fullRiskThreshold)
        {
            return (float)ResolveCalculatorMethod(
                "CalculateAboveThreshold",
                typeof(float),
                typeof(float),
                typeof(float)).Invoke(null, new object[] { value, warningThreshold, fullRiskThreshold });
        }

        private static float CalculateOutsideRange(
            float value,
            float minimum,
            float maximum,
            float fullRiskDistance)
        {
            return (float)ResolveCalculatorMethod(
                "CalculateOutsideRange",
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float)).Invoke(null, new object[] { value, minimum, maximum, fullRiskDistance });
        }

        private static float FindMaximumFinite(params float[] values)
        {
            return (float)ResolveCalculatorMethod(
                "FindMaximumFinite",
                typeof(float[])).Invoke(null, new object[] { values });
        }

        private static bool TryCalculateHelperRelationshipRisk(
            float currentDistance,
            float initialDistance,
            float rotationDelta,
            float spreadRisk,
            float projectionRisk,
            out float helperDistanceRisk,
            out float helperRotationRisk,
            out float webbingRisk)
        {
            MethodInfo method = ResolveCalculatorMethod(
                "TryCalculateHelperRelationshipRisk",
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float).MakeByRefType(),
                typeof(float).MakeByRefType(),
                typeof(float).MakeByRefType());
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

        private static MethodInfo ResolveCalculatorMethod(string methodName, params Type[] parameterTypes)
        {
            Type calculatorType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ThumbPoseRiskCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
        }
    }
}
