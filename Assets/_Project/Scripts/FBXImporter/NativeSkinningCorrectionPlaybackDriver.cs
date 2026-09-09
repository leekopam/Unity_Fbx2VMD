using System;
using System.Collections.Generic;
using System.Linq;
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
            if (!IsConfigured || IsPreparing)
            {
                return false;
            }
            if (IsReady)
            {
                return true;
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

            if (!NativeSkinningCorrectionPreprocessor.TryCreateSession(
                    TotalFrameCount,
                    _contracts,
                    ReadFrameVertices,
                    null,
                    out _preprocessSession))
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
            if (IsPreparing)
            {
                ProcessNextPreparationFrame();
                return;
            }
            if (IsReady)
            {
                PresentCurrentFrame();
            }
        }

        private void ProcessNextPreparationFrame()
        {
            int frameIndex = _preprocessSession.ProcessedFrameCount;
            if (!_playbackController.SeekFrame(frameIndex))
            {
                Fail($"Native 보정 준비 중 frame {frameIndex}을 재생하지 못했습니다.");
                return;
            }
            if (!_preprocessSession.TryProcessNextFrame())
            {
                Fail(_preprocessSession.FailureMessage);
                return;
            }
            if (_preprocessSession.IsComplete)
            {
                CompletePreparation(_preprocessSession.Result);
            }
        }

        private void CompletePreparation(
            NativeSkinningCorrectionPreprocessResult result)
        {
            _playbackController.Stop();
            RestoreRendererStates();
            DestroyBakedMeshes();
            _preprocessSession = null;
            Result = result;

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
            RestoreRendererStates();
            DestroyBakedMeshes();
            DisposePresenters();
            IsReady = false;
            IsFaulted = false;
            FailureMessage = string.Empty;
            Result = null;
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
