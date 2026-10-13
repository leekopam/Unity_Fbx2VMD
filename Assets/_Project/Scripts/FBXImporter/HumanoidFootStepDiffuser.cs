using RootMotion.FinalIK;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 소스 클립에 굽어진 단발 불연속으로 정지 상태의 발이 한 프레임에 크게
    /// 순간이동할 때, 그 이동을 수 프레임에 걸쳐 확산해 시각적 스냅을 완화함.
    /// 직전 프레임도 움직이는 연속 고속 동작(킥 등)은 발화하지 않아
    /// 원본 충실도를 유지함.
    /// </summary>
    internal sealed class HumanoidFootStepDiffuser
    {
        // 직전 프레임의 발 이동이 이 이하이면 정지(지지 또는 공중 정지) 상태로 간주함.
        // 지지 발의 정상 미세 이동은 수 mm이므로 여유를 둠.
        private const float PlantedDeltaMeters = 0.02f;
        // 정지 발의 단발 이동이 이 이상이면 순간이동으로 판정함.
        // 측정된 팝은 0.09~0.25m, 고속 발차기의 발화 프레임은 약 0.077m라
        // 그 사이의 0.08로 두어 실제 킥 개시는 그대로 두고 순간이동만 잡음.
        private const float TriggerStepMeters = 0.08f;
        // 확산 속도 — 150mm 스텝을 60fps 기준 약 7프레임에 소화함.
        private const float DiffuseSpeedMetersPerSecond = 1.5f;
        // 드라이버와 같은 탐색 판정 — 재생 위치가 크게 건너뛰면 이전 상태를 버림.
        private const float SeekFrameMargin = 1.5f;

        private FootTrack _left;
        private FootTrack _right;
        private float _lastTimeSeconds;
        private bool _hasLastTime;

        internal void Initialize(Animator animator)
        {
            Clear();
            _left = new FootTrack(animator, HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes);
            _right = new FootTrack(animator, HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes);
        }

        internal void Clear()
        {
            _left = default;
            _right = default;
            _lastTimeSeconds = 0f;
            _hasLastTime = false;
        }

        internal bool TryApply(float timeSeconds, float frameRate)
        {
            if (_left.Foot == null || _right.Foot == null)
                return false;

            float safeFrameRate = Mathf.Max(frameRate, 1f);
            bool jumped = !_hasLastTime ||
                Mathf.Abs(timeSeconds - _lastTimeSeconds) >
                SeekFrameMargin / safeFrameRate;
            float dt = jumped ? 0f : Mathf.Max(0f, timeSeconds - _lastTimeSeconds);
            _lastTimeSeconds = timeSeconds;
            _hasLastTime = true;
            if (jumped)
            {
                _left.Reset();
                _right.Reset();
            }

            bool applied = Apply(ref _left, dt) | Apply(ref _right, dt);
            return applied;
        }

        private static bool Apply(ref FootTrack track, float dt)
        {
            Vector3 raw = track.Foot.position;
            float rawDelta = track.HasState
                ? (raw - track.PrevRaw).magnitude
                : 0f;

            // 정지 상태의 발이 임계 이상 순간이동하면 그 차이를 잔여로 보유해
            // 이후 프레임에 걸쳐 상쇄시킴 — 출력은 원자세로부터 미끄러지듯 이동함.
            // dt=0인 동일 시각 재평가(보정 프리뷰 등)에서는 판정하지 않음 — 그 경로는
            // 실제 프레임 전이가 아니라 같은 자세의 재적용이라 debt가 소진되지 않음.
            if (dt > 0f && track.HasState &&
                track.PrevRawDelta <= PlantedDeltaMeters &&
                rawDelta >= TriggerStepMeters)
            {
                track.Debt += track.PrevOut - raw;
            }

            track.PrevRaw = raw;
            // 리셋 직후 첫 전이는 직전 이동 이력이 없으므로 "정지"로 시딩하지 않음 —
            // 탐색 직후 실제 고속 이동 중인 발을 순간이동으로 오판해 확산하는 것을 막음.
            track.PrevRawDelta = track.HasState ? rawDelta : float.MaxValue;
            track.HasState = true;

            if (track.Debt.sqrMagnitude < 0.000000001f)
            {
                track.PrevOut = raw;
                track.Debt = Vector3.zero;
                return false;
            }

            track.Debt = Vector3.MoveTowards(track.Debt, Vector3.zero,
                DiffuseSpeedMetersPerSecond * dt);
            Vector3 target = raw + track.Debt;
            if ((target - raw).sqrMagnitude < 0.000000001f)
            {
                track.PrevOut = raw;
                return false;
            }

            // 상승 클램프와 같은 기준 — 자세 cross가 안정적이면 그 방향이 정답이고
            // 무릎이 거의 펴져 퇴화할 때만 발 우측을 폴백으로 사용함.
            Vector3 bendNormal = Vector3.Cross(
                track.Lower.position - track.Upper.position,
                track.Foot.position - track.Lower.position);
            if (bendNormal.sqrMagnitude < 0.00000001f)
                bendNormal = track.Foot.right;

            Quaternion footRotation = track.Foot.rotation;
            Quaternion toeRotation = track.Toes == null
                ? Quaternion.identity : track.Toes.rotation;
            IKSolverTrigonometric.Solve(
                track.Upper, track.Lower, track.Foot, target, bendNormal, 1f);
            track.Foot.rotation = footRotation;
            if (track.Toes != null)
                track.Toes.rotation = toeRotation;

            track.PrevOut = target;
            return true;
        }

        private struct FootTrack
        {
            internal FootTrack(Animator animator, HumanBodyBones upper,
                HumanBodyBones lower, HumanBodyBones foot, HumanBodyBones toes)
            {
                Upper = animator.GetBoneTransform(upper);
                Lower = animator.GetBoneTransform(lower);
                Foot = animator.GetBoneTransform(foot);
                Toes = animator.GetBoneTransform(toes);
                PrevRaw = Vector3.zero;
                PrevOut = Vector3.zero;
                Debt = Vector3.zero;
                PrevRawDelta = 0f;
                HasState = false;
            }

            internal Transform Upper { get; }
            internal Transform Lower { get; }
            internal Transform Foot { get; }
            internal Transform Toes { get; }
            internal Vector3 PrevRaw;
            internal Vector3 PrevOut;
            internal Vector3 Debt;
            internal float PrevRawDelta;
            internal bool HasState;

            internal void Reset()
            {
                PrevRaw = PrevOut = Debt = Vector3.zero;
                PrevRawDelta = 0f;
                HasState = false;
            }
        }
    }
}
