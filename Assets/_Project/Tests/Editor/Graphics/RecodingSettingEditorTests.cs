using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Editor.Graphics
{
    public class RecodingSettingEditorTests
    {
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_recoding.unity";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string SettingsWindowTypeName =
            "Member_Han.Modules.Graphics.EditorTools.MainRecordingSettingsWindow, Assembly-CSharp-Editor";
        private const string SettingsWindowContextTypeName =
            "Member_Han.Modules.Graphics.EditorTools.MainRecordingSettingsWindowContext, Assembly-CSharp-Editor";

        [Test]
        public void Given_RecodingSetting_When_CreateEditor_Then_UsesRecordingInspector()
        {
            var settingObject = new GameObject("Recoding Setting Editor Test");

            try
            {
                var recodingSetting = settingObject.AddComponent<RecodingSetting>();
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(recodingSetting);

                try
                {
                    Assert.That(editor.GetType().FullName,
                        Is.EqualTo("Member_Han.Modules.Graphics.EditorTools.RecodingSettingEditor"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_BackgroundColorSetting_When_CreateEditor_Then_UsesKoreanInspector()
        {
            var settingObject = new GameObject("Background Color Setting Editor Test");

            try
            {
                var backgroundColorSetting = settingObject.AddComponent<BackgroundColorSetting>();
                UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(backgroundColorSetting);

                try
                {
                    Assert.That(editor.GetType().FullName,
                        Is.EqualTo("Member_Han.Modules.Graphics.EditorTools.BackgroundColorSettingEditor"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settingObject);
            }
        }

        [Test]
        public void Given_SettingFields_When_InspectingAttributes_Then_UsesKoreanLabels()
        {
            AssertHeader<BackgroundColorSetting>("targetCamera", "대상");
            AssertInspectorName<BackgroundColorSetting>("targetCamera", "대상 카메라");
            AssertHeader<BackgroundColorSetting>("applyOnAwake", "적용");
            AssertInspectorName<BackgroundColorSetting>("applyOnAwake", "실행 시작 시 자동 적용");
            AssertInspectorName<BackgroundColorSetting>("applyOnValidate", "Unity OnValidate 자동 적용");
            AssertHeader<BackgroundColorSetting>("applyBackgroundColor", "카메라 배경");
            AssertInspectorName<BackgroundColorSetting>("applyBackgroundColor", "배경색 적용");
            AssertInspectorName<BackgroundColorSetting>("backgroundColor", "배경색");

            AssertHeader<RecodingSetting>("recordingFileManager", "수동 녹화");
            AssertInspectorName<RecodingSetting>("recordingFileManager", "녹화 FileManager");
            AssertInspectorName<RecodingSetting>("manualRecordButton", "수동 녹화 버튼");
            AssertInspectorName<RecodingSetting>("recordingController", "녹화 대상");
            AssertHeader<RecodingSetting>("enableRecordingDiagnostics", "화면 녹화 진단");
            AssertInspectorName<RecodingSetting>("enableRecordingDiagnostics", "녹화 진단/캡처 사용");
            AssertInspectorName<RecodingSetting>(
                "useDeterministicCaptureFramerateForDiagnostics",
                "테스트용 30fps 시간 고정");
            AssertInspectorName<RecodingSetting>("enableDiagnosticFingerCloseups", "손 close-up 캡처");
            AssertInspectorName<RecodingSetting>("applyDiagnosticsToFileManagerOnAwake", "실행 시작 시 FileManager에 적용");
        }

        [Test]
        public void Given_SettingsWindowType_When_InspectingMetadata_Then_UsesSeparateEditorWindowForMainRecording()
        {
            Type windowType = RequireType(SettingsWindowTypeName);

            Assert.That(typeof(EditorWindow).IsAssignableFrom(windowType), Is.True);
            Assert.That(InvokeStatic<string>(windowType, "GetWindowTitle"), Is.EqualTo("Main_recording 설정"));
            Assert.That(InvokeStatic<bool>(windowType, "ShouldOpenForScene", MainRecordingScenePath), Is.True);
            Assert.That(InvokeStatic<bool>(windowType, "ShouldOpenForScene", MainAutoScenePath), Is.False);
        }

        [Test]
        public void Given_MainRecordingScene_When_OpenSettingsWindow_Then_ResolvesSplitSettingComponents()
        {
            Type windowType = RequireType(SettingsWindowTypeName);
            Type contextType = RequireType(SettingsWindowContextTypeName);
            EditorSceneManager.OpenScene(MainRecordingScenePath);

            EditorWindow window = null;
            try
            {
                window = (EditorWindow)InvokeStatic<object>(windowType, "OpenForMainRecordingScene");
                Assert.That(window, Is.Not.Null);
                Assert.That(window.GetType(), Is.EqualTo(windowType));
                Assert.That(window.titleContent.text, Is.EqualTo("Main_recording 설정"));

                object context = InvokeStatic<object>(windowType, "ResolveContext");
                Assert.That(GetMemberValue<Component>(context, "GraphicSetting"), Is.Not.Null);
                Assert.That(GetMemberValue<BackgroundColorSetting>(context, "BackgroundColorSetting"), Is.Not.Null);
                Assert.That(GetMemberValue<RecodingSetting>(context, "RecodingSetting"), Is.Not.Null);
                Assert.That(GetMemberValue<Camera>(context, "TargetCamera"), Is.EqualTo(Camera.main));
                Assert.That(GetMemberValue<bool>(context, "IsComplete"), Is.True);
                Assert.That(context.GetType(), Is.EqualTo(contextType));
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        private static void AssertHeader<T>(string fieldName, string expectedHeader)
        {
            FieldInfo field = RequireField<T>(fieldName);
            HeaderAttribute attribute = field.GetCustomAttribute<HeaderAttribute>();
            Assert.That(attribute, Is.Not.Null, $"{fieldName} must expose a Korean inspector section header.");
            Assert.That(attribute.header, Is.EqualTo(expectedHeader));
        }

        private static void AssertInspectorName<T>(string fieldName, string expectedName)
        {
            FieldInfo field = RequireField<T>(fieldName);
            InspectorNameAttribute attribute = field.GetCustomAttribute<InspectorNameAttribute>();
            Assert.That(attribute, Is.Not.Null, $"{fieldName} must expose a Korean inspector label.");
            Assert.That(attribute.displayName, Is.EqualTo(expectedName));
        }

        private static FieldInfo RequireField<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{typeof(T).Name}.{fieldName} must exist.");
            return field;
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} must exist.");
            return type;
        }

        private static T InvokeStatic<T>(Type type, string methodName, params object[] args)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} must exist.");
            return (T)method.Invoke(null, args);
        }

        private static T GetMemberValue<T>(object instance, string memberName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                return (T)property.GetValue(instance);
            }

            FieldInfo field = instance.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{instance.GetType().FullName}.{memberName} must exist.");
            return (T)field.GetValue(instance);
        }
    }
}
