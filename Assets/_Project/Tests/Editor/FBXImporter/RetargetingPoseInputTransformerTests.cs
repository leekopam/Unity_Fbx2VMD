using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
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

        [Test]
        public void Given_PoseInputTransformer_When_CheckingOwnership_Then_OwnsArrayTransformation()
        {
            Type transformerType = ResolveTransformerType();
            Assert.That(FindTransformMethod(transformerType), Is.Not.Null);
            Assert.That(typeof(PoseSpaceRetargeter).GetMethod(
                "TransformRetargetPoseInputMuscles",
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
