using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fbx2Vmd.Profiling;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Native 보정의 준비와 최종 프레임 표시를 늦은 Unity 수명주기에서 실행함.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    [DisallowMultipleComponent]
    internal sealed class NativeSkinningCorrectionPlaybackDriver : MonoBehaviour
    {
        private HumanoidMotionPlaybackController _playbackController;
        private Animator _animator;
        private NativeSkinningSurfaceContract[] _contracts =
            Array.Empty<NativeSkinningSurfaceContract>();
        private NativeSkinningCorrectionPreprocessSession _preprocessSession;
        private readonly Dictionary<SkinnedMeshRenderer, Mesh> _bakedMeshes =
            new Dictionary<SkinnedMeshRenderer, Mesh>();
        private readonly List<NativeSkinningCorrectionPresenter> _presenters =
            new List<NativeSkinningCorrectionPresenter>();
        private Renderer[] _hiddenRenderers = Array.Empty<Renderer>();
        private bool[] _rendererEnabledStates = Array.Empty<bool>();
        private bool _hasCapturedRendererStates;
        // 병렬 준비: 메인스레드가 스냅샷을 캡처해 큐에 넣으면 워커 Task가 배치로 계산한다.
        private const int MaxPendingPreparedFrames = 48;
        private const int MaxCapturesPerTick = 8;
        private const int SolveBatchFrameCount = 24;
        private int _nextCaptureFrame;
        private readonly Queue<NativeSkinningPreparedFrame> _pendingPreparedFrames =
            new Queue<NativeSkinningPreparedFrame>();
        private Task _solveTask;
        // 준비 병목 분해 계측 — 완료/실패 시 [NativePrepTiming] 로그로 남긴다.
        private readonly System.Diagnostics.Stopwatch _prepWallWatch =
            new System.Diagnostics.Stopwatch();
        private long _seekTicks;
        private long _bakeTicks;
        private long _boneSampleTicks;
        private long _solveTicks;
        private int _solverIdleTicks;
        private int _solverBackloggedTicks;
        // 디스크 캐시 — 식별자가 주입되지 않으면(테스트 등) 비활성.
        private string _cacheMotionName = string.Empty;
        private byte[] _cacheKeyHash;
        private bool _resultFromCache;
        // 빌드에서는 에셋 식별자가 없어 소스 FBX 경로가 캐시 키 입력이 된다.
        private string _runtimeCacheSourcePath = string.Empty;
#if UNITY_EDITOR
        private AnimationClip _cacheMotionClip;
        private GameObject _cacheSourceModelAsset;

        // 메뉴 설정과 다음 준비 사이에 도메인 리로드가 끼어도 유지되도록
        // SessionState(에디터 세션 생존)에 보관한다.
        private const string ForceRecalculateSessionKey =
            "Fbx2Vmd.NativeSkinningCorrection.ForceRecalculate";

        // 메뉴 등 수동 경로가 다음 준비에서 캐시 로드를 건너뛰고 재계산하게 함.
        internal static bool ForceCacheRecalculate
        {
            get => UnityEditor.SessionState.GetBool(
                ForceRecalculateSessionKey, false);
            set => UnityEditor.SessionState.SetBool(
                ForceRecalculateSessionKey, value);
        }
#endif

        internal bool IsConfigured =>
            _playbackController != null &&
            _animator != null &&
            _contracts.Length > 0;

        internal bool IsPreparing =>
            _preprocessSession != null && !_preprocessSession.IsFinished;

        internal bool IsReady { get; private set; }

        internal bool IsFaulted { get; private set; }

        internal float Progress => _preprocessSession?.Progress ??
            (IsReady ? 1f : 0f);

        internal int ProcessedFrameCount =>
            _preprocessSession?.ProcessedFrameCount ?? 0;

        internal int TotalFrameCount =>
            _playbackController == null
                ? 0
                : _playbackController.LastFrameIndex + 1;

        internal string FailureMessage { get; private set; } = string.Empty;

        internal NativeSkinningCorrectionPreprocessResult Result { get; private set; }

        internal static bool TryAttach(
            GameObject host,
            Animator animator,
            HumanoidMotionPlaybackController playbackController,
            out NativeSkinningCorrectionPlaybackDriver driver,
            out string errorMessage)
        {
            driver = null;
            errorMessage = string.Empty;
            if (host == null ||
                animator == null ||
                playbackController == null ||
                !playbackController.IsPrepared)
            {
                errorMessage = "Native 보정 표시기를 구성할 재생 대상이 없습니다.";
                return false;
            }

            try
            {
                NativeSkinningArmSurfaceSelection[] selections =
                    NativeSkinningArmSurfaceSelector.FindAll(animator);
                if (selections.Length == 0)
                {
                    return true;
                }
                var contracts = new List<NativeSkinningSurfaceContract>(
                    selections.Length);
                foreach (NativeSkinningArmSurfaceSelection selection in selections)
                {
                    if (!NativeSkinningSurfaceContractBuilder.TryBuild(
                            selection,
                            out NativeSkinningSurfaceContract contract))
                    {
                        errorMessage = "팔 표면의 Native 보정 계약을 만들지 못했습니다.";
                        return false;
                    }
                    contracts.Add(contract);
                }

                driver = host.GetComponent<NativeSkinningCorrectionPlaybackDriver>();
                if (driver == null)
                {
                    driver = host.AddComponent<NativeSkinningCorrectionPlaybackDriver>();
                    driver.hideFlags = HideFlags.DontSave;
                }
                driver.Configure(animator, playbackController, contracts.ToArray());
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"Native 보정 표시기 구성 중 예외가 발생했습니다: {exception.Message}";
                if (driver != null)
                {
                    driver.Release();
                    driver = null;
                }
                return false;
            }
        }

        internal bool BeginPreparation()
        {
            using var perfScope = PerfScope.Measure("NativeSkinningCorrectionPlaybackDriver.BeginPreparation");
            if (!IsConfigured || IsPreparing)
            {
                return false;
            }
            if (IsReady)
            {
                return true;
            }
            // 완료 직후 틱이 소비하기 전에 재요청되면 완료 처리부터 수행해 재계산을 막는다.
            if (_preprocessSession != null)
            {
                if (_preprocessSession.IsFaulted)
                {
                    return Fail(_preprocessSession.FailureMessage);
                }
                if (!_preprocessSession.IsComplete)
                {
                    return false;
                }
                CompletePreparation(_preprocessSession.Result);
                return IsReady;
            }

            ResetTransientState();
            // 디스크 캐시 히트 시 캡처·솔버를 전부 건너뛰고 결과만 복원한다.
            _prepWallWatch.Restart();
            if (TryRestoreResultFromCache(
                    out NativeSkinningCorrectionPreprocessResult cachedResult))
            {
                CompletePreparation(cachedResult);
                return IsReady;
            }
            _prepWallWatch.Reset();
            foreach (SkinnedMeshRenderer renderer in _contracts
                         .Select(contract => contract.Renderer)
                         .Distinct())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    return Fail("Native 보정 대상 Renderer가 사라졌습니다.");
                }
                var bakedMesh = new Mesh
                {
                    name = $"{renderer.sharedMesh.name} Native Skinning Preprocess",
                    hideFlags = HideFlags.HideAndDontSave
                };
                _bakedMeshes.Add(renderer, bakedMesh);
            }

            bool sessionCreated =
                NativeSkinningCorrectionPreprocessor
                    .IsFailureCollectionRequested()
                    ? NativeSkinningCorrectionPreprocessor
                        .TryCreateFailureCollectingSession(
                            TotalFrameCount,
                            _contracts,
                            ReadFrameVertices,
                            null,
                            out _preprocessSession)
                    : NativeSkinningCorrectionPreprocessor.TryCreateSession(
                            TotalFrameCount,
                            _contracts,
                            ReadFrameVertices,
                            null,
                            out _preprocessSession);
            if (!sessionCreated)
            {
                return Fail("Native 보정 사전 계산 session을 만들지 못했습니다.");
            }

            _prepWallWatch.Restart();
            _seekTicks = _bakeTicks = _boneSampleTicks = _solveTicks = 0;
            _solverIdleTicks = _solverBackloggedTicks = 0;
            CaptureAndHideRenderers();
            IsFaulted = false;
            FailureMessage = string.Empty;
            return true;
        }

        internal void CancelPreparation()
        {
            _preprocessSession?.Cancel();
            _preprocessSession = null;
            ClearPreparationPipeline();
            _playbackController?.Stop();
            RestoreRendererStates();
            DestroyBakedMeshes();
            IsReady = false;
            IsFaulted = false;
            FailureMessage = string.Empty;
            Result = null;
        }

        internal void Invalidate()
        {
            if (IsPreparing)
            {
                CancelPreparation();
                return;
            }
            DisposePresenters();
            IsReady = false;
            IsFaulted = false;
            FailureMessage = string.Empty;
            Result = null;
        }

        internal void Release()
        {
            _preprocessSession?.Cancel();
            _preprocessSession = null;
            ClearPreparationPipeline();
            RestoreRendererStates();
            DestroyBakedMeshes();
            DisposePresenters();
            _playbackController = null;
            _animator = null;
            _contracts = Array.Empty<NativeSkinningSurfaceContract>();
            // 드라이버 재사용 시 이전 조합의 캐시 식별자가 남지 않게 한다.
            _cacheMotionName = string.Empty;
            _runtimeCacheSourcePath = string.Empty;
            _cacheKeyHash = null;
            _resultFromCache = false;
#if UNITY_EDITOR
            _cacheMotionClip = null;
            _cacheSourceModelAsset = null;
#endif
            IsReady = false;
            IsFaulted = false;
            FailureMessage = string.Empty;
            Result = null;
        }

        private void Configure(
            Animator animator,
            HumanoidMotionPlaybackController playbackController,
            NativeSkinningSurfaceContract[] contracts)
        {
            Release();
            _animator = animator;
            _playbackController = playbackController;
            _contracts = contracts;
        }

        /// <summary>
        /// 빌드 환경의 캐시 식별자 — 소스 FBX 경로를 키 입력으로 주입한다.
        /// 주입되지 않으면 캐시 경로는 자동으로 비활성된다.
        /// </summary>
        internal void ConfigureRuntimeCacheIdentity(
            string sourceFilePath,
            string motionName)
        {
            _runtimeCacheSourcePath = sourceFilePath ?? string.Empty;
            _cacheMotionName = motionName ?? string.Empty;
        }

#if UNITY_EDITOR
        // 캐시 키 입력이 되는 식별자 — 파이프라인이 Attach 직후 주입한다.
        // 주입되지 않으면 캐시 경로는 자동으로 비활성된다.
        internal void ConfigureCacheIdentity(
            AnimationClip motionClip,
            string motionName,
            GameObject sourceModelAsset)
        {
            _cacheMotionClip = motionClip;
            _cacheMotionName = motionName ?? string.Empty;
            _cacheSourceModelAsset = sourceModelAsset;
        }
#endif

        private void LateUpdate()
        {
            // 워커가 세션을 완료하는 순간 IsFinished가 되어 IsPreparing이 꺼지므로,
            // 완료 감지(CompletePreparation)까지 세션 생존 동안 틱을 유지한다.
            if (_preprocessSession != null)
            {
                ProcessPreparationTick();
                return;
            }
            if (IsReady)
            {
                PresentCurrentFrame();
            }
        }

        // 매 틱: 완료 감지 → 스냅샷 캡처(메인스레드) → 병렬 솔버 배치 가동.
        private void ProcessPreparationTick()
        {
            if (_solveTask != null && _solveTask.IsCompleted)
            {
                _solveTask = null;
            }
            if (_preprocessSession.IsFaulted)
            {
                Fail(_preprocessSession.FailureMessage);
                return;
            }
            if (_preprocessSession.IsComplete)
            {
                CompletePreparation(_preprocessSession.Result);
                return;
            }

            // 큐가 비고 솔버도 없으면 캡처가, 큐가 차 있으면 솔버가 병목이다.
            if (_solveTask == null && _pendingPreparedFrames.Count == 0)
            {
                _solverIdleTicks++;
            }
            if (_pendingPreparedFrames.Count >= MaxPendingPreparedFrames)
            {
                _solverBackloggedTicks++;
            }

            // Seek + Bake + 본 변환은 Unity API라 메인스레드에서만 가능 — 틱당 상한까지 캡처.
            int capturedCount = 0;
            while (_nextCaptureFrame < TotalFrameCount &&
                   _pendingPreparedFrames.Count < MaxPendingPreparedFrames &&
                   capturedCount < MaxCapturesPerTick)
            {
                if (!CapturePreparedFrame(_nextCaptureFrame))
                {
                    return;
                }
                _nextCaptureFrame++;
                capturedCount++;
            }

            // 대기 중인 스냅샷을 배치로 워커 솔버에 넘긴다.
            if (_solveTask == null && _pendingPreparedFrames.Count > 0)
            {
                int batchCount = Math.Min(
                    SolveBatchFrameCount,
                    _pendingPreparedFrames.Count);
                var batch = new List<NativeSkinningPreparedFrame>(batchCount);
                for (int index = 0; index < batchCount; index++)
                {
                    batch.Add(_pendingPreparedFrames.Dequeue());
                }
                NativeSkinningCorrectionPreprocessSession session =
                    _preprocessSession;
                _solveTask = Task.Run(() =>
                {
                    var solveWatch = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        return session.TryProcessPreparedFrames(batch);
                    }
                    finally
                    {
                        System.Threading.Interlocked.Add(
                            ref _solveTicks, solveWatch.ElapsedTicks);
                    }
                });
            }
        }

        // 프레임 하나의 솔버 입력(스키닝 정점 + 본 변환)을 메인스레드에서 캡처함.
        private bool CapturePreparedFrame(int frameIndex)
        {
            var stageWatch = System.Diagnostics.Stopwatch.StartNew();
            if (!_playbackController.SeekFrame(frameIndex))
            {
                Fail($"Native 보정 준비 중 frame {frameIndex}을 재생하지 못했습니다.");
                return false;
            }
            _seekTicks += stageWatch.ElapsedTicks;

            var baselineVertices =
                new Dictionary<SkinnedMeshRenderer, Vector3[]>();
            var boneTransforms = new Dictionary<
                SkinnedMeshRenderer,
                NativeSkinningDualQuaternionTransform[]>();
            foreach (SkinnedMeshRenderer renderer in _contracts
                         .Select(contract => contract.Renderer)
                         .Distinct())
            {
                if (renderer == null || renderer.sharedMesh == null)
                {
                    Fail("사전 계산 중 Renderer 토폴로지가 변경됐습니다.");
                    return false;
                }
                if (!_bakedMeshes.TryGetValue(renderer, out Mesh bakedMesh))
                {
                    Fail("Native 보정 대상 Renderer가 사라졌습니다.");
                    return false;
                }
                stageWatch.Restart();
                renderer.BakeMesh(bakedMesh, false);
                baselineVertices[renderer] =
                    (Vector3[])bakedMesh.vertices.Clone();
                _bakeTicks += stageWatch.ElapsedTicks;
                // 솔버가 워커에서 돌므로 본 변환도 미리 표본화해 둔다.
                stageWatch.Restart();
                if (!NativeSkinningBoneTransformSampler.TrySample(
                        renderer,
                        out Matrix4x4[] skinningMatrices) ||
                    !NativeSkinningDualQuaternionVertexCalculator
                        .TryBuildTransforms(
                            skinningMatrices,
                            out NativeSkinningDualQuaternionTransform[]
                                transforms))
                {
                    Fail(
                        $"Native 보정 준비 중 frame {frameIndex}의 " +
                        "스키닝 본 자세를 변환하지 못했습니다.");
                    return false;
                }
                _boneSampleTicks += stageWatch.ElapsedTicks;
                boneTransforms[renderer] = transforms;
            }
            _pendingPreparedFrames.Enqueue(new NativeSkinningPreparedFrame(
                frameIndex,
                baselineVertices,
                boneTransforms));
            return true;
        }

        private void CompletePreparation(
            NativeSkinningCorrectionPreprocessResult result)
        {
            LogPrepTimingSummary(_preprocessSession);
            _playbackController.Stop();
            RestoreRendererStates();
            DestroyBakedMeshes();
            _preprocessSession = null;
            Result = result;

            if (result != null && result.CollectedFailureCount > 0)
            {
                Debug.LogWarning(
                    "[NativePrepDump] 수집 모드 결과가 부분 보정입니다. " +
                    $"건너뛴 실패 {result.CollectedFailureCount}건 — " +
                    "native-prep-failures-*.log를 확인하세요.",
                    this);
            }

            try
            {
                foreach (NativeSkinningRendererCorrection correction in
                         result.RendererCorrections)
                {
                    _presenters.Add(new NativeSkinningCorrectionPresenter(
                        correction.Renderer,
                        correction.Cache));
                }
                IsReady = true;
                TrySaveResultToCache(result);
                if (!PresentCurrentFrame())
                {
                    Fail("첫 Native 보정 프레임을 표시하지 못했습니다.");
                }
            }
            catch (Exception exception)
            {
                Fail($"Native 보정 표시 준비 중 예외가 발생했습니다: {exception.Message}");
            }
        }

        // 준비 병목 분해를 한 줄 로그로 남긴다 — 캡처(메인)와 솔버(워커)를 구분하는 게 목적.
        private void LogPrepTimingSummary(
            NativeSkinningCorrectionPreprocessSession session)
        {
            _prepWallWatch.Stop();
            double frequency = System.Diagnostics.Stopwatch.Frequency;
            Debug.Log(
                "[NativePrepTiming] " +
                $"wall={_prepWallWatch.ElapsedMilliseconds}ms " +
                $"seek={_seekTicks * 1000.0 / frequency:F0}ms " +
                $"bake={_bakeTicks * 1000.0 / frequency:F0}ms " +
                $"boneSample={_boneSampleTicks * 1000.0 / frequency:F0}ms " +
                $"solve={_solveTicks * 1000.0 / frequency:F0}ms " +
                $"solverIdleTicks={_solverIdleTicks} " +
                $"backloggedTicks={_solverBackloggedTicks} " +
                $"frames={TotalFrameCount} " +
                (session?.DescribeSolveTiming() ?? "solveTiming=n/a"),
                this);
        }

        private bool PresentCurrentFrame()
        {
            int frameIndex = _playbackController.CurrentFrameIndex;
            foreach (NativeSkinningCorrectionPresenter presenter in _presenters)
            {
                if (!presenter.TryPresentFrame(frameIndex, out _))
                {
                    return false;
                }
            }
            return true;
        }

        private Vector3[] ReadFrameVertices(
            int frameIndex,
            SkinnedMeshRenderer renderer)
        {
            if (!_bakedMeshes.TryGetValue(renderer, out Mesh bakedMesh))
            {
                return null;
            }
            renderer.BakeMesh(bakedMesh, false);
            return bakedMesh.vertices;
        }

        // 캐시 복원 시도 — 실패 사유는 전부 미스로 처리해 기존 계산 경로로 넘긴다.
        private bool TryRestoreResultFromCache(
            out NativeSkinningCorrectionPreprocessResult result)
        {
            result = null;
            _resultFromCache = false;
            _cacheKeyHash = null;

            Debug.Log("[NativePrepCache] 캐시 조회를 시작합니다.", this);
            // 수집 모드 실행이 수동 재계산 플래그를 소비하지 않도록 먼저 확인한다.
            if (NativeSkinningCorrectionPreprocessor.IsFailureCollectionRequested())
            {
                return false;
            }
#if UNITY_EDITOR
            if (ForceCacheRecalculate)
            {
                ForceCacheRecalculate = false;
                Debug.Log(
                    "[NativePrepCache] 수동 재계산 요청 — 캐시 로드를 건너뜁니다.",
                    this);
                // 강제 재계산이어도 결과는 다시 저장해야 하므로 키는 계산한다.
                ComputeCacheKey();
                return false;
            }
#endif
            if (!ComputeCacheKey())
            {
                return false;
            }

            string path = NativeSkinningCorrectionCacheFileStore.GetCachePath(
                _cacheKeyHash);
            if (!NativeSkinningCorrectionCacheFileStore.TryLoad(
                    path,
                    _cacheKeyHash,
                    out NativeSkinningCorrectionCacheDocument document,
                    out string loadError))
            {
                Debug.Log(
                    $"[NativePrepCache] 캐시 미스 — {loadError}", this);
                return false;
            }
            // 복원 경로의 어떤 예외도 미스로 내려 기존 계산 경로를 보존한다.
            NativeSkinningRendererCorrection[] corrections;
            try
            {
                if (!TryBindDocument(document, out corrections,
                        out string bindError))
                {
                    Debug.Log(
                        "[NativePrepCache] 캐시 렌더러 재바인딩 실패 — " +
                        $"재계산합니다: {bindError}",
                        this);
                    return false;
                }

                result = new NativeSkinningCorrectionPreprocessResult(
                    document.FrameCount,
                    document.CorrectedFrameCount,
                    document.FallbackFrameCount,
                    document.CorrectionEntryCount,
                    corrections);
            }
            catch (Exception exception)
            {
                Debug.Log(
                    $"[NativePrepCache] 캐시 복원 중 예외 — 재계산합니다: " +
                    $"{exception.Message}",
                    this);
                result = null;
                return false;
            }

            _resultFromCache = true;
            Debug.Log(
                $"[NativePrepCache] 캐시 히트 — 렌더러 {corrections.Length}개, " +
                $"보정 프레임 {document.CorrectedFrameCount}개를 복원했습니다.",
                this);
            return true;
        }

        private bool ComputeCacheKey()
        {
            if (_cacheKeyHash != null)
            {
                return true;
            }
            if (_animator == null ||
                _playbackController == null ||
                _contracts.Length == 0)
            {
                Debug.Log(
                    "[NativePrepCache] 캐시 식별자가 주입되지 않아 " +
                    "캐시를 사용하지 않습니다.",
                    this);
                return false;
            }
#if UNITY_EDITOR
            if (_cacheMotionClip == null)
            {
                Debug.Log(
                    "[NativePrepCache] 캐시 식별자가 주입되지 않아 " +
                    "캐시를 사용하지 않습니다.",
                    this);
                return false;
            }
            bool computed = NativeSkinningCorrectionCacheKey.TryCompute(
                _cacheMotionClip,
                _cacheMotionName,
                TotalFrameCount,
                _playbackController.ClipFrameRate,
                _cacheSourceModelAsset,
                _animator,
                _contracts,
                out _cacheKeyHash,
                out string keyError);
#else
            if (string.IsNullOrEmpty(_runtimeCacheSourcePath))
            {
                Debug.Log(
                    "[NativePrepCache] 캐시 식별자가 주입되지 않아 " +
                    "캐시를 사용하지 않습니다.",
                    this);
                return false;
            }
            bool computed = NativeSkinningCorrectionCacheKey.TryComputeRuntime(
                _runtimeCacheSourcePath,
                _cacheMotionName,
                TotalFrameCount,
                _playbackController.ClipFrameRate,
                _animator,
                _contracts,
                out _cacheKeyHash,
                out string keyError);
#endif
            if (!computed)
            {
                Debug.LogWarning(
                    $"[NativePrepCache] 캐시 키 계산 실패 — {keyError}", this);
                return false;
            }
            return true;
        }

        // 캐시 문서·바인딩이 쓰는 메시 식별자 — 에디터는 에셋 id,
        // 빌드는 정점 내용 해시로 같은 메시를 식별한다.
        private static string GetMeshCacheIdentity(Mesh mesh)
        {
#if UNITY_EDITOR
            return NativeSkinningCorrectionCacheKey.GetAssetIdentity(mesh);
#else
            return NativeSkinningCorrectionCacheKey.GetRuntimeMeshIdentity(mesh);
#endif
        }

        // 캐시 문서의 렌더러 항목을 현재 계약의 Renderer에 재바인딩한다.
        private bool TryBindDocument(
            NativeSkinningCorrectionCacheDocument document,
            out NativeSkinningRendererCorrection[] corrections,
            out string failReason)
        {
            corrections = null;
            failReason = null;
            if (document.FrameCount != TotalFrameCount)
            {
                failReason =
                    $"프레임 수 불일치 문서={document.FrameCount} 현재={TotalFrameCount}";
                return false;
            }

            // 경로→렌더러 1:1 맵 — 씬 쪽 경로 충돌(모호)은 미스로 처리한다.
            // 문서는 보정 프레임이 생긴 렌더러만 담으므로 부분 집합이 정상이다 —
            // 계약 렌더러 집합 자체는 캐시 키에 이미 포함돼 다른 조합은 파일이
            // 열리지 않는다.
            var byPath = new Dictionary<string, SkinnedMeshRenderer>(
                StringComparer.Ordinal);
            foreach (SkinnedMeshRenderer candidate in _contracts
                         .Select(contract => contract.Renderer)
                         .Where(renderer => renderer != null)
                         .Distinct())
            {
                string path = NativeSkinningCorrectionCacheKey
                    .GetRendererPath(_animator, candidate);
                if (candidate.sharedMesh == null ||
                    string.IsNullOrEmpty(path) ||
                    byPath.ContainsKey(path))
                {
                    failReason =
                        $"씬 렌더러 경로 구성 실패 path='{path}' mesh={(candidate.sharedMesh == null ? "null" : "ok")} 중복={byPath.ContainsKey(path)}";
                    return false;
                }
                byPath.Add(path, candidate);
            }

            var bound = new List<NativeSkinningRendererCorrection>(
                document.Renderers.Length);
            foreach (NativeSkinningCorrectionCacheRendererEntry entry in
                     document.Renderers)
            {
                if (!byPath.TryGetValue(entry.RendererPath,
                        out SkinnedMeshRenderer renderer))
                {
                    failReason = $"문서 경로 '{entry.RendererPath}'에 대응하는 씬 렌더러 없음";
                    return false;
                }
                if (renderer.sharedMesh.vertexCount != entry.VertexCount ||
                    GetMeshCacheIdentity(renderer.sharedMesh) !=
                        entry.MeshAssetId)
                {
                    failReason =
                        $"메시 식별 불일치 path='{entry.RendererPath}' 정점={renderer.sharedMesh.vertexCount}/{entry.VertexCount}";
                    return false;
                }

                var frames = new NativeSkinningCorrectionFrame[
                    entry.Frames.Length];
                for (int index = 0; index < entry.Frames.Length; index++)
                {
                    NativeSkinningCorrectionCacheFrameData frame =
                        entry.Frames[index];
                    var items = new NativeSkinningVertexCorrection[
                        frame.VertexIndices.Length];
                    for (int item = 0; item < items.Length; item++)
                    {
                        items[item] = new NativeSkinningVertexCorrection(
                            frame.VertexIndices[item],
                            frame.Deltas[item]);
                    }
                    frames[index] = new NativeSkinningCorrectionFrame(
                        frame.FrameIndex,
                        items);
                }

                bound.Add(new NativeSkinningRendererCorrection(
                    renderer,
                    new NativeSkinningCorrectionCache(
                        entry.VertexCount,
                        frames)));
            }

            corrections = bound.ToArray();
            return true;
        }

        // 계산 결과를 캐시 파일로 기록 — 부분 결과·수집 모드·캐시 재사용분은 제외.
        private void TrySaveResultToCache(
            NativeSkinningCorrectionPreprocessResult result)
        {
            try
            {
                if (_resultFromCache ||
                    result == null ||
                    result.CollectedFailureCount > 0 ||
                    NativeSkinningCorrectionPreprocessor
                        .IsFailureCollectionRequested() ||
                    !ComputeCacheKey())
                {
                    return;
                }

                var document = new NativeSkinningCorrectionCacheDocument
                {
                    FrameCount = result.FrameCount,
                    CorrectedFrameCount = result.CorrectedFrameCount,
                    FallbackFrameCount = result.FallbackFrameCount,
                    CorrectionEntryCount = result.CorrectionEntryCount,
                    Renderers = result.RendererCorrections
                        .Select(BuildRendererEntry)
                        .ToArray()
                };
                string path = NativeSkinningCorrectionCacheFileStore
                    .GetCachePath(_cacheKeyHash);
                if (NativeSkinningCorrectionCacheFileStore.TrySave(
                        path,
                        _cacheKeyHash,
                        document,
                        out string errorMessage))
                {
                    Debug.Log(
                        $"[NativePrepCache] 보정 캐시 저장: {path}", this);
                }
                else
                {
                    Debug.LogWarning(
                        $"[NativePrepCache] 보정 캐시 저장 실패: {errorMessage}",
                        this);
                }
            }
            catch (Exception exception)
            {
                // 캐시 기록 실패는 준비 결과를 실패로 뒤집지 않는다.
                Debug.LogWarning(
                    $"[NativePrepCache] 보정 캐시 저장 중 예외: {exception.Message}",
                    this);
            }
        }

        private NativeSkinningCorrectionCacheRendererEntry BuildRendererEntry(
            NativeSkinningRendererCorrection correction)
        {
            return new NativeSkinningCorrectionCacheRendererEntry
            {
                RendererPath =
                    NativeSkinningCorrectionCacheKey.GetRendererPath(
                        _animator, correction.Renderer),
                MeshAssetId =
                    GetMeshCacheIdentity(correction.Renderer.sharedMesh),
                VertexCount = correction.Cache.VertexCount,
                Frames = correction.Cache.ExportFrames()
                    .Select(frame =>
                    {
                        NativeSkinningVertexCorrection[] items =
                            frame.ExportCorrections();
                        return new NativeSkinningCorrectionCacheFrameData
                        {
                            FrameIndex = frame.FrameIndex,
                            VertexIndices = items
                                .Select(item => item.VertexIndex)
                                .ToArray(),
                            Deltas = items
                                .Select(item => item.Delta)
                                .ToArray()
                        };
                    })
                    .ToArray()
            };
        }

        private void CaptureAndHideRenderers()
        {
            _hiddenRenderers = _animator.GetComponentsInChildren<Renderer>(true);
            _rendererEnabledStates = _hiddenRenderers
                .Select(renderer => renderer.enabled)
                .ToArray();
            for (int index = 0; index < _hiddenRenderers.Length; index++)
            {
                _hiddenRenderers[index].enabled = false;
            }
            _hasCapturedRendererStates = true;
        }

        private void RestoreRendererStates()
        {
            if (!_hasCapturedRendererStates)
            {
                return;
            }
            for (int index = 0; index < _hiddenRenderers.Length; index++)
            {
                if (_hiddenRenderers[index] != null)
                {
                    _hiddenRenderers[index].enabled = _rendererEnabledStates[index];
                }
            }
            _hiddenRenderers = Array.Empty<Renderer>();
            _rendererEnabledStates = Array.Empty<bool>();
            _hasCapturedRendererStates = false;
        }

        private bool Fail(string message)
        {
            LogPrepTimingSummary(_preprocessSession);
            _preprocessSession?.Cancel();
            _preprocessSession = null;
            ClearPreparationPipeline();
            _playbackController?.Stop();
            RestoreRendererStates();
            DestroyBakedMeshes();
            DisposePresenters();
            IsReady = false;
            IsFaulted = true;
            FailureMessage = string.IsNullOrWhiteSpace(message)
                ? "Native 보정 준비에 실패했습니다."
                : message;
            Result = null;
            return false;
        }

        private void ResetTransientState()
        {
            _preprocessSession?.Cancel();
            _preprocessSession = null;
            ClearPreparationPipeline();
            RestoreRendererStates();
            DestroyBakedMeshes();
            DisposePresenters();
            IsReady = false;
            IsFaulted = false;
            FailureMessage = string.Empty;
            Result = null;
        }

        // 파이프라인의 순서 상태를 비운다. 진행 중인 솔버 작업은 세션 취소로 종료된다.
        private void ClearPreparationPipeline()
        {
            _nextCaptureFrame = 0;
            _pendingPreparedFrames.Clear();
            _solveTask = null;
        }

        private void DestroyBakedMeshes()
        {
            foreach (Mesh mesh in _bakedMeshes.Values)
            {
                DestroyOwnedObject(mesh);
            }
            _bakedMeshes.Clear();
        }

        private void DisposePresenters()
        {
            foreach (NativeSkinningCorrectionPresenter presenter in _presenters)
            {
                presenter.Dispose();
            }
            _presenters.Clear();
        }

        private void OnDestroy()
        {
            Release();
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
                DestroyImmediate(target);
                return;
            }
#endif
            Destroy(target);
        }
    }
}
