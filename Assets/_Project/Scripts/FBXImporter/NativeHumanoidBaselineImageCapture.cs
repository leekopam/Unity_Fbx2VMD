#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct NativeHumanoidBaselineImageMetrics
    {
        internal NativeHumanoidBaselineImageMetrics(
            int changedPixelCount,
            float pixelChangeRatio)
        {
            ChangedPixelCount = changedPixelCount;
            PixelChangeRatio = pixelChangeRatio;
        }

        internal int ChangedPixelCount { get; }
        internal float PixelChangeRatio { get; }
    }

    internal static class NativeHumanoidBaselineImageCapture
    {
        private const int IsolationLayer = 30;
        private const int CaptureWidth = 768;
        private const int CaptureHeight = 768;

        internal static void PrepareTarget(GameObject target)
        {
            SetLayerRecursively(target.transform);
            foreach (SkinnedMeshRenderer renderer in
                target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;
            }
        }

        internal static Bounds CalculateRendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "기준선 이미지를 렌더링할 Renderer가 필요합니다.");
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        internal static Camera CreateCamera(Scene scene, Bounds bounds)
        {
            var cameraObject = new GameObject("Native Humanoid Baseline Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            camera.fieldOfView = 30f;
            camera.aspect = (float)CaptureWidth / CaptureHeight;
            camera.nearClipPlane = 0.01f;
            camera.cullingMask = 1 << IsolationLayer;

            float halfFovRadians = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float verticalDistance = bounds.extents.y / Mathf.Tan(halfFovRadians);
            float horizontalDistance =
                bounds.extents.x /
                Mathf.Max(0.01f, Mathf.Tan(halfFovRadians) * camera.aspect);
            float distance = Mathf.Max(verticalDistance, horizontalDistance) +
                bounds.extents.z + 0.5f;
            camera.farClipPlane = Mathf.Max(100f, distance * 4f);
            camera.transform.position = bounds.center + Vector3.forward * distance;
            camera.transform.LookAt(bounds.center);
            return camera;
        }

        internal static void CreateDirectionalLight(Scene scene, Quaternion cameraRotation)
        {
            var lightObject = new GameObject("Native Humanoid Baseline Light");
            lightObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(lightObject, scene);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = Color.white;
            light.cullingMask = 1 << IsolationLayer;
            light.transform.rotation = cameraRotation * Quaternion.Euler(25f, -25f, 0f);
        }

        internal static Color32[] Render(Camera camera, string imagePath)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                CaptureWidth,
                CaptureHeight,
                24,
                RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            var texture = new Texture2D(
                CaptureWidth,
                CaptureHeight,
                TextureFormat.RGB24,
                mipChain: false);

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                texture.ReadPixels(
                    new Rect(0f, 0f, CaptureWidth, CaptureHeight),
                    0,
                    0);
                texture.Apply();
                File.WriteAllBytes(imagePath, texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        internal static NativeHumanoidBaselineImageMetrics CalculateDifference(
            IReadOnlyList<Color32> startPixels,
            IReadOnlyList<Color32> samplePixels)
        {
            if (startPixels.Count != samplePixels.Count || startPixels.Count == 0)
            {
                throw new InvalidOperationException(
                    "기준선 이미지의 픽셀 크기가 일치해야 합니다.");
            }

            int changedPixelCount = 0;
            for (int i = 0; i < startPixels.Count; i++)
            {
                Color32 start = startPixels[i];
                Color32 sample = samplePixels[i];
                int difference =
                    Mathf.Abs(start.r - sample.r) +
                    Mathf.Abs(start.g - sample.g) +
                    Mathf.Abs(start.b - sample.b);
                if (difference > 3)
                {
                    changedPixelCount++;
                }
            }

            return new NativeHumanoidBaselineImageMetrics(
                changedPixelCount,
                (float)changedPixelCount / startPixels.Count);
        }

        private static void SetLayerRecursively(Transform root)
        {
            root.gameObject.layer = IsolationLayer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i));
            }
        }
    }
}
#endif
