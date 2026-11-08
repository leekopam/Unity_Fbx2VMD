using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed partial class HumanoidFootGrounding
    {
        private const float MaximumReleaseTargetShift = 0.01f;
        private const float MaximumReleaseKneeChange = 1f;
        private const float MaximumReleasePelvisChange = 0.001f;
        private readonly Dictionary<int, ReleaseSmoothingWindow> _releaseSmoothingWindows = new();

        private sealed class ReleaseSmoothingWindow
        {
            internal readonly Leg Leg;
            internal readonly int Start;
            internal readonly int End;
            internal readonly int Channel;
            internal readonly int Point;
            internal readonly RaycastHit Ground;
            internal readonly Matrix4x4 GroundMatrix;
            internal readonly Vector3 Direction;
            internal readonly float[] Offsets;

            internal ReleaseSmoothingWindow(Leg leg, int start, int end, int channel, int point,
                RaycastHit ground, Vector3 direction, float[] offsets)
            {
                Leg = leg;
                Start = start;
                End = end;
                Channel = channel;
                Point = point;
                Ground = ground;
                GroundMatrix = ground.collider.transform.localToWorldMatrix;
                Direction = direction;
                Offsets = offsets;
            }

            internal Vector3 GetShift(float frame)
            {
                int index = Mathf.Clamp(Mathf.FloorToInt(frame) - Start, 0, Offsets.Length - 2);
                return Direction * Mathf.SmoothStep(Offsets[index], Offsets[index + 1], frame - Start - index);
            }
        }

        internal void PrepareReleaseSmoothing(AnimationClip clip, Action<float> evaluate,
            HumanoidGroundResponse ground)
        {
            _releaseSmoothingWindows.Clear();
            try
            {
                PrepareLeg(_left);
                PrepareLeg(_right);
            }
            finally
            {
                RestoreAppliedPose();
                _left.ResetPreparationHistory();
                _right.ResetPreparationHistory();
                evaluate(0f);
            }

            void PrepareLeg(Leg leg)
            {
                for (int start = 0; start + 2 < leg.WeightFrameCount; start++)
                {
                    if (!leg.TryGetReleaseRange(start, out int end, out int channel, out var contact) ||
                        !leg.HasAbruptRelease(start, end, _sourceScaleRatio))
                        continue;
                    bool overlaps = false;
                    for (int frame = start; frame < end && !overlaps; frame++)
                        overlaps = _releaseSmoothingWindows.ContainsKey(frame) || _touchdownWindows.ContainsKey(frame) ||
                            _left.HasReleaseReference(frame) || _right.HasReleaseReference(frame);
                    if (overlaps)
                        continue;

                    var targets = new Vector3[end - start + 1];
                    Vector3 rawStart = Vector3.zero, rawEnd = Vector3.zero;
                    RaycastHit surface = default;
                    bool valid = true;
                    _left.ResetPreparationHistory();
                    _right.ResetPreparationHistory();
                    for (int frame = start; frame <= end; frame++)
                    {
                        RestoreAppliedPose();
                        float time = Mathf.Min(frame / _frameRate, clip.length);
                        evaluate(time);
                        if (frame == start) rawStart = leg.Foot.position;
                        if (frame == end) rawEnd = leg.Foot.position;
                        if (!TryApplyCore(time, ground, null, false, out _, out _) ||
                            (frame == start && !leg.TryReadReleaseSurface(out surface)))
                        {
                            valid = false;
                            break;
                        }
                        targets[frame - start] = leg.CaptureTarget().Position;
                    }

                    Vector3 direction = Vector3.ProjectOnPlane(rawEnd - rawStart, Vector3.up);
                    if (!valid || direction.sqrMagnitude < 0.000001f ||
                        !TryBuildReleaseOffsets(targets, direction.normalized, MaximumReleaseTargetShift, out var offsets))
                        continue;

                    var candidate = new ReleaseSmoothingWindow(leg, start, end, channel,
                        contact.FirstPoint, surface, direction.normalized, offsets);
                    if (!PreservesReleaseSmoothing(candidate, clip, evaluate, ground))
                        continue;
                    for (int frame = start; frame < end; frame++)
                        _releaseSmoothingWindows.Add(frame, candidate);
                }
            }
        }

        private bool PreservesReleaseSmoothing(ReleaseSmoothingWindow candidate, AnimationClip clip,
            Action<float> evaluate, HumanoidGroundResponse ground)
        {
            _left.ResetPreparationHistory();
            _right.ResetPreparationHistory();
            float maxIntegerBaseline = 0f, maxIntegerCandidate = 0f;
            float maxBaseline = 0f, maxCandidate = 0f;
            Vector3 previousBaseline = default, previousCandidate = default;
            Vector3 integerBaseline = default, integerCandidate = default;
            int startTick = candidate.Start * TouchdownSamplesPerFrame;
            int endTick = candidate.End * TouchdownSamplesPerFrame;
            for (int tick = startTick; tick <= endTick; tick++)
            {
                float frame = (float)tick / TouchdownSamplesPerFrame;
                bool integer = tick % TouchdownSamplesPerFrame == 0;
                bool checkBoundary = integer && tick > startTick;
                TouchdownPose beforeBaseline = default, beforeCandidate = default;
                if (checkBoundary && !Sample(frame - 0.001f, out beforeBaseline, out beforeCandidate))
                    return false;
                if (!Sample(frame, out var baseline, out var correctedPose) ||
                    (checkBoundary && !correctedPose.PreservesBoundary(beforeCandidate, baseline, beforeBaseline)))
                    return false;

                bool left = candidate.Leg == _left;
                Vector3 original = left ? baseline.LeftFoot : baseline.RightFoot;
                Vector3 corrected = left ? correctedPose.LeftFoot : correctedPose.RightFoot;
                if (tick > startTick)
                {
                    maxBaseline = Mathf.Max(maxBaseline, Vector3.Distance(original, previousBaseline));
                    maxCandidate = Mathf.Max(maxCandidate, Vector3.Distance(corrected, previousCandidate));
                }
                previousBaseline = original;
                previousCandidate = corrected;
                if (!integer) continue;
                if (tick > startTick)
                {
                    maxIntegerBaseline = Mathf.Max(maxIntegerBaseline, Vector3.Distance(original, integerBaseline));
                    maxIntegerCandidate = Mathf.Max(maxIntegerCandidate, Vector3.Distance(corrected, integerCandidate));
                }
                integerBaseline = original;
                integerCandidate = corrected;
            }
            return maxIntegerCandidate < maxIntegerBaseline - 0.001f &&
                maxCandidate <= maxBaseline + 0.00001f;

            bool Sample(float frame, out TouchdownPose baseline, out TouchdownPose corrected)
            {
                corrected = default;
                RestoreAppliedPose();
                float time = Mathf.Min(frame / _frameRate, clip.length);
                evaluate(time);
                if (!TryApplyCore(time, ground, null, false, out bool rejected, out baseline, candidate) || rejected)
                    return false;
                corrected = ReadTouchdownPose();
                return true;
            }
        }

        private bool TryApplyReleaseSmoothing(ReleaseSmoothingWindow window, float frame,
            HumanoidGroundResponse ground, Leg.TargetState leftTarget, Leg.TargetState rightTarget,
            float offset, Vector2 footOffsets, Vector2 clearance, out bool rejected)
        {
            rejected = false;
            Vector3 shift = window.GetShift(frame);
            if (shift.sqrMagnitude < 0.000000000001f)
                return true;

            float contactLimit = _humanScale * SoleClearancePerHumanScale;
            Vector3 target = (window.Leg == _left ? leftTarget.Position : rightTarget.Position) + shift;
            if (!window.Leg.CanApplyReleaseSmoothing(window, ground, target, contactLimit) ||
                !ReadRegions(_left, out var leftRegions) || !ReadRegions(_right, out var rightRegions))
            {
                rejected = true;
                return true;
            }

            TouchdownPose baseline = ReadTouchdownPose();
            bool usedPhysicalReach = UsedPhysicalReach;
            RestoreAppliedPose();
            _left.RestoreTarget(leftTarget);
            _right.RestoreTarget(rightTarget);
            if (window.Leg.TrySetTouchdownTarget(target) &&
                TryResolveReach(out float newOffset, out Vector2 newFootOffsets) &&
                TrySolveGroundPose(newOffset, newFootOffsets, out var newClearance) && RawGatePass &&
                SameRegionContact(clearance, newClearance, contactLimit) &&
                ReadRegions(_left, out var newLeft) && ReadRegions(_right, out var newRight) &&
                SameRegionContact(leftRegions, newLeft, contactLimit) &&
                SameRegionContact(rightRegions, newRight, contactLimit) &&
                Mathf.Abs(_hips.position.y - baseline.HipsY) <= MaximumReleasePelvisChange &&
                Mathf.Abs(_left.KneeAngle - baseline.Knees.x) <= MaximumReleaseKneeChange &&
                Mathf.Abs(_right.KneeAngle - baseline.Knees.y) <= MaximumReleaseKneeChange)
                return true;

            rejected = true;
            RestoreAppliedPose();
            _left.RestoreTarget(leftTarget);
            _right.RestoreTarget(rightTarget);
            UsedPhysicalReach = usedPhysicalReach;
            return TrySolveGroundPose(offset, footOffsets, out _);

            static bool ReadRegions(Leg leg, out Vector2 heights)
            {
                heights = default;
                return !leg.HasGround || leg.TryGetRegionHeights(out heights);
            }
        }

        // 원본 진행축에 한정해 최대 발 스텝을 줄일 수 있는 프레임별 오프셋을 찾음.
        private static bool TryBuildReleaseOffsets(Vector3[] targets, Vector3 direction,
            float bound, out float[] offsets)
        {
            offsets = null;
            if (targets.Length < 3 || bound <= 0f) return false;
            int last = targets.Length - 1;
            float maximum = 0f;
            var parallel = new float[last];
            var normalSquared = new float[last];
            for (int i = 0; i < last; i++)
            {
                Vector3 step = targets[i + 1] - targets[i];
                maximum = Mathf.Max(maximum, step.magnitude);
                parallel[i] = Vector3.Dot(step, direction);
                normalSquared[i] = Mathf.Max(0f, step.sqrMagnitude - parallel[i] * parallel[i]);
            }

            var intervals = new Vector2[targets.Length];
            var stepIntervals = new Vector2[last];
            float low = 0f, high = maximum;
            for (int iteration = 0; iteration < 24; iteration++)
            {
                float middle = (low + high) * 0.5f;
                if (TryFit(middle)) high = middle;
                else low = middle;
            }
            if (maximum - high < 0.001f || !TryFit(high))
                return false;

            offsets = new float[targets.Length];
            for (int i = last - 1; i > 0; i--)
            {
                float minimum = Mathf.Max(intervals[i].x, offsets[i + 1] - stepIntervals[i].y);
                float upper = Mathf.Min(intervals[i].y, offsets[i + 1] - stepIntervals[i].x);
                offsets[i] = Mathf.Clamp(0f, minimum, upper);
            }
            return true;

            bool TryFit(float stepLimit)
            {
                for (int i = 0; i < last; i++)
                {
                    float remaining = stepLimit * stepLimit - normalSquared[i];
                    if (remaining < 0f) return false;
                    float span = Mathf.Sqrt(remaining);
                    var stepRange = new Vector2(
                        Mathf.Max(-span, Mathf.Min(0f, parallel[i])) - parallel[i], span - parallel[i]);
                    stepIntervals[i] = stepRange;
                    float extent = i + 1 == last ? 0f : bound;
                    intervals[i + 1] = new Vector2(
                        Mathf.Max(-extent, intervals[i].x + stepRange.x),
                        Mathf.Min(extent, intervals[i].y + stepRange.y));
                    if (intervals[i + 1].x > intervals[i + 1].y) return false;
                }
                return true;
            }
        }

        private sealed partial class Leg
        {
            internal bool TryGetReleaseRange(int frame, out int end, out int channel,
                out HumanoidFootAnchorPlan.Sample contact)
            {
                end = frame + 1;
                channel = GetSingleSupportChannel(_weights[frame]);
                contact = default;
                if (channel < 0 || _weights[frame][channel] < 0.999f ||
                    GetSingleSupportChannel(_weights[end]) != channel ||
                    _weights[end][channel] >= 0.999f)
                    return false;

                contact = _plan.GetSample(channel, frame);
                while (end < _weights.Length && GetSingleSupportChannel(_weights[end]) == channel &&
                    _weights[end][channel] < _weights[end - 1][channel] &&
                    _plan.GetSample(channel, end).FirstPoint == contact.FirstPoint)
                    end++;
                // 재획득과 접점 교체는 기존 지지 계획을 따름.
                return contact.HasValue && end > frame + 1 && end < _weights.Length &&
                    _weights[end].x <= 0f && _weights[end].y <= 0f;
            }


            internal bool HasReleaseReference(int frame) => _releaseReferences.ContainsKey(frame);

            internal bool HasAbruptRelease(int start, int end, float scale)
            {
                for (int frame = start; frame < end; frame++)
                {
                    Vector3 step = _sourcePoints[0][frame + 1] - _sourcePoints[0][frame];
                    if (Vector3.ProjectOnPlane(step, Vector3.up).sqrMagnitude * scale * scale >
                        16f * MaximumReleaseTargetShift * MaximumReleaseTargetShift)
                        return true;
                }
                return false;
            }

            internal bool TryReadReleaseSurface(out RaycastHit surface)
            {
                surface = _ground;
                return HasGround && !Diagnostic.mixed_ground_collider && _ground.collider != null;
            }

            internal bool CanApplyReleaseSmoothing(ReleaseSmoothingWindow window, HumanoidGroundResponse ground,
                Vector3 target, float contactLimit) =>
                HasGround && _support > 0f && _support < 0.999f && GetSingleSupportChannel(_activeWeights) == window.Channel &&
                _activeContacts[window.Channel].FirstPoint == window.Point && !Diagnostic.mixed_ground_collider &&
                MatchesReferenceGround(_ground, window.Ground, contactLimit) &&
                _ground.collider.transform.localToWorldMatrix == window.GroundMatrix &&
                ground.TryFindGround(target, out var hit) && MatchesReferenceGround(hit, window.Ground, contactLimit);
        }
    }
}
