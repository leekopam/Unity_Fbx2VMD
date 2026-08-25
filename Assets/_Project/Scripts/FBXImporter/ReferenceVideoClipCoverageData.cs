using System;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class ReferenceVideoClipCoverageData
    {
        public float EndSeconds { get; set; }
        public int SampleCount { get; set; }
        public float[] SampleSeconds { get; set; } = Array.Empty<float>();
        public float FirstSampleSeconds { get; set; } = float.NaN;
        public float LastSampleSeconds { get; set; } = float.NaN;
        public float SampleCoverageRatio { get; set; }
        public float SampleGapSeconds { get; set; }
        public float AverageBBoxHeightRatio { get; set; } = float.NaN;
        public float AverageBBoxWidthRatio { get; set; } = float.NaN;
        public float CenterXRangeRatio { get; set; } = float.NaN;
        public float MaxBottomGapRatio { get; set; } = float.NaN;
        public float AverageBrightAreaRatio { get; set; } = float.NaN;
        public ReferenceMp4FrameMetricRow[] Rows { get; set; } =
            Array.Empty<ReferenceMp4FrameMetricRow>();
    }
}
