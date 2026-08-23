using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
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
        public void Given_VmdArtifactPostProcessing_When_CheckingOwnership_Then_ControllerOwnsFileIo()
        {
            MethodInfo controllerManifestMethod = typeof(VMDRecordingController).GetMethod(
                "TryAppendVmdArtifactToComparisonSessionManifest",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo controllerCopyMethod = typeof(VMDRecordingController).GetMethod(
                "TryCopyVmdToAdditionalFolder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo controllerPathMethod = typeof(VMDRecordingController).GetMethod(
                "MakeProjectRelativePath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string) },
                modifiers: null);
            MethodInfo pipelineManifestMethod = typeof(FBXVmdPipeline).GetMethod(
                "TryAppendVmdArtifactToComparisonSessionManifest",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo pipelineCopyMethod = typeof(FBXVmdPipeline).GetMethod(
                "TryCopyVmdToAdditionalFolder",
                BindingFlags.Instance | BindingFlags.NonPublic);

            string controllerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "VMDRecordingController.cs"));
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));

            Assert.That(controllerManifestMethod, Is.Not.Null);
            Assert.That(controllerCopyMethod, Is.Not.Null);
            Assert.That(controllerPathMethod, Is.Not.Null);
            Assert.That(controllerSource, Does.Contain("MakeProjectRelativePath(result.FilePath)"));
            Assert.That(
                controllerSource,
                Does.Contain("MotionComparisonProbeSessionManifestPatcher.TryAppendExportedVmdToSessionManifest("));
            Assert.That(controllerSource, Does.Contain("overwrite: true"));
            Assert.That(pipelineManifestMethod, Is.Null);
            Assert.That(pipelineCopyMethod, Is.Null);
            Assert.That(pipelineSource, Does.Not.Contain("MakeProjectRelativePath("));
        }

        [Test]
        public void Given_RecordingCaptureResolution_When_CheckingOwnership_Then_ControllerUsesPurePlan()
        {
            MethodInfo pipelinePlanMethod = typeof(FBXVmdPipeline).GetMethod(
                "CreateRecordingCaptureResolutionPlan",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string controllerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "VMDRecordingController.cs"));

            Assert.That(pipelinePlanMethod, Is.Null);
            Assert.That(controllerSource, Does.Contain("RecordingCaptureResolution.CreatePlan("));
            Assert.That(controllerSource, Does.Not.Contain("_pipeline.CreateRecordingCaptureResolutionPlan()"));
        }

        [Test]
        public void Given_RecordingPlaybackTiming_When_CheckingOwnership_Then_ControllerOwnsPureCalculations()
        {
            Type[] parameterTypes = { typeof(float), typeof(float) };
            MethodInfo controllerPlaybackSpeedMethod = typeof(VMDRecordingController).GetMethod(
                "ResolveVmdRecordingPlaybackSpeed",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(float) },
                modifiers: null);
            MethodInfo controllerRecordingLengthMethod = typeof(VMDRecordingController).GetMethod(
                "ResolveRecordingLengthForPlaybackSpeed",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            MethodInfo pipelinePlaybackSpeedMethod = typeof(FBXVmdPipeline).GetMethod(
                "ResolveVmdRecordingPlaybackSpeed",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo pipelineRecordingLengthMethod = typeof(FBXVmdPipeline).GetMethod(
                "ResolveRecordingLengthForPlaybackSpeed",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(controllerPlaybackSpeedMethod, Is.Not.Null);
            Assert.That(controllerRecordingLengthMethod, Is.Not.Null);
            Assert.That(pipelinePlaybackSpeedMethod, Is.Null);
            Assert.That(pipelineRecordingLengthMethod, Is.Null);
        }

        [Test]
        public void Given_RecordingModePolicies_When_CheckingOwnership_Then_ControllerOwnsPureCalculations()
        {
            Type[] recordingModeParameterTypes = { typeof(bool), typeof(bool) };
            Type[] prewarmModeParameterTypes = { typeof(int), typeof(bool) };
            MethodInfo controllerRecordingDecisionMethod = typeof(VMDRecordingController).GetMethod(
                "ShouldStartVmdRecording",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: recordingModeParameterTypes,
                modifiers: null);
            MethodInfo controllerPrewarmMethod = typeof(VMDRecordingController).GetMethod(
                "ResolvePrewarmFrameCountForRecordingMode",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: prewarmModeParameterTypes,
                modifiers: null);
            MethodInfo controllerVisiblePrewarmMethod = typeof(VMDRecordingController).GetMethod(
                "ResolveVisiblePrewarmYieldFrameCountForRecordingMode",
                BindingFlags.Static | BindingFlags.Public,
                binder: null,
                types: prewarmModeParameterTypes,
                modifiers: null);
            MethodInfo pipelineRecordingDecisionMethod = typeof(FBXVmdPipeline).GetMethod(
                "ShouldStartVmdRecordingAfterImport",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo pipelinePrewarmMethod = typeof(FBXVmdPipeline).GetMethod(
                "ResolveRetargetPrewarmFrameCountForRecordingMode",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo pipelineVisiblePrewarmMethod = typeof(FBXVmdPipeline).GetMethod(
                "ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(controllerRecordingDecisionMethod, Is.Not.Null);
            Assert.That(controllerPrewarmMethod, Is.Not.Null);
            Assert.That(controllerVisiblePrewarmMethod, Is.Not.Null);
            Assert.That(pipelineRecordingDecisionMethod, Is.Null);
            Assert.That(pipelinePrewarmMethod, Is.Null);
            Assert.That(pipelineVisiblePrewarmMethod, Is.Null);
        }

        [Test]
        public void Given_RecordingBoundaryValues_When_ResolvingControllerPolicies_Then_ClampsSafely()
        {
            Assert.That(VMDRecordingController.ResolveStartDelay(1f, false), Is.EqualTo(0f));
            Assert.That(VMDRecordingController.ResolveStartDelay(float.NaN, true), Is.EqualTo(0f));
            Assert.That(VMDRecordingController.ResolveStartDelay(20f, true), Is.EqualTo(10f));
            Assert.That(VMDRecordingController.ResolveVmdRecordingPlaybackSpeed(float.NaN), Is.EqualTo(1f));
            Assert.That(VMDRecordingController.ResolveVmdRecordingPlaybackSpeed(-1f), Is.EqualTo(1f));
            Assert.That(VMDRecordingController.ResolveVmdRecordingPlaybackSpeed(0.00001f), Is.EqualTo(0.0001f));
            Assert.That(VMDRecordingController.ResolveRecordingLengthForPlaybackSpeed(float.NaN, 1f), Is.EqualTo(0f));
            Assert.That(VMDRecordingController.ResolveRecordingLengthForPlaybackSpeed(10f, 2f), Is.EqualTo(5f));
            Assert.That(VMDRecordingController.ResolvePrewarmFrameCount(-1), Is.EqualTo(0));
            Assert.That(VMDRecordingController.ResolvePrewarmFrameCount(121), Is.EqualTo(120));
        }
    }
}
