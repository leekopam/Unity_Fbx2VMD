#if UNITY_EDITOR
using System;
using RootMotion.FinalIK;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 원본 접촉 궤적을 대상 양다리에 본 길이 변화 없이 적용함.
    /// </summary>
    internal sealed class EditorHumanoidFootContactStabilizer
    {
        private const float MinimumBendNormalSquaredMagnitude = 0.00000001f;
        private const float MinimumCorrectionSquaredMagnitude = 0.0000000001f;

        private readonly LegChain _leftLeg = new LegChain();
        private readonly LegChain _rightLeg = new LegChain();
        private Transform _targetRoot;
        private HumanoidFootContactPlan _plan;

        internal bool IsInitialized =>
            _targetRoot != null &&
            _plan != null &&
            _leftLeg.IsValid &&
            _rightLeg.IsValid;

        internal int LeftContactRunCount => _plan?.LeftContactRunCount ?? 0;

        internal int RightContactRunCount => _plan?.RightContactRunCount ?? 0;

        internal void Initialize(
            Animator targetAnimator,
            AnimationClip clip,
            Action<float> evaluateTarget,
            EditorHumanoidPoseReferencePlayer sourceReference)
        {
            if (targetAnimator == null)
            {
                throw new ArgumentNullException(nameof(targetAnimator));
            }

            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (evaluateTarget == null)
            {
                throw new ArgumentNullException(nameof(evaluateTarget));
            }

            if (sourceReference == null || !sourceReference.IsInitialized)
            {
                throw new InvalidOperationException(
                    "발 접촉 계획에는 초기화된 원본 Humanoid 기준이 필요합니다.");
            }

            Clear();
            _targetRoot = targetAnimator.transform;
            _leftLeg.Initialize(
                targetAnimator,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes);
            _rightLeg.Initialize(
                targetAnimator,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes);

            float frameRate = HumanoidMotionFrameCalculator.NormalizeFrameRate(
                clip.frameRate);
            int frameCount = Mathf.CeilToInt(clip.length * frameRate) + 1;
            var sourceSamples = new HumanoidFootContactSample[frameCount];
            var targetSamples = new HumanoidFootContactSample[frameCount];
            Quaternion sourceToTargetRotation = _targetRoot.rotation;

            try
            {
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float timeSeconds = Mathf.Min(frameIndex / frameRate, clip.length);
                    if (!sourceReference.TryEvaluateFootContactPointsAt(
                            timeSeconds,
                            out Vector3 sourceLeft,
                            out Vector3 sourceRight))
                    {
                        throw new InvalidOperationException(
                            "원본 Humanoid 발 접촉점을 샘플링하지 못했습니다.");
                    }

                    evaluateTarget(timeSeconds);
                    sourceSamples[frameIndex] = new HumanoidFootContactSample(
                        sourceLeft,
                        sourceRight);
                    targetSamples[frameIndex] = new HumanoidFootContactSample(
                        _leftLeg.CaptureContactPoint(),
                        _rightLeg.CaptureContactPoint());
                }

                _plan = HumanoidFootContactPlanner.Build(
                    sourceSamples,
                    targetSamples,
                    frameRate,
                    sourceReference.SourceHumanScale,
                    sourceToTargetRotation);
            }
            finally
            {
                evaluateTarget(0f);
            }
        }

        internal bool TryApply(float timeSeconds)
        {
            if (!IsInitialized)
            {
                return true;
            }

            if (!_plan.TryEvaluate(
                    timeSeconds,
                    out Vector3 leftRootSpaceCorrection,
                    out Vector3 rightRootSpaceCorrection))
            {
                return false;
            }

            Vector3 leftWorldCorrection =
                _targetRoot.TransformDirection(leftRootSpaceCorrection);
            Vector3 rightWorldCorrection =
                _targetRoot.TransformDirection(rightRootSpaceCorrection);
            if (!IsFinite(leftWorldCorrection) || !IsFinite(rightWorldCorrection))
            {
                return false;
            }

            _leftLeg.Apply(leftWorldCorrection);
            _rightLeg.Apply(rightWorldCorrection);
            return true;
        }

        internal void Clear()
        {
            _plan = null;
            _targetRoot = null;
            _leftLeg.Clear();
            _rightLeg.Clear();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private sealed class LegChain
        {
            private Transform _upperLeg;
            private Transform _lowerLeg;
            private Transform _foot;
            private Transform _toes;
            private Vector3 _fallbackBendNormal;

            internal bool IsValid =>
                _upperLeg != null && _lowerLeg != null && _foot != null;

            internal void Initialize(
                Animator animator,
                HumanBodyBones upperLeg,
                HumanBodyBones lowerLeg,
                HumanBodyBones foot,
                HumanBodyBones toes)
            {
                _upperLeg = animator.GetBoneTransform(upperLeg);
                _lowerLeg = animator.GetBoneTransform(lowerLeg);
                _foot = animator.GetBoneTransform(foot);
                _toes = animator.GetBoneTransform(toes);
                if (!IsValid)
                {
                    throw new InvalidOperationException(
                        "고정 길이 발 접촉 보정에는 대퇴·하퇴·발 Humanoid 본이 필요합니다.");
                }

                _fallbackBendNormal = CalculateBendNormal();
                if (_fallbackBendNormal.sqrMagnitude <=
                    MinimumBendNormalSquaredMagnitude)
                {
                    _fallbackBendNormal = animator.transform.right;
                }
            }

            internal Vector3 CaptureContactPoint()
            {
                return _toes == null
                    ? _foot.position
                    : (_foot.position + _toes.position) * 0.5f;
            }

            internal void Apply(Vector3 worldCorrection)
            {
                if (!IsValid ||
                    worldCorrection.sqrMagnitude <=
                    MinimumCorrectionSquaredMagnitude)
                {
                    return;
                }

                Quaternion footRotation = _foot.rotation;
                Vector3 bendNormal = CalculateBendNormal();
                if (bendNormal.sqrMagnitude <= MinimumBendNormalSquaredMagnitude)
                {
                    bendNormal = _fallbackBendNormal;
                }

                IKSolverTrigonometric.Solve(
                    _upperLeg,
                    _lowerLeg,
                    _foot,
                    _foot.position + worldCorrection,
                    bendNormal,
                    1f);
                _foot.rotation = footRotation;
            }

            internal void Clear()
            {
                _upperLeg = null;
                _lowerLeg = null;
                _foot = null;
                _toes = null;
                _fallbackBendNormal = Vector3.zero;
            }

            private Vector3 CalculateBendNormal()
            {
                if (!IsValid)
                {
                    return Vector3.zero;
                }

                return Vector3.Cross(
                    _lowerLeg.position - _upperLeg.position,
                    _foot.position - _lowerLeg.position);
            }
        }
    }
}
#endif
