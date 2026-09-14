#if UNITY_EDITOR
using RootMotion.FinalIK;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 실제 지면 아래의 Avatar 발 하단 기준점을 본 길이 변화 없이 올림.
    /// </summary>
    internal sealed class EditorHumanoidGroundResponse
    {
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Transform _root;
        private float _probeDistance;
        private Leg _left;
        private Leg _right;

        internal void Initialize(Animator animator)
        {
            Clear();
            _root = animator.transform;
            _probeDistance = Mathf.Max(animator.humanScale, 0.01f);
            _left = new Leg(animator, HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.LeftToes, animator.leftFeetBottomHeight);
            _right = new Leg(animator, HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
                HumanBodyBones.RightToes, animator.rightFeetBottomHeight);
        }

        internal void Apply(Vector3 leftBendNormal, Vector3 rightBendNormal)
        {
            if (_root == null)
                return;

            // 편집 중 이동한 Collider도 같은 Seek의 지면 질의에 반영함.
            Physics.SyncTransforms();
            Apply(_left, leftBendNormal);
            Apply(_right, rightBendNormal);
        }

        internal void Clear()
        {
            _root = null;
            _left = default;
            _right = default;
            _probeDistance = 0f;
        }

        private void Apply(Leg leg, Vector3 bendNormal)
        {
            if (leg.Upper == null || leg.Lower == null || leg.Foot == null ||
                !TryFindGroundHeight(leg.Foot.position, out float groundHeight))
                return;

            float targetHeight = groundHeight + leg.BottomHeight;
            if (targetHeight <= leg.Foot.position.y + 0.00001f)
                return;

            Vector3 target = leg.Foot.position;
            target.y = targetHeight;
            // 기존 IK가 다리를 거의 펴면 외적으로 구한 평면이 흔들리므로 원본 평가의 굽힘 방향을 우선함.
            if (bendNormal.sqrMagnitude < 0.00000001f)
                bendNormal = Vector3.Cross(
                    leg.Lower.position - leg.Upper.position,
                    leg.Foot.position - leg.Lower.position);
            if (bendNormal.sqrMagnitude < 0.00000001f)
                bendNormal = _root.right;

            Quaternion footRotation = leg.Foot.rotation;
            Quaternion toeRotation = leg.Toes == null ? Quaternion.identity : leg.Toes.rotation;
            // 지면이 멀면 solver의 도달 한계를 사용하며 본 위치나 scale을 늘리지 않음.
            IKSolverTrigonometric.Solve(
                leg.Upper, leg.Lower, leg.Foot, target, bendNormal, 1f);
            leg.Foot.rotation = footRotation;
            if (leg.Toes != null)
                leg.Toes.rotation = toeRotation;
        }

        private bool TryFindGroundHeight(Vector3 footPosition, out float height)
        {
            bool found = TryFindGround(footPosition, out RaycastHit hit);
            height = found ? hit.point.y : 0f;
            return found;
        }

        internal bool TryFindGround(Vector3 footPosition, out RaycastHit ground)
        {
            ground = default;
            if (_root == null)
                return false;
            var ray = new Ray(footPosition + Vector3.up * (_probeDistance * 0.5f), Vector3.down);
            int count = Physics.RaycastNonAlloc(ray, _hits, _probeDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = _hits;
            // 버퍼 포화 시 누락된 가까운 지면을 임의로 무시하지 않음.
            if (count == _hits.Length)
            {
                hits = Physics.RaycastAll(ray, _probeDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.transform == null || hit.transform.IsChildOf(_root) ||
                    _root.IsChildOf(hit.transform) || hit.normal.y < 0.5f ||
                    hit.distance >= nearest)
                    continue;

                nearest = hit.distance;
                ground = hit;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private readonly struct Leg
        {
            internal Leg(Animator animator, HumanBodyBones upper, HumanBodyBones lower,
                HumanBodyBones foot, HumanBodyBones toes, float bottomHeight)
            {
                Upper = animator.GetBoneTransform(upper);
                Lower = animator.GetBoneTransform(lower);
                Foot = animator.GetBoneTransform(foot);
                Toes = animator.GetBoneTransform(toes);
                BottomHeight = bottomHeight;
            }

            internal Transform Upper { get; }
            internal Transform Lower { get; }
            internal Transform Foot { get; }
            internal Transform Toes { get; }
            internal float BottomHeight { get; }
        }
    }
}
#endif
