using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonRunResumePlannerTests
    {
        [TestCase(true, false, false, false, false, false, "QueueAdvanceAfterPlayStop")]
        [TestCase(false, true, true, false, false, false, "QueuePlayModeEntry")]
        [TestCase(false, true, false, false, false, false, "RecoverMissingActiveJob")]
        [TestCase(false, false, true, true, false, false, "DeferActiveJobStartInPlayMode")]
        [TestCase(false, false, true, false, true, false, "DeferNextJob")]
        [TestCase(false, false, true, false, false, false, "DeferActiveJobEntry")]
        [TestCase(false, false, false, false, false, true, "StartNextJob")]
        [TestCase(false, false, false, false, false, false, "FinalizeRun")]
        public void Given_RestoredRunState_When_ResolvingResumeAction_Then_PreservesExistingPriority(
            bool isAdvanceAfterPlayStopPending,
            bool isPlayModeEntryPending,
            bool hasActiveJob,
            bool isPlaying,
            bool isActiveJobFinished,
            bool hasPendingJobs,
            string expectedAction)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type plannerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonRunResumePlanner",
                throwOnError: false);

            Assert.That(plannerType, Is.Not.Null, "범용 시각 비교 실행 복원 계획기가 필요합니다.");

            MethodInfo resolveMethod = plannerType.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null);

            object action = resolveMethod.Invoke(
                null,
                new object[]
                {
                    isAdvanceAfterPlayStopPending,
                    isPlayModeEntryPending,
                    hasActiveJob,
                    isPlaying,
                    isActiveJobFinished,
                    hasPendingJobs
                });

            Assert.That(action.ToString(), Is.EqualTo(expectedAction));
        }
    }
}
