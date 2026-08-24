using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonCaptureJobPlannerTests
    {
        [Test]
        public void Given_AlternateModelProfile_When_BuildingJobs_Then_UsesProfileValuesWithoutYybNames()
        {
            object profile = CreateProfile(
                modelDisplayName: "대체 캐릭터",
                manualReferenceDisplayName: "기준 캐릭터",
                manualReferenceTargetNameToken: "ReferenceAvatar",
                manualTargetNameToken: "AlternativeAvatar");

            object[] jobs = BuildJobs(profile, includePlaybackProbe: false);

            Assert.That(jobs, Has.Length.EqualTo(4));
            AssertJob(
                jobs[0],
                "ManualReference",
                "Assets/Scenes/Manual.unity",
                "Manual",
                "Manual 기준 캐릭터 수동 기준",
                "ReferenceAvatar");
            AssertJob(
                jobs[1],
                "ManualTarget",
                "Assets/Scenes/Manual.unity",
                "Manual",
                "Manual 대체 캐릭터 수동 기준",
                "AlternativeAvatar");
            AssertJob(
                jobs[2],
                "DirectRecording",
                "Assets/Scenes/Recording.unity",
                "Recording",
                "Recording 대체 캐릭터 자동 경로",
                string.Empty);
            AssertJob(
                jobs[3],
                "Automatic",
                "Assets/Scenes/Automatic.unity",
                "Automatic",
                "Automatic 대체 캐릭터 자동 경로",
                string.Empty);
        }

        [Test]
        public void Given_PlaybackProbeEnabled_When_BuildingJobs_Then_InsertsProbeAfterDirectRecording()
        {
            object profile = CreateProfile(
                modelDisplayName: "대체 캐릭터",
                manualReferenceDisplayName: "기준 캐릭터",
                manualReferenceTargetNameToken: "ReferenceAvatar",
                manualTargetNameToken: "AlternativeAvatar");

            object[] jobs = BuildJobs(profile, includePlaybackProbe: true);

            Assert.That(jobs, Has.Length.EqualTo(5));
            Assert.That(ReadProperty(jobs[2], "Role").ToString(), Is.EqualTo("DirectRecording"));
            AssertJob(
                jobs[3],
                "PlaybackProbe",
                "Assets/Scenes/Recording.unity",
                "Recording",
                "Recording 대체 캐릭터 VMD replay probe",
                string.Empty);
            Assert.That(ReadProperty(jobs[4], "Role").ToString(), Is.EqualTo("Automatic"));
        }

        private static object CreateProfile(
            string modelDisplayName,
            string manualReferenceDisplayName,
            string manualReferenceTargetNameToken,
            string manualTargetNameToken)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type sceneType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonScene",
                throwOnError: false);
            Type profileType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCaptureProfile",
                throwOnError: false);

            Assert.That(sceneType, Is.Not.Null, "범용 시각 비교 씬 타입이 필요합니다.");
            Assert.That(profileType, Is.Not.Null, "범용 시각 비교 캡처 프로필 타입이 필요합니다.");

            object manualScene = Activator.CreateInstance(
                sceneType,
                "Assets/Scenes/Manual.unity",
                "Manual");
            object recordingScene = Activator.CreateInstance(
                sceneType,
                "Assets/Scenes/Recording.unity",
                "Recording");
            object automaticScene = Activator.CreateInstance(
                sceneType,
                "Assets/Scenes/Automatic.unity",
                "Automatic");

            return Activator.CreateInstance(
                profileType,
                modelDisplayName,
                manualReferenceDisplayName,
                manualReferenceTargetNameToken,
                manualTargetNameToken,
                manualScene,
                recordingScene,
                automaticScene);
        }

        private static object[] BuildJobs(object profile, bool includePlaybackProbe)
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type plannerType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonCaptureJobPlanner",
                throwOnError: false);

            Assert.That(plannerType, Is.Not.Null, "범용 시각 비교 캡처 작업 계획기가 필요합니다.");

            MethodInfo buildMethod = plannerType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(buildMethod, Is.Not.Null);

            var jobs = (IEnumerable)buildMethod.Invoke(
                null,
                new[] { profile, (object)includePlaybackProbe });
            return jobs.Cast<object>().ToArray();
        }

        private static void AssertJob(
            object job,
            string expectedRole,
            string expectedScenePath,
            string expectedSceneName,
            string expectedDisplayName,
            string expectedTargetNameToken)
        {
            Assert.That(ReadProperty(job, "Role").ToString(), Is.EqualTo(expectedRole));
            Assert.That(ReadProperty(job, "ScenePath"), Is.EqualTo(expectedScenePath));
            Assert.That(ReadProperty(job, "SceneName"), Is.EqualTo(expectedSceneName));
            Assert.That(ReadProperty(job, "DisplayName"), Is.EqualTo(expectedDisplayName));
            Assert.That(ReadProperty(job, "TargetNameToken"), Is.EqualTo(expectedTargetNameToken));
        }

        private static object ReadProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return property.GetValue(target);
        }
    }
}
