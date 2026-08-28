using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Fbx2Vmd.Settings
{
    internal static class GraphicRenderPipelineSettingsApplier
    {
        internal static void Apply(
            UniversalRenderPipelineAsset pipelineAsset,
            float renderScale,
            bool enableCameraMsaa,
            int msaaSampleCount)
        {
            if (pipelineAsset != null)
            {
                pipelineAsset.msaaSampleCount = NormalizeMsaaSampleCount(msaaSampleCount);
                pipelineAsset.renderScale = Mathf.Clamp(renderScale, 0.1f, 2.0f);
                return;
            }

            QualitySettings.antiAliasing = enableCameraMsaa
                ? NormalizeMsaaSampleCount(msaaSampleCount)
                : 0;
        }

        internal static int NormalizeMsaaSampleCount(int samples)
        {
            if (samples >= 8)
            {
                return 8;
            }

            if (samples >= 4)
            {
                return 4;
            }

            if (samples >= 2)
            {
                return 2;
            }

            return 1;
        }
    }
}
