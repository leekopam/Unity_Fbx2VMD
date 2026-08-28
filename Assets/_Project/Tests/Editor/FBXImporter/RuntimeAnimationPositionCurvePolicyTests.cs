using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class RuntimeAnimationPositionCurvePolicyTests
    {
        private const string PolicyTypeName =
            "Fbx2Vmd.FBXImporter.RuntimeAnimationPositionCurvePolicy";

        [Test]
        public void Given_RuntimeAnimationImport_When_CheckingOwnership_Then_DelegatesPositionCurvePolicy()
        {
            Type policyType = typeof(AssimpFBXImporter).Assembly.GetType(PolicyTypeName);
            string importerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpFBXImporter.cs"));

            Assert.That(policyType, Is.Not.Null);
            Assert.That(importerSource, Does.Contain("RuntimeAnimationPositionCurvePolicy.ShouldImport("));
            Assert.That(importerSource, Does.Contain("string positionCurveNodeName = targetNode != null"));
            Assert.That(importerSource, Does.Contain(": Path.GetFileName(relativePath);"));
            Assert.That(importerSource, Does.Not.Contain("private static bool ShouldImportPositionCurves("));
        }

        [TestCase(null, "Spine", true)]
        [TestCase("", null, true)]
        [TestCase("Armature/Root", "mixamorig:Root", true)]
        [TestCase("Armature/Hips", "mixamo_Hips", true)]
        [TestCase("Armature/Hips", "mixamo Hips", true)]
        [TestCase("Armature/Pelvis", "Pelvis", true)]
        [TestCase("Armature/Center", "Center", true)]
        [TestCase("Armature/Groove", "Groove", true)]
        [TestCase("Armature/Spine", "Spine", false)]
        [TestCase("Armature/Spine", null, false)]
        [TestCase("Armature/Spine", "", false)]
        public void Given_NodeName_When_Evaluating_Then_ImportsOnlyRootMotionCandidates(
            string relativePath,
            string nodeName,
            bool expected)
        {
            MethodInfo shouldImportMethod = FindPolicyMethod();

            bool actual = (bool)shouldImportMethod.Invoke(
                null,
                new object[] { relativePath, nodeName });

            Assert.That(actual, Is.EqualTo(expected));
        }

        private static MethodInfo FindPolicyMethod()
        {
            Type policyType = typeof(AssimpFBXImporter).Assembly.GetType(PolicyTypeName);
            Assert.That(policyType, Is.Not.Null);

            MethodInfo method = policyType.GetMethod(
                "ShouldImport",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(string) },
                modifiers: null);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
