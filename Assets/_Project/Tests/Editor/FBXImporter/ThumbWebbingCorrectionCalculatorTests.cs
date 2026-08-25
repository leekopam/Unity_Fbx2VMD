using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ThumbWebbingCorrectionCalculatorTests
    {
        [Test]
        public void Given_NoPoseRisk_When_CalculatingSettings_Then_ReturnsClampedBaseLimits()
        {
            object[] result = CalculateEffectiveSettings(
                configuredWeight: 0.8f,
                configuredMaxLocalAngle: 10f,
                configuredMaxPositionOffset: 0.01f,
                baseMaxLocalAngle: 60f,
                baseMaxPositionOffset: 0.03f,
                poseRisk: 0f,
                dynamicMinLocalAngle: 15f,
                dynamicMinPositionOffset: 0.005f);

            Assert.That((float)result[8], Is.Zero);
            Assert.That((float)result[9], Is.EqualTo(45f));
            Assert.That((float)result[10], Is.EqualTo(0.02f));
        }

        [Test]
        public void Given_FullPoseRisk_When_CalculatingSettings_Then_UsesNarrowestConfiguredLimits()
        {
            object[] result = CalculateEffectiveSettings(
                configuredWeight: 0.8f,
                configuredMaxLocalAngle: 20f,
                configuredMaxPositionOffset: 0.01f,
                baseMaxLocalAngle: 30f,
                baseMaxPositionOffset: 0.015f,
                poseRisk: 1f,
                dynamicMinLocalAngle: 15f,
                dynamicMinPositionOffset: 0.005f);

            Assert.That((float)result[8], Is.EqualTo(0.8f));
            Assert.That((float)result[9], Is.EqualTo(15f));
            Assert.That((float)result[10], Is.EqualTo(0.005f));
        }

        [Test]
        public void Given_PartialPoseRisk_When_CalculatingSettings_Then_InterpolatesFromBaseLimits()
        {
            object[] result = CalculateEffectiveSettings(
                configuredWeight: 0.8f,
                configuredMaxLocalAngle: 10f,
                configuredMaxPositionOffset: 0.004f,
                baseMaxLocalAngle: 30f,
                baseMaxPositionOffset: 0.02f,
                poseRisk: 0.5f,
                dynamicMinLocalAngle: 15f,
                dynamicMinPositionOffset: 0.005f);

            Assert.That((float)result[8], Is.EqualTo(0.4f));
            Assert.That((float)result[9], Is.EqualTo(20f));
            Assert.That((float)result[10], Is.EqualTo(0.012f).Within(0.000001f));
        }

        [Test]
        public void Given_NonFinitePoseRisk_When_CalculatingSettings_Then_ReturnsBaseLimits()
        {
            object[] result = CalculateEffectiveSettings(
                configuredWeight: 1f,
                configuredMaxLocalAngle: 5f,
                configuredMaxPositionOffset: 0.001f,
                baseMaxLocalAngle: 25f,
                baseMaxPositionOffset: 0.01f,
                poseRisk: float.NaN,
                dynamicMinLocalAngle: 10f,
                dynamicMinPositionOffset: 0.002f);

            Assert.That((float)result[8], Is.Zero);
            Assert.That((float)result[9], Is.EqualTo(25f));
            Assert.That((float)result[10], Is.EqualTo(0.01f));
        }

        [Test]
        public void Given_WeightedTargetBeyondLimit_When_ConstrainingPosition_Then_BlendsBeforeClamping()
        {
            Vector3 result = ConstrainPosition(
                Vector3.zero,
                new Vector3(0.02f, 0f, 0f),
                weight: 0.5f,
                maxOffset: 0.006f);

            Assert.That(result.x, Is.EqualTo(0.006f).Within(0.000001f));
            Assert.That(result.y, Is.Zero);
            Assert.That(result.z, Is.Zero);
        }

        [Test]
        public void Given_ZeroOffsetLimit_When_ConstrainingPosition_Then_ReturnsInitialPosition()
        {
            Vector3 initialPosition = new Vector3(1f, 2f, 3f);

            Vector3 result = ConstrainPosition(
                initialPosition,
                new Vector3(5f, 6f, 7f),
                weight: 0f,
                maxOffset: 0f);

            Assert.That(result, Is.EqualTo(initialPosition));
        }

        private static object[] CalculateEffectiveSettings(
            float configuredWeight,
            float configuredMaxLocalAngle,
            float configuredMaxPositionOffset,
            float baseMaxLocalAngle,
            float baseMaxPositionOffset,
            float poseRisk,
            float dynamicMinLocalAngle,
            float dynamicMinPositionOffset)
        {
            MethodInfo method = ResolveCalculatorMethod("CalculateEffectiveSettings");
            object[] arguments =
            {
                configuredWeight,
                configuredMaxLocalAngle,
                configuredMaxPositionOffset,
                baseMaxLocalAngle,
                baseMaxPositionOffset,
                poseRisk,
                dynamicMinLocalAngle,
                dynamicMinPositionOffset,
                0f,
                0f,
                0f
            };
            method.Invoke(null, arguments);
            return arguments;
        }

        private static Vector3 ConstrainPosition(
            Vector3 initialPosition,
            Vector3 targetPosition,
            float weight,
            float maxOffset)
        {
            MethodInfo method = ResolveCalculatorMethod("ConstrainPosition");
            return (Vector3)method.Invoke(
                null,
                new object[] { initialPosition, targetPosition, weight, maxOffset });
        }

        private static MethodInfo ResolveCalculatorMethod(string methodName)
        {
            Type calculatorType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ThumbWebbingCorrectionCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method;
        }
    }
}
