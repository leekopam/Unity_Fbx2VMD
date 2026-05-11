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

        [Tooltip("Keep target HumanPose bodyPosition Y stable while preserving FBX X/Z body sway.")]
        public bool preserveTargetBodyPosition = true;

        [Tooltip("Use HumanPose bodyPosition X/Z delta as target root motion to reduce visible foot sliding.")]
        public bool useBodyPositionXZRootMotion = false;

        [Tooltip("Editor-only experimental RootT X/Z root motion reference. Keep disabled until visual_body_arc_jitter passes without increasing jitter.")]
        public bool useEditorHumanoidRootTranslationReference = false;

        [Tooltip("Weight for Editor Humanoid RootT translation reference.")]
        [Range(0f, 1f)]
        public float editorHumanoidRootTranslationWeight = 0.25f;

        [Tooltip("Current-frame blend for smoothed Editor Humanoid RootT translation delta.")]
        [Range(0.05f, 1f)]
        public float editorHumanoidRootTranslationCurrentWeight = 0.35f;

        [Tooltip("When a foot is visually grounded, add a small X/Z root correction to reduce skating.")]
        public bool stabilizeGroundedFootXZ = false;

        [Tooltip("Foot-lock correction strength. Lower values preserve dance motion, higher values reduce skating.")]
        [Range(0f, 1f)]
        public float groundedFootLockWeight = 0.45f;

        [Tooltip("Maximum X/Z root correction per frame for grounded foot lock.")]
        [Range(0.001f, 0.1f)]
        public float maxGroundedFootLockStep = 0.025f;

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

        [Tooltip("손목 localRotation을 Sub_Manual/testPrefab Animator가 같은 FBX clip에서 평가한 값을 기준으로 덮어씁니다. t13.2 hand pose parity 회귀 보호용입니다.")]
        public bool useManualAnimatorHandLocalRotationReference = true;

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

        [Tooltip("수동 기준 Animator의 Hips localPosition을 target Hips에 선택적으로 적용합니다. testprefab Hips delta가 YYB에 전달되어 발 호 궤적이 심해지므로 기본 비활성화합니다.")]
        public bool useManualAnimatorHipsLocalPositionReference = false;

        [Tooltip("Sub_Manual/testPrefab Animator의 HumanPose bodyRotation을 retarget pose 기준으로 사용해 팔꿈치 bend plane 기준축 차이를 줄입니다.")]
        public bool useManualAnimatorBodyRotationReference = true;

        [Tooltip("preserveTargetBodyPosition=true 일 때 body Y 높이를 수동 기준 Animator의 HumanPose bodyPosition.y로 대체합니다. ghost Legacy-animation bodyPos 스파이크 없이 상체 높이를 애니메이션에 맞게 따라가도록 합니다.")]
        public bool useManualAnimatorBodyPositionYReference = false;

        [Tooltip("수동 기준 Hips localPosition 보정 강도입니다.")]
        [Range(0f, 1f)]
        public float manualAnimatorHipsLocalPositionWeight = 1f;

        [Tooltip("프레임당 수동 기준 Hips localPosition으로 이동할 수 있는 최대 보정 거리입니다.")]
        [Range(0.001f, 0.2f)]
        public float manualAnimatorHipsLocalPositionMaxOffset = 0.12f;

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

        [Header("--- GROUNDING STABILITY GUARD ---")]
        [Tooltip("발바닥 접지 보정이 한 프레임에 크게 튀지 않도록 부드럽게 반영합니다.")]
        public bool smoothGrounding = true;

        [Tooltip("한 프레임에 허용할 최대 수직 접지 보정값입니다.")]
        [Range(0.001f, 0.2f)]
        public float maxGroundingVerticalStepPerFrame = 0.01f;

        [Tooltip("접지 보정 목표값을 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)]
        public float groundingSmoothing = 0.25f;

        [Tooltip("이 값보다 작은 발바닥 떨림은 무시합니다.")]
        [Range(0f, 0.05f)]
        public float groundingDeadZone = 0.005f;

        [Tooltip("초기 접지 확정 뒤에는 root Y를 고정해 매 프레임 접지 추종으로 생기는 화면 떨림을 제거합니다.")]
        public bool freezeRootYAfterInitialGrounding = true;

        [Tooltip("Editor/GameView 프레임이 밀려도 Ghost clip time이 한 프레임에 크게 건너뛰지 않게 제한합니다.")]
        public bool clampLegacyAnimationVisualStep = false;

        [Tooltip("Ghost clip time이 한 렌더 프레임에 전진할 수 있는 기준 FPS입니다.")]
        [Range(15f, 120f)]
        public float legacyAnimationVisualFrameRate = 30f;

        [Tooltip("프레임 지연으로 pose가 한 번에 크게 바뀌면 clip time은 보존하고 target pose만 부드럽게 따라가게 합니다.")]
        public bool smoothPoseOnLegacyAnimationStepSpike = true;

        [Tooltip("pose spike smoothing 때 현재 FBX pose를 반영하는 비율입니다.")]
        [Range(0.1f, 1f)]
        public float poseVisualSpikeCurrentWeight = 0.65f;

        [Tooltip("이 값보다 큰 muscle delta가 발생하면 frame-time spike가 아니어도 pose smoothing을 적용합니다.")]
        [Range(0.05f, 1f)]
        public float poseVisualMuscleDeltaThreshold = 0.35f;

        [Tooltip("Renderer bounds 하단이 발바닥 추정치에서 과하게 멀면 접지 기준에서 제외합니다.")]
        public bool rejectRendererGroundingOutliers = true;

        [Tooltip("Renderer bounds 하단과 발바닥 추정치 사이에 허용할 최대 거리입니다.")]
        [Range(0.02f, 0.3f)]
        public float maxRendererFootGroundingSeparation = 0.12f;

        [Tooltip("LateUpdate 후반의 손/팔 보호 로직이 끝난 뒤 메시 bounds 기준으로 루트 Y만 한 번 더 보정합니다.")]
        public bool enableLateVisualGroundingCorrection = true;

        [Tooltip("최종 메시 bounds 보정이 한 프레임에 적용할 수 있는 최대 Y 이동량입니다.")]
        [Range(0.01f, 0.2f)]
        public float maxLateVisualGroundingCorrection = 0.2f;

        [Tooltip("최종 메시 bounds 접지 보정의 작은 잔여 오차를 부드럽게 반영해 모델 전체 떨림을 줄입니다.")]
        public bool smoothLateVisualGroundingCorrection = true;

        [Tooltip("Late visual grounding 잔여 오차가 이 값보다 작으면 smoothing 대상으로 봅니다. 큰 오차는 공중 부유 방지를 위해 즉시 보정합니다.")]
        [Range(0.005f, 0.1f)]
        public float lateVisualGroundingSnapThreshold = 0.03f;

        [Tooltip("작은 late visual grounding 잔여 오차를 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)]
        public float lateVisualGroundingSmoothing = 0.25f;

        [Tooltip("작은 late visual grounding smoothing 보정이 한 프레임에 움직일 수 있는 최대 Y 이동량입니다.")]
        [Range(0.001f, 0.05f)]
        public float maxLateVisualGroundingStepPerFrame = 0.003f;

        public float LastRootDeltaMagnitude => _lastRootDeltaMagnitude;
        public float MaxRootDeltaMagnitude => _maxRootDeltaMagnitude;
        public int RootDeltaSpikeSkippedCount => _rootDeltaSpikeSkippedCount;
        public float LastRootPositionPoseDeltaMagnitude => _lastRootPositionPoseDeltaMagnitude;
        public float MaxRootPositionPoseDeltaMagnitude => _maxRootPositionPoseDeltaMagnitude;
        public int RootPositionSpikeClampedCount => _rootPositionSpikeClampedCount;
        public float LastGroundingAdjustment => _lastGroundingAdjustment;
        public float MaxGroundingAdjustment => _maxGroundingAdjustment;
        public int GroundingStepClampedCount => _groundingStepClampedCount;
        public int GroundingSmoothedCount => _groundingSmoothedCount;
        public float LastGroundingVerticalStep => _lastGroundingVerticalStep;
        public float MaxGroundingVerticalStep => _maxGroundingVerticalStep;
        public float InitialGroundingVerticalStep => _initialGroundingVerticalStep;
        public float MaxGroundingVerticalStepAfterInitial => _maxGroundingVerticalStepAfterInitial;
        public float LastGroundingTargetY => _lastGroundingTargetY;
        public float LastGroundingLowestFootBottomY => _lastGroundingLowestFootBottomY;
        public float LastLegacyAnimationStep => _lastLegacyAnimationStep;
        public float MaxLegacyAnimationStep => _maxLegacyAnimationStep;
        public int LegacyAnimationStepSpikeCount => _legacyAnimationStepSpikeCount;
        public int PoseVisualSmoothingCount => _poseVisualSmoothingCount;
        public int PoseVisualMuscleDeltaOnlySkippedCount => _poseVisualMuscleDeltaOnlySkippedCount;
        public float LastPoseVisualMaxMuscleDelta => _lastPoseVisualMaxMuscleDelta;
        public float MaxPoseVisualMaxMuscleDelta => _maxPoseVisualMaxMuscleDelta;
        public int LastLeftThumbLocalRotationGuardClampCount => _lastLeftThumbLocalRotationGuardClampCount;
        public int LastRightThumbLocalRotationGuardClampCount => _lastRightThumbLocalRotationGuardClampCount;
        public int LastLeftThumbLocalRotationGuardPreserveCount => _lastLeftThumbLocalRotationGuardPreserveCount;
        public int LastRightThumbLocalRotationGuardPreserveCount => _lastRightThumbLocalRotationGuardPreserveCount;
        public float LastLeftThumbLocalRotationGuardCurrentRisk => _lastLeftThumbLocalRotationGuardCurrentRisk;
        public float LastRightThumbLocalRotationGuardCurrentRisk => _lastRightThumbLocalRotationGuardCurrentRisk;
        public float LastLeftThumbLocalRotationGuardLimitedRisk => _lastLeftThumbLocalRotationGuardLimitedRisk;
        public float LastRightThumbLocalRotationGuardLimitedRisk => _lastRightThumbLocalRotationGuardLimitedRisk;
        public bool LastLeftThumbWorldRotationSuppressCompetingOverride => _lastLeftThumbWorldRotationSuppressCompetingOverride;
        public bool LastRightThumbWorldRotationSuppressCompetingOverride => _lastRightThumbWorldRotationSuppressCompetingOverride;
        public bool LastLeftThumbWorldRotationKeepDetachedHelperOverride => _lastLeftThumbWorldRotationKeepDetachedHelperOverride;
        public bool LastRightThumbWorldRotationKeepDetachedHelperOverride => _lastRightThumbWorldRotationKeepDetachedHelperOverride;
        public float LastLeftThumbWorldRotationCurrentReferenceFrameDeviation => _lastLeftThumbWorldRotationCurrentReferenceFrameDeviation;
        public float LastRightThumbWorldRotationCurrentReferenceFrameDeviation => _lastRightThumbWorldRotationCurrentReferenceFrameDeviation;
        public float LastLeftThumbWorldRotationCandidateReferenceFrameDeviation => _lastLeftThumbWorldRotationCandidateReferenceFrameDeviation;
        public float LastRightThumbWorldRotationCandidateReferenceFrameDeviation => _lastRightThumbWorldRotationCandidateReferenceFrameDeviation;
        public int LastLeftThumbProximalWorldRotationPreserveReason => (int)_lastLeftThumbProximalWorldRotationPreserveReason;
        public int LastRightThumbProximalWorldRotationPreserveReason => (int)_lastRightThumbProximalWorldRotationPreserveReason;
        public int LastLeftThumbIntermediateWorldRotationPreserveReason => (int)_lastLeftThumbIntermediateWorldRotationPreserveReason;
        public int LastRightThumbIntermediateWorldRotationPreserveReason => (int)_lastRightThumbIntermediateWorldRotationPreserveReason;
        public float LastLeftThumbProximalWorldRotationCurrentReferenceAngle => _lastLeftThumbProximalWorldRotationCurrentReferenceAngle;
        public float LastRightThumbProximalWorldRotationCurrentReferenceAngle => _lastRightThumbProximalWorldRotationCurrentReferenceAngle;
        public float LastLeftThumbIntermediateWorldRotationCurrentReferenceAngle => _lastLeftThumbIntermediateWorldRotationCurrentReferenceAngle;
        public float LastRightThumbIntermediateWorldRotationCurrentReferenceAngle => _lastRightThumbIntermediateWorldRotationCurrentReferenceAngle;
        public float LastLeftThumbProximalWorldRotationCandidateReferenceAngle => _lastLeftThumbProximalWorldRotationCandidateReferenceAngle;
        public float LastRightThumbProximalWorldRotationCandidateReferenceAngle => _lastRightThumbProximalWorldRotationCandidateReferenceAngle;
        public float LastLeftThumbIntermediateWorldRotationCandidateReferenceAngle => _lastLeftThumbIntermediateWorldRotationCandidateReferenceAngle;
        public float LastRightThumbIntermediateWorldRotationCandidateReferenceAngle => _lastRightThumbIntermediateWorldRotationCandidateReferenceAngle;
        public float LastLeftThumbProximalWorldRotationPreserveCurrentRisk => _lastLeftThumbProximalWorldRotationPreserveCurrentRisk;
        public float LastRightThumbProximalWorldRotationPreserveCurrentRisk => _lastRightThumbProximalWorldRotationPreserveCurrentRisk;
        public float LastLeftThumbIntermediateWorldRotationPreserveCurrentRisk => _lastLeftThumbIntermediateWorldRotationPreserveCurrentRisk;
        public float LastRightThumbIntermediateWorldRotationPreserveCurrentRisk => _lastRightThumbIntermediateWorldRotationPreserveCurrentRisk;
        public float LastLeftThumbProximalWorldRotationPreserveLimitedRisk => _lastLeftThumbProximalWorldRotationPreserveLimitedRisk;
        public float LastRightThumbProximalWorldRotationPreserveLimitedRisk => _lastRightThumbProximalWorldRotationPreserveLimitedRisk;
        public float LastLeftThumbIntermediateWorldRotationPreserveLimitedRisk => _lastLeftThumbIntermediateWorldRotationPreserveLimitedRisk;
        public float LastRightThumbIntermediateWorldRotationPreserveLimitedRisk => _lastRightThumbIntermediateWorldRotationPreserveLimitedRisk;

        public void ResetPlaybackStabilityMetrics()
        {
            _hasPreviousLegacyAnimationTime = false;
            _previousLegacyAnimationTime = 0f;
            ResetEditorHumanoidRootTranslationReferenceState();
            _lastLegacyAnimationStep = float.NaN;
            _maxLegacyAnimationStep = 0f;
            _legacyAnimationStepSpikeCount = 0;
            _legacyAnimationStepSpikeThisFrame = false;
            ResetVisualPoseHistory();
            _poseVisualSmoothingCount = 0;
            _poseVisualMuscleDeltaOnlySkippedCount = 0;
            _lastPoseVisualMaxMuscleDelta = float.NaN;
            _maxPoseVisualMaxMuscleDelta = 0f;
        }

        [Tooltip("Target Humanoid 본의 localPosition을 초기값으로 되돌려 팔/다리 길이 변형을 막습니다.")]
        public bool lockTargetHumanoidBonePositions = true;

        // --- 내부 변수 ---
        private HumanPoseHandler _ghostHandler;
        private HumanPoseHandler _targetHandler;
        private HumanPose _humanPose;
        private HumanPose _appliedTargetPose;

        private Vector3 _prevGhostPos;
        private static readonly Quaternion LegacyFacingCorrection = Quaternion.Euler(0f, 180f, 0f);
        private const string LeftThumbBaseHelperNameSuffix = "joint_LeftThumb0";
        private const string RightThumbBaseHelperNameSuffix = "joint_RightThumb0";
        private const float ManualThumbOverrideSpreadWarningAngle = 38f;
        private const float ManualThumbOverrideSpreadFullRiskAngle = 52f;
        private const float ManualThumbOverrideProjectionMin = 0.358f;
        private const float ManualThumbOverrideProjectionMax = 0.5f;
        private const float ManualThumbHelperDistanceDeltaWarning = 0.003f;
        private const float ManualThumbHelperDistanceDeltaFullRisk = 0.008f;
        private const float ManualThumbHelperRotationWarning = 28f;
        private const float ManualThumbHelperRotationFullRisk = 70f;
        private const float ManualThumbWebbingRotationWarning = 18f;
        private const float ManualThumbWebbingRotationFullRisk = 45f;
        private const float ManualThumbWorldRotationReferenceToleranceDegrees = 1.5f;
        private const float ManualThumbDetachedHelperPreserveCurrentReferenceAngleMax = 12f;
        private const float ManualThumbReferenceSpreadDeviationToleranceDegrees = 1.5f;
        private const float ManualThumbReferenceProjectionDeviationTolerance = 0.015f;
        private const float ManualThumbDetachedHelperOverrideKeepSpreadDeltaMax = 14f;
        private const float ManualThumbDetachedHelperOverrideKeepProjectionDeltaMax = 0.1f;
        private const float ManualThumbPoseShapingSuppressMaxRisk = 0.35f;
        private const float ManualThumbOverrideSuppressRiskThreshold = 0.2f;
        private const float ManualThumbOverrideRiskIncreaseTolerance = 0.05f;

        private enum ThumbWorldRotationPreserveReason
        {
            None = 0,
            DetachedHelperReferenceAngle = 1,
            DetachedHelperReferenceFrameDeviation = 2,
            LocalRotationReference = 3,
            SuppressedManualReferenceAngle = 4,
            LocalRotationReferenceFallbackNoManualReference = 5,
            LocalRotationReferenceAfterSuppressedReference = 6
        }

        private Quaternion _facingCorrection = LegacyFacingCorrection;
        private Quaternion _poseRootRotationCorrection = Quaternion.identity;
        private Vector3 _targetReferenceBodyPosition;
        private bool _hasTargetReferenceBodyPosition;
        private Vector3 _previousBodyRootMotionPosition;
        private bool _hasPreviousBodyRootMotionPosition;
        private Vector3 _leftFootLockPosition;
        private Vector3 _rightFootLockPosition;
        private bool _leftFootLocked;
        private bool _rightFootLocked;
        private float _scaleRatio = 1.0f; // 체형 차이 비율
        private float _movementScaleMultiplier = 1.0f;
        private float _initialGhostHipHeight = 1.0f;
        private float _initialTargetHipHeight = 1.0f;
        private readonly Dictionary<Transform, Vector3> _targetInitialScales = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _targetInitialHumanoidLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _targetInitialThumbBaseHelperLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Quaternion> _targetInitialThumbLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<bool, Transform> _cachedThumbBaseHelpers = new Dictionary<bool, Transform>();
        private readonly Dictionary<bool, Transform> _cachedThumbBaseExplicitSources = new Dictionary<bool, Transform>();
        private readonly Dictionary<bool, float> _initialThumbBaseHelperSourceDistances = new Dictionary<bool, float>();
        private readonly Dictionary<bool, Quaternion> _initialThumbBaseHelperSourceRelativeRotations = new Dictionary<bool, Quaternion>();
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
        private float _lastRootDeltaMagnitude = float.NaN;
        private float _maxRootDeltaMagnitude;
        private int _rootDeltaSpikeSkippedCount;
        private float _lastRootPositionPoseDeltaMagnitude = float.NaN;
        private float _maxRootPositionPoseDeltaMagnitude;
        private int _rootPositionSpikeClampedCount;
        private bool _groundingInitialized;
        private float _lastGroundingAdjustment = float.NaN;
        private float _maxGroundingAdjustment;
        private int _groundingStepClampedCount;
        private int _groundingSmoothedCount;
        private float _lastGroundingVerticalStep = float.NaN;
        private float _maxGroundingVerticalStep;
        private float _initialGroundingVerticalStep = float.NaN;
        private float _maxGroundingVerticalStepAfterInitial;
        private float _lastGroundingTargetY = float.NaN;
        private float _lastGroundingLowestFootBottomY = float.NaN;
        private bool _lateVisualGroundingWarningLogged;
        private bool _rendererGroundingOutlierWarningLogged;
        private bool _lateVisualGroundingInitialized;
        private bool _hasFrozenGroundingRootY;
        private float _frozenGroundingRootY;
        private bool _hasPreviousLegacyAnimationTime;
        private float _previousLegacyAnimationTime;
        private float _lastLegacyAnimationStep = float.NaN;
        private float _maxLegacyAnimationStep;
        private int _legacyAnimationStepSpikeCount;
        private bool _legacyAnimationStepSpikeThisFrame;
        private bool _hasPreviousVisualPose;
        private float[] _previousVisualPoseMuscles;
        private Vector3 _previousVisualPoseBodyPosition;
        private Quaternion _previousVisualPoseBodyRotation = Quaternion.identity;
        private int _poseVisualSmoothingCount;
        private int _poseVisualMuscleDeltaOnlySkippedCount;
        private float _lastPoseVisualMaxMuscleDelta = float.NaN;
        private float _maxPoseVisualMaxMuscleDelta;
        private int _lastLeftThumbLocalRotationGuardClampCount;
        private int _lastRightThumbLocalRotationGuardClampCount;
        private int _lastLeftThumbLocalRotationGuardPreserveCount;
        private int _lastRightThumbLocalRotationGuardPreserveCount;
        private float _lastLeftThumbLocalRotationGuardCurrentRisk = float.NaN;
        private float _lastRightThumbLocalRotationGuardCurrentRisk = float.NaN;
        private float _lastLeftThumbLocalRotationGuardLimitedRisk = float.NaN;
        private float _lastRightThumbLocalRotationGuardLimitedRisk = float.NaN;
        private bool _lastLeftThumbWorldRotationSuppressCompetingOverride;
        private bool _lastRightThumbWorldRotationSuppressCompetingOverride;
        private bool _lastLeftThumbWorldRotationKeepDetachedHelperOverride;
        private bool _lastRightThumbWorldRotationKeepDetachedHelperOverride;
        private float _lastLeftThumbWorldRotationCurrentReferenceFrameDeviation = float.NaN;
        private float _lastRightThumbWorldRotationCurrentReferenceFrameDeviation = float.NaN;
        private float _lastLeftThumbWorldRotationCandidateReferenceFrameDeviation = float.NaN;
        private float _lastRightThumbWorldRotationCandidateReferenceFrameDeviation = float.NaN;
        private ThumbWorldRotationPreserveReason _lastLeftThumbProximalWorldRotationPreserveReason;
        private ThumbWorldRotationPreserveReason _lastRightThumbProximalWorldRotationPreserveReason;
        private ThumbWorldRotationPreserveReason _lastLeftThumbIntermediateWorldRotationPreserveReason;
        private ThumbWorldRotationPreserveReason _lastRightThumbIntermediateWorldRotationPreserveReason;
        private float _lastLeftThumbProximalWorldRotationCurrentReferenceAngle = float.NaN;
        private float _lastRightThumbProximalWorldRotationCurrentReferenceAngle = float.NaN;
        private float _lastLeftThumbIntermediateWorldRotationCurrentReferenceAngle = float.NaN;
        private float _lastRightThumbIntermediateWorldRotationCurrentReferenceAngle = float.NaN;
        private float _lastLeftThumbProximalWorldRotationCandidateReferenceAngle = float.NaN;
        private float _lastRightThumbProximalWorldRotationCandidateReferenceAngle = float.NaN;
        private float _lastLeftThumbIntermediateWorldRotationCandidateReferenceAngle = float.NaN;
        private float _lastRightThumbIntermediateWorldRotationCandidateReferenceAngle = float.NaN;
        private float _lastLeftThumbProximalWorldRotationPreserveCurrentRisk = float.NaN;
        private float _lastRightThumbProximalWorldRotationPreserveCurrentRisk = float.NaN;
        private float _lastLeftThumbIntermediateWorldRotationPreserveCurrentRisk = float.NaN;
        private float _lastRightThumbIntermediateWorldRotationPreserveCurrentRisk = float.NaN;
        private float _lastLeftThumbProximalWorldRotationPreserveLimitedRisk = float.NaN;
        private float _lastRightThumbProximalWorldRotationPreserveLimitedRisk = float.NaN;
        private float _lastLeftThumbIntermediateWorldRotationPreserveLimitedRisk = float.NaN;
        private float _lastRightThumbIntermediateWorldRotationPreserveLimitedRisk = float.NaN;
        private bool _hasEstimatedFootRadius;
        private float _estimatedFootRadius = DefaultFootRadius;
        private const float DefaultFootRadius = 0.04f;
        private const float GroundingDirectionReversalStepScale = 0.4f;
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
        private AnimationCurve _editorRootTranslationX;
        private AnimationCurve _editorRootTranslationZ;
        private Vector3 _previousEditorRootTranslation;
        private Vector3 _smoothedEditorRootTranslationDelta;
        private bool _hasPreviousEditorRootTranslation;
        private bool _hasSmoothedEditorRootTranslationDelta;
        private bool _useEditorRootTranslationReference;
        private bool _editorRootTranslationReferenceLogged;
        private readonly List<int> _editorFingerReferenceMuscleIndices = new List<int>();
        private GameObject _editorFingerReferenceInstance;
        private Animator _editorFingerReferenceAnimator;
        private HumanPoseHandler _editorFingerReferenceHandler;
        private HumanPose _editorFingerReferencePose;
        private int _editorFingerReferenceStateHash;
        private float _editorFingerReferenceClipLength;
        private bool _useEditorFingerPoseReference;
        private bool _editorFingerPoseReferenceLogged;
        private bool _editorBodyRotationReferenceLogged;
        private bool _hasEditorReferenceBodyPosition;
        private Vector3 _editorReferenceBodyPosition;
        private bool _hasEditorReferenceHipsRestLocalPosition;
        private Vector3 _editorReferenceHipsRestLocalPosition;
        private bool _editorHandLocalRotationReferenceLogged;
        private bool _editorThumbLocalRotationReferenceLogged;
        private bool _editorThumbSegmentDirectionReferenceLogged;
        private bool _editorHandPalmFrameReferenceLogged;
        private bool _editorThumbBasePositionReferenceLogged;
        private bool _editorHipsLocalPositionReferenceLogged;
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
            CacheTargetReferenceBodyPosition();
            CalibrateTargetFootRadius();

            // 초기 위치 저장
            _prevGhostPos = ghostAnimator.transform.position;
            _prevGhostPos = ghostAnimator.transform.position;
            ResetEditorHumanoidRootTranslationReferenceState();
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
                preserveTargetBodyPosition = settings.preserveRetargetBodyPosition;
                useBodyPositionXZRootMotion = settings.useRetargetBodyPositionXZRootMotion;
                useEditorHumanoidRootTranslationReference = settings.useEditorHumanoidRootTranslationReference;
                editorHumanoidRootTranslationWeight = Mathf.Clamp01(settings.editorHumanoidRootTranslationWeight);
                editorHumanoidRootTranslationCurrentWeight = Mathf.Clamp(settings.editorHumanoidRootTranslationCurrentWeight, 0.05f, 1f);
                stabilizeGroundedFootXZ = settings.stabilizeGroundedFootXZ;
                groundedFootLockWeight = Mathf.Clamp01(settings.GroundedFootLockWeight);
                maxGroundedFootLockStep = Mathf.Max(0.001f, settings.MaxGroundedFootLockStep);
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
                useManualAnimatorHandLocalRotationReference = settings.useManualAnimatorHandLocalRotationReference;
                useManualAnimatorThumbSegmentDirectionReference = settings.useManualAnimatorThumbSegmentDirectionReference;
                manualAnimatorThumbSegmentDirectionWeight = settings.manualAnimatorThumbSegmentDirectionWeight;
                useManualAnimatorThumbHandDirectionReference = settings.useManualAnimatorThumbHandDirectionReference;
                manualAnimatorThumbHandDirectionWeight = settings.manualAnimatorThumbHandDirectionWeight;
                useManualAnimatorHandPalmFrameReference = settings.useManualAnimatorHandPalmFrameReference;
                manualAnimatorHandPalmFrameWeight = settings.manualAnimatorHandPalmFrameWeight;
                useManualAnimatorThumbBasePositionReference = settings.useManualAnimatorThumbBasePositionReference;
                useManualAnimatorHipsLocalPositionReference = settings.useManualAnimatorHipsLocalPositionReference;
                useManualAnimatorBodyRotationReference = settings.useManualAnimatorBodyRotationReference;
                useManualAnimatorBodyPositionYReference = settings.useManualAnimatorBodyPositionYReference;
                manualAnimatorHipsLocalPositionWeight = Mathf.Clamp01(settings.manualAnimatorHipsLocalPositionWeight);
                manualAnimatorHipsLocalPositionMaxOffset = Mathf.Max(0.001f, settings.manualAnimatorHipsLocalPositionMaxOffset);
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
                smoothGrounding = settings.smoothRetargetGrounding;
                maxGroundingVerticalStepPerFrame = Mathf.Max(0.001f, settings.MaxGroundingVerticalStepPerFrame);
                groundingSmoothing = Mathf.Clamp01(settings.GroundingSmoothing);
                groundingDeadZone = Mathf.Max(0f, settings.GroundingDeadZone);
                freezeRootYAfterInitialGrounding = settings.FreezeRootYAfterInitialGrounding;
                clampLegacyAnimationVisualStep = settings.clampRetargetVisualClipStep;
                legacyAnimationVisualFrameRate = Mathf.Clamp(settings.RetargetVisualClipFrameRate, 15f, 120f);
                smoothPoseOnLegacyAnimationStepSpike = settings.smoothRetargetPoseOnVisualStepSpike;
                poseVisualSpikeCurrentWeight = Mathf.Clamp(settings.RetargetPoseVisualSpikeCurrentWeight, 0.1f, 1f);
                poseVisualMuscleDeltaThreshold = Mathf.Clamp(settings.RetargetPoseVisualMuscleDeltaThreshold, 0.05f, 1f);
                rejectRendererGroundingOutliers = settings.rejectRendererGroundingOutliers;
                maxRendererFootGroundingSeparation = Mathf.Max(0.02f, settings.MaxRendererFootGroundingSeparation);
                smoothLateVisualGroundingCorrection = settings.smoothLateVisualGroundingCorrection;
                lateVisualGroundingSnapThreshold = Mathf.Max(0.005f, settings.LateVisualGroundingSnapThreshold);
                lateVisualGroundingSmoothing = Mathf.Clamp01(settings.LateVisualGroundingSmoothing);
                maxLateVisualGroundingStepPerFrame = Mathf.Max(0.001f, settings.MaxLateVisualGroundingStepPerFrame);
                lockTargetHumanoidBonePositions = settings.lockTargetHumanoidBonePositions;
            }

            _isInitialized = true;
            EnsureLateVisualGroundingCorrection();
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

        public void ConfigureEditorHumanoidRootTranslationReference(AnimationClip referenceClip)
        {
            _editorRootTranslationX = null;
            _editorRootTranslationZ = null;
            _useEditorRootTranslationReference = false;
            _editorRootTranslationReferenceLogged = false;
            ResetEditorHumanoidRootTranslationReferenceState();

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

                if (binding.propertyName == "RootT.x")
                {
                    _editorRootTranslationX = UnityEditor.AnimationUtility.GetEditorCurve(referenceClip, binding);
                }
                else if (binding.propertyName == "RootT.z")
                {
                    _editorRootTranslationZ = UnityEditor.AnimationUtility.GetEditorCurve(referenceClip, binding);
                }
            }

            _useEditorRootTranslationReference = _editorRootTranslationX != null && _editorRootTranslationZ != null;
            if (_useEditorRootTranslationReference)
            {
                Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid RootT translation reference ready from {referenceClip.name}");
            }
        }

        public void ConfigureEditorHumanoidFingerPoseReference(
            GameObject referencePrefab,
            RuntimeAnimatorController referenceController,
            AnimationClip referenceClip,
            bool enableFingerPoseReference = true)
        {
            DisposeEditorHumanoidFingerPoseReference();
            _useEditorFingerPoseReference = false;
            _editorFingerPoseReferenceLogged = false;
            _editorBodyRotationReferenceLogged = false;
            _hasEditorReferenceBodyPosition = false;
            _hasEditorReferenceHipsRestLocalPosition = false;
            _editorHandLocalRotationReferenceLogged = false;
            _editorThumbLocalRotationReferenceLogged = false;
            _editorThumbSegmentDirectionReferenceLogged = false;
            _editorHandPalmFrameReferenceLogged = false;
            _editorThumbBasePositionReferenceLogged = false;
            _editorHipsLocalPositionReferenceLogged = false;
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
            DisableEditorReferenceRecordingComponents(_editorFingerReferenceInstance);
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
            _useEditorFingerPoseReference = enableFingerPoseReference && _editorFingerReferenceMuscleIndices.Count > 0;

            // testprefab의 clip 시작(frame 0) Hips localPosition을 캐시 — delta 기반 보정의 기준점
            _hasEditorReferenceHipsRestLocalPosition = false;
            _editorFingerReferenceAnimator.Play(_editorFingerReferenceStateHash, 0, 0f);
            _editorFingerReferenceAnimator.Update(0f);
            Transform refHipsRest = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (refHipsRest != null && IsFinite(refHipsRest.localPosition))
            {
                _editorReferenceHipsRestLocalPosition = refHipsRest.localPosition;
                _hasEditorReferenceHipsRestLocalPosition = true;
            }

            Debug.Log($"[PoseSpaceRetargeter] Manual Animator finger reference ready: prefab={referencePrefab.name}, controller={referenceController.name}, state={stateName}, clip={referenceClip.name}, muscles={_editorFingerReferenceMuscleIndices.Count}, hipsRest={(_hasEditorReferenceHipsRestLocalPosition ? _editorReferenceHipsRestLocalPosition.y.ToString("F4") : "N/A")}");
        }

        private static void DisableEditorReferenceRecordingComponents(GameObject referenceInstance)
        {
            if (referenceInstance == null)
            {
                return;
            }

            int disabledSampleCodeCount = 0;
            foreach (global::HumanoidSampleCode sampleCode in referenceInstance.GetComponentsInChildren<global::HumanoidSampleCode>(true))
            {
                sampleCode.AutoStartRecording = false;
                sampleCode.enabled = false;
                disabledSampleCodeCount++;
            }

            int disabledRecorderCount = 0;
            foreach (global::UnityHumanoidVMDRecorder recorder in referenceInstance.GetComponentsInChildren<global::UnityHumanoidVMDRecorder>(true))
            {
                if (recorder.IsRecording)
                {
                    recorder.StopRecording();
                }

                recorder.enabled = false;
                disabledRecorderCount++;
            }

            if (disabledSampleCodeCount > 0 || disabledRecorderCount > 0)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual finger reference recording components disabled. sampleCode={disabledSampleCodeCount}, recorder={disabledRecorderCount}");
            }
        }
#endif

        void LateUpdate()
        {
            if (!_isInitialized || ghostAnimator == null || targetAnimator == null || _ghostHandler == null || _targetHandler == null) return;

            UpdateLegacyAnimationVisualStep();

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
            ApplyEditorHumanoidBodyRotationReference(ref _humanPose);
            ApplyThumbAnatomicalGuard(ref _humanPose, ShouldApplyThumbStretchOffset());
            ClampPoseMuscles(ref _humanPose);
            ApplyAnatomicalArmGuard(ref _humanPose);
            SmoothPoseOnVisualSpike(ref _humanPose);
            Quaternion poseRootRotation = _humanPose.bodyRotation;
            if (preserveFbxRootRotation && !_hasPoseRootRotationCorrection && IsFinite(poseRootRotation) && _legacyAnim != null && _legacyAnim.isPlaying)
            {
                _poseRootRotationCorrection = Quaternion.Inverse(poseRootRotation);
                _hasPoseRootRotationCorrection = true;
            }

            // Y축은 target 기준으로 안정화하고, X/Z 체중 이동은 FBX 값을 유지한다.
            Vector3 bodyPos = _humanPose.bodyPosition;
            bodyPos.x *= _scaleRatio;
            bodyPos.z *= _scaleRatio;
            Vector3 bodyRootDelta = ExtractBodyPositionXZRootDelta(bodyPos);
            if (preserveTargetBodyPosition && _hasTargetReferenceBodyPosition)
            {
                bodyPos = _targetReferenceBodyPosition;
                // 수동 기준 Animator의 bodyPos.y로 Y를 대체: ghost Legacy bodyPos 스파이크 없이 애니메이션 높이를 따른다.
                if (useManualAnimatorBodyPositionYReference && _hasEditorReferenceBodyPosition)
                {
                    bodyPos.y = _editorReferenceBodyPosition.y;
                }
            }
            else
            {
                bodyPos.y *= _scaleRatio;
            }
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
            ApplyEditorHumanoidHipsLocalPositionReference();
            ApplyEditorHumanoidThumbBasePositionReference();
#endif
            ClampTargetThumbLocalRotations();
#if UNITY_EDITOR
            ApplyEditorHumanoidThumbLocalRotationReference();
            ApplyEditorHumanoidHandPalmFrameReference();
            ApplyEditorHumanoidHandLocalRotationReference();
            ApplyEditorHumanoidThumbSegmentDirectionReference();
            ApplyEditorHumanoidThumbHandDirectionReference();
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
            Vector3 editorRootTranslationDelta = ExtractEditorRootTranslationDelta(ghostDelta);

            // 내 캐릭터 크기에 맞춰 이동량 스케일링
            Vector3 targetDelta = (ghostDelta * _scaleRatio + editorRootTranslationDelta + bodyRootDelta) * _movementScaleMultiplier;
            if (!IsFinite(targetDelta))
            {
                LogPoseWarning("Retarget root delta became non-finite. Skipping root motion for this frame.");
                _lastRootDeltaMagnitude = float.NaN;
                _rootDeltaSpikeSkippedCount++;
                targetDelta = Vector3.zero;
            }
            else
            {
                _lastRootDeltaMagnitude = targetDelta.magnitude;
                _maxRootDeltaMagnitude = Mathf.Max(_maxRootDeltaMagnitude, _lastRootDeltaMagnitude);

                if (clampRootDeltaSpikes && _lastRootDeltaMagnitude > maxRootDeltaPerFrame)
                {
                    _rootDeltaSpikeSkippedCount++;
                    if (logRootDeltaSpikes && !_rootDeltaSpikeWarningLogged)
                    {
                        Debug.LogWarning($"[PoseSpaceRetargeter] Root delta spike {_lastRootDeltaMagnitude:F3}m skipped. ghostDelta={ghostDelta.magnitude:F3}m, editorRootDelta={editorRootTranslationDelta.magnitude:F3}m, limit={maxRootDeltaPerFrame:F3}m");
                        _rootDeltaSpikeWarningLogged = true;
                    }

                    targetDelta = Vector3.zero;
                }
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

        private void UpdateLegacyAnimationVisualStep()
        {
            _legacyAnimationStepSpikeThisFrame = false;

            if (_legacyAnim == null || _legacyAnim.clip == null)
            {
                return;
            }

            AnimationState state = _legacyAnim[_legacyAnim.clip.name];
            if (state == null)
            {
                return;
            }

            float length = Mathf.Max(0f, state.length);
            float currentTime = Mathf.Clamp(state.time, 0f, length);
            if (!_hasPreviousLegacyAnimationTime)
            {
                _previousLegacyAnimationTime = currentTime;
                _hasPreviousLegacyAnimationTime = true;
                _lastLegacyAnimationStep = 0f;
                return;
            }

            if (currentTime + 0.0001f < _previousLegacyAnimationTime)
            {
                _previousLegacyAnimationTime = currentTime;
                _lastLegacyAnimationStep = 0f;
                ResetVisualPoseHistory();
                return;
            }

            float maxStep = 1f / Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            float step = currentTime - _previousLegacyAnimationTime;
            _lastLegacyAnimationStep = step;
            _maxLegacyAnimationStep = Mathf.Max(_maxLegacyAnimationStep, step);
            float spikeTolerance = Mathf.Max(0.001f, maxStep * 0.05f);
            if (step > maxStep + spikeTolerance)
            {
                _legacyAnimationStepSpikeThisFrame = true;
                _legacyAnimationStepSpikeCount++;
                if (clampLegacyAnimationVisualStep)
                {
                    currentTime = Mathf.Min(_previousLegacyAnimationTime + maxStep, length);
                    state.time = currentTime;
                    _legacyAnim.Sample();
                    step = currentTime - _previousLegacyAnimationTime;
                    _lastLegacyAnimationStep = step;
                }
            }

            _previousLegacyAnimationTime = currentTime;
        }

        private void SmoothPoseOnVisualSpike(ref HumanPose pose)
        {
            if (!smoothPoseOnLegacyAnimationStepSpike || pose.muscles == null || pose.muscles.Length == 0)
            {
                RememberVisualPose(pose);
                return;
            }

            if (!_hasPreviousVisualPose ||
                _previousVisualPoseMuscles == null ||
                _previousVisualPoseMuscles.Length != pose.muscles.Length)
            {
                RememberVisualPose(pose);
                return;
            }

            float maxMuscleDelta = 0f;
            for (int i = 0; i < pose.muscles.Length; i++)
            {
                float delta = Mathf.Abs(pose.muscles[i] - _previousVisualPoseMuscles[i]);
                if (delta > maxMuscleDelta)
                {
                    maxMuscleDelta = delta;
                }
            }

            _lastPoseVisualMaxMuscleDelta = maxMuscleDelta;
            _maxPoseVisualMaxMuscleDelta = Mathf.Max(_maxPoseVisualMaxMuscleDelta, maxMuscleDelta);

            float bodyPositionDelta = Vector3.Distance(_previousVisualPoseBodyPosition, pose.bodyPosition);
            float bodyRotationDelta = Quaternion.Angle(_previousVisualPoseBodyRotation, pose.bodyRotation);
            bool bodyPoseSpike = bodyPositionDelta > 0.08f || bodyRotationDelta > 25f;
            bool muscleDeltaOnlySpike = maxMuscleDelta > poseVisualMuscleDeltaThreshold &&
                !_legacyAnimationStepSpikeThisFrame &&
                !bodyPoseSpike;
            bool shouldSmooth = _legacyAnimationStepSpikeThisFrame || bodyPoseSpike;

            if (shouldSmooth)
            {
                float currentWeight = Mathf.Clamp(poseVisualSpikeCurrentWeight, 0.1f, 1f);
                for (int i = 0; i < pose.muscles.Length; i++)
                {
                    pose.muscles[i] = Mathf.Lerp(_previousVisualPoseMuscles[i], pose.muscles[i], currentWeight);
                }

                pose.bodyPosition = Vector3.Lerp(_previousVisualPoseBodyPosition, pose.bodyPosition, currentWeight);
                pose.bodyRotation = Quaternion.Slerp(_previousVisualPoseBodyRotation, pose.bodyRotation, currentWeight);
                _poseVisualSmoothingCount++;
            }
            else if (muscleDeltaOnlySpike)
            {
                // 빠른 손/팔 동작 자체를 smoothing하면 의도한 동작이 멈칫하고 몸통이 늦게 따라오는 것처럼 보인다.
                _poseVisualMuscleDeltaOnlySkippedCount++;
            }

            RememberVisualPose(pose);
        }

        private void RememberVisualPose(HumanPose pose)
        {
            if (pose.muscles == null || pose.muscles.Length == 0 || !IsFinite(pose))
            {
                return;
            }

            if (_previousVisualPoseMuscles == null || _previousVisualPoseMuscles.Length != pose.muscles.Length)
            {
                _previousVisualPoseMuscles = new float[pose.muscles.Length];
            }

            Array.Copy(pose.muscles, _previousVisualPoseMuscles, pose.muscles.Length);
            _previousVisualPoseBodyPosition = pose.bodyPosition;
            _previousVisualPoseBodyRotation = pose.bodyRotation;
            _hasPreviousVisualPose = true;
        }

        private void ResetVisualPoseHistory()
        {
            _hasPreviousVisualPose = false;
            _previousVisualPoseBodyPosition = Vector3.zero;
            _previousVisualPoseBodyRotation = Quaternion.identity;
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

                if (!ShouldUseEditorHumanoidMuscleReference(pair.Key))
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

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

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
                float time = GetLegacyAnimationTime();
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator finger reference applied at t={time:F3}s.");
                _editorFingerPoseReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidBodyRotationReference(ref HumanPose pose)
        {
            if (!useManualAnimatorBodyRotationReference ||
                _editorFingerReferenceAnimator == null ||
                _editorFingerReferenceHandler == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            _editorFingerReferenceHandler.GetHumanPose(ref _editorFingerReferencePose);
            Quaternion referenceBodyRotation = _editorFingerReferencePose.bodyRotation;
            if (!IsFinite(referenceBodyRotation))
            {
                return;
            }

            pose.bodyRotation = referenceBodyRotation;

            Vector3 refBodyPos = _editorFingerReferencePose.bodyPosition;
            if (IsFinite(refBodyPos) && refBodyPos.y > 0.01f)
            {
                _editorReferenceBodyPosition = refBodyPos;
                _hasEditorReferenceBodyPosition = true;
            }
            if (!_editorBodyRotationReferenceLogged)
            {
                float time = GetLegacyAnimationTime();
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator bodyRotation reference applied at t={time:F3}s.");
                _editorBodyRotationReferenceLogged = true;
            }
        }

        private bool UpdateEditorManualReferenceAnimator()
        {
            if (_editorFingerReferenceAnimator == null || _editorFingerReferenceClipLength <= 0f)
            {
                return false;
            }

            float time = GetLegacyAnimationTime();
            float normalizedTime = Mathf.Clamp01(time / _editorFingerReferenceClipLength);
            _editorFingerReferenceAnimator.Play(_editorFingerReferenceStateHash, 0, normalizedTime);
            _editorFingerReferenceAnimator.Update(0f);
            return true;
        }

        private void ApplyEditorHumanoidHipsLocalPositionReference()
        {
            if (!useManualAnimatorHipsLocalPositionReference ||
                manualAnimatorHipsLocalPositionWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            Transform referenceHips = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (referenceHips == null || targetHips == null)
            {
                return;
            }

            Vector3 refCurrentLocalPosition = referenceHips.localPosition;
            Vector3 currentLocalPosition = targetHips.localPosition;
            if (!IsFinite(refCurrentLocalPosition) || !IsFinite(currentLocalPosition))
            {
                return;
            }

            // Delta 방식: testprefab의 clip 시작 대비 현재 변위만 YYB 자연 위치에 더한다.
            // 절대 복사는 모델 비율 차이(YYB Hips Y≈1.024 vs testprefab≈1.056)로 인해 YYB Hips를 잘못된 높이로 강제한다.
            Vector3 desiredLocalPosition;
            if (_hasEditorReferenceHipsRestLocalPosition)
            {
                Vector3 refDelta = refCurrentLocalPosition - _editorReferenceHipsRestLocalPosition;
                desiredLocalPosition = currentLocalPosition + refDelta;
            }
            else
            {
                desiredLocalPosition = refCurrentLocalPosition;
            }

            Vector3 delta = desiredLocalPosition - currentLocalPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            float maxOffset = Mathf.Max(0f, manualAnimatorHipsLocalPositionMaxOffset);
            if (maxOffset > 0f)
            {
                delta = Vector3.ClampMagnitude(delta, maxOffset);
            }

            Vector3 nextLocalPosition = currentLocalPosition + delta * Mathf.Clamp01(manualAnimatorHipsLocalPositionWeight);
            if (!IsFinite(nextLocalPosition))
            {
                return;
            }

            targetHips.localPosition = nextLocalPosition;
            if (!_editorHipsLocalPositionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator Hips localPosition reference applied. weight={manualAnimatorHipsLocalPositionWeight:F2}, maxOffset={manualAnimatorHipsLocalPositionMaxOffset:F3}m");
                _editorHipsLocalPositionReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidHandLocalRotationReference()
        {
            if (!useManualAnimatorHandLocalRotationReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            int changed = 0;
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += ApplyEditorHumanoidHandLocalRotationReferenceBone(HumanBodyBones.LeftHand);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += ApplyEditorHumanoidHandLocalRotationReferenceBone(HumanBodyBones.RightHand);
            }

            if (changed > 0 && !_editorHandLocalRotationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator hand localRotation reference applied. bones={changed}");
                _editorHandLocalRotationReferenceLogged = true;
            }
        }

        private int ApplyEditorHumanoidHandLocalRotationReferenceBone(HumanBodyBones handBone)
        {
            Transform source = _editorFingerReferenceAnimator.GetBoneTransform(handBone);
            Transform target = targetAnimator.GetBoneTransform(handBone);
            if (source == null || target == null)
            {
                return 0;
            }

            Quaternion sourceRotation = source.localRotation;
            if (!IsFinite(sourceRotation) || Quaternion.Angle(target.localRotation, sourceRotation) <= 0.001f)
            {
                return 0;
            }

            target.localRotation = sourceRotation;
            return 1;
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

            bool suppressLeftThumbLocalRotation = ShouldSuppressCompetingManualThumbOverride(true);
            bool suppressRightThumbLocalRotation = ShouldSuppressCompetingManualThumbOverride(false);
            int changed = 0;
            foreach (HumanBodyBones thumbBone in ThumbRotationBones)
            {
                Transform source = _editorFingerReferenceAnimator.GetBoneTransform(thumbBone);
                Transform target = targetAnimator.GetBoneTransform(thumbBone);
                if (source == null || target == null)
                {
                    continue;
                }

                if ((thumbBone == HumanBodyBones.LeftThumbProximal ||
                        thumbBone == HumanBodyBones.LeftThumbIntermediate ||
                        thumbBone == HumanBodyBones.LeftThumbDistal) &&
                    suppressLeftThumbLocalRotation)
                {
                    continue;
                }

                if ((thumbBone == HumanBodyBones.RightThumbProximal ||
                        thumbBone == HumanBodyBones.RightThumbIntermediate ||
                        thumbBone == HumanBodyBones.RightThumbDistal) &&
                    suppressRightThumbLocalRotation)
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
                LeftThumbBaseHelperNameSuffix);
            changed += ApplyEditorHumanoidThumbBasePositionReferenceSide(
                HumanBodyBones.RightHand,
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightIndexProximal,
                RightThumbBaseHelperNameSuffix);

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
            if (ShouldRejectManualThumbBasePositionOverride(leftHand, targetThumb, desiredWorldPosition))
            {
                return 0;
            }

            int changed = 0;
            changed += ApplyThumbBasePositionToTransform(
                targetThumb,
                desiredWorldPosition,
                _targetInitialHumanoidLocalPositions);

            Transform helperTransform = GetCachedThumbBaseHelper(leftHand) ?? FindTargetTransformByNameSuffix(helperNameSuffix);
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
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += AlignEditorHumanoidThumbSegmentDirection(true, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, weight);
                changed += AlignEditorHumanoidThumbSegmentDirection(true, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal, weight);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += AlignEditorHumanoidThumbSegmentDirection(false, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, weight);
                changed += AlignEditorHumanoidThumbSegmentDirection(false, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal, weight);
            }

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
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += AlignEditorHumanoidThumbHandDirection(true, weight);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += AlignEditorHumanoidThumbHandDirection(false, weight);
            }

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
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += AlignEditorHumanoidHandPalmFrame(true, weight);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += AlignEditorHumanoidHandPalmFrame(false, weight);
            }

            if (changed > 0 && !_editorHandPalmFrameReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator hand palm-frame reference applied. hands={changed}, weight={weight:F2}");
                _editorHandPalmFrameReferenceLogged = true;
            }
        }

        private bool ShouldSuppressCompetingManualThumbOverride(bool leftHand)
        {
            if (!TryEvaluateThumbManualOverrideRisk(leftHand, out float risk) ||
                risk < ManualThumbOverrideSuppressRiskThreshold)
            {
                return false;
            }

            return !ShouldKeepDetachedHelperManualThumbOverrides(leftHand);
        }

        private bool ShouldKeepDetachedHelperManualThumbOverrides(bool leftHand)
        {
            HumanBodyBones proximalBone = leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
            if (!HasDetachedThumbBaseHelperRelationship(proximalBone, leftHand) ||
                !TryEvaluateCurrentThumbReferenceFrameDelta(leftHand, out float spreadDelta, out float projectionDelta))
            {
                return false;
            }

            return spreadDelta <= ManualThumbDetachedHelperOverrideKeepSpreadDeltaMax &&
                projectionDelta <= ManualThumbDetachedHelperOverrideKeepProjectionDeltaMax;
        }

        public bool TryGetHighRiskManualThumbPoseConstraintOverrides(
            bool leftHand,
            out float projectionMin,
            out float projectionMax,
            out float maxSpreadAngle)
        {
            projectionMin = ManualThumbOverrideProjectionMin;
            projectionMax = ManualThumbOverrideProjectionMax;
            maxSpreadAngle = ManualThumbOverrideSpreadFullRiskAngle;

            bool manualOverridePathActive =
                ShouldSuppressCompetingManualThumbOverride(leftHand) ||
                ShouldKeepDetachedHelperManualThumbOverrides(leftHand);

            return manualOverridePathActive &&
                TryEvaluateThumbManualOverrideRisk(leftHand, out float risk) &&
                IsFinite(risk) &&
                risk >= ManualThumbPoseShapingSuppressMaxRisk;
        }

        public string BuildThumbHelperRelationshipDebugSummary(bool leftHand)
        {
            Transform helperTransform = GetCachedThumbBaseHelper(leftHand);
            Transform sourceTransform = GetCachedExplicitThumbBaseSource(leftHand);
            string sideLabel = leftHand ? "L" : "R";

            if (helperTransform == null || sourceTransform == null)
            {
                return $"side={sideLabel}, helper={GetHierarchyPath(helperTransform)}, source={GetHierarchyPath(sourceTransform)}, state=missing";
            }

            float currentDistance = Vector3.Distance(helperTransform.position, sourceTransform.position);
            float initialDistance = _initialThumbBaseHelperSourceDistances.TryGetValue(leftHand, out float storedDistance)
                ? storedDistance
                : float.NaN;
            float distanceDelta = IsFinite(initialDistance) && IsFinite(currentDistance)
                ? Mathf.Abs(currentDistance - initialDistance)
                : float.NaN;

            float relativeRotationDelta = float.NaN;
            if (_initialThumbBaseHelperSourceRelativeRotations.TryGetValue(leftHand, out Quaternion initialRelativeRotation))
            {
                Quaternion currentRelativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
                relativeRotationDelta = Quaternion.Angle(initialRelativeRotation, currentRelativeRotation);
            }

            float risk = float.NaN;
            TryEvaluateThumbManualOverrideRisk(leftHand, out risk);
            float spreadDelta = float.NaN;
            float projectionDelta = float.NaN;
            TryEvaluateCurrentThumbReferenceFrameDelta(leftHand, out spreadDelta, out projectionDelta);

            return
                $"side={sideLabel}, helper={GetHierarchyPath(helperTransform)}, source={GetHierarchyPath(sourceTransform)}, " +
                $"initDist={FormatDebugFloat(initialDistance)}, currDist={FormatDebugFloat(currentDistance)}, distDelta={FormatDebugFloat(distanceDelta)}, " +
                $"relRotDelta={FormatDebugFloat(relativeRotationDelta)}, risk={FormatDebugFloat(risk)}, " +
                $"suppress={ShouldSuppressCompetingManualThumbOverride(leftHand)}, keepDetached={ShouldKeepDetachedHelperManualThumbOverrides(leftHand)}, " +
                $"spreadDelta={FormatDebugFloat(spreadDelta)}, projectionDelta={FormatDebugFloat(projectionDelta)}";
        }

        private bool ShouldRejectManualThumbBasePositionOverride(bool leftHand, Transform targetThumb, Vector3 desiredWorldPosition)
        {
            if (targetThumb == null)
            {
                return false;
            }

            if (!TryEvaluateThumbManualOverrideRisk(leftHand, targetThumb.position, false, Vector3.zero, out float currentRisk) ||
                !TryEvaluateThumbManualOverrideRisk(leftHand, desiredWorldPosition, true, desiredWorldPosition, out float desiredRisk))
            {
                return false;
            }

            return desiredRisk >= ManualThumbOverrideSuppressRiskThreshold &&
                desiredRisk > currentRisk + ManualThumbOverrideRiskIncreaseTolerance;
        }

        private bool TryEvaluateThumbManualOverrideRisk(bool leftHand, out float risk)
        {
            Transform thumbProximal = targetAnimator != null
                ? targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal)
                : null;
            return TryEvaluateThumbManualOverrideRisk(
                leftHand,
                thumbProximal != null ? thumbProximal.position : Vector3.zero,
                false,
                Vector3.zero,
                out risk);
        }

        private bool TryEvaluateThumbManualOverrideRisk(
            bool leftHand,
            Vector3 thumbProximalWorldPosition,
            bool overrideHelperWorldPosition,
            Vector3 helperWorldPosition,
            out float risk)
        {
            risk = float.NaN;
            if (targetAnimator == null ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return false;
            }

            Transform hand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform index = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform thumbIntermediate = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
            if (hand == null || index == null || thumbIntermediate == null)
            {
                return false;
            }

            Vector3 thumbDirection = thumbIntermediate.position - thumbProximalWorldPosition;
            Vector3 indexDirection = index.position - hand.position;
            if (!TryNormalize(thumbDirection, out thumbDirection) ||
                !TryNormalize(indexDirection, out indexDirection))
            {
                return false;
            }

            float spreadAngle = Vector3.Angle(thumbDirection, indexDirection);
            float spreadRisk = RiskAbove(
                spreadAngle,
                ManualThumbOverrideSpreadWarningAngle,
                ManualThumbOverrideSpreadFullRiskAngle);
            float projection = Vector3.Dot(thumbDirection, targetFrame.Normal);
            float projectionRisk = RiskOutsideRange(
                projection,
                ManualThumbOverrideProjectionMin,
                ManualThumbOverrideProjectionMax,
                1f);
            float helperSeparationRisk = float.NaN;
            float webbingRisk = float.NaN;
            if (TryEvaluateThumbHelperRelationshipRisk(
                leftHand,
                overrideHelperWorldPosition,
                helperWorldPosition,
                spreadRisk,
                projectionRisk,
                out float helperDistanceRisk,
                out float helperRotationRisk,
                out float helperWebbingRisk))
            {
                helperSeparationRisk = MaxFinite(helperDistanceRisk, helperRotationRisk);
                webbingRisk = helperWebbingRisk;
            }

            risk = MaxFinite(spreadRisk, projectionRisk, helperSeparationRisk, webbingRisk);
            return !float.IsNaN(risk) && !float.IsInfinity(risk);
        }

        private bool TryEvaluateThumbHelperRelationshipRisk(
            bool leftHand,
            bool overrideHelperWorldPosition,
            Vector3 helperWorldPosition,
            float spreadRisk,
            float projectionRisk,
            out float helperDistanceRisk,
            out float helperRotationRisk,
            out float webbingRisk)
        {
            helperDistanceRisk = float.NaN;
            helperRotationRisk = float.NaN;
            webbingRisk = float.NaN;

            Transform helperTransform = GetCachedThumbBaseHelper(leftHand);
            Transform sourceTransform = GetCachedExplicitThumbBaseSource(leftHand);
            if (helperTransform == null || sourceTransform == null)
            {
                return false;
            }

            EnsureThumbBaseHelperRelationshipBaseline(leftHand, helperTransform, sourceTransform);
            if (!_initialThumbBaseHelperSourceDistances.TryGetValue(leftHand, out float initialDistance) ||
                !_initialThumbBaseHelperSourceRelativeRotations.TryGetValue(leftHand, out Quaternion initialRelativeRotation))
            {
                return false;
            }

            Vector3 effectiveHelperWorldPosition = overrideHelperWorldPosition ? helperWorldPosition : helperTransform.position;
            float distance = Vector3.Distance(effectiveHelperWorldPosition, sourceTransform.position);
            if (IsFinite(distance))
            {
                helperDistanceRisk = RiskAbove(
                    Mathf.Abs(distance - initialDistance),
                    ManualThumbHelperDistanceDeltaWarning,
                    ManualThumbHelperDistanceDeltaFullRisk);
            }

            Quaternion relativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
            if (IsFinite(relativeRotation))
            {
                float rotationDelta = Quaternion.Angle(initialRelativeRotation, relativeRotation);
                helperRotationRisk = RiskAbove(
                    rotationDelta,
                    ManualThumbHelperRotationWarning,
                    ManualThumbHelperRotationFullRisk);
                webbingRisk = MaxFinite(
                    spreadRisk,
                    projectionRisk,
                    helperDistanceRisk,
                    RiskAbove(
                        rotationDelta,
                        ManualThumbWebbingRotationWarning,
                        ManualThumbWebbingRotationFullRisk));
            }

            return !float.IsNaN(MaxFinite(helperDistanceRisk, helperRotationRisk, webbingRisk));
        }

        private void EnsureThumbBaseHelperRelationshipBaseline(bool leftHand, Transform helperTransform, Transform sourceTransform)
        {
            if (!_initialThumbBaseHelperSourceDistances.ContainsKey(leftHand) ||
                !_initialThumbBaseHelperSourceRelativeRotations.ContainsKey(leftHand))
            {
                CaptureThumbBaseHelperRelationshipBaseline(leftHand, helperTransform, sourceTransform);
            }
        }

        private Transform GetCachedThumbBaseHelper(bool leftHand)
        {
            if (_cachedThumbBaseHelpers.TryGetValue(leftHand, out Transform helperTransform) && helperTransform != null)
            {
                return helperTransform;
            }

            if (TryFindThumbBaseHelperCandidate(leftHand, out helperTransform))
            {
                _cachedThumbBaseHelpers[leftHand] = helperTransform;
                return helperTransform;
            }

            return null;
        }

        private Transform GetCachedExplicitThumbBaseSource(bool leftHand)
        {
            if (_cachedThumbBaseExplicitSources.TryGetValue(leftHand, out Transform sourceTransform) && sourceTransform != null)
            {
                return sourceTransform;
            }

            if (TryFindExplicitThumbBaseSource(leftHand, out sourceTransform))
            {
                _cachedThumbBaseExplicitSources[leftHand] = sourceTransform;
                return sourceTransform;
            }

            return null;
        }

        private static float RiskAbove(float value, float warningThreshold, float fullRiskThreshold)
        {
            if (!IsFinite(value))
            {
                return float.NaN;
            }

            if (value <= warningThreshold)
            {
                return 0f;
            }

            if (fullRiskThreshold <= warningThreshold)
            {
                return 1f;
            }

            return Mathf.Clamp01((value - warningThreshold) / (fullRiskThreshold - warningThreshold));
        }

        private static float RiskOutsideRange(float value, float minValue, float maxValue, float fullRiskDistance)
        {
            if (!IsFinite(value))
            {
                return float.NaN;
            }

            if (value < minValue)
            {
                return RiskAbove(minValue - value, 0f, Mathf.Max(0.0001f, fullRiskDistance));
            }

            if (value > maxValue)
            {
                return RiskAbove(value - maxValue, 0f, Mathf.Max(0.0001f, fullRiskDistance));
            }

            return 0f;
        }

        private static float MaxFinite(params float[] values)
        {
            float max = float.NaN;
            if (values == null)
            {
                return max;
            }

            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                max = float.IsNaN(max) ? value : Mathf.Max(max, value);
            }

            return max;
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
            _editorBodyRotationReferenceLogged = false;
            _editorHandLocalRotationReferenceLogged = false;
            _editorHipsLocalPositionReferenceLogged = false;
            _hasEditorReferenceBodyPosition = false;
            _hasEditorReferenceHipsRestLocalPosition = false;
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

        private static bool ShouldUseEditorHumanoidMuscleReference(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm") ||
                   normalized.Contains("hand") ||
                   normalized.Contains("thumb") ||
                   normalized.Contains("index") ||
                   normalized.Contains("middle") ||
                   normalized.Contains("ring") ||
                   normalized.Contains("little");
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

        private void ApplyEditorHumanoidBodyRotationReference(ref HumanPose pose)
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

        private void CacheTargetReferenceBodyPosition()
        {
            _hasTargetReferenceBodyPosition = false;
            _hasPreviousBodyRootMotionPosition = false;

            if (_targetHandler == null)
            {
                return;
            }

            var targetPose = new HumanPose();
            _targetHandler.GetHumanPose(ref targetPose);
            if (!IsFinite(targetPose.bodyPosition))
            {
                return;
            }

            _targetReferenceBodyPosition = targetPose.bodyPosition;
            _hasTargetReferenceBodyPosition = true;
        }

        private Vector3 ExtractBodyPositionXZRootDelta(Vector3 bodyPosition)
        {
            if (!useBodyPositionXZRootMotion || !_hasTargetReferenceBodyPosition)
            {
                _hasPreviousBodyRootMotionPosition = false;
                return Vector3.zero;
            }

            Vector3 current = new Vector3(bodyPosition.x, 0f, bodyPosition.z);
            if (!IsFinite(current))
            {
                _hasPreviousBodyRootMotionPosition = false;
                return Vector3.zero;
            }

            if (!_hasPreviousBodyRootMotionPosition)
            {
                _previousBodyRootMotionPosition = current;
                _hasPreviousBodyRootMotionPosition = true;
                return Vector3.zero;
            }

            Vector3 delta = current - _previousBodyRootMotionPosition;
            _previousBodyRootMotionPosition = current;
            if (!IsFinite(delta))
            {
                return Vector3.zero;
            }

            delta.y = 0f;
            return delta;
        }

        private Vector3 ExtractEditorRootTranslationDelta(Vector3 ghostDelta)
        {
#if UNITY_EDITOR
            if (!useEditorHumanoidRootTranslationReference ||
                !_useEditorRootTranslationReference ||
                _editorRootTranslationX == null ||
                _editorRootTranslationZ == null)
            {
                ResetEditorHumanoidRootTranslationReferenceState();
                return Vector3.zero;
            }

            float time = GetLegacyAnimationTime();
            Vector3 current = SampleEditorRootTranslation(time);
            if (!IsFinite(current))
            {
                ResetEditorHumanoidRootTranslationReferenceState();
                return Vector3.zero;
            }

            if (!_hasPreviousEditorRootTranslation)
            {
                _previousEditorRootTranslation = current;
                _hasPreviousEditorRootTranslation = true;
                return Vector3.zero;
            }

            Vector3 delta = current - _previousEditorRootTranslation;
            _previousEditorRootTranslation = current;
            if (!IsFinite(delta))
            {
                return Vector3.zero;
            }

            if (FlattenXZ(ghostDelta).sqrMagnitude > 0.00000025f)
            {
                return Vector3.zero;
            }

            if (!_editorRootTranslationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid RootT translation reference applied at t={time:F3}s.");
                _editorRootTranslationReferenceLogged = true;
            }

            delta.y = 0f;
            delta *= Mathf.Clamp01(editorHumanoidRootTranslationWeight);
            if (!_hasSmoothedEditorRootTranslationDelta)
            {
                _smoothedEditorRootTranslationDelta = delta;
                _hasSmoothedEditorRootTranslationDelta = true;
                return delta;
            }

            float currentWeight = Mathf.Clamp(editorHumanoidRootTranslationCurrentWeight, 0.05f, 1f);
            _smoothedEditorRootTranslationDelta = Vector3.Lerp(_smoothedEditorRootTranslationDelta, delta, currentWeight);
            return _smoothedEditorRootTranslationDelta;
#else
            return Vector3.zero;
#endif
        }

        private void ResetEditorHumanoidRootTranslationReferenceState()
        {
#if UNITY_EDITOR
            _hasPreviousEditorRootTranslation = false;
            _hasSmoothedEditorRootTranslationDelta = false;
            _previousEditorRootTranslation = Vector3.zero;
            _smoothedEditorRootTranslationDelta = Vector3.zero;
#endif
        }

#if UNITY_EDITOR
        private Vector3 SampleEditorRootTranslation(float time)
        {
            // Unity Humanoid RootT uses the FBX avatar basis. In this project the manual
            // reference root path matches RootT with X/Z swapped in world space.
            return new Vector3(
                _editorRootTranslationZ.Evaluate(time),
                0f,
                _editorRootTranslationX.Evaluate(time));
        }
#endif

        private static Vector3 FlattenXZ(Vector3 value)
        {
            value.y = 0f;
            return value;
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

        public bool IsManualThumbLocalRotationReferenceActive
        {
            get
            {
#if UNITY_EDITOR
                return useManualAnimatorThumbLocalRotationReference &&
                    _useEditorFingerPoseReference &&
                    _editorFingerReferenceAnimator != null;
#else
                return false;
#endif
            }
        }

        public bool ShouldSuppressThumbPoseShapingGuard
        {
            get
            {
#if UNITY_EDITOR
                return ShouldSuppressThumbPoseShapingGuardForHand(true) ||
                    ShouldSuppressThumbPoseShapingGuardForHand(false);
#else
                return false;
#endif
            }
        }

        public bool ShouldSuppressLeftThumbPoseShapingGuard
        {
            get
            {
#if UNITY_EDITOR
                return ShouldSuppressThumbPoseShapingGuardForHand(true);
#else
                return false;
#endif
            }
        }

        public bool ShouldSuppressRightThumbPoseShapingGuard
        {
            get
            {
#if UNITY_EDITOR
                return ShouldSuppressThumbPoseShapingGuardForHand(false);
#else
                return false;
#endif
            }
        }

        public bool ShouldSuppressThumbPoseShapingGuardForHand(bool leftHand)
        {
#if UNITY_EDITOR
            if (!IsManualThumbLocalRotationReferenceActive ||
                !preserveManualFingerReferenceThumbMuscles)
            {
                return false;
            }

            if (ShouldSuppressCompetingManualThumbOverride(leftHand))
            {
                return false;
            }

            return !TryEvaluateThumbManualOverrideRisk(leftHand, out float risk) ||
                risk < ManualThumbPoseShapingSuppressMaxRisk;
#else
            return false;
#endif
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

            ResetThumbLocalRotationGuardDiagnostics();
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

                Quaternion limitedRotation = LimitThumbLocalRotation(initialRotation, currentRotation, limit);
                if (ShouldPreserveManualThumbLocalRotationReference(thumbBone, thumbTransform, currentRotation, limitedRotation))
                {
                    continue;
                }

                thumbTransform.localRotation = limitedRotation;
                RecordThumbLocalRotationGuardClamp(thumbBone);
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

        private void ResetThumbLocalRotationGuardDiagnostics()
        {
            _lastLeftThumbLocalRotationGuardClampCount = 0;
            _lastRightThumbLocalRotationGuardClampCount = 0;
            _lastLeftThumbLocalRotationGuardPreserveCount = 0;
            _lastRightThumbLocalRotationGuardPreserveCount = 0;
            _lastLeftThumbLocalRotationGuardCurrentRisk = float.NaN;
            _lastRightThumbLocalRotationGuardCurrentRisk = float.NaN;
            _lastLeftThumbLocalRotationGuardLimitedRisk = float.NaN;
            _lastRightThumbLocalRotationGuardLimitedRisk = float.NaN;
        }

        public void ResetThumbWorldRotationPreserveDiagnostics()
        {
            _lastLeftThumbWorldRotationSuppressCompetingOverride = false;
            _lastRightThumbWorldRotationSuppressCompetingOverride = false;
            _lastLeftThumbWorldRotationKeepDetachedHelperOverride = false;
            _lastRightThumbWorldRotationKeepDetachedHelperOverride = false;
            _lastLeftThumbWorldRotationCurrentReferenceFrameDeviation = float.NaN;
            _lastRightThumbWorldRotationCurrentReferenceFrameDeviation = float.NaN;
            _lastLeftThumbWorldRotationCandidateReferenceFrameDeviation = float.NaN;
            _lastRightThumbWorldRotationCandidateReferenceFrameDeviation = float.NaN;

            ResetThumbWorldRotationPreserveBoneDiagnostics(true, true);
            ResetThumbWorldRotationPreserveBoneDiagnostics(true, false);
            ResetThumbWorldRotationPreserveBoneDiagnostics(false, true);
            ResetThumbWorldRotationPreserveBoneDiagnostics(false, false);
        }

        private void ResetThumbWorldRotationPreserveBoneDiagnostics(bool leftHand, bool proximalBone)
        {
            RecordThumbWorldRotationReferenceAngles(
                leftHand,
                proximalBone,
                float.NaN,
                float.NaN);
            RecordThumbWorldRotationPreserveLocalRisk(
                leftHand,
                proximalBone,
                float.NaN,
                float.NaN);
            RecordThumbWorldRotationPreserveReason(
                leftHand,
                proximalBone,
                ThumbWorldRotationPreserveReason.None);
        }

        private static bool IsThumbProximalBone(HumanBodyBones thumbBone)
        {
            return thumbBone == HumanBodyBones.LeftThumbProximal ||
                thumbBone == HumanBodyBones.RightThumbProximal;
        }

        private static bool IsThumbIntermediateBone(HumanBodyBones thumbBone)
        {
            return thumbBone == HumanBodyBones.LeftThumbIntermediate ||
                thumbBone == HumanBodyBones.RightThumbIntermediate;
        }

        private static bool TryGetThumbWorldRotationDiagnosticBone(HumanBodyBones thumbBone, out bool proximalBone)
        {
            if (IsThumbProximalBone(thumbBone))
            {
                proximalBone = true;
                return true;
            }

            if (IsThumbIntermediateBone(thumbBone))
            {
                proximalBone = false;
                return true;
            }

            proximalBone = false;
            return false;
        }

        private void RecordThumbWorldRotationOverrideContext(
            bool leftHand,
            bool suppressCompetingManualOverride,
            bool keepDetachedHelperOverride,
            float currentReferenceFrameDeviation,
            float candidateReferenceFrameDeviation)
        {
            if (leftHand)
            {
                _lastLeftThumbWorldRotationSuppressCompetingOverride = suppressCompetingManualOverride;
                _lastLeftThumbWorldRotationKeepDetachedHelperOverride = keepDetachedHelperOverride;
                _lastLeftThumbWorldRotationCurrentReferenceFrameDeviation = currentReferenceFrameDeviation;
                _lastLeftThumbWorldRotationCandidateReferenceFrameDeviation = candidateReferenceFrameDeviation;
            }
            else
            {
                _lastRightThumbWorldRotationSuppressCompetingOverride = suppressCompetingManualOverride;
                _lastRightThumbWorldRotationKeepDetachedHelperOverride = keepDetachedHelperOverride;
                _lastRightThumbWorldRotationCurrentReferenceFrameDeviation = currentReferenceFrameDeviation;
                _lastRightThumbWorldRotationCandidateReferenceFrameDeviation = candidateReferenceFrameDeviation;
            }
        }

        private void RecordThumbWorldRotationReferenceAngles(
            bool leftHand,
            bool proximalBone,
            float currentReferenceAngle,
            float candidateReferenceAngle)
        {
            if (leftHand)
            {
                if (proximalBone)
                {
                    _lastLeftThumbProximalWorldRotationCurrentReferenceAngle = currentReferenceAngle;
                    _lastLeftThumbProximalWorldRotationCandidateReferenceAngle = candidateReferenceAngle;
                }
                else
                {
                    _lastLeftThumbIntermediateWorldRotationCurrentReferenceAngle = currentReferenceAngle;
                    _lastLeftThumbIntermediateWorldRotationCandidateReferenceAngle = candidateReferenceAngle;
                }
            }
            else
            {
                if (proximalBone)
                {
                    _lastRightThumbProximalWorldRotationCurrentReferenceAngle = currentReferenceAngle;
                    _lastRightThumbProximalWorldRotationCandidateReferenceAngle = candidateReferenceAngle;
                }
                else
                {
                    _lastRightThumbIntermediateWorldRotationCurrentReferenceAngle = currentReferenceAngle;
                    _lastRightThumbIntermediateWorldRotationCandidateReferenceAngle = candidateReferenceAngle;
                }
            }
        }

        private void RecordThumbWorldRotationPreserveLocalRisk(
            bool leftHand,
            bool proximalBone,
            float currentRisk,
            float limitedRisk)
        {
            if (leftHand)
            {
                if (proximalBone)
                {
                    _lastLeftThumbProximalWorldRotationPreserveCurrentRisk = currentRisk;
                    _lastLeftThumbProximalWorldRotationPreserveLimitedRisk = limitedRisk;
                }
                else
                {
                    _lastLeftThumbIntermediateWorldRotationPreserveCurrentRisk = currentRisk;
                    _lastLeftThumbIntermediateWorldRotationPreserveLimitedRisk = limitedRisk;
                }
            }
            else
            {
                if (proximalBone)
                {
                    _lastRightThumbProximalWorldRotationPreserveCurrentRisk = currentRisk;
                    _lastRightThumbProximalWorldRotationPreserveLimitedRisk = limitedRisk;
                }
                else
                {
                    _lastRightThumbIntermediateWorldRotationPreserveCurrentRisk = currentRisk;
                    _lastRightThumbIntermediateWorldRotationPreserveLimitedRisk = limitedRisk;
                }
            }
        }

        private void RecordThumbWorldRotationPreserveReason(
            bool leftHand,
            bool proximalBone,
            ThumbWorldRotationPreserveReason reason)
        {
            if (leftHand)
            {
                if (proximalBone)
                {
                    _lastLeftThumbProximalWorldRotationPreserveReason = reason;
                }
                else
                {
                    _lastLeftThumbIntermediateWorldRotationPreserveReason = reason;
                }
            }
            else
            {
                if (proximalBone)
                {
                    _lastRightThumbProximalWorldRotationPreserveReason = reason;
                }
                else
                {
                    _lastRightThumbIntermediateWorldRotationPreserveReason = reason;
                }
            }
        }

        private void RecordThumbLocalRotationGuardClamp(HumanBodyBones thumbBone)
        {
            if (!TryGetThumbBoneSide(thumbBone, out bool leftHand))
            {
                return;
            }

            if (leftHand)
            {
                _lastLeftThumbLocalRotationGuardClampCount++;
            }
            else
            {
                _lastRightThumbLocalRotationGuardClampCount++;
            }
        }

        private void RecordThumbLocalRotationGuardRisk(bool leftHand, float currentRisk, float limitedRisk, bool preserved)
        {
            if (leftHand)
            {
                _lastLeftThumbLocalRotationGuardCurrentRisk = currentRisk;
                _lastLeftThumbLocalRotationGuardLimitedRisk = limitedRisk;
                if (preserved)
                {
                    _lastLeftThumbLocalRotationGuardPreserveCount++;
                }
            }
            else
            {
                _lastRightThumbLocalRotationGuardCurrentRisk = currentRisk;
                _lastRightThumbLocalRotationGuardLimitedRisk = limitedRisk;
                if (preserved)
                {
                    _lastRightThumbLocalRotationGuardPreserveCount++;
                }
            }
        }

        private bool ShouldPreserveManualThumbLocalRotationReference(
            HumanBodyBones thumbBone,
            Transform targetThumb,
            Quaternion currentRotation,
            Quaternion limitedRotation)
        {
            if (!_useEditorFingerPoseReference ||
                !useManualAnimatorThumbLocalRotationReference ||
                _editorFingerReferenceAnimator == null ||
                targetThumb == null ||
                !TryGetThumbBoneSide(thumbBone, out bool leftHand))
            {
                return false;
            }

            if (!TryEvaluateThumbLocalRotationOverrideRisk(leftHand, targetThumb, currentRotation, out float currentRisk) ||
                !TryEvaluateThumbLocalRotationOverrideRisk(leftHand, targetThumb, limitedRotation, out float limitedRisk))
            {
                return false;
            }

            if (!IsFinite(currentRisk) || !IsFinite(limitedRisk))
            {
                return false;
            }

            RecordThumbLocalRotationGuardRisk(leftHand, currentRisk, limitedRisk, false);

            if (Mathf.Max(currentRisk, limitedRisk) < ManualThumbOverrideSuppressRiskThreshold)
            {
                return false;
            }

            bool preserved = limitedRisk > currentRisk + ManualThumbOverrideRiskIncreaseTolerance;
            if (preserved)
            {
                RecordThumbLocalRotationGuardRisk(leftHand, currentRisk, limitedRisk, true);
            }

            return preserved;
        }

        public bool ShouldPreserveManualThumbWorldRotationCorrection(
            HumanBodyBones thumbBone,
            Transform targetThumb,
            Quaternion candidateWorldRotation)
        {
            if (targetThumb == null || !IsFinite(candidateWorldRotation))
            {
                return false;
            }

            Quaternion candidateLocalRotation = targetThumb.parent != null
                ? Quaternion.Inverse(targetThumb.parent.rotation) * candidateWorldRotation
                : candidateWorldRotation;

            if (!IsFinite(candidateLocalRotation))
            {
                return false;
            }

            if (!TryGetThumbBoneSide(thumbBone, out bool leftHand))
            {
                return ShouldPreserveManualThumbLocalRotationReference(
                    thumbBone,
                    targetThumb,
                    targetThumb.localRotation,
                    candidateLocalRotation);
            }

            if (!TryGetThumbWorldRotationDiagnosticBone(thumbBone, out bool proximalBone))
            {
                return ShouldPreserveManualThumbLocalRotationReference(
                    thumbBone,
                    targetThumb,
                    targetThumb.localRotation,
                    candidateLocalRotation);
            }

            ResetThumbWorldRotationPreserveBoneDiagnostics(leftHand, proximalBone);

            bool suppressCompetingManualOverride = ShouldSuppressCompetingManualThumbOverride(leftHand);
            bool keepDetachedHelperOverride = ShouldKeepDetachedHelperManualThumbOverrides(leftHand);
            bool allowManualReferenceWorldPreserve = !suppressCompetingManualOverride || keepDetachedHelperOverride;
            bool allowDetachedHelperShortcutPreserve = allowManualReferenceWorldPreserve && !keepDetachedHelperOverride;
            bool hasDetachedHelperRelationship = HasDetachedThumbBaseHelperRelationship(thumbBone, leftHand);
            bool hasReferenceLocalRotation = TryGetManualReferenceThumbLocalRotation(thumbBone, out Quaternion referenceLocalRotation);
            float currentReferenceAngle = float.NaN;
            float candidateReferenceAngle = float.NaN;
            if (hasReferenceLocalRotation)
            {
                currentReferenceAngle = Quaternion.Angle(referenceLocalRotation, targetThumb.localRotation);
                candidateReferenceAngle = Quaternion.Angle(referenceLocalRotation, candidateLocalRotation);
            }

            RecordThumbWorldRotationReferenceAngles(
                leftHand,
                proximalBone,
                currentReferenceAngle,
                candidateReferenceAngle);

            float currentReferenceFrameDeviation = float.NaN;
            float candidateReferenceFrameDeviation = float.NaN;
            bool hasReferenceFrameDeviation =
                hasDetachedHelperRelationship &&
                TryEvaluateThumbManualReferenceFrameDeviation(
                    leftHand,
                    targetThumb,
                    candidateWorldRotation,
                    out currentReferenceFrameDeviation,
                    out candidateReferenceFrameDeviation);
            RecordThumbWorldRotationOverrideContext(
                leftHand,
                suppressCompetingManualOverride,
                keepDetachedHelperOverride,
                currentReferenceFrameDeviation,
                candidateReferenceFrameDeviation);

            if (allowDetachedHelperShortcutPreserve &&
                hasDetachedHelperRelationship &&
                hasReferenceLocalRotation)
            {
                if (currentReferenceAngle <= ManualThumbDetachedHelperPreserveCurrentReferenceAngleMax &&
                    candidateReferenceAngle > currentReferenceAngle + ManualThumbWorldRotationReferenceToleranceDegrees)
                {
                    RecordThumbWorldRotationPreserveReason(
                        leftHand,
                        proximalBone,
                        ThumbWorldRotationPreserveReason.DetachedHelperReferenceAngle);
                    return true;
                }
            }

            if (allowDetachedHelperShortcutPreserve &&
                hasReferenceFrameDeviation &&
                candidateReferenceFrameDeviation > currentReferenceFrameDeviation + 0.001f)
            {
                RecordThumbWorldRotationPreserveReason(
                    leftHand,
                    proximalBone,
                    ThumbWorldRotationPreserveReason.DetachedHelperReferenceFrameDeviation);
                return true;
            }

            if (!suppressCompetingManualOverride)
            {
                bool preserved = ShouldPreserveManualThumbLocalRotationReference(
                    thumbBone,
                    targetThumb,
                    targetThumb.localRotation,
                    candidateLocalRotation);
                if (preserved)
                {
                    RecordThumbWorldRotationPreserveLocalRisk(
                        leftHand,
                        proximalBone,
                        leftHand ? _lastLeftThumbLocalRotationGuardCurrentRisk : _lastRightThumbLocalRotationGuardCurrentRisk,
                        leftHand ? _lastLeftThumbLocalRotationGuardLimitedRisk : _lastRightThumbLocalRotationGuardLimitedRisk);
                    RecordThumbWorldRotationPreserveReason(
                        leftHand,
                        proximalBone,
                        ThumbWorldRotationPreserveReason.LocalRotationReference);
                }

                return preserved;
            }

            if (!hasReferenceLocalRotation)
            {
                bool preserved = ShouldPreserveManualThumbLocalRotationReference(
                    thumbBone,
                    targetThumb,
                    targetThumb.localRotation,
                    candidateLocalRotation);
                if (preserved)
                {
                    RecordThumbWorldRotationPreserveLocalRisk(
                        leftHand,
                        proximalBone,
                        leftHand ? _lastLeftThumbLocalRotationGuardCurrentRisk : _lastRightThumbLocalRotationGuardCurrentRisk,
                        leftHand ? _lastLeftThumbLocalRotationGuardLimitedRisk : _lastRightThumbLocalRotationGuardLimitedRisk);
                    RecordThumbWorldRotationPreserveReason(
                        leftHand,
                        proximalBone,
                        ThumbWorldRotationPreserveReason.LocalRotationReferenceFallbackNoManualReference);
                }

                return preserved;
            }

            if (allowManualReferenceWorldPreserve &&
                candidateReferenceAngle > currentReferenceAngle + ManualThumbWorldRotationReferenceToleranceDegrees)
            {
                RecordThumbWorldRotationPreserveReason(
                    leftHand,
                    proximalBone,
                    ThumbWorldRotationPreserveReason.SuppressedManualReferenceAngle);
                return true;
            }

            bool preservedAfterSuppressedReference = ShouldPreserveManualThumbLocalRotationReference(
                thumbBone,
                targetThumb,
                targetThumb.localRotation,
                candidateLocalRotation);
            if (preservedAfterSuppressedReference)
            {
                RecordThumbWorldRotationPreserveLocalRisk(
                    leftHand,
                    proximalBone,
                    leftHand ? _lastLeftThumbLocalRotationGuardCurrentRisk : _lastRightThumbLocalRotationGuardCurrentRisk,
                    leftHand ? _lastLeftThumbLocalRotationGuardLimitedRisk : _lastRightThumbLocalRotationGuardLimitedRisk);
                RecordThumbWorldRotationPreserveReason(
                    leftHand,
                    proximalBone,
                    ThumbWorldRotationPreserveReason.LocalRotationReferenceAfterSuppressedReference);
            }

            return preservedAfterSuppressedReference;
        }

        private bool TryGetManualReferenceThumbLocalRotation(HumanBodyBones thumbBone, out Quaternion localRotation)
        {
            localRotation = Quaternion.identity;
            if (!_useEditorFingerPoseReference ||
                !useManualAnimatorThumbLocalRotationReference ||
                _editorFingerReferenceAnimator == null)
            {
                return false;
            }

            Transform referenceThumb = _editorFingerReferenceAnimator.GetBoneTransform(thumbBone);
            if (referenceThumb == null || !IsFinite(referenceThumb.localRotation))
            {
                return false;
            }

            localRotation = referenceThumb.localRotation;
            return true;
        }

        private bool HasDetachedThumbBaseHelperRelationship(HumanBodyBones thumbBone, bool leftHand)
        {
            if (thumbBone != HumanBodyBones.LeftThumbProximal &&
                thumbBone != HumanBodyBones.RightThumbProximal)
            {
                return false;
            }

            return GetCachedThumbBaseHelper(leftHand) != null &&
                GetCachedExplicitThumbBaseSource(leftHand) != null;
        }

        private bool TryEvaluateThumbManualReferenceFrameDeviation(
            bool leftHand,
            Transform targetThumb,
            Quaternion candidateWorldRotation,
            out float currentDeviation,
            out float candidateDeviation)
        {
            currentDeviation = float.NaN;
            candidateDeviation = float.NaN;

            if (_editorFingerReferenceAnimator == null ||
                targetAnimator == null ||
                targetThumb == null ||
                !IsFinite(candidateWorldRotation) ||
                !TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return false;
            }

            if (!TryEvaluateThumbReferenceFrameMetrics(_editorFingerReferenceAnimator, leftHand, referenceFrame, out float referenceSpread, out float referenceProjection) ||
                !TryEvaluateThumbReferenceFrameMetrics(targetAnimator, leftHand, targetFrame, out float currentSpread, out float currentProjection))
            {
                return false;
            }

            currentDeviation = EvaluateThumbReferenceFrameDeviation(
                currentSpread,
                currentProjection,
                referenceSpread,
                referenceProjection);

            Quaternion originalWorldRotation = targetThumb.rotation;
            targetThumb.rotation = candidateWorldRotation;
            try
            {
                if (!TryEvaluateThumbReferenceFrameMetrics(targetAnimator, leftHand, targetFrame, out float candidateSpread, out float candidateProjection))
                {
                    return false;
                }

                candidateDeviation = EvaluateThumbReferenceFrameDeviation(
                    candidateSpread,
                    candidateProjection,
                    referenceSpread,
                    referenceProjection);
            }
            finally
            {
                targetThumb.rotation = originalWorldRotation;
            }

            return IsFinite(currentDeviation) && IsFinite(candidateDeviation);
        }

        private static bool TryEvaluateThumbReferenceFrameMetrics(
            Animator animator,
            bool leftHand,
            ThumbPalmFrame frame,
            out float spreadAngle,
            out float projection)
        {
            spreadAngle = float.NaN;
            projection = float.NaN;

            if (animator == null)
            {
                return false;
            }

            Transform hand = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform index = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform proximal = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            Transform intermediate = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
            if (hand == null || index == null || proximal == null || intermediate == null)
            {
                return false;
            }

            Vector3 thumbDirection = intermediate.position - proximal.position;
            Vector3 indexDirection = index.position - hand.position;
            if (!TryNormalize(thumbDirection, out thumbDirection) ||
                !TryNormalize(indexDirection, out indexDirection))
            {
                return false;
            }

            spreadAngle = Vector3.Angle(thumbDirection, indexDirection);
            projection = Vector3.Dot(thumbDirection, frame.Normal);
            return IsFinite(spreadAngle) && IsFinite(projection);
        }

        private static float EvaluateThumbReferenceFrameDeviation(
            float spreadAngle,
            float projection,
            float referenceSpreadAngle,
            float referenceProjection)
        {
            float spreadDeviation = Mathf.Max(
                0f,
                Mathf.Abs(spreadAngle - referenceSpreadAngle) - ManualThumbReferenceSpreadDeviationToleranceDegrees);
            float projectionDeviation = Mathf.Max(
                0f,
                Mathf.Abs(projection - referenceProjection) - ManualThumbReferenceProjectionDeviationTolerance);
            return spreadDeviation + projectionDeviation * 100f;
        }

        private bool TryEvaluateCurrentThumbReferenceFrameDelta(
            bool leftHand,
            out float spreadDelta,
            out float projectionDelta)
        {
            spreadDelta = float.NaN;
            projectionDelta = float.NaN;

            if (_editorFingerReferenceAnimator == null ||
                targetAnimator == null ||
                !TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame) ||
                !TryEvaluateThumbReferenceFrameMetrics(_editorFingerReferenceAnimator, leftHand, referenceFrame, out float referenceSpread, out float referenceProjection) ||
                !TryEvaluateThumbReferenceFrameMetrics(targetAnimator, leftHand, targetFrame, out float currentSpread, out float currentProjection))
            {
                return false;
            }

            spreadDelta = Mathf.Abs(currentSpread - referenceSpread);
            projectionDelta = Mathf.Abs(currentProjection - referenceProjection);
            return IsFinite(spreadDelta) && IsFinite(projectionDelta);
        }

        private bool TryEvaluateThumbLocalRotationOverrideRisk(
            bool leftHand,
            Transform targetThumb,
            Quaternion candidateRotation,
            out float risk)
        {
            risk = float.NaN;
            if (targetThumb == null || !IsFinite(candidateRotation))
            {
                return false;
            }

            Quaternion originalRotation = targetThumb.localRotation;
            targetThumb.localRotation = candidateRotation;
            try
            {
                return TryEvaluateThumbManualOverrideRisk(leftHand, out risk);
            }
            finally
            {
                targetThumb.localRotation = originalRotation;
            }
        }

        private static bool TryGetThumbBoneSide(HumanBodyBones thumbBone, out bool leftHand)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                case HumanBodyBones.LeftThumbIntermediate:
                case HumanBodyBones.LeftThumbDistal:
                    leftHand = true;
                    return true;
                case HumanBodyBones.RightThumbProximal:
                case HumanBodyBones.RightThumbIntermediate:
                case HumanBodyBones.RightThumbDistal:
                    leftHand = false;
                    return true;
                default:
                    leftHand = false;
                    return false;
            }
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
            if (!IsFinite(poseDelta))
            {
                _lastRootPositionPoseDeltaMagnitude = float.NaN;
                return;
            }

            _lastRootPositionPoseDeltaMagnitude = poseDelta.magnitude;
            _maxRootPositionPoseDeltaMagnitude = Mathf.Max(_maxRootPositionPoseDeltaMagnitude, _lastRootPositionPoseDeltaMagnitude);
            if (_lastRootPositionPoseDeltaMagnitude <= maxRootDeltaPerFrame)
            {
                return;
            }

            _rootPositionSpikeClampedCount++;
            if (logRootDeltaSpikes && !_rootDeltaSpikeWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] {source} root position spike {_lastRootPositionPoseDeltaMagnitude:F3}m clamped. limit={maxRootDeltaPerFrame:F3}m");
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
            _cachedThumbBaseHelpers.Clear();
            _cachedThumbBaseExplicitSources.Clear();
            _initialThumbBaseHelperSourceDistances.Clear();
            _initialThumbBaseHelperSourceRelativeRotations.Clear();
            _scaleWarningLogged = false;
            _positionWarningLogged = false;
            _muscleClampWarningLogged = false;
            _anatomyGuardWarningLogged = false;
            _thumbGuardWarningLogged = false;
            _thumbLocalRotationGuardWarningLogged = false;
            _rootDeltaSpikeWarningLogged = false;
            _hasPreviousBodyRootMotionPosition = false;
            _leftFootLocked = false;
            _rightFootLocked = false;
            _lastRootDeltaMagnitude = float.NaN;
            _maxRootDeltaMagnitude = 0f;
            _rootDeltaSpikeSkippedCount = 0;
            _lastRootPositionPoseDeltaMagnitude = float.NaN;
            _maxRootPositionPoseDeltaMagnitude = 0f;
            _rootPositionSpikeClampedCount = 0;
            _groundingInitialized = false;
            _hasFrozenGroundingRootY = false;
            _frozenGroundingRootY = 0f;
            _hasPreviousLegacyAnimationTime = false;
            _previousLegacyAnimationTime = 0f;
            _lastLegacyAnimationStep = float.NaN;
            _maxLegacyAnimationStep = 0f;
            _legacyAnimationStepSpikeCount = 0;
            _legacyAnimationStepSpikeThisFrame = false;
            _hasPreviousVisualPose = false;
            _poseVisualSmoothingCount = 0;
            _poseVisualMuscleDeltaOnlySkippedCount = 0;
            _lastPoseVisualMaxMuscleDelta = float.NaN;
            _maxPoseVisualMaxMuscleDelta = 0f;
            _lastGroundingAdjustment = float.NaN;
            _maxGroundingAdjustment = 0f;
            _groundingStepClampedCount = 0;
            _groundingSmoothedCount = 0;
            _lastGroundingVerticalStep = float.NaN;
            _maxGroundingVerticalStep = 0f;
            _initialGroundingVerticalStep = float.NaN;
            _maxGroundingVerticalStepAfterInitial = 0f;
            _lastGroundingTargetY = float.NaN;
            _lastGroundingLowestFootBottomY = float.NaN;
            _lateVisualGroundingWarningLogged = false;
            _rendererGroundingOutlierWarningLogged = false;
            _lateVisualGroundingInitialized = false;
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

            CaptureThumbBaseHelperState(true);
            CaptureThumbBaseHelperState(false);
        }

        private void CaptureThumbBaseHelperState(bool leftHand)
        {
            if (!TryFindThumbBaseHelperCandidate(leftHand, out Transform helperTransform))
            {
                return;
            }

            _cachedThumbBaseHelpers[leftHand] = helperTransform;
            if (!_targetInitialThumbBaseHelperLocalPositions.ContainsKey(helperTransform))
            {
                _targetInitialThumbBaseHelperLocalPositions[helperTransform] = helperTransform.localPosition;
            }

            if (!TryFindExplicitThumbBaseSource(leftHand, out Transform sourceTransform))
            {
                return;
            }

            _cachedThumbBaseExplicitSources[leftHand] = sourceTransform;
            CaptureThumbBaseHelperRelationshipBaseline(leftHand, helperTransform, sourceTransform);
        }

        private void CaptureThumbBaseHelperRelationshipBaseline(bool leftHand, Transform helperTransform, Transform sourceTransform)
        {
            if (helperTransform == null || sourceTransform == null)
            {
                return;
            }

            float initialDistance = Vector3.Distance(helperTransform.position, sourceTransform.position);
            if (IsFinite(initialDistance))
            {
                _initialThumbBaseHelperSourceDistances[leftHand] = initialDistance;
            }

            Quaternion initialRelativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
            if (IsFinite(initialRelativeRotation))
            {
                _initialThumbBaseHelperSourceRelativeRotations[leftHand] = initialRelativeRotation;
            }
        }

        private static string FormatDebugFloat(float value)
        {
            return IsFinite(value) ? value.ToString("F4") : "NaN";
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "<none>";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(transform.name);
            Transform current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
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

        private Transform FindTargetTransform(Func<Transform, bool> predicate)
        {
            if (targetAnimator == null || targetAnimator.gameObject == null || predicate == null)
            {
                return null;
            }

            foreach (Transform candidate in targetAnimator.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != null && predicate(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool TryFindThumbBaseHelperCandidate(bool leftHand, out Transform helper)
        {
            helper = FindThumbBaseHelperByName(leftHand);
            if (helper != null)
            {
                return true;
            }

            Transform hand = targetAnimator != null
                ? targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand)
                : null;
            if (hand == null)
            {
                return false;
            }

            Transform thumbProximal = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            Transform thumbIntermediate = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
            Transform thumbDistal = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbDistal : HumanBodyBones.RightThumbDistal);
            Transform explicitSource = null;
            TryFindExplicitThumbBaseSource(leftHand, out explicitSource);

            float bestDistance = float.PositiveInfinity;
            foreach (Transform candidate in hand.GetComponentsInChildren<Transform>(true))
            {
                if (!IsAmbiguousThumbExtraTransformCandidate(candidate, hand, thumbProximal, thumbIntermediate, thumbDistal))
                {
                    continue;
                }

                float distance = explicitSource != null
                    ? (candidate.position - explicitSource.position).sqrMagnitude
                    : thumbProximal != null
                        ? (candidate.position - thumbProximal.position).sqrMagnitude
                        : (candidate.position - hand.position).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                helper = candidate;
            }

            return helper != null;
        }

        private Transform FindThumbBaseHelperByName(bool leftHand)
        {
            string sideToken = leftHand ? "left" : "right";
            return FindTargetTransform(candidate =>
            {
                string normalizedName = NormalizeTransformName(candidate.name);
                return normalizedName.Contains(sideToken) && IsDetachedThumbBaseHelperName(normalizedName);
            });
        }

        private bool TryFindExplicitThumbBaseSource(bool leftHand, out Transform source)
        {
            string sideToken = leftHand ? "left" : "right";
            source = FindTargetTransform(candidate =>
            {
                string normalizedName = NormalizeTransformName(candidate.name);
                return normalizedName.Contains(sideToken) && IsActiveThumbBaseSourceName(normalizedName);
            });
            if (source != null)
            {
                return true;
            }

            Transform hand = targetAnimator != null
                ? targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand)
                : null;
            Transform thumbProximal = targetAnimator != null
                ? targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal)
                : null;
            if (hand == null)
            {
                return false;
            }

            float bestDistance = float.PositiveInfinity;
            foreach (Transform candidate in targetAnimator.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == null)
                {
                    continue;
                }

                string normalizedName = NormalizeTransformName(candidate.name);
                if (!IsActiveThumbBaseSourceName(normalizedName))
                {
                    continue;
                }

                float distance = thumbProximal != null
                    ? (candidate.position - thumbProximal.position).sqrMagnitude
                    : (candidate.position - hand.position).sqrMagnitude;
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                source = candidate;
            }

            return source != null;
        }

        private static bool IsAmbiguousThumbExtraTransformCandidate(
            Transform candidate,
            Transform hand,
            Transform thumbProximal,
            Transform thumbIntermediate,
            Transform thumbDistal)
        {
            if (candidate == null || candidate == hand || candidate == thumbProximal || candidate == thumbIntermediate || candidate == thumbDistal)
            {
                return false;
            }

            string normalizedName = NormalizeTransformName(candidate.name);
            if (string.IsNullOrEmpty(normalizedName) ||
                !normalizedName.Contains("thumb") ||
                normalizedName.Contains("ghost") ||
                IsActiveThumbBaseSourceName(normalizedName))
            {
                return false;
            }

            if (normalizedName.Contains("thumb1") ||
                normalizedName.Contains("thumb2") ||
                normalizedName.Contains("thumb3") ||
                normalizedName.Contains("proximal") ||
                normalizedName.Contains("intermediate") ||
                normalizedName.Contains("distal") ||
                normalizedName.Contains("thumbtip"))
            {
                return false;
            }

            if (IsAncestorWithinHand(candidate, thumbProximal, hand) ||
                IsAncestorWithinHand(candidate, thumbIntermediate, hand) ||
                IsAncestorWithinHand(candidate, thumbDistal, hand) ||
                IsAncestorWithinHand(thumbProximal, candidate, hand) ||
                IsAncestorWithinHand(thumbIntermediate, candidate, hand) ||
                IsAncestorWithinHand(thumbDistal, candidate, hand))
            {
                return false;
            }

            return true;
        }

        private static bool IsAncestorWithinHand(Transform ancestor, Transform descendant, Transform hand)
        {
            if (ancestor == null || descendant == null || hand == null || ancestor == descendant)
            {
                return false;
            }

            Transform current = descendant.parent;
            while (current != null)
            {
                if (current == ancestor)
                {
                    return true;
                }

                if (current == hand)
                {
                    break;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsDetachedThumbBaseHelperName(string normalizedName)
        {
            if (string.IsNullOrEmpty(normalizedName) ||
                normalizedName.Contains("!") ||
                normalizedName.Contains("ghost") ||
                normalizedName.Contains("thumb0m"))
            {
                return false;
            }

            return IsThumbBaseName(normalizedName);
        }

        private static bool IsActiveThumbBaseSourceName(string normalizedName)
        {
            return !string.IsNullOrEmpty(normalizedName) &&
                normalizedName.Contains("thumb0m") &&
                !normalizedName.Contains("ghost") &&
                !normalizedName.Contains("thumb1") &&
                !normalizedName.Contains("thumb2") &&
                !normalizedName.Contains("thumbtip");
        }

        private static bool IsThumbBaseName(string normalizedName)
        {
            return !string.IsNullOrEmpty(normalizedName) &&
                normalizedName.Contains("thumb0") &&
                !normalizedName.Contains("thumb1") &&
                !normalizedName.Contains("thumb2") &&
                !normalizedName.Contains("thumbtip");
        }

        private static string NormalizeTransformName(string value)
        {
            return string.IsNullOrEmpty(value) ? "" : value.ToLowerInvariant();
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

        private void EnsureLateVisualGroundingCorrection()
        {
            if (!enableLateVisualGroundingCorrection)
            {
                return;
            }

            PoseSpaceLateVisualGroundingCorrection correction = GetComponent<PoseSpaceLateVisualGroundingCorrection>();
            if (correction == null)
            {
                correction = gameObject.AddComponent<PoseSpaceLateVisualGroundingCorrection>();
            }

            correction.Initialize(this);
        }

        void ApplyRaycastGrounding()
        {
            // 양발 위치 확보 (발목)
            Transform lFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);

            if (lFoot == null || rFoot == null) return;

            // 발바닥 위치 (발목 - 반지름)
            if (!_hasEstimatedFootRadius)
            {
                CalibrateTargetFootRadius();
            }

            float footRadius = GetEstimatedFootRadius();
            float lBottom = lFoot.position.y - footRadius;
            float rBottom = rFoot.position.y - footRadius;
            if (!IsFinite(lBottom) || !IsFinite(rBottom))
            {
                LogPoseWarning("Foot position became non-finite. Skipping grounding for this frame.");
                return;
            }

            // 현재 가장 낮은 발바닥 높이. Renderer bounds가 발에서 너무 멀면
            // 옷/머리카락/소매 outlier로 보고 발 기준을 유지한다.
            float lowestFootCurrentY = Mathf.Min(lBottom, rBottom);
            float contactBottomY = _hasEstimatedFootRadius
                ? lowestFootCurrentY
                : ResolveGroundingContactBottomY(lowestFootCurrentY);

            // 목표는 지면(0) + Offset
            // Raycast를 사용하여 실제 지면을 찾을 수도 있으나, 현재는 평면(Plane) 위라고 가정하고 0.0f 사용
            // 만약 계단이나 경사면이라면 Physics.Raycast로 hit.point.y를 구해야 함.
            float targetGroundY = 0.0f; // 평면 가정
            float targetHeight = targetGroundY + groundOffset;
            _lastGroundingTargetY = targetGroundY;
            _lastGroundingLowestFootBottomY = contactBottomY;

            // 보정값 계산 (목표 - 현재)
            // 양수면 들어 올리고, 음수면 내림 (양방향)
            float adjustment = targetHeight - contactBottomY;
            if (!IsFinite(adjustment))
            {
                LogPoseWarning("Grounding adjustment became non-finite. Skipping grounding for this frame.");
                _lastGroundingAdjustment = float.NaN;
                return;
            }

            _lastGroundingAdjustment = adjustment;
            _maxGroundingAdjustment = Mathf.Max(_maxGroundingAdjustment, Mathf.Abs(adjustment));

            Vector3 currentPos = targetAnimator.transform.position;
            if (!IsFinite(currentPos))
            {
                LogPoseWarning("Target position became non-finite before grounding. Resetting to origin.");
                currentPos = Vector3.zero;
            }

            if (freezeRootYAfterInitialGrounding && _groundingInitialized && _hasFrozenGroundingRootY)
            {
                currentPos.y = _frozenGroundingRootY;
                if (IsFinite(currentPos))
                {
                    targetAnimator.transform.position = currentPos;
                    ApplyGroundedFootLockXZ(lFoot, rFoot, targetHeight, footRadius);
                }

                _lastGroundingVerticalStep = 0f;
                return;
            }

            float deadZone = Mathf.Max(0f, groundingDeadZone);
            if (_groundingInitialized && Mathf.Abs(adjustment) <= deadZone)
            {
                _lastGroundingVerticalStep = 0f;
                return;
            }

            bool wasGroundingInitialized = _groundingInitialized;
            float effectiveAdjustment = adjustment;
            if (_groundingInitialized && deadZone > 0f)
            {
                // Dead zone should not only skip tiny errors. Subtracting it from
                // larger corrections prevents the root from chasing small foot noise.
                effectiveAdjustment = Mathf.Sign(adjustment) * Mathf.Max(0f, Mathf.Abs(adjustment) - deadZone);
            }

            float desiredY = currentPos.y + effectiveAdjustment;
            float nextY = desiredY;
            if (!_groundingInitialized)
            {
                _groundingInitialized = true;
            }
            else if (smoothGrounding)
            {
                float smoothing = Mathf.Clamp01(groundingSmoothing);
                if (smoothing < 1f)
                {
                    nextY = Mathf.Lerp(currentPos.y, desiredY, smoothing);
                    _groundingSmoothedCount++;
                }

                float maxStep = Mathf.Max(0.001f, maxGroundingVerticalStepPerFrame);
                float verticalStep = nextY - currentPos.y;
                if (IsGroundingDirectionReversal(verticalStep))
                {
                    maxStep = Mathf.Max(0.001f, maxStep * GroundingDirectionReversalStepScale);
                }

                if (Mathf.Abs(verticalStep) > maxStep)
                {
                    nextY = currentPos.y + Mathf.Sign(verticalStep) * maxStep;
                    _groundingStepClampedCount++;
                }
            }

            float clampedNextY = nextY;
            float appliedVerticalStep = clampedNextY - currentPos.y;
            _lastGroundingVerticalStep = appliedVerticalStep;
            _maxGroundingVerticalStep = Mathf.Max(_maxGroundingVerticalStep, Mathf.Abs(appliedVerticalStep));
            if (wasGroundingInitialized)
            {
                _maxGroundingVerticalStepAfterInitial = Mathf.Max(_maxGroundingVerticalStepAfterInitial, Mathf.Abs(appliedVerticalStep));
            }
            else
            {
                _initialGroundingVerticalStep = appliedVerticalStep;
            }

            currentPos.y = clampedNextY;

            if (IsFinite(currentPos))
            {
                targetAnimator.transform.position = currentPos;
                if (freezeRootYAfterInitialGrounding && !_hasFrozenGroundingRootY)
                {
                    _frozenGroundingRootY = currentPos.y;
                    _hasFrozenGroundingRootY = true;
                }

                ApplyGroundedFootLockXZ(lFoot, rFoot, targetHeight, footRadius);
            }
        }

        private bool IsGroundingDirectionReversal(float verticalStep)
        {
            if (!IsFinite(_lastGroundingVerticalStep) || Mathf.Abs(verticalStep) <= 0.0005f || Mathf.Abs(_lastGroundingVerticalStep) <= 0.0005f)
            {
                return false;
            }

            return Mathf.Sign(verticalStep) != Mathf.Sign(_lastGroundingVerticalStep);
        }

        public void ApplyLateVisualGroundingCorrection()
        {
            if (!_isInitialized || !useSmartGrounding || !enableLateVisualGroundingCorrection || targetAnimator == null)
            {
                return;
            }

            if (freezeRootYAfterInitialGrounding && _groundingInitialized && _hasFrozenGroundingRootY)
            {
                Vector3 frozenPos = targetAnimator.transform.position;
                frozenPos.y = _frozenGroundingRootY;
                if (IsFinite(frozenPos))
                {
                    targetAnimator.transform.position = frozenPos;
                }

                _lateVisualGroundingInitialized = true;
                _lastGroundingVerticalStep = 0f;
                return;
            }

            if (!TryGetLowestFootBottomY(out float lowestFootBottomY))
            {
                return;
            }

            float rendererMinY = ResolveGroundingContactBottomY(lowestFootBottomY);

            float targetGroundY = 0.0f;
            float targetHeight = targetGroundY + groundOffset;
            float residual = targetHeight - rendererMinY;
            _lastGroundingTargetY = targetGroundY;
            _lastGroundingLowestFootBottomY = rendererMinY;

            if (!IsFinite(residual))
            {
                LogPoseWarning("Late visual grounding residual became non-finite. Skipping final grounding for this frame.");
                return;
            }

            float deadZone = Mathf.Max(0.001f, groundingDeadZone);
            if (Mathf.Abs(residual) <= deadZone)
            {
                _lateVisualGroundingInitialized = true;
                return;
            }

            if (smoothLateVisualGroundingCorrection && Mathf.Abs(_lastGroundingVerticalStep) > 0.0005f)
            {
                _lateVisualGroundingInitialized = true;
                return;
            }

            float maxCorrection = Mathf.Max(0.001f, maxLateVisualGroundingCorrection);
            if (Mathf.Abs(residual) > maxCorrection)
            {
                if (!_lateVisualGroundingWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] Late visual grounding residual {residual:F3}m exceeded max {maxCorrection:F3}m. Skipping this frame to avoid collapsing a real jump.");
                    _lateVisualGroundingWarningLogged = true;
                }

                _lateVisualGroundingInitialized = true;
                return;
            }

            float effectiveResidual = residual;
            if (smoothLateVisualGroundingCorrection && deadZone > 0f)
            {
                effectiveResidual = Mathf.Sign(residual) * Mathf.Max(0f, Mathf.Abs(residual) - deadZone);
                if (Mathf.Abs(effectiveResidual) <= 0.0001f)
                {
                    _lateVisualGroundingInitialized = true;
                    return;
                }
            }

            Vector3 currentPos = targetAnimator.transform.position;
            if (!IsFinite(currentPos))
            {
                LogPoseWarning("Target position became non-finite before late visual grounding. Skipping final grounding for this frame.");
                return;
            }

            float appliedResidual = CalculateLateVisualGroundingStep(effectiveResidual);
            if (Mathf.Abs(appliedResidual) <= 0.000001f)
            {
                return;
            }

            currentPos.y += appliedResidual;
            if (!IsFinite(currentPos))
            {
                LogPoseWarning("Target position became non-finite after late visual grounding. Skipping final grounding for this frame.");
                return;
            }

            targetAnimator.transform.position = currentPos;
            _lateVisualGroundingInitialized = true;

            _lastGroundingAdjustment = appliedResidual;
            _maxGroundingAdjustment = Mathf.Max(_maxGroundingAdjustment, Mathf.Abs(appliedResidual));
            _lastGroundingVerticalStep = appliedResidual;
            _maxGroundingVerticalStep = Mathf.Max(_maxGroundingVerticalStep, Mathf.Abs(appliedResidual));
            if (_groundingInitialized)
            {
                _maxGroundingVerticalStepAfterInitial = Mathf.Max(_maxGroundingVerticalStepAfterInitial, Mathf.Abs(appliedResidual));
            }
            else
            {
                _groundingInitialized = true;
                _initialGroundingVerticalStep = appliedResidual;
            }
        }

        private float CalculateLateVisualGroundingStep(float residual)
        {
            if (!smoothLateVisualGroundingCorrection)
            {
                return residual;
            }

            if (!_lateVisualGroundingInitialized)
            {
                return residual;
            }

            float snapThreshold = Mathf.Max(0.005f, lateVisualGroundingSnapThreshold);
            float smoothing = Mathf.Clamp01(lateVisualGroundingSmoothing);
            float step = Mathf.Abs(residual) > snapThreshold
                ? residual * Mathf.Max(0.1f, smoothing)
                : residual * smoothing;
            float maxStep = Mathf.Max(0.001f, maxLateVisualGroundingStepPerFrame);
            if (Mathf.Abs(step) > maxStep)
            {
                step = Mathf.Sign(step) * maxStep;
            }

            return step;
        }

        private bool TryGetLowestFootBottomY(out float lowestFootBottomY)
        {
            lowestFootBottomY = 0f;
            if (targetAnimator == null)
            {
                return false;
            }

            Transform leftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null)
            {
                return false;
            }

            float footRadius = GetEstimatedFootRadius();
            float leftBottom = leftFoot.position.y - footRadius;
            float rightBottom = rightFoot.position.y - footRadius;
            if (!IsFinite(leftBottom) || !IsFinite(rightBottom))
            {
                return false;
            }

            lowestFootBottomY = Mathf.Min(leftBottom, rightBottom);
            return true;
        }

        private float GetEstimatedFootRadius()
        {
            return _hasEstimatedFootRadius ? _estimatedFootRadius : DefaultFootRadius;
        }

        private void CalibrateTargetFootRadius()
        {
            _hasEstimatedFootRadius = false;
            _estimatedFootRadius = DefaultFootRadius;
            if (targetAnimator == null || !TryGetRendererBoundsMinY(out float rendererMinY))
            {
                return;
            }

            Transform leftFoot = targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null)
            {
                return;
            }

            float lowestFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            float estimatedRadius = lowestFootY - rendererMinY;
            if (!IsFinite(estimatedRadius))
            {
                return;
            }

            _estimatedFootRadius = Mathf.Clamp(estimatedRadius, 0.02f, 0.16f);
            _hasEstimatedFootRadius = true;
        }

        private float ResolveGroundingContactBottomY(float lowestFootBottomY)
        {
            if (!TryGetRendererBoundsMinY(out float rendererMinY))
            {
                return lowestFootBottomY;
            }

            if (!rejectRendererGroundingOutliers)
            {
                return rendererMinY;
            }

            float separation = Mathf.Abs(rendererMinY - lowestFootBottomY);
            float maxSeparation = Mathf.Max(0.02f, maxRendererFootGroundingSeparation);
            if (separation <= maxSeparation)
            {
                return rendererMinY;
            }

            if (!_rendererGroundingOutlierWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] Renderer bounds grounding outlier ignored. rendererMinY={rendererMinY:F3}, footBottomY={lowestFootBottomY:F3}, separation={separation:F3}, limit={maxSeparation:F3}");
                _rendererGroundingOutlierWarningLogged = true;
            }

            return lowestFootBottomY;
        }

        private void ApplyGroundedFootLockXZ(Transform leftFoot, Transform rightFoot, float targetHeight, float footRadius)
        {
            if (!stabilizeGroundedFootXZ || groundedFootLockWeight <= 0f || targetAnimator == null)
            {
                _leftFootLocked = false;
                _rightFootLocked = false;
                return;
            }

            Vector3 correctionSum = Vector3.zero;
            int correctionCount = 0;
            AddFootLockCorrection(leftFoot, targetHeight, footRadius, ref _leftFootLocked, ref _leftFootLockPosition, ref correctionSum, ref correctionCount);
            AddFootLockCorrection(rightFoot, targetHeight, footRadius, ref _rightFootLocked, ref _rightFootLockPosition, ref correctionSum, ref correctionCount);
            if (correctionCount <= 0)
            {
                return;
            }

            Vector3 correction = correctionSum / correctionCount;
            correction.y = 0f;
            correction *= Mathf.Clamp01(groundedFootLockWeight);
            float maxStep = Mathf.Max(0.001f, maxGroundedFootLockStep);
            if (correction.magnitude > maxStep)
            {
                correction = correction.normalized * maxStep;
            }

            if (!IsFinite(correction) || correction.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            Vector3 rootPosition = targetAnimator.transform.position + correction;
            if (IsFinite(rootPosition))
            {
                targetAnimator.transform.position = rootPosition;
            }
        }

        private void AddFootLockCorrection(
            Transform foot,
            float targetHeight,
            float footRadius,
            ref bool locked,
            ref Vector3 lockPosition,
            ref Vector3 correctionSum,
            ref int correctionCount)
        {
            if (foot == null)
            {
                locked = false;
                return;
            }

            const float contactHeight = 0.08f;
            const float releaseHeight = 0.14f;
            const float resetDistance = 0.25f;
            float bottomY = foot.position.y - footRadius;
            if (!IsFinite(bottomY))
            {
                locked = false;
                return;
            }

            if (bottomY > targetHeight + releaseHeight)
            {
                locked = false;
                return;
            }

            Vector3 footPosition = foot.position;
            footPosition.y = 0f;
            if (!IsFinite(footPosition))
            {
                locked = false;
                return;
            }

            if (!locked || bottomY > targetHeight + contactHeight)
            {
                lockPosition = footPosition;
                locked = bottomY <= targetHeight + contactHeight;
                return;
            }

            Vector3 correction = lockPosition - footPosition;
            correction.y = 0f;
            if (!IsFinite(correction))
            {
                locked = false;
                return;
            }

            if (correction.magnitude > resetDistance)
            {
                lockPosition = footPosition;
                correction = Vector3.zero;
            }

            correctionSum += correction;
            correctionCount++;
        }

        private bool TryGetRendererBoundsMinY(out float minY)
        {
            minY = float.NaN;
            if (targetAnimator == null)
            {
                return false;
            }

            Renderer[] renderers = targetAnimator.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.bounds.size.sqrMagnitude <= 0f)
                {
                    continue;
                }

                if (!IsFinite(renderer.bounds.min.y))
                {
                    continue;
                }

                minY = hasBounds ? Mathf.Min(minY, renderer.bounds.min.y) : renderer.bounds.min.y;
                hasBounds = true;
            }

            return hasBounds;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(29950)]
    public class PoseSpaceLateVisualGroundingCorrection : MonoBehaviour
    {
        [SerializeField] private PoseSpaceRetargeter retargeter;

        public void Initialize(PoseSpaceRetargeter owner)
        {
            retargeter = owner;
            enabled = retargeter != null;
        }

        private void Awake()
        {
            if (retargeter == null)
            {
                retargeter = GetComponent<PoseSpaceRetargeter>();
            }
        }

        private void LateUpdate()
        {
            if (retargeter == null)
            {
                return;
            }

            retargeter.ApplyLateVisualGroundingCorrection();
        }
    }
}
