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
        }

        public void Dispose()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_targetAnimator != null)
            {
                _targetAnimator.applyRootMotion = _originalApplyRootMotion;
                _targetAnimator.cullingMode = _originalCullingMode;
                _targetAnimator.runtimeAnimatorController = _originalAnimatorController;
            }

            _clipPlayable = default;
            _targetAnimator = null;
            _originalAnimatorController = null;
        }

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
