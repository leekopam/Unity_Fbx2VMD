using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 수동 포즈 기준 Animator에서 대상 Transform으로 로컬 회전과 Hips·Foot 위치를 적용함.
    /// </summary>
    internal static class ManualPoseReferenceApplier
    {
        internal static float CalculateBodyPositionXzFrameGateWeight(
            float currentFrame,
            float startFrame,
            float endFrame,
            float blendFrames)
        {
            float start = Mathf.Max(0f, Mathf.Round(startFrame));
            float end = Mathf.Max(0f, Mathf.Round(endFrame));
            if (start <= 0f && end <= 0f)
            {
                return 1f;
            }

            if (end < start || end <= 0f)
            {
                return 1f;
            }

            float blend = Mathf.Max(0f, blendFrames);
            if (blend <= 0f)
            {
                return currentFrame >= start && currentFrame <= end ? 1f : 0f;
            }

            if (currentFrame >= start && currentFrame <= end)
            {
                return 1f;
            }

            if (currentFrame < start)
            {
                float fadeStart = start - blend;
                if (currentFrame <= fadeStart)
                {
                    return 0f;
                }

                return Mathf.Clamp01((currentFrame - fadeStart) / blend);
            }

            float fadeEnd = end + blend;
            if (currentFrame >= fadeEnd)
            {
                return 0f;
            }

            return Mathf.Clamp01((fadeEnd - currentFrame) / blend);
        }

        internal static bool TryCalculateBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 referenceBodyPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            out Vector3 nextBodyPosition)
        {
            nextBodyPosition = currentBodyPosition;
            if (!IsFinite(currentBodyPosition) || !IsFinite(referenceBodyPosition))
            {
                return false;
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight <= 0f)
            {
                return false;
            }

            Vector3 delta = new Vector3(
                (referenceBodyPosition.x - currentBodyPosition.x) * Mathf.Clamp01(axisXScale),
                0f,
                (referenceBodyPosition.z - currentBodyPosition.z) * Mathf.Clamp01(axisZScale));
            return TryApplyBodyPositionXzDelta(
                currentBodyPosition,
                delta,
                clampedWeight,
                maxOffset,
                out nextBodyPosition);
        }

        internal static bool TryCalculateSignCorrectedBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            out Vector3 nextBodyPosition)
        {
            return TryCalculateSignCorrectedBodyPositionXzReference(
                currentBodyPosition,
                referenceFootPosition,
                currentFootPosition,
                weight,
                maxOffset,
                axisXScale,
                axisZScale,
                invertX: false,
                invertZ: false,
                out nextBodyPosition);
        }

        internal static bool TryCalculateSignCorrectedBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            bool invertX,
            bool invertZ,
            out Vector3 nextBodyPosition)
        {
            nextBodyPosition = currentBodyPosition;
            if (!IsFinite(currentBodyPosition) ||
                !IsFinite(referenceFootPosition) ||
                !IsFinite(currentFootPosition))
            {
                return false;
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight <= 0f)
            {
                return false;
            }

            Vector3 delta = referenceFootPosition - currentFootPosition;
            delta = new Vector3(
                delta.x * Mathf.Clamp01(axisXScale),
                0f,
                delta.z * Mathf.Clamp01(axisZScale));
            if (invertX)
            {
                delta.x = -delta.x;
            }
            if (invertZ)
            {
                delta.z = -delta.z;
            }

            return TryApplyBodyPositionXzDelta(
                currentBodyPosition,
                delta,
                clampedWeight,
                maxOffset,
                out nextBodyPosition);
        }

        internal static float CalculateBoundedMuscleOutputReference(
            float inputValue,
            float outputValue,
            float weight,
            float maxDelta,
            float fallbackValue)
        {
            if (!IsFinite(outputValue))
            {
                return IsFinite(fallbackValue) ? fallbackValue : outputValue;
            }

            if (!IsFinite(inputValue))
            {
                return outputValue;
            }

            float clampedCorrection = Mathf.Clamp(
                inputValue - outputValue,
                -Mathf.Max(0f, maxDelta),
                Mathf.Max(0f, maxDelta));
            return outputValue + clampedCorrection * Mathf.Clamp01(weight);
        }

        internal static bool HasActiveFrameGate(float startFrame, float endFrame)
        {
            float start = Mathf.Max(0f, startFrame);
            float end = Mathf.Max(0f, endFrame);
            return HasConfiguredFrameGate(start, end) && end >= start && end > 0f;
        }

        internal static bool HasConfiguredFrameGate(float startFrame, float endFrame)
        {
            float start = Mathf.Max(0f, startFrame);
            float end = Mathf.Max(0f, endFrame);
            return start > 0f || end > 0f;
        }

        internal static bool IsFrameWithinGate(int currentFrame, float startFrame, float endFrame)
        {
            if (!HasActiveFrameGate(startFrame, endFrame))
            {
                return true;
            }

            float start = Mathf.Max(0f, startFrame);
            float end = Mathf.Max(0f, endFrame);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        internal static bool IsFrameWithinSingleFrameFallbackGate(
            int currentFrame,
            float startFrame,
            float endFrame)
        {
            float start = Mathf.Max(0f, startFrame);
            float end = Mathf.Max(0f, endFrame);
            if (!HasConfiguredFrameGate(start, end))
            {
                return true;
            }

            if (end <= 0f || end < start)
            {
                end = start;
            }

            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

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

        internal static bool TryResolveHipsAlignedEndpointPositionReference(
            Animator referenceAnimator,
            Animator targetAnimator,
            HumanBodyBones endpointBone,
            Transform referenceHips,
            Transform targetHips,
            Transform targetEndpoint,
            out Vector3 desiredEndpointPosition)
        {
            desiredEndpointPosition = targetEndpoint != null ? targetEndpoint.position : Vector3.zero;
            if (referenceAnimator == null || targetAnimator == null ||
                referenceHips == null || targetHips == null || targetEndpoint == null)
            {
                return false;
            }

            Transform referenceEndpoint = referenceAnimator.GetBoneTransform(endpointBone);
            if (referenceEndpoint == null)
            {
                return false;
            }

            return TryCalculateHipsAlignedEndpointPositionReference(
                referenceEndpoint.position,
                referenceHips.position,
                referenceAnimator.transform,
                targetHips.position,
                targetEndpoint.position,
                targetAnimator.transform,
                out desiredEndpointPosition);
        }

        internal static bool TryCalculateHipsAlignedEndpointPositionReference(
            Vector3 referenceEndpointPosition,
            Vector3 referenceHipsPosition,
            Transform referenceRoot,
            Vector3 targetHipsPosition,
            Vector3 currentTargetEndpointPosition,
            Transform targetRoot,
            out Vector3 desiredEndpointPosition)
        {
            desiredEndpointPosition = currentTargetEndpointPosition;
            if (referenceRoot == null || targetRoot == null)
            {
                return false;
            }

            Vector3 referenceOffset = referenceEndpointPosition - referenceHipsPosition;
            if (!IsFinite(referenceOffset))
            {
                return false;
            }

            Vector3 referenceRootOffset = referenceRoot.InverseTransformVector(referenceOffset);
            Vector3 desiredTargetOffset = targetRoot.TransformVector(referenceRootOffset);
            if (!IsFinite(desiredTargetOffset))
            {
                return false;
            }

            desiredEndpointPosition = targetHipsPosition + desiredTargetOffset;
            desiredEndpointPosition.y = currentTargetEndpointPosition.y;
            return IsFinite(desiredEndpointPosition);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Quaternion value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
                !float.IsNaN(value.w) && !float.IsInfinity(value.w);
        }

        private static bool TryApplyBodyPositionXzDelta(
            Vector3 currentBodyPosition,
            Vector3 delta,
            float clampedWeight,
            float maxOffset,
            out Vector3 nextBodyPosition)
        {
            nextBodyPosition = currentBodyPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                float magnitude = delta.magnitude;
                if (magnitude > clampedMaxOffset)
                {
                    delta = delta / magnitude * clampedMaxOffset;
                }
            }

            nextBodyPosition = new Vector3(
                currentBodyPosition.x + delta.x * clampedWeight,
                currentBodyPosition.y,
                currentBodyPosition.z + delta.z * clampedWeight);
            return IsFinite(nextBodyPosition);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
