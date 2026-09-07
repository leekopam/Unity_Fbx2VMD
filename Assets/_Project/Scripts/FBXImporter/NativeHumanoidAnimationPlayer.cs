using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Unity Humanoid 클립을 별도 보정 경로 없이 Animator에 직접 재생함.
    /// </summary>
    internal sealed class NativeHumanoidAnimationPlayer : IDisposable
    {
        private const string PlayableOutputName = "Native Humanoid Animation";

        private PlayableGraph _graph;
        private AnimationClipPlayable _clipPlayable;
        private Animator _targetAnimator;
        private bool _originalApplyRootMotion;
        private AnimatorCullingMode _originalCullingMode;
        private RuntimeAnimatorController _originalAnimatorController;
#if UNITY_EDITOR
        private readonly EditorHumanoidRootTranslationSampler _rootTranslationSampler =
            new EditorHumanoidRootTranslationSampler();
        private Vector3 _rootPositionAnchor;
        private Quaternion _rootRotationAnchor;
        private bool _hasRootTranslationAnchor;
#endif

        internal bool IsInitialized => _graph.IsValid();

        internal bool IsFootIkEnabled =>
            IsInitialized && _clipPlayable.GetApplyFootIK();

        internal bool IsPlayableIkEnabled =>
            IsInitialized && _clipPlayable.GetApplyPlayableIK();

        internal void Initialize(Animator targetAnimator, AnimationClip clip)
        {
            ValidateTarget(targetAnimator);
            ValidateClip(clip);

            Dispose();

            _targetAnimator = targetAnimator;
            _originalApplyRootMotion = targetAnimator.applyRootMotion;
            _originalCullingMode = targetAnimator.cullingMode;
            _originalAnimatorController = targetAnimator.runtimeAnimatorController;
#if UNITY_EDITOR
            _rootPositionAnchor = targetAnimator.transform.position;
            _rootRotationAnchor = targetAnimator.transform.rotation;
            _hasRootTranslationAnchor = true;
            _rootTranslationSampler.Initialize(clip);
#endif

            try
            {
                targetAnimator.applyRootMotion = false;
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                targetAnimator.runtimeAnimatorController = null;

                _graph = PlayableGraph.Create(nameof(NativeHumanoidAnimationPlayer));
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

                _clipPlayable = AnimationClipPlayable.Create(_graph, clip);
                _clipPlayable.SetApplyFootIK(false);
                _clipPlayable.SetApplyPlayableIK(false);

                AnimationPlayableOutput output = AnimationPlayableOutput.Create(
                    _graph,
                    PlayableOutputName,
                    targetAnimator);
                output.SetSourcePlayable(_clipPlayable);
                _graph.Play();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void EvaluateAt(float timeSeconds)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException(
                    "Native Humanoid 재생기를 먼저 초기화해야 합니다.");
            }

            if (float.IsNaN(timeSeconds) || float.IsInfinity(timeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds));
            }

            AnimationClip clip = _clipPlayable.GetAnimationClip();
            double evaluationTime = Mathf.Clamp(timeSeconds, 0f, clip.length);
            _clipPlayable.SetTime(evaluationTime);
            _graph.Evaluate(0f);
            ApplyEditorRootTranslation((float)evaluationTime);
        }

        public void Dispose()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_targetAnimator != null)
            {
#if UNITY_EDITOR
                RestoreEditorRootTranslationAnchor();
#endif
                _targetAnimator.applyRootMotion = _originalApplyRootMotion;
                _targetAnimator.cullingMode = _originalCullingMode;
                _targetAnimator.runtimeAnimatorController = _originalAnimatorController;
            }

            _clipPlayable = default;
            _targetAnimator = null;
            _originalAnimatorController = null;
#if UNITY_EDITOR
            _rootTranslationSampler.Clear();
            _rootPositionAnchor = Vector3.zero;
            _rootRotationAnchor = Quaternion.identity;
            _hasRootTranslationAnchor = false;
#endif
        }

#if UNITY_EDITOR
        private void ApplyEditorRootTranslation(float timeSeconds)
        {
            if (!_hasRootTranslationAnchor ||
                _targetAnimator == null ||
                !_rootTranslationSampler.TryEvaluateClipSpaceOffset(
                    timeSeconds,
                    out Vector3 clipSpaceOffset))
            {
                return;
            }

            Vector3 expectedPosition =
                _rootPositionAnchor + _rootRotationAnchor * clipSpaceOffset;
            Vector3 currentPosition = _targetAnimator.transform.position;
            _targetAnimator.transform.position = new Vector3(
                expectedPosition.x,
                currentPosition.y,
                expectedPosition.z);
        }

        private void RestoreEditorRootTranslationAnchor()
        {
            if (!_hasRootTranslationAnchor || _targetAnimator == null)
            {
                return;
            }

            Vector3 currentPosition = _targetAnimator.transform.position;
            _targetAnimator.transform.position = new Vector3(
                _rootPositionAnchor.x,
                currentPosition.y,
                _rootPositionAnchor.z);
        }
#endif

        private static void ValidateTarget(Animator targetAnimator)
        {
            if (targetAnimator == null)
            {
                throw new ArgumentNullException(nameof(targetAnimator));
            }

            Avatar avatar = targetAnimator.avatar;
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                throw new InvalidOperationException(
                    "Unity Native Humanoid 재생에는 유효한 Humanoid Avatar가 필요합니다.");
            }
        }

        private static void ValidateClip(AnimationClip clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (!clip.humanMotion)
            {
                throw new InvalidOperationException(
                    "Unity Native Humanoid 재생에는 Humanoid AnimationClip이 필요합니다.");
            }
        }
    }
}
