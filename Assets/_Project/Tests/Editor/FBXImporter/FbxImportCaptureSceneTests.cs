using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class FbxImportCaptureSceneTests
    {
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string CaptureScenePath = "Assets/_Project/Scene/FbxImport_Capture.unity";

        [Test]
        public void MainAutoScene_RemainsVmdGenerationScene()
        {
            EditorSceneManager.OpenScene(MainAutoScenePath);

            FileManager fileManager = Object.FindObjectOfType<FileManager>();

            Assert.That(fileManager, Is.Not.Null, "Main_Auto scene must keep its FileManager.");
            Assert.That(ReadRecordVmdAfterImport(fileManager), Is.True, "Main_Auto must remain the existing VMD generation scene.");
        }

        [Test]
        public void FbxImportCaptureScene_IsRegisteredAndConfiguredForCaptureOnly()
        {
            Assert.That(File.Exists(CaptureScenePath), Is.True, "A separate FBX import/capture scene must exist.");
            Assert.That(BuildSettingsContains(MainAutoScenePath), Is.True, "Existing VMD generation scene must stay in build settings.");
            Assert.That(BuildSettingsContains(CaptureScenePath), Is.True, "Capture-only scene must be available from build settings.");

            EditorSceneManager.OpenScene(CaptureScenePath);

            FileManager fileManager = Object.FindObjectOfType<FileManager>();
            Camera mainCamera = Camera.main;

            Assert.That(fileManager, Is.Not.Null, "Capture scene must contain FileManager for FBX selection/import.");
            Assert.That(ReadRecordVmdAfterImport(fileManager), Is.False, "Capture scene must not start VMD export after FBX import.");
            Assert.That(fileManager.targetCharacter, Is.Not.Null, "Capture scene must keep a target character for Unity playback/camera capture.");
            Assert.That(mainCamera, Is.Not.Null, "Capture scene must keep a MainCamera for Unity shooting.");

            var sampleCode = fileManager.targetCharacter.GetComponent<HumanoidSampleCode>();
            Assert.That(sampleCode, Is.Not.Null, "Capture target must keep HumanoidSampleCode for UI/progress status.");
            Assert.That(sampleCode.AutoStartRecording, Is.False, "Capture scene must not auto-start VMD recording on scene load.");
        }

        private static bool BuildSettingsContains(string scenePath)
        {
            return EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && string.Equals(scene.path, scenePath, System.StringComparison.Ordinal));
        }

        private static bool ReadRecordVmdAfterImport(FileManager fileManager)
        {
            FieldInfo field = typeof(FileManager).GetField(
                "recordVmdAfterImport",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "FileManager must expose a scene-level VMD recording mode flag.");
            return (bool)field.GetValue(fileManager);
        }
    }
}
