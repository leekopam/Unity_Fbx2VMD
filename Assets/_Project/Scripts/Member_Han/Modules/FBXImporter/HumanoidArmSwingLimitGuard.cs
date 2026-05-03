using UnityEngine;

namespace Member_Han.Modules.FBXImporter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(23000)]
    public class HumanoidArmSwingLimitGuard : MonoBehaviour
    {
        [Header("YYB Arm Swing Limit Guard")]
        [Tooltip("손이 몸 밖/어깨 근처에 있는데 상완만 과도하게 아래로 떨어지는 포즈를 제한합니다.")]
        public bool enableSwingLimit = true;

        [Tooltip("보정 강도입니다. 0이면 적용하지 않고, 1이면 제한 방향까지 즉시 보정합니다.")]
        [Range(0f, 1f)]
        public float correctionWeight = 0.85f;

        [Tooltip("상완 방향이 캐릭터 아래 방향과 이 값보다 더 가까우면 보정 후보로 봅니다.")]
        [Range(0f, 1f)]
        public float maxUpperArmDownDot = 0.68f;

        [Tooltip("손이 어깨에서 팔 길이 대비 이 비율 이상 옆/앞으로 떨어져 있을 때만 보정합니다. YYB는 몸 가까이에서도 소매가 무너질 수 있어 낮게 둡니다.")]
        [Range(0f, 1.5f)]
        public float minHandHorizontalRatio = 0.05f;

        [Tooltip("손이 어깨보다 팔 길이 대비 이 비율보다 더 낮으면 자연스럽게 내려간 팔로 보고 보정하지 않습니다.")]
        [Range(0f, 1.5f)]
        public float maxHandBelowShoulderRatio = 0.75f;

        [Tooltip("상완 보정 후 전완을 다시 손 방향으로 맞춰 손이 크게 밀리는 현상을 줄입니다.")]
        public bool preserveHandTarget = true;

        public bool logCorrections = false;

        private Animator _animator;
        private bool _warningLogged;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
        }

        private void LateUpdate()
        {
            if (!enableSwingLimit || correctionWeight <= 0f || !InitializeIfNeeded())
            {
                return;
            }

            ApplyArmLimit(
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand,
                -1f);
            ApplyArmLimit(
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.RightHand,
                1f);
        }

        public void Configure(
            Animator animator,
            bool enabled,
            float weight,
            float upperArmDownDot,
            float handHorizontalRatio,
            float handBelowShoulderRatio,
            bool logCorrectionMessages)
        {
            _animator = animator != null ? animator : GetComponent<Animator>();
            enableSwingLimit = enabled;
            correctionWeight = Mathf.Clamp01(weight);
            maxUpperArmDownDot = Mathf.Clamp01(upperArmDownDot);
            minHandHorizontalRatio = Mathf.Clamp(handHorizontalRatio, 0f, 1.5f);
            maxHandBelowShoulderRatio = Mathf.Clamp(handBelowShoulderRatio, 0f, 1.5f);
            logCorrections = logCorrectionMessages;
            _warningLogged = false;
            this.enabled = enableSwingLimit;
        }

        private bool InitializeIfNeeded()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            return _animator != null &&
                   _animator.avatar != null &&
                   _animator.avatar.isValid &&
                   _animator.avatar.isHuman;
        }

        private void ApplyArmLimit(
            HumanBodyBones upperBone,
            HumanBodyBones lowerBone,
            HumanBodyBones handBone,
            float fallbackSideSign)
        {
            Transform upper = _animator.GetBoneTransform(upperBone);
            Transform lower = _animator.GetBoneTransform(lowerBone);
            Transform hand = _animator.GetBoneTransform(handBone);
            if (upper == null || lower == null || hand == null)
            {
                return;
            }

            Vector3 upperToLower = lower.position - upper.position;
            Vector3 lowerToHand = hand.position - lower.position;
            float upperLength = upperToLower.magnitude;
            float lowerLength = lowerToHand.magnitude;
            float armLength = upperLength + lowerLength;
            if (upperLength <= 0.0001f || armLength <= 0.0001f)
            {
                return;
            }

            Vector3 currentWorldDirection = upperToLower / upperLength;
            Vector3 originalHandPosition = hand.position;
            Vector3 currentLocalDirection = transform.InverseTransformDirection(currentWorldDirection).normalized;
            float currentDownDot = Mathf.Clamp01(-currentLocalDirection.y);
            if (currentDownDot <= maxUpperArmDownDot)
            {
                return;
            }

            Vector3 localHandOffset = transform.InverseTransformPoint(hand.position) -
                                      transform.InverseTransformPoint(upper.position);
            float horizontalRatio = new Vector2(localHandOffset.x, localHandOffset.z).magnitude / armLength;
            float belowShoulderRatio = Mathf.Max(0f, -localHandOffset.y) / armLength;
            if (horizontalRatio < minHandHorizontalRatio ||
                belowShoulderRatio > maxHandBelowShoulderRatio)
            {
                return;
            }

            Vector3 horizontal = new Vector3(currentLocalDirection.x, 0f, currentLocalDirection.z);
            if (horizontal.sqrMagnitude <= 0.000001f)
            {
                horizontal = new Vector3(fallbackSideSign, 0f, 0f);
            }

            horizontal.Normalize();
            float targetHorizontalMagnitude = Mathf.Sqrt(Mathf.Max(0f, 1f - maxUpperArmDownDot * maxUpperArmDownDot));
            Vector3 targetLocalDirection = horizontal * targetHorizontalMagnitude;
            targetLocalDirection.y = -maxUpperArmDownDot;
            targetLocalDirection.Normalize();

            Vector3 targetWorldDirection = transform.TransformDirection(targetLocalDirection).normalized;
            if (!IsFinite(targetWorldDirection))
            {
                return;
            }

            Quaternion rotationDelta = Quaternion.FromToRotation(currentWorldDirection, targetWorldDirection);
            Quaternion targetRotation = rotationDelta * upper.rotation;
            if (!IsFinite(targetRotation))
            {
                return;
            }

            upper.rotation = Quaternion.Slerp(upper.rotation, targetRotation, correctionWeight);
            if (preserveHandTarget)
            {
                RedirectForearmToHandTarget(lower, hand, originalHandPosition, correctionWeight);
            }

            LogOnce($"상완 하강 각도를 제한했습니다. bone={upper.name}, downDot={currentDownDot:F3}, horizontalRatio={horizontalRatio:F3}, belowRatio={belowShoulderRatio:F3}");
        }

        private static void RedirectForearmToHandTarget(
            Transform lower,
            Transform hand,
            Vector3 originalHandPosition,
            float weight)
        {
            if (lower == null || hand == null || weight <= 0f)
            {
                return;
            }

            Vector3 currentDirection = hand.position - lower.position;
            Vector3 targetDirection = originalHandPosition - lower.position;
            if (currentDirection.sqrMagnitude <= 0.000001f ||
                targetDirection.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            currentDirection.Normalize();
            targetDirection.Normalize();
            if (!IsFinite(currentDirection) || !IsFinite(targetDirection))
            {
                return;
            }

            Quaternion correction = Quaternion.FromToRotation(currentDirection, targetDirection);
            Quaternion targetRotation = correction * lower.rotation;
            if (!IsFinite(targetRotation))
            {
                return;
            }

            lower.rotation = Quaternion.Slerp(lower.rotation, targetRotation, Mathf.Clamp01(weight));
        }

        private void LogOnce(string message)
        {
            if (_warningLogged || !logCorrections)
            {
                _warningLogged = true;
                return;
            }

            Debug.LogWarning($"[HumanoidArmSwingLimitGuard] {message}");
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
