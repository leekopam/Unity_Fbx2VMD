using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class FBXVmdPipelineEditorSmokePathTests
    {
        private static readonly Type[] SmokeResolverParameterTypes =
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Func<string, bool>)
        };

        private static readonly Type[] HumanoidReferenceResolverParameterTypes =
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Func<string, bool>)
        };

        private static readonly Type[] ProjectRelativePathParameterTypes =
        {
            typeof(string),
            typeof(string)
        };

        private static readonly Type[] GhostSkeletonFallbackParameterTypes =
        {
            typeof(bool),
            typeof(int)
        };

        private static readonly Type[] EditorSmokeReferenceTimingParameterTypes =
        {
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(int),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(int).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] EditorSmokeReferenceTimingWithOptionParameterTypes =
        {
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(int),
            typeof(float),
            typeof(bool),
            typeof(float).MakeByRefType(),
            typeof(int).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] EditorSmokeReferenceTimingPolicyParameterTypes =
        {
            typeof(string),
            typeof(float),
            typeof(int),
            typeof(float),
            typeof(bool)
        };

        private static readonly Type[] SceneSupportParameterTypes =
        {
            typeof(string)
        };

        private static readonly Type[] CaptureResolutionOverrideParameterTypes =
        {
            typeof(int),
            typeof(int),
            typeof(int).MakeByRefType(),
            typeof(int).MakeByRefType()
        };

        [Test]
        public void Given_ControlledFileExists_When_ResolvingEditorSmokeFbxPath_Then_UsesControlledPath()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(controlledDirectory, "dance.fbx");

            string resolved = Resolve(" dance ", controlledDirectory, dataPath, controlledPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_ControlledMissingAndProjectFbxExists_When_ResolvingEditorSmokeFbxPath_Then_UsesProjectFallback()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string fallbackPath = Path.Combine(dataPath, "_Project", "FBX", "dance.fbx");

            string resolved = Resolve("dance", controlledDirectory, dataPath, fallbackPath);

            Assert.That(resolved, Is.EqualTo(fallbackPath));
        }

        [Test]
        public void Given_NoCandidateExists_When_ResolvingEditorSmokeFbxPath_Then_ReturnsControlledCandidate()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(controlledDirectory, "missing.fbx");

            string resolved = Resolve("missing.fbx", controlledDirectory, dataPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_UppercaseFbxExtension_When_ResolvingEditorSmokeFbxPath_Then_PreservesFileName()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(controlledDirectory, "Dance.FBX");

            string resolved = Resolve("Dance.FBX", controlledDirectory, dataPath, controlledPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_PathLikeInput_When_ResolvingEditorSmokeFbxPath_Then_UsesOnlyFileNameForFallback()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string fallbackPath = Path.Combine(dataPath, "_Project", "FBX", "dance.fbx");

            string resolved = Resolve(@"..\_Project\FBX\dance", controlledDirectory, dataPath, fallbackPath);

            Assert.That(resolved, Is.EqualTo(fallbackPath));
        }

        [Test]
        public void Given_MainSceneName_When_CheckingFbxSmokeSceneSupport_Then_AllowsBothMainScenes()
        {
            Assert.That(IsSupportedMainScene("Main_Auto"), Is.True);
            Assert.That(IsSupportedMainScene("Main_recoding"), Is.True);
            Assert.That(IsSupportedMainScene("Main_Recoding"), Is.True);
            Assert.That(IsSupportedMainScene("Sub_Manual"), Is.False);
        }

        [Test]
        public void Given_ProjectSourceHasHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesSourcePath()
        {
            string sourcePath = "Assets/_Project/FBX/source.fbx";
            string manualPath = "Assets/_Project/FBX/source.fbx";

            string resolved = ResolveHumanoidReference(
                "Assets/Resources/Import_FBX/source.fbx",
                sourcePath,
                sourcePath,
                sourcePath,
                manualPath);

            Assert.That(resolved, Is.EqualTo(sourcePath));
        }

        [Test]
        public void Given_ControlledSourceAndProjectClipExists_When_ResolvingEditorHumanoidReferencePath_Then_UsesProjectFallbackPath()
        {
            string controlledPath = "Assets/Resources/Import_FBX/dance.fbx";
            string projectPath = "Assets/_Project/FBX/dance.fbx";

            string resolved = ResolveHumanoidReference(
                controlledPath,
                controlledPath,
                @"C:\Project\Assets\Resources\Import_FBX\dance.fbx",
                controlledPath,
                projectPath);

            Assert.That(resolved, Is.EqualTo(projectPath));
        }

        [Test]
        public void Given_ControlledSourceOnlyHasClip_When_ResolvingEditorHumanoidReferencePath_Then_FallsBackToControlledSourcePath()
        {
            string controlledPath = "Assets/Resources/Import_FBX/dance.fbx";

            string resolved = ResolveHumanoidReference(
                "Assets/Resources/Import_FBX/dance.fbx",
                controlledPath,
                @"C:\Project\Assets\Resources\Import_FBX\dance.fbx",
                controlledPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_ImportedPathHasOnlyHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesImportedPath()
        {
            string importedPath = "Assets/Resources/Import_FBX/imported.fbx";

            string resolved = ResolveHumanoidReference(
                importedPath,
                "",
                "",
                importedPath);

            Assert.That(resolved, Is.EqualTo(importedPath));
        }

        [Test]
        public void Given_NoHumanoidClipCandidate_When_ResolvingEditorHumanoidReferencePath_Then_ReturnsEmptyPath()
        {
            string resolved = ResolveHumanoidReference(
                "Assets/Resources/Import_FBX/missing.fbx",
                "Assets/Resources/Import_FBX/missing.fbx",
                @"C:\Project\Assets\Resources\Import_FBX\missing.fbx");

            Assert.That(resolved, Is.EqualTo(""));
        }

        [Test]
        public void Given_EditorHumanoidReference_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidReferenceApplier",
                throwOnError: false);
            Type optionsType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorManualPoseReferenceOptions",
                throwOnError: false);

            Assert.That(applierType, Is.Not.Null);
            Assert.That(optionsType, Is.Not.Null);
            Assert.That(
                applierType?.GetMethod(
                    "Apply",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                applierType?.GetMethod(
                    "ApplyManualPoseReference",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "ConfigureEditorHumanoidMuscleReference",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "ResolveEditorHumanoidReferencePath",
                    BindingFlags.Static | BindingFlags.NonPublic,
                    binder: null,
                    types: HumanoidReferenceResolverParameterTypes,
                    modifiers: null),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "LoadEditorHumanoidAnimationClip",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "ConfigureEditorManualFingerPoseReference",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "CreateEditorManualPoseReferenceOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
        }

        [Test]
        public void Given_ManualLowerBodyReference_When_SnapshottingEditorOptions_Then_EnablesManualPoseApplication()
        {
            var pipelineObject = new GameObject("EditorManualPoseOptionsPipeline");
            try
            {
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = true;
                MethodInfo createOptionsMethod = typeof(FBXVmdPipeline).GetMethod(
                    "CreateEditorManualPoseReferenceOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(createOptionsMethod, Is.Not.Null);
                object options = createOptionsMethod.Invoke(pipeline, null);
                PropertyInfo shouldUseLowerBodyProperty = options.GetType().GetProperty(
                    "ShouldUseLowerBodySegmentDirectionReference",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                PropertyInfo shouldApplyProperty = options.GetType().GetProperty(
                    "ShouldApply",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(shouldUseLowerBodyProperty, Is.Not.Null);
                Assert.That(shouldApplyProperty, Is.Not.Null);
                Assert.That(shouldUseLowerBodyProperty.GetValue(options), Is.True);
                Assert.That(shouldApplyProperty.GetValue(options), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_ThumbGuardBinding_When_CheckingOwnership_Then_UsesDedicatedApplier()
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidThumbDeformationGuardApplier",
                throwOnError: false);
            Type optionsType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidThumbDeformationGuardOptions",
                throwOnError: false);

            Assert.That(applierType, Is.Not.Null);
            Assert.That(optionsType, Is.Not.Null);
            Assert.That(
                applierType?.GetMethod(
                    "Apply",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "ConfigureTargetThumbDeformationGuard",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "CreateThumbDeformationGuardOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null);
        }

        [Test]
        public void Given_ControlledSourceAlreadyInImportFolder_When_DecidingImportSettings_Then_PreservesExistingImporter()
        {
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(dataPath, "Resources", "Import_FBX", "dance.fbx");

            bool shouldConfigure = ShouldConfigureImportSettings(controlledPath, controlledPath, dataPath);

            Assert.That(shouldConfigure, Is.False);
        }

        [Test]
        public void Given_ExternalSourceCopiedToControlledImportFolder_When_DecidingImportSettings_Then_ConfiguresCopiedImporter()
        {
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string sourcePath = Path.Combine("D:", "Downloads", "dance.fbx");
            string controlledPath = Path.Combine(dataPath, "Resources", "Import_FBX", "dance.fbx");

            bool shouldConfigure = ShouldConfigureImportSettings(sourcePath, controlledPath, dataPath);

            Assert.That(shouldConfigure, Is.True);
        }

        [Test]
        public void Given_CaptureOnlyModeWithoutEditorSmoke_When_DecidingRecordingMode_Then_SkipsVmdRecording()
        {
            bool shouldRecord = VMDRecordingController.ShouldStartVmdRecording(
                shouldRecordVmdAfterImport: false,
                editorSmokeRecordingOverrideActive: false);

            Assert.That(shouldRecord, Is.False);
        }

        [Test]
        public void Given_CaptureOnlyModeWithEditorSmoke_When_DecidingRecordingMode_Then_AllowsDiagnosticVmdRecording()
        {
            bool shouldRecord = VMDRecordingController.ShouldStartVmdRecording(
                shouldRecordVmdAfterImport: false,
                editorSmokeRecordingOverrideActive: true);

            Assert.That(shouldRecord, Is.True);
        }

        [Test]
        public void Given_VmdMode_When_DecidingRecordingMode_Then_StartsVmdRecording()
        {
            bool shouldRecord = VMDRecordingController.ShouldStartVmdRecording(
                shouldRecordVmdAfterImport: true,
                editorSmokeRecordingOverrideActive: false);

            Assert.That(shouldRecord, Is.True);
        }

        [Test]
        public void Given_PreviewRetargeterIsActive_When_DecidingIdlePoseFrameGuard_Then_SkipsGuard()
        {
            bool shouldApply = ShouldApplyTargetIdlePoseGuardThisFrame(
                isProcessing: false,
                hasActiveRetargeter: true);

            Assert.That(shouldApply, Is.False);
        }

        [Test]
        public void Given_NoProcessingAndNoRetargeter_When_DecidingIdlePoseFrameGuard_Then_AppliesGuard()
        {
            bool shouldApply = ShouldApplyTargetIdlePoseGuardThisFrame(
                isProcessing: false,
                hasActiveRetargeter: false);

            Assert.That(shouldApply, Is.True);
        }

        [Test]
        public void Given_Processing_When_DecidingIdlePoseFrameGuard_Then_SkipsGuard()
        {
            bool shouldApply = ShouldApplyTargetIdlePoseGuardThisFrame(
                isProcessing: true,
                hasActiveRetargeter: false);

            Assert.That(shouldApply, Is.False);
        }

        [Test]
        public void Given_ProjectArtifactPath_When_MakingProjectRelativePath_Then_ReturnsSlashSeparatedRelativePath()
        {
            string projectRoot = @"C:\Project";
            string artifactPath = Path.Combine(projectRoot, "Exports", "dance.vmd");

            string resolved = MakeProjectRelativePath(artifactPath, projectRoot);

            Assert.That(resolved, Is.EqualTo("Exports/dance.vmd"));
        }

        [Test]
        public void Given_OutsidePathWithSharedPrefix_When_MakingProjectRelativePath_Then_ReturnsNormalizedOriginalPath()
        {
            string resolved = MakeProjectRelativePath(
                @"C:\Projector\Exports\dance.vmd",
                @"C:\Project");

            Assert.That(resolved, Is.EqualTo("C:/Projector/Exports/dance.vmd"));
        }

        [Test]
        public void Given_EmptyPath_When_MakingProjectRelativePath_Then_ReturnsEmptyPath()
        {
            string resolved = MakeProjectRelativePath("", @"C:\Project");

            Assert.That(resolved, Is.EqualTo(""));
        }

        [Test]
        public void Given_SatisfactionFullClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference()
        {
            bool hasPlan = VMDRecordingController.TryBuildKnownMmdReferenceRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7667f,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.True);
            Assert.That(targetFrameCount, Is.EqualTo(6001));
            Assert.That(recordingLengthSeconds, Is.EqualTo(6001f / 30f).Within(0.0001f));
            Assert.That(playbackSpeed, Is.EqualTo(207.7667f / (6001f / 30f)).Within(0.0001f));
        }

        [Test]
        public void Given_SatisfactionFullClipAndReferenceTimingDisabled_When_CalculatingReferenceTiming_Then_KeepsNormalPlayback()
        {
            bool hasPlan = VMDRecordingController.TryBuildKnownMmdReferenceRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7667f,
                recordingFrameRate: 30f,
                useKnownReferenceTiming: false,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.False);
            Assert.That(targetFrameCount, Is.EqualTo(0));
            Assert.That(recordingLengthSeconds, Is.EqualTo(207.7667f).Within(0.0001f));
            Assert.That(playbackSpeed, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_CustomVmdPlaybackSpeed_When_ResolvingRecordingLength_Then_PreservesFullClipAtThatSpeed()
        {
            float recordingLengthSeconds = VMDRecordingController.ResolveRecordingLengthForPlaybackSpeed(
                clipLengthSeconds: 207.7667f,
                playbackSpeed: 2f);

            Assert.That(recordingLengthSeconds, Is.EqualTo(103.88335f).Within(0.0001f));
        }

        [Test]
        public void Given_GhostHasNoRenderers_When_ShowGhostModelIsEnabled_Then_UsesSkeletonDebugFallback()
        {
            Assert.That(ShouldAttachGhostSkeletonDebugRenderer(visible: true, rendererCount: 0), Is.True);
            Assert.That(ShouldAttachGhostSkeletonDebugRenderer(visible: false, rendererCount: 0), Is.False);
        }

        [Test]
        public void Given_GhostHasRenderers_When_ShowGhostModelIsEnabled_Then_StillUsesSkeletonDebugVisibilityAid()
        {
            Assert.That(
                ShouldAttachGhostSkeletonDebugRenderer(visible: true, rendererCount: 2),
                Is.True,
                "The visible Ghost container can still be hard to inspect in Scene/Game view, so enabling Ghost display must add a skeleton/root marker visibility aid even when renderers exist.");
        }

        [Test]
        public void Given_GhostPresentation_When_CheckingOwnership_Then_UsesDedicatedPresenter()
        {
            Type presenterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.GhostModelPresenter",
                throwOnError: false);
            MethodInfo createContainerMethod = presenterType?.GetMethod(
                "CreateContainer",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo setVisibilityMethod = presenterType?.GetMethod(
                "SetVisibility",
                BindingFlags.Static | BindingFlags.NonPublic);
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));
            int createIndex = pipelineSource.IndexOf(
                "GhostModelPresenter.CreateContainer(",
                StringComparison.Ordinal);
            int registerIndex = createIndex < 0
                ? -1
                : pipelineSource.IndexOf(
                    "_activeGhostContainer = ghostContainer;",
                    createIndex,
                    StringComparison.Ordinal);
            int visibilityIndex = registerIndex < 0
                ? -1
                : pipelineSource.IndexOf(
                    "GhostModelPresenter.SetVisibility(",
                    registerIndex,
                    StringComparison.Ordinal);

            Assert.That(presenterType, Is.Not.Null);
            Assert.That(createContainerMethod, Is.Not.Null);
            Assert.That(setVisibilityMethod, Is.Not.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "CreateGhostContainer",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "SetGhostVisibility",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(createIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(registerIndex, Is.GreaterThan(createIndex));
            Assert.That(visibilityIndex, Is.GreaterThan(registerIndex));
        }

        [Test]
        public void Given_GhostSkeletonRenderer_When_CheckingFileOwnership_Then_UsesDedicatedSourceFile()
        {
            string importerDirectory = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter");
            string pipelineSource = File.ReadAllText(Path.Combine(
                importerDirectory,
                "FBXVmdPipeline.cs"));
            string rendererPath = Path.Combine(
                importerDirectory,
                "GhostSkeletonDebugRenderer.cs");

            Assert.That(File.Exists(rendererPath), Is.True);
            string rendererSource = File.ReadAllText(rendererPath);
            Assert.That(
                pipelineSource,
                Does.Not.Contain("public sealed class GhostSkeletonDebugRenderer"));
            Assert.That(
                rendererSource,
                Does.Contain("public sealed class GhostSkeletonDebugRenderer"));
        }

        [Test]
        public void Given_EditorSmokePlanning_When_CheckingOwnership_Then_UsesDedicatedPlanner()
        {
            Type plannerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FBXEditorDiagnosticPlanner",
                throwOnError: false);

            Assert.That(plannerType, Is.Not.Null);
            Assert.That(
                plannerType?.GetMethod(
                    "TryBuildCaptureResolutionOverride",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                plannerType?.GetMethod(
                    "ResolveFbxPath",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Not.Null);
            Assert.That(
                Array.Exists(
                    plannerType?.GetMethods(BindingFlags.Static | BindingFlags.NonPublic) ??
                    Array.Empty<MethodInfo>(),
                    method => method.Name == "TryBuildKnownReferenceRecordingPlan"),
                Is.True);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "TryBuildEditorSmokeCaptureResolutionOverride",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "ResolveEditorSmokeFbxPath",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetMethod(
                    "TryBuildKnownMmdReferenceEditorSmokeRecordingPlan",
                    BindingFlags.Static | BindingFlags.NonPublic),
                Is.Null);
        }

        [Test]
        public void Given_EditorSmokeRuntimeState_When_CheckingOwnership_Then_UsesDedicatedSession()
        {
            Type sessionType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FBXEditorDiagnosticSession",
                throwOnError: false);
            FieldInfo sessionField = typeof(FBXVmdPipeline).GetField(
                "_editorDiagnosticSession",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(sessionType, Is.Not.Null);
            Assert.That(sessionField, Is.Not.Null);
            Assert.That(sessionField?.FieldType, Is.EqualTo(sessionType));
            Assert.That(
                typeof(FBXVmdPipeline).GetField(
                    "_editorSmokeRecordingOverrideActive",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);
            Assert.That(
                typeof(FBXVmdPipeline).GetField(
                    "_editorSmokeSettingsSnapshot",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Null);

            string controllerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "VMDRecordingController.cs"));
            Assert.That(controllerSource, Does.Contain("EditorDiagnosticSession"));
            Assert.That(controllerSource, Does.Not.Contain("_editorSmoke"));
        }

        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head, "head", "smoke_clip_2s")]
        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle, "middle", "smoke_middle_clip_2s")]
        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail, "tail", "smoke_tail_clip_2s")]
        public void Given_EditorSmokeSegment_When_BuildingLabels_Then_UsesExistingContract(
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            string expectedLabel,
            string expectedOutputBaseName)
        {
            string label = InvokeEditorDiagnosticPlanner<string>(
                "GetSegmentLabel",
                new[] { typeof(FBXVmdPipeline.EditorDiagnosticSmokeSegment) },
                segment);
            string outputBaseName = InvokeEditorDiagnosticPlanner<string>(
                "BuildOutputBaseName",
                new[]
                {
                    typeof(string),
                    typeof(float),
                    typeof(FBXVmdPipeline.EditorDiagnosticSmokeSegment)
                },
                "clip",
                1.25f,
                segment);

            Assert.That(label, Is.EqualTo(expectedLabel));
            Assert.That(outputBaseName, Is.EqualTo(expectedOutputBaseName));
        }

        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head, 2f, 0f)]
        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle, 2f, 4f)]
        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail, 2f, 8f)]
        [TestCase(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle, 12f, 0f)]
        public void Given_EditorSmokeSegment_When_CalculatingStartTime_Then_ClampsToClip(
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            float durationSeconds,
            float expectedStartTime)
        {
            var clip = new AnimationClip();
            clip.SetCurve(
                string.Empty,
                typeof(Transform),
                "localPosition.x",
                AnimationCurve.Linear(0f, 0f, 10f, 1f));

            try
            {
                float startTime = InvokeEditorDiagnosticPlanner<float>(
                    "CalculateStartTime",
                    new[]
                    {
                        typeof(AnimationClip),
                        typeof(float),
                        typeof(FBXVmdPipeline.EditorDiagnosticSmokeSegment)
                    },
                    clip,
                    durationSeconds,
                    segment);

                Assert.That(startTime, Is.EqualTo(expectedStartTime).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Given_MissingClip_When_CalculatingStartTime_Then_ReturnsZero()
        {
            float startTime = InvokeEditorDiagnosticPlanner<float>(
                "CalculateStartTime",
                new[]
                {
                    typeof(AnimationClip),
                    typeof(float),
                    typeof(FBXVmdPipeline.EditorDiagnosticSmokeSegment)
                },
                null,
                2f,
                FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle);

            Assert.That(startTime, Is.Zero);
        }

        [Test]
        public void Given_SampleTimes_When_CloningPlan_Then_ReturnsIndependentCopy()
        {
            float[] source = { 0.25f, 0.75f };
            float[] clone = InvokeEditorDiagnosticPlanner<float[]>(
                "CloneSampleTimes",
                new[] { typeof(float[]) },
                source);

            source[0] = 1f;

            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone, Is.EqualTo(new[] { 0.25f, 0.75f }));
            Assert.That(
                InvokeEditorDiagnosticPlanner<float[]>(
                    "CloneSampleTimes",
                    new[] { typeof(float[]) },
                    Array.Empty<float>()),
                Is.Null);
        }

        [Test]
        public void Given_InvalidEditorSmokeOverrides_When_Normalizing_Then_UsesExistingBounds()
        {
            Assert.That(InvokePlannerFloat("NormalizeStartTimeOverride", -1f), Is.NaN);
            Assert.That(InvokePlannerFloat("NormalizeStartTimeOverride", float.PositiveInfinity), Is.NaN);
            Assert.That(InvokePlannerFloat("NormalizeStartTimeOverride", 0f), Is.Zero);

            Assert.That(InvokePlannerFloat("NormalizePlaybackSpeedOverride", 0f), Is.NaN);
            Assert.That(InvokePlannerFloat("NormalizePlaybackSpeedOverride", float.NaN), Is.NaN);
            Assert.That(InvokePlannerFloat("NormalizePlaybackSpeedOverride", 0.00001f), Is.EqualTo(0.0001f));

            Assert.That(InvokePlannerFloat("NormalizeScreenshotPaddingOverride", 0f), Is.NaN);
            Assert.That(InvokePlannerFloat("NormalizeScreenshotPaddingOverride", 0.1f), Is.EqualTo(0.25f));
            Assert.That(InvokePlannerFloat("NormalizeScreenshotPaddingOverride", 3f), Is.EqualTo(2f));

            Assert.That(
                InvokePlannerFloat(
                    "NormalizeScreenshotVerticalViewportCenterOverride",
                    float.NegativeInfinity),
                Is.NaN);
            Assert.That(
                InvokePlannerFloat("NormalizeScreenshotVerticalViewportCenterOverride", -1f),
                Is.Zero);
            Assert.That(
                InvokePlannerFloat("NormalizeScreenshotVerticalViewportCenterOverride", 2f),
                Is.EqualTo(1f));
        }

        [Test]
        public void Given_ImportedModel_When_PreparingGhostPresentation_Then_PreservesContainerAndVisibility()
        {
            Type presenterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.GhostModelPresenter",
                throwOnError: false);
            MethodInfo createContainerMethod = presenterType?.GetMethod(
                "CreateContainer",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo setVisibilityMethod = presenterType?.GetMethod(
                "SetVisibility",
                BindingFlags.Static | BindingFlags.NonPublic);
            GameObject importedModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            importedModel.name = "ImportedGhost";

            try
            {
                Assert.That(createContainerMethod, Is.Not.Null);
                Assert.That(setVisibilityMethod, Is.Not.Null);

                var container = (GameObject)createContainerMethod.Invoke(
                    null,
                    new object[] { importedModel });
                setVisibilityMethod.Invoke(
                    null,
                    new object[] { importedModel, false, false });
                Renderer renderer = importedModel.GetComponent<Renderer>();

                Assert.That(container.name, Is.EqualTo("GhostContainer_ImportedGhost"));
                Assert.That(container.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(container.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(container.transform.localScale, Is.EqualTo(Vector3.one * 0.01f));
                Assert.That(importedModel.transform.parent, Is.EqualTo(container.transform));
                Assert.That(importedModel.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(renderer.enabled, Is.False);
                Assert.That(importedModel.GetComponent<GhostSkeletonDebugRenderer>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(
                    importedModel.transform.parent != null
                        ? importedModel.transform.parent.gameObject
                        : importedModel);
            }
        }

        [Test]
        public void Given_VisibleGhostWithRenderer_When_TogglingVisibility_Then_ReusesAndDisablesSkeletonAid()
        {
            Type presenterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.GhostModelPresenter",
                throwOnError: false);
            MethodInfo setVisibilityMethod = presenterType?.GetMethod(
                "SetVisibility",
                BindingFlags.Static | BindingFlags.NonPublic);
            GameObject importedModel = GameObject.CreatePrimitive(PrimitiveType.Cube);

            try
            {
                Assert.That(setVisibilityMethod, Is.Not.Null);

                setVisibilityMethod.Invoke(null, new object[] { importedModel, true, true });
                setVisibilityMethod.Invoke(null, new object[] { importedModel, true, true });

                GhostSkeletonDebugRenderer[] debugRenderers =
                    importedModel.GetComponents<GhostSkeletonDebugRenderer>();
                Assert.That(debugRenderers, Has.Length.EqualTo(1));
                Assert.That(debugRenderers[0].enabled, Is.True);
                Assert.That(importedModel.GetComponent<Renderer>().enabled, Is.True);

                setVisibilityMethod.Invoke(null, new object[] { importedModel, false, true });

                Assert.That(debugRenderers[0].enabled, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(importedModel);
            }
        }

        [Test]
        public void Given_GhostSkeletonDebugRendererInitializedBeforeAnimator_When_AnimatorIsAdded_Then_ReacquiresAnimatorForSceneViewLines()
        {
            GameObject ghost = new GameObject("RendererlessGhostForSceneView");

            try
            {
                GhostSkeletonDebugRenderer debugRenderer = ghost.AddComponent<GhostSkeletonDebugRenderer>();
                debugRenderer.SetVisible(true);

                Animator animator = ghost.AddComponent<Animator>();
                InvokeInstance(debugRenderer, "LateUpdate");

                Assert.That(ReadInstanceField<Animator>(debugRenderer, "animator"), Is.EqualTo(animator),
                    "Rendererless Ghost display is enabled before HumanoidAvatarBuilder adds Animator, so the Scene view debug skeleton must reacquire it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ghost);
            }
        }

        [Test]
        public void Given_GhostContainerScaleIsSmall_When_DrawingDebugSkeleton_Then_CompensatesLinePositionsForSceneVisibility()
        {
            Assert.That(
                CalculateGhostSkeletonDebugDisplayScale(new Vector3(0.01f, 0.01f, 0.01f)),
                Is.EqualTo(100f).Within(0.0001f),
                "Main_Auto wraps the imported Ghost in a 0.01 scale container, so debug skeleton lines must be expanded for Scene/Game visibility without changing the retargeting transform.");
        }

        [Test]
        public void Given_FullEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference()
        {
            bool hasPlan = TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7833f,
                requestedDurationSeconds: 207.7833f,
                requestedTargetFrameCount: 6234,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.True);
            Assert.That(targetFrameCount, Is.EqualTo(6001));
            Assert.That(recordingLengthSeconds, Is.EqualTo(6001f / 30f).Within(0.0001f));
            Assert.That(playbackSpeed, Is.EqualTo(207.7833f / (6001f / 30f)).Within(0.0001f));
        }

        [Test]
        public void Given_FullEditorSmokeSatisfactionClipAndSceneReferenceTimingDisabled_When_ResolvingReferenceTimingPolicy_Then_Forces6000FrameYybReference()
        {
            bool shouldUseReferenceTiming = ShouldUseKnownMmdReferenceTimingForEditorSmoke(
                "satisfaction_2",
                requestedDurationSeconds: 207.7833f,
                requestedTargetFrameCount: 6234,
                recordingFrameRate: 30f,
                sceneUseKnownReferenceTiming: false);

            Assert.That(shouldUseReferenceTiming, Is.True);

            bool hasPlan = TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7833f,
                requestedDurationSeconds: 207.7833f,
                requestedTargetFrameCount: 6234,
                recordingFrameRate: 30f,
                useKnownReferenceTiming: shouldUseReferenceTiming,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.True);
            Assert.That(targetFrameCount, Is.EqualTo(6001));
            Assert.That(recordingLengthSeconds, Is.EqualTo(6001f / 30f).Within(0.0001f));
            Assert.That(playbackSpeed, Is.EqualTo(207.7833f / (6001f / 30f)).Within(0.0001f));
        }

        [Test]
        public void Given_ShortEditorSmokeSatisfactionClipAndSceneReferenceTimingDisabled_When_ResolvingReferenceTimingPolicy_Then_KeepsRequestedSmokeWindow()
        {
            bool shouldUseReferenceTiming = ShouldUseKnownMmdReferenceTimingForEditorSmoke(
                "satisfaction_2",
                requestedDurationSeconds: 31f,
                requestedTargetFrameCount: 930,
                recordingFrameRate: 30f,
                sceneUseKnownReferenceTiming: false);

            Assert.That(shouldUseReferenceTiming, Is.False);
        }

        [Test]
        public void Given_ShortEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsRequestedSmokeWindow()
        {
            bool hasPlan = TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7833f,
                requestedDurationSeconds: 31f,
                requestedTargetFrameCount: 930,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.False);
            Assert.That(recordingLengthSeconds, Is.EqualTo(31f).Within(0.0001f));
            Assert.That(targetFrameCount, Is.EqualTo(930));
            Assert.That(playbackSpeed, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_LongRetargetPrewarmConfigured_When_ResolvingPrewarmFrameCount_Then_DoesNotCapAtLegacyTenFrames()
        {
            int resolved = VMDRecordingController.ResolvePrewarmFrameCount(60);

            Assert.That(resolved, Is.EqualTo(60));
        }

        [Test]
        public void Given_PreviewOnlyImport_When_ResolvingPrewarmFrameCount_Then_DoesNotHoldStartPoseAcrossVisibleFrames()
        {
            Assert.That(VMDRecordingController.ResolvePrewarmFrameCountForRecordingMode(6, shouldStartVmdRecording: false), Is.EqualTo(0));
            Assert.That(VMDRecordingController.ResolvePrewarmFrameCountForRecordingMode(6, shouldStartVmdRecording: true), Is.EqualTo(6));
        }

        [Test]
        public void Given_PreviewOnlyImport_When_ResolvingPrewarmVisibleYieldFrames_Then_DoesNotRenderExtraStartFrame()
        {
            Assert.That(VMDRecordingController.ResolveVisiblePrewarmYieldFrameCountForRecordingMode(6, shouldStartVmdRecording: false), Is.EqualTo(0));
            Assert.That(VMDRecordingController.ResolveVisiblePrewarmYieldFrameCountForRecordingMode(6, shouldStartVmdRecording: true), Is.EqualTo(6));
            Assert.That(VMDRecordingController.ResolveVisiblePrewarmYieldFrameCountForRecordingMode(0, shouldStartVmdRecording: true), Is.EqualTo(1));
        }

        [Test]
        public void Given_PreviewOnlyImport_When_ResolvingStartDelay_Then_DoesNotHoldInitialPoseBeforePlayback()
        {
            Assert.That(VMDRecordingController.ResolveStartDelay(1f, shouldStartVmdRecording: false), Is.EqualTo(0f));
            Assert.That(VMDRecordingController.ResolveStartDelay(1f, shouldStartVmdRecording: true), Is.EqualTo(1f));
        }

        [Test]
        public void Given_NonSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsDefaultClipTiming()
        {
            bool hasPlan = VMDRecordingController.TryBuildKnownMmdReferenceRecordingPlan(
                "other_dance",
                clipLengthSeconds: 207.7667f,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.False);
            Assert.That(recordingLengthSeconds, Is.EqualTo(207.7667f).Within(0.0001f));
            Assert.That(targetFrameCount, Is.EqualTo(0));
            Assert.That(playbackSpeed, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_EditorSmokeCaptureOverride_When_NormalizingResolution_Then_AllowsUhd4KAndRejectsInvalidInput()
        {
            bool hasOverride = TryBuildEditorSmokeCaptureResolutionOverride(3840, 2160, out int width, out int height);

            Assert.That(hasOverride, Is.True);
            Assert.That(width, Is.EqualTo(3840));
            Assert.That(height, Is.EqualTo(2160));

            hasOverride = TryBuildEditorSmokeCaptureResolutionOverride(0, 2160, out width, out height);

            Assert.That(hasOverride, Is.False);
            Assert.That(width, Is.EqualTo(0));
            Assert.That(height, Is.EqualTo(0));
        }

        [Test]
        public void Given_MainSceneFullRegressionEvidenceCommand_When_InspectingRunnerPlan_Then_UsesFullClipUhd4KAndHeadMiddleTailSamples()
        {
            Assert.That(GetRunnerFullRegressionCommand(), Is.EqualTo("capture_satisfaction_full_regression_evidence_208s_4k"));
            Assert.That(GetRunnerFullRegressionDurationSeconds(), Is.EqualTo(207.7833f).Within(0.0001f));

            int[] resolution = GetRunnerFullRegressionCaptureResolution();
            Assert.That(resolution, Is.EqualTo(new[] { 3840, 2160 }));

            float[] sampleTimes = GetRunnerFullRegressionSampleTimes();
            Assert.That(ContainsWithin(sampleTimes, 0.6f, 0.0001f), Is.True);
            Assert.That(ContainsWithin(sampleTimes, 13.1f, 0.0001f), Is.True);
            Assert.That(ContainsWithin(sampleTimes, 102.125f, 0.0001f), Is.True);
            Assert.That(ContainsWithin(sampleTimes, 181.25f, 0.0001f), Is.True);
        }

        [Test]
        public void Given_MainRecordingFullRegressionEvidenceCommand_When_InspectingRunnerPlan_Then_UsesSatisfaction2Fbx()
        {
            Assert.That(GetRunnerFullRegressionCommand(), Is.EqualTo("capture_satisfaction_full_regression_evidence_208s_4k"));
            Assert.That(GetRunnerFullRegressionFbxFileName(), Is.EqualTo("satisfaction_2.fbx"));
        }

        [Test]
        public void Given_QuickVmdSmokeCommand_When_InspectingRunnerPlan_Then_UsesSatisfaction2ForTwoSeconds()
        {
            Assert.That(GetRunnerQuickVmdSmokeCommand(), Is.EqualTo("capture_satisfaction_quick_vmd_smoke_2s"));
            Assert.That(GetRunnerQuickVmdSmokeFbxFileName(), Is.EqualTo("satisfaction_2.fbx"));
            Assert.That(GetRunnerQuickVmdSmokeDurationSeconds(), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(GetRunnerQuickVmdSmokeTargetFrameCount(), Is.EqualTo(60));
        }

        [Test]
        public void Given_RetargeterLegacyState_When_PreparingRecordingStartPose_Then_SamplesRetargeterPlaybackState()
        {
            GameObject ghost = new GameObject("retargeter-prewarm-ghost");
            try
            {
                Animation animation = ghost.AddComponent<Animation>();
                AnimationClip externalClip = CreateLegacyPositionClip("satisfaction_2", 1f);
                AnimationClip retargeterClip = CreateLegacyPositionClip("__PoseSpaceRetargeter_GhostClip", 2f);
                animation.AddClip(externalClip, externalClip.name);
                animation.AddClip(retargeterClip, retargeterClip.name);
                animation.clip = retargeterClip;

                bool sampled = PrepareRetargeterLegacyRecordingStartPose(animation, 0f, 1f, holdPose: true);

                Assert.That(sampled, Is.True);
                Assert.That(ghost.transform.localPosition.x, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(animation[retargeterClip.name].speed, Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ghost);
            }
        }

        [Test]
        public void Given_EditorSmokeSettingsSnapshot_When_CapturingAndClearing_Then_RestoresAllRuntimeSettings()
        {
            GameObject root = new GameObject("EditorSmokeSettingsSnapshotTest");
            FBXVmdPipeline fileManager = root.AddComponent<FBXVmdPipeline>();

            try
            {
                fileManager.enableRecordingDiagnostics = false;
                fileManager.enableDiagnosticFingerCloseups = true;
                fileManager.useDeterministicCaptureFramerateForDiagnostics = false;
                fileManager.startDelay = 1.25f;
                BeginEditorDiagnosticSession(
                    fileManager,
                    "snapshot.fbx",
                    hasExtendedOverrides: false);

                InvokeInstance(fileManager, "CaptureEditorSmokeSettings");
                fileManager.enableRecordingDiagnostics = true;
                fileManager.enableDiagnosticFingerCloseups = false;
                fileManager.useDeterministicCaptureFramerateForDiagnostics = true;
                fileManager.startDelay = 9.5f;
                InvokeInstance(fileManager, "ClearEditorSmokeOverride");

                Assert.That(fileManager.enableRecordingDiagnostics, Is.False);
                Assert.That(fileManager.enableDiagnosticFingerCloseups, Is.True);
                Assert.That(fileManager.useDeterministicCaptureFramerateForDiagnostics, Is.False);
                Assert.That(fileManager.startDelay, Is.EqualTo(1.25f));
                AssertEditorSmokeCoreCleanupState(fileManager);
                AssertEditorSmokeSettingsSnapshotIsDefault(fileManager);

                InvokeInstance(fileManager, "ClearEditorSmokeOverride");

                Assert.That(fileManager.enableRecordingDiagnostics, Is.False);
                Assert.That(fileManager.enableDiagnosticFingerCloseups, Is.True);
                Assert.That(fileManager.useDeterministicCaptureFramerateForDiagnostics, Is.False);
                Assert.That(fileManager.startDelay, Is.EqualTo(1.25f));
                AssertEditorSmokeCoreCleanupState(fileManager);
                AssertEditorSmokeSettingsSnapshotIsDefault(fileManager);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Given_EditorSmokeSettingsSnapshot_When_StartDelayIsNaN_Then_RestoresNaNExactly()
        {
            GameObject root = new GameObject("EditorSmokeSettingsSnapshotNaNTest");
            FBXVmdPipeline fileManager = root.AddComponent<FBXVmdPipeline>();

            try
            {
                fileManager.enableRecordingDiagnostics = true;
                fileManager.enableDiagnosticFingerCloseups = false;
                fileManager.useDeterministicCaptureFramerateForDiagnostics = true;
                fileManager.startDelay = float.NaN;

                InvokeInstance(fileManager, "CaptureEditorSmokeSettings");
                fileManager.enableRecordingDiagnostics = false;
                fileManager.enableDiagnosticFingerCloseups = true;
                fileManager.useDeterministicCaptureFramerateForDiagnostics = false;
                fileManager.startDelay = 3.75f;
                InvokeInstance(fileManager, "ClearEditorSmokeOverride");

                Assert.That(fileManager.enableRecordingDiagnostics, Is.True);
                Assert.That(fileManager.enableDiagnosticFingerCloseups, Is.False);
                Assert.That(fileManager.useDeterministicCaptureFramerateForDiagnostics, Is.True);
                Assert.That(float.IsNaN(fileManager.startDelay), Is.True);
                Assert.That(fileManager.startDelay, Is.NaN);
                AssertEditorSmokeSettingsSnapshotIsDefault(fileManager);

                InvokeInstance(fileManager, "ClearEditorSmokeOverride");

                Assert.That(fileManager.enableRecordingDiagnostics, Is.True);
                Assert.That(fileManager.enableDiagnosticFingerCloseups, Is.False);
                Assert.That(fileManager.useDeterministicCaptureFramerateForDiagnostics, Is.True);
                Assert.That(float.IsNaN(fileManager.startDelay), Is.True);
                Assert.That(fileManager.startDelay, Is.NaN);
                AssertEditorSmokeSettingsSnapshotIsDefault(fileManager);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Given_EditorSmokeSettingsSnapshot_When_ClearRunsWithoutActiveSnapshot_Then_DoesNotChangeSettings()
        {
            GameObject root = new GameObject("EditorSmokeSettingsSnapshotInactiveTest");
            FBXVmdPipeline fileManager = root.AddComponent<FBXVmdPipeline>();

            try
            {
                fileManager.enableRecordingDiagnostics = true;
                fileManager.enableDiagnosticFingerCloseups = false;
                fileManager.useDeterministicCaptureFramerateForDiagnostics = true;
                fileManager.startDelay = 4.5f;
                BeginEditorDiagnosticSession(
                    fileManager,
                    "inactive.fbx",
                    hasExtendedOverrides: true);

                AssertEditorSmokeSettingsSnapshotIsDefault(fileManager);
                InvokeInstance(fileManager, "ClearEditorSmokeOverride");

                Assert.That(fileManager.enableRecordingDiagnostics, Is.True);
                Assert.That(fileManager.enableDiagnosticFingerCloseups, Is.False);
                Assert.That(fileManager.useDeterministicCaptureFramerateForDiagnostics, Is.True);
                Assert.That(fileManager.startDelay, Is.EqualTo(4.5f));
                AssertEditorSmokeCoreCleanupState(fileManager);
                AssertEditorSmokeExtendedCleanupState(fileManager);
                AssertEditorSmokeSettingsSnapshotIsDefault(fileManager);

                InvokeInstance(fileManager, "ClearEditorSmokeOverride");

                Assert.That(fileManager.enableRecordingDiagnostics, Is.True);
                Assert.That(fileManager.enableDiagnosticFingerCloseups, Is.False);
                Assert.That(fileManager.useDeterministicCaptureFramerateForDiagnostics, Is.True);
                Assert.That(fileManager.startDelay, Is.EqualTo(4.5f));
                AssertEditorSmokeCoreCleanupState(fileManager);
                AssertEditorSmokeExtendedCleanupState(fileManager);
                AssertEditorSmokeSettingsSnapshotIsDefault(fileManager);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Given_ActiveEditorDiagnosticSession_When_NotifyingFinished_Then_ForwardsCurrentFbxOnce()
        {
            GameObject root = new GameObject("EditorDiagnosticSessionFinishedTest");
            FBXVmdPipeline fileManager = root.AddComponent<FBXVmdPipeline>();
            int invocationCount = 0;
            string notifiedFbxFileName = null;
            VmdSaveResult notifiedResult = default(VmdSaveResult);
            fileManager.EditorDiagnosticSmokeFinished += (fbxFileName, result) =>
            {
                invocationCount++;
                notifiedFbxFileName = fbxFileName;
                notifiedResult = result;
            };

            try
            {
                BeginEditorDiagnosticSession(
                    fileManager,
                    "finished.fbx",
                    hasExtendedOverrides: false);
                VmdSaveResult result = VmdSaveResult.Ok("output.vmd", 30, 1024);

                InvokeInstance(fileManager, "NotifyEditorSmokeFinished", result);
                InvokeInstance(fileManager, "ClearEditorSmokeOverride");
                InvokeInstance(fileManager, "NotifyEditorSmokeFinished", result);

                Assert.That(invocationCount, Is.EqualTo(1));
                Assert.That(notifiedFbxFileName, Is.EqualTo("finished.fbx"));
                Assert.That(notifiedResult.FilePath, Is.EqualTo("output.vmd"));
                Assert.That(notifiedResult.FrameCount, Is.EqualTo(30));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Type GetEditorDiagnosticPlannerType()
        {
            Type plannerType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FBXEditorDiagnosticPlanner",
                throwOnError: false);
            Assert.That(plannerType, Is.Not.Null);
            return plannerType;
        }

        private static float InvokePlannerFloat(string methodName, float value)
        {
            return InvokeEditorDiagnosticPlanner<float>(
                methodName,
                new[] { typeof(float) },
                value);
        }

        private static T InvokeEditorDiagnosticPlanner<T>(
            string methodName,
            Type[] parameterTypes,
            params object[] arguments)
        {
            MethodInfo method = GetEditorDiagnosticPlannerType().GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null, $"FBXEditorDiagnosticPlanner must expose {methodName}.");
            return (T)method.Invoke(null, arguments);
        }

        private static string Resolve(
            string fbxFileName,
            string controlledDirectory,
            string dataPath,
            params string[] existingPaths)
        {
            MethodInfo method = GetEditorDiagnosticPlannerType().GetMethod(
                "ResolveFbxPath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: SmokeResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXEditorDiagnosticPlanner must expose a fakeable path resolver.");

            var existing = new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);
            Func<string, bool> fileExists = existing.Contains;

            return (string)method.Invoke(null, new object[] { fbxFileName, controlledDirectory, dataPath, fileExists });
        }

        private static string ResolveHumanoidReference(
            string importedRelativePath,
            string sourceRelativePath,
            string sourceFileName,
            params string[] humanoidClipPaths)
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.EditorHumanoidReferenceApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null,
                "EditorHumanoidReferenceApplier should own reference path resolution.");

            MethodInfo method = applierType.GetMethod(
                "ResolveEditorHumanoidReferencePath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HumanoidReferenceResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "EditorHumanoidReferenceApplier must expose a fakeable reference path resolver.");

            var clips = new HashSet<string>(humanoidClipPaths, StringComparer.OrdinalIgnoreCase);
            Func<string, bool> hasHumanoidClip = clips.Contains;

            return (string)method.Invoke(null, new object[] { importedRelativePath, sourceRelativePath, sourceFileName, hasHumanoidClip });
        }

        private static bool ShouldConfigureImportSettings(string sourcePath, string targetPath, string dataPath)
        {
            MethodInfo method = typeof(FBXImportController).GetMethod(
                "ShouldConfigureEditorImportSettings",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string), typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXImportController must expose a fakeable import-settings decision helper.");
            return (bool)method.Invoke(null, new object[] { sourcePath, targetPath, dataPath });
        }

        private static bool ShouldApplyTargetIdlePoseGuardThisFrame(bool isProcessing, bool hasActiveRetargeter)
        {
            MethodInfo method = typeof(FBXVmdPipeline).GetMethod(
                "ShouldApplyTargetIdlePoseGuardThisFrame",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(bool), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXVmdPipeline must expose a testable frame guard decision so preview-only retarget playback is not reset to the idle pose.");

            return (bool)method.Invoke(null, new object[] { isProcessing, hasActiveRetargeter });
        }

        private static bool TryBuildEditorSmokeCaptureResolutionOverride(
            int requestedWidth,
            int requestedHeight,
            out int width,
            out int height)
        {
            MethodInfo method = GetEditorDiagnosticPlannerType().GetMethod(
                "TryBuildCaptureResolutionOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: CaptureResolutionOverrideParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXEditorDiagnosticPlanner must expose a fakeable capture resolution helper.");

            object[] args =
            {
                requestedWidth,
                requestedHeight,
                0,
                0
            };

            bool result = (bool)method.Invoke(null, args);
            width = (int)args[2];
            height = (int)args[3];
            return result;
        }

        private static string GetRunnerFullRegressionCommand()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetFullRegressionEvidenceCommandForTest", Type.EmptyTypes);
            return (string)method.Invoke(null, Array.Empty<object>());
        }

        private static float GetRunnerFullRegressionDurationSeconds()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetFullRegressionEvidenceDurationSecondsForTest", Type.EmptyTypes);
            return (float)method.Invoke(null, Array.Empty<object>());
        }

        private static string GetRunnerFullRegressionFbxFileName()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetFullRegressionEvidenceFbxFileNameForTest", Type.EmptyTypes);
            return (string)method.Invoke(null, Array.Empty<object>());
        }

        private static int[] GetRunnerFullRegressionCaptureResolution()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetFullRegressionEvidenceCaptureResolutionForTest", Type.EmptyTypes);
            return (int[])method.Invoke(null, Array.Empty<object>());
        }

        private static float[] GetRunnerFullRegressionSampleTimes()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetFullRegressionEvidenceSampleTimesForTest", Type.EmptyTypes);
            return (float[])method.Invoke(null, Array.Empty<object>());
        }

        private static string GetRunnerQuickVmdSmokeCommand()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetQuickVmdSmokeCommandForTest", Type.EmptyTypes);
            return (string)method.Invoke(null, Array.Empty<object>());
        }

        private static string GetRunnerQuickVmdSmokeFbxFileName()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetQuickVmdSmokeFbxFileNameForTest", Type.EmptyTypes);
            return (string)method.Invoke(null, Array.Empty<object>());
        }

        private static float GetRunnerQuickVmdSmokeDurationSeconds()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetQuickVmdSmokeDurationSecondsForTest", Type.EmptyTypes);
            return (float)method.Invoke(null, Array.Empty<object>());
        }

        private static int GetRunnerQuickVmdSmokeTargetFrameCount()
        {
            MethodInfo method = GetSmokeRunnerMethod("GetQuickVmdSmokeTargetFrameCountForTest", Type.EmptyTypes);
            return (int)method.Invoke(null, Array.Empty<object>());
        }

        private static MethodInfo GetSmokeRunnerMethod(string methodName, Type[] parameterTypes)
        {
            Type runnerType = typeof(FbxPlaybackSmokeRunner);
            MethodInfo method = runnerType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, $"FbxPlaybackSmokeRunner must expose {methodName} for smoke plan verification.");
            return method;
        }

        private static AnimationClip CreateLegacyPositionClip(string clipName, float x)
        {
            AnimationClip clip = new AnimationClip
            {
                name = clipName,
                legacy = true
            };
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, 1f, x));
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, 1f, 0f));
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, 1f, 0f));
            return clip;
        }

        private static bool PrepareRetargeterLegacyRecordingStartPose(Animation animation, float startTimeSeconds, float playbackSpeed, bool holdPose)
        {
            MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                "PrepareRecordingStartPoseForTest",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Animation), typeof(float), typeof(float), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "PoseSpaceRetargeter should expose a fakeable recording-start prewarm helper.");
            return (bool)method.Invoke(null, new object[] { animation, startTimeSeconds, playbackSpeed, holdPose });
        }

        private static bool ContainsWithin(float[] values, float expected, float tolerance)
        {
            if (values == null)
            {
                return false;
            }

            foreach (float value in values)
            {
                if (Math.Abs(value - expected) <= tolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static string MakeProjectRelativePath(string path, string projectRoot)
        {
            MethodInfo method = typeof(VMDRecordingController).GetMethod(
                "MakeProjectRelativePath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ProjectRelativePathParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "VMDRecordingController must own a fakeable project-relative path policy.");

            return (string)method.Invoke(null, new object[] { path, projectRoot });
        }

        private static bool ShouldAttachGhostSkeletonDebugRenderer(bool visible, int rendererCount)
        {
            Type presenterType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.GhostModelPresenter",
                throwOnError: false);
            Assert.That(presenterType, Is.Not.Null, "GhostModelPresenter should own Ghost visibility decisions.");

            MethodInfo method = presenterType.GetMethod(
                "ShouldAttachGhostSkeletonDebugRenderer",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: GhostSkeletonFallbackParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "GhostModelPresenter must expose a testable fallback decision for Ghost visibility.");

            return (bool)method.Invoke(null, new object[] { visible, rendererCount });
        }

        private static T ReadInstanceField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} must keep a private {fieldName} field for this visibility regression test.");

            return (T)field.GetValue(target);
        }

        private static void AssertEditorSmokeCoreCleanupState(FBXVmdPipeline fileManager)
        {
            Assert.That(ReadEditorDiagnosticSessionProperty<bool>(fileManager, "IsRecordingOverrideActive"), Is.False);
            Assert.That(ReadEditorDiagnosticSessionProperty<int>(fileManager, "TargetFrameCount"), Is.EqualTo(0));
            Assert.That(ReadEditorDiagnosticSessionProperty<float>(fileManager, "DurationSeconds"), Is.EqualTo(0f));
            Assert.That(ReadEditorDiagnosticSessionProperty<float[]>(fileManager, "SampleTimesOverride"), Is.Null);
            Assert.That(ReadEditorDiagnosticSessionProperty<FBXVmdPipeline.EditorDiagnosticSmokeSegment>(fileManager, "Segment"),
                Is.EqualTo(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head));
            Assert.That(ReadEditorDiagnosticSessionProperty<string>(fileManager, "CurrentFbxFileName"), Is.Null);
        }

        private static void AssertEditorSmokeExtendedCleanupState(FBXVmdPipeline fileManager)
        {
            Assert.That(ReadEditorDiagnosticSessionProperty<bool>(fileManager, "HasCaptureResolutionOverride"), Is.False);
            Assert.That(ReadEditorDiagnosticSessionProperty<int>(fileManager, "CaptureWidth"), Is.EqualTo(0));
            Assert.That(ReadEditorDiagnosticSessionProperty<int>(fileManager, "CaptureHeight"), Is.EqualTo(0));
            Assert.That(ReadEditorDiagnosticSessionProperty<float>(fileManager, "DiagnosticScreenshotPaddingOverride"), Is.NaN);
            Assert.That(ReadEditorDiagnosticSessionProperty<float>(fileManager, "DiagnosticScreenshotVerticalViewportCenterOverride"), Is.NaN);
            Assert.That(ReadEditorDiagnosticSessionProperty<bool>(fileManager, "UseKnownReferenceTiming"), Is.False);
            Assert.That(ReadEditorDiagnosticSessionProperty<float>(fileManager, "RecordingStartTimeOverrideSeconds"), Is.NaN);
            Assert.That(ReadEditorDiagnosticSessionProperty<float>(fileManager, "RecordingPlaybackSpeedOverride"), Is.NaN);
        }

        private static void AssertEditorSmokeSettingsSnapshotIsDefault(FBXVmdPipeline fileManager)
        {
            object session = GetEditorDiagnosticSession(fileManager);
            FieldInfo snapshotField = session.GetType().GetField(
                "_settingsSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(snapshotField, Is.Not.Null, "FBXEditorDiagnosticSession must keep the settings snapshot backing field.");
            Assert.That(ReadEditorDiagnosticSessionProperty<bool>(fileManager, "HasSettingsSnapshot"), Is.False);

            object snapshot = snapshotField.GetValue(session);
            object defaultSnapshot = Activator.CreateInstance(snapshotField.FieldType);
            Assert.That(snapshot, Is.EqualTo(defaultSnapshot));
        }

        private static void BeginEditorDiagnosticSession(
            FBXVmdPipeline fileManager,
            string currentFbxFileName,
            bool hasExtendedOverrides)
        {
            object session = GetEditorDiagnosticSession(fileManager);
            Type planType = session.GetType().GetNestedType(
                "Plan",
                BindingFlags.NonPublic);
            Assert.That(planType, Is.Not.Null);
            object plan = Activator.CreateInstance(planType, nonPublic: true);
            SetProperty(plan, "TargetFrameCount", 17);
            SetProperty(plan, "DurationSeconds", 2.5f);
            SetProperty(plan, "SampleTimesOverride", new[] { 0.5f });
            SetProperty(
                plan,
                "Segment",
                FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle);
            SetProperty(plan, "CurrentFbxFileName", currentFbxFileName);
            if (hasExtendedOverrides)
            {
                SetProperty(plan, "HasCaptureResolutionOverride", true);
                SetProperty(plan, "CaptureWidth", 1920);
                SetProperty(plan, "CaptureHeight", 1080);
                SetProperty(plan, "DiagnosticScreenshotPaddingOverride", 0.25f);
                SetProperty(
                    plan,
                    "DiagnosticScreenshotVerticalViewportCenterOverride",
                    0.75f);
                SetProperty(plan, "UseKnownReferenceTiming", true);
                SetProperty(plan, "RecordingStartTimeOverrideSeconds", 1f);
                SetProperty(plan, "RecordingPlaybackSpeedOverride", 0.5f);
            }

            MethodInfo beginMethod = session.GetType().GetMethod(
                "Begin",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(beginMethod, Is.Not.Null);
            beginMethod.Invoke(session, new[] { plan });
        }

        private static T ReadEditorDiagnosticSessionProperty<T>(
            FBXVmdPipeline fileManager,
            string propertyName)
        {
            object session = GetEditorDiagnosticSession(fileManager);
            PropertyInfo property = session.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(session);
        }

        private static object GetEditorDiagnosticSession(FBXVmdPipeline fileManager)
        {
            PropertyInfo property = typeof(FBXVmdPipeline).GetProperty(
                "EditorDiagnosticSession",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return property.GetValue(fileManager);
        }

        private static void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            property.SetValue(target, value);
        }

        private static void InvokeInstance(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"{target.GetType().Name} must keep a private {methodName} method for this visibility regression test.");

            method.Invoke(target, arguments);
        }

        private static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            MethodInfo method = GetEditorDiagnosticPlannerType().GetMethod(
                "TryBuildKnownReferenceRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorSmokeReferenceTimingParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXEditorDiagnosticPlanner must expose a fakeable reference timing helper.");

            object[] args =
            {
                outputBaseName,
                clipLengthSeconds,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                recordingFrameRate,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                1f
            };

            bool result = (bool)method.Invoke(null, args);
            recordingLengthSeconds = (float)args[5];
            targetFrameCount = (int)args[6];
            playbackSpeed = (float)args[7];
            return result;
        }

        private static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            bool useKnownReferenceTiming,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            MethodInfo method = GetEditorDiagnosticPlannerType().GetMethod(
                "TryBuildKnownReferenceRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorSmokeReferenceTimingWithOptionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXEditorDiagnosticPlanner must expose a reference timing option.");

            object[] args =
            {
                outputBaseName,
                clipLengthSeconds,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                recordingFrameRate,
                useKnownReferenceTiming,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                1f
            };

            bool result = (bool)method.Invoke(null, args);
            recordingLengthSeconds = (float)args[6];
            targetFrameCount = (int)args[7];
            playbackSpeed = (float)args[8];
            return result;
        }

        private static bool ShouldUseKnownMmdReferenceTimingForEditorSmoke(
            string outputBaseName,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            bool sceneUseKnownReferenceTiming)
        {
            MethodInfo method = GetEditorDiagnosticPlannerType().GetMethod(
                "ShouldUseKnownReferenceTiming",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorSmokeReferenceTimingPolicyParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FBXEditorDiagnosticPlanner must expose a fakeable reference timing policy.");

            return (bool)method.Invoke(
                null,
                new object[]
                {
                    outputBaseName,
                    requestedDurationSeconds,
                    requestedTargetFrameCount,
                    recordingFrameRate,
                    sceneUseKnownReferenceTiming
                });
        }

        private static bool IsSupportedMainScene(string sceneName)
        {
            MethodInfo method = typeof(FbxPlaybackSmokeRunner).GetMethod(
                "IsSupportedMainScene",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: SceneSupportParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FbxPlaybackSmokeRunner should expose a testable scene support helper.");

            return (bool)method.Invoke(null, new object[] { sceneName });
        }

        private static float CalculateGhostSkeletonDebugDisplayScale(Vector3 lossyScale)
        {
            MethodInfo method = typeof(GhostSkeletonDebugRenderer).GetMethod(
                "CalculateDisplayScaleCompensation",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Vector3) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "GhostSkeletonDebugRenderer must expose a testable display-scale compensation helper.");

            return (float)method.Invoke(null, new object[] { lossyScale });
        }

    }
}
