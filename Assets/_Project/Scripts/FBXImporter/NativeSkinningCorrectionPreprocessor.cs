using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class NativeSkinningRendererCorrection
    {
        internal NativeSkinningRendererCorrection(
            SkinnedMeshRenderer renderer,
            NativeSkinningCorrectionCache cache)
        {
            Renderer = renderer != null
                ? renderer
                : throw new ArgumentNullException(nameof(renderer));
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        internal SkinnedMeshRenderer Renderer { get; }

        internal NativeSkinningCorrectionCache Cache { get; }
    }

    internal sealed class NativeSkinningCorrectionPreprocessResult
    {
        internal NativeSkinningCorrectionPreprocessResult(
            int frameCount,
            int correctedFrameCount,
            int fallbackFrameCount,
            int correctionEntryCount,
            NativeSkinningRendererCorrection[] rendererCorrections)
        {
            FrameCount = frameCount;
            CorrectedFrameCount = correctedFrameCount;
            FallbackFrameCount = fallbackFrameCount;
            CorrectionEntryCount = correctionEntryCount;
            RendererCorrections = rendererCorrections;
        }

        internal int FrameCount { get; }

        internal int CorrectedFrameCount { get; }

        internal int FallbackFrameCount { get; }

        internal int CorrectionEntryCount { get; }

        internal NativeSkinningRendererCorrection[] RendererCorrections { get; }
    }

    /// <summary>
    /// Native 보정 사전 계산을 프레임 단위로 진행하고 완료 결과만 공개함.
    /// </summary>
    internal sealed class NativeSkinningCorrectionPreprocessSession
    {
        private const float MinimumCorrectionSquaredMagnitude =
            0.0000000000000001f;
        private readonly int _frameCount;
        private readonly RendererWork[] _rendererWorks;
        private readonly Func<int, SkinnedMeshRenderer, Vector3[]>
            _readFrameVertices;
        private readonly Action<int, int> _reportProgress;
        private int _correctedFrameCount;
        private int _fallbackFrameCount;
        private int _correctionEntryCount;

        internal NativeSkinningCorrectionPreprocessSession(
            int frameCount,
            RendererWork[] rendererWorks,
            Func<int, SkinnedMeshRenderer, Vector3[]> readFrameVertices,
            Action<int, int> reportProgress)
        {
            _frameCount = frameCount;
            _rendererWorks = rendererWorks;
            _readFrameVertices = readFrameVertices;
            _reportProgress = reportProgress;
        }

        internal int ProcessedFrameCount { get; private set; }

        internal float Progress => (float)ProcessedFrameCount / _frameCount;

        internal bool IsComplete { get; private set; }

        internal bool IsFaulted { get; private set; }

        internal bool IsCanceled { get; private set; }

        internal bool IsFinished => IsComplete || IsFaulted || IsCanceled;

        internal string FailureMessage { get; private set; } = string.Empty;

        internal NativeSkinningCorrectionPreprocessResult Result { get; private set; }

        internal bool TryProcessNextFrame()
        {
            if (IsComplete)
            {
                return true;
            }
            if (IsFaulted || IsCanceled)
            {
                return false;
            }

            try
            {
                int frameIndex = ProcessedFrameCount;
                var pendingFrames = new List<PendingRendererFrame>(
                    _rendererWorks.Length);
                bool usedFallback = false;
                int frameCorrectionEntryCount = 0;
                foreach (RendererWork work in _rendererWorks)
                {
                    if (!TryCalculateRendererFrame(
                            frameIndex,
                            work,
                            out NativeSkinningCorrectionFrame frame,
                            out bool rendererUsedFallback,
                            out string errorMessage))
                    {
                        return Fail(errorMessage);
                    }
                    usedFallback |= rendererUsedFallback;
                    if (frame == null)
                    {
                        continue;
                    }
                    pendingFrames.Add(new PendingRendererFrame(work, frame));
                    frameCorrectionEntryCount += frame.CorrectionCount;
                }

                foreach (PendingRendererFrame pending in pendingFrames)
                {
                    pending.Work.Frames.Add(pending.Frame);
                }
                if (pendingFrames.Count > 0)
                {
                    _correctedFrameCount++;
                    _correctionEntryCount += frameCorrectionEntryCount;
                }
                if (usedFallback)
                {
                    _fallbackFrameCount++;
                }

                ProcessedFrameCount++;
                _reportProgress?.Invoke(ProcessedFrameCount, _frameCount);
                if (ProcessedFrameCount == _frameCount)
                {
                    Complete();
                }
                return true;
            }
            catch (Exception exception)
            {
                return Fail(
                    $"Native 보정 사전 계산 중 예외가 발생했습니다: {exception.Message}");
            }
        }

        internal void Cancel()
        {
            if (IsFinished)
            {
                return;
            }
            IsCanceled = true;
            ClearPartialFrames();
        }

        private bool TryCalculateRendererFrame(
            int frameIndex,
            RendererWork work,
            out NativeSkinningCorrectionFrame frame,
            out bool usedFallback,
            out string errorMessage)
        {
            frame = null;
            usedFallback = false;
            errorMessage = string.Empty;
            if (work.Renderer == null ||
                work.Renderer.sharedMesh == null ||
                work.Renderer.sharedMesh.vertexCount != work.VertexCount)
            {
                errorMessage = "사전 계산 중 Renderer 토폴로지가 변경됐습니다.";
                return false;
            }

            Vector3[] baselineVertices = _readFrameVertices(
                frameIndex,
                work.Renderer);
            if (baselineVertices == null || baselineVertices.Length != work.VertexCount)
            {
                errorMessage =
                    $"frame {frameIndex}의 스키닝 정점 수가 계약과 다릅니다.";
                return false;
            }
            if (!NativeSkinningSurfaceCorrectionCalculator.AreVerticesFinite(
                    baselineVertices))
            {
                errorMessage =
                    $"frame {frameIndex}의 스키닝 정점에 유한하지 않은 값이 있습니다.";
                return false;
            }

            var deltaByVertex = new Dictionary<int, Vector3>();
            foreach (NativeSkinningSurfaceContract contract in work.Contracts)
            {
                if (!NativeSkinningSurfaceCorrectionCalculator
                        .TryCalculateFastValidatedFrame(
                        baselineVertices,
                        contract,
                        out NativeSkinningSurfaceCorrectionResult correction))
                {
                    errorMessage =
                        $"frame {frameIndex}의 빠른 표면 보정을 계산하지 못했습니다.";
                    return false;
                }
                if (!NativeSkinningSurfaceCorrectionCalculator
                        .IsWithinFastQuality(correction, contract))
                {
                    usedFallback = true;
                    if (!NativeSkinningSurfaceCorrectionCalculator
                            .TryCalculateStrongValidatedFrame(
                            baselineVertices,
                            contract,
                            out correction))
                    {
                        errorMessage =
                            $"frame {frameIndex}의 강한 표면 보정을 계산하지 못했습니다.";
                        return false;
                    }
                }
                if (!NativeSkinningSurfaceCorrectionCalculator
                        .IsWithinStrongQuality(correction, contract))
                {
                    errorMessage =
                        $"frame {frameIndex}의 Native 보정이 품질 계약을 통과하지 못했습니다.";
                    return false;
                }
                AddSparseCorrections(
                    baselineVertices,
                    correction,
                    deltaByVertex);
            }

            if (deltaByVertex.Count == 0)
            {
                return true;
            }
            NativeSkinningVertexCorrection[] corrections = deltaByVertex
                .OrderBy(entry => entry.Key)
                .Select(entry => new NativeSkinningVertexCorrection(
                    entry.Key,
                    entry.Value))
                .ToArray();
            frame = new NativeSkinningCorrectionFrame(frameIndex, corrections);
            return true;
        }

        private void Complete()
        {
            NativeSkinningRendererCorrection[] rendererCorrections =
                _rendererWorks
                    .Where(work => work.Frames.Count > 0)
                    .Select(work => new NativeSkinningRendererCorrection(
                        work.Renderer,
                        new NativeSkinningCorrectionCache(
                            work.VertexCount,
                            work.Frames.ToArray())))
                    .ToArray();
            Result = new NativeSkinningCorrectionPreprocessResult(
                _frameCount,
                _correctedFrameCount,
                _fallbackFrameCount,
                _correctionEntryCount,
                rendererCorrections);
            IsComplete = true;
        }

        private bool Fail(string message)
        {
            FailureMessage = string.IsNullOrWhiteSpace(message)
                ? "Native 보정 사전 계산에 실패했습니다."
                : message;
            IsFaulted = true;
            Result = null;
            ClearPartialFrames();
            return false;
        }

        private void ClearPartialFrames()
        {
            foreach (RendererWork work in _rendererWorks)
            {
                work.Frames.Clear();
            }
        }

        private static void AddSparseCorrections(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceCorrectionResult correction,
            IDictionary<int, Vector3> deltaByVertex)
        {
            if (correction.InitialSharpFoldCount == 0)
            {
                return;
            }
            foreach (int vertexIndex in correction.CorrectedVertexIndices)
            {
                Vector3 delta = correction.CorrectedVertices[vertexIndex] -
                    baselineVertices[vertexIndex];
                if (delta.sqrMagnitude <= MinimumCorrectionSquaredMagnitude)
                {
                    continue;
                }
                deltaByVertex[vertexIndex] =
                    deltaByVertex.TryGetValue(vertexIndex, out Vector3 existing)
                        ? existing + delta
                        : delta;
            }
        }

        internal sealed class RendererWork
        {
            internal RendererWork(
                SkinnedMeshRenderer renderer,
                NativeSkinningSurfaceContract[] contracts)
            {
                Renderer = renderer;
                Contracts = contracts;
                VertexCount = renderer.sharedMesh.vertexCount;
                Frames = new List<NativeSkinningCorrectionFrame>();
            }

            internal SkinnedMeshRenderer Renderer { get; }

            internal NativeSkinningSurfaceContract[] Contracts { get; }

            internal int VertexCount { get; }

            internal List<NativeSkinningCorrectionFrame> Frames { get; }
        }

        private readonly struct PendingRendererFrame
        {
            internal PendingRendererFrame(
                RendererWork work,
                NativeSkinningCorrectionFrame frame)
            {
                Work = work;
                Frame = frame;
            }

            internal RendererWork Work { get; }

            internal NativeSkinningCorrectionFrame Frame { get; }
        }
    }

    /// <summary>
    /// Native 보정 사전 계산 session을 검증하고 생성함.
    /// </summary>
    internal static class NativeSkinningCorrectionPreprocessor
    {
        internal static bool TryCreateSession(
            int frameCount,
            NativeSkinningSurfaceContract[] contracts,
            Func<int, SkinnedMeshRenderer, Vector3[]> readFrameVertices,
            Action<int, int> reportProgress,
            out NativeSkinningCorrectionPreprocessSession session)
        {
            session = null;
            if (frameCount <= 0 ||
                contracts == null ||
                contracts.Length == 0 ||
                contracts.Any(contract => contract == null) ||
                readFrameVertices == null)
            {
                return false;
            }

            NativeSkinningCorrectionPreprocessSession.RendererWork[] works =
                contracts
                    .GroupBy(contract => contract.Renderer)
                    .Where(group => group.Key != null &&
                        group.Key.sharedMesh != null &&
                        group.All(contract =>
                            contract.VertexCount == group.Key.sharedMesh.vertexCount))
                    .Select(group =>
                        new NativeSkinningCorrectionPreprocessSession.RendererWork(
                            group.Key,
                            group.ToArray()))
                    .ToArray();
            if (works.Length == 0 ||
                works.Sum(work => work.Contracts.Length) != contracts.Length)
            {
                return false;
            }

            session = new NativeSkinningCorrectionPreprocessSession(
                frameCount,
                works,
                readFrameVertices,
                reportProgress);
            return true;
        }

        internal static bool TryBuild(
            int frameCount,
            NativeSkinningSurfaceContract[] contracts,
            Func<int, SkinnedMeshRenderer, Vector3[]> readFrameVertices,
            Action<int, int> reportProgress,
            out NativeSkinningCorrectionPreprocessResult result)
        {
            result = null;
            if (!TryCreateSession(
                    frameCount,
                    contracts,
                    readFrameVertices,
                    reportProgress,
                    out NativeSkinningCorrectionPreprocessSession session))
            {
                return false;
            }
            while (!session.IsComplete)
            {
                if (!session.TryProcessNextFrame())
                {
                    return false;
                }
            }
            result = session.Result;
            return true;
        }
    }
}
