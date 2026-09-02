using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public sealed class VisualComparisonCaptureJobAdvancePolicyTests
    {
        [Test]
        public void Given_ActiveCaptureJobIsUnfinished_When_CheckingStartNextJobGate_Then_IgnoresDuplicateAdvance()
        {
            Assert.That(CanStartNextJob(isRunning: true, hasActiveJob: true, activeJobFinished: false), Is.False);
            Assert.That(CanStartNextJob(isRunning: true, hasActiveJob: true, activeJobFinished: true), Is.True);
            Assert.That(CanStartNextJob(isRunning: true, hasActiveJob: false, activeJobFinished: false), Is.True);
            Assert.That(CanStartNextJob(isRunning: false, hasActiveJob: false, activeJobFinished: false), Is.False);
        }

        private static bool CanStartNextJob(
            bool isRunning,
            bool hasActiveJob,
            bool activeJobFinished)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type policyType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCaptureJobAdvancePolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null, "범용 시각 비교 캡처 작업 진행 정책이 필요합니다.");

            MethodInfo method = policyType.GetMethod(
                "CanStartNextJob",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            return (bool)method.Invoke(
                null,
                new object[] { isRunning, hasActiveJob, activeJobFinished });
        }
    }
}
