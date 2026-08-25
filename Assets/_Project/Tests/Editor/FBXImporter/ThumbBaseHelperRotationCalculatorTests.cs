using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ThumbBaseHelperRotationCalculatorTests
    {
        [Test]
        public void Given_MissingSourceBaseline_When_CalculatingBaseRotation_Then_UsesSourceRotation()
        {
            Quaternion sourceRotation = Quaternion.Euler(20f, 10f, 0f);

            Quaternion result = CalculateBaseRotation(
                Quaternion.identity,
                sourceRotation,
                hasSourceInitialRotation: false,
                Quaternion.identity,
                syncEnabled: true,
                syncWeight: 1f,
                Quaternion.identity,
                Quaternion.identity,
                stabilizePalm: false,
                palmWeight: 0f);

            Assert.That(Quaternion.Angle(result, sourceRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_SourceBaseline_When_CalculatingBaseRotation_Then_AppliesSourceDeltaToHelperBaseline()
        {
            Quaternion helperInitialRotation = Quaternion.Euler(0f, 10f, 0f);
            Quaternion sourceInitialRotation = Quaternion.Euler(20f, 0f, 0f);
            Quaternion sourceRotation = Quaternion.Euler(40f, 0f, 0f);
            Quaternion expected = helperInitialRotation *
                (Quaternion.Inverse(sourceInitialRotation) * sourceRotation);

            Quaternion result = CalculateBaseRotation(
                helperInitialRotation,
                sourceRotation,
                hasSourceInitialRotation: true,
                sourceInitialRotation,
                syncEnabled: true,
                syncWeight: 1f,
                Quaternion.identity,
                Quaternion.identity,
                stabilizePalm: false,
                palmWeight: 0f);

            Assert.That(Quaternion.Angle(result, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_PartialSyncWeight_When_CalculatingBaseRotation_Then_InterpolatesFromHelperBaseline()
        {
            Quaternion helperInitialRotation = Quaternion.identity;
            Quaternion sourceRotation = Quaternion.Euler(40f, 0f, 0f);
            Quaternion expected = Quaternion.Slerp(helperInitialRotation, sourceRotation, 0.5f);

            Quaternion result = CalculateBaseRotation(
                helperInitialRotation,
                sourceRotation,
                hasSourceInitialRotation: false,
                Quaternion.identity,
                syncEnabled: true,
                syncWeight: 0.5f,
                Quaternion.identity,
                Quaternion.identity,
                stabilizePalm: false,
                palmWeight: 0f);

            Assert.That(Quaternion.Angle(result, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_TargetOffsetAndPalmStabilization_When_CalculatingBaseRotation_Then_AppliesThemInOrder()
        {
            Quaternion helperInitialRotation = Quaternion.identity;
            Quaternion targetOffset = Quaternion.Euler(20f, 0f, 0f);
            Quaternion expected = Quaternion.Slerp(targetOffset, helperInitialRotation, 0.5f);

            Quaternion result = CalculateBaseRotation(
                helperInitialRotation,
                Quaternion.identity,
                hasSourceInitialRotation: false,
                Quaternion.identity,
                syncEnabled: false,
                syncWeight: 0f,
                Quaternion.identity,
                targetOffset,
                stabilizePalm: true,
                palmWeight: 0.5f);

            Assert.That(Quaternion.Angle(result, expected), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_FullWebbingStabilization_When_FinalizingRotation_Then_ReturnsHelperBaseline()
        {
            Quaternion result = FinalizeRotation(
                Quaternion.identity,
                Quaternion.Euler(40f, 0f, 0f),
                stabilizeWebbing: true,
                webbingWeight: 1f,
                helperMaxAngle: 45f,
                stabilizePalm: false,
                palmWeight: 0f,
                palmMaxAngle: 45f,
                webbingMaxAngle: 45f);

            Assert.That(Quaternion.Angle(result, Quaternion.identity), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_TargetExceedsPalmAngleLimit_When_FinalizingRotation_Then_UsesNarrowestLimit()
        {
            Quaternion result = FinalizeRotation(
                Quaternion.identity,
                Quaternion.Euler(40f, 0f, 0f),
                stabilizeWebbing: false,
                webbingWeight: 0f,
                helperMaxAngle: 45f,
                stabilizePalm: true,
                palmWeight: 0.5f,
                palmMaxAngle: 10f,
                webbingMaxAngle: 45f);

            Assert.That(Quaternion.Angle(Quaternion.identity, result), Is.EqualTo(10f).Within(0.02f));
        }

        private static Quaternion CalculateBaseRotation(
            Quaternion helperInitialRotation,
            Quaternion sourceRotation,
            bool hasSourceInitialRotation,
            Quaternion sourceInitialRotation,
            bool syncEnabled,
            float syncWeight,
            Quaternion deltaAxisRemap,
            Quaternion targetRotationOffset,
            bool stabilizePalm,
            float palmWeight)
        {
            MethodInfo method = ResolveCalculatorMethod("CalculateBaseRotation");
            return (Quaternion)method.Invoke(
                null,
                new object[]
                {
                    helperInitialRotation,
                    sourceRotation,
                    hasSourceInitialRotation,
                    sourceInitialRotation,
                    syncEnabled,
                    syncWeight,
                    deltaAxisRemap,
                    targetRotationOffset,
                    stabilizePalm,
                    palmWeight
                });
        }

        private static Quaternion FinalizeRotation(
            Quaternion helperInitialRotation,
            Quaternion targetRotation,
            bool stabilizeWebbing,
            float webbingWeight,
            float helperMaxAngle,
            bool stabilizePalm,
            float palmWeight,
            float palmMaxAngle,
            float webbingMaxAngle)
        {
            MethodInfo method = ResolveCalculatorMethod("FinalizeRotation");
            return (Quaternion)method.Invoke(
                null,
                new object[]
                {
                    helperInitialRotation,
                    targetRotation,
                    stabilizeWebbing,
                    webbingWeight,
                    helperMaxAngle,
                    stabilizePalm,
                    palmWeight,
                    palmMaxAngle,
                    webbingMaxAngle
                });
        }

        private static MethodInfo ResolveCalculatorMethod(string methodName)
        {
            Type calculatorType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ThumbBaseHelperRotationCalculator",
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
