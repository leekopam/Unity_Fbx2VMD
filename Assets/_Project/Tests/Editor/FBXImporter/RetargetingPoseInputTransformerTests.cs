using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RetargetingPoseInputTransformerTests
    {
        private static readonly Type[] MuscleValuesParameterTypes =
        {
            typeof(float[])
        };

        private static readonly Type[] HumanPoseReferenceParameterTypes =
        {
            typeof(HumanPose).MakeByRefType()
        };

        private static readonly Type[] ReferenceCurveAlignmentParameterTypes =
        {
            typeof(float[]),
            typeof(Dictionary<int, AnimationCurve>),
            typeof(float)
        };

        private static readonly Type[] ReferenceCurveApplicationParameterTypes =
        {
            typeof(float[]),
            typeof(Dictionary<int, AnimationCurve>),
            typeof(float),
            typeof(bool)
        };

        [Test]
        public void Given_PoseInputTransformer_When_CheckingOwnership_Then_OwnsArrayTransformation()
        {
            Type transformerType = ResolveTransformerType();
            Assert.That(FindTransformMethod(transformerType), Is.Not.Null);
            Assert.That(FindReferenceCurveAlignmentMethod(transformerType), Is.Not.Null);
            Assert.That(FindReferenceCurveApplicationMethod(transformerType), Is.Not.Null);
            Assert.That(typeof(PoseSpaceRetargeter).GetMethod(
                "TransformRetargetPoseInputMuscles",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HumanPoseReferenceParameterTypes,
                modifiers: null), Is.Null);
            Assert.That(typeof(PoseSpaceRetargeter).GetMethod(
                "AlignRetargetPoseInputWithEditorHumanoidMuscleReference",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HumanPoseReferenceParameterTypes,
                modifiers: null), Is.Null);
        }

        [Test]
        public void Given_PoseInputMuscleValues_When_TransformingInPlace_Then_FlipsLeftTwistSignOnly()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            int leftShoulderFrontBackIndex = FindHumanMuscleIndex("Left Shoulder Front-Back");
            var muscleValues = new float[HumanTrait.MuscleCount];
            muscleValues[leftArmTwistIndex] = 0.797207f;
            muscleValues[rightArmTwistIndex] = -0.250876f;
            muscleValues[leftShoulderFrontBackIndex] = 1f;

            TransformInPlace(muscleValues);

            Assert.That(muscleValues[leftArmTwistIndex], Is.EqualTo(-0.797207f).Within(0.000001f));
            Assert.That(muscleValues[rightArmTwistIndex], Is.EqualTo(-0.250876f).Within(0.000001f));
            Assert.That(muscleValues[leftShoulderFrontBackIndex], Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void Given_MissingPoseInputMuscleValues_When_TransformingInPlace_Then_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => TransformInPlace(null));
        }

        [Test]
        public void Given_ReferenceCurves_When_AligningInPlace_Then_EvaluatesValidCurvesAndSkipsInvalidEntries()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            int leftShoulderFrontBackIndex = FindHumanMuscleIndex("Left Shoulder Front-Back");
            var muscleValues = new float[HumanTrait.MuscleCount];
            muscleValues[leftArmTwistIndex] = -0.760319f;
            muscleValues[leftShoulderFrontBackIndex] = 0.25f;
            var referenceCurves = new Dictionary<int, AnimationCurve>
            {
                [leftArmTwistIndex] = AnimationCurve.Linear(0f, -0.758726f, 1f, 0.758726f),
                [leftShoulderFrontBackIndex] = null,
                [-1] = AnimationCurve.Constant(0f, 1f, 1f),
                [HumanTrait.MuscleCount] = AnimationCurve.Constant(0f, 1f, 1f)
            };

            AlignWithReferenceCurvesInPlace(muscleValues, referenceCurves, 1f);

            Assert.That(muscleValues[leftArmTwistIndex], Is.EqualTo(0.760319f).Within(0.000001f));
            Assert.That(muscleValues[leftShoulderFrontBackIndex], Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void Given_MissingReferenceCurveInputs_When_AligningInPlace_Then_DoesNotThrowOrChangeValues()
        {
            var muscleValues = new[] { 0.25f };

            Assert.DoesNotThrow(() => AlignWithReferenceCurvesInPlace(
                null,
                new Dictionary<int, AnimationCurve>(),
                0f));
            Assert.DoesNotThrow(() => AlignWithReferenceCurvesInPlace(muscleValues, null, 0f));
            Assert.DoesNotThrow(() => AlignWithReferenceCurvesInPlace(
                muscleValues,
                new Dictionary<int, AnimationCurve>(),
                0f));
            Assert.That(muscleValues[0], Is.EqualTo(0.25f).Within(0.000001f));
        }

        [Test]
        public void Given_CompleteNativeReference_When_ApplyingAfterInputTransform_Then_OverridesTwistAndBodyMuscles()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            int spineIndex = FindHumanMuscleIndex("Spine Front-Back");
            var muscleValues = new float[HumanTrait.MuscleCount];
            muscleValues[leftArmTwistIndex] = 0.8f;
            muscleValues[rightArmTwistIndex] = -0.4f;
            muscleValues[spineIndex] = 0.1f;

            TransformInPlace(muscleValues);
            ApplyReferenceCurvesInPlace(
                muscleValues,
                new Dictionary<int, AnimationCurve>
                {
                    [leftArmTwistIndex] = AnimationCurve.Constant(0f, 1f, 1.661389f),
                    [rightArmTwistIndex] = AnimationCurve.Constant(0f, 1f, 1.902608f),
                    [spineIndex] = AnimationCurve.Constant(0f, 1f, -0.35f)
                },
                0.5f,
                useCompleteReference: true);

            Assert.That(muscleValues[leftArmTwistIndex], Is.EqualTo(1.661389f).Within(0.000001f));
            Assert.That(muscleValues[rightArmTwistIndex], Is.EqualTo(1.902608f).Within(0.000001f));
            Assert.That(muscleValues[spineIndex], Is.EqualTo(-0.35f).Within(0.000001f));
        }

        [Test]
        public void Given_PartialReference_When_ApplyingCurves_Then_PreservesLegacyTwistExclusions()
        {
            int leftArmTwistIndex = FindHumanMuscleIndex("Left Arm Twist In-Out");
            int rightArmTwistIndex = FindHumanMuscleIndex("Right Arm Twist In-Out");
            var muscleValues = new float[HumanTrait.MuscleCount];
            muscleValues[leftArmTwistIndex] = -0.8f;
            muscleValues[rightArmTwistIndex] = -0.4f;

            ApplyReferenceCurvesInPlace(
                muscleValues,
                new Dictionary<int, AnimationCurve>
                {
                    [leftArmTwistIndex] = AnimationCurve.Constant(0f, 1f, 1.661389f),
                    [rightArmTwistIndex] = AnimationCurve.Constant(0f, 1f, 1.902608f)
                },
                0.5f,
                useCompleteReference: false);

            Assert.That(muscleValues[leftArmTwistIndex], Is.EqualTo(-0.8f).Within(0.000001f));
            Assert.That(muscleValues[rightArmTwistIndex], Is.EqualTo(-0.4f).Within(0.000001f));
        }

        private static void TransformInPlace(float[] muscleValues)
        {
            Type transformerType = ResolveTransformerType();
            MethodInfo method = FindTransformMethod(transformerType);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { muscleValues });
        }

        private static Type ResolveTransformerType()
        {
            Type transformerType = typeof(PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.RetargetingPoseInputTransformer");
            Assert.That(transformerType, Is.Not.Null,
                "RetargetingPoseInputTransformer must own pose input array transformation.");
            return transformerType;
        }

        private static MethodInfo FindTransformMethod(Type transformerType)
        {
            return transformerType.GetMethod(
                "TransformInPlace",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: MuscleValuesParameterTypes,
                modifiers: null);
        }

        private static void AlignWithReferenceCurvesInPlace(
            float[] muscleValues,
            Dictionary<int, AnimationCurve> referenceCurves,
            float time)
        {
            Type transformerType = ResolveTransformerType();
            MethodInfo method = FindReferenceCurveAlignmentMethod(transformerType);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { muscleValues, referenceCurves, time });
        }

        private static MethodInfo FindReferenceCurveAlignmentMethod(Type transformerType)
        {
            return transformerType.GetMethod(
                "AlignWithReferenceCurvesInPlace",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ReferenceCurveAlignmentParameterTypes,
                modifiers: null);
        }

        private static void ApplyReferenceCurvesInPlace(
            float[] muscleValues,
            Dictionary<int, AnimationCurve> referenceCurves,
            float time,
            bool useCompleteReference)
        {
            Type transformerType = ResolveTransformerType();
            MethodInfo method = FindReferenceCurveApplicationMethod(transformerType);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[]
            {
                muscleValues,
                referenceCurves,
                time,
                useCompleteReference
            });
        }

        private static MethodInfo FindReferenceCurveApplicationMethod(Type transformerType)
        {
            return transformerType.GetMethod(
                "ApplyReferenceCurvesInPlace",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ReferenceCurveApplicationParameterTypes,
                modifiers: null);
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
    }
}
