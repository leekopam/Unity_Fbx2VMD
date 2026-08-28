using System;
using System.Collections.Generic;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal static class GraphicSettingInspectorAutoApplyController
    {
        private static readonly HashSet<int> PendingAutoApplyIds = new HashSet<int>();

        public static void Schedule(
            GraphicSetting setting,
            GraphicSettingInspectorCategory category)
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

                ApplyToTargets(
                    setting,
                    selectedSetting => ApplyChangedSettings(selectedSetting, category));
            };
        }

        private static void ApplyChangedSettings(
            GraphicSetting setting,
            GraphicSettingInspectorCategory category)
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

        private static void ApplyToTargets(
            GraphicSetting setting,
            Action<GraphicSetting> action)
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
            GraphicMaterialShaderApplyResult result =
                GraphicMaterialShaderEditorController.Apply(setting);
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
    }
}
