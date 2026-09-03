using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    // NotThreadSafe: Unity Animation 객체를 메인 스레드에서만 소유하고 호출함.
    internal sealed class LegacyAnimationDriver
    {
        private const string LegacyClipStateName = "__PoseSpaceRetargeter_GhostClip";

        private Animation _animation;
        private Animator _ghostAnimator;
        private bool _ghostAnimatorWasEnabled;
        private bool _addedAnimationComponent;
        private AnimationClip _ownedClip;
        private bool _hasPreviousTime;
        private float _previousTime;
        private float _lastStep = float.NaN;
        private float _maxStep;
        private int _stepSpikeCount;
        private bool _stepSpikeThisFrame;

        public float CurrentTime
        {
            get
            {
                AnimationState state = GetState();
                return state == null ? 0f : Mathf.Clamp(state.time, 0f, Mathf.Max(0f, state.length));
            }
        }

        public bool IsPlaying => _animation != null && _animation.isPlaying;
        public float CurrentClipLength
        {
            get
            {
                AnimationState state = GetState();
                return state != null
                    ? Mathf.Max(0f, state.length)
                    : Mathf.Max(0f, _animation != null && _animation.clip != null
                        ? _animation.clip.length
                        : 0f);
            }
        }
        public string CurrentClipName =>
            _animation != null && _animation.clip != null
                ? _animation.clip.name
                : string.Empty;
        public bool StepSpikeThisFrame => _stepSpikeThisFrame;
        public float LastStep => _lastStep;
        public float MaxStep => _maxStep;
        public int StepSpikeCount => _stepSpikeCount;

        public void Initialize(GameObject ghostRoot, Animator ghostAnimator, AnimationClip clip)
        {
            Dispose();
            if (ghostRoot == null || clip == null)
            {
                return;
            }

            _ghostAnimator = ghostAnimator;
            _ghostAnimatorWasEnabled = _ghostAnimator != null && _ghostAnimator.enabled;
            if (_ghostAnimator != null)
            {
                _ghostAnimator.enabled = false;
            }

            _animation = ghostRoot.GetComponent<Animation>();
            _addedAnimationComponent = _animation == null;
            if (_animation == null)
            {
                _animation = ghostRoot.AddComponent<Animation>();
            }

            _animation.Stop();
            AnimationClip legacyClip = CreateLegacyClip(clip);
            RemoveClipStateIfPresent(_animation, LegacyClipStateName);
            _animation.AddClip(legacyClip, LegacyClipStateName);
            _animation.clip = legacyClip;

            AnimationState state = GetState();
            if (state != null)
            {
                state.wrapMode = WrapMode.Once;
                state.time = 0f;
            }

            _animation.Play(LegacyClipStateName);
        }

        /// <returns>true이면 이전 visual pose를 초기화해야 하는 역방향 시간 이동이 발생했음을 뜻함.</returns>
        public bool Tick(float deltaTime, bool isPlaying, bool clampStep, float frameRate)
        {
            _stepSpikeThisFrame = false;
            AnimationState state = GetState();
            if (state == null)
            {
                return false;
            }

            float length = Mathf.Max(0f, state.length);
            float currentTime = Mathf.Clamp(state.time, 0f, length);
            if (!_hasPreviousTime)
            {
                _previousTime = currentTime;
                _hasPreviousTime = true;
                _lastStep = 0f;
                return false;
            }

            if (TryCalculateManualLegacyAnimationTime(
                currentTime,
                _previousTime,
                length,
                state.speed,
                deltaTime,
                isPlaying,
                out float manualAnimationTime))
            {
                currentTime = manualAnimationTime;
                state.enabled = true;
                state.time = currentTime;
                _animation.Sample();
            }

            float maxStep = 1f / Mathf.Clamp(frameRate, 15f, 120f);
            if (TryClampLegacyAnimationEndWrap(
                currentTime,
                _previousTime,
                length,
                maxStep,
                out float clampedEndTime))
            {
                currentTime = clampedEndTime;
                state.enabled = true;
                state.time = currentTime;
                state.speed = 0f;
                _animation.Sample();
            }

            if (currentTime + 0.0001f < _previousTime)
            {
                _previousTime = currentTime;
                _lastStep = 0f;
                return true;
            }

            float step = currentTime - _previousTime;
            _lastStep = step;
            _maxStep = Mathf.Max(_maxStep, step);
            float spikeTolerance = Mathf.Max(0.001f, maxStep * 0.05f);
            if (step > maxStep + spikeTolerance)
            {
                _stepSpikeThisFrame = true;
                _stepSpikeCount++;
                if (clampStep)
                {
                    currentTime = Mathf.Min(_previousTime + maxStep, length);
                    state.time = currentTime;
                    _animation.Sample();
                    _lastStep = currentTime - _previousTime;
                }
            }

            _previousTime = currentTime;
            return false;
        }

        /// <returns>true이면 시작 포즈를 Sample했으며 false이면 Animation state가 없어 변경하지 않았음을 뜻함.</returns>
        public bool TryPrepareRecordingStartPose(float startTimeSeconds, float playbackSpeed, bool holdPose)
        {
            AnimationState state = GetState();
            if (state == null)
            {
                return false;
            }

            float sampleTime = Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, state.length));
            float safePlaybackSpeed = Mathf.Max(0.0001f, playbackSpeed);
            _animation.Play(LegacyClipStateName);
            state.enabled = true;
            state.wrapMode = WrapMode.Once;
            state.time = sampleTime;
            state.speed = holdPose ? 0f : safePlaybackSpeed;
            _animation.Sample();

            _hasPreviousTime = false;
            _previousTime = sampleTime;
            _lastStep = 0f;
            _stepSpikeThisFrame = false;
            return true;
        }

        public void ResetStabilityMetrics()
        {
            _hasPreviousTime = false;
            _previousTime = 0f;
            _lastStep = float.NaN;
            _maxStep = 0f;
            _stepSpikeCount = 0;
            _stepSpikeThisFrame = false;
        }

        public void Dispose()
        {
            if (_animation != null)
            {
                _animation.Stop();
                RemoveClipStateIfPresent(_animation, LegacyClipStateName);
            }

            if (_ownedClip != null)
            {
                DestroyOwnedObject(_ownedClip);
                _ownedClip = null;
            }

            if (_addedAnimationComponent && _animation != null)
            {
                DestroyOwnedObject(_animation);
            }

            if (_ghostAnimator != null)
            {
                _ghostAnimator.enabled = _ghostAnimatorWasEnabled;
            }

            _animation = null;
            _ghostAnimator = null;
            _addedAnimationComponent = false;
            _ghostAnimatorWasEnabled = false;
        }

        private AnimationClip CreateLegacyClip(AnimationClip clip)
        {
            if (clip.legacy)
            {
                return clip;
            }

            _ownedClip = UnityEngine.Object.Instantiate(clip);
            _ownedClip.name = clip.name;
            _ownedClip.legacy = true;
            return _ownedClip;
        }

        private AnimationState GetState()
        {
            return _animation == null ? null : _animation[LegacyClipStateName];
        }

        private static bool HasLegacyAnimationClipState(Animation legacyAnimation, string stateName)
        {
            return legacyAnimation != null &&
                !string.IsNullOrEmpty(stateName) &&
                legacyAnimation[stateName] != null;
        }

        private static void RemoveClipStateIfPresent(Animation legacyAnimation, string stateName)
        {
            if (HasLegacyAnimationClipState(legacyAnimation, stateName))
            {
                legacyAnimation.RemoveClip(stateName);
            }
        }

        private static void DestroyOwnedObject(UnityEngine.Object unityObject)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(unityObject);
                return;
            }

            UnityEngine.Object.DestroyImmediate(unityObject);
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
