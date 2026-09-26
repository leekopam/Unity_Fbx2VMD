using UnityEngine;

namespace Fbx2Vmd.Recording
{
    /// <summary>
    /// 영상 녹화 프레임률 선택지. enum 값이 곧 FPS 값임.
    /// </summary>
    public enum MotionVideoFrameRate
    {
        [InspectorName("30 FPS")] Fps30 = 30,
        [InspectorName("60 FPS")] Fps60 = 60,
    }
}
