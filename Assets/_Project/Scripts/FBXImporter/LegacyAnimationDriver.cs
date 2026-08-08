using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
        private sealed class LegacyAnimationDriver
        {
            private readonly PoseSpaceRetargeter _retargeter;

            public LegacyAnimationDriver(PoseSpaceRetargeter retargeter)
            {
                _retargeter = retargeter;
            }

            public void UpdateLegacyAnimationVisualStep()
            {
                _retargeter._legacyAnimationStepSpikeThisFrame = false;

                if (_retargeter._legacyAnim == null)
                {
                    return;
                }

                AnimationState state = _retargeter._legacyAnim[LegacyClipStateName];
                if (state == null)
                {
                    return;
                }

                float length = Mathf.Max(0f, state.length);
                float currentTime = Mathf.Clamp(state.time, 0f, length);
                if (!_retargeter._hasPreviousLegacyAnimationTime)
                {
                    _retargeter._previousLegacyAnimationTime = currentTime;
                    _retargeter._hasPreviousLegacyAnimationTime = true;
                    _retargeter._lastLegacyAnimationStep = 0f;
                    return;
                }

                if (TryCalculateManualLegacyAnimationTime(
                    currentTime,
                    _retargeter._previousLegacyAnimationTime,
                    length,
                    state.speed,
                    Time.deltaTime,
                    Application.isPlaying,
                    out float manualAnimationTime))
                {
                    currentTime = manualAnimationTime;
                    state.enabled = true;
                    state.time = currentTime;
                    _retargeter._legacyAnim.Sample();
                }

                float maxStep = 1f / Mathf.Clamp(_retargeter.legacyAnimationVisualFrameRate, 15f, 120f);
                if (TryClampLegacyAnimationEndWrap(
                    currentTime,
                    _retargeter._previousLegacyAnimationTime,
                    length,
                    maxStep,
                    out float clampedEndTime))
                {
                    currentTime = clampedEndTime;
                    state.enabled = true;
                    state.time = currentTime;
                    state.speed = 0f;
                    _retargeter._legacyAnim.Sample();
                }

                if (currentTime + 0.0001f < _retargeter._previousLegacyAnimationTime)
                {
                    _retargeter._previousLegacyAnimationTime = currentTime;
                    _retargeter._lastLegacyAnimationStep = 0f;
                    _retargeter.ResetVisualPoseHistory();
                    return;
                }

                float step = currentTime - _retargeter._previousLegacyAnimationTime;
                _retargeter._lastLegacyAnimationStep = step;
                _retargeter._maxLegacyAnimationStep = Mathf.Max(_retargeter._maxLegacyAnimationStep, step);
                float spikeTolerance = Mathf.Max(0.001f, maxStep * 0.05f);
                if (step > maxStep + spikeTolerance)
                {
                    _retargeter._legacyAnimationStepSpikeThisFrame = true;
                    _retargeter._legacyAnimationStepSpikeCount++;
                    if (_retargeter.clampLegacyAnimationVisualStep)
                    {
                        currentTime = Mathf.Min(_retargeter._previousLegacyAnimationTime + maxStep, length);
                        state.time = currentTime;
                        _retargeter._legacyAnim.Sample();
                        step = currentTime - _retargeter._previousLegacyAnimationTime;
                        _retargeter._lastLegacyAnimationStep = step;
                    }
                }

                _retargeter._previousLegacyAnimationTime = currentTime;
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
