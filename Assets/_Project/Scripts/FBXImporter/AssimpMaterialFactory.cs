using System.IO;
using Assimp;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public static class AssimpMaterialFactory
    {
        private const byte ALPHA_CUTOUT_OPAQUE_THRESHOLD = 250;
        private const float STANDARD_SHADER_CUTOUT_MODE = 1f;
        private const float STANDARD_SHADER_CUTOUT_THRESHOLD = 0.5f;
        private const string UNLIT_TRANSPARENT_CUTOUT_SHADER = "Unlit/Transparent Cutout";

        /// <summary>
        /// Assimp 메시의 런타임 Material을 할당함. UnityEngine.Object를 생성하므로 메인 스레드에서 호출해야 함.
        /// </summary>
        public static void AssignRuntimeMaterial(GameObject go, Assimp.Mesh asmMesh, Scene scene, string sourceDirectory)
        {
            if (go == null || asmMesh == null || scene == null)
            {
                return;
            }

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            Assimp.Material sourceMaterial = ResolveAssimpMaterial(scene, asmMesh.MaterialIndex);
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
            Shader shader = SelectRuntimeMaterialShader(texturePath);

            var material = new UnityEngine.Material(shader)
            {
                name = string.IsNullOrWhiteSpace(sourceMaterial?.Name)
                    ? "ImportedMaterial"
                    : sourceMaterial.Name
            };

            ApplyReferenceMaterialDefaults(material);
            AssignMainTexture(material, texturePath);
            return material;
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

        private static void AssignMainTexture(UnityEngine.Material material, string texturePath)
        {
            if (string.IsNullOrEmpty(texturePath))
            {
                return;
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
                    material.mainTexture = texture;
                    ApplyTextureMaterialState(material, texture);
                    return;
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
