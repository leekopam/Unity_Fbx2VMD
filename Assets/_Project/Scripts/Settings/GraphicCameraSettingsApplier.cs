using System;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Rendering.Universal;

namespace Fbx2Vmd.Settings
{
    internal static class GraphicCameraSettingsApplier
    {
        internal static void Apply(
            Camera camera,
            bool useUniversalRenderPipeline,
            GraphicAntiAliasingPlan plan,
            Func<PostProcessResources> builtInPostProcessResourcesResolver)
        {
            if (camera == null)
            {
                return;
            }

            camera.allowMSAA = plan.EnableCameraMsaa;

            if (useUniversalRenderPipeline)
            {
                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = plan.EnableCameraPostProcessing;
                cameraData.antialiasing = ToUrpAntialiasingMode(plan.AntiAliasing);
                cameraData.antialiasingQuality = plan.SmaaQuality;
                return;
            }

            PostProcessLayer layer = camera.GetComponent<PostProcessLayer>();
            if (layer == null)
            {
                layer = camera.gameObject.AddComponent<PostProcessLayer>();
            }

            layer.enabled = plan.EnableCameraPostProcessing &&
                plan.AntiAliasing != GraphicAntiAliasingMode.Off;
            layer.volumeLayer = ~0;
            layer.antialiasingMode = ToBuiltInAntialiasingMode(plan.AntiAliasing);
            layer.subpixelMorphologicalAntialiasing.quality =
                ToBuiltInSmaaQuality(plan.SmaaQuality);

            PostProcessResources resources = builtInPostProcessResourcesResolver?.Invoke();
            if (resources != null)
            {
                layer.Init(resources);
            }
        }

        private static AntialiasingMode ToUrpAntialiasingMode(GraphicAntiAliasingMode mode)
        {
            switch (mode)
            {
                case GraphicAntiAliasingMode.FXAA:
                    return AntialiasingMode.FastApproximateAntialiasing;
                case GraphicAntiAliasingMode.SMAA:
                    return AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                case GraphicAntiAliasingMode.TAA:
                    return AntialiasingMode.TemporalAntiAliasing;
                default:
                    return AntialiasingMode.None;
            }
        }

        private static PostProcessLayer.Antialiasing ToBuiltInAntialiasingMode(
            GraphicAntiAliasingMode mode)
        {
            switch (mode)
            {
                case GraphicAntiAliasingMode.FXAA:
                    return PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
                case GraphicAntiAliasingMode.SMAA:
                    return PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
                case GraphicAntiAliasingMode.TAA:
                    return PostProcessLayer.Antialiasing.TemporalAntialiasing;
                default:
                    return PostProcessLayer.Antialiasing.None;
            }
        }

        private static SubpixelMorphologicalAntialiasing.Quality ToBuiltInSmaaQuality(
            AntialiasingQuality quality)
        {
            switch (quality)
            {
                case AntialiasingQuality.Low:
                    return SubpixelMorphologicalAntialiasing.Quality.Low;
                case AntialiasingQuality.Medium:
                    return SubpixelMorphologicalAntialiasing.Quality.Medium;
                default:
                    return SubpixelMorphologicalAntialiasing.Quality.High;
            }
        }
    }
}
