using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonSummarySettingsSnapshotterTests
    {
        [Test]
        public void Given_RunState_When_Capturing_Then_MapsGenericAndYybSettings()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type stateType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunStateData",
                throwOnError: true);
            Type summaryType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonSummaryData",
                throwOnError: true);
            Type snapshotterType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonSummarySettingsSnapshotter",
                throwOnError: false);
            Assert.That(snapshotterType, Is.Not.Null, "실행 상태를 요약 설정으로 변환하는 경계가 필요합니다.");

            object state = Activator.CreateInstance(stateType, nonPublic: true);
            stateType.GetField("fbxFileName").SetValue(state, "dance.fbx");
            stateType.GetField("durationSeconds").SetValue(state, 2.5f);
            stateType.GetField("editorDiagnosticSmokeSegment").SetValue(state, "Full");
            stateType.GetField("enableFingerCloseups").SetValue(state, true);
            stateType.GetField("enableYybArmSwingLimitRuntimeOverride").SetValue(state, true);
            stateType.GetField("yybArmSwingLimitWeight").SetValue(state, 0.75f);

            object summary = Activator.CreateInstance(summaryType, nonPublic: true);
            MethodInfo captureMethod = snapshotterType.GetMethod(
                "Capture",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(captureMethod, Is.Not.Null);

            captureMethod.Invoke(
                null,
                new[] { summary, state, (object)120, "Assets/output/reference.vmd" });

            Assert.That(summaryType.GetField("fbx_file").GetValue(summary), Is.EqualTo("dance.fbx"));
            Assert.That(summaryType.GetField("duration_seconds").GetValue(summary), Is.EqualTo(2.5f));
            Assert.That(summaryType.GetField("target_frame_count").GetValue(summary), Is.EqualTo(120));
            Assert.That(summaryType.GetField("segment").GetValue(summary), Is.EqualTo("Full"));
            Assert.That(summaryType.GetField("finger_closeups").GetValue(summary), Is.True);
            Assert.That(summaryType.GetField("yyb_arm_swing_limit_enabled").GetValue(summary), Is.True);
            Assert.That(summaryType.GetField("yyb_arm_swing_limit_weight").GetValue(summary), Is.EqualTo(0.75f));
            Assert.That(
                summaryType.GetField("vmd_playback_probe_source_vmd_path").GetValue(summary),
                Is.EqualTo("Assets/output/reference.vmd"));
        }
    }
}
