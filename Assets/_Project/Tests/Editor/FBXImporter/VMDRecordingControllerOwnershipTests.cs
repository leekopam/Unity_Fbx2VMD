using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class VMDRecordingControllerOwnershipTests
    {
        [Test]
        public void Given_RecordingController_When_CheckingPipelineComposition_Then_KeepsSingleControllerField()
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                "_recordingController",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(VMDRecordingController)));
        }

        [Test]
        public void Given_StableRecordingFlow_When_InspectingPipelineSource_Then_DelegatesToController()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string source = File.ReadAllText(sourcePath);
            MethodInfo stableSequenceMethod = typeof(FBXVmdPipeline).GetMethod(
                "StartRecordingSequenceStable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo legacySequenceMethod = typeof(FBXVmdPipeline).GetMethod(
                "StartRecordingSequence",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo prewarmMethod = typeof(FBXVmdPipeline).GetMethod(
                "PrewarmRetargetStartPose",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(Regex.Matches(source, @"new VMDRecordingController\(").Count, Is.EqualTo(1));
            Assert.That(source, Does.Contain("StartCoroutine(_recordingController.RecordAsync("));
            Assert.That(stableSequenceMethod, Is.Null);
            Assert.That(legacySequenceMethod, Is.Null);
            Assert.That(prewarmMethod, Is.Null);
        }

        [Test]
        public void Given_RecordingCompletion_When_CheckingOwnership_Then_ControllerOwnsSubscriptionLifecycle()
        {
            FieldInfo controllerRecorderField = typeof(VMDRecordingController).GetField(
                "_activeRecorderController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo controllerCompletionMethod = typeof(VMDRecordingController).GetMethod(
                "OnRecordingFinished",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo controllerClearMethod = typeof(VMDRecordingController).GetMethod(
                "ClearActiveRecordingSubscription",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo pipelineRecorderField = typeof(FBXVmdPipeline).GetField(
                "_activeRecorderController",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineCompletionMethod = typeof(FBXVmdPipeline).GetMethod(
                "OnRecordingFinished",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineClearMethod = typeof(FBXVmdPipeline).GetMethod(
                "ClearActiveRecordingSubscription",
                BindingFlags.Instance | BindingFlags.NonPublic);

            string controllerSourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "VMDRecordingController.cs");
            string controllerSource = File.ReadAllText(controllerSourcePath);
            string pipelineSourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string pipelineSource = File.ReadAllText(pipelineSourcePath);

            Assert.That(controllerRecorderField, Is.Not.Null);
            Assert.That(controllerCompletionMethod, Is.Not.Null);
            Assert.That(controllerClearMethod, Is.Not.Null);
            Assert.That(controllerSource, Does.Contain("RecordingFinished += OnRecordingFinished"));
            Assert.That(controllerSource, Does.Contain("RecordingFinished -= OnRecordingFinished"));
            Assert.That(
                Regex.IsMatch(
                    pipelineSource,
                    @"private void OnDestroy\(\)\s*\{\s*_recordingController\?\.ClearActiveRecordingSubscription\(\);"),
                Is.True);
            Assert.That(pipelineRecorderField, Is.Null);
            Assert.That(pipelineCompletionMethod, Is.Null);
            Assert.That(pipelineClearMethod, Is.Null);
        }

        [Test]
        public void Given_RecordingBoundaryValues_When_ResolvingControllerPolicies_Then_ClampsSafely()
        {
            Assert.That(VMDRecordingController.ResolveStartDelay(1f, false), Is.EqualTo(0f));
            Assert.That(VMDRecordingController.ResolveStartDelay(float.NaN, true), Is.EqualTo(0f));
            Assert.That(VMDRecordingController.ResolveStartDelay(20f, true), Is.EqualTo(10f));
            Assert.That(VMDRecordingController.ResolvePrewarmFrameCount(-1), Is.EqualTo(0));
            Assert.That(VMDRecordingController.ResolvePrewarmFrameCount(121), Is.EqualTo(120));
        }
    }
}
