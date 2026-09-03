using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class FBXImportControllerOwnershipTests
    {
        [Test]
        public void Given_ImportController_When_CheckingPipelineComposition_Then_KeepsSingleControllerField()
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                "_importController",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null);
            Assert.That(field.FieldType, Is.EqualTo(typeof(FBXImportController)));
        }

        [Test]
        public void Given_ImportEntryPoints_When_InspectingPipelineSource_Then_DelegatesToSingleController()
        {
            string sourcePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(Regex.Matches(source, @"new FBXImportController\(").Count, Is.EqualTo(1));
            Assert.That(source, Does.Contain("_importController.ImportFromDialog();"));
            Assert.That(source, Does.Contain("_importController.LoadFromImportFolder();"));
            Assert.That(source, Does.Contain("return _importController.TryImportFromSharedSettings(sourcePath);"));
        }

        [Test]
        public void Given_ControlledImportStorage_When_CheckingOwnership_Then_ControllerOwnsFileIo()
        {
            MethodInfo controllerCopyMethod = typeof(FBXImportController).GetMethod(
                "CopyToControlledImportFolder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo controllerDirectoryMethod = typeof(FBXImportController).GetMethod(
                "GetControlledImportDirectory",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo controllerSanitizeMethod = typeof(FBXImportController).GetMethod(
                "SanitizeFileName",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo pipelineCopyMethod = typeof(FBXVmdPipeline).GetMethod(
                "CopyToControlledImportFolder",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo pipelineDirectoryMethod = typeof(FBXVmdPipeline).GetMethod(
                "GetControlledImportDirectory",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo pipelineSanitizeMethod = typeof(FBXVmdPipeline).GetMethod(
                "SanitizeFileName",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(controllerCopyMethod, Is.Not.Null);
            Assert.That(controllerDirectoryMethod, Is.Not.Null);
            Assert.That(controllerSanitizeMethod, Is.Not.Null);
            Assert.That(pipelineCopyMethod, Is.Null);
            Assert.That(pipelineDirectoryMethod, Is.Null);
            Assert.That(pipelineSanitizeMethod, Is.Null);
        }

        [Test]
        public void Given_EditorImportSettings_When_CheckingOwnership_Then_ControllerOwnsConfiguration()
        {
            MethodInfo controllerConfigureIfNeededMethod = typeof(FBXImportController).GetMethod(
                "ConfigureEditorImportSettingsIfNeeded",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo controllerDecisionMethod = typeof(FBXImportController).GetMethod(
                "ShouldConfigureEditorImportSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo controllerConfigureMethod = typeof(FBXImportController).GetMethod(
                "ConfigureImportSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo pipelineDecisionMethod = typeof(FBXVmdPipeline).GetMethod(
                "ShouldConfigureEditorImportSettings",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo pipelineConfigureMethod = typeof(FBXVmdPipeline).GetMethod(
                "ConfigureImportSettings",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(controllerConfigureIfNeededMethod, Is.Not.Null);
            Assert.That(controllerDecisionMethod, Is.Not.Null);
            Assert.That(controllerConfigureMethod, Is.Not.Null);
            Assert.That(pipelineDecisionMethod, Is.Null);
            Assert.That(pipelineConfigureMethod, Is.Null);
        }

        [Test]
        public void Given_RuntimeImportResult_When_CheckingOwnership_Then_ControllerOwnsInterpretation()
        {
            string[] methodNames =
            {
                "LoadBoneMappingRuntime",
                "ValidateGhostAvatar",
                "ExtractPrimaryClip"
            };

            foreach (string methodName in methodNames)
            {
                MethodInfo controllerMethod = typeof(FBXImportController).GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo pipelineMethod = typeof(FBXVmdPipeline).GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                Assert.That(controllerMethod, Is.Not.Null, $"{methodName}은 FBXImportController가 소유해야 합니다.");
                Assert.That(pipelineMethod, Is.Null, $"{methodName}은 FBXVmdPipeline에 남으면 안 됩니다.");
            }
        }

        [Test]
        public void Given_ImportSourceValidation_When_CheckingOwnership_Then_ControllerOwnsPathRules()
        {
            MethodInfo validationMethod = typeof(FBXImportController).GetMethod(
                "TryValidateSourcePath",
                BindingFlags.Static | BindingFlags.NonPublic);
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));
            string controllerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXImportController.cs"));

            Assert.That(validationMethod, Is.Not.Null);
            Assert.That(controllerSource, Does.Contain("TryValidateSourcePath(sourcePath"));
            Assert.That(pipelineSource, Does.Not.Contain("FBXImportController.TryValidateSourcePath(sourcePath"));
            Assert.That(
                pipelineSource,
                Does.Not.Contain("string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)"));
            Assert.That(pipelineSource, Does.Not.Contain("Path.GetExtension(sourcePath)"));

            string missingPath = Path.Combine(
                Path.GetTempPath(),
                $"missing-{System.Guid.NewGuid():N}.fbx");
            object[] missingArguments = { missingPath, null };
            bool missingResult = (bool)validationMethod.Invoke(null, missingArguments);
            object[] extensionArguments = { typeof(FBXImportController).Assembly.Location, null };
            bool extensionResult = (bool)validationMethod.Invoke(null, extensionArguments);

            Assert.That(missingResult, Is.False);
            Assert.That(missingArguments[1], Is.EqualTo($"FBX 파일을 찾을 수 없습니다: {missingPath}"));
            Assert.That(extensionResult, Is.False);
            Assert.That(extensionArguments[1], Is.EqualTo("FBX 파일만 선택할 수 있습니다."));
        }

        [Test]
        public void Given_RuntimeModelImport_When_CheckingOwnership_Then_ControllerOwnsLoadStage()
        {
            MethodInfo importMethod = typeof(FBXImportController).GetMethod(
                "ImportRuntimeModelAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            System.Type resultType = typeof(FBXImportController).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FBXModelImportResult");
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));
            string coordinatorSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXConversionCoordinator.cs"));
            string controllerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXImportController.cs"));
            int validationIndex = controllerSource.IndexOf("if (!TryValidateSourcePath(sourcePath");
            int selectedStateIndex = controllerSource.IndexOf("FBXSessionState.Selected", validationIndex);
            int copyIndex = controllerSource.IndexOf("CopyToControlledImportFolder(sourcePath)", selectedStateIndex);
            int editorSettingsIndex = controllerSource.IndexOf(
                "ConfigureEditorImportSettingsIfNeeded(sourcePath, targetPath)",
                copyIndex);
            int loadingStateIndex = controllerSource.IndexOf("FBXSessionState.LoadingFbx", editorSettingsIndex);
            int importIndex = controllerSource.IndexOf("await _importModelAsync(targetPath)", loadingStateIndex);

            Assert.That(importMethod, Is.Not.Null);
            Assert.That(resultType, Is.Not.Null);
            Assert.That(coordinatorSource, Does.Contain("await importController.ImportRuntimeModelAsync("));
            Assert.That(pipelineSource, Does.Not.Contain("_importController.ImportRuntimeModelAsync("));
            Assert.That(pipelineSource, Does.Not.Contain("_importController.CopyToControlledImportFolder(sourcePath)"));
            Assert.That(pipelineSource, Does.Not.Contain("_importController.ConfigureEditorImportSettingsIfNeeded(sourcePath, targetPath)"));
            Assert.That(pipelineSource, Does.Not.Contain("_fbxImporter.ImportAsync(targetPath)"));
            Assert.That(controllerSource, Does.Contain("TryValidateSourcePath(sourcePath"));
            Assert.That(controllerSource, Does.Contain("CopyToControlledImportFolder(sourcePath)"));
            Assert.That(controllerSource, Does.Contain("ConfigureEditorImportSettingsIfNeeded(sourcePath, targetPath)"));
            Assert.That(controllerSource, Does.Contain("await _importModelAsync(targetPath)"));
            Assert.That(validationIndex, Is.LessThan(selectedStateIndex));
            Assert.That(selectedStateIndex, Is.LessThan(copyIndex));
            Assert.That(copyIndex, Is.LessThan(editorSettingsIndex));
            Assert.That(editorSettingsIndex, Is.LessThan(loadingStateIndex));
            Assert.That(loadingStateIndex, Is.LessThan(importIndex));
        }

        [Test]
        public void Given_RuntimeAvatarPreparation_When_CheckingOwnership_Then_ControllerOwnsFallbackSequence()
        {
            MethodInfo preparationMethod = typeof(FBXImportController).GetMethod(
                "TryPrepareRuntimeAvatar",
                BindingFlags.Static | BindingFlags.NonPublic);
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));
            string coordinatorSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXConversionCoordinator.cs"));

            Assert.That(preparationMethod, Is.Not.Null);
            Assert.That(coordinatorSource, Does.Contain("importController.TryPrepareRuntimeAnimation("));
            Assert.That(pipelineSource, Does.Not.Contain("_importController.TryPrepareRuntimeAnimation("));
            Assert.That(pipelineSource, Does.Not.Contain("FBXImportController.TryPrepareRuntimeAvatar("));
            Assert.That(pipelineSource, Does.Not.Contain("FBXImportController.LoadBoneMappingRuntime()"));
            Assert.That(pipelineSource, Does.Not.Contain("HumanoidAvatarBuilder.SetupHumanoid(importedModel"));
            Assert.That(pipelineSource, Does.Not.Contain("HumanoidAvatarBuilder.BuildAutoMapping(importedModel)"));
            Assert.That(pipelineSource, Does.Not.Contain("FBXImportController.ValidateGhostAvatar(importedModel)"));
        }

        [Test]
        public void Given_RuntimeAnimationPreparation_When_CheckingOwnership_Then_ControllerAppliesReferencePoseBeforeAvatar()
        {
            MethodInfo preparationMethod = typeof(FBXImportController).GetMethod(
                "TryPrepareRuntimeAnimation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string pipelineSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXVmdPipeline.cs"));
            string coordinatorSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXConversionCoordinator.cs"));
            string controllerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "FBXImportController.cs"));
            int preparationIndex = controllerSource.IndexOf("TryPrepareRuntimeAnimation(");
            int animationIndex = preparationIndex >= 0
                ? controllerSource.IndexOf("GetComponent<Animation>()", preparationIndex)
                : -1;
            int clipIndex = animationIndex >= 0
                ? controllerSource.IndexOf("ExtractPrimaryClip(", animationIndex)
                : -1;
            int referencePoseIndex = clipIndex >= 0
                ? controllerSource.IndexOf("RuntimeHumanoidReferencePoseApplier.TryApply(", clipIndex)
                : -1;
            int avatarIndex = referencePoseIndex >= 0
                ? controllerSource.IndexOf("TryPrepareRuntimeAvatar(", referencePoseIndex)
                : -1;
            int avatarReadyIndex = avatarIndex >= 0
                ? controllerSource.IndexOf("FBXSessionState.AvatarReady", avatarIndex)
                : -1;

            Assert.That(preparationMethod, Is.Not.Null);
            Assert.That(coordinatorSource, Does.Contain("importController.TryPrepareRuntimeAnimation("));
            Assert.That(pipelineSource, Does.Not.Contain("_importController.TryPrepareRuntimeAnimation("));
            Assert.That(pipelineSource, Does.Not.Contain("FBXImportController.TryPrepareRuntimeAvatar("));
            Assert.That(pipelineSource, Does.Not.Contain("FBXImportController.ExtractPrimaryClip("));
            Assert.That(preparationIndex, Is.LessThan(animationIndex));
            Assert.That(animationIndex, Is.LessThan(clipIndex));
            Assert.That(clipIndex, Is.LessThan(referencePoseIndex));
            Assert.That(referencePoseIndex, Is.LessThan(avatarIndex));
            Assert.That(avatarIndex, Is.LessThan(avatarReadyIndex));
        }

        [Test]
        public void Given_ValidRuntimeAnimation_When_ExtractingPrimaryClip_Then_ReturnsAssignedClip()
        {
            GameObject root = new GameObject("runtime-import-clip-test");
            AnimationClip clip = new AnimationClip { legacy = true };

            try
            {
                clip.SetCurve(
                    "",
                    typeof(Transform),
                    "localPosition.x",
                    AnimationCurve.Linear(0f, 0f, 1f, 1f));

                Animation animation = root.AddComponent<Animation>();
                animation.AddClip(clip, "motion");
                animation.clip = clip;

                MethodInfo extractMethod = typeof(FBXImportController).GetMethod(
                    "ExtractPrimaryClip",
                    BindingFlags.Static | BindingFlags.NonPublic);
                AnimationClip result = (AnimationClip)extractMethod.Invoke(
                    null,
                    new object[] { animation, false });

                Assert.That(result, Is.SameAs(clip));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(clip);
            }
        }
    }
}
