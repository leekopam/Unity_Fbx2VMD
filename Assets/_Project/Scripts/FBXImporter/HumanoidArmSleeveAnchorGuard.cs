using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fbx2Vmd.FBXImporter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(25500)]
    public class HumanoidArmSleeveAnchorGuard : MonoBehaviour
    {
        [Header("YYB Arm Sleeve Anchor Guard")]
        [Tooltip("YYB 소매/어깨 보조본이 상완 본과 따로 놀아 소매가 어깨에서 무너져 보이는 현상을 줄입니다.")]
        [FormerlySerializedAs("enableSleeveAnchor")]
        [SerializeField] private bool _enableSleeveAnchor= true;
        public bool enableSleeveAnchor { get => _enableSleeveAnchor; set => _enableSleeveAnchor = value; }

        [Tooltip("소매 상단 보조본이 상완 회전을 따라가는 강도입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("armAnchorInfluence")]
        [SerializeField] private float _armAnchorInfluence= 0.85f;
        public float armAnchorInfluence { get => _armAnchorInfluence; private set => _armAnchorInfluence = value; }

        [Tooltip("어깨 캡 보조본이 상완 회전을 따라가는 강도입니다. MMD4Mecanim PPH와 겹치지 않도록 기본값은 0입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("shoulderCapInfluence")]
        [SerializeField] private float _shoulderCapInfluence= 0f;
        public float shoulderCapInfluence { get => _shoulderCapInfluence; private set => _shoulderCapInfluence = value; }

        [Tooltip("보조본이 한 프레임에 따라갈 수 있는 최대 회전각입니다.")]
        [Range(0f, 120f)]
        [FormerlySerializedAs("maxFollowDegrees")]
        [SerializeField] private float _maxFollowDegrees= 85f;
        public float maxFollowDegrees { get => _maxFollowDegrees; private set => _maxFollowDegrees = value; }

        [FormerlySerializedAs("logConfiguration")]
        [SerializeField] private bool _logConfiguration= false;
        public bool logConfiguration { get => _logConfiguration; private set => _logConfiguration = value; }

        private readonly List<AnchorCorrection> _anchors = new List<AnchorCorrection>();
        private readonly List<Transform> _controlledTransforms = new List<Transform>();
        private Animator _animator;
        private bool _configured;
        private bool _warningLogged;

        public IReadOnlyList<Transform> ControlledTransforms => _controlledTransforms;

        internal static Transform FindLegacyArmAnchor(Animator animator, HumanBodyBones upperArm)
        {
            string anchorName;
            switch (upperArm)
            {
                case HumanBodyBones.LeftUpperArm:
                    anchorName = "joint_LeftArmM";
                    break;
                case HumanBodyBones.RightUpperArm:
                    anchorName = "joint_RightArmM";
                    break;
                default:
                    return null;
            }

            // 기존 리그의 명시적 이름만 해석하며 미지 모델의 weighted sibling을 추측하지 않음.
            Transform match = null;
            foreach (Transform candidate in animator.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name != anchorName &&
                    !candidate.name.EndsWith("." + anchorName, System.StringComparison.Ordinal))
                    continue;
                if (match != null)
                    return null;
                match = candidate;
            }
            return match;
        }

        private struct AnchorCorrection
        {
            public Transform Source;
            public Transform Node;
            public Quaternion BaselineLocalRotation;
            public Quaternion BaselineSourceRelativeRotation;
            public float Influence;

            public AnchorCorrection(
                Transform source,
                Transform node,
                Quaternion baselineLocalRotation,
                Quaternion baselineSourceRelativeRotation,
                float influence)
            {
                Source = source;
                Node = node;
                BaselineLocalRotation = baselineLocalRotation;
                BaselineSourceRelativeRotation = baselineSourceRelativeRotation;
                Influence = influence;
            }
        }

        private void LateUpdate()
        {
            if (!enableSleeveAnchor || !_configured)
            {
                return;
            }

            foreach (AnchorCorrection anchor in _anchors)
            {
                ApplyAnchor(anchor);
            }
        }

        public bool Configure(
            Animator animator,
            float nextArmAnchorInfluence,
            float nextShoulderCapInfluence,
            float nextMaxFollowDegrees,
            bool nextLogConfiguration)
        {
            _animator = animator != null ? animator : GetComponent<Animator>();
            armAnchorInfluence = Mathf.Clamp01(nextArmAnchorInfluence);
            shoulderCapInfluence = Mathf.Clamp01(nextShoulderCapInfluence);
            maxFollowDegrees = Mathf.Clamp(nextMaxFollowDegrees, 0f, 120f);
            logConfiguration = nextLogConfiguration;
            _anchors.Clear();
            _controlledTransforms.Clear();
            _configured = false;
            _warningLogged = false;

            if (!enableSleeveAnchor || _animator == null || !_animator.isHuman)
            {
                return false;
            }

            int configuredCount = 0;
            configuredCount += ConfigureArm(
                HumanBodyBones.LeftUpperArm,
                "joint_LeftArmM",
                "!joint_LeftShoulderC");
            configuredCount += ConfigureArm(
                HumanBodyBones.RightUpperArm,
                "joint_RightArmM",
                "!joint_RightShoulderC");

            _configured = configuredCount > 0;
            enabled = _configured;

            if (logConfiguration)
            {
                Debug.Log($"[HumanoidArmSleeveAnchorGuard] 소매/어깨 보조본 anchor 구성 완료. nodes={configuredCount}");
            }

            return _configured;
        }

        public void DisableCorrection()
        {
            enableSleeveAnchor = false;
            _configured = false;
            _anchors.Clear();
            _controlledTransforms.Clear();
        }

        private int ConfigureArm(
            HumanBodyBones sourceBone,
            string armAnchorName,
            string shoulderCapName)
        {
            Transform source = _animator.GetBoneTransform(sourceBone);
            if (source == null)
            {
                return 0;
            }

            int count = 0;
            count += AddAnchor(source, armAnchorName, armAnchorInfluence);
            count += AddAnchor(source, shoulderCapName, shoulderCapInfluence);
            return count;
        }

        private int AddAnchor(Transform source, string nodeName, float influence)
        {
            Transform node = FindDescendantByName(transform, nodeName);
            if (source == null || node == null || node == source || influence <= 0f)
            {
                return 0;
            }

            Quaternion baselineSourceRelative = Quaternion.Inverse(source.rotation) * node.rotation;
            _anchors.Add(new AnchorCorrection(
                source,
                node,
                node.localRotation,
                baselineSourceRelative,
                Mathf.Clamp01(influence)));
            _controlledTransforms.Add(node);
            return 1;
        }

        private void ApplyAnchor(AnchorCorrection anchor)
        {
            if (anchor.Source == null || anchor.Node == null || anchor.Node.parent == null)
            {
                return;
            }

            Quaternion baselineWorldRotation = anchor.Node.parent.rotation * anchor.BaselineLocalRotation;
            Quaternion sourceDrivenWorldRotation = anchor.Source.rotation * anchor.BaselineSourceRelativeRotation;
            if (!IsFinite(baselineWorldRotation) || !IsFinite(sourceDrivenWorldRotation))
            {
                return;
            }

            Quaternion correctionDelta = sourceDrivenWorldRotation * Quaternion.Inverse(baselineWorldRotation);
            correctionDelta = LimitRotation(correctionDelta, maxFollowDegrees);
            Quaternion weightedDelta = Quaternion.Slerp(Quaternion.identity, correctionDelta, anchor.Influence);
            Quaternion finalWorldRotation = weightedDelta * baselineWorldRotation;
            if (!IsFinite(finalWorldRotation))
            {
                LogWarningOnce("보조본 회전 계산이 유효하지 않아 이번 프레임 보정을 건너뜁니다.");
                return;
            }

            anchor.Node.localRotation = Quaternion.Inverse(anchor.Node.parent.rotation) * finalWorldRotation;
        }

        private static Transform FindDescendantByName(Transform root, string targetName)
        {
            if (root == null || string.IsNullOrEmpty(targetName))
            {
                return null;
            }

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root)
                {
                    continue;
                }

                if (child.name == targetName ||
                    child.name.EndsWith("." + targetName, System.StringComparison.Ordinal) ||
                    child.name.EndsWith(targetName, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
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

        private void LogWarningOnce(string message)
        {
            if (_warningLogged || !logConfiguration)
            {
                _warningLogged = true;
                return;
            }

            Debug.LogWarning($"[HumanoidArmSleeveAnchorGuard] {message}");
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
