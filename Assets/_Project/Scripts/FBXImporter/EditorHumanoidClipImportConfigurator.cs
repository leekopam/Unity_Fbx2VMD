#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
            else if ((description.human == null || description.human.Length == 0) &&
                     TryBuildFallbackHumanDescription(model, description, out HumanDescription fallback))
            {
                // 자동 매핑이 비어 있는 리그에 알려진 본 명명 규칙으로 Humanoid 매핑을 생성함.
                Debug.Log(
                    $"[FBXImport] Humanoid 자동 매핑이 비어 명명 규칙 매핑을 적용함. " +
                    $"매핑 {fallback.human.Length}개: {assetPath}");
                importer.humanDescription = fallback;
                shouldReimport = true;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips != null && clips.Length > 0 &&
                !HumanoidClipImportPolicy.HasRootPoseContract(clips))
            {
                HumanoidClipImportPolicy.ApplyRootPoseContract(clips);
                importer.clipAnimations = clips;
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

        // 임포터 자동 매핑이 비어 나올 때 사용할 Humanoid 필수 본 집합.
        private static readonly string[] RequiredHumanBoneNames =
        {
            "Hips", "Spine", "Chest", "Head",
            "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg",
            "LeftFoot", "RightFoot",
            "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm",
            "LeftHand", "RightHand"
        };

        /// <summary>
        /// 자동 매핑이 비어 있는 리그의 본 이름을 알려진 명명 규칙으로 Humanoid 역할에 연결함.
        /// 필수 본이 하나라도 없으면 매핑을 만들지 않고 false를 반환해 기존 실패 경로를 유지함.
        /// </summary>
        internal static bool TryBuildFallbackHumanDescription(
            GameObject model,
            HumanDescription current,
            out HumanDescription result)
        {
            result = current;
            Transform[] bones = model == null ? null : model.GetComponentsInChildren<Transform>(true);
            if (bones == null || bones.Length == 0)
                return false;

            // 메쉬 표시용 노드가 본 이름과 겹쳐 잘못 매핑되지 않도록 실제 스킨 본만 후보로 인정함.
            // 스킨 본 정보가 아예 없는 모션 전용 파일은 전체 노드를 후보로 둠.
            var skinBones = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer renderer in
                     model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (Transform skinBone in renderer.bones)
                {
                    if (skinBone != null)
                        skinBones.Add(skinBone);
                }
            }

            List<HumanBone> mapped = MapFallbackRoles(
                bones,
                bone => skinBones.Count == 0 || skinBones.Contains(bone));
            if (!CoversRequiredHumanBones(mapped))
            {
                // 스킨 본 목록이 실제 관절을 누락한 파일은 제한을 해제하고 다시 매핑함.
                mapped = MapFallbackRoles(bones, _ => true);
            }

            if (!CoversRequiredHumanBones(mapped))
                return false;

            result.human = mapped.ToArray();
            result.skeleton = bones.Select(bone => new SkeletonBone
            {
                name = bone.name,
                position = bone.localPosition,
                rotation = bone.localRotation,
                scale = bone.localScale
            }).ToArray();
            return true;
        }

        private static List<HumanBone> MapFallbackRoles(
            Transform[] bones,
            Func<Transform, bool> allow)
        {
            var used = new HashSet<Transform>();
            var mapped = new List<HumanBone>();
            foreach ((string role, string[] candidates) in BuildFallbackBoneCandidates())
            {
                if (mapped.Exists(bone => bone.humanName == role))
                    continue;

                foreach (string candidate in candidates)
                {
                    if (HumanoidAvatarBuilder.TryFindUniqueMappedTransform(bones, candidate, out Transform match) &&
                        allow(match) &&
                        used.Add(match))
                    {
                        mapped.Add(new HumanBone
                        {
                            boneName = match.name,
                            humanName = role,
                            limit = new HumanLimit { useDefaultValues = true }
                        });
                        break;
                    }
                }
            }

            return mapped;
        }

        private static bool CoversRequiredHumanBones(List<HumanBone> mapped)
        {
            foreach (string required in RequiredHumanBoneNames)
            {
                if (!mapped.Exists(bone => bone.humanName == required))
                    return false;
            }

            return true;
        }

        // 리그 명명 규칙별 본 이름 후보. 앞쪽 후보부터 순서대로 하나만 매칭함.
        private static IEnumerable<(string Role, string[] Candidates)> BuildFallbackBoneCandidates()
        {
            yield return ("Hips", new[] { "Hips", "Pelvis" });
            yield return ("Spine", new[] { "Spine" });
            yield return ("Chest", new[] { "Chest", "Spine1" });
            yield return ("UpperChest", new[] { "UpperChest", "Spine2", "Spine3" });
            yield return ("Neck", new[] { "Neck" });
            yield return ("Head", new[] { "Head" });
            foreach (string side in new[] { "Left", "Right" })
            {
                string s = side;
                string l = side[0].ToString();
                yield return ($"{s}Shoulder", new[] { $"{s}Shoulder", $"{s}Clavicle", $"Shoulder{l}", $"Clavicle{l}" });
                yield return ($"{s}UpperArm", new[] { $"{s}UpperArm", $"{s}Arm", $"UpperArm{l}", $"Arm{l}" });
                yield return ($"{s}LowerArm", new[] { $"{s}LowerArm", $"{s}ForeArm", $"{s}Forearm", $"LowerArm{l}", $"Forearm{l}", $"ForeArm{l}", $"{s}Elbow", $"Elbow{l}" });
                yield return ($"{s}Hand", new[] { $"{s}Hand", $"Hand{l}", $"{s}Wrist", $"Wrist{l}" });
                yield return ($"{s}UpperLeg", new[] { $"{s}UpperLeg", $"{s}UpLeg", $"UpperLeg{l}", $"UpLeg{l}", $"{s}Thigh", $"Thigh{l}" });
                yield return ($"{s}LowerLeg", new[] { $"{s}LowerLeg", $"{s}Leg", $"LowerLeg{l}", $"Leg{l}", $"{s}Calf", $"Calf{l}", $"{s}Shin", $"Shin{l}", $"{s}Knee", $"Knee{l}" });
                yield return ($"{s}Foot", new[] { $"{s}Foot", $"Foot{l}", $"{s}Ankle", $"Ankle{l}" });
                yield return ($"{s}Toes", new[] { $"{s}Toes", $"{s}ToeBase", $"{s}Toe", $"Toes{l}", $"ToeBase{l}", $"Toe{l}" });
                var fingerNames = new (string Human, string Bone)[] { ("Thumb", "Thumb"), ("Index", "Index"), ("Middle", "Middle"), ("Ring", "Ring"), ("Little", "Pinky"), ("Little", "Little") };
                foreach ((string human, string bone) in fingerNames)
                {
                    for (int joint = 1; joint <= 3; joint++)
                    {
                        string slot = joint == 1 ? "Proximal" : joint == 2 ? "Intermediate" : "Distal";
                        yield return ($"{s} {human} {slot}", new[]
                        {
                            $"{s}Hand{bone}{joint}", $"{s}{bone}{joint}", $"{s}Hand{bone}{slot}", $"{s}{bone}{slot}"
                        });
                    }
                }
            }
        }
    }
}
#endif
