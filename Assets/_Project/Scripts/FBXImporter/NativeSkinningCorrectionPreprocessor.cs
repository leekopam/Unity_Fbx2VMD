using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            NativeSkinningDualQuaternionTransform[] boneTransforms = null;
            if (!TryCalculateSurfaceCorrections(
                    frameIndex,
                    baselineVertices,
                    work.Contracts,
                    out NativeSkinningSurfaceCorrectionResult[] surfaceCorrections,
                    out bool surfaceUsedFallback,
                    out errorMessage))
            {
                return false;
            }
            usedFallback |= surfaceUsedFallback;
            for (int contractIndex = 0;
                 contractIndex < work.Contracts.Length;
                 contractIndex++)
            {
                NativeSkinningSurfaceContract contract =
                    work.Contracts[contractIndex];
                NativeSkinningSurfaceCorrectionResult correction =
                    surfaceCorrections[contractIndex];
                IReadOnlyList<Vector3> correctedVertices =
                    correction.CorrectedVertices;
                IReadOnlyCollection<int> correctedVertexIndices =
                    correction.CorrectedVertexIndices;

                if (correction.UsedRestShapeRecovery)
                {
                    if (boneTransforms == null &&
                        (!NativeSkinningBoneTransformSampler.TrySample(
                             work.Renderer,
                             out Matrix4x4[] skinningMatrices) ||
                         !NativeSkinningDualQuaternionVertexCalculator
                             .TryBuildTransforms(
                                 skinningMatrices,
                                 out boneTransforms)))
                    {
                        errorMessage =
                            $"frame {frameIndex}의 Native 스키닝 본 자세를 변환하지 못했습니다.";
                        return false;
                    }

                    if (!NativeSkinningDualQuaternionBlendCalculator
                            .TryCalculateWithPreparedTransforms(
                                correctedVertices,
                                work.RestVertices,
                                boneTransforms,
                                work.BlendContracts[contractIndex],
                                out NativeSkinningDualQuaternionBlendResult
                                    volumeCorrection))
                    {
                        errorMessage =
                            $"frame {frameIndex}의 Native 체적 보정을 계산하지 못했습니다.";
                        return false;
                    }
                    correctedVertices = volumeCorrection.BlendedVertices;
                    correctedVertexIndices = correctedVertexIndices
                        .Concat(volumeCorrection.ChangedVertexIndices)
                        .Distinct()
                        .ToArray();
                }
                AddSparseCorrections(
                    baselineVertices,
                    correctedVertices,
                    correctedVertexIndices,
                    deltaByVertex);
            }

            NativeSkinningVertexCorrection[] corrections = deltaByVertex
                .OrderBy(entry => entry.Key)
                .Select(entry => new NativeSkinningVertexCorrection(
                    entry.Key,
                    entry.Value))
                .Where(correction =>
                    correction.Delta.sqrMagnitude > MinimumCorrectionSquaredMagnitude)
                .ToArray();
            if (corrections.Length == 0)
            {
                return true;
            }
            frame = new NativeSkinningCorrectionFrame(frameIndex, corrections);
            return true;
        }

        private static bool TryCalculateSurfaceCorrections(
            int frameIndex,
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract[] contracts,
            out NativeSkinningSurfaceCorrectionResult[] corrections,
            out bool usedFallback,
            out string errorMessage)
        {
            var calculatedCorrections =
                new NativeSkinningSurfaceCorrectionResult[contracts.Length];
            corrections = calculatedCorrections;
            var successByContract = new bool[contracts.Length];
            var fallbackByContract = new bool[contracts.Length];
            var errorByContract = new string[contracts.Length];
            if (contracts.Length == 1)
            {
                bool fallback = false;
                bool success = TryCalculateValidatedSurfaceCorrection(
                    frameIndex,
                    vertices,
                    contracts[0],
                    ref fallback,
                    out calculatedCorrections[0],
                    out errorByContract[0]);
                successByContract[0] = success;
                fallbackByContract[0] = fallback;
            }
            else
            {
                Parallel.For(0, contracts.Length, contractIndex =>
                {
                    bool fallback = false;
                    successByContract[contractIndex] =
                        TryCalculateValidatedSurfaceCorrection(
                            frameIndex,
                            vertices,
                            contracts[contractIndex],
                            ref fallback,
                            out calculatedCorrections[contractIndex],
                            out errorByContract[contractIndex]);
                    fallbackByContract[contractIndex] = fallback;
                });
            }

            usedFallback = fallbackByContract.Any(value => value);
            int failedContractIndex = Array.FindIndex(
                successByContract,
                success => !success);
            if (failedContractIndex < 0)
            {
                errorMessage = string.Empty;
                return true;
            }

            string failure = string.IsNullOrWhiteSpace(
                    errorByContract[failedContractIndex])
                ? $"frame {frameIndex}의 Native 표면 보정을 계산하지 못했습니다."
                : errorByContract[failedContractIndex];
            NativeSkinningSurfaceContract failedContract =
                contracts[failedContractIndex];
            NativeSkinningSurfaceCorrectionResult failedCorrection =
                calculatedCorrections[failedContractIndex];
            errorMessage = $"{failure} renderer={failedContract.Renderer.name} " +
                $"side={failedContract.Side} contract={failedContractIndex}";
            if (failedCorrection != null)
            {
                errorMessage +=
                    $" initialFold={failedCorrection.InitialSharpFoldCount}" +
                    $" residualFold={failedCorrection.ResidualSharpFoldCount}" +
                    $" newFold={failedCorrection.NewSharpFoldCount}" +
                    $" degenerate={failedCorrection.NewDegenerateFaceCount}" +
                    $" reversed={failedCorrection.ReversedFaceCount}" +
                    $" displacement={failedCorrection.MaximumVertexDisplacement:F6}" +
                    $" strain={failedCorrection.MaximumEdgeLengthStrain:F6}" +
                    $" restRecovery={failedCorrection.UsedRestShapeRecovery}" +
                    $" restStrain={failedCorrection.MaximumRestEdgeStrainIncrease:F6}" +
                    $" unresolved={failedCorrection.UnresolvedRestShapeRecoveryCount}";
                for (int pairIndex = 0;
                     pairIndex < failedContract.FacePairs.Length;
                     pairIndex++)
                {
                    if (failedContract.RestAnglesDegrees[pairIndex] >
                            NativeSkinningSurfaceDefectDetector
                                .MaximumCorrectableRestAngleDegrees ||
                        !NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                            failedCorrection.CorrectedVertices,
                            failedContract.FacePairs[pairIndex],
                            out float correctedAngle) ||
                        correctedAngle <= NativeSkinningSurfaceDefectDetector
                            .MinimumSharpFoldAngleDegrees)
                    {
                        continue;
                    }

                    NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        failedContract.FacePairs[pairIndex],
                        out float baselineAngle);
                    errorMessage += $" pair={pairIndex}" +
                        $" restAngle={failedContract.RestAnglesDegrees[pairIndex]:F2}" +
                        $" baselineAngle={baselineAngle:F2}" +
                        $" correctedAngle={correctedAngle:F2}";
                    break;
                }
            }
            return false;
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

        private static bool TryCalculateValidatedSurfaceCorrection(
            int frameIndex,
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            ref bool usedFallback,
            out NativeSkinningSurfaceCorrectionResult correction,
            out string errorMessage)
        {
            correction = null;
            errorMessage = string.Empty;
            if (!NativeSkinningSurfaceCorrectionCalculator
                    .TryCalculateFastValidatedFrame(
                        vertices,
                        contract,
                        out correction))
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
                            vertices,
                            contract,
                            out correction))
                {
                    errorMessage =
                        $"frame {frameIndex}의 강한 표면 보정을 계산하지 못했습니다.";
                    return false;
                }
            }
            if (NativeSkinningSurfaceCorrectionCalculator
                    .IsWithinStrongQuality(correction, contract))
            {
                return true;
            }

            errorMessage =
                $"frame {frameIndex}의 Native 보정이 품질 계약을 통과하지 못했습니다.";
            return false;
        }

        private static void AddSparseCorrections(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> correctedVertices,
            IEnumerable<int> correctedVertexIndices,
            IDictionary<int, Vector3> deltaByVertex)
        {
            foreach (int vertexIndex in correctedVertexIndices)
            {
                Vector3 delta = correctedVertices[vertexIndex] -
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
            private RendererWork(
                SkinnedMeshRenderer renderer,
                NativeSkinningSurfaceContract[] contracts,
                NativeSkinningDualQuaternionBlendContract[] blendContracts,
                Vector3[] restVertices)
            {
                Renderer = renderer;
                Contracts = contracts;
                BlendContracts = blendContracts;
                VertexCount = restVertices.Length;
                RestVertices = restVertices;
                Frames = new List<NativeSkinningCorrectionFrame>();
            }

            internal static bool TryCreate(
                SkinnedMeshRenderer renderer,
                NativeSkinningSurfaceContract[] contracts,
                out RendererWork work)
            {
                work = null;
                if (renderer == null ||
                    renderer.sharedMesh == null ||
                    contracts == null ||
                    contracts.Length == 0)
                {
                    return false;
                }

                Mesh mesh = renderer.sharedMesh;
                Vector3[] restVertices = mesh.vertices;
                BoneWeight[] boneWeights = mesh.boneWeights;
                if (restVertices.Length != mesh.vertexCount ||
                    boneWeights.Length != mesh.vertexCount ||
                    !NativeSkinningSurfaceCorrectionCalculator.AreVerticesFinite(
                        restVertices))
                {
                    return false;
                }

                var blendContracts = new NativeSkinningDualQuaternionBlendContract[
                    contracts.Length];
                for (int contractIndex = 0;
                     contractIndex < contracts.Length;
                     contractIndex++)
                {
                    if (!NativeSkinningDualQuaternionBlendContractBuilder.TryBuild(
                            boneWeights,
                            contracts[contractIndex],
                            out blendContracts[contractIndex]))
                    {
                        return false;
                    }
                }

                work = new RendererWork(
                    renderer,
                    contracts,
                    blendContracts,
                    restVertices);
                return true;
            }

            internal SkinnedMeshRenderer Renderer { get; }

            internal NativeSkinningSurfaceContract[] Contracts { get; }

            internal NativeSkinningDualQuaternionBlendContract[] BlendContracts { get; }

            internal int VertexCount { get; }

            internal Vector3[] RestVertices { get; }

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

            var works = new List<
                NativeSkinningCorrectionPreprocessSession.RendererWork>();
            foreach (IGrouping<SkinnedMeshRenderer, NativeSkinningSurfaceContract>
                     group in contracts.GroupBy(contract => contract.Renderer))
            {
                NativeSkinningSurfaceContract[] rendererContracts = group.ToArray();
                if (group.Key == null ||
                    group.Key.sharedMesh == null ||
                    rendererContracts.Any(contract =>
                        contract.VertexCount != group.Key.sharedMesh.vertexCount) ||
                    !NativeSkinningCorrectionPreprocessSession.RendererWork.TryCreate(
                        group.Key,
                        rendererContracts,
                        out NativeSkinningCorrectionPreprocessSession.RendererWork work))
                {
                    return false;
                }
                works.Add(work);
            }
            if (works.Count == 0 ||
                works.Sum(work => work.Contracts.Length) != contracts.Length)
            {
                return false;
            }

            session = new NativeSkinningCorrectionPreprocessSession(
                frameCount,
                works.ToArray(),
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
