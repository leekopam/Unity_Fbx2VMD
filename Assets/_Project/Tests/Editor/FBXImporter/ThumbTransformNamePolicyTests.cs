using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class ThumbTransformNamePolicyTests
    {
        private const string PolicyTypeName =
            "Fbx2Vmd.FBXImporter.ThumbTransformNamePolicy";

        [Test]
        public void Given_ThumbTransformDiscovery_When_CheckingOwnership_Then_UsesSharedNamePolicy()
        {
            Type policyType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(PolicyTypeName);
            string guardSource = ReadRuntimeSource("HumanoidThumbDeformationGuard.cs");
            string retargeterSource = ReadRuntimeSource("PoseSpaceRetargeter.cs");

            Assert.That(policyType, Is.Not.Null);
            Assert.That(guardSource, Does.Contain("ThumbTransformNamePolicy.IsBaseHelper("));
            Assert.That(guardSource, Does.Contain("ThumbTransformNamePolicy.IsActiveBaseSource("));
            Assert.That(guardSource, Does.Contain("ThumbTransformNamePolicy.TryResolveSide("));
            Assert.That(retargeterSource, Does.Contain("ThumbTransformNamePolicy.IsActiveBaseSource("));
            Assert.That(retargeterSource, Does.Contain("ThumbTransformNamePolicy.IsDetachedBaseHelper("));
            Assert.That(guardSource, Does.Not.Contain("private static bool IsThumbBaseHelperName("));
            Assert.That(guardSource, Does.Not.Contain("private static bool IsActiveThumbBaseSourceName("));
            Assert.That(guardSource, Does.Not.Contain("private static bool TryResolveThumbSideFromName("));
            Assert.That(retargeterSource, Does.Not.Contain("private static bool IsActiveThumbBaseSourceName("));
            Assert.That(retargeterSource, Does.Not.Contain("private static bool IsDetachedThumbBaseHelperName("));
            Assert.That(retargeterSource, Does.Not.Contain("private static bool IsThumbBaseName("));
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("joint_LeftThumb0", true)]
        [TestCase("joint-Right.Thumb 0", true)]
        [TestCase("joint_LeftThumb0M", true)]
        [TestCase("joint_LeftThumb0_Thumb1", false)]
        [TestCase("joint_LeftThumb0_Thumb2", false)]
        [TestCase("joint_LeftThumb0_Thumb3", false)]
        [TestCase("joint_LeftThumb0_Proximal", false)]
        [TestCase("joint_LeftThumb0_Intermediate", false)]
        [TestCase("joint_LeftThumb0_Distal", false)]
        [TestCase("joint_LeftThumb0_ThumbTip", false)]
        public void Given_TransformName_When_CheckingBaseHelper_Then_PreservesClassification(
            string transformName,
            bool expected)
        {
            bool actual = InvokePolicy("IsBaseHelper", transformName);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("joint_LeftThumb0M", true)]
        [TestCase("JOINT_RIGHTTHUMB0M", true)]
        [TestCase("ghost_LeftThumb0M", false)]
        [TestCase("joint_LeftThumb0M_Thumb1", false)]
        [TestCase("joint_LeftThumb0M_Thumb2", false)]
        [TestCase("joint_LeftThumb0M_ThumbTip", false)]
        public void Given_TransformName_When_CheckingActiveBaseSource_Then_PreservesClassification(
            string transformName,
            bool expected)
        {
            bool actual = InvokePolicy("IsActiveBaseSource", transformName);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("joint_LeftThumb0", true)]
        [TestCase("JOINT_RIGHTTHUMB0", true)]
        [TestCase("!joint_LeftThumb0", false)]
        [TestCase("ghost_LeftThumb0", false)]
        [TestCase("joint_LeftThumb0M", false)]
        [TestCase("joint_LeftThumb0_Thumb1", false)]
        [TestCase("joint_LeftThumb0_Thumb2", false)]
        [TestCase("joint_LeftThumb0_ThumbTip", false)]
        [TestCase("joint_LeftThumb0_Thumb3", true)]
        [TestCase("joint_LeftThumb0_Proximal", true)]
        [TestCase("joint_LeftThumb0_Intermediate", true)]
        [TestCase("joint_LeftThumb0_Distal", true)]
        [TestCase("joint_LeftThumb-0", false)]
        [TestCase("joint_LeftThumb 0", false)]
        public void Given_TransformName_When_CheckingDetachedBaseHelper_Then_PreservesClassification(
            string transformName,
            bool expected)
        {
            bool actual = InvokePolicy("IsDetachedBaseHelper", transformName);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Given_PoseReferenceHierarchy_When_FindingHelper_Then_PreservesFirstMatchingCandidate()
        {
            var root = new GameObject("ThumbHelperPolicyRoot");
            try
            {
                Animator animator = root.AddComponent<Animator>();
                PoseSpaceRetargeter retargeter = root.AddComponent<PoseSpaceRetargeter>();
                retargeter.targetAnimator = animator;
                AddChild(root.transform, "joint_LeftThumb0M");
                AddChild(root.transform, "ghost_LeftThumb0");
                Transform expected = AddChild(root.transform, "joint_LeftThumb0_Proximal");
                AddChild(root.transform, "joint_LeftThumb0");

                MethodInfo method = typeof(PoseSpaceRetargeter).GetMethod(
                    "FindThumbBaseHelperByName",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);

                Transform actual = (Transform)method.Invoke(retargeter, new object[] { true });

                Assert.That(actual, Is.SameAs(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase(null, false, false)]
        [TestCase("", false, false)]
        [TestCase("joint_RightThumb0", true, true)]
        [TestCase("joint_LeftThumb0", true, false)]
        [TestCase("joint_RThumb1", true, true)]
        [TestCase("joint_LThumb1", true, false)]
        [TestCase("joint_thumb_r", true, true)]
        [TestCase("joint_thumb_l", true, false)]
        [TestCase("joint.rThumb", true, true)]
        [TestCase("joint.lThumb", true, false)]
        [TestCase("joint_Left_RightThumb0", true, true)]
        [TestCase("joint_thumb0_root", true, true)]
        [TestCase("joint_Thumb0", false, false)]
        public void Given_TransformName_When_ResolvingSide_Then_PreservesClassification(
            string transformName,
            bool expectedResolved,
            bool expectedIsRight)
        {
            bool resolved = TryResolveSide(transformName, out bool isRight);

            Assert.That(resolved, Is.EqualTo(expectedResolved));
            Assert.That(isRight, Is.EqualTo(expectedIsRight));
        }

        private static bool InvokePolicy(string methodName, string transformName)
        {
            Type policyType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(PolicyTypeName);
            Assert.That(policyType, Is.Not.Null);

            MethodInfo method = policyType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);
            Assert.That(method, Is.Not.Null, methodName);
            return (bool)method.Invoke(null, new object[] { transformName });
        }

        private static bool TryResolveSide(string transformName, out bool isRight)
        {
            Type policyType = typeof(HumanoidThumbDeformationGuard).Assembly.GetType(PolicyTypeName);
            Assert.That(policyType, Is.Not.Null);

            MethodInfo method = policyType.GetMethod(
                "TryResolveSide",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object[] arguments = { transformName, false };
            bool resolved = (bool)method.Invoke(null, arguments);
            isRight = (bool)arguments[1];
            return resolved;
        }

        private static Transform AddChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static string ReadRuntimeSource(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                fileName));
        }
    }
}
