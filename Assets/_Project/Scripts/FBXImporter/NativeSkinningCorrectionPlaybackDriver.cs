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
                _solveTask = Task.Run(
                    () => session.TryProcessPreparedFrames(batch));
            }
        }

        // 프레임 하나의 솔버 입력(스키닝 정점 + 본 변환)을 메인스레드에서 캡처함.
        private bool CapturePreparedFrame(int frameIndex)
        {
            if (!_playbackController.SeekFrame(frameIndex))
            {
                Fail($"Native 보정 준비 중 frame {frameIndex}을 재생하지 못했습니다.");
                return false;
            }

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
                renderer.BakeMesh(bakedMesh, false);
                baselineVertices[renderer] =
                    (Vector3[])bakedMesh.vertices.Clone();
                // 솔버가 워커에서 돌므로 본 변환도 미리 표본화해 둔다.
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
