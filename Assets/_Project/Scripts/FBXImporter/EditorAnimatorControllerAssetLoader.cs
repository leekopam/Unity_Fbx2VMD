#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class EditorAnimatorControllerAssetLoader
    {
        internal static RuntimeAnimatorController LoadFirst(params string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return null;
            }

            foreach (string assetPath in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    continue;
                }

                RuntimeAnimatorController controller =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(assetPath);
                if (controller != null)
                {
                    return controller;
                }
            }

            return null;
        }
    }
}
#endif
