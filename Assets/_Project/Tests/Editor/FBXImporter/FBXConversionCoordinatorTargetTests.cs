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
            GameObject targetObject = new GameObject("TargetWithDirectionGuard");
            try
            {
                HumanoidArmDirectionRetargetGuard existingGuard =
                    targetObject.AddComponent<HumanoidArmDirectionRetargetGuard>();
                existingGuard.enableDirectionRetarget = true;
                existingGuard.enabled = true;

                HumanoidArmDirectionRetargetGuard configuredGuard = ConfigureArmDirectionGuard(
                    targetObject,
                    targetAnimator: null,
                    ghostAnimator: null,
                    shouldEnable: false);

                Assert.That(configuredGuard, Is.Null);
                Assert.That(existingGuard.enableDirectionRetarget, Is.False);
                Assert.That(existingGuard.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
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
            GameObject targetObject,
            Animator targetAnimator,
            Animator ghostAnimator,
            bool shouldEnable)
        {
            MethodInfo method = typeof(FBXConversionCoordinator).GetMethod(
                "ConfigureArmDirectionGuard",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments =
            {
                targetObject,
                targetAnimator,
                ghostAnimator,
                shouldEnable,
                0.65f,
                0.75f,
                65f,
                85f,
                1f,
                1f,
                false
            };
            return (HumanoidArmDirectionRetargetGuard)method.Invoke(null, arguments);
        }
    }
}
