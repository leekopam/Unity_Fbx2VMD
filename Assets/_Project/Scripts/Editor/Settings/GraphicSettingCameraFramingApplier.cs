using UnityEngine;

namespace Fbx2Vmd.Settings.EditorTools
{
    internal static class GraphicSettingCameraFramingApplier
    {
        private const float DefaultComparisonCameraViewportHeight = 0.56f;
        private const float DefaultComparisonCameraViewportWidth = 0.82f;
        private const float DefaultComparisonCameraViewportCenterY = 0.28f;
        private const float DefaultComparisonCameraAspect = 16f / 9f;
        private const float DefaultComparisonCameraDepth = 39f;

        internal static void ApplyDefaultFraming(Camera camera, GameObject targetRoot)
        {
            if (camera == null || targetRoot == null)
            {
                return;
            }

            if (!TryGetVisibleRendererBounds(targetRoot, out Bounds bounds))
            {
                return;
            }

            Vector3 focus = bounds.center;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(
                bounds.extents.y / DefaultComparisonCameraViewportHeight,
                bounds.extents.x / (DefaultComparisonCameraAspect * DefaultComparisonCameraViewportWidth));
            float cameraY = focus.y - (DefaultComparisonCameraViewportCenterY - 0.5f) * 2f * camera.orthographicSize;
            camera.transform.position = new Vector3(focus.x, cameraY, focus.z + DefaultComparisonCameraDepth);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = DefaultComparisonCameraDepth + bounds.extents.z + 100f;
            camera.useOcclusionCulling = false;
        }

        private static bool TryGetVisibleRendererBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (!TryGetRendererWorldBounds(renderer, out Bounds rendererBounds))
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = rendererBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(rendererBounds);
                }
            }

            return hasBounds && bounds.size.sqrMagnitude > 0.000001f;
        }

        private static bool TryGetRendererWorldBounds(Renderer renderer, out Bounds bounds)
        {
            if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
            {
                var bakedMesh = new Mesh();
                try
                {
                    skinnedRenderer.BakeMesh(bakedMesh);
                    bounds = TransformBounds(skinnedRenderer.transform.localToWorldMatrix, bakedMesh.bounds);
                    return bounds.size.sqrMagnitude > 0.000001f;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bakedMesh);
                }
            }

            bounds = renderer.bounds;
            return bounds.size.sqrMagnitude > 0.000001f;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds localBounds)
        {
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;
            var worldBounds = new Bounds(matrix.MultiplyPoint3x4(center), Vector3.zero);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        worldBounds.Encapsulate(matrix.MultiplyPoint3x4(corner));
                    }
                }
            }

            return worldBounds;
        }
    }
}
