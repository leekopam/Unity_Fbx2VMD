using Assimp;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class AssimpRuntimeMaterialApplier
    {
        private const byte ALPHA_CUTOUT_OPAQUE_THRESHOLD = 250;
        private const float STANDARD_SHADER_CUTOUT_MODE = 1f;
        private const float STANDARD_SHADER_CUTOUT_THRESHOLD = 0.5f;
        private const string UNLIT_TRANSPARENT_CUTOUT_SHADER = "Unlit/Transparent Cutout";

        internal static void Apply(GameObject gameObject, Assimp.Mesh sourceMesh, Scene scene, string sourceDirectory)
        {
            if (gameObject == null || sourceMesh == null || scene == null)
            {
                return;
            }

            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Assimp.Material sourceMaterial = ResolveAssimpMaterial(scene, sourceMesh.MaterialIndex);
            if (sourceMaterial == null)
            {
                return;
            }

            renderer.sharedMaterial = CreateRuntimeMaterial(sourceMaterial, sourceDirectory);
        }

        private static Assimp.Material ResolveAssimpMaterial(Scene scene, int materialIndex)
        {
            if (scene == null || materialIndex < 0 || materialIndex >= scene.MaterialCount)
            {
                return null;
            }

            return scene.Materials[materialIndex];
        }

        private static UnityEngine.Material CreateRuntimeMaterial(Assimp.Material sourceMaterial, string sourceDirectory)
        {
            string texturePath = ResolveMainTexturePath(sourceMaterial, sourceDirectory);
            Texture2D texture = LoadMainTexture(texturePath);
            string materialName = string.IsNullOrWhiteSpace(sourceMaterial?.Name)
                ? "ImportedMaterial"
                : sourceMaterial.Name;

            // 툰 템플릿이 있으면 복제해 쓰고, 없으면 기존 셰이더 경로로 폴백한다.
            bool isToon = TryCreateToonMaterial(sourceMaterial?.Name, texture, out UnityEngine.Material material);
            if (!isToon)
            {
                material = CreateLegacyMaterial(texturePath, materialName);
            }

            material.name = materialName;
            ApplyReferenceMaterialDefaults(material);
            AssignMainTexture(material, texture, isToon);
            return material;
        }

        private static bool TryCreateToonMaterial(
            string sourceMaterialName, Texture2D texture, out UnityEngine.Material material)
        {
            material = null;
            bool hasAlpha = texture != null && TextureContainsTransparentPixels(texture);
            ToonMaterialRole surface = hasAlpha ? ToonMaterialRole.Cutout : ToonMaterialRole.Opaque;
            ToonMaterialRole role =
                ToonMaterialRoleResolver.ResolveCharacterRole(sourceMaterialName) ?? surface;
            // 피부·눈 템플릿은 불투명 변형이라 알파 텍스처가 배정되면 알파가 유실된다.
            // 알파가 있으면 부위 역할보다 표면 방식을 우선해 Cutout으로 내린다.
            if (hasAlpha &&
                (role == ToonMaterialRole.Skin || role == ToonMaterialRole.Eye))
            {
                role = ToonMaterialRole.Cutout;
            }

            return ToonMaterialLibrary.TryInstantiate(role, surface, out material);
        }

        private static UnityEngine.Material CreateLegacyMaterial(string texturePath, string materialName)
        {
            Shader shader = SelectRuntimeMaterialShader(texturePath);
            return new UnityEngine.Material(shader)
            {
                name = materialName
            };
        }

        private static Shader SelectRuntimeMaterialShader(string texturePath)
        {
            if (!string.IsNullOrEmpty(texturePath))
            {
                Shader textureCutoutShader = Shader.Find(UNLIT_TRANSPARENT_CUTOUT_SHADER);
                if (textureCutoutShader != null)
                {
                    return textureCutoutShader;
                }
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            return shader;
        }

        private static void ApplyReferenceMaterialDefaults(UnityEngine.Material material)
        {
            SetMaterialFloatIfSupported(material, "_Glossiness", 0f);
            SetMaterialFloatIfSupported(material, "_Metallic", 0f);
        }

        private static void SetMaterialFloatIfSupported(UnityEngine.Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static string ResolveMainTexturePath(Assimp.Material sourceMaterial, string sourceDirectory)
        {
            string textureReference = ResolveDiffuseTextureReference(sourceMaterial);
            string texturePath = FbxMaterialResolver.ResolveTextureCandidateFromDirectory(sourceDirectory, textureReference);
            if (string.IsNullOrEmpty(texturePath))
            {
                texturePath = FbxMaterialResolver.ResolveTextureCandidateFromMaterialName(
                    sourceDirectory,
                    sourceMaterial?.Name);
            }

            return texturePath;
        }

        private static Texture2D LoadMainTexture(string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                return null;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(texturePath);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileName(texturePath)
                };

                if (texture.LoadImage(bytes))
                {
                    return texture;
                }

                DestroyTexture(texture);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[FBXImport] 텍스처 불러오기 실패함. 경로={texturePath}, 오류={e.Message}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[FBXImport] 텍스처 적용 실패함. 경로={texturePath}, 오류={e.Message}");
            }

            return null;
        }

        private static void AssignMainTexture(UnityEngine.Material material, Texture2D texture, bool isToon)
        {
            if (material == null || texture == null)
            {
                return;
            }

            material.mainTexture = texture;
            // 레거시 머티리얼만 알파컷 상태를 추가 설정한다.
            // 툰 템플릿은 선택 시점에 이미 Opaque/Cutout이 정해져 있다.
            if (!isToon)
            {
                ApplyTextureMaterialState(material, texture);
            }
        }

        private static string ResolveDiffuseTextureReference(Assimp.Material sourceMaterial)
        {
            if (sourceMaterial == null)
            {
                return string.Empty;
            }

            if (sourceMaterial.GetMaterialTexture(TextureType.Diffuse, 0, out TextureSlot textureSlot))
            {
                return textureSlot.FilePath;
            }

            if (sourceMaterial.HasTextureDiffuse)
            {
                return sourceMaterial.TextureDiffuse.FilePath;
            }

            return string.Empty;
        }

        private static void ApplyTextureMaterialState(UnityEngine.Material material, Texture2D texture)
        {
            if (material == null)
            {
                return;
            }

            if (UsesCutoutShader(material) || TextureContainsTransparentPixels(texture))
            {
                ApplyAlphaCutoutMaterialState(material);
            }
        }

        private static bool UsesCutoutShader(UnityEngine.Material material)
        {
            return material != null
                && material.shader != null
                && string.Equals(material.shader.name, UNLIT_TRANSPARENT_CUTOUT_SHADER, System.StringComparison.Ordinal);
        }

        private static void ApplyAlphaCutoutMaterialState(UnityEngine.Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", STANDARD_SHADER_CUTOUT_MODE);
            }

            SetMaterialFloatIfSupported(material, "_Cutoff", STANDARD_SHADER_CUTOUT_THRESHOLD);
            SetMaterialFloatIfSupported(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            SetMaterialFloatIfSupported(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            SetMaterialFloatIfSupported(material, "_ZWrite", 1f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        private static bool TextureContainsTransparentPixels(Texture2D texture)
        {
            try
            {
                Color32[] pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a < ALPHA_CUTOUT_OPAQUE_THRESHOLD)
                    {
                        return true;
                    }
                }
            }
            catch (UnityException e)
            {
                Debug.LogWarning($"[FBXImport] 텍스처 알파 검사 건너뜀. 텍스처={texture.name}, 오류={e.Message}");
            }

            return false;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (Application.isEditor && !Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            else
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
