using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRunStateDataTests
    {
        [Test]
        public void Given_PersistedState_When_RoundTrippingJson_Then_PreservesJobAndYybSettings()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type stateDataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunStateData",
                throwOnError: false);
            Assert.That(stateDataType, Is.Not.Null, "YYB 비교 실행 상태 데이터 경계가 필요합니다.");

            Type jobDataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCaptureJobStateData",
                throwOnError: true);
            Type resultDataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonCaptureResultData",
                throwOnError: true);
            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type persistedStateType = runnerType.GetNestedType(
                "PersistedState",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(persistedStateType, Is.Not.Null);
            Assert.That(persistedStateType.BaseType, Is.EqualTo(stateDataType));

            object state = Activator.CreateInstance(persistedStateType, nonPublic: true);
            object job = Activator.CreateInstance(jobDataType, nonPublic: true);
            object result = Activator.CreateInstance(resultDataType, nonPublic: true);
            jobDataType.GetField("scenePath").SetValue(job, "Assets/Main.unity");
            resultDataType.GetField("jobMode").SetValue(result, "MainAuto");

            stateDataType.GetField("fbxFileName").SetValue(state, "motion.fbx");
            stateDataType.GetField("enableYybArmSwingLimitRuntimeOverride").SetValue(state, true);
            stateDataType.GetField("activeJob").SetValue(state, job);
            stateDataType.GetField("results").SetValue(state, CreateArray(resultDataType, result));

            string json = JsonUtility.ToJson(state);
            object restored = JsonUtility.FromJson(json, persistedStateType);

            Assert.That(stateDataType.GetField("fbxFileName").GetValue(restored), Is.EqualTo("motion.fbx"));
            Assert.That(stateDataType.GetField("enableYybArmSwingLimitRuntimeOverride").GetValue(restored), Is.True);
            Assert.That(stateDataType.GetField("activeJob").GetValue(restored), Is.Not.Null);
            Array restoredResults = (Array)stateDataType.GetField("results").GetValue(restored);
            Assert.That(restoredResults, Has.Length.EqualTo(1));
            Assert.That(resultDataType.GetField("jobMode").GetValue(restoredResults.GetValue(0)), Is.EqualTo("MainAuto"));
        }

        private static Array CreateArray(Type elementType, object value)
        {
            Array array = Array.CreateInstance(elementType, 1);
            array.SetValue(value, 0);
            return array;
        }
    }
}
