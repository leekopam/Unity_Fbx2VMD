using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ManualLowerBodySegmentDirectionRuntimeOverrideApplier
    {
        internal static bool Apply(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight,
            float maxAngle,
            bool disableUpperLegToLowerLeg,
            float upperLegToLowerLegMaxAngle,
            bool disableLowerLegToFoot,
            float lowerLegToFootMaxAngle,
            float leftLowerLegToFootMaxAngle,
            float rightLowerLegToFootMaxAngle,
            float rightLowerLegToFootAxisXzScale,
            float rightLowerLegToFootBlendWeight,
            float rightLowerLegToFootFrameGateStart,
            float rightLowerLegToFootFrameGateEnd,
            float rightLowerLegToFootEndpointBlendWeight,
            bool disableFootToToes,
            float footToToesMaxAngle)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.ShouldUseManualAnimatorLowerBodySegmentDirectionReference = enabled;
            pipeline.manualAnimatorLowerBodySegmentDirectionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            pipeline.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = Mathf.Max(0f, maxAngle);
            pipeline.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                enabled && disableUpperLegToLowerLeg;
            pipeline.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, upperLegToLowerLegMaxAngle);
            pipeline.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference =
                enabled && disableLowerLegToFoot;
            pipeline.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, lowerLegToFootMaxAngle);
            pipeline.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, leftLowerLegToFootMaxAngle);
            pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                Mathf.Max(0f, rightLowerLegToFootMaxAngle);
            pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                Mathf.Clamp01(rightLowerLegToFootAxisXzScale);
            pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                Mathf.Clamp01(rightLowerLegToFootBlendWeight);
            pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                Mathf.Max(0f, rightLowerLegToFootFrameGateStart);
            pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                Mathf.Max(0f, rightLowerLegToFootFrameGateEnd);
            pipeline.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                Mathf.Clamp01(rightLowerLegToFootEndpointBlendWeight);
            pipeline.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = enabled && disableFootToToes;
            pipeline.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = Mathf.Max(0f, footToToesMaxAngle);
            return true;
        }
    }
}
