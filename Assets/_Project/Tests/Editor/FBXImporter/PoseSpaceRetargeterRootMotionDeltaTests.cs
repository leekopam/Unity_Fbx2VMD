using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Retargeting;
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

        private static readonly Type[] BodyPositionRootMotionSourceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(bool),
            typeof(bool)
        };

        [Test]
        public void Given_RootMotionGuardOwnsRootDelta_When_CheckingPoseSpaceRetargeterContract_Then_DoesNotKeepDuplicateHelpers()
        {
            MethodInfo rootDeltaMethod = typeof(RootMotionGuard).GetMethod(
                "CalculateRetargetRootDelta",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: RootMotionDeltaParameterTypes,
                modifiers: null);
            MethodInfo normalizeMethod = typeof(RootMotionGuard).GetMethod(
                "NormalizeMovementScaleMultiplier",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: MovementScaleMultiplierParameterTypes,
                modifiers: null);

            Assert.That(rootDeltaMethod, Is.Not.Null);
            Assert.That(normalizeMethod, Is.Not.Null);
            Assert.That(typeof(PoseSpaceRetargeter).GetMethod(
                "CalculateRetargetRootDelta",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RootMotionDeltaParameterTypes,
                modifiers: null), Is.Null);
            Assert.That(typeof(PoseSpaceRetargeter).GetMethod(
                "NormalizeMovementScaleMultiplier",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: MovementScaleMultiplierParameterTypes,
                modifiers: null), Is.Null);
        }

        [Test]
        public void Given_FiniteInputsWithoutBodyRootPolicy_When_CalculatingRootMotionDelta_Then_CombinesScaledGhostAndEditorDelta()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(1f, 2f, 3f),
                scaleRatio: 2f,
                editorRootTranslationDelta: new Vector3(0.5f, 0.25f, -0.5f),
                bodyRootDelta: new Vector3(0.1f, -0.25f, 0.2f),
                movementScaleMultiplier: 0.5f,
                useBodyPositionXZRootMotion: false,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 10f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.False);
            Assert.That(delta.x, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(2.125f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(2.75f).Within(0.0001f));
            Assert.That(deltaMagnitude, Is.EqualTo(delta.magnitude).Within(0.0001f));
        }

        [Test]
        public void Given_ZeroMovementScale_When_CalculatingRootMotionDelta_Then_SuppressesGhostEditorAndBodyRootSources()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(1.5f, 0f, -2.5f),
                scaleRatio: 2f,
                editorRootTranslationDelta: new Vector3(0.25f, 0f, 0.5f),
                bodyRootDelta: new Vector3(-0.2f, 0f, 0.35f),
                movementScaleMultiplier: 0f,
                useBodyPositionXZRootMotion: false,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 0.006f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(delta, Is.EqualTo(Vector3.zero));
            Assert.That(deltaMagnitude, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.False);
        }

        [Test]
        public void Given_MainRecordingMovingRootPolicy_When_CalculatingRootMotionDelta_Then_PreservesBodyRootSourceWithoutLegacyDoubleCount()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(0.2f, 0f, 0.1f),
                scaleRatio: 1f,
                editorRootTranslationDelta: new Vector3(0.05f, 0f, 0.02f),
                bodyRootDelta: new Vector3(0.3f, 0f, 0.12f),
                movementScaleMultiplier: 1f,
                useBodyPositionXZRootMotion: true,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 10f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.False);
            Assert.That(delta.x, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(deltaMagnitude, Is.EqualTo(delta.magnitude).Within(0.0001f));
        }

        [Test]
        public void Given_MainRecordingMovingRootPolicyAndMovementScale_When_CalculatingRootMotionDelta_Then_ScalesBodyRootSourceOnly()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(0.2f, 0f, 0.1f),
                scaleRatio: 1f,
                editorRootTranslationDelta: new Vector3(0.05f, 0f, 0.02f),
                bodyRootDelta: new Vector3(0.3f, 0f, 0.12f),
                movementScaleMultiplier: 1.2f,
                useBodyPositionXZRootMotion: true,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 10f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.False);
            Assert.That(delta.x, Is.EqualTo(0.36f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(0.144f).Within(0.0001f));
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
                useBodyPositionXZRootMotion: false,
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
        public void Given_DeltaExceedsLimitAndClampEnabled_When_CalculatingRootMotionDelta_Then_LimitsDeltaAndReportsSpike()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(0.3f, 0f, 0.4f),
                scaleRatio: 1f,
                editorRootTranslationDelta: Vector3.zero,
                bodyRootDelta: Vector3.zero,
                movementScaleMultiplier: 1f,
                useBodyPositionXZRootMotion: false,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 0.25f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(delta.x, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(delta.magnitude, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(deltaMagnitude, Is.EqualTo(delta.magnitude).Within(0.0001f));
            Assert.That(skippedByNonFinite, Is.False);
            Assert.That(skippedBySpike, Is.True);
        }

        [Test]
        public void Given_DeltaExceedsLimitAndClampEnabled_When_CalculatingRootMotionDelta_Then_KeepsLimitedMovement()
        {
            Vector3 delta = CalculateRetargetRootDelta(
                ghostDelta: new Vector3(0.03f, 0f, 0.04f),
                scaleRatio: 1f,
                editorRootTranslationDelta: Vector3.zero,
                bodyRootDelta: Vector3.zero,
                movementScaleMultiplier: 1f,
                useBodyPositionXZRootMotion: false,
                clampRootDeltaSpikes: true,
                maxRootDeltaPerFrame: 0.006f,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool skippedBySpike);

            Assert.That(delta.x, Is.EqualTo(0.0036f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(0.0048f).Within(0.0001f));
            Assert.That(delta.magnitude, Is.EqualTo(0.006f).Within(0.0001f));
            Assert.That(deltaMagnitude, Is.EqualTo(delta.magnitude).Within(0.0001f));
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
                useBodyPositionXZRootMotion: false,
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
            Assert.That(NormalizeMovementScaleMultiplier(1.45f), Is.EqualTo(1.45f).Within(0.0001f));
            Assert.That(NormalizeMovementScaleMultiplier(1.75f), Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualBodyReferenceAvailable_When_SelectingBodyRootMotionSource_Then_PreservesFbxXZAndKeepsPoseY()
        {
            Vector3 source = SelectBodyPositionRootMotionSource(
                poseBodyPosition: new Vector3(0.1f, 1.2f, -0.2f),
                manualReferenceBodyPosition: new Vector3(-0.4f, 0.9f, 0.35f),
                hasManualReferenceBodyPosition: true,
                preferManualReferenceXZ: true);

            Assert.That(source.x, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(source.y, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(source.z, Is.EqualTo(-0.2f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualBodyReferenceUnavailable_When_SelectingBodyRootMotionSource_Then_KeepsPoseBodyPosition()
        {
            Vector3 poseBodyPosition = new Vector3(0.1f, 1.2f, -0.2f);

            Vector3 source = SelectBodyPositionRootMotionSource(
                poseBodyPosition: poseBodyPosition,
                manualReferenceBodyPosition: new Vector3(-0.4f, 0.9f, 0.35f),
                hasManualReferenceBodyPosition: false,
                preferManualReferenceXZ: true);

            Assert.That(source.x, Is.EqualTo(poseBodyPosition.x).Within(0.0001f));
            Assert.That(source.y, Is.EqualTo(poseBodyPosition.y).Within(0.0001f));
            Assert.That(source.z, Is.EqualTo(poseBodyPosition.z).Within(0.0001f));
        }

        private static Vector3 CalculateRetargetRootDelta(
            Vector3 ghostDelta,
            float scaleRatio,
            Vector3 editorRootTranslationDelta,
            Vector3 bodyRootDelta,
            float movementScaleMultiplier,
            bool useBodyPositionXZRootMotion,
            bool clampRootDeltaSpikes,
            float maxRootDeltaPerFrame,
            out float deltaMagnitude,
            out bool skippedByNonFinite,
            out bool skippedBySpike)
        {
            return RootMotionGuard.CalculateRetargetRootDelta(
                ghostDelta,
                scaleRatio,
                editorRootTranslationDelta,
                bodyRootDelta,
                movementScaleMultiplier,
                useBodyPositionXZRootMotion,
                clampRootDeltaSpikes,
                maxRootDeltaPerFrame,
                out deltaMagnitude,
                out skippedByNonFinite,
                out skippedBySpike);
        }

        private static float NormalizeMovementScaleMultiplier(float value)
        {
            return RootMotionGuard.NormalizeMovementScaleMultiplier(value);
        }

        private static Vector3 SelectBodyPositionRootMotionSource(
            Vector3 poseBodyPosition,
            Vector3 manualReferenceBodyPosition,
            bool hasManualReferenceBodyPosition,
            bool preferManualReferenceXZ)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "SelectBodyPositionRootMotionSource",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: BodyPositionRootMotionSourceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure helper for choosing the bodyPosition X/Z root-motion source.");

            return (Vector3)method.Invoke(null, new object[]
            {
                poseBodyPosition,
                manualReferenceBodyPosition,
                hasManualReferenceBodyPosition,
                preferManualReferenceXZ
            });
        }

    }
}
