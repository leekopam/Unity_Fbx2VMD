using UnityEngine;

namespace Member_Han.Modules.Graphics
{
    public enum RecordingCaptureQualityPreset
    {
        Existing960Square,
        FullHd,
        Uhd4K,
        Custom
    }

    public readonly struct RecordingCaptureResolutionPlan
    {
        public RecordingCaptureResolutionPlan(int width, int height)
        {
            Width = Mathf.Clamp(width, RecordingCaptureResolution.MinWidth, RecordingCaptureResolution.MaxWidth);
            Height = Mathf.Clamp(height, RecordingCaptureResolution.MinHeight, RecordingCaptureResolution.MaxHeight);
        }

        public int Width { get; }
        public int Height { get; }
        public int PixelCount => Width * Height;
    }

    public static class RecordingCaptureResolution
    {
        public const int MinWidth = 128;
        public const int MinHeight = 128;
        public const int MaxWidth = 7680;
        public const int MaxHeight = 4320;

        public static RecordingCaptureResolutionPlan CreatePlan(RecordingCaptureQualityPreset preset)
        {
            switch (preset)
            {
                case RecordingCaptureQualityPreset.FullHd:
                    return new RecordingCaptureResolutionPlan(1920, 1080);
                case RecordingCaptureQualityPreset.Uhd4K:
                    return new RecordingCaptureResolutionPlan(3840, 2160);
                case RecordingCaptureQualityPreset.Custom:
                    return new RecordingCaptureResolutionPlan(3840, 2160);
                case RecordingCaptureQualityPreset.Existing960Square:
                default:
                    return new RecordingCaptureResolutionPlan(960, 960);
            }
        }

        public static RecordingCaptureResolutionPlan CreateCustomPlan(int width, int height)
        {
            return new RecordingCaptureResolutionPlan(width, height);
        }
    }
}
