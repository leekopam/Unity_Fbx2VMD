using UnityEngine.Rendering.Universal;

namespace Fbx2Vmd.Settings
{
    internal readonly struct GraphicAntiAliasingPlan
    {
        internal GraphicAntiAliasingPlan(
            GraphicAntiAliasingMode antiAliasing,
            AntialiasingQuality smaaQuality,
            bool enableCameraPostProcessing,
            bool enableCameraMsaa,
            int msaaSampleCount)
        {
            AntiAliasing = antiAliasing;
            SmaaQuality = smaaQuality;
            EnableCameraPostProcessing = enableCameraPostProcessing;
            EnableCameraMsaa = enableCameraMsaa;
            MsaaSampleCount = msaaSampleCount;
        }

        internal GraphicAntiAliasingMode AntiAliasing { get; }
        internal AntialiasingQuality SmaaQuality { get; }
        internal bool EnableCameraPostProcessing { get; }
        internal bool EnableCameraMsaa { get; }
        internal int MsaaSampleCount { get; }
    }
}
