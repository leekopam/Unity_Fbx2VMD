namespace Fbx2Vmd.Recording
{
    /// <summary>
    /// 영상 녹화 백엔드가 없는 환경(스탠드얼론 빌드)에서 재생 컨트롤러의
    /// 수명주기와 UI 조회 경로를 유지하는 비활성 구현.
    /// </summary>
    internal sealed class NoOpMotionVideoRecorder : IMotionVideoRecorder
    {
        private const string UnavailableMessage =
            "이 환경에서는 영상 녹화를 지원하지 않습니다.";

        public bool IsRecording => false;

        public string OutputFilePath => string.Empty;

        public bool TryPrepare(
            MotionVideoRecordingSettings settings,
            out string errorMessage)
        {
            errorMessage = UnavailableMessage;
            return false;
        }

        public bool TryStart(out string errorMessage)
        {
            errorMessage = UnavailableMessage;
            return false;
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
