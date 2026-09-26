namespace Fbx2Vmd.Recording
{
    /// <summary>
    /// 모델 종류와 무관한 에디터 영상 녹화 입력값을 전달함.
    /// </summary>
    public readonly struct MotionVideoRecordingSettings
    {
        public MotionVideoRecordingSettings(
            string motionName,
            int width,
            int height,
            float frameRate,
            bool captureAudio = false,
            string outputDirectory = null,
            MotionVideoFileFormat format = MotionVideoFileFormat.Mp4)
        {
            MotionName = motionName;
            Width = width;
            Height = height;
            FrameRate = frameRate;
            CaptureAudio = captureAudio;
            OutputDirectory = outputDirectory;
            Format = format;
        }

        public string MotionName { get; }
        public int Width { get; }
        public int Height { get; }
        public float FrameRate { get; }
        public bool CaptureAudio { get; }

        /// <summary>비어 있으면 프로젝트의 Recordings 폴더를 사용함.</summary>
        public string OutputDirectory { get; }
        public MotionVideoFileFormat Format { get; }
    }
}
