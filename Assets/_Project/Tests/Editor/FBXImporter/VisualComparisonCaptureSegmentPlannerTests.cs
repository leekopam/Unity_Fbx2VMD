using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCaptureSegmentPlannerTests
    {
        [Test]
        public void Given_TailSegment_When_BuildingPlan_Then_AlignsWindowAndLabelsArtifacts()
        {
            Type plannerType = FindPlannerType();
            MethodInfo resolveMethod = FindMethod(plannerType, "ResolveSegment");
            MethodInfo buildMethod = FindMethod(plannerType, "BuildManualCapturePlan");
            object tail = resolveMethod.Invoke(null, new object[] { "tail" });

            object plan = buildMethod.Invoke(
                null,
                new[] { "target", "motion.fbx", (object)20f, 5f, 30f, tail });

            Assert.That(ReadField<float>(plan, "StartTimeSeconds"), Is.EqualTo(15f));
            Assert.That(ReadField<float>(plan, "DurationSeconds"), Is.EqualTo(5f));
            Assert.That(ReadField<int>(plan, "TargetFrameCount"), Is.EqualTo(150));
            Assert.That(ReadField<string>(plan, "OutputBaseName"), Is.EqualTo("target_motion_tail_5s_animtime"));
        }

        [Test]
        public void Given_HeadSegment_When_BuildingPlan_Then_PreservesLegacyArtifactName()
        {
            Type plannerType = FindPlannerType();
            object head = FindMethod(plannerType, "ResolveSegment").Invoke(null, new object[] { "unknown" });
            object plan = FindMethod(plannerType, "BuildManualCapturePlan").Invoke(
                null,
                new[] { "target", "motion.fbx", (object)20f, 5f, 30f, head });

            Assert.That(ReadField<string>(plan, "OutputBaseName"), Is.EqualTo("target_motion_5s_animtime"));
        }

        [TestCase("tail", FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail)]
        [TestCase("TAIL", FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail)]
        [TestCase("middle", FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle)]
        [TestCase("MIDDLE", FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle)]
        [TestCase("", FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head)]
        [TestCase("unknown", FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head)]
        [TestCase(null, FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head)]
        public void Given_SegmentText_When_ResolvingSegment_Then_UsesStableFallback(
            string value,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment expected)
        {
            object actual = FindMethod(FindPlannerType(), "ResolveSegment")
                .Invoke(null, new object[] { value });

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head, "head")]
        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle, "middle")]
        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail, "tail")]
        [TestCase((FBXVmdPipeline.EditorDiagnosticSmokeSegment)999, "head")]
        public void Given_Segment_When_GettingLabel_Then_UsesStableToken(
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            string expected)
        {
            object actual = FindMethod(FindPlannerType(), "GetSegmentLabel")
                .Invoke(null, new object[] { segment });

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Given_CentralSegmentLabelPolicy_When_CheckingPlaybackRunner_Then_NoPrivateWrapperRemains()
        {
            MethodInfo method = typeof(FbxPlaybackSmokeRunner).GetMethod(
                "GetSegmentLabel",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(
                method,
                Is.Null,
                "FBX smoke runner가 segment label 계산을 중복 소유하면 안 됩니다.");
        }

        private static Type FindPlannerType()
        {
            Type plannerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCaptureSegmentPlanner",
                throwOnError: false);
            Assert.That(plannerType, Is.Not.Null, "모델 중립 캡처 구간 계획 경계가 필요합니다.");
            return plannerType;
        }

        private static MethodInfo FindMethod(Type plannerType, string methodName)
        {
            MethodInfo method = plannerType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static T ReadField<T>(object instance, string fieldName)
        {
            FieldInfo field = instance.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(instance);
        }
    }
}
