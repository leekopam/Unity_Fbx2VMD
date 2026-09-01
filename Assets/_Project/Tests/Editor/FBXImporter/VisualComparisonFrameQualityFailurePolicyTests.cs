using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonFrameQualityFailurePolicyTests
    {
        [Test]
        public void Given_FrameQualitySummaryFails_When_BuildingCompletionFailures_Then_PromotesToRunFailure()
        {
            var mainAuto = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Auto automatic path",
                frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics",
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded",
                candidate_metrics_csv = "main-auto.csv",
                candidate_vmd_path = "main-auto.vmd",
            };

            string[] failures = InvokeBuild(new[] { mainAuto }, false);

            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures[0], Does.Contain("frame-quality gate failed"));
            Assert.That(failures[0], Does.Contain("Main_Auto automatic path"));
            Assert.That(failures[0], Does.Contain("same-frame limb pose delta"));
        }

        [Test]
        public void Given_AcceptedArtifactPreservingRawDiagnostic_When_RawCandidateFails_Then_DoesNotPromoteFailure()
        {
            MotionComparisonFrameQualitySummary raw = CreateFailedSummary(
                "raw",
                "raw_candidate_metrics");

            string[] failures = InvokeBuild(new[] { raw }, true);

            Assert.That(failures, Is.Empty);
        }

        [Test]
        public void Given_CorrectedCandidateFailure_When_BuildingMessages_Then_ReportsCandidateEvidence()
        {
            MotionComparisonFrameQualitySummary corrected = CreateFailedSummary(
                "corrected",
                "corrected_candidate_metrics");

            string[] failures = InvokeBuild(new[] { corrected }, true);

            Assert.That(failures, Has.Length.EqualTo(1));
            Assert.That(failures[0], Does.Contain("candidate=corrected"));
            Assert.That(failures[0], Does.Contain("reason=pose delta"));
        }

        private static MotionComparisonFrameQualitySummary CreateFailedSummary(string label, string role)
        {
            return new MotionComparisonFrameQualitySummary
            {
                candidate_label = label,
                frame_quality_evaluation_role = role,
                status = "fail",
                status_reason = "pose delta",
                candidate_metrics_csv = "metrics.csv",
                candidate_vmd_path = "candidate.vmd",
            };
        }

        private static string[] InvokeBuild(
            MotionComparisonFrameQualitySummary[] summaries,
            bool acceptedArtifactPreservesRawDiagnostic)
        {
            Type policyType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonFrameQualityFailurePolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null, "모델 중립 frame-quality 실패 정책 경계가 필요합니다.");

            MethodInfo method = policyType.GetMethod(
                "BuildFailureMessages",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string[])method.Invoke(
                null,
                new object[] { summaries, acceptedArtifactPreservesRawDiagnostic });
        }
    }
}
