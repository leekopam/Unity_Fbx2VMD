using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonFrameRoleDiagnosticsDataTests
    {
        [Test]
        public void Given_RunnerDiagnostics_When_Serializing_Then_PreservesMetricsAndExcludesRuntimeRows()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonFrameRoleDiagnosticsData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "모델 중립 프레임 역할 진단 데이터 경계가 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type diagnosticsType = runnerType.GetNestedType(
                "SummaryFrameRoleDiagnostics",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(diagnosticsType, Is.Not.Null);
            Assert.That(diagnosticsType.BaseType, Is.EqualTo(dataType));

            object diagnostics = Activator.CreateInstance(diagnosticsType, nonPublic: true);
            diagnosticsType.GetField("reference_target_frame_count").SetValue(diagnostics, 120);
            diagnosticsType.GetField("candidate_recorded_frame_count").SetValue(diagnostics, 118);
            IList runtimeRows = (IList)diagnosticsType.GetField("referenceMp4CurrentClipRows").GetValue(diagnostics);
            Assert.That(runtimeRows, Is.Empty);

            string json = JsonUtility.ToJson(diagnostics);

            Assert.That(json, Does.Contain("\"reference_target_frame_count\":120"));
            Assert.That(json, Does.Contain("\"candidate_recorded_frame_count\":118"));
            Assert.That(json, Does.Not.Contain("referenceMp4CurrentClipRows"));
        }
    }
}
