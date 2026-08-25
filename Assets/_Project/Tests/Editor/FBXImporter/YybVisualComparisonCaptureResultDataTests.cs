using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonCaptureResultDataTests
    {
        [Test]
        public void Given_RunnerCaptureResult_When_Serializing_Then_PreservesInheritedContractFields()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonCaptureResultData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "YYB 전용 캡처 결과 데이터 경계가 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type captureResultType = runnerType.GetNestedType(
                "CaptureResult",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(captureResultType, Is.Not.Null);
            Assert.That(captureResultType.BaseType, Is.EqualTo(dataType));

            object result = Activator.CreateInstance(captureResultType, nonPublic: true);
            captureResultType.GetField("jobMode").SetValue(result, "MainAuto");
            captureResultType.GetField("enableYybArmSwingLimitCorrection").SetValue(result, true);

            string json = JsonUtility.ToJson(result);

            Assert.That(json, Does.Contain("\"jobMode\":\"MainAuto\""));
            Assert.That(json, Does.Contain("\"enableYybArmSwingLimitCorrection\":true"));
        }
    }
}
