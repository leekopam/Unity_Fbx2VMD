using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidSoleGroundConstraintTests
    {
        [Test]
        public void Given_ContactPivotWithPenetratingSole_When_Constraining_Then_LiftsAllPoints()
        {
            var points = new[] { Vector3.zero, new Vector3(0f, -0.07f, 0.2f) };
            var result = Calculate(Vector3.zero, Quaternion.identity, points, 1f,
                Vector3.zero, Vector3.up);

            Assert.That(result.success, Is.True);
            Assert.That(result.lift, Is.EqualTo(0.07f).Within(0.000001f));
            foreach (Vector3 point in points)
                Assert.That((result.position + point).y, Is.GreaterThanOrEqualTo(-0.000001f));
            // 관통 제거를 위해 원래 접촉점이 떠오른 결과를 숨기지 않음.
            Assert.That(result.position.y, Is.EqualTo(0.07f).Within(0.000001f));
        }

        [Test]
        public void Given_ClearSole_When_Constraining_Then_DoesNotPullFootDown()
        {
            Vector3 position = new Vector3(1f, 0.2f, 2f);
            var result = Calculate(position, Quaternion.identity, new[] { Vector3.zero },
                1f, Vector3.zero, Vector3.up);
            Assert.That(result.success, Is.True);
            Assert.That(result.position, Is.EqualTo(position));
            Assert.That(result.lift, Is.Zero);
        }

        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(2f)]
        public void Given_RolledFoot_When_Constraining_Then_UsesRotatedAndScaledSole(float scale)
        {
            var points = new[] { Vector3.zero, Vector3.forward * 0.2f };
            Quaternion rotation = Quaternion.Euler(30f, 0f, 0f);
            var result = Calculate(Vector3.zero, rotation, points, scale, Vector3.zero, Vector3.up);
            Assert.That(result.success, Is.True);
            Assert.That(result.lift, Is.EqualTo(0.1f * scale).Within(0.000001f));
            Matrix4x4 matrix = Matrix4x4.TRS(result.position, rotation, Vector3.one * scale);
            foreach (Vector3 point in points)
                Assert.That(matrix.MultiplyPoint3x4(point).y, Is.GreaterThanOrEqualTo(-0.000001f));
        }

        [Test]
        public void Given_SlopedGround_When_Constraining_Then_PreservesPlaneTangentPosition()
        {
            Vector3 normal = new Vector3(0f, 1f, 1f).normalized;
            Vector3 groundPoint = new Vector3(2f, 3f, -4f);
            Vector3 position = groundPoint - normal * 0.3f;
            var result = Calculate(position, Quaternion.identity, new[] { Vector3.zero },
                1f, groundPoint, normal);
            Assert.That(result.success, Is.True);
            Assert.That(result.lift, Is.EqualTo(0.3f).Within(0.000001f));
            Assert.That(Vector3.ProjectOnPlane(result.position - position, normal).magnitude,
                Is.LessThan(0.000001f));
            Assert.That(Vector3.Distance(result.position, groundPoint), Is.LessThan(0.000001f));
        }

        [Test]
        public void Given_ReorderedDuplicatePoints_When_Constraining_Then_ReturnsSameResult()
        {
            var a = Calculate(Vector3.zero, Quaternion.identity,
                new[] { Vector3.up, Vector3.down }, 1f, Vector3.zero, Vector3.up);
            var b = Calculate(Vector3.zero, Quaternion.identity,
                new[] { Vector3.down, Vector3.up, Vector3.down }, 1f, Vector3.zero, Vector3.up);
            Assert.That(a.success && b.success, Is.True);
            Assert.That(b.position, Is.EqualTo(a.position));
            Assert.That(b.lift, Is.EqualTo(a.lift));
        }

        [Test]
        public void Given_MissingOrNonfiniteSole_When_Constraining_Then_Rejects()
        {
            foreach (var points in new[] { null, Array.Empty<Vector3>(), new[] { new Vector3(float.NaN, 0f, 0f) } })
                Assert.That(Calculate(Vector3.zero, Quaternion.identity, points, 1f,
                    Vector3.zero, Vector3.up).success, Is.False);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        public void Given_InvalidScale_When_Constraining_Then_Rejects(float scale)
        {
            Assert.That(Calculate(Vector3.zero, Quaternion.identity, new[] { Vector3.zero },
                scale, Vector3.zero, Vector3.up).success, Is.False);
        }

        [Test]
        public void Given_InvalidGroundOrRotation_When_Constraining_Then_Rejects()
        {
            foreach (var normal in new[] { Vector3.zero, Vector3.down, Vector3.up * 2f, new Vector3(0f, float.NaN, 0f) })
                Assert.That(Calculate(Vector3.zero, Quaternion.identity, new[] { Vector3.zero },
                    1f, Vector3.zero, normal).success, Is.False);
            Assert.That(Calculate(Vector3.zero, default, new[] { Vector3.zero },
                1f, Vector3.zero, Vector3.up).success, Is.False);
        }

        [Test]
        public void Given_OverflowingSole_When_Constraining_Then_RejectsBeforeApplying()
        {
            var result = Calculate(Vector3.up, Quaternion.identity,
                new[] { Vector3.down * float.MaxValue }, 2f, Vector3.zero, Vector3.up);
            Assert.That(result.success, Is.False);
            Assert.That(result.position, Is.EqualTo(Vector3.up));
            Assert.That(result.lift, Is.Zero);
        }

        private static (bool success, Vector3 position, float lift) Calculate(
            Vector3 position, Quaternion rotation, Vector3[] points, float scale,
            Vector3 groundPoint, Vector3 normal)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidSoleGroundConstraint");
            Assert.That(type, Is.Not.Null, "밑창 전체의 지면 제약 계산이 아직 구현되지 않았습니다.");
            MethodInfo method = type.GetMethod("TryLiftAbovePlane", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] args = { position, rotation, points, scale, groundPoint, normal, Vector3.zero, 0f };
            bool success = (bool)method.Invoke(null, args);
            return (success, (Vector3)args[6], (float)args[7]);
        }
    }
}
