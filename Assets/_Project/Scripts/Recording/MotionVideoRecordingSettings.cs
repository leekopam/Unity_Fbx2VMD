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
            MotionVideoFileFormat format = MotionVideoFileFormat.Mp4,
            float startTimeSeconds = 0f,
            float endTimeSeconds = 0f)
        {
            MotionName = motionName;
            Width = width;
            Height = height;
            FrameRate = frameRate;
            CaptureAudio = captureAudio;
            OutputDirectory = outputDirectory;
            Format = format;
            StartTimeSeconds = startTimeSeconds;
            EndTimeSeconds = endTimeSeconds;
        }

        public string MotionName { get; }
        public int Width { get; }
        public int Height { get; }
        public float FrameRate { get; }
        public bool CaptureAudio { get; }

        /// <summary>비어 있으면 프로젝트의 Recordings 폴더를 사용함.</summary>
        public string OutputDirectory { get; }
        public MotionVideoFileFormat Format { get; }

        /// <summary>녹화할 클립 시작 시각(초). 첫 캡처 프레임이 이 시각의 자세가 됨.</summary>
        public float StartTimeSeconds { get; }

        /// <summary>녹화할 클립 종료 시각(초). 0 이하면 클립 끝까지 녹화함.</summary>
        public float EndTimeSeconds { get; }
    }
}
