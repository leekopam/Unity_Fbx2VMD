#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBX 애니메이션을 Unity Humanoid 기준 클립으로 임포트하도록 설정함.
    /// </summary>
    internal static class EditorHumanoidClipImportConfigurator
    {
        internal static bool EnsureHumanoid(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException(
                    "Humanoid로 설정할 FBX asset 경로가 필요합니다.",
                    nameof(assetPath));
            }

            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter importer))
            {
                throw new InvalidOperationException(
                    $"ModelImporter를 찾을 수 없습니다: {assetPath}");
            }

            bool shouldReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                shouldReimport = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                shouldReimport = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                shouldReimport = true;
            }

            if (importer.animationCompression != ModelImporterAnimationCompression.Off)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                shouldReimport = true;
            }

            if (importer.optimizeGameObjects)
            {
                importer.optimizeGameObjects = false;
                shouldReimport = true;
            }

            if (shouldReimport)
            {
                importer.SaveAndReimport();
            }

            AnimationClip clip = EditorAnimationClipAssetLoader.LoadFirst(assetPath);
            if (clip == null || !clip.humanMotion)
            {
                throw new InvalidOperationException(
                    $"Humanoid AnimationClip 임포트에 실패했습니다. Avatar 본 매핑을 확인하세요: {assetPath}");
            }

            return shouldReimport;
        }
    }
}
#endif
