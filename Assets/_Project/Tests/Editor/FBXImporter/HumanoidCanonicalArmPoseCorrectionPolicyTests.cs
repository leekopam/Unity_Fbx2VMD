using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidCanonicalArmPoseCorrectionPolicyTests
    {
        private const float ValueTolerance = 0.0001f;

        [TestCase(0.004f, 0f)]
        [TestCase(0.0125f, 0.5f)]
        [TestCase(0.02f, 1f)]
        [TestCase(0.04f, 1f)]
        public void Given_ArmError_When_CalculatingBlendWeight_Then_UsesMeasuredThresholds(
            float meanError,
            float expectedWeight)
        {
            Type policyType = RequirePolicyType();

            float actualWeight = (float)InvokeStatic(
                policyType,
                "CalculateBlendWeight",
                meanError);

            Assert.That(actualWeight,
                Is.EqualTo(expectedWeight).Within(ValueTolerance));
        }

        [Test]
        public void Given_CanonicalArmPose_When_Blending_Then_ChangesOnlyArmMuscles()
        {
            Type policyType = RequirePolicyType();
            float[] sourceMuscles = new float[HumanTrait.MuscleCount];
            float[] targetMuscles = new float[HumanTrait.MuscleCount];
            int[] armIndices = FindArmMuscleIndices();
            foreach (int index in armIndices)
            {
                sourceMuscles[index] = 2f;
            }

            object[] arguments =
            {
                sourceMuscles,
                targetMuscles,
                null,
                0f,
                0f
            };

            Assert.That(
                (bool)InvokeStatic(policyType, "TryBlend", arguments),
                Is.True);

            var blendedMuscles = (float[])arguments[2];
            Assert.That((float)arguments[3], Is.EqualTo(1f).Within(ValueTolerance));
            Assert.That((float)arguments[4], Is.EqualTo(1f).Within(ValueTolerance));
            Assert.That(
                armIndices.All(index =>
                    Mathf.Abs(blendedMuscles[index] - 1f) <= ValueTolerance),
                Is.True,
                "팔 muscle은 Humanoid 유효 범위로 제한한 기준 자세를 사용해야 합니다.");
            Assert.That(
                Enumerable.Range(0, HumanTrait.MuscleCount)
                    .Where(index => !armIndices.Contains(index))
                    .All(index =>
                        Mathf.Abs(blendedMuscles[index] - targetMuscles[index]) <=
                        ValueTolerance),
                Is.True,
                "팔 이외 muscle은 자동 보정이 변경하면 안 됩니다.");
        }

        [Test]
        public void Given_InvalidMuscleArrays_When_Blending_Then_RejectsWithoutOutput()
        {
            Type policyType = RequirePolicyType();
            object[] arguments =
            {
                new float[HumanTrait.MuscleCount - 1],
                new float[HumanTrait.MuscleCount],
                null,
                0f,
                0f
            };

            Assert.That(
                (bool)InvokeStatic(policyType, "TryBlend", arguments),
                Is.False);
            Assert.That(arguments[2], Is.Null);
            Assert.That((float)arguments[3], Is.Zero);
            Assert.That((float)arguments[4], Is.Zero);
        }

        private static int[] FindArmMuscleIndices()
        {
            return Enumerable.Range(0, HumanTrait.MuscleCount)
                .Where(index =>
                    HumanTrait.MuscleName[index].Contains("Arm") ||
                    HumanTrait.MuscleName[index].Contains("Forearm"))
                .ToArray();
        }

        private static Type RequirePolicyType()
        {
            Type policyType = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidCanonicalArmPoseCorrectionPolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null,
                "Humanoid 표준 팔 자세 오차를 제한하는 순수 계산 정책이 필요합니다.");
            return policyType;
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(null, arguments);
        }
    }
}
