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
        public void Given_CommonDiagnostics_When_Serializing_Then_PreservesMetricsAndExcludesRuntimeRows()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonFrameRoleDiagnosticsData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "모델 중립 프레임 역할 진단 데이터 경계가 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type runnerOwnedDiagnosticsType = runnerType.GetNestedType(
                "SummaryFrameRoleDiagnostics",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(
                runnerOwnedDiagnosticsType,
                Is.Null,
                "모델 중립 진단 데이터가 YYB Runner 내부 타입에 종속되면 안 됩니다.");

            object diagnostics = Activator.CreateInstance(dataType, nonPublic: true);
            dataType.GetField("reference_target_frame_count").SetValue(diagnostics, 120);
            dataType.GetField("candidate_recorded_frame_count").SetValue(diagnostics, 118);
            IList runtimeRows = (IList)dataType.GetField("referenceMp4CurrentClipRows").GetValue(diagnostics);
            Assert.That(runtimeRows, Is.Empty);

            string json = JsonUtility.ToJson(diagnostics);

            Assert.That(json, Does.Contain("\"reference_target_frame_count\":120"));
            Assert.That(json, Does.Contain("\"candidate_recorded_frame_count\":118"));
            Assert.That(json, Does.Not.Contain("referenceMp4CurrentClipRows"));
        }
    }
}
