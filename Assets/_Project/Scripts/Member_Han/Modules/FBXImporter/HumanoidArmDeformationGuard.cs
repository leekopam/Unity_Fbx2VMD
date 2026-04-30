using System.Collections.Generic;
using UnityEngine;

namespace Member_Han.Modules.FBXImporter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(25000)]
    public class HumanoidArmDeformationGuard : MonoBehaviour
    {
        [Header("팔 변형 방지")]
        [Tooltip("Humanoid muscle 값을 Unity 기본 안전 범위인 -1~1로 제한합니다.")]
        public bool clampMusclesToHumanRange = false;

        [Tooltip("팔이 늘어나거나 비정상적으로 비틀리는 Humanoid arm muscle 값을 제한합니다.")]
        public bool enableAnatomicalArmGuard = false;

        [Tooltip("직접 Animator 재생에도 Forearm Stretch muscle을 제한합니다. 이 값은 팔꿈치 굽힘에 가까워 모션이 굳을 수 있으므로 기본값은 끕니다.")]
        public bool clampArmStretchMuscles = false;

        [Tooltip("Humanoid arm stretch muscle 허용치입니다. Forearm Stretch는 팔꿈치 굽힘에 가까우므로 직접 켤 때만 사용합니다.")]
        [Range(0f, 0.5f)]
        public float armStretchMuscleLimit = 0f;

        [Tooltip("상완 twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        public float upperArmTwistMuscleLimit = 0.75f;

        [Tooltip("전완 twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        public float lowerArmTwistMuscleLimit = 0.65f;

        [Tooltip("Humanoid 본 localPosition을 시작 값으로 복구해 팔/다리 길이 변형을 막습니다.")]
        public bool lockHumanoidBonePositions = true;

        [Tooltip("소매/팔 보조본처럼 Humanoid 매핑 밖의 팔/다리 하위 Transform localPosition을 시작 값으로 복구합니다.")]
        public bool lockLimbChildLocalPositions = true;

        [Tooltip("소매/팔 보조본처럼 Humanoid 매핑 밖의 팔/다리 하위 Transform localRotation을 시작 값으로 복구합니다.")]
        public bool lockLimbChildLocalRotations = false;

        [Tooltip("모델 전체 Transform localScale을 시작 값으로 복구합니다.")]
        public bool restoreLocalScales = true;

        [Tooltip("처음 보정이 발생했을 때 진단 로그를 출력합니다.")]
        public bool logCorrections = false;

        private readonly Dictionary<Transform, Vector3> _initialLocalScales = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _initialHumanoidLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _initialLimbChildLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Quaternion> _initialLimbChildLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly HashSet<Transform> _limbChildRotationExclusions = new HashSet<Transform>();
        private static readonly HumanBodyBones[] LimbPositionRoots =
        {
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot,
            HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes
        };
        private Animator _animator;
        private Avatar _cachedAvatar;
        private Transform _cachedRoot;
        private HumanPoseHandler _poseHandler;
        private HumanPose _pose;
        private bool _initialized;
        private bool _poseWarningLogged;
        private bool _scaleWarningLogged;
        private bool _positionWarningLogged;
        private bool _limbChildPositionWarningLogged;
        private bool _limbChildRotationWarningLogged;
        private bool _muscleWarningLogged;
        private bool _armWarningLogged;

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
            if (!InitializeIfNeeded())
            {
                return;
            }

            if (_poseHandler == null)
            {
                return;
            }

            _poseHandler.GetHumanPose(ref _pose);
            if (!IsFinite(_pose))
            {
                LogOnce(ref _poseWarningLogged, "HumanPose에 유효하지 않은 값이 있어 이 프레임의 팔 변형 가드를 건너뜁니다.");
                RestoreLocalScales();
                RestoreHumanoidLocalPositions();
                RestoreLimbChildLocalPositions();
                RestoreLimbChildLocalRotations();
                return;
            }

            int rangeClampCount = clampMusclesToHumanRange ? ClampMusclesToHumanRange(ref _pose) : 0;
            int armClampCount = enableAnatomicalArmGuard
                ? ClampAnatomicalArmMuscles(
                    ref _pose,
                    armStretchMuscleLimit,
                    upperArmTwistMuscleLimit,
                    lowerArmTwistMuscleLimit,
                    clampArmStretchMuscles)
                : 0;

            if (rangeClampCount > 0 || armClampCount > 0)
            {
                _poseHandler.SetHumanPose(ref _pose);

                if (rangeClampCount > 0)
                {
                    LogOnce(ref _muscleWarningLogged, $"Humanoid muscle {rangeClampCount}개를 -1~1 범위로 제한했습니다.");
                }

                if (armClampCount > 0)
                {
                    LogOnce(ref _armWarningLogged, $"팔 변형 방지를 위해 arm muscle {armClampCount}개를 제한했습니다.");
                }
            }

            RestoreHumanoidLocalPositions();
            RestoreLimbChildLocalPositions();
            RestoreLimbChildLocalRotations();
            RestoreLocalScales();
        }

        public void Configure(
            bool clampHumanRange,
            bool enableArmGuard,
            float stretchLimit,
            float upperTwistLimit,
            float lowerTwistLimit,
            bool lockBonePositions,
            bool logCorrectionMessages,
            bool clampStretchMuscles = false,
            bool lockLimbChildPositions = true,
            bool lockLimbChildRotations = false)
        {
            clampMusclesToHumanRange = clampHumanRange;
            enableAnatomicalArmGuard = enableArmGuard;
            clampArmStretchMuscles = clampStretchMuscles;
            armStretchMuscleLimit = Mathf.Clamp(stretchLimit, 0f, 0.5f);
            upperArmTwistMuscleLimit = Mathf.Clamp(upperTwistLimit, 0.1f, 1f);
            lowerArmTwistMuscleLimit = Mathf.Clamp(lowerTwistLimit, 0.1f, 1f);
            lockHumanoidBonePositions = lockBonePositions;
            lockLimbChildLocalPositions = lockLimbChildPositions;
            lockLimbChildLocalRotations = lockLimbChildRotations;
            logCorrections = logCorrectionMessages;
        }

        public void RecaptureBaseline()
        {
            if (!InitializeIfNeeded())
            {
                return;
            }

            CaptureBaseline();
        }

        public void SetLimbChildRotationExclusions(IEnumerable<Transform> excludedTransforms)
        {
            _limbChildRotationExclusions.Clear();

            if (excludedTransforms == null)
            {
                return;
            }

            foreach (Transform excludedTransform in excludedTransforms)
            {
                if (excludedTransform == null)
                {
                    continue;
                }

                _limbChildRotationExclusions.Add(excludedTransform);
            }
        }

        public static int ClampMusclesToHumanRange(ref HumanPose pose)
        {
            if (pose.muscles == null)
            {
                return 0;
            }

            int changed = 0;
            int count = Mathf.Min(pose.muscles.Length, HumanTrait.MuscleCount);
            for (int i = 0; i < count; i++)
            {
                if (IsFingerMuscle(HumanTrait.MuscleName[i]))
                {
                    continue;
                }

                float before = pose.muscles[i];
                float after = Mathf.Clamp(before, -1f, 1f);
                if (Mathf.Approximately(before, after))
                {
                    continue;
                }

                pose.muscles[i] = after;
                changed++;
            }

            return changed;
        }

        private static bool IsFingerMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalizedName = muscleName.Replace(" ", "").ToLowerInvariant();
            return normalizedName.Contains("thumb")
                || normalizedName.Contains("index")
                || normalizedName.Contains("middle")
                || normalizedName.Contains("ring")
                || normalizedName.Contains("little");
        }

        public static int ClampAnatomicalArmMuscles(
            ref HumanPose pose,
            float stretchLimit,
            float upperTwistLimit,
            float lowerTwistLimit,
            bool clampStretchMuscles = false)
        {
            if (pose.muscles == null)
            {
                return 0;
            }

            int count = Mathf.Min(pose.muscles.Length, HumanTrait.MuscleCount);
            int changed = 0;
            float safeStretchLimit = Mathf.Clamp(stretchLimit, 0f, 0.5f);
            float safeUpperTwistLimit = Mathf.Clamp(upperTwistLimit, 0.1f, 1f);
            float safeLowerTwistLimit = Mathf.Clamp(lowerTwistLimit, 0.1f, 1f);

            for (int i = 0; i < count; i++)
            {
                string muscleName = HumanTrait.MuscleName[i];
                if (string.IsNullOrEmpty(muscleName))
                {
                    continue;
                }

                string normalizedName = muscleName.Replace(" ", "").ToLowerInvariant();
                if (!normalizedName.Contains("arm") && !normalizedName.Contains("forearm"))
                {
                    continue;
                }

                float before = pose.muscles[i];
                float after = before;

                if (normalizedName.Contains("stretch"))
                {
                    if (!clampStretchMuscles)
                    {
                        continue;
                    }

                    after = Mathf.Clamp(before, -safeStretchLimit, safeStretchLimit);
                }
                else if (normalizedName.Contains("forearm") && normalizedName.Contains("twist"))
                {
                    after = Mathf.Clamp(before, -safeLowerTwistLimit, safeLowerTwistLimit);
                }
                else if (normalizedName.Contains("arm") && normalizedName.Contains("twist"))
                {
                    after = Mathf.Clamp(before, -safeUpperTwistLimit, safeUpperTwistLimit);
                }

                if (Mathf.Approximately(before, after))
                {
                    continue;
                }

                pose.muscles[i] = after;
                changed++;
            }

            return changed;
        }

        private bool InitializeIfNeeded()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator == null || _animator.avatar == null || !_animator.avatar.isValid || !_animator.avatar.isHuman)
            {
                return false;
            }

            if (_initialized && _poseHandler != null && _cachedAvatar == _animator.avatar && _cachedRoot == _animator.transform)
            {
                return true;
            }

            _cachedAvatar = _animator.avatar;
            _cachedRoot = _animator.transform;
            _poseHandler = new HumanPoseHandler(_cachedAvatar, _cachedRoot);
            _pose = new HumanPose();
            _initialized = true;
            CaptureBaseline();
            return true;
        }

        private void CaptureBaseline()
        {
            _initialLocalScales.Clear();
            _initialHumanoidLocalPositions.Clear();
            _initialLimbChildLocalPositions.Clear();
            _initialLimbChildLocalRotations.Clear();
            _poseWarningLogged = false;
            _scaleWarningLogged = false;
            _positionWarningLogged = false;
            _limbChildPositionWarningLogged = false;
            _limbChildRotationWarningLogged = false;
            _muscleWarningLogged = false;
            _armWarningLogged = false;

            foreach (Transform targetTransform in GetComponentsInChildren<Transform>(true))
            {
                _initialLocalScales[targetTransform] = targetTransform.localScale;
            }

            if (_animator == null || !_animator.isHuman)
            {
                return;
            }

            var humanoidBones = new HashSet<Transform>();
            for (int i = (int)HumanBodyBones.Hips; i < (int)HumanBodyBones.LastBone; i++)
            {
                HumanBodyBones bone = (HumanBodyBones)i;
                if (bone == HumanBodyBones.Hips)
                {
                    continue;
                }

                Transform boneTransform = _animator.GetBoneTransform(bone);
                if (boneTransform == null || _initialHumanoidLocalPositions.ContainsKey(boneTransform))
                {
                    continue;
                }

                _initialHumanoidLocalPositions[boneTransform] = boneTransform.localPosition;
                humanoidBones.Add(boneTransform);
            }

            CaptureLimbChildLocalPositions(humanoidBones);
        }

        private void CaptureLimbChildLocalPositions(HashSet<Transform> humanoidBones)
        {
            if (_animator == null)
            {
                return;
            }

            foreach (HumanBodyBones rootBone in LimbPositionRoots)
            {
                Transform root = _animator.GetBoneTransform(rootBone);
                if (root == null)
                {
                    continue;
                }

                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || child == transform || humanoidBones.Contains(child) || _initialLimbChildLocalPositions.ContainsKey(child))
                    {
                        continue;
                    }

                    _initialLimbChildLocalPositions[child] = child.localPosition;
                    _initialLimbChildLocalRotations[child] = child.localRotation;
                }
            }
        }

        private void RestoreHumanoidLocalPositions()
        {
            if (!lockHumanoidBonePositions)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Vector3> snapshot in _initialHumanoidLocalPositions)
            {
                Transform bone = snapshot.Key;
                if (bone == null || (bone.localPosition - snapshot.Value).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                LogOnce(ref _positionWarningLogged, $"Humanoid 본 localPosition 변형을 복구했습니다. 첫 본: {bone.name}");
                bone.localPosition = snapshot.Value;
            }
        }

        private void RestoreLimbChildLocalPositions()
        {
            if (!lockLimbChildLocalPositions)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Vector3> snapshot in _initialLimbChildLocalPositions)
            {
                Transform child = snapshot.Key;
                if (child == null || (child.localPosition - snapshot.Value).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                LogOnce(ref _limbChildPositionWarningLogged, $"Limb child Transform localPosition 변형을 복구했습니다. 첫 Transform: {child.name}");
                child.localPosition = snapshot.Value;
            }
        }

        private void RestoreLimbChildLocalRotations()
        {
            if (!lockLimbChildLocalRotations)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Quaternion> snapshot in _initialLimbChildLocalRotations)
            {
                Transform child = snapshot.Key;
                if (child == null ||
                    _limbChildRotationExclusions.Contains(child) ||
                    Mathf.Abs(Quaternion.Dot(child.localRotation, snapshot.Value)) >= 0.999999f)
                {
                    continue;
                }

                LogOnce(ref _limbChildRotationWarningLogged, $"Limb child Transform localRotation 변형을 복구했습니다. 첫 Transform: {child.name}");
                child.localRotation = snapshot.Value;
            }
        }

        private void RestoreLocalScales()
        {
            if (!restoreLocalScales)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Vector3> snapshot in _initialLocalScales)
            {
                Transform targetTransform = snapshot.Key;
                if (targetTransform == null || (targetTransform.localScale - snapshot.Value).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                LogOnce(ref _scaleWarningLogged, $"Transform localScale 변형을 복구했습니다. 첫 Transform: {targetTransform.name}");
                targetTransform.localScale = snapshot.Value;
            }
        }

        private void LogOnce(ref bool flag, string message)
        {
            if (flag || !logCorrections)
            {
                flag = true;
                return;
            }

            Debug.LogWarning($"[HumanoidArmDeformationGuard] {message}");
            flag = true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(HumanPose pose)
        {
            if (!IsFinite(pose.bodyPosition) || !IsFinite(pose.bodyRotation))
            {
                return false;
            }

            if (pose.muscles == null)
            {
                return true;
            }

            foreach (float muscle in pose.muscles)
            {
                if (!IsFinite(muscle))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(29900)]
    public class HumanoidThumbDeformationGuard : MonoBehaviour
    {
        [SerializeField] private Animator targetAnimator;
        [SerializeField, Range(0f, 90f)] private float proximalMaxLocalAngle = 10f;
        [SerializeField, Range(0f, 120f)] private float intermediateMaxLocalAngle = 55f;
        [SerializeField, Range(0f, 120f)] private float distalMaxLocalAngle = 55f;
        [SerializeField] private Vector3 proximalLocalRotationOffset;
        [SerializeField] private bool mirrorRightProximalLocalRotationOffset = true;
        [SerializeField] private Vector3 leftProximalLocalRotationOffset;
        [SerializeField] private Vector3 rightProximalLocalRotationOffset;
        [SerializeField] private bool logCorrections;
        [SerializeField] private bool clampHumanoidThumbRotations = true;
        [SerializeField] private bool syncDetachedThumbBaseHelpers = true;
        [SerializeField] private bool syncDetachedThumbBaseHelperPositions = true;
        [SerializeField, Range(0f, 1f)] private float detachedThumbBaseHelperSyncWeight = 0.8f;
        [SerializeField, Range(0f, 45f)] private float detachedThumbBaseHelperMaxLocalAngle = 28f;
        [SerializeField] private bool stabilizeDetachedThumbBasePalm = false;
        [SerializeField, Range(0f, 1f)] private float detachedThumbBasePalmStabilizeWeight = 0f;
        [SerializeField, Range(0f, 45f)] private float detachedThumbBasePalmMaxLocalAngle = 45f;
        [SerializeField] private bool stabilizeThumbWebbingCrease = true;
        [SerializeField, Range(0f, 1f)] private float thumbWebbingCreaseStabilizeWeight = 0.35f;
        [SerializeField, Range(0f, 45f)] private float thumbWebbingCreaseMaxLocalAngle = 18f;
        [SerializeField, Range(0f, 0.02f)] private float thumbWebbingCreaseMaxPositionOffset = 0.005f;
        [SerializeField] private bool enableThumbVisualLengthGuard = true;
        [SerializeField, Range(0f, 1f)] private float thumbProjectionMinPalmNormal = 0.36f;
        [SerializeField, Range(0f, 1f)] private float thumbProjectionMaxPalmNormal = 0.5f;
        [SerializeField, Range(0f, 1f)] private float thumbProjectionGuardWeight = 1f;
        [SerializeField, Range(0f, 90f)] private float thumbIndexMaxSpreadAngle = 42f;
        [SerializeField, Range(0f, 1f)] private float thumbIndexSpreadGuardWeight = 1f;
        [SerializeField, Range(0f, 60f)] private float thumbMaxSegmentBendAngle = 10f;
        [SerializeField, Range(0f, 1f)] private float thumbSegmentStraightenWeight = 0.9f;

        private readonly Dictionary<Transform, Quaternion> _initialLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Vector3> _initialLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly HashSet<Transform> _thumbBaseHelperTransforms = new HashSet<Transform>();
        private readonly Dictionary<Transform, Transform> _detachedThumbBaseHelperSources = new Dictionary<Transform, Transform>();
        private readonly Dictionary<Transform, Quaternion> _detachedThumbBaseSourceInitialLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> _lastRawLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> _lastCorrectedLocalRotations = new Dictionary<Transform, Quaternion>();
        private bool _warningLogged;
        private const float LocalRotationOvershootRatio = 0.35f;
        private const float LocalRotationHardOvershootDegrees = 8f;
        private const float DetachedThumbBaseHelperMaxPositionOffset = 0.008f;

        private static readonly HumanBodyBones[] ThumbBones =
        {
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal
        };

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
            if (!InitializeIfNeeded())
            {
                return;
            }

            int changed = 0;
            if (clampHumanoidThumbRotations)
            {
                foreach (HumanBodyBones thumbBone in ThumbBones)
                {
                    Transform thumbTransform = targetAnimator.GetBoneTransform(thumbBone);
                    if (thumbTransform == null ||
                        !_initialLocalRotations.TryGetValue(thumbTransform, out Quaternion initialRotation))
                    {
                        continue;
                    }

                    float limit = GetLimit(thumbBone);
                    Quaternion rawRotation = GetCurrentRawLocalRotation(thumbTransform);
                    if (!IsFinite(rawRotation))
                    {
                        SetCorrectedLocalRotation(thumbTransform, initialRotation, initialRotation);
                        changed++;
                        continue;
                    }

                    Quaternion offsetRotation = GetThumbRotationOffset(thumbBone);
                    Quaternion baselineRotation = ApplyLimitSpaceOffset(initialRotation, offsetRotation);
                    Quaternion currentRotation = ApplyLimitSpaceOffset(rawRotation, offsetRotation);
                    if (limit <= 0f)
                    {
                        if (Quaternion.Angle(initialRotation, rawRotation) > 0.001f)
                        {
                            SetCorrectedLocalRotation(thumbTransform, rawRotation, initialRotation);
                            changed++;
                        }

                        continue;
                    }

                    float angle = Quaternion.Angle(baselineRotation, currentRotation);
                    if (angle <= limit)
                    {
                        if (Quaternion.Angle(thumbTransform.localRotation, rawRotation) > 0.001f)
                        {
                            SetCorrectedLocalRotation(thumbTransform, rawRotation, rawRotation);
                            changed++;
                        }

                        continue;
                    }

                    Quaternion limitedRotation = LimitLocalRotation(baselineRotation, currentRotation, limit);
                    SetCorrectedLocalRotation(thumbTransform, rawRotation, RemoveLimitSpaceOffset(limitedRotation, offsetRotation));
                    changed++;
                }

                changed += ClampThumbBaseHelperTransforms();
            }

            if (enableThumbVisualLengthGuard)
            {
                changed += PreserveThumbVisualLength();
            }

            changed += SyncDetachedThumbBaseHelperTransforms();

            if (changed > 0 && logCorrections && !_warningLogged)
            {
                Debug.LogWarning($"[HumanoidThumbDeformationGuard] 엄지 본 localRotation {changed}개를 최종 렌더 포즈에서 제한했습니다.");
                _warningLogged = true;
            }
        }

        public void Configure(
            Animator animator,
            float proximalLimit,
            float intermediateLimit,
            float distalLimit,
            Vector3 proximalOffset,
            bool mirrorRightOffset,
            Vector3 leftProximalOffset,
            Vector3 rightProximalOffset,
            bool logCorrectionMessages,
            bool clampHumanoidRotations = true,
            bool syncDetachedBaseHelpers = true,
            bool syncDetachedBaseHelperPositions = true,
            float detachedBaseHelperSyncWeight = 1f,
            float detachedBaseHelperMaxLocalAngle = 45f,
            bool stabilizeDetachedBasePalm = false,
            float detachedBasePalmStabilizeWeight = 0f,
            float detachedBasePalmMaxLocalAngle = 45f,
            bool enableVisualLengthGuard = true,
            float projectionMinPalmNormal = 0.32f,
            float projectionMaxPalmNormal = 0.5f,
            float projectionGuardWeight = 0.9f,
            float indexMaxSpreadAngle = 44f,
            float indexSpreadGuardWeight = 0.9f,
            float maxSegmentBendAngle = 10f,
            float segmentStraightenWeight = 0.9f,
            bool stabilizeWebbingCrease = true,
            float webbingCreaseStabilizeWeight = 0.35f,
            float webbingCreaseMaxLocalAngle = 18f,
            float webbingCreaseMaxPositionOffset = 0.005f)
        {
            targetAnimator = animator;
            proximalMaxLocalAngle = Mathf.Clamp(proximalLimit, 0f, 90f);
            intermediateMaxLocalAngle = Mathf.Clamp(intermediateLimit, 0f, 120f);
            distalMaxLocalAngle = Mathf.Clamp(distalLimit, 0f, 120f);
            proximalLocalRotationOffset = proximalOffset;
            mirrorRightProximalLocalRotationOffset = mirrorRightOffset;
            leftProximalLocalRotationOffset = leftProximalOffset;
            rightProximalLocalRotationOffset = rightProximalOffset;
            logCorrections = logCorrectionMessages;
            clampHumanoidThumbRotations = clampHumanoidRotations;
            syncDetachedThumbBaseHelpers = syncDetachedBaseHelpers;
            syncDetachedThumbBaseHelperPositions = syncDetachedBaseHelperPositions;
            detachedThumbBaseHelperSyncWeight = Mathf.Clamp01(detachedBaseHelperSyncWeight);
            detachedThumbBaseHelperMaxLocalAngle = Mathf.Clamp(detachedBaseHelperMaxLocalAngle, 0f, 45f);
            stabilizeDetachedThumbBasePalm = stabilizeDetachedBasePalm;
            detachedThumbBasePalmStabilizeWeight = Mathf.Clamp01(detachedBasePalmStabilizeWeight);
            detachedThumbBasePalmMaxLocalAngle = Mathf.Clamp(detachedBasePalmMaxLocalAngle, 0f, 45f);
            stabilizeThumbWebbingCrease = stabilizeWebbingCrease;
            thumbWebbingCreaseStabilizeWeight = Mathf.Clamp01(webbingCreaseStabilizeWeight);
            thumbWebbingCreaseMaxLocalAngle = Mathf.Clamp(webbingCreaseMaxLocalAngle, 0f, 45f);
            thumbWebbingCreaseMaxPositionOffset = Mathf.Clamp(webbingCreaseMaxPositionOffset, 0f, 0.02f);
            enableThumbVisualLengthGuard = enableVisualLengthGuard;
            thumbProjectionMinPalmNormal = Mathf.Clamp01(projectionMinPalmNormal);
            thumbProjectionMaxPalmNormal = Mathf.Clamp01(projectionMaxPalmNormal);
            thumbProjectionGuardWeight = Mathf.Clamp01(projectionGuardWeight);
            thumbIndexMaxSpreadAngle = Mathf.Clamp(indexMaxSpreadAngle, 0f, 90f);
            thumbIndexSpreadGuardWeight = Mathf.Clamp01(indexSpreadGuardWeight);
            thumbMaxSegmentBendAngle = Mathf.Clamp(maxSegmentBendAngle, 0f, 60f);
            thumbSegmentStraightenWeight = Mathf.Clamp01(segmentStraightenWeight);
            RecaptureBaseline();
        }

        public void RecaptureBaseline()
        {
            _initialLocalRotations.Clear();
            _initialLocalPositions.Clear();
            _thumbBaseHelperTransforms.Clear();
            _detachedThumbBaseHelperSources.Clear();
            _detachedThumbBaseSourceInitialLocalRotations.Clear();
            _lastRawLocalRotations.Clear();
            _lastCorrectedLocalRotations.Clear();
            _warningLogged = false;

            if (!InitializeIfNeeded())
            {
                return;
            }

            foreach (HumanBodyBones thumbBone in ThumbBones)
            {
                Transform thumbTransform = targetAnimator.GetBoneTransform(thumbBone);
                if (thumbTransform == null || _initialLocalRotations.ContainsKey(thumbTransform))
                {
                    continue;
                }

                _initialLocalRotations[thumbTransform] = thumbTransform.localRotation;
                _initialLocalPositions[thumbTransform] = thumbTransform.localPosition;
            }

            CaptureThumbBaseHelperRotations();
            CaptureDetachedThumbBaseHelperSources();
        }

        private bool InitializeIfNeeded()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<Animator>();
            }

            return targetAnimator != null &&
                targetAnimator.avatar != null &&
                targetAnimator.avatar.isValid &&
                targetAnimator.avatar.isHuman;
        }

        private float GetLimit(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                case HumanBodyBones.RightThumbProximal:
                    return proximalMaxLocalAngle;
                case HumanBodyBones.LeftThumbIntermediate:
                case HumanBodyBones.RightThumbIntermediate:
                    return intermediateMaxLocalAngle;
                case HumanBodyBones.LeftThumbDistal:
                case HumanBodyBones.RightThumbDistal:
                    return distalMaxLocalAngle;
                default:
                    return 0f;
            }
        }

        private int ClampThumbBaseHelperTransforms()
        {
            int changed = 0;
            foreach (Transform thumbTransform in _thumbBaseHelperTransforms)
            {
                if (thumbTransform == null ||
                    !_initialLocalRotations.TryGetValue(thumbTransform, out Quaternion initialRotation))
                {
                    continue;
                }

                Quaternion rawRotation = GetCurrentRawLocalRotation(thumbTransform);
                if (!IsFinite(rawRotation))
                {
                    SetCorrectedLocalRotation(thumbTransform, initialRotation, initialRotation);
                    changed++;
                    continue;
                }

                Quaternion offsetRotation = GetProximalRotationOffsetRotation(thumbTransform);
                Quaternion baselineRotation = ApplyLimitSpaceOffset(initialRotation, offsetRotation);
                Quaternion currentRotation = ApplyLimitSpaceOffset(rawRotation, offsetRotation);
                float limit = proximalMaxLocalAngle;
                if (limit <= 0f)
                {
                    if (Quaternion.Angle(initialRotation, rawRotation) > 0.001f)
                    {
                        SetCorrectedLocalRotation(thumbTransform, rawRotation, initialRotation);
                        changed++;
                    }

                    continue;
                }

                float angle = Quaternion.Angle(baselineRotation, currentRotation);
                if (angle <= limit)
                {
                    if (Quaternion.Angle(thumbTransform.localRotation, rawRotation) > 0.001f)
                    {
                        SetCorrectedLocalRotation(thumbTransform, rawRotation, rawRotation);
                        changed++;
                    }

                    continue;
                }

                Quaternion limitedRotation = LimitLocalRotation(baselineRotation, currentRotation, limit);
                SetCorrectedLocalRotation(thumbTransform, rawRotation, RemoveLimitSpaceOffset(limitedRotation, offsetRotation));
                changed++;
            }

            return changed;
        }

        private int PreserveThumbVisualLength()
        {
            if ((thumbProjectionGuardWeight <= 0f || (thumbProjectionMinPalmNormal <= 0.001f && thumbProjectionMaxPalmNormal >= 0.999f)) &&
                (thumbIndexSpreadGuardWeight <= 0f || thumbIndexMaxSpreadAngle >= 89.999f) &&
                (thumbSegmentStraightenWeight <= 0f || thumbMaxSegmentBendAngle >= 59.999f))
            {
                return 0;
            }

            return PreserveThumbVisualLength(false) + PreserveThumbVisualLength(true);
        }

        private int PreserveThumbVisualLength(bool isRightThumb)
        {
            Transform proximal = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            Transform intermediate = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);
            Transform distal = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);

            if (proximal == null || intermediate == null)
            {
                return 0;
            }

            int changed = ProjectThumbProximalIntoPalmFrame(proximal, intermediate, isRightThumb);
            if (distal != null)
            {
                changed += StraightenThumbSegmentBend(proximal, intermediate, distal);
            }

            return changed;
        }

        private int ProjectThumbProximalIntoPalmFrame(Transform proximal, Transform intermediate, bool isRightThumb)
        {
            if (!TryBuildPalmFrame(isRightThumb, out Vector3 sideAxis, out Vector3 palmNormal, out Vector3 forwardAxis))
            {
                return 0;
            }

            Transform hand = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform index = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Vector3 direction = intermediate.position - proximal.position;
            if (!TryNormalize(direction, out direction))
            {
                return 0;
            }

            Vector3 targetDirection = direction;
            float correctionWeight = 0f;

            if (hand != null &&
                index != null &&
                thumbIndexSpreadGuardWeight > 0f &&
                thumbIndexMaxSpreadAngle < 89.999f &&
                TryNormalize(index.position - hand.position, out Vector3 indexDirection))
            {
                float spreadAngle = Vector3.Angle(targetDirection, indexDirection);
                if (spreadAngle > thumbIndexMaxSpreadAngle)
                {
                    targetDirection = Vector3.RotateTowards(
                        targetDirection,
                        indexDirection,
                        (spreadAngle - thumbIndexMaxSpreadAngle) * Mathf.Deg2Rad,
                        0f);
                    correctionWeight = Mathf.Max(correctionWeight, Mathf.Clamp01(thumbIndexSpreadGuardWeight));
                }
            }

            float side = Vector3.Dot(targetDirection, sideAxis);
            float normal = Vector3.Dot(targetDirection, palmNormal);
            float forward = Vector3.Dot(targetDirection, forwardAxis);
            float minNormal = Mathf.Clamp01(thumbProjectionMinPalmNormal);
            float maxNormal = Mathf.Clamp(Mathf.Max(thumbProjectionMaxPalmNormal, minNormal), 0f, 1f);
            float clampedNormal = Mathf.Clamp(normal, minNormal, maxNormal);
            if (Mathf.Abs(clampedNormal - normal) > 0.001f)
            {
                targetDirection =
                    sideAxis * side +
                    palmNormal * clampedNormal +
                    forwardAxis * forward;
                correctionWeight = Mathf.Max(correctionWeight, Mathf.Clamp01(thumbProjectionGuardWeight));
            }

            if (!TryNormalize(targetDirection, out targetDirection))
            {
                return 0;
            }

            if (correctionWeight <= 0f)
            {
                return 0;
            }

            targetDirection = Vector3.Slerp(direction, targetDirection, correctionWeight);
            if (!TryNormalize(targetDirection, out targetDirection) ||
                Vector3.Angle(direction, targetDirection) <= 0.1f)
            {
                return 0;
            }

            ApplyWorldRotationCorrection(proximal, Quaternion.FromToRotation(direction, targetDirection) * proximal.rotation);
            return 1;
        }

        private int StraightenThumbSegmentBend(Transform proximal, Transform intermediate, Transform distal)
        {
            if (thumbSegmentStraightenWeight <= 0f || thumbMaxSegmentBendAngle >= 59.999f)
            {
                return 0;
            }

            Vector3 proximalDirection = intermediate.position - proximal.position;
            Vector3 intermediateDirection = distal.position - intermediate.position;
            if (!TryNormalize(proximalDirection, out proximalDirection) ||
                !TryNormalize(intermediateDirection, out intermediateDirection))
            {
                return 0;
            }

            float bendAngle = Vector3.Angle(proximalDirection, intermediateDirection);
            if (bendAngle <= thumbMaxSegmentBendAngle)
            {
                return 0;
            }

            Vector3 targetDirection = Vector3.Slerp(
                intermediateDirection,
                proximalDirection,
                Mathf.Clamp01(thumbSegmentStraightenWeight));

            if (!TryNormalize(targetDirection, out targetDirection) ||
                Vector3.Angle(intermediateDirection, targetDirection) <= 0.1f)
            {
                return 0;
            }

            ApplyWorldRotationCorrection(intermediate, Quaternion.FromToRotation(intermediateDirection, targetDirection) * intermediate.rotation);
            return 1;
        }

        private bool TryBuildPalmFrame(bool isRightThumb, out Vector3 sideAxis, out Vector3 palmNormal, out Vector3 forwardAxis)
        {
            sideAxis = Vector3.zero;
            palmNormal = Vector3.zero;
            forwardAxis = Vector3.zero;

            Transform hand = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform index = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform middle = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform little = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            if (hand == null || index == null || middle == null || little == null)
            {
                return false;
            }

            Vector3 rawSide = index.position - little.position;
            if (isRightThumb)
            {
                rawSide = -rawSide;
            }

            Vector3 rawForward = ((index.position + middle.position + little.position) / 3f) - hand.position;
            if (!TryNormalize(rawSide, out sideAxis) ||
                !TryNormalize(rawForward, out forwardAxis) ||
                !TryNormalize(Vector3.Cross(sideAxis, forwardAxis), out palmNormal) ||
                !TryNormalize(Vector3.Cross(palmNormal, sideAxis), out forwardAxis))
            {
                return false;
            }

            return true;
        }

        private void ApplyWorldRotationCorrection(Transform targetTransform, Quaternion correctedWorldRotation)
        {
            Quaternion rawLocalRotation = _lastRawLocalRotations.TryGetValue(targetTransform, out Quaternion lastRawRotation)
                ? lastRawRotation
                : targetTransform.localRotation;

            targetTransform.rotation = correctedWorldRotation;
            _lastRawLocalRotations[targetTransform] = rawLocalRotation;
            _lastCorrectedLocalRotations[targetTransform] = targetTransform.localRotation;
        }

        private int SyncDetachedThumbBaseHelperTransforms()
        {
            bool useWebbingGuard = stabilizeThumbWebbingCrease && thumbWebbingCreaseStabilizeWeight > 0f;
            if ((!syncDetachedThumbBaseHelpers || detachedThumbBaseHelperSyncWeight <= 0f) &&
                (!stabilizeDetachedThumbBasePalm || detachedThumbBasePalmStabilizeWeight <= 0f) &&
                !useWebbingGuard)
            {
                return 0;
            }

            int changed = 0;
            foreach (KeyValuePair<Transform, Transform> pair in _detachedThumbBaseHelperSources)
            {
                Transform helperTransform = pair.Key;
                Transform sourceTransform = pair.Value;
                if (helperTransform == null || sourceTransform == null)
                {
                    continue;
                }

                Quaternion targetRotation = CalculateDetachedThumbBaseHelperTargetRotation(helperTransform, sourceTransform);

                if (Quaternion.Angle(helperTransform.localRotation, targetRotation) > 0.001f)
                {
                    helperTransform.localRotation = targetRotation;
                    changed++;
                }

                if (!syncDetachedThumbBaseHelperPositions)
                {
                    if ((stabilizeDetachedThumbBasePalm || useWebbingGuard) &&
                        _initialLocalPositions.TryGetValue(helperTransform, out Vector3 anchoredPosition) &&
                        (helperTransform.localPosition - anchoredPosition).sqrMagnitude > 0.00000001f)
                    {
                        // YYB 손꿈치 스킨용 Thumb0 보조본은 위치까지 움직이면 엄지 뿌리 메시가 손바닥 밖으로 끌려간다.
                        // 실제 엄지 구동본은 별도로 움직이고, 보조본 위치는 초기 손바닥 앵커에 고정한다.
                        helperTransform.localPosition = anchoredPosition;
                        changed++;
                    }

                    continue;
                }

                Vector3 targetPosition = GetSourcePositionInHelperParentSpace(helperTransform, sourceTransform);
                if (_initialLocalPositions.TryGetValue(helperTransform, out Vector3 initialPosition) &&
                    detachedThumbBaseHelperSyncWeight < 0.999f)
                {
                    targetPosition = Vector3.Lerp(initialPosition, targetPosition, detachedThumbBaseHelperSyncWeight);
                }

                if (_initialLocalPositions.TryGetValue(helperTransform, out initialPosition))
                {
                    targetPosition = initialPosition + Vector3.ClampMagnitude(
                        targetPosition - initialPosition,
                        DetachedThumbBaseHelperMaxPositionOffset);
                }

                if (useWebbingGuard)
                {
                    targetPosition = ConstrainThumbWebbingHelperPosition(helperTransform, targetPosition);
                }

                if ((helperTransform.localPosition - targetPosition).sqrMagnitude <= 0.00000001f)
                {
                    continue;
                }

                helperTransform.localPosition = targetPosition;
                changed++;
            }

            return changed;
        }

        private Quaternion CalculateDetachedThumbBaseHelperTargetRotation(Transform helperTransform, Transform sourceTransform)
        {
            if (!_initialLocalRotations.TryGetValue(helperTransform, out Quaternion helperInitialRotation))
            {
                return sourceTransform.localRotation;
            }

            Quaternion targetRotation = helperInitialRotation;
            if (syncDetachedThumbBaseHelpers && detachedThumbBaseHelperSyncWeight > 0f)
            {
                Quaternion sourceRotation = sourceTransform.localRotation;
                targetRotation = sourceRotation;
                if (_detachedThumbBaseSourceInitialLocalRotations.TryGetValue(sourceTransform, out Quaternion sourceInitialRotation))
                {
                    Quaternion sourceDelta = Quaternion.Inverse(sourceInitialRotation) * sourceRotation;
                    targetRotation = helperInitialRotation * sourceDelta;
                }

                if (detachedThumbBaseHelperSyncWeight < 0.999f)
                {
                    targetRotation = Quaternion.Slerp(helperInitialRotation, targetRotation, detachedThumbBaseHelperSyncWeight);
                }
            }

            if (stabilizeDetachedThumbBasePalm && detachedThumbBasePalmStabilizeWeight > 0f)
            {
                // YYB 손꿈치 스킨은 joint_*Thumb0 회전에 강하게 끌린다.
                // 엄지 구동본은 따로 움직이게 두고, 스킨용 Thumb0 보조본은 기본 손바닥 자세를 우선 보존한다.
                targetRotation = Quaternion.Slerp(targetRotation, helperInitialRotation, detachedThumbBasePalmStabilizeWeight);
            }

            if (stabilizeThumbWebbingCrease && thumbWebbingCreaseStabilizeWeight > 0f)
            {
                // 엄지 웹빙 경계는 joint_*Thumb0 보조본의 작은 회전 차이에도 선처럼 접혀 보인다.
                // 엄지 구동본 자체는 유지하고, 스킨용 보조본만 초기 손바닥 경계 형태 쪽으로 약하게 되돌린다.
                targetRotation = Quaternion.Slerp(targetRotation, helperInitialRotation, thumbWebbingCreaseStabilizeWeight);
            }

            float maxLocalAngle = Mathf.Clamp(detachedThumbBaseHelperMaxLocalAngle, 0f, 45f);
            if (stabilizeDetachedThumbBasePalm && detachedThumbBasePalmStabilizeWeight > 0f)
            {
                maxLocalAngle = Mathf.Min(maxLocalAngle, Mathf.Clamp(detachedThumbBasePalmMaxLocalAngle, 0f, 45f));
            }

            if (stabilizeThumbWebbingCrease && thumbWebbingCreaseStabilizeWeight > 0f)
            {
                maxLocalAngle = Mathf.Min(maxLocalAngle, Mathf.Clamp(thumbWebbingCreaseMaxLocalAngle, 0f, 45f));
            }

            if (maxLocalAngle <= 0.001f)
            {
                return helperInitialRotation;
            }

            return Quaternion.Angle(helperInitialRotation, targetRotation) > maxLocalAngle
                ? Quaternion.RotateTowards(helperInitialRotation, targetRotation, maxLocalAngle)
                : targetRotation;
        }

        private Vector3 ConstrainThumbWebbingHelperPosition(Transform helperTransform, Vector3 targetPosition)
        {
            if (helperTransform == null ||
                !_initialLocalPositions.TryGetValue(helperTransform, out Vector3 initialPosition))
            {
                return targetPosition;
            }

            float weight = Mathf.Clamp01(thumbWebbingCreaseStabilizeWeight);
            if (weight > 0f)
            {
                targetPosition = Vector3.Lerp(targetPosition, initialPosition, weight);
            }

            float maxOffset = Mathf.Clamp(thumbWebbingCreaseMaxPositionOffset, 0f, 0.02f);
            if (maxOffset <= 0.000001f)
            {
                return initialPosition;
            }

            return initialPosition + Vector3.ClampMagnitude(targetPosition - initialPosition, maxOffset);
        }

        private static Vector3 GetSourcePositionInHelperParentSpace(Transform helperTransform, Transform sourceTransform)
        {
            if (helperTransform == null || sourceTransform == null)
            {
                return Vector3.zero;
            }

            if (helperTransform.parent == sourceTransform.parent)
            {
                return sourceTransform.localPosition;
            }

            return helperTransform.parent != null
                ? helperTransform.parent.InverseTransformPoint(sourceTransform.position)
                : sourceTransform.position;
        }

        private void CaptureThumbBaseHelperRotations()
        {
            if (targetAnimator == null)
            {
                return;
            }

            foreach (Transform candidate in targetAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == null || !IsThumbBaseHelperName(candidate.name))
                {
                    continue;
                }

                if (_initialLocalRotations.ContainsKey(candidate))
                {
                    continue;
                }

                _initialLocalRotations[candidate] = candidate.localRotation;
                _initialLocalPositions[candidate] = candidate.localPosition;
                _thumbBaseHelperTransforms.Add(candidate);
            }
        }

        private void CaptureDetachedThumbBaseHelperSources()
        {
            if (targetAnimator == null)
            {
                return;
            }

            foreach (Transform helperTransform in targetAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (helperTransform == null || !IsDetachedThumbBaseHelperName(helperTransform.name))
                {
                    continue;
                }

                Transform sourceTransform = FindMatchingActiveThumbBaseSource(helperTransform);
                if (sourceTransform == null)
                {
                    continue;
                }

                _detachedThumbBaseHelperSources[helperTransform] = sourceTransform;
                if (!_detachedThumbBaseSourceInitialLocalRotations.ContainsKey(sourceTransform))
                {
                    _detachedThumbBaseSourceInitialLocalRotations[sourceTransform] = sourceTransform.localRotation;
                }
            }
        }

        private Transform FindMatchingActiveThumbBaseSource(Transform helperTransform)
        {
            bool isRightThumb = IsRightThumbTransform(helperTransform);
            foreach (Transform candidate in targetAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == null || candidate == helperTransform)
                {
                    continue;
                }

                if (!IsActiveThumbBaseSourceName(candidate.name) || IsRightThumbTransform(candidate) != isRightThumb)
                {
                    continue;
                }

                return candidate;
            }

            return null;
        }

        private static bool IsThumbBaseHelperName(string transformName)
        {
            if (string.IsNullOrEmpty(transformName))
            {
                return false;
            }

            string normalizedName = transformName.ToLowerInvariant();
            if (!normalizedName.Contains("thumb0"))
            {
                return false;
            }

            return !normalizedName.Contains("thumb1") &&
                !normalizedName.Contains("thumb2") &&
                !normalizedName.Contains("thumbtip");
        }

        private static bool IsDetachedThumbBaseHelperName(string transformName)
        {
            if (string.IsNullOrEmpty(transformName))
            {
                return false;
            }

            string normalizedName = transformName.ToLowerInvariant();
            if (normalizedName.Contains("!") || normalizedName.Contains("ghost"))
            {
                return false;
            }

            return IsThumbBaseHelperName(transformName);
        }

        private static bool IsActiveThumbBaseSourceName(string transformName)
        {
            if (string.IsNullOrEmpty(transformName))
            {
                return false;
            }

            string normalizedName = transformName.ToLowerInvariant();
            return normalizedName.Contains("thumb0m") &&
                !normalizedName.Contains("ghost") &&
                !normalizedName.Contains("thumb1") &&
                !normalizedName.Contains("thumb2") &&
                !normalizedName.Contains("thumbtip");
        }

        private Quaternion GetThumbRotationOffset(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                    return GetProximalRotationOffsetRotation(false);
                case HumanBodyBones.RightThumbProximal:
                    return GetProximalRotationOffsetRotation(true);
                default:
                    return Quaternion.identity;
            }
        }

        private Quaternion GetProximalRotationOffsetRotation(Transform thumbTransform)
        {
            return GetProximalRotationOffsetRotation(IsRightThumbTransform(thumbTransform));
        }

        private Quaternion GetProximalRotationOffsetRotation(bool isRightThumb)
        {
            Vector3 offset = GetProximalRotationOffset(isRightThumb);
            if (offset.sqrMagnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(offset);
        }

        private static Quaternion ApplyLimitSpaceOffset(Quaternion localRotation, Quaternion offsetRotation)
        {
            return localRotation * offsetRotation;
        }

        private static Quaternion RemoveLimitSpaceOffset(Quaternion localRotation, Quaternion offsetRotation)
        {
            return localRotation * Quaternion.Inverse(offsetRotation);
        }

        private Vector3 GetProximalRotationOffset(bool isRightThumb)
        {
            Vector3 offset = proximalLocalRotationOffset;
            if (isRightThumb && mirrorRightProximalLocalRotationOffset)
            {
                offset = new Vector3(offset.x, -offset.y, -offset.z);
            }

            return offset + (isRightThumb ? rightProximalLocalRotationOffset : leftProximalLocalRotationOffset);
        }

        private static bool IsRightThumbTransform(Transform thumbTransform)
        {
            if (thumbTransform == null)
            {
                return false;
            }

            string normalizedName = thumbTransform.name.ToLowerInvariant();
            return normalizedName.Contains("right") || normalizedName.Contains("_r") || normalizedName.Contains("rthumb");
        }

        private Quaternion GetCurrentRawLocalRotation(Transform targetTransform)
        {
            Quaternion currentRotation = targetTransform.localRotation;
            if (_lastCorrectedLocalRotations.TryGetValue(targetTransform, out Quaternion lastCorrectedRotation) &&
                _lastRawLocalRotations.TryGetValue(targetTransform, out Quaternion lastRawRotation) &&
                Quaternion.Angle(currentRotation, lastCorrectedRotation) <= 0.001f)
            {
                return lastRawRotation;
            }

            return currentRotation;
        }

        private void SetCorrectedLocalRotation(Transform targetTransform, Quaternion rawRotation, Quaternion correctedRotation)
        {
            _lastRawLocalRotations[targetTransform] = rawRotation;
            _lastCorrectedLocalRotations[targetTransform] = correctedRotation;
            targetTransform.localRotation = correctedRotation;
        }

        private static Quaternion LimitLocalRotation(Quaternion initialRotation, Quaternion currentRotation, float softLimit)
        {
            float angle = Quaternion.Angle(initialRotation, currentRotation);
            float hardLimit = softLimit + LocalRotationHardOvershootDegrees;
            float targetAngle = Mathf.Min(hardLimit, softLimit + (angle - softLimit) * LocalRotationOvershootRatio);
            return Quaternion.RotateTowards(initialRotation, currentRotation, targetAngle);
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion rotation)
        {
            return IsFinite(rotation.x) &&
                IsFinite(rotation.y) &&
                IsFinite(rotation.z) &&
                IsFinite(rotation.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
