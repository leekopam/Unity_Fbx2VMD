using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.EditorTools
{
    /// <summary>
    /// Resources/ToonPresets 아래에 툰 템플릿 머티리얼과 무대용 포스트 프로필을 생성한다.
    /// 템플릿은 lilToon 인스펙터로 직접 다듬는 에셋이며, 이 빌더는 초기값만 만든다.
    /// 헤드리스: -executeMethod Fbx2Vmd.EditorTools.ToonMaterialLibraryBuilder.Build
    /// </summary>
    public static class ToonMaterialLibraryBuilder
    {
        const string PresetFolder = "Assets/Resources/ToonPresets";

        // 표면 역할 → lilToon 쉐이더 변형명. 설치 버전에 따라 없으면 다음 후보를 시도한다.
        static readonly (string role, string[] shaderCandidates, int renderQueue)[] surfaceDefs =
        {
            ("Opaque", new[] { "Hidden/lilToonOutline", "lilToon", "Standard" }, -1),
            ("Cutout", new[] { "Hidden/lilToonCutoutOutline", "Hidden/lilToonCutout", "Standard" }, 2450),
            ("Transparent", new[] { "Hidden/lilToonTransparentOutline", "Hidden/lilToonTransparent", "Standard" }, 3000),
        };

        // 캐릭터 부위 역할 → 베이스가 되는 표면 역할 + 미쿠 공연 룩 초기값.
        // Face는 Cutout 베이스: 눈썹·속눈썹·입 등 알파 텍스처 파츠가 같은 프리셋을 쓴다.
        static readonly Dictionary<string, string> roleSurface = new Dictionary<string, string>
        {
            { "Skin", "Opaque" },
            { "Hair", "Cutout" },
            { "Eye", "Opaque" },
            { "Face", "Cutout" },
        };

        [MenuItem("Tools/Toon/Build Toon Presets")]
        public static void Build()
        {
            EnsureFolder(PresetFolder);

            foreach ((string role, string[] candidates, int renderQueue) in surfaceDefs)
            {
                Material material = CreateFromCandidates(candidates, $"Toon_{role}");
                if (material == null)
                {
                    Debug.LogWarning($"[Toon] {role}용 셰이더를 찾지 못했습니다.");
                    continue;
                }

                ApplySurfaceState(material, role, renderQueue);
                ApplyLookDefaults(material, role);
                SaveMaterial(material);
            }

            foreach (KeyValuePair<string, string> pair in roleSurface)
            {
                Material source = LoadPreset($"Toon_{pair.Value}");
                if (source == null)
                {
                    continue;
                }

                var material = new Material(source) { name = $"Toon_{pair.Key}" };
                ApplyRoleDefaults(material, pair.Key);
                SaveMaterial(material);
            }

            ToonStageLookBuilder.Build(PresetFolder);
            AssetDatabase.SaveAssets();
            Debug.Log("[Toon] 툰 프리셋 생성 완료.");
        }

        private static Material CreateFromCandidates(string[] candidates, string name)
        {
            foreach (string shaderName in candidates)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    continue;
                }

                if (!shader.name.StartsWith("Hidden/lilToon") && shader.name != "lilToon")
                {
                    Debug.LogWarning(
                        $"[Toon] lilToon 셰이더를 찾지 못해 {shader.name}로 {name} 프리셋을 만듭니다. " +
                        "lilToon 설치 후 프리셋을 지우고 다시 빌드하세요.");
                }
                return new Material(shader) { name = name };
            }

            return null;
        }

        // Opaque/Cutout/Transparent 표면 상태(블렌드·큐·렌더타입)를 템플릿에 굽는다.
        private static void ApplySurfaceState(Material material, string role, int renderQueue)
        {
            switch (role)
            {
                case "Cutout":
                    SetFloat(material, "_TransparentMode", 1f);
                    SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                    SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                    SetFloat(material, "_ZWrite", 1f);
                    SetFloat(material, "_Cutoff", 0.5f);
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    break;
                case "Transparent":
                    SetFloat(material, "_TransparentMode", 2f);
                    SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    SetFloat(material, "_ZWrite", 0f);
                    material.SetOverrideTag("RenderType", "Transparent");
                    break;
            }

            if (renderQueue >= 0)
            {
                material.renderQueue = renderQueue;
            }
        }

        // 공연 툰 룩 공통 기본값 — 그림자는 2단 선명하게, 얇은 아웃라인, 림·역광 활성.
        private static void ApplyLookDefaults(Material material, string role)
        {
            SetFloat(material, "_ShadowStrength", 0.55f);
            SetFloat(material, "_ShadowBorder", 0.5f);
            SetFloat(material, "_ShadowBlur", 0f);
            SetColor(material, "_ShadowColor", new Color(0.82f, 0.80f, 0.90f));
            SetFloat(material, "_Shadow2ndBorder", 0.25f);
            SetFloat(material, "_Shadow2ndBlur", 0f);
            SetColor(material, "_Shadow2ndColor", new Color(0.66f, 0.64f, 0.78f));

            SetFloat(material, "_OutlineWidth", 0.05f);
            SetColor(material, "_OutlineColor", new Color(0.05f, 0.05f, 0.08f));

            // lilToon은 기능 토글(_Use*)이 꺼져 있으면 파라미터만 있어도 적용되지 않는다.
            SetFloat(material, "_UseRim", 1f);
            SetColor(material, "_RimColor", new Color(1f, 1f, 1f, 0.5f));
            SetFloat(material, "_RimBorder", 0.5f);
            SetFloat(material, "_RimBlur", 0.65f);
            SetFloat(material, "_RimFresnelPower", 3.5f);

            SetFloat(material, "_UseBacklight", 1f);
            SetColor(material, "_BacklightColor", new Color(0.85f, 0.80f, 0.70f));
            SetFloat(material, "_BacklightMainStrength", 0.55f);
            SetFloat(material, "_BacklightBorder", 0.35f);
            SetFloat(material, "_BacklightBlur", 0.05f);
        }

        // 부위별 미세 조정 — 피부는 밝게, 머리카락은 그림자 강하게.
        private static void ApplyRoleDefaults(Material material, string role)
        {
            switch (role)
            {
                case "Skin":
                    SetFloat(material, "_ShadowStrength", 0.35f);
                    SetFloat(material, "_ShadowBorder", 0.55f);
                    SetColor(material, "_ShadowColor", new Color(0.88f, 0.80f, 0.78f));
                    SetFloat(material, "_OutlineWidth", 0.06f);
                    break;
                case "Hair":
                    SetFloat(material, "_ShadowStrength", 0.6f);
                    SetFloat(material, "_ShadowBorder", 0.45f);
                    break;
                case "Eye":
                    SetFloat(material, "_ShadowStrength", 0.25f);
                    SetFloat(material, "_OutlineWidth", 0.04f);
                    break;
                case "Face":
                    // 얼굴은 그림자를 거의 치지 않는다 — 세카이식 무그림자에 가까운 룩.
                    SetFloat(material, "_ShadowStrength", 0.12f);
                    SetFloat(material, "_ShadowBorder", 0.6f);
                    SetColor(material, "_ShadowColor", new Color(0.95f, 0.88f, 0.86f));
                    SetFloat(material, "_OutlineWidth", 0.04f);
                    break;
            }
        }

        private static void SaveMaterial(Material material)
        {
            string path = $"{PresetFolder}/{material.name}.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // 이미 에셋이 있으면 아티스트가 다듬은 값을 덮지 않고 건너뛴다.
                Debug.Log($"[Toon] 기존 프리셋 유지: {path}");
                Object.DestroyImmediate(material);
                return;
            }

            AssetDatabase.CreateAsset(material, path);
            Debug.Log($"[Toon] 프리셋 생성: {path}");
        }

        private static Material LoadPreset(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{PresetFolder}/{name}.mat");
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }
    }
}
