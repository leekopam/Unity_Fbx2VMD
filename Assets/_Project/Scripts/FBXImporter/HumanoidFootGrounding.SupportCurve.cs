using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed partial class HumanoidFootGrounding
    {
        internal float SupportCurveContactLimitMm => _humanScale * SoleClearancePerHumanScale * 1000f;

        internal readonly struct SupportCurveWindow
        {
            internal SupportCurveWindow(int side, int start, int full, int fadeEnd, int supportEnd, int[] boundaries)
            {
                Side = side;
                Start = start;
                Full = full;
                FadeEnd = fadeEnd;
                SupportEnd = supportEnd;
                Boundaries = boundaries;
            }

            internal int Side { get; }
            internal int Start { get; }
            internal int Full { get; }
            internal int FadeEnd { get; }
            internal int SupportEnd { get; }
            internal int[] Boundaries { get; }
        }

        internal IEnumerable<SupportCurveWindow> GetSupportCurveWindows()
        {
            if (!IsPrepared) yield break;
            foreach (var window in ReadWindows(_left, _right, 0)) yield return window;
            foreach (var window in ReadWindows(_right, _left, 1)) yield return window;
        }

        private IEnumerable<SupportCurveWindow> ReadWindows(Leg entering, Leg supporting, int side)
        {
            const int MaximumInitialHoldFrames = 15;
            // 국소 곡선은 다음 역할 전환보다 먼저 0으로 복귀함.
            const int TransitionMarginFrames = 2;
            for (int start = 0; start + 2 < entering.WeightFrameCount; start++)
            {
                if (_touchdownWindows.ContainsKey(start) ||
                    !entering.TryFindTouchdown(start, supporting, out int full, out int channel))
                    continue;

                int point = entering.ReadSupportRole(full).Point(channel);
                int end = full;
                var otherRole = supporting.ReadSupportRole(full);
                var boundaries = new List<int>();
                while (end + 1 < entering.WeightFrameCount && !HasOtherCorrection(end + 1) &&
                       entering.ReadSupportRole(end + 1).IsSameFullContact(channel, point))
                {
                    end++;
                    var nextRole = supporting.ReadSupportRole(end);
                    if (!otherRole.Equals(nextRole)) boundaries.Add(end);
                    otherRole = nextRole;
                }
                int firstBoundary = boundaries.Count == 0 ? end + 1 : boundaries[0];
                int fadeEnd = Math.Min(full + MaximumInitialHoldFrames, firstBoundary - TransitionMarginFrames);
                if (fadeEnd <= full) continue;
                bool overlaps = false;
                for (int frame = start; frame <= fadeEnd && !overlaps; frame++) overlaps = HasOtherCorrection(frame);
                if (!overlaps) yield return new SupportCurveWindow(side, start, full, fadeEnd, end, boundaries.ToArray());
            }
        }

        private bool HasOtherCorrection(int frame) => _touchdownWindows.ContainsKey(frame) ||
            _releaseSmoothingWindows.ContainsKey(frame) || _left.HasReleaseReference(frame) || _right.HasReleaseReference(frame);

        private readonly struct SupportRole : IEquatable<SupportRole>
        {
            private readonly int _rear;
            private readonly int _front;
            private readonly int _rearPoint;
            private readonly int _frontPoint;

            internal SupportRole(int rear, int front, int rearPoint, int frontPoint)
            {
                _rear = rear;
                _front = front;
                _rearPoint = rearPoint;
                _frontPoint = frontPoint;
            }

            internal int Point(int channel) => channel == 0 ? _rearPoint : _frontPoint;

            internal bool IsSameFullContact(int channel, int point) => channel == 0
                ? _rear == 2 && _front == 0 && _rearPoint == point
                : _front == 2 && _rear == 0 && _frontPoint == point;

            public bool Equals(SupportRole other) => _rear == other._rear && _front == other._front &&
                _rearPoint == other._rearPoint && _frontPoint == other._frontPoint;
        }

        private sealed partial class Leg
        {
            internal SupportRole ReadSupportRole(int frame)
            {
                var weights = _weights[frame];
                int rear = Category(weights.x);
                int front = Category(weights.y);
                return new SupportRole(rear, front, rear > 0 ? _plan.GetSample(0, frame).FirstPoint : -1,
                    front > 0 ? _plan.GetSample(1, frame).FirstPoint : -1);

                static int Category(float weight) => weight <= 0f ? 0 : weight < .999f ? 1 : 2;
            }
        }
    }
}
