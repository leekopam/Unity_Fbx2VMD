using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct NativeSkinningVertexCorrection
    {
        internal NativeSkinningVertexCorrection(int vertexIndex, Vector3 delta)
        {
            if (vertexIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexIndex));
            }
            if (!IsFinite(delta))
            {
                throw new ArgumentException("정점 보정값은 유한한 값이어야 합니다.", nameof(delta));
            }

            VertexIndex = vertexIndex;
            Delta = delta;
        }

        internal int VertexIndex { get; }

        internal Vector3 Delta { get; }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    internal sealed class NativeSkinningCorrectionFrame
    {
        private readonly NativeSkinningVertexCorrection[] _corrections;

        internal NativeSkinningCorrectionFrame(
            int frameIndex,
            NativeSkinningVertexCorrection[] corrections)
        {
            if (frameIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameIndex));
            }
            if (corrections == null)
            {
                throw new ArgumentNullException(nameof(corrections));
            }

            var uniqueVertexIndices = new HashSet<int>();
            _corrections = new NativeSkinningVertexCorrection[corrections.Length];
            for (int index = 0; index < corrections.Length; index++)
            {
                NativeSkinningVertexCorrection correction = corrections[index];
                if (!uniqueVertexIndices.Add(correction.VertexIndex))
                {
                    throw new ArgumentException(
                        $"같은 프레임에 정점 {correction.VertexIndex} 보정이 중복됐습니다.",
                        nameof(corrections));
                }

                _corrections[index] = correction;
            }

            FrameIndex = frameIndex;
        }

        internal int FrameIndex { get; }

        internal int CorrectionCount => _corrections.Length;

        internal bool TryApply(IList<Vector3> vertices)
        {
            if (vertices == null)
            {
                return false;
            }

            for (int index = 0; index < _corrections.Length; index++)
            {
                if (_corrections[index].VertexIndex >= vertices.Count)
                {
                    return false;
                }
            }

            for (int index = 0; index < _corrections.Length; index++)
            {
                NativeSkinningVertexCorrection correction = _corrections[index];
                vertices[correction.VertexIndex] += correction.Delta;
            }

            return true;
        }
    }

    /// <summary>
    /// 모델 토폴로지에 맞춰 사전 계산한 프레임별 희소 정점 보정을 보관함.
    /// </summary>
    internal sealed class NativeSkinningCorrectionCache
    {
        private readonly Dictionary<int, NativeSkinningCorrectionFrame> _frames;

        internal NativeSkinningCorrectionCache(
            int vertexCount,
            NativeSkinningCorrectionFrame[] frames)
        {
            if (vertexCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(vertexCount));
            }
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            VertexCount = vertexCount;
            _frames = new Dictionary<int, NativeSkinningCorrectionFrame>(frames.Length);
            for (int index = 0; index < frames.Length; index++)
            {
                NativeSkinningCorrectionFrame frame = frames[index] ??
                    throw new ArgumentException("null 보정 프레임을 저장할 수 없습니다.", nameof(frames));
                if (!_frames.TryAdd(frame.FrameIndex, frame))
                {
                    throw new ArgumentException(
                        $"프레임 {frame.FrameIndex} 보정이 중복됐습니다.",
                        nameof(frames));
                }
            }
        }

        internal int VertexCount { get; }

        internal int CorrectedFrameCount => _frames.Count;

        internal bool TryApply(
            int frameIndex,
            IList<Vector3> vertices,
            out int appliedCorrectionCount)
        {
            appliedCorrectionCount = 0;
            if (frameIndex < 0 || vertices == null || vertices.Count != VertexCount)
            {
                return false;
            }

            if (!_frames.TryGetValue(frameIndex, out NativeSkinningCorrectionFrame frame))
            {
                return true;
            }

            if (!frame.TryApply(vertices))
            {
                return false;
            }

            appliedCorrectionCount = frame.CorrectionCount;
            return true;
        }
    }
}
