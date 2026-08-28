using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

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
            Assert.That(retargeterSource, Does.Contain("ThumbTransformNamePolicy.IsActiveBaseSource("));
            Assert.That(guardSource, Does.Not.Contain("private static bool IsThumbBaseHelperName("));
            Assert.That(guardSource, Does.Not.Contain("private static bool IsActiveThumbBaseSourceName("));
            Assert.That(retargeterSource, Does.Not.Contain("private static bool IsActiveThumbBaseSourceName("));
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
