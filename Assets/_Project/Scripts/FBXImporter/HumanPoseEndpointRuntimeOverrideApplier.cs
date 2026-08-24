using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class HumanPoseEndpointRuntimeOverrideApplier
    {
        internal static bool ApplyPostSetReference(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide,
            bool evaluatorXzReferenceEnabled,
            float evaluatorXzTargetMagnitude)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.usePostSetHumanPoseRightEndpointPositionReference = enabled;
            pipeline.postSetHumanPoseRightEndpointPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            pipeline.postSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            pipeline.postSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(positiveZScale);
            pipeline.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(toesBlendWeight);
            pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            pipeline.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            pipeline.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = enabled && useLeftSide;
            pipeline.usePostSetHumanPoseRightFootEvaluatorXzReference = enabled && evaluatorXzReferenceEnabled;
            pipeline.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                Mathf.Max(0f, evaluatorXzTargetMagnitude);
            return true;
        }

        internal static bool ApplyPreSetReference(
            FBXVmdPipeline pipeline,
            bool enabled,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            float frameGateStart,
            float frameGateEnd,
            bool useLeftSide,
            bool useGhostCurrentBasis,
            bool invertBodyPositionX,
            bool invertBodyPositionZ)
        {
            if (pipeline == null)
            {
                return false;
            }

            pipeline.usePreSetHumanPoseRightEndpointPositionReference = enabled;
            pipeline.preSetHumanPoseRightEndpointPositionReferenceWeight = enabled ? Mathf.Clamp01(weight) : 0f;
            pipeline.preSetHumanPoseRightEndpointPositionReferenceMaxOffset = Mathf.Max(0f, maxOffset);
            pipeline.preSetHumanPoseRightEndpointPositionReferencePositiveZScale = Mathf.Clamp01(positiveZScale);
            pipeline.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = Mathf.Clamp01(toesBlendWeight);
            pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = Mathf.Max(0f, frameGateStart);
            pipeline.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = Mathf.Max(0f, frameGateEnd);
            pipeline.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = enabled && useLeftSide;
            pipeline.preSetHumanPoseEndpointPositionUseGhostCurrentBasis = enabled && useGhostCurrentBasis;
            pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyX = enabled && invertBodyPositionX;
            pipeline.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = enabled && invertBodyPositionZ;
            return true;
        }
    }
}
