using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonPlayModeTransitionPlannerTests
    {
        [TestCase("EnteredPlayMode", false, true, false, false, "Ignore")]
        [TestCase("EnteredPlayMode", true, true, false, false, "StartActiveJob")]
        [TestCase("EnteredEditMode", true, false, false, false, "CleanupOnly")]
        [TestCase("EnteredEditMode", true, true, false, true, "QueueAdvanceAfterPlayStop")]
        [TestCase("EnteredEditMode", true, true, false, false, "QueuePlayModeEntry")]
        [TestCase("ExitingPlayMode", true, true, false, false, "ReportPrematureExit")]
        [TestCase("ExitingPlayMode", true, true, true, false, "ObservePlayModeExit")]
        [TestCase("Other", true, true, false, false, "Ignore")]
        public void Given_PlayModeState_When_ResolvingTransition_Then_PreservesRunnerBehavior(
            string phaseName,
            bool isRunActive,
            bool hasActiveJob,
            bool isActiveJobFinished,
            bool isAdvanceAfterPlayStopPending,
            string expectedAction)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type phaseType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonPlayModePhase",
                throwOnError: false);
            Type plannerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonPlayModeTransitionPlanner",
                throwOnError: false);

            Assert.That(phaseType, Is.Not.Null, "범용 PlayMode 단계 타입이 필요합니다.");
            Assert.That(plannerType, Is.Not.Null, "범용 PlayMode 전환 계획기가 필요합니다.");

            MethodInfo resolveMethod = plannerType.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null);

            object phase = Enum.Parse(phaseType, phaseName);
            object action = resolveMethod.Invoke(
                null,
                new[]
                {
                    phase,
                    (object)isRunActive,
                    hasActiveJob,
                    isActiveJobFinished,
                    isAdvanceAfterPlayStopPending
                });

            Assert.That(action.ToString(), Is.EqualTo(expectedAction));
        }
    }
}
