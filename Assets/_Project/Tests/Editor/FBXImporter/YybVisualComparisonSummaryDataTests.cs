using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonSummaryDataTests
    {
        [Test]
        public void Given_RunnerSummary_When_Serializing_Then_PreservesGenericAndYybFields()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonSummaryData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "YYB 비교 요약 데이터 경계가 필요합니다.");

            Type runnerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner",
                throwOnError: true);
            Type summaryType = runnerType.GetNestedType(
                "SummaryContainer",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(summaryType, Is.Not.Null);
            Assert.That(summaryType.BaseType, Is.EqualTo(dataType));

            object summary = Activator.CreateInstance(summaryType, nonPublic: true);
            summaryType.GetField("session_id").SetValue(summary, "session-01");
            summaryType.GetField("yyb_arm_swing_limit_enabled").SetValue(summary, true);
            summaryType.GetField("target_frame_count").SetValue(summary, 120);

            string json = JsonUtility.ToJson(summary);

            Assert.That(json, Does.Contain("\"session_id\":\"session-01\""));
            Assert.That(json, Does.Contain("\"yyb_arm_swing_limit_enabled\":true"));
            Assert.That(json, Does.Contain("\"target_frame_count\":120"));
        }
    }
}
