using System.Collections.Generic;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    public static class GraphicTextureImportEditorController
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
}
