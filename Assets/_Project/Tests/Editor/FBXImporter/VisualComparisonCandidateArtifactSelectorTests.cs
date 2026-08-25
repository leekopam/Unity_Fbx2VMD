using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCandidateArtifactSelectorTests
    {
        [Test]
        public void Given_PassingRawCandidate_When_Selecting_Then_ReturnsAcceptanceArtifact()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "VisualComparisonCandidateSelector_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string vmdPath = Path.Combine(root, "raw.vmd");
            string metricsPath = Path.Combine(root, "raw.csv");

            try
            {
                File.WriteAllText(vmdPath, "raw-vmd");
                File.WriteAllText(metricsPath, "raw-metrics");
                var summary = new MotionComparisonFrameQualitySummary
                {
                    frame_quality_evaluation_role = "evaluation_candidate_metrics",
                    candidate_label = "Main_Auto raw",
                    status = "pass",
                    candidate_vmd_path = vmdPath,
                    candidate_metrics_csv = metricsPath
                };
                Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
                Type selectorType = runtimeAssembly.GetType(
                    "Fbx2Vmd.FBXImporter.VisualComparisonCandidateArtifactSelector",
                    throwOnError: false);
                Assert.That(selectorType, Is.Not.Null, "모델 중립 후보 산출물 선택 경계가 필요합니다.");

                MethodInfo selectMethod = selectorType.GetMethod(
                    "Select",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(selectMethod, Is.Not.Null);

                object selection = selectMethod.Invoke(
                    null,
                    new object[] { new[] { summary }, root });
                Assert.That(selection, Is.Not.Null);
                Type selectionType = selection.GetType();

                Assert.That(
                    selectionType.GetField("selected_candidate_role").GetValue(selection),
                    Is.EqualTo("evaluation_candidate_metrics"));
                Assert.That(
                    selectionType.GetField("selected_candidate_output_role").GetValue(selection),
                    Is.EqualTo("user_facing_export_artifact"));
                Assert.That(
                    selectionType.GetField("selected_candidate_is_acceptance_artifact").GetValue(selection),
                    Is.True);
                Assert.That(
                    selectionType.GetField("selected_candidate_preserves_raw_diagnostic").GetValue(selection),
                    Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }
}
