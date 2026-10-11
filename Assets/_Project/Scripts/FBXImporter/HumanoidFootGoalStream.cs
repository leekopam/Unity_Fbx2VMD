using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// ghost(원본) 발의 프레임별 IK 목표 궤적을 기록함. 관측 전용 — 출력을 바꾸지 않음.
    /// 좌표·회전은 ghost 루트 기준 상대 공간으로 정규화해 대상 루트 이동과 독립시킴.
    /// </summary>
    internal sealed class HumanoidFootGoalStream
    {
        internal struct GoalSample
        {
            internal int Frame;
            internal bool IsLeft;
            // 루트 기준 상대 공간의 발목 위치·회전·발끝 위치임.
            internal Vector3 RootSpaceFootPosition;
            internal Quaternion RootSpaceFootRotation;
            internal Vector3 RootSpaceToesPosition;
            // 발목-발끝 루트공간 높이차. 양수면 발목이 발끝 위(롤 스탠스)임.
            internal float SoleOffset;
        }

        private readonly List<GoalSample> _samples = new List<GoalSample>();

        internal IReadOnlyList<GoalSample> Samples => _samples;

        // Animator 없이도 단위 테스트할 수 있게 뼈대 Transform만 받음.
        internal void Record(int frame, bool isLeft,
            Transform root, Transform foot, Transform toes)
        {
            if (root == null || foot == null)
            {
                throw new ArgumentNullException(
                    root == null ? nameof(root) : nameof(foot),
                    "goal 스트림에는 루트와 발목 Transform이 필요합니다.");
            }

            Record(frame, isLeft, root.position, root.rotation,
                foot.position, foot.rotation,
                toes == null ? (Vector3?)null : toes.position);
        }

        // 월드값 직접 입력 — 루트는 매 프레임 움직이는 골반 기준으로도 쓸 수 있음.
        internal void Record(int frame, bool isLeft,
            Vector3 rootPosition, Quaternion rootRotation,
            Vector3 footPosition, Quaternion footRotation, Vector3? toesPosition)
        {
            Quaternion inverseRootRotation = Quaternion.Inverse(rootRotation);
            Vector3 rootSpaceFoot = inverseRootRotation *
                (footPosition - rootPosition);
            Vector3 rootSpaceToes = toesPosition.HasValue
                ? inverseRootRotation * (toesPosition.Value - rootPosition)
                : rootSpaceFoot;
            _samples.Add(new GoalSample
            {
                Frame = frame,
                IsLeft = isLeft,
                RootSpaceFootPosition = rootSpaceFoot,
                RootSpaceFootRotation = inverseRootRotation * footRotation,
                RootSpaceToesPosition = rootSpaceToes,
                SoleOffset = rootSpaceFoot.y - rootSpaceToes.y,
            });
        }

        // pin 구간 전용 — 목표의 발끝이 지면 아래로 내려가지 않도록
        // 부족분만큼 발목·발끝을 함께 들어올림. 지면 위 목표는 건드리지 않음.
        internal static GoalSample SnapAboveGround(GoalSample goal, float groundY)
        {
            float lift = groundY - goal.RootSpaceToesPosition.y;
            if (lift <= 0f)
            {
                return goal;
            }

            goal.RootSpaceToesPosition.y += lift;
            goal.RootSpaceFootPosition.y += lift;
            goal.SoleOffset =
                goal.RootSpaceFootPosition.y - goal.RootSpaceToesPosition.y;
            return goal;
        }

        // join-goal-columns.mjs가 소비하는 한 줄 JSON 형식으로 씀.
        internal void WriteJsonl(TextWriter writer)
        {
            foreach (GoalSample sample in _samples)
            {
                Vector3 euler = sample.RootSpaceFootRotation.eulerAngles;
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{{\"frame\":{0},\"side\":\"{1}\",\"pos\":[{2},{3},{4}]," +
                    "\"rot\":[{5},{6},{7}],\"toe\":[{8},{9},{10}]," +
                    "\"sole_offset\":{11}}}",
                    sample.Frame,
                    sample.IsLeft ? "left" : "right",
                    sample.RootSpaceFootPosition.x,
                    sample.RootSpaceFootPosition.y,
                    sample.RootSpaceFootPosition.z,
                    euler.x, euler.y, euler.z,
                    sample.RootSpaceToesPosition.x,
                    sample.RootSpaceToesPosition.y,
                    sample.RootSpaceToesPosition.z,
                    sample.SoleOffset));
            }
        }
    }
}
