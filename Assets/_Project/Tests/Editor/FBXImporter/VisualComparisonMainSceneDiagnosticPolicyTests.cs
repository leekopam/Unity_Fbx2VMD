using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public sealed class VisualComparisonMainSceneDiagnosticPolicyTests
    {
        [Test]
        public void Given_CaptureModes_When_CheckingSummaryCandidateMode_Then_IncludesBothMainScenes()
        {
            Assert.That(InvokePolicy<bool>("IsCandidateMode", "MainAuto"), Is.True);
            Assert.That(InvokePolicy<bool>("IsCandidateMode", "MainRecording"), Is.True);
            Assert.That(InvokePolicy<bool>("IsCandidateMode", "MainRecordingVmdPlaybackProbe"), Is.True);
            Assert.That(InvokePolicy<bool>("IsCandidateMode", "SubManualTestPrefab"), Is.False);
            Assert.That(InvokePolicy<bool>("IsCandidateMode", "SubManualYyb"), Is.False);
        }

        [Test]
        public void Given_MainSceneCandidateModes_When_ResolvingIntegratedVerticalSolveRole_Then_ReplayAndMainAutoUseSeparateRoles()
        {
            Assert.That(
                InvokePolicy<string>("ResolveIntegratedVerticalSolveRole", "MainAuto"),
                Is.EqualTo("main_auto_integrated_vertical_solve_metrics"));
            Assert.That(
                InvokePolicy<string>("ResolveIntegratedVerticalSolveRole", "MainRecordingVmdPlaybackProbe"),
                Is.EqualTo("vmd_replay_integrated_vertical_solve_metrics"));
            Assert.That(InvokePolicy<string>("ResolveIntegratedVerticalSolveRole", "MainRecording"), Is.Empty);
            Assert.That(InvokePolicy<string>("ResolveIntegratedVerticalSolveRole", "SubManualTestPrefab"), Is.Empty);
        }

        [Test]
        public void Given_MainSceneCandidateFailedButHasMetricsAndVmd_When_CheckingFrameQualityEligibility_Then_KeepsDiagnosticCandidate()
        {
            Assert.That(
                InvokePolicy<bool>("ShouldBuildFrameQualityDiagnostic", false, "failed.csv", "failed.vmd"),
                Is.True);
            Assert.That(
                InvokePolicy<bool>("ShouldBuildFrameQualityDiagnostic", false, "failed.csv", ""),
                Is.False);
            Assert.That(
                InvokePolicy<bool>("ShouldBuildFrameQualityDiagnostic", true, "", ""),
                Is.True);
        }

        [Test]
        public void Given_IntegratedVerticalSolveModes_When_ResolvingBasis_Then_UsesModeSpecificDescriptions()
        {
            Assert.That(
                InvokePolicy<string>("ResolveIntegratedVerticalSolveBasis", "MainRecordingVmdPlaybackProbe"),
                Is.EqualTo("primary VMD replay diagnostic output after bounded vertical solve promotion; raw replay metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts"));
            Assert.That(
                InvokePolicy<string>("ResolveIntegratedVerticalSolveBasis", "MainAuto"),
                Is.EqualTo("primary Main_Auto result paths after bounded vertical solve promotion; raw metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts"));
        }

        private static T InvokePolicy<T>(string methodName, params object[] arguments)
        {
            Type policyType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonMainSceneDiagnosticPolicy",
                throwOnError: false);
            Assert.That(policyType, Is.Not.Null);

            MethodInfo method = policyType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return (T)method.Invoke(null, arguments);
        }
    }
}
