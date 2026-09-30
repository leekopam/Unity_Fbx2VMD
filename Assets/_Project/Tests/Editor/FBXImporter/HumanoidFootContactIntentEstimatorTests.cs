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
            // 첫 관측 프레임부터 이어진 지지는 touchdown 미관측 구간으로 표시됨.
            Assert.That(Field(left[0], "StartsAtClipStart"), Is.EqualTo(true));
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
            Assert.That(Field(left[1], "StartsAtClipStart"), Is.EqualTo(false));
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
        public void Given_ShortUncertainGapInSupport_When_Estimating_Then_MergesIntoOneIntent()
        {
            // 지지 도중 최소 지지 길이(0.1s)보다 짧은 불확실 틈새는 의미 있는 반대 위상을
            // 담을 수 없으므로 하나의 지지로 병합함. satisfaction_2 왼발 런의
            // 4~5프레임 갭으로 인한 Plant 섬 분단이 대표 케이스임.
            List<Vector3> foot = Still(120);
            for (int frame = 45; frame <= 48; frame++)
            {
                foot[frame] += Vector3.up * 0.02f;
            }

            object estimate = Estimate(foot);
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(1));
            Assert.That(Field(left[0], "Certainty").ToString(), Is.EqualTo("Uncertain"));
            // 병합된 틈새는 확신을 낮추되 별도 불확실 구간으로는 남기지 않음.
            List<Vector2Int> uncertain =
                (List<Vector2Int>)Field(estimate, "LeftUncertainSpans");
            Assert.That(uncertain.Exists(span =>
                span.x <= 45 && span.y >= 49), Is.False);
        }

        [Test]
        public void Given_LongUncertainGapInSupport_When_Estimating_Then_KeepsSplit()
        {
            // 최소 지지 길이 이상의 불확실 틈새는 여전히 구간을 갈라야 함.
            List<Vector3> foot = Still(120);
            for (int frame = 45; frame <= 51; frame++)
            {
                foot[frame] += Vector3.up * 0.02f;
            }

            object estimate = Estimate(foot);
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(2));
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
        public void Given_StaticFeetAtDifferentHeights_When_Estimating_Then_NoIntents()
        {
            // 두 발의 고정 높이 차이는 클립 동작이 아니므로 분석기와 같이 동작 없음으로 판정됨.
            var leftFoot = Still(90);
            List<Vector3> rightFoot = Still(90)
                .Select(point => point + Vector3.up * 0.1f).ToList();

            object estimate = Estimate(leftFoot, rightFoot);
            Assert.That(Intents(estimate, "Left"), Is.Empty);
            Assert.That(Intents(estimate, "Right"), Is.Empty);
            List<Vector2Int> uncertain =
                (List<Vector2Int>)Field(estimate, "LeftUncertainSpans");
            Assert.That(uncertain.Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_HumanAirborneLabel_When_Estimating_Then_SplitsSupportSpan()
        {
            // 자동으로 지지로 분류된 구간 중간에 사람이 자유발 표식을 남기면 구간이 갈라짐.
            object labels = CreateLabelSet(new[] { CreateLabel(40, 49, false, null) });
            object estimate = Estimate(Still(90), labels);
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(2));
            Assert.That(Field(left[0], "EndFrameExclusive"), Is.EqualTo(40));
            Assert.That(Field(left[1], "StartFrame"), Is.EqualTo(50));
            // 표식 구간도 사전 병합 기준으로 덮어써서 확신은 유지됨.
            Assert.That(Field(left[0], "Certainty").ToString(), Is.EqualTo("Confident"));
        }

        [Test]
        public void Given_HumanSlideLabel_When_Estimating_Then_OverridesMode()
        {
            // 자동 판정 Plant를 사람의 Slide 표식이 덮어씀.
            object labels = CreateLabelSet(new[] { CreateLabel(1, 89, null, 1) });
            object estimate = Estimate(Still(90), labels);
            List<object> left = Intents(estimate, "Left");
            Assert.That(left.Count, Is.EqualTo(1));
            Assert.That(Field(left[0], "Mode").ToString(), Is.EqualTo("Slide"));
        }

        [Test]
        public void Given_PerfectPrediction_When_ScoringFrames_Then_F1IsOne()
        {
            // 표식 구간 전부를 지지로 맞힌 예측은 모든 지표가 1이어야 함.
            var predicted = new bool[100];
            for (int i = 10; i < 30; i++) predicted[i] = true;
            var labels = new List<FbxIntentCalibration.LabelSpan>
            {
                new FbxIntentCalibration.LabelSpan(10, 29, true),
                new FbxIntentCalibration.LabelSpan(40, 59, false),
            };
            var metrics = FbxIntentCalibration.ComputeFrameMetrics(
                predicted, labels);
            Assert.That(metrics.Precision, Is.EqualTo(1.0));
            Assert.That(metrics.Recall, Is.EqualTo(1.0));
            Assert.That(metrics.F1, Is.EqualTo(1.0));
        }

        [Test]
        public void Given_MissedSupport_When_ScoringFrames_Then_RecallDrops()
        {
            // 지지 표식 구간의 절반만 맞히면 재현율이 0.5가 됨.
            var predicted = new bool[100];
            for (int i = 10; i < 20; i++) predicted[i] = true;
            var labels = new List<FbxIntentCalibration.LabelSpan>
            {
                new FbxIntentCalibration.LabelSpan(10, 29, true),
            };
            var metrics = FbxIntentCalibration.ComputeFrameMetrics(
                predicted, labels);
            Assert.That(metrics.Recall, Is.EqualTo(0.5));
            Assert.That(metrics.Precision, Is.EqualTo(1.0));
        }

        [Test]
        public void Given_PredictedInsideAirborneLabel_When_ScoringFrames_Then_PrecisionDrops()
        {
            // 자유발 표식 구간에 지지를 예측하면 정밀도가 떨어짐.
            var predicted = new bool[100];
            for (int i = 0; i < 20; i++) predicted[i] = true;
            var labels = new List<FbxIntentCalibration.LabelSpan>
            {
                new FbxIntentCalibration.LabelSpan(0, 9, true),
                new FbxIntentCalibration.LabelSpan(10, 19, false),
            };
            var metrics = FbxIntentCalibration.ComputeFrameMetrics(
                predicted, labels);
            Assert.That(metrics.Precision, Is.EqualTo(0.5));
            Assert.That(metrics.Recall, Is.EqualTo(1.0));
        }

        [Test]
        public void Given_InvalidInput_When_Estimating_Then_Throws()
        {
            Assert.Throws<TargetInvocationException>(() =>
                EstimateMethod().Invoke(null, new object[]
                    { Array.CreateInstance(SampleType, 1), FrameRate, HumanScale, null }));
        }

        private static object Estimate(List<Vector3> leftFoot, object labels = null)
        {
            var rightFoot = new List<Vector3>(leftFoot.Count);
            for (int index = 0; index < leftFoot.Count; index++)
            {
                rightFoot.Add(Vector3.up * (0.3f + 0.004f * index) +
                    Vector3.left * 0.1f);
            }

            return Estimate(leftFoot, rightFoot, labels);
        }

        private static object Estimate(List<Vector3> leftFoot, List<Vector3> rightFoot,
            object labels = null)
        {
            Array samples = Array.CreateInstance(SampleType, leftFoot.Count);
            for (int index = 0; index < leftFoot.Count; index++)
            {
                Vector3 foot = leftFoot[index];
                Vector3 right = rightFoot[index];
                samples.SetValue(Activator.CreateInstance(
                    SampleType, Flags, null,
                    new object[] { foot, foot + Vector3.forward * 0.1f,
                        right, right + Vector3.forward * 0.1f },
                    null), index);
            }

            return EstimateMethod().Invoke(null,
                new object[] { samples, FrameRate, HumanScale, labels });
        }

        // Tuning 오버로드가 추가되어 이름만으로는 모호하므로 4인자 시그니처로 고름.
        private static MethodInfo EstimateMethod()
        {
            return EstimatorType.GetMethods(Flags)
                .First(m => m.Name == "Estimate" &&
                    m.GetParameters().Length == 4);
        }

        private static object CreateLabel(int startFrame, int endFrameInclusive,
            bool? isSupport, int? mode)
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type labelType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentLabel", true);
            Type modeType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentMode", true);
            return Activator.CreateInstance(labelType, Flags, null,
                new object[] { startFrame, endFrameInclusive, isSupport,
                    mode.HasValue ? Enum.ToObject(modeType, mode.Value) : null },
                null);
        }

        private static object CreateLabelSet(object[] leftLabels)
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type labelType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentLabel", true);
            Type setType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentLabelSet", true);
            Array left = Array.CreateInstance(labelType, leftLabels.Length);
            for (int index = 0; index < leftLabels.Length; index++)
                left.SetValue(leftLabels[index], index);
            return Activator.CreateInstance(setType, Flags, null,
                new object[] { left, Array.CreateInstance(labelType, 0),
                    leftLabels.Length, leftLabels.Length },
                null);
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
