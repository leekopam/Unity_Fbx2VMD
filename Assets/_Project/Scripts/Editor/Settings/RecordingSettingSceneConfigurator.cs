using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal static class RecordingSettingSceneConfigurator
    {
        private const string ManualRecordButtonName = "MMD_Record_Button";

        public static void Configure(RecordingSetting recordingSetting)
        {
            if (recordingSetting == null)
            {
                return;
            }

            FBXVmdPipeline pipeline = recordingSetting.RecordingFBXVmdPipeline;
            Button manualRecordButton = ResolveManualRecordButton();
            HumanoidSampleCode recordingController = ResolveRecordingController(pipeline);
            var serialized = new SerializedObject(recordingSetting);
            serialized.FindProperty("recordingFBXVmdPipeline").objectReferenceValue = pipeline;
            serialized.FindProperty("manualRecordButton").objectReferenceValue = manualRecordButton;
            SetObjectReference(serialized, "recordingController", recordingController);
            SetBool(
                serialized,
                "enableRecordingDiagnostics",
                pipeline != null && pipeline.enableRecordingDiagnostics);
            SetBool(
                serialized,
                "useDeterministicCaptureFramerateForDiagnostics",
                pipeline != null && pipeline.useDeterministicCaptureFramerateForDiagnostics);
            SetBool(
                serialized,
                "enableDiagnosticFingerCloseups",
                pipeline == null || pipeline.enableDiagnosticFingerCloseups);
            SetEnum(
                serialized,
                "recordingCaptureQuality",
                (int)RecordingCaptureQualityPreset.Uhd4K);
            SetInt(serialized, "customRecordingCaptureWidth", 3840);
            SetInt(serialized, "customRecordingCaptureHeight", 2160);
            SetBool(serialized, "applyDiagnosticsToFBXVmdPipelineOnAwake", true);
            SetObjectReference(serialized, "settingsPopup", null);
            SetBool(serialized, "openSettingsPopupOnStart", true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            ManualRecordingButtonBindingApplier.Apply(
                manualRecordButton,
                recordingSetting,
                pipeline);
        }

        private static HumanoidSampleCode ResolveRecordingController(FBXVmdPipeline pipeline)
        {
            if (pipeline != null && pipeline.targetCharacter != null)
            {
                HumanoidSampleCode controller =
                    pipeline.targetCharacter.GetComponent<HumanoidSampleCode>();
                if (controller != null)
                {
                    return controller;
                }
            }

            return null;
        }

        private static void SetObjectReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static void SetBool(
            SerializedObject serialized,
            string propertyName,
            bool value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetEnum(
            SerializedObject serialized,
            string propertyName,
            int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }

        private static void SetInt(
            SerializedObject serialized,
            string propertyName,
            int value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static Button ResolveManualRecordButton()
        {
            GameObject buttonObject = GameObject.Find(ManualRecordButtonName);
            return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
        }
    }
}
