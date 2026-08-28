using System;
using System.IO;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Editor.Settings
{
    public class ManualRecordingButtonBindingApplierTests
    {
        private const string ApplierTypeName =
            "Fbx2Vmd.Settings.EditorTools.ManualRecordingButtonBindingApplier, Assembly-CSharp-Editor";
        private const string ApplierSourcePath =
            "Assets/_Project/Scripts/Editor/Settings/ManualRecordingButtonBindingApplier.cs";
        private const string SceneConfiguratorSourcePath =
            "Assets/_Project/Scripts/Editor/Settings/RecordingSettingSceneConfigurator.cs";

        [Test]
        public void Given_ManualRecordingButton_When_ApplyingBinding_Then_ReplacesOwnedPersistentListeners()
        {
            Assert.That(File.Exists(ApplierSourcePath), Is.True, ApplierSourcePath);
            string sceneConfiguratorSource = File.ReadAllText(SceneConfiguratorSourcePath);
            Assert.That(sceneConfiguratorSource, Does.Not.Contain("UnityEventTools"));
            Assert.That(
                sceneConfiguratorSource,
                Does.Not.Contain("LegacyFBXVmdPipelineManualRecordMethodName"));
            Assert.That(
                sceneConfiguratorSource,
                Does.Contain("ManualRecordingButtonBindingApplier.Apply("));
            Assert.That(sceneConfiguratorSource, Does.Contain("manualRecordButton,"));
            Assert.That(sceneConfiguratorSource, Does.Contain("recordingSetting,"));
            Assert.That(sceneConfiguratorSource, Does.Contain("pipeline);"));

            MethodInfo applyMethod = RequireApplyMethod();

            var settingObject = new GameObject("Manual Recording Button Binding Setting");
            var buttonObject = new GameObject("Manual Recording Button Binding Button");
            var pipelineObject = new GameObject("Manual Recording Button Binding Pipeline");
            var legacyListener = ScriptableObject.CreateInstance<LegacyManualRecordingListenerProbe>();

            try
            {
                var setting = settingObject.AddComponent<RecordingSetting>();
                var button = buttonObject.AddComponent<Button>();
                var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                UnityEventTools.AddPersistentListener(button.onClick, setting.StartManualRecording);
                UnityEventTools.AddPersistentListener(button.onClick, setting.StartManualRecording);
                UnityEventTools.AddPersistentListener(button.onClick, pipeline.OnClickImportButton);
                UnityEventTools.AddPersistentListener(button.onClick, legacyListener.OnClickManualRecordButton);
                UnityEventTools.AddPersistentListener(button.onClick, button.Select);

                applyMethod.Invoke(null, new object[] { button, setting, pipeline });

                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(2));
                Assert.That(
                    CountPersistentCalls(button, setting, nameof(RecordingSetting.StartManualRecording)),
                    Is.EqualTo(1));
                Assert.That(
                    CountPersistentCalls(button, pipeline, nameof(FBXVmdPipeline.OnClickImportButton)),
                    Is.Zero);
                Assert.That(
                    CountPersistentCalls(
                        button,
                        legacyListener,
                        nameof(LegacyManualRecordingListenerProbe.OnClickManualRecordButton)),
                    Is.Zero);
                Assert.That(CountPersistentCalls(button, button, nameof(Button.Select)), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacyListener);
                UnityEngine.Object.DestroyImmediate(pipelineObject);
                UnityEngine.Object.DestroyImmediate(buttonObject);
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_MissingPipeline_When_ApplyingBinding_Then_PreservesUnrelatedListener()
        {
            MethodInfo applyMethod = RequireApplyMethod();
            var settingObject = new GameObject("Manual Recording Button Binding Missing Pipeline Setting");
            var buttonObject = new GameObject("Manual Recording Button Binding Missing Pipeline Button");

            try
            {
                var setting = settingObject.AddComponent<RecordingSetting>();
                var button = buttonObject.AddComponent<Button>();
                UnityEventTools.AddPersistentListener(button.onClick, button.Select);

                applyMethod.Invoke(null, new object[] { button, setting, null });

                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(2));
                Assert.That(
                    CountPersistentCalls(button, setting, nameof(RecordingSetting.StartManualRecording)),
                    Is.EqualTo(1));
                Assert.That(CountPersistentCalls(button, button, nameof(Button.Select)), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(buttonObject);
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        private static MethodInfo RequireApplyMethod()
        {
            Type applierType = Type.GetType(ApplierTypeName);
            Assert.That(applierType, Is.Not.Null, ApplierTypeName);
            MethodInfo applyMethod = applierType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);
            return applyMethod;
        }

        private static int CountPersistentCalls(Button button, UnityEngine.Object target, string methodName)
        {
            int count = 0;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == target &&
                    button.onClick.GetPersistentMethodName(i) == methodName)
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class LegacyManualRecordingListenerProbe : ScriptableObject
        {
            public void OnClickManualRecordButton()
            {
            }
        }
    }
}
