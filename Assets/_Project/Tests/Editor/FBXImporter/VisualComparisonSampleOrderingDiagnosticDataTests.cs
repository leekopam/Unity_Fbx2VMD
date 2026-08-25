using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonSampleOrderingDiagnosticDataTests
    {
        [Test]
        public void Given_RunnerDiagnostic_When_Serializing_Then_PreservesOrderingFields()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonSampleOrderingDiagnosticData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "모델 중립 샘플 순서 진단 데이터 경계가 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type diagnosticType = runnerType.GetNestedType(
                "SummarySampleOrderingDiagnostic",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(diagnosticType, Is.Not.Null);
            Assert.That(diagnosticType.BaseType, Is.EqualTo(dataType));

            object diagnostic = Activator.CreateInstance(diagnosticType, nonPublic: true);
            diagnosticType.GetField("job_mode").SetValue(diagnostic, "MainAuto");
            diagnosticType.GetField("recording_metric_recorder_frame_span").SetValue(diagnostic, 119);

            string json = JsonUtility.ToJson(diagnostic);

            Assert.That(json, Does.Contain("\"job_mode\":\"MainAuto\""));
            Assert.That(json, Does.Contain("\"recording_metric_recorder_frame_span\":119"));
        }
    }
}
