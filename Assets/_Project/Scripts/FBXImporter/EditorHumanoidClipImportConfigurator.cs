#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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

            HumanDescription description = importer.humanDescription;
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (TryResolveHumanBoneNames(model, description.human, out HumanBone[] resolved) &&
                !ReferenceEquals(description.human, resolved))
            {
                // 다른 리그의 템플릿 이름만 실제 본 이름으로 연결하고 기존 제한값과 골격은 보존함.
                description.human = resolved;
                importer.humanDescription = description;
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

        internal static bool TryResolveHumanBoneNames(GameObject model, HumanBone[] source,
            out HumanBone[] resolved)
        {
            resolved = source;
            if (model == null || source == null || source.Length == 0)
                return false;

            Transform[] bones = model.GetComponentsInChildren<Transform>(true);
            var roles = new HashSet<string>(StringComparer.Ordinal);
            var used = new HashSet<Transform>();
            var candidate = (HumanBone[])source.Clone();
            bool changed = false;
            for (int i = 0; i < candidate.Length; i++)
            {
                HumanBone bone = candidate[i];
                if (string.IsNullOrWhiteSpace(bone.humanName) || !roles.Add(bone.humanName) ||
                    !HumanoidAvatarBuilder.TryFindUniqueMappedTransform(bones, bone.boneName, out Transform match) ||
                    !used.Add(match))
                    return false;
                string name = match.name;
                changed |= !string.Equals(candidate[i].boneName, name, StringComparison.Ordinal);
                candidate[i].boneName = name;
            }

            // 일부 항목만 바뀌지 않도록 모든 이름을 확인한 뒤 결과를 반환함.
            resolved = changed ? candidate : source;
            return true;
        }
    }
}
#endif
