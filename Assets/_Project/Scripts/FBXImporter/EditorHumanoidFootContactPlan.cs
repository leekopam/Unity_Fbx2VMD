#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 지지점 신원과 공통 이동을 미리 계산하여 탐색 순서와 무관하게 평가함.
    /// </summary>
    internal sealed class EditorHumanoidFootContactPlan
    {
        private readonly Frame[][] _frames;
        internal int UnresolvedPairCount { get; private set; }
        internal string FirstIssue { get; private set; }

        private EditorHumanoidFootContactPlan(int count)
        {
            _frames = new[] { new Frame[count], new Frame[count] };
        }

        internal readonly struct Sample
        {
            private readonly int _firstPoint;
            private readonly int _secondPoint;
            private readonly float _blend;
            internal readonly Vector3 Anchor;
            internal readonly bool CanAlignPair;
            internal bool HasValue => _firstPoint >= 0;
            internal bool IsInterpolatedPoint => _firstPoint != _secondPoint && _blend > 0f && _blend < 1f;

            internal Sample(int firstPoint, int secondPoint, float blend, Vector3 anchor, bool canAlignPair)
            {
                _firstPoint = firstPoint;
                _secondPoint = secondPoint;
                _blend = blend;
                Anchor = anchor;
                CanAlignPair = canAlignPair;
            }

            internal bool TryGetLocalPoint(EditorHumanoidFootSoleSampler sampler, out Vector3 point)
            {
                point = Vector3.zero;
                if (!sampler.TryGetLocalPoint(_firstPoint, out Vector3 first) ||
                    !sampler.TryGetLocalPoint(_secondPoint, out Vector3 second)) return false;
                // 신원 전환 사이의 가상 접촉점도 앵커와 같은 계수로 보간함.
                point = Vector3.LerpUnclamped(first, second, _blend);
                return true;
            }
        }

        internal Sample GetSample(int channel, float frame)
        {
            frame = Mathf.Clamp(frame, 0f, _frames[channel].Length - 1);
            int firstIndex = Mathf.FloorToInt(frame);
            Frame first = _frames[channel][firstIndex];
            Frame second = _frames[channel][Mathf.Min(firstIndex + 1, _frames[channel].Length - 1)];
            if (first.Point < 0) first = second;
            if (second.Point < 0) second = first;
            float blend = frame - firstIndex;
            // 접촉점 신원이 갈리는 경계는 지면과 무관한 가상점 보간 대신
            // 해당 프레임의 실제 피벗과 앵커를 그대로 교체함.
            if (first.Point >= 0 && second.Point >= 0 && first.Point != second.Point)
            {
                Frame chosen = blend < 0.5f ? first : second;
                return new Sample(chosen.Point, chosen.Point, 0f, chosen.Anchor,
                    first.CanAlignPair || second.CanAlignPair);
            }
            return new Sample(first.Point, second.Point, blend,
                Vector3.LerpUnclamped(first.Anchor, second.Anchor, blend), first.CanAlignPair || second.CanAlignPair);
        }

        internal static bool TryBuild(Transform foot, EditorHumanoidFootSoleSampler sampler,
            Vector3[][] source, Vector2[] weights, Quaternion sourceRotation, float scaleRatio,
            float frameRate, float clipLength, Action<float> evaluate, out EditorHumanoidFootContactPlan plan,
            Quaternion[] sourceFrames = null, Quaternion? localFootFrame = null,
            HumanoidFootAnchorPolicy[] anchorPolicies = null,
            float pinReleaseSourceDistance = 0f, float anchorTrackStep = 0f)
        {
            plan = null;
            if (foot == null || sampler == null || source == null || source.Length != 2 || weights == null ||
                weights.Length == 0 || source[0].Length != weights.Length || source[1].Length != weights.Length ||
                evaluate == null || frameRate <= 0f || scaleRatio <= 0f ||
                !HasValidFrames(sourceFrames, localFootFrame, weights.Length) ||
                (anchorPolicies != null && anchorPolicies.Length != weights.Length)) return false;
            var result = new EditorHumanoidFootContactPlan(weights.Length);
            var states = new Contact[2];
            var offsets = new Vector3[2];
            bool TryUpdateOffset(int channel, int point)
            {
                if (sourceFrames == null) return true;
                if (!sampler.TryGetLocalPoint(point, out Vector3 localPoint)) return false;
                // 준비한 발 기준축의 원본 단위 offset을 고정하여 회전 기여와 배율을 한 번만 적용함.
                offsets[channel] = Quaternion.Inverse(localFootFrame.Value) * (localPoint * foot.lossyScale.x / scaleRatio);
                return true;
            }
            Pair pair = null;
            bool wasOverlapping = false;
            var emitted = new Vector3[2];
            var emittedValid = new bool[2];
            var emittedContact = new Contact[2];
            for (int frame = 0; frame < weights.Length; frame++)
            {
                Vector3 SourcePoint(int channel) => sourceFrames == null ? source[channel][frame] :
                    source[0][frame] + sourceFrames[frame] * offsets[channel];
                bool hasSample = false;
                bool TrySample()
                {
                    if (hasSample) return true;
                    evaluate(Mathf.Min(frame / frameRate, clipLength));
                    hasSample = sampler.TrySample();
                    return hasSample;
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    if (weights[frame][channel] <= 0f) states[channel] = null;
                    else if (states[channel] == null)
                    {
                        if (!TrySample() || !sampler.TrySelectContact(channel == 1, Vector3.up,
                                out int point, out Vector3 anchor)) return false;
                        if (!TryUpdateOffset(channel, point)) return false;
                        states[channel] = new Contact(frame, point, anchor, SourcePoint(channel));
                    }
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    states[channel]?.ApplyAnchorPolicy(SourcePoint(channel), sourceRotation,
                        scaleRatio,
                        anchorPolicies == null
                            ? HumanoidFootAnchorPolicy.Free
                            : anchorPolicies[frame],
                        pinReleaseSourceDistance);
                }
                bool overlapping = states[0] != null && states[1] != null;
                if (overlapping && !wasOverlapping)
                {
                    if (!TrySample()) return false;
                    Vector3 previousRear = SourcePoint(0), previousFront = SourcePoint(1);
                    if (!TryCreatePair(foot, sampler, states, source, frame, sourceRotation, out pair))
                        result.RecordIssue(frame, "공동 접촉 자세 또는 지상 방향을 준비하지 못함");
                    else
                    {
                        for (int channel = 0; channel < 2; channel++)
                        {
                            if (!TryUpdateOffset(channel, states[channel].Point)) return false;
                            // 핀 고정 접촉은 기준점을 현재 원본 위치로 재설정해 앵커가 움직이지 않게 함.
                            // 비핀 접촉은 점 교체에 따른 기존 앵커 이동을 유지하고 원본 기준점 변경만 상쇄함.
                            states[channel].SourcePoint = states[channel].Pinned
                                ? SourcePoint(channel)
                                : states[channel].SourcePoint + SourcePoint(channel) -
                                    (channel == 0 ? previousRear : previousFront);
                        }
                    }
                }
                if (!overlapping) pair = null;
                wasOverlapping = overlapping;
                bool canAlign = false;
                if (pair != null)
                {
                    Vector3 direction = Vector3.ProjectOnPlane(sourceRotation * (source[1][frame] - source[0][frame]), Vector3.up);
                    if (direction.sqrMagnitude >= 0.00000001f)
                    {
                        float yaw = Vector3.SignedAngle(pair.SourceDirection, direction, Vector3.up);
                        Vector3 anchor = states[pair.Primary].GetAnchor(SourcePoint(pair.Primary), sourceRotation, scaleRatio);
                        int secondary = 1 - pair.Primary;
                        // 핀 고정된 보조 접촉은 공동 정렬로 앵커를 덮어쓰지 않음.
                        if (!states[secondary].Pinned)
                        {
                            states[secondary].Anchor = anchor + Quaternion.AngleAxis(yaw, Vector3.up) * pair.Offset;
                            states[secondary].SourcePoint = SourcePoint(secondary);
                        }
                        canAlign = true;
                    }
                    else
                    {
                        result.RecordIssue(frame, "원본 발 방향의 지상 투영이 소실됨");
                        pair = null;
                    }
                }
                for (int channel = 0; channel < 2; channel++)
                {
                    if (states[channel] == null)
                    {
                        result._frames[channel][frame] = new Frame(-1, Vector3.zero, false);
                        emittedValid[channel] = false;
                        emittedContact[channel] = null;
                        continue;
                    }
                    HumanoidFootAnchorPolicy policy = anchorPolicies == null
                        ? HumanoidFootAnchorPolicy.Free
                        : anchorPolicies[frame];
                    Vector3 anchor = states[channel].GetAnchor(
                        SourcePoint(channel), sourceRotation, scaleRatio);
                    // 자유 구간의 앵커 수평 이동은 프레임당 상한으로 제한해 자세 연속성을 지킴.
                    if (anchorTrackStep > 0f && policy == HumanoidFootAnchorPolicy.Free &&
                        emittedValid[channel] && emittedContact[channel] == states[channel])
                        anchor = Vector3.MoveTowards(emitted[channel], anchor, anchorTrackStep);
                    result._frames[channel][frame] =
                        new Frame(states[channel].Point, anchor, canAlign);
                    emitted[channel] = anchor;
                    emittedValid[channel] = true;
                    emittedContact[channel] = states[channel];
                }
            }
            plan = result;
            return true;
        }

        private static bool HasValidFrames(Quaternion[] sourceFrames, Quaternion? localFootFrame, int count)
        {
            if ((sourceFrames != null) != localFootFrame.HasValue) return false;
            if (sourceFrames == null) return true;
            if (sourceFrames.Length != count || !IsUnitRotation(localFootFrame.Value)) return false;
            foreach (Quaternion frame in sourceFrames)
                if (!IsUnitRotation(frame)) return false;
            return true;
        }

        private static bool IsUnitRotation(Quaternion rotation)
        {
            float length = Quaternion.Dot(rotation, rotation);
            return !float.IsNaN(length) && !float.IsInfinity(length) && Mathf.Abs(length - 1f) < 0.0001f;
        }

        private static bool TryCreatePair(Transform foot, EditorHumanoidFootSoleSampler sampler,
            Contact[] states, Vector3[][] source, int frame, Quaternion sourceRotation, out Pair pair)
        {
            pair = null;
            Vector3 direction = Vector3.ProjectOnPlane(sourceRotation * (source[1][frame] - source[0][frame]), Vector3.up);
            if (direction.sqrMagnitude < 0.00000001f ||
                !sampler.TryGetLocalPoint(states[0].Point, out Vector3 originalRear) ||
                !sampler.TryGetLocalPoint(states[1].Point, out Vector3 originalFront)) return false;
            Vector3 axis = Vector3.Cross(Vector3.up, foot.TransformVector(originalFront - originalRear)).normalized;
            if (axis.sqrMagnitude < 0.5f || !TryFindSupportPose(foot, sampler, axis, foot.rotation, Vector3.up,
                    out Quaternion rotation, out int rear, out int front)) return false;
            if (!sampler.TryGetLocalPoint(rear, out Vector3 newRear) ||
                !sampler.TryGetLocalPoint(front, out Vector3 newFront)) return false;
            int primary = states[0].StartFrame <= states[1].StartFrame ? 0 : 1;
            Vector3 offset = Vector3.ProjectOnPlane(rotation * ((newFront - newRear) * foot.lossyScale.x), Vector3.up);
            if (offset.sqrMagnitude < 0.00000001f) return false;
            Vector3 change = primary == 0 ? newRear - originalRear : newFront - originalFront;
            // 핀 고정된 주 접촉의 앵커는 재정렬로 이동하지 않음.
            if (!states[primary].Pinned)
                states[primary].Anchor += Vector3.ProjectOnPlane(rotation * (change * foot.lossyScale.x), Vector3.up);
            states[0].Point = rear;
            states[1].Point = front;
            pair = new Pair(primary, direction, primary == 0 ? offset : -offset);
            return true;
        }

        internal static bool TryFindSupportPose(Transform foot, EditorHumanoidFootSoleSampler sampler, Vector3 axis,
            Quaternion referenceRotation, Vector3 groundNormal,
            out Quaternion rotation, out int rear, out int front)
        {
            rotation = referenceRotation;
            rear = front = -1;
            Quaternion original = rotation;
            float Evaluate(float angle, out int rearPoint, out int frontPoint)
            {
                rearPoint = frontPoint = -1;
                Quaternion candidate = Quaternion.AngleAxis(angle, axis) * original;
                Vector3 normal = foot.rotation * (Quaternion.Inverse(candidate) * groundNormal);
                if (!sampler.TrySelectContact(false, normal.normalized, out rearPoint, out _) ||
                    !sampler.TrySelectContact(true, normal.normalized, out frontPoint, out _) ||
                    !sampler.TryGetLocalPoint(rearPoint, out Vector3 rearLocal) ||
                    !sampler.TryGetLocalPoint(frontPoint, out Vector3 frontLocal)) return float.NaN;
                return Vector3.Dot(candidate * ((rearLocal - frontLocal) * foot.lossyScale.x), groundNormal);
            }
            float best = float.PositiveInfinity;
            float leftValue = Evaluate(-45f, out _, out _);
            // 단조성을 가정하지 않고 짧은 구간별로 해를 찾은 뒤 원본에 가장 가까운 각도를 선택함.
            for (int step = -44; step <= 45; step++)
            {
                float rightValue = Evaluate(step, out _, out _);
                if (float.IsNaN(leftValue) || float.IsNaN(rightValue)) return false;
                if (leftValue * rightValue <= 0f)
                {
                    float left = step - 1f, right = step, value = leftValue;
                    for (int iteration = 0; iteration < 18; iteration++)
                    {
                        float middle = (left + right) * 0.5f;
                        float middleValue = Evaluate(middle, out _, out _);
                        if (value * middleValue <= 0f) right = middle;
                        else { left = middle; value = middleValue; }
                    }
                    float angle = (left + right) * 0.5f;
                    if (Mathf.Abs(angle) < Mathf.Abs(best)) best = angle;
                }
                leftValue = rightValue;
            }
            if (float.IsInfinity(best)) return false;
            Evaluate(best, out rear, out front);
            rotation = Quaternion.AngleAxis(best, axis) * original;
            return true;
        }

        private void RecordIssue(int frame, string reason)
        {
            UnresolvedPairCount++;
            if (FirstIssue == null) FirstIssue = $"프레임 {frame}: {reason}";
        }

        private readonly struct Frame
        {
            internal readonly int Point;
            internal readonly Vector3 Anchor;
            internal readonly bool CanAlignPair;
            internal Frame(int point, Vector3 anchor, bool canAlignPair)
            { Point = point; Anchor = anchor; CanAlignPair = canAlignPair; }
        }

        private sealed class Contact
        {
            internal readonly int StartFrame;
            internal int Point;
            internal Vector3 Anchor;
            internal Vector3 SourcePoint;
            internal bool Pinned { get; private set; }
            private Vector3 _pinSource;
            private bool _pinBlocked;

            internal Contact(int startFrame, int point, Vector3 anchor, Vector3 sourcePoint)
            { StartFrame = startFrame; Point = point; Anchor = anchor; SourcePoint = sourcePoint; }

            internal Vector3 GetAnchor(Vector3 source, Quaternion rotation, float scaleRatio) =>
                Anchor + Vector3.ProjectOnPlane(rotation * (source - SourcePoint), Vector3.up) * scaleRatio;

            // 확정 Plant 구간에서는 지금까지 추종한 위치를 진입 시점에 접어 앵커를 고정함.
            // 원본이 핀 시작점에서 임계 이상 움직이면 의도 추정 오류로 보고 남은 수명 동안 핀을 해제함.
            internal void ApplyAnchorPolicy(Vector3 source, Quaternion rotation, float scaleRatio,
                HumanoidFootAnchorPolicy policy, float pinReleaseSourceDistance)
            {
                if (policy != HumanoidFootAnchorPolicy.Pinned || _pinBlocked)
                {
                    Pinned = false;
                    return;
                }
                if (!Pinned)
                {
                    Anchor = GetAnchor(source, rotation, scaleRatio);
                    _pinSource = source;
                    Pinned = true;
                }
                else if (pinReleaseSourceDistance > 0f &&
                    Vector3.ProjectOnPlane(source - _pinSource, Vector3.up).magnitude >=
                        pinReleaseSourceDistance)
                {
                    Pinned = false;
                    _pinBlocked = true;
                    // 해제 순간에도 앵커가 튀지 않게 기준점을 현재 원본으로 넘김.
                    SourcePoint = source;
                }
                // 핀 동안 기준점을 매 프레임 갱신해 GetAnchor가 고정 앵커를 반환하게 함.
                if (Pinned) SourcePoint = source;
            }
        }

        private sealed class Pair
        {
            internal readonly int Primary;
            internal readonly Vector3 SourceDirection;
            internal readonly Vector3 Offset;
            internal Pair(int primary, Vector3 sourceDirection, Vector3 offset)
            { Primary = primary; SourceDirection = sourceDirection; Offset = offset; }
        }
    }
}
#endif
