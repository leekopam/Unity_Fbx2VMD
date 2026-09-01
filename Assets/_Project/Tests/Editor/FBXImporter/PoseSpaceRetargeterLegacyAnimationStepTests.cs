using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class PoseSpaceRetargeterLegacyAnimationStepTests
    {
        private static Type LegacyAnimationDriverType =>
            typeof(PoseSpaceRetargeter).Assembly.GetType("Fbx2Vmd.FBXImporter.LegacyAnimationDriver", throwOnError: true);

        private static Type RetargetingPoseSmoothingType =>
            typeof(PoseSpaceRetargeter).Assembly.GetType("Fbx2Vmd.FBXImporter.RetargetingPoseSmoothing", throwOnError: true);

        private static Type RetargetingEndpointDiagnosticsType =>
            typeof(PoseSpaceRetargeter).Assembly.GetType("Fbx2Vmd.FBXImporter.RetargetingEndpointDiagnostics", throwOnError: true);

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

        private static readonly Type[] EndWrapClampParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(float),
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

        private static readonly Type[] VisualPoseSpikeMuscleBlendParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(bool),
            typeof(float)
        };

        private static readonly Type[] EditorHumanoidMuscleReferenceParameterTypes =
        {
            typeof(int)
        };

        private static readonly Type[] EditorHumanoidMuscleReferenceValueParameterTypes =
        {
            typeof(int),
            typeof(float)
        };

        private static readonly Type[] RetargetPoseInputMuscleTransformParameterTypes =
        {
            typeof(int),
            typeof(float)
        };

        private static readonly Type[] RetargetPoseInputReferenceAlignmentParameterTypes =
        {
            typeof(int),
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] FootHipsAlignedResidualYawReferenceParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(Vector3),
            typeof(Quaternion),
            typeof(float),
            typeof(float),
            typeof(Quaternion).MakeByRefType()
        };

        private static readonly Type[] FootHipsAlignedResidualYawSideAwareMaxAngleParameterTypes =
        {
            typeof(float),
            typeof(float),
            typeof(float),
            typeof(bool)
        };

        private static readonly Type[] EndpointPositionMaxYawAngleParameterTypes =
        {
            typeof(Vector3),
            typeof(Vector3),
            typeof(float)
        };

        private static readonly Type[] RetargetEndpointStageJumpParameterTypes =
        {
            typeof(string[]),
            typeof(Vector3[]),
            typeof(float),
            typeof(string).MakeByRefType(),
            typeof(Vector3).MakeByRefType(),
            typeof(float).MakeByRefType()
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
        public void Given_MainRecordingResidualBodyPositionSpike_When_DeterminingVisualPoseSmoothing_Then_Smooths()
        {
            bool shouldSmooth = ShouldSmoothVisualPoseSpike(
                maxMuscleDelta: 0.1f,
                bodyPositionDelta: 0.046524f,
                bodyRotationDelta: 5.580996f,
                poseVisualMuscleDeltaThreshold: 0.35f,
                legacyAnimationStepSpikeThisFrame: false,
                out bool muscleDeltaOnlySpike);

            Assert.That(shouldSmooth, Is.True);
            Assert.That(muscleDeltaOnlySpike, Is.False);
        }

        [Test]
        public void Given_MainRecordingHeadSpikeResidualBodyDelta_When_DeterminingVisualPoseSmoothing_Then_Smooths()
        {
            bool shouldSmooth = ShouldSmoothVisualPoseSpike(
                maxMuscleDelta: 0.1f,
                bodyPositionDelta: 0.027252f,
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
        public void Given_VisualPoseSmoothingCalculation_When_CheckingOwnership_Then_UsesDedicatedPureType()
        {
            Type smoothingType = typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingPoseSmoothing",
                throwOnError: false);
            Assert.That(smoothingType, Is.Not.Null,
                "RetargetingPoseSmoothing should own visual pose spike calculation.");

            string[] extractedMethodNames =
            {
                "ShouldSmoothVisualPoseSpike",
                "CalculateVisualPoseSpikeCurrentWeight",
                "BlendVisualPoseSpikeMuscle",
                "ClampForearmStretchVisualSpikeBlend",
                "IsBodyPoseSpike"
            };
            foreach (string methodName in extractedMethodNames)
            {
                Assert.That(
                    smoothingType.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Not.Null,
                    $"RetargetingPoseSmoothing should expose {methodName}.");
                Assert.That(
                    typeof(PoseSpaceRetargeter).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic),
                    Is.Null,
                    $"PoseSpaceRetargeter should delegate {methodName} calculation.");
            }
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
        public void Given_MainRecordingHeadSpikeResidualBodyDelta_When_CalculatingVisualPoseSpikeWeight_Then_UsesStrongOutlierClamp()
        {
            float weight = CalculateVisualPoseSpikeCurrentWeight(
                configuredWeight: 0.65f,
                bodyPositionDelta: 0.027252f,
                bodyRotationDelta: 0f,
                legacyAnimationStepSpikeThisFrame: false);

            Assert.That(weight, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void Given_EditorReferenceShoulderMuscle_When_BlendingVisualPoseSpike_Then_PreservesCurrentReferenceValue()
        {
            int leftShoulderFrontBackIndex = FindHumanMuscleIndex("Left Shoulder Front-Back");
            Assert.That(leftShoulderFrontBackIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the left shoulder front/back muscle.");

            float blended = BlendVisualPoseSpikeMuscle(
                previousValue: -0.041113f,
                currentValue: 1f,
                currentWeight: 0.1f,
                muscleIndex: leftShoulderFrontBackIndex,
                useEditorHumanoidMuscleReference: true,
                hasEditorHumanoidMuscleReferenceCurve: true);

            Assert.That(blended, Is.EqualTo(1f).Within(0.000001f),
                "Visual spike smoothing must not overwrite a shoulder muscle that was just restored from the editor Humanoid reference curve.");
        }

        [Test]
        public void Given_RowLocalForearmStretchSpike_When_BlendingVisualPoseSpikeWithClamp_Then_LimitsBlendAroundCurrent()
        {
            int leftForearmStretchIndex = FindHumanMuscleIndex("Left Forearm Stretch");
            Assert.That(leftForearmStretchIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the left forearm stretch muscle.");

            float blended = BlendVisualPoseSpikeMuscle(
                previousValue: 1.054f,
                currentValue: -0.738464f,
                currentWeight: 0.65f,
                muscleIndex: leftForearmStretchIndex,
                useEditorHumanoidMuscleReference: false,
                hasEditorHumanoidMuscleReferenceCurve: false,
                forearmStretchClampMaxOffset: 0.15f);

            Assert.That(blended, Is.EqualTo(-0.588464f).Within(0.0001f));
        }

        [Test]
        public void Given_BodyPoseSpikeForearmStretchRow_When_BlendingVisualPoseSpikeWithClamp_Then_LimitsBlendAroundCurrent()
        {
            int leftForearmStretchIndex = FindHumanMuscleIndex("Left Forearm Stretch");
            Assert.That(leftForearmStretchIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the left forearm stretch muscle.");

            float blended = BlendVisualPoseSpikeMuscle(
                previousValue: -0.0416f,
                currentValue: -0.738464f,
                currentWeight: 0.1f,
                muscleIndex: leftForearmStretchIndex,
                useEditorHumanoidMuscleReference: false,
                hasEditorHumanoidMuscleReferenceCurve: false,
                forearmStretchClampMaxOffset: 0.15f);

            Assert.That(blended, Is.EqualTo(-0.588464f).Within(0.0001f));
        }

        [Test]
        public void Given_Frame49StyleForearmValue_When_BlendingVisualPoseSpikeWithClamp_Then_KeepsDefaultSmoothing()
        {
            int rightForearmStretchIndex = FindHumanMuscleIndex("Right Forearm Stretch");
            Assert.That(rightForearmStretchIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right forearm stretch muscle.");

            float previousValue = 1.2955f;
            float currentValue = -0.051935f;
            float currentWeight = 0.65f;
            float blended = BlendVisualPoseSpikeMuscle(
                previousValue,
                currentValue,
                currentWeight,
                rightForearmStretchIndex,
                useEditorHumanoidMuscleReference: false,
                hasEditorHumanoidMuscleReferenceCurve: false,
                forearmStretchClampMaxOffset: 0.15f);

            Assert.That(blended, Is.EqualTo(Mathf.Lerp(previousValue, currentValue, currentWeight)).Within(0.0001f));
        }

        [Test]
        public void Given_EditorReferenceForearmStretchMuscle_When_CheckingReferenceUse_Then_DoesNotUseReference()
        {
            int rightForearmStretchIndex = FindHumanMuscleIndex("Right Forearm Stretch");
            Assert.That(rightForearmStretchIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right forearm stretch muscle.");

            bool shouldUseReference = ShouldUseEditorHumanoidMuscleReference(rightForearmStretchIndex);

            Assert.That(shouldUseReference, Is.False,
                "Forearm stretch editor curves can exceed HumanPose muscle range and should not override the live ghost pose.");
        }

        [Test]
        public void Given_EditorReferenceLeftUpperArmTwistMuscle_When_CheckingReferenceUse_Then_DoesNotUseReference()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            Assert.That(leftArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the left arm twist muscle.");

            bool shouldUseReference = ShouldUseEditorHumanoidMuscleReference(leftArmTwistIndex);

            Assert.That(shouldUseReference, Is.False,
                "Upper arm twist editor curves can over-rotate tail frames and should not override the live ghost pose.");
        }

        [Test]
        public void Given_EditorReferenceRightUpperArmTwistMuscle_When_CheckingReferenceUse_Then_UsesReference()
        {
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            Assert.That(rightArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right arm twist muscle.");

            bool shouldUseReference = ShouldUseEditorHumanoidMuscleReference(rightArmTwistIndex);

            Assert.That(shouldUseReference, Is.True,
                "Right upper arm twist ghost pose input can drift from the manual reference while the neighboring arm muscles stay aligned.");
        }

        [Test]
        public void Given_UpperArmTwistPoseInput_When_TransformingRetargetInput_Then_FlipsTwistSign()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            Assert.That(leftArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the left arm twist muscle.");
            Assert.That(rightArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right arm twist muscle.");

            Assert.That(TransformRetargetPoseInputMuscleValue(leftArmTwistIndex, 0.797207f), Is.EqualTo(-0.797207f).Within(0.000001f));
            Assert.That(TransformRetargetPoseInputMuscleValue(rightArmTwistIndex, -0.250876f), Is.EqualTo(-0.250876f).Within(0.000001f));

            int leftShoulderFrontBackIndex = FindHumanMuscleIndex("Left Shoulder Front-Back");
            Assert.That(TransformRetargetPoseInputMuscleValue(leftShoulderFrontBackIndex, 1f), Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void Given_LeftArmTwistInputOpposesBoundedReference_When_AligningRetargetInput_Then_FlipsSignOnly()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            Assert.That(leftArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the left arm twist muscle.");

            float aligned = AlignRetargetPoseInputWithEditorReference(leftArmTwistIndex, -0.760319f, 0.758726f);

            Assert.That(aligned, Is.EqualTo(0.760319f).Within(0.000001f),
                "ERINN left arm twist ghost input is sign-flipped while the bounded manual reference has the same magnitude.");
        }

        [Test]
        public void Given_LeftArmTwistInputOpposesOverrangeReference_When_AligningRetargetInput_Then_KeepsLiveInput()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            Assert.That(leftArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the left arm twist muscle.");

            float aligned = AlignRetargetPoseInputWithEditorReference(leftArmTwistIndex, -0.10761f, 2.917387f);

            Assert.That(aligned, Is.EqualTo(-0.10761f).Within(0.000001f),
                "tetoris left arm twist tail reference can over-rotate, so bounded sign alignment must not re-enable it.");
        }

        [Test]
        public void Given_RightArmTwistInputSharesModerateOverrangeReferenceSign_When_AligningRetargetInput_Then_FlipsSignOnly()
        {
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            Assert.That(rightArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right arm twist muscle.");

            float aligned = AlignRetargetPoseInputWithEditorReference(rightArmTwistIndex, 0.852882f, 2.083053f);

            Assert.That(aligned, Is.EqualTo(-0.852882f).Within(0.000001f),
                "ERINN right arm twist has a moderately overrange same-sign reference; use it as a sign hint, not as a full override.");
        }

        [Test]
        public void Given_RightArmTwistInputSharesLowerOverrangeReferenceSign_When_AligningRetargetInput_Then_KeepsLiveInput()
        {
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            Assert.That(rightArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right arm twist muscle.");

            float aligned = AlignRetargetPoseInputWithEditorReference(rightArmTwistIndex, 0.574437f, 1.862711f);

            Assert.That(aligned, Is.EqualTo(0.574437f).Within(0.000001f),
                "ERINN t30 is still aligned by keeping the live right arm twist input; sign flipping this lower overrange reference creates the residual.");
        }

        [Test]
        public void Given_RightUpperArmTwistReferenceIsModeratelyOverrange_When_CheckingReferenceValueUse_Then_DoesNotUseReference()
        {
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            Assert.That(rightArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right arm twist muscle.");

            bool shouldUseReference = ShouldApplyEditorHumanoidMuscleReferenceValue(rightArmTwistIndex, 2.083053f);

            Assert.That(shouldUseReference, Is.False,
                "A moderate overrange right arm twist curve is useful as a sign hint but should not replace the live pose value.");
        }

        [Test]
        public void Given_RightUpperArmTwistReferenceIsBounded_When_CheckingReferenceValueUse_Then_UsesReference()
        {
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            Assert.That(rightArmTwistIndex, Is.GreaterThanOrEqualTo(0), "Unity HumanTrait must expose the right arm twist muscle.");

            bool shouldUseReference = ShouldApplyEditorHumanoidMuscleReferenceValue(rightArmTwistIndex, -0.568725f);

            Assert.That(shouldUseReference, Is.True,
                "tetoris right arm twist needs the bounded manual reference curve to correct ghost-pose drift.");
        }

        [Test]
        public void Given_FootHipsAlignedResidualYawCorrection_When_TargetDirectionDiffers_Then_LimitsYawOnlyRotation()
        {
            bool calculated = TryCalculateEditorFootHipsAlignedResidualYawReference(
                desiredFootPosition: new Vector3(1f, 0f, 0f),
                currentFootPosition: new Vector3(0f, 0f, 1f),
                pivotPosition: Vector3.zero,
                currentParentWorldRotation: Quaternion.identity,
                weight: 1f,
                maxAngleDegrees: 15f,
                out Quaternion nextParentWorldRotation);

            Assert.That(calculated, Is.True);
            Assert.That(Quaternion.Angle(Quaternion.identity, nextParentWorldRotation), Is.EqualTo(15f).Within(0.0001f));

            Vector3 rotatedFootDirection = nextParentWorldRotation * Vector3.forward;
            Assert.That(rotatedFootDirection.y, Is.EqualTo(0f).Within(0.000001f),
                "The residual X/Z candidate must not introduce vertical foot movement while correcting a horizontal arc.");
            Assert.That(Vector3.Angle(rotatedFootDirection, Vector3.right), Is.EqualTo(75f).Within(0.0001f),
                "The candidate should reduce the hips-aligned foot residual without snapping directly to the reference foot target.");
        }

        [Test]
        public void Given_OneFootResidualAlreadyInsideGate_When_ResolvingYawMaxAngle_Then_ProtectsPassingSide()
        {
            float leftMaxAngle = ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
                thisFootResidual: 0.126f,
                otherFootResidual: 0.114f,
                requestedMaxAngle: 35f,
                isThisFootDominantResidual: true);
            float rightMaxAngle = ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
                thisFootResidual: 0.114f,
                otherFootResidual: 0.126f,
                requestedMaxAngle: 35f,
                isThisFootDominantResidual: false);

            Assert.That(leftMaxAngle, Is.EqualTo(35f).Within(0.0001f),
                "The failing side should be allowed to use the requested correction budget.");
            Assert.That(rightMaxAngle, Is.EqualTo(20f).Within(0.0001f),
                "The passing opposite side should stay capped so the fix does not move the failure to the other foot.");
        }

        [Test]
        public void Given_EndpointOffsetWithinFootRadius_When_CalculatingMaxYawAngle_Then_UsesReachableArc()
        {
            float maxYawAngle = CalculateEndpointPositionMaxYawAngle(
                currentFootPosition: new Vector3(0f, 0f, 1f),
                pivotPosition: Vector3.zero,
                maxOffset: 0.5f);

            Assert.That(maxYawAngle, Is.EqualTo(30f).Within(0.0001f));
        }

        [Test]
        public void Given_EndpointYawCalculation_When_CheckingOwnership_Then_UsesEndpointDiagnostics()
        {
            const BindingFlags CalculationFlags = BindingFlags.Static | BindingFlags.NonPublic;
            string[] methodNames =
            {
                "TryCalculateEditorFootHipsAlignedResidualYawReference",
                "ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle",
                "CalculateEndpointPositionMaxYawAngle"
            };

            foreach (string methodName in methodNames)
            {
                Assert.That(
                    RetargetingEndpointDiagnosticsType.GetMember(methodName, CalculationFlags),
                    Is.Not.Empty,
                    $"{methodName} must belong to RetargetingEndpointDiagnostics.");
                Assert.That(
                    typeof(PoseSpaceRetargeter).GetMember(methodName, CalculationFlags),
                    Is.Empty,
                    $"{methodName} must not remain in PoseSpaceRetargeter.");
            }
        }

        [Test]
        public void Given_LeftArmTwistStageDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties()
        {
            AssertReadableFloatProperty("LastPoseInputLeftArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterEditorMuscleReferenceLeftArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterClampPoseMusclesLeftArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterAnatomicalArmGuardLeftArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterVisualSpikeSmoothingLeftArmTwistMuscle");
            AssertReadableFloatProperty("LastSetHumanPoseInputLeftArmTwistMuscle");
            AssertReadableFloatProperty("LastSetHumanPoseOutputLeftArmTwistMuscle");
            AssertReadableFloatProperty("LastSetHumanPoseLeftArmTwistDelta");
        }

        [Test]
        public void Given_RightArmTwistStageDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties()
        {
            AssertReadableFloatProperty("LastPoseInputRightArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterEditorMuscleReferenceRightArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterClampPoseMusclesRightArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterAnatomicalArmGuardRightArmTwistMuscle");
            AssertReadableFloatProperty("LastAfterVisualSpikeSmoothingRightArmTwistMuscle");
            AssertReadableFloatProperty("LastSetHumanPoseInputRightArmTwistMuscle");
            AssertReadableFloatProperty("LastSetHumanPoseOutputRightArmTwistMuscle");
            AssertReadableFloatProperty("LastSetHumanPoseRightArmTwistDelta");
        }

        [Test]
        public void Given_RetargetEndpointStagesWithFirstJump_When_AttributingStage_Then_ReportsExactlyFirstStageDelta()
        {
            bool attributed = TryFindFirstRetargetEndpointStageJump(
                new[] { "pre_set", "after_set_human_pose", "after_manual_reference", "after_root_restore" },
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0.08f, 0f, -0.02f),
                    new Vector3(0.20f, 0f, -0.02f),
                    new Vector3(0.20f, 0f, -0.10f)
                },
                threshold: 0.05f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(attributed, Is.True);
            Assert.That(stage, Is.EqualTo("after_set_human_pose"));
            Assert.That(delta.x, Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(delta.z, Is.EqualTo(-0.02f).Within(0.0001f));
            Assert.That(magnitude, Is.EqualTo(new Vector3(0.08f, 0f, -0.02f).magnitude).Within(0.0001f));
        }

        [Test]
        public void Given_RetargetEndpointStagesWithinTolerance_When_AttributingStage_Then_ReturnsNoAttribution()
        {
            bool attributed = TryFindFirstRetargetEndpointStageJump(
                new[] { "pre_set", "after_set_human_pose", "after_manual_reference" },
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0.01f, 0f, 0f),
                    new Vector3(0.02f, 0f, 0f)
                },
                threshold: 0.05f,
                out string stage,
                out Vector3 delta,
                out float magnitude);

            Assert.That(attributed, Is.False);
            Assert.That(stage, Is.EqualTo(""));
            Assert.That(delta.x, Is.NaN);
            Assert.That(delta.y, Is.NaN);
            Assert.That(delta.z, Is.NaN);
            Assert.That(magnitude, Is.NaN);
        }

        [Test]
        public void Given_RetargetEndpointStageAttributionDiagnostics_When_InspectingRetargeter_Then_ExposesReadableProperties()
        {
            AssertReadableStringProperty("LastRetargetEndpointFirstJumpStage");
            AssertReadableStringProperty("LastRetargetEndpointFirstJumpEndpoint");
            AssertReadableFloatProperty("LastRetargetEndpointFirstJumpMagnitude");
            AssertReadableFloatProperty("LastRetargetEndpointFirstJumpDeltaX");
            AssertReadableFloatProperty("LastRetargetEndpointFirstJumpDeltaY");
            AssertReadableFloatProperty("LastRetargetEndpointFirstJumpDeltaZ");
        }

        [Test]
        public void Given_LegTwistOnlyFullBodyPoseMask_When_CheckingReferenceMuscles_Then_AllowsOnlyLegInOutAndTwist()
        {
            var root = new GameObject("leg twist full body pose mask fixture");
            try
            {
                var retargeter = root.AddComponent<PoseSpaceRetargeter>();
                retargeter.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = true;

                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Left Upper Leg In-Out")), Is.True);
                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Right Upper Leg Twist In-Out")), Is.True);
                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Left Lower Leg Twist In-Out")), Is.True);
                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Right Foot Twist In-Out")), Is.True);

                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Left Upper Leg Front-Back")), Is.False);
                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Right Lower Leg Stretch")), Is.False);
                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Left Foot Up-Down")), Is.False);
                Assert.That(ShouldApplyManualFullBodyPoseReferenceMuscle(retargeter, FindHumanMuscleIndex("Spine Twist Left-Right")), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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

        [Test]
        public void Given_TailSegmentWrapsToClipStart_When_CheckingLegacyEndWrap_Then_ClampsToClipEnd()
        {
            bool clamped = TryClampLegacyAnimationEndWrap(
                currentTime: 0f,
                previousTime: 207.76f,
                length: 207.7833f,
                maxStep: 1f / 30f,
                out float clampedTime);

            Assert.That(clamped, Is.True,
                "tail smoke must keep the final satisfaction_2 pose instead of accepting a Legacy Animation wrap back to clip start.");
            Assert.That(clampedTime, Is.EqualTo(207.7833f).Within(0.0001f));
        }

        [Test]
        public void Given_MidClipTimeJumpsBackward_When_CheckingLegacyEndWrap_Then_DoesNotClamp()
        {
            bool clamped = TryClampLegacyAnimationEndWrap(
                currentTime: 0f,
                previousTime: 120f,
                length: 207.7833f,
                maxStep: 1f / 30f,
                out float clampedTime);

            Assert.That(clamped, Is.False,
                "Only a wrap from the final sampling window should be treated as clip end; arbitrary seek/reset must keep the existing reset path.");
            Assert.That(clampedTime, Is.EqualTo(0f).Within(0.0001f));
        }

        private static void AssertReadableFloatProperty(string propertyName)
        {
            PropertyInfo property = typeof(PoseSpaceRetargeter).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(property, Is.Not.Null, $"PoseSpaceRetargeter should expose {propertyName} for left arm twist stage diagnostics.");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(float)));
            Assert.That(property.GetMethod, Is.Not.Null);
        }

        private static void AssertReadableStringProperty(string propertyName)
        {
            PropertyInfo property = typeof(PoseSpaceRetargeter).GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(property, Is.Not.Null, $"PoseSpaceRetargeter should expose {propertyName} for endpoint stage attribution diagnostics.");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(property.GetMethod, Is.Not.Null);
        }

        private static bool TryFindFirstRetargetEndpointStageJump(
            string[] stageNames,
            Vector3[] positions,
            float threshold,
            out string stage,
            out Vector3 delta,
            out float magnitude)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TryFindFirstRetargetEndpointStageJump",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RetargetEndpointStageJumpParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "PoseSpaceRetargeter should expose a pure static helper for first endpoint stage-jump attribution diagnostics.");

            object[] args =
            {
                stageNames,
                positions,
                threshold,
                "",
                Vector3.zero,
                0f
            };

            bool found = (bool)method.Invoke(null, args);
            stage = (string)args[3];
            delta = (Vector3)args[4];
            magnitude = (float)args[5];
            return found;
        }

        [Test]
        public void Given_LegacyAnimationClipStateMissing_When_CheckingClipPresence_Then_DoesNotRequestRemove()
        {
            var root = new GameObject("legacy-animation-presence-fixture");

            try
            {
                Animation legacyAnimation = root.AddComponent<Animation>();

                Assert.That(HasLegacyAnimationClipState(legacyAnimation, "__PoseSpaceRetargeter_GhostClip"), Is.False);

                var clip = new AnimationClip
                {
                    legacy = true
                };
                legacyAnimation.AddClip(clip, "__PoseSpaceRetargeter_GhostClip");

                Assert.That(HasLegacyAnimationClipState(legacyAnimation, "__PoseSpaceRetargeter_GhostClip"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
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
            MethodInfo method = LegacyAnimationDriverType.GetMethod(
                "TryCalculateManualLegacyAnimationTime",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ManualAdvanceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "LegacyAnimationDriver should expose a pure static helper for Legacy Animation manual advance timing.");

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

        private static bool TryClampLegacyAnimationEndWrap(
            float currentTime,
            float previousTime,
            float length,
            float maxStep,
            out float clampedTime)
        {
            MethodInfo method = LegacyAnimationDriverType.GetMethod(
                "TryClampLegacyAnimationEndWrap",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EndWrapClampParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "LegacyAnimationDriver should expose a pure static helper for tail-end Legacy Animation wrap clamping.");

            object[] args =
            {
                currentTime,
                previousTime,
                length,
                maxStep,
                currentTime
            };

            bool clamped = (bool)method.Invoke(null, args);
            clampedTime = (float)args[4];
            return clamped;
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
            MethodInfo method = RetargetingPoseSmoothingType.GetMethod(
                "ShouldSmoothVisualPoseSpike",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: VisualPoseSpikeParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "RetargetingPoseSmoothing should expose the visual pose spike smoothing decision.");

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
            MethodInfo method = RetargetingPoseSmoothingType.GetMethod(
                "CalculateVisualPoseSpikeCurrentWeight",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: VisualPoseSpikeWeightParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "RetargetingPoseSmoothing should expose the visual pose spike blend weight calculation.");

            return (float)method.Invoke(null, new object[]
            {
                configuredWeight,
                bodyPositionDelta,
                bodyRotationDelta,
                legacyAnimationStepSpikeThisFrame
            });
        }

        private static float BlendVisualPoseSpikeMuscle(
            float previousValue,
            float currentValue,
            float currentWeight,
            int muscleIndex,
            bool useEditorHumanoidMuscleReference,
            bool hasEditorHumanoidMuscleReferenceCurve,
            float forearmStretchClampMaxOffset = 0f)
        {
            MethodInfo method = RetargetingPoseSmoothingType.GetMethod(
                "BlendVisualPoseSpikeMuscle",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: VisualPoseSpikeMuscleBlendParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "RetargetingPoseSmoothing should expose the per-muscle visual spike blend calculation.");

            bool shouldPreserveCurrentValue = useEditorHumanoidMuscleReference &&
                hasEditorHumanoidMuscleReferenceCurve &&
                ShouldUseEditorHumanoidMuscleReference(muscleIndex);
            bool isForearmStretchMuscle = !shouldPreserveCurrentValue &&
                forearmStretchClampMaxOffset > 0f &&
                IsForearmStretchMuscle(muscleIndex);

            return (float)method.Invoke(null, new object[]
            {
                previousValue,
                currentValue,
                currentWeight,
                shouldPreserveCurrentValue,
                isForearmStretchMuscle,
                forearmStretchClampMaxOffset
            });
        }

        private static bool IsForearmStretchMuscle(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return false;
            }

            string muscleName = HumanTrait.MuscleName[muscleIndex];
            return muscleName.IndexOf("Forearm", StringComparison.OrdinalIgnoreCase) >= 0 &&
                muscleName.IndexOf("Stretch", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SetInstanceBool(object target, string fieldName, bool value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} must expose {fieldName} for the diagnostic runtime path.");

            field.SetValue(target, value);
        }

        private static bool ShouldApplyManualFullBodyPoseReferenceMuscle(PoseSpaceRetargeter retargeter, int muscleIndex)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ShouldApplyManualFullBodyPoseReferenceMuscle",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter must expose the full-body pose mask predicate for focused diagnostics.");

            return (bool)method.Invoke(retargeter, new object[] { muscleIndex });
        }

        private static int FindHumanMuscleIndex(string muscleName)
        {
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                if (string.Equals(HumanTrait.MuscleName[i], muscleName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool ShouldUseEditorHumanoidMuscleReference(int muscleIndex)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ShouldUseEditorHumanoidMuscleReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorHumanoidMuscleReferenceParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should centralize editor Humanoid muscle reference filtering.");

            return (bool)method.Invoke(null, new object[] { muscleIndex });
        }

        private static bool ShouldApplyEditorHumanoidMuscleReferenceValue(int muscleIndex, float referenceValue)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "ShouldApplyEditorHumanoidMuscleReferenceValue",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorHumanoidMuscleReferenceValueParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should filter editor Humanoid muscle references by evaluated value.");

            return (bool)method.Invoke(null, new object[] { muscleIndex, referenceValue });
        }

        private static float TransformRetargetPoseInputMuscleValue(int muscleIndex, float value)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "TransformRetargetPoseInputMuscleValue",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RetargetPoseInputMuscleTransformParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "TransformRetargetPoseInputMuscleValue must exist.");

            return (float)method.Invoke(null, new object[] { muscleIndex, value });
        }

        private static float AlignRetargetPoseInputWithEditorReference(int muscleIndex, float value, float referenceValue)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "AlignRetargetPoseInputWithEditorReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RetargetPoseInputReferenceAlignmentParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a bounded editor-reference sign alignment helper.");

            return (float)method.Invoke(null, new object[] { muscleIndex, value, referenceValue });
        }

        private static bool TryCalculateEditorFootHipsAlignedResidualYawReference(
            Vector3 desiredFootPosition,
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextParentWorldRotation)
        {
            MethodInfo method = RetargetingEndpointDiagnosticsType.GetMethod(
                "TryCalculateEditorFootHipsAlignedResidualYawReference",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FootHipsAlignedResidualYawReferenceParameterTypes,
                modifiers: null);
            if (method == null)
            {
                method = RetargetingEndpointDiagnosticsType.GetMethod(
                    "TryCalculateEditorFootHipsAlignedResidualYawReference",
                    BindingFlags.Static | BindingFlags.NonPublic);
            }

            Assert.That(method, Is.Not.Null, "RetargetingEndpointDiagnostics should own the pure lower-body foot X/Z residual yaw calculation.");

            object[] args =
            {
                desiredFootPosition,
                currentFootPosition,
                pivotPosition,
                currentParentWorldRotation,
                weight,
                maxAngleDegrees,
                currentParentWorldRotation
            };

            bool calculated = (bool)method.Invoke(null, args);
            nextParentWorldRotation = (Quaternion)args[6];
            return calculated;
        }

        private static float ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
            float thisFootResidual,
            float otherFootResidual,
            float requestedMaxAngle,
            bool isThisFootDominantResidual)
        {
            MethodInfo method = RetargetingEndpointDiagnosticsType.GetMethod(
                "ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: FootHipsAlignedResidualYawSideAwareMaxAngleParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "RetargetingEndpointDiagnostics should own the pure side-aware foot residual yaw correction budget.");

            return (float)method.Invoke(null, new object[]
            {
                thisFootResidual,
                otherFootResidual,
                requestedMaxAngle,
                isThisFootDominantResidual
            });
        }

        private static float CalculateEndpointPositionMaxYawAngle(
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            float maxOffset)
        {
            MethodInfo method = RetargetingEndpointDiagnosticsType.GetMethod(
                "CalculateEndpointPositionMaxYawAngle",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EndpointPositionMaxYawAngleParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "RetargetingEndpointDiagnostics should own the pure endpoint position max yaw calculation.");

            return (float)method.Invoke(null, new object[]
            {
                currentFootPosition,
                pivotPosition,
                maxOffset
            });
        }

        private static bool HasLegacyAnimationClipState(Animation legacyAnimation, string stateName)
        {
            MethodInfo method = LegacyAnimationDriverType.GetMethod(
                "HasLegacyAnimationClipState",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Animation), typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "LegacyAnimationDriver should check legacy clip presence before RemoveClip to avoid Unity console asserts.");

            return (bool)method.Invoke(null, new object[] { legacyAnimation, stateName });
        }
    }
}
