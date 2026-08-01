using UnityEngine;

namespace Fbx2Vmd.Modules.FBXImporter
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

        [Tooltip("손이 몸 밖으로 과하게 벌어진 경우 수평 reach를 제한하는 보정 강도입니다. 0이면 비활성화합니다.")]
        [Range(0f, 1f)]
        public float horizontalReachLimitWeight = 0f;

        [Tooltip("팔 길이 대비 허용할 최대 손 수평 reach입니다. 0이면 수평 reach 제한을 사용하지 않습니다.")]
        [Range(0f, 1.5f)]
        public float maxHandHorizontalReachRatio = 0f;

        [Tooltip("Optional below-shoulder gate for horizontal reach only. 0 keeps using maxHandBelowShoulderRatio.")]
        [Range(0f, 1.5f)]
        public float horizontalReachMaxHandBelowShoulderRatio = 0f;

        [Tooltip("Rolls back the horizontal reach result when the post-apply elbow angle falls below this value. 0 disables the guard.")]
        [Range(0f, 180f)]
        public float horizontalReachMinElbowAngleAfterApply = 0f;

        [Tooltip("Limits horizontal reach while the upper arm is raised enough to avoid the existing down-swing clamp.")]
        [Range(0f, 1f)]
        public float raisedPoseHorizontalReachLimitWeight = 0f;

        [Tooltip("Minimum downward upper-arm dot required before the raised-pose reach cap can run.")]
        [Range(0f, 1f)]
        public float raisedPoseMinUpperArmDownDot = 0.55f;

        [Tooltip("Raised-pose reach cap is skipped when the hand is already this far below the shoulder.")]
        [Range(0f, 1.5f)]
        public float raisedPoseMaxHandBelowShoulderRatio = 0.05f;

        [Tooltip("Maximum horizontal reach ratio for raised-pose frames.")]
        [Range(0f, 1.5f)]
        public float raisedPoseMaxHandHorizontalReachRatio = 0f;

        [Tooltip("상완 보정 후 전완을 다시 손 방향으로 맞춰 손이 크게 밀리는 현상을 줄입니다.")]
        public bool preserveHandTarget = true;

        public bool logCorrections = false;

        private Animator _animator;
        private bool _warningLogged;
        private HumanPoseHandler _diagnosticPoseHandler;
        private HumanPose _diagnosticPose;
        private int _leftForearmStretchMuscleIndex = -2;
        private int _rightForearmStretchMuscleIndex = -2;

        public int LastLeftApplied { get; private set; }
        public int LastLeftHorizontalReachApplied { get; private set; }
        public int LastLeftRaisedReachApplied { get; private set; }
        public float LastLeftForearmStretchBefore { get; private set; } = float.NaN;
        public float LastLeftForearmStretchAfter { get; private set; } = float.NaN;
        public float LastLeftForearmStretchDelta { get; private set; } = float.NaN;
        public int LastRightApplied { get; private set; }
        public int LastRightHorizontalReachApplied { get; private set; }
        public int LastRightRaisedReachApplied { get; private set; }
        public float LastRightForearmStretchBefore { get; private set; } = float.NaN;
        public float LastRightForearmStretchAfter { get; private set; } = float.NaN;
        public float LastRightForearmStretchDelta { get; private set; } = float.NaN;

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
            ResetDiagnostics();
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

        private void OnDisable()
        {
            ResetDiagnostics();
            DisposeDiagnosticPoseHandler();
        }

        private void OnDestroy()
        {
            DisposeDiagnosticPoseHandler();
        }

        public void Configure(
            Animator animator,
            bool enabled,
            float weight,
            float upperArmDownDot,
            float handHorizontalRatio,
            float handBelowShoulderRatio,
            float reachLimitWeight,
            float handHorizontalReachRatio,
            float reachMaxHandBelowShoulderRatio,
            float reachMinElbowAngleAfterApply,
            float raisedReachLimitWeight,
            float raisedMinUpperArmDownDot,
            float raisedMaxHandBelowShoulderRatio,
            float raisedMaxHandHorizontalReachRatio,
            bool logCorrectionMessages)
        {
            if (_animator != animator)
            {
                DisposeDiagnosticPoseHandler();
            }

            _animator = animator != null ? animator : GetComponent<Animator>();
            enableSwingLimit = enabled;
            correctionWeight = Mathf.Clamp01(weight);
            maxUpperArmDownDot = Mathf.Clamp01(upperArmDownDot);
            minHandHorizontalRatio = Mathf.Clamp(handHorizontalRatio, 0f, 1.5f);
            maxHandBelowShoulderRatio = Mathf.Clamp(handBelowShoulderRatio, 0f, 1.5f);
            horizontalReachLimitWeight = Mathf.Clamp01(reachLimitWeight);
            maxHandHorizontalReachRatio = Mathf.Clamp(handHorizontalReachRatio, 0f, 1.5f);
            horizontalReachMaxHandBelowShoulderRatio = Mathf.Clamp(reachMaxHandBelowShoulderRatio, 0f, 1.5f);
            horizontalReachMinElbowAngleAfterApply = Mathf.Clamp(reachMinElbowAngleAfterApply, 0f, 180f);
            raisedPoseHorizontalReachLimitWeight = Mathf.Clamp01(raisedReachLimitWeight);
            raisedPoseMinUpperArmDownDot = Mathf.Clamp01(raisedMinUpperArmDownDot);
            raisedPoseMaxHandBelowShoulderRatio = Mathf.Clamp(raisedMaxHandBelowShoulderRatio, 0f, 1.5f);
            raisedPoseMaxHandHorizontalReachRatio = Mathf.Clamp(raisedMaxHandHorizontalReachRatio, 0f, 1.5f);
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

            float forearmStretchBefore = CaptureForearmStretchMuscle(upperBone);
            Vector3 currentWorldDirection = upperToLower / upperLength;
            Vector3 originalHandPosition = hand.position;
            Vector3 currentLocalDirection = transform.InverseTransformDirection(currentWorldDirection).normalized;
            float currentDownDot = Mathf.Clamp01(-currentLocalDirection.y);
            Vector3 localHandOffset = transform.InverseTransformPoint(hand.position) -
                                      transform.InverseTransformPoint(upper.position);
            float horizontalRatio = new Vector2(localHandOffset.x, localHandOffset.z).magnitude / armLength;
            float belowShoulderRatio = Mathf.Max(0f, -localHandOffset.y) / armLength;
            if (currentDownDot <= maxUpperArmDownDot)
            {
                bool raisedReachLimited = TryApplyRaisedPoseHorizontalReachLimit(
                    upper,
                    lower,
                    hand,
                    localHandOffset,
                    armLength,
                    currentDownDot,
                    belowShoulderRatio);
                RecordArmLimitDiagnostics(
                    upperBone,
                    standardLimitApplied: false,
                    horizontalReachLimited: false,
                    raisedReachLimited: raisedReachLimited,
                    forearmStretchBefore: forearmStretchBefore);
                return;
            }

            bool horizontalReachLimited = TryApplyHorizontalReachLimit(
                upper,
                lower,
                hand,
                localHandOffset,
                armLength,
                belowShoulderRatio);

            if (horizontalRatio < minHandHorizontalRatio ||
                belowShoulderRatio > maxHandBelowShoulderRatio)
            {
                RecordArmLimitDiagnostics(
                    upperBone,
                    standardLimitApplied: false,
                    horizontalReachLimited: horizontalReachLimited,
                    raisedReachLimited: false,
                    forearmStretchBefore: forearmStretchBefore);
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
                RecordArmLimitDiagnostics(
                    upperBone,
                    standardLimitApplied: false,
                    horizontalReachLimited: horizontalReachLimited,
                    raisedReachLimited: false,
                    forearmStretchBefore: forearmStretchBefore);
                return;
            }

            Quaternion rotationDelta = Quaternion.FromToRotation(currentWorldDirection, targetWorldDirection);
            Quaternion targetRotation = rotationDelta * upper.rotation;
            if (!IsFinite(targetRotation))
            {
                RecordArmLimitDiagnostics(
                    upperBone,
                    standardLimitApplied: false,
                    horizontalReachLimited: horizontalReachLimited,
                    raisedReachLimited: false,
                    forearmStretchBefore: forearmStretchBefore);
                return;
            }

            upper.rotation = Quaternion.Slerp(upper.rotation, targetRotation, correctionWeight);
            if (preserveHandTarget && !horizontalReachLimited)
            {
                RedirectForearmToHandTarget(lower, hand, originalHandPosition, correctionWeight);
            }

            LogOnce($"상완 하강 각도를 제한했습니다. bone={upper.name}, downDot={currentDownDot:F3}, horizontalRatio={horizontalRatio:F3}, belowRatio={belowShoulderRatio:F3}");
            RecordArmLimitDiagnostics(
                upperBone,
                standardLimitApplied: true,
                horizontalReachLimited: horizontalReachLimited,
                raisedReachLimited: false,
                forearmStretchBefore: forearmStretchBefore);
        }

        private bool TryApplyHorizontalReachLimit(
            Transform upper,
            Transform lower,
            Transform hand,
            Vector3 localHandOffset,
            float armLength,
            float belowShoulderRatio)
        {
            float reachWeight = Mathf.Clamp01(horizontalReachLimitWeight);
            float maxReachRatio = Mathf.Clamp(maxHandHorizontalReachRatio, 0f, 1.5f);
            float reachMaxBelowRatio = horizontalReachMaxHandBelowShoulderRatio > 0f
                ? Mathf.Clamp(horizontalReachMaxHandBelowShoulderRatio, 0f, 1.5f)
                : maxHandBelowShoulderRatio;
            if (reachWeight <= 0f ||
                maxReachRatio <= 0f ||
                armLength <= 0.0001f ||
                belowShoulderRatio > reachMaxBelowRatio)
            {
                return false;
            }

            Vector2 horizontal = new Vector2(localHandOffset.x, localHandOffset.z);
            float horizontalDistance = horizontal.magnitude;
            float maxHorizontalDistance = maxReachRatio * armLength;
            if (horizontalDistance <= maxHorizontalDistance ||
                horizontalDistance <= 0.0001f)
            {
                return false;
            }

            Vector2 targetHorizontal = horizontal.normalized * maxHorizontalDistance;
            Vector3 targetLocalOffset = new Vector3(
                targetHorizontal.x,
                localHandOffset.y,
                targetHorizontal.y);
            Vector3 upperLocalPosition = transform.InverseTransformPoint(upper.position);
            Vector3 targetWorldHandPosition = transform.TransformPoint(upperLocalPosition + targetLocalOffset);
            if (!IsFinite(targetWorldHandPosition))
            {
                return false;
            }

            Quaternion upperRotationBefore = upper.rotation;
            Quaternion lowerRotationBefore = lower.rotation;
            RotateBoneTowardDirection(
                upper,
                lower.position - upper.position,
                targetWorldHandPosition - upper.position,
                reachWeight);
            RedirectForearmToHandTarget(lower, hand, targetWorldHandPosition, reachWeight);
            if (ShouldRollbackHorizontalReachLimit(upper, lower, hand))
            {
                upper.rotation = upperRotationBefore;
                lower.rotation = lowerRotationBefore;
                return false;
            }

            LogOnce($"손 수평 reach를 제한했습니다. bone={upper.name}, horizontalRatio={horizontalDistance / armLength:F3}, maxRatio={maxReachRatio:F3}");
            return true;
        }

        private bool ShouldRollbackHorizontalReachLimit(Transform upper, Transform lower, Transform hand)
        {
            float minElbowAngle = Mathf.Clamp(horizontalReachMinElbowAngleAfterApply, 0f, 180f);
            if (minElbowAngle <= 0f)
            {
                return false;
            }

            float elbowAngle = CalculateElbowAngleDegrees(upper, lower, hand);
            return IsFinite(elbowAngle) && elbowAngle < minElbowAngle;
        }

        private bool TryApplyRaisedPoseHorizontalReachLimit(
            Transform upper,
            Transform lower,
            Transform hand,
            Vector3 localHandOffset,
            float armLength,
            float currentDownDot,
            float belowShoulderRatio)
        {
            float reachWeight = Mathf.Clamp01(raisedPoseHorizontalReachLimitWeight);
            float minDownDot = Mathf.Clamp01(raisedPoseMinUpperArmDownDot);
            float maxBelowRatio = Mathf.Clamp(raisedPoseMaxHandBelowShoulderRatio, 0f, 1.5f);
            float maxReachRatio = Mathf.Clamp(raisedPoseMaxHandHorizontalReachRatio, 0f, 1.5f);
            if (reachWeight <= 0f ||
                maxReachRatio <= 0f ||
                armLength <= 0.0001f ||
                currentDownDot < minDownDot ||
                belowShoulderRatio > maxBelowRatio)
            {
                return false;
            }

            Vector2 horizontal = new Vector2(localHandOffset.x, localHandOffset.z);
            float horizontalDistance = horizontal.magnitude;
            float maxHorizontalDistance = maxReachRatio * armLength;
            if (horizontalDistance <= maxHorizontalDistance ||
                horizontalDistance <= 0.0001f)
            {
                return false;
            }

            Vector2 targetHorizontal = horizontal.normalized * maxHorizontalDistance;
            Vector3 targetLocalOffset = new Vector3(
                targetHorizontal.x,
                localHandOffset.y,
                targetHorizontal.y);
            Vector3 upperLocalPosition = transform.InverseTransformPoint(upper.position);
            Vector3 targetWorldHandPosition = transform.TransformPoint(upperLocalPosition + targetLocalOffset);
            if (!IsFinite(targetWorldHandPosition))
            {
                return false;
            }

            RotateBoneTowardDirection(
                upper,
                lower.position - upper.position,
                targetWorldHandPosition - upper.position,
                reachWeight);
            RedirectForearmToHandTarget(lower, hand, targetWorldHandPosition, reachWeight);
            LogOnce($"Raised-pose horizontal reach limited. bone={upper.name}, downDot={currentDownDot:F3}, belowRatio={belowShoulderRatio:F3}, horizontalRatio={horizontalDistance / armLength:F3}, maxRatio={maxReachRatio:F3}");
            return true;
        }

        private static void RotateBoneTowardDirection(
            Transform bone,
            Vector3 currentDirection,
            Vector3 targetDirection,
            float weight)
        {
            if (bone == null ||
                currentDirection.sqrMagnitude <= 0.000001f ||
                targetDirection.sqrMagnitude <= 0.000001f ||
                weight <= 0f)
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
            Quaternion targetRotation = correction * bone.rotation;
            if (!IsFinite(targetRotation))
            {
                return;
            }

            bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, Mathf.Clamp01(weight));
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

        private static float CalculateElbowAngleDegrees(Transform upper, Transform lower, Transform hand)
        {
            if (upper == null || lower == null || hand == null)
            {
                return float.NaN;
            }

            Vector3 toUpper = upper.position - lower.position;
            Vector3 toHand = hand.position - lower.position;
            if (toUpper.sqrMagnitude <= 0.000001f || toHand.sqrMagnitude <= 0.000001f)
            {
                return float.NaN;
            }

            return Vector3.Angle(toUpper, toHand);
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

        private void ResetDiagnostics()
        {
            LastLeftApplied = 0;
            LastLeftHorizontalReachApplied = 0;
            LastLeftRaisedReachApplied = 0;
            LastLeftForearmStretchBefore = float.NaN;
            LastLeftForearmStretchAfter = float.NaN;
            LastLeftForearmStretchDelta = float.NaN;
            LastRightApplied = 0;
            LastRightHorizontalReachApplied = 0;
            LastRightRaisedReachApplied = 0;
            LastRightForearmStretchBefore = float.NaN;
            LastRightForearmStretchAfter = float.NaN;
            LastRightForearmStretchDelta = float.NaN;
        }

        private void RecordArmLimitDiagnostics(
            HumanBodyBones upperBone,
            bool standardLimitApplied,
            bool horizontalReachLimited,
            bool raisedReachLimited,
            float forearmStretchBefore)
        {
            float forearmStretchAfter = CaptureForearmStretchMuscle(upperBone);
            float forearmStretchDelta = IsFinite(forearmStretchBefore) && IsFinite(forearmStretchAfter)
                ? forearmStretchAfter - forearmStretchBefore
                : float.NaN;
            int anyApplied = standardLimitApplied || horizontalReachLimited || raisedReachLimited ? 1 : 0;
            int horizontalApplied = horizontalReachLimited ? 1 : 0;
            int raisedApplied = raisedReachLimited ? 1 : 0;

            if (upperBone == HumanBodyBones.LeftUpperArm)
            {
                LastLeftApplied = anyApplied;
                LastLeftHorizontalReachApplied = horizontalApplied;
                LastLeftRaisedReachApplied = raisedApplied;
                LastLeftForearmStretchBefore = forearmStretchBefore;
                LastLeftForearmStretchAfter = forearmStretchAfter;
                LastLeftForearmStretchDelta = forearmStretchDelta;
                return;
            }

            LastRightApplied = anyApplied;
            LastRightHorizontalReachApplied = horizontalApplied;
            LastRightRaisedReachApplied = raisedApplied;
            LastRightForearmStretchBefore = forearmStretchBefore;
            LastRightForearmStretchAfter = forearmStretchAfter;
            LastRightForearmStretchDelta = forearmStretchDelta;
        }

        private float CaptureForearmStretchMuscle(HumanBodyBones upperBone)
        {
            if (!EnsureDiagnosticPoseHandler())
            {
                return float.NaN;
            }

            _diagnosticPoseHandler.GetHumanPose(ref _diagnosticPose);
            if (_diagnosticPose.muscles == null || _diagnosticPose.muscles.Length == 0)
            {
                return float.NaN;
            }

            int muscleIndex = upperBone == HumanBodyBones.LeftUpperArm
                ? GetLeftForearmStretchMuscleIndex()
                : GetRightForearmStretchMuscleIndex();
            if (muscleIndex < 0 || muscleIndex >= _diagnosticPose.muscles.Length)
            {
                return float.NaN;
            }

            return _diagnosticPose.muscles[muscleIndex];
        }

        private bool EnsureDiagnosticPoseHandler()
        {
            if (_diagnosticPoseHandler != null)
            {
                return true;
            }

            if (!InitializeIfNeeded())
            {
                return false;
            }

            _diagnosticPoseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
            _diagnosticPose = new HumanPose();
            return true;
        }

        private void DisposeDiagnosticPoseHandler()
        {
            if (_diagnosticPoseHandler == null)
            {
                return;
            }

            _diagnosticPoseHandler.Dispose();
            _diagnosticPoseHandler = null;
        }

        private int GetLeftForearmStretchMuscleIndex()
        {
            if (_leftForearmStretchMuscleIndex == -2)
            {
                _leftForearmStretchMuscleIndex = FindMuscleIndex("left", "forearm", "stretch");
            }

            return _leftForearmStretchMuscleIndex;
        }

        private int GetRightForearmStretchMuscleIndex()
        {
            if (_rightForearmStretchMuscleIndex == -2)
            {
                _rightForearmStretchMuscleIndex = FindMuscleIndex("right", "forearm", "stretch");
            }

            return _rightForearmStretchMuscleIndex;
        }

        private static int FindMuscleIndex(params string[] tokens)
        {
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string muscleName = NormalizeMuscleName(HumanTrait.MuscleName[i]);
                bool matches = true;
                foreach (string token in tokens)
                {
                    if (!muscleName.Contains(NormalizeMuscleName(token)))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeMuscleName(string value)
        {
            return string.IsNullOrEmpty(value)
                ? ""
                : value.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
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
