using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class LegacyAnimationDriverLifecycleTests
    {
        private static readonly Type DriverType =
            typeof(Fbx2Vmd.FBXImporter.PoseSpaceRetargeter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.LegacyAnimationDriver", throwOnError: true);

        [Test]
        public void Given_AddedAnimationAndEnabledAnimator_When_InitializingAndDisposing_Then_RemovesOwnedComponentAndRestoresAnimator()
        {
            var root = new GameObject("Legacy Animation Driver Added Component Test");
            object driver = CreateDriver();
            AnimationClip clip = CreateNonLegacyClip();

            try
            {
                Animator animator = root.AddComponent<Animator>();

                Invoke(driver, "Initialize", root, animator, clip);

                Assert.That(root.GetComponent<Animation>(), Is.Not.Null);
                Assert.That(animator.enabled, Is.False);
                Assert.That((bool)Invoke(driver, "TryPrepareRecordingStartPose", 0.5f, 1f, false), Is.True);
                Assert.That((float)GetProperty(driver, "CurrentTime"), Is.EqualTo(0.5f).Within(0.001f));

                Invoke(driver, "Dispose");

                Assert.That(root.GetComponent<Animation>(), Is.Null);
                Assert.That(animator.enabled, Is.True);

                Invoke(driver, "Dispose");

                Assert.That(root.GetComponent<Animation>(), Is.Null);
                Assert.That(animator.enabled, Is.True);
            }
            finally
            {
                Invoke(driver, "Dispose");
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Given_ExistingAnimationAndDisabledAnimator_When_InitializingAndDisposing_Then_PreservesExistingComponentState()
        {
            var root = new GameObject("Legacy Animation Driver Existing Component Test");
            object driver = CreateDriver();
            AnimationClip clip = CreateLegacyClip();

            try
            {
                Animation existingAnimation = root.AddComponent<Animation>();
                Animator animator = root.AddComponent<Animator>();
                animator.enabled = false;

                Invoke(driver, "Initialize", root, animator, clip);
                Invoke(driver, "Tick", 0f, false, false, 30f);
                Invoke(driver, "ResetStabilityMetrics");
                Invoke(driver, "Dispose");

                Assert.That(root.GetComponent<Animation>(), Is.SameAs(existingAnimation));
                Assert.That(animator.enabled, Is.False);
                Assert.That(existingAnimation["__PoseSpaceRetargeter_GhostClip"], Is.Null);
                Assert.That(float.IsNaN((float)GetProperty(driver, "LastStep")), Is.True);
                Assert.That((int)GetProperty(driver, "StepSpikeCount"), Is.Zero);

                Invoke(driver, "Dispose");

                Assert.That(root.GetComponent<Animation>(), Is.SameAs(existingAnimation));
                Assert.That(animator.enabled, Is.False);
                Assert.That(existingAnimation["__PoseSpaceRetargeter_GhostClip"], Is.Null);
            }
            finally
            {
                Invoke(driver, "Dispose");
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static AnimationClip CreateLegacyClip()
        {
            var clip = new AnimationClip { legacy = true };
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        private static AnimationClip CreateNonLegacyClip()
        {
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            return clip;
        }

        private static object CreateDriver()
        {
            return Activator.CreateInstance(DriverType);
        }

        private static object Invoke(object driver, string methodName, params object[] arguments)
        {
            MethodInfo method = DriverType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, $"LegacyAnimationDriver must expose {methodName}.");
            return method.Invoke(driver, arguments);
        }

        private static object GetProperty(object driver, string propertyName)
        {
            PropertyInfo property = DriverType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"LegacyAnimationDriver must expose {propertyName}.");
            return property.GetValue(driver);
        }
    }
}
