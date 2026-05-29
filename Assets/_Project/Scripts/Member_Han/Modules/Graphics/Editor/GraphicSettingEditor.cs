using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;

namespace Member_Han.Modules.Graphics.EditorTools
{
    public enum GraphicSettingInspectorCategory
    {
        Quality,
        Target,
        Texture,
        Model,
        Advanced
    }

    public static class GraphicSettingInspectorSchema
    {
        private static readonly string[] QualityFields =
        {
            "textureResolution",
            "antiAliasingPreset",
            "renderSharpness",
            "modelEdgeAndAlpha",
            "gameViewScaleMode"
        };

        private static readonly string[] TargetFields =
        {
            "targetCamera",
            "targetRenderPipelineAsset",
            "builtInPostProcessResources",
            "applyOnAwake",
            "applyOnValidate",
            "applyBackgroundColor",
            "backgroundColor"
        };

        private static readonly string[] TextureFields =
        {
            "textureResolution",
            "textureSourceRoots",
            "textureImportTargets",
            "textureAssetFolders",
            "textureImportProfile"
        };

        private static readonly string[] ModelFields =
        {
            "modelEdgeAndAlpha",
            "materialSourceRoots",
            "materialShaderTargets",
            "materialAssetFolders",
            "materialShaderProfile"
        };

        private static readonly string[] AdvancedFields =
        {
            "antiAliasing",
            "smaaQuality",
            "enableCameraPostProcessing",
            "enableCameraMsaa",
            "msaaSampleCount",
            "renderScale",
            "textureImportProfile",
            "materialShaderProfile"
        };

        private static readonly Dictionary<string, string> PropertyLabels = new Dictionary<string, string>
        {
            { "targetCamera", "대상 카메라" },
            { "targetRenderPipelineAsset", "URP 렌더 파이프라인 에셋" },
            { "builtInPostProcessResources", "Built-in Post Processing 리소스" },
            { "applyOnAwake", "실행 시작 시 자동 적용" },
            { "applyOnValidate", "Unity OnValidate 자동 적용" },
            { "textureResolution", "텍스처 Import 기준" },
            { "antiAliasingPreset", "GameView 안티앨리어싱" },
            { "renderSharpness", "렌더 스케일 기준" },
            { "modelEdgeAndAlpha", "모델 윤곽선/알파 기준" },
            { "antiAliasing", "후처리 안티앨리어싱 방식" },
            { "smaaQuality", "SMAA 품질" },
            { "enableCameraPostProcessing", "카메라 후처리 사용" },
            { "enableCameraMsaa", "카메라 MSAA 사용" },
            { "msaaSampleCount", "MSAA 샘플 수" },
            { "renderScale", "URP 렌더 스케일" },
            { "applyBackgroundColor", "카메라 배경색 변경" },
            { "backgroundColor", "카메라 배경색" },
            { "gameViewScaleMode", "GameView 확대 표시" },
            { "textureImportProfile", "직접 설정: 텍스처 Import" },
            { "textureSourceRoots", "텍스처 대상 루트 오브젝트" },
            { "textureImportTargets", "텍스처 직접 대상" },
            { "textureAssetFolders", "텍스처 대상 폴더" },
            { "materialShaderProfile", "직접 설정: 모델 머티리얼" },
            { "materialSourceRoots", "모델 대상 루트 오브젝트" },
            { "materialShaderTargets", "머티리얼 직접 대상" },
            { "materialAssetFolders", "머티리얼 대상 폴더" },
            { "filterMode", "필터 방식" },
            { "anisoLevel", "비등방성 필터링 단계" },
            { "maxTextureSize", "최대 텍스처 크기" },
            { "compression", "텍스처 압축" },
            { "alphaIsTransparency", "알파를 투명도로 처리" },
            { "applyOutline", "윤곽선 값 변경" },
            { "outlineScale", "_EdgeScale 윤곽선 스케일" },
            { "outlineSize", "_EdgeSize 윤곽선 크기" },
            { "applyAlphaCutoff", "알파 컷오프 변경" },
            { "alphaCutoff", "_Cutoff 알파 컷오프" },
            { "surfaceMode", "표면 렌더링 모드" },
            { "enableAlphaToCoverage", "Alpha To Coverage 사용" }
        };

        public static string[] CategoryLabels { get; } =
        {
            "품질",
            "대상",
            "텍스처",
            "모델",
            "고급"
        };

        public static Type CategoryEnumType => typeof(GraphicSettingInspectorCategory);

        public static string[] GetVisiblePropertyNames(GraphicSettingInspectorCategory category)
        {
            switch (category)
            {
                case GraphicSettingInspectorCategory.Target:
                    return TargetFields;
                case GraphicSettingInspectorCategory.Texture:
                    return TextureFields;
                case GraphicSettingInspectorCategory.Model:
                    return ModelFields;
                case GraphicSettingInspectorCategory.Advanced:
                    return AdvancedFields;
                default:
                    return QualityFields;
            }
        }

        public static string GetPropertyDisplayName(string propertyName)
        {
            return PropertyLabels.TryGetValue(propertyName, out string label) ? label : ObjectNames.NicifyVariableName(propertyName);
        }

        public static string[] GetPresetOptionLabels(string propertyName)
        {
            switch (propertyName)
            {
                case "textureResolution":
                    return new[] { "작업용 2K", "표준 4K", "검수용 원본(최대 8K)", "세부값 직접 입력" };
                case "antiAliasingPreset":
                    return new[] { "빠른 확인(FXAA/MSAA 2x)", "일반 확인(SMAA/MSAA 4x)", "확대 검수(SMAA/MSAA 8x)", "세부값 직접 입력" };
                case "renderSharpness":
                    return new[] { "렌더 스케일 1.0x", "렌더 스케일 1.25x", "렌더 스케일 1.5x", "세부값 직접 입력" };
                case "modelEdgeAndAlpha":
                    return new[] { "윤곽선 생략", "기본 윤곽선", "얇은 윤곽선", "세부값 직접 입력" };
                default:
                    return Array.Empty<string>();
            }
        }

        public static bool AppliesAutomatically(GraphicSettingInspectorCategory category)
        {
            return true;
        }

        public static bool UsesManualApplyButton(GraphicSettingInspectorCategory category)
        {
            return false;
        }
    }

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
                    GraphicSettingGameViewScaleUtility.TryApply(setting.GameViewScaleMode);
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
            int changed = GraphicSettingTextureImportEditorUtility.Apply(setting);
            if (changed > 0)
            {
                Debug.Log($"GraphicSetting 텍스처 Import 자동 적용: 변경={changed}");
            }
        }

        private static void ApplyMaterialShaderSettings(GraphicSetting setting)
        {
            GraphicMaterialShaderApplyResult result = GraphicSettingMaterialShaderEditorUtility.Apply(setting);
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

    public static class GraphicSettingMaterialShaderEditorUtility
    {
        public static GraphicMaterialShaderApplyResult Apply(GraphicSetting setting)
        {
            if (setting == null)
            {
                return new GraphicMaterialShaderApplyResult(0, 0, 0, 0, 0);
            }

            Material[] materials = CollectMaterials(setting);
            GraphicMaterialShaderApplyResult result = GraphicMaterialShaderUtility.Apply(
                setting.CreateMaterialShaderPlan(),
                materials);

            if (result.ChangedMaterials > 0)
            {
                foreach (Material material in materials)
                {
                    if (material != null)
                    {
                        EditorUtility.SetDirty(material);
                    }
                }

                AssetDatabase.SaveAssets();
            }

            return result;
        }

        private static Material[] CollectMaterials(GraphicSetting setting)
        {
            var materials = new List<Material>();
            var seen = new HashSet<Material>();

            AddMaterials(setting.MaterialShaderTargets, materials, seen);

            foreach (GameObject root in setting.MaterialSourceRoots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    AddMaterials(renderer.sharedMaterials, materials, seen);
                }
            }

            foreach (string folder in setting.MaterialAssetFolders)
            {
                if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material != null && seen.Add(material))
                    {
                        materials.Add(material);
                    }
                }
            }

            return materials.ToArray();
        }

        private static void AddMaterials(
            IEnumerable<Material> candidates,
            ICollection<Material> materials,
            ISet<Material> seen)
        {
            if (candidates == null)
            {
                return;
            }

            foreach (Material material in candidates)
            {
                if (material != null && seen.Add(material))
                {
                    materials.Add(material);
                }
            }
        }
    }

    public static class GraphicSettingTextureImportEditorUtility
    {
        public static int Apply(GraphicSetting setting)
        {
            if (setting == null)
            {
                return 0;
            }

            GraphicTextureImportPlan plan = setting.CreateTextureImportPlan();
            int changed = 0;
            foreach (Texture2D texture in CollectTextures(setting))
            {
                string path = AssetDatabase.GetAssetPath(texture);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool dirty = SetIfDifferent(importer, plan);

                if (!dirty)
                {
                    continue;
                }

                importer.SaveAndReimport();
                changed++;
            }

            return changed;
        }

        private static bool SetIfDifferent(TextureImporter importer, GraphicTextureImportPlan plan)
        {
            bool dirty = false;
            if (importer.filterMode != plan.FilterMode)
            {
                importer.filterMode = plan.FilterMode;
                dirty = true;
            }

            if (importer.anisoLevel != plan.AnisoLevel)
            {
                importer.anisoLevel = plan.AnisoLevel;
                dirty = true;
            }

            if (importer.maxTextureSize != plan.MaxTextureSize)
            {
                importer.maxTextureSize = plan.MaxTextureSize;
                dirty = true;
            }

            if (importer.alphaIsTransparency != plan.AlphaIsTransparency)
            {
                importer.alphaIsTransparency = plan.AlphaIsTransparency;
                dirty = true;
            }

            TextureImporterCompression compression = importer.textureCompression;
            switch (plan.Compression)
            {
                case GraphicTextureCompressionPreference.None:
                    compression = TextureImporterCompression.Uncompressed;
                    break;
                case GraphicTextureCompressionPreference.HighQuality:
                    compression = TextureImporterCompression.CompressedHQ;
                    break;
            }

            if (plan.Compression != GraphicTextureCompressionPreference.Keep && importer.textureCompression != compression)
            {
                importer.textureCompression = compression;
                dirty = true;
            }

            return dirty;
        }

        private static IEnumerable<Texture2D> CollectTextures(GraphicSetting setting)
        {
            var seen = new HashSet<Texture2D>();

            foreach (Texture2D texture in setting.TextureImportTargets)
            {
                if (texture != null && seen.Add(texture))
                {
                    yield return texture;
                }
            }

            foreach (GameObject root in setting.TextureSourceRoots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null)
                        {
                            continue;
                        }

                        foreach (string propertyName in material.GetTexturePropertyNames())
                        {
                            if (material.GetTexture(propertyName) is Texture2D texture && seen.Add(texture))
                            {
                                yield return texture;
                            }
                        }
                    }
                }
            }

            foreach (string folder in setting.TextureAssetFolders)
            {
                if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture != null && seen.Add(texture))
                    {
                        yield return texture;
                    }
                }
            }
        }
    }

    public static class GraphicSettingGameViewScaleUtility
    {
        public static bool TryApply(GraphicGameViewScaleMode mode)
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                Debug.LogWarning("GraphicSetting이 UnityEditor.GameView를 찾지 못했습니다.");
                return false;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView == null)
            {
                return false;
            }

            gameView.Show();
            if (mode == GraphicGameViewScaleMode.OneX && TrySetZoomAreaScale(gameView, Vector2.one))
            {
                gameView.Repaint();
                return true;
            }

            if (mode == GraphicGameViewScaleMode.Fit && TryInvokeSizeSelection(gameView, 0))
            {
                gameView.Repaint();
                return true;
            }

            Debug.LogWarning("현재 Unity 버전에서 GraphicSetting이 GameView 확대 표시를 변경하지 못했습니다.");
            return false;
        }

        private static bool TrySetZoomAreaScale(EditorWindow gameView, Vector2 scale)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo zoomAreaField = gameView.GetType().GetField("m_ZoomArea", flags);
            object zoomArea = zoomAreaField?.GetValue(gameView);
            if (zoomArea == null)
            {
                return false;
            }

            FieldInfo scaleField = zoomArea.GetType().GetField("m_Scale", flags);
            if (scaleField == null || scaleField.FieldType != typeof(Vector2))
            {
                return false;
            }

            scaleField.SetValue(zoomArea, scale);
            return true;
        }

        private static bool TryInvokeSizeSelection(EditorWindow gameView, int index)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = gameView.GetType().GetMethod("SizeSelectionCallback", flags);
            if (method == null)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 2)
            {
                method.Invoke(gameView, new object[] { index, null });
                return true;
            }

            if (parameters.Length == 1)
            {
                method.Invoke(gameView, new object[] { index });
                return true;
            }

            return false;
        }
    }

    public static class GraphicSettingSceneInstaller
    {
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string DefaultPostProcessResourcesPath =
            "Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset";

        [MenuItem("Tools/Graphics/Install Graphic Setting In Main_Auto")]
        public static void InstallMainAuto()
        {
            var scene = EditorSceneManager.OpenScene(MainAutoScenePath);
            GraphicSetting setting = EnsureInActiveScene();
            Selection.activeObject = setting.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"GraphicSetting 설치 완료: {setting.gameObject.name}");
        }

        public static GraphicSetting EnsureInActiveScene()
        {
            GameObject root = GameObject.Find("Setting");
            if (root == null)
            {
                root = new GameObject("Setting");
            }

            GraphicSetting setting = root.GetComponent<GraphicSetting>();
            if (setting == null)
            {
                setting = root.AddComponent<GraphicSetting>();
            }

            RemoveLegacyGraphicSettingChild(root);
            ConfigureDefaults(setting);
            return setting;
        }

        private static void RemoveLegacyGraphicSettingChild(GameObject root)
        {
            Transform child = root.transform.Find("Graphic Setting");
            if (child == null || child.GetComponent<GraphicSetting>() == null || child.childCount > 0)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }

        private static void ConfigureDefaults(GraphicSetting setting)
        {
            UniversalRenderPipelineAsset pipelineAsset = ResolveUniversalRenderPipelineAsset();
            var serialized = new SerializedObject(setting);
            serialized.FindProperty("targetCamera").objectReferenceValue = Camera.main;
            serialized.FindProperty("targetRenderPipelineAsset").objectReferenceValue = pipelineAsset;
            serialized.FindProperty("builtInPostProcessResources").objectReferenceValue =
                ResolvePostProcessResources();
            serialized.FindProperty("applyOnAwake").boolValue = true;
            serialized.FindProperty("applyOnValidate").boolValue = false;
            serialized.FindProperty("textureResolution").enumValueIndex = (int)GraphicSettingQualityPreset.Balanced;
            serialized.FindProperty("antiAliasingPreset").enumValueIndex = (int)GraphicSettingQualityPreset.Quality;
            serialized.FindProperty("renderSharpness").enumValueIndex = (int)GraphicSettingQualityPreset.Balanced;
            serialized.FindProperty("modelEdgeAndAlpha").enumValueIndex = (int)GraphicSettingQualityPreset.Balanced;
            serialized.FindProperty("antiAliasing").enumValueIndex = (int)GraphicAntiAliasingMode.SMAA;
            serialized.FindProperty("smaaQuality").enumValueIndex = (int)AntialiasingQuality.High;
            serialized.FindProperty("enableCameraPostProcessing").boolValue = true;
            serialized.FindProperty("enableCameraMsaa").boolValue = true;
            serialized.FindProperty("msaaSampleCount").intValue = 8;
            serialized.FindProperty("renderScale").floatValue = pipelineAsset == null ? 1.0f : 1.25f;
            serialized.FindProperty("applyBackgroundColor").boolValue = true;
            serialized.FindProperty("backgroundColor").colorValue = new Color(0.5f, 0.5f, 0.5f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static UniversalRenderPipelineAsset ResolveUniversalRenderPipelineAsset()
        {
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset qualityAsset)
            {
                return qualityAsset;
            }

            if (GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset graphicsAsset)
            {
                return graphicsAsset;
            }

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultAsset)
            {
                return defaultAsset;
            }

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset currentAsset)
            {
                return currentAsset;
            }

            return null;
        }

        private static PostProcessResources ResolvePostProcessResources()
        {
            return AssetDatabase.LoadAssetAtPath<PostProcessResources>(DefaultPostProcessResourcesPath);
        }
    }
}
