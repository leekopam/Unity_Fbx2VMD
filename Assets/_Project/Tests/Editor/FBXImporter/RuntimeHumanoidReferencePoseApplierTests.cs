using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RuntimeHumanoidReferencePoseApplierTests
    {
        private const string ApplierTypeName =
            "Fbx2Vmd.FBXImporter.RuntimeHumanoidReferencePoseApplier";

        [Test]
        public void Given_AnimatedHierarchy_When_ApplyingReferencePose_Then_UsesFirstClipSample()
        {
            Type applierType = typeof(AssimpFBXImporter).Assembly.GetType(ApplierTypeName);
            Assert.That(applierType, Is.Not.Null);

            MethodInfo tryApplyMethod = applierType.GetMethod(
                "TryApply",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(tryApplyMethod, Is.Not.Null);

            GameObject root = new GameObject("runtime-reference-root");
            GameObject bone = new GameObject("Bone");
            bone.transform.SetParent(root.transform, false);
            AnimationClip clip = CreateReferencePoseClip();

            try
            {
                root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                bool applied = (bool)tryApplyMethod.Invoke(null, new object[] { root, clip });

                Assert.That(applied, Is.True);
                Assert.That(root.transform.eulerAngles.y, Is.EqualTo(180f).Within(0.001f));
                Assert.That(bone.transform.localPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(
                    Quaternion.Angle(bone.transform.localRotation, Quaternion.Euler(10f, 20f, 30f)),
                    Is.LessThan(0.01f));
                Assert.That(bone.transform.localScale, Is.EqualTo(new Vector3(1.1f, 1.2f, 1.3f)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Given_MissingRootOrClip_When_ApplyingReferencePose_Then_ReturnsFalse()
        {
            Type applierType = typeof(AssimpFBXImporter).Assembly.GetType(ApplierTypeName);
            Assert.That(applierType, Is.Not.Null);

            MethodInfo tryApplyMethod = applierType.GetMethod(
                "TryApply",
                BindingFlags.Static | BindingFlags.NonPublic);
            GameObject root = new GameObject("runtime-reference-root");
            AnimationClip clip = new AnimationClip { legacy = true };

            try
            {
                Assert.That(
                    (bool)tryApplyMethod.Invoke(null, new object[] { null, clip }),
                    Is.False);
                Assert.That(
                    (bool)tryApplyMethod.Invoke(null, new object[] { root, null }),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static AnimationClip CreateReferencePoseClip()
        {
            var clip = new AnimationClip { legacy = true };
            SetVector3Curves(
                clip,
                "localPosition",
                new Vector3(1f, 2f, 3f),
                new Vector3(4f, 5f, 6f));
            SetQuaternionCurves(
                clip,
                Quaternion.Euler(10f, 20f, 30f),
                Quaternion.Euler(40f, 50f, 60f));
            SetVector3Curves(
                clip,
                "localScale",
                new Vector3(1.1f, 1.2f, 1.3f),
                new Vector3(1.4f, 1.5f, 1.6f));
            return clip;
        }

        private static void SetVector3Curves(
            AnimationClip clip,
            string propertyPrefix,
            Vector3 first,
            Vector3 second)
        {
            clip.SetCurve("Bone", typeof(Transform), $"{propertyPrefix}.x", CreateCurve(first.x, second.x));
            clip.SetCurve("Bone", typeof(Transform), $"{propertyPrefix}.y", CreateCurve(first.y, second.y));
            clip.SetCurve("Bone", typeof(Transform), $"{propertyPrefix}.z", CreateCurve(first.z, second.z));
        }

        private static void SetQuaternionCurves(
            AnimationClip clip,
            Quaternion first,
            Quaternion second)
        {
            clip.SetCurve("Bone", typeof(Transform), "localRotation.x", CreateCurve(first.x, second.x));
            clip.SetCurve("Bone", typeof(Transform), "localRotation.y", CreateCurve(first.y, second.y));
            clip.SetCurve("Bone", typeof(Transform), "localRotation.z", CreateCurve(first.z, second.z));
            clip.SetCurve("Bone", typeof(Transform), "localRotation.w", CreateCurve(first.w, second.w));
        }

        private static AnimationCurve CreateCurve(float first, float second)
        {
            return new AnimationCurve(
                new Keyframe(0f, first),
                new Keyframe(1f, second));
        }
    }
}
