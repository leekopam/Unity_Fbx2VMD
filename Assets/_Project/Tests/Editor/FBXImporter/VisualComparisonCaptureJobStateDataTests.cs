using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCaptureJobStateDataTests
    {
        [Test]
        public void Given_PersistedCaptureJob_When_Serializing_Then_PreservesGenericJobFields()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCaptureJobStateData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "모델 중립 캡처 작업 상태 계약이 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type persistedJobType = runnerType.GetNestedType(
                "PersistedCaptureJob",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(persistedJobType, Is.Not.Null);
            Assert.That(persistedJobType.BaseType, Is.EqualTo(dataType));

            object job = Activator.CreateInstance(persistedJobType, nonPublic: true);
            persistedJobType.GetField("mode").SetValue(job, 3);
            persistedJobType.GetField("scenePath").SetValue(job, "Assets/Scene.unity");
            persistedJobType.GetField("manualTargetNameToken").SetValue(job, "Target");

            string json = JsonUtility.ToJson(job);

            Assert.That(json, Does.Contain("\"mode\":3"));
            Assert.That(json, Does.Contain("\"scenePath\":\"Assets/Scene.unity\""));
            Assert.That(json, Does.Contain("\"manualTargetNameToken\":\"Target\""));
        }
    }
}
