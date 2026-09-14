using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidLegPoseSolverTests
    {
        [Test]
        public void Given_AdditionalBend_When_CalculatingDirection_Then_UsesCalibratedPlane()
        {
            Vector3 upper = Vector3.down * 0.5f;
            Vector3 lower = Quaternion.AngleAxis(3f, Vector3.right) * upper;
            Vector3 target = (upper + lower).normalized * 0.8f;
            Vector3 reference = Quaternion.AngleAxis(70f, target.normalized) * Vector3.right;
            object[] args = { upper, lower, target, reference, Vector3.zero, 0f };
            Assert.That(CalculateDirection(args), Is.True);
            Assert.That((float)args[5], Is.GreaterThan(0.9f));
            Assert.That(Vector3.Angle((Vector3)args[4], reference), Is.LessThan(8f));
            Assert.That(Mathf.Abs(Vector3.Dot(((Vector3)args[4]).normalized, target.normalized)), Is.LessThan(0.000001f));
        }

        [Test]
        public void Given_NoAdditionalBend_When_CalculatingDirection_Then_PreservesOriginalPlane()
        {
            Vector3 upper = Vector3.down * 0.5f;
            Vector3 lower = Quaternion.AngleAxis(40f, Vector3.right) * upper;
            Vector3 original = Vector3.Cross(upper, lower).normalized;
            foreach (float distanceScale in new[] { 1f, 1.02f })
            {
                object[] args = { upper, lower, (upper + lower) * distanceScale, -original, Vector3.zero, 0f };
                Assert.That(CalculateDirection(args), Is.True);
                Assert.That((float)args[5], Is.EqualTo(0f).Within(0.000001f));
                Assert.That(Vector3.Distance((Vector3)args[4], original), Is.LessThan(0.000001f));
            }
        }

        [Test]
        public void Given_RotatedAndScaledLeg_When_CalculatingDirection_Then_PreservesRelativeResult()
        {
            Vector3 upper = Vector3.down * 0.5f;
            Vector3 lower = Quaternion.AngleAxis(5f, Vector3.right) * upper;
            Vector3 target = (upper + lower).normalized * 0.85f;
            Vector3 reference = Quaternion.AngleAxis(40f, target.normalized) * Vector3.right;
            object[] original = { upper, lower, target, reference, Vector3.zero, 0f };
            Assert.That(CalculateDirection(original), Is.True);
            Quaternion rotation = Quaternion.Euler(25f, 60f, -15f);
            object[] changed = { rotation * upper * 2.5f, rotation * lower * 2.5f,
                rotation * target * 2.5f, rotation * reference, Vector3.zero, 0f };
            Assert.That(CalculateDirection(changed), Is.True);
            Assert.That((float)changed[5], Is.EqualTo((float)original[5]).Within(0.000001f));
            Assert.That(Vector3.Distance((Vector3)changed[4], rotation * (Vector3)original[4]), Is.LessThan(0.000001f));
        }

        [Test]
        public void Given_StraightOrInvalidLeg_When_CalculatingDirection_Then_HandlesMissingPlaneExplicitly()
        {
            Vector3 upper = Vector3.down * 0.5f;
            object[] straight = { upper, upper, Vector3.down * 0.8f, Vector3.right, Vector3.zero, 0f };
            Assert.That(CalculateDirection(straight), Is.True);
            Assert.That((Vector3)straight[4], Is.EqualTo(Vector3.right));
            Assert.That((float)straight[5], Is.EqualTo(1f));
            foreach (Vector3 invalidTarget in new[] { Vector3.zero, Vector3.down * 2f, new Vector3(float.NaN, 0f, 0f) })
            {
                object[] invalid = { upper, upper, invalidTarget, Vector3.right, Vector3.one, 1f };
                Assert.That(CalculateDirection(invalid), Is.False);
                Assert.That((Vector3)invalid[4], Is.EqualTo(Vector3.zero));
                Assert.That((float)invalid[5], Is.EqualTo(0f));
            }
            Vector3 lower = Quaternion.AngleAxis(3f, Vector3.right) * upper;
            object[] opposite = { upper, lower, (upper + lower).normalized * 0.8f, Vector3.left, Vector3.one, 1f };
            Assert.That(CalculateDirection(opposite), Is.False);
            Assert.That((Vector3)opposite[4], Is.EqualTo(Vector3.zero));
            object[] lostPlane = { upper, lower, Vector3.right * 0.8f, Vector3.forward, Vector3.one, 1f };
            Assert.That(CalculateDirection(lostPlane), Is.False);
            Assert.That((Vector3)lostPlane[4], Is.EqualTo(Vector3.zero));
            Assert.That((float)lostPlane[5], Is.EqualTo(0f));
        }

        private static bool CalculateDirection(object[] args)
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidLegPoseSolver");
            MethodInfo method = type.GetMethod("TryCalculateBendNormal", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "추가 굽힘의 방향 증폭을 제어하는 계산 필요");
            return (bool)method.Invoke(null, args);
        }

        [Test]
        public void Given_SubDegreeResidual_When_Solving_Then_ReachesTargetWithoutStretch()
        {
            var upper = new GameObject("허벅지");
            var lower = new GameObject("종아리");
            var foot = new GameObject("발");
            try
            {
                lower.transform.SetParent(upper.transform, false);
                foot.transform.SetParent(lower.transform, false);
                lower.transform.localPosition = new Vector3(0f, -0.445f, 0f);
                foot.transform.localPosition = Quaternion.Euler(3.2f, 0f, 0f) * new Vector3(0f, -0.445f, 0f);
                Vector3 originalLower = lower.transform.localPosition;
                Vector3 originalFoot = foot.transform.localPosition;
                Vector3 direction = foot.transform.position - lower.transform.position;
                Vector3 target = lower.transform.position + Quaternion.AngleAxis(0.079f, Vector3.right) * direction;
                Vector3 bend = Vector3.Cross(lower.transform.position - upper.transform.position, direction);
                RootMotion.FinalIK.IKSolverTrigonometric.Solve(upper.transform, lower.transform,
                    foot.transform, target, bend, 1f);
                Assert.That(Vector3.Distance(foot.transform.position, target), Is.GreaterThan(0.0001f),
                    "기존 계산에서 미세 회전 누락이 재현되어야 함");
                upper.transform.localRotation = Quaternion.identity;
                lower.transform.localRotation = Quaternion.identity;
                Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidLegPoseSolver");
                Assert.That(type, Is.Not.Null, "미세 회전 누락을 보완하는 다리 계산 필요");
                object[] args = { upper.transform, lower.transform, foot.transform, target, bend, 0f };
                bool success = (bool)type.GetMethod("TrySolve", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
                Assert.That(success, Is.True);
                Assert.That((float)args[5], Is.LessThan(0.000001f));
                Assert.That(Vector3.Distance(foot.transform.position, target), Is.LessThan(0.000001f));
                Assert.That(lower.transform.localPosition, Is.EqualTo(originalLower));
                Assert.That(foot.transform.localPosition, Is.EqualTo(originalFoot));
                Assert.That(lower.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(foot.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(upper);
            }
        }
    }
}
