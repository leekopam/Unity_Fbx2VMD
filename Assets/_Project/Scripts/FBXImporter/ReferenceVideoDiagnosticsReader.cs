using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class ReferenceVideoDiagnosticsReader
    {
        internal static ReferenceVideoDiagnosticsData Read(
            string analysisPath,
            string frameMetricsPath)
        {
            ReferenceVideoDiagnosticsData data = new ReferenceVideoDiagnosticsData
            {
                AnalysisFileExists = File.Exists(analysisPath),
                FrameMetricsFileExists = File.Exists(frameMetricsPath)
            };

            ReadAnalysis(analysisPath, data);
            ReadFrameMetrics(frameMetricsPath, data);
            return data;
        }

        private static void ReadAnalysis(string path, ReferenceVideoDiagnosticsData data)
        {
            if (!data.AnalysisFileExists)
            {
                return;
            }

            try
            {
                AnalysisDocument document = JsonUtility.FromJson<AnalysisDocument>(
                    File.ReadAllText(path, Encoding.UTF8));
                if (document == null)
                {
                    return;
                }

                data.AnalysisSchema = document.schema ?? string.Empty;
                data.ExtractedFrameCount = Math.Max(0, document.extractedFrameCount);
                if (document.video == null)
                {
                    return;
                }

                data.VideoWidth = Math.Max(0, document.video.width);
                data.VideoHeight = Math.Max(0, document.video.height);
                data.AverageFrameRate = document.video.avg_frame_rate ?? string.Empty;
                data.StreamDurationSeconds = ParseInvariantFloat(document.video.stream_duration);
                data.TotalVideoFrames = ParseInvariantInt(document.video.nb_frames);
            }
            catch (Exception ex)
            {
                data.AnalysisError = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static void ReadFrameMetrics(string path, ReferenceVideoDiagnosticsData data)
        {
            if (!data.FrameMetricsFileExists)
            {
                return;
            }

            try
            {
                FrameMetricsDocument document = JsonUtility.FromJson<FrameMetricsDocument>(
                    File.ReadAllText(path, Encoding.UTF8));
                if (document == null)
                {
                    return;
                }

                data.FrameMetricsSchema = document.schema ?? string.Empty;
                data.FrameMetricsSampleCount = Math.Max(0, document.sampleCount);
                data.FrameMetricsExtractedFrameCount = Math.Max(0, document.extractedFrameCount);
                data.AverageBBoxHeightRatio = document.avgBBoxHeightRatio;
                data.AverageBBoxWidthRatio = document.avgBBoxWidthRatio;
                data.CenterXRangeRatio = document.centerXRangeRatio;
                data.MaxBottomGapRatio = document.maxBottomGapRatio;
                data.AverageBrightAreaRatio = document.avgBrightAreaRatio;
                data.FrameMetricRows = document.rows ?? Array.Empty<ReferenceMp4FrameMetricRow>();
            }
            catch (Exception ex)
            {
                data.FrameMetricsError = ex.GetType().Name + ": " + ex.Message;
            }
        }

        private static int ParseInvariantInt(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        private static float ParseInvariantFloat(string value)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : float.NaN;
        }

        [Serializable]
        private sealed class AnalysisDocument
        {
            public string schema = string.Empty;
            public int extractedFrameCount = 0;
            public VideoStreamDocument video = null;
        }

        [Serializable]
        private sealed class VideoStreamDocument
        {
            public int width = 0;
            public int height = 0;
            public string avg_frame_rate = string.Empty;
            public string stream_duration = string.Empty;
            public string nb_frames = string.Empty;
        }

        [Serializable]
        private sealed class FrameMetricsDocument
        {
            public string schema = string.Empty;
            public int sampleCount = 0;
            public int extractedFrameCount = 0;
            public float avgBBoxHeightRatio = 0f;
            public float avgBBoxWidthRatio = 0f;
            public float centerXRangeRatio = 0f;
            public float maxBottomGapRatio = 0f;
            public float avgBrightAreaRatio = 0f;
            public ReferenceMp4FrameMetricRow[] rows = null;
        }
    }
}
