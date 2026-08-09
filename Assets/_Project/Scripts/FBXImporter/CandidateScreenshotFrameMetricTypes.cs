#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class CandidateScreenshotFrameMetrics
    {
        public int SampleCount;
        public int NonblankCount;
        public float AvgBBoxHeightRatio = float.NaN;
        public float AvgBBoxWidthRatio = float.NaN;
        public float AvgUpperLimbSpanRatio = float.NaN;
        public float AvgLowerLimbSpanRatio = float.NaN;
        public float CenterXRangeRatio = float.NaN;
        public float MaxBottomGapRatio = float.NaN;
        public float MaxTopGapRatio = float.NaN;
        public float AvgBrightAreaRatio = float.NaN;
        public readonly List<int> RecorderFrames = new List<int>();
        public readonly List<CandidateScreenshotFrameSample> Samples =
            new List<CandidateScreenshotFrameSample>();
        public string Error = string.Empty;
    }

    internal sealed class CandidateScreenshotFrameSample
    {
        public CandidateScreenshotFrameSample(int recorderFrame, CandidateScreenshotFrameMetric metric)
        {
            RecorderFrame = recorderFrame;
            Metric = metric;
            Seconds = float.NaN;
        }

        public int RecorderFrame;
        public CandidateScreenshotFrameMetric Metric;
        public float Seconds;
    }

    internal sealed class CandidateScreenshotFrameMetric
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
