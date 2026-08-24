using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 임포트 모델의 Ghost 계층과 표시용 Renderer를 조립함.
    /// </summary>
    internal static class GhostModelPresenter
    {
        private const float GhostContainerScale = 0.01f;

        internal static GameObject CreateContainer(GameObject importedModel)
        {
            var ghostContainer = new GameObject($"GhostContainer_{importedModel.name}");
            ghostContainer.transform.position = Vector3.zero;
            ghostContainer.transform.rotation = Quaternion.identity;
            ghostContainer.transform.localScale = Vector3.one * GhostContainerScale;
            importedModel.transform.SetParent(ghostContainer.transform, false);
            importedModel.transform.localPosition = Vector3.zero;
            return ghostContainer;
        }

        internal static void SetVisibility(
            GameObject importedModel,
            bool visible,
            bool useSkeletonFallbackWhenRendererless)
        {
            if (importedModel == null)
            {
                return;
            }

            Renderer[] renderers = importedModel.GetComponentsInChildren<Renderer>(true);
            int controlledRendererCount = 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    renderer.GetComponentInParent<GhostSkeletonDebugRenderer>() != null)
                {
                    continue;
                }

                renderer.enabled = visible;
                controlledRendererCount++;
            }

            SetGhostSkeletonDebugRenderer(
                importedModel,
                visible && useSkeletonFallbackWhenRendererless,
                controlledRendererCount);
        }

        internal static bool ShouldAttachGhostSkeletonDebugRenderer(
            bool visible,
            int rendererCount)
        {
            // Renderer가 있어도 Ghost 가시성 보조선을 유지하는 기존 진단 동작을 보존함.
            return visible;
        }

        private static void SetGhostSkeletonDebugRenderer(
            GameObject importedModel,
            bool visible,
            int rendererCount)
        {
            GhostSkeletonDebugRenderer debugRenderer =
                importedModel.GetComponent<GhostSkeletonDebugRenderer>();
            bool shouldAttach = ShouldAttachGhostSkeletonDebugRenderer(
                visible,
                rendererCount);

            if (shouldAttach)
            {
                if (debugRenderer == null)
                {
                    debugRenderer = importedModel.AddComponent<GhostSkeletonDebugRenderer>();
                }

                debugRenderer.SetVisible(true);
                return;
            }

            if (debugRenderer != null)
            {
                debugRenderer.SetVisible(false);
            }
        }
    }
}
