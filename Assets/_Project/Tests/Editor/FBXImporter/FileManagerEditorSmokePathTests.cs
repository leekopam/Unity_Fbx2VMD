using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class FileManagerEditorSmokePathTests
    {
        private static readonly Type[] SmokeResolverParameterTypes =
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Func<string, bool>)
        };

        private static readonly Type[] HumanoidReferenceResolverParameterTypes =
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(Func<string, bool>)
        };

        private static readonly Type[] ProjectRelativePathParameterTypes =
        {
            typeof(string),
            typeof(string)
        };

        private static readonly Type[] ImportSettingsDecisionParameterTypes =
        {
            typeof(string),
            typeof(string),
            typeof(string)
        };

        private static readonly Type[] ReferenceTimingParameterTypes =
        {
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(int).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] EditorSmokeReferenceTimingParameterTypes =
        {
            typeof(string),
            typeof(float),
            typeof(float),
            typeof(int),
            typeof(float),
            typeof(float).MakeByRefType(),
            typeof(int).MakeByRefType(),
            typeof(float).MakeByRefType()
        };

        private static readonly Type[] RetargetPrewarmFrameCountParameterTypes =
        {
            typeof(int)
        };

        [Test]
        public void Given_ControlledFileExists_When_ResolvingEditorSmokeFbxPath_Then_UsesControlledPath()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(controlledDirectory, "dance.fbx");

            string resolved = Resolve(" dance ", controlledDirectory, dataPath, controlledPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_ControlledMissingAndProjectFbxExists_When_ResolvingEditorSmokeFbxPath_Then_UsesProjectFallback()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string fallbackPath = Path.Combine(dataPath, "_Project", "FBX", "dance.fbx");

            string resolved = Resolve("dance", controlledDirectory, dataPath, fallbackPath);

            Assert.That(resolved, Is.EqualTo(fallbackPath));
        }

        [Test]
        public void Given_NoCandidateExists_When_ResolvingEditorSmokeFbxPath_Then_ReturnsControlledCandidate()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(controlledDirectory, "missing.fbx");

            string resolved = Resolve("missing.fbx", controlledDirectory, dataPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_UppercaseFbxExtension_When_ResolvingEditorSmokeFbxPath_Then_PreservesFileName()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(controlledDirectory, "Dance.FBX");

            string resolved = Resolve("Dance.FBX", controlledDirectory, dataPath, controlledPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_PathLikeInput_When_ResolvingEditorSmokeFbxPath_Then_UsesOnlyFileNameForFallback()
        {
            string controlledDirectory = Path.Combine("C:", "Project", "Assets", "Resources", "Import_FBX");
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string fallbackPath = Path.Combine(dataPath, "_Project", "FBX", "dance.fbx");

            string resolved = Resolve(@"..\_Project\FBX\dance", controlledDirectory, dataPath, fallbackPath);

            Assert.That(resolved, Is.EqualTo(fallbackPath));
        }

        [Test]
        public void Given_ProjectSourceHasHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesSourcePath()
        {
            string sourcePath = "Assets/_Project/FBX/source.fbx";
            string manualPath = "Assets/_Project/FBX/source.fbx";

            string resolved = ResolveHumanoidReference(
                "Assets/Resources/Import_FBX/source.fbx",
                sourcePath,
                sourcePath,
                sourcePath,
                manualPath);

            Assert.That(resolved, Is.EqualTo(sourcePath));
        }

        [Test]
        public void Given_ControlledSourceAndManualClipExists_When_ResolvingEditorHumanoidReferencePath_Then_UsesControlledSourcePath()
        {
            string controlledPath = "Assets/Resources/Import_FBX/dance.fbx";

            string resolved = ResolveHumanoidReference(
                controlledPath,
                controlledPath,
                @"C:\Project\Assets\Resources\Import_FBX\dance.fbx",
                controlledPath,
                "Assets/_Project/FBX/dance.fbx");

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_ControlledSourceOnlyHasClip_When_ResolvingEditorHumanoidReferencePath_Then_FallsBackToControlledSourcePath()
        {
            string controlledPath = "Assets/Resources/Import_FBX/dance.fbx";

            string resolved = ResolveHumanoidReference(
                "Assets/Resources/Import_FBX/dance.fbx",
                controlledPath,
                @"C:\Project\Assets\Resources\Import_FBX\dance.fbx",
                controlledPath);

            Assert.That(resolved, Is.EqualTo(controlledPath));
        }

        [Test]
        public void Given_ImportedPathHasOnlyHumanoidClip_When_ResolvingEditorHumanoidReferencePath_Then_UsesImportedPath()
        {
            string importedPath = "Assets/Resources/Import_FBX/imported.fbx";

            string resolved = ResolveHumanoidReference(
                importedPath,
                "",
                "",
                importedPath);

            Assert.That(resolved, Is.EqualTo(importedPath));
        }

        [Test]
        public void Given_NoHumanoidClipCandidate_When_ResolvingEditorHumanoidReferencePath_Then_ReturnsEmptyPath()
        {
            string resolved = ResolveHumanoidReference(
                "Assets/Resources/Import_FBX/missing.fbx",
                "Assets/Resources/Import_FBX/missing.fbx",
                @"C:\Project\Assets\Resources\Import_FBX\missing.fbx");

            Assert.That(resolved, Is.EqualTo(""));
        }

        [Test]
        public void Given_ControlledSourceAlreadyInImportFolder_When_DecidingImportSettings_Then_PreservesExistingImporter()
        {
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string controlledPath = Path.Combine(dataPath, "Resources", "Import_FBX", "dance.fbx");

            bool shouldConfigure = ShouldConfigureImportSettings(controlledPath, controlledPath, dataPath);

            Assert.That(shouldConfigure, Is.False);
        }

        [Test]
        public void Given_ExternalSourceCopiedToControlledImportFolder_When_DecidingImportSettings_Then_ConfiguresCopiedImporter()
        {
            string dataPath = Path.Combine("C:", "Project", "Assets");
            string sourcePath = Path.Combine("D:", "Downloads", "dance.fbx");
            string controlledPath = Path.Combine(dataPath, "Resources", "Import_FBX", "dance.fbx");

            bool shouldConfigure = ShouldConfigureImportSettings(sourcePath, controlledPath, dataPath);

            Assert.That(shouldConfigure, Is.True);
        }

        [Test]
        public void Given_ProjectArtifactPath_When_MakingProjectRelativePath_Then_ReturnsSlashSeparatedRelativePath()
        {
            string projectRoot = @"C:\Project";
            string artifactPath = Path.Combine(projectRoot, "Exports", "dance.vmd");

            string resolved = MakeProjectRelativePath(artifactPath, projectRoot);

            Assert.That(resolved, Is.EqualTo("Exports/dance.vmd"));
        }

        [Test]
        public void Given_OutsidePathWithSharedPrefix_When_MakingProjectRelativePath_Then_ReturnsNormalizedOriginalPath()
        {
            string resolved = MakeProjectRelativePath(
                @"C:\Projector\Exports\dance.vmd",
                @"C:\Project");

            Assert.That(resolved, Is.EqualTo("C:/Projector/Exports/dance.vmd"));
        }

        [Test]
        public void Given_EmptyPath_When_MakingProjectRelativePath_Then_ReturnsEmptyPath()
        {
            string resolved = MakeProjectRelativePath("", @"C:\Project");

            Assert.That(resolved, Is.EqualTo(""));
        }

        [Test]
        public void Given_SatisfactionFullClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference()
        {
            bool hasPlan = TryBuildKnownMmdReferenceRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7667f,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.True);
            Assert.That(targetFrameCount, Is.EqualTo(6001));
            Assert.That(recordingLengthSeconds, Is.EqualTo(6001f / 30f).Within(0.0001f));
            Assert.That(playbackSpeed, Is.EqualTo(207.7667f / (6001f / 30f)).Within(0.0001f));
        }

        [Test]
        public void Given_FullEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_Matches6000FrameYybReference()
        {
            bool hasPlan = TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7833f,
                requestedDurationSeconds: 207.7833f,
                requestedTargetFrameCount: 6234,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.True);
            Assert.That(targetFrameCount, Is.EqualTo(6001));
            Assert.That(recordingLengthSeconds, Is.EqualTo(6001f / 30f).Within(0.0001f));
            Assert.That(playbackSpeed, Is.EqualTo(207.7833f / (6001f / 30f)).Within(0.0001f));
        }

        [Test]
        public void Given_ShortEditorSmokeSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsRequestedSmokeWindow()
        {
            bool hasPlan = TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                "satisfaction_2",
                clipLengthSeconds: 207.7833f,
                requestedDurationSeconds: 31f,
                requestedTargetFrameCount: 930,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.False);
            Assert.That(recordingLengthSeconds, Is.EqualTo(31f).Within(0.0001f));
            Assert.That(targetFrameCount, Is.EqualTo(930));
            Assert.That(playbackSpeed, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Given_LongRetargetPrewarmConfigured_When_ResolvingPrewarmFrameCount_Then_DoesNotCapAtLegacyTenFrames()
        {
            int resolved = ResolveRetargetPrewarmFrameCount(60);

            Assert.That(resolved, Is.EqualTo(60));
        }

        [Test]
        public void Given_NonSatisfactionClip_When_CalculatingReferenceTiming_Then_KeepsDefaultClipTiming()
        {
            bool hasPlan = TryBuildKnownMmdReferenceRecordingPlan(
                "other_dance",
                clipLengthSeconds: 207.7667f,
                recordingFrameRate: 30f,
                out float recordingLengthSeconds,
                out int targetFrameCount,
                out float playbackSpeed);

            Assert.That(hasPlan, Is.False);
            Assert.That(recordingLengthSeconds, Is.EqualTo(207.7667f).Within(0.0001f));
            Assert.That(targetFrameCount, Is.EqualTo(0));
            Assert.That(playbackSpeed, Is.EqualTo(1f).Within(0.0001f));
        }

        private static string Resolve(
            string fbxFileName,
            string controlledDirectory,
            string dataPath,
            params string[] existingPaths)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveEditorSmokeFbxPath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: SmokeResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a static resolver overload for fakeable path tests.");

            var existing = new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);
            Func<string, bool> fileExists = existing.Contains;

            return (string)method.Invoke(null, new object[] { fbxFileName, controlledDirectory, dataPath, fileExists });
        }

        private static string ResolveHumanoidReference(
            string importedRelativePath,
            string sourceRelativePath,
            string sourceFileName,
            params string[] humanoidClipPaths)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveEditorHumanoidReferencePath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: HumanoidReferenceResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a static humanoid reference resolver overload for fakeable path tests.");

            var clips = new HashSet<string>(humanoidClipPaths, StringComparer.OrdinalIgnoreCase);
            Func<string, bool> hasHumanoidClip = clips.Contains;

            return (string)method.Invoke(null, new object[] { importedRelativePath, sourceRelativePath, sourceFileName, hasHumanoidClip });
        }

        private static bool ShouldConfigureImportSettings(string sourcePath, string targetPath, string dataPath)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ShouldConfigureEditorImportSettings",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ImportSettingsDecisionParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable import-settings decision helper.");

            return (bool)method.Invoke(null, new object[] { sourcePath, targetPath, dataPath });
        }

        private static string MakeProjectRelativePath(string path, string projectRoot)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "MakeProjectRelativePath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ProjectRelativePathParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a static project-relative path overload for fakeable path tests.");

            return (string)method.Invoke(null, new object[] { path, projectRoot });
        }

        private static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "TryBuildKnownMmdReferenceRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: ReferenceTimingParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable reference timing helper for YYB MMD acceptance.");

            object[] args =
            {
                outputBaseName,
                clipLengthSeconds,
                recordingFrameRate,
                0f,
                0,
                0f
            };

            bool result = (bool)method.Invoke(null, args);
            recordingLengthSeconds = (float)args[3];
            targetFrameCount = (int)args[4];
            playbackSpeed = (float)args[5];
            return result;
        }

        private static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "TryBuildKnownMmdReferenceEditorSmokeRecordingPlan",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: EditorSmokeReferenceTimingParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager must expose a fakeable editor smoke reference timing helper for ref MP4 alignment.");

            object[] args =
            {
                outputBaseName,
                clipLengthSeconds,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                recordingFrameRate,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                1f
            };

            bool result = (bool)method.Invoke(null, args);
            recordingLengthSeconds = (float)args[5];
            targetFrameCount = (int)args[6];
            playbackSpeed = (float)args[7];
            return result;
        }

        private static int ResolveRetargetPrewarmFrameCount(int configuredFrameCount)
        {
            MethodInfo method = typeof(FileManager).GetMethod(
                "ResolveRetargetPrewarmFrameCount",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: RetargetPrewarmFrameCountParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "FileManager should expose a deterministic prewarm frame resolver for full-reference smoke stabilization.");

            return (int)method.Invoke(null, new object[] { configuredFrameCount });
        }
    }
}
