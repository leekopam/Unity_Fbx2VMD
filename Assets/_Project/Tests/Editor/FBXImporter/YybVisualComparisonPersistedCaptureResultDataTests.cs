using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonPersistedCaptureResultDataTests
    {
        [Test]
        public void Given_RunnerPersistedCaptureResult_When_Serializing_Then_PreservesInheritedContractFields()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonCaptureResultData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "YYB 캡처 결과의 공통 데이터 계약이 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type persistedResultType = runnerType.GetNestedType(
                "PersistedCaptureResult",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(persistedResultType, Is.Not.Null);
            Assert.That(persistedResultType.BaseType, Is.EqualTo(dataType));

            object result = Activator.CreateInstance(persistedResultType, nonPublic: true);
            persistedResultType.GetField("jobMode").SetValue(result, "SubManual");
            persistedResultType.GetField("enableYybArmSwingLimitCorrection").SetValue(result, true);

            string json = JsonUtility.ToJson(result);

            Assert.That(json, Does.Contain("\"jobMode\":\"SubManual\""));
            Assert.That(json, Does.Contain("\"enableYybArmSwingLimitCorrection\":true"));
        }
    }
}
