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
