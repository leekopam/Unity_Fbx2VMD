using System;
using System.Collections.Generic;
using UnityEditor;

namespace Fbx2Vmd.Settings.EditorTools
{
    public enum GraphicSettingInspectorCategory
    {
        Quality,
        Target,
        Recoding,
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
            "applyOnValidate"
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
            "녹화",
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
                case GraphicSettingInspectorCategory.Recoding:
                    return Array.Empty<string>();
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
}
