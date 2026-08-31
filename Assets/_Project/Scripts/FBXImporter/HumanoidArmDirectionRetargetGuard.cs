using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fbx2Vmd.FBXImporter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(22500)]
    public class HumanoidArmDirectionRetargetGuard : MonoBehaviour
    {
        [Header("Humanoid Arm Direction Retarget Guard")]
        [FormerlySerializedAs("enableDirectionRetarget")]
        [SerializeField] private bool _enableDirectionRetarget= true;
        public bool enableDirectionRetarget { get => _enableDirectionRetarget; set => _enableDirectionRetarget = value; }

        [Range(0f, 1f)]
        [FormerlySerializedAs("upperArmWeight")]
        [SerializeField] private float _upperArmWeight= 0.65f;
        public float upperArmWeight { get => _upperArmWeight; private set => _upperArmWeight = value; }

        [Range(0f, 1f)]
        [FormerlySerializedAs("forearmWeight")]
        [SerializeField] private float _forearmWeight= 0.75f;
        public float forearmWeight { get => _forearmWeight; private set => _forearmWeight = value; }

        [Range(0f, 120f)]
        [FormerlySerializedAs("upperArmMaxDegrees")]
        [SerializeField] private float _upperArmMaxDegrees= 65f;
        public float upperArmMaxDegrees { get => _upperArmMaxDegrees; private set => _upperArmMaxDegrees = value; }

        [Range(0f, 120f)]
        [FormerlySerializedAs("forearmMaxDegrees")]
        [SerializeField] private float _forearmMaxDegrees= 85f;
        public float forearmMaxDegrees { get => _forearmMaxDegrees; private set => _forearmMaxDegrees = value; }

        [Range(0f, 1f)]
        [FormerlySerializedAs("leftSideWeightScale")]
        [SerializeField] private float _leftSideWeightScale= 1f;
        public float leftSideWeightScale { get => _leftSideWeightScale; private set => _leftSideWeightScale = value; }

        [Range(0f, 1f)]
        [FormerlySerializedAs("rightSideWeightScale")]
        [SerializeField] private float _rightSideWeightScale= 1f;
        public float rightSideWeightScale { get => _rightSideWeightScale; private set => _rightSideWeightScale = value; }

        [FormerlySerializedAs("logConfiguration")]
        [SerializeField] private bool _logConfiguration= false;
        public bool logConfiguration { get => _logConfiguration; private set => _logConfiguration = value; }

        private Animator _ghostAnimator;
        private Animator _targetAnimator;
        private readonly List<SegmentMapping> _segments = new List<SegmentMapping>();
        private bool _configured;
        private bool _warningLogged;

        private struct SegmentMapping
        {
            public HumanBodyBones SourceBone;
            public HumanBodyBones EndBone;
            public Quaternion GhostToTargetLocalCorrection;
            public float Weight;
            public float MaxDegrees;

            public SegmentMapping(
                HumanBodyBones sourceBone,
                HumanBodyBones endBone,
                Quaternion ghostToTargetLocalCorrection,
                float weight,
                float maxDegrees)
            {
                SourceBone = sourceBone;
                EndBone = endBone;
                GhostToTargetLocalCorrection = ghostToTargetLocalCorrection;
                Weight = weight;
                MaxDegrees = maxDegrees;
            }
        }

        private void LateUpdate()
        {
            if (!enableDirectionRetarget || !_configured)
            {
                return;
            }

            if (!HasLiveRuntimeState())
            {
                DisableCorrection();
                return;
            }

            foreach (SegmentMapping segment in _segments)
            {
                ApplySegment(segment);
            }
        }

        public bool Configure(
            Animator ghostAnimator,
            Animator targetAnimator,
            float nextUpperArmWeight,
            float nextForearmWeight,
            float nextUpperArmMaxDegrees,
            float nextForearmMaxDegrees,
            bool nextLogConfiguration)
        {
            return Configure(
                ghostAnimator,
                targetAnimator,
                nextUpperArmWeight,
                nextForearmWeight,
                nextUpperArmMaxDegrees,
                nextForearmMaxDegrees,
                1f,
                1f,
                nextLogConfiguration);
        }

        public bool Configure(
            Animator ghostAnimator,
            Animator targetAnimator,
            float nextUpperArmWeight,
            float nextForearmWeight,
            float nextUpperArmMaxDegrees,
            float nextForearmMaxDegrees,
            float nextLeftSideWeightScale,
            float nextRightSideWeightScale,
            bool nextLogConfiguration)
        {
            _ghostAnimator = ghostAnimator;
            _targetAnimator = targetAnimator != null ? targetAnimator : GetComponent<Animator>();
            upperArmWeight = Mathf.Clamp01(nextUpperArmWeight);
            forearmWeight = Mathf.Clamp01(nextForearmWeight);
            upperArmMaxDegrees = Mathf.Clamp(nextUpperArmMaxDegrees, 0f, 120f);
            forearmMaxDegrees = Mathf.Clamp(nextForearmMaxDegrees, 0f, 120f);
            leftSideWeightScale = Mathf.Clamp01(nextLeftSideWeightScale);
            rightSideWeightScale = Mathf.Clamp01(nextRightSideWeightScale);
            logConfiguration = nextLogConfiguration;
            _warningLogged = false;
            _segments.Clear();

            _configured = IsValidHumanoid(_ghostAnimator) && IsValidHumanoid(_targetAnimator);
            if (_configured)
            {
                AddSegment(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, upperArmWeight * leftSideWeightScale, upperArmMaxDegrees);
                AddSegment(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, upperArmWeight * rightSideWeightScale, upperArmMaxDegrees);
                AddSegment(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, forearmWeight * leftSideWeightScale, forearmMaxDegrees);
                AddSegment(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, forearmWeight * rightSideWeightScale, forearmMaxDegrees);
                _configured = _segments.Count > 0;
            }

            enabled = _configured && enableDirectionRetarget;

            if (logConfiguration)
            {
                Debug.Log($"[HumanoidArmDirectionRetargetGuard] Arm direction retarget configured={_configured}, segments={_segments.Count}, sideScales={leftSideWeightScale:F2}/{rightSideWeightScale:F2}");
            }

            return _configured;
        }

        private bool HasLiveRuntimeState()
        {
            return IsValidHumanoid(_ghostAnimator) &&
                   IsValidHumanoid(_targetAnimator) &&
                   _segments.Count > 0;
        }

        public void DisableCorrection()
        {
            enableDirectionRetarget = false;
            _configured = false;
            _segments.Clear();
            enabled = false;
        }

        private void AddSegment(
            HumanBodyBones sourceBone,
            HumanBodyBones endBone,
            float weight,
            float maxDegrees)
        {
            Transform ghostRoot = _ghostAnimator.transform;
            Transform targetRoot = _targetAnimator.transform;
            Transform ghostSource = _ghostAnimator.GetBoneTransform(sourceBone);
            Transform ghostEnd = _ghostAnimator.GetBoneTransform(endBone);
            Transform targetSource = _targetAnimator.GetBoneTransform(sourceBone);
            Transform targetEnd = _targetAnimator.GetBoneTransform(endBone);

            if (ghostRoot == null || targetRoot == null ||
                ghostSource == null || ghostEnd == null ||
                targetSource == null || targetEnd == null ||
                weight <= 0f)
            {
                return;
            }

            Vector3 ghostLocalBaseline = ghostRoot.InverseTransformDirection(ghostEnd.position - ghostSource.position).normalized;
            Vector3 targetLocalBaseline = targetRoot.InverseTransformDirection(targetEnd.position - targetSource.position).normalized;
            if (!IsFinite(ghostLocalBaseline) || !IsFinite(targetLocalBaseline) ||
                ghostLocalBaseline.sqrMagnitude <= 0.000001f ||
                targetLocalBaseline.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Quaternion correction = Quaternion.FromToRotation(ghostLocalBaseline, targetLocalBaseline);
            _segments.Add(new SegmentMapping(
                sourceBone,
                endBone,
                correction,
                Mathf.Clamp01(weight),
                Mathf.Clamp(maxDegrees, 0f, 120f)));
        }

        private void ApplySegment(SegmentMapping segment)
        {
            Transform ghostRoot = _ghostAnimator.transform;
            Transform targetRoot = _targetAnimator.transform;
            Transform ghostSource = _ghostAnimator.GetBoneTransform(segment.SourceBone);
            Transform ghostEnd = _ghostAnimator.GetBoneTransform(segment.EndBone);
            Transform targetSource = _targetAnimator.GetBoneTransform(segment.SourceBone);
            Transform targetEnd = _targetAnimator.GetBoneTransform(segment.EndBone);
            if (ghostRoot == null || targetRoot == null ||
                ghostSource == null || ghostEnd == null ||
                targetSource == null || targetEnd == null)
            {
                return;
            }

            Vector3 ghostWorldDirection = ghostEnd.position - ghostSource.position;
            if (ghostWorldDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 ghostLocalDirection = ghostRoot.InverseTransformDirection(ghostWorldDirection.normalized);
            Vector3 desiredTargetLocalDirection = segment.GhostToTargetLocalCorrection * ghostLocalDirection;
            Vector3 desiredTargetWorldDirection = targetRoot.TransformDirection(desiredTargetLocalDirection).normalized;

            AlignBoneDirection(
                targetSource,
                targetEnd.position - targetSource.position,
                desiredTargetWorldDirection,
                segment.Weight,
                segment.MaxDegrees);
        }

        private void AlignBoneDirection(
            Transform bone,
            Vector3 currentWorldDirection,
            Vector3 desiredWorldDirection,
            float weight,
            float maxDegrees)
        {
            if (bone == null ||
                currentWorldDirection.sqrMagnitude <= 0.000001f ||
                desiredWorldDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 current = currentWorldDirection.normalized;
            Vector3 desired = desiredWorldDirection.normalized;
            if (!IsFinite(current) || !IsFinite(desired))
            {
                return;
            }

            Quaternion delta = Quaternion.FromToRotation(current, desired);
            delta = LimitRotation(delta, maxDegrees);
            Quaternion weightedDelta = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight));
            Quaternion nextRotation = weightedDelta * bone.rotation;
            if (!IsFinite(nextRotation))
            {
                LogWarningOnce("Calculated arm direction rotation was invalid. Skipping this frame.");
                return;
            }

            bone.rotation = nextRotation;
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

            return Quaternion.AngleAxis(Mathf.Clamp(angle, -maxDegrees, maxDegrees), axis.normalized);
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

        private static bool IsValidHumanoid(Animator animator)
        {
            return animator != null &&
                   animator.avatar != null &&
                   animator.avatar.isValid &&
                   animator.avatar.isHuman;
        }

        private void LogWarningOnce(string message)
        {
            if (_warningLogged || !logConfiguration)
            {
                _warningLogged = true;
                return;
            }

            Debug.LogWarning($"[HumanoidArmDirectionRetargetGuard] {message}");
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
