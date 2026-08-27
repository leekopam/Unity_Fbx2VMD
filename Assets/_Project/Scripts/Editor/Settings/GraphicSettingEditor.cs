using System;
using System.Collections.Generic;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;

namespace Fbx2Vmd.Settings.EditorTools
{
    [CustomEditor(typeof(GraphicSetting))]
    public sealed class GraphicSettingEditor : Editor
    {
        private static readonly GUIContent[] AntiAliasingLabels =
        {
            new GUIContent("끄기"),
            new GUIContent("FXAA"),
            new GUIContent("SMAA"),
            new GUIContent("TAA")
        };

        private static readonly int[] AntiAliasingValues =
        {
            (int)GraphicAntiAliasingMode.Off,
            (int)GraphicAntiAliasingMode.FXAA,
            (int)GraphicAntiAliasingMode.SMAA,
            (int)GraphicAntiAliasingMode.TAA
        };

        private static readonly GUIContent[] QualityPresetLabels =
            ToGuiContents(GraphicSettingInspectorSchema.GetPresetOptionLabels("antiAliasingPreset"));

        private static readonly int[] QualityPresetValues =
        {
            (int)GraphicSettingQualityPreset.Performance,
            (int)GraphicSettingQualityPreset.Balanced,
            (int)GraphicSettingQualityPreset.Quality,
            (int)GraphicSettingQualityPreset.Custom
        };

        private static readonly GUIContent[] GameViewScaleLabels =
        {
            new GUIContent("창에 맞춤(Fit)"),
            new GUIContent("1:1 표시(1x)")
        };

        private static readonly int[] GameViewScaleValues =
        {
            (int)GraphicGameViewScaleMode.Fit,
            (int)GraphicGameViewScaleMode.OneX
        };

        private static readonly GUIContent[] FilterModeLabels =
        {
            new GUIContent("포인트(Point)"),
            new GUIContent("양선형(Bilinear)"),
            new GUIContent("삼선형(Trilinear)")
        };

        private static readonly int[] FilterModeValues =
        {
            (int)FilterMode.Point,
            (int)FilterMode.Bilinear,
            (int)FilterMode.Trilinear
        };

        private static readonly GUIContent[] CompressionLabels =
        {
            new GUIContent("기존 유지"),
            new GUIContent("압축 없음"),
            new GUIContent("고품질 압축")
        };

        private static readonly int[] CompressionValues =
        {
            (int)GraphicTextureCompressionPreference.Keep,
            (int)GraphicTextureCompressionPreference.None,
            (int)GraphicTextureCompressionPreference.HighQuality
        };

        private static readonly GUIContent[] SurfaceModeLabels =
        {
            new GUIContent("기존 유지"),
            new GUIContent("불투명"),
            new GUIContent("컷아웃"),
            new GUIContent("페이드"),
            new GUIContent("투명")
        };

        private static readonly int[] SurfaceModeValues =
        {
            (int)GraphicMaterialSurfaceMode.Keep,
            (int)GraphicMaterialSurfaceMode.Opaque,
            (int)GraphicMaterialSurfaceMode.Cutout,
            (int)GraphicMaterialSurfaceMode.Fade,
            (int)GraphicMaterialSurfaceMode.Transparent
        };

        private static readonly GUIContent[] SmaaQualityLabels =
        {
            new GUIContent("낮음"),
            new GUIContent("중간"),
            new GUIContent("높음")
        };

        private static readonly int[] SmaaQualityValues =
        {
            (int)AntialiasingQuality.Low,
            (int)AntialiasingQuality.Medium,
            (int)AntialiasingQuality.High
        };

        private static readonly HashSet<int> PendingAutoApplyIds = new HashSet<int>();

        private GraphicSettingInspectorCategory selectedCategory = GraphicSettingInspectorCategory.Quality;

        public override void OnInspectorGUI()
        {
            var setting = (GraphicSetting)target;
            serializedObject.Update();

            DrawCategoryToolbar();
            EditorGUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();
            DrawSelectedCategory();
            bool changedByGui = EditorGUI.EndChangeCheck();
            if (changedByGui)
            {
                PromoteDirectSettingsToCustomPreset();
            }

            serializedObject.ApplyModifiedProperties();

            if (changedByGui && IsUserDrivenInspectorEvent())
            {
                ScheduleAutoApply(setting, selectedCategory);
            }
        }

        private void DrawCategoryToolbar()
        {
            int selectedIndex = Mathf.Clamp(
                (int)selectedCategory,
                0,
                GraphicSettingInspectorSchema.CategoryLabels.Length - 1);
            selectedIndex = GUILayout.Toolbar(selectedIndex, GraphicSettingInspectorSchema.CategoryLabels);
            selectedCategory = (GraphicSettingInspectorCategory)selectedIndex;
        }

        private void DrawSelectedCategory()
        {
            foreach (string propertyName in GraphicSettingInspectorSchema.GetVisiblePropertyNames(selectedCategory))
            {
                DrawProperty(propertyName);
            }
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            switch (propertyName)
            {
                case "textureResolution":
                case "antiAliasingPreset":
                case "renderSharpness":
                case "modelEdgeAndAlpha":
                    DrawQualityPreset(property, propertyName);
                    break;
                case "gameViewScaleMode":
                    DrawGameViewScale(property, propertyName);
                    break;
                case "antiAliasing":
                    DrawEnumPopup(property, propertyName, AntiAliasingLabels, AntiAliasingValues);
                    break;
                case "smaaQuality":
                    DrawEnumPopup(property, propertyName, SmaaQualityLabels, SmaaQualityValues);
                    break;
                case "textureImportProfile":
                    DrawTextureImportProfile(property);
                    break;
                case "materialShaderProfile":
                    DrawMaterialShaderProfile(property);
                    break;
                default:
                    EditorGUILayout.PropertyField(property, Label(propertyName), true);
                    break;
            }
        }

        private static void DrawQualityPreset(SerializedProperty property, string propertyName)
        {
            GUIContent[] labels = propertyName == "antiAliasingPreset"
                ? QualityPresetLabels
                : ToGuiContents(GraphicSettingInspectorSchema.GetPresetOptionLabels(propertyName));

            property.enumValueIndex = EditorGUILayout.IntPopup(
                Label(propertyName),
                property.enumValueIndex,
                labels,
                QualityPresetValues);
        }

        private static void DrawGameViewScale(SerializedProperty property, string propertyName)
        {
            property.enumValueIndex = EditorGUILayout.IntPopup(
                Label(propertyName),
                property.enumValueIndex,
                GameViewScaleLabels,
                GameViewScaleValues);
        }

        private static void DrawTextureImportProfile(SerializedProperty property)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, Label("textureImportProfile"), true);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                DrawEnumPopup(property.FindPropertyRelative("filterMode"), "filterMode", FilterModeLabels, FilterModeValues);
                EditorGUILayout.PropertyField(property.FindPropertyRelative("anisoLevel"), Label("anisoLevel"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("maxTextureSize"), Label("maxTextureSize"));
                DrawEnumPopup(property.FindPropertyRelative("compression"), "compression", CompressionLabels, CompressionValues);
                EditorGUILayout.PropertyField(property.FindPropertyRelative("alphaIsTransparency"), Label("alphaIsTransparency"));
            }
        }

        private static void DrawMaterialShaderProfile(SerializedProperty property)
        {
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, Label("materialShaderProfile"), true);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(property.FindPropertyRelative("applyOutline"), Label("applyOutline"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("outlineScale"), Label("outlineScale"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("outlineSize"), Label("outlineSize"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("applyAlphaCutoff"), Label("applyAlphaCutoff"));
                EditorGUILayout.PropertyField(property.FindPropertyRelative("alphaCutoff"), Label("alphaCutoff"));
                DrawEnumPopup(property.FindPropertyRelative("surfaceMode"), "surfaceMode", SurfaceModeLabels, SurfaceModeValues);
                EditorGUILayout.PropertyField(property.FindPropertyRelative("enableAlphaToCoverage"), Label("enableAlphaToCoverage"));
            }
        }

        private static void DrawEnumPopup(
            SerializedProperty property,
            string propertyName,
            GUIContent[] labels,
            int[] values)
        {
            if (property == null)
            {
                return;
            }

            property.enumValueIndex = EditorGUILayout.IntPopup(
                Label(propertyName),
                property.enumValueIndex,
                labels,
                values);
        }

        private void PromoteDirectSettingsToCustomPreset()
        {
            if (selectedCategory != GraphicSettingInspectorCategory.Advanced)
            {
                return;
            }

            SetEnumValue("textureResolution", (int)GraphicSettingQualityPreset.Custom);
            SetEnumValue("antiAliasingPreset", (int)GraphicSettingQualityPreset.Custom);
            SetEnumValue("renderSharpness", (int)GraphicSettingQualityPreset.Custom);
            SetEnumValue("modelEdgeAndAlpha", (int)GraphicSettingQualityPreset.Custom);
        }

        private void SetEnumValue(string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.enumValueIndex = value;
            }
        }

        private static bool IsUserDrivenInspectorEvent()
        {
            Event current = Event.current;
            return current == null ||
                   (current.type != EventType.Layout && current.type != EventType.Repaint);
        }

        private static void ScheduleAutoApply(GraphicSetting setting, GraphicSettingInspectorCategory category)
        {
            if (setting == null)
            {
                return;
            }

            int instanceId = setting.GetInstanceID();
            if (!PendingAutoApplyIds.Add(instanceId))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                PendingAutoApplyIds.Remove(instanceId);
                if (setting == null)
                {
                    return;
                }

                ApplyToTargets(setting, selectedSetting => ApplyChangedSettings(selectedSetting, category));
            };
        }

        private static void ApplyChangedSettings(GraphicSetting setting, GraphicSettingInspectorCategory category)
        {
            switch (category)
            {
                case GraphicSettingInspectorCategory.Texture:
                    ApplyTextureImports(setting);
                    break;
                case GraphicSettingInspectorCategory.Model:
                    ApplyMaterialShaderSettings(setting);
                    break;
                case GraphicSettingInspectorCategory.Advanced:
                    setting.ApplyNow();
                    ApplyTextureImports(setting);
                    ApplyMaterialShaderSettings(setting);
                    MarkSettingDirty(setting);
                    break;
                case GraphicSettingInspectorCategory.Quality:
                    setting.ApplyNow();
                    ApplyTextureImports(setting);
                    ApplyMaterialShaderSettings(setting);
                    GameViewScaleController.TryApply(setting.GameViewScaleMode);
                    MarkSettingDirty(setting);
                    break;
                default:
                    setting.ApplyNow();
                    MarkSettingDirty(setting);
                    break;
            }
        }

        private static void ApplyToTargets(GraphicSetting setting, Action<GraphicSetting> action)
        {
            var applied = new HashSet<GraphicSetting>();
            foreach (UnityEngine.Object candidate in Selection.objects)
            {
                if (candidate is GraphicSetting directSetting && applied.Add(directSetting))
                {
                    action(directSetting);
                    continue;
                }

                if (candidate is GameObject gameObject &&
                    gameObject.TryGetComponent(out GraphicSetting selectedSetting) &&
                    applied.Add(selectedSetting))
                {
                    action(selectedSetting);
                }
            }

            if (setting != null && applied.Add(setting))
            {
                action(setting);
            }
        }

        private static void ApplyTextureImports(GraphicSetting setting)
        {
            int changed = GraphicTextureImportEditorController.Apply(setting);
            if (changed > 0)
            {
                Debug.Log($"GraphicSetting 텍스처 임포트 자동 적용: changes={changed}");
            }
        }

        private static void ApplyMaterialShaderSettings(GraphicSetting setting)
        {
            GraphicMaterialShaderApplyResult result = GraphicMaterialShaderEditorController.Apply(setting);
            if (result.ChangedMaterials > 0 || result.ChangedProperties > 0)
            {
                Debug.Log($"GraphicSetting 모델 머티리얼 자동 적용: {result}");
            }
        }

        private static void MarkSettingDirty(GraphicSetting setting)
        {
            if (setting == null)
            {
                return;
            }

            EditorUtility.SetDirty(setting);
            if (!Application.isPlaying && setting.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(setting.gameObject.scene);
            }
        }

        private static GUIContent Label(string propertyName)
        {
            return new GUIContent(GraphicSettingInspectorSchema.GetPropertyDisplayName(propertyName));
        }

        private static GUIContent[] ToGuiContents(string[] labels)
        {
            var contents = new GUIContent[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                contents[i] = new GUIContent(labels[i]);
            }

            return contents;
        }
    }
}
