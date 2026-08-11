using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
        private sealed class LegacyAnimationDriver
        {
            private readonly PoseSpaceRetargeter _retargeter;
            private Animation _legacyAnim;
            private AnimationClip _ownedLegacyClip;
            private bool _addedLegacyAnimationComponent;
            private bool _ghostAnimatorWasEnabled;
            private bool _hasPreviousLegacyAnimationTime;
            private float _previousLegacyAnimationTime;
            private float _lastLegacyAnimationStep = float.NaN;
            private float _maxLegacyAnimationStep;
            private int _legacyAnimationStepSpikeCount;
            private bool _legacyAnimationStepSpikeThisFrame;

            public LegacyAnimationDriver(PoseSpaceRetargeter retargeter)
            {
                _retargeter = retargeter;
            }

            public float LastLegacyAnimationStep => _lastLegacyAnimationStep;
            public float MaxLegacyAnimationStep => _maxLegacyAnimationStep;
            public int LegacyAnimationStepSpikeCount => _legacyAnimationStepSpikeCount;
            public bool LegacyAnimationStepSpikeThisFrame => _legacyAnimationStepSpikeThisFrame;
            public Animation Animation => _legacyAnim;
            public bool IsPlaying => _legacyAnim != null && _legacyAnim.isPlaying;

            public void Initialize(GameObject ghostRoot, Animator ghostAnimator, AnimationClip clip)
            {
                _ghostAnimatorWasEnabled = ghostAnimator != null && ghostAnimator.enabled;
                if (ghostAnimator != null) ghostAnimator.enabled = false;

                _legacyAnim = ghostRoot.GetComponent<Animation>();
                _addedLegacyAnimationComponent = _legacyAnim == null;
                if (_legacyAnim == null) _legacyAnim = ghostRoot.AddComponent<Animation>();
                _legacyAnim.Stop();

                AnimationClip legacyClip = clip;
                if (legacyClip != null && !legacyClip.legacy)
                {
                    string legacyClipName = legacyClip.name;
                    _ownedLegacyClip = UnityEngine.Object.Instantiate(legacyClip);
                    _ownedLegacyClip.name = legacyClipName;
                    _ownedLegacyClip.legacy = true;
                    legacyClip = _ownedLegacyClip;
                }

                RemoveLegacyAnimationClipStateIfPresent(_legacyAnim, LegacyClipStateName);
                _legacyAnim.AddClip(legacyClip, LegacyClipStateName);
                _legacyAnim.clip = legacyClip;
                AnimationState state = _legacyAnim[LegacyClipStateName];
                if (state != null)
                {
                    state.wrapMode = WrapMode.Once;
                    state.time = 0f;
                }

                _legacyAnim.Play(LegacyClipStateName);
            }

            public void Cleanup(Animator ghostAnimator)
            {
                if (_legacyAnim != null)
                {
                    _legacyAnim.Stop();
                    RemoveLegacyAnimationClipStateIfPresent(_legacyAnim, LegacyClipStateName);
                }

                if (_ownedLegacyClip != null)
                {
                    UnityEngine.Object.Destroy(_ownedLegacyClip);
                    _ownedLegacyClip = null;
                }

                if (_addedLegacyAnimationComponent && _legacyAnim != null)
                {
                    UnityEngine.Object.Destroy(_legacyAnim);
                }

                if (ghostAnimator != null)
                {
                    ghostAnimator.enabled = _ghostAnimatorWasEnabled;
                }

                _legacyAnim = null;
                _addedLegacyAnimationComponent = false;
                _ghostAnimatorWasEnabled = false;
            }

            public void ResetPlaybackStabilityMetrics()
            {
                _hasPreviousLegacyAnimationTime = false;
                _previousLegacyAnimationTime = 0f;
                _lastLegacyAnimationStep = float.NaN;
                _maxLegacyAnimationStep = 0f;
                _legacyAnimationStepSpikeCount = 0;
                _legacyAnimationStepSpikeThisFrame = false;
            }

            public bool PrepareRecordingStartPose(float startTimeSeconds, float playbackSpeed, bool holdPose)
            {
                if (!TryPrepareRecordingStartPose(_legacyAnim, startTimeSeconds, playbackSpeed, holdPose))
                {
                    return false;
                }

                _hasPreviousLegacyAnimationTime = false;
                _previousLegacyAnimationTime = Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, _legacyAnim[LegacyClipStateName].length));
                _lastLegacyAnimationStep = 0f;
                _legacyAnimationStepSpikeThisFrame = false;
                return true;
            }

            public bool UpdateLegacyAnimationVisualStep()
            {
                _legacyAnimationStepSpikeThisFrame = false;

                if (_legacyAnim == null)
                {
                    return false;
                }

                AnimationState state = _legacyAnim[LegacyClipStateName];
                if (state == null)
                {
                    return false;
                }

                float length = Mathf.Max(0f, state.length);
                float currentTime = Mathf.Clamp(state.time, 0f, length);
                if (!_hasPreviousLegacyAnimationTime)
                {
                    _previousLegacyAnimationTime = currentTime;
                    _hasPreviousLegacyAnimationTime = true;
                    _lastLegacyAnimationStep = 0f;
                    return false;
                }

                if (TryCalculateManualLegacyAnimationTime(
                    currentTime,
                    _previousLegacyAnimationTime,
                    length,
                    state.speed,
                    Time.deltaTime,
                    Application.isPlaying,
                    out float manualAnimationTime))
                {
                    currentTime = manualAnimationTime;
                    state.enabled = true;
                    state.time = currentTime;
                    _legacyAnim.Sample();
                }

                float maxStep = 1f / Mathf.Clamp(_retargeter.legacyAnimationVisualFrameRate, 15f, 120f);
                if (TryClampLegacyAnimationEndWrap(
                    currentTime,
                    _previousLegacyAnimationTime,
                    length,
                    maxStep,
                    out float clampedEndTime))
                {
                    currentTime = clampedEndTime;
                    state.enabled = true;
                    state.time = currentTime;
                    state.speed = 0f;
                    _legacyAnim.Sample();
                }

                if (currentTime + 0.0001f < _previousLegacyAnimationTime)
                {
                    _previousLegacyAnimationTime = currentTime;
                    _lastLegacyAnimationStep = 0f;
                    return true;
                }

                float step = currentTime - _previousLegacyAnimationTime;
                _lastLegacyAnimationStep = step;
                _maxLegacyAnimationStep = Mathf.Max(_maxLegacyAnimationStep, step);
                float spikeTolerance = Mathf.Max(0.001f, maxStep * 0.05f);
                if (step > maxStep + spikeTolerance)
                {
                    _legacyAnimationStepSpikeThisFrame = true;
                    _legacyAnimationStepSpikeCount++;
                    if (_retargeter.clampLegacyAnimationVisualStep)
                    {
                        currentTime = Mathf.Min(_previousLegacyAnimationTime + maxStep, length);
                        state.time = currentTime;
                        _legacyAnim.Sample();
                        step = currentTime - _previousLegacyAnimationTime;
                        _lastLegacyAnimationStep = step;
                    }
                }

                _previousLegacyAnimationTime = currentTime;
                return false;
            }

            private static bool TryCalculateManualLegacyAnimationTime(
                float currentTime,
                float previousTime,
                float length,
                float playbackSpeed,
                float deltaTime,
                bool isPlaying,
                out float manualAnimationTime)
            {
                manualAnimationTime = currentTime;

                if (!isPlaying ||
                    length <= 0f ||
                    currentTime > previousTime + 0.0001f ||
                    currentTime + 0.0001f < previousTime ||
                    previousTime >= length - 0.0001f)
                {
                    return false;
                }

                float effectivePlaybackSpeed = Mathf.Approximately(playbackSpeed, 0f)
                    ? 1f
                    : Mathf.Abs(playbackSpeed);
                float manualStep = Mathf.Max(0f, deltaTime * effectivePlaybackSpeed);
                if (manualStep <= 0f)
                {
                    return false;
                }

                manualAnimationTime = Mathf.Min(previousTime + manualStep, length);
                return true;
            }

            private static bool TryClampLegacyAnimationEndWrap(
                float currentTime,
                float previousTime,
                float length,
                float maxStep,
                out float clampedTime)
            {
                clampedTime = currentTime;

                if (!IsFinite(currentTime) ||
                    !IsFinite(previousTime) ||
                    !IsFinite(length) ||
                    !IsFinite(maxStep) ||
                    length <= 0f ||
                    maxStep <= 0f ||
                    currentTime + 0.0001f >= previousTime)
                {
                    return false;
                }

                float endWindow = Mathf.Max(0.01f, maxStep * 2f + 0.005f);
                if (previousTime < length - endWindow || currentTime > endWindow)
                {
                    return false;
                }

                clampedTime = length;
                return true;
            }
        }
    }
}
