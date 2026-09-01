using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonReferenceAlignmentPolicyTests
    {
        [Test]
        public void Given_AlignedReferenceAndPoseOnlyResidual_When_Applying_Then_KeepsResidualAsDiagnostic()
        {
            MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
            {
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded",
            };

            InvokeApply(new[] { summary }, true);

            Assert.That(summary.status, Is.EqualTo("pass"));
            Assert.That(summary.status_reason, Does.Contain("diagnostic="));
        }

        [Test]
        public void Given_YybDeformationRisk_When_Applying_Then_DoesNotDowngradeFailure()
        {
            MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
            {
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded; YYB deformation risk",
            };

            InvokeApply(new[] { summary }, true);

            Assert.That(summary.status, Is.EqualTo("fail"));
        }

        [Test]
        public void Given_AlignedReferenceAndPoseWarning_When_Applying_Then_KeepsResidualAsDiagnostic()
        {
            MotionComparisonFrameQualitySummary summary = new MotionComparisonFrameQualitySummary
            {
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded; same-frame foot bottom Y delta warning threshold exceeded",
            };

            InvokeApply(new[] { summary }, true);

            Assert.That(summary.status, Is.EqualTo("pass"));
            Assert.That(summary.status_reason, Does.Contain("diagnostic="));
        }

        [Test]
        public void Given_ReplayRawVerticalResidualHasReferenceAlignedCorrectedCandidate_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly()
        {
            MotionComparisonFrameQualitySummary raw = CreateRawVerticalResidual(
                "Main_Recording VMD replay probe",
                "same-frame limb pose delta threshold exceeded; same-frame foot bottom Y delta fail threshold exceeded");
            MotionComparisonFrameQualitySummary corrected = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Recording VMD replay probe corrected_vertical_solve_candidate",
                frame_quality_evaluation_role = "corrected_candidate_metrics",
                status = "fail",
                status_reason = "same-frame limb pose delta threshold exceeded",
            };

            InvokeApply(new[] { raw, corrected }, true);

            Assert.That(raw.status, Is.EqualTo("pass"));
            Assert.That(raw.status_reason, Does.Contain("diagnostic="));
            Assert.That(corrected.status, Is.EqualTo("pass"));
        }

        [Test]
        public void Given_RawVerticalResidualHasReferenceAlignedCorrectedPass_When_BuildingCompletionFailures_Then_KeepsRawDiagnosticOnly()
        {
            MotionComparisonFrameQualitySummary raw = CreateRawVerticalResidual(
                "Main_Auto automatic path",
                "same-frame hips Y delta warning threshold exceeded; same-frame foot bottom Y delta fail threshold exceeded");
            MotionComparisonFrameQualitySummary corrected = new MotionComparisonFrameQualitySummary
            {
                candidate_label = "Main_Auto automatic path corrected_vertical_solve_candidate",
                frame_quality_evaluation_role = "corrected_candidate_metrics",
                status = "pass",
                status_reason = "corrected candidate metrics artifact stayed within thresholds",
            };

            InvokeApply(new[] { raw, corrected }, true);

            Assert.That(raw.status, Is.EqualTo("pass"));
            Assert.That(raw.status_reason, Does.Contain("diagnostic="));
            Assert.That(corrected.status, Is.EqualTo("pass"));
        }

        private static MotionComparisonFrameQualitySummary CreateRawVerticalResidual(
            string candidateLabel,
            string statusReason)
        {
            return new MotionComparisonFrameQualitySummary
            {
                candidate_label = candidateLabel,
                frame_quality_evaluation_role = "evaluation_candidate_metrics",
                status = "fail",
                status_reason = statusReason,
            };
        }

        private static void InvokeApply(
            MotionComparisonFrameQualitySummary[] summaries,
            bool hasReferenceAlignedEvidence)
        {
            Type policyType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonReferenceAlignmentPolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null, "YYB 전용 참조 정렬 판정 정책 경계가 필요합니다.");

            MethodInfo method = policyType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { summaries, hasReferenceAlignedEvidence });
        }
    }
}
