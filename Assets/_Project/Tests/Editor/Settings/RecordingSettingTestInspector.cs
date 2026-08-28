using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Settings;
using NUnit.Framework;

namespace Tests.Editor.Settings
{
    internal static class RecordingSettingTestInspector
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static Func<FBXVmdPipeline, string, bool> GetFbxImportStarter(
            RecordingSetting recordingSetting)
        {
            return (Func<FBXVmdPipeline, string, bool>)RequireField(
                "sharedSettingsFbxImportStarter").GetValue(recordingSetting);
        }

        internal static void SetFbxImportStarter(
            RecordingSetting recordingSetting,
            Func<FBXVmdPipeline, string, bool> starter)
        {
            RequireField("sharedSettingsFbxImportStarter").SetValue(recordingSetting, starter);
        }

        internal static MainRecordingSettingsActionResult LoadSharedSettingsFromPath(
            RecordingSetting recordingSetting,
            string path)
        {
            RequireField("sharedSettingsFilePathOverride").SetValue(recordingSetting, path);
            return recordingSetting.LoadSharedSettings();
        }

        internal static MainRecordingSettingsActionResult WriteRuntimePlayModeState(
            RecordingSetting recordingSetting,
            string playMode)
        {
            MethodInfo method = typeof(RecordingSetting).GetMethod(
                "WriteRuntimePlayModeState",
                InstanceMembers);
            Assert.That(method, Is.Not.Null);
            return (MainRecordingSettingsActionResult)method.Invoke(
                recordingSetting,
                new object[] { playMode });
        }

        private static FieldInfo RequireField(string fieldName)
        {
            FieldInfo field = typeof(RecordingSetting).GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, fieldName);
            return field;
        }
    }
}
