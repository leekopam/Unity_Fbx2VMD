using System;

namespace Fbx2Vmd.FBXImporter
{
    [Serializable]
    internal class ReferenceMp4FrameMetricRow
    {
        public float seconds = float.NaN;
        public string framePath = string.Empty;
        public float bboxHeightRatio = 0f;
        public float bboxWidthRatio = 0f;
        public float centerXRatio = 0f;
        public float bottomGapRatio = 0f;
        public float brightAreaRatio = 0f;
        [NonSerialized]
        public float upperLimbSpanRatio = float.NaN;
        [NonSerialized]
        public float lowerLimbSpanRatio = float.NaN;
        [NonSerialized]
        public float[] silhouetteSpanProfile = Array.Empty<float>();
        [NonSerialized]
        public float[] silhouetteEndpointProfile = Array.Empty<float>();
        [NonSerialized]
        public float[] imageSpaceKeypointProfile = Array.Empty<float>();
        [NonSerialized]
        public bool hasNonHairBrightPixels;
        [NonSerialized]
        public float nonHairBBoxHeightRatio = float.NaN;
        [NonSerialized]
        public float nonHairBBoxWidthRatio = float.NaN;
        [NonSerialized]
        public float nonHairCenterXRatio = float.NaN;
        [NonSerialized]
        public float nonHairBottomGapRatio = float.NaN;
        [NonSerialized]
        public float[] nonHairImageSpaceKeypointProfile = Array.Empty<float>();
    }
}
