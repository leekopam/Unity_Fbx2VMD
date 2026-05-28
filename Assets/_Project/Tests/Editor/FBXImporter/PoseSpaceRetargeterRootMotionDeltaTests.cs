using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterRootMotionDeltaTests
    {
        private static readonly Type[] RootMotionDeltaParameterTypes =
        {
            typeof(Vector3),
            typeof(float),
            typeof(Vector3),
            typeof(Vector3),
            typeof(float),
            typeof(bool),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(bool).MakeByRefType(),
            typeof(bool).MakeByRefType()
        };

        private static readonly Type[] MovementScaleMultiplierParameterTypes =
        {
            typeof(float)
        };

        [Test]
        public void Given_FiniteInputs_When_CalculatingRootMotionDelta_Then_CombinesScaledGhostEditorAndBodyDelta()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(1f, 2f, 3f),
                scaleRatio: 2f,
                editorRootTranslationDelta: new Vector3(0.5f, 0.25f, -0.5f),
                bodyRootDelta: new Vector3(0.1f, -0.25f, 0.2f),
                movementScaleMultiplier: 0.5f,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 10f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.False);
            Assert.That(delta.x, Is.EqualTo(1.3f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(2.85f).Within(0.0001f));
            Assert.That(deltaMagnitude, Is.EqualTo(delta.magnitude).Within(0.0001f));
        }

        [Test]
        public void Given_NonFiniteInput_When_CalculatingRootMotionDelta_Then_ReturnsZeroAndReportsNaN()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(float.NaN, 0f, 0f),
                scaleRatio: 1f,
                editorRootTranslationDelta: Vector3.zero,
                bodyRootDelta: Vector3.zero,
                movementScaleMultiplier: 1f,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 0.25f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(delta, Is.EqualTo(Vector3.zero));
            Assert.That(deltaMagnitude, Is.NaN);
            Assert.That(skippedByNonFinite, Is.True);
            Assert.That(skippedBySpike, Is.False);
        }

        [Test]
        public void Given_DeltaExceedsLimitAndClampEnabled_When_CalculatingRootMotionDelta_Then_ReturnsZeroAndReportsSpike()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(0.3f, 0f, 0.4f),
                scaleRatio: 1f,
                editorRootTranslationDelta: Vector3.zero,
                bodyRootDelta: Vector3.zero,
                movementScaleMultiplier: 1f,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 0.25f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(delta, Is.EqualTo(Vector3.zero));
            Assert.That(deltaMagnitude, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.True);
        }

        [Test]
        public void Given_DeltaExceedsLimitAndClampDisabled_When_CalculatingRootMotionDelta_Then_KeepsDelta()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(0.3f, 0f, 0.4f),
                scaleRatio: 1f,
                editorRootTranslationDelta: Vector3.zero,
                bodyRootDelta: Vector3.zero,
                movementScaleMultiplier: 1f,
                clampRootDeltaSpikes: false,
                maxRootDeltaPerFrame: 0.25f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(delta.x, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(deltaMagnitude, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.False);
        }

        [Test]
        public void Given_ZeroMovementScaleMultiplier_When_Normalizing_Then_AllowsStationaryRootMotion()
        {
            Assert.That(NormalizeMovementScaleMultiplier(0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(NormalizeMovementScaleMultiplier(-0.5f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(NormalizeMovementScaleMultiplier(1.5f), Is.EqualTo(1.2f).Within(0.0001f));
        }

        private static Vector3 CalculateRetargetRootDelta(
            Vector3 ghostDelta,
            float scaleRatio,
            Vector3 editorRootTranslationDelta,
            Vector3 bodyRootDelta,
            float movementScaleMultiplier,
            bool clampRootDeltaSpikes,
            float maxRootDeltaPerFrame,
            out float deltaMagnitude,
            out bool skippedByNonFinite,
            out bool skippedBySpike)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "CalculateRetargetRootDelta",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RootMotionDeltaParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for retarget root motion delta calculation.");

            object[] args =
            {
                ghostDelta,
                scaleRatio,
                editorRootTranslationDelta,
                bodyRootDelta,
                movementScaleMultiplier,
                clampRootDeltaSpikes,
                maxRootDeltaPerFrame,
                float.NaN,
                false,
                false
            };

            Vector3 delta = (Vector3)method.Invoke(null, args);
            deltaMagnitude = (float)args[7];
            skippedByNonFinite = (bool)args[8];
            skippedBySpike = (bool)args[9];
            return delta;
        }

        private static float NormalizeMovementScaleMultiplier(float value)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "NormalizeMovementScaleMultiplier",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: MovementScaleMultiplierParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for root motion scale normalization.");

            return (float)method.Invoke(null, new object[] { value });
        }
    }
}
