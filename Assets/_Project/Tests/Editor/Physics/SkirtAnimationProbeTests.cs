using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>
    /// Phase 3 — 스커트 본 애니메이션 구동 판정과 커스텀 스키닝 폴백 검증.
    /// </summary>
    public class SkirtAnimationProbeTests
    {
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in created)
                if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        Transform Bone(string name, Transform parent)
        {
            var go = new GameObject(name);
            created.Add(go);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        [Test]
        public void Given_MovingBoneSamples_When_IsAnimated_Then_True()
        {
            var pos = new List<Vector3> { Vector3.zero, Vector3.zero, new Vector3(0.01f, 0, 0) };
            var rot = new List<Quaternion> {
                Quaternion.identity, Quaternion.identity, Quaternion.Euler(0, 0, 5f) };
            Assert.IsTrue(SkirtAnimationProbe.IsAnimated(pos, rot));
        }

        [Test]
        public void Given_StaticBoneSamples_When_IsAnimated_Then_False()
        {
            var pos = new List<Vector3> { Vector3.zero, Vector3.zero, Vector3.zero };
            var rot = new List<Quaternion> {
                Quaternion.identity, Quaternion.identity, Quaternion.identity };
            Assert.IsFalse(SkirtAnimationProbe.IsAnimated(pos, rot));
        }

        [Test]
        public void Given_ClipWithSkirtBinding_When_Evaluate_Then_Animated()
        {
            var root = new GameObject("Model").transform;
            created.Add(root.gameObject);
            var skirt = Bone("スカート_0_0", root);

            var clip = new AnimationClip();
            created.Add(clip);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            UnityEditor.AnimationUtility.SetEditorCurve(clip,
                UnityEditor.EditorCurveBinding.FloatCurve("スカート_0_0", typeof(Transform), "m_LocalRotation.x"),
                curve);

            var verdict = SkirtAnimationProbe.EvaluateClips(
                new[] { clip }, root, new List<Transform> { skirt }, out string detail);
            Assert.AreEqual(SkirtAnimationProbe.Verdict.Animated, verdict, detail);
        }

        [Test]
        public void Given_ClipWithoutSkirtBinding_When_Evaluate_Then_Static()
        {
            var root = new GameObject("Model").transform;
            created.Add(root.gameObject);
            var skirt = Bone("スカート_0_0", root);

            var clip = new AnimationClip();
            created.Add(clip);
            UnityEditor.AnimationUtility.SetEditorCurve(clip,
                UnityEditor.EditorCurveBinding.FloatCurve("Spine", typeof(Transform), "m_LocalPosition.y"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));

            var verdict = SkirtAnimationProbe.EvaluateClips(
                new[] { clip }, root, new List<Transform> { skirt }, out string detail);
            Assert.AreEqual(SkirtAnimationProbe.Verdict.Static, verdict, detail);
        }

        [Test]
        public void Given_NoClips_When_Evaluate_Then_NoClip()
        {
            var root = new GameObject("Model").transform;
            created.Add(root.gameObject);
            var skirt = Bone("スカート_0_0", root);

            var verdict = SkirtAnimationProbe.EvaluateClips(
                new AnimationClip[0], root, new List<Transform> { skirt }, out _);
            Assert.AreEqual(SkirtAnimationProbe.Verdict.NoClip, verdict);
        }

        [Test]
        public void Given_NoBones_When_Evaluate_Then_MissingBones()
        {
            var verdict = SkirtAnimationProbe.EvaluateClips(
                null, null, new List<Transform>(), out _);
            Assert.AreEqual(SkirtAnimationProbe.Verdict.MissingBones, verdict);
        }

        [Test]
        public void Given_ResolvedBones_When_ApplyCustomSkinning_Then_EnabledWithBones()
        {
            var sdata = new ClothSerializeData();
            var root = new GameObject("Model").transform;
            created.Add(root.gameObject);
            var hips = Bone("Hips", root);
            var leg = Bone("UpperLeg_L", root);

            int n = SkirtAnimationProbe.ApplyCustomSkinning(
                sdata, new List<Transform> { hips, leg, null });
            Assert.AreEqual(2, n);
            Assert.IsTrue(sdata.customSkinningSetting.enable);
            CollectionAssert.AreEqual(new[] { hips, leg },
                sdata.customSkinningSetting.skinningBones);
        }

        [Test]
        public void Given_NoBones_When_ApplyCustomSkinning_Then_ZeroAndDisabled()
        {
            var sdata = new ClothSerializeData();
            int n = SkirtAnimationProbe.ApplyCustomSkinning(sdata, null);
            Assert.AreEqual(0, n);
            Assert.IsFalse(sdata.customSkinningSetting.enable);
        }
    }
}
