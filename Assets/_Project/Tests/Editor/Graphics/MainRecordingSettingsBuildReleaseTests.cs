using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Member_Han.Build.EditorTools;
using Member_Han.Modules.FBXImporter;
using Member_Han.Modules.Graphics;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Tests.Editor.Graphics
{
    public class MainRecordingSettingsBuildReleaseTests
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string SubManualScenePath = "Assets/_Project/Scene/Sub_Manual.unity";
        private const string FbxImportCaptureScenePath = "Assets/_Project/Scene/FbxImport_Capture.unity";
        private const string CompanionScenePath = "Assets/_Project/Scene/MainRecording_SettingsCompanion.unity";
        private const string ReleaseSmokeScriptPath = "Docs/Machine_Spirit/Tools/Local/scripts/harness/build_main_recording_release_smoke.ps1";

        [Test]
        public void Given_BuildSettings_When_InspectingScenes_Then_IncludesCompanionSceneAfterMainRecording()
        {
            Assert.That(File.Exists(CompanionScenePath), Is.True, "Companion settings scene asset must exist.");

            string[] enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => NormalizePath(scene.path))
                .ToArray();

            Assert.That(enabledScenePaths, Does.Contain(CompanionScenePath));
            Assert.That(enabledScenePaths.Count(path => path == CompanionScenePath), Is.EqualTo(1));

            int mainRecordingIndex = Array.IndexOf(enabledScenePaths, MainRecordingScenePath);
            int companionIndex = Array.IndexOf(enabledScenePaths, CompanionScenePath);

            Assert.That(mainRecordingIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(companionIndex, Is.GreaterThan(mainRecordingIndex));
            Assert.That(enabledScenePaths.Length, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void Given_CompanionScene_When_Loaded_Then_ContainsDedicatedSettingsHierarchy()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            Scene scene = EditorSceneManager.OpenScene(CompanionScenePath, OpenSceneMode.Single);

            try
            {
                Assert.That(scene.name, Is.EqualTo("MainRecording_SettingsCompanion"));

                GameObject root = GameObject.Find("SettingsCompanionRoot");
                Assert.That(root, Is.Not.Null);

                var controller = root.GetComponent<MainRecordingSettingsCompanionController>();
                Assert.That(controller, Is.Not.Null);

                Transform canvasTransform = RequireChild(root.transform, "UI_Canvas");
                Transform panelTransform = RequireChild(canvasTransform, "SettingsPanel");
                RequireChild(panelTransform, "LeftRail");
                RequireChild(panelTransform, "Sidebar");
                RequireChild(panelTransform, "MainCards");
                Transform footerActions = RequireChild(panelTransform, "FooterActions");

                Canvas canvas = canvasTransform.GetComponent<Canvas>();
                Assert.That(canvas, Is.Not.Null);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));

                CanvasScaler scaler = canvasTransform.GetComponent<CanvasScaler>();
                Assert.That(scaler, Is.Not.Null);
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(MainRecordingSettingsLayoutSpec.ReferenceSize));

                Assert.That(UnityEngine.Object.FindObjectOfType<EventSystem>(), Is.Not.Null);
                Assert.That(footerActions.GetComponentInChildren<Button>(true), Is.Not.Null);

                AssertSerializedReference(controller, "fbxPathInput", true);
                AssertSerializedReference(controller, "captureWidthInput", true);
                AssertSerializedReference(controller, "captureHeightInput", true);
                AssertSerializedReference(controller, "openSettingsOnStartToggle", true);
                AssertSerializedReference(controller, "saveButton", true);
                AssertSerializedReference(controller, "feedbackText", true);
                AssertSerializedReference(controller, "characterModelPathInput", false);

                string[] visibleLabels = canvasTransform
                    .GetComponentsInChildren<TMP_Text>(true)
                    .Select(text => text.text)
                    .ToArray();
                Assert.That(visibleLabels, Does.Contain("FBX 파일 임포트"));
                Assert.That(visibleLabels, Does.Contain("FBX 파일을 선택해 프로젝트로 가져오고 모션 캡쳐 설정을 시작합니다"));
                Assert.That(visibleLabels, Does.Contain("공유 설정"));
                Assert.That(visibleLabels, Does.Contain("FBX 경로"));
                Assert.That(visibleLabels, Does.Contain("설정을 저장"));
                Assert.That(visibleLabels, Does.Not.Contain("FBX 파일 선택"));
                Assert.That(visibleLabels, Does.Not.Contain("기본 설정"));
                Assert.That(visibleLabels.Any(label => label.Contains("모델을 가져오고")), Is.False);
                Assert.That(visibleLabels, Does.Contain("Character 1 (비활성화)"));
                Assert.That(InvokeInstance<bool>(controller, "HasReadableKoreanTextForTests"), Is.True);
                Canvas.ForceUpdateCanvases();
                TextMeshProUGUI saveButtonLabel = canvasTransform
                    .GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Single(text => text.text == "설정을 저장");
                RectTransform saveButtonLabelRect = saveButtonLabel.rectTransform;
                float availableWidth =
                    saveButtonLabelRect.rect.width -
                    saveButtonLabel.margin.x -
                    saveButtonLabel.margin.z;
                Vector2 preferredSize = saveButtonLabel.GetPreferredValues(
                    saveButtonLabel.text,
                    float.PositiveInfinity,
                    saveButtonLabelRect.rect.height);
                Assert.That(availableWidth, Is.GreaterThan(0f));
                Assert.That(
                    preferredSize.x,
                    Is.LessThanOrEqualTo(availableWidth),
                    $"Companion save label preferred width ({preferredSize.x}) must fit in the available TMP rect width ({availableWidth}).");
                Assert.That(
                    saveButtonLabel.enableAutoSizing,
                    Is.True,
                    "Companion TMP action label must enable autosizing to avoid Korean label clipping across font fallback differences.");
                Assert.That(
                    saveButtonLabel.fontSizeMax,
                    Is.LessThanOrEqualTo(15f),
                    "Companion TMP action label max font size must stay within the measured safe button contract.");

                Assert.That(UnityEngine.Object.FindObjectOfType<RecodingSetting>(), Is.Null);
                Assert.That(UnityEngine.Object.FindObjectOfType<FileManager>(), Is.Null);
                Assert.That(UnityEngine.Object.FindObjectOfType<UnityHumanoidVMDRecorder>(), Is.Null);
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScenePath) && File.Exists(previousScenePath))
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }
            }
        }

        [Test]
        public void Given_ReleaseBuildRunner_When_InspectingContract_Then_MainAndCompanionOutputsAreSeparate()
        {
            string[] mainScenes = MainRecordingReleaseBuildRunner.MainScenePaths
                .Select(NormalizePath)
                .ToArray();
            string[] companionScenes = MainRecordingReleaseBuildRunner.CompanionScenePaths
                .Select(NormalizePath)
                .ToArray();

            Assert.That(mainScenes, Is.EqualTo(new[]
            {
                MainAutoScenePath,
                MainRecordingScenePath,
                SubManualScenePath,
                FbxImportCaptureScenePath,
            }));
            Assert.That(mainScenes, Does.Not.Contain(CompanionScenePath));
            Assert.That(companionScenes, Is.EqualTo(new[] { CompanionScenePath }));
            Assert.That(NormalizePath(MainRecordingReleaseBuildRunner.MainExecutablePath), Is.EqualTo("Builds/Local/MainRecordingRelease/Unity_Fbx2VMD.exe"));
            Assert.That(NormalizePath(MainRecordingReleaseBuildRunner.CompanionExecutablePath), Is.EqualTo("Builds/Local/MainRecordingRelease/Unity_Fbx2VMD_Settings.exe"));
        }

        [Test]
        public void Given_RuntimeSettingsSourceFiles_When_InspectingContents_Then_UnityEditorIsNotReferenced()
        {
            string[] runtimeSourceFiles =
            {
                "Assets/_Project/Scripts/Member_Han/Modules/Setting/MainRecordingSettingsActionResult.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/Setting/MainRecordingSettingsActions.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/Setting/MainRecordingSettingsCompanionController.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/Setting/MainRecordingSettingsDocument.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/Setting/MainRecordingSettingsPathResolver.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/Setting/MainRecordingSettingsStore.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/Setting/RecodingSetting.cs",
            };

            foreach (string sourceFile in runtimeSourceFiles)
            {
                Assert.That(File.Exists(sourceFile), Is.True, sourceFile);

                string contents = File.ReadAllText(sourceFile);
                Assert.That(contents, Does.Not.Contain("UnityEditor"), sourceFile);
            }
        }

        [Test]
        public void Given_ReleaseSmokeScript_When_InspectingContents_Then_InvokesBuildRunnerAndChecksTwoOutputs()
        {
            Assert.That(File.Exists(ReleaseSmokeScriptPath), Is.True, "Release smoke script must exist.");

            string script = File.ReadAllText(ReleaseSmokeScriptPath);
            Assert.That(script, Does.Contain("Member_Han.Build.EditorTools.MainRecordingReleaseBuildRunner.BuildWindowsSmoke"));
            Assert.That(script, Does.Contain("Unity_Fbx2VMD.exe"));
            Assert.That(script, Does.Contain("Unity_Fbx2VMD_Settings.exe"));
            Assert.That(script, Does.Contain("main_exe_exists"));
            Assert.That(script, Does.Contain("settings_exe_exists"));
        }

        [Test]
        public void Given_GitIgnore_When_InspectingReleaseBuildOutput_Then_BuildsDirectoryIsIgnored()
        {
            string gitIgnore = File.ReadAllText(".gitignore");

            Assert.That(gitIgnore, Does.Contain("/Builds/"));
        }

        [Test]
        public void Given_ProjectDependencies_When_InspectingMainRecordingSettingsScope_Then_DoesNotAddPackageManagerClothDependencies()
        {
            string manifest = File.ReadAllText("Packages/manifest.json");
            string packageLock = File.ReadAllText("Packages/packages-lock.json");

            Assert.That(manifest, Does.Not.Contain("magica").IgnoreCase);
            Assert.That(packageLock, Does.Not.Contain("magica").IgnoreCase);
        }

        [Test]
        public void Given_GitIgnore_When_InspectingLocalLargeAssets_Then_LocalOnlyFoldersAreIgnored()
        {
            string gitIgnore = File.ReadAllText(".gitignore");

            Assert.That(gitIgnore, Does.Contain("/tmp/"));
            Assert.That(gitIgnore, Does.Contain("/Docs/ref/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/GUIPack-Clean&Minimalist/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/GUIPack-Clean&Minimalist.meta"));
            Assert.That(gitIgnore, Does.Contain("/Assets/UI/GUIPack-Clean&Minimalist/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/UI/GUIPack-Clean&Minimalist.meta"));
            Assert.That(gitIgnore, Does.Contain("/Assets/MagicaCloth2/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/MagicaCloth2.meta"));
            Assert.That(gitIgnore, Does.Contain("/Assets/_Project/Tool/MagicaCloth2/"));
            Assert.That(gitIgnore, Does.Contain("/Assets/_Project/Tool/MagicaCloth2.meta"));
        }

        private static Transform RequireChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            Assert.That(child, Is.Not.Null, $"{parent.name}/{childName} must exist.");
            return child;
        }

        private static void AssertSerializedReference(
            MainRecordingSettingsCompanionController controller,
            string propertyName,
            bool expectedAssigned)
        {
            var serializedObject = new SerializedObject(controller);
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            Assert.That(property, Is.Not.Null, $"{propertyName} must be serialized.");
            if (expectedAssigned)
            {
                Assert.That(property.objectReferenceValue, Is.Not.Null, $"{propertyName} must be wired in the scene.");
            }
            else
            {
                Assert.That(property.objectReferenceValue, Is.Null, $"{propertyName} must stay unwired for excluded character features.");
            }
        }

        private static T InvokeInstance<T>(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} must exist.");
            return (T)method.Invoke(target, Array.Empty<object>());
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }
    }
}
