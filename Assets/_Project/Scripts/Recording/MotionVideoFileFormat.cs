using UnityEngine;

namespace Fbx2Vmd.Recording
{
    /// <summary>
    /// 영상 녹화 출력 컨테이너/코덱 선택지.
    /// </summary>
    public enum MotionVideoFileFormat
    {
        [InspectorName("MP4 (H.264)")] Mp4 = 0,
        [InspectorName("WebM (VP8)")] WebM = 1,
        [InspectorName("MOV (ProRes 4444, 배경 투명)")] MovProRes = 2,
    }
}
