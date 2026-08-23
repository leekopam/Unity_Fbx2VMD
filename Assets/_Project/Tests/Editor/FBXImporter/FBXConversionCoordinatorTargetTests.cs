using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
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
    }
}
