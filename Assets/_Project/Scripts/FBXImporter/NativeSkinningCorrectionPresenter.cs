using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 스키닝된 메시를 굽힘 보정 캐시와 결합해 별도 Renderer로 표시함.
    /// </summary>
    internal sealed class NativeSkinningCorrectionPresenter : IDisposable
    {
        private readonly SkinnedMeshRenderer _sourceRenderer;
        private readonly NativeSkinningCorrectionCache _cache;
        private readonly bool _sourceWasEnabled;
        private readonly List<Vector3> _vertices;
        private readonly GameObject _previewObject;
        private readonly MeshRenderer _previewRenderer;
        private bool _isDisposed;

        internal NativeSkinningCorrectionPresenter(
            SkinnedMeshRenderer sourceRenderer,
            NativeSkinningCorrectionCache cache)
        {
            _sourceRenderer = sourceRenderer != null
                ? sourceRenderer
                : throw new ArgumentNullException(nameof(sourceRenderer));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            if (_sourceRenderer.sharedMesh == null)
            {
                throw new ArgumentException(
                    "보정 표시 대상에 sharedMesh가 필요합니다.",
                    nameof(sourceRenderer));
            }
            if (_sourceRenderer.sharedMesh.vertexCount != _cache.VertexCount)
            {
                throw new ArgumentException(
                    "보정 캐시와 대상 메시의 정점 수가 일치해야 합니다.",
                    nameof(cache));
            }

            _sourceWasEnabled = _sourceRenderer.enabled;
            _vertices = new List<Vector3>(_cache.VertexCount);
            PreviewMesh = new Mesh
            {
                name = $"{_sourceRenderer.sharedMesh.name} Native Skinning Preview",
                hideFlags = HideFlags.HideAndDontSave
            };
            PreviewMesh.MarkDynamic();

            _previewObject = new GameObject(
                $"{_sourceRenderer.name} Native Skinning Preview")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = _sourceRenderer.gameObject.layer
            };
            _previewObject.transform.SetParent(_sourceRenderer.transform.parent, false);
            _previewObject.transform.localPosition = _sourceRenderer.transform.localPosition;
            _previewObject.transform.localRotation = _sourceRenderer.transform.localRotation;
            _previewObject.transform.localScale = _sourceRenderer.transform.localScale;

            MeshFilter previewFilter = _previewObject.AddComponent<MeshFilter>();
            previewFilter.sharedMesh = PreviewMesh;
            _previewRenderer = _previewObject.AddComponent<MeshRenderer>();
            CopyRendererSettings(_sourceRenderer, _previewRenderer);
            _previewRenderer.enabled = false;
        }

        internal Mesh PreviewMesh { get; }

        internal bool IsPresenting => !_isDisposed && _previewRenderer.enabled;

        internal bool TryPresentFrame(int frameIndex, out int appliedCorrectionCount)
        {
            appliedCorrectionCount = 0;
            if (_isDisposed || _sourceRenderer == null || PreviewMesh == null)
            {
                return false;
            }

            _sourceRenderer.BakeMesh(PreviewMesh, false);
            _vertices.Clear();
            PreviewMesh.GetVertices(_vertices);
            if (!_cache.TryApply(frameIndex, _vertices, out appliedCorrectionCount))
            {
                return false;
            }

            PreviewMesh.SetVertices(_vertices);
            PreviewMesh.RecalculateBounds();
            PreviewMesh.UploadMeshData(false);
            _sourceRenderer.enabled = false;
            _previewRenderer.enabled = _sourceWasEnabled;
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (_sourceRenderer != null)
            {
                _sourceRenderer.enabled = _sourceWasEnabled;
            }
            DestroyOwnedObject(_previewObject);
            DestroyOwnedObject(PreviewMesh);
        }

        private static void CopyRendererSettings(
            SkinnedMeshRenderer source,
            MeshRenderer destination)
        {
            destination.sharedMaterials = source.sharedMaterials;
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.probeAnchor = source.probeAnchor;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(target);
                return;
            }
#endif
            UnityEngine.Object.Destroy(target);
        }
    }
}
