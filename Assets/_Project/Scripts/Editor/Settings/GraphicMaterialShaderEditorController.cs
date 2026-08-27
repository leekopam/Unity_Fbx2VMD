using System.Collections.Generic;
using Fbx2Vmd.Settings;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    public static class GraphicMaterialShaderEditorController
    {
        public static GraphicMaterialShaderApplyResult Apply(GraphicSetting setting)
        {
            if (setting == null)
            {
                return new GraphicMaterialShaderApplyResult(0, 0, 0, 0, 0);
            }

            Material[] materials = CollectMaterials(setting);
            GraphicMaterialShaderApplyResult result = GraphicMaterialShaderController.Apply(
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
}
