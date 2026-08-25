#if UNITY_EDITOR
using System;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class VisualComparisonCandidateFrameSample
    {
        public VisualComparisonCandidateFrameSample(
            int recorderFrame,
            VisualComparisonCandidateFrameMetric metric)
        {
            RecorderFrame = recorderFrame;
            Metric = metric;
            Seconds = float.NaN;
        }

        public int RecorderFrame;
        public VisualComparisonCandidateFrameMetric Metric;
        public float Seconds;
    }

    internal sealed class VisualComparisonCandidateFrameMetric
    {
        public bool HasBrightPixels;
        public float BBoxHeightRatio;
        public float BBoxWidthRatio;
        public float UpperLimbSpanRatio = float.NaN;
        public float LowerLimbSpanRatio = float.NaN;
        public float[] SilhouetteSpanProfile = Array.Empty<float>();
        public float[] SilhouetteEndpointProfile = Array.Empty<float>();
        public float[] ImageSpaceKeypointProfile = Array.Empty<float>();
        public bool HasNonHairBrightPixels;
        public float NonHairBBoxHeightRatio = float.NaN;
        public float NonHairBBoxWidthRatio = float.NaN;
        public float NonHairCenterX = float.NaN;
        public float NonHairBottomGapRatio = float.NaN;
        public float NonHairTopGapRatio = float.NaN;
        public float[] NonHairImageSpaceKeypointProfile = Array.Empty<float>();
        public float CenterX = float.NaN;
        public float BottomGapRatio = 1f;
        public float TopGapRatio = 1f;
        public float BrightAreaRatio;
    }
}
#endif
