using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ThumbLocalRotationCalculatorTests
    {
        [Test]
        public void Given_LocalRotationLimitPolicy_When_CheckingOwnership_Then_CalculatorOwnsItWithoutRetargeterDuplicate()
        {
            Type calculatorType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ThumbLocalRotationCalculator",
                throwOnError: true);
            MethodInfo calculatorMethod = calculatorType.GetMethod(
                "LimitLocalRotation",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo duplicateMethod = typeof(PoseSpaceRetargeter).GetMethod(
                "LimitThumbLocalRotation",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(calculatorMethod, Is.Not.Null);
            Assert.That(calculatorMethod.IsAssembly, Is.True);
            Assert.That(duplicateMethod, Is.Null);
        }

        [Test]
        public void Given_ZeroLimitAndChangedRotation_When_CalculatingCorrection_Then_RestoresInitialRotation()
        {
            Quaternion initialRotation = Quaternion.identity;
            Quaternion rawRotation = Quaternion.Euler(20f, 0f, 0f);

            bool corrected = TryCalculateCorrection(
                initialRotation,
                rawRotation,
                rawRotation,
                Quaternion.identity,
                0f,
                out Quaternion correctedRotation);

            Assert.That(corrected, Is.True);
            Assert.That(Quaternion.Angle(correctedRotation, initialRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_ZeroLimitAndInitialRotation_When_CalculatingCorrection_Then_ReturnsFalse()
        {
            bool corrected = TryCalculateCorrection(
                Quaternion.identity,
                Quaternion.identity,
                Quaternion.identity,
                Quaternion.identity,
                0f,
                out Quaternion correctedRotation);

            Assert.That(corrected, Is.False);
            Assert.That(Quaternion.Angle(correctedRotation, Quaternion.identity), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_RawRotationWithinLimitButDisplayDiffers_When_CalculatingCorrection_Then_RestoresRawRotation()
        {
            Quaternion rawRotation = Quaternion.Euler(5f, 0f, 0f);

            bool corrected = TryCalculateCorrection(
                Quaternion.identity,
                rawRotation,
                Quaternion.identity,
                Quaternion.identity,
                10f,
                out Quaternion correctedRotation);

            Assert.That(corrected, Is.True);
            Assert.That(Quaternion.Angle(correctedRotation, rawRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_RawAndDisplayRotationsMatchWithinLimit_When_CalculatingCorrection_Then_ReturnsFalse()
        {
            Quaternion rawRotation = Quaternion.Euler(5f, 0f, 0f);

            bool corrected = TryCalculateCorrection(
                Quaternion.identity,
                rawRotation,
                rawRotation,
                Quaternion.identity,
                10f,
                out Quaternion correctedRotation);

            Assert.That(corrected, Is.False);
            Assert.That(Quaternion.Angle(correctedRotation, rawRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void Given_RotationExceedsSoftLimit_When_CalculatingCorrection_Then_AllowsBoundedOvershoot()
        {
            bool corrected = TryCalculateCorrection(
                Quaternion.identity,
                Quaternion.Euler(30f, 0f, 0f),
                Quaternion.Euler(30f, 0f, 0f),
                Quaternion.identity,
                10f,
                out Quaternion correctedRotation);

            Assert.That(corrected, Is.True);
            Assert.That(Quaternion.Angle(Quaternion.identity, correctedRotation), Is.EqualTo(17f).Within(0.02f));
        }

        private static bool TryCalculateCorrection(
            Quaternion initialRotation,
            Quaternion rawRotation,
            Quaternion currentDisplayedRotation,
            Quaternion limitSpaceOffset,
            float softLimit,
            out Quaternion correctedRotation)
        {
            Type calculatorType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ThumbLocalRotationCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                "TryCalculateCorrection",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] args =
            {
                initialRotation,
                rawRotation,
                currentDisplayedRotation,
                limitSpaceOffset,
                softLimit,
                Quaternion.identity
            };
            bool corrected = (bool)method.Invoke(null, args);
            correctedRotation = (Quaternion)args[5];
            return corrected;
        }
    }
}
