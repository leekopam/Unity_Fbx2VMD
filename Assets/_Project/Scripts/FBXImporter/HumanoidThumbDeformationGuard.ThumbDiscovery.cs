using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fbx2Vmd.FBXImporter
{
    public partial class HumanoidThumbDeformationGuard
    {
        private static readonly HumanBodyBones[] ThumbBones =
        {
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal
        };

        private void CacheThumbSide(Transform thumbTransform, bool? knownIsRightThumb = null)
        {
            if (thumbTransform == null || _cachedThumbSides.ContainsKey(thumbTransform))
            {
                return;
            }

            if (knownIsRightThumb.HasValue)
            {
                _cachedThumbSides[thumbTransform] = knownIsRightThumb.Value;
                return;
            }

            if (TryResolveThumbSideFromHumanMapping(thumbTransform, out bool isRightThumb) ||
                TryResolveThumbSideFromName(thumbTransform, out isRightThumb) ||
                TryResolveThumbSideByReferenceDistance(thumbTransform, out isRightThumb))
            {
                _cachedThumbSides[thumbTransform] = isRightThumb;
            }
        }

        private bool IsThumbBaseHelperTransform(Transform candidate)
        {
            return candidate != null &&
                !IsMappedHumanThumbBone(candidate) &&
                IsThumbBaseHelperName(candidate.name);
        }

        private bool IsMappedHumanThumbBone(Transform candidate)
        {
            if (candidate == null || targetAnimator == null)
            {
                return false;
            }

            foreach (HumanBodyBones thumbBone in ThumbBones)
            {
                if (targetAnimator.GetBoneTransform(thumbBone) == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private Transform GetMappedThumbProximal(bool isRightThumb)
        {
            if (targetAnimator == null)
            {
                return null;
            }

            return targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
        }

        private Transform GetClosestMappedThumbProximal(Transform referenceTransform)
        {
            if (referenceTransform == null)
            {
                return null;
            }

            Transform leftThumbProximal = GetMappedThumbProximal(false);
            Transform rightThumbProximal = GetMappedThumbProximal(true);
            if (leftThumbProximal == null)
            {
                return rightThumbProximal != referenceTransform ? rightThumbProximal : null;
            }

            if (rightThumbProximal == null)
            {
                return leftThumbProximal != referenceTransform ? leftThumbProximal : null;
            }

            float leftDistance = (referenceTransform.position - leftThumbProximal.position).sqrMagnitude;
            float rightDistance = (referenceTransform.position - rightThumbProximal.position).sqrMagnitude;
            Transform closest = rightDistance < leftDistance ? rightThumbProximal : leftThumbProximal;
            return closest != referenceTransform ? closest : null;
        }

        private bool TryResolveThumbSide(Transform thumbTransform, out bool isRightThumb)
        {
            if (thumbTransform != null && _cachedThumbSides.TryGetValue(thumbTransform, out isRightThumb))
            {
                return true;
            }

            if (TryResolveThumbSideFromHumanMapping(thumbTransform, out isRightThumb) ||
                TryResolveThumbSideFromName(thumbTransform, out isRightThumb) ||
                TryResolveThumbSideByReferenceDistance(thumbTransform, out isRightThumb))
            {
                if (thumbTransform != null)
                {
                    _cachedThumbSides[thumbTransform] = isRightThumb;
                }

                return true;
            }

            isRightThumb = false;
            return false;
        }

        private bool TryResolveThumbSideFromHumanMapping(Transform thumbTransform, out bool isRightThumb)
        {
            if (thumbTransform != null && targetAnimator != null)
            {
                foreach (HumanBodyBones thumbBone in ThumbBones)
                {
                    if (targetAnimator.GetBoneTransform(thumbBone) == thumbTransform)
                    {
                        isRightThumb = IsRightHumanThumbBone(thumbBone);
                        return true;
                    }
                }

                if (targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand) == thumbTransform)
                {
                    isRightThumb = false;
                    return true;
                }

                if (targetAnimator.GetBoneTransform(HumanBodyBones.RightHand) == thumbTransform)
                {
                    isRightThumb = true;
                    return true;
                }
            }

            isRightThumb = false;
            return false;
        }

        private static bool TryResolveThumbSideFromName(Transform thumbTransform, out bool isRightThumb)
        {
            if (thumbTransform != null)
            {
                string normalizedName = thumbTransform.name.ToLowerInvariant();
                if (normalizedName.Contains("right") ||
                    normalizedName.Contains("_r") ||
                    normalizedName.Contains(".r") ||
                    normalizedName.Contains("rthumb") ||
                    normalizedName.Contains("thumb_r"))
                {
                    isRightThumb = true;
                    return true;
                }

                if (normalizedName.Contains("left") ||
                    normalizedName.Contains("_l") ||
                    normalizedName.Contains(".l") ||
                    normalizedName.Contains("lthumb") ||
                    normalizedName.Contains("thumb_l"))
                {
                    isRightThumb = false;
                    return true;
                }
            }

            isRightThumb = false;
            return false;
        }

        private bool TryResolveThumbSideByReferenceDistance(Transform thumbTransform, out bool isRightThumb)
        {
            if (thumbTransform != null && targetAnimator != null)
            {
                float leftDistance = GetThumbSideReferenceDistance(
                    thumbTransform,
                    targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand),
                    GetMappedThumbProximal(false));
                float rightDistance = GetThumbSideReferenceDistance(
                    thumbTransform,
                    targetAnimator.GetBoneTransform(HumanBodyBones.RightHand),
                    GetMappedThumbProximal(true));
                if (IsFinite(leftDistance) || IsFinite(rightDistance))
                {
                    if (!IsFinite(leftDistance))
                    {
                        isRightThumb = true;
                        return true;
                    }

                    if (!IsFinite(rightDistance))
                    {
                        isRightThumb = false;
                        return true;
                    }

                    isRightThumb = rightDistance < leftDistance;
                    return true;
                }
            }

            isRightThumb = false;
            return false;
        }

        private static float GetThumbSideReferenceDistance(
            Transform thumbTransform,
            Transform handTransform,
            Transform thumbProximalTransform)
        {
            float handDistance = handTransform != null
                ? (thumbTransform.position - handTransform.position).sqrMagnitude
                : float.NaN;
            float thumbDistance = thumbProximalTransform != null
                ? (thumbTransform.position - thumbProximalTransform.position).sqrMagnitude
                : float.NaN;

            if (!IsFinite(handDistance))
            {
                return thumbDistance;
            }

            if (!IsFinite(thumbDistance))
            {
                return handDistance;
            }

            return Mathf.Min(handDistance, thumbDistance);
        }

        private static bool IsThumbBaseHelperName(string transformName)
        {
            if (string.IsNullOrEmpty(transformName))
            {
                return false;
            }

            string normalizedName = transformName.ToLowerInvariant();
            string compactName = normalizedName
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(".", string.Empty)
                .Replace(" ", string.Empty);
            if (!compactName.Contains("thumb0"))
            {
                return false;
            }

            return !normalizedName.Contains("thumb1") &&
                !normalizedName.Contains("thumb2") &&
                !normalizedName.Contains("thumb3") &&
                !normalizedName.Contains("proximal") &&
                !normalizedName.Contains("intermediate") &&
                !normalizedName.Contains("distal") &&
                !normalizedName.Contains("thumbtip");
        }

        private static bool IsRightHumanThumbBone(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.RightThumbProximal:
                case HumanBodyBones.RightThumbIntermediate:
                case HumanBodyBones.RightThumbDistal:
                    return true;
                default:
                    return false;
            }
        }
    }
}
