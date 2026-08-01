using Fbx2Vmd.Modules.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Editor.FBXImporter
{
    public class FbxImportCaptureSceneTests
    {
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string CaptureScenePath = "Assets/_Project/Scene/FbxImport_Capture.unity";
        private const string RecodingSettingTypeName = "RecordingSetting, Assembly-CSharp";
        private const string ManualRecordButtonName = "MMD_Record_Button";
        private const string LegacyFBXVmdPipelineManualRecordMethodName = "OnClickManualRecordButton";
        private const string SettingManualRecordMethodName = "StartManualRecording";
        private const string HumanoidManualRecordMethodName = nameof(HumanoidSampleCode.OnManualRecordButtonClick);

        [Test]
        public void MainAutoScene_RemainsVmdGenerationScene()
        {
            EditorSceneManager.OpenScene(MainAutoScenePath);

            FBXVmdPipeline fileManager = Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must keep its FBXVmdPipeline.");
            Assert.That(ReadRecordVmdAfterImport(fileManager), Is.True, "Main_Auto must remain the existing VMD generation scene.");
        }

        [Test]
        public void FbxImportCaptureScene_IsRegisteredAndConfiguredForCaptureOnly()
        {
            Assert.That(File.Exists(CaptureScenePath), Is.True, "A separate FBX import/capture scene must exist.");
            Assert.That(BuildSettingsContains(MainAutoScenePath), Is.True, "Existing VMD generation scene must stay in build settings.");
            Assert.That(BuildSettingsContains(CaptureScenePath), Is.True, "Capture-only scene must be available from build settings.");

            EditorSceneManager.OpenScene(CaptureScenePath);

            FBXVmdPipeline fileManager = Object.FindObjectOfType<FBXVmdPipeline>();
            Camera mainCamera = Camera.main;

            Assert.That(fileManager, Is.Not.Null, "Capture scene must contain FBXVmdPipeline for FBX selection/import.");
            Assert.That(ReadRecordVmdAfterImport(fileManager), Is.False, "Capture scene must not start VMD export after FBX import.");
            Assert.That(fileManager.targetCharacter, Is.Not.Null, "Capture scene must keep a target character for Unity playback/camera capture.");
            Assert.That(mainCamera, Is.Not.Null, "Capture scene must keep a MainCamera for Unity shooting.");

            var sampleCode = fileManager.targetCharacter.GetComponent<HumanoidSampleCode>();
            Assert.That(sampleCode, Is.Not.Null, "Capture target must keep HumanoidSampleCode for UI/progress status.");
            Assert.That(sampleCode.AutoStartRecording, Is.False, "Capture scene must not auto-start VMD recording on scene load.");
        }

        [Test]
        public void NewFBXVmdPipeline_DefaultsGhostDisplayOff()
        {
            var root = new GameObject("New FBXVmdPipeline Ghost Default Test");

            try
            {
                FBXVmdPipeline fileManager = root.AddComponent<FBXVmdPipeline>();

                Assert.That(fileManager.showGhostModel, Is.False,
                    "A newly added FBXVmdPipeline must not show imported Ghost models until the user enables the option.");
                Assert.That(fileManager.showGhostSkeletonWhenNoRenderers, Is.False,
                    "Rendererless Ghost skeleton fallback must also default off with the Ghost display option.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MainImportScenes_DefaultGhostDisplayOff()
        {
            AssertSceneGhostDisplayOff(MainAutoScenePath, "Main_Auto");
            AssertSceneGhostDisplayOff(MainRecordingScenePath, "Main_Recoding");
            AssertSceneGhostDisplayOff(CaptureScenePath, "FbxImport_Capture");
        }

        [Test]
        public void MainRecordingScene_HasManualMmdRecordingButtonWiredToTarget()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            FBXVmdPipeline fileManager = Object.FindObjectOfType<FBXVmdPipeline>();
            Assert.That(fileManager, Is.Not.Null, "Main_recoding scene must keep FBXVmdPipeline for FBX selection/import.");
            Assert.That(ReadRecordVmdAfterImport(fileManager), Is.False,
                "Main_recoding must keep FBX import capture separate from automatic VMD conversion.");
            Assert.That(fileManager.targetCharacter, Is.Not.Null, "Main_recoding scene must keep a recording target character.");

            var sampleCode = fileManager.targetCharacter.GetComponent<HumanoidSampleCode>();
            Assert.That(sampleCode, Is.Not.Null, "Main_recoding target must keep HumanoidSampleCode for manual MMD-style recording.");
            Assert.That(typeof(FBXVmdPipeline).GetMethod(LegacyFBXVmdPipelineManualRecordMethodName, BindingFlags.Instance | BindingFlags.Public),
                Is.Null, "FBXVmdPipeline must not expose screen/manual recording button handling after RecordingSetting owns it.");
            Assert.That(typeof(HumanoidSampleCode).GetMethod(HumanoidManualRecordMethodName, BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null, "HumanoidSampleCode must keep the actual VMD recorder button endpoint.");
            System.Type recodingSettingType = RequireType(RecodingSettingTypeName);
            GameObject settingRoot = GameObject.Find("Setting");
            Assert.That(settingRoot, Is.Not.Null, "Main_recoding scene must keep the visible Setting object.");
            Component setting = settingRoot.GetComponent(recodingSettingType);
            Assert.That(setting, Is.Not.Null, "Setting must expose the recording assignment through RecordingSetting.");

            GameObject buttonObject = GameObject.Find(ManualRecordButtonName);
            Assert.That(buttonObject, Is.Not.Null,
                $"Main_recoding must expose an active {ManualRecordButtonName} UI button for manual MMD-style recording.");

            Button button = buttonObject.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, $"{ManualRecordButtonName} must use a Unity UI Button component.");
            Assert.That(button.interactable, Is.True, $"{ManualRecordButtonName} must be interactable in Main_recoding.");
            Assert.That(ReadSerializedField<Component>(setting, "recordingFBXVmdPipeline"), Is.EqualTo(fileManager),
                "Setting must show which FBXVmdPipeline owns manual recording.");
            Assert.That(ReadSerializedField<Button>(setting, "manualRecordButton"), Is.EqualTo(button),
                "Setting must show which UI button starts manual recording.");
            Assert.That(ReadSerializedField<Component>(setting, "recordingController"), Is.EqualTo(sampleCode),
                "Setting must show which HumanoidSampleCode receives manual recording.");
            Assert.That(HasPersistentCall(button, setting, SettingManualRecordMethodName), Is.True,
                $"{ManualRecordButtonName} must call Setting.{SettingManualRecordMethodName} so the assignment is visible on Setting.");
        }

        [Test]
        public void MainRecordingScene_DoesNotReplaceImportedFbxPoseWithManualFullBodyReference()
        {
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            FBXVmdPipeline fileManager = Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, "Main_recoding scene must keep FBXVmdPipeline for FBX selection/import.");
            Assert.That(fileManager.useManualAnimatorFingerPoseReference, Is.False,
                "Main_Recoding must not copy manual finger pose into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorFullBodyPoseReference, Is.False,
                "Main_Recoding must preserve the imported FBX full-body pose during normal Play/import playback.");
            Assert.That(fileManager.useManualAnimatorBodyRotationReference, Is.False,
                "Main_Recoding must not copy manual body rotation into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorBodyPositionYReference, Is.False,
                "Main_Recoding must not copy manual body Y into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorHipsLocalPositionReference, Is.False,
                "Main_Recoding must not copy manual Hips local position into the default playback/import path.");
            Assert.That(fileManager.useManualAnimatorThumbLocalRotationReference, Is.False,
                "Main_Recoding must not copy manual thumb local rotation into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorHandLocalRotationReference, Is.False,
                "Main_Recoding must not copy manual hand local rotation into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorThumbSegmentDirectionReference, Is.False,
                "Main_Recoding must not copy manual thumb segment directions into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorThumbHandDirectionReference, Is.False,
                "Main_Recoding must not copy manual thumb-hand directions into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorHandPalmFrameReference, Is.False,
                "Main_Recoding must not copy manual palm frame into the normal Play/import path.");
            Assert.That(fileManager.useManualAnimatorThumbBasePositionReference, Is.False,
                "Main_Recoding must not copy manual thumb base position into the normal Play/import path.");
            Assert.That(fileManager.preserveManualFingerReferenceThumbMuscles, Is.False,
                "Main_Recoding must not preserve manual thumb muscles while the manual finger reference is disabled.");
            Assert.That(fileManager.manualAnimatorHipsLocalPositionMaxOffset, Is.EqualTo(0.12f).Within(0.0001f),
                "Main_Recoding keeps only the conservative serialized Hips reference cap while the reference is disabled.");
            Assert.That(fileManager.preserveRetargetBodyPosition, Is.False,
                "Main_Recoding must let the imported FBX body position drive the moving-root solve.");
            Assert.That(fileManager.MovementScaleMultiplier, Is.GreaterThanOrEqualTo(0.9f),
                "Main_Recoding must keep the visible root carrier moving for manual-style natural motion.");
            Assert.That(fileManager.useRetargetBodyPositionXZRootMotion, Is.True,
                "Main_Recoding must add bodyPosition X/Z root motion to the manual-style preview carrier.");
            Assert.That(fileManager.useEditorHumanoidRootTranslationReference, Is.False,
                "Main_Recoding must not add Humanoid RootT translation on top of bodyPosition X/Z root motion.");
        }

        [Test]
        public void ConversionAndCaptureScenes_DoNotExposeManualMmdRecordingButton()
        {
            AssertSceneDoesNotExposeManualRecordButton(MainAutoScenePath, "Main_Auto");
            AssertSceneDoesNotExposeManualRecordButton(CaptureScenePath, "FbxImport_Capture");
        }

        private static bool BuildSettingsContains(string scenePath)
        {
            return EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && string.Equals(scene.path, scenePath, System.StringComparison.Ordinal));
        }

        private static bool ReadRecordVmdAfterImport(FBXVmdPipeline fileManager)
        {
            FieldInfo field = typeof(FBXVmdPipeline).GetField(
                "recordVmdAfterImport",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "FBXVmdPipeline must expose a scene-level VMD recording mode flag.");
            return (bool)field.GetValue(fileManager);
        }

        private static void AssertSceneGhostDisplayOff(string scenePath, string sceneName)
        {
            EditorSceneManager.OpenScene(scenePath);

            FBXVmdPipeline fileManager = Object.FindObjectOfType<FBXVmdPipeline>();

            Assert.That(fileManager, Is.Not.Null, $"{sceneName} must contain FBXVmdPipeline.");
            Assert.That(fileManager.showGhostModel, Is.False,
                $"{sceneName} must keep Ghost display off by default; it is only a user-enabled debug option.");
            Assert.That(fileManager.showGhostSkeletonWhenNoRenderers, Is.False,
                $"{sceneName} must keep rendererless Ghost skeleton fallback off while Ghost display is disabled.");
        }

        private static void AssertSceneDoesNotExposeManualRecordButton(string scenePath, string sceneName)
        {
            EditorSceneManager.OpenScene(scenePath);

            Assert.That(GameObject.Find(ManualRecordButtonName), Is.Null,
                $"{sceneName} must not expose the Main_recoding-only manual recording button.");

            Button[] buttons = Object.FindObjectsOfType<Button>(true);
            foreach (Button button in buttons)
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    Assert.That(button.onClick.GetPersistentMethodName(i), Is.Not.EqualTo(LegacyFBXVmdPipelineManualRecordMethodName),
                        $"{sceneName} must not wire any UI button directly to {LegacyFBXVmdPipelineManualRecordMethodName}.");
                    Assert.That(button.onClick.GetPersistentMethodName(i), Is.Not.EqualTo(SettingManualRecordMethodName),
                        $"{sceneName} must not wire any UI button directly to {SettingManualRecordMethodName}.");
                    Assert.That(button.onClick.GetPersistentMethodName(i), Is.Not.EqualTo(HumanoidManualRecordMethodName),
                        $"{sceneName} must not wire any UI button directly to {HumanoidManualRecordMethodName}.");
                }
            }
        }

        private static bool HasPersistentCall(Button button, Object target, string methodName)
        {
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == target &&
                    button.onClick.GetPersistentMethodName(i) == methodName)
                {
                    return true;
                }
            }

            return false;
        }

        private static T ReadSerializedField<T>(Component component, string fieldName) where T : Object
        {
            FieldInfo field = component.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
            return field.GetValue(component) as T;
        }

        private static System.Type RequireType(string typeName)
        {
            System.Type type = System.Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"Expected type '{typeName}'.");
            return type;
        }
    }
}
