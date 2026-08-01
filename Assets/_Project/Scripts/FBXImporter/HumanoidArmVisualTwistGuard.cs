using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.Modules.FBXImporter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(26000)]
    public class HumanoidArmVisualTwistGuard : MonoBehaviour
    {
        [Header("YYB Arm Visual Twist Guard")]
        public bool enableVisualTwistGuard = true;
        [Range(0f, 1f)] public float upperArmInfluence = 0.35f;
        [Range(0f, 1f)] public float forearmInfluence = 0.75f;
        [Range(0f, 120f)] public float upperArmMaxDegrees = 45f;
        [Range(0f, 120f)] public float forearmMaxDegrees = 75f;
        public bool logConfiguration = false;

        private readonly List<SegmentCorrection> _segments = new List<SegmentCorrection>();
        private readonly List<Transform> _controlledTransforms = new List<Transform>();
        private Animator _animator;
        private bool _configured;
        private bool _warningLogged;

        public IReadOnlyList<Transform> ControlledTransforms => _controlledTransforms;

        private sealed class SegmentCorrection
        {
            public Transform Source;
            public Transform End;
            public Quaternion BaselineRelativeRotation;
            public Vector3 LocalAxis;
            public float Influence;
            public float MaxDegrees;
            public readonly List<NodeCorrection> Nodes = new List<NodeCorrection>();
        }

        private struct NodeCorrection
        {
            public Transform Transform;
            public Quaternion BaselineLocalRotation;
            public Quaternion BaselineSourceRelativeRotation;
            public float Weight;

            public NodeCorrection(
                Transform transform,
                Quaternion baselineLocalRotation,
                Quaternion baselineSourceRelativeRotation,
                float weight)
            {
                Transform = transform;
                BaselineLocalRotation = baselineLocalRotation;
                BaselineSourceRelativeRotation = baselineSourceRelativeRotation;
                Weight = weight;
            }
        }

        private void LateUpdate()
        {
            if (!enableVisualTwistGuard || !_configured)
            {
                return;
            }

            foreach (SegmentCorrection segment in _segments)
            {
                ApplySegment(segment);
            }
        }

        public bool Configure(
            Animator animator,
            float nextUpperArmInfluence,
            float nextForearmInfluence,
            float nextUpperArmMaxDegrees,
            float nextForearmMaxDegrees,
            bool nextLogConfiguration)
        {
            _animator = animator != null ? animator : GetComponent<Animator>();
            upperArmInfluence = Mathf.Clamp01(nextUpperArmInfluence);
            forearmInfluence = Mathf.Clamp01(nextForearmInfluence);
            upperArmMaxDegrees = Mathf.Clamp(nextUpperArmMaxDegrees, 0f, 120f);
            forearmMaxDegrees = Mathf.Clamp(nextForearmMaxDegrees, 0f, 120f);
            logConfiguration = nextLogConfiguration;
            _segments.Clear();
            _controlledTransforms.Clear();
            _configured = false;
            _warningLogged = false;

            if (!enableVisualTwistGuard || _animator == null || !_animator.isHuman)
            {
                return false;
            }

            int configuredCount = 0;
            configuredCount += ConfigureSegment(
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                upperArmInfluence,
                upperArmMaxDegrees,
                "joint_LeftArmTwist");
            configuredCount += ConfigureSegment(
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                upperArmInfluence,
                upperArmMaxDegrees,
                "joint_RightArmTwist");
            configuredCount += ConfigureSegment(
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                forearmInfluence,
                forearmMaxDegrees,
                "joint_LeftHandTwist",
                "joint_LeftHandTwist1",
                "!joint_LeftHandTwist2",
                "joint_LeftHandTwist3");
            configuredCount += ConfigureSegment(
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                forearmInfluence,
                forearmMaxDegrees,
                "joint_RightHandTwist",
                "joint_RightHandTwist1",
                "!joint_RightHandTwist2",
                "joint_RightHandTwist3");

            _configured = configuredCount > 0;
            enabled = _configured;

            if (logConfiguration)
            {
                Debug.Log($"[HumanoidArmVisualTwistGuard] 보조본 회전 분배 구성 완료. segments={_segments.Count}, nodes={configuredCount}");
            }

            return _configured;
        }

        public void DisableCorrection()
        {
            enableVisualTwistGuard = false;
            _configured = false;
            _segments.Clear();
            _controlledTransforms.Clear();
        }

        private int ConfigureSegment(
            HumanBodyBones sourceBone,
            HumanBodyBones endBone,
            float influence,
            float maxDegrees,
            params string[] nodeNames)
        {
            Transform source = _animator.GetBoneTransform(sourceBone);
            Transform end = _animator.GetBoneTransform(endBone);
            if (source == null || end == null || nodeNames == null || nodeNames.Length == 0)
            {
                return 0;
            }

            var nodes = new List<Transform>();
            foreach (string nodeName in nodeNames)
            {
                Transform node = FindDescendantByName(source, nodeName);
                if (node != null && !nodes.Contains(node))
                {
                    nodes.Add(node);
                }
            }

            if (nodes.Count == 0)
            {
                return 0;
            }

            Vector3 localEnd = source.InverseTransformPoint(end.position);
            float length = localEnd.magnitude;
            Vector3 localDirection = length > 0.0001f ? localEnd / length : Vector3.right;

            var segment = new SegmentCorrection
            {
                Source = source,
                End = end,
                BaselineRelativeRotation = GetRelativeRotation(source, end),
                LocalAxis = localDirection,
                Influence = Mathf.Clamp01(influence),
                MaxDegrees = Mathf.Clamp(maxDegrees, 0f, 120f)
            };

            nodes.Sort((a, b) =>
            {
                float wa = CalculateNodeWeight(source, a, localDirection, length);
                float wb = CalculateNodeWeight(source, b, localDirection, length);
                return wa.CompareTo(wb);
            });

            foreach (Transform node in nodes)
            {
                float weight = CalculateNodeWeight(source, node, localDirection, length);
                Quaternion baselineSourceRelative = Quaternion.Inverse(source.rotation) * node.rotation;
                segment.Nodes.Add(new NodeCorrection(node, node.localRotation, baselineSourceRelative, weight));
                AddControlledTransform(node);
            }

            _segments.Add(segment);
            return segment.Nodes.Count;
        }

        private void AddControlledTransform(Transform targetTransform)
        {
            if (targetTransform != null && !_controlledTransforms.Contains(targetTransform))
            {
                _controlledTransforms.Add(targetTransform);
            }
        }

        private void ApplySegment(SegmentCorrection segment)
        {
            if (segment == null || segment.Source == null || segment.End == null || segment.Nodes.Count == 0)
            {
                return;
            }

            // 상완/전완 segment가 기준 자세에서 얼마나 비틀렸는지 상대 회전 차이로 구합니다.
            Quaternion currentRelative = GetRelativeRotation(segment.Source, segment.End);
            Quaternion delta = Quaternion.Inverse(segment.BaselineRelativeRotation) * currentRelative;

            // 팔 방향 전체 회전이 아니라 길이축 기준 twist 성분만 뽑아 소매 보조본에 나눠 줍니다.
            delta = ExtractTwist(delta, segment.LocalAxis);
            delta = LimitRotation(delta, segment.MaxDegrees);

            if (!IsFinite(delta))
            {
                LogWarningOnce("계산된 보조본 회전값이 유효하지 않아 이 프레임의 시각 보정을 건너뜁니다.");
                return;
            }

            foreach (NodeCorrection node in segment.Nodes)
            {
                if (node.Transform == null)
                {
                    continue;
                }

                // 보조본이 팔 길이 방향으로 어디에 있는지에 따라 twist 영향도를 분산합니다.
                float weight = Mathf.Clamp01(node.Weight * segment.Influence);
                Quaternion distributed = Quaternion.Slerp(Quaternion.identity, delta, weight);
                ApplySourceRelativeRotation(segment.Source, node, distributed);
            }
        }

        private static void ApplySourceRelativeRotation(
            Transform source,
            NodeCorrection node,
            Quaternion distributed)
        {
            if (source == null || node.Transform == null)
            {
                return;
            }

            // source 본 기준 상대 자세를 유지한 채 분산된 twist만 얹어 최종 월드 회전을 만듭니다.
            Quaternion desiredWorldRotation = source.rotation * distributed * node.BaselineSourceRelativeRotation;
            if (!IsFinite(desiredWorldRotation))
            {
                node.Transform.localRotation = node.BaselineLocalRotation;
                return;
            }

            if (node.Transform.parent == null)
            {
                node.Transform.rotation = desiredWorldRotation;
                return;
            }

            Quaternion parentInverse = Quaternion.Inverse(node.Transform.parent.rotation);
            node.Transform.localRotation = parentInverse * desiredWorldRotation;
        }

        private static Quaternion GetRelativeRotation(Transform source, Transform end)
        {
            return Quaternion.Inverse(source.rotation) * end.rotation;
        }

        private static float CalculateNodeWeight(Transform source, Transform node, Vector3 localDirection, float segmentLength)
        {
            if (source == null || node == null || segmentLength <= 0.0001f)
            {
                return 0f;
            }

            Vector3 localNode = source.InverseTransformPoint(node.position);
            float projected = Vector3.Dot(localNode, localDirection);
            return Mathf.Clamp01(projected / segmentLength);
        }

        private static Quaternion LimitRotation(Quaternion rotation, float maxDegrees)
        {
            if (maxDegrees <= 0f)
            {
                return Quaternion.identity;
            }

            rotation = Normalize(rotation);
            rotation.ToAngleAxis(out float angle, out Vector3 axis);
            if (!IsFinite(axis) || axis.sqrMagnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            if (angle > 180f)
            {
                angle -= 360f;
            }

            float limitedAngle = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
            return Quaternion.AngleAxis(limitedAngle, axis.normalized);
        }

        private static Quaternion ExtractTwist(Quaternion rotation, Vector3 localAxis)
        {
            if (!IsFinite(rotation) || !IsFinite(localAxis) || localAxis.sqrMagnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            rotation = Normalize(rotation);
            Vector3 axis = localAxis.normalized;
            Vector3 vector = new Vector3(rotation.x, rotation.y, rotation.z);
            Vector3 projected = Vector3.Project(vector, axis);
            return Normalize(new Quaternion(projected.x, projected.y, projected.z, rotation.w));
        }

        private static Quaternion Normalize(Quaternion rotation)
        {
            float length = Mathf.Sqrt(
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w);

            if (length <= 0.000001f)
            {
                return Quaternion.identity;
            }

            float inv = 1f / length;
            return new Quaternion(rotation.x * inv, rotation.y * inv, rotation.z * inv, rotation.w * inv);
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && IsMatchingBoneName(child.name, targetName))
                {
                    return child;
                }
            }

            return null;
        }

        private static bool IsMatchingBoneName(string boneName, string targetName)
        {
            if (string.IsNullOrEmpty(boneName) || string.IsNullOrEmpty(targetName))
            {
                return false;
            }

            return boneName == targetName ||
                   boneName.EndsWith("." + targetName, System.StringComparison.Ordinal) ||
                   boneName.EndsWith(targetName, System.StringComparison.Ordinal);
        }

        private void LogWarningOnce(string message)
        {
            if (_warningLogged || !logConfiguration)
            {
                _warningLogged = true;
                return;
            }

            Debug.LogWarning($"[HumanoidArmVisualTwistGuard] {message}");
            _warningLogged = true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
