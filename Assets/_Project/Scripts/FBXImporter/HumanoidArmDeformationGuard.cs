using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fbx2Vmd.FBXImporter
{
    public sealed class ArmDeformationSettings
    {
        public bool ClampMusclesToHumanRange { get; }
        public bool EnableAnatomicalArmGuard { get; }
        public float StretchMuscleLimit { get; }
        public float UpperArmTwistMuscleLimit { get; }
        public float LowerArmTwistMuscleLimit { get; }
        public bool LockHumanoidBonePositions { get; }
        public bool LogCorrections { get; }
        public bool ClampArmStretchMuscles { get; }
        public bool LockLimbChildLocalPositions { get; }
        public bool LockLimbChildLocalRotations { get; }

        public ArmDeformationSettings(
            bool clampMusclesToHumanRange,
            bool enableAnatomicalArmGuard,
            float stretchMuscleLimit,
            float upperArmTwistMuscleLimit,
            float lowerArmTwistMuscleLimit,
            bool lockHumanoidBonePositions,
            bool logCorrections,
            bool clampArmStretchMuscles,
            bool lockLimbChildLocalPositions,
            bool lockLimbChildLocalRotations)
        {
            ClampMusclesToHumanRange = clampMusclesToHumanRange;
            EnableAnatomicalArmGuard = enableAnatomicalArmGuard;
            StretchMuscleLimit = stretchMuscleLimit;
            UpperArmTwistMuscleLimit = upperArmTwistMuscleLimit;
            LowerArmTwistMuscleLimit = lowerArmTwistMuscleLimit;
            LockHumanoidBonePositions = lockHumanoidBonePositions;
            LogCorrections = logCorrections;
            ClampArmStretchMuscles = clampArmStretchMuscles;
            LockLimbChildLocalPositions = lockLimbChildLocalPositions;
            LockLimbChildLocalRotations = lockLimbChildLocalRotations;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(25000)]
    public class HumanoidArmDeformationGuard : MonoBehaviour
    {
        [Header("팔 변형 방지")]
        [Tooltip("Humanoid muscle 값을 Unity 기본 안전 범위인 -1~1로 제한합니다.")]
        [FormerlySerializedAs("clampMusclesToHumanRange")]
        [SerializeField] private bool _clampMusclesToHumanRange= false;
        public bool clampMusclesToHumanRange { get => _clampMusclesToHumanRange; private set => _clampMusclesToHumanRange = value; }

        [Tooltip("팔이 늘어나거나 비정상적으로 비틀리는 Humanoid arm muscle 값을 제한합니다.")]
        [FormerlySerializedAs("enableAnatomicalArmGuard")]
        [SerializeField] private bool _enableAnatomicalArmGuard= false;
        public bool enableAnatomicalArmGuard { get => _enableAnatomicalArmGuard; set => _enableAnatomicalArmGuard = value; }

        [Tooltip("직접 Animator 재생에도 Forearm Stretch muscle을 제한합니다. 이 값은 팔꿈치 굽힘에 가까워 모션이 굳을 수 있으므로 기본값은 끕니다.")]
        [FormerlySerializedAs("clampArmStretchMuscles")]
        [SerializeField] private bool _clampArmStretchMuscles= false;
        public bool clampArmStretchMuscles { get => _clampArmStretchMuscles; private set => _clampArmStretchMuscles = value; }

        [Tooltip("Humanoid arm stretch muscle 허용치입니다. Forearm Stretch는 팔꿈치 굽힘에 가까우므로 직접 켤 때만 사용합니다.")]
        [Range(0f, 0.5f)]
        [FormerlySerializedAs("armStretchMuscleLimit")]
        [SerializeField] private float _armStretchMuscleLimit= 0f;
        public float armStretchMuscleLimit { get => _armStretchMuscleLimit; set => _armStretchMuscleLimit = value; }

        [Tooltip("상완 twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        [FormerlySerializedAs("upperArmTwistMuscleLimit")]
        [SerializeField] private float _upperArmTwistMuscleLimit= 0.75f;
        public float upperArmTwistMuscleLimit { get => _upperArmTwistMuscleLimit; private set => _upperArmTwistMuscleLimit = value; }

        [Tooltip("전완 twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        [FormerlySerializedAs("lowerArmTwistMuscleLimit")]
        [SerializeField] private float _lowerArmTwistMuscleLimit= 0.65f;
        public float lowerArmTwistMuscleLimit { get => _lowerArmTwistMuscleLimit; private set => _lowerArmTwistMuscleLimit = value; }

        [Tooltip("Humanoid 본 localPosition을 시작 값으로 복구해 팔/다리 길이 변형을 막습니다.")]
        [FormerlySerializedAs("lockHumanoidBonePositions")]
        [SerializeField] private bool _lockHumanoidBonePositions= true;
        public bool lockHumanoidBonePositions { get => _lockHumanoidBonePositions; private set => _lockHumanoidBonePositions = value; }

        [Tooltip("소매/팔 보조본처럼 Humanoid 매핑 밖의 팔/다리 하위 Transform localPosition을 시작 값으로 복구합니다.")]
        [FormerlySerializedAs("lockLimbChildLocalPositions")]
        [SerializeField] private bool _lockLimbChildLocalPositions= true;
        public bool lockLimbChildLocalPositions { get => _lockLimbChildLocalPositions; private set => _lockLimbChildLocalPositions = value; }

        [Tooltip("소매/팔 보조본처럼 Humanoid 매핑 밖의 팔/다리 하위 Transform localRotation을 시작 값으로 복구합니다.")]
        [FormerlySerializedAs("lockLimbChildLocalRotations")]
        [SerializeField] private bool _lockLimbChildLocalRotations= false;
        public bool lockLimbChildLocalRotations { get => _lockLimbChildLocalRotations; private set => _lockLimbChildLocalRotations = value; }

        [Tooltip("모델 전체 Transform localScale을 시작 값으로 복구합니다.")]
        [FormerlySerializedAs("restoreLocalScales")]
        [SerializeField] private bool _restoreLocalScales= true;
        public bool restoreLocalScales { get => _restoreLocalScales; private set => _restoreLocalScales = value; }

        [Tooltip("처음 보정이 발생했을 때 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logCorrections")]
        [SerializeField] private bool _logCorrections= false;
        public bool logCorrections { get => _logCorrections; private set => _logCorrections = value; }

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

        public void Configure(ArmDeformationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            clampMusclesToHumanRange = settings.ClampMusclesToHumanRange;
            enableAnatomicalArmGuard = settings.EnableAnatomicalArmGuard;
            clampArmStretchMuscles = settings.ClampArmStretchMuscles;
            armStretchMuscleLimit = Mathf.Clamp(settings.StretchMuscleLimit, 0f, 0.5f);
            upperArmTwistMuscleLimit = Mathf.Clamp(settings.UpperArmTwistMuscleLimit, 0.1f, 1f);
            lowerArmTwistMuscleLimit = Mathf.Clamp(settings.LowerArmTwistMuscleLimit, 0.1f, 1f);
            lockHumanoidBonePositions = settings.LockHumanoidBonePositions;
            lockLimbChildLocalPositions = settings.LockLimbChildLocalPositions;
            lockLimbChildLocalRotations = settings.LockLimbChildLocalRotations;
            logCorrections = settings.LogCorrections;
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

}
