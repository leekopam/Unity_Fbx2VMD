using System;

namespace Fbx2Vmd.Recording
{
    /// <summary>
    /// 에디터 영상 인코더와 재생 흐름 사이의 좁은 외부 의존성 경계임.
    /// </summary>
    public interface IMotionVideoRecorder : IDisposable
    {
        bool IsRecording { get; }
        string OutputFilePath { get; }
        bool TryPrepare(
            MotionVideoRecordingSettings settings,
            out string errorMessage);
        bool TryStart(out string errorMessage);
        void Stop();
    }
}
