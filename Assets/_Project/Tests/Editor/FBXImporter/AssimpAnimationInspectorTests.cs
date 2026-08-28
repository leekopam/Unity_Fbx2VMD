using Assimp;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class AssimpAnimationInspectorTests
    {
        [Test]
        public void Given_AnimationInspector_When_CheckingOwnership_Then_ImporterKeepsPublicFacadeOnly()
        {
            Type inspectorType = typeof(AssimpFBXImporter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.AssimpAnimationInspector");
            MethodInfo inspectMethod = inspectorType?.GetMethod(
                "Inspect",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string), typeof(PostProcessSteps) },
                modifiers: null);
            string importerSource = ReadFbxImporterSource("AssimpFBXImporter.cs");
            string inspectorPath = GetFbxImporterPath("AssimpAnimationInspector.cs");

            Assert.That(inspectorType, Is.Not.Null);
            Assert.That(inspectMethod, Is.Not.Null);
            Assert.That(inspectMethod.ReturnType, Is.EqualTo(typeof(AssimpFBXImporter.AnimationInspectionReport)));
            Assert.That(File.Exists(inspectorPath), Is.True);
            Assert.That(
                importerSource,
                Does.Contain("return AssimpAnimationInspector.Inspect(path, BuildAssimpPostProcessSteps());"));
            Assert.That(importerSource, Does.Not.Contain("CalculateAnimationDurationSeconds("));
        }

        [Test]
        public void Given_PublicInspectionFacade_When_InspectingSignature_Then_PreservesNestedReportContract()
        {
            MethodInfo inspectMethod = typeof(AssimpFBXImporter).GetMethod(
                "InspectAnimationFile",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(inspectMethod, Is.Not.Null);
            Assert.That(inspectMethod.ReturnType, Is.EqualTo(typeof(AssimpFBXImporter.AnimationInspectionReport)));
            Assert.That(typeof(AssimpFBXImporter.AnimationInspectionReport).IsNestedPublic, Is.True);
            Assert.That(typeof(AssimpFBXImporter.AnimationInspectionReport).IsSealed, Is.True);
        }

        [Test]
        public void Given_MissingFbxPath_When_InspectingAnimation_Then_ReturnsUnreadableReport()
        {
            string missingPath = Path.Combine(
                Path.GetTempPath(),
                Guid.NewGuid().ToString("N"),
                "missing.fbx");

            AssimpFBXImporter.AnimationInspectionReport report =
                AssimpFBXImporter.InspectAnimationFile(missingPath);

            Assert.That(report.FileReadable, Is.False);
            Assert.That(report.ImportSucceeded, Is.False);
            Assert.That(report.ErrorMessage, Is.EqualTo($"FBX file not found: {missingPath}"));
            Assert.That(report.AnimationCount, Is.Zero);
            Assert.That(report.AnimationNames, Is.Empty);
            Assert.That(report.AnimationLengthsSeconds, Is.Empty);
        }

        [Test]
        public void Given_AnimationTicks_When_CalculatingDuration_Then_PreservesFallbackAndValidation()
        {
            Type inspectorType = typeof(AssimpFBXImporter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.AssimpAnimationInspector");
            MethodInfo calculateMethod = inspectorType?.GetMethod(
                "CalculateDurationSeconds",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Assimp.Animation) },
                modifiers: null);

            Assert.That(calculateMethod, Is.Not.Null);

            var missingRate = new Assimp.Animation
            {
                DurationInTicks = 120d,
                TicksPerSecond = 0d
            };
            var explicitRate = new Assimp.Animation
            {
                DurationInTicks = 120d,
                TicksPerSecond = 30d
            };
            var invalidDuration = new Assimp.Animation
            {
                DurationInTicks = -1d,
                TicksPerSecond = 30d
            };
            var overflowingDuration = new Assimp.Animation
            {
                DurationInTicks = double.MaxValue,
                TicksPerSecond = 60d
            };

            Assert.That(InvokeDuration(calculateMethod, null), Is.Zero);
            Assert.That(InvokeDuration(calculateMethod, missingRate), Is.EqualTo(2f));
            Assert.That(InvokeDuration(calculateMethod, explicitRate), Is.EqualTo(4f));
            Assert.That(InvokeDuration(calculateMethod, invalidDuration), Is.Zero);
            Assert.That(InvokeDuration(calculateMethod, overflowingDuration), Is.Zero);
        }

        [Test]
        public void Given_SyntheticScene_When_PopulatingReport_Then_AggregatesAnimationMetadata()
        {
            Type inspectorType = typeof(AssimpFBXImporter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.AssimpAnimationInspector");
            MethodInfo populateMethod = inspectorType?.GetMethod(
                "PopulateReport",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Scene), typeof(AssimpFBXImporter.AnimationInspectionReport) },
                modifiers: null);
            var scene = new Scene();
            var animation = new Assimp.Animation
            {
                DurationInTicks = 120d,
                TicksPerSecond = 60d
            };
            var channel = new NodeAnimationChannel { NodeName = "Hips" };
            channel.PositionKeys.Add(new VectorKey(0d, new Vector3D(1f, 2f, 3f)));
            channel.ScalingKeys.Add(new VectorKey(0d, new Vector3D(1f, 1f, 1f)));
            animation.NodeAnimationChannels.Add(channel);
            scene.Animations.Add(animation);
            var report = new AssimpFBXImporter.AnimationInspectionReport();

            Assert.That(populateMethod, Is.Not.Null);

            populateMethod.Invoke(null, new object[] { scene, report });

            Assert.That(report.ImportSucceeded, Is.True);
            Assert.That(report.AnimationCount, Is.EqualTo(1));
            Assert.That(report.NodeAnimationChannelCount, Is.EqualTo(1));
            Assert.That(report.PositionKeyCount, Is.EqualTo(1));
            Assert.That(report.RotationKeyCount, Is.Zero);
            Assert.That(report.ScaleKeyCount, Is.EqualTo(1));
            Assert.That(report.AnimationNames, Is.EqualTo("Animation_0"));
            Assert.That(report.AnimationLengthsSeconds, Is.EqualTo("2"));
            Assert.That(report.MaxAnimationLengthSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void Given_MultilineError_When_NormalizingMessage_Then_ReplacesLineBreaks()
        {
            Type inspectorType = typeof(AssimpFBXImporter).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.AssimpAnimationInspector");
            MethodInfo normalizeMethod = inspectorType?.GetMethod(
                "NormalizeErrorMessage",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.That(normalizeMethod, Is.Not.Null);
            Assert.That(
                normalizeMethod.Invoke(null, new object[] { "line1\r\nline2\nline3" }),
                Is.EqualTo("line1  line2 line3"));
        }

        private static float InvokeDuration(MethodInfo method, Assimp.Animation animation)
        {
            return (float)method.Invoke(null, new object[] { animation });
        }

        private static string ReadFbxImporterSource(string fileName)
        {
            return File.ReadAllText(GetFbxImporterPath(fileName));
        }

        private static string GetFbxImporterPath(string fileName)
        {
            return Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                fileName);
        }
    }
}
