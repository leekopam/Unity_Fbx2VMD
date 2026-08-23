using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using UnityEngine;

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
            Assert.That(pipelineSource, Does.Contain("_conversionCoordinator.ConfigureTargetRetargetGuards("));
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
            Assert.That(pipelineSource, Does.Contain("_conversionCoordinator.PrepareTargetPlaybackState("));
            Assert.That(pipelineSource, Does.Contain("FBXConversionCoordinator.RemoveLegacyIkControl("));
            Assert.That(pipelineSource, Does.Not.Contain("targetAnimator.applyRootMotion = false"));
            Assert.That(pipelineSource, Does.Not.Contain("targetAnimator.runtimeAnimatorController = null"));
            Assert.That(pipelineSource, Does.Not.Contain("targetObject.GetComponent<IKControl>()"));
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
            Assert.That(pipelineSource, Does.Contain("_conversionCoordinator.DisableMmdPostPoseCorrectionForRetarget("));
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
