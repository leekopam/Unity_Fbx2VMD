using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System;
using System.Collections.Generic;

namespace Member_Han.Modules.FBXImporter
{
    [DefaultExecutionOrder(20000)]
    public class PoseSpaceRetargeter : MonoBehaviour
    {
        [Header("--- CORE COMPONENTS ---")]
        public Animator ghostAnimator;  // (Container 내부의 모델)
        public Animator targetAnimator; // 내 캐릭터

        [Header("--- FINAL TUNING ---")]
        [Tooltip("캐릭터가 뒤를 보고 있다면 체크 (180도 회전)")]
        public bool fixReverseRotation = true;

        [Tooltip("Sub_Manual 직접 Animator 재생처럼 FBX HumanPose의 body/root 회전을 보존합니다.")]
        public bool preserveFbxRootRotation = false;

        [Tooltip("체크 시 공중 부양/박힘을 모두 해결 (Raycast 사용)")]
        public bool useSmartGrounding = true;

        [Tooltip("발바닥 높이 미세 조절 (양수: 띄움, 음수: 박음)")]
        [Range(-0.1f, 0.1f)]
        public float groundOffset = 0.0f;

        [Tooltip("FBX Avatar에서 비정상적으로 튀는 Humanoid muscle 값을 안전 범위로 제한합니다.")]
        public bool clampMusclesToHumanRange = false;

        [Header("--- ANATOMY GUARD ---")]
        [Tooltip("Target 팔이 늘어나거나 비정상적으로 비틀리는 Humanoid muscle 값을 제한합니다.")]
        public bool enableAnatomicalArmGuard = true;

        [Tooltip("Humanoid 팔 Stretch muscle 허용치입니다. Forearm Stretch는 팔꿈치 굽힘에 가까우므로 기본적으로 제한하지 않습니다.")]
        [Range(0f, 0.5f)]
        public float armStretchMuscleLimit = 0f;

        [Tooltip("Forearm Stretch muscle 제한 여부입니다. Unity Humanoid에서는 팔꿈치 굽힘에 가까우므로 기본값은 꺼야 합니다.")]
        public bool clampArmStretchMuscles = false;

        [Tooltip("상완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        public float upperArmTwistMuscleLimit = 0.75f;

        [Tooltip("전완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        public float lowerArmTwistMuscleLimit = 0.65f;

        [Header("--- THUMB ANATOMY GUARD ---")]
        [Tooltip("수동 기준 손가락 pose를 유지하되, YYB 손 구조에서 엄지가 과하게 꺾이는 범위만 제한합니다.")]
        public bool enableThumbAnatomicalGuard = true;

        [Tooltip("엄지 굽힘 muscle 최소값입니다.")]
        [Range(-2.5f, 0f)]
        public float thumbStretchMin = -2.1f;

        [Tooltip("엄지 굽힘 muscle 최대값입니다.")]
        [Range(0f, 2.5f)]
        public float thumbStretchMax = 1.0f;

        [Tooltip("엄지 굽힘 muscle에 더하는 offset입니다. YYB 엄지 rest pose가 수동 기준보다 과하게 펴져 보일 때만 사용합니다.")]
        [Range(-0.5f, 0.5f)]
        public float thumbStretchOffset = 0f;

        [Tooltip("Manual Animator finger reference를 사용할 때는 엄지 stretch offset을 추가하지 않고 수동 기준 엄지 muscle을 보존합니다.")]
        public bool preserveManualFingerReferenceThumbMuscles = true;

        [Tooltip("Manual Animator finger reference의 엄지 체인 localRotation도 Target에 적용해 모델별 엄지 축 차이를 줄입니다.")]
        public bool useManualAnimatorThumbLocalRotationReference = true;

        [Tooltip("Manual Animator finger reference의 엄지 세그먼트 방향을 Target 손 기준 방향에 맞춰 모델별 bind axis 차이를 줄입니다.")]
        public bool useManualAnimatorThumbSegmentDirectionReference = true;

        [Tooltip("엄지 세그먼트 방향 보정 강도입니다.")]
        [Range(0f, 1f)]
        public float manualAnimatorThumbSegmentDirectionWeight = 1f;

        [Tooltip("Manual Animator finger reference의 손바닥 기준 Hand->ThumbIntermediate 방향을 Target에 적용합니다.")]
        public bool useManualAnimatorThumbHandDirectionReference = true;

        [Tooltip("손바닥 기준 엄지 시작 방향 보정 강도입니다.")]
        [Range(0f, 1f)]
        public float manualAnimatorThumbHandDirectionWeight = 1f;

        [Tooltip("Manual Animator finger reference의 손바닥 전체 프레임을 Target 손에 적용합니다.")]
        public bool useManualAnimatorHandPalmFrameReference = true;

        [Tooltip("손바닥 프레임 보정 강도입니다.")]
        [Range(0f, 1f)]
        public float manualAnimatorHandPalmFrameWeight = 1f;

        [Tooltip("Manual Animator finger reference의 손 기준 엄지 시작 위치를 Target에 적용합니다.")]
        public bool useManualAnimatorThumbBasePositionReference = true;

        [Tooltip("엄지 시작 위치 보정 강도입니다.")]
        [Range(0f, 1f)]
        public float manualAnimatorThumbBasePositionWeight = 1f;

        [Tooltip("엄지 시작 위치가 원본 위치에서 벗어날 수 있는 최대 거리입니다.")]
        [Range(0f, 0.03f)]
        public float manualAnimatorThumbBasePositionMaxOffset = 0.03f;

        [Tooltip("엄지 벌림 muscle 최소값입니다.")]
        [Range(-1.5f, 0f)]
        public float thumbSpreadMin = -0.9f;

        [Tooltip("엄지 벌림 muscle 최대값입니다.")]
        [Range(0f, 1.5f)]
        public float thumbSpreadMax = 0.9f;

        [Tooltip("엄지 해부학적 제한이 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        public bool logThumbAnatomicalGuardCorrections = false;

        [Tooltip("엄지 muscle 제한 이후에도 YYB 엄지 본이 손 구조상 이상하게 꺾이면, 실제 엄지 본 localRotation을 기준 자세 근처로 제한합니다.")]
        public bool enableThumbLocalRotationGuard = true;

        [Tooltip("엄지 첫 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 90f)]
        public float thumbProximalMaxLocalAngle = 10f;

        [Tooltip("엄지 두 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)]
        public float thumbIntermediateMaxLocalAngle = 55f;

        [Tooltip("엄지 끝 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)]
        public float thumbDistalMaxLocalAngle = 55f;

        [Tooltip("엄지 본 localRotation 제한이 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        public bool logThumbLocalRotationGuardCorrections = false;

        [Header("--- ROOT MOTION SPIKE GUARD ---")]
        [Tooltip("Ghost root delta가 한 프레임에 과도하게 튀면 순간이동으로 보고 해당 프레임의 추가 root 이동을 무시합니다.")]
        public bool clampRootDeltaSpikes = true;

        [Tooltip("한 프레임에 허용할 최대 root 이동량입니다.")]
        [Range(0.01f, 1.0f)]
        public float maxRootDeltaPerFrame = 0.25f;

        [Tooltip("root delta spike를 무시했을 때 최초 1회 진단 로그를 출력합니다.")]
        public bool logRootDeltaSpikes = false;

        [Tooltip("Target Humanoid 본의 localPosition을 초기값으로 되돌려 팔/다리 길이 변형을 막습니다.")]
        public bool lockTargetHumanoidBonePositions = true;

        // --- 내부 변수 ---
        private HumanPoseHandler _ghostHandler;
        private HumanPoseHandler _targetHandler;
        private HumanPose _humanPose;
        private HumanPose _appliedTargetPose;

        private Vector3 _prevGhostPos;
        private static readonly Quaternion LegacyFacingCorrection = Quaternion.Euler(0f, 180f, 0f);
        private Quaternion _facingCorrection = LegacyFacingCorrection;
        private Quaternion _poseRootRotationCorrection = Quaternion.identity;
        private float _scaleRatio = 1.0f; // 체형 차이 비율
        private float _movementScaleMultiplier = 1.0f;
        private float _initialGhostHipHeight = 1.0f;
        private float _initialTargetHipHeight = 1.0f;
        private readonly Dictionary<Transform, Vector3> _targetInitialScales = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _targetInitialHumanoidLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _targetInitialThumbBaseHelperLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Quaternion> _targetInitialThumbLocalRotations = new Dictionary<Transform, Quaternion>();
        private bool _scaleWarningLogged;
        private bool _positionWarningLogged;
        private bool _poseWarningLogged;
        private bool _muscleClampWarningLogged;
        private bool _anatomyGuardWarningLogged;
        private bool _thumbGuardWarningLogged;
        private bool _thumbLocalRotationGuardWarningLogged;
        private bool _rootDeltaSpikeWarningLogged;
        private bool _appliedPoseClampWarningLogged;
        private bool _hasPoseRootRotationCorrection;
        private const float ThumbLocalRotationOvershootRatio = 0.35f;
        private const float ThumbLocalRotationHardOvershootDegrees = 8f;
        private static readonly HumanBodyBones[] ThumbRotationBones =
        {
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal
        };
#if UNITY_EDITOR
        private readonly Dictionary<int, AnimationCurve> _editorHumanoidMuscleCurves = new Dictionary<int, AnimationCurve>();
        private bool _useEditorHumanoidMuscleReference;
        private bool _editorHumanoidMuscleReferenceLogged;
        private readonly List<int> _editorFingerReferenceMuscleIndices = new List<int>();
        private GameObject _editorFingerReferenceInstance;
        private Animator _editorFingerReferenceAnimator;
        private HumanPoseHandler _editorFingerReferenceHandler;
        private HumanPose _editorFingerReferencePose;
        private int _editorFingerReferenceStateHash;
        private float _editorFingerReferenceClipLength;
        private bool _useEditorFingerPoseReference;
        private bool _editorFingerPoseReferenceLogged;
        private bool _editorThumbLocalRotationReferenceLogged;
        private bool _editorThumbSegmentDirectionReferenceLogged;
        private bool _editorHandPalmFrameReferenceLogged;
        private bool _editorThumbBasePositionReferenceLogged;
#endif

        // --- 초기화 ---
        private bool _isInitialized = false;
        private Animation _legacyAnim;

        public void Initialize(GameObject ghostRoot, GameObject targetRoot, Dictionary<string, string> mappingData, AnimationClip clip, FileManager settings)
        {
            ghostAnimator = ghostRoot.GetComponent<Animator>();
            targetAnimator = targetRoot.GetComponent<Animator>();
            CaptureTargetInitialTransforms(targetRoot);

            // Ghost Animator 끄기 (Legacy 구동용)
            if (ghostAnimator != null) ghostAnimator.enabled = false;

            // Legacy Animation 재생
            _legacyAnim = ghostRoot.GetComponent<Animation>();
            if (_legacyAnim == null) _legacyAnim = ghostRoot.AddComponent<Animation>();

            clip.legacy = true;
            clip.wrapMode = WrapMode.Once; // Loop 방지: 한 번만 재생
            _legacyAnim.AddClip(clip, clip.name);
            _legacyAnim.clip = clip;
            _legacyAnim.Play();

            // 포즈 핸들러 초기화
            if (!ghostAnimator.avatar || !targetAnimator.avatar) return;
            _ghostHandler = new HumanPoseHandler(ghostAnimator.avatar, ghostAnimator.transform);
            _targetHandler = new HumanPoseHandler(targetAnimator.avatar, targetAnimator.transform);
            _humanPose = new HumanPose();
            _appliedTargetPose = new HumanPose();

            // 초기 위치 저장
            _prevGhostPos = ghostAnimator.transform.position;
            CacheInitialHipHeights();
            _facingCorrection = settings != null && settings.useLegacyPoseSpaceFacingCorrection
                ? LegacyFacingCorrection
                : Quaternion.Inverse(ghostAnimator.transform.rotation) * targetAnimator.transform.rotation;
            _poseRootRotationCorrection = Quaternion.identity;
            _hasPoseRootRotationCorrection = false;
            if (settings != null)
            {
                groundOffset = settings.HeightOffset;
                _movementScaleMultiplier = Mathf.Max(0.0001f, settings.MovementScaleMultiplier);
                preserveFbxRootRotation = settings.preserveFbxRootRotation && !settings.useLegacyPoseSpaceFacingCorrection;
                clampMusclesToHumanRange = settings.clampRetargetMusclesToHumanRange;
                enableAnatomicalArmGuard = settings.enableAnatomicalArmGuard;
                armStretchMuscleLimit = settings.ArmStretchMuscleLimit;
                clampArmStretchMuscles = settings.clampRetargetArmStretchMuscles;
                upperArmTwistMuscleLimit = settings.UpperArmTwistMuscleLimit;
                lowerArmTwistMuscleLimit = settings.LowerArmTwistMuscleLimit;
                enableThumbAnatomicalGuard = settings.enableThumbAnatomicalGuard;
                thumbStretchMin = settings.ThumbStretchMin;
                thumbStretchMax = settings.ThumbStretchMax;
                thumbStretchOffset = settings.EffectiveThumbStretchOffset;
                preserveManualFingerReferenceThumbMuscles = settings.preserveManualFingerReferenceThumbMuscles;
                useManualAnimatorThumbLocalRotationReference = settings.useManualAnimatorThumbLocalRotationReference;
                useManualAnimatorThumbSegmentDirectionReference = settings.useManualAnimatorThumbSegmentDirectionReference;
                manualAnimatorThumbSegmentDirectionWeight = settings.manualAnimatorThumbSegmentDirectionWeight;
                useManualAnimatorThumbHandDirectionReference = settings.useManualAnimatorThumbHandDirectionReference;
                manualAnimatorThumbHandDirectionWeight = settings.manualAnimatorThumbHandDirectionWeight;
                useManualAnimatorHandPalmFrameReference = settings.useManualAnimatorHandPalmFrameReference;
                manualAnimatorHandPalmFrameWeight = settings.manualAnimatorHandPalmFrameWeight;
                useManualAnimatorThumbBasePositionReference = settings.useManualAnimatorThumbBasePositionReference;
                manualAnimatorThumbBasePositionWeight = settings.manualAnimatorThumbBasePositionWeight;
                manualAnimatorThumbBasePositionMaxOffset = settings.manualAnimatorThumbBasePositionMaxOffset;
                thumbSpreadMin = settings.ThumbSpreadMin;
                thumbSpreadMax = settings.ThumbSpreadMax;
                logThumbAnatomicalGuardCorrections = settings.logThumbAnatomicalGuardCorrections;
                enableThumbLocalRotationGuard = settings.EffectiveThumbLocalRotationGuard;
                thumbProximalMaxLocalAngle = settings.EffectiveThumbProximalMaxLocalAngle;
                thumbIntermediateMaxLocalAngle = settings.ThumbIntermediateMaxLocalAngle;
                thumbDistalMaxLocalAngle = settings.ThumbDistalMaxLocalAngle;
                logThumbLocalRotationGuardCorrections = settings.logThumbLocalRotationGuardCorrections;
                clampRootDeltaSpikes = settings.clampRetargetRootDeltaSpikes;
                maxRootDeltaPerFrame = settings.MaxRetargetRootDeltaPerFrame;
                logRootDeltaSpikes = settings.logRetargetRootDeltaSpikes;
                lockTargetHumanoidBonePositions = settings.lockTargetHumanoidBonePositions;
            }

            _isInitialized = true;
            Debug.Log("[Master Stage] System Initialized. Waiting for First Update...");
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            DisposeEditorHumanoidFingerPoseReference();
#endif
        }

#if UNITY_EDITOR
        public void ConfigureEditorHumanoidMuscleReference(AnimationClip referenceClip)
        {
            _editorHumanoidMuscleCurves.Clear();
            _useEditorHumanoidMuscleReference = false;
            _editorHumanoidMuscleReferenceLogged = false;

            if (referenceClip == null || !referenceClip.humanMotion)
            {
                return;
            }

            UnityEditor.EditorCurveBinding[] bindings = UnityEditor.AnimationUtility.GetCurveBindings(referenceClip);
            for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
            {
                UnityEditor.EditorCurveBinding binding = bindings[bindingIndex];
                if (binding.type != typeof(Animator) || !string.IsNullOrEmpty(binding.path))
                {
                    continue;
                }

                int muscleIndex = FindHumanMuscleIndex(binding.propertyName);
                if (muscleIndex < 0)
                {
                    continue;
                }

                AnimationCurve curve = UnityEditor.AnimationUtility.GetEditorCurve(referenceClip, binding);
                if (curve != null)
                {
                    _editorHumanoidMuscleCurves[muscleIndex] = curve;
                }
            }

            _useEditorHumanoidMuscleReference = _editorHumanoidMuscleCurves.Count > 0;
            Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid muscle reference curves: {_editorHumanoidMuscleCurves.Count} from {referenceClip.name}");
        }

        public void ConfigureEditorHumanoidFingerPoseReference(GameObject referencePrefab, RuntimeAnimatorController referenceController, AnimationClip referenceClip)
        {
            DisposeEditorHumanoidFingerPoseReference();
            _useEditorFingerPoseReference = false;
            _editorFingerPoseReferenceLogged = false;
            _editorThumbLocalRotationReferenceLogged = false;
            _editorThumbSegmentDirectionReferenceLogged = false;
            _editorHandPalmFrameReferenceLogged = false;
            _editorThumbBasePositionReferenceLogged = false;
            _editorFingerReferenceMuscleIndices.Clear();

            if (referencePrefab == null || referenceController == null || referenceClip == null || !referenceClip.humanMotion)
            {
                return;
            }

            string stateName = ResolveFirstAnimatorStateName(referenceController);
            if (string.IsNullOrEmpty(stateName))
            {
                stateName = referenceClip.name;
            }

            _editorFingerReferenceStateHash = Animator.StringToHash(stateName);
            _editorFingerReferenceClipLength = Mathf.Max(0.0001f, referenceClip.length);
            _editorFingerReferenceInstance = Instantiate(referencePrefab);
            _editorFingerReferenceInstance.name = $"EditorFingerReference_{referencePrefab.name}";
            _editorFingerReferenceInstance.hideFlags = HideFlags.HideAndDontSave;
            _editorFingerReferenceInstance.transform.SetParent(transform, false);
            _editorFingerReferenceInstance.SetActive(true);

            foreach (Renderer renderer in _editorFingerReferenceInstance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            _editorFingerReferenceAnimator = _editorFingerReferenceInstance.GetComponentInChildren<Animator>(true);
            if (_editorFingerReferenceAnimator == null ||
                _editorFingerReferenceAnimator.avatar == null ||
                !_editorFingerReferenceAnimator.avatar.isValid ||
                !_editorFingerReferenceAnimator.avatar.isHuman)
            {
                Debug.LogWarning("[PoseSpaceRetargeter] 수동 기준 손가락 Reference에 유효한 Humanoid Animator가 없습니다.");
                DisposeEditorHumanoidFingerPoseReference();
                return;
            }

            AnimatorOverrideController overrideController = new AnimatorOverrideController(referenceController);
            List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            if (overrides.Count > 0 && overrides[0].Key != null)
            {
                overrideController[overrides[0].Key] = referenceClip;
            }

            _editorFingerReferenceAnimator.runtimeAnimatorController = overrideController;
            _editorFingerReferenceAnimator.applyRootMotion = false;
            _editorFingerReferenceAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _editorFingerReferenceAnimator.enabled = true;
            _editorFingerReferenceAnimator.speed = 0f;
            _editorFingerReferenceAnimator.Rebind();
            _editorFingerReferenceAnimator.Update(0f);

            if (!_editorFingerReferenceAnimator.HasState(0, _editorFingerReferenceStateHash))
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] 수동 기준 손가락 Reference state를 찾지 못했습니다: {stateName}");
                DisposeEditorHumanoidFingerPoseReference();
                return;
            }

            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                if (IsFingerMuscle(HumanTrait.MuscleName[i]))
                {
                    _editorFingerReferenceMuscleIndices.Add(i);
                }
            }

            _editorFingerReferenceHandler = new HumanPoseHandler(_editorFingerReferenceAnimator.avatar, _editorFingerReferenceAnimator.transform);
            _editorFingerReferencePose = new HumanPose();
            _useEditorFingerPoseReference = _editorFingerReferenceMuscleIndices.Count > 0;
            Debug.Log($"[PoseSpaceRetargeter] Manual Animator finger reference ready: prefab={referencePrefab.name}, controller={referenceController.name}, state={stateName}, clip={referenceClip.name}, muscles={_editorFingerReferenceMuscleIndices.Count}");
        }
#endif

        void LateUpdate()
        {
            if (!_isInitialized || ghostAnimator == null || targetAnimator == null || _ghostHandler == null || _targetHandler == null) return;

            // 스케일 비율 계산 (매 프레임 체크하여 안정성 확보)
            // Container가 작동 중이라면 ghostHip.position.y는 ~0.8m 수준이어야 함.
            Transform ghostHip = ghostAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHip = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);

            _scaleRatio = CalculateSafeScaleRatio(ghostHip, targetHip);

            // 포즈(근육) 동기화
            _ghostHandler.GetHumanPose(ref _humanPose);
            if (!IsFinite(_humanPose))
            {
                LogPoseWarning("Ghost HumanPose contains non-finite values. Skipping this retarget frame.");
                RestoreTargetLocalScales();
                return;
            }

            ApplyEditorHumanoidMuscleReference(ref _humanPose);
            ApplyEditorHumanoidFingerPoseReference(ref _humanPose);
            ApplyThumbAnatomicalGuard(ref _humanPose, ShouldApplyThumbStretchOffset());
            ClampPoseMuscles(ref _humanPose);
            ApplyAnatomicalArmGuard(ref _humanPose);
            Quaternion poseRootRotation = _humanPose.bodyRotation;
            if (preserveFbxRootRotation && !_hasPoseRootRotationCorrection && IsFinite(poseRootRotation) && _legacyAnim != null && _legacyAnim.isPlaying)
            {
                _poseRootRotationCorrection = Quaternion.Inverse(poseRootRotation);
                _hasPoseRootRotationCorrection = true;
            }

            // 골반 높이(Y)는 비율에 맞춰 재조정 (나머지는 Root Motion이 담당)
            Vector3 bodyPos = _humanPose.bodyPosition;
            bodyPos.y *= _scaleRatio;
            if (!IsFinite(bodyPos))
            {
                LogPoseWarning("Retarget body position became non-finite. Skipping this retarget frame.");
                RestoreTargetLocalScales();
                return;
            }
            _humanPose.bodyPosition = bodyPos;

            Vector3 targetPositionBeforePose = targetAnimator.transform.position;
            _targetHandler.SetHumanPose(ref _humanPose);
            ClampAppliedTargetPose();
            RestoreTargetHumanoidLocalPositions();
#if UNITY_EDITOR
            ApplyEditorHumanoidThumbBasePositionReference();
#endif
            ClampTargetThumbLocalRotations();
#if UNITY_EDITOR
            ApplyEditorHumanoidThumbLocalRotationReference();
            ApplyEditorHumanoidThumbSegmentDirectionReference();
            ApplyEditorHumanoidThumbHandDirectionReference();
            ApplyEditorHumanoidHandPalmFrameReference();
#endif
            ClampTargetRootPositionSpike(targetPositionBeforePose, "SetHumanPose");

            // 월드 회전 동기화 (180도 문제 해결)
            if (preserveFbxRootRotation && _hasPoseRootRotationCorrection && IsFinite(poseRootRotation))
            {
                Quaternion correctedRootRotation = _poseRootRotationCorrection * poseRootRotation;
                if (IsFinite(correctedRootRotation))
                {
                    targetAnimator.transform.rotation = correctedRootRotation;
                }
            }
            else if (!preserveFbxRootRotation && fixReverseRotation)
            {
                // Ghost 회전 * 180도 보정
                targetAnimator.transform.rotation = ghostAnimator.transform.rotation * _facingCorrection;
            }
            else if (!preserveFbxRootRotation)
            {
                targetAnimator.transform.rotation = ghostAnimator.transform.rotation;
            }

            // 루트 모션 동기화 (호 그리기 방지)
            // Ghost 이동량 계산
            Vector3 ghostDelta = ghostAnimator.transform.position - _prevGhostPos;

            // 내 캐릭터 크기에 맞춰 이동량 스케일링
            Vector3 targetDelta = ghostDelta * _scaleRatio * _movementScaleMultiplier;
            if (!IsFinite(targetDelta))
            {
                LogPoseWarning("Retarget root delta became non-finite. Skipping root motion for this frame.");
                targetDelta = Vector3.zero;
            }
            else if (clampRootDeltaSpikes && targetDelta.magnitude > maxRootDeltaPerFrame)
            {
                if (logRootDeltaSpikes && !_rootDeltaSpikeWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] Root delta spike {targetDelta.magnitude:F3}m skipped. ghostDelta={ghostDelta.magnitude:F3}m, limit={maxRootDeltaPerFrame:F3}m");
                    _rootDeltaSpikeWarningLogged = true;
                }

                targetDelta = Vector3.zero;
            }

            // 이동 적용
            targetAnimator.transform.position += targetDelta;

            // 위치 갱신
            _prevGhostPos = ghostAnimator.transform.position;

            // 스마트 접지 (Raycast Grounding) - 공중 부양 해결
            if (useSmartGrounding)
            {
                ApplyRaycastGrounding();
            }

            RestoreTargetLocalScales();
        }

#if UNITY_EDITOR
        private void ApplyEditorHumanoidMuscleReference(ref HumanPose pose)
        {
            if (!_useEditorHumanoidMuscleReference || pose.muscles == null || _editorHumanoidMuscleCurves.Count == 0)
            {
                return;
            }

            float time = GetLegacyAnimationTime();
            foreach (KeyValuePair<int, AnimationCurve> pair in _editorHumanoidMuscleCurves)
            {
                if (pair.Key < 0 || pair.Key >= pose.muscles.Length || pair.Value == null)
                {
                    continue;
                }

                pose.muscles[pair.Key] = pair.Value.Evaluate(time);
            }

            if (!_editorHumanoidMuscleReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid muscle reference applied at t={time:F3}s.");
                _editorHumanoidMuscleReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidFingerPoseReference(ref HumanPose pose)
        {
            if (!_useEditorFingerPoseReference ||
                pose.muscles == null ||
                _editorFingerReferenceAnimator == null ||
                _editorFingerReferenceHandler == null ||
                _editorFingerReferenceMuscleIndices.Count == 0)
            {
                return;
            }

            float time = GetLegacyAnimationTime();
            float normalizedTime = _editorFingerReferenceClipLength > 0f
                ? Mathf.Clamp01(time / _editorFingerReferenceClipLength)
                : 0f;

            _editorFingerReferenceAnimator.Play(_editorFingerReferenceStateHash, 0, normalizedTime);
            _editorFingerReferenceAnimator.Update(0f);
            _editorFingerReferenceHandler.GetHumanPose(ref _editorFingerReferencePose);

            if (_editorFingerReferencePose.muscles == null)
            {
                return;
            }

            foreach (int muscleIndex in _editorFingerReferenceMuscleIndices)
            {
                if (muscleIndex < 0 || muscleIndex >= pose.muscles.Length || muscleIndex >= _editorFingerReferencePose.muscles.Length)
                {
                    continue;
                }

                pose.muscles[muscleIndex] = _editorFingerReferencePose.muscles[muscleIndex];
            }

            if (!_editorFingerPoseReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator finger reference applied at t={time:F3}s.");
                _editorFingerPoseReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidThumbLocalRotationReference()
        {
            if (!useManualAnimatorThumbLocalRotationReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            int changed = 0;
            foreach (HumanBodyBones thumbBone in ThumbRotationBones)
            {
                Transform source = _editorFingerReferenceAnimator.GetBoneTransform(thumbBone);
                Transform target = targetAnimator.GetBoneTransform(thumbBone);
                if (source == null || target == null)
                {
                    continue;
                }

                Quaternion sourceRotation = source.localRotation;
                if (!IsFinite(sourceRotation) || Quaternion.Angle(target.localRotation, sourceRotation) <= 0.001f)
                {
                    continue;
                }

                target.localRotation = sourceRotation;
                changed++;
            }

            if (changed > 0 && !_editorThumbLocalRotationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator thumb localRotation reference applied. bones={changed}");
                _editorThumbLocalRotationReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidThumbBasePositionReference()
        {
            if (!useManualAnimatorThumbBasePositionReference ||
                manualAnimatorThumbBasePositionWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            int changed = 0;
            changed += ApplyEditorHumanoidThumbBasePositionReferenceSide(
                HumanBodyBones.LeftHand,
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftIndexProximal,
                "joint_LeftThumb0");
            changed += ApplyEditorHumanoidThumbBasePositionReferenceSide(
                HumanBodyBones.RightHand,
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightIndexProximal,
                "joint_RightThumb0");

            if (changed > 0 && !_editorThumbBasePositionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator thumb base position reference applied. targets={changed}, weight={manualAnimatorThumbBasePositionWeight:F2}, maxOffset={manualAnimatorThumbBasePositionMaxOffset:F4}");
                _editorThumbBasePositionReferenceLogged = true;
            }
        }

        private int ApplyEditorHumanoidThumbBasePositionReferenceSide(
            HumanBodyBones handBone,
            HumanBodyBones thumbBone,
            HumanBodyBones indexBone,
            string helperNameSuffix)
        {
            Transform referenceThumb = _editorFingerReferenceAnimator.GetBoneTransform(thumbBone);
            Transform targetThumb = targetAnimator.GetBoneTransform(thumbBone);

            if (referenceThumb == null || targetThumb == null)
            {
                return 0;
            }

            bool leftHand = handBone == HumanBodyBones.LeftHand;
            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referencePalmLocalThumb = referenceFrame.InverseTransformPoint(referenceThumb.position);
            float palmScale = Mathf.Clamp(targetFrame.Scale / referenceFrame.Scale, 0.25f, 4f);
            Vector3 desiredWorldPosition = targetFrame.TransformPoint(referencePalmLocalThumb * palmScale);

            int changed = 0;
            changed += ApplyThumbBasePositionToTransform(
                targetThumb,
                desiredWorldPosition,
                _targetInitialHumanoidLocalPositions);

            Transform helperTransform = FindTargetTransformByNameSuffix(helperNameSuffix);
            if (helperTransform != null && helperTransform != targetThumb)
            {
                changed += ApplyThumbBasePositionToTransform(
                    helperTransform,
                    desiredWorldPosition,
                    _targetInitialThumbBaseHelperLocalPositions);
            }

            return changed;
        }

        private int ApplyThumbBasePositionToTransform(
            Transform targetTransform,
            Vector3 desiredWorldPosition,
            IDictionary<Transform, Vector3> initialLocalPositions)
        {
            if (targetTransform == null || targetTransform.parent == null)
            {
                return 0;
            }

            if (!initialLocalPositions.TryGetValue(targetTransform, out Vector3 initialLocalPosition))
            {
                initialLocalPosition = targetTransform.localPosition;
                initialLocalPositions[targetTransform] = initialLocalPosition;
            }

            Vector3 desiredLocalPosition = targetTransform.parent.InverseTransformPoint(desiredWorldPosition);
            float maxOffset = Mathf.Max(0f, manualAnimatorThumbBasePositionMaxOffset);
            if (maxOffset > 0f)
            {
                desiredLocalPosition = initialLocalPosition + Vector3.ClampMagnitude(desiredLocalPosition - initialLocalPosition, maxOffset);
            }

            Vector3 targetLocalPosition = Vector3.Lerp(
                initialLocalPosition,
                desiredLocalPosition,
                Mathf.Clamp01(manualAnimatorThumbBasePositionWeight));

            if ((targetTransform.localPosition - targetLocalPosition).sqrMagnitude <= 0.00000001f)
            {
                return 0;
            }

            targetTransform.localPosition = targetLocalPosition;
            return 1;
        }

        private void ApplyEditorHumanoidThumbSegmentDirectionReference()
        {
            if (!useManualAnimatorThumbSegmentDirectionReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorThumbSegmentDirectionWeight);
            if (weight <= 0.0001f)
            {
                return;
            }

            int changed = 0;
            changed += AlignEditorHumanoidThumbSegmentDirection(true, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, weight);
            changed += AlignEditorHumanoidThumbSegmentDirection(true, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal, weight);
            changed += AlignEditorHumanoidThumbSegmentDirection(false, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, weight);
            changed += AlignEditorHumanoidThumbSegmentDirection(false, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal, weight);

            if (changed > 0 && !_editorThumbSegmentDirectionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator thumb segment direction reference applied. segments={changed}, weight={weight:F2}");
                _editorThumbSegmentDirectionReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidThumbHandDirectionReference()
        {
            if (!useManualAnimatorThumbHandDirectionReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorThumbHandDirectionWeight);
            if (weight <= 0.0001f)
            {
                return;
            }

            int changed = 0;
            changed += AlignEditorHumanoidThumbHandDirection(true, weight);
            changed += AlignEditorHumanoidThumbHandDirection(false, weight);
            if (changed <= 0)
            {
                return;
            }
        }

        private int AlignEditorHumanoidThumbHandDirection(bool leftHand, float weight)
        {
            Transform targetHand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform referenceHand = _editorFingerReferenceAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform targetProximal = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            Transform targetIntermediate = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
            Transform referenceIntermediate = _editorFingerReferenceAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);

            if (targetHand == null || referenceHand == null ||
                targetProximal == null || targetIntermediate == null ||
                referenceIntermediate == null)
            {
                return 0;
            }

            Vector3 targetDirection = targetIntermediate.position - targetHand.position;
            Vector3 referenceDirection = referenceIntermediate.position - referenceHand.position;
            if (!IsFinite(targetDirection) || !IsFinite(referenceDirection) ||
                targetDirection.sqrMagnitude <= 0.00000001f ||
                referenceDirection.sqrMagnitude <= 0.00000001f)
            {
                return 0;
            }

            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referenceHandDirection = referenceFrame.InverseTransformDirection(referenceDirection.normalized).normalized;
            Vector3 desiredWorldDirection = targetFrame.TransformDirection(referenceHandDirection).normalized;
            Vector3 currentWorldDirection = targetDirection.normalized;
            if (!IsFinite(referenceHandDirection) || !IsFinite(desiredWorldDirection) || !IsFinite(currentWorldDirection))
            {
                return 0;
            }

            Quaternion correction = Quaternion.FromToRotation(currentWorldDirection, desiredWorldDirection);
            if (!IsFinite(correction))
            {
                return 0;
            }

            if (weight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, weight);
            }

            Quaternion nextWorldRotation = correction * targetProximal.rotation;
            if (!IsFinite(nextWorldRotation) || Quaternion.Angle(targetProximal.rotation, nextWorldRotation) <= 0.001f)
            {
                return 0;
            }

            targetProximal.rotation = nextWorldRotation;
            return 1;
        }

        private void ApplyEditorHumanoidHandPalmFrameReference()
        {
            if (!useManualAnimatorHandPalmFrameReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorHandPalmFrameWeight);
            if (weight <= 0.0001f)
            {
                return;
            }

            int changed = 0;
            changed += AlignEditorHumanoidHandPalmFrame(true, weight);
            changed += AlignEditorHumanoidHandPalmFrame(false, weight);

            if (changed > 0 && !_editorHandPalmFrameReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator hand palm-frame reference applied. hands={changed}, weight={weight:F2}");
                _editorHandPalmFrameReferenceLogged = true;
            }
        }

        private int AlignEditorHumanoidHandPalmFrame(bool leftHand, float weight)
        {
            Transform targetHand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (targetHand == null)
            {
                return 0;
            }

            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referenceForwardLocal = _editorFingerReferenceAnimator.transform.InverseTransformDirection(referenceFrame.Forward).normalized;
            Vector3 referenceNormalLocal = _editorFingerReferenceAnimator.transform.InverseTransformDirection(referenceFrame.Normal).normalized;
            Vector3 desiredForward = targetAnimator.transform.TransformDirection(referenceForwardLocal).normalized;
            Vector3 desiredNormal = targetAnimator.transform.TransformDirection(referenceNormalLocal).normalized;
            if (!IsFinite(referenceForwardLocal) || !IsFinite(referenceNormalLocal) ||
                !IsFinite(desiredForward) || !IsFinite(desiredNormal))
            {
                return 0;
            }

            Quaternion currentFrameRotation = Quaternion.LookRotation(targetFrame.Forward, targetFrame.Normal);
            Quaternion desiredFrameRotation = Quaternion.LookRotation(desiredForward, desiredNormal);
            Quaternion correction = desiredFrameRotation * Quaternion.Inverse(currentFrameRotation);
            if (!IsFinite(currentFrameRotation) || !IsFinite(desiredFrameRotation) || !IsFinite(correction))
            {
                return 0;
            }

            if (weight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, weight);
            }

            Quaternion nextWorldRotation = correction * targetHand.rotation;
            if (!IsFinite(nextWorldRotation) || Quaternion.Angle(targetHand.rotation, nextWorldRotation) <= 0.001f)
            {
                return 0;
            }

            targetHand.rotation = nextWorldRotation;
            return 1;
        }

        private int AlignEditorHumanoidThumbSegmentDirection(bool leftHand, HumanBodyBones parentBone, HumanBodyBones childBone, float weight)
        {
            Transform targetHand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform referenceHand = _editorFingerReferenceAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform targetParent = targetAnimator.GetBoneTransform(parentBone);
            Transform targetChild = targetAnimator.GetBoneTransform(childBone);
            Transform referenceParent = _editorFingerReferenceAnimator.GetBoneTransform(parentBone);
            Transform referenceChild = _editorFingerReferenceAnimator.GetBoneTransform(childBone);

            if (targetHand == null || referenceHand == null ||
                targetParent == null || targetChild == null ||
                referenceParent == null || referenceChild == null)
            {
                return 0;
            }

            Vector3 targetSegment = targetChild.position - targetParent.position;
            Vector3 referenceSegment = referenceChild.position - referenceParent.position;
            if (!IsFinite(targetSegment) || !IsFinite(referenceSegment) ||
                targetSegment.sqrMagnitude <= 0.00000001f ||
                referenceSegment.sqrMagnitude <= 0.00000001f)
            {
                return 0;
            }

            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referenceHandDirection = referenceFrame.InverseTransformDirection(referenceSegment.normalized).normalized;
            Vector3 desiredWorldDirection = targetFrame.TransformDirection(referenceHandDirection).normalized;
            Vector3 currentWorldDirection = targetSegment.normalized;
            if (!IsFinite(referenceHandDirection) || !IsFinite(desiredWorldDirection) || !IsFinite(currentWorldDirection))
            {
                return 0;
            }

            Quaternion correction = Quaternion.FromToRotation(currentWorldDirection, desiredWorldDirection);
            if (!IsFinite(correction))
            {
                return 0;
            }

            if (weight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, weight);
            }

            Quaternion nextWorldRotation = correction * targetParent.rotation;
            if (!IsFinite(nextWorldRotation) || Quaternion.Angle(targetParent.rotation, nextWorldRotation) <= 0.001f)
            {
                return 0;
            }

            targetParent.rotation = nextWorldRotation;
            return 1;
        }

        private struct ThumbPalmFrame
        {
            public Vector3 Origin;
            public Vector3 Side;
            public Vector3 Normal;
            public Vector3 Forward;
            public float Scale;

            public Vector3 InverseTransformPoint(Vector3 worldPoint)
            {
                Vector3 delta = worldPoint - Origin;
                return new Vector3(
                    Vector3.Dot(delta, Side),
                    Vector3.Dot(delta, Normal),
                    Vector3.Dot(delta, Forward));
            }

            public Vector3 TransformPoint(Vector3 localPoint)
            {
                return Origin +
                    Side * localPoint.x +
                    Normal * localPoint.y +
                    Forward * localPoint.z;
            }

            public Vector3 InverseTransformDirection(Vector3 worldDirection)
            {
                return new Vector3(
                    Vector3.Dot(worldDirection, Side),
                    Vector3.Dot(worldDirection, Normal),
                    Vector3.Dot(worldDirection, Forward));
            }

            public Vector3 TransformDirection(Vector3 localDirection)
            {
                return Side * localDirection.x +
                    Normal * localDirection.y +
                    Forward * localDirection.z;
            }
        }

        private static bool TryBuildThumbPalmFrame(Animator animator, bool leftHand, out ThumbPalmFrame frame)
        {
            frame = default;
            if (animator == null)
            {
                return false;
            }

            Transform hand = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform thumb = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            Transform index = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform middle = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Transform little = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);

            if (hand == null || thumb == null || index == null)
            {
                return false;
            }

            Vector3 origin = hand.position;
            Vector3 fingerCenter = Vector3.zero;
            int fingerCount = 0;
            AddPalmPoint(index, origin, ref fingerCenter, ref fingerCount);
            AddPalmPoint(middle, origin, ref fingerCenter, ref fingerCount);
            AddPalmPoint(little, origin, ref fingerCenter, ref fingerCount);
            if (fingerCount <= 0)
            {
                return false;
            }

            Vector3 forward = fingerCenter / fingerCount - origin;
            if (!TryNormalize(forward, out forward))
            {
                forward = index.position - origin;
                if (!TryNormalize(forward, out forward))
                {
                    return false;
                }
            }

            Vector3 side = Vector3.zero;
            if (little != null)
            {
                side = index.position - little.position;
            }

            side = Vector3.ProjectOnPlane(side, forward);
            if (!TryNormalize(side, out side))
            {
                side = Vector3.ProjectOnPlane(thumb.position - origin, forward);
                if (!TryNormalize(side, out side))
                {
                    side = Vector3.ProjectOnPlane(hand.right, forward);
                    if (!TryNormalize(side, out side))
                    {
                        return false;
                    }
                }
            }

            Vector3 thumbSide = Vector3.ProjectOnPlane(thumb.position - origin, forward);
            if (TryNormalize(thumbSide, out thumbSide) && Vector3.Dot(side, thumbSide) < 0f)
            {
                side = -side;
            }

            Vector3 normal = Vector3.Cross(side, forward);
            if (!TryNormalize(normal, out normal))
            {
                return false;
            }

            side = Vector3.Cross(forward, normal);
            if (!TryNormalize(side, out side))
            {
                return false;
            }

            frame = new ThumbPalmFrame
            {
                Origin = origin,
                Side = side,
                Normal = normal,
                Forward = forward,
                Scale = CalculatePalmScale(origin, index, middle, little)
            };

            return frame.Scale > 0.0001f &&
                IsFinite(frame.Origin) &&
                IsFinite(frame.Side) &&
                IsFinite(frame.Normal) &&
                IsFinite(frame.Forward);
        }

        private static void AddPalmPoint(Transform point, Vector3 origin, ref Vector3 sum, ref int count)
        {
            if (point == null)
            {
                return;
            }

            Vector3 delta = point.position - origin;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            sum += point.position;
            count++;
        }

        private static float CalculatePalmScale(Vector3 origin, params Transform[] points)
        {
            float sum = 0f;
            int count = 0;
            foreach (Transform point in points)
            {
                if (point == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(origin, point.position);
                if (!IsFinite(distance) || distance <= 0.0001f)
                {
                    continue;
                }

                sum += distance;
                count++;
            }

            return count > 0 ? sum / count : 0f;
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

        private void DisposeEditorHumanoidFingerPoseReference()
        {
            if (_editorFingerReferenceHandler != null)
            {
                _editorFingerReferenceHandler.Dispose();
                _editorFingerReferenceHandler = null;
            }

            if (_editorFingerReferenceInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_editorFingerReferenceInstance);
                }
                else
                {
                    DestroyImmediate(_editorFingerReferenceInstance);
                }
            }

            _editorFingerReferenceInstance = null;
            _editorFingerReferenceAnimator = null;
            _editorFingerReferenceMuscleIndices.Clear();
            _useEditorFingerPoseReference = false;
        }

        private static string ResolveFirstAnimatorStateName(RuntimeAnimatorController controller)
        {
            if (controller == null)
            {
                return "";
            }

            AnimatorOverrideController overrideController = controller as AnimatorOverrideController;
            if (overrideController != null && overrideController.runtimeAnimatorController != null)
            {
                controller = overrideController.runtimeAnimatorController;
            }

            UnityEditor.Animations.AnimatorController animatorController = controller as UnityEditor.Animations.AnimatorController;
            if (animatorController == null || animatorController.layers == null || animatorController.layers.Length == 0)
            {
                return "";
            }

            UnityEditor.Animations.ChildAnimatorState[] states = animatorController.layers[0].stateMachine.states;
            return states != null && states.Length > 0 ? states[0].state.name : "";
        }

        private static bool IsFingerMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            return normalized.Contains("thumb") ||
                   normalized.Contains("index") ||
                   normalized.Contains("middle") ||
                   normalized.Contains("ring") ||
                   normalized.Contains("little");
        }

        private float GetLegacyAnimationTime()
        {
            if (_legacyAnim == null || _legacyAnim.clip == null)
            {
                return 0f;
            }

            AnimationState state = _legacyAnim[_legacyAnim.clip.name];
            if (state == null)
            {
                return 0f;
            }

            return Mathf.Clamp(state.time, 0f, Mathf.Max(0f, state.length));
        }

        private static int FindHumanMuscleIndex(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return -1;
            }

            string normalizedInput = NormalizeEditorMuscleName(muscleName);
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string humanMuscleName = HumanTrait.MuscleName[i];
                if (string.Equals(humanMuscleName, muscleName, StringComparison.Ordinal) ||
                    string.Equals(NormalizeEditorMuscleName(humanMuscleName), normalizedInput, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeEditorMuscleName(string muscleName)
        {
            string normalized = muscleName.Replace(" ", "").Replace(".", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
            normalized = normalized.Replace("lefthandthumb", "leftthumb")
                .Replace("lefthandindex", "leftindex")
                .Replace("lefthandmiddle", "leftmiddle")
                .Replace("lefthandring", "leftring")
                .Replace("lefthandlittle", "leftlittle")
                .Replace("righthandthumb", "rightthumb")
                .Replace("righthandindex", "rightindex")
                .Replace("righthandmiddle", "rightmiddle")
                .Replace("righthandring", "rightring")
                .Replace("righthandlittle", "rightlittle");
            return normalized;
        }
#else
        private void ApplyEditorHumanoidMuscleReference(ref HumanPose pose)
        {
        }

        private void ApplyEditorHumanoidFingerPoseReference(ref HumanPose pose)
        {
        }
#endif

        private float CalculateSafeScaleRatio(Transform ghostHip, Transform targetHip)
        {
            float ratio = _scaleRatio;

            if (ghostAnimator != null && targetAnimator != null && ghostAnimator.humanScale > 0.0001f && targetAnimator.humanScale > 0.0001f)
            {
                ratio = targetAnimator.humanScale / ghostAnimator.humanScale;
            }
            else if (_initialGhostHipHeight > 0.01f)
            {
                ratio = _initialTargetHipHeight / _initialGhostHipHeight;
            }
            else if (ghostHip != null && targetHip != null && ghostHip.position.y > 0.01f)
            {
                ratio = targetHip.position.y / ghostHip.position.y;
            }

            if (!IsFinite(ratio) || ratio <= 0f)
            {
                LogPoseWarning("Invalid retarget scale ratio. Falling back to 1.0.");
                return 1f;
            }

            return Mathf.Clamp(ratio, 0.01f, 10f);
        }

        private void CacheInitialHipHeights()
        {
            Transform ghostHip = ghostAnimator != null ? ghostAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;
            Transform targetHip = targetAnimator != null ? targetAnimator.GetBoneTransform(HumanBodyBones.Hips) : null;

            if (ghostHip != null && IsFinite(ghostHip.position.y) && Mathf.Abs(ghostHip.position.y) > 0.01f)
            {
                _initialGhostHipHeight = Mathf.Abs(ghostHip.position.y);
            }

            if (targetHip != null && IsFinite(targetHip.position.y) && Mathf.Abs(targetHip.position.y) > 0.01f)
            {
                _initialTargetHipHeight = Mathf.Abs(targetHip.position.y);
            }
        }

        private void LogPoseWarning(string message)
        {
            if (_poseWarningLogged)
            {
                return;
            }

            Debug.LogWarning($"[PoseSpaceRetargeter] {message}");
            _poseWarningLogged = true;
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

        private void ClampPoseMuscles(ref HumanPose pose)
        {
            if (!clampMusclesToHumanRange || pose.muscles == null)
            {
                return;
            }

            int clampedCount = HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);

            if (clampedCount > 0 && !_muscleClampWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] Humanoid muscle 값 {clampedCount}개가 안전 범위를 벗어나 [-1, 1]로 제한되었습니다.");
                _muscleClampWarningLogged = true;
            }
        }

        private void ApplyAnatomicalArmGuard(ref HumanPose pose)
        {
            if (!enableAnatomicalArmGuard || pose.muscles == null)
            {
                return;
            }

            int changed = HumanoidArmDeformationGuard.ClampAnatomicalArmMuscles(
                ref pose,
                armStretchMuscleLimit,
                upperArmTwistMuscleLimit,
                lowerArmTwistMuscleLimit,
                clampArmStretchMuscles);

            if (changed > 0 && !_anatomyGuardWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] 팔 변형 방지를 위해 Humanoid arm muscle {changed}개를 제한했습니다.");
                _anatomyGuardWarningLogged = true;
            }
        }

        private void ClampAppliedTargetPose()
        {
            if ((!clampMusclesToHumanRange && !enableAnatomicalArmGuard && !enableThumbAnatomicalGuard) || _targetHandler == null)
            {
                return;
            }

            _targetHandler.GetHumanPose(ref _appliedTargetPose);
            if (!IsFinite(_appliedTargetPose))
            {
                LogPoseWarning("Applied target HumanPose contains non-finite values. Skipping applied pose clamp.");
                return;
            }

            int changed = 0;
            if (clampMusclesToHumanRange)
            {
                changed += HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref _appliedTargetPose);
            }

            if (enableAnatomicalArmGuard)
            {
                changed += HumanoidArmDeformationGuard.ClampAnatomicalArmMuscles(
                    ref _appliedTargetPose,
                    armStretchMuscleLimit,
                    upperArmTwistMuscleLimit,
                    lowerArmTwistMuscleLimit,
                    clampArmStretchMuscles);
            }

            changed += ApplyThumbAnatomicalGuard(ref _appliedTargetPose, false);

            if (changed <= 0)
            {
                return;
            }

            _targetHandler.SetHumanPose(ref _appliedTargetPose);
            if (!_appliedPoseClampWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] Target 적용 후 범위를 벗어난 Humanoid muscle {changed}개를 추가 보정했습니다.");
                _appliedPoseClampWarningLogged = true;
            }
        }

        private bool ShouldApplyThumbStretchOffset()
        {
            return !ShouldPreserveManualFingerReferenceThumbMuscles();
        }

        private bool ShouldPreserveManualFingerReferenceThumbMuscles()
        {
#if UNITY_EDITOR
            return _useEditorFingerPoseReference && preserveManualFingerReferenceThumbMuscles;
#else
            return false;
#endif
        }

        private int ApplyThumbAnatomicalGuard(ref HumanPose pose, bool applyStretchOffset)
        {
            if (!enableThumbAnatomicalGuard || pose.muscles == null)
            {
                return 0;
            }

            if (ShouldPreserveManualFingerReferenceThumbMuscles())
            {
                return 0;
            }

            float safeStretchMin = Mathf.Min(thumbStretchMin, thumbStretchMax);
            float safeStretchMax = Mathf.Max(thumbStretchMin, thumbStretchMax);
            float safeSpreadMin = Mathf.Min(thumbSpreadMin, thumbSpreadMax);
            float safeSpreadMax = Mathf.Max(thumbSpreadMin, thumbSpreadMax);
            int count = Mathf.Min(pose.muscles.Length, HumanTrait.MuscleCount);
            int changed = 0;

            for (int i = 0; i < count; i++)
            {
                string muscleName = HumanTrait.MuscleName[i];
                if (string.IsNullOrEmpty(muscleName))
                {
                    continue;
                }

                string normalizedName = muscleName.Replace(" ", "").ToLowerInvariant();
                if (!normalizedName.Contains("thumb"))
                {
                    continue;
                }

                float before = pose.muscles[i];
                float after = before;
                if (normalizedName.Contains("spread"))
                {
                    after = Mathf.Clamp(before, safeSpreadMin, safeSpreadMax);
                }
                else if (normalizedName.Contains("stretch"))
                {
                    float offset = applyStretchOffset ? thumbStretchOffset : 0f;
                    after = Mathf.Clamp(before + offset, safeStretchMin, safeStretchMax);
                }

                if (Mathf.Approximately(before, after))
                {
                    continue;
                }

                pose.muscles[i] = after;
                changed++;
            }

            if (changed > 0 && logThumbAnatomicalGuardCorrections && !_thumbGuardWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] 엄지 해부학적 제한으로 thumb muscle {changed}개를 보정했습니다.");
                _thumbGuardWarningLogged = true;
            }

            return changed;
        }

        private void ClampTargetThumbLocalRotations()
        {
            if (!enableThumbLocalRotationGuard || targetAnimator == null || _targetInitialThumbLocalRotations.Count == 0)
            {
                return;
            }

            if (HasFinalThumbLocalRotationGuard())
            {
                return;
            }

            int changed = 0;
            foreach (HumanBodyBones thumbBone in ThumbRotationBones)
            {
                Transform thumbTransform = targetAnimator.GetBoneTransform(thumbBone);
                if (thumbTransform == null || !_targetInitialThumbLocalRotations.TryGetValue(thumbTransform, out Quaternion initialRotation))
                {
                    continue;
                }

                float limit = GetThumbLocalRotationLimit(thumbBone);
                if (limit <= 0f)
                {
                    thumbTransform.localRotation = initialRotation;
                    changed++;
                    continue;
                }

                Quaternion currentRotation = thumbTransform.localRotation;
                if (!IsFinite(currentRotation))
                {
                    thumbTransform.localRotation = initialRotation;
                    changed++;
                    continue;
                }

                float angle = Quaternion.Angle(initialRotation, currentRotation);
                if (angle <= limit)
                {
                    continue;
                }

                thumbTransform.localRotation = LimitThumbLocalRotation(initialRotation, currentRotation, limit);
                changed++;
            }

            if (changed > 0 && logThumbLocalRotationGuardCorrections && !_thumbLocalRotationGuardWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] 엄지 본 localRotation {changed}개를 기준 자세 허용각 안으로 제한했습니다.");
                _thumbLocalRotationGuardWarningLogged = true;
            }
        }

        private bool HasFinalThumbLocalRotationGuard()
        {
            if (targetAnimator == null)
            {
                return false;
            }

            HumanoidThumbDeformationGuard guard = targetAnimator.GetComponent<HumanoidThumbDeformationGuard>();
            return guard != null && guard.enabled && guard.isActiveAndEnabled;
        }

        private static Quaternion LimitThumbLocalRotation(Quaternion initialRotation, Quaternion currentRotation, float softLimit)
        {
            float angle = Quaternion.Angle(initialRotation, currentRotation);
            float hardLimit = softLimit + ThumbLocalRotationHardOvershootDegrees;
            float targetAngle = Mathf.Min(hardLimit, softLimit + (angle - softLimit) * ThumbLocalRotationOvershootRatio);
            return Quaternion.RotateTowards(initialRotation, currentRotation, targetAngle);
        }

        private float GetThumbLocalRotationLimit(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                case HumanBodyBones.RightThumbProximal:
                    return Mathf.Clamp(thumbProximalMaxLocalAngle, 0f, 90f);
                case HumanBodyBones.LeftThumbIntermediate:
                case HumanBodyBones.RightThumbIntermediate:
                    return Mathf.Clamp(thumbIntermediateMaxLocalAngle, 0f, 120f);
                case HumanBodyBones.LeftThumbDistal:
                case HumanBodyBones.RightThumbDistal:
                    return Mathf.Clamp(thumbDistalMaxLocalAngle, 0f, 120f);
                default:
                    return 0f;
            }
        }

        private void ClampTargetRootPositionSpike(Vector3 positionBeforePose, string source)
        {
            if (!clampRootDeltaSpikes || targetAnimator == null)
            {
                return;
            }

            Vector3 poseDelta = targetAnimator.transform.position - positionBeforePose;
            if (!IsFinite(poseDelta) || poseDelta.magnitude <= maxRootDeltaPerFrame)
            {
                return;
            }

            if (logRootDeltaSpikes && !_rootDeltaSpikeWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] {source} root position spike {poseDelta.magnitude:F3}m clamped. limit={maxRootDeltaPerFrame:F3}m");
                _rootDeltaSpikeWarningLogged = true;
            }

            targetAnimator.transform.position = positionBeforePose + Vector3.ClampMagnitude(poseDelta, maxRootDeltaPerFrame);
        }

        private void CaptureTargetInitialTransforms(GameObject targetRoot)
        {
            _targetInitialScales.Clear();
            _targetInitialHumanoidLocalPositions.Clear();
            _targetInitialThumbBaseHelperLocalPositions.Clear();
            _targetInitialThumbLocalRotations.Clear();
            _scaleWarningLogged = false;
            _positionWarningLogged = false;
            _muscleClampWarningLogged = false;
            _anatomyGuardWarningLogged = false;
            _thumbGuardWarningLogged = false;
            _thumbLocalRotationGuardWarningLogged = false;
            _rootDeltaSpikeWarningLogged = false;
            _appliedPoseClampWarningLogged = false;

            foreach (Transform targetBone in targetRoot.GetComponentsInChildren<Transform>(true))
            {
                _targetInitialScales[targetBone] = targetBone.localScale;
            }

            if (targetAnimator == null || !targetAnimator.isHuman)
            {
                return;
            }

            for (int i = (int)HumanBodyBones.Hips; i < (int)HumanBodyBones.LastBone; i++)
            {
                HumanBodyBones bone = (HumanBodyBones)i;
                if (bone == HumanBodyBones.Hips)
                {
                    continue;
                }

                Transform targetBone = targetAnimator.GetBoneTransform(bone);
                if (targetBone == null || _targetInitialHumanoidLocalPositions.ContainsKey(targetBone))
                {
                    continue;
                }

                _targetInitialHumanoidLocalPositions[targetBone] = targetBone.localPosition;
            }

            foreach (HumanBodyBones thumbBone in ThumbRotationBones)
            {
                Transform thumbTransform = targetAnimator.GetBoneTransform(thumbBone);
                if (thumbTransform == null || _targetInitialThumbLocalRotations.ContainsKey(thumbTransform))
                {
                    continue;
                }

                _targetInitialThumbLocalRotations[thumbTransform] = thumbTransform.localRotation;
            }

            CaptureThumbBaseHelperLocalPosition("joint_LeftThumb0");
            CaptureThumbBaseHelperLocalPosition("joint_RightThumb0");
        }

        private void CaptureThumbBaseHelperLocalPosition(string nameSuffix)
        {
            Transform helperTransform = FindTargetTransformByNameSuffix(nameSuffix);
            if (helperTransform == null || _targetInitialThumbBaseHelperLocalPositions.ContainsKey(helperTransform))
            {
                return;
            }

            _targetInitialThumbBaseHelperLocalPositions[helperTransform] = helperTransform.localPosition;
        }

        private Transform FindTargetTransformByNameSuffix(string nameSuffix)
        {
            if (targetAnimator == null || targetAnimator.gameObject == null || string.IsNullOrEmpty(nameSuffix))
            {
                return null;
            }

            foreach (Transform candidate in targetAnimator.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != null && candidate.name.EndsWith(nameSuffix, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void RestoreTargetHumanoidLocalPositions()
        {
            if (!lockTargetHumanoidBonePositions)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Vector3> positionSnapshot in _targetInitialHumanoidLocalPositions)
            {
                Transform targetBone = positionSnapshot.Key;
                if (targetBone == null)
                {
                    continue;
                }

                if ((targetBone.localPosition - positionSnapshot.Value).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                if (!_positionWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] Target humanoid bone localPosition changed during retargeting. Restoring original bone length. First bone: {targetBone.name}");
                    _positionWarningLogged = true;
                }

                targetBone.localPosition = positionSnapshot.Value;
            }
        }

        private void RestoreTargetLocalScales()
        {
            foreach (KeyValuePair<Transform, Vector3> scaleSnapshot in _targetInitialScales)
            {
                Transform targetBone = scaleSnapshot.Key;
                if (targetBone == null)
                {
                    continue;
                }

                if ((targetBone.localScale - scaleSnapshot.Value).sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                if (!_scaleWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] Target bone scale changed during retargeting. Restoring original localScale. First bone: {targetBone.name}");
                    _scaleWarningLogged = true;
                }

                targetBone.localScale = scaleSnapshot.Value;
            }
        }

        void ApplyRaycastGrounding()
        {
            // 양발 위치 확보 (발목)
            Transform lFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);

            if (lFoot == null || rFoot == null) return;

            // 발바닥 위치 (발목 - 반지름)
            float footRadius = 0.04f; // 약간 여유 있게
            float lBottom = lFoot.position.y - footRadius;
            float rBottom = rFoot.position.y - footRadius;
            if (!IsFinite(lBottom) || !IsFinite(rBottom))
            {
                LogPoseWarning("Foot position became non-finite. Skipping grounding for this frame.");
                return;
            }

            // 현재 가장 낮은 발바닥 높이
            float lowestFootCurrentY = Mathf.Min(lBottom, rBottom);

            // 목표는 지면(0) + Offset
            // Raycast를 사용하여 실제 지면을 찾을 수도 있으나, 현재는 평면(Plane) 위라고 가정하고 0.0f 사용
            // 만약 계단이나 경사면이라면 Physics.Raycast로 hit.point.y를 구해야 함.
            float targetGroundY = 0.0f; // 평면 가정
            float targetHeight = targetGroundY + groundOffset;

            // 보정값 계산 (목표 - 현재)
            // 양수면 들어 올리고, 음수면 내림 (양방향)
            float adjustment = targetHeight - lowestFootCurrentY;
            if (!IsFinite(adjustment))
            {
                LogPoseWarning("Grounding adjustment became non-finite. Skipping grounding for this frame.");
                return;
            }

            Vector3 currentPos = targetAnimator.transform.position;
            if (!IsFinite(currentPos))
            {
                LogPoseWarning("Target position became non-finite before grounding. Resetting to origin.");
                currentPos = Vector3.zero;
            }

            // SetHumanPose가 매 프레임 bodyPosition을 다시 적용하므로 접지는 즉시 보정한다.
            currentPos.y += adjustment;

            // 최소 안전장치 (땅 밑으로 꺼지지 않게)
            if (currentPos.y < targetGroundY) currentPos.y = targetHeight;

            if (IsFinite(currentPos))
            {
                targetAnimator.transform.position = currentPos;
            }
        }
    }
}
