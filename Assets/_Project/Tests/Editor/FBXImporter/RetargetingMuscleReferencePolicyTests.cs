using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RetargetingMuscleReferencePolicyTests
    {
        private static Type PolicyType =>
            typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingMuscleReferencePolicy",
                throwOnError: true);

        private static readonly Type[] PoseReferenceParameterTypes =
        {
            typeof(bool),
            typeof(bool),
            typeof(int)
        };

        private static readonly Type[] MuscleReferenceParameterTypes =
        {
            typeof(int)
        };

        private static readonly Type[] MuscleNameParameterTypes =
        {
            typeof(string)
        };

        private static readonly Type[] MuscleReferenceValueParameterTypes =
        {
            typeof(int),
            typeof(float)
        };

        private static readonly Type[] VisualSmoothingMusclePreservationParameterTypes =
        {
            typeof(int),
            typeof(bool),
            typeof(bool)
        };

        private static readonly Type[] PoseInputTransformParameterTypes =
        {
            typeof(int),
            typeof(float)
        };

        private static readonly Type[] PoseInputAlignmentParameterTypes =
        {
            typeof(int),
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] ManualFullBodyMuscleParameterTypes =
        {
            typeof(int),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool),
            typeof(bool)
        };

        [Test]
        public void Given_MuscleReferencePolicyOwner_When_InspectingResponsibilities_Then_OwnsPurePolicies()
        {
            AssertPolicyMethod("ShouldUsePoseReference", PoseReferenceParameterTypes);
            AssertPolicyMethod("ShouldUseHumanoidMuscleReference", MuscleReferenceParameterTypes);
            AssertPolicyMethod("ShouldApplyHumanoidMuscleReferenceValue", MuscleReferenceValueParameterTypes);
            AssertPolicyMethod("ShouldPreserveHumanoidMuscleDuringVisualSmoothing", VisualSmoothingMusclePreservationParameterTypes);
            AssertPolicyMethod("IsForearmStretchMuscle", MuscleReferenceParameterTypes);
            AssertPolicyMethod("FindHumanMuscleIndex", MuscleNameParameterTypes);
            AssertPolicyMethod("TransformPoseInputValue", PoseInputTransformParameterTypes);
            AssertPolicyMethod("AlignPoseInputWithReference", PoseInputAlignmentParameterTypes);
            AssertPolicyMethod("ShouldApplyManualFullBodyMuscle", ManualFullBodyMuscleParameterTypes);

            AssertPoseSpaceMethodAbsent("ShouldUseEditorPoseReference", PoseReferenceParameterTypes);
            AssertPoseSpaceMethodAbsent("ShouldUseEditorHumanoidMuscleReference", MuscleReferenceParameterTypes);
            AssertPoseSpaceMethodAbsent("ShouldApplyEditorHumanoidMuscleReferenceValue", MuscleReferenceValueParameterTypes);
            AssertPoseSpaceMethodAbsent("ShouldPreserveEditorHumanoidMuscleDuringVisualSmoothing", VisualSmoothingMusclePreservationParameterTypes);
            AssertPoseSpaceMethodAbsent("IsForearmStretchMuscleIndex", MuscleReferenceParameterTypes);
            AssertPoseSpaceMethodAbsent("FindHumanMuscleIndex", MuscleNameParameterTypes);
            AssertPoseSpaceMethodAbsent("TransformRetargetPoseInputMuscleValue", PoseInputTransformParameterTypes);
            AssertPoseSpaceMethodAbsent("AlignRetargetPoseInputWithEditorReference", PoseInputAlignmentParameterTypes);
            AssertPoseSpaceMethodAbsent("ShouldApplyManualFullBodyPoseReferenceMuscle", MuscleReferenceParameterTypes);
        }

        [Test]
        public void Given_FullBodyReferenceEnabledWithoutFingerMuscles_When_DeterminingPoseReferenceUse_Then_UsesReference()
        {
            bool shouldUseReference = ShouldUsePoseReference(
                enableFingerPoseReference: false,
                enableFullBodyPoseReference: true,
                fingerReferenceMuscleCount: 0);

            Assert.That(shouldUseReference, Is.True);
        }

        [Test]
        public void Given_FingerReferenceEnabledWithFingerMuscles_When_DeterminingPoseReferenceUse_Then_UsesReference()
        {
            bool shouldUseReference = ShouldUsePoseReference(
                enableFingerPoseReference: true,
                enableFullBodyPoseReference: false,
                fingerReferenceMuscleCount: 1);

            Assert.That(shouldUseReference, Is.True);
        }

        [Test]
        public void Given_NoReferenceSource_When_DeterminingPoseReferenceUse_Then_DoesNotUseReference()
        {
            bool shouldUseReference = ShouldUsePoseReference(
                enableFingerPoseReference: true,
                enableFullBodyPoseReference: false,
                fingerReferenceMuscleCount: 0);

            Assert.That(shouldUseReference, Is.False);
        }

        [Test]
        public void Given_ForearmStretchMuscle_When_CheckingHumanoidReferenceUse_Then_DoesNotUseReference()
        {
            int muscleIndex = FindHumanMuscleIndex("Right Forearm Stretch");

            Assert.That(ShouldUseHumanoidMuscleReference(muscleIndex), Is.False);
            Assert.That(IsForearmStretchMuscle(muscleIndex), Is.True);
        }

        [Test]
        public void Given_EditorReferenceCurve_When_CheckingVisualSmoothingPreservation_Then_PreservesOnlyEligibleMuscle()
        {
            int shoulderIndex = FindHumanMuscleIndex("Right Shoulder Front-Back");
            int forearmStretchIndex = FindHumanMuscleIndex("Right Forearm Stretch");

            Assert.That(ShouldPreserveHumanoidMuscleDuringVisualSmoothing(
                shoulderIndex,
                useHumanoidMuscleReference: true,
                hasHumanoidMuscleReferenceCurve: true), Is.True);
            Assert.That(ShouldPreserveHumanoidMuscleDuringVisualSmoothing(
                shoulderIndex,
                useHumanoidMuscleReference: false,
                hasHumanoidMuscleReferenceCurve: true), Is.False);
            Assert.That(ShouldPreserveHumanoidMuscleDuringVisualSmoothing(
                shoulderIndex,
                useHumanoidMuscleReference: true,
                hasHumanoidMuscleReferenceCurve: false), Is.False);
            Assert.That(ShouldPreserveHumanoidMuscleDuringVisualSmoothing(
                forearmStretchIndex,
                useHumanoidMuscleReference: true,
                hasHumanoidMuscleReferenceCurve: true), Is.False);
        }

        [Test]
        public void Given_HumanMuscleNameVariant_When_FindingIndex_Then_UsesNormalizedExactMatch()
        {
            int expectedIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");

            Assert.That(FindHumanMuscleIndexByPolicy("Right Arm Twist In-Out"), Is.EqualTo(expectedIndex));
            Assert.That(FindHumanMuscleIndexByPolicy("right_arm.twist-in_out"), Is.EqualTo(expectedIndex));
        }

        [Test]
        public void Given_MissingHumanMuscleName_When_FindingIndex_Then_ReturnsMinusOne()
        {
            Assert.That(FindHumanMuscleIndexByPolicy(null), Is.EqualTo(-1));
            Assert.That(FindHumanMuscleIndexByPolicy(string.Empty), Is.EqualTo(-1));
            Assert.That(FindHumanMuscleIndexByPolicy("Unknown Humanoid Muscle"), Is.EqualTo(-1));
        }

        [Test]
        public void Given_LeftUpperArmTwistMuscle_When_CheckingHumanoidReferenceUse_Then_DoesNotUseReference()
        {
            int muscleIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");

            Assert.That(ShouldUseHumanoidMuscleReference(muscleIndex), Is.False);
        }

        [Test]
        public void Given_RightUpperArmTwistMuscle_When_CheckingHumanoidReferenceUse_Then_UsesReference()
        {
            int muscleIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");

            Assert.That(ShouldUseHumanoidMuscleReference(muscleIndex), Is.True);
        }

        [Test]
        public void Given_UpperArmTwistPoseInput_When_TransformingInput_Then_FlipsLeftTwistSignOnly()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            int leftShoulderFrontBackIndex = FindHumanMuscleIndex("Left Shoulder Front-Back");

            Assert.That(TransformPoseInputValue(leftArmTwistIndex, 0.797207f), Is.EqualTo(-0.797207f).Within(0.000001f));
            Assert.That(TransformPoseInputValue(rightArmTwistIndex, -0.250876f), Is.EqualTo(-0.250876f).Within(0.000001f));
            Assert.That(TransformPoseInputValue(leftShoulderFrontBackIndex, 1f), Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void Given_LeftArmTwistInputOpposesBoundedReference_When_AligningInput_Then_FlipsSignOnly()
        {
            int muscleIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");

            float aligned = AlignPoseInputWithReference(muscleIndex, -0.760319f, 0.758726f);

            Assert.That(aligned, Is.EqualTo(0.760319f).Within(0.000001f));
        }

        [Test]
        public void Given_LeftArmTwistInputOpposesOverrangeReference_When_AligningInput_Then_KeepsLiveInput()
        {
            int muscleIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");

            float aligned = AlignPoseInputWithReference(muscleIndex, -0.10761f, 2.917387f);

            Assert.That(aligned, Is.EqualTo(-0.10761f).Within(0.000001f));
        }

        [Test]
        public void Given_RightArmTwistInputSharesModerateOverrangeReferenceSign_When_AligningInput_Then_FlipsSignOnly()
        {
            int muscleIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");

            float aligned = AlignPoseInputWithReference(muscleIndex, 0.852882f, 2.083053f);

            Assert.That(aligned, Is.EqualTo(-0.852882f).Within(0.000001f));
        }

        [Test]
        public void Given_RightArmTwistInputSharesLowerOverrangeReferenceSign_When_AligningInput_Then_KeepsLiveInput()
        {
            int muscleIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");

            float aligned = AlignPoseInputWithReference(muscleIndex, 0.574437f, 1.862711f);

            Assert.That(aligned, Is.EqualTo(0.574437f).Within(0.000001f));
        }

        [Test]
        public void Given_RightUpperArmTwistReferenceIsModeratelyOverrange_When_CheckingValueUse_Then_DoesNotUseReference()
        {
            int muscleIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");

            Assert.That(ShouldApplyHumanoidMuscleReferenceValue(muscleIndex, 2.083053f), Is.False);
        }

        [Test]
        public void Given_RightUpperArmTwistReferenceIsBounded_When_CheckingValueUse_Then_UsesReference()
        {
            int muscleIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");

            Assert.That(ShouldApplyHumanoidMuscleReferenceValue(muscleIndex, -0.568725f), Is.True);
        }

        [Test]
        public void Given_NonFiniteReferenceValue_When_CheckingValueUse_Then_DoesNotUseReference()
        {
            int muscleIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");

            Assert.That(ShouldApplyHumanoidMuscleReferenceValue(muscleIndex, float.NaN), Is.False);
        }

        [Test]
        public void Given_RightArmOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyRightArmChain()
        {
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Arm"), rightArmOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Forearm"), rightArmOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Left", "Arm"), rightArmOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Upper Leg"), rightArmOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Index"), rightArmOnly: true), Is.False);
        }

        [Test]
        public void Given_LeftArmOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyLeftArmChain()
        {
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Left", "Arm"), leftArmOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Left", "Forearm"), leftArmOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Arm"), leftArmOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Left", "Upper Leg"), leftArmOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Left", "Index"), leftArmOnly: true), Is.False);
        }

        [Test]
        public void Given_RightSleeveChainOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsSpineAndRightSleeveChain()
        {
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Spine"), rightSleeveChainOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Arm"), rightSleeveChainOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Forearm"), rightSleeveChainOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Left", "Arm"), rightSleeveChainOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Upper Leg"), rightSleeveChainOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndexContaining("Right", "Index"), rightSleeveChainOnly: true), Is.False);
        }

        [Test]
        public void Given_LegTwistOnlyMask_When_CheckingManualFullBodyMuscles_Then_AllowsOnlyLegInOutAndTwist()
        {
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Left Upper Leg In-Out"), legTwistOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Right Upper Leg Twist In-Out"), legTwistOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Left Lower Leg Twist In-Out"), legTwistOnly: true), Is.True);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Right Foot Twist In-Out"), legTwistOnly: true), Is.True);

            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Left Upper Leg Front-Back"), legTwistOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Right Lower Leg Stretch"), legTwistOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Left Foot Up-Down"), legTwistOnly: true), Is.False);
            Assert.That(ShouldApplyManualFullBodyMuscle(FindHumanMuscleIndex("Spine Twist Left-Right"), legTwistOnly: true), Is.False);
        }

        [Test]
        public void Given_InvalidMuscleIndex_When_ApplyingPolicies_Then_PreservesExistingFallbacks()
        {
            Assert.That(ShouldUseHumanoidMuscleReference(-1), Is.False);
            Assert.That(ShouldApplyHumanoidMuscleReferenceValue(-1, 0.5f), Is.False);
            Assert.That(ShouldPreserveHumanoidMuscleDuringVisualSmoothing(-1, true, true), Is.False);
            Assert.That(IsForearmStretchMuscle(-1), Is.False);
            Assert.That(TransformPoseInputValue(-1, 0.25f), Is.EqualTo(0.25f));
            Assert.That(AlignPoseInputWithReference(-1, 0.25f, -0.25f), Is.EqualTo(0.25f));
            Assert.That(ShouldApplyManualFullBodyMuscle(-1), Is.True);
        }

        private static bool ShouldUsePoseReference(
            bool enableFingerPoseReference,
            bool enableFullBodyPoseReference,
            int fingerReferenceMuscleCount)
        {
            return InvokePolicy<bool>(
                "ShouldUsePoseReference",
                PoseReferenceParameterTypes,
                enableFingerPoseReference,
                enableFullBodyPoseReference,
                fingerReferenceMuscleCount);
        }

        private static bool ShouldUseHumanoidMuscleReference(int muscleIndex)
        {
            return InvokePolicy<bool>(
                "ShouldUseHumanoidMuscleReference",
                MuscleReferenceParameterTypes,
                muscleIndex);
        }

        private static bool ShouldApplyHumanoidMuscleReferenceValue(int muscleIndex, float referenceValue)
        {
            return InvokePolicy<bool>(
                "ShouldApplyHumanoidMuscleReferenceValue",
                MuscleReferenceValueParameterTypes,
                muscleIndex,
                referenceValue);
        }

        private static bool ShouldPreserveHumanoidMuscleDuringVisualSmoothing(
            int muscleIndex,
            bool useHumanoidMuscleReference,
            bool hasHumanoidMuscleReferenceCurve)
        {
            return InvokePolicy<bool>(
                "ShouldPreserveHumanoidMuscleDuringVisualSmoothing",
                VisualSmoothingMusclePreservationParameterTypes,
                muscleIndex,
                useHumanoidMuscleReference,
                hasHumanoidMuscleReferenceCurve);
        }

        private static bool IsForearmStretchMuscle(int muscleIndex)
        {
            return InvokePolicy<bool>(
                "IsForearmStretchMuscle",
                MuscleReferenceParameterTypes,
                muscleIndex);
        }

        private static int FindHumanMuscleIndexByPolicy(string muscleName)
        {
            return InvokePolicy<int>(
                "FindHumanMuscleIndex",
                MuscleNameParameterTypes,
                muscleName);
        }

        private static float TransformPoseInputValue(int muscleIndex, float value)
        {
            return InvokePolicy<float>(
                "TransformPoseInputValue",
                PoseInputTransformParameterTypes,
                muscleIndex,
                value);
        }

        private static float AlignPoseInputWithReference(int muscleIndex, float value, float referenceValue)
        {
            return InvokePolicy<float>(
                "AlignPoseInputWithReference",
                PoseInputAlignmentParameterTypes,
                muscleIndex,
                value,
                referenceValue);
        }

        private static bool ShouldApplyManualFullBodyMuscle(
            int muscleIndex,
            bool rightSleeveChainOnly = false,
            bool rightArmOnly = false,
            bool leftArmOnly = false,
            bool legTwistOnly = false,
            bool lowerBodyOnly = false,
            bool excludeLowerBody = false)
        {
            return InvokePolicy<bool>(
                "ShouldApplyManualFullBodyMuscle",
                ManualFullBodyMuscleParameterTypes,
                muscleIndex,
                rightSleeveChainOnly,
                rightArmOnly,
                leftArmOnly,
                legTwistOnly,
                lowerBodyOnly,
                excludeLowerBody);
        }

        private static T InvokePolicy<T>(string methodName, Type[] parameterTypes, params object[] arguments)
        {
            MethodInfo method = PolicyType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, $"RetargetingMuscleReferencePolicy must own {methodName}.");
            return (T)method.Invoke(null, arguments);
        }

        private static void AssertPolicyMethod(string methodName, Type[] parameterTypes)
        {
            Assert.That(PolicyType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null), Is.Not.Null);
        }

        private static void AssertPoseSpaceMethodAbsent(string methodName, Type[] parameterTypes)
        {
            Assert.That(typeof(PoseSpaceRetargeter).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null), Is.Null);
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

            Assert.Fail($"Unity HumanTrait must expose the requested muscle: {muscleName}");
            return -1;
        }

        private static int FindHumanMuscleIndexContaining(params string[] tokens)
        {
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string muscleName = HumanTrait.MuscleName[i] ?? string.Empty;
                bool containsAll = true;
                foreach (string token in tokens)
                {
                    if (muscleName.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        containsAll = false;
                        break;
                    }
                }

                if (containsAll)
                {
                    return i;
                }
            }

            Assert.Fail($"Unity HumanTrait must expose a muscle containing: {string.Join(", ", tokens)}");
            return -1;
        }
    }
}
