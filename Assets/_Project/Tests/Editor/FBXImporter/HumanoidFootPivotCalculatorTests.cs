using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootPivotCalculatorTests
    {
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        public void Given_CalibratedPivot_When_Rolling_Then_KeepsWorldContact(float scale)
        {
            Vector3 position = new Vector3(2f, 0.1f, -3f);
            Vector3 localPivot = new Vector3(0.02f, -0.08f, 0.12f);
            Quaternion original = Quaternion.Euler(5f, 63f, 2f);
            Vector3 anchor = Matrix4x4.TRS(position, original, Vector3.one * scale)
                .MultiplyPoint3x4(localPivot);
            Quaternion desired = original * Quaternion.Euler(-25f, 0f, 0f);

            var result = Calculate(position, original, localPivot, scale, anchor, desired, 1f);

            Assert.That(result.success, Is.True);
            Vector3 actual = Matrix4x4.TRS(result.position, result.rotation, Vector3.one * scale)
                .MultiplyPoint3x4(localPivot);
            Assert.That(Vector3.Distance(actual, anchor), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(result.rotation, desired), Is.LessThan(0.01f));
        }

        [Test]
        public void Given_ZeroWeight_When_Releasing_Then_ReturnsExactAnimationPose()
        {
            Vector3 position = new Vector3(0.1f, 0.3f, 0.5f);
            Quaternion rotation = Quaternion.Euler(17f, 81f, -3f);
            var result = Calculate(position, rotation, Vector3.forward, 1f,
                Vector3.zero, Quaternion.identity, 0f);

            Assert.That(result.success, Is.True);
            Assert.That(result.position, Is.EqualTo(position));
            Assert.That(result.rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void Given_HalfContactWeight_When_Rolling_Then_BlendsAroundCurrentPivotOffset()
        {
            Vector3 position = Vector3.up;
            Vector3 localPivot = Vector3.forward;
            var result = Calculate(position, Quaternion.identity, localPivot, 1f,
                Vector3.zero, Quaternion.Euler(0f, 90f, 0f), 0.5f);

            Assert.That(result.success, Is.True);
            Vector3 expected = new Vector3(-Mathf.Sqrt(0.5f) * 0.5f, 0.5f,
                -Mathf.Sqrt(0.5f) * 0.5f);
            Assert.That(Vector3.Distance(result.position, expected), Is.LessThan(0.00001f));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void Given_InvalidScale_When_Calculating_Then_Rejects(float scale)
        {
            Assert.That(Calculate(Vector3.zero, Quaternion.identity, Vector3.forward,
                scale, Vector3.zero, Quaternion.identity, 1f).success, Is.False);
        }

        [TestCase(-0.1f)]
        [TestCase(1.1f)]
        [TestCase(float.NaN)]
        public void Given_InvalidWeight_When_Calculating_Then_Rejects(float weight)
        {
            Assert.That(Calculate(Vector3.zero, Quaternion.identity, Vector3.forward,
                1f, Vector3.zero, Quaternion.identity, weight).success, Is.False);
        }

        [Test]
        public void Given_DegenerateRotationOrNonfinitePoint_When_Calculating_Then_Rejects()
        {
            Assert.That(Calculate(Vector3.zero, Quaternion.identity, Vector3.forward,
                1f, Vector3.zero, default, 1f).success, Is.False);
            Assert.That(Calculate(Vector3.zero, Quaternion.identity, Vector3.forward,
                1f, new Vector3(float.NaN, 0f, 0f), Quaternion.identity, 1f).success, Is.False);
        }

        [Test]
        public void Given_OverflowingOffset_When_Calculating_Then_RejectsWithoutInvalidOutput()
        {
            var result = Calculate(Vector3.up, Quaternion.identity,
                new Vector3(float.MaxValue, 0f, 0f), 2f,
                Vector3.zero, Quaternion.identity, 1f);
            Assert.That(result.success, Is.False);
            Assert.That(result.position, Is.EqualTo(Vector3.up));
            Assert.That(result.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Given_ReorderedEvaluations_When_Repeating_Then_ReturnsSamePose()
        {
            var first = Calculate(Vector3.up, Quaternion.identity, Vector3.forward,
                1f, Vector3.zero, Quaternion.Euler(25f, 0f, 0f), 0.7f);
            Calculate(Vector3.right, Quaternion.Euler(0f, 90f, 0f), Vector3.left,
                2f, Vector3.up, Quaternion.identity, 0.2f);
            var repeated = Calculate(Vector3.up, Quaternion.identity, Vector3.forward,
                1f, Vector3.zero, Quaternion.Euler(25f, 0f, 0f), 0.7f);
            Assert.That(first.success && repeated.success, Is.True);
            Assert.That(repeated.position, Is.EqualTo(first.position));
            Assert.That(repeated.rotation, Is.EqualTo(first.rotation));
        }

        private static (bool success, Vector3 position, Quaternion rotation) Calculate(
            Vector3 position, Quaternion rotation, Vector3 localPivot, float scale,
            Vector3 anchor, Quaternion desiredRotation, float weight)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootPivotCalculator");
            Assert.That(type, Is.Not.Null, "접촉점을 기준으로 하는 계산이 아직 구현되지 않았습니다.");
            MethodInfo method = type.GetMethod("TryCalculatePose",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { position, rotation, localPivot, scale, anchor,
                desiredRotation, weight, Vector3.zero, Quaternion.identity };
            bool success = (bool)method.Invoke(null, arguments);
            return (success, (Vector3)arguments[7], (Quaternion)arguments[8]);
        }
    }
}
