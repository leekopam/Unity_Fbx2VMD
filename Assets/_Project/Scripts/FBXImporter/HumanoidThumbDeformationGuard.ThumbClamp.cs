using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class HumanoidThumbDeformationGuard
    {
        /// <summary>엄지 본별 허용 최대 각도를 반환한다.</summary>
        private float GetLimit(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                case HumanBodyBones.RightThumbProximal:
                    return proximalMaxLocalAngle;
                case HumanBodyBones.LeftThumbIntermediate:
                case HumanBodyBones.RightThumbIntermediate:
                    return intermediateMaxLocalAngle;
                case HumanBodyBones.LeftThumbDistal:
                case HumanBodyBones.RightThumbDistal:
                    return distalMaxLocalAngle;
                default:
                    return 0f;
            }
        }

        /// <summary>ThumbBaseHelper Transform의 local rotation을 proximal clamp 적용해 교정한다.</summary>
        private int ClampThumbBaseHelperTransforms()
        {
            int changed = 0;
            foreach (Transform thumbTransform in _thumbBaseHelperTransforms)
            {
                if (thumbTransform == null ||
                    !_initialLocalRotations.TryGetValue(thumbTransform, out Quaternion initialRotation))
                {
                    continue;
                }

                Quaternion rawRotation = GetCurrentRawLocalRotation(thumbTransform);
                if (!IsFinite(rawRotation))
                {
                    SetCorrectedLocalRotation(thumbTransform, initialRotation, initialRotation);
                    changed++;
                    continue;
                }

                Quaternion offsetRotation = GetProximalRotationOffsetRotation(thumbTransform);
                Quaternion baselineRotation = ApplyLimitSpaceOffset(initialRotation, offsetRotation);
                Quaternion currentRotation = ApplyLimitSpaceOffset(rawRotation, offsetRotation);
                float limit = proximalMaxLocalAngle;
                if (limit <= 0f)
                {
                    if (Quaternion.Angle(initialRotation, rawRotation) > 0.001f)
                    {
                        SetCorrectedLocalRotation(thumbTransform, rawRotation, initialRotation);
                        changed++;
                    }

                    continue;
                }

                float angle = Quaternion.Angle(baselineRotation, currentRotation);
                if (angle <= limit)
                {
                    if (Quaternion.Angle(thumbTransform.localRotation, rawRotation) > 0.001f)
                    {
                        SetCorrectedLocalRotation(thumbTransform, rawRotation, rawRotation);
                        changed++;
                    }

                    continue;
                }

                Quaternion limitedRotation = LimitLocalRotation(baselineRotation, currentRotation, limit);
                SetCorrectedLocalRotation(thumbTransform, rawRotation, RemoveLimitSpaceOffset(limitedRotation, offsetRotation));
                changed++;
            }

            return changed;
        }

        /// <summary>엄지 본에 대응하는 offset rotation을 반환한다.</summary>
        private Quaternion GetThumbRotationOffset(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                    return GetProximalRotationOffsetRotation(false);
                case HumanBodyBones.RightThumbProximal:
                    return GetProximalRotationOffsetRotation(true);
                default:
                    return Quaternion.identity;
            }
        }

        private Quaternion GetProximalRotationOffsetRotation(Transform thumbTransform)
        {
            return GetProximalRotationOffsetRotation(TryResolveThumbSide(thumbTransform, out bool isRightThumb) && isRightThumb);
        }

        private Quaternion GetProximalRotationOffsetRotation(bool isRightThumb)
        {
            Vector3 offset = GetProximalRotationOffset(isRightThumb);
            if (offset.sqrMagnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(offset);
        }

        private Vector3 GetProximalRotationOffset(bool isRightThumb)
        {
            Vector3 offset = proximalLocalRotationOffset;
            if (isRightThumb && mirrorRightProximalLocalRotationOffset)
            {
                offset = new Vector3(offset.x, -offset.y, -offset.z);
            }

            return offset + (isRightThumb ? rightProximalLocalRotationOffset : leftProximalLocalRotationOffset);
        }

        /// <summary>local rotation을 limit 공간으로 변환한다.</summary>
        private static Quaternion ApplyLimitSpaceOffset(Quaternion localRotation, Quaternion offsetRotation)
        {
            return localRotation * offsetRotation;
        }

        /// <summary>limit 공간에서 원래 공간으로 되돌린다.</summary>
        private static Quaternion RemoveLimitSpaceOffset(Quaternion localRotation, Quaternion offsetRotation)
        {
            return localRotation * Quaternion.Inverse(offsetRotation);
        }

        /// <summary>soft limit + overshoot 완화로 rotation을 교정한다.</summary>
        private static Quaternion LimitLocalRotation(Quaternion initialRotation, Quaternion currentRotation, float softLimit)
        {
            float angle = Quaternion.Angle(initialRotation, currentRotation);
            float hardLimit = softLimit + LocalRotationHardOvershootDegrees;
            float targetAngle = Mathf.Min(hardLimit, softLimit + (angle - softLimit) * LocalRotationOvershootRatio);
            return Quaternion.RotateTowards(initialRotation, currentRotation, targetAngle);
        }
    }
}
