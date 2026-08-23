using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 수동 포즈 기준 Animator에서 대상 Transform으로 로컬 회전을 적용함.
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

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }
    }
}
