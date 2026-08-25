#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class EditorAnimationClipAssetLoader
    {
        internal static AnimationClip LoadFirst(string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.humanMotion)
                {
                    return clip;
                }
            }

            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }
    }
}
#endif
