using System.Collections.Generic;
using System.Reflection;
using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    public class PhysicsValidationProbeTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        Transform MakeTransform(string name, Transform parent,
            Vector3 pos, Vector3 euler, float scale = 1f)
        {
            var go = new GameObject(name);
            created.Add(go);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localEulerAngles = euler;
            t.localScale = Vector3.one * scale;
            return t;
        }

        [Test]
        public void Given_XSymmetry_When_ComputeSymmetryPose_Then_MirrorsSourceLocalPose()
        {
            var target = MakeTransform("armR", null,
                new Vector3(-0.3f, 1.2f, 0f), new Vector3(0f, 45f, 0f));
            var src = MakeTransform("col", null,
                new Vector3(0.1f, 0.05f, -0.02f), new Vector3(10f, 20f, 30f));

            bool ok = PhysicsValidationProbe.TryComputeSymmetryPose(
                ColliderSymmetryMode.X_Symmetry, src, target,
                out Vector3 pos, out Quaternion rot, out Vector3 scl);

            Assert.That(ok, Is.True);
            // MC2 규칙: 로컬 x 반사, 오일러 y/z 부호 반전, 타겟을 부모로 사용
            Vector3 expectedPos = target.TransformPoint(new Vector3(-0.1f, 0.05f, -0.02f));
            Quaternion expectedRot = target.rotation * Quaternion.Euler(10f, -20f, -30f);
            Assert.That(Vector3.Distance(pos, expectedPos), Is.LessThan(0.0001f));
            Assert.That(Quaternion.Angle(rot, expectedRot), Is.LessThan(0.01f));
            Assert.That(scl, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void Given_XyzSymmetry_When_ComputeSymmetryPose_Then_NegatesPosition()
        {
            var target = MakeTransform("armR", null, Vector3.zero, Vector3.zero);
            var src = MakeTransform("col", null,
                new Vector3(0.1f, 0.05f, -0.02f), new Vector3(10f, 20f, 30f));

            bool ok = PhysicsValidationProbe.TryComputeSymmetryPose(
                ColliderSymmetryMode.XYZ_Symmetry, src, target,
                out Vector3 pos, out Quaternion rot, out _);

            Assert.That(ok, Is.True);
            Assert.That(Vector3.Distance(pos, new Vector3(-0.1f, -0.05f, 0.02f)),
                Is.LessThan(0.0001f));
            // XYZ 대칭은 회전을 건드리지 않는다 (MC2 규칙)
            Assert.That(Quaternion.Angle(rot, Quaternion.Euler(10f, 20f, 30f)),
                Is.LessThan(0.01f));
        }

        [Test]
        public void Given_NoTarget_When_ComputeSymmetryPose_Then_False()
        {
            var src = MakeTransform("col", null, Vector3.zero, Vector3.zero);
            Assert.That(PhysicsValidationProbe.TryComputeSymmetryPose(
                ColliderSymmetryMode.X_Symmetry, src, null,
                out _, out _, out _), Is.False);
            Assert.That(PhysicsValidationProbe.TryComputeSymmetryPose(
                ColliderSymmetryMode.None, src, src,
                out _, out _, out _), Is.False);
        }

        [Test]
        public void Given_SampledBaselines_When_ResetStats_Then_RearmedForRebaseline()
        {
            var go = new GameObject("probe");
            created.Add(go);
            var probe = go.AddComponent<PhysicsValidationProbe>();
            // 내부 기저선 슬롯을 채운 뒤 ResetStats가 NaN으로 재무장하는지 확인한다.
            var field = typeof(PhysicsValidationProbe).GetField(
                "baselines", BindingFlags.NonPublic | BindingFlags.Instance);
            var baselines = (List<float>)field.GetValue(probe);
            baselines.Add(0.01f);
            baselines.Add(0.02f);

            probe.ResetStats();

            Assert.That(baselines, Has.Count.EqualTo(2));
            Assert.That(float.IsNaN(baselines[0]), Is.True);
            Assert.That(float.IsNaN(baselines[1]), Is.True);
        }

        [Test]
        public void Given_BaselinePenetrated_When_BuildReport_Then_Warns()
        {
            var go = new GameObject("probe");
            created.Add(go);
            var probe = go.AddComponent<PhysicsValidationProbe>();
            probe.penetrationWarningDepth = 0.005f;
            probe.baselineWorstPenetration = 0.02f;

            string report = probe.BuildReport();

            Assert.That(report, Does.Contain("기저선 최대 침투"));
            Assert.That(report, Does.Contain("경고: 시작 자세"));
        }
    }
}
