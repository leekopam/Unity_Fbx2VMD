using System;
using System.Collections.Generic;
using System.IO;
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
            NativeSkinningRendererCorrection[] rendererCorrections,
            int collectedFailureCount = 0)
        {
            FrameCount = frameCount;
            CorrectedFrameCount = correctedFrameCount;
            FallbackFrameCount = fallbackFrameCount;
            CorrectionEntryCount = correctionEntryCount;
            RendererCorrections = rendererCorrections;
            CollectedFailureCount = collectedFailureCount;
        }

        internal int FrameCount { get; }

        internal int CorrectedFrameCount { get; }

        internal int FallbackFrameCount { get; }

        internal int CorrectionEntryCount { get; }

        internal NativeSkinningRendererCorrection[] RendererCorrections { get; }

        /// <summary>
        /// 수집 모드에서 보정 없이 건너뛴 실패 수입니다. 0보다 크면 결과는 부분 보정입니다.
        /// </summary>
        internal int CollectedFailureCount { get; }
    }

    /// <summary>
    /// 병렬 사전 계산 입력. 메인스레드에서 미리 캡처한 정점 스냅샷과 본 변환을 담고,
    /// 솔버가 워커 스레드에서 Unity API 없이 처리할 수 있게 한다.
    /// </summary>
    internal sealed class NativeSkinningPreparedFrame
    {
        internal NativeSkinningPreparedFrame(
            int frameIndex,
            IReadOnlyDictionary<SkinnedMeshRenderer, Vector3[]> baselineVertices,
            IReadOnlyDictionary<
                SkinnedMeshRenderer,
                NativeSkinningDualQuaternionTransform[]> boneTransforms)
        {
            FrameIndex = frameIndex;
            BaselineVertices = baselineVertices;
            BoneTransforms = boneTransforms;
        }

        internal int FrameIndex { get; }

        internal IReadOnlyDictionary<SkinnedMeshRenderer, Vector3[]>
            BaselineVertices { get; }

        internal IReadOnlyDictionary<
            SkinnedMeshRenderer,
            NativeSkinningDualQuaternionTransform[]> BoneTransforms { get; }
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
        // 진단용: 수집 모드에서 실패한 프레임의 오류 메시지 누적.
        private readonly List<string> _collectedFailures = new List<string>();
        private readonly bool _collectModeEnabled;
        // 병렬 솔버 커밋과 취소/실패가 겹칠 때 상태 쓰기를 직렬화한다.
        private readonly object _stateLock = new object();
        // 진단 디렉터리는 워커 스레드에서 읽을 수 없으므로 생성 시점에 미리 구한다.
        private readonly string _diagnosticsDirectory;
        private int _processedFrameCount;
        private volatile bool _isComplete;
        private volatile bool _isFaulted;
        private volatile bool _isCanceled;

        internal NativeSkinningCorrectionPreprocessSession(
            int frameCount,
            RendererWork[] rendererWorks,
            Func<int, SkinnedMeshRenderer, Vector3[]> readFrameVertices,
            Action<int, int> reportProgress,
            bool collectFailures)
        {
            _frameCount = frameCount;
            _rendererWorks = rendererWorks;
            _readFrameVertices = readFrameVertices;
            _reportProgress = reportProgress;
            _collectModeEnabled = collectFailures;
            _diagnosticsDirectory = GetDiagnosticsDirectory();
        }

        internal int ProcessedFrameCount => _processedFrameCount;

        internal float Progress => (float)_processedFrameCount / _frameCount;

        internal bool IsComplete
        {
            get => _isComplete;
            private set => _isComplete = value;
        }

        internal bool IsFaulted
        {
            get => _isFaulted;
            private set => _isFaulted = value;
        }

        internal bool IsCanceled
        {
            get => _isCanceled;
            private set => _isCanceled = value;
        }

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
                        // 수집 모드에서는 실패 프레임을 기록하고 보정 없이 계속 진행한다.
                        if (_collectModeEnabled)
                        {
                            _collectedFailures.Add(
                                $"renderer={work.RendererName} {errorMessage}");
                            continue;
                        }
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

                _processedFrameCount++;
                _reportProgress?.Invoke(_processedFrameCount, _frameCount);
                if (_processedFrameCount == _frameCount)
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
            lock (_stateLock)
            {
                ClearPartialFrames();
            }
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
            return TryCalculateRendererFrameCore(
                frameIndex,
                baselineVertices,
                () => TrySampleBoneTransforms(work.Renderer),
                work,
                out frame,
                out usedFallback,
                out errorMessage);
        }

        // 프레임 독립 계산부. Unity API에 접근하지 않아 워커 스레드에서도 실행 가능하다.
        private bool TryCalculateRendererFrameCore(
            int frameIndex,
            Vector3[] baselineVertices,
            Func<NativeSkinningDualQuaternionTransform[]> boneTransformsProvider,
            RendererWork work,
            out NativeSkinningCorrectionFrame frame,
            out bool usedFallback,
            out string errorMessage)
        {
            frame = null;
            usedFallback = false;
            errorMessage = string.Empty;
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
                    work.RendererName,
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
                    if (boneTransforms == null)
                    {
                        boneTransforms = boneTransformsProvider?.Invoke();
                    }
                    if (boneTransforms == null)
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

        // 순차 경로에서만 사용: 필요할 때만 본 자세를 표본화한다 (메인스레드 전용).
        private static NativeSkinningDualQuaternionTransform[]
            TrySampleBoneTransforms(SkinnedMeshRenderer renderer)
        {
            return NativeSkinningBoneTransformSampler.TrySample(
                    renderer,
                    out Matrix4x4[] skinningMatrices) &&
                NativeSkinningDualQuaternionVertexCalculator.TryBuildTransforms(
                    skinningMatrices,
                    out NativeSkinningDualQuaternionTransform[] transforms)
                ? transforms
                : null;
        }

        private bool TryCalculateSurfaceCorrections(
            int frameIndex,
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract[] contracts,
            string rendererName,
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
            errorMessage = $"{failure} renderer={rendererName} " +
                $"side={failedContract.Side} contract={failedContractIndex}";
            errorMessage += " failedContractFolds=" + string.Join(",",
                Enumerable.Range(0, contracts.Length)
                    .Where(index => !successByContract[index])
                    .Select(index => $"{index}:{contracts[index].Side}:" +
                        (calculatedCorrections[index] == null
                            ? "none"
                            : calculatedCorrections[index]
                                .ResidualSharpFoldCount.ToString())));
            TryDumpFailureBaselineVertices(
                frameIndex,
                rendererName,
                vertices);
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

        // 수집 모드 완료 시 누적된 실패 목록을 로그 파일로 남긴다.
        private void WriteCollectedFailures()
        {
            if (_collectedFailures.Count == 0)
            {
                return;
            }
            try
            {
                string directory = _diagnosticsDirectory;
                Directory.CreateDirectory(directory);
                // 연속 런 간 덮어쓰기를 피하기 위해 타임스탬프를 붙인다.
                string path = Path.Combine(
                    directory,
                    $"native-prep-failures-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
                File.WriteAllLines(path, _collectedFailures);
                Debug.LogWarning(
                    $"[NativePrepDump] 실패 {_collectedFailures.Count}건 수집 완료: {path}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[NativePrepDump] 실패 목록 기록 실패: {exception.Message}");
            }
        }

        internal static string GetDiagnosticsDirectory()
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../Docs/Workflow/Local/runtime/native-prep-dumps"));
        }

        // 진단용: 플래그 파일이 있을 때만 실패 프레임의 입력 정점을 덤프함.
        private void TryDumpFailureBaselineVertices(
            int frameIndex,
            string rendererName,
            IReadOnlyList<Vector3> vertices)
        {
            try
            {
                string directory = _diagnosticsDirectory;
                if (!File.Exists(
                        Path.Combine(directory, "native-prep-dump.flag")))
                {
                    return;
                }
                Directory.CreateDirectory(directory);
                // GameObject 이름에는 파일명 비허용 문자(경로 구분자 포함)가 올 수 있어 정제한다.
                string safeName = rendererName ?? "null";
                foreach (char invalid in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(invalid, '_');
                }
                string path = Path.Combine(directory,
                    $"f{frameIndex}-{safeName}.bin");
                using var writer = new BinaryWriter(File.Create(path));
                writer.Write(vertices.Count);
                foreach (Vector3 vertex in vertices)
                {
                    writer.Write(vertex.x);
                    writer.Write(vertex.y);
                    writer.Write(vertex.z);
                }
                Debug.Log(
                    $"[NativePrepDump] frame={frameIndex} renderer={rendererName} " +
                    $"verts={vertices.Count} path={path}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[NativePrepDump] 실패 입력 덤프 기록 실패: {exception.Message}");
            }
        }

        private void Complete()
        {
            lock (_stateLock)
            {
                CompleteLocked();
            }
        }

        // 호출자가 _stateLock을 쥔 상태에서만 호출한다.
        private void CompleteLocked()
        {
            WriteCollectedFailures();
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
                rendererCorrections,
                _collectedFailures.Count);
            IsComplete = true;
        }

        private bool Fail(string message)
        {
            lock (_stateLock)
            {
                FailureMessage = string.IsNullOrWhiteSpace(message)
                    ? "Native 보정 사전 계산에 실패했습니다."
                    : message;
                IsFaulted = true;
                Result = null;
                ClearPartialFrames();
                return false;
            }
        }

        // 호출자가 _stateLock을 쥔 상태에서만 호출한다.
        private void ClearPartialFrames()
        {
            foreach (RendererWork work in _rendererWorks)
            {
                work.Frames.Clear();
            }
        }

        /// <summary>
        /// 미리 캡처한 프레임 스냅샷 묶음을 병렬로 계산하고 결과를 프레임 순서대로 커밋함.
        /// 워커 스레드에서 호출되므로 Unity API에 접근하지 않는다.
        /// </summary>
        internal bool TryProcessPreparedFrames(
            IReadOnlyList<NativeSkinningPreparedFrame> preparedFrames)
        {
            if (preparedFrames == null)
            {
                return Fail("병렬 사전 계산 입력이 없습니다.");
            }

            int expectedFrameIndex = _processedFrameCount;
            var outcomes = new PreparedFrameOutcome[preparedFrames.Count];
            try
            {
                Parallel.For(0, preparedFrames.Count, index =>
                {
                    if (IsCanceled || IsFaulted)
                    {
                        return;
                    }

                    NativeSkinningPreparedFrame prepared = preparedFrames[index];
                    if (prepared == null ||
                        prepared.FrameIndex != expectedFrameIndex + index)
                    {
                        outcomes[index] = PreparedFrameOutcome.Fail(
                            "병렬 사전 계산 입력이 프레임 순서와 다릅니다.");
                        return;
                    }
                    outcomes[index] = SolvePreparedFrame(prepared);
                });

                // 계산 결과는 프레임 순서대로 커밋해 순차 처리와 동일한 결과 순서를 보장한다.
                foreach (PreparedFrameOutcome outcome in outcomes)
                {
                    if (outcome == null || IsCanceled)
                    {
                        break;
                    }
                    if (outcome.FatalMessage != null)
                    {
                        return Fail(outcome.FatalMessage);
                    }
                    lock (_stateLock)
                    {
                        if (IsCanceled)
                        {
                            break;
                        }
                        _collectedFailures.AddRange(outcome.WorkFailures);
                        foreach (PendingRendererFrame pending in outcome.Frames)
                        {
                            pending.Work.Frames.Add(pending.Frame);
                        }
                        if (outcome.Frames.Count > 0)
                        {
                            _correctedFrameCount++;
                            _correctionEntryCount += outcome.CorrectionEntryCount;
                        }
                        if (outcome.UsedFallback)
                        {
                            _fallbackFrameCount++;
                        }
                        _processedFrameCount++;
                        _reportProgress?.Invoke(
                            _processedFrameCount,
                            _frameCount);
                        if (_processedFrameCount == _frameCount)
                        {
                            CompleteLocked();
                            break;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                return Fail(
                    $"Native 보정 사전 계산 중 예외가 발생했습니다: {exception.Message}");
            }
            return !IsFaulted;
        }

        // 한 프레임의 모든 렌더러 계약을 스냅샷 입력으로 계산한다 (Unity API 미사용).
        private PreparedFrameOutcome SolvePreparedFrame(
            NativeSkinningPreparedFrame prepared)
        {
            int frameIndex = prepared.FrameIndex;
            var outcome = new PreparedFrameOutcome();
            foreach (RendererWork work in _rendererWorks)
            {
                Vector3[] baselineVertices = null;
                NativeSkinningDualQuaternionTransform[] boneTransforms = null;
                prepared.BaselineVertices?.TryGetValue(
                    work.Renderer,
                    out baselineVertices);
                prepared.BoneTransforms?.TryGetValue(
                    work.Renderer,
                    out boneTransforms);
                NativeSkinningDualQuaternionTransform[] capturedTransforms =
                    boneTransforms;
                if (!TryCalculateRendererFrameCore(
                        frameIndex,
                        baselineVertices,
                        () => capturedTransforms,
                        work,
                        out NativeSkinningCorrectionFrame frame,
                        out bool usedFallback,
                        out string errorMessage))
                {
                    if (_collectModeEnabled)
                    {
                        outcome.WorkFailures.Add(
                            $"renderer={work.RendererName} {errorMessage}");
                        continue;
                    }
                    outcome.FatalMessage = errorMessage;
                    return outcome;
                }
                outcome.UsedFallback |= usedFallback;
                if (frame != null)
                {
                    outcome.Frames.Add(new PendingRendererFrame(work, frame));
                    outcome.CorrectionEntryCount += frame.CorrectionCount;
                }
            }
            return outcome;
        }

        // 병렬 프레임 하나의 계산 결과. 커밋은 호출 순서(프레임 순서)대로만 이뤄진다.
        private sealed class PreparedFrameOutcome
        {
            internal static PreparedFrameOutcome Fail(string message)
            {
                var outcome = new PreparedFrameOutcome();
                outcome.FatalMessage = message;
                return outcome;
            }

            internal List<PendingRendererFrame> Frames { get; } =
                new List<PendingRendererFrame>();

            internal List<string> WorkFailures { get; } =
                new List<string>();

            internal string FatalMessage { get; set; }

            internal bool UsedFallback { get; set; }

            internal int CorrectionEntryCount { get; set; }
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
                // 워커 스레드의 오류 메시지용 — 생성 시점(메인스레드)에 이름을 캐시한다.
                RendererName = renderer.name;
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

            internal string RendererName { get; }

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
        // 진단용: 플래그 파일이 있으면 세션을 실패 수집 모드로 생성해야 함을 나타낸다.
        internal static bool IsFailureCollectionRequested()
        {
            return File.Exists(Path.Combine(
                NativeSkinningCorrectionPreprocessSession
                    .GetDiagnosticsDirectory(),
                "native-prep-collect.flag"));
        }

        // 진단용: 첫 실패에서 멈추지 않고 실패 프레임 전체를 수집하는 세션.
        internal static bool TryCreateFailureCollectingSession(
            int frameCount,
            NativeSkinningSurfaceContract[] contracts,
            Func<int, SkinnedMeshRenderer, Vector3[]> readFrameVertices,
            Action<int, int> reportProgress,
            out NativeSkinningCorrectionPreprocessSession session)
        {
            return TryCreateSessionCore(
                frameCount,
                contracts,
                readFrameVertices,
                reportProgress,
                out session,
                collectFailures: true);
        }

        internal static bool TryCreateSession(
            int frameCount,
            NativeSkinningSurfaceContract[] contracts,
            Func<int, SkinnedMeshRenderer, Vector3[]> readFrameVertices,
            Action<int, int> reportProgress,
            out NativeSkinningCorrectionPreprocessSession session)
        {
            return TryCreateSessionCore(
                frameCount,
                contracts,
                readFrameVertices,
                reportProgress,
                out session,
                collectFailures: false);
        }

        private static bool TryCreateSessionCore(
            int frameCount,
            NativeSkinningSurfaceContract[] contracts,
            Func<int, SkinnedMeshRenderer, Vector3[]> readFrameVertices,
            Action<int, int> reportProgress,
            out NativeSkinningCorrectionPreprocessSession session,
            bool collectFailures)
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
                reportProgress,
                collectFailures);
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
