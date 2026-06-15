using Member_Han.Modules.FBXImporter;
using Member_Han.Modules.FBXImporter.EditorTools;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class FileManagerEditorSmokePathTests
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

        private static readonly Type[] ImportSettingsDecisionParameterTypes =
        {
            typeof(string),
            typeof(string),
            typeof(string)
        };

        private static readonly Type[] ReferenceTimingWithOptionParameterTypes =
        {
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(bool),
            typeof(float).MakeByRefType(),
            typeof(int).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] RecordingLengthParameterTypes =
        {
            typeof(float),
            typeof(float)
        };

        private static readonly Type[] GhostSkeletonFallbackParameterTypes =
        {
            typeof(bool),
            typeof(int)
        };

        private static readonly Type[] ReferenceTimingParameterTypes =
        {
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(int).MakeByRefType(),
            typeof(float).MakeByRefType()
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

        private static readonly Type[] RetargetPrewarmFrameCountParameterTypes =
        {
            typeof(int)
        };

        private static readonly Type[] RetargetPrewarmFrameCountForRecordingModeParameterTypes =
        {
            typeof(int),
            typeof(bool)
        };

        private static readonly Type[] RetargetPrewarmVisibleYieldFrameCountForRecordingModeParameterTypes =
        {
            typeof(int),
            typeof(bool)
        };

        private static readonly Type[] StartDelayForRecordingModeParameterTypes =
        {
            typeof(float),
            typeof(bool)
        };

        private static readonly Type[] RecordingModeParameterTypes =
        {
            typeof(bool),
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
        public void Given_ControlledSourceAndManualClipExists_When_ResolvingEditorHumanoidReferencePath_Then_UsesControlledSourcePath()
        {
            string controlledPath = "Assets/Resources/Import_FBX/dance.fbx";

            string resolved = ResolveHumanoidReference(
                controlledPath,
                controlledPath,
                @"C:\Project\Assets\Resources\Import_FBX\dance.fbx",
                controlledPath,
                "Assets/_Project/FBX/dance.fbx");

            Assert.That(resolved, Is.EqualTo(controlledPath));
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
            bool shouldRecord = ShouldStartVmdRecordingAfterImport(
                recordVmdAfterImport: false,
                editorSmokeRecordingOverrideActive: false);

            Assert.That(shouldRecord, Is.False);
        }

        [Test]
        public void Given_CaptureOnlyModeWithEditorSmoke_When_DecidingRecordingMode_Then_AllowsDiagnosticVmdRecording()
        {
            bool shouldRecord = ShouldStartVmdRecordingAfterImport(
                recordVmdAfterImport: false,
                editorSmokeRecordingOverrideActive: true);

            Assert.That(shouldRecord, Is.True);
        }

        [Test]
        public void Given_VmdMode_When_DecidingRecordingMode_Then_StartsVmdRecording()
        {
            bool shouldRecord = ShouldStartVmdRecordingAfterImport(
                recordVmdAfterImport: true,
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
            bool hasPlan = TryBuildKnownMmdReferenceRecordingPlan(
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
            bool hasPlan = TryBuildKnownMmdReferenceRecordingPlan(
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
            float recordingLengthSeconds = ResolveRecordingLengthForPlaybackSpeed(
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
            int resolved = ResolveRetargetPrewarmFrameCount(60);

            Assert.That(resolved, Is.EqualTo(60));
        }

        [Test]
        public void Given_PreviewOnlyImport_When_ResolvingPrewarmFrameCount_Then_DoesNotHoldStartPoseAcrossVisibleFrames()
        {
            Assert.That(ResolveRetargetPrewarmFrameCountForRecordingMode(6, shouldStartVmdRecording: false), Is.EqualTo(0));
            Assert.That(ResolveRetargetPrewarmFrameCountForRecordingMode(6, shouldStartVmdRecording: true), Is.EqualTo(6));
        }

        [Test]
        public void Given_PreviewOnlyImport_When_ResolvingPrewarmVisibleYieldFrames_Then_DoesNotRenderExtraStartFrame()
        {
            Assert.That(ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode(6, shouldStartVmdRecording: false), Is.EqualTo(0));
            Assert.That(ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode(6, shouldStartVmdRecording: true), Is.EqualTo(6));
            Assert.That(ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode(0, shouldStartVmdRecording: true), Is.EqualTo(1));
        }

        [Test]
        public void Given_PreviewOnlyImport_When_ResolvingStartDelay_Then_DoesNotHoldInitialPoseBeforePlayback()
        {
            Assert.That(ResolveStartDelayForRecordingMode(1f, shouldStartVmdRecording: false), Is.EqualTo(0f));
            Assert.That(ResolveStartDelayForRecordingMode(1f, shouldStartVmdRecording: true), Is.EqualTo(1f));
        }

        [Test]
        public void Given_NonSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsDefaultClipTiming()
        {
            bool hasPlan = TryBuildKnownMmdReferenceRecordingPlan(
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

        private static string Resolve(
            string fbxFileName,
            string controlledDirectory,
            string dataPath,
            params string[] existingPaths)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveEditorSmokeFbxPath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: SmokeResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a static resolver overload for fakeable path tests.");

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
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveEditorHumanoidReferencePath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HumanoidReferenceResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a static humanoid reference resolver overload for fakeable path tests.");

            var clips = new HashSet<string>(humanoidClipPaths, StringComparer.OrdinalIgnoreCase);
            Func<string, bool> hasHumanoidClip = clips.Contains;

            return (string)method.Invoke(null, new object[] { importedRelativePath, sourceRelativePath, sourceFileName, hasHumanoidClip });
        }

        private static bool ShouldConfigureImportSettings(string sourcePath, string targetPath, string dataPath)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ShouldConfigureEditorImportSettings",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ImportSettingsDecisionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable import-settings decision helper.");

            return (bool)method.Invoke(null, new object[] { sourcePath, targetPath, dataPath });
        }

        private static bool ShouldStartVmdRecordingAfterImport(bool recordVmdAfterImport, bool editorSmokeRecordingOverrideActive)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ShouldStartVmdRecordingAfterImport",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RecordingModeParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a testable recording-mode decision helper.");

            return (bool)method.Invoke(null, new object[] { recordVmdAfterImport, editorSmokeRecordingOverrideActive });
        }

        private static bool ShouldApplyTargetIdlePoseGuardThisFrame(bool isProcessing, bool hasActiveRetargeter)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ShouldApplyTargetIdlePoseGuardThisFrame",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(bool), typeof(bool) },
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a testable frame guard decision so preview-only retarget playback is not reset to the idle pose.");

            return (bool)method.Invoke(null, new object[] { isProcessing, hasActiveRetargeter });
        }

        private static bool TryBuildEditorSmokeCaptureResolutionOverride(
            int requestedWidth,
            int requestedHeight,
            out int width,
            out int height)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "TryBuildEditorSmokeCaptureResolutionOverride",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: CaptureResolutionOverrideParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable editor-smoke capture resolution override helper.");

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
            MethodInfo method = typeof(FileManager).GetMethod(
                "MakeProjectRelativePath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ProjectRelativePathParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a static project-relative path overload for fakeable path tests.");

            return (string)method.Invoke(null, new object[] { path, projectRoot });
        }

        private static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "TryBuildKnownMmdReferenceRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ReferenceTimingParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable reference timing helper for YYB MMD acceptance.");

            object[] args =
            {
                outputBaseName,
                clipLengthSeconds,
                recordingFrameRate,
                0f,
                0,
                0f
            };

            bool result = (bool)method.Invoke(null, args);
            recordingLengthSeconds = (float)args[3];
            targetFrameCount = (int)args[4];
            playbackSpeed = (float)args[5];
            return result;
        }

        private static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            bool useKnownReferenceTiming,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "TryBuildKnownMmdReferenceRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ReferenceTimingWithOptionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a reference timing option so Main_Auto can default to 1x VMD recording.");

            object[] args =
            {
                outputBaseName,
                clipLengthSeconds,
                recordingFrameRate,
                useKnownReferenceTiming,
                clipLengthSeconds,
                0,
                1f
            };

            bool result = (bool)method.Invoke(null, args);
            recordingLengthSeconds = (float)args[4];
            targetFrameCount = (int)args[5];
            playbackSpeed = (float)args[6];
            return result;
        }

        private static float ResolveRecordingLengthForPlaybackSpeed(float clipLengthSeconds, float playbackSpeed)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveRecordingLengthForPlaybackSpeed",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RecordingLengthParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable playback-speed duration helper.");

            return (float)method.Invoke(null, new object[] { clipLengthSeconds, playbackSpeed });
        }

        private static bool ShouldAttachGhostSkeletonDebugRenderer(bool visible, int rendererCount)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ShouldAttachGhostSkeletonDebugRenderer",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: GhostSkeletonFallbackParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a testable fallback decision for rendererless Ghost visibility.");

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

        private static void InvokeInstance(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null, $"{target.GetType().Name} must keep a private {methodName} method for this visibility regression test.");

            method.Invoke(target, Array.Empty<object>());
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
            MethodInfo method = typeof(FileManager).GetMethod(
                "TryBuildKnownMmdReferenceEditorSmokeRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorSmokeReferenceTimingParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable editor smoke reference timing helper for ref MP4 alignment.");

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
            MethodInfo method = typeof(FileManager).GetMethod(
                "TryBuildKnownMmdReferenceEditorSmokeRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorSmokeReferenceTimingWithOptionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose an editor smoke reference timing option for scene-independent full smoke policy.");

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
            MethodInfo method = typeof(FileManager).GetMethod(
                "ShouldUseKnownMmdReferenceTimingForEditorSmoke",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorSmokeReferenceTimingPolicyParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable editor smoke reference timing policy for full 208s YYB acceptance.");

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

        private static int ResolveRetargetPrewarmFrameCount(int configuredFrameCount)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveRetargetPrewarmFrameCount",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RetargetPrewarmFrameCountParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager should expose a deterministic prewarm frame resolver for full-reference smoke stabilization.");

            return (int)method.Invoke(null, new object[] { configuredFrameCount });
        }

        private static int ResolveRetargetPrewarmFrameCountForRecordingMode(int configuredFrameCount, bool shouldStartVmdRecording)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveRetargetPrewarmFrameCountForRecordingMode",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RetargetPrewarmFrameCountForRecordingModeParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager should expose recording-mode aware prewarm frame policy for preview-only import.");

            return (int)method.Invoke(null, new object[] { configuredFrameCount, shouldStartVmdRecording });
        }

        private static int ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode(int configuredFrameCount, bool shouldStartVmdRecording)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RetargetPrewarmVisibleYieldFrameCountForRecordingModeParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager should expose recording-mode aware visible prewarm yield policy so preview-only import does not render an extra start frame.");

            return (int)method.Invoke(null, new object[] { configuredFrameCount, shouldStartVmdRecording });
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

        private static float ResolveStartDelayForRecordingMode(float configuredStartDelay, bool shouldStartVmdRecording)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveStartDelayForRecordingMode",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: StartDelayForRecordingModeParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager should expose recording-mode aware start delay policy so preview-only import does not hold the initial pose before playback.");

            return (float)method.Invoke(null, new object[] { configuredStartDelay, shouldStartVmdRecording });
        }
    }
}
