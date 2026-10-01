using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// Phase 2 깊이 커브 튜닝 계층 검증 — tipStiffnessScale/animationPoseRatio/
    /// depthInertia가 템플릿 위에 정확히 반영되는지, 프리셋 왕복이 결정적인지.
    /// </summary>
    public class DepthCurveTuningTests
    {
        readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in created)
                if (go != null) Object.DestroyImmediate(go);
            created.Clear();
        }

        [Test]
        public void Given_TipStiffnessScale_When_Apply_Then_CurveTipOnlyScaled()
        {
            var s = new ClothSerializeData();
            var t = HairTuning.Default;
            t.tipStiffnessScale = 0.5f;
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t, false);

            var keys = s.angleRestorationConstraint.stiffness.curve.keys;
            // 템플릿 곡선은 (깊이0: 1.0, 깊이1: 0.11) — 끝만 절반이어야 한다
            Assert.That(keys[0].value, Is.EqualTo(1.0f).Within(0.001f),
                "루트(깊이0) 강성은 유지돼야 한다");
            Assert.That(keys[keys.Length - 1].value,
                Is.EqualTo(0.11f * 0.5f).Within(0.001f),
                "끝단(깊이1) 강성만 배율 적용돼야 한다");
        }

        [Test]
        public void Given_PoseRatioAndDepthInertia_When_Apply_Then_FieldsSet()
        {
            var s = new ClothSerializeData();
            var t = HairTuning.Default;
            t.animationPoseRatio = 0.5f;
            t.overrideDepthInertia = true;
            t.depthInertia = 0.9f;
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true, t, false);

            Assert.That(s.animationPoseRatio, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(s.inertiaConstraint.depthInertia, Is.EqualTo(0.9f).Within(0.001f));
        }

        [Test]
        public void Given_DefaultTuning_When_Apply_Then_PoseRatioZero()
        {
            var s = new ClothSerializeData();
            s.animationPoseRatio = 0.77f; // 사전 오염 값 — 튜닝이 명시적으로 0으로 덮어야 한다
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true,
                HairTuning.Default, false);
            Assert.That(s.animationPoseRatio, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Given_TunedSdata_When_ExportImportJson_Then_ValueFieldsRoundTrip()
        {
            var s1 = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s1, 0.6f, null, true,
                HairTuning.Default, false);
            s1.damping.SetValue(0.42f);

            var s2 = new ClothSerializeData();
            Assert.IsTrue(s2.ImportJson(s1.ExportJson()));
            Assert.That(s2.damping.value, Is.EqualTo(0.42f).Within(0.001f));
        }

        [Test]
        public void Given_SkirtTuning_When_LoopCreate_Then_CurveAndPoseApplied()
        {
            var root = new GameObject("Model");
            created.Add(root);
            var hips = new GameObject("Hips").transform;
            created.Add(hips.gameObject);
            hips.SetParent(root.transform, false);
            var roots = new List<Transform>();
            for (int i = 0; i < 6; i++)
            {
                float ang = i * Mathf.PI * 2f / 6f;
                var r = new GameObject($"スカート_{i}_0").transform;
                created.Add(r.gameObject);
                r.SetParent(hips, false);
                r.localPosition = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.1f;
                roots.Add(r);
            }

            var t = SkirtTuning.Default;
            t.tipStiffnessScale = 0.5f;
            t.animationPoseRatio = 0.4f;
            t.overrideDepthInertia = true;
            t.depthInertia = 0.3f;
            var cloth = SkirtBoneClothBuilder.Create(
                root.transform, roots, new List<ColliderComponent>(), false, 0f,
                false, t);
            Assert.IsNotNull(cloth);
            created.Add(cloth.gameObject);

            var sdata = cloth.SerializeData;
            Assert.That(sdata.animationPoseRatio, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(sdata.inertiaConstraint.depthInertia,
                Is.EqualTo(0.3f).Within(0.001f));
            var keys = sdata.angleRestorationConstraint.stiffness.curve.keys;
            // ApplySimParams 곡선 (깊이0: 1.0, 깊이1: 0.5) — 끝만 절반
            Assert.That(keys[0].value, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(keys[keys.Length - 1].value,
                Is.EqualTo(0.5f * 0.5f).Within(0.001f));
        }
    }
}
