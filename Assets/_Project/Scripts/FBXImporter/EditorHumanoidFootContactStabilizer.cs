#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        private Vector3[] _leftBaseRootSpaceFootPositions = Array.Empty<Vector3>();
        private Vector3[] _rightBaseRootSpaceFootPositions = Array.Empty<Vector3>();
        private Vector3[] _leftBaseRootSpaceBendNormals = Array.Empty<Vector3>();
        private Vector3[] _rightBaseRootSpaceBendNormals = Array.Empty<Vector3>();
        private float _frameRate;

        internal bool IsInitialized =>
            _targetRoot != null &&
            _plan != null &&
            _leftLeg.IsValid &&
            _rightLeg.IsValid;

        internal int LeftContactRunCount => _plan?.LeftContactRunCount ?? 0;

        internal int RightContactRunCount => _plan?.RightContactRunCount ?? 0;

        internal IReadOnlyList<HumanoidFootContactSample> SourceSamples { get; private set; } =
            Array.Empty<HumanoidFootContactSample>();

        internal float SourceHumanScale { get; private set; }

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
            _leftBaseRootSpaceFootPositions = new Vector3[frameCount];
            _rightBaseRootSpaceFootPositions = new Vector3[frameCount];
            _leftBaseRootSpaceBendNormals = new Vector3[frameCount];
            _rightBaseRootSpaceBendNormals = new Vector3[frameCount];
            _frameRate = frameRate;
            Quaternion sourceToTargetRotation = _targetRoot.rotation;

            try
            {
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float timeSeconds = Mathf.Min(frameIndex / frameRate, clip.length);
                    if (!sourceReference.TryEvaluateFootSupportPointsAt(
                            timeSeconds,
                            out Vector3 sourceLeftFoot,
                            out Vector3 sourceLeftToes,
                            out Vector3 sourceRightFoot,
                            out Vector3 sourceRightToes))
                    {
                        throw new InvalidOperationException(
                            "원본 Humanoid 발 접촉점을 샘플링하지 못했습니다.");
                    }

                    Vector2? relativeFootHeights = null;
                    Quaternion? leftFootFrame = null, rightFootFrame = null;
                    if (sourceReference.HasFootRotationReference)
                    {
                        if (!sourceReference.TryCaptureFootSupportReference(out Vector2 heights,
                                out Quaternion leftFrame, out Quaternion rightFrame))
                            throw new InvalidOperationException("원본 Humanoid 발 지지 방향을 샘플링하지 못했습니다.");
                        relativeFootHeights = heights;
                        leftFootFrame = leftFrame;
                        rightFootFrame = rightFrame;
                    }

                    evaluateTarget(timeSeconds);
                    _leftBaseRootSpaceFootPositions[frameIndex] =
                        CaptureRootSpacePosition(_leftLeg.CaptureFootPosition());
                    _rightBaseRootSpaceFootPositions[frameIndex] =
                        CaptureRootSpacePosition(_rightLeg.CaptureFootPosition());
                    _leftBaseRootSpaceBendNormals[frameIndex] =
                        CaptureRootSpaceDirection(_leftLeg.CaptureBendNormal());
                    _rightBaseRootSpaceBendNormals[frameIndex] =
                        CaptureRootSpaceDirection(_rightLeg.CaptureBendNormal());
                    sourceSamples[frameIndex] = new HumanoidFootContactSample(
                        sourceLeftFoot,
                        sourceLeftToes,
                        sourceRightFoot,
                        sourceRightToes,
                        relativeFootHeights,
                        leftFootFrame,
                        rightFootFrame);
                    targetSamples[frameIndex] = new HumanoidFootContactSample(
                        _leftLeg.CaptureFootPosition(),
                        _leftLeg.CaptureToesPosition(),
                        _rightLeg.CaptureFootPosition(),
                        _rightLeg.CaptureToesPosition());
                }

                _plan = HumanoidFootContactPlanner.Build(
                    sourceSamples,
                    targetSamples,
                    frameRate,
                    sourceReference.SourceHumanScale,
                    sourceToTargetRotation,
                    targetAnimator.humanScale);
                // 이미 평가한 원본 궤적을 정밀 밑창 접지에서도 재사용함.
                SourceSamples = Array.AsReadOnly(sourceSamples);
                SourceHumanScale = sourceReference.SourceHumanScale;
            }
            finally
            {
                evaluateTarget(0f);
            }
        }

        internal bool TryCaptureBendNormals(
            out Vector3 leftBendNormal,
            out Vector3 rightBendNormal)
        {
            leftBendNormal = Vector3.zero;
            rightBendNormal = Vector3.zero;
            if (!IsInitialized)
            {
                return true;
            }

            leftBendNormal = _leftLeg.CaptureBendNormal();
            rightBendNormal = _rightLeg.CaptureBendNormal();
            return IsFinite(leftBendNormal) && IsFinite(rightBendNormal);
        }

        internal bool TryApply(
            float timeSeconds,
            Vector3 leftBendNormal,
            Vector3 rightBendNormal)
        {
            if (!IsInitialized)
            {
                return true;
            }

            if (!_plan.TryEvaluateSupportPose(
                    timeSeconds,
                    out Vector3 leftRootSpaceCorrection,
                    out Vector3 rightRootSpaceCorrection,
                    out Vector3 leftRootSpaceToeDirection,
                    out Vector3 rightRootSpaceToeDirection))
            {
                return false;
            }

            if (!TryEvaluateBaseFootPositions(
                    timeSeconds,
                    out Vector3 leftBaseRootSpacePosition,
                    out Vector3 rightBaseRootSpacePosition,
                    out Vector3 leftBaseRootSpaceBendNormal,
                    out Vector3 rightBaseRootSpaceBendNormal))
            {
                return false;
            }

            Vector3 leftCurrentRootSpacePosition =
                CaptureRootSpacePosition(_leftLeg.CaptureFootPosition());
            Vector3 rightCurrentRootSpacePosition =
                CaptureRootSpacePosition(_rightLeg.CaptureFootPosition());
            Vector3 leftWorldCorrection = _targetRoot.TransformDirection(
                leftBaseRootSpacePosition + leftRootSpaceCorrection -
                leftCurrentRootSpacePosition);
            Vector3 rightWorldCorrection = _targetRoot.TransformDirection(
                rightBaseRootSpacePosition + rightRootSpaceCorrection -
                rightCurrentRootSpacePosition);
            Vector3 leftWorldToeDirection =
                _targetRoot.TransformDirection(leftRootSpaceToeDirection);
            Vector3 rightWorldToeDirection =
                _targetRoot.TransformDirection(rightRootSpaceToeDirection);
            Vector3 leftWorldBendNormal = _targetRoot.TransformDirection(
                leftBaseRootSpaceBendNormal);
            Vector3 rightWorldBendNormal = _targetRoot.TransformDirection(
                rightBaseRootSpaceBendNormal);
            if (!IsFinite(leftWorldCorrection) ||
                !IsFinite(rightWorldCorrection) ||
                !IsFinite(leftWorldToeDirection) ||
                !IsFinite(rightWorldToeDirection))
            {
                return false;
            }

            _leftLeg.Apply(
                leftWorldCorrection,
                leftWorldToeDirection,
                leftWorldBendNormal.sqrMagnitude >
                    MinimumBendNormalSquaredMagnitude
                        ? leftWorldBendNormal
                        : leftBendNormal);
            _rightLeg.Apply(
                rightWorldCorrection,
                rightWorldToeDirection,
                rightWorldBendNormal.sqrMagnitude >
                    MinimumBendNormalSquaredMagnitude
                        ? rightWorldBendNormal
                        : rightBendNormal);
            return true;
        }

        internal void Clear()
        {
            _plan = null;
            SourceSamples = Array.Empty<HumanoidFootContactSample>();
            SourceHumanScale = 0f;
            _targetRoot = null;
            _leftBaseRootSpaceFootPositions = Array.Empty<Vector3>();
            _rightBaseRootSpaceFootPositions = Array.Empty<Vector3>();
            _leftBaseRootSpaceBendNormals = Array.Empty<Vector3>();
            _rightBaseRootSpaceBendNormals = Array.Empty<Vector3>();
            _frameRate = 0f;
            _leftLeg.Clear();
            _rightLeg.Clear();
        }

        private Vector3 CaptureRootSpacePosition(Vector3 worldPosition)
        {
            return Quaternion.Inverse(_targetRoot.rotation) *
                (worldPosition - _targetRoot.position);
        }

        private Vector3 CaptureRootSpaceDirection(Vector3 worldDirection)
        {
            return Quaternion.Inverse(_targetRoot.rotation) * worldDirection;
        }

        private bool TryEvaluateBaseFootPositions(
            float timeSeconds,
            out Vector3 leftRootSpacePosition,
            out Vector3 rightRootSpacePosition,
            out Vector3 leftRootSpaceBendNormal,
            out Vector3 rightRootSpaceBendNormal)
        {
            leftRootSpacePosition = Vector3.zero;
            rightRootSpacePosition = Vector3.zero;
            leftRootSpaceBendNormal = Vector3.zero;
            rightRootSpaceBendNormal = Vector3.zero;
            if (!IsFinite(timeSeconds) ||
                timeSeconds < 0f ||
                _frameRate <= 0f ||
                _leftBaseRootSpaceFootPositions.Length == 0 ||
                _leftBaseRootSpaceFootPositions.Length !=
                _rightBaseRootSpaceFootPositions.Length ||
                _leftBaseRootSpaceBendNormals.Length !=
                _leftBaseRootSpaceFootPositions.Length ||
                _rightBaseRootSpaceBendNormals.Length !=
                _rightBaseRootSpaceFootPositions.Length)
            {
                return false;
            }

            float frame = Mathf.Clamp(
                timeSeconds * _frameRate,
                0f,
                _leftBaseRootSpaceFootPositions.Length - 1);
            int firstFrame = Mathf.FloorToInt(frame);
            int secondFrame = Mathf.Min(
                firstFrame + 1,
                _leftBaseRootSpaceFootPositions.Length - 1);
            float interpolation = frame - firstFrame;
            leftRootSpacePosition = Vector3.LerpUnclamped(
                _leftBaseRootSpaceFootPositions[firstFrame],
                _leftBaseRootSpaceFootPositions[secondFrame],
                interpolation);
            rightRootSpacePosition = Vector3.LerpUnclamped(
                _rightBaseRootSpaceFootPositions[firstFrame],
                _rightBaseRootSpaceFootPositions[secondFrame],
                interpolation);
            leftRootSpaceBendNormal = Vector3.LerpUnclamped(
                _leftBaseRootSpaceBendNormals[firstFrame],
                _leftBaseRootSpaceBendNormals[secondFrame],
                interpolation).normalized;
            rightRootSpaceBendNormal = Vector3.LerpUnclamped(
                _rightBaseRootSpaceBendNormals[firstFrame],
                _rightBaseRootSpaceBendNormals[secondFrame],
                interpolation).normalized;
            return IsFinite(leftRootSpacePosition) &&
                IsFinite(rightRootSpacePosition) &&
                IsFinite(leftRootSpaceBendNormal) &&
                IsFinite(rightRootSpaceBendNormal);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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

            internal Vector3 CaptureFootPosition()
            {
                return _foot.position;
            }

            internal Vector3 CaptureToesPosition()
            {
                return _toes == null ? _foot.position : _toes.position;
            }

            internal Vector3 CaptureBendNormal()
            {
                return CalculateBendNormal();
            }

            internal void Apply(
                Vector3 worldCorrection,
                Vector3 worldToeDirection,
                Vector3 bendNormal)
            {
                if (!IsValid)
                {
                    return;
                }

                Quaternion footRotation = _foot.rotation;
                if (worldCorrection.sqrMagnitude > MinimumCorrectionSquaredMagnitude)
                {
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

                if (_toes == null ||
                    worldToeDirection.sqrMagnitude <=
                    MinimumCorrectionSquaredMagnitude)
                {
                    return;
                }

                Vector3 currentToeDirection = _toes.position - _foot.position;
                if (currentToeDirection.sqrMagnitude <=
                    MinimumCorrectionSquaredMagnitude)
                {
                    return;
                }

                _foot.rotation = Quaternion.FromToRotation(
                    currentToeDirection,
                    worldToeDirection) * _foot.rotation;
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
