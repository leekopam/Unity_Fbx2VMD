using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootContactIntentEstimatorTests
    {
        private const float FrameRate = 60f;
        private const float HumanScale = 1f;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type SampleType = typeof(FBXVmdPipeline).Assembly
            .GetType("Fbx2Vmd.FBXImporter.HumanoidFootContactSample", true);
        private static readonly Type EstimatorType = typeof(FBXVmdPipeline).Assembly
            .GetType("Fbx2Vmd.FBXImporter.HumanoidFootContactIntentEstimator", true);

        [Test]
        public void Given_StillFoot_When_Estimating_Then_ReportsOneConfidentPlant()
        {
            // 0프레임은 속도 미정의로 항상 불확실이므로 지지 시작은 1프레임부터.
            object estimate = Estimate(Still(90));
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(1));
            Assert.That(Field(left[0], "StartFrame"), Is.EqualTo(1));
            Assert.That(Field(left[0], "EndFrameExclusive"), Is.EqualTo(90));
            Assert.That(Field(left[0], "Mode").ToString(), Is.EqualTo("Plant"));
            Assert.That(Field(left[0], "Certainty").ToString(), Is.EqualTo("Confident"));
            // 앵커는 발목·발끝 중점임.
            Assert.That((Vector3)Field(left[0], "Anchor"),
                Is.EqualTo(new Vector3(0.1f, 0f, 0.25f)));
        }

        [Test]
        public void Given_LiftedInterval_When_Estimating_Then_SplitsTwoIntents()
        {
            List<Vector3> foot = Still(120);
            for (int frame = 30; frame < 60; frame++)
            {
                foot[frame] = foot[frame] + Vector3.up * 0.3f +
                    Vector3.forward * 0.001f * frame;
            }

            object estimate = Estimate(foot);
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(2));
            Assert.That(Field(left[0], "EndFrameExclusive"), Is.EqualTo(30));
            Assert.That(Field(left[1], "StartFrame"), Is.GreaterThanOrEqualTo(60));
            List<Vector2Int> uncertain =
                (List<Vector2Int>)Field(estimate, "LeftUncertainSpans");
            Assert.That(uncertain.Exists(span =>
                span.x <= 30 && span.y >= 60), Is.True);
        }

        [Test]
        public void Given_DirectedGlide_When_Estimating_Then_ReportsSlide()
        {
            // 느린 활주는 지지 후보 속도 안쪽에서 방향성 있는 수평 이동을 만듦.
            List<Vector3> foot = Still(90);
            for (int frame = 1; frame < foot.Count; frame++)
            {
                foot[frame] += Vector3.right * 0.0003f * frame;
            }

            object estimate = Estimate(foot);
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(1));
            Assert.That(Field(left[0], "Mode").ToString(), Is.EqualTo("Slide"));
        }

        [Test]
        public void Given_GapFilledSpan_When_Estimating_Then_MarksUncertain()
        {
            // 지지 도중 2프레임 이내의 틈새는 병합되지만 확신은 낮춤.
            List<Vector3> foot = Still(90);
            foot[45] += Vector3.up * 0.02f;

            object estimate = Estimate(foot);
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(1));
            Assert.That(Field(left[0], "Certainty").ToString(), Is.EqualTo("Uncertain"));
        }

        [Test]
        public void Given_AirborneClip_When_Estimating_Then_NoIntents()
        {
            List<Vector3> foot = Still(90);
            for (int frame = 1; frame < foot.Count; frame++)
            {
                foot[frame] = new Vector3(0.1f, 0.5f + 0.001f * frame, 0.2f);
            }

            object estimate = Estimate(foot);
            Assert.That(Intents(estimate, "Left"), Is.Empty);
            Assert.That(Intents(estimate, "Right"), Is.Empty);
        }

        [Test]
        public void Given_InvalidInput_When_Estimating_Then_Throws()
        {
            MethodInfo estimate = EstimatorType.GetMethod("Estimate", Flags);
            Assert.Throws<TargetInvocationException>(() =>
                estimate.Invoke(null, new object[]
                    { Array.CreateInstance(SampleType, 1), FrameRate, HumanScale }));
        }

        private static object Estimate(List<Vector3> leftFoot)
        {
            Array samples = Array.CreateInstance(SampleType, leftFoot.Count);
            for (int index = 0; index < leftFoot.Count; index++)
            {
                Vector3 foot = leftFoot[index];
                Vector3 rightFoot = Vector3.up * (0.3f + 0.004f * index) +
                    Vector3.left * 0.1f;
                samples.SetValue(Activator.CreateInstance(
                    SampleType, Flags, null,
                    new object[] { foot, foot + Vector3.forward * 0.1f,
                        rightFoot, rightFoot + Vector3.forward * 0.1f },
                    null), index);
            }

            return EstimatorType.GetMethod("Estimate", Flags).Invoke(null,
                new object[] { samples, FrameRate, HumanScale });
        }

        private static List<Vector3> Still(int count)
        {
            var foot = new List<Vector3>(count);
            for (int index = 0; index < count; index++)
            {
                foot.Add(new Vector3(0.1f, 0f, 0.2f));
            }

            return foot;
        }

        private static List<object> Intents(object estimate, string side)
        {
            return new List<object>(
                ((IEnumerable)Field(estimate, side)).Cast<object>());
        }

        private static object Field(object instance, string name)
        {
            return instance.GetType().GetProperty(name, Flags).GetValue(instance);
        }
    }
}
