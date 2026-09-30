using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class ColliderMathTests
    {
        [Test]
        public void Given_PointOnSegmentAxis_When_Distance_Then_ReturnsPerpendicular()
        {
            var a = Vector3.zero;
            var b = Vector3.right; // x축 선분
            float d = ColliderMath.DistancePointToSegment(new Vector3(0.5f, 1f, 0f), a, b);
            Assert.That(d, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Given_PointBeyondSegmentEnd_When_Distance_Then_ReturnsEndDistance()
        {
            float d = ColliderMath.DistancePointToSegment(new Vector3(2f, 0f, 0f), Vector3.zero, Vector3.right);
            Assert.That(d, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Given_DegenerateSegment_When_Distance_Then_ReturnsPointDistance()
        {
            float d = ColliderMath.DistancePointToSegment(Vector3.up, Vector3.zero, Vector3.zero);
            Assert.That(d, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Given_Points_When_Percentile_Then_ReturnsExpected()
        {
            var values = new List<float> { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };
            Assert.That(ColliderMath.Percentile(values, 0.5f), Is.EqualTo(5.5f).Within(1e-4f));
            Assert.That(ColliderMath.Percentile(values, 0.9f), Is.EqualTo(9.1f).Within(1e-4f));
            Assert.That(ColliderMath.Percentile(values, 1.0f), Is.EqualTo(10f));
        }

        [Test]
        public void Given_SphereVerts_When_FitRadius_Then_CoversPercentile()
        {
            // 반경 0.1 구 위의 점들 + 이상치 1개
            var verts = new List<Vector3>();
            for (int i = 0; i < 20; i++)
                verts.Add(Random.onUnitSphere * 0.1f);
            verts.Add(Vector3.right * 5f); // 이상치 — 95 백분위로 무시되어야 함

            float r = ColliderMath.FitSphereRadius(Vector3.zero, verts, 0.95f);
            Assert.That(r, Is.GreaterThan(0.09f));
            Assert.That(r, Is.LessThan(1f), "이상치가 반경을 지배하면 안 됩니다.");
        }

        [Test]
        public void Given_SignedDistance_When_InsideSphere_Then_Negative()
        {
            float sd = ColliderMath.SignedDistanceSphere(Vector3.zero, Vector3.zero, 0.1f);
            Assert.That(sd, Is.LessThan(0f));
            Assert.That(sd, Is.EqualTo(-0.1f).Within(1e-5f));
        }
    }
}
