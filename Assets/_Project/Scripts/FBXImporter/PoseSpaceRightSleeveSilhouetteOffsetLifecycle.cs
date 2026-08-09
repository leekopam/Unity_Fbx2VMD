using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
#if UNITY_EDITOR
        private bool ShouldApplyYybRightSleeveSilhouetteLocalOffsetFrameGate()
        {
            float start = Mathf.Max(0f, yybRightSleeveSilhouetteLocalOffsetFrameGateStart);
            float end = Mathf.Max(0f, yybRightSleeveSilhouetteLocalOffsetFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end <= 0f || end < start)
            {
                end = start;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 1f, 240f);
            int currentFrame = Mathf.RoundToInt(GetLegacyAnimationTime() * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private void ApplyYybRightSleeveSilhouetteLocalOffsetReference()
        {
            RestoreYybRightSleeveSilhouetteLocalOffsetReference();
            if (!useYybRightSleeveSilhouetteLocalOffsetReference ||
                !ShouldApplyYybRightSleeveSilhouetteLocalOffsetFrameGate())
            {
                return;
            }

            float offsetX = Mathf.Clamp(yybRightSleeveSilhouetteLocalOffsetX, -0.2f, 0.2f);
            if (Mathf.Abs(offsetX) <= 0.00001f)
            {
                return;
            }

            Vector3 offset = new Vector3(offsetX, 0f, 0f);
            ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(
                targetAnimator != null ? targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null,
                offset);
            ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(
                targetAnimator != null ? targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null,
                offset);
            ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(
                targetAnimator != null ? targetAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null,
                offset);
            for (int i = 0; i < RightSleeveSilhouetteLocalOffsetTransformSuffixes.Length; i++)
            {
                Transform target = FindTargetTransformByNameSuffix(
                    RightSleeveSilhouetteLocalOffsetTransformSuffixes[i]);
                ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(target, offset);
            }
        }

        private void ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(Transform target, Vector3 offset)
        {
            if (target == null ||
                !IsFinite(target.localPosition) ||
                !IsFinite(offset) ||
                _rightSleeveSilhouetteLocalOffsetBaseLocalPositions.ContainsKey(target))
            {
                return;
            }

            _rightSleeveSilhouetteLocalOffsetBaseLocalPositions[target] = target.localPosition;
            target.localPosition += offset;
        }

        private void RestoreYybRightSleeveSilhouetteLocalOffsetReference()
        {
            if (_rightSleeveSilhouetteLocalOffsetBaseLocalPositions.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Vector3> entry in _rightSleeveSilhouetteLocalOffsetBaseLocalPositions)
            {
                if (entry.Key != null && IsFinite(entry.Value))
                {
                    entry.Key.localPosition = entry.Value;
                }
            }

            _rightSleeveSilhouetteLocalOffsetBaseLocalPositions.Clear();
        }
#endif
    }
}
