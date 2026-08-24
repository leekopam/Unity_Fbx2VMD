#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class EditorManualPoseReferenceOptions
    {
        internal GameObject ReferencePrefab { get; set; }
        internal RuntimeAnimatorController ReferenceController { get; set; }
        internal bool ShouldUseFingerPoseReference { get; set; }
        internal bool ShouldUseFullBodyPoseReference { get; set; }
        internal float FullBodyPoseReferenceWeight { get; set; }
        internal bool ShouldExcludeFullBodyLowerMuscles { get; set; }
        internal bool ShouldApplyFullBodyLowerMusclesOnly { get; set; }
        internal bool ShouldApplyFullBodyLegTwistMusclesOnly { get; set; }
        internal bool ShouldApplyFullBodyRightArmMusclesOnly { get; set; }
        internal bool ShouldApplyFullBodyLeftArmMusclesOnly { get; set; }
        internal bool ShouldApplyFullBodyRightSleeveChainMusclesOnly { get; set; }
        internal float FullBodyPoseFrameGateStart { get; set; }
        internal float FullBodyPoseFrameGateEnd { get; set; }
        internal bool ShouldUseHipsLocalPositionReference { get; set; }
        internal bool ShouldUseBodyRotationReference { get; set; }
        internal bool ShouldUseHandLocalRotationReference { get; set; }
        internal bool ShouldUseFootLocalRotationReference { get; set; }
        internal bool ShouldUseLowerBodySegmentDirectionReference { get; set; }
        internal bool ShouldUseFootHipsAlignedResidualYawReference { get; set; }
        internal bool ShouldUsePostSetHumanPoseRightEndpointPositionReference { get; set; }
        internal bool ShouldUsePostSetHumanPoseRightFootEvaluatorXzReference { get; set; }
        internal bool ShouldUsePreSetHumanPoseRightEndpointPositionReference { get; set; }
        internal bool ShouldUseBodyPositionXzReference { get; set; }
        internal bool ShouldUseRightSleeveSilhouetteOffsetReference { get; set; }
        internal float RightSleeveSilhouetteOffsetX { get; set; }
        internal float RightSleeveSilhouetteFrameGateStart { get; set; }
        internal float RightSleeveSilhouetteFrameGateEnd { get; set; }
        internal bool ShouldUseBipedIkFootPositionReference { get; set; }

        internal bool ShouldApply =>
            ShouldUseFingerPoseReference ||
            ShouldUseFullBodyPoseReference ||
            ShouldUseHipsLocalPositionReference ||
            ShouldUseBodyRotationReference ||
            ShouldUseHandLocalRotationReference ||
            ShouldUseFootLocalRotationReference ||
            ShouldUseLowerBodySegmentDirectionReference ||
            ShouldUseFootHipsAlignedResidualYawReference ||
            ShouldUsePostSetHumanPoseRightEndpointPositionReference ||
            ShouldUsePostSetHumanPoseRightFootEvaluatorXzReference ||
            ShouldUsePreSetHumanPoseRightEndpointPositionReference ||
            ShouldUseBodyPositionXzReference ||
            ShouldUseRightSleeveSilhouetteOffsetReference ||
            ShouldUseBipedIkFootPositionReference;
    }

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

        internal static void ApplyManualPoseReference(
            PoseSpaceRetargeter retargeter,
            AnimationClip referenceClip,
            EditorManualPoseReferenceOptions options)
        {
            if (options == null ||
                !options.ShouldApply ||
                retargeter == null ||
                referenceClip == null)
            {
                return;
            }

            GameObject referencePrefab = options.ReferencePrefab;
            if (referencePrefab == null)
            {
                referencePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Plugins/VMDRecorderSample/Models/TestModel/testPrefab.prefab");
            }

            RuntimeAnimatorController referenceController = options.ReferenceController;
            if (referenceController == null)
            {
                referenceController =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/_ManualReference/SampleAnimation/TestAnimator1_Manual.controller");
            }

            if (referenceController == null)
            {
                referenceController =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller");
            }

            if (referencePrefab == null || referenceController == null)
            {
                Debug.LogWarning(
                    "[FBXImport] 수동 기준 손가락 Reference prefab/controller를 찾지 못해 raw FBX finger curve를 사용합니다.");
                return;
            }

            retargeter.ConfigureEditorHumanoidFingerPoseReference(
                referencePrefab,
                referenceController,
                referenceClip,
                options.ShouldUseFingerPoseReference,
                options.ShouldUseFullBodyPoseReference,
                options.FullBodyPoseReferenceWeight,
                options.ShouldExcludeFullBodyLowerMuscles,
                options.ShouldApplyFullBodyLowerMusclesOnly,
                options.ShouldApplyFullBodyLegTwistMusclesOnly,
                options.ShouldApplyFullBodyRightArmMusclesOnly,
                options.ShouldApplyFullBodyLeftArmMusclesOnly,
                options.ShouldApplyFullBodyRightSleeveChainMusclesOnly,
                options.FullBodyPoseFrameGateStart,
                options.FullBodyPoseFrameGateEnd);
            retargeter.useYybRightSleeveSilhouetteLocalOffsetReference =
                options.ShouldUseRightSleeveSilhouetteOffsetReference;
            retargeter.yybRightSleeveSilhouetteLocalOffsetX =
                Mathf.Clamp(options.RightSleeveSilhouetteOffsetX, -0.2f, 0.2f);
            retargeter.yybRightSleeveSilhouetteLocalOffsetFrameGateStart =
                Mathf.Max(0f, options.RightSleeveSilhouetteFrameGateStart);
            retargeter.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd =
                Mathf.Max(0f, options.RightSleeveSilhouetteFrameGateEnd);
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
