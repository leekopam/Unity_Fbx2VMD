using System;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class ReferenceVideoDiagnosticsData
    {
        public bool AnalysisFileExists { get; set; }
        public string AnalysisError { get; set; } = string.Empty;
        public string AnalysisSchema { get; set; } = string.Empty;
        public int ExtractedFrameCount { get; set; }
        public int VideoWidth { get; set; }
        public int VideoHeight { get; set; }
        public string AverageFrameRate { get; set; } = string.Empty;
        public float StreamDurationSeconds { get; set; } = float.NaN;
        public int TotalVideoFrames { get; set; }
        public bool FrameMetricsFileExists { get; set; }
        public string FrameMetricsError { get; set; } = string.Empty;
        public string FrameMetricsSchema { get; set; } = string.Empty;
        public int FrameMetricsSampleCount { get; set; }
        public int FrameMetricsExtractedFrameCount { get; set; }
        public float AverageBBoxHeightRatio { get; set; }
        public float AverageBBoxWidthRatio { get; set; }
        public float CenterXRangeRatio { get; set; }
        public float MaxBottomGapRatio { get; set; }
        public float AverageBrightAreaRatio { get; set; }
        public ReferenceMp4FrameMetricRow[] FrameMetricRows { get; set; } =
            Array.Empty<ReferenceMp4FrameMetricRow>();
    }
}
