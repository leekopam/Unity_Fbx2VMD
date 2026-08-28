using Fbx2Vmd.FBXImporter;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal static class ManualRecordingButtonBindingApplier
    {
        private const string RecordingSettingManualRecordMethodName =
            nameof(RecordingSetting.StartManualRecording);
        private const string LegacyFBXVmdPipelineManualRecordMethodName = "OnClickManualRecordButton";

        internal static void Apply(
            Button button,
            RecordingSetting recordingSetting,
            FBXVmdPipeline pipeline)
        {
            if (button == null)
            {
                return;
            }

            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                UnityEngine.Object target = button.onClick.GetPersistentTarget(i);
                string methodName = button.onClick.GetPersistentMethodName(i);
                if (target == recordingSetting ||
                    target == pipeline ||
                    methodName == RecordingSettingManualRecordMethodName ||
                    methodName == LegacyFBXVmdPipelineManualRecordMethodName)
                {
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                }
            }

            UnityEventTools.AddPersistentListener(button.onClick, recordingSetting.StartManualRecording);
            EditorUtility.SetDirty(button);
        }
    }
}
