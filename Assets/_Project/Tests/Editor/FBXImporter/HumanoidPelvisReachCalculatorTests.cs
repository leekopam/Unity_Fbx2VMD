using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidPelvisReachCalculatorTests
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        [Test]
        public void Given_AdditionalExtension_When_Damping_Then_LimitsReachWithoutChangingLengths()
        {
            var limit = CalculateLimit(170f * Mathf.Deg2Rad, 1.02f, 1f);
            Assert.That(limit.success, Is.True);
            Assert.That(limit.reach, Is.GreaterThan(0.9961f).And.LessThan(0.998f));
            var pose = Calculate(Vector3.up * 1.02f, Vector3.up * 0.9f,
                1f, 0f, 0f, 0.1f, maximumReach: limit.reach);
            Assert.That(pose.success, Is.True);
            Assert.That(pose.offset, Is.EqualTo(limit.reach - 1.02f).Within(0.000001f));
        }

        [Test]
        public void Given_NoAdditionalExtension_When_Damping_Then_PreservesOriginalReach()
        {
            foreach (var result in new[]
            {
                CalculateLimit(Mathf.PI, 1.02f, 1f),
                CalculateLimit(170f * Mathf.Deg2Rad, 1.02f, 0f),
                CalculateLimit(170f * Mathf.Deg2Rad, 0.9f, 1f),
                CalculateLimit(100f * Mathf.Deg2Rad, 0.8f, 1f)
            })
            {
                Assert.That(result.success, Is.True);
                Assert.That(result.reach, Is.EqualTo(1f).Within(0.000001f));
            }
        }

        [Test]
        public void Given_InvalidDampingOrReachLimit_When_Calculating_Then_Rejects()
        {
            Assert.That(CalculateLimit(float.NaN, 1f, 1f).success, Is.False);
            Assert.That(CalculateLimit(2.8f, -1f, 1f).success, Is.False);
            Assert.That(CalculateLimit(2.8f, 1f, 1.1f).success, Is.False);
            Assert.That(Calculate(Vector3.up, Vector3.up, 1f, 1f, 0f, 0f,
                maximumReach: 1.1f).success, Is.False);
        }

        private static (bool success, float reach) CalculateLimit(float originalAngle, float distance, float weight)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidPelvisReachCalculator");
            MethodInfo method = type.GetMethod("TryCalculateExtensionReach", Flags);
            Assert.That(method, Is.Not.Null, "추가 신전 감쇠 계산 필요");
            object[] args = { 0.5f, 0.5f, originalAngle, distance, weight, 2.8f, 0f };
            bool success = (bool)method.Invoke(null, args);
            return (success, (float)args[6]);
        }

        [Test]
        public void Given_ReachableLegs_When_Calculating_Then_PreservesPelvis()
        {
            var result = Calculate(Vector3.up * 0.9f, Vector3.up, 1f, 0f, 0f, 0.2f);
            Assert.That(result.success, Is.True);
            Assert.That(result.offset, Is.Zero);
            Assert.That(result.shifts, Is.EqualTo(Vector2.zero));
            var roundedLimit = Calculate(Vector3.up * 0.8f, Vector3.up * 0.8f,
                1f, 1f, 0f, 0f, 0.443714648f, 0.4452781f, 0.8889928f);
            Assert.That(roundedLimit.success, Is.True, "float 합 반올림을 실제 다리 초과로 오인하지 않음");
            Assert.That(roundedLimit.offset, Is.Zero);
        }

        [Test]
        public void Given_SupportAndFreeFoot_When_Lowering_Then_StopsFreeFootAtFloor()
        {
            var airborne = Calculate(Vector3.up * 1.02f, Vector3.up * 0.9f,
                1f, 0f, 0f, 0.1f);
            Assert.That(airborne.success, Is.True);
            Assert.That(airborne.offset, Is.EqualTo(-0.02f).Within(0.000001f));
            Assert.That(airborne.shifts.x, Is.Zero);
            Assert.That(airborne.shifts.y, Is.EqualTo(airborne.offset));

            var floor = Calculate(Vector3.up * 1.02f, Vector3.up * 0.9f,
                1f, 0f, 0f, 0.005f);
            Assert.That(floor.success, Is.True);
            Assert.That(floor.shifts.y, Is.EqualTo(-0.005f).Within(0.000001f));

            var partial = Calculate(Vector3.up * 1.02f, Vector3.up * 1.03f,
                1f, 0.5f, 0f, 0.005f);
            Assert.That(partial.success, Is.True);
            Assert.That(partial.offset, Is.EqualTo(-0.03f).Within(0.000001f));
            Assert.That(partial.shifts.y, Is.EqualTo(0f).Within(0.000001f));
        }

        [Test]
        public void Given_PartialSupportBeyondReach_When_Calculating_Then_LimitsFollowBeforeExtraPelvisDrop()
        {
            var result = Calculate(Vector3.up * 1.03f, Vector3.up * 0.9f,
                0.25f, 1f, 0.1f, 0f);
            Assert.That(result.success, Is.True);
            Assert.That(result.offset, Is.EqualTo(-0.03f).Within(0.000001f));
            Assert.That(result.shifts.x, Is.EqualTo(0f).Within(0.000001f));
        }

        [Test]
        public void Given_BlendedTargetAtReachBoundary_When_SupportStarts_Then_PelvisRemainsContinuous()
        {
            foreach (float weight in new[] { 0f, 0.0001f, 0.001f, 0.1f, 0.5f, 0.9999f, 1f })
            {
                var result = Calculate(Vector3.up * (1f + 0.03f * weight), Vector3.up * 0.9f,
                    weight, 1f, 0.1f, 0f);
                Assert.That(result.success, Is.True, $"지지 강도 {weight}");
                Assert.That(result.offset, Is.EqualTo(-0.03f * weight).Within(0.000001f));
                Assert.That(result.shifts.x, Is.EqualTo(0f).Within(0.000001f));
            }
        }

        [Test]
        public void Given_OtherLegDrivesPelvis_When_FollowIsReachable_Then_PreservesPreferredFootMovement()
        {
            foreach (float direction in new[] { -1f, 1f })
            foreach (float weight in new[] { 0f, 0.0001f, 0.5f, 0.9999f, 1f })
            {
                var result = Calculate(Vector3.up * (1.04f * direction), Vector3.up * (0.9f * direction),
                    1f, weight, 0f, 0.1f);
                Assert.That(result.success, Is.True);
                Assert.That(result.offset, Is.EqualTo(-0.04f * direction).Within(0.000001f));
                Assert.That(result.shifts.y, Is.EqualTo((1f - weight) * result.offset).Within(0.000001f));
            }
        }

        [Test]
        public void Given_PartialFollowExceedsReach_When_OtherLegDrivesPelvis_Then_KeepsFeasibleIntermediateShift()
        {
            var result = Calculate(Vector3.up * 1.04f, Vector3.up * 1.02f,
                1f, 0.25f, 0f, 0.1f);
            Assert.That(result.success, Is.True);
            Assert.That(result.offset, Is.EqualTo(-0.04f).Within(0.000001f));
            Assert.That(result.shifts.y, Is.EqualTo(-0.02f).Within(0.000001f));
        }

        [Test]
        public void Given_InfeasibleOrInvalidLegs_When_Calculating_Then_RejectsWithoutPartialMove()
        {
            foreach (var result in new[]
            {
                Calculate(Vector3.up * 1.1f, Vector3.up, 1f, 1f, 0f, 0f),
                Calculate(Vector3.right * 1.1f, Vector3.up, 1f, 1f, 0f, 0f),
                Calculate(Vector3.up * 1.02f, Vector3.down * 1.02f, 1f, 1f, 0f, 0f),
                Calculate(Vector3.up, Vector3.up, float.NaN, 1f, 0f, 0f),
                Calculate(Vector3.up, Vector3.up, 1f, 1f, -0.01f, 0f),
                Calculate(Vector3.up * 0.1f, Vector3.up, 1f, 1f, 0f, 0f, 0.8f, 0.2f)
            })
            {
                Assert.That(result.success, Is.False);
                Assert.That(result.offset, Is.Zero);
                Assert.That(result.shifts, Is.EqualTo(Vector2.zero));
            }
        }

        private static (bool success, float offset, Vector2 shifts) Calculate(
            Vector3 left, Vector3 right, float leftWeight, float rightWeight,
            float leftClearance, float rightClearance, float upper = 0.5f, float lower = 0.5f,
            float? maximumReach = null)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidPelvisReachCalculator");
            Assert.That(type, Is.Not.Null, "골반 도달 계산기 필요");
            Type legType = type.GetNestedType("Leg", Flags);
            object Leg(Vector3 distance, float weight, float clearance)
            {
                object[] values = maximumReach.HasValue && weight > 0f
                    ? new object[] { distance, upper, lower, weight, clearance, maximumReach.Value }
                    : new object[] { distance, upper, lower, weight, clearance };
                return Activator.CreateInstance(legType, Flags, null, values, null);
            }
            object[] args = { Leg(left, leftWeight, leftClearance), Leg(right, rightWeight, rightClearance),
                Vector3.up, 0.05f, 0f, Vector2.zero };
            bool success = (bool)type.GetMethod("TryCalculateOffset", Flags).Invoke(null, args);
            return (success, (float)args[4], (Vector2)args[5]);
        }
    }
}
