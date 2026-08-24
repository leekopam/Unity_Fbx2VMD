#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Unity Editor의 Humanoid 기준 clip을 찾아 retargeter에 적용함.
    /// </summary>
    internal static class EditorHumanoidReferenceApplier
    {
        internal static AnimationClip Apply(
            PoseSpaceRetargeter retargeter,
            string importedFilePath,
            string sourceFilePath,
            bool shouldUseMuscleReference,
            bool shouldUseRootTranslationReference)
        {
            if (!shouldUseMuscleReference || retargeter == null)
            {
                return null;
            }

            string relativePath = ResolveEditorHumanoidReferencePath(
                importedFilePath,
                sourceFilePath);
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            AnimationClip referenceClip = LoadHumanoidAnimationClip(relativePath);
            if (referenceClip == null)
            {
                Debug.LogWarning(
                    $"[FBXImport] Unity Editor Humanoid 기준 클립을 찾지 못했습니다: {relativePath}");
                return null;
            }

            Debug.Log(
                $"[FBXImport] Editor Humanoid muscle 기준 clip: {relativePath}/{referenceClip.name}");
            retargeter.ConfigureEditorHumanoidMuscleReference(referenceClip);
            if (shouldUseRootTranslationReference)
            {
                retargeter.ConfigureEditorHumanoidRootTranslationReference(referenceClip);
            }

            return referenceClip;
        }

        private static string ResolveEditorHumanoidReferencePath(
            string importedFilePath,
            string sourceFilePath)
        {
            string sourceRelativePath = FBXImportController.ToAssetRelativePath(
                sourceFilePath,
                Application.dataPath);
            string importedRelativePath = FBXImportController.ToAssetRelativePath(
                importedFilePath,
                Application.dataPath);
            string sourceFileName = string.IsNullOrEmpty(sourceFilePath)
                ? importedFilePath
                : sourceFilePath;
            return ResolveEditorHumanoidReferencePath(
                importedRelativePath,
                sourceRelativePath,
                sourceFileName,
                HasHumanoidAnimationClip);
        }

        internal static string ResolveEditorHumanoidReferencePath(
            string importedRelativePath,
            string sourceRelativePath,
            string sourceFileName,
            Func<string, bool> hasHumanoidAnimationClip)
        {
            if (!FBXImportController.IsControlledImportAssetPath(sourceRelativePath) &&
                hasHumanoidAnimationClip(sourceRelativePath))
            {
                return sourceRelativePath;
            }

            string fileName = Path.GetFileName(
                string.IsNullOrEmpty(sourceFileName)
                    ? importedRelativePath
                    : sourceFileName);
            if (!string.IsNullOrEmpty(fileName))
            {
                string manualReferencePath = Path.Combine(
                        "Assets",
                        "_Project",
                        "FBX",
                        fileName)
                    .Replace("\\", "/");
                if (hasHumanoidAnimationClip(manualReferencePath))
                {
                    return manualReferencePath;
                }
            }

            return hasHumanoidAnimationClip(importedRelativePath)
                ? importedRelativePath
                : string.Empty;
        }

        private static bool HasHumanoidAnimationClip(string relativePath)
        {
            return !string.IsNullOrEmpty(relativePath) &&
                LoadHumanoidAnimationClip(relativePath) != null;
        }

        private static AnimationClip LoadHumanoidAnimationClip(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            UnityEngine.Object[] assets =
                UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativePath);
            foreach (UnityEngine.Object asset in assets)
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (clip.humanMotion)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
#endif
