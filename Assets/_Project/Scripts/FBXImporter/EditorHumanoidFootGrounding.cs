#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal enum HumanoidFootGroundingStatus
    {
        Disabled,
        Unavailable,
        NoGround,
        Applied,
        Fallback
    }

    [Serializable]
    internal sealed class HumanoidFootGroundingSnapshot
    {
        public bool has_ground;
        public string support_role;
        public float rear_weight;
        public float front_weight;
        public Vector3 ground_point;
        public Vector3 ground_normal;
        public Vector3 rear_anchor;
        public Vector3 front_anchor;
        public Vector3 rear_point;
        public Vector3 front_point;
        public float rear_distance_mm;
        public float front_distance_mm;
        public float minimum_distance_mm;
        public string rear_renderer;
        public string front_renderer;
        public int rear_vertex;
        public int front_vertex;
    }

    [Serializable]
    internal sealed class HumanoidFootGroundingGate
    {
        public bool measured;
        public float target_error_m;
        public float sole_clearance_m;
        public float supported_contact_error_m;
        public int supported_contact_count;
    }

    /// <summary>
    /// 명시적 재생 시각의 지지·밑창 목표와 양다리 도달 보정을 조립함.
    /// </summary>
    internal sealed class EditorHumanoidFootGrounding : IDisposable
    {
        private readonly Transform _hips;
        private readonly Leg _left;
        private readonly Leg _right;
        private readonly Quaternion _sourceToTargetRotation;
        private readonly float _humanScale;
        private float _frameRate;
        private float _sourceScaleRatio;
        private Vector3 _originalHipsPosition;
        private bool _hasAppliedPose;

        private EditorHumanoidFootGrounding(Animator animator, Leg left, Leg right)
        {
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            _left = left;
            _right = right;
            _sourceToTargetRotation = animator.transform.rotation;
            _humanScale = animator.humanScale;
        }

        internal bool IsPrepared { get; private set; }
        internal bool HasGround { get; private set; }
        internal bool UsedPhysicalReach { get; private set; }
        internal float MaximumTargetError { get; private set; }
        internal float MinimumSoleClearance { get; private set; }
        internal float MaximumSupportedContactError { get; private set; }
        internal int FullySupportedContactCount { get; private set; }
        internal int UnresolvedSupportPairCount => _left.UnresolvedPairCount + _right.UnresolvedPairCount;
        internal int InterpolatedSupportContactCount => _left.InterpolatedContactCount + _right.InterpolatedContactCount;

        internal bool TryCaptureCurrentSurface(out HumanoidFootGroundingSnapshot left,
            out HumanoidFootGroundingSnapshot right)
        {
            left = null;
            right = null;
            return IsPrepared && _left.TryCaptureCurrentSurface(out left) &&
                _right.TryCaptureCurrentSurface(out right);
        }

        internal static bool TryCreate(Animator animator, out EditorHumanoidFootGrounding grounding)
        {
            grounding = null;
            if (animator == null || animator.avatar == null || !animator.isHuman ||
                animator.GetBoneTransform(HumanBodyBones.Hips) == null ||
                !EditorHumanoidLegBendCalibration.TryCapture(animator,
                    out Vector3 leftNormal, out Vector3 rightNormal))
                return false;

            SkinnedMeshRenderer[] renderers = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Leg left = null;
            Leg right = null;
            try
            {
                if (!Leg.TryCreate(animator, true, renderers, leftNormal, out left) ||
                    !Leg.TryCreate(animator, false, renderers, rightNormal, out right))
                    return false;
                grounding = new EditorHumanoidFootGrounding(animator, left, right);
                return true;
            }
            finally
            {
                if (grounding == null)
                {
                    left?.Sampler.Dispose();
                    right?.Sampler.Dispose();
                }
            }
        }

        internal bool TryPrepare(IReadOnlyList<HumanoidFootContactSample> samples,
            float sourceHumanScale, AnimationClip clip, Action<float> evaluateReference,
            HumanoidFootContactIntentEstimate intents = null)
        {
            IsPrepared = false;
            if (samples == null || samples.Count < 2 || clip == null || evaluateReference == null ||
                sourceHumanScale <= 0f || float.IsNaN(sourceHumanScale) || float.IsInfinity(sourceHumanScale))
                return false;

            _frameRate = HumanoidMotionFrameCalculator.NormalizeFrameRate(clip.frameRate);
            _sourceScaleRatio = _humanScale / sourceHumanScale;
            try
            {
                IsPrepared = _left.TryBuildContacts(samples, true, sourceHumanScale,
                    _frameRate, clip.length, evaluateReference, _sourceToTargetRotation, _sourceScaleRatio,
                    intents?.Left) &&
                    _right.TryBuildContacts(samples, false, sourceHumanScale,
                    _frameRate, clip.length, evaluateReference, _sourceToTargetRotation, _sourceScaleRatio,
                    intents?.Right);
                if (IsPrepared && UnresolvedSupportPairCount > 0)
                    Debug.LogWarning($"공동 지지 계획 미해결 {UnresolvedSupportPairCount}건. " +
                        $"왼발: {_left.FirstPlanIssue}, 오른발: {_right.FirstPlanIssue}. 해당 구간은 개별 목표를 유지함.");
                return IsPrepared;
            }
            finally
            {
                evaluateReference(0f);
            }
        }

        // 이전 접지의 골반 이동과 회전을 원본 평가 전에 해제하여 누적·정지 잔류를 막음.
        internal void RestoreAppliedPose()
        {
            if (!_hasAppliedPose)
                return;
            if (_hips != null)
                _hips.localPosition = _originalHipsPosition;
            _left.RestorePose();
            _right.RestorePose();
            _hasAppliedPose = false;
        }

        internal bool TryApply(float timeSeconds, EditorHumanoidGroundResponse ground)
        {
            HasGround = false;
            UsedPhysicalReach = false;
            MaximumTargetError = 0f;
            MinimumSoleClearance = 0f;
            MaximumSupportedContactError = 0f;
            FullySupportedContactCount = 0;
            if (!IsPrepared || _hips == null || ground == null ||
                float.IsNaN(timeSeconds) || float.IsInfinity(timeSeconds) || timeSeconds < 0f)
                return false;

            _originalHipsPosition = _hips.localPosition;
            _left.CapturePose();
            _right.CapturePose();
            Physics.SyncTransforms();
            float frame = timeSeconds * _frameRate;
            if (!_left.TryCalculateTarget(frame, ground, _humanScale) ||
                !_right.TryCalculateTarget(frame, ground, _humanScale))
                return false;

            HasGround = _left.HasGround || _right.HasGround;
            // 고정 신장 비율로 도달 가능한 지지를 포기하지 않도록 실제 다리 크기 안에서 최소 이동을 찾음.
            float maximumOffset = Mathf.Max(_left.TotalBoneLength, _right.TotalBoneLength);
            // 무지면에는 원래 발 목표와 골반 높이를 유지하되 무릎 방향 정책은 계속 적용함.
            float offset = 0f;
            Vector2 footOffsets = Vector2.zero;
            if (HasGround && !HumanoidPelvisReachCalculator.TryCalculateOffset(
                    _left.BuildReach(true), _right.BuildReach(true), Vector3.up,
                    maximumOffset, out offset, out footOffsets))
            {
                // 감쇠와 양발 지지가 충돌하면 본을 늘리지 않고 물리 길이 한도로 다시 판단함.
                UsedPhysicalReach = true;
                if (!HumanoidPelvisReachCalculator.TryCalculateOffset(
                        _left.BuildReach(false), _right.BuildReach(false), Vector3.up,
                        maximumOffset, out offset, out footOffsets))
                    return false;
            }

            bool isApplied = false;
            _hasAppliedPose = true;
            try
            {
                _hips.position += Vector3.up * offset;
                if (!_left.TrySolve(footOffsets.x, out float leftError) ||
                    !_right.TrySolve(footOffsets.y, out float rightError))
                    return false;
                MaximumTargetError = Mathf.Max(leftError, rightError);
                if (!_left.TryMeasure(out float leftClearance, out float leftContact, out int leftCount) ||
                    !_right.TryMeasure(out float rightClearance, out float rightContact, out int rightCount))
                    return false;
                MinimumSoleClearance = Mathf.Min(leftClearance, rightClearance);
                MaximumSupportedContactError = Mathf.Max(leftContact, rightContact);
                FullySupportedContactCount = leftCount + rightCount;
                isApplied = ResolveApplied(MaximumTargetError, MinimumSoleClearance,
                    MaximumSupportedContactError, _humanScale);
                return isApplied;
            }
            finally
            {
                if (!isApplied)
                    RestoreAppliedPose();
            }
        }

        public void Dispose()
        {
            RestoreAppliedPose();
            IsPrepared = false;
            _left.Sampler.Dispose();
            _right.Sampler.Dispose();
        }

        // 완전 지지 중에는 앵커와 실측 접촉점의 괴리도 게이트 조건임.
        // 지지 중에 부유·관통이 남은 프레임을 Applied로 보고하지 않음.
        // 계측 CSV가 같은 임계로 거절 사유를 재현할 수 있게 비율을 공개함.
        internal const float TargetErrorPerHumanScale = 0.0001f;
        internal const float SoleClearancePerHumanScale = 0.0001f;
        internal const float SupportedContactErrorPerHumanScale = 0.005f;

        internal static bool ResolveApplied(float targetError, float soleClearance,
            float supportedContactError, float humanScale)
        {
            return targetError <= humanScale * TargetErrorPerHumanScale &&
                soleClearance >= -humanScale * SoleClearancePerHumanScale &&
                supportedContactError <= humanScale * SupportedContactErrorPerHumanScale;
        }

        private sealed class Leg
        {
            private const float MaximumSupportedToePitch = 40f;
            internal readonly Transform Upper;
            internal readonly Transform Lower;
            internal readonly Transform Foot;
            internal readonly Transform Toes;
            internal readonly EditorHumanoidFootSoleSampler Sampler;
            private readonly Vector3 _referenceNormal;
            private readonly Quaternion? _localFootFrame;
            private readonly Vector3[][] _sourcePoints = new Vector3[2][];
            private EditorHumanoidFootContactPlan _plan;
            private readonly EditorHumanoidFootContactPlan.Sample[] _activeContacts = new EditorHumanoidFootContactPlan.Sample[2];
            private readonly Vector3[] _localContactPoints = new Vector3[2];
            private readonly Vector3[] _anchors = new Vector3[2];
            private Vector2[] _weights;
            private float[] _relativeHeights;
            private float _nearLevelHeight;
            private bool _hasSupportRoles;
            private Vector2 _activeWeights;
            private Quaternion _upperRotation, _lowerRotation, _footRotation, _toeRotation;
            private Quaternion _originalFootRotation, _originalUpperRotation, _targetFootRotation;
            private Vector3 _upperToKnee, _kneeToFoot, _originalFootPosition, _target;
            private float _support, _clearance, _maximumReach;
            private RaycastHit _ground;

            internal bool HasGround { get; private set; }
            internal int UnresolvedPairCount => _plan?.UnresolvedPairCount ?? 0;
            internal string FirstPlanIssue => _plan?.FirstIssue;
            internal int InterpolatedContactCount =>
                (_activeWeights.x >= 0.999f && _activeContacts[0].IsInterpolatedPoint ? 1 : 0) +
                (_activeWeights.y >= 0.999f && _activeContacts[1].IsInterpolatedPoint ? 1 : 0);

            internal bool TryCaptureCurrentSurface(out HumanoidFootGroundingSnapshot sample)
            {
                sample = new HumanoidFootGroundingSnapshot
                {
                    has_ground = HasGround,
                    rear_weight = _activeWeights.x,
                    front_weight = _activeWeights.y,
                    rear_anchor = _anchors[0],
                    front_anchor = _anchors[1],
                    support_role = _activeWeights.x > 0f
                        ? (_activeWeights.y > 0f ? "both" : "rear")
                        : (_activeWeights.y > 0f ? "front" : "released")
                };
                if (!HasGround) return true;
                if (!Sampler.TrySample() ||
                    !Sampler.TrySelectContact(false, _ground.normal, out int rear, out Vector3 rearPoint) ||
                    !Sampler.TrySelectContact(true, _ground.normal, out int front, out Vector3 frontPoint) ||
                    !Sampler.TryGetPointIdentity(rear, out SkinnedMeshRenderer rearRenderer, out int rearVertex) ||
                    !Sampler.TryGetPointIdentity(front, out SkinnedMeshRenderer frontRenderer, out int frontVertex))
                    return false;

                sample.ground_point = _ground.point;
                sample.ground_normal = _ground.normal;
                sample.rear_point = rearPoint;
                sample.front_point = frontPoint;
                sample.rear_distance_mm = Vector3.Dot(rearPoint - _ground.point, _ground.normal) * 1000f;
                sample.front_distance_mm = Vector3.Dot(frontPoint - _ground.point, _ground.normal) * 1000f;
                sample.minimum_distance_mm = Mathf.Min(sample.rear_distance_mm, sample.front_distance_mm);
                sample.rear_renderer = rearRenderer.name;
                sample.front_renderer = frontRenderer.name;
                sample.rear_vertex = rearVertex;
                sample.front_vertex = frontVertex;
                return true;
            }

            private Leg(Transform upper, Transform lower, Transform foot, Transform toes,
                EditorHumanoidFootSoleSampler sampler, Vector3 referenceNormal, Quaternion? localFootFrame)
            {
                Upper = upper;
                Lower = lower;
                Foot = foot;
                Toes = toes;
                Sampler = sampler;
                _referenceNormal = referenceNormal;
                _localFootFrame = localFootFrame;
            }

            internal static bool TryCreate(Animator animator, bool isLeft,
                SkinnedMeshRenderer[] renderers, Vector3 normal, out Leg leg)
            {
                leg = null;
                Transform upper = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                Transform lower = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                Transform foot = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                Transform toes = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                Quaternion? localFootFrame = null;
                if (HumanoidFootRotationBinding.TryCreate(foot, toes, animator.transform.up, out var binding) &&
                    binding.TryCaptureWorldFrame(out Quaternion worldFrame))
                    localFootFrame = (Quaternion.Inverse(foot.rotation) * worldFrame).normalized;
                if (upper == null || lower == null || foot == null ||
                    !lower.IsChildOf(upper) || !foot.IsChildOf(lower) ||
                    !EditorHumanoidFootSoleSampler.TryCreate(foot, toes, renderers, Vector3.up, out var sampler))
                    return false;
                leg = new Leg(upper, lower, foot, toes, sampler, normal, localFootFrame);
                return true;
            }

            internal bool TryBuildContacts(IReadOnlyList<HumanoidFootContactSample> samples,
                bool isLeft, float sourceScale, float frameRate, float clipLength, Action<float> evaluate,
                Quaternion sourceRotation, float scaleRatio,
                IReadOnlyList<HumanoidFootContactIntent> intents = null)
            {
                Vector2 referenceHeights = Vector2.zero;
                bool hasRelativeHeights = samples[0].RelativeFootHeights.HasValue;
                bool hasSourceFrames = samples[0].LeftFootFrame.HasValue;
                _hasSupportRoles = false;
                var relativeHeights = hasRelativeHeights ? new float[samples.Count] : null;
                var sourceFrames = hasSourceFrames ? new Quaternion[samples.Count] : null;
                for (int i = 0; i < samples.Count; i++)
                {
                    if (samples[i].LeftFootFrame.HasValue != hasSourceFrames ||
                        samples[i].RightFootFrame.HasValue != hasSourceFrames)
                        return false;
                    if (hasSourceFrames)
                        sourceFrames[i] = (isLeft ? samples[i].LeftFootFrame : samples[i].RightFootFrame).Value;
                    Vector2? heights = samples[i].RelativeFootHeights;
                    if (heights.HasValue != hasRelativeHeights)
                        return false;
                    if (hasRelativeHeights)
                        relativeHeights[i] = isLeft ? heights.Value.x : heights.Value.y;
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    var points = new Vector3[samples.Count];
                    var heights = new float[samples.Count];
                    for (int i = 0; i < samples.Count; i++)
                    {
                        HumanoidFootContactSample sample = samples[i];
                        points[i] = isLeft ? (channel == 0 ? sample.LeftFoot : sample.LeftToes) :
                            (channel == 0 ? sample.RightFoot : sample.RightToes);
                        heights[i] = points[i].y;
                    }
                    _sourcePoints[channel] = points;
                    Array.Sort(heights);
                    referenceHeights[channel] = heights[Mathf.Max(0, Mathf.CeilToInt(heights.Length * 0.02f) - 1)];
                }
                if (!HumanoidFootSupportWeightCalculator.TryBuild(_sourcePoints[0], _sourcePoints[1],
                        frameRate, referenceHeights, sourceScale / 30f, sourceScale / 6f,
                        0.1f, 0.1f, out _weights))
                    return false;

                // 확정된 지지 의도 구간 안에서 감지된 접촉은 빠르게 잠금함(터치다운 비대칭 전이).
                HumanoidFootAnchorPolicyResolver.ApplyTouchdownLock(_weights, intents);

                // 역할 배열을 먼저 확정하여 임의 탐색 순서에도 같은 접촉 계획을 사용함.
                if (hasRelativeHeights)
                {
                    if (!HumanoidFootSupportRoleCalculator.TryBuild(_weights, relativeHeights,
                            frameRate, sourceScale / 300f, 0.1f, out Vector2[] roleWeights))
                        return false;
                    _weights = roleWeights;
                    _hasSupportRoles = true;
                }
                _relativeHeights = relativeHeights;
                _nearLevelHeight = sourceScale / 70f;

                // 의도 구간을 프레임별 앵커 방침으로 변환하고 자유 구간은 속도 상한을 둠.
                HumanoidFootAnchorPolicy[] policies =
                    HumanoidFootAnchorPolicyResolver.Rasterize(intents, _weights.Length);
                float pinRelease = sourceScale *
                    HumanoidFootContactIntentEstimator.SlideDisplacementPerHumanScale;
                float trackStep = sourceScale * scaleRatio *
                    HumanoidFootContactIntentEstimator.SupportSpeedPerHumanScale / frameRate;
                return EditorHumanoidFootContactPlan.TryBuild(Foot, Sampler, _sourcePoints, _weights,
                    sourceRotation, scaleRatio, frameRate, clipLength, evaluate, out _plan,
                    sourceFrames, hasSourceFrames ? _localFootFrame : null,
                    policies, pinRelease, trackStep);
            }

            internal void CapturePose()
            {
                _upperRotation = Upper.localRotation;
                _lowerRotation = Lower.localRotation;
                _footRotation = Foot.localRotation;
                _toeRotation = Toes.localRotation;
                _originalFootRotation = Foot.rotation;
                _originalUpperRotation = Upper.rotation;
                _originalFootPosition = Foot.position;
                _upperToKnee = Lower.position - Upper.position;
                _kneeToFoot = Foot.position - Lower.position;
            }

            internal void RestorePose()
            {
                if (Upper != null) Upper.localRotation = _upperRotation;
                if (Lower != null) Lower.localRotation = _lowerRotation;
                if (Foot != null) Foot.localRotation = _footRotation;
                if (Toes != null) Toes.localRotation = _toeRotation;
            }

            internal bool TryCalculateTarget(float frame, EditorHumanoidGroundResponse ground, float humanScale)
            {
                _target = _originalFootPosition;
                _targetFootRotation = _originalFootRotation;
                _support = 0f;
                _activeWeights = Vector2.zero;
                HasGround = ground.TryFindGround(_originalFootPosition, out _ground);
                _clearance = humanScale;
                _maximumReach = _upperToKnee.magnitude + _kneeToFoot.magnitude;
                if (!HasGround)
                    return true;
                if (!Sampler.TrySample())
                    return false;

                frame = Mathf.Clamp(frame, 0f, _weights.Length - 1);
                int first = Mathf.FloorToInt(frame);
                int second = Mathf.Min(first + 1, _weights.Length - 1);
                float blend = frame - first;
                Vector2 weights = _hasSupportRoles
                    ? HumanoidFootSupportRoleCalculator.Interpolate(_weights[first], _weights[second], blend)
                    : Vector2.LerpUnclamped(_weights[first], _weights[second], blend);
                for (int channel = 0; channel < 2; channel++)
                {
                    EditorHumanoidFootContactPlan.Sample contact = _plan.GetSample(channel, frame);
                    _activeContacts[channel] = contact;
                    if (!contact.HasValue || weights[channel] <= 0f)
                        continue;
                    if (!ground.TryFindGround(contact.Anchor, out RaycastHit contactGround))
                        continue;
                    _anchors[channel] = contactGround.point;
                    _activeWeights[channel] = weights[channel];
                    if (!contact.TryGetLocalPoint(Sampler, out _localContactPoints[channel])) return false;
                }
                if (_activeWeights.x > 0f && _activeWeights.y > 0f &&
                    _activeContacts[0].CanAlignPair && _activeContacts[1].CanAlignPair)
                {
                    Vector3 current = _originalFootRotation * (_localContactPoints[1] - _localContactPoints[0]);
                    Vector3 desired = _anchors[1] - _anchors[0];
                    if (current.sqrMagnitude < 0.00000001f || desired.sqrMagnitude < 0.00000001f ||
                        Vector3.Dot(current.normalized, desired.normalized) < -0.99f) return false;
                    _targetFootRotation = Quaternion.Slerp(_originalFootRotation,
                        Quaternion.FromToRotation(current, desired) * _originalFootRotation,
                        Mathf.Min(_activeWeights.x, _activeWeights.y));
                }
                if (!TryAlignSupportSurface(frame)) return false;
                Vector3 weightedTarget = Vector3.zero;
                float totalWeight = 0f;
                for (int channel = 0; channel < 2; channel++)
                {
                    if (_activeWeights[channel] <= 0f) continue;
                    if (!HumanoidFootPivotCalculator.TryCalculatePose(_originalFootPosition,
                            _originalFootRotation, _localContactPoints[channel], Foot.lossyScale.x, _anchors[channel],
                            _targetFootRotation, 1f, out Vector3 candidate, out _))
                        return false;
                    weightedTarget += candidate * _activeWeights[channel];
                    totalWeight += _activeWeights[channel];
                }
                _support = Mathf.Max(_activeWeights.x, _activeWeights.y);
                if (totalWeight > 0f)
                    _target = Vector3.LerpUnclamped(_originalFootPosition, weightedTarget / totalWeight, _support);
                if (!HumanoidSoleGroundConstraint.TryLiftAbovePlane(_target, _targetFootRotation,
                        Sampler.LocalSolePoints, Foot.lossyScale.x, _ground.point, _ground.normal,
                        out _target, out _))
                    return false;
                _clearance = Mathf.Max(0f, CalculateSoleClearance(_target, _targetFootRotation));
                float originalAngle = Vector3.Angle(-_upperToKnee, _kneeToFoot) * Mathf.Deg2Rad;
                return HumanoidPelvisReachCalculator.TryCalculateExtensionReach(
                    _upperToKnee.magnitude, _kneeToFoot.magnitude, originalAngle,
                    Vector3.Distance(Upper.position, _target), _support, 2.8f, out _maximumReach);
            }

            private bool TryAlignSupportSurface(float frame)
            {
                if ((_activeWeights.x <= 0f && _activeWeights.y <= 0f) || !_localFootFrame.HasValue)
                    return true;
                Vector3 queryNormal = _originalFootRotation *
                    (Quaternion.Inverse(_targetFootRotation) * _ground.normal);
                if (!Sampler.TrySelectContact(false, queryNormal.normalized, out int rear, out _) ||
                    !Sampler.TrySelectContact(true, queryNormal.normalized, out int front, out _) ||
                    !Sampler.TryGetLocalPoint(rear, out Vector3 rearPoint) ||
                    !Sampler.TryGetLocalPoint(front, out Vector3 frontPoint)) return false;

                float difference = Vector3.Dot(_targetFootRotation * (rearPoint - frontPoint), _ground.normal);
                float weight = difference > 0f ? _activeWeights.x : _activeWeights.y;
                if (weight <= 0f && _relativeHeights != null)
                {
                    int first = Mathf.FloorToInt(frame);
                    int second = Mathf.Min(first + 1, _relativeHeights.Length - 1);
                    float sourceHeight = Mathf.Lerp(_relativeHeights[first], _relativeHeights[second], frame - first);
                    float oppositeWeight = difference > 0f ? _activeWeights.y : _activeWeights.x;
                    // 거의 평평한 자세는 단일 지지로 분류되어도 밑창의 들린 쪽을 함께 내림.
                    weight = oppositeWeight * Mathf.Clamp01(2f - Mathf.Abs(sourceHeight) / _nearLevelHeight);
                }
                Vector3 axis = Vector3.Cross(_ground.normal,
                    _targetFootRotation * (_localFootFrame.Value * Vector3.forward)).normalized;
                if (axis.sqrMagnitude < 0.5f) return true;
                if (weight > 0f && EditorHumanoidFootContactPlan.TryFindSupportPose(Foot, Sampler, axis,
                        _targetFootRotation, _ground.normal, out Quaternion rotation, out _, out _))
                {
                    // 들리는 영역의 지지 강도만 반영하여 해제 중인 발을 수평으로 강제하지 않음.
                    _targetFootRotation = Quaternion.Slerp(_targetFootRotation, rotation, weight);
                }
                if (difference > 0f && _activeWeights.x <= 0f && _activeWeights.y > 0f)
                {
                    Vector3 forward = _targetFootRotation * (_localFootFrame.Value * Vector3.forward);
                    float pitch = Mathf.Asin(Mathf.Clamp(Vector3.Dot(forward, _ground.normal), -1f, 1f)) *
                        Mathf.Rad2Deg;
                    if (pitch < -MaximumSupportedToePitch)
                    {
                        // 앞꿈치 지지는 유지하되 원본의 과도한 발끝 하향 회전만 제한함.
                        Quaternion limited = Quaternion.AngleAxis(pitch + MaximumSupportedToePitch, axis) *
                            _targetFootRotation;
                        _targetFootRotation = Quaternion.Slerp(_targetFootRotation, limited, _activeWeights.y);
                    }
                }
                return true;
            }

            internal HumanoidPelvisReachCalculator.Leg BuildReach(bool dampExtension) =>
                new HumanoidPelvisReachCalculator.Leg(Upper.position - _target,
                    _upperToKnee.magnitude, _kneeToFoot.magnitude, _support, _clearance,
                    dampExtension ? _maximumReach : (double)_upperToKnee.magnitude + _kneeToFoot.magnitude);

            internal float TotalBoneLength => _upperToKnee.magnitude + _kneeToFoot.magnitude;

            internal bool TrySolve(float footOffset, out float error)
            {
                _target += Vector3.up * footOffset;
                error = Vector3.Distance(Foot.position, _target);
                // 목표 변위가 0에 가까워져도 무릎 방향 보정을 계속 적용하여 자세가 튀지 않게 함.
                if (!HumanoidLegPoseSolver.TryCalculateBendNormal(_upperToKnee, _kneeToFoot,
                        _target - Upper.position, _originalUpperRotation * _referenceNormal,
                        out Vector3 normal, out _) ||
                    !HumanoidLegPoseSolver.TrySolve(Upper, Lower, Foot, _target, normal, out error))
                    return false;
                Foot.rotation = _targetFootRotation;
                Toes.localRotation = _toeRotation;
                return true;
            }

            internal bool TryMeasure(out float clearance, out float contactError, out int contactCount)
            {
                clearance = 0f;
                contactError = 0f;
                contactCount = 0;
                if (!HasGround)
                    return true;
                if (!Sampler.TrySample())
                    return false;
                clearance = CalculateSoleClearance(Foot.position, Foot.rotation);
                for (int channel = 0; channel < 2; channel++)
                {
                    if (!_activeContacts[channel].HasValue || _activeWeights[channel] < 0.999f)
                        continue;
                    if (!_activeContacts[channel].TryGetLocalPoint(Sampler, out Vector3 point))
                        return false;
                    contactCount++;
                    contactError = Mathf.Max(contactError, Vector3.Distance(Foot.TransformPoint(point), _anchors[channel]));
                }
                return true;
            }

            private float CalculateSoleClearance(Vector3 position, Quaternion rotation)
            {
                float minimum = float.PositiveInfinity;
                foreach (Vector3 point in Sampler.LocalSolePoints)
                    minimum = Mathf.Min(minimum, Vector3.Dot(position + rotation * (point * Foot.lossyScale.x) -
                        _ground.point, _ground.normal) / _ground.normal.y);
                return minimum;
            }
        }
    }
}
#endif
