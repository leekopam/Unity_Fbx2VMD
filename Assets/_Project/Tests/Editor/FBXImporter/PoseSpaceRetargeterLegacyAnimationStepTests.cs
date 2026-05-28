using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterLegacyAnimationStepTests
    {
        private static readonly Type[] ManualAdvanceParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] EditorPoseReferenceEnabledParameterTypes =
        {
            typeof(bool),
            typeof(bool),
            typeof(int)
        };

        private static readonly Type[] VisualPoseSpikeParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(bool).MakeByRefType()
        };

        private static readonly Type[] VisualPoseSpikeWeightParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool)
        };

        [Test]
        public void Given_FullBodyReferenceEnabledWithoutFingerMuscles_When_DeterminingEditorPoseReferenceUse_Then_UsesReference()
        {
            bool shouldUseReference = ShouldUseEditorPoseReference(
                enableFingerPoseReference: false,
                enableFullBodyPoseReference: true,
                fingerReferenceMuscleCount: 0);

            Assert.That(shouldUseReference, Is.True);
        }

        [Test]
        public void Given_BodyPositionSpike_When_DeterminingVisualPoseSmoothing_Then_SmoothsWithoutMuscleOnlySkip()
        {
            bool shouldSmooth = ShouldSmoothVisualPoseSpike(
                maxMuscleDelta: 0.5f,
                bodyPositionDelta: 0.081f,
                bodyRotationDelta: 0f,
                poseVisualMuscleDeltaThreshold: 0.35f,
                legacyAnimationStepSpikeThisFrame: false,
                out bool muscleDeltaOnlySpike);

            Assert.That(shouldSmooth, Is.True);
            Assert.That(muscleDeltaOnlySpike, Is.False);
        }

        [Test]
        public void Given_MuscleOnlySpike_When_DeterminingVisualPoseSmoothing_Then_DoesNotSmoothAndReportsMuscleOnlySkip()
        {
            bool shouldSmooth = ShouldSmoothVisualPoseSpike(
                maxMuscleDelta: 0.5f,
                bodyPositionDelta: 0.02f,
                bodyRotationDelta: 5f,
                poseVisualMuscleDeltaThreshold: 0.35f,
                legacyAnimationStepSpikeThisFrame: false,
                out bool muscleDeltaOnlySpike);

            Assert.That(shouldSmooth, Is.False);
            Assert.That(muscleDeltaOnlySpike, Is.True);
        }

        [Test]
        public void Given_LegacyAnimationStepSpike_When_DeterminingVisualPoseSmoothing_Then_Smooths()
        {
            bool shouldSmooth = ShouldSmoothVisualPoseSpike(
                maxMuscleDelta: 0f,
                bodyPositionDelta: 0f,
                bodyRotationDelta: 0f,
                poseVisualMuscleDeltaThreshold: 0.35f,
                legacyAnimationStepSpikeThisFrame: true,
                out bool muscleDeltaOnlySpike);

            Assert.That(shouldSmooth, Is.True);
            Assert.That(muscleDeltaOnlySpike, Is.False);
        }

        [Test]
        public void Given_BodyPositionSpike_When_CalculatingVisualPoseSpikeWeight_Then_UsesStrongOutlierClamp()
        {
            float weight = CalculateVisualPoseSpikeCurrentWeight(
                configuredWeight: 0.65f,
                bodyPositionDelta: 0.41f,
                bodyRotationDelta: 8f,
                legacyAnimationStepSpikeThisFrame: false);

            Assert.That(weight, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void Given_PlayModeAndStalledState_When_CalculatingManualLegacyTime_Then_AdvancesByDeltaTimeAndSpeed()
        {
            bool advanced = TryCalculateManualLegacyAnimationTime(
                currentTime: 0.25f,
                previousTime: 0.25f,
                length: 1f,
                playbackSpeed: 2f,
                deltaTime: 0.05f,
                isPlaying: true,
                out float advancedTime);

            Assert.That(advanced, Is.True);
            Assert.That(advancedTime, Is.EqualTo(0.35f).Within(0.0001f));
        }

        [Test]
        public void Given_ZeroPlaybackSpeed_When_CalculatingManualLegacyTime_Then_UsesNormalPlaybackStep()
        {
            bool advanced = TryCalculateManualLegacyAnimationTime(
                currentTime: 0.25f,
                previousTime: 0.25f,
                length: 1f,
                playbackSpeed: 0f,
                deltaTime: 0.05f,
                isPlaying: true,
                out float advancedTime);

            Assert.That(advanced, Is.True);
            Assert.That(advancedTime, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void Given_ManualStepWouldPassClipEnd_When_CalculatingManualLegacyTime_Then_ClampsToClipLength()
        {
            bool advanced = TryCalculateManualLegacyAnimationTime(
                currentTime: 0.98f,
                previousTime: 0.98f,
                length: 1f,
                playbackSpeed: 2f,
                deltaTime: 0.1f,
                isPlaying: true,
                out float advancedTime);

            Assert.That(advanced, Is.True);
            Assert.That(advancedTime, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_EditorMode_When_CalculatingManualLegacyTime_Then_DoesNotAdvance()
        {
            bool advanced = TryCalculateManualLegacyAnimationTime(
                currentTime: 0.25f,
                previousTime: 0.25f,
                length: 1f,
                playbackSpeed: 2f,
                deltaTime: 0.05f,
                isPlaying: false,
                out float advancedTime);

            Assert.That(advanced, Is.False);
            Assert.That(advancedTime, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Given_CurrentTimeAlreadyAdvanced_When_CalculatingManualLegacyTime_Then_DoesNotAdvance()
        {
            bool advanced = TryCalculateManualLegacyAnimationTime(
                currentTime: 0.31f,
                previousTime: 0.25f,
                length: 1f,
                playbackSpeed: 2f,
                deltaTime: 0.05f,
                isPlaying: true,
                out float advancedTime);

            Assert.That(advanced, Is.False);
            Assert.That(advancedTime, Is.EqualTo(0.31f).Within(0.0001f));
        }

        [Test]
        public void Given_CurrentTimeLoopedBack_When_CalculatingManualLegacyTime_Then_DoesNotAdvance()
        {
            bool advanced = TryCalculateManualLegacyAnimationTime(
                currentTime: 0.05f,
                previousTime: 0.95f,
                length: 1f,
                playbackSpeed: 1f,
                deltaTime: 0.05f,
                isPlaying: true,
                out float advancedTime);

            Assert.That(advanced, Is.False);
            Assert.That(advancedTime, Is.EqualTo(0.05f).Within(0.0001f));
        }

        private static bool TryCalculateManualLegacyAnimationTime(
            float currentTime,
            float previousTime,
            float length,
            float playbackSpeed,
            float deltaTime,
            bool isPlaying,
            out float advancedTime)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryCalculateManualLegacyAnimationTime",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ManualAdvanceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for Legacy Animation manual advance timing.");

            object[] args =
            {
                currentTime,
                previousTime,
                length,
                playbackSpeed,
                deltaTime,
                isPlaying,
                currentTime
            };

            bool advanced = (bool)method.Invoke(null, args);
            advancedTime = (float)args[6];
            return advanced;
        }

        private static bool ShouldUseEditorPoseReference(
            bool enableFingerPoseReference,
            bool enableFullBodyPoseReference,
            int fingerReferenceMuscleCount)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ShouldUseEditorPoseReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorPoseReferenceEnabledParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for editor pose reference enablement.");

            return (bool)method.Invoke(null, new object[] { enableFingerPoseReference, enableFullBodyPoseReference, fingerReferenceMuscleCount });
        }

        private static bool ShouldSmoothVisualPoseSpike(
            float maxMuscleDelta,
            float bodyPositionDelta,
            float bodyRotationDelta,
            float poseVisualMuscleDeltaThreshold,
            bool legacyAnimationStepSpikeThisFrame,
            out bool muscleDeltaOnlySpike)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ShouldSmoothVisualPoseSpike",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: VisualPoseSpikeParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for visual pose spike smoothing decisions.");

            object[] args =
            {
                maxMuscleDelta,
                bodyPositionDelta,
                bodyRotationDelta,
                poseVisualMuscleDeltaThreshold,
                legacyAnimationStepSpikeThisFrame,
                false
            };

            bool shouldSmooth = (bool)method.Invoke(null, args);
            muscleDeltaOnlySpike = (bool)args[5];
            return shouldSmooth;
        }

        private static float CalculateVisualPoseSpikeCurrentWeight(
            float configuredWeight,
            float bodyPositionDelta,
            float bodyRotationDelta,
            bool legacyAnimationStepSpikeThisFrame)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "CalculateVisualPoseSpikeCurrentWeight",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: VisualPoseSpikeWeightParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a pure static helper for visual pose spike blend weight.");

            return (float)method.Invoke(null, new object[]
            {
                configuredWeight,
                bodyPositionDelta,
                bodyRotationDelta,
                legacyAnimationStepSpikeThisFrame
            });
        }
    }
}
