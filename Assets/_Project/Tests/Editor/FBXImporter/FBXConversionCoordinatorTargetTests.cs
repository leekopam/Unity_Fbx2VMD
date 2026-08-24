using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using RootMotion.FinalIK;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Editor.FBXImporter
{
    public class FBXConversionCoordinatorTargetTests
    {
        [Test]
        public void Given_MissingTargetCharacter_When_ResolvingTargetAnimator_Then_ReturnsTargetError()
        {
            bool resolved = TryResolveTargetAnimator(
                targetObject: null,
                out Animator targetAnimator,
                out string errorMessage);

            Assert.That(resolved, Is.False);
            Assert.That(targetAnimator, Is.Null);
            Assert.That(errorMessage, Is.EqualTo("Target Character가 지정되어 있지 않습니다."));
        }

        [Test]
        public void Given_TargetWithoutAnimator_When_ResolvingTargetAnimator_Then_ReturnsAvatarError()
        {
            GameObject targetObject = new GameObject("TargetWithoutAnimator");
            try
            {
                bool resolved = TryResolveTargetAnimator(
                    targetObject,
                    out Animator targetAnimator,
                    out string errorMessage);

                Assert.That(resolved, Is.False);
                Assert.That(targetAnimator, Is.Null);
                Assert.That(errorMessage, Is.EqualTo("Target Character에 유효한 Humanoid Avatar가 없습니다."));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Given_DisabledArmDirectionCorrection_When_ConfiguringTargetGuard_Then_DisablesExistingGuard()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            GameObject targetObject = new GameObject("TargetWithDirectionGuard");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.enableYybArmDirectionRetargetCorrection = false;
                var coordinator = new FBXConversionCoordinator(pipeline);
                HumanoidArmDirectionRetargetGuard existingGuard =
                    targetObject.AddComponent<HumanoidArmDirectionRetargetGuard>();
                existingGuard.enableDirectionRetarget = true;
                existingGuard.enabled = true;

                HumanoidArmDirectionRetargetGuard configuredGuard = ConfigureArmDirectionGuard(
                    coordinator,
                    targetObject,
                    targetAnimator: null,
                    ghostAnimator: null);

                Assert.That(configuredGuard, Is.Null);
                Assert.That(existingGuard.enableDirectionRetarget, Is.False);
                Assert.That(existingGuard.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_ArmSwingGuardAssembly_When_InspectingOwnership_Then_CoordinatorOwnsIt()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                MethodInfo ensureServicesMethod = typeof(FBXVmdPipeline).GetMethod(
                    "EnsureServicesInitialized",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                    "ConfigureArmSwingLimitGuard",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo pipelineMethod = typeof(FBXVmdPipeline).GetMethod(
                    "ConfigureTargetArmSwingLimitCorrection",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo coordinatorField = typeof(FBXVmdPipeline).GetField(
                    "_conversionCoordinator",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(ensureServicesMethod, Is.Not.Null);
                ensureServicesMethod.Invoke(pipeline, null);
                Assert.That(coordinatorMethod, Is.Not.Null);
                Assert.That(pipelineMethod, Is.Null);
                Assert.That(coordinatorField, Is.Not.Null);
                Assert.That(coordinatorField.GetValue(pipeline), Is.TypeOf<FBXConversionCoordinator>());
            }
            finally
            {
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_ArmSleeveAnchorGuardAssembly_When_InspectingOwnership_Then_CoordinatorOwnsIt()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmSleeveAnchorGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineMethod = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureTargetArmSleeveAnchorCorrection",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineMethod, Is.Null);
        }

        [Test]
        public void Given_ArmVisualTwistGuardAssembly_When_InspectingOwnership_Then_CoordinatorOwnsIt()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmVisualTwistGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineMethod = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureTargetArmVisualTwistCorrection",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineMethod, Is.Null);
        }

        [Test]
        public void Given_ArmDeformationGuardAssembly_When_InspectingOwnership_Then_CoordinatorOwnsIt()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmDeformationGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineConfigureMethod = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureTargetArmDeformationGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineExclusionMethod = typeof(FBXVmdPipeline).GetMethod(
                "BuildLimbChildRotationExclusions",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineConfigureMethod, Is.Null);
            Assert.That(pipelineExclusionMethod, Is.Null);
        }

        [Test]
        public void Given_DefaultPipelineSettings_When_ConfiguringArmDeformationGuard_Then_AppliesSettings()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            GameObject targetObject = new GameObject("TargetWithDeformationGuard");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                var coordinator = new FBXConversionCoordinator(pipeline);

                ConfigureArmDeformationGuard(coordinator, targetObject);

                HumanoidArmDeformationGuard guard =
                    targetObject.GetComponent<HumanoidArmDeformationGuard>();
                Assert.That(guard, Is.Not.Null);
                Assert.That(guard.enabled, Is.True);
                Assert.That(guard.clampMusclesToHumanRange, Is.False);
                Assert.That(
                    guard.enableAnatomicalArmGuard,
                    Is.EqualTo(pipeline.targetGuardClampAnatomicalArmMuscles));
                Assert.That(
                    guard.clampArmStretchMuscles,
                    Is.EqualTo(pipeline.targetGuardClampArmStretchMuscles));
                Assert.That(guard.armStretchMuscleLimit, Is.EqualTo(pipeline.ArmStretchMuscleLimit));
                Assert.That(guard.upperArmTwistMuscleLimit, Is.EqualTo(pipeline.UpperArmTwistMuscleLimit));
                Assert.That(guard.lowerArmTwistMuscleLimit, Is.EqualTo(pipeline.LowerArmTwistMuscleLimit));
                Assert.That(
                    guard.lockHumanoidBonePositions,
                    Is.EqualTo(pipeline.ShouldLockTargetHumanoidBonePositions));
                Assert.That(
                    guard.lockLimbChildLocalPositions,
                    Is.EqualTo(pipeline.lockTargetLimbChildLocalPositions));
                Assert.That(
                    guard.lockLimbChildLocalRotations,
                    Is.EqualTo(pipeline.lockTargetLimbChildLocalRotations));
                Assert.That(guard.logCorrections, Is.EqualTo(pipeline.logArmDeformationGuardCorrections));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_ArmTwistRiggingAssembly_When_InspectingOwnership_Then_CoordinatorOwnsIt()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmTwistRiggingGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineConfigureMethod = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureTargetAnimationRiggingArmTwistCorrection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineDisableMethod = typeof(FBXVmdPipeline).GetMethod(
                "DisableTargetRigBuilder",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineConfigureMethod, Is.Null);
            Assert.That(pipelineDisableMethod, Is.Null);
        }

        [Test]
        public void Given_DisabledArmTwistRigging_When_ConfiguringTargetGuard_Then_DisablesExistingRigging()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            GameObject targetObject = new GameObject("TargetWithTwistRigging");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                var coordinator = new FBXConversionCoordinator(pipeline);
                HumanoidArmTwistRiggingGuard existingGuard =
                    targetObject.AddComponent<HumanoidArmTwistRiggingGuard>();
                existingGuard.enableTwistRigging = true;
                existingGuard.enabled = true;
                var rigBuilder =
                    targetObject.AddComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                rigBuilder.enabled = true;

                HumanoidArmTwistRiggingGuard configuredGuard = ConfigureArmTwistRiggingGuard(
                    coordinator,
                    targetObject,
                    targetAnimator: null);

                Assert.That(configuredGuard, Is.Null);
                Assert.That(existingGuard.enableTwistRigging, Is.False);
                Assert.That(existingGuard.enabled, Is.False);
                Assert.That(rigBuilder.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_TargetRetargetGuards_When_CheckingOwnership_Then_CoordinatorOrchestratesAssembly()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureTargetRetargetGuards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string pipelinePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string pipelineSource = File.ReadAllText(pipelinePath);

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureTargetRetargetGuards("));
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureArmTwistRiggingGuard("));
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureArmDirectionGuard("));
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureArmSwingLimitGuard("));
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureArmSleeveAnchorGuard("));
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureArmVisualTwistGuard("));
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureArmDeformationGuard("));
        }

        [Test]
        public void Given_TargetPlaybackPreparation_When_CheckingOwnership_Then_CoordinatorOwnsBaseStateAndLegacyIkCleanup()
        {
            MethodInfo prepareMethod = typeof(FBXConversionCoordinator).GetMethod(
                "PrepareTargetPlaybackState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo removeIkMethod = typeof(FBXConversionCoordinator).GetMethod(
                "RemoveLegacyIkControl",
                BindingFlags.Static | BindingFlags.NonPublic);
            string pipelinePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string pipelineSource = File.ReadAllText(pipelinePath);

            Assert.That(prepareMethod, Is.Not.Null);
            Assert.That(removeIkMethod, Is.Not.Null);
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.PrepareTargetPlaybackState("));
            Assert.That(pipelineSource, Does.Not.Contain("FBXConversionCoordinator.RemoveLegacyIkControl("));
            Assert.That(pipelineSource, Does.Not.Contain("targetAnimator.applyRootMotion = false"));
            Assert.That(pipelineSource, Does.Not.Contain("targetAnimator.runtimeAnimatorController = null"));
            Assert.That(pipelineSource, Does.Not.Contain("targetObject.GetComponent<IKControl>()"));
        }

        [Test]
        public void Given_TargetPreparationSequence_When_CheckingOwnership_Then_CoordinatorOwnsOrder()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "PrepareRetargetingTarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineMethod = typeof(FBXVmdPipeline).GetMethod(
                "PrepareTargetCharacter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string scriptsPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter");
            string pipelineSource = File.ReadAllText(Path.Combine(scriptsPath, "FBXVmdPipeline.cs"));
            string coordinatorSource = File.ReadAllText(Path.Combine(scriptsPath, "FBXConversionCoordinator.cs"));
            int methodStart = coordinatorSource.IndexOf(
                "internal void PrepareRetargetingTarget(",
                System.StringComparison.Ordinal);
            int methodEnd = methodStart < 0
                ? -1
                : coordinatorSource.IndexOf(
                    "internal void ConfigureTargetRetargetGuards(",
                    methodStart,
                    System.StringComparison.Ordinal);
            string methodSource = methodStart >= 0 && methodEnd > methodStart
                ? coordinatorSource.Substring(methodStart, methodEnd - methodStart)
                : string.Empty;

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineMethod, Is.Null);
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.PrepareRetargetingTarget("));
            Assert.That(methodSource.IndexOf("PrepareTargetPlaybackState(", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(methodSource.IndexOf("DisableMmdPostPoseCorrectionForRetarget(", System.StringComparison.Ordinal),
                Is.GreaterThan(methodSource.IndexOf("PrepareTargetPlaybackState(", System.StringComparison.Ordinal)));
            Assert.That(methodSource.IndexOf("ConfigureTargetRetargetGuards(", System.StringComparison.Ordinal),
                Is.GreaterThan(methodSource.IndexOf("DisableMmdPostPoseCorrectionForRetarget(", System.StringComparison.Ordinal)));
            Assert.That(methodSource.IndexOf("restoreIdlePoseBeforeRetargetBaselines?.Invoke()", System.StringComparison.Ordinal),
                Is.GreaterThan(methodSource.IndexOf("ConfigureTargetRetargetGuards(", System.StringComparison.Ordinal)));
            Assert.That(methodSource.IndexOf("RemoveLegacyIkControl(", System.StringComparison.Ordinal),
                Is.GreaterThan(methodSource.IndexOf("restoreIdlePoseBeforeRetargetBaselines?.Invoke()", System.StringComparison.Ordinal)));
            Assert.That(methodSource.IndexOf("ConfigureFinalIkFootGroundingExperiment(", System.StringComparison.Ordinal),
                Is.GreaterThan(methodSource.IndexOf("RemoveLegacyIkControl(", System.StringComparison.Ordinal)));
        }

        [Test]
        public void Given_TargetPreparation_When_RestoringIdleBaseline_Then_BaseStateIsAppliedBeforeCallback()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            GameObject targetObject = new GameObject("Target");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.enableYybArmDirectionRetargetCorrection = false;
                pipeline.enableYybArmSwingLimitCorrection = false;
                pipeline.enableYybArmSleeveAnchorCorrection = false;
                pipeline.enableYybArmVisualTwistCorrection = false;
                typeof(FBXVmdPipeline).GetField(
                    "_attachTargetArmDeformationGuard",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(pipeline, false);
                var coordinator = new FBXConversionCoordinator(pipeline);
                Animator targetAnimator = targetObject.AddComponent<Animator>();
                targetObject.transform.position = new Vector3(1f, 2f, 3f);
                targetAnimator.applyRootMotion = true;
                int callbackCount = 0;
                Vector3 callbackPosition = Vector3.one;
                bool callbackRootMotion = true;
                System.Action restoreIdlePose = () =>
                {
                    callbackCount++;
                    callbackPosition = targetObject.transform.position;
                    callbackRootMotion = targetAnimator.applyRootMotion;
                };
                MethodInfo prepareMethod = typeof(FBXConversionCoordinator).GetMethod(
                    "PrepareRetargetingTarget",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(prepareMethod, Is.Not.Null);
                prepareMethod.Invoke(coordinator, new object[]
                {
                    targetObject,
                    targetAnimator,
                    null,
                    false,
                    false,
                    restoreIdlePose
                });

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackPosition, Is.EqualTo(Vector3.zero));
                Assert.That(callbackRootMotion, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_MmdPostPoseCorrectionLifecycle_When_CheckingOwnership_Then_CoordinatorOwnsSnapshots()
        {
            MethodInfo disableMethod = typeof(FBXConversionCoordinator).GetMethod(
                "DisableMmdPostPoseCorrectionForRetarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo restoreMethod = typeof(FBXConversionCoordinator).GetMethod(
                "RestoreMmdPostPoseCorrectionForRetarget",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string pipelinePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string pipelineSource = File.ReadAllText(pipelinePath);

            Assert.That(disableMethod, Is.Not.Null);
            Assert.That(restoreMethod, Is.Not.Null);
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.DisableMmdPostPoseCorrectionForRetarget("));
            Assert.That(pipelineSource, Does.Contain("_conversionCoordinator?.RestoreMmdPostPoseCorrectionForRetarget("));
            Assert.That(pipelineSource, Does.Not.Contain("private struct BooleanFieldSnapshot"));
            Assert.That(pipelineSource, Does.Not.Contain("_retargetBooleanSnapshots"));
            Assert.That(pipelineSource, Does.Not.Contain("private bool TrySetBooleanField("));
            Assert.That(pipelineSource, Does.Not.Contain("private static FieldInfo FindFieldInHierarchy("));
        }

        [Test]
        public void Given_InheritedBooleanField_When_RestoringMmdPostPoseCorrection_Then_OriginalValueReturns()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                var coordinator = new FBXConversionCoordinator(pipeline);
                var probe = new MmdBooleanFieldProbe();
                MethodInfo setMethod = typeof(FBXConversionCoordinator).GetMethod(
                    "TrySetBooleanField",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo restoreMethod = typeof(FBXConversionCoordinator).GetMethod(
                    "RestoreMmdPostPoseCorrectionForRetarget",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(setMethod, Is.Not.Null);
                Assert.That(restoreMethod, Is.Not.Null);
                Assert.That(
                    (bool)setMethod.Invoke(coordinator, new object[] { probe, "pphShoulderEnabled", false }),
                    Is.True);
                Assert.That(probe.IsShoulderPostPoseEnabled, Is.False);

                restoreMethod.Invoke(coordinator, null);

                Assert.That(probe.IsShoulderPostPoseEnabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_FinalIkFootGroundingExperiment_When_CheckingOwnership_Then_CoordinatorOwnsAssembly()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureFinalIkFootGroundingExperiment",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo pipelineMethod = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureFinalIkFootGroundingExperiment",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string pipelinePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string pipelineSource = File.ReadAllText(pipelinePath);

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineMethod, Is.Null);
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.ConfigureFinalIkFootGroundingExperiment("));
            Assert.That(pipelineSource, Does.Not.Contain("targetObject.GetComponent<GrounderBipedIK>()"));
            Assert.That(pipelineSource, Does.Not.Contain("targetObject.GetComponent<BipedIK>()"));
        }

        [Test]
        public void Given_DisabledFinalIkFootGrounding_When_ConfiguringTarget_Then_DisablesExistingComponents()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            GameObject targetObject = new GameObject("TargetWithFinalIkGrounding");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.enableFinalIkFootGroundingExperiment = false;
                var coordinator = new FBXConversionCoordinator(pipeline);
                BipedIK bipedIk = targetObject.AddComponent<BipedIK>();
                bipedIk.fixTransforms = true;
                bipedIk.enabled = true;
                GrounderBipedIK grounder = targetObject.AddComponent<GrounderBipedIK>();
                grounder.weight = 0.2f;
                grounder.enabled = true;
                MethodInfo configureMethod = typeof(FBXConversionCoordinator).GetMethod(
                    "ConfigureFinalIkFootGroundingExperiment",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(configureMethod, Is.Not.Null);
                configureMethod.Invoke(coordinator, new object[] { targetObject });

                Assert.That(grounder.weight, Is.Zero);
                Assert.That(grounder.enabled, Is.False);
                Assert.That(bipedIk.fixTransforms, Is.False);
                Assert.That(bipedIk.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_TargetSessionReset_When_CheckingOwnership_Then_CoordinatorOwnsGuardBaselineRecapture()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "RecaptureTargetGuardBaselines",
                BindingFlags.Static | BindingFlags.NonPublic);
            string pipelinePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string pipelineSource = File.ReadAllText(pipelinePath);
            int resetMethodStart = pipelineSource.IndexOf(
                "internal void ResetTargetStateAfterSession(",
                System.StringComparison.Ordinal);
            int nextMethodStart = pipelineSource.IndexOf(
                "private GameObject CreateGhostContainer(",
                resetMethodStart,
                System.StringComparison.Ordinal);
            string resetMethodSource = pipelineSource.Substring(
                resetMethodStart,
                nextMethodStart - resetMethodStart);

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(resetMethodSource, Does.Contain("FBXConversionCoordinator.RecaptureTargetGuardBaselines("));
            Assert.That(resetMethodSource, Does.Not.Contain("targetCharacter.GetComponent<HumanoidArmDeformationGuard>()"));
            Assert.That(resetMethodSource, Does.Not.Contain("targetCharacter.GetComponent<HumanoidThumbDeformationGuard>()"));
        }

        [Test]
        public void Given_RetargeterAssembly_When_CheckingOwnership_Then_CoordinatorCreatesAndInitializesComponent()
        {
            MethodInfo coordinatorMethod = typeof(FBXConversionCoordinator).GetMethod(
                "CreateRetargeter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));

            Assert.That(coordinatorMethod, Is.Not.Null);
            Assert.That(pipelineSource, Does.Not.Contain("_conversionCoordinator.CreateRetargeter("));
            Assert.That(pipelineSource, Does.Not.Contain("importedModel.AddComponent<PoseSpaceRetargeter>()"));
            Assert.That(pipelineSource, Does.Not.Contain("new RetargetingContext("));
            Assert.That(pipelineSource, Does.Not.Contain("RetargetingSettings.CreateSnapshot(this)"));
            Assert.That(pipelineSource, Does.Not.Contain("retargeter.Initialize(context, settings)"));
        }

        [Test]
        public void Given_ConversionEntry_When_CheckingOwnership_Then_PipelineDispatchesThroughCoordinator()
        {
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));
            string coordinatorSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXConversionCoordinator.cs"));
            int entryStart = pipelineSource.IndexOf(
                "internal async void ProcessFBXAsync(string sourcePath)",
                System.StringComparison.Ordinal);
            int entryEnd = pipelineSource.IndexOf(
                "internal void BeginConversionSession()",
                entryStart,
                System.StringComparison.Ordinal);
            string entrySource = pipelineSource.Substring(entryStart, entryEnd - entryStart);

            Assert.That(entrySource, Does.Contain("EnsureServicesInitialized();"));
            Assert.That(entrySource, Does.Contain("await _conversionCoordinator.ConvertAsync("));
            Assert.That(entrySource, Does.Contain("new FBXConversionRequest(sourcePath)"));
            Assert.That(entrySource, Does.Not.Contain("await ProcessFBXSessionAsync(sourcePath)"));
            Assert.That(coordinatorSource, Does.Contain("return await RunSessionAsync(request);"));
        }

        [Test]
        public void Given_SessionUseCase_When_CheckingOwnership_Then_CoordinatorOwnsOrderedFlow()
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo sessionMethod = typeof(FBXVmdPipeline).GetMethod(
                "ProcessFBXSessionAsync",
                privateInstance);
            MethodInfo prepareGhostMethod = typeof(FBXVmdPipeline).GetMethod(
                "PrepareGhostModel",
                privateInstance);
            MethodInfo prepareRetargeterMethod = typeof(FBXVmdPipeline).GetMethod(
                "PrepareRetargeterForRecording",
                privateInstance);
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));
            string coordinatorSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXConversionCoordinator.cs"));
            int sessionStart = coordinatorSource.IndexOf(
                "public async Task<FBXConversionResult> RunSessionAsync(FBXConversionRequest request)",
                System.StringComparison.Ordinal);
            string sessionSource = sessionStart >= 0
                ? coordinatorSource.Substring(sessionStart)
                : string.Empty;

            Assert.That(sessionMethod, Is.Null);
            Assert.That(prepareGhostMethod, Is.Not.Null);
            Assert.That(prepareRetargeterMethod, Is.Not.Null);
            Assert.That(pipelineSource, Does.Contain("new FBXConversionCoordinator(this, _importController)"));
            Assert.That(coordinatorSource, Does.Not.Contain("_pipeline.ProcessFBXSessionAsync("));

            string[] orderedCalls =
            {
                "_pipeline.BeginConversionSession();",
                "ImportRuntimeModelAsync(",
                "_pipeline.PrepareGhostModel(",
                "TryPrepareRuntimeAnimation(",
                "TryResolveTargetAnimator(",
                "PrepareRetargetingTarget(",
                "CreateRetargeter(",
                "_pipeline.PrepareRetargeterForRecording(",
                "_pipeline.DispatchRecording("
            };
            int previousCallIndex = -1;
            foreach (string orderedCall in orderedCalls)
            {
                int currentCallIndex = sessionSource.IndexOf(
                    orderedCall,
                    System.StringComparison.Ordinal);
                Assert.That(currentCallIndex, Is.GreaterThan(previousCallIndex), orderedCall);
                previousCallIndex = currentCallIndex;
            }

            int retargeterBridgeStart = pipelineSource.IndexOf(
                "internal void PrepareRetargeterForRecording(",
                System.StringComparison.Ordinal);
            int retargeterBridgeEnd = pipelineSource.IndexOf(
                "internal void DispatchRecording(",
                retargeterBridgeStart,
                System.StringComparison.Ordinal);
            string retargeterBridgeSource = pipelineSource.Substring(
                retargeterBridgeStart,
                retargeterBridgeEnd - retargeterBridgeStart);
            Assert.That(
                retargeterBridgeSource.IndexOf("ConfigureTargetThumbDeformationGuard(", System.StringComparison.Ordinal),
                Is.GreaterThan(retargeterBridgeSource.IndexOf("_activeRetargeter = retargeter;", System.StringComparison.Ordinal)));
            Assert.That(
                retargeterBridgeSource.IndexOf("ConfigureEditorHumanoidMuscleReference(", System.StringComparison.Ordinal),
                Is.GreaterThan(retargeterBridgeSource.IndexOf("ConfigureTargetThumbDeformationGuard(", System.StringComparison.Ordinal)));
            Assert.That(
                retargeterBridgeSource.IndexOf("SetSessionState(FBXSessionState.GhostReady", System.StringComparison.Ordinal),
                Is.GreaterThan(retargeterBridgeSource.IndexOf("ConfigureEditorHumanoidMuscleReference(", System.StringComparison.Ordinal)));

            int dispatchEnd = pipelineSource.IndexOf(
                "internal static bool PrepareRetargeterRecordingStartPose(",
                retargeterBridgeEnd,
                System.StringComparison.Ordinal);
            string dispatchSource = pipelineSource.Substring(
                retargeterBridgeEnd,
                dispatchEnd - retargeterBridgeEnd);
            Assert.That(
                dispatchSource.IndexOf("StartCoroutine(_recordingController.RecordAsync(", System.StringComparison.Ordinal),
                Is.GreaterThan(dispatchSource.IndexOf("ghostAnimation.Stop();", System.StringComparison.Ordinal)));
        }

        [Test]
        public async Task Given_StandaloneCoordinator_When_RunningMissingFileSession_Then_DelegatesToPipelineOwner()
        {
            GameObject pipelineObject = new GameObject("Pipeline");
            string missingPath = Path.Combine(
                Path.GetTempPath(),
                $"missing-{System.Guid.NewGuid():N}.fbx");
            try
            {
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                MethodInfo ensureServicesMethod = typeof(FBXVmdPipeline).GetMethod(
                    "EnsureServicesInitialized",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo coordinatorField = typeof(FBXVmdPipeline).GetField(
                    "_conversionCoordinator",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                string coordinatorSource = File.ReadAllText(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets",
                    "_Project",
                    "Scripts",
                    "FBXImporter",
                    "FBXConversionCoordinator.cs"));

                Assert.That(ensureServicesMethod, Is.Not.Null);
                Assert.That(coordinatorField, Is.Not.Null);
                ensureServicesMethod.Invoke(pipeline, null);
                var pipelineCoordinator = (FBXConversionCoordinator)coordinatorField.GetValue(pipeline);
                var standaloneCoordinator = new FBXConversionCoordinator(pipeline);

                Assert.That(pipelineCoordinator, Is.Not.Null);
                Assert.That(standaloneCoordinator, Is.Not.SameAs(pipelineCoordinator));
                Assert.That(coordinatorSource, Does.Contain("!ReferenceEquals(registeredCoordinator, this)"));

                string errorMessage = $"FBX 파일을 찾을 수 없습니다: {missingPath}";
                LogAssert.Expect(
                    LogType.Error,
                    $"[FBXImport] FBX 처리 실패함. 메시지={errorMessage}");

                FBXConversionResult result = await standaloneCoordinator.RunSessionAsync(
                    new FBXConversionRequest(missingPath));

                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.ErrorMessage, Is.EqualTo(errorMessage));
                Assert.That(pipeline.IsProcessing, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_UnusedPipelineHelpers_When_CheckingGodClassSurface_Then_LegacyMethodsAreAbsent()
        {
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

            Assert.That(typeof(FBXVmdPipeline).GetMethod("ValidateBoneMapping", privateInstance), Is.Null);
            Assert.That(typeof(FBXVmdPipeline).GetMethod("SetupGhostRetargeting", privateInstance), Is.Null);
            Assert.That(typeof(FBXVmdPipeline).GetMethod("GetHipsHeight", privateInstance), Is.Null);
        }

        private bool TryResolveTargetAnimator(
            GameObject targetObject,
            out Animator targetAnimator,
            out string errorMessage)
        {
            MethodInfo method = typeof(FBXConversionCoordinator).GetMethod(
                "TryResolveTargetAnimator",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { targetObject, null, string.Empty };
            bool resolved = (bool)method.Invoke(null, arguments);
            targetAnimator = (Animator)arguments[1];
            errorMessage = (string)arguments[2];
            return resolved;
        }

        private HumanoidArmDirectionRetargetGuard ConfigureArmDirectionGuard(
            FBXConversionCoordinator coordinator,
            GameObject targetObject,
            Animator targetAnimator,
            Animator ghostAnimator)
        {
            MethodInfo method = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmDirectionGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments =
            {
                targetObject,
                targetAnimator,
                ghostAnimator
            };
            return (HumanoidArmDirectionRetargetGuard)method.Invoke(coordinator, arguments);
        }

        private void ConfigureArmDeformationGuard(
            FBXConversionCoordinator coordinator,
            GameObject targetObject)
        {
            MethodInfo method = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmDeformationGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { targetObject, null, null, null };
            method.Invoke(coordinator, arguments);
        }

        private HumanoidArmTwistRiggingGuard ConfigureArmTwistRiggingGuard(
            FBXConversionCoordinator coordinator,
            GameObject targetObject,
            Animator targetAnimator)
        {
            MethodInfo method = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmTwistRiggingGuard",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { targetObject, targetAnimator };
            return (HumanoidArmTwistRiggingGuard)method.Invoke(coordinator, arguments);
        }

        private class MmdBooleanFieldProbeBase
        {
            private bool pphShoulderEnabled = true;

            public bool IsShoulderPostPoseEnabled => pphShoulderEnabled;
        }

        private sealed class MmdBooleanFieldProbe : MmdBooleanFieldProbeBase
        {
        }
    }
}
