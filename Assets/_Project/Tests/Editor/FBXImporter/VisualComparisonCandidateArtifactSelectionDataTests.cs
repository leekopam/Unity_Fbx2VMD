using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCandidateArtifactSelectionDataTests
    {
        [Test]
        public void Given_RunnerSelection_When_Serializing_Then_PreservesArtifactContract()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCandidateArtifactSelectionData",
                throwOnError: false);
            Assert.That(dataType, Is.Not.Null, "모델 중립 후보 산출물 선택 데이터 경계가 필요합니다.");

            object selection = Activator.CreateInstance(dataType, nonPublic: true);
            dataType.GetField("selected_candidate_role").SetValue(selection, "evaluation_candidate_metrics");
            dataType.GetField("selected_candidate_is_acceptance_artifact").SetValue(selection, true);

            string json = JsonUtility.ToJson(selection);

            Assert.That(json, Does.Contain("\"selected_candidate_role\":\"evaluation_candidate_metrics\""));
            Assert.That(json, Does.Contain("\"selected_candidate_is_acceptance_artifact\":true"));
        }
    }
}
