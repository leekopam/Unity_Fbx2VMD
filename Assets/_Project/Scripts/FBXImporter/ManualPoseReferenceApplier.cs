using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 수동 포즈 기준 Animator에서 대상 Transform으로 로컬 회전과 Hips·Foot 위치를 적용함.
    /// </summary>
    internal static class ManualPoseReferenceApplier
    {
        internal static int ApplyExactLocalRotationReference(
            Animator referenceAnimator,
            Animator targetAnimator,
            HumanBodyBones bone)
        {
            if (referenceAnimator == null || targetAnimator == null)
            {
                return 0;
            }

            Transform source = referenceAnimator.GetBoneTransform(bone);
            Transform target = targetAnimator.GetBoneTransform(bone);
            if (source == null || target == null)
            {
                return 0;
            }

            Quaternion sourceRotation = source.localRotation;
            if (!IsFinite(sourceRotation) || Quaternion.Angle(target.localRotation, sourceRotation) <= 0.001f)
            {
                return 0;
            }

            target.localRotation = sourceRotation;
            return 1;
        }

        internal static int ApplyBlendedLocalRotationReference(
            Animator referenceAnimator,
            Animator targetAnimator,
            HumanBodyBones bone,
            float weight)
        {
            if (referenceAnimator == null || targetAnimator == null)
            {
                return 0;
            }

            Transform source = referenceAnimator.GetBoneTransform(bone);
            Transform target = targetAnimator.GetBoneTransform(bone);
            if (source == null || target == null)
            {
                return 0;
            }

            if (!TryCalculateLocalRotationReference(
                    source.localRotation,
                    target.localRotation,
                    weight,
                    out Quaternion nextLocalRotation))
            {
                return 0;
            }

            target.localRotation = nextLocalRotation;
            return 1;
        }

        internal static bool TryCalculateLocalRotationReference(
            Quaternion referenceLocalRotation,
            Quaternion currentLocalRotation,
            float weight,
            out Quaternion nextLocalRotation)
        {
            nextLocalRotation = currentLocalRotation;
            if (!IsFinite(referenceLocalRotation) || !IsFinite(currentLocalRotation))
            {
                return false;
            }

            if (Quaternion.Angle(currentLocalRotation, referenceLocalRotation) <= 0.001f)
            {
                return false;
            }

            nextLocalRotation = Quaternion.Slerp(
                currentLocalRotation,
                referenceLocalRotation,
                Mathf.Clamp01(weight));
            if (!IsFinite(nextLocalRotation))
            {
                nextLocalRotation = currentLocalRotation;
                return false;
            }

            return true;
        }

        internal static bool TryResolveHipsTransforms(
            Animator referenceAnimator,
            Animator targetAnimator,
            out Transform referenceHips,
            out Transform targetHips)
        {
            referenceHips = null;
            targetHips = null;
            if (referenceAnimator == null || targetAnimator == null)
            {
                return false;
            }

            referenceHips = referenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            return referenceHips != null && targetHips != null;
        }

        internal static bool TryResolveHipsLocalPositionReference(
            Animator referenceAnimator,
            Animator targetAnimator,
            out Transform targetHips,
            out Vector3 referenceCurrentLocalPosition,
            out Vector3 currentLocalPosition)
        {
            targetHips = null;
            referenceCurrentLocalPosition = Vector3.zero;
            currentLocalPosition = Vector3.zero;
            if (!TryResolveHipsTransforms(
                referenceAnimator,
                targetAnimator,
                out Transform referenceHips,
                out targetHips))
            {
                return false;
            }

            referenceCurrentLocalPosition = referenceHips.localPosition;
            currentLocalPosition = targetHips.localPosition;
            return true;
        }

        internal static void ApplyHipsLocalPosition(Transform targetHips, Vector3 localPosition)
        {
            targetHips.localPosition = localPosition;
        }

        internal static bool TryCalculateHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            return TryCalculateHipsLocalPositionReference(
                referenceCurrentLocalPosition,
                referenceRestLocalPosition,
                hasReferenceRestLocalPosition,
                currentLocalPosition,
                false,
                currentLocalPosition,
                weight,
                maxOffset,
                out nextLocalPosition);
        }

        internal static bool TryCalculateHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 targetRestLocalPosition,
            bool hasTargetRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            nextLocalPosition = currentLocalPosition;
            if (!IsFinite(referenceCurrentLocalPosition) || !IsFinite(currentLocalPosition))
            {
                return false;
            }

            if (hasReferenceRestLocalPosition && !IsFinite(referenceRestLocalPosition))
            {
                return false;
            }

            Vector3 desiredLocalPosition;
            if (hasReferenceRestLocalPosition)
            {
                Vector3 referenceDelta = referenceCurrentLocalPosition - referenceRestLocalPosition;
                Vector3 anchorLocalPosition = hasTargetRestLocalPosition && IsFinite(targetRestLocalPosition)
                    ? targetRestLocalPosition
                    : currentLocalPosition;
                desiredLocalPosition = anchorLocalPosition + referenceDelta;
            }
            else
            {
                desiredLocalPosition = referenceCurrentLocalPosition;
            }

            Vector3 delta = desiredLocalPosition - currentLocalPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                delta = Vector3.ClampMagnitude(delta, clampedMaxOffset);
            }

            nextLocalPosition = currentLocalPosition + delta * Mathf.Clamp01(weight);
            if (!IsFinite(nextLocalPosition))
            {
                nextLocalPosition = currentLocalPosition;
                return false;
            }

            return true;
        }

        internal static bool TryResolveFootIkPositionReference(
            Animator referenceAnimator,
            Animator targetAnimator,
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips,
            float weight,
            float maxOffset,
            out Vector3 nextPosition)
        {
            nextPosition = Vector3.zero;
            if (referenceAnimator == null || targetAnimator == null ||
                referenceHips == null || targetHips == null)
            {
                return false;
            }

            Transform referenceFoot = referenceAnimator.GetBoneTransform(footBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            if (referenceFoot == null || targetFoot == null)
            {
                return false;
            }

            return TryCalculateFootIkPositionReference(
                referenceFoot.position,
                referenceHips.position,
                targetFoot.position,
                targetHips.position,
                weight,
                maxOffset,
                out nextPosition);
        }

        internal static bool TryCalculateFootIkPositionReference(
            Vector3 referenceFootPosition,
            Vector3 referenceHipsPosition,
            Vector3 currentFootPosition,
            Vector3 targetHipsPosition,
            float weight,
            float maxOffset,
            out Vector3 nextPosition)
        {
            nextPosition = currentFootPosition;
            if (!IsFinite(referenceFootPosition) ||
                !IsFinite(referenceHipsPosition) ||
                !IsFinite(currentFootPosition) ||
                !IsFinite(targetHipsPosition))
            {
                return false;
            }

            Vector3 desiredPosition = targetHipsPosition + (referenceFootPosition - referenceHipsPosition);
            Vector3 delta = desiredPosition - currentFootPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                delta = Vector3.ClampMagnitude(delta, clampedMaxOffset);
            }

            nextPosition = currentFootPosition + delta * Mathf.Clamp01(weight);
            if (!IsFinite(nextPosition))
            {
                nextPosition = currentFootPosition;
                return false;
            }

            return true;
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
