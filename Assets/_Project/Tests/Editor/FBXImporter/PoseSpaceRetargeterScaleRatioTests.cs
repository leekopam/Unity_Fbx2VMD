using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterScaleRatioTests
    {
        private static readonly Type[] ScaleRatioParameterTypes =
        {
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(float),
            typeof(bool).MakeByRefType()
        };

        [Test]
        public void Given_ValidAnimatorScales_When_CalculatingScaleRatio_Then_UsesHumanScaleRatio()
        {
            float ratio = CalculateSafeScaleRatio(
                currentScaleRatio: 0.75f,
                hasAnimatorScale: true,
                ghostHumanScale: 2f,
                targetHumanScale: 1f,
                initialGhostHipHeight: 0.4f,
                initialTargetHipHeight: 1.2f,
                hasHipPositions: true,
                ghostHipY: 0.2f,
                targetHipY: 0.8f,
                out bool usedInvalidFallback);

            Assert.That(ratio, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(usedInvalidFallback, Is.False);
        }

        [Test]
        public void Given_InvalidAnimatorScalesAndCachedHipHeights_When_CalculatingScaleRatio_Then_UsesCachedHipRatio()
        {
            float ratio = CalculateSafeScaleRatio(
                currentScaleRatio: 0.75f,
                hasAnimatorScale: true,
                ghostHumanScale: 0f,
                targetHumanScale: 1f,
                initialGhostHipHeight: 0.5f,
                initialTargetHipHeight: 1.25f,
                hasHipPositions: true,
                ghostHipY: 0.2f,
                targetHipY: 0.8f,
                out bool usedInvalidFallback);

            Assert.That(ratio, Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(usedInvalidFallback, Is.False);
        }

        [Test]
        public void Given_NoCachedHeightsAndHipPositions_When_CalculatingScaleRatio_Then_UsesCurrentHipYRatio()
        {
            float ratio = CalculateSafeScaleRatio(
                currentScaleRatio: 0.75f,
                hasAnimatorScale: false,
                ghostHumanScale: 0f,
                targetHumanScale: 0f,
                initialGhostHipHeight: 0f,
                initialTargetHipHeight: 0f,
                hasHipPositions: true,
                ghostHipY: 0.4f,
                targetHipY: 1.2f,
                out bool usedInvalidFallback);

            Assert.That(ratio, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(usedInvalidFallback, Is.False);
        }

        [Test]
        public void Given_SelectedRatioExceedsLimit_When_CalculatingScaleRatio_Then_ClampsToMaximum()
        {
            float ratio = CalculateSafeScaleRatio(
                currentScaleRatio: 0.75f,
                hasAnimatorScale: true,
                ghostHumanScale: 0.1f,
                targetHumanScale: 2f,
                initialGhostHipHeight: 0f,
                initialTargetHipHeight: 0f,
                hasHipPositions: false,
                ghostHipY: 0f,
                targetHipY: 0f,
                out bool usedInvalidFallback);

            Assert.That(ratio, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(usedInvalidFallback, Is.False);
        }

        [Test]
        public void Given_SelectedRatioIsNonFinite_When_CalculatingScaleRatio_Then_FallsBackToOne()
        {
            float ratio = CalculateSafeScaleRatio(
                currentScaleRatio: 0.75f,
                hasAnimatorScale: false,
                ghostHumanScale: 0f,
                targetHumanScale: 0f,
                initialGhostHipHeight: 0.5f,
                initialTargetHipHeight: float.NaN,
                hasHipPositions: true,
                ghostHipY: 0.4f,
                targetHipY: 1.2f,
                out bool usedInvalidFallback);

            Assert.That(ratio, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(usedInvalidFallback, Is.True);
        }

        private static float CalculateSafeScaleRatio(
            float currentScaleRatio,
            bool hasAnimatorScale,
            float ghostHumanScale,
            float targetHumanScale,
            float initialGhostHipHeight,
            float initialTargetHipHeight,
            bool hasHipPositions,
            float ghostHipY,
            float targetHipY,
            out bool usedInvalidFallback)
        {
            Type calculatorType = typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingScaleRatioCalculator",
                throwOnError: false);
            Assert.That(calculatorType, Is.Not.Null, "RetargetingScaleRatioCalculator should own scale ratio selection.");

            MethodInfo method = calculatorType.GetMethod(
                "CalculateSafeScaleRatio",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ScaleRatioParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "RetargetingScaleRatioCalculator should expose the pure scale ratio calculation.");

            object[] args =
            {
                currentScaleRatio,
                hasAnimatorScale,
                ghostHumanScale,
                targetHumanScale,
                initialGhostHipHeight,
                initialTargetHipHeight,
                hasHipPositions,
                ghostHipY,
                targetHipY,
                false
            };

            float ratio = (float)method.Invoke(null, args);
            usedInvalidFallback = (bool)args[9];
            return ratio;
        }
    }
}
