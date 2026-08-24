using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 리타게팅 안정성과 엄지 기준 상태를 진단 로그로 기록함.
    /// </summary>
    internal static class RetargetingDiagnosticReporter
    {
        internal static void LogPlaybackStability(PoseSpaceRetargeter retargeter)
        {
            if (retargeter == null)
            {
                return;
            }

            Debug.Log(
                $"[FBXImport] Retarget playback stability: " +
                $"clipTimeClamp={retargeter.clampLegacyAnimationVisualStep}, " +
                $"maxClipStep={retargeter.MaxLegacyAnimationStep:F4}s, " +
                $"stepSpikes={retargeter.LegacyAnimationStepSpikeCount}, " +
                $"poseSmooth={retargeter.PoseVisualSmoothingCount}, " +
                $"muscleOnlySmoothSkipped={retargeter.PoseVisualMuscleDeltaOnlySkippedCount}, " +
                $"maxPoseMuscleDelta={retargeter.MaxPoseVisualMaxMuscleDelta:F4}, " +
                $"hipsLocalClamp={retargeter.TargetHipsLocalPositionSpikeClampedCount}, " +
                $"maxHipsLocalDelta={retargeter.MaxTargetHipsLocalPositionDelta:F4}m, " +
                $"thumbReference[{BuildThumbReferenceSummary(retargeter)}]");
        }

#if UNITY_EDITOR
        internal static void LogEditorSmokeThumbState(
            string stage,
            string fbxFileName,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            float projectionMinPalmNormal,
            GameObject targetCharacter,
            PoseSpaceRetargeter retargeter)
        {
            if (targetCharacter == null)
            {
                return;
            }

            HumanoidThumbDeformationGuard thumbGuard =
                targetCharacter.GetComponent<HumanoidThumbDeformationGuard>();
            string leftGuard = thumbGuard != null
                ? thumbGuard.BuildThumbHelperDebugSummary(false)
                : "thumbGuard=<none>";
            string rightGuard = thumbGuard != null
                ? thumbGuard.BuildThumbHelperDebugSummary(true)
                : "thumbGuard=<none>";
            string leftRetargeter = retargeter != null
                ? retargeter.BuildThumbHelperRelationshipDebugSummary(true)
                : "retargeter=<none>";
            string rightRetargeter = retargeter != null
                ? retargeter.BuildThumbHelperRelationshipDebugSummary(false)
                : "retargeter=<none>";

            Debug.Log(
                $"[FBXImport] Editor smoke thumb state ({stage}): " +
                $"fbx={fbxFileName ?? "<none>"}, " +
                $"segment={FBXEditorDiagnosticPlanner.GetSegmentLabel(segment)}, " +
                $"projectionMin={projectionMinPalmNormal:F3}, " +
                $"thumbReference[{BuildThumbReferenceSummary(retargeter)}], " +
                $"guardLeft[{leftGuard}], guardRight[{rightGuard}], " +
                $"retargeterLeft[{leftRetargeter}], retargeterRight[{rightRetargeter}]");
        }
#endif

        internal static string BuildThumbReferenceSummary(PoseSpaceRetargeter retargeter)
        {
            if (retargeter == null)
            {
                return "retargeter=<none>";
            }

            Animator referenceAnimator = ReadRetargeterPrivateField<Animator>(
                retargeter,
                "_editorFingerReferenceAnimator");
            bool editorFingerRuntime = ReadRetargeterPrivateField<bool>(
                retargeter,
                "_useEditorFingerPoseReference");
            return
                $"retargeter={GetHierarchyPath(retargeter.transform)}, " +
                $"targetAnimator={GetHierarchyPath(retargeter.targetAnimator != null ? retargeter.targetAnimator.transform : null)}, " +
                $"thumbLocalRefConfig={retargeter.useManualAnimatorThumbLocalRotationReference}, " +
                $"preserveThumbMuscles={retargeter.preserveManualFingerReferenceThumbMuscles}, " +
                $"editorFingerRuntime={editorFingerRuntime}, " +
                $"referenceAnimator={GetHierarchyPath(referenceAnimator != null ? referenceAnimator.transform : null)}, " +
                $"manualThumbActive={retargeter.IsManualThumbLocalRotationReferenceActive}, " +
                $"suppressLeft={retargeter.ShouldSuppressLeftThumbPoseShapingGuard}, " +
                $"suppressRight={retargeter.ShouldSuppressRightThumbPoseShapingGuard}, " +
                $"leftLocalGuardClamp={retargeter.LastLeftThumbLocalRotationGuardClampCount}, " +
                $"rightLocalGuardClamp={retargeter.LastRightThumbLocalRotationGuardClampCount}, " +
                $"leftLocalGuardPreserve={retargeter.LastLeftThumbLocalRotationGuardPreserveCount}, " +
                $"rightLocalGuardPreserve={retargeter.LastRightThumbLocalRotationGuardPreserveCount}";
        }

        private static T ReadRetargeterPrivateField<T>(
            PoseSpaceRetargeter retargeter,
            string fieldName)
        {
            if (retargeter == null)
            {
                return default(T);
            }

            FieldInfo field = typeof(PoseSpaceRetargeter).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return default(T);
            }

            object value = field.GetValue(retargeter);
            return value is T typedValue ? typedValue : default(T);
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            var parts = new List<string>();
            Transform current = target;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
