using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System;
using System.Collections.Generic;
using Fbx2Vmd.Retargeting;
using RootMotion;
using RootMotion.FinalIK;

namespace Fbx2Vmd.FBXImporter
{
    [DefaultExecutionOrder(20000)]
    public class PoseSpaceRetargeter : MonoBehaviour
    {
        private const float LateVisualGroundingPenetrationRecoverySmoothing = 0.55f;
        private const float LateVisualGroundingPenetrationRecoveryMaxStep = 0.1f;
        private const float RecordingStartHipsBaselineFlipWarningThreshold = 0.02f;
        private const float FootHipsAlignedResidualYawGateMeters = 0.12f;
        private const float FootHipsAlignedResidualYawSideGapMeters = 0.005f;
        private const float FootHipsAlignedResidualYawProtectedMaxAngle = 20f;
        private const float HipsLocalPositionTargetGapGuardMaxIncreaseMeters = 0.0005f;
        private const string RecordingStartHipsReferenceStagePrewarmComplete = "prewarm-complete";
        private const int UnresolvedHumanMuscleIndex = -2;
        private static readonly string[] RightSleeveSilhouetteLocalOffsetTransformSuffixes =
        {
            "joint_RightArmM",
            "!joint_RightShoulderC"
        };
        private readonly Dictionary<Transform, Vector3> _rightSleeveSilhouetteLocalOffsetBaseLocalPositions =
            new Dictionary<Transform, Vector3>();
        [Header("--- CORE COMPONENTS ---")]
        [FormerlySerializedAs("ghostAnimator")]
        [SerializeField] private Animator _ghostAnimator;  // (Container 내부의 모델)
        public Animator ghostAnimator { get => _ghostAnimator; private set => _ghostAnimator = value; }
        [FormerlySerializedAs("targetAnimator")]
        [SerializeField] private Animator _targetAnimator; // 내 캐릭터
        public Animator targetAnimator { get => _targetAnimator; set => _targetAnimator = value; }

        [Header("--- FINAL TUNING ---")]
        [Tooltip("캐릭터가 뒤를 보고 있다면 체크 (180도 회전)")]
        [FormerlySerializedAs("fixReverseRotation")]
        [SerializeField] private bool _fixReverseRotation= true;
        public bool fixReverseRotation { get => _fixReverseRotation; private set => _fixReverseRotation = value; }

        [Tooltip("Sub_Manual 직접 Animator 재생처럼 FBX HumanPose의 body/root 회전을 보존합니다.")]
        [FormerlySerializedAs("ShouldPreserveFbxRootRotation")]
        [SerializeField] private bool _ShouldPreserveFbxRootRotation= false;
        public bool ShouldPreserveFbxRootRotation { get => _ShouldPreserveFbxRootRotation; private set => _ShouldPreserveFbxRootRotation = value; }

        [Tooltip("Keep target HumanPose bodyPosition Y stable while preserving FBX X/Z body sway.")]
        [FormerlySerializedAs("preserveTargetBodyPosition")]
        [SerializeField] private bool _preserveTargetBodyPosition= true;
        public bool preserveTargetBodyPosition { get => _preserveTargetBodyPosition; private set => _preserveTargetBodyPosition = value; }

        [Tooltip("Use HumanPose bodyPosition X/Z delta as target root motion to reduce visible foot sliding.")]
        [FormerlySerializedAs("useBodyPositionXZRootMotion")]
        [SerializeField] private bool _useBodyPositionXZRootMotion= false;
        public bool useBodyPositionXZRootMotion { get => _useBodyPositionXZRootMotion; private set => _useBodyPositionXZRootMotion = value; }

        [Tooltip("Editor-only experimental RootT X/Z root motion reference. Keep disabled until visual_body_arc_jitter passes without increasing jitter.")]
        [FormerlySerializedAs("ShouldUseEditorHumanoidRootTranslationReference")]
        [SerializeField] private bool _ShouldUseEditorHumanoidRootTranslationReference= false;
        public bool ShouldUseEditorHumanoidRootTranslationReference { get => _ShouldUseEditorHumanoidRootTranslationReference; private set => _ShouldUseEditorHumanoidRootTranslationReference = value; }

        [Tooltip("Weight for Editor Humanoid RootT translation reference.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("editorHumanoidRootTranslationWeight")]
        [SerializeField] private float _editorHumanoidRootTranslationWeight= 0.25f;
        public float editorHumanoidRootTranslationWeight { get => _editorHumanoidRootTranslationWeight; private set => _editorHumanoidRootTranslationWeight = value; }

        [Tooltip("Current-frame blend for smoothed Editor Humanoid RootT translation delta.")]
        [Range(0.05f, 1f)]
        [FormerlySerializedAs("editorHumanoidRootTranslationCurrentWeight")]
        [SerializeField] private float _editorHumanoidRootTranslationCurrentWeight= 0.35f;
        public float editorHumanoidRootTranslationCurrentWeight { get => _editorHumanoidRootTranslationCurrentWeight; private set => _editorHumanoidRootTranslationCurrentWeight = value; }

        [Tooltip("When a foot is visually grounded, add a small X/Z root correction to reduce skating.")]
        [FormerlySerializedAs("ShouldStabilizeGroundedFootXZ")]
        [SerializeField] private bool _ShouldStabilizeGroundedFootXZ= false;
        public bool ShouldStabilizeGroundedFootXZ { get => _ShouldStabilizeGroundedFootXZ; private set => _ShouldStabilizeGroundedFootXZ = value; }

        [Tooltip("Foot-lock correction strength. Lower values preserve dance motion, higher values reduce skating.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("groundedFootLockWeight")]
        [SerializeField] private float _groundedFootLockWeight= 0.45f;
        public float groundedFootLockWeight { get => _groundedFootLockWeight; private set => _groundedFootLockWeight = value; }

        [Tooltip("Maximum X/Z root correction per frame for grounded foot lock.")]
        [Range(0.001f, 0.1f)]
        [FormerlySerializedAs("maxGroundedFootLockStep")]
        [SerializeField] private float _maxGroundedFootLockStep= 0.025f;
        public float maxGroundedFootLockStep { get => _maxGroundedFootLockStep; private set => _maxGroundedFootLockStep = value; }

        [Tooltip("체크 시 공중 부양/박힘을 모두 해결 (Raycast 사용)")]
        [FormerlySerializedAs("useSmartGrounding")]
        [SerializeField] private bool _useSmartGrounding= true;
        public bool useSmartGrounding { get => _useSmartGrounding; private set => _useSmartGrounding = value; }

        [Tooltip("발바닥 높이 미세 조절 (양수: 띄움, 음수: 박음)")]
        [Range(-0.1f, 0.1f)]
        [FormerlySerializedAs("groundOffset")]
        [SerializeField] private float _groundOffset= 0.0f;
        public float groundOffset { get => _groundOffset; private set => _groundOffset = value; }

        [Tooltip("FBX Avatar에서 비정상적으로 튀는 Humanoid muscle 값을 안전 범위로 제한합니다.")]
        [FormerlySerializedAs("clampMusclesToHumanRange")]
        [SerializeField] private bool _clampMusclesToHumanRange= false;
        public bool clampMusclesToHumanRange { get => _clampMusclesToHumanRange; private set => _clampMusclesToHumanRange = value; }

        [Header("--- ANATOMY GUARD ---")]
        [Tooltip("Target 팔이 늘어나거나 비정상적으로 비틀리는 Humanoid muscle 값을 제한합니다.")]
        [FormerlySerializedAs("enableAnatomicalArmGuard")]
        [SerializeField] private bool _enableAnatomicalArmGuard= true;
        public bool enableAnatomicalArmGuard { get => _enableAnatomicalArmGuard; set => _enableAnatomicalArmGuard = value; }

        [Tooltip("Humanoid 팔 Stretch muscle 허용치입니다. Forearm Stretch는 팔꿈치 굽힘에 가까우므로 기본적으로 제한하지 않습니다.")]
        [Range(0f, 0.5f)]
        [FormerlySerializedAs("armStretchMuscleLimit")]
        [SerializeField] private float _armStretchMuscleLimit= 0f;
        public float armStretchMuscleLimit { get => _armStretchMuscleLimit; set => _armStretchMuscleLimit = value; }

        [Tooltip("Forearm Stretch muscle 제한 여부입니다. Unity Humanoid에서는 팔꿈치 굽힘에 가까우므로 기본값은 꺼야 합니다.")]
        [FormerlySerializedAs("clampArmStretchMuscles")]
        [SerializeField] private bool _clampArmStretchMuscles= false;
        public bool clampArmStretchMuscles { get => _clampArmStretchMuscles; private set => _clampArmStretchMuscles = value; }

        [Tooltip("상완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        [FormerlySerializedAs("upperArmTwistMuscleLimit")]
        [SerializeField] private float _upperArmTwistMuscleLimit= 0.75f;
        public float upperArmTwistMuscleLimit { get => _upperArmTwistMuscleLimit; private set => _upperArmTwistMuscleLimit = value; }

        [Tooltip("전완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)]
        [FormerlySerializedAs("lowerArmTwistMuscleLimit")]
        [SerializeField] private float _lowerArmTwistMuscleLimit= 0.65f;
        public float lowerArmTwistMuscleLimit { get => _lowerArmTwistMuscleLimit; private set => _lowerArmTwistMuscleLimit = value; }

        [Header("--- THUMB ANATOMY GUARD ---")]
        [Tooltip("수동 기준 손가락 pose를 유지하되, YYB 손 구조에서 엄지가 과하게 꺾이는 범위만 제한합니다.")]
        [FormerlySerializedAs("enableThumbAnatomicalGuard")]
        [SerializeField] private bool _enableThumbAnatomicalGuard= true;
        public bool enableThumbAnatomicalGuard { get => _enableThumbAnatomicalGuard; private set => _enableThumbAnatomicalGuard = value; }

        [Tooltip("엄지 굽힘 muscle 최소값입니다.")]
        [Range(-2.5f, 0f)]
        [FormerlySerializedAs("thumbStretchMin")]
        [SerializeField] private float _thumbStretchMin= -2.1f;
        public float thumbStretchMin { get => _thumbStretchMin; private set => _thumbStretchMin = value; }

        [Tooltip("엄지 굽힘 muscle 최대값입니다.")]
        [Range(0f, 2.5f)]
        [FormerlySerializedAs("thumbStretchMax")]
        [SerializeField] private float _thumbStretchMax= 1.0f;
        public float thumbStretchMax { get => _thumbStretchMax; private set => _thumbStretchMax = value; }

        [Tooltip("엄지 굽힘 muscle에 더하는 offset입니다. YYB 엄지 rest pose가 수동 기준보다 과하게 펴져 보일 때만 사용합니다.")]
        [Range(-0.5f, 0.5f)]
        [FormerlySerializedAs("thumbStretchOffset")]
        [SerializeField] private float _thumbStretchOffset= 0f;
        public float thumbStretchOffset { get => _thumbStretchOffset; private set => _thumbStretchOffset = value; }

        [Tooltip("Manual Animator finger reference를 사용할 때는 엄지 stretch offset을 추가하지 않고 수동 기준 엄지 muscle을 보존합니다.")]
        [FormerlySerializedAs("preserveManualFingerReferenceThumbMuscles")]
        [SerializeField] private bool _preserveManualFingerReferenceThumbMuscles= true;
        public bool preserveManualFingerReferenceThumbMuscles { get => _preserveManualFingerReferenceThumbMuscles; private set => _preserveManualFingerReferenceThumbMuscles = value; }

        [FormerlySerializedAs("ShouldUseManualAnimatorFullBodyPoseReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorFullBodyPoseReference= false;
        public bool ShouldUseManualAnimatorFullBodyPoseReference { get => _ShouldUseManualAnimatorFullBodyPoseReference; set => _ShouldUseManualAnimatorFullBodyPoseReference = value; }

        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseReferenceWeight")]
        [SerializeField] private float _manualAnimatorFullBodyPoseReferenceWeight= 1f;
        public float manualAnimatorFullBodyPoseReferenceWeight { get => _manualAnimatorFullBodyPoseReferenceWeight; set => _manualAnimatorFullBodyPoseReferenceWeight = value; }

        [Tooltip("Runtime diagnostic: keep manual full-body reference active but skip lower-body muscles.")]
        [FormerlySerializedAs("ShouldExcludeManualAnimatorFullBodyLowerMuscles")]
        [SerializeField] private bool _ShouldExcludeManualAnimatorFullBodyLowerMuscles= false;
        public bool ShouldExcludeManualAnimatorFullBodyLowerMuscles { get => _ShouldExcludeManualAnimatorFullBodyLowerMuscles; set => _ShouldExcludeManualAnimatorFullBodyLowerMuscles = value; }

        [Tooltip("Runtime diagnostic: apply manual full-body reference only to lower-body muscles.")]
        [FormerlySerializedAs("ShouldApplyManualAnimatorFullBodyLowerMusclesOnly")]
        [SerializeField] private bool _ShouldApplyManualAnimatorFullBodyLowerMusclesOnly= false;
        public bool ShouldApplyManualAnimatorFullBodyLowerMusclesOnly { get => _ShouldApplyManualAnimatorFullBodyLowerMusclesOnly; set => _ShouldApplyManualAnimatorFullBodyLowerMusclesOnly = value; }

        [Tooltip("Runtime diagnostic: apply manual full-body reference only to leg in-out/twist muscles.")]
        [FormerlySerializedAs("ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly")]
        [SerializeField] private bool _ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly= false;
        public bool ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly { get => _ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly; set => _ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = value; }

        [Tooltip("Runtime diagnostic: apply manual full-body reference only to right arm and shoulder muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseRightArmMusclesOnly")]
        [SerializeField] private bool _manualAnimatorFullBodyPoseRightArmMusclesOnly= false;
        public bool manualAnimatorFullBodyPoseRightArmMusclesOnly { get => _manualAnimatorFullBodyPoseRightArmMusclesOnly; set => _manualAnimatorFullBodyPoseRightArmMusclesOnly = value; }

        [Tooltip("Runtime diagnostic: apply manual full-body reference only to left arm and shoulder muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseLeftArmMusclesOnly")]
        [SerializeField] private bool _manualAnimatorFullBodyPoseLeftArmMusclesOnly= false;
        public bool manualAnimatorFullBodyPoseLeftArmMusclesOnly { get => _manualAnimatorFullBodyPoseLeftArmMusclesOnly; set => _manualAnimatorFullBodyPoseLeftArmMusclesOnly = value; }

        [Tooltip("Runtime diagnostic: apply manual full-body reference only to spine and right sleeve chain muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly")]
        [SerializeField] private bool _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly= false;
        public bool manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly { get => _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly; set => _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly = value; }

        [Range(0f, 6000f)]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseFrameGateStart")]
        [SerializeField] private float _manualAnimatorFullBodyPoseFrameGateStart= 0f;
        public float manualAnimatorFullBodyPoseFrameGateStart { get => _manualAnimatorFullBodyPoseFrameGateStart; set => _manualAnimatorFullBodyPoseFrameGateStart = value; }

        [Range(0f, 6000f)]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseFrameGateEnd")]
        [SerializeField] private float _manualAnimatorFullBodyPoseFrameGateEnd= 0f;
        public float manualAnimatorFullBodyPoseFrameGateEnd { get => _manualAnimatorFullBodyPoseFrameGateEnd; set => _manualAnimatorFullBodyPoseFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic: after SetHumanPose, blend only right upper/lower leg twist output muscles back toward the solver input within a small cap.")]
        [FormerlySerializedAs("ShouldUseSetHumanPoseRightLegTwistOutputReference")]
        [SerializeField] private bool _ShouldUseSetHumanPoseRightLegTwistOutputReference= false;
        public bool ShouldUseSetHumanPoseRightLegTwistOutputReference { get => _ShouldUseSetHumanPoseRightLegTwistOutputReference; set => _ShouldUseSetHumanPoseRightLegTwistOutputReference = value; }

        [Range(0f, 1f)]
        [FormerlySerializedAs("setHumanPoseRightLegTwistOutputReferenceWeight")]
        [SerializeField] private float _setHumanPoseRightLegTwistOutputReferenceWeight= 1f;
        public float setHumanPoseRightLegTwistOutputReferenceWeight { get => _setHumanPoseRightLegTwistOutputReferenceWeight; set => _setHumanPoseRightLegTwistOutputReferenceWeight = value; }

        [Range(0f, 0.1f)]
        [FormerlySerializedAs("setHumanPoseRightLegTwistOutputReferenceMaxDelta")]
        [SerializeField] private float _setHumanPoseRightLegTwistOutputReferenceMaxDelta= 0.02f;
        public float setHumanPoseRightLegTwistOutputReferenceMaxDelta { get => _setHumanPoseRightLegTwistOutputReferenceMaxDelta; set => _setHumanPoseRightLegTwistOutputReferenceMaxDelta = value; }

        [Tooltip("Manual Animator finger reference의 엄지 체인 localRotation도 Target에 적용해 모델별 엄지 축 차이를 줄입니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbLocalRotationReference")]
        [SerializeField] private bool _useManualAnimatorThumbLocalRotationReference= true;
        public bool useManualAnimatorThumbLocalRotationReference { get => _useManualAnimatorThumbLocalRotationReference; set => _useManualAnimatorThumbLocalRotationReference = value; }

        [Tooltip("손목 localRotation을 Sub_Manual/testPrefab Animator가 같은 FBX clip에서 평가한 값을 기준으로 덮어씁니다. t13.2 hand pose parity 회귀 보호용입니다.")]
        [FormerlySerializedAs("useManualAnimatorHandLocalRotationReference")]
        [SerializeField] private bool _useManualAnimatorHandLocalRotationReference= true;
        public bool useManualAnimatorHandLocalRotationReference { get => _useManualAnimatorHandLocalRotationReference; set => _useManualAnimatorHandLocalRotationReference = value; }

        [Tooltip("Manual Animator finger reference의 엄지 세그먼트 방향을 Target 손 기준 방향에 맞춰 모델별 bind axis 차이를 줄입니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbSegmentDirectionReference")]
        [SerializeField] private bool _useManualAnimatorThumbSegmentDirectionReference= true;
        public bool useManualAnimatorThumbSegmentDirectionReference { get => _useManualAnimatorThumbSegmentDirectionReference; private set => _useManualAnimatorThumbSegmentDirectionReference = value; }

        [Tooltip("엄지 세그먼트 방향 보정 강도입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorThumbSegmentDirectionWeight")]
        [SerializeField] private float _manualAnimatorThumbSegmentDirectionWeight= 1f;
        public float manualAnimatorThumbSegmentDirectionWeight { get => _manualAnimatorThumbSegmentDirectionWeight; private set => _manualAnimatorThumbSegmentDirectionWeight = value; }

        [Tooltip("Manual Animator finger reference의 손바닥 기준 Hand->ThumbIntermediate 방향을 Target에 적용합니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbHandDirectionReference")]
        [SerializeField] private bool _useManualAnimatorThumbHandDirectionReference= true;
        public bool useManualAnimatorThumbHandDirectionReference { get => _useManualAnimatorThumbHandDirectionReference; private set => _useManualAnimatorThumbHandDirectionReference = value; }

        [Tooltip("손바닥 기준 엄지 시작 방향 보정 강도입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorThumbHandDirectionWeight")]
        [SerializeField] private float _manualAnimatorThumbHandDirectionWeight= 1f;
        public float manualAnimatorThumbHandDirectionWeight { get => _manualAnimatorThumbHandDirectionWeight; private set => _manualAnimatorThumbHandDirectionWeight = value; }

        [Tooltip("Manual Animator finger reference의 손바닥 전체 프레임을 Target 손에 적용합니다.")]
        [FormerlySerializedAs("useManualAnimatorHandPalmFrameReference")]
        [SerializeField] private bool _useManualAnimatorHandPalmFrameReference= true;
        public bool useManualAnimatorHandPalmFrameReference { get => _useManualAnimatorHandPalmFrameReference; set => _useManualAnimatorHandPalmFrameReference = value; }

        [Tooltip("손바닥 프레임 보정 강도입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorHandPalmFrameWeight")]
        [SerializeField] private float _manualAnimatorHandPalmFrameWeight= 1f;
        public float manualAnimatorHandPalmFrameWeight { get => _manualAnimatorHandPalmFrameWeight; set => _manualAnimatorHandPalmFrameWeight = value; }

        [Tooltip("Manual Animator finger reference의 손 기준 엄지 시작 위치를 Target에 적용합니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbBasePositionReference")]
        [SerializeField] private bool _useManualAnimatorThumbBasePositionReference= true;
        public bool useManualAnimatorThumbBasePositionReference { get => _useManualAnimatorThumbBasePositionReference; private set => _useManualAnimatorThumbBasePositionReference = value; }

        [Tooltip("수동 기준 Animator의 Hips localPosition을 target Hips에 선택적으로 적용합니다. testprefab Hips delta가 YYB에 전달되어 발 호 궤적이 심해지므로 기본 비활성화합니다.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorHipsLocalPositionReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorHipsLocalPositionReference= false;
        public bool ShouldUseManualAnimatorHipsLocalPositionReference { get => _ShouldUseManualAnimatorHipsLocalPositionReference; set => _ShouldUseManualAnimatorHipsLocalPositionReference = value; }

        [Tooltip("Sub_Manual/testPrefab Animator의 HumanPose bodyRotation을 retarget pose 기준으로 사용해 팔꿈치 bend plane 기준축 차이를 줄입니다.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorBodyRotationReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorBodyRotationReference= true;
        public bool ShouldUseManualAnimatorBodyRotationReference { get => _ShouldUseManualAnimatorBodyRotationReference; set => _ShouldUseManualAnimatorBodyRotationReference = value; }

        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorBodyRotationReferenceWeight")]
        [SerializeField] private float _manualAnimatorBodyRotationReferenceWeight= 1f;
        public float manualAnimatorBodyRotationReferenceWeight { get => _manualAnimatorBodyRotationReferenceWeight; set => _manualAnimatorBodyRotationReferenceWeight = value; }

        [Tooltip("preserveTargetBodyPosition=true 일 때 body Y 높이를 수동 기준 Animator의 HumanPose bodyPosition.y로 대체합니다. ghost Legacy-animation bodyPos 스파이크 없이 상체 높이를 애니메이션에 맞게 따라가도록 합니다.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorBodyPositionYReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorBodyPositionYReference= false;
        public bool ShouldUseManualAnimatorBodyPositionYReference { get => _ShouldUseManualAnimatorBodyPositionYReference; private set => _ShouldUseManualAnimatorBodyPositionYReference = value; }

        [Tooltip("Runtime diagnostic: blend HumanPose bodyPosition X/Z toward the manual Animator reference before SetHumanPose.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorBodyPositionXzReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorBodyPositionXzReference= false;
        public bool ShouldUseManualAnimatorBodyPositionXzReference { get => _ShouldUseManualAnimatorBodyPositionXzReference; set => _ShouldUseManualAnimatorBodyPositionXzReference = value; }

        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceWeight")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceWeight= 1f;
        public float manualAnimatorBodyPositionXzReferenceWeight { get => _manualAnimatorBodyPositionXzReferenceWeight; set => _manualAnimatorBodyPositionXzReferenceWeight = value; }

        [Range(0f, 0.2f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceMaxOffset")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceMaxOffset= 0.025f;
        public float manualAnimatorBodyPositionXzReferenceMaxOffset { get => _manualAnimatorBodyPositionXzReferenceMaxOffset; set => _manualAnimatorBodyPositionXzReferenceMaxOffset = value; }

        [Range(0f, 6000f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceFrameGateStart")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceFrameGateStart= 0f;
        public float manualAnimatorBodyPositionXzReferenceFrameGateStart { get => _manualAnimatorBodyPositionXzReferenceFrameGateStart; set => _manualAnimatorBodyPositionXzReferenceFrameGateStart = value; }

        [Range(0f, 6000f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceFrameGateEnd")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceFrameGateEnd= 0f;
        public float manualAnimatorBodyPositionXzReferenceFrameGateEnd { get => _manualAnimatorBodyPositionXzReferenceFrameGateEnd; set => _manualAnimatorBodyPositionXzReferenceFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic blend width in recorder frames for manual Animator bodyPosition X/Z frame gates. Zero keeps the legacy hard gate.")]
        [Range(0f, 600f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames= 0f;
        public float manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames { get => _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames; set => _manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames = value; }

        [Tooltip("Runtime diagnostic scale for the manual Animator bodyPosition X solver-input basis. One keeps the legacy X contribution.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceAxisXScale")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceAxisXScale= 1f;
        public float manualAnimatorBodyPositionXzReferenceAxisXScale { get => _manualAnimatorBodyPositionXzReferenceAxisXScale; set => _manualAnimatorBodyPositionXzReferenceAxisXScale = value; }

        [Tooltip("Runtime diagnostic scale for the manual Animator bodyPosition Z solver-input basis. One keeps the legacy Z contribution.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceAxisZScale")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceAxisZScale= 1f;
        public float manualAnimatorBodyPositionXzReferenceAxisZScale { get => _manualAnimatorBodyPositionXzReferenceAxisZScale; set => _manualAnimatorBodyPositionXzReferenceAxisZScale = value; }

        [Tooltip("Runtime diagnostic: apply a frame-local local-X offset to the right sleeve helper silhouette after SetHumanPose.")]
        [FormerlySerializedAs("useYybRightSleeveSilhouetteLocalOffsetReference")]
        [SerializeField] private bool _useYybRightSleeveSilhouetteLocalOffsetReference= false;
        public bool useYybRightSleeveSilhouetteLocalOffsetReference { get => _useYybRightSleeveSilhouetteLocalOffsetReference; set => _useYybRightSleeveSilhouetteLocalOffsetReference = value; }

        [Tooltip("Local X offset in meters for the frame-local right sleeve silhouette probe.")]
        [Range(-0.2f, 0.2f)] [FormerlySerializedAs("yybRightSleeveSilhouetteLocalOffsetX")]
        [SerializeField] private float _yybRightSleeveSilhouetteLocalOffsetX= 0f;
        public float yybRightSleeveSilhouetteLocalOffsetX { get => _yybRightSleeveSilhouetteLocalOffsetX; set => _yybRightSleeveSilhouetteLocalOffsetX = value; }

        [Tooltip("Runtime diagnostic start recorder frame for the right sleeve silhouette local-X offset. Zero with end zero keeps the full clip.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("yybRightSleeveSilhouetteLocalOffsetFrameGateStart")]
        [SerializeField] private float _yybRightSleeveSilhouetteLocalOffsetFrameGateStart= 0f;
        public float yybRightSleeveSilhouetteLocalOffsetFrameGateStart { get => _yybRightSleeveSilhouetteLocalOffsetFrameGateStart; set => _yybRightSleeveSilhouetteLocalOffsetFrameGateStart = value; }

        [Tooltip("Runtime diagnostic end recorder frame for the right sleeve silhouette local-X offset. Zero with start zero keeps the full clip.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("yybRightSleeveSilhouetteLocalOffsetFrameGateEnd")]
        [SerializeField] private float _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd= 0f;
        public float yybRightSleeveSilhouetteLocalOffsetFrameGateEnd { get => _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd; set => _yybRightSleeveSilhouetteLocalOffsetFrameGateEnd = value; }

        [Tooltip("수동 기준 Hips localPosition 보정 강도입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorHipsLocalPositionWeight")]
        [SerializeField] private float _manualAnimatorHipsLocalPositionWeight= 1f;
        public float manualAnimatorHipsLocalPositionWeight { get => _manualAnimatorHipsLocalPositionWeight; set => _manualAnimatorHipsLocalPositionWeight = value; }

        [Tooltip("프레임당 수동 기준 Hips localPosition으로 이동할 수 있는 최대 보정 거리입니다.")]
        [Range(0.001f, 0.5f)]
        [FormerlySerializedAs("manualAnimatorHipsLocalPositionMaxOffset")]
        [SerializeField] private float _manualAnimatorHipsLocalPositionMaxOffset= 0.12f;
        public float manualAnimatorHipsLocalPositionMaxOffset { get => _manualAnimatorHipsLocalPositionMaxOffset; set => _manualAnimatorHipsLocalPositionMaxOffset = value; }

        [Tooltip("Use the manual Animator lowest-foot lift as the grounding target height so jump/foot-height arcs are not flattened to the floor.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorFootHeightGroundingReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorFootHeightGroundingReference= false;
        public bool ShouldUseManualAnimatorFootHeightGroundingReference { get => _ShouldUseManualAnimatorFootHeightGroundingReference; set => _ShouldUseManualAnimatorFootHeightGroundingReference = value; }

        [Tooltip("Blend weight for the manual Animator lowest-foot grounding height reference.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorFootHeightGroundingReferenceWeight")]
        [SerializeField] private float _manualAnimatorFootHeightGroundingReferenceWeight= 1f;
        public float manualAnimatorFootHeightGroundingReferenceWeight { get => _manualAnimatorFootHeightGroundingReferenceWeight; private set => _manualAnimatorFootHeightGroundingReferenceWeight = value; }

        [Tooltip("Maximum positive grounding target lift from the manual Animator lowest-foot reference.")]
        [Range(0f, 0.12f)]
        [FormerlySerializedAs("manualAnimatorFootHeightGroundingReferenceMaxLift")]
        [SerializeField] private float _manualAnimatorFootHeightGroundingReferenceMaxLift= 0.08f;
        public float manualAnimatorFootHeightGroundingReferenceMaxLift { get => _manualAnimatorFootHeightGroundingReferenceMaxLift; private set => _manualAnimatorFootHeightGroundingReferenceMaxLift = value; }

        [Tooltip("Apply the manual Animator lower-body leg-chain localRotation to the target as an isolated runtime candidate.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorFootLocalRotationReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorFootLocalRotationReference= false;
        public bool ShouldUseManualAnimatorFootLocalRotationReference { get => _ShouldUseManualAnimatorFootLocalRotationReference; set => _ShouldUseManualAnimatorFootLocalRotationReference = value; }

        [Tooltip("Blend weight for the manual Animator lower-body leg-chain localRotation reference.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorFootLocalRotationReferenceWeight")]
        [SerializeField] private float _manualAnimatorFootLocalRotationReferenceWeight= 1f;
        public float manualAnimatorFootLocalRotationReferenceWeight { get => _manualAnimatorFootLocalRotationReferenceWeight; set => _manualAnimatorFootLocalRotationReferenceWeight = value; }

        [Tooltip("Apply manual Animator lower-body segment directions as an isolated runtime candidate without changing bone lengths or scale.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorLowerBodySegmentDirectionReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorLowerBodySegmentDirectionReference= false;
        public bool ShouldUseManualAnimatorLowerBodySegmentDirectionReference { get => _ShouldUseManualAnimatorLowerBodySegmentDirectionReference; set => _ShouldUseManualAnimatorLowerBodySegmentDirectionReference = value; }

        [Tooltip("Blend weight for the manual Animator lower-body segment direction correction.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorLowerBodySegmentDirectionReferenceWeight")]
        [SerializeField] private float _manualAnimatorLowerBodySegmentDirectionReferenceWeight= 1f;
        public float manualAnimatorLowerBodySegmentDirectionReferenceWeight { get => _manualAnimatorLowerBodySegmentDirectionReferenceWeight; set => _manualAnimatorLowerBodySegmentDirectionReferenceWeight = value; }

        [Tooltip("Maximum per-frame lower-body segment direction correction angle in degrees.")]
        [Range(0f, 20f)]
        [FormerlySerializedAs("manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle= 6.2f;
        public float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle { get => _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle; set => _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Skip only the upper-leg-to-lower-leg segments from the manual Animator lower-body segment direction correction.")]
        [FormerlySerializedAs("ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference")]
        [SerializeField] private bool _ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference= false;
        public bool ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference { get => _ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference; set => _ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference = value; }

        [Tooltip("Optional upper-leg-to-lower-leg segment direction max angle in degrees. Zero keeps the shared lower-body segment cap.")]
        [Range(0f, 20f)]
        [FormerlySerializedAs("manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle { get => _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle; set => _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Skip only the lower-leg-to-foot segments from the manual Animator lower-body segment direction correction.")]
        [FormerlySerializedAs("ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference")]
        [SerializeField] private bool _ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference= false;
        public bool ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference { get => _ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference; set => _ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference = value; }

        [Tooltip("Optional lower-leg-to-foot segment direction max angle in degrees. Zero keeps the shared lower-body segment cap.")]
        [Range(0f, 20f)]
        [FormerlySerializedAs("manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle { get => _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle; set => _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Optional left lower-leg-to-foot segment direction max angle in degrees. Zero keeps the lower-leg-to-foot segment cap.")]
        [Range(0f, 20f)]
        [FormerlySerializedAs("manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle { get => _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle; set => _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Optional right lower-leg-to-foot segment direction max angle in degrees. Zero keeps the lower-leg-to-foot segment cap.")]
        [Range(0f, 20f)]
        [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Runtime diagnostic scale for right lower-leg-to-foot correction axis X/Z components. One keeps the original axis.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale= 1f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = value; }

        [Tooltip("Blend for right lower-leg-to-foot correction strength. The measured default reduces right-foot X/Z residual without worsening hips-aligned foot residual.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight= 0.125f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = value; }

        [Tooltip("Runtime diagnostic start recorder frame for right lower-leg-to-foot cap. Zero disables frame gating.")]
        [Range(0f, 2000f)]
        [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart= 0f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = value; }

        [Tooltip("Runtime diagnostic end recorder frame for right lower-leg-to-foot cap. Zero disables frame gating.")]
        [Range(0f, 2000f)]
        [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd= 0f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic blend for preserving right foot world rotation after lower-leg-to-foot correction. One keeps the existing endpoint drift.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight= 1f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = value; }

        [Tooltip("Skip only the foot-to-toes segment from the manual Animator lower-body segment direction correction.")]
        [FormerlySerializedAs("ShouldDisableManualAnimatorFootToToesSegmentDirectionReference")]
        [SerializeField] private bool _ShouldDisableManualAnimatorFootToToesSegmentDirectionReference= false;
        public bool ShouldDisableManualAnimatorFootToToesSegmentDirectionReference { get => _ShouldDisableManualAnimatorFootToToesSegmentDirectionReference; set => _ShouldDisableManualAnimatorFootToToesSegmentDirectionReference = value; }

        [Tooltip("Optional foot-to-toes-only segment direction max angle in degrees. Zero keeps the shared lower-body segment cap.")]
        [Range(0f, 20f)]
        [FormerlySerializedAs("manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle { get => _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle; set => _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Apply a yaw-only upper-leg correction toward the manual Animator hips-relative foot X/Z path.")]
        [FormerlySerializedAs("ShouldUseManualAnimatorFootHipsAlignedResidualYawReference")]
        [SerializeField] private bool _ShouldUseManualAnimatorFootHipsAlignedResidualYawReference= false;
        public bool ShouldUseManualAnimatorFootHipsAlignedResidualYawReference { get => _ShouldUseManualAnimatorFootHipsAlignedResidualYawReference; set => _ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = value; }

        [Tooltip("Blend weight for the hips-aligned foot X/Z residual yaw correction.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorFootHipsAlignedResidualYawReferenceWeight")]
        [SerializeField] private float _manualAnimatorFootHipsAlignedResidualYawReferenceWeight= 1f;
        public float manualAnimatorFootHipsAlignedResidualYawReferenceWeight { get => _manualAnimatorFootHipsAlignedResidualYawReferenceWeight; set => _manualAnimatorFootHipsAlignedResidualYawReferenceWeight = value; }

        [Tooltip("Maximum per-frame yaw correction angle for each upper leg in degrees.")]
        [Range(0f, 45f)]
        [FormerlySerializedAs("manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle= 15f;
        public float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle { get => _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle; set => _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = value; }

        [Tooltip("Apply manual Animator hips-relative foot positions through BipedIK as an isolated runtime candidate.")]
        [FormerlySerializedAs("useManualAnimatorBipedIkFootPositionReference")]
        [SerializeField] private bool _useManualAnimatorBipedIkFootPositionReference= false;
        public bool useManualAnimatorBipedIkFootPositionReference { get => _useManualAnimatorBipedIkFootPositionReference; set => _useManualAnimatorBipedIkFootPositionReference = value; }

        [Tooltip("Blend weight for manual Animator BipedIK foot position targets.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorBipedIkFootPositionReferenceWeight")]
        [SerializeField] private float _manualAnimatorBipedIkFootPositionReferenceWeight= 0.65f;
        public float manualAnimatorBipedIkFootPositionReferenceWeight { get => _manualAnimatorBipedIkFootPositionReferenceWeight; set => _manualAnimatorBipedIkFootPositionReferenceWeight = value; }

        [Tooltip("Maximum per-frame BipedIK foot target correction distance from the current target foot position.")]
        [Range(0f, 0.2f)]
        [FormerlySerializedAs("manualAnimatorBipedIkFootPositionReferenceMaxOffset")]
        [SerializeField] private float _manualAnimatorBipedIkFootPositionReferenceMaxOffset= 0.12f;
        public float manualAnimatorBipedIkFootPositionReferenceMaxOffset { get => _manualAnimatorBipedIkFootPositionReferenceMaxOffset; set => _manualAnimatorBipedIkFootPositionReferenceMaxOffset = value; }

        [Tooltip("Runtime diagnostic: apply a bounded right foot/toes endpoint X/Z correction immediately after SetHumanPose.")]
        [FormerlySerializedAs("usePostSetHumanPoseRightEndpointPositionReference")]
        [SerializeField] private bool _usePostSetHumanPoseRightEndpointPositionReference= false;
        public bool usePostSetHumanPoseRightEndpointPositionReference { get => _usePostSetHumanPoseRightEndpointPositionReference; set => _usePostSetHumanPoseRightEndpointPositionReference = value; }

        [Tooltip("Blend weight for post-SetHumanPose right-foot endpoint X/Z correction.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceWeight")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceWeight= 1f;
        public float postSetHumanPoseRightEndpointPositionReferenceWeight { get => _postSetHumanPoseRightEndpointPositionReferenceWeight; set => _postSetHumanPoseRightEndpointPositionReferenceWeight = value; }

        [Tooltip("Maximum per-frame post-SetHumanPose right-foot endpoint X/Z correction distance.")]
        [Range(0f, 0.2f)]
        [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceMaxOffset")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceMaxOffset= 0.04f;
        public float postSetHumanPoseRightEndpointPositionReferenceMaxOffset { get => _postSetHumanPoseRightEndpointPositionReferenceMaxOffset; set => _postSetHumanPoseRightEndpointPositionReferenceMaxOffset = value; }

        [Tooltip("Scale applied only to positive world-Z endpoint correction after SetHumanPose; 1 keeps existing behavior.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferencePositiveZScale")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferencePositiveZScale= 1f;
        public float postSetHumanPoseRightEndpointPositionReferencePositiveZScale { get => _postSetHumanPoseRightEndpointPositionReferencePositiveZScale; set => _postSetHumanPoseRightEndpointPositionReferencePositiveZScale = value; }

        [Tooltip("Blend from foot-only endpoint delta to the existing foot/toes average after SetHumanPose; 1 keeps existing behavior.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight= 1f;
        public float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight { get => _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight; set => _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = value; }

        [Tooltip("First legacy animation frame for post-SetHumanPose right-foot endpoint correction; 0 with end 0 keeps existing behavior.")]
        [Range(0f, 6000f)]
        [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceFrameGateStart")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart= 0f;
        public float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart { get => _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart; set => _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = value; }

        [Tooltip("Last legacy animation frame for post-SetHumanPose right-foot endpoint correction; 0 with start 0 keeps existing behavior.")]
        [Range(0f, 6000f)]
        [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd= 0f;
        public float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd { get => _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd; set => _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic: apply the post-SetHumanPose endpoint correction to the left foot row instead of the right foot row.")]
        [FormerlySerializedAs("ShouldUseLeftSideForPostSetHumanPoseEndpointPosition")]
        [SerializeField] private bool _ShouldUseLeftSideForPostSetHumanPoseEndpointPosition= false;
        public bool ShouldUseLeftSideForPostSetHumanPoseEndpointPosition { get => _ShouldUseLeftSideForPostSetHumanPoseEndpointPosition; set => _ShouldUseLeftSideForPostSetHumanPoseEndpointPosition = value; }

        [Tooltip("Use the first matched reference foot X/Z offset as the post-SetHumanPose right-foot correction basis.")]
        [FormerlySerializedAs("usePostSetHumanPoseRightFootEvaluatorXzReference")]
        [SerializeField] private bool _usePostSetHumanPoseRightFootEvaluatorXzReference= false;
        public bool usePostSetHumanPoseRightFootEvaluatorXzReference { get => _usePostSetHumanPoseRightFootEvaluatorXzReference; set => _usePostSetHumanPoseRightFootEvaluatorXzReference = value; }

        [Tooltip("Target normalized right-foot X/Z magnitude for the first-offset evaluator-basis post-SetHumanPose prototype.")]
        [Range(0f, 0.2f)]
        [FormerlySerializedAs("postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude")]
        [SerializeField] private float _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude= 0.049f;
        public float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude { get => _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude; set => _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = value; }

        [Tooltip("Apply a right-foot endpoint X/Z correction immediately before SetHumanPose as an isolated runtime candidate.")]
        [FormerlySerializedAs("usePreSetHumanPoseRightEndpointPositionReference")]
        [SerializeField] private bool _usePreSetHumanPoseRightEndpointPositionReference= false;
        public bool usePreSetHumanPoseRightEndpointPositionReference { get => _usePreSetHumanPoseRightEndpointPositionReference; set => _usePreSetHumanPoseRightEndpointPositionReference = value; }

        [Tooltip("Blend weight for pre-SetHumanPose right-foot endpoint X/Z correction.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceWeight")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceWeight= 1f;
        public float preSetHumanPoseRightEndpointPositionReferenceWeight { get => _preSetHumanPoseRightEndpointPositionReferenceWeight; set => _preSetHumanPoseRightEndpointPositionReferenceWeight = value; }

        [Tooltip("Maximum per-frame pre-SetHumanPose right-foot endpoint X/Z correction distance.")]
        [Range(0f, 0.2f)]
        [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceMaxOffset")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceMaxOffset= 0.025f;
        public float preSetHumanPoseRightEndpointPositionReferenceMaxOffset { get => _preSetHumanPoseRightEndpointPositionReferenceMaxOffset; set => _preSetHumanPoseRightEndpointPositionReferenceMaxOffset = value; }

        [Tooltip("Scale applied only to positive world-Z endpoint correction before SetHumanPose; 1 keeps existing behavior.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferencePositiveZScale")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferencePositiveZScale= 1f;
        public float preSetHumanPoseRightEndpointPositionReferencePositiveZScale { get => _preSetHumanPoseRightEndpointPositionReferencePositiveZScale; set => _preSetHumanPoseRightEndpointPositionReferencePositiveZScale = value; }

        [Tooltip("Blend from foot-only endpoint delta to the foot/toes average before SetHumanPose.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight= 1f;
        public float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight { get => _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight; set => _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = value; }

        [Tooltip("First legacy animation frame for pre-SetHumanPose right-foot endpoint correction; 0 with end 0 keeps existing behavior.")]
        [Range(0f, 6000f)]
        [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceFrameGateStart")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart= 0f;
        public float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart { get => _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart; set => _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = value; }

        [Tooltip("Last legacy animation frame for pre-SetHumanPose right-foot endpoint correction; 0 with start 0 keeps existing behavior.")]
        [Range(0f, 6000f)]
        [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd= 0f;
        public float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd { get => _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd; set => _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic: apply the pre-SetHumanPose endpoint correction to the left foot row instead of the right foot row.")]
        [FormerlySerializedAs("ShouldUseLeftSideForPreSetHumanPoseEndpointPosition")]
        [SerializeField] private bool _ShouldUseLeftSideForPreSetHumanPoseEndpointPosition= false;
        public bool ShouldUseLeftSideForPreSetHumanPoseEndpointPosition { get => _ShouldUseLeftSideForPreSetHumanPoseEndpointPosition; set => _ShouldUseLeftSideForPreSetHumanPoseEndpointPosition = value; }

        [Tooltip("Runtime diagnostic: use ghost/current endpoint rows as a sign-corrected bodyPosition X/Z translation basis before SetHumanPose.")]
        [FormerlySerializedAs("preSetHumanPoseEndpointPositionUseGhostCurrentBasis")]
        [SerializeField] private bool _preSetHumanPoseEndpointPositionUseGhostCurrentBasis= false;
        public bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis { get => _preSetHumanPoseEndpointPositionUseGhostCurrentBasis; set => _preSetHumanPoseEndpointPositionUseGhostCurrentBasis = value; }

        [Tooltip("Runtime diagnostic: invert the pre-SetHumanPose endpoint bodyPosition X input delta.")]
        [FormerlySerializedAs("ShouldInvertPreSetHumanPoseEndpointPositionBodyX")]
        [SerializeField] private bool _ShouldInvertPreSetHumanPoseEndpointPositionBodyX= false;
        public bool ShouldInvertPreSetHumanPoseEndpointPositionBodyX { get => _ShouldInvertPreSetHumanPoseEndpointPositionBodyX; set => _ShouldInvertPreSetHumanPoseEndpointPositionBodyX = value; }

        [Tooltip("Runtime diagnostic: invert the pre-SetHumanPose endpoint bodyPosition Z input delta.")]
        [FormerlySerializedAs("ShouldInvertPreSetHumanPoseEndpointPositionBodyZ")]
        [SerializeField] private bool _ShouldInvertPreSetHumanPoseEndpointPositionBodyZ= false;
        public bool ShouldInvertPreSetHumanPoseEndpointPositionBodyZ { get => _ShouldInvertPreSetHumanPoseEndpointPositionBodyZ; set => _ShouldInvertPreSetHumanPoseEndpointPositionBodyZ = value; }

        [Tooltip("엄지 시작 위치 보정 강도입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("manualAnimatorThumbBasePositionWeight")]
        [SerializeField] private float _manualAnimatorThumbBasePositionWeight= 1f;
        public float manualAnimatorThumbBasePositionWeight { get => _manualAnimatorThumbBasePositionWeight; private set => _manualAnimatorThumbBasePositionWeight = value; }

        [Tooltip("엄지 시작 위치가 원본 위치에서 벗어날 수 있는 최대 거리입니다.")]
        [Range(0f, 0.03f)]
        [FormerlySerializedAs("manualAnimatorThumbBasePositionMaxOffset")]
        [SerializeField] private float _manualAnimatorThumbBasePositionMaxOffset= 0.03f;
        public float manualAnimatorThumbBasePositionMaxOffset { get => _manualAnimatorThumbBasePositionMaxOffset; private set => _manualAnimatorThumbBasePositionMaxOffset = value; }

        [Tooltip("엄지 벌림 muscle 최소값입니다.")]
        [Range(-1.5f, 0f)]
        [FormerlySerializedAs("thumbSpreadMin")]
        [SerializeField] private float _thumbSpreadMin= -0.9f;
        public float thumbSpreadMin { get => _thumbSpreadMin; private set => _thumbSpreadMin = value; }

        [Tooltip("엄지 벌림 muscle 최대값입니다.")]
        [Range(0f, 1.5f)]
        [FormerlySerializedAs("thumbSpreadMax")]
        [SerializeField] private float _thumbSpreadMax= 0.9f;
        public float thumbSpreadMax { get => _thumbSpreadMax; private set => _thumbSpreadMax = value; }

        [Tooltip("엄지 해부학적 제한이 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logThumbAnatomicalGuardCorrections")]
        [SerializeField] private bool _logThumbAnatomicalGuardCorrections= false;
        public bool logThumbAnatomicalGuardCorrections { get => _logThumbAnatomicalGuardCorrections; private set => _logThumbAnatomicalGuardCorrections = value; }

        [Tooltip("엄지 muscle 제한 이후에도 YYB 엄지 본이 손 구조상 이상하게 꺾이면, 실제 엄지 본 localRotation을 기준 자세 근처로 제한합니다.")]
        [FormerlySerializedAs("enableThumbLocalRotationGuard")]
        [SerializeField] private bool _enableThumbLocalRotationGuard= true;
        public bool enableThumbLocalRotationGuard { get => _enableThumbLocalRotationGuard; private set => _enableThumbLocalRotationGuard = value; }

        [Tooltip("엄지 첫 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 90f)]
        [FormerlySerializedAs("thumbProximalMaxLocalAngle")]
        [SerializeField] private float _thumbProximalMaxLocalAngle= 10f;
        public float thumbProximalMaxLocalAngle { get => _thumbProximalMaxLocalAngle; private set => _thumbProximalMaxLocalAngle = value; }

        [Tooltip("엄지 두 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)]
        [FormerlySerializedAs("thumbIntermediateMaxLocalAngle")]
        [SerializeField] private float _thumbIntermediateMaxLocalAngle= 55f;
        public float thumbIntermediateMaxLocalAngle { get => _thumbIntermediateMaxLocalAngle; private set => _thumbIntermediateMaxLocalAngle = value; }

        [Tooltip("엄지 끝 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)]
        [FormerlySerializedAs("thumbDistalMaxLocalAngle")]
        [SerializeField] private float _thumbDistalMaxLocalAngle= 55f;
        public float thumbDistalMaxLocalAngle { get => _thumbDistalMaxLocalAngle; private set => _thumbDistalMaxLocalAngle = value; }

        [Tooltip("엄지 본 localRotation 제한이 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logThumbLocalRotationGuardCorrections")]
        [SerializeField] private bool _logThumbLocalRotationGuardCorrections= false;
        public bool logThumbLocalRotationGuardCorrections { get => _logThumbLocalRotationGuardCorrections; private set => _logThumbLocalRotationGuardCorrections = value; }

        [Header("--- ROOT MOTION SPIKE GUARD ---")]
        [Tooltip("Ghost root delta가 한 프레임에 과도하게 튀면 순간이동으로 보고 해당 프레임의 추가 root 이동을 무시합니다.")]
        [FormerlySerializedAs("clampRootDeltaSpikes")]
        [SerializeField] private bool _clampRootDeltaSpikes= true;
        public bool clampRootDeltaSpikes { get => _clampRootDeltaSpikes; private set => _clampRootDeltaSpikes = value; }

        [Tooltip("한 프레임에 허용할 최대 root 이동량입니다.")]
        [Range(0.001f, 1.0f)]
        [FormerlySerializedAs("maxRootDeltaPerFrame")]
        [SerializeField] private float _maxRootDeltaPerFrame= 0.25f;
        public float maxRootDeltaPerFrame { get => _maxRootDeltaPerFrame; private set => _maxRootDeltaPerFrame = value; }

        [Tooltip("root delta spike를 무시했을 때 최초 1회 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logRootDeltaSpikes")]
        [SerializeField] private bool _logRootDeltaSpikes= false;
        public bool logRootDeltaSpikes { get => _logRootDeltaSpikes; private set => _logRootDeltaSpikes = value; }

        [Header("--- HIPS LOCAL POSITION SPIKE GUARD ---")]
        [Tooltip("Clamp one-frame target Hips localPosition outliers after SetHumanPose.")]
        [FormerlySerializedAs("clampTargetHipsLocalPositionSpikes")]
        [SerializeField] private bool _clampTargetHipsLocalPositionSpikes= false;
        public bool clampTargetHipsLocalPositionSpikes { get => _clampTargetHipsLocalPositionSpikes; private set => _clampTargetHipsLocalPositionSpikes = value; }

        [Tooltip("Maximum target Hips localPosition delta allowed per frame.")]
        [Range(0.005f, 0.25f)]
        [FormerlySerializedAs("maxTargetHipsLocalPositionDeltaPerFrame")]
        [SerializeField] private float _maxTargetHipsLocalPositionDeltaPerFrame= 0.02f;
        public float maxTargetHipsLocalPositionDeltaPerFrame { get => _maxTargetHipsLocalPositionDeltaPerFrame; private set => _maxTargetHipsLocalPositionDeltaPerFrame = value; }

        [Header("--- GROUNDING STABILITY GUARD ---")]
        [Tooltip("발바닥 접지 보정이 한 프레임에 크게 튀지 않도록 부드럽게 반영합니다.")]
        [FormerlySerializedAs("smoothGrounding")]
        [SerializeField] private bool _smoothGrounding= true;
        public bool smoothGrounding { get => _smoothGrounding; private set => _smoothGrounding = value; }

        [Tooltip("한 프레임에 허용할 최대 수직 접지 보정값입니다.")]
        [Range(0.001f, 0.2f)]
        [FormerlySerializedAs("maxGroundingVerticalStepPerFrame")]
        [SerializeField] private float _maxGroundingVerticalStepPerFrame= 0.01f;
        public float maxGroundingVerticalStepPerFrame { get => _maxGroundingVerticalStepPerFrame; private set => _maxGroundingVerticalStepPerFrame = value; }

        [Tooltip("접지 보정 목표값을 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("groundingSmoothing")]
        [SerializeField] private float _groundingSmoothing= 0.25f;
        public float groundingSmoothing { get => _groundingSmoothing; private set => _groundingSmoothing = value; }

        [Tooltip("이 값보다 작은 발바닥 떨림은 무시합니다.")]
        [Range(0f, 0.05f)]
        [FormerlySerializedAs("groundingDeadZone")]
        [SerializeField] private float _groundingDeadZone= 0.005f;
        public float groundingDeadZone { get => _groundingDeadZone; private set => _groundingDeadZone = value; }

        [Tooltip("초기 접지 확정 뒤에는 root Y를 고정합니다. MMD VMD export에서는 이후 프레임의 발 빠짐을 막기 위해 기본 비활성화합니다.")]
        [FormerlySerializedAs("freezeRootYAfterInitialGrounding")]
        [SerializeField] private bool _freezeRootYAfterInitialGrounding= false;
        public bool freezeRootYAfterInitialGrounding { get => _freezeRootYAfterInitialGrounding; private set => _freezeRootYAfterInitialGrounding = value; }

        [Tooltip("Editor/GameView 프레임이 밀려도 Ghost clip time이 한 프레임에 크게 건너뛰지 않게 제한합니다.")]
        [FormerlySerializedAs("clampLegacyAnimationVisualStep")]
        [SerializeField] private bool _clampLegacyAnimationVisualStep= false;
        public bool clampLegacyAnimationVisualStep { get => _clampLegacyAnimationVisualStep; private set => _clampLegacyAnimationVisualStep = value; }

        [Tooltip("Ghost clip time이 한 렌더 프레임에 전진할 수 있는 기준 FPS입니다.")]
        [Range(15f, 120f)]
        [FormerlySerializedAs("legacyAnimationVisualFrameRate")]
        [SerializeField] private float _legacyAnimationVisualFrameRate= 30f;
        public float legacyAnimationVisualFrameRate { get => _legacyAnimationVisualFrameRate; private set => _legacyAnimationVisualFrameRate = value; }

        [Tooltip("프레임 지연으로 pose가 한 번에 크게 바뀌면 clip time은 보존하고 target pose만 부드럽게 따라가게 합니다.")]
        [FormerlySerializedAs("smoothPoseOnLegacyAnimationStepSpike")]
        [SerializeField] private bool _smoothPoseOnLegacyAnimationStepSpike= true;
        public bool smoothPoseOnLegacyAnimationStepSpike { get => _smoothPoseOnLegacyAnimationStepSpike; private set => _smoothPoseOnLegacyAnimationStepSpike = value; }

        [Tooltip("pose spike smoothing 때 현재 FBX pose를 반영하는 비율입니다.")]
        [Range(0.1f, 1f)]
        [FormerlySerializedAs("poseVisualSpikeCurrentWeight")]
        [SerializeField] private float _poseVisualSpikeCurrentWeight= 0.65f;
        public float poseVisualSpikeCurrentWeight { get => _poseVisualSpikeCurrentWeight; private set => _poseVisualSpikeCurrentWeight = value; }

        [Tooltip("Optional forearm stretch clamp around the current pose during visual spike smoothing. 0 disables the clamp.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("poseVisualSpikeForearmStretchClampMaxOffset")]
        [SerializeField] private float _poseVisualSpikeForearmStretchClampMaxOffset= 0f;
        public float poseVisualSpikeForearmStretchClampMaxOffset { get => _poseVisualSpikeForearmStretchClampMaxOffset; private set => _poseVisualSpikeForearmStretchClampMaxOffset = value; }

        [Tooltip("이 값보다 큰 muscle delta가 발생하면 frame-time spike가 아니어도 pose smoothing을 적용합니다.")]
        [Range(0.05f, 1f)]
        [FormerlySerializedAs("poseVisualMuscleDeltaThreshold")]
        [SerializeField] private float _poseVisualMuscleDeltaThreshold= 0.35f;
        public float poseVisualMuscleDeltaThreshold { get => _poseVisualMuscleDeltaThreshold; private set => _poseVisualMuscleDeltaThreshold = value; }

        [Tooltip("Renderer bounds 하단이 발바닥 추정치에서 과하게 멀면 접지 기준에서 제외합니다.")]
        [FormerlySerializedAs("rejectRendererGroundingOutliers")]
        [SerializeField] private bool _rejectRendererGroundingOutliers= true;
        public bool rejectRendererGroundingOutliers { get => _rejectRendererGroundingOutliers; private set => _rejectRendererGroundingOutliers = value; }

        [Tooltip("Renderer bounds 하단과 발바닥 추정치 사이에 허용할 최대 거리입니다.")]
        [Range(0.02f, 0.3f)]
        [FormerlySerializedAs("maxRendererFootGroundingSeparation")]
        [SerializeField] private float _maxRendererFootGroundingSeparation= 0.12f;
        public float maxRendererFootGroundingSeparation { get => _maxRendererFootGroundingSeparation; private set => _maxRendererFootGroundingSeparation = value; }

        [Tooltip("LateUpdate 후반의 손/팔 보호 로직이 끝난 뒤 메시 bounds 기준으로 루트 Y만 한 번 더 보정합니다.")]
        [FormerlySerializedAs("enableLateVisualGroundingCorrection")]
        [SerializeField] private bool _enableLateVisualGroundingCorrection= true;
        public bool enableLateVisualGroundingCorrection { get => _enableLateVisualGroundingCorrection; private set => _enableLateVisualGroundingCorrection = value; }

        [Tooltip("최종 메시 bounds 보정이 한 프레임에 적용할 수 있는 최대 Y 이동량입니다.")]
        [Range(0.01f, 0.2f)]
        [FormerlySerializedAs("maxLateVisualGroundingCorrection")]
        [SerializeField] private float _maxLateVisualGroundingCorrection= 0.2f;
        public float maxLateVisualGroundingCorrection { get => _maxLateVisualGroundingCorrection; private set => _maxLateVisualGroundingCorrection = value; }

        [Tooltip("최종 메시 bounds 접지 보정의 작은 잔여 오차를 부드럽게 반영해 모델 전체 떨림을 줄입니다.")]
        [FormerlySerializedAs("smoothLateVisualGroundingCorrection")]
        [SerializeField] private bool _smoothLateVisualGroundingCorrection= true;
        public bool smoothLateVisualGroundingCorrection { get => _smoothLateVisualGroundingCorrection; private set => _smoothLateVisualGroundingCorrection = value; }

        [Tooltip("Late visual grounding 잔여 오차가 이 값보다 작으면 smoothing 대상으로 봅니다. 큰 오차는 공중 부유 방지를 위해 즉시 보정합니다.")]
        [Range(0.005f, 0.1f)]
        [FormerlySerializedAs("lateVisualGroundingSnapThreshold")]
        [SerializeField] private float _lateVisualGroundingSnapThreshold= 0.03f;
        public float lateVisualGroundingSnapThreshold { get => _lateVisualGroundingSnapThreshold; private set => _lateVisualGroundingSnapThreshold = value; }

        [Tooltip("작은 late visual grounding 잔여 오차를 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("lateVisualGroundingSmoothing")]
        [SerializeField] private float _lateVisualGroundingSmoothing= 0.25f;
        public float lateVisualGroundingSmoothing { get => _lateVisualGroundingSmoothing; private set => _lateVisualGroundingSmoothing = value; }

        [Tooltip("작은 late visual grounding smoothing 보정이 한 프레임에 움직일 수 있는 최대 Y 이동량입니다.")]
        [Range(0.001f, 0.05f)]
        [FormerlySerializedAs("maxLateVisualGroundingStepPerFrame")]
        [SerializeField] private float _maxLateVisualGroundingStepPerFrame= 0.003f;
        public float maxLateVisualGroundingStepPerFrame { get => _maxLateVisualGroundingStepPerFrame; private set => _maxLateVisualGroundingStepPerFrame = value; }

        public float LastRootDeltaMagnitude => _lastRootDeltaMagnitude;
        public float MaxRootDeltaMagnitude => _maxRootDeltaMagnitude;
        public int RootDeltaSpikeSkippedCount => _rootDeltaSpikeSkippedCount;
        public float LastRootPositionPoseDeltaMagnitude => _lastRootPositionPoseDeltaMagnitude;
        public float MaxRootPositionPoseDeltaMagnitude => _maxRootPositionPoseDeltaMagnitude;
        public int RootPositionSpikeClampedCount => _rootPositionSpikeClampedCount;
        public float LastTargetHipsLocalPositionDelta => _lastTargetHipsLocalPositionDelta;
        public float MaxTargetHipsLocalPositionDelta => _maxTargetHipsLocalPositionDelta;
        public int TargetHipsLocalPositionSpikeClampedCount => _targetHipsLocalPositionSpikeClampedCount;
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
        public float LastEditorFootHeightGroundingReferenceLift => _lastEditorFootHeightGroundingReferenceLift;
        public float RecordingStartRootY => _recordingStartRootY;
        public float RecordingStartBodyPositionY => _recordingStartBodyPositionY;
        public float RecordingStartHipsLocalY => _recordingStartHipsLocalY;
        public float RecordingStartHipsY => _recordingStartHipsY;
        public float RecordingStartHipsReferenceBeforeLocalY => _recordingStartHipsReferenceBeforeLocalY;
        public float RecordingStartHipsReferenceAfterLocalY => _recordingStartHipsReferenceAfterLocalY;
        public float RecordingStartHipsReferenceDeltaY => _recordingStartHipsReferenceDeltaY;
        public int RecordingStartHipsReferenceFlipDetected => _recordingStartHipsReferenceFlipDetected ? 1 : 0;
        public string RecordingStartHipsReferenceStage => _recordingStartHipsReferenceStage;
        public float LastLegacyAnimationStep => _legacyAnimationDriver.LastStep;
        public float MaxLegacyAnimationStep => _legacyAnimationDriver.MaxStep;
        public int LegacyAnimationStepSpikeCount => _legacyAnimationDriver.StepSpikeCount;
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
        public float LastPoseInputLeftShoulderFrontBackMuscle => _lastPoseInputLeftShoulderFrontBackMuscle;
        public float LastAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle => _lastAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle;
        public float LastAfterClampPoseMusclesLeftShoulderFrontBackMuscle => _lastAfterClampPoseMusclesLeftShoulderFrontBackMuscle;
        public float LastAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle => _lastAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle;
        public float LastAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle => _lastAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle;
        public float LastSetHumanPoseInputLeftShoulderFrontBackMuscle => _lastSetHumanPoseInputLeftShoulderFrontBackMuscle;
        public float LastSetHumanPoseOutputLeftShoulderFrontBackMuscle => _lastSetHumanPoseOutputLeftShoulderFrontBackMuscle;
        public float LastSetHumanPoseLeftShoulderFrontBackDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputLeftShoulderFrontBackMuscle,
            _lastSetHumanPoseOutputLeftShoulderFrontBackMuscle);
        public float LastPoseInputLeftArmTwistMuscle => _lastPoseInputLeftArmTwistMuscle;
        public float LastAfterEditorMuscleReferenceLeftArmTwistMuscle => _lastAfterEditorMuscleReferenceLeftArmTwistMuscle;
        public float LastAfterClampPoseMusclesLeftArmTwistMuscle => _lastAfterClampPoseMusclesLeftArmTwistMuscle;
        public float LastAfterAnatomicalArmGuardLeftArmTwistMuscle => _lastAfterAnatomicalArmGuardLeftArmTwistMuscle;
        public float LastAfterVisualSpikeSmoothingLeftArmTwistMuscle => _lastAfterVisualSpikeSmoothingLeftArmTwistMuscle;
        public float LastSetHumanPoseInputLeftArmTwistMuscle => _lastSetHumanPoseInputLeftArmTwistMuscle;
        public float LastSetHumanPoseOutputLeftArmTwistMuscle => _lastSetHumanPoseOutputLeftArmTwistMuscle;
        public float LastSetHumanPoseLeftArmTwistDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputLeftArmTwistMuscle,
            _lastSetHumanPoseOutputLeftArmTwistMuscle);
        public float LastPoseInputLeftForearmStretchMuscle => _lastPoseInputLeftForearmStretchMuscle;
        public float LastAfterEditorMuscleReferenceLeftForearmStretchMuscle => _lastAfterEditorMuscleReferenceLeftForearmStretchMuscle;
        public float LastAfterClampPoseMusclesLeftForearmStretchMuscle => _lastAfterClampPoseMusclesLeftForearmStretchMuscle;
        public float LastAfterAnatomicalArmGuardLeftForearmStretchMuscle => _lastAfterAnatomicalArmGuardLeftForearmStretchMuscle;
        public float LastAfterVisualSpikeSmoothingLeftForearmStretchMuscle => _lastAfterVisualSpikeSmoothingLeftForearmStretchMuscle;
        public float LastSetHumanPoseInputLeftForearmStretchMuscle => _lastSetHumanPoseInputLeftForearmStretchMuscle;
        public float LastSetHumanPoseOutputLeftForearmStretchMuscle => _lastSetHumanPoseOutputLeftForearmStretchMuscle;
        public float LastSetHumanPoseLeftForearmStretchDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputLeftForearmStretchMuscle,
            _lastSetHumanPoseOutputLeftForearmStretchMuscle);
        public float LastPoseInputRightForearmStretchMuscle => _lastPoseInputRightForearmStretchMuscle;
        public float LastAfterEditorMuscleReferenceRightForearmStretchMuscle => _lastAfterEditorMuscleReferenceRightForearmStretchMuscle;
        public float LastAfterClampPoseMusclesRightForearmStretchMuscle => _lastAfterClampPoseMusclesRightForearmStretchMuscle;
        public float LastAfterAnatomicalArmGuardRightForearmStretchMuscle => _lastAfterAnatomicalArmGuardRightForearmStretchMuscle;
        public float LastAfterVisualSpikeSmoothingRightForearmStretchMuscle => _lastAfterVisualSpikeSmoothingRightForearmStretchMuscle;
        public float LastSetHumanPoseInputRightForearmStretchMuscle => _lastSetHumanPoseInputRightForearmStretchMuscle;
        public float LastSetHumanPoseOutputRightForearmStretchMuscle => _lastSetHumanPoseOutputRightForearmStretchMuscle;
        public float LastSetHumanPoseRightForearmStretchDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightForearmStretchMuscle,
            _lastSetHumanPoseOutputRightForearmStretchMuscle);
        public float LastPoseInputRightArmTwistMuscle => _lastPoseInputRightArmTwistMuscle;
        public float LastAfterEditorMuscleReferenceRightArmTwistMuscle => _lastAfterEditorMuscleReferenceRightArmTwistMuscle;
        public float LastAfterClampPoseMusclesRightArmTwistMuscle => _lastAfterClampPoseMusclesRightArmTwistMuscle;
        public float LastAfterAnatomicalArmGuardRightArmTwistMuscle => _lastAfterAnatomicalArmGuardRightArmTwistMuscle;
        public float LastAfterVisualSpikeSmoothingRightArmTwistMuscle => _lastAfterVisualSpikeSmoothingRightArmTwistMuscle;
        public float LastSetHumanPoseInputRightArmTwistMuscle => _lastSetHumanPoseInputRightArmTwistMuscle;
        public float LastSetHumanPoseOutputRightArmTwistMuscle => _lastSetHumanPoseOutputRightArmTwistMuscle;
        public float LastSetHumanPoseRightArmTwistDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightArmTwistMuscle,
            _lastSetHumanPoseOutputRightArmTwistMuscle);
        public float LastSetHumanPoseInputLeftUpperLegFrontBackMuscle => _lastSetHumanPoseInputLeftUpperLegFrontBackMuscle;
        public float LastSetHumanPoseOutputLeftUpperLegFrontBackMuscle => _lastSetHumanPoseOutputLeftUpperLegFrontBackMuscle;
        public float LastSetHumanPoseLeftUpperLegFrontBackDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputLeftUpperLegFrontBackMuscle,
            _lastSetHumanPoseOutputLeftUpperLegFrontBackMuscle);
        public float LastSetHumanPoseInputRightUpperLegFrontBackMuscle => _lastSetHumanPoseInputRightUpperLegFrontBackMuscle;
        public float LastSetHumanPoseOutputRightUpperLegFrontBackMuscle => _lastSetHumanPoseOutputRightUpperLegFrontBackMuscle;
        public float LastSetHumanPoseRightUpperLegFrontBackDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightUpperLegFrontBackMuscle,
            _lastSetHumanPoseOutputRightUpperLegFrontBackMuscle);
        public float LastSetHumanPoseInputLeftLowerLegStretchMuscle => _lastSetHumanPoseInputLeftLowerLegStretchMuscle;
        public float LastSetHumanPoseOutputLeftLowerLegStretchMuscle => _lastSetHumanPoseOutputLeftLowerLegStretchMuscle;
        public float LastSetHumanPoseLeftLowerLegStretchDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputLeftLowerLegStretchMuscle,
            _lastSetHumanPoseOutputLeftLowerLegStretchMuscle);
        public float LastSetHumanPoseInputRightLowerLegStretchMuscle => _lastSetHumanPoseInputRightLowerLegStretchMuscle;
        public float LastSetHumanPoseOutputRightLowerLegStretchMuscle => _lastSetHumanPoseOutputRightLowerLegStretchMuscle;
        public float LastSetHumanPoseRightLowerLegStretchDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightLowerLegStretchMuscle,
            _lastSetHumanPoseOutputRightLowerLegStretchMuscle);
        public float LastSetHumanPoseInputLeftFootUpDownMuscle => _lastSetHumanPoseInputLeftFootUpDownMuscle;
        public float LastSetHumanPoseOutputLeftFootUpDownMuscle => _lastSetHumanPoseOutputLeftFootUpDownMuscle;
        public float LastSetHumanPoseLeftFootUpDownDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputLeftFootUpDownMuscle,
            _lastSetHumanPoseOutputLeftFootUpDownMuscle);
        public float LastSetHumanPoseInputRightFootUpDownMuscle => _lastSetHumanPoseInputRightFootUpDownMuscle;
        public float LastSetHumanPoseOutputRightFootUpDownMuscle => _lastSetHumanPoseOutputRightFootUpDownMuscle;
        public float LastSetHumanPoseRightFootUpDownDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightFootUpDownMuscle,
            _lastSetHumanPoseOutputRightFootUpDownMuscle);
        public float LastSetHumanPoseInputBodyPositionX => _lastSetHumanPoseInputBodyPosition.x;
        public float LastSetHumanPoseInputBodyPositionY => _lastSetHumanPoseInputBodyPosition.y;
        public float LastSetHumanPoseInputBodyPositionZ => _lastSetHumanPoseInputBodyPosition.z;
        public float LastSetHumanPoseOutputBodyPositionX => _lastSetHumanPoseOutputBodyPosition.x;
        public float LastSetHumanPoseOutputBodyPositionY => _lastSetHumanPoseOutputBodyPosition.y;
        public float LastSetHumanPoseOutputBodyPositionZ => _lastSetHumanPoseOutputBodyPosition.z;
        public float LastSetHumanPoseBodyPositionDeltaXZ => CalculateFiniteXzDelta(
            _lastSetHumanPoseInputBodyPosition,
            _lastSetHumanPoseOutputBodyPosition);
        public float LastSetHumanPoseInputBodyRotationYaw => ReadBodyRotationYaw(_lastSetHumanPoseInputBodyRotation);
        public float LastSetHumanPoseOutputBodyRotationYaw => ReadBodyRotationYaw(_lastSetHumanPoseOutputBodyRotation);
        public float LastSetHumanPoseBodyRotationDeltaAngle => CalculateFiniteAngleDelta(
            _lastSetHumanPoseInputBodyRotation,
            _lastSetHumanPoseOutputBodyRotation);
        public float LastSetHumanPosePreSolveGhostRootWorldX => _lastSetHumanPosePreSolveGhostRootWorldPosition.x;
        public float LastSetHumanPosePreSolveGhostRootWorldY => _lastSetHumanPosePreSolveGhostRootWorldPosition.y;
        public float LastSetHumanPosePreSolveGhostRootWorldZ => _lastSetHumanPosePreSolveGhostRootWorldPosition.z;
        public float LastSetHumanPosePreSolveGhostRootYaw => ReadBodyRotationYaw(_lastSetHumanPosePreSolveGhostRootWorldRotation);
        public float LastSetHumanPosePreSolveTargetRootWorldX => _lastSetHumanPosePreSolveTargetRootWorldPosition.x;
        public float LastSetHumanPosePreSolveTargetRootWorldY => _lastSetHumanPosePreSolveTargetRootWorldPosition.y;
        public float LastSetHumanPosePreSolveTargetRootWorldZ => _lastSetHumanPosePreSolveTargetRootWorldPosition.z;
        public float LastSetHumanPosePreSolveTargetRootYaw => ReadBodyRotationYaw(_lastSetHumanPosePreSolveTargetRootWorldRotation);
        public float LastSetHumanPosePreSolveTargetHipsWorldX => _lastSetHumanPosePreSolveTargetHipsWorldPosition.x;
        public float LastSetHumanPosePreSolveTargetHipsWorldY => _lastSetHumanPosePreSolveTargetHipsWorldPosition.y;
        public float LastSetHumanPosePreSolveTargetHipsWorldZ => _lastSetHumanPosePreSolveTargetHipsWorldPosition.z;
        public float LastSetHumanPosePreSolveTargetHipsLocalX => _lastSetHumanPosePreSolveTargetHipsLocalPosition.x;
        public float LastSetHumanPosePreSolveTargetHipsLocalY => _lastSetHumanPosePreSolveTargetHipsLocalPosition.y;
        public float LastSetHumanPosePreSolveTargetHipsLocalZ => _lastSetHumanPosePreSolveTargetHipsLocalPosition.z;
        public float LastSetHumanPosePreSolveBodyPositionX => _lastSetHumanPosePreSolveBodyPosition.x;
        public float LastSetHumanPosePreSolveBodyPositionY => _lastSetHumanPosePreSolveBodyPosition.y;
        public float LastSetHumanPosePreSolveBodyPositionZ => _lastSetHumanPosePreSolveBodyPosition.z;
        public float LastSetHumanPosePreSolveBodyRotationYaw => ReadBodyRotationYaw(_lastSetHumanPosePreSolveBodyRotation);
        public float LastPreSetHumanPoseEndpointBodyPositionBeforeX => _lastPreSetHumanPoseEndpointBodyPositionBefore.x;
        public float LastPreSetHumanPoseEndpointBodyPositionBeforeZ => _lastPreSetHumanPoseEndpointBodyPositionBefore.z;
        public float LastPreSetHumanPoseEndpointBodyPositionAfterX => _lastPreSetHumanPoseEndpointBodyPositionAfter.x;
        public float LastPreSetHumanPoseEndpointBodyPositionAfterZ => _lastPreSetHumanPoseEndpointBodyPositionAfter.z;
        public float LastPreSetHumanPoseEndpointBodyPositionDeltaX => _lastPreSetHumanPoseEndpointBodyPositionDelta.x;
        public float LastPreSetHumanPoseEndpointBodyPositionDeltaZ => _lastPreSetHumanPoseEndpointBodyPositionDelta.z;
        public float LastPreSetHumanPoseEndpointBodyPositionDeltaMagnitudeXZ => CalculateFiniteXzDelta(
            Vector3.zero,
            _lastPreSetHumanPoseEndpointBodyPositionDelta);
        public float LastSetHumanPosePreSolveGhostLeftFootWorldX => _lastSetHumanPosePreSolveGhostEndpointPositions.LeftFoot.x;
        public float LastSetHumanPosePreSolveGhostLeftFootWorldZ => _lastSetHumanPosePreSolveGhostEndpointPositions.LeftFoot.z;
        public float LastSetHumanPosePreSolveGhostLeftToesWorldX => _lastSetHumanPosePreSolveGhostEndpointPositions.LeftToes.x;
        public float LastSetHumanPosePreSolveGhostLeftToesWorldZ => _lastSetHumanPosePreSolveGhostEndpointPositions.LeftToes.z;
        public float LastSetHumanPosePreSolveCurrentLeftFootWorldX => _lastSetHumanPosePreSolveCurrentEndpointPositions.LeftFoot.x;
        public float LastSetHumanPosePreSolveCurrentLeftFootWorldZ => _lastSetHumanPosePreSolveCurrentEndpointPositions.LeftFoot.z;
        public float LastSetHumanPosePreSolveCurrentLeftToesWorldX => _lastSetHumanPosePreSolveCurrentEndpointPositions.LeftToes.x;
        public float LastSetHumanPosePreSolveCurrentLeftToesWorldZ => _lastSetHumanPosePreSolveCurrentEndpointPositions.LeftToes.z;
        public float LastSetHumanPosePreSolveTargetLeftFootWorldX => _lastSetHumanPosePreSolveTargetEndpointPositions.LeftFoot.x;
        public float LastSetHumanPosePreSolveTargetLeftFootWorldZ => _lastSetHumanPosePreSolveTargetEndpointPositions.LeftFoot.z;
        public float LastSetHumanPosePreSolveTargetLeftToesWorldX => _lastSetHumanPosePreSolveTargetEndpointPositions.LeftToes.x;
        public float LastSetHumanPosePreSolveTargetLeftToesWorldZ => _lastSetHumanPosePreSolveTargetEndpointPositions.LeftToes.z;
        public float LastSetHumanPosePreSolveGhostRightFootWorldX => _lastSetHumanPosePreSolveGhostEndpointPositions.RightFoot.x;
        public float LastSetHumanPosePreSolveGhostRightFootWorldZ => _lastSetHumanPosePreSolveGhostEndpointPositions.RightFoot.z;
        public float LastSetHumanPosePreSolveGhostRightToesWorldX => _lastSetHumanPosePreSolveGhostEndpointPositions.RightToes.x;
        public float LastSetHumanPosePreSolveGhostRightToesWorldZ => _lastSetHumanPosePreSolveGhostEndpointPositions.RightToes.z;
        public float LastSetHumanPosePreSolveCurrentRightFootWorldX => _lastSetHumanPosePreSolveCurrentEndpointPositions.RightFoot.x;
        public float LastSetHumanPosePreSolveCurrentRightFootWorldZ => _lastSetHumanPosePreSolveCurrentEndpointPositions.RightFoot.z;
        public float LastSetHumanPosePreSolveCurrentRightToesWorldX => _lastSetHumanPosePreSolveCurrentEndpointPositions.RightToes.x;
        public float LastSetHumanPosePreSolveCurrentRightToesWorldZ => _lastSetHumanPosePreSolveCurrentEndpointPositions.RightToes.z;
        public float LastSetHumanPosePreSolveTargetRightFootWorldX => _lastSetHumanPosePreSolveTargetEndpointPositions.RightFoot.x;
        public float LastSetHumanPosePreSolveTargetRightFootWorldZ => _lastSetHumanPosePreSolveTargetEndpointPositions.RightFoot.z;
        public float LastSetHumanPosePreSolveTargetRightToesWorldX => _lastSetHumanPosePreSolveTargetEndpointPositions.RightToes.x;
        public float LastSetHumanPosePreSolveTargetRightToesWorldZ => _lastSetHumanPosePreSolveTargetEndpointPositions.RightToes.z;
        public float LastSetHumanPoseInputSpineFrontBackMuscle => _lastSetHumanPoseInputSpineFrontBackMuscle;
        public float LastSetHumanPoseInputSpineLeftRightMuscle => _lastSetHumanPoseInputSpineLeftRightMuscle;
        public float LastSetHumanPoseInputSpineTwistLeftRightMuscle => _lastSetHumanPoseInputSpineTwistLeftRightMuscle;
        public float LastSetHumanPoseInputChestFrontBackMuscle => _lastSetHumanPoseInputChestFrontBackMuscle;
        public float LastSetHumanPoseInputChestLeftRightMuscle => _lastSetHumanPoseInputChestLeftRightMuscle;
        public float LastSetHumanPoseInputChestTwistLeftRightMuscle => _lastSetHumanPoseInputChestTwistLeftRightMuscle;
        public float LastSetHumanPoseInputUpperChestFrontBackMuscle => _lastSetHumanPoseInputUpperChestFrontBackMuscle;
        public float LastSetHumanPoseInputUpperChestLeftRightMuscle => _lastSetHumanPoseInputUpperChestLeftRightMuscle;
        public float LastSetHumanPoseInputUpperChestTwistLeftRightMuscle => _lastSetHumanPoseInputUpperChestTwistLeftRightMuscle;
        public float LastSetHumanPoseInputLeftUpperLegInOutMuscle => _lastSetHumanPoseInputLeftUpperLegInOutMuscle;
        public float LastSetHumanPoseInputRightUpperLegInOutMuscle => _lastSetHumanPoseInputRightUpperLegInOutMuscle;
        public float LastSetHumanPoseInputLeftUpperLegTwistInOutMuscle => _lastSetHumanPoseInputLeftUpperLegTwistInOutMuscle;
        public float LastSetHumanPoseInputRightUpperLegTwistInOutMuscle => _lastSetHumanPoseInputRightUpperLegTwistInOutMuscle;
        public float LastSetHumanPoseInputLeftLowerLegTwistInOutMuscle => _lastSetHumanPoseInputLeftLowerLegTwistInOutMuscle;
        public float LastSetHumanPoseInputRightLowerLegTwistInOutMuscle => _lastSetHumanPoseInputRightLowerLegTwistInOutMuscle;
        public float LastSetHumanPoseInputLeftFootTwistInOutMuscle => _lastSetHumanPoseInputLeftFootTwistInOutMuscle;
        public float LastSetHumanPoseInputRightFootTwistInOutMuscle => _lastSetHumanPoseInputRightFootTwistInOutMuscle;
        public float LastSetHumanPoseInputLeftToesUpDownMuscle => _lastSetHumanPoseInputLeftToesUpDownMuscle;
        public float LastSetHumanPoseInputRightToesUpDownMuscle => _lastSetHumanPoseInputRightToesUpDownMuscle;
        public float LastSetHumanPoseOutputRightUpperLegInOutMuscle => _lastSetHumanPoseOutputRightUpperLegInOutMuscle;
        public float LastSetHumanPoseRightUpperLegInOutDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightUpperLegInOutMuscle,
            _lastSetHumanPoseOutputRightUpperLegInOutMuscle);
        public float LastSetHumanPoseOutputRightUpperLegTwistInOutMuscle => _lastSetHumanPoseOutputRightUpperLegTwistInOutMuscle;
        public float LastSetHumanPoseRightUpperLegTwistInOutDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightUpperLegTwistInOutMuscle,
            _lastSetHumanPoseOutputRightUpperLegTwistInOutMuscle);
        public float LastSetHumanPoseOutputRightLowerLegTwistInOutMuscle => _lastSetHumanPoseOutputRightLowerLegTwistInOutMuscle;
        public float LastSetHumanPoseRightLowerLegTwistInOutDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightLowerLegTwistInOutMuscle,
            _lastSetHumanPoseOutputRightLowerLegTwistInOutMuscle);
        public float LastSetHumanPoseOutputRightFootTwistInOutMuscle => _lastSetHumanPoseOutputRightFootTwistInOutMuscle;
        public float LastSetHumanPoseRightFootTwistInOutDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightFootTwistInOutMuscle,
            _lastSetHumanPoseOutputRightFootTwistInOutMuscle);
        public float LastSetHumanPoseOutputRightToesUpDownMuscle => _lastSetHumanPoseOutputRightToesUpDownMuscle;
        public float LastSetHumanPoseRightToesUpDownDelta => CalculateFiniteAbsDelta(
            _lastSetHumanPoseInputRightToesUpDownMuscle,
            _lastSetHumanPoseOutputRightToesUpDownMuscle);
        public float LastRetargetStageGhostLeftFootWorldX => _lastRetargetStageGhostEndpointPositions.LeftFoot.x;
        public float LastRetargetStageGhostLeftFootWorldZ => _lastRetargetStageGhostEndpointPositions.LeftFoot.z;
        public float LastRetargetStageGhostLeftToesWorldX => _lastRetargetStageGhostEndpointPositions.LeftToes.x;
        public float LastRetargetStageGhostLeftToesWorldZ => _lastRetargetStageGhostEndpointPositions.LeftToes.z;
        public float LastRetargetStageGhostRightFootWorldX => _lastRetargetStageGhostEndpointPositions.RightFoot.x;
        public float LastRetargetStageGhostRightFootWorldZ => _lastRetargetStageGhostEndpointPositions.RightFoot.z;
        public float LastRetargetStageGhostRightToesWorldX => _lastRetargetStageGhostEndpointPositions.RightToes.x;
        public float LastRetargetStageGhostRightToesWorldZ => _lastRetargetStageGhostEndpointPositions.RightToes.z;
        public float LastRetargetStageAfterSetHumanPoseLeftFootWorldX => _lastRetargetStageAfterSetHumanPoseEndpointPositions.LeftFoot.x;
        public float LastRetargetStageAfterSetHumanPoseLeftFootWorldZ => _lastRetargetStageAfterSetHumanPoseEndpointPositions.LeftFoot.z;
        public float LastRetargetStageAfterSetHumanPoseLeftToesWorldX => _lastRetargetStageAfterSetHumanPoseEndpointPositions.LeftToes.x;
        public float LastRetargetStageAfterSetHumanPoseLeftToesWorldZ => _lastRetargetStageAfterSetHumanPoseEndpointPositions.LeftToes.z;
        public float LastRetargetStageAfterSetHumanPoseRightFootWorldX => _lastRetargetStageAfterSetHumanPoseEndpointPositions.RightFoot.x;
        public float LastRetargetStageAfterSetHumanPoseRightFootWorldZ => _lastRetargetStageAfterSetHumanPoseEndpointPositions.RightFoot.z;
        public float LastRetargetStageAfterSetHumanPoseRightToesWorldX => _lastRetargetStageAfterSetHumanPoseEndpointPositions.RightToes.x;
        public float LastRetargetStageAfterSetHumanPoseRightToesWorldZ => _lastRetargetStageAfterSetHumanPoseEndpointPositions.RightToes.z;
        public float LastRetargetStageAfterManualReferencesLeftFootWorldX => _lastRetargetStageAfterManualReferencesEndpointPositions.LeftFoot.x;
        public float LastRetargetStageAfterManualReferencesLeftFootWorldZ => _lastRetargetStageAfterManualReferencesEndpointPositions.LeftFoot.z;
        public float LastRetargetStageAfterManualReferencesLeftToesWorldX => _lastRetargetStageAfterManualReferencesEndpointPositions.LeftToes.x;
        public float LastRetargetStageAfterManualReferencesLeftToesWorldZ => _lastRetargetStageAfterManualReferencesEndpointPositions.LeftToes.z;
        public float LastRetargetStageAfterManualReferencesRightFootWorldX => _lastRetargetStageAfterManualReferencesEndpointPositions.RightFoot.x;
        public float LastRetargetStageAfterManualReferencesRightFootWorldZ => _lastRetargetStageAfterManualReferencesEndpointPositions.RightFoot.z;
        public float LastRetargetStageAfterManualReferencesRightToesWorldX => _lastRetargetStageAfterManualReferencesEndpointPositions.RightToes.x;
        public float LastRetargetStageAfterManualReferencesRightToesWorldZ => _lastRetargetStageAfterManualReferencesEndpointPositions.RightToes.z;
        public float LastRetargetStageAfterRootRestoreLeftFootWorldX => _lastRetargetStageAfterRootRestoreEndpointPositions.LeftFoot.x;
        public float LastRetargetStageAfterRootRestoreLeftFootWorldZ => _lastRetargetStageAfterRootRestoreEndpointPositions.LeftFoot.z;
        public float LastRetargetStageAfterRootRestoreLeftToesWorldX => _lastRetargetStageAfterRootRestoreEndpointPositions.LeftToes.x;
        public float LastRetargetStageAfterRootRestoreLeftToesWorldZ => _lastRetargetStageAfterRootRestoreEndpointPositions.LeftToes.z;
        public float LastRetargetStageAfterRootRestoreRightFootWorldX => _lastRetargetStageAfterRootRestoreEndpointPositions.RightFoot.x;
        public float LastRetargetStageAfterRootRestoreRightFootWorldZ => _lastRetargetStageAfterRootRestoreEndpointPositions.RightFoot.z;
        public float LastRetargetStageAfterRootRestoreRightToesWorldX => _lastRetargetStageAfterRootRestoreEndpointPositions.RightToes.x;
        public float LastRetargetStageAfterRootRestoreRightToesWorldZ => _lastRetargetStageAfterRootRestoreEndpointPositions.RightToes.z;
        public float LastRetargetStageAfterRootDeltaLeftFootWorldX => _lastRetargetStageAfterRootDeltaEndpointPositions.LeftFoot.x;
        public float LastRetargetStageAfterRootDeltaLeftFootWorldZ => _lastRetargetStageAfterRootDeltaEndpointPositions.LeftFoot.z;
        public float LastRetargetStageAfterRootDeltaLeftToesWorldX => _lastRetargetStageAfterRootDeltaEndpointPositions.LeftToes.x;
        public float LastRetargetStageAfterRootDeltaLeftToesWorldZ => _lastRetargetStageAfterRootDeltaEndpointPositions.LeftToes.z;
        public float LastRetargetStageAfterRootDeltaRightFootWorldX => _lastRetargetStageAfterRootDeltaEndpointPositions.RightFoot.x;
        public float LastRetargetStageAfterRootDeltaRightFootWorldZ => _lastRetargetStageAfterRootDeltaEndpointPositions.RightFoot.z;
        public float LastRetargetStageAfterRootDeltaRightToesWorldX => _lastRetargetStageAfterRootDeltaEndpointPositions.RightToes.x;
        public float LastRetargetStageAfterRootDeltaRightToesWorldZ => _lastRetargetStageAfterRootDeltaEndpointPositions.RightToes.z;
        public float LastRetargetStageAfterGroundingLeftFootWorldX => _lastRetargetStageAfterGroundingEndpointPositions.LeftFoot.x;
        public float LastRetargetStageAfterGroundingLeftFootWorldZ => _lastRetargetStageAfterGroundingEndpointPositions.LeftFoot.z;
        public float LastRetargetStageAfterGroundingLeftToesWorldX => _lastRetargetStageAfterGroundingEndpointPositions.LeftToes.x;
        public float LastRetargetStageAfterGroundingLeftToesWorldZ => _lastRetargetStageAfterGroundingEndpointPositions.LeftToes.z;
        public float LastRetargetStageAfterGroundingRightFootWorldX => _lastRetargetStageAfterGroundingEndpointPositions.RightFoot.x;
        public float LastRetargetStageAfterGroundingRightFootWorldZ => _lastRetargetStageAfterGroundingEndpointPositions.RightFoot.z;
        public float LastRetargetStageAfterGroundingRightToesWorldX => _lastRetargetStageAfterGroundingEndpointPositions.RightToes.x;
        public float LastRetargetStageAfterGroundingRightToesWorldZ => _lastRetargetStageAfterGroundingEndpointPositions.RightToes.z;
        public float LastRetargetStageAfterBipedIKLeftFootWorldX => _lastRetargetStageAfterBipedIKEndpointPositions.LeftFoot.x;
        public float LastRetargetStageAfterBipedIKLeftFootWorldZ => _lastRetargetStageAfterBipedIKEndpointPositions.LeftFoot.z;
        public float LastRetargetStageAfterBipedIKLeftToesWorldX => _lastRetargetStageAfterBipedIKEndpointPositions.LeftToes.x;
        public float LastRetargetStageAfterBipedIKLeftToesWorldZ => _lastRetargetStageAfterBipedIKEndpointPositions.LeftToes.z;
        public float LastRetargetStageAfterBipedIKRightFootWorldX => _lastRetargetStageAfterBipedIKEndpointPositions.RightFoot.x;
        public float LastRetargetStageAfterBipedIKRightFootWorldZ => _lastRetargetStageAfterBipedIKEndpointPositions.RightFoot.z;
        public float LastRetargetStageAfterBipedIKRightToesWorldX => _lastRetargetStageAfterBipedIKEndpointPositions.RightToes.x;
        public float LastRetargetStageAfterBipedIKRightToesWorldZ => _lastRetargetStageAfterBipedIKEndpointPositions.RightToes.z;
        public float LastRetargetStageAfterLateVisualGroundingLeftFootWorldX => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.LeftFoot.x;
        public float LastRetargetStageAfterLateVisualGroundingLeftFootWorldZ => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.LeftFoot.z;
        public float LastRetargetStageAfterLateVisualGroundingLeftToesWorldX => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.LeftToes.x;
        public float LastRetargetStageAfterLateVisualGroundingLeftToesWorldZ => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.LeftToes.z;
        public float LastRetargetStageAfterLateVisualGroundingRightFootWorldX => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.RightFoot.x;
        public float LastRetargetStageAfterLateVisualGroundingRightFootWorldZ => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.RightFoot.z;
        public float LastRetargetStageAfterLateVisualGroundingRightToesWorldX => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.RightToes.x;
        public float LastRetargetStageAfterLateVisualGroundingRightToesWorldZ => _lastRetargetStageAfterLateVisualGroundingEndpointPositions.RightToes.z;
        public string LastRetargetEndpointFirstJumpStage => _lastRetargetEndpointFirstJumpStage;
        public string LastRetargetEndpointFirstJumpEndpoint => _lastRetargetEndpointFirstJumpEndpoint;
        public float LastRetargetEndpointFirstJumpMagnitude => _lastRetargetEndpointFirstJumpMagnitude;
        public float LastRetargetEndpointFirstJumpDeltaX => _lastRetargetEndpointFirstJumpDelta.x;
        public float LastRetargetEndpointFirstJumpDeltaY => _lastRetargetEndpointFirstJumpDelta.y;
        public float LastRetargetEndpointFirstJumpDeltaZ => _lastRetargetEndpointFirstJumpDelta.z;
        public float LastEditorFootLocalRotationLeftFootXzDelta => _lastEditorFootLocalRotationLeftFootXzDelta;
        public float LastEditorFootLocalRotationRightFootXzDelta => _lastEditorFootLocalRotationRightFootXzDelta;
        public float LastEditorLowerBodySegmentDirectionLeftFootXzDelta => _lastEditorLowerBodySegmentDirectionLeftFootXzDelta;
        public float LastEditorLowerBodySegmentDirectionRightFootXzDelta => _lastEditorLowerBodySegmentDirectionRightFootXzDelta;
        public string LastEditorLowerBodySegmentDirectionMaxCorrectionSegment => _lastEditorLowerBodySegmentDirectionMaxCorrectionSegment;
        public float LastEditorLowerBodySegmentDirectionMaxCorrectionAngle => _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle;
        public float LastEditorLowerBodySegmentDirectionMaxPreAngle => _lastEditorLowerBodySegmentDirectionMaxPreAngle;
        public float LastEditorLowerBodySegmentDirectionMaxPostAngle => _lastEditorLowerBodySegmentDirectionMaxPostAngle;
        public float LastEditorLowerBodySegmentDirectionMaxCorrectionAxisX => _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis.x;
        public float LastEditorLowerBodySegmentDirectionMaxCorrectionAxisY => _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis.y;
        public float LastEditorLowerBodySegmentDirectionMaxCorrectionAxisZ => _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis.z;
        public float LastEditorLowerBodySegmentDirectionMaxReferenceDirectionX => _lastEditorLowerBodySegmentDirectionMaxReferenceDirection.x;
        public float LastEditorLowerBodySegmentDirectionMaxReferenceDirectionY => _lastEditorLowerBodySegmentDirectionMaxReferenceDirection.y;
        public float LastEditorLowerBodySegmentDirectionMaxReferenceDirectionZ => _lastEditorLowerBodySegmentDirectionMaxReferenceDirection.z;
        public float LastEditorLowerBodySegmentDirectionMaxPreDirectionX => _lastEditorLowerBodySegmentDirectionMaxPreDirection.x;
        public float LastEditorLowerBodySegmentDirectionMaxPreDirectionY => _lastEditorLowerBodySegmentDirectionMaxPreDirection.y;
        public float LastEditorLowerBodySegmentDirectionMaxPreDirectionZ => _lastEditorLowerBodySegmentDirectionMaxPreDirection.z;
        public float LastEditorLowerBodySegmentDirectionMaxPostDirectionX => _lastEditorLowerBodySegmentDirectionMaxPostDirection.x;
        public float LastEditorLowerBodySegmentDirectionMaxPostDirectionY => _lastEditorLowerBodySegmentDirectionMaxPostDirection.y;
        public float LastEditorLowerBodySegmentDirectionMaxPostDirectionZ => _lastEditorLowerBodySegmentDirectionMaxPostDirection.z;
        public float LastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle => _lastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle;
        public float LastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle => _lastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle => _lastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle => _lastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle;
        public float LastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle => _lastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle;
        public float LastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle => _lastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle => _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle => _lastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle => _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle => _lastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX => _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection.x;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY => _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection.y;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ => _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection.z;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX => _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection.x;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY => _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection.y;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ => _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection.z;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX => _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection.x;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY => _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection.y;
        public float LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ => _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection.z;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX => _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection.x;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY => _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection.y;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ => _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection.z;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionX => _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection.x;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionY => _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection.y;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ => _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection.z;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionX => _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection.x;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionY => _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection.y;
        public float LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ => _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection.z;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegWorldX => _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition.x;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegWorldY => _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition.y;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegWorldZ => _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition.z;
        public float LastEditorLowerBodySegmentDirectionLeftFootWorldX => _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition.x;
        public float LastEditorLowerBodySegmentDirectionLeftFootWorldY => _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition.y;
        public float LastEditorLowerBodySegmentDirectionLeftFootWorldZ => _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition.z;
        public float LastEditorLowerBodySegmentDirectionLeftToesWorldX => _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition.x;
        public float LastEditorLowerBodySegmentDirectionLeftToesWorldY => _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition.y;
        public float LastEditorLowerBodySegmentDirectionLeftToesWorldZ => _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition.z;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegWorldX => _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition.x;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegWorldY => _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition.y;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegWorldZ => _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition.z;
        public float LastEditorLowerBodySegmentDirectionRightFootWorldX => _lastEditorLowerBodySegmentDirectionRightFootWorldPosition.x;
        public float LastEditorLowerBodySegmentDirectionRightFootWorldY => _lastEditorLowerBodySegmentDirectionRightFootWorldPosition.y;
        public float LastEditorLowerBodySegmentDirectionRightFootWorldZ => _lastEditorLowerBodySegmentDirectionRightFootWorldPosition.z;
        public float LastEditorLowerBodySegmentDirectionRightToesWorldX => _lastEditorLowerBodySegmentDirectionRightToesWorldPosition.x;
        public float LastEditorLowerBodySegmentDirectionRightToesWorldY => _lastEditorLowerBodySegmentDirectionRightToesWorldPosition.y;
        public float LastEditorLowerBodySegmentDirectionRightToesWorldZ => _lastEditorLowerBodySegmentDirectionRightToesWorldPosition.z;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX => _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis.x;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY => _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis.y;
        public float LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ => _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis.z;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX => _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis.x;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY => _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis.y;
        public float LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ => _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis.z;
        public float LastEditorLowerBodySegmentDirectionLeftFootForwardX => _lastEditorLowerBodySegmentDirectionLeftFootForward.x;
        public float LastEditorLowerBodySegmentDirectionLeftFootForwardY => _lastEditorLowerBodySegmentDirectionLeftFootForward.y;
        public float LastEditorLowerBodySegmentDirectionLeftFootForwardZ => _lastEditorLowerBodySegmentDirectionLeftFootForward.z;
        public float LastEditorLowerBodySegmentDirectionLeftFootUpX => _lastEditorLowerBodySegmentDirectionLeftFootUp.x;
        public float LastEditorLowerBodySegmentDirectionLeftFootUpY => _lastEditorLowerBodySegmentDirectionLeftFootUp.y;
        public float LastEditorLowerBodySegmentDirectionLeftFootUpZ => _lastEditorLowerBodySegmentDirectionLeftFootUp.z;
        public float LastEditorLowerBodySegmentDirectionRightFootForwardX => _lastEditorLowerBodySegmentDirectionRightFootForward.x;
        public float LastEditorLowerBodySegmentDirectionRightFootForwardY => _lastEditorLowerBodySegmentDirectionRightFootForward.y;
        public float LastEditorLowerBodySegmentDirectionRightFootForwardZ => _lastEditorLowerBodySegmentDirectionRightFootForward.z;
        public float LastEditorLowerBodySegmentDirectionRightFootUpX => _lastEditorLowerBodySegmentDirectionRightFootUp.x;
        public float LastEditorLowerBodySegmentDirectionRightFootUpY => _lastEditorLowerBodySegmentDirectionRightFootUp.y;
        public float LastEditorLowerBodySegmentDirectionRightFootUpZ => _lastEditorLowerBodySegmentDirectionRightFootUp.z;
        public float LastEditorFootHipsAlignedResidualYawLeftFootXzDelta => _lastEditorFootHipsAlignedResidualYawLeftFootXzDelta;
        public float LastEditorFootHipsAlignedResidualYawRightFootXzDelta => _lastEditorFootHipsAlignedResidualYawRightFootXzDelta;
        public float LastPostSetHumanPoseRightEndpointDesiredFootWorldX => _lastPostSetHumanPoseRightEndpointDesiredFootWorldPosition.x;
        public float LastPostSetHumanPoseRightEndpointDesiredFootWorldZ => _lastPostSetHumanPoseRightEndpointDesiredFootWorldPosition.z;
        public float LastPostSetHumanPoseRightEndpointDesiredToesWorldX => _lastPostSetHumanPoseRightEndpointDesiredToesWorldPosition.x;
        public float LastPostSetHumanPoseRightEndpointDesiredToesWorldZ => _lastPostSetHumanPoseRightEndpointDesiredToesWorldPosition.z;
        public float LastPostSetHumanPoseRightEndpointCurrentFootWorldX => _lastPostSetHumanPoseRightEndpointCurrentFootWorldPosition.x;
        public float LastPostSetHumanPoseRightEndpointCurrentFootWorldZ => _lastPostSetHumanPoseRightEndpointCurrentFootWorldPosition.z;
        public float LastPostSetHumanPoseRightEndpointCurrentToesWorldX => _lastPostSetHumanPoseRightEndpointCurrentToesWorldPosition.x;
        public float LastPostSetHumanPoseRightEndpointCurrentToesWorldZ => _lastPostSetHumanPoseRightEndpointCurrentToesWorldPosition.z;
        public float LastPostSetHumanPoseRightEndpointDeltaBeforeClampX => _lastPostSetHumanPoseRightEndpointDeltaBeforeClamp.x;
        public float LastPostSetHumanPoseRightEndpointDeltaBeforeClampZ => _lastPostSetHumanPoseRightEndpointDeltaBeforeClamp.z;
        public float LastPostSetHumanPoseRightEndpointDeltaAfterClampX => _lastPostSetHumanPoseRightEndpointDeltaAfterClamp.x;
        public float LastPostSetHumanPoseRightEndpointDeltaAfterClampZ => _lastPostSetHumanPoseRightEndpointDeltaAfterClamp.z;
        public float LastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScaleX => _lastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScale.x;
        public float LastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScaleZ => _lastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScale.z;
        public float LastPostSetHumanPoseRightEndpointCorrectionX => _lastPostSetHumanPoseRightEndpointCorrection.x;
        public float LastPostSetHumanPoseRightEndpointCorrectionZ => _lastPostSetHumanPoseRightEndpointCorrection.z;
        public float LastPostSetHumanPoseRightEndpointNextFootWorldX => _lastPostSetHumanPoseRightEndpointNextFootWorldPosition.x;
        public float LastPostSetHumanPoseRightEndpointNextFootWorldZ => _lastPostSetHumanPoseRightEndpointNextFootWorldPosition.z;
        public float LastPostSetHumanPoseRightEndpointMaxYawAngle => _lastPostSetHumanPoseRightEndpointMaxYawAngle;
        public float LastPostSetHumanPoseRightEndpointYawCorrectionAngle => _lastPostSetHumanPoseRightEndpointYawCorrectionAngle;
        public float LastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle => _lastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle;
        public float LastPostSetHumanPoseRightEndpointApplied => _lastPostSetHumanPoseRightEndpointApplied;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled => _lastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffsetX => _lastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffset.x;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffsetZ => _lastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffset.z;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDeltaX => _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta.x;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDeltaZ => _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta.z;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedMagnitude => _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta.magnitude;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDeltaX => _lastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDelta.x;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDeltaZ => _lastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDelta.z;
        public float LastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude => _lastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude;

        public void ResetPlaybackStabilityMetrics()
        {
            _legacyAnimationDriver.ResetStabilityMetrics();
            ResetEditorHumanoidRootTranslationReferenceState();
            ResetTargetHipsLocalPositionSpikeState();
            ResetVisualPoseHistory();
            _poseVisualSmoothingCount = 0;
            _poseVisualMuscleDeltaOnlySkippedCount = 0;
            _lastPoseVisualMaxMuscleDelta = float.NaN;
            _maxPoseVisualMaxMuscleDelta = 0f;
            _maxGroundingAdjustment = 0f;
            _groundingStepClampedCount = 0;
            _groundingSmoothedCount = 0;
            _maxGroundingVerticalStep = 0f;
            _maxGroundingVerticalStepAfterInitial = 0f;
            _lastEditorFootHeightGroundingReferenceLift = float.NaN;
            _hasEditorReferenceLowestFootRestY = false;
            _allowEditorFootHeightGroundingReference = true;
            ResetRetargetPoseStageDiagnostics();
        }

        public bool PrepareRecordingStartPose(float startTimeSeconds, float playbackSpeed, bool holdPose)
        {
            if (!_legacyAnimationDriver.TryPrepareRecordingStartPose(startTimeSeconds, playbackSpeed, holdPose))
            {
                return false;
            }

            ResetVisualPoseHistory();
            return true;
        }

        public void CaptureRecordingStartBaselineSnapshot()
        {
            ResetRecordingStartHipsBaselineDiagnostics();
            _recordingStartHipsReferenceStage = RecordingStartHipsReferenceStagePrewarmComplete;
            if (targetAnimator == null)
            {
                return;
            }

            Vector3 rootPosition = targetAnimator.transform.position;
            _recordingStartRootY = IsFinite(rootPosition) ? rootPosition.y : float.NaN;
            _recordingStartBodyPositionY = TryGetTargetBodyPositionY(out float bodyPositionY)
                ? bodyPositionY
                : float.NaN;

            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (targetHips != null)
            {
                Vector3 hipsLocalPosition = targetHips.localPosition;
                Vector3 hipsPosition = targetHips.position;
                _recordingStartHipsLocalY = IsFinite(hipsLocalPosition) ? hipsLocalPosition.y : float.NaN;
                _recordingStartHipsY = IsFinite(hipsPosition) ? hipsPosition.y : float.NaN;
            }

            _recordingStartHipsReferenceBeforeLocalY = _lastEditorHipsLocalReferenceBeforeLocalY;
            _recordingStartHipsReferenceAfterLocalY = _lastEditorHipsLocalReferenceAfterLocalY;
            _recordingStartHipsReferenceDeltaY = _lastEditorHipsLocalReferenceDeltaY;
            _recordingStartHipsReferenceFlipDetected = IsRecordingStartHipsBaselineFlip(
                _recordingStartHipsReferenceBeforeLocalY,
                _recordingStartHipsReferenceAfterLocalY,
                RecordingStartHipsBaselineFlipWarningThreshold);
        }

        [Tooltip("Target Humanoid 본의 localPosition을 초기값으로 되돌려 팔/다리 길이 변형을 막습니다.")]
        [FormerlySerializedAs("ShouldLockTargetHumanoidBonePositions")]
        [SerializeField] private bool _ShouldLockTargetHumanoidBonePositions= true;
        public bool ShouldLockTargetHumanoidBonePositions { get => _ShouldLockTargetHumanoidBonePositions; set => _ShouldLockTargetHumanoidBonePositions = value; }

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
        private Vector3 _targetRootPoseGuardAnchorPosition;
        private bool _hasTargetRootPoseGuardAnchorPosition;
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
        private Vector3 _targetHipsRestLocalPosition;
        private bool _hasTargetHipsRestLocalPosition;
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
        private Vector3 _previousTargetHipsLocalPosition;
        private bool _hasPreviousTargetHipsLocalPosition;
        private float _lastTargetHipsLocalPositionDelta = float.NaN;
        private float _maxTargetHipsLocalPositionDelta;
        private int _targetHipsLocalPositionSpikeClampedCount;
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
        private float _lastEditorFootHeightGroundingReferenceLift = float.NaN;
        private float _lastEditorHipsLocalReferenceBeforeLocalY = float.NaN;
        private float _lastEditorHipsLocalReferenceAfterLocalY = float.NaN;
        private float _lastEditorHipsLocalReferenceDeltaY = float.NaN;
        private float _recordingStartRootY = float.NaN;
        private float _recordingStartBodyPositionY = float.NaN;
        private float _recordingStartHipsLocalY = float.NaN;
        private float _recordingStartHipsY = float.NaN;
        private float _recordingStartHipsReferenceBeforeLocalY = float.NaN;
        private float _recordingStartHipsReferenceAfterLocalY = float.NaN;
        private float _recordingStartHipsReferenceDeltaY = float.NaN;
        private bool _recordingStartHipsReferenceFlipDetected;
        private string _recordingStartHipsReferenceStage = string.Empty;
        private bool _lateVisualGroundingWarningLogged;
        private bool _rendererGroundingOutlierWarningLogged;
        private bool _lateVisualGroundingInitialized;
        private bool _hasFrozenGroundingRootY;
        private float _frozenGroundingRootY;
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
        private float _lastPoseInputLeftShoulderFrontBackMuscle = float.NaN;
        private float _lastAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle = float.NaN;
        private float _lastAfterClampPoseMusclesLeftShoulderFrontBackMuscle = float.NaN;
        private float _lastAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle = float.NaN;
        private float _lastAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftShoulderFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseOutputLeftShoulderFrontBackMuscle = float.NaN;
        private float _lastPoseInputLeftArmTwistMuscle = float.NaN;
        private float _lastAfterEditorMuscleReferenceLeftArmTwistMuscle = float.NaN;
        private float _lastAfterClampPoseMusclesLeftArmTwistMuscle = float.NaN;
        private float _lastAfterAnatomicalArmGuardLeftArmTwistMuscle = float.NaN;
        private float _lastAfterVisualSpikeSmoothingLeftArmTwistMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftArmTwistMuscle = float.NaN;
        private float _lastSetHumanPoseOutputLeftArmTwistMuscle = float.NaN;
        private float _lastPoseInputLeftForearmStretchMuscle = float.NaN;
        private float _lastAfterEditorMuscleReferenceLeftForearmStretchMuscle = float.NaN;
        private float _lastAfterClampPoseMusclesLeftForearmStretchMuscle = float.NaN;
        private float _lastAfterAnatomicalArmGuardLeftForearmStretchMuscle = float.NaN;
        private float _lastAfterVisualSpikeSmoothingLeftForearmStretchMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftForearmStretchMuscle = float.NaN;
        private float _lastSetHumanPoseOutputLeftForearmStretchMuscle = float.NaN;
        private float _lastPoseInputRightForearmStretchMuscle = float.NaN;
        private float _lastAfterEditorMuscleReferenceRightForearmStretchMuscle = float.NaN;
        private float _lastAfterClampPoseMusclesRightForearmStretchMuscle = float.NaN;
        private float _lastAfterAnatomicalArmGuardRightForearmStretchMuscle = float.NaN;
        private float _lastAfterVisualSpikeSmoothingRightForearmStretchMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightForearmStretchMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightForearmStretchMuscle = float.NaN;
        private float _lastPoseInputRightArmTwistMuscle = float.NaN;
        private float _lastAfterEditorMuscleReferenceRightArmTwistMuscle = float.NaN;
        private float _lastAfterClampPoseMusclesRightArmTwistMuscle = float.NaN;
        private float _lastAfterAnatomicalArmGuardRightArmTwistMuscle = float.NaN;
        private float _lastAfterVisualSpikeSmoothingRightArmTwistMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightArmTwistMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightArmTwistMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftUpperLegFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseOutputLeftUpperLegFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightUpperLegFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightUpperLegFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftLowerLegStretchMuscle = float.NaN;
        private float _lastSetHumanPoseOutputLeftLowerLegStretchMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightLowerLegStretchMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightLowerLegStretchMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftFootUpDownMuscle = float.NaN;
        private float _lastSetHumanPoseOutputLeftFootUpDownMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightFootUpDownMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightFootUpDownMuscle = float.NaN;
        private Vector3 _lastSetHumanPoseInputBodyPosition = BuildNaNVector3();
        private Vector3 _lastSetHumanPoseOutputBodyPosition = BuildNaNVector3();
        private Quaternion _lastSetHumanPoseInputBodyRotation = BuildNaNQuaternion();
        private Quaternion _lastSetHumanPoseOutputBodyRotation = BuildNaNQuaternion();
        private Vector3 _lastSetHumanPosePreSolveGhostRootWorldPosition = BuildNaNVector3();
        private Quaternion _lastSetHumanPosePreSolveGhostRootWorldRotation = BuildNaNQuaternion();
        private Vector3 _lastSetHumanPosePreSolveTargetRootWorldPosition = BuildNaNVector3();
        private Quaternion _lastSetHumanPosePreSolveTargetRootWorldRotation = BuildNaNQuaternion();
        private Vector3 _lastSetHumanPosePreSolveTargetHipsWorldPosition = BuildNaNVector3();
        private Vector3 _lastSetHumanPosePreSolveTargetHipsLocalPosition = BuildNaNVector3();
        private Vector3 _lastSetHumanPosePreSolveBodyPosition = BuildNaNVector3();
        private Quaternion _lastSetHumanPosePreSolveBodyRotation = BuildNaNQuaternion();
        private Vector3 _lastPreSetHumanPoseEndpointBodyPositionBefore = BuildNaNVector3();
        private Vector3 _lastPreSetHumanPoseEndpointBodyPositionAfter = BuildNaNVector3();
        private Vector3 _lastPreSetHumanPoseEndpointBodyPositionDelta = BuildNaNVector3();
        private float _lastSetHumanPoseInputSpineFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseInputSpineLeftRightMuscle = float.NaN;
        private float _lastSetHumanPoseInputSpineTwistLeftRightMuscle = float.NaN;
        private float _lastSetHumanPoseInputChestFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseInputChestLeftRightMuscle = float.NaN;
        private float _lastSetHumanPoseInputChestTwistLeftRightMuscle = float.NaN;
        private float _lastSetHumanPoseInputUpperChestFrontBackMuscle = float.NaN;
        private float _lastSetHumanPoseInputUpperChestLeftRightMuscle = float.NaN;
        private float _lastSetHumanPoseInputUpperChestTwistLeftRightMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftUpperLegInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightUpperLegInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftUpperLegTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightUpperLegTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftLowerLegTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightLowerLegTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftFootTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightFootTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseInputLeftToesUpDownMuscle = float.NaN;
        private float _lastSetHumanPoseInputRightToesUpDownMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightUpperLegInOutMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightUpperLegTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightLowerLegTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightFootTwistInOutMuscle = float.NaN;
        private float _lastSetHumanPoseOutputRightToesUpDownMuscle = float.NaN;
        private float _lastEditorFootLocalRotationLeftFootXzDelta = float.NaN;
        private float _lastEditorFootLocalRotationRightFootXzDelta = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionLeftFootXzDelta = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionRightFootXzDelta = float.NaN;
        private string _lastEditorLowerBodySegmentDirectionMaxCorrectionSegment = string.Empty;
        private float _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionMaxPreAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionMaxPostAngle = float.NaN;
        private Vector3 _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionMaxReferenceDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionMaxPreDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionMaxPostDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private float _lastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN;
        private float _lastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN;
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightFootWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightToesWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftFootForward = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionLeftFootUp = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightFootForward = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastEditorLowerBodySegmentDirectionRightFootUp = new Vector3(float.NaN, float.NaN, float.NaN);
        private float _lastEditorFootHipsAlignedResidualYawLeftFootXzDelta = float.NaN;
        private float _lastEditorFootHipsAlignedResidualYawRightFootXzDelta = float.NaN;
        private Vector3 _lastPostSetHumanPoseRightEndpointDesiredFootWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointDesiredToesWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointCurrentFootWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointCurrentToesWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointDeltaBeforeClamp = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointDeltaAfterClamp = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScale = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointCorrection = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointNextFootWorldPosition = new Vector3(float.NaN, float.NaN, float.NaN);
        private float _lastPostSetHumanPoseRightEndpointMaxYawAngle = float.NaN;
        private float _lastPostSetHumanPoseRightEndpointYawCorrectionAngle = float.NaN;
        private float _lastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle = float.NaN;
        private float _lastPostSetHumanPoseRightEndpointApplied = float.NaN;
        private float _lastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled = float.NaN;
        private Vector3 _lastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffset = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDelta = new Vector3(float.NaN, float.NaN, float.NaN);
        private float _lastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude = float.NaN;
        private bool _hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset;
        private Vector3 _postSetHumanPoseRightFootEvaluatorXzFirstOffset = new Vector3(float.NaN, float.NaN, float.NaN);
        private RetargetEndpointStageWorldPositions _lastRetargetStageGhostEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastSetHumanPosePreSolveGhostEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastSetHumanPosePreSolveCurrentEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastSetHumanPosePreSolveTargetEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastRetargetStageAfterSetHumanPoseEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastRetargetStageAfterManualReferencesEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastRetargetStageAfterRootRestoreEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastRetargetStageAfterRootDeltaEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastRetargetStageAfterGroundingEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastRetargetStageAfterBipedIKEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private RetargetEndpointStageWorldPositions _lastRetargetStageAfterLateVisualGroundingEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
        private string _lastRetargetEndpointFirstJumpStage = "";
        private string _lastRetargetEndpointFirstJumpEndpoint = "";
        private Vector3 _lastRetargetEndpointFirstJumpDelta = BuildNaNVector3();
        private float _lastRetargetEndpointFirstJumpMagnitude = float.NaN;
        private int _setHumanPoseLeftShoulderFrontBackMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftArmTwistMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftForearmStretchMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightForearmStretchMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightArmTwistMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftUpperLegFrontBackMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightUpperLegFrontBackMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftLowerLegStretchMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightLowerLegStretchMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftFootUpDownMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightFootUpDownMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseSpineFrontBackMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseSpineLeftRightMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseSpineTwistLeftRightMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseChestFrontBackMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseChestLeftRightMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseChestTwistLeftRightMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseUpperChestFrontBackMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseUpperChestLeftRightMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseUpperChestTwistLeftRightMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftUpperLegInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightUpperLegInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftUpperLegTwistInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightUpperLegTwistInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftLowerLegTwistInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightLowerLegTwistInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftFootTwistInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightFootTwistInOutMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseLeftToesUpDownMuscleIndex = UnresolvedHumanMuscleIndex;
        private int _setHumanPoseRightToesUpDownMuscleIndex = UnresolvedHumanMuscleIndex;
        private bool _hasEstimatedFootRadius;
        private float _estimatedFootRadius = DefaultFootRadius;
        private const float DefaultFootRadius = 0.04f;
        private const float UpperArmTwistReferenceSignMagnitudeTolerance = 0.35f;
        private const float UpperArmTwistOverrangeReferenceSignMagnitudeTolerance = 1.5f;
        private const float UpperArmTwistReferenceSignMaxAbs = 2.25f;
        private const float RightUpperArmTwistReferenceSignMinAbs = 2f;
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
        private bool _hasEditorReferenceLowestFootRestY;
        private float _editorReferenceLowestFootRestY;
        private bool _allowEditorFootHeightGroundingReference;
        private bool _editorHandLocalRotationReferenceLogged;
        private bool _editorFootLocalRotationReferenceLogged;
        private bool _editorLowerBodySegmentDirectionReferenceLogged;
        private bool _editorFootHipsAlignedResidualYawReferenceLogged;
        private bool _editorThumbLocalRotationReferenceLogged;
        private bool _editorThumbSegmentDirectionReferenceLogged;
        private bool _editorHandPalmFrameReferenceLogged;
        private bool _editorThumbBasePositionReferenceLogged;
        private bool _editorHipsLocalPositionReferenceLogged;
        private bool _editorBodyPositionXzReferenceLogged;
        private bool _editorFootIkPositionReferenceLogged;
        private BipedIK _editorManualFootBipedIk;
        private bool _editorManualFootBipedIkCreated;
        private bool _editorManualFootBipedIkInitiated;
#else
        private Animator _editorFingerReferenceAnimator;
        private bool _useEditorFingerPoseReference;
        private bool _hasEditorReferenceBodyPosition;
        private Vector3 _editorReferenceBodyPosition;
        private bool _hasEditorReferenceLowestFootRestY;
        private bool _allowEditorFootHeightGroundingReference;
#endif

        // --- 초기화 ---
        private bool _isInitialized = false;
        private readonly LegacyAnimationDriver _legacyAnimationDriver = new LegacyAnimationDriver();

        public void Initialize(RetargetingContext context, RetargetingSettings settings)
        {
            GameObject ghostRoot = context.GhostRoot;
            GameObject targetRoot = context.TargetRoot;
            AnimationClip clip = context.Clip;

            _legacyAnimationDriver.Dispose();

            ghostAnimator = ghostRoot.GetComponent<Animator>();
            targetAnimator = targetRoot.GetComponent<Animator>();
            CaptureTargetInitialTransforms(targetRoot);
            _targetRootPoseGuardAnchorPosition = targetAnimator != null ? targetAnimator.transform.position : Vector3.zero;
            _hasTargetRootPoseGuardAnchorPosition = targetAnimator != null && IsFinite(_targetRootPoseGuardAnchorPosition);

            if (clip == null) return;

            _legacyAnimationDriver.Initialize(ghostRoot, ghostAnimator, clip);

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
            ResetEditorHumanoidRootTranslationReferenceState();
            ResetRecordingStartHipsBaselineDiagnostics();
            ResetLastEditorHipsLocalReferenceDiagnostics();
            CacheInitialHipHeights();
            _hasEditorReferenceLowestFootRestY = false;
            _allowEditorFootHeightGroundingReference = false;
            _facingCorrection = settings != null && settings.ShouldUseLegacyPoseSpaceFacingCorrection
                ? LegacyFacingCorrection
                : Quaternion.Inverse(ghostAnimator.transform.rotation) * targetAnimator.transform.rotation;
            _poseRootRotationCorrection = Quaternion.identity;
            _hasPoseRootRotationCorrection = false;
            if (settings != null)
            {
                groundOffset = settings.HeightOffset;
                _movementScaleMultiplier = RootMotionGuard.NormalizeMovementScaleMultiplier(settings.MovementScaleMultiplier);
                ShouldPreserveFbxRootRotation = settings.ShouldPreserveFbxRootRotation && !settings.ShouldUseLegacyPoseSpaceFacingCorrection;
                preserveTargetBodyPosition = settings.ShouldPreserveRetargetBodyPosition;
                useBodyPositionXZRootMotion = settings.ShouldUseRetargetBodyPositionXZRootMotion;
                ShouldUseEditorHumanoidRootTranslationReference = settings.ShouldUseEditorHumanoidRootTranslationReference;
                editorHumanoidRootTranslationWeight = Mathf.Clamp01(settings.editorHumanoidRootTranslationWeight);
                editorHumanoidRootTranslationCurrentWeight = Mathf.Clamp(settings.editorHumanoidRootTranslationCurrentWeight, 0.05f, 1f);
                ShouldStabilizeGroundedFootXZ = settings.ShouldStabilizeGroundedFootXZ;
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
                ShouldUseManualAnimatorFullBodyPoseReference = settings.ShouldUseManualAnimatorFullBodyPoseReference;
                manualAnimatorFullBodyPoseReferenceWeight = Mathf.Clamp01(settings.manualAnimatorFullBodyPoseReferenceWeight);
                ShouldExcludeManualAnimatorFullBodyLowerMuscles =
                    settings.ShouldExcludeManualAnimatorFullBodyLowerMuscles;
                ShouldApplyManualAnimatorFullBodyLowerMusclesOnly =
                    settings.ShouldApplyManualAnimatorFullBodyLowerMusclesOnly;
                ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly =
                    settings.ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly;
                manualAnimatorFullBodyPoseRightArmMusclesOnly =
                    settings.manualAnimatorFullBodyPoseRightArmMusclesOnly;
                manualAnimatorFullBodyPoseLeftArmMusclesOnly =
                    settings.manualAnimatorFullBodyPoseLeftArmMusclesOnly;
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly =
                    settings.manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly;
                manualAnimatorFullBodyPoseFrameGateStart =
                    Mathf.Max(0f, settings.manualAnimatorFullBodyPoseFrameGateStart);
                manualAnimatorFullBodyPoseFrameGateEnd =
                    Mathf.Max(0f, settings.manualAnimatorFullBodyPoseFrameGateEnd);
                ShouldUseSetHumanPoseRightLegTwistOutputReference =
                    settings.ShouldUseSetHumanPoseRightLegTwistOutputReference;
                setHumanPoseRightLegTwistOutputReferenceWeight =
                    Mathf.Clamp01(settings.setHumanPoseRightLegTwistOutputReferenceWeight);
                setHumanPoseRightLegTwistOutputReferenceMaxDelta =
                    Mathf.Max(0f, settings.setHumanPoseRightLegTwistOutputReferenceMaxDelta);
                useManualAnimatorThumbLocalRotationReference = settings.useManualAnimatorThumbLocalRotationReference;
                useManualAnimatorHandLocalRotationReference = settings.useManualAnimatorHandLocalRotationReference;
                useManualAnimatorThumbSegmentDirectionReference = settings.useManualAnimatorThumbSegmentDirectionReference;
                manualAnimatorThumbSegmentDirectionWeight = settings.manualAnimatorThumbSegmentDirectionWeight;
                useManualAnimatorThumbHandDirectionReference = settings.useManualAnimatorThumbHandDirectionReference;
                manualAnimatorThumbHandDirectionWeight = settings.manualAnimatorThumbHandDirectionWeight;
                useManualAnimatorHandPalmFrameReference = settings.useManualAnimatorHandPalmFrameReference;
                manualAnimatorHandPalmFrameWeight = settings.manualAnimatorHandPalmFrameWeight;
                useManualAnimatorThumbBasePositionReference = settings.useManualAnimatorThumbBasePositionReference;
                ShouldUseManualAnimatorHipsLocalPositionReference = settings.ShouldUseManualAnimatorHipsLocalPositionReference;
                ShouldUseManualAnimatorBodyRotationReference = settings.ShouldUseManualAnimatorBodyRotationReference;
                manualAnimatorBodyRotationReferenceWeight = Mathf.Clamp01(settings.manualAnimatorBodyRotationReferenceWeight);
                ShouldUseManualAnimatorBodyPositionYReference = settings.ShouldUseManualAnimatorBodyPositionYReference;
                ShouldUseManualAnimatorBodyPositionXzReference = settings.ShouldUseManualAnimatorBodyPositionXzReference;
                manualAnimatorBodyPositionXzReferenceWeight =
                    Mathf.Clamp01(settings.manualAnimatorBodyPositionXzReferenceWeight);
                manualAnimatorBodyPositionXzReferenceMaxOffset =
                    Mathf.Max(0f, settings.manualAnimatorBodyPositionXzReferenceMaxOffset);
                manualAnimatorBodyPositionXzReferenceFrameGateStart =
                    Mathf.Max(0f, settings.manualAnimatorBodyPositionXzReferenceFrameGateStart);
                manualAnimatorBodyPositionXzReferenceFrameGateEnd =
                    Mathf.Max(0f, settings.manualAnimatorBodyPositionXzReferenceFrameGateEnd);
                manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames =
                    Mathf.Max(0f, settings.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames);
                manualAnimatorBodyPositionXzReferenceAxisXScale =
                    Mathf.Clamp01(settings.manualAnimatorBodyPositionXzReferenceAxisXScale);
                manualAnimatorBodyPositionXzReferenceAxisZScale =
                    Mathf.Clamp01(settings.manualAnimatorBodyPositionXzReferenceAxisZScale);
                manualAnimatorHipsLocalPositionWeight = Mathf.Clamp01(settings.manualAnimatorHipsLocalPositionWeight);
                manualAnimatorHipsLocalPositionMaxOffset = Mathf.Max(0.001f, settings.manualAnimatorHipsLocalPositionMaxOffset);
                ShouldUseManualAnimatorFootHeightGroundingReference = settings.ShouldUseManualAnimatorFootHeightGroundingReference;
                manualAnimatorFootHeightGroundingReferenceWeight = Mathf.Clamp01(settings.manualAnimatorFootHeightGroundingReferenceWeight);
                manualAnimatorFootHeightGroundingReferenceMaxLift = Mathf.Max(0f, settings.manualAnimatorFootHeightGroundingReferenceMaxLift);
                ShouldUseManualAnimatorFootLocalRotationReference = settings.ShouldUseManualAnimatorFootLocalRotationReference;
                manualAnimatorFootLocalRotationReferenceWeight = Mathf.Clamp01(settings.manualAnimatorFootLocalRotationReferenceWeight);
                ShouldUseManualAnimatorLowerBodySegmentDirectionReference = settings.ShouldUseManualAnimatorLowerBodySegmentDirectionReference;
                manualAnimatorLowerBodySegmentDirectionReferenceWeight = Mathf.Clamp01(settings.manualAnimatorLowerBodySegmentDirectionReferenceWeight);
                manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = Mathf.Max(0f, settings.manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
                ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference =
                    settings.ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference;
                manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle =
                    Mathf.Max(0f, settings.manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
                ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference =
                    settings.ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference;
                manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    Mathf.Max(0f, settings.manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
                manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    Mathf.Max(0f, settings.manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle =
                    Mathf.Max(0f, settings.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle);
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale =
                    Mathf.Clamp01(settings.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale);
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight =
                    Mathf.Clamp01(settings.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart =
                    Mathf.Max(0f, settings.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd =
                    Mathf.Max(0f, settings.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
                manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight =
                    Mathf.Clamp01(settings.manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
                ShouldDisableManualAnimatorFootToToesSegmentDirectionReference =
                    settings.ShouldDisableManualAnimatorFootToToesSegmentDirectionReference;
                manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle =
                    Mathf.Max(0f, settings.manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
                ShouldUseManualAnimatorFootHipsAlignedResidualYawReference = settings.ShouldUseManualAnimatorFootHipsAlignedResidualYawReference;
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight = Mathf.Clamp01(settings.manualAnimatorFootHipsAlignedResidualYawReferenceWeight);
                manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = Mathf.Max(0f, settings.manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
                useManualAnimatorBipedIkFootPositionReference = settings.useManualAnimatorBipedIkFootPositionReference;
                manualAnimatorBipedIkFootPositionReferenceWeight = Mathf.Clamp01(settings.manualAnimatorBipedIkFootPositionReferenceWeight);
                manualAnimatorBipedIkFootPositionReferenceMaxOffset = Mathf.Max(0f, settings.manualAnimatorBipedIkFootPositionReferenceMaxOffset);
                usePostSetHumanPoseRightEndpointPositionReference =
                    settings.usePostSetHumanPoseRightEndpointPositionReference;
                postSetHumanPoseRightEndpointPositionReferenceWeight =
                    Mathf.Clamp01(settings.postSetHumanPoseRightEndpointPositionReferenceWeight);
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    Mathf.Max(0f, settings.postSetHumanPoseRightEndpointPositionReferenceMaxOffset);
                postSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    Mathf.Clamp01(settings.postSetHumanPoseRightEndpointPositionReferencePositiveZScale);
                postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    Mathf.Clamp01(settings.postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
                postSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    Mathf.Max(0f, settings.postSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
                postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    Mathf.Max(0f, settings.postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
                ShouldUseLeftSideForPostSetHumanPoseEndpointPosition =
                    settings.ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
                usePostSetHumanPoseRightFootEvaluatorXzReference =
                    settings.usePostSetHumanPoseRightFootEvaluatorXzReference;
                postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude =
                    Mathf.Max(0f, settings.postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude);
                usePreSetHumanPoseRightEndpointPositionReference =
                    settings.usePreSetHumanPoseRightEndpointPositionReference;
                preSetHumanPoseRightEndpointPositionReferenceWeight =
                    Mathf.Clamp01(settings.preSetHumanPoseRightEndpointPositionReferenceWeight);
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset =
                    Mathf.Max(0f, settings.preSetHumanPoseRightEndpointPositionReferenceMaxOffset);
                preSetHumanPoseRightEndpointPositionReferencePositiveZScale =
                    Mathf.Clamp01(settings.preSetHumanPoseRightEndpointPositionReferencePositiveZScale);
                preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight =
                    Mathf.Clamp01(settings.preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight);
                preSetHumanPoseRightEndpointPositionReferenceFrameGateStart =
                    Mathf.Max(0f, settings.preSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
                preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd =
                    Mathf.Max(0f, settings.preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
                ShouldUseLeftSideForPreSetHumanPoseEndpointPosition =
                    settings.ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
                preSetHumanPoseEndpointPositionUseGhostCurrentBasis =
                    settings.preSetHumanPoseEndpointPositionUseGhostCurrentBasis;
                ShouldInvertPreSetHumanPoseEndpointPositionBodyX =
                    settings.ShouldInvertPreSetHumanPoseEndpointPositionBodyX;
                ShouldInvertPreSetHumanPoseEndpointPositionBodyZ =
                    settings.ShouldInvertPreSetHumanPoseEndpointPositionBodyZ;
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
                clampTargetHipsLocalPositionSpikes = settings.clampRetargetHipsLocalPositionSpikes;
                maxTargetHipsLocalPositionDeltaPerFrame = Mathf.Max(0.005f, settings.MaxRetargetHipsLocalPositionDeltaPerFrame);
                smoothGrounding = settings.smoothRetargetGrounding;
                maxGroundingVerticalStepPerFrame = Mathf.Max(0.001f, settings.MaxGroundingVerticalStepPerFrame);
                groundingSmoothing = Mathf.Clamp01(settings.GroundingSmoothing);
                groundingDeadZone = Mathf.Max(0f, settings.GroundingDeadZone);
                freezeRootYAfterInitialGrounding = settings.FreezeRootYAfterInitialGrounding;
                clampLegacyAnimationVisualStep = settings.clampRetargetVisualClipStep;
                legacyAnimationVisualFrameRate = Mathf.Clamp(settings.RetargetVisualClipFrameRate, 15f, 120f);
                smoothPoseOnLegacyAnimationStepSpike = settings.smoothRetargetPoseOnVisualStepSpike;
                poseVisualSpikeCurrentWeight = Mathf.Clamp(settings.RetargetPoseVisualSpikeCurrentWeight, 0.1f, 1f);
                poseVisualSpikeForearmStretchClampMaxOffset =
                    Mathf.Clamp01(settings.RetargetPoseVisualSpikeForearmStretchClampMaxOffset);
                poseVisualMuscleDeltaThreshold = Mathf.Clamp(settings.RetargetPoseVisualMuscleDeltaThreshold, 0.05f, 1f);
                rejectRendererGroundingOutliers = settings.rejectRendererGroundingOutliers;
                maxRendererFootGroundingSeparation = Mathf.Max(0.02f, settings.MaxRendererFootGroundingSeparation);
                smoothLateVisualGroundingCorrection = settings.smoothLateVisualGroundingCorrection;
                lateVisualGroundingSnapThreshold = Mathf.Max(0.005f, settings.LateVisualGroundingSnapThreshold);
                lateVisualGroundingSmoothing = Mathf.Clamp01(settings.LateVisualGroundingSmoothing);
                maxLateVisualGroundingStepPerFrame = Mathf.Max(0.001f, settings.MaxLateVisualGroundingStepPerFrame);
                ShouldLockTargetHumanoidBonePositions = settings.ShouldLockTargetHumanoidBonePositions;
            }

            _isInitialized = true;
            EnsureLateVisualGroundingCorrection();
            Debug.Log("[Master Stage] 시스템 초기화됨. 첫 Update 대기 중...");
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            DisposeEditorHumanoidFingerPoseReference();
#endif
            _legacyAnimationDriver.Dispose();
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
            bool enableFingerPoseReference = true,
            bool enableFullBodyPoseReference = true,
            float fullBodyPoseReferenceWeight = 1f,
            bool fullBodyPoseExcludeLowerBodyMuscles = false,
            bool fullBodyPoseLowerBodyMusclesOnly = false,
            bool fullBodyPoseLegTwistMusclesOnly = false,
            bool fullBodyPoseRightArmMusclesOnly = false,
            bool fullBodyPoseLeftArmMusclesOnly = false,
            bool fullBodyPoseRightSleeveChainMusclesOnly = false,
            float fullBodyPoseFrameGateStart = 0f,
            float fullBodyPoseFrameGateEnd = 0f)
        {
            DisposeEditorHumanoidFingerPoseReference();
            _useEditorFingerPoseReference = false;
            _editorFingerPoseReferenceLogged = false;
            _editorBodyRotationReferenceLogged = false;
            _hasEditorReferenceBodyPosition = false;
            _hasEditorReferenceHipsRestLocalPosition = false;
            _hasEditorReferenceLowestFootRestY = false;
            _allowEditorFootHeightGroundingReference = false;
            _editorHandLocalRotationReferenceLogged = false;
            _editorFootLocalRotationReferenceLogged = false;
            _editorLowerBodySegmentDirectionReferenceLogged = false;
            _editorFootHipsAlignedResidualYawReferenceLogged = false;
            _editorThumbLocalRotationReferenceLogged = false;
            _editorThumbSegmentDirectionReferenceLogged = false;
            _editorHandPalmFrameReferenceLogged = false;
            _editorThumbBasePositionReferenceLogged = false;
            _editorHipsLocalPositionReferenceLogged = false;
            _editorBodyPositionXzReferenceLogged = false;
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
            ShouldUseManualAnimatorFullBodyPoseReference = enableFullBodyPoseReference;
            manualAnimatorFullBodyPoseReferenceWeight = Mathf.Clamp01(fullBodyPoseReferenceWeight);
            ShouldExcludeManualAnimatorFullBodyLowerMuscles = fullBodyPoseExcludeLowerBodyMuscles;
            ShouldApplyManualAnimatorFullBodyLowerMusclesOnly = fullBodyPoseLowerBodyMusclesOnly;
            ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = fullBodyPoseLegTwistMusclesOnly;
            manualAnimatorFullBodyPoseRightArmMusclesOnly = fullBodyPoseRightArmMusclesOnly;
            manualAnimatorFullBodyPoseLeftArmMusclesOnly = fullBodyPoseLeftArmMusclesOnly;
            manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly = fullBodyPoseRightSleeveChainMusclesOnly;
            manualAnimatorFullBodyPoseFrameGateStart = Mathf.Max(0f, fullBodyPoseFrameGateStart);
            manualAnimatorFullBodyPoseFrameGateEnd = Mathf.Max(0f, fullBodyPoseFrameGateEnd);
            _useEditorFingerPoseReference = ShouldUseEditorPoseReference(
                enableFingerPoseReference,
                ShouldUseManualAnimatorFullBodyPoseReference,
                _editorFingerReferenceMuscleIndices.Count);

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

        private static bool ShouldUseEditorPoseReference(
            bool enableFingerPoseReference,
            bool enableFullBodyPoseReference,
            int fingerReferenceMuscleCount)
        {
            return enableFullBodyPoseReference || (enableFingerPoseReference && fingerReferenceMuscleCount > 0);
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

            if (_legacyAnimationDriver.Tick(
                Time.deltaTime,
                Application.isPlaying,
                clampLegacyAnimationVisualStep,
                legacyAnimationVisualFrameRate))
            {
                ResetVisualPoseHistory();
            }

            // 스케일 비율 계산 (매 프레임 체크하여 안정성 확보)
            // Container가 작동 중이라면 ghostHip.position.y는 ~0.8m 수준이어야 함.
            Transform ghostHip = ghostAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHip = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);

            _scaleRatio = CalculateSafeScaleRatio(ghostHip, targetHip);

            // 포즈(근육) 동기화
            ResetRetargetPoseStageDiagnostics();
            _ghostHandler.GetHumanPose(ref _humanPose);
            _lastRetargetStageGhostEndpointPositions = CaptureEndpointStageWorldPositions(ghostAnimator);
            if (!IsFinite(_humanPose))
            {
                LogPoseWarning("Ghost HumanPose contains non-finite values. Skipping this retarget frame.");
                RestoreTargetLocalScales();
                return;
            }

            TransformRetargetPoseInputMuscles(ref _humanPose);
#if UNITY_EDITOR
            AlignRetargetPoseInputWithEditorHumanoidMuscleReference(ref _humanPose);
#endif
            CapturePoseInputDiagnostics(_humanPose);
            ApplyEditorHumanoidMuscleReference(ref _humanPose);
            CaptureAfterEditorMuscleReferenceDiagnostics(_humanPose);
            ApplyEditorHumanoidFingerPoseReference(ref _humanPose);
            ApplyEditorHumanoidBodyRotationReference(ref _humanPose);
            ApplyThumbAnatomicalGuard(ref _humanPose, ShouldApplyThumbStretchOffset());
            ClampPoseMuscles(ref _humanPose);
            CaptureAfterClampPoseMusclesDiagnostics(_humanPose);
            ApplyAnatomicalArmGuard(ref _humanPose);
            CaptureAfterAnatomicalArmGuardDiagnostics(_humanPose);
            SmoothPoseOnVisualSpike(ref _humanPose);
            CaptureAfterVisualSpikeSmoothingDiagnostics(_humanPose);
            Quaternion poseRootRotation = _humanPose.bodyRotation;
            if (ShouldPreserveFbxRootRotation && !_hasPoseRootRotationCorrection && IsFinite(poseRootRotation) && _legacyAnimationDriver.IsPlaying)
            {
                _poseRootRotationCorrection = Quaternion.Inverse(poseRootRotation);
                _hasPoseRootRotationCorrection = true;
            }

            // Y축은 target 기준으로 안정화하고, X/Z 체중 이동은 FBX 값을 유지한다.
            Vector3 bodyPos = _humanPose.bodyPosition;
            bodyPos.x *= _scaleRatio;
            bodyPos.z *= _scaleRatio;
            Vector3 bodyRootMotionSource = bodyPos;
#if UNITY_EDITOR
            bodyRootMotionSource = SelectBodyPositionRootMotionSource(
                bodyPos,
                _editorReferenceBodyPosition,
                _hasEditorReferenceBodyPosition,
                ShouldUseManualAnimatorBodyRotationReference);
#endif
            Vector3 bodyRootDelta = ExtractBodyPositionXZRootDelta(bodyRootMotionSource);
            if (preserveTargetBodyPosition && _hasTargetReferenceBodyPosition)
            {
                bodyPos = _targetReferenceBodyPosition;
                // 수동 기준 Animator의 bodyPos.y로 Y를 대체: ghost Legacy bodyPos 스파이크 없이 애니메이션 높이를 따른다.
                if (ShouldUseManualAnimatorBodyPositionYReference && _hasEditorReferenceBodyPosition)
                {
                    bodyPos.y = _editorReferenceBodyPosition.y;
                }
            }
            else
            {
                bodyPos.y *= _scaleRatio;
            }
#if UNITY_EDITOR
            float manualBodyPositionXzFrameGateWeight =
                ResolveManualAnimatorBodyPositionXzFrameGateWeight();
            if (ShouldUseManualAnimatorBodyPositionXzReference &&
                manualBodyPositionXzFrameGateWeight > 0f &&
                _hasEditorReferenceBodyPosition &&
                TryCalculateManualAnimatorBodyPositionXzReference(
                    bodyPos,
                    _editorReferenceBodyPosition,
                    manualAnimatorBodyPositionXzReferenceWeight * manualBodyPositionXzFrameGateWeight,
                    manualAnimatorBodyPositionXzReferenceMaxOffset,
                    manualAnimatorBodyPositionXzReferenceAxisXScale,
                    manualAnimatorBodyPositionXzReferenceAxisZScale,
                    out Vector3 manualBodyPositionXz))
            {
                bodyPos = manualBodyPositionXz;
                if (!_editorBodyPositionXzReferenceLogged)
                {
                    Debug.Log(
                        $"[PoseSpaceRetargeter] Manual Animator bodyPosition X/Z reference applied. " +
                        $"weight={manualAnimatorBodyPositionXzReferenceWeight:F2}, " +
                        $"maxOffset={manualAnimatorBodyPositionXzReferenceMaxOffset:F3}m, " +
                        $"frameGate={manualAnimatorBodyPositionXzReferenceFrameGateStart:F0}-{manualAnimatorBodyPositionXzReferenceFrameGateEnd:F0}, " +
                        $"blendFrames={manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames:F0}, " +
                        $"axisScale={manualAnimatorBodyPositionXzReferenceAxisXScale:F2}/{manualAnimatorBodyPositionXzReferenceAxisZScale:F2}");
                    _editorBodyPositionXzReferenceLogged = true;
                }
            }
#endif
            if (!IsFinite(bodyPos))
            {
                LogPoseWarning("Retarget body position became non-finite. Skipping this retarget frame.");
                RestoreTargetLocalScales();
                return;
            }
            _humanPose.bodyPosition = bodyPos;

            Vector3 rootMotionCarrierPositionBeforePose = targetAnimator.transform.position;
            Vector3 poseSolveRootPosition = SelectPoseSolveRootPosition(
                rootMotionCarrierPositionBeforePose,
                _hasTargetRootPoseGuardAnchorPosition ? _targetRootPoseGuardAnchorPosition : rootMotionCarrierPositionBeforePose,
                useBodyPositionXZRootMotion);
            if (IsFinite(poseSolveRootPosition))
            {
                targetAnimator.transform.position = poseSolveRootPosition;
            }

            Vector3 targetPositionBeforePose = targetAnimator.transform.position;
            _lastSetHumanPosePreSolveCurrentEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);
#if UNITY_EDITOR
            ApplyPreSetHumanPoseSignCorrectedRowLocalBodyPositionReference(ref _humanPose);
            if (!preSetHumanPoseEndpointPositionUseGhostCurrentBasis)
            {
                ApplyPreSetHumanPoseRightEndpointPositionReference();
            }
#endif
            CaptureSetHumanPoseInputDiagnostics(_humanPose);
            _targetHandler.SetHumanPose(ref _humanPose);
            ApplySetHumanPoseRightLegTwistOutputReference(_humanPose);
            CaptureSetHumanPoseOutputDiagnostics();
            _lastRetargetStageAfterSetHumanPoseEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);
            ClampAppliedTargetPose();
            RestoreTargetHumanoidLocalPositions();
#if UNITY_EDITOR
            ApplyEditorHumanoidHipsLocalPositionReference();
            ApplyEditorHumanoidFootLocalRotationReference();
            ApplyEditorHumanoidLowerBodySegmentDirectionReference();
            ApplyEditorHumanoidFootHipsAlignedResidualYawReference();
            ApplyPostSetHumanPoseRightEndpointPositionReference();
            ApplyEditorHumanoidThumbBasePositionReference();
#endif
            _lastRetargetStageAfterManualReferencesEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);
            ClampTargetHipsLocalPositionSpike();
            ClampTargetThumbLocalRotations();
#if UNITY_EDITOR
            ApplyEditorHumanoidThumbLocalRotationReference();
            ApplyEditorHumanoidHandPalmFrameReference();
            ApplyEditorHumanoidHandLocalRotationReference();
            ApplyEditorHumanoidThumbSegmentDirectionReference();
            ApplyEditorHumanoidThumbHandDirectionReference();
            ApplyYybRightSleeveSilhouetteLocalOffsetReference();
#endif
            ClampTargetRootPositionSpike(targetPositionBeforePose, "SetHumanPose");
            Vector3 implicitRootGuardReference = SelectImplicitRootGuardReference(
                _hasTargetRootPoseGuardAnchorPosition ? _targetRootPoseGuardAnchorPosition : targetPositionBeforePose,
                targetPositionBeforePose,
                _movementScaleMultiplier);
            targetAnimator.transform.position = ApplyImplicitBodyPositionRootGuard(
                implicitRootGuardReference,
                targetAnimator.transform.position,
                useBodyPositionXZRootMotion,
                bodyRootDelta);
            targetAnimator.transform.position = RestoreRootMotionCarrierPositionAfterPose(
                rootMotionCarrierPositionBeforePose,
                targetAnimator.transform.position,
                useBodyPositionXZRootMotion);
            _lastRetargetStageAfterRootRestoreEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);

            // 월드 회전 동기화 (180도 문제 해결)
            if (ShouldPreserveFbxRootRotation && _hasPoseRootRotationCorrection && IsFinite(poseRootRotation))
            {
                Quaternion correctedRootRotation = _poseRootRotationCorrection * poseRootRotation;
                if (IsFinite(correctedRootRotation))
                {
                    targetAnimator.transform.rotation = correctedRootRotation;
                }
            }
            else if (!ShouldPreserveFbxRootRotation && fixReverseRotation)
            {
                // Ghost 회전 * 180도 보정
                targetAnimator.transform.rotation = ghostAnimator.transform.rotation * _facingCorrection;
            }
            else if (!ShouldPreserveFbxRootRotation)
            {
                targetAnimator.transform.rotation = ghostAnimator.transform.rotation;
            }

            // 루트 모션 동기화 (호 그리기 방지)
            // Ghost 이동량 계산
            Vector3 ghostDelta = ghostAnimator.transform.position - _prevGhostPos;
            Vector3 editorRootTranslationDelta = ExtractEditorRootTranslationDelta(ghostDelta);

            // 내 캐릭터 크기에 맞춰 이동량 스케일링
            Vector3 targetDelta = RootMotionGuard.CalculateRetargetRootDelta(
                ghostDelta,
                _scaleRatio,
                editorRootTranslationDelta,
                bodyRootDelta,
                _movementScaleMultiplier,
                useBodyPositionXZRootMotion,
                clampRootDeltaSpikes,
                maxRootDeltaPerFrame,
                out float targetDeltaMagnitude,
                out bool skippedByNonFinite,
                out bool limitedBySpike);
            if (skippedByNonFinite)
            {
                LogPoseWarning("Retarget root delta became non-finite. Skipping root motion for this frame.");
                _lastRootDeltaMagnitude = float.NaN;
                _rootDeltaSpikeSkippedCount++;
            }
            else
            {
                _lastRootDeltaMagnitude = targetDeltaMagnitude;
                _maxRootDeltaMagnitude = Mathf.Max(_maxRootDeltaMagnitude, _lastRootDeltaMagnitude);

                if (limitedBySpike)
                {
                    _rootDeltaSpikeSkippedCount++;
                    if (logRootDeltaSpikes && !_rootDeltaSpikeWarningLogged)
                    {
                        Debug.LogWarning($"[PoseSpaceRetargeter] Root delta spike {_lastRootDeltaMagnitude:F3}m limited. ghostDelta={ghostDelta.magnitude:F3}m, editorRootDelta={editorRootTranslationDelta.magnitude:F3}m, limit={maxRootDeltaPerFrame:F3}m");
                        _rootDeltaSpikeWarningLogged = true;
                    }
                }
            }

            // 이동 적용
            targetAnimator.transform.position += targetDelta;
            _lastRetargetStageAfterRootDeltaEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);

            // 위치 갱신
            _prevGhostPos = ghostAnimator.transform.position;

            // 스마트 접지 (Raycast Grounding) - 공중 부양 해결
            if (useSmartGrounding)
            {
                ApplyRaycastGrounding();
            }
            _lastRetargetStageAfterGroundingEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);

            RestoreTargetLocalScales();
#if UNITY_EDITOR
            ApplyEditorHumanoidBipedIkFootPositionReference();
#endif
            _lastRetargetStageAfterBipedIKEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);
            CaptureRetargetEndpointStageAttributionDiagnostics();
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
            bool shouldSmooth = RetargetingPoseSmoothing.ShouldSmoothVisualPoseSpike(
                maxMuscleDelta,
                bodyPositionDelta,
                bodyRotationDelta,
                poseVisualMuscleDeltaThreshold,
                _legacyAnimationDriver.StepSpikeThisFrame,
                out bool muscleDeltaOnlySpike);

            if (shouldSmooth)
            {
                float currentWeight = RetargetingPoseSmoothing.CalculateVisualPoseSpikeCurrentWeight(
                    poseVisualSpikeCurrentWeight,
                    bodyPositionDelta,
                    bodyRotationDelta,
                    _legacyAnimationDriver.StepSpikeThisFrame);
                bool useEditorHumanoidMuscleReference = false;
#if UNITY_EDITOR
                useEditorHumanoidMuscleReference = _useEditorHumanoidMuscleReference;
#endif
                for (int i = 0; i < pose.muscles.Length; i++)
                {
                    bool hasEditorHumanoidMuscleReferenceCurve = false;
#if UNITY_EDITOR
                    hasEditorHumanoidMuscleReferenceCurve = _editorHumanoidMuscleCurves.ContainsKey(i);
#endif
                    bool shouldPreserveCurrentValue = ShouldPreserveEditorHumanoidMuscleDuringVisualSmoothing(
                        i,
                        useEditorHumanoidMuscleReference,
                        hasEditorHumanoidMuscleReferenceCurve);
                    bool isForearmStretchMuscle = !shouldPreserveCurrentValue &&
                        poseVisualSpikeForearmStretchClampMaxOffset > 0f &&
                        IsForearmStretchMuscleIndex(i);
                    pose.muscles[i] = RetargetingPoseSmoothing.BlendVisualPoseSpikeMuscle(
                        _previousVisualPoseMuscles[i],
                        pose.muscles[i],
                        currentWeight,
                        shouldPreserveCurrentValue,
                        isForearmStretchMuscle,
                        poseVisualSpikeForearmStretchClampMaxOffset);
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

        private static bool ShouldPreserveEditorHumanoidMuscleDuringVisualSmoothing(
            int muscleIndex,
            bool useEditorHumanoidMuscleReference,
            bool hasEditorHumanoidMuscleReferenceCurve)
        {
#if UNITY_EDITOR
            return useEditorHumanoidMuscleReference &&
                hasEditorHumanoidMuscleReferenceCurve &&
                ShouldUseEditorHumanoidMuscleReference(muscleIndex);
#else
            return false;
#endif
        }

        private static bool IsForearmStretchMuscleIndex(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            return normalized.Contains("forearm") && normalized.Contains("stretch");
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

        private void ApplyEditorHumanoidMuscleReference(ref HumanPose pose)
        {
#if UNITY_EDITOR
            ApplyEditorHumanoidMuscleReferenceEditor(ref pose);
#endif
        }

        private void ApplyEditorHumanoidFingerPoseReference(ref HumanPose pose)
        {
#if UNITY_EDITOR
            ApplyEditorHumanoidFingerPoseReferenceEditor(ref pose);
#endif
        }

        private void ApplyEditorHumanoidBodyRotationReference(ref HumanPose pose)
        {
#if UNITY_EDITOR
            ApplyEditorHumanoidBodyRotationReferenceEditor(ref pose);
#endif
        }

        private void ResetLastEditorHipsLocalReferenceDiagnostics()
        {
            _lastEditorHipsLocalReferenceBeforeLocalY = float.NaN;
            _lastEditorHipsLocalReferenceAfterLocalY = float.NaN;
            _lastEditorHipsLocalReferenceDeltaY = float.NaN;
        }

        private void ResetRecordingStartHipsBaselineDiagnostics()
        {
            _recordingStartRootY = float.NaN;
            _recordingStartBodyPositionY = float.NaN;
            _recordingStartHipsLocalY = float.NaN;
            _recordingStartHipsY = float.NaN;
            _recordingStartHipsReferenceBeforeLocalY = float.NaN;
            _recordingStartHipsReferenceAfterLocalY = float.NaN;
            _recordingStartHipsReferenceDeltaY = float.NaN;
            _recordingStartHipsReferenceFlipDetected = false;
            _recordingStartHipsReferenceStage = string.Empty;
        }

        private bool TryGetTargetBodyPositionY(out float bodyPositionY)
        {
            bodyPositionY = float.NaN;
            if (_targetHandler == null)
            {
                return false;
            }

            HumanPose targetPose = new HumanPose();
            _targetHandler.GetHumanPose(ref targetPose);
            if (!IsFinite(targetPose.bodyPosition))
            {
                return false;
            }

            bodyPositionY = targetPose.bodyPosition.y;
            return true;
        }

        private static bool IsRecordingStartHipsBaselineFlip(float beforeLocalY, float afterLocalY, float warningThreshold)
        {
            if (!IsFinite(beforeLocalY) || !IsFinite(afterLocalY) || !IsFinite(warningThreshold))
            {
                return false;
            }

            return Mathf.Abs(afterLocalY - beforeLocalY) > Mathf.Max(0f, warningThreshold);
        }

        private bool ShouldSuppressCompetingManualThumbOverride(bool leftHand)
        {
#if UNITY_EDITOR
            return ShouldSuppressCompetingManualThumbOverrideEditor(leftHand);
#else
            return false;
#endif
        }

        private bool ShouldKeepDetachedHelperManualThumbOverrides(bool leftHand)
        {
#if UNITY_EDITOR
            return ShouldKeepDetachedHelperManualThumbOverridesEditor(leftHand);
#else
            return false;
#endif
        }

        public bool TryGetHighRiskManualThumbPoseConstraintOverrides(
            bool leftHand,
            out float projectionMin,
            out float projectionMax,
            out float maxSpreadAngle)
        {
#if UNITY_EDITOR
            return TryGetHighRiskManualThumbPoseConstraintOverridesEditor(
                leftHand,
                out projectionMin,
                out projectionMax,
                out maxSpreadAngle);
#else
            projectionMin = 0f;
            projectionMax = 0f;
            maxSpreadAngle = 0f;
            return false;
#endif
        }

        public string BuildThumbHelperRelationshipDebugSummary(bool leftHand)
        {
#if UNITY_EDITOR
            return BuildThumbHelperRelationshipDebugSummaryEditor(leftHand);
#else
            return leftHand
                ? "side=L, state=editor-only"
                : "side=R, state=editor-only";
#endif
        }

        private Transform GetCachedThumbBaseHelper(bool leftHand)
        {
#if UNITY_EDITOR
            return GetCachedThumbBaseHelperEditor(leftHand);
#else
            return null;
#endif
        }

        private Transform GetCachedExplicitThumbBaseSource(bool leftHand)
        {
#if UNITY_EDITOR
            return GetCachedExplicitThumbBaseSourceEditor(leftHand);
#else
            return null;
#endif
        }

        private bool TryEvaluateThumbManualReferenceFrameDeviation(
            bool leftHand,
            Transform targetThumb,
            Quaternion candidateWorldRotation,
            out float currentDeviation,
            out float candidateDeviation)
        {
#if UNITY_EDITOR
            return TryEvaluateThumbManualReferenceFrameDeviationEditor(
                leftHand,
                targetThumb,
                candidateWorldRotation,
                out currentDeviation,
                out candidateDeviation);
#else
            currentDeviation = float.NaN;
            candidateDeviation = float.NaN;
            return false;
#endif
        }

        private bool TryEvaluateCurrentThumbReferenceFrameDelta(
            bool leftHand,
            out float spreadDelta,
            out float projectionDelta)
        {
#if UNITY_EDITOR
            return TryEvaluateCurrentThumbReferenceFrameDeltaEditor(leftHand, out spreadDelta, out projectionDelta);
#else
            spreadDelta = float.NaN;
            projectionDelta = float.NaN;
            return false;
#endif
        }

        private bool TryEvaluateThumbLocalRotationOverrideRisk(
            bool leftHand,
            Transform targetThumb,
            Quaternion candidateRotation,
            out float risk)
        {
#if UNITY_EDITOR
            return TryEvaluateThumbLocalRotationOverrideRiskEditor(leftHand, targetThumb, candidateRotation, out risk);
#else
            risk = float.NaN;
            return false;
#endif
        }

#if UNITY_EDITOR
        private void ApplyEditorHumanoidMuscleReferenceEditor(ref HumanPose pose)
        {
            if (!_useEditorHumanoidMuscleReference || pose.muscles == null || _editorHumanoidMuscleCurves.Count == 0)
            {
                return;
            }

            float time = _legacyAnimationDriver.CurrentTime;
            foreach (KeyValuePair<int, AnimationCurve> pair in _editorHumanoidMuscleCurves)
            {
                if (pair.Key < 0 || pair.Key >= pose.muscles.Length || pair.Value == null)
                {
                    continue;
                }

                float referenceValue = pair.Value.Evaluate(time);
                if (!ShouldApplyEditorHumanoidMuscleReferenceValue(pair.Key, referenceValue))
                {
                    continue;
                }

                pose.muscles[pair.Key] = referenceValue;
            }

            if (!_editorHumanoidMuscleReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid muscle reference applied at t={time:F3}s.");
                _editorHumanoidMuscleReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidFingerPoseReferenceEditor(ref HumanPose pose)
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

            if (ShouldUseManualAnimatorFullBodyPoseReference)
            {
                float weight = Mathf.Clamp01(manualAnimatorFullBodyPoseReferenceWeight);
                if (weight <= 0f)
                {
                    return;
                }

                if (!ShouldApplyManualFullBodyPoseReferenceFrameGate())
                {
                    return;
                }

                int count = Mathf.Min(pose.muscles.Length, _editorFingerReferencePose.muscles.Length);
                for (int i = 0; i < count; i++)
                {
                    if (!ShouldApplyManualFullBodyPoseReferenceMuscle(i))
                    {
                        continue;
                    }

                    pose.muscles[i] = Mathf.Lerp(pose.muscles[i], _editorFingerReferencePose.muscles[i], weight);
                }
            }
            else
            {
                foreach (int muscleIndex in _editorFingerReferenceMuscleIndices)
                {
                    if (muscleIndex < 0 || muscleIndex >= pose.muscles.Length || muscleIndex >= _editorFingerReferencePose.muscles.Length)
                    {
                        continue;
                    }

                    pose.muscles[muscleIndex] = _editorFingerReferencePose.muscles[muscleIndex];
                }

            }

            if (!_editorFingerPoseReferenceLogged)
            {
                float time = _legacyAnimationDriver.CurrentTime;
                string scope = ShouldUseManualAnimatorFullBodyPoseReference ? "full-body muscle" : "finger";
                string weightSuffix = ShouldUseManualAnimatorFullBodyPoseReference
                    ? $", weight={Mathf.Clamp01(manualAnimatorFullBodyPoseReferenceWeight):F2}"
                    : string.Empty;
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator {scope} reference applied at t={time:F3}s{weightSuffix}.");
                _editorFingerPoseReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidBodyRotationReferenceEditor(ref HumanPose pose)
        {
            if (!ShouldUseManualAnimatorBodyRotationReference ||
                manualAnimatorBodyRotationReferenceWeight <= 0f ||
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

            float weight = Mathf.Clamp01(manualAnimatorBodyRotationReferenceWeight);
            pose.bodyRotation = Quaternion.Slerp(pose.bodyRotation, referenceBodyRotation, weight);

            Vector3 refBodyPos = _editorFingerReferencePose.bodyPosition;
            if (IsFinite(refBodyPos) && refBodyPos.y > 0.01f)
            {
                _editorReferenceBodyPosition = refBodyPos;
                _hasEditorReferenceBodyPosition = true;
            }
            if (!_editorBodyRotationReferenceLogged)
            {
                float time = _legacyAnimationDriver.CurrentTime;
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator bodyRotation reference applied at t={time:F3}s, weight={weight:F2}.");
                _editorBodyRotationReferenceLogged = true;
            }
        }

        private bool UpdateEditorManualReferenceAnimator()
        {
            if (_editorFingerReferenceAnimator == null || _editorFingerReferenceClipLength <= 0f)
            {
                return false;
            }

            float time = _legacyAnimationDriver.CurrentTime;
            float normalizedTime = Mathf.Clamp01(time / _editorFingerReferenceClipLength);
            _editorFingerReferenceAnimator.Play(_editorFingerReferenceStateHash, 0, normalizedTime);
            _editorFingerReferenceAnimator.Update(0f);
            return true;
        }

        private void ApplyEditorHumanoidHipsLocalPositionReference()
        {
            if (!ShouldUseManualAnimatorHipsLocalPositionReference ||
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
            Vector3 ghostRightFootPosition = ReadAnimatorBoneWorldPosition(ghostAnimator, HumanBodyBones.RightFoot);
            Vector3 ghostRightToesPosition = ReadAnimatorBoneWorldPosition(ghostAnimator, HumanBodyBones.RightToes);
            Vector3 beforeRightFootPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightFoot);
            Vector3 beforeRightToesPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightToes);
            // Delta 방식: testprefab의 clip 시작 대비 현재 변위만 YYB 자연 위치에 더한다.
            // 절대 복사는 모델 비율 차이(YYB Hips Y≈1.024 vs testprefab≈1.056)로 인해 YYB Hips를 잘못된 높이로 강제한다.
            if (!TryCalculateEditorHipsLocalPositionReference(
                refCurrentLocalPosition,
                _editorReferenceHipsRestLocalPosition,
                _hasEditorReferenceHipsRestLocalPosition,
                _targetHipsRestLocalPosition,
                _hasTargetHipsRestLocalPosition,
                currentLocalPosition,
                manualAnimatorHipsLocalPositionWeight,
                manualAnimatorHipsLocalPositionMaxOffset,
                out Vector3 nextLocalPosition))
            {
                return;
            }

            targetHips.localPosition = nextLocalPosition;
            Vector3 afterRightFootPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightFoot);
            Vector3 afterRightToesPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightToes);
            if (!ShouldKeepEditorHipsLocalPositionReferenceByTargetGap(
                ghostRightFootPosition,
                ghostRightToesPosition,
                beforeRightFootPosition,
                beforeRightToesPosition,
                afterRightFootPosition,
                afterRightToesPosition,
                HipsLocalPositionTargetGapGuardMaxIncreaseMeters))
            {
                targetHips.localPosition = currentLocalPosition;
                RecordEditorHipsLocalReferenceDiagnostics(currentLocalPosition, currentLocalPosition);
                return;
            }

            RecordEditorHipsLocalReferenceDiagnostics(currentLocalPosition, nextLocalPosition);
            if (!_editorHipsLocalPositionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator Hips localPosition reference applied. weight={manualAnimatorHipsLocalPositionWeight:F2}, maxOffset={manualAnimatorHipsLocalPositionMaxOffset:F3}m");
                _editorHipsLocalPositionReferenceLogged = true;
            }
        }

        private static bool TryCalculateEditorHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            return TryCalculateEditorHipsLocalPositionReference(
                referenceCurrentLocalPosition,
                referenceRestLocalPosition,
                hasReferenceRestLocalPosition,
                currentLocalPosition,
                false,
                currentLocalPosition,
                weight,
                maxOffset,
                out nextLocalPosition);
        }

        private static bool TryCalculateEditorHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 targetRestLocalPosition,
            bool hasTargetRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            nextLocalPosition = currentLocalPosition;
            if (!IsFinite(referenceCurrentLocalPosition) || !IsFinite(currentLocalPosition))
            {
                return false;
            }

            if (hasReferenceRestLocalPosition && !IsFinite(referenceRestLocalPosition))
            {
                return false;
            }

            Vector3 desiredLocalPosition;
            if (hasReferenceRestLocalPosition)
            {
                Vector3 referenceDelta = referenceCurrentLocalPosition - referenceRestLocalPosition;
                Vector3 anchorLocalPosition = hasTargetRestLocalPosition && IsFinite(targetRestLocalPosition)
                    ? targetRestLocalPosition
                    : currentLocalPosition;
                desiredLocalPosition = anchorLocalPosition + referenceDelta;
            }
            else
            {
                desiredLocalPosition = referenceCurrentLocalPosition;
            }

            Vector3 delta = desiredLocalPosition - currentLocalPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                delta = Vector3.ClampMagnitude(delta, clampedMaxOffset);
            }

            nextLocalPosition = currentLocalPosition + delta * Mathf.Clamp01(weight);
            if (!IsFinite(nextLocalPosition))
            {
                nextLocalPosition = currentLocalPosition;
                return false;
            }

            return true;
        }

        private static bool ShouldKeepEditorHipsLocalPositionReferenceByTargetGap(
            Vector3 ghostRightFootPosition,
            Vector3 ghostRightToesPosition,
            Vector3 beforeRightFootPosition,
            Vector3 beforeRightToesPosition,
            Vector3 afterRightFootPosition,
            Vector3 afterRightToesPosition,
            float maxAllowedIncrease)
        {
            if (!TryCalculateRightEndpointTargetGap(
                    ghostRightFootPosition,
                    ghostRightToesPosition,
                    beforeRightFootPosition,
                    beforeRightToesPosition,
                    out float beforeGap) ||
                !TryCalculateRightEndpointTargetGap(
                    ghostRightFootPosition,
                    ghostRightToesPosition,
                    afterRightFootPosition,
                    afterRightToesPosition,
                    out float afterGap))
            {
                return true;
            }

            return afterGap <= beforeGap + Mathf.Max(0f, maxAllowedIncrease);
        }

        private static bool TryCalculateRightEndpointTargetGap(
            Vector3 ghostRightFootPosition,
            Vector3 ghostRightToesPosition,
            Vector3 targetRightFootPosition,
            Vector3 targetRightToesPosition,
            out float gap)
        {
            gap = float.NaN;
            if (!TryCalculateXzDistance(ghostRightFootPosition, targetRightFootPosition, out float footGap) ||
                !TryCalculateXzDistance(ghostRightToesPosition, targetRightToesPosition, out float toesGap))
            {
                return false;
            }

            gap = Mathf.Max(footGap, toesGap);
            return IsFinite(gap);
        }

        private static bool TryCalculateXzDistance(Vector3 a, Vector3 b, out float distance)
        {
            distance = float.NaN;
            if (!IsFinite(a) || !IsFinite(b))
            {
                return false;
            }

            distance = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            return IsFinite(distance);
        }

        private void RecordEditorHipsLocalReferenceDiagnostics(Vector3 beforeLocalPosition, Vector3 afterLocalPosition)
        {
            _lastEditorHipsLocalReferenceBeforeLocalY = IsFinite(beforeLocalPosition) ? beforeLocalPosition.y : float.NaN;
            _lastEditorHipsLocalReferenceAfterLocalY = IsFinite(afterLocalPosition) ? afterLocalPosition.y : float.NaN;
            _lastEditorHipsLocalReferenceDeltaY =
                IsFinite(_lastEditorHipsLocalReferenceBeforeLocalY) && IsFinite(_lastEditorHipsLocalReferenceAfterLocalY)
                    ? _lastEditorHipsLocalReferenceAfterLocalY - _lastEditorHipsLocalReferenceBeforeLocalY
                    : float.NaN;
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

        private void ApplyEditorHumanoidFootLocalRotationReference()
        {
            if (!ShouldUseManualAnimatorFootLocalRotationReference ||
                manualAnimatorFootLocalRotationReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            CaptureTargetFootPositions(out Vector3 leftFootBefore, out Vector3 rightFootBefore);
            int changed = 0;
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftUpperLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightUpperLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftLowerLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightLowerLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftFoot);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightFoot);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftToes);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightToes);
            RecordEditorFootLocalRotationReferenceDiagnostics(leftFootBefore, rightFootBefore);

            if (changed > 0 && !_editorFootLocalRotationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator lower-body localRotation reference applied. bones={changed}, weight={manualAnimatorFootLocalRotationReferenceWeight:F2}");
                _editorFootLocalRotationReferenceLogged = true;
            }
        }

        private int ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones footBone)
        {
            Transform source = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            Transform target = targetAnimator.GetBoneTransform(footBone);
            if (source == null || target == null)
            {
                return 0;
            }

            if (!TryCalculateEditorFootLocalRotationReference(
                    source.localRotation,
                    target.localRotation,
                    manualAnimatorFootLocalRotationReferenceWeight,
                    out Quaternion nextLocalRotation))
            {
                return 0;
            }

            target.localRotation = nextLocalRotation;
            return 1;
        }

        private static bool TryCalculateEditorFootLocalRotationReference(
            Quaternion referenceLocalRotation,
            Quaternion currentLocalRotation,
            float weight,
            out Quaternion nextLocalRotation)
        {
            nextLocalRotation = currentLocalRotation;
            if (!IsFinite(referenceLocalRotation) || !IsFinite(currentLocalRotation))
            {
                return false;
            }

            if (Quaternion.Angle(currentLocalRotation, referenceLocalRotation) <= 0.001f)
            {
                return false;
            }

            nextLocalRotation = Quaternion.Slerp(currentLocalRotation, referenceLocalRotation, Mathf.Clamp01(weight));
            if (!IsFinite(nextLocalRotation))
            {
                nextLocalRotation = currentLocalRotation;
                return false;
            }

            return true;
        }

        private void CaptureTargetFootPositions(out Vector3 leftFootPosition, out Vector3 rightFootPosition)
        {
            leftFootPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftFoot);
            rightFootPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightFoot);
        }

        private Vector3 ReadTargetBoneWorldPosition(HumanBodyBones bone)
        {
            if (targetAnimator == null)
            {
                return BuildNaNVector3();
            }

            Transform targetBone = targetAnimator.GetBoneTransform(bone);
            return targetBone != null ? targetBone.position : BuildNaNVector3();
        }

        private static Vector3 ReadAnimatorBoneWorldPosition(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
            {
                return BuildNaNVector3();
            }

            Transform targetBone = animator.GetBoneTransform(bone);
            return targetBone != null ? targetBone.position : BuildNaNVector3();
        }

        private static Vector3 ReadAnimatorBoneLocalPosition(Animator animator, HumanBodyBones bone)
        {
            if (animator == null)
            {
                return BuildNaNVector3();
            }

            Transform targetBone = animator.GetBoneTransform(bone);
            if (targetBone == null)
            {
                return BuildNaNVector3();
            }

            Vector3 localPosition = targetBone.localPosition;
            return IsFinite(localPosition) ? localPosition : BuildNaNVector3();
        }

        private static Vector3 ReadAnimatorRootWorldPosition(Animator animator)
        {
            if (animator == null)
            {
                return BuildNaNVector3();
            }

            Vector3 position = animator.transform.position;
            return IsFinite(position) ? position : BuildNaNVector3();
        }

        private static Quaternion ReadAnimatorRootWorldRotation(Animator animator)
        {
            if (animator == null)
            {
                return BuildNaNQuaternion();
            }

            Quaternion rotation = animator.transform.rotation;
            return IsFinite(rotation) ? rotation : BuildNaNQuaternion();
        }

        private static RetargetEndpointStageWorldPositions CaptureEndpointStageWorldPositions(Animator animator)
        {
            return new RetargetEndpointStageWorldPositions
            {
                LeftFoot = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.LeftFoot),
                LeftToes = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.LeftToes),
                RightFoot = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.RightFoot),
                RightToes = ReadAnimatorBoneWorldPosition(animator, HumanBodyBones.RightToes)
            };
        }

        private static bool TryFindFirstRetargetEndpointStageJump(
            string[] stageNames,
            Vector3[] positions,
            float threshold,
            out string stage,
            out Vector3 delta,
            out float magnitude)
        {
            stage = "";
            delta = BuildNaNVector3();
            magnitude = float.NaN;
            if (stageNames == null ||
                positions == null ||
                stageNames.Length != positions.Length ||
                positions.Length < 2)
            {
                return false;
            }

            float safeThreshold = Mathf.Max(0f, threshold);
            for (int i = 1; i < positions.Length; i++)
            {
                Vector3 previous = positions[i - 1];
                Vector3 current = positions[i];
                if (!IsFinite(previous) || !IsFinite(current))
                {
                    continue;
                }

                Vector3 stageDelta = current - previous;
                float stageMagnitude = stageDelta.magnitude;
                if (!IsFinite(stageDelta) || !IsFinite(stageMagnitude) || stageMagnitude <= safeThreshold)
                {
                    continue;
                }

                stage = stageNames[i] ?? "";
                delta = stageDelta;
                magnitude = stageMagnitude;
                return true;
            }

            return false;
        }

        private static readonly string[] RetargetEndpointStageNames =
        {
            "ghost",
            "after_set_human_pose",
            "after_manual_reference",
            "after_root_restore",
            "after_root_delta",
            "after_grounding",
            "after_biped_ik",
            "after_late_visual_grounding"
        };

        private const float RetargetEndpointStageJumpAttributionThreshold = 0.001f;

        private void CaptureRetargetEndpointStageAttributionDiagnostics()
        {
            ResetRetargetEndpointStageAttributionDiagnostics();
            bool hasBest = false;
            int bestStageIndex = int.MaxValue;
            TryRecordRetargetEndpointStageJump(
                "left_foot",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.LeftFoot),
                ref hasBest,
                ref bestStageIndex);
            TryRecordRetargetEndpointStageJump(
                "left_toes",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.LeftToes),
                ref hasBest,
                ref bestStageIndex);
            TryRecordRetargetEndpointStageJump(
                "right_foot",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.RightFoot),
                ref hasBest,
                ref bestStageIndex);
            TryRecordRetargetEndpointStageJump(
                "right_toes",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.RightToes),
                ref hasBest,
                ref bestStageIndex);
        }

        private delegate Vector3 RetargetEndpointStageSelector(RetargetEndpointStageWorldPositions endpointPositions);

        private Vector3[] BuildRetargetEndpointStagePositions(RetargetEndpointStageSelector selector)
        {
            return new[]
            {
                selector(_lastRetargetStageGhostEndpointPositions),
                selector(_lastRetargetStageAfterSetHumanPoseEndpointPositions),
                selector(_lastRetargetStageAfterManualReferencesEndpointPositions),
                selector(_lastRetargetStageAfterRootRestoreEndpointPositions),
                selector(_lastRetargetStageAfterRootDeltaEndpointPositions),
                selector(_lastRetargetStageAfterGroundingEndpointPositions),
                selector(_lastRetargetStageAfterBipedIKEndpointPositions),
                selector(_lastRetargetStageAfterLateVisualGroundingEndpointPositions)
            };
        }

        private void TryRecordRetargetEndpointStageJump(
            string endpointName,
            Vector3[] positions,
            ref bool hasBest,
            ref int bestStageIndex)
        {
            if (!TryFindFirstRetargetEndpointStageJump(
                    RetargetEndpointStageNames,
                    positions,
                    RetargetEndpointStageJumpAttributionThreshold,
                    out string stage,
                    out Vector3 delta,
                    out float magnitude))
            {
                return;
            }

            int stageIndex = Array.IndexOf(RetargetEndpointStageNames, stage);
            if (stageIndex < 0)
            {
                return;
            }

            if (hasBest && stageIndex >= bestStageIndex)
            {
                return;
            }

            hasBest = true;
            bestStageIndex = stageIndex;
            _lastRetargetEndpointFirstJumpStage = stage;
            _lastRetargetEndpointFirstJumpEndpoint = endpointName ?? "";
            _lastRetargetEndpointFirstJumpDelta = delta;
            _lastRetargetEndpointFirstJumpMagnitude = magnitude;
        }

        private void ResetRetargetEndpointStageAttributionDiagnostics()
        {
            _lastRetargetEndpointFirstJumpStage = "";
            _lastRetargetEndpointFirstJumpEndpoint = "";
            _lastRetargetEndpointFirstJumpDelta = BuildNaNVector3();
            _lastRetargetEndpointFirstJumpMagnitude = float.NaN;
        }

        private void RecordEditorFootLocalRotationReferenceDiagnostics(Vector3 leftFootBefore, Vector3 rightFootBefore)
        {
            _lastEditorFootLocalRotationLeftFootXzDelta = CalculateTargetFootXzDelta(leftFootBefore, HumanBodyBones.LeftFoot);
            _lastEditorFootLocalRotationRightFootXzDelta = CalculateTargetFootXzDelta(rightFootBefore, HumanBodyBones.RightFoot);
        }

        private void RecordEditorLowerBodySegmentDirectionReferenceDiagnostics(Vector3 leftFootBefore, Vector3 rightFootBefore)
        {
            _lastEditorLowerBodySegmentDirectionLeftFootXzDelta = CalculateTargetFootXzDelta(leftFootBefore, HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionRightFootXzDelta = CalculateTargetFootXzDelta(rightFootBefore, HumanBodyBones.RightFoot);
            RecordEditorLowerBodySegmentDirectionEndpointDiagnostics();
        }

        private void RecordEditorFootHipsAlignedResidualYawReferenceDiagnostics(Vector3 leftFootBefore, Vector3 rightFootBefore)
        {
            _lastEditorFootHipsAlignedResidualYawLeftFootXzDelta = CalculateTargetFootXzDelta(leftFootBefore, HumanBodyBones.LeftFoot);
            _lastEditorFootHipsAlignedResidualYawRightFootXzDelta = CalculateTargetFootXzDelta(rightFootBefore, HumanBodyBones.RightFoot);
        }

        private float CalculateTargetFootXzDelta(Vector3 beforePosition, HumanBodyBones footBone)
        {
            Vector3 afterPosition = ReadTargetBoneWorldPosition(footBone);
            if (!IsFinite(beforePosition) || !IsFinite(afterPosition))
            {
                return float.NaN;
            }

            Vector2 beforeXz = new Vector2(beforePosition.x, beforePosition.z);
            Vector2 afterXz = new Vector2(afterPosition.x, afterPosition.z);
            return Vector2.Distance(beforeXz, afterXz);
        }

        private struct RetargetEndpointStageWorldPositions
        {
            public Vector3 LeftFoot;
            public Vector3 LeftToes;
            public Vector3 RightFoot;
            public Vector3 RightToes;

            public static RetargetEndpointStageWorldPositions Empty => new RetargetEndpointStageWorldPositions
            {
                LeftFoot = BuildNaNVector3(),
                LeftToes = BuildNaNVector3(),
                RightFoot = BuildNaNVector3(),
                RightToes = BuildNaNVector3()
            };
        }

        private struct PostSetHumanPoseEndpointPositionDiagnostics
        {
            public Vector3 DesiredFootPosition;
            public Vector3 DesiredToesPosition;
            public Vector3 CurrentFootPosition;
            public Vector3 CurrentToesPosition;
            public Vector3 EndpointDeltaBeforeClamp;
            public Vector3 EndpointDeltaAfterClamp;
            public Vector3 EndpointDeltaAfterPositiveZScale;
            public Vector3 Correction;
            public Vector3 NextFootPosition;
            public float EvaluatorXzReferenceEnabled;
            public Vector3 EvaluatorXzFirstOffset;
            public Vector3 EvaluatorXzNormalizedDelta;
            public Vector3 EvaluatorXzDesiredNormalizedDelta;
            public float EvaluatorXzTargetMagnitude;

            public static PostSetHumanPoseEndpointPositionDiagnostics Empty => new PostSetHumanPoseEndpointPositionDiagnostics
            {
                DesiredFootPosition = BuildNaNVector3(),
                DesiredToesPosition = BuildNaNVector3(),
                CurrentFootPosition = BuildNaNVector3(),
                CurrentToesPosition = BuildNaNVector3(),
                EndpointDeltaBeforeClamp = BuildNaNVector3(),
                EndpointDeltaAfterClamp = BuildNaNVector3(),
                EndpointDeltaAfterPositiveZScale = BuildNaNVector3(),
                Correction = BuildNaNVector3(),
                NextFootPosition = BuildNaNVector3(),
                EvaluatorXzReferenceEnabled = float.NaN,
                EvaluatorXzFirstOffset = BuildNaNVector3(),
                EvaluatorXzNormalizedDelta = BuildNaNVector3(),
                EvaluatorXzDesiredNormalizedDelta = BuildNaNVector3(),
                EvaluatorXzTargetMagnitude = float.NaN
            };
        }

        private static Vector3 BuildNaNVector3()
        {
            return new Vector3(float.NaN, float.NaN, float.NaN);
        }

        private static Quaternion BuildNaNQuaternion()
        {
            return new Quaternion(float.NaN, float.NaN, float.NaN, float.NaN);
        }

        private void ResetPostSetHumanPoseRightEndpointPositionDiagnostics()
        {
            _lastPostSetHumanPoseRightEndpointDesiredFootWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDesiredToesWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointCurrentFootWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointCurrentToesWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDeltaBeforeClamp = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDeltaAfterClamp = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScale = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointCorrection = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointNextFootWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointMaxYawAngle = float.NaN;
            _lastPostSetHumanPoseRightEndpointYawCorrectionAngle = float.NaN;
            _lastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle = float.NaN;
            _lastPostSetHumanPoseRightEndpointApplied = float.NaN;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled = float.NaN;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffset = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDelta = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude = float.NaN;
        }

        private void RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
            PostSetHumanPoseEndpointPositionDiagnostics diagnostics,
            float maxYawAngle,
            float yawCorrectionAngle,
            float upperLegRotationDeltaAngle,
            float applied)
        {
            _lastPostSetHumanPoseRightEndpointDesiredFootWorldPosition = diagnostics.DesiredFootPosition;
            _lastPostSetHumanPoseRightEndpointDesiredToesWorldPosition = diagnostics.DesiredToesPosition;
            _lastPostSetHumanPoseRightEndpointCurrentFootWorldPosition = diagnostics.CurrentFootPosition;
            _lastPostSetHumanPoseRightEndpointCurrentToesWorldPosition = diagnostics.CurrentToesPosition;
            _lastPostSetHumanPoseRightEndpointDeltaBeforeClamp = diagnostics.EndpointDeltaBeforeClamp;
            _lastPostSetHumanPoseRightEndpointDeltaAfterClamp = diagnostics.EndpointDeltaAfterClamp;
            _lastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScale = diagnostics.EndpointDeltaAfterPositiveZScale;
            _lastPostSetHumanPoseRightEndpointCorrection = diagnostics.Correction;
            _lastPostSetHumanPoseRightEndpointNextFootWorldPosition = diagnostics.NextFootPosition;
            _lastPostSetHumanPoseRightEndpointMaxYawAngle = maxYawAngle;
            _lastPostSetHumanPoseRightEndpointYawCorrectionAngle = yawCorrectionAngle;
            _lastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle = upperLegRotationDeltaAngle;
            _lastPostSetHumanPoseRightEndpointApplied = applied;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled = diagnostics.EvaluatorXzReferenceEnabled;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffset = diagnostics.EvaluatorXzFirstOffset;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta = diagnostics.EvaluatorXzNormalizedDelta;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDelta = diagnostics.EvaluatorXzDesiredNormalizedDelta;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude = diagnostics.EvaluatorXzTargetMagnitude;
        }

        private void RecordEditorLowerBodySegmentDirectionEndpointDiagnostics()
        {
            _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftLowerLeg);
            _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftToes);
            _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightLowerLeg);
            _lastEditorLowerBodySegmentDirectionRightFootWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightFoot);
            _lastEditorLowerBodySegmentDirectionRightToesWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightToes);
            _lastEditorLowerBodySegmentDirectionLeftFootForward = ReadTargetBoneWorldForward(HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionLeftFootUp = ReadTargetBoneWorldUp(HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionRightFootForward = ReadTargetBoneWorldForward(HumanBodyBones.RightFoot);
            _lastEditorLowerBodySegmentDirectionRightFootUp = ReadTargetBoneWorldUp(HumanBodyBones.RightFoot);
        }

        private Vector3 ReadTargetBoneWorldForward(HumanBodyBones bone)
        {
            Transform targetBone = targetAnimator != null ? targetAnimator.GetBoneTransform(bone) : null;
            return targetBone != null && IsFinite(targetBone.forward) ? targetBone.forward : BuildNaNVector3();
        }

        private Vector3 ReadTargetBoneWorldUp(HumanBodyBones bone)
        {
            Transform targetBone = targetAnimator != null ? targetAnimator.GetBoneTransform(bone) : null;
            return targetBone != null && IsFinite(targetBone.up) ? targetBone.up : BuildNaNVector3();
        }

        private void ApplyEditorHumanoidLowerBodySegmentDirectionReference()
        {
            if (!ShouldUseManualAnimatorLowerBodySegmentDirectionReference ||
                manualAnimatorLowerBodySegmentDirectionReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorLowerBodySegmentDirectionReferenceWeight);
            float maxAngle = Mathf.Max(0f, manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
            float upperLegToLowerLegMaxAngle = ResolveManualAnimatorUpperLegToLowerLegSegmentDirectionMaxAngle(maxAngle);
            float lowerLegToFootMaxAngle = ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(maxAngle);
            float footToToesMaxAngle = ResolveManualAnimatorFootToToesSegmentDirectionMaxAngle(maxAngle);
            CaptureTargetFootPositions(out Vector3 leftFootBefore, out Vector3 rightFootBefore);
            ResetEditorLowerBodySegmentDirectionDetailedDiagnostics();
            int changed = 0;
            if (!ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference)
            {
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, weight, upperLegToLowerLegMaxAngle);
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, weight, upperLegToLowerLegMaxAngle);
            }

            if (!ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference)
            {
                changed += AlignEditorHumanoidLowerBodySegmentDirection(
                    HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot,
                    weight,
                    ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(lowerLegToFootMaxAngle, rightSide: false));
                changed += AlignEditorHumanoidLowerBodySegmentDirection(
                    HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot,
                    ResolveManualAnimatorRightLowerLegToFootSegmentDirectionBlendWeight(weight),
                    ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(lowerLegToFootMaxAngle, rightSide: true),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
            }

            if (!ShouldDisableManualAnimatorFootToToesSegmentDirectionReference)
            {
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, weight, footToToesMaxAngle);
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, weight, footToToesMaxAngle);
            }

            RecordEditorLowerBodySegmentDirectionReferenceDiagnostics(leftFootBefore, rightFootBefore);

            if (changed > 0 && !_editorLowerBodySegmentDirectionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator lower-body segment direction reference applied. segments={changed}, weight={weight:F2}, maxAngle={maxAngle:F1}deg");
                _editorLowerBodySegmentDirectionReferenceLogged = true;
            }
        }

        private float ResolveManualAnimatorUpperLegToLowerLegSegmentDirectionMaxAngle(float fallbackMaxAngle)
        {
            float segmentMaxAngle = Mathf.Max(0f, manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            return segmentMaxAngle > 0f ? segmentMaxAngle : fallbackMaxAngle;
        }

        private float ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(float fallbackMaxAngle)
        {
            float segmentMaxAngle = Mathf.Max(0f, manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            return segmentMaxAngle > 0f ? segmentMaxAngle : fallbackMaxAngle;
        }

        private float ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(
            float fallbackMaxAngle,
            bool rightSide)
        {
            float sideMaxAngle = Mathf.Max(
                0f,
                rightSide
                    ? manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle
                    : manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            if (rightSide && sideMaxAngle > 0f && !ShouldApplyManualAnimatorRightLowerLegToFootFrameGate())
            {
                sideMaxAngle = 0f;
            }

            return sideMaxAngle > 0f
                ? sideMaxAngle
                : ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(fallbackMaxAngle);
        }

        private bool ShouldApplyManualAnimatorRightLowerLegToFootFrameGate()
        {
            float start = Mathf.Max(0f, manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            float end = Mathf.Max(0f, manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end < start || end <= 0f)
            {
                return true;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            int currentFrame = Mathf.RoundToInt(_legacyAnimationDriver.CurrentTime * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private bool ShouldApplyPostSetHumanPoseRightEndpointPositionFrameGate()
        {
            float start = Mathf.Max(0f, postSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            float end = Mathf.Max(0f, postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end < start || end <= 0f)
            {
                return true;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            int currentFrame = Mathf.RoundToInt(_legacyAnimationDriver.CurrentTime * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private bool ShouldApplyPreSetHumanPoseRightEndpointPositionFrameGate()
        {
            float start = Mathf.Max(0f, preSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            float end = Mathf.Max(0f, preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end < start || end <= 0f)
            {
                return true;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            int currentFrame = Mathf.RoundToInt(_legacyAnimationDriver.CurrentTime * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private float ResolveManualAnimatorBodyPositionXzFrameGateWeight()
        {
            float start = Mathf.Max(0f, manualAnimatorBodyPositionXzReferenceFrameGateStart);
            float end = Mathf.Max(0f, manualAnimatorBodyPositionXzReferenceFrameGateEnd);
            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            float currentFrame = Mathf.RoundToInt(_legacyAnimationDriver.CurrentTime * frameRate);
            return CalculateManualAnimatorBodyPositionXzFrameGateWeight(
                currentFrame,
                start,
                end,
                Mathf.Max(0f, manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames));
        }

        private static float CalculateManualAnimatorBodyPositionXzFrameGateWeight(
            float currentFrame,
            float startFrame,
            float endFrame,
            float blendFrames)
        {
            float start = Mathf.Max(0f, Mathf.Round(startFrame));
            float end = Mathf.Max(0f, Mathf.Round(endFrame));
            if (start <= 0f && end <= 0f)
            {
                return 1f;
            }

            if (end < start || end <= 0f)
            {
                return 1f;
            }

            float blend = Mathf.Max(0f, blendFrames);
            if (blend <= 0f)
            {
                return currentFrame >= start && currentFrame <= end ? 1f : 0f;
            }

            if (currentFrame >= start && currentFrame <= end)
            {
                return 1f;
            }

            if (currentFrame < start)
            {
                float fadeStart = start - blend;
                if (currentFrame <= fadeStart)
                {
                    return 0f;
                }

                return Mathf.Clamp01((currentFrame - fadeStart) / blend);
            }

            float fadeEnd = end + blend;
            if (currentFrame >= fadeEnd)
            {
                return 0f;
            }

            return Mathf.Clamp01((fadeEnd - currentFrame) / blend);
        }

        private float ResolveManualAnimatorRightLowerLegToFootSegmentDirectionBlendWeight(float fallbackWeight)
        {
            return Mathf.Clamp01(fallbackWeight) *
                Mathf.Clamp01(manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
        }

        private float ResolveManualAnimatorFootToToesSegmentDirectionMaxAngle(float fallbackMaxAngle)
        {
            float footToToesMaxAngle = Mathf.Max(0f, manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            return footToToesMaxAngle > 0f ? footToToesMaxAngle : fallbackMaxAngle;
        }

        private int AlignEditorHumanoidLowerBodySegmentDirection(
            HumanBodyBones parentBone,
            HumanBodyBones childBone,
            float weight,
            float maxAngle,
            float correctionAxisXzScale = 1f,
            float childWorldRotationBlendWeight = 1f)
        {
            Transform targetParent = targetAnimator.GetBoneTransform(parentBone);
            Transform targetChild = targetAnimator.GetBoneTransform(childBone);
            Transform referenceParent = _editorFingerReferenceAnimator.GetBoneTransform(parentBone);
            Transform referenceChild = _editorFingerReferenceAnimator.GetBoneTransform(childBone);
            if (targetParent == null || targetChild == null || referenceParent == null || referenceChild == null)
            {
                return 0;
            }

            Vector3 currentSegment = targetChild.position - targetParent.position;
            Vector3 referenceSegment = referenceChild.position - referenceParent.position;
            if (!TryNormalize(currentSegment, out Vector3 currentDirection) ||
                !TryNormalize(referenceSegment, out Vector3 referenceDirection))
            {
                return 0;
            }

            Vector3 referenceRootDirection = _editorFingerReferenceAnimator.transform.InverseTransformDirection(referenceDirection).normalized;
            Vector3 desiredWorldDirection = targetAnimator.transform.TransformDirection(referenceRootDirection).normalized;
            if (!IsFinite(referenceRootDirection) || !IsFinite(desiredWorldDirection))
            {
                return 0;
            }

            Quaternion currentParentRotation = targetParent.rotation;
            Quaternion childWorldRotationBefore = targetChild.rotation;
            Quaternion childLocalRotationBefore = targetChild.localRotation;
            float preAngle = Vector3.Angle(currentDirection, desiredWorldDirection);
            if (!TryCalculateEditorLowerBodySegmentDirectionReference(
                    desiredWorldDirection,
                    currentDirection,
                    currentParentRotation,
                    weight,
                    maxAngle,
                    correctionAxisXzScale,
                    out Quaternion nextWorldRotation))
            {
                return 0;
            }

            targetParent.rotation = nextWorldRotation;
            float clampedChildWorldRotationBlend = Mathf.Clamp01(childWorldRotationBlendWeight);
            if (clampedChildWorldRotationBlend < 0.9999f)
            {
                targetChild.rotation = Quaternion.Slerp(
                    childWorldRotationBefore,
                    targetChild.rotation,
                    clampedChildWorldRotationBlend);
            }

            Vector3 postSegment = targetChild.position - targetParent.position;
            if (TryNormalize(postSegment, out Vector3 postDirection))
            {
                Quaternion correction = nextWorldRotation * Quaternion.Inverse(currentParentRotation);
                float correctionAngle = Quaternion.Angle(Quaternion.identity, correction);
                float parentWorldRotationDeltaAngle = Quaternion.Angle(currentParentRotation, targetParent.rotation);
                float childLocalRotationDeltaAngle = Quaternion.Angle(childLocalRotationBefore, targetChild.localRotation);
                float postAngle = Vector3.Angle(postDirection, desiredWorldDirection);
                RecordEditorLowerBodySegmentDirectionSegmentDiagnostics(
                    parentBone,
                    childBone,
                    correctionAngle,
                    parentWorldRotationDeltaAngle,
                    childLocalRotationDeltaAngle,
                    preAngle,
                    postAngle,
                    ReadFiniteCorrectionAxis(correction),
                    desiredWorldDirection,
                    currentDirection,
                    postDirection);
            }

            return 1;
        }

        private void ResetEditorLowerBodySegmentDirectionDetailedDiagnostics()
        {
            _lastEditorLowerBodySegmentDirectionMaxCorrectionSegment = string.Empty;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionMaxPreAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionMaxPostAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionMaxReferenceDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionMaxPreDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionMaxPostDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightToesWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootForward = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootUp = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootForward = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootUp = BuildNaNVector3();
        }

        private void RecordEditorLowerBodySegmentDirectionSegmentDiagnostics(
            HumanBodyBones parentBone,
            HumanBodyBones childBone,
            float correctionAngle,
            float parentWorldRotationDeltaAngle,
            float childLocalRotationDeltaAngle,
            float preAngle,
            float postAngle,
            Vector3 correctionAxis,
            Vector3 referenceDirection,
            Vector3 preDirection,
            Vector3 postDirection)
        {
            string segmentName = BuildLowerBodySegmentName(parentBone, childBone);
            SetEditorLowerBodySegmentDirectionCorrectionAngle(segmentName, correctionAngle);
            SetEditorLowerBodySegmentDirectionCouplingDiagnostics(
                segmentName,
                parentWorldRotationDeltaAngle,
                childLocalRotationDeltaAngle,
                correctionAxis,
                referenceDirection,
                preDirection,
                postDirection);
            if (!IsFinite(correctionAngle) ||
                (!float.IsNaN(_lastEditorLowerBodySegmentDirectionMaxCorrectionAngle) &&
                    correctionAngle <= _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle))
            {
                return;
            }

            _lastEditorLowerBodySegmentDirectionMaxCorrectionSegment = segmentName;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle = correctionAngle;
            _lastEditorLowerBodySegmentDirectionMaxPreAngle = preAngle;
            _lastEditorLowerBodySegmentDirectionMaxPostAngle = postAngle;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis = correctionAxis;
            _lastEditorLowerBodySegmentDirectionMaxReferenceDirection = referenceDirection;
            _lastEditorLowerBodySegmentDirectionMaxPreDirection = preDirection;
            _lastEditorLowerBodySegmentDirectionMaxPostDirection = postDirection;
        }

        private void SetEditorLowerBodySegmentDirectionCouplingDiagnostics(
            string segmentName,
            float parentWorldRotationDeltaAngle,
            float childLocalRotationDeltaAngle,
            Vector3 correctionAxis,
            Vector3 referenceDirection,
            Vector3 preDirection,
            Vector3 postDirection)
        {
            switch (segmentName)
            {
                case "LeftLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = parentWorldRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = childLocalRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis = correctionAxis;
                    break;
                case "RightLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = parentWorldRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = childLocalRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis = correctionAxis;
                    break;
                case "LeftFootToToes":
                    _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection = referenceDirection;
                    _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection = preDirection;
                    _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection = postDirection;
                    break;
                case "RightFootToToes":
                    _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection = referenceDirection;
                    _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection = preDirection;
                    _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection = postDirection;
                    break;
            }
        }

        private void SetEditorLowerBodySegmentDirectionCorrectionAngle(string segmentName, float correctionAngle)
        {
            switch (segmentName)
            {
                case "LeftUpperLegToLowerLeg":
                    _lastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = correctionAngle;
                    break;
                case "RightUpperLegToLowerLeg":
                    _lastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = correctionAngle;
                    break;
                case "LeftLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = correctionAngle;
                    break;
                case "RightLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = correctionAngle;
                    break;
                case "LeftFootToToes":
                    _lastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = correctionAngle;
                    break;
                case "RightFootToToes":
                    _lastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = correctionAngle;
                    break;
            }
        }

        private static string BuildLowerBodySegmentName(HumanBodyBones parentBone, HumanBodyBones childBone)
        {
            if (parentBone == HumanBodyBones.LeftUpperLeg && childBone == HumanBodyBones.LeftLowerLeg)
            {
                return "LeftUpperLegToLowerLeg";
            }

            if (parentBone == HumanBodyBones.RightUpperLeg && childBone == HumanBodyBones.RightLowerLeg)
            {
                return "RightUpperLegToLowerLeg";
            }

            if (parentBone == HumanBodyBones.LeftLowerLeg && childBone == HumanBodyBones.LeftFoot)
            {
                return "LeftLowerLegToFoot";
            }

            if (parentBone == HumanBodyBones.RightLowerLeg && childBone == HumanBodyBones.RightFoot)
            {
                return "RightLowerLegToFoot";
            }

            if (parentBone == HumanBodyBones.LeftFoot && childBone == HumanBodyBones.LeftToes)
            {
                return "LeftFootToToes";
            }

            if (parentBone == HumanBodyBones.RightFoot && childBone == HumanBodyBones.RightToes)
            {
                return "RightFootToToes";
            }

            return $"{parentBone}To{childBone}";
        }

        private static Vector3 ReadFiniteCorrectionAxis(Quaternion correction)
        {
            if (!IsFinite(correction))
            {
                return BuildNaNVector3();
            }

            correction.ToAngleAxis(out float angle, out Vector3 axis);
            if (!IsFinite(angle) || angle <= 0.001f || !IsFinite(axis))
            {
                return BuildNaNVector3();
            }

            return axis.normalized;
        }

        private static bool TryCalculateEditorLowerBodySegmentDirectionReference(
            Vector3 referenceSegmentDirection,
            Vector3 currentSegmentDirection,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextParentWorldRotation)
        {
            return TryCalculateEditorLowerBodySegmentDirectionReference(
                referenceSegmentDirection,
                currentSegmentDirection,
                currentParentWorldRotation,
                weight,
                maxAngleDegrees,
                1f,
                out nextParentWorldRotation);
        }

        private static bool TryCalculateEditorLowerBodySegmentDirectionReference(
            Vector3 referenceSegmentDirection,
            Vector3 currentSegmentDirection,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            float correctionAxisXzScale,
            out Quaternion nextParentWorldRotation)
        {
            nextParentWorldRotation = currentParentWorldRotation;
            if (!IsFinite(referenceSegmentDirection) ||
                !IsFinite(currentSegmentDirection) ||
                !IsFinite(currentParentWorldRotation) ||
                !TryNormalize(referenceSegmentDirection, out Vector3 referenceDirection) ||
                !TryNormalize(currentSegmentDirection, out Vector3 currentDirection))
            {
                return false;
            }

            Quaternion correction = Quaternion.FromToRotation(currentDirection, referenceDirection);
            if (!IsFinite(correction))
            {
                return false;
            }

            float maxAngle = Mathf.Max(0f, maxAngleDegrees);
            if (maxAngle > 0f)
            {
                float angle = Quaternion.Angle(Quaternion.identity, correction);
                if (angle > maxAngle)
                {
                    correction = Quaternion.Slerp(Quaternion.identity, correction, maxAngle / angle);
                }
            }

            correction = ScaleCorrectionAxisXz(correction, correctionAxisXzScale);
            if (!IsFinite(correction))
            {
                return false;
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, clampedWeight);
            }

            nextParentWorldRotation = correction * currentParentWorldRotation;
            if (!IsFinite(nextParentWorldRotation) ||
                Quaternion.Angle(currentParentWorldRotation, nextParentWorldRotation) <= 0.001f)
            {
                nextParentWorldRotation = currentParentWorldRotation;
                return false;
            }

            return true;
        }

        private static Quaternion ScaleCorrectionAxisXz(Quaternion correction, float axisXzScale)
        {
            float scale = Mathf.Clamp01(axisXzScale);
            if (scale >= 0.999f)
            {
                return correction;
            }

            correction.ToAngleAxis(out float angle, out Vector3 axis);
            if (!IsFinite(angle) || angle <= 0.001f || !IsFinite(axis))
            {
                return correction;
            }

            Vector3 scaledAxis = new Vector3(axis.x * scale, axis.y, axis.z * scale);
            if (!TryNormalize(scaledAxis, out Vector3 normalizedAxis))
            {
                return Quaternion.identity;
            }

            return Quaternion.AngleAxis(angle, normalizedAxis);
        }

        private void ApplyEditorHumanoidFootHipsAlignedResidualYawReference()
        {
            if (!ShouldUseManualAnimatorFootHipsAlignedResidualYawReference ||
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight <= 0f ||
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

            float weight = Mathf.Clamp01(manualAnimatorFootHipsAlignedResidualYawReferenceWeight);
            float maxAngle = Mathf.Max(0f, manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            float leftResidual = float.NaN;
            float rightResidual = float.NaN;
            TryCalculateEditorFootHipsAlignedResidualForBone(
                HumanBodyBones.LeftFoot,
                referenceHips,
                targetHips,
                out leftResidual);
            TryCalculateEditorFootHipsAlignedResidualForBone(
                HumanBodyBones.RightFoot,
                referenceHips,
                targetHips,
                out rightResidual);

            bool leftDominantResidual = IsFinite(leftResidual) &&
                (!IsFinite(rightResidual) || leftResidual > rightResidual);
            bool rightDominantResidual = IsFinite(rightResidual) &&
                (!IsFinite(leftResidual) || rightResidual > leftResidual);
            float leftMaxAngle = ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
                leftResidual,
                rightResidual,
                maxAngle,
                leftDominantResidual);
            float rightMaxAngle = ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
                rightResidual,
                leftResidual,
                maxAngle,
                rightDominantResidual);
            CaptureTargetFootPositions(out Vector3 leftFootBefore, out Vector3 rightFootBefore);
            int changed = 0;
            changed += ApplyEditorHumanoidFootHipsAlignedResidualYawReferenceBone(
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftFoot,
                referenceHips,
                targetHips,
                weight,
                leftMaxAngle);
            changed += ApplyEditorHumanoidFootHipsAlignedResidualYawReferenceBone(
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightFoot,
                referenceHips,
                targetHips,
                weight,
                rightMaxAngle);
            RecordEditorFootHipsAlignedResidualYawReferenceDiagnostics(leftFootBefore, rightFootBefore);

            if (changed > 0 && !_editorFootHipsAlignedResidualYawReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator hips-aligned foot X/Z residual yaw reference applied. feet={changed}, weight={weight:F2}, maxAngle={maxAngle:F1}deg");
                _editorFootHipsAlignedResidualYawReferenceLogged = true;
            }
        }

        private bool TryCalculateEditorFootHipsAlignedResidualForBone(
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips,
            out float residual)
        {
            residual = float.NaN;
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            if (targetFoot == null ||
                !TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    footBone,
                    referenceHips,
                    targetHips,
                    targetFoot,
                    out Vector3 desiredFootPosition))
            {
                return false;
            }

            Vector3 residualVector = desiredFootPosition - targetFoot.position;
            residualVector.y = 0f;
            residual = residualVector.magnitude;
            return IsFinite(residual);
        }

        private int ApplyEditorHumanoidFootHipsAlignedResidualYawReferenceBone(
            HumanBodyBones upperLegBone,
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips,
            float weight,
            float maxAngle)
        {
            Transform targetUpperLeg = targetAnimator.GetBoneTransform(upperLegBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            if (targetUpperLeg == null || targetFoot == null || referenceFoot == null)
            {
                return 0;
            }

            if (!TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    footBone,
                    referenceHips,
                    targetHips,
                    targetFoot,
                    out Vector3 desiredFootPosition))
            {
                return 0;
            }

            if (!TryCalculateEditorFootHipsAlignedResidualYawReference(
                    desiredFootPosition,
                    targetFoot.position,
                    targetUpperLeg.position,
                    targetUpperLeg.rotation,
                    weight,
                    maxAngle,
                    out Quaternion nextWorldRotation))
            {
                return 0;
            }

            targetUpperLeg.rotation = nextWorldRotation;
            return 1;
        }

        private bool TryCalculateEditorFootHipsAlignedDesiredFootPosition(
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips,
            Transform targetFoot,
            out Vector3 desiredFootPosition)
        {
            desiredFootPosition = targetFoot != null ? targetFoot.position : Vector3.zero;
            if (_editorFingerReferenceAnimator == null ||
                targetAnimator == null ||
                referenceHips == null ||
                targetHips == null ||
                targetFoot == null)
            {
                return false;
            }

            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            if (referenceFoot == null)
            {
                return false;
            }

            Vector3 referenceOffset = referenceFoot.position - referenceHips.position;
            if (!IsFinite(referenceOffset))
            {
                return false;
            }

            Vector3 referenceRootOffset = _editorFingerReferenceAnimator.transform.InverseTransformVector(referenceOffset);
            Vector3 desiredTargetOffset = targetAnimator.transform.TransformVector(referenceRootOffset);
            if (!IsFinite(desiredTargetOffset))
            {
                return false;
            }

            desiredFootPosition = targetHips.position + desiredTargetOffset;
            desiredFootPosition.y = targetFoot.position.y;
            return IsFinite(desiredFootPosition);
        }

        private static bool TryCalculateEditorFootHipsAlignedResidualYawReference(
            Vector3 desiredFootPosition,
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextParentWorldRotation)
        {
            return TryCalculateEditorFootHipsAlignedResidualYawReference(
                desiredFootPosition,
                currentFootPosition,
                pivotPosition,
                currentParentWorldRotation,
                weight,
                maxAngleDegrees,
                out nextParentWorldRotation,
                out _);
        }

        private static bool TryCalculateEditorFootHipsAlignedResidualYawReference(
            Vector3 desiredFootPosition,
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextParentWorldRotation,
            out float yawCorrectionAngle)
        {
            nextParentWorldRotation = currentParentWorldRotation;
            yawCorrectionAngle = float.NaN;
            if (!IsFinite(desiredFootPosition) ||
                !IsFinite(currentFootPosition) ||
                !IsFinite(pivotPosition) ||
                !IsFinite(currentParentWorldRotation))
            {
                return false;
            }

            Vector3 currentOffset = currentFootPosition - pivotPosition;
            Vector3 desiredOffset = desiredFootPosition - pivotPosition;
            currentOffset.y = 0f;
            desiredOffset.y = 0f;
            if (!TryNormalize(currentOffset, out Vector3 currentDirection) ||
                !TryNormalize(desiredOffset, out Vector3 desiredDirection))
            {
                return false;
            }

            Quaternion correction = Quaternion.FromToRotation(currentDirection, desiredDirection);
            if (!IsFinite(correction))
            {
                return false;
            }

            float maxAngle = Mathf.Max(0f, maxAngleDegrees);
            if (maxAngle > 0f)
            {
                float angle = Quaternion.Angle(Quaternion.identity, correction);
                if (angle > maxAngle)
                {
                    correction = Quaternion.Slerp(Quaternion.identity, correction, maxAngle / angle);
                }
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, clampedWeight);
            }
            yawCorrectionAngle = Quaternion.Angle(Quaternion.identity, correction);

            if (yawCorrectionAngle <= 0.001f)
            {
                return false;
            }

            nextParentWorldRotation = correction * currentParentWorldRotation;
            if (!IsFinite(nextParentWorldRotation) ||
                Quaternion.Angle(currentParentWorldRotation, nextParentWorldRotation) <= 0.001f)
            {
                nextParentWorldRotation = currentParentWorldRotation;
                return false;
            }

            return true;
        }

        private static float ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
            float thisFootResidual,
            float otherFootResidual,
            float requestedMaxAngle,
            bool isThisFootDominantResidual)
        {
            float maxAngle = Mathf.Max(0f, requestedMaxAngle);
            if (maxAngle <= FootHipsAlignedResidualYawProtectedMaxAngle ||
                !IsFinite(thisFootResidual) ||
                !IsFinite(otherFootResidual) ||
                isThisFootDominantResidual)
            {
                return maxAngle;
            }

            bool thisFootAlreadyPassing = thisFootResidual <= FootHipsAlignedResidualYawGateMeters;
            bool otherFootStillFailing = otherFootResidual > FootHipsAlignedResidualYawGateMeters;
            bool meaningfulSideGap = otherFootResidual - thisFootResidual >= FootHipsAlignedResidualYawSideGapMeters;
            if (thisFootAlreadyPassing && otherFootStillFailing && meaningfulSideGap)
            {
                return Mathf.Min(maxAngle, FootHipsAlignedResidualYawProtectedMaxAngle);
            }

            return maxAngle;
        }

        private void ApplyPreSetHumanPoseRightEndpointPositionReference()
        {
            if (!usePreSetHumanPoseRightEndpointPositionReference ||
                preSetHumanPoseRightEndpointPositionReferenceWeight <= 0f ||
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset <= 0f ||
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
            bool useLeftSide = ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            HumanBodyBones footBone = useLeftSide ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            HumanBodyBones toesBone = useLeftSide ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
            HumanBodyBones upperLegBone = useLeftSide ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            Transform targetUpperLeg = targetAnimator.GetBoneTransform(upperLegBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            Transform targetToes = targetAnimator.GetBoneTransform(toesBone);
            if (referenceHips == null ||
                targetHips == null ||
                targetUpperLeg == null ||
                targetFoot == null)
            {
                return;
            }

            if (!ShouldApplyPreSetHumanPoseRightEndpointPositionFrameGate())
            {
                return;
            }

            if (!TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    footBone,
                    referenceHips,
                    targetHips,
                    targetFoot,
                    out Vector3 desiredFootPosition))
            {
                return;
            }

            Vector3 desiredToesPosition = BuildNaNVector3();
            if (targetToes != null)
            {
                TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    toesBone,
                    referenceHips,
                    targetHips,
                    targetToes,
                    out desiredToesPosition);
            }

            if (!TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                    desiredFootPosition,
                    desiredToesPosition,
                    targetFoot.position,
                    targetToes != null ? targetToes.position : BuildNaNVector3(),
                    preSetHumanPoseRightEndpointPositionReferenceWeight,
                    preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    out Vector3 nextFootPosition))
            {
                return;
            }

            float maxAngleDegrees = CalculateEndpointPositionMaxYawAngle(
                targetFoot.position,
                targetUpperLeg.position,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            if (!TryCalculateEditorFootHipsAlignedResidualYawReference(
                    nextFootPosition,
                    targetFoot.position,
                    targetUpperLeg.position,
                    targetUpperLeg.rotation,
                    1f,
                    maxAngleDegrees,
                    out Quaternion nextWorldRotation,
                    out _))
            {
                return;
            }

            targetUpperLeg.rotation = nextWorldRotation;
        }

        private void ApplyPreSetHumanPoseSignCorrectedRowLocalBodyPositionReference(ref HumanPose pose)
        {
            ResetPreSetHumanPoseEndpointBodyPositionDiagnostics();
            if (!usePreSetHumanPoseRightEndpointPositionReference ||
                !preSetHumanPoseEndpointPositionUseGhostCurrentBasis ||
                preSetHumanPoseRightEndpointPositionReferenceWeight <= 0f ||
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset <= 0f ||
                ghostAnimator == null ||
                targetAnimator == null ||
                !IsFinite(pose.bodyPosition))
            {
                return;
            }

            if (!ShouldApplyPreSetHumanPoseRightEndpointPositionFrameGate())
            {
                return;
            }

            RetargetEndpointStageWorldPositions ghostPositions = CaptureEndpointStageWorldPositions(ghostAnimator);
            RetargetEndpointStageWorldPositions currentPositions = _lastSetHumanPosePreSolveCurrentEndpointPositions;
            bool useLeftSide = ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            Vector3 ghostFootPosition = useLeftSide ? ghostPositions.LeftFoot : ghostPositions.RightFoot;
            Vector3 currentFootPosition = useLeftSide ? currentPositions.LeftFoot : currentPositions.RightFoot;
            Vector3 bodyPositionBefore = pose.bodyPosition;

            if (TryCalculateSignCorrectedRowLocalBodyPositionXzReference(
                    bodyPositionBefore,
                    ghostFootPosition,
                    currentFootPosition,
                    preSetHumanPoseRightEndpointPositionReferenceWeight,
                    preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    axisXScale: 1f,
                    axisZScale: 1f,
                    invertX: ShouldInvertPreSetHumanPoseEndpointPositionBodyX,
                    invertZ: ShouldInvertPreSetHumanPoseEndpointPositionBodyZ,
                    out Vector3 nextBodyPosition))
            {
                CapturePreSetHumanPoseEndpointBodyPositionDiagnostics(bodyPositionBefore, nextBodyPosition);
                pose.bodyPosition = nextBodyPosition;
            }
        }

        private void ResetPreSetHumanPoseEndpointBodyPositionDiagnostics()
        {
            _lastPreSetHumanPoseEndpointBodyPositionBefore = BuildNaNVector3();
            _lastPreSetHumanPoseEndpointBodyPositionAfter = BuildNaNVector3();
            _lastPreSetHumanPoseEndpointBodyPositionDelta = BuildNaNVector3();
        }

        private void CapturePreSetHumanPoseEndpointBodyPositionDiagnostics(Vector3 before, Vector3 after)
        {
            if (!IsFinite(before) || !IsFinite(after))
            {
                ResetPreSetHumanPoseEndpointBodyPositionDiagnostics();
                return;
            }

            _lastPreSetHumanPoseEndpointBodyPositionBefore = before;
            _lastPreSetHumanPoseEndpointBodyPositionAfter = after;
            _lastPreSetHumanPoseEndpointBodyPositionDelta = after - before;
        }

        private void ApplyPostSetHumanPoseRightEndpointPositionReference()
        {
            ResetPostSetHumanPoseRightEndpointPositionDiagnostics();
            if (!usePostSetHumanPoseRightFootEvaluatorXzReference)
            {
                _hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset = false;
                _postSetHumanPoseRightFootEvaluatorXzFirstOffset = BuildNaNVector3();
            }

            if (!usePostSetHumanPoseRightEndpointPositionReference ||
                postSetHumanPoseRightEndpointPositionReferenceWeight <= 0f ||
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset <= 0f ||
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
            bool useLeftSide = ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            HumanBodyBones footBone = useLeftSide ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            HumanBodyBones toesBone = useLeftSide ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
            HumanBodyBones upperLegBone = useLeftSide ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            Transform targetUpperLeg = targetAnimator.GetBoneTransform(upperLegBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            Transform targetToes = targetAnimator.GetBoneTransform(toesBone);
            if (referenceHips == null ||
                targetHips == null ||
                targetUpperLeg == null ||
                targetFoot == null)
            {
                return;
            }

            Vector3 evaluatorXzFirstOffset = BuildNaNVector3();
            if (usePostSetHumanPoseRightFootEvaluatorXzReference)
            {
                if (referenceFoot == null ||
                    !TryResolvePostSetHumanPoseRightFootEvaluatorXzFirstOffset(
                        referenceFoot.position,
                        targetFoot.position,
                        out evaluatorXzFirstOffset))
                {
                    return;
                }
            }

            if (!ShouldApplyPostSetHumanPoseRightEndpointPositionFrameGate())
            {
                return;
            }

            Vector3 nextFootPosition;
            PostSetHumanPoseEndpointPositionDiagnostics endpointDiagnostics;
            bool calculated;
            if (usePostSetHumanPoseRightFootEvaluatorXzReference)
            {
                calculated = TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
                    referenceFoot.position,
                    targetFoot.position,
                    evaluatorXzFirstOffset,
                    postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                    postSetHumanPoseRightEndpointPositionReferenceWeight,
                    postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    out nextFootPosition,
                    out endpointDiagnostics);
            }
            else
            {
                if (!TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                        footBone,
                        referenceHips,
                        targetHips,
                        targetFoot,
                        out Vector3 desiredFootPosition))
                {
                    return;
                }

                Vector3 desiredToesPosition = BuildNaNVector3();
                if (targetToes != null)
                {
                    TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                        toesBone,
                        referenceHips,
                        targetHips,
                        targetToes,
                        out desiredToesPosition);
                }

                calculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                    desiredFootPosition,
                    desiredToesPosition,
                    targetFoot.position,
                    targetToes != null ? targetToes.position : BuildNaNVector3(),
                    postSetHumanPoseRightEndpointPositionReferenceWeight,
                    postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    out nextFootPosition,
                    out endpointDiagnostics);
            }

            if (!calculated)
            {
                RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
                    endpointDiagnostics,
                    maxYawAngle: float.NaN,
                    yawCorrectionAngle: float.NaN,
                    upperLegRotationDeltaAngle: float.NaN,
                    applied: 0f);
                return;
            }

            float maxAngleDegrees = CalculateEndpointPositionMaxYawAngle(
                targetFoot.position,
                targetUpperLeg.position,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            if (!TryCalculateEditorFootHipsAlignedResidualYawReference(
                    nextFootPosition,
                    targetFoot.position,
                    targetUpperLeg.position,
                    targetUpperLeg.rotation,
                    1f,
                    maxAngleDegrees,
                    out Quaternion nextWorldRotation,
                    out float yawCorrectionAngle))
            {
                RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
                    endpointDiagnostics,
                    maxAngleDegrees,
                    yawCorrectionAngle,
                    upperLegRotationDeltaAngle: float.NaN,
                    applied: 0f);
                return;
            }

            float upperLegRotationDeltaAngle = Quaternion.Angle(targetUpperLeg.rotation, nextWorldRotation);
            RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
                endpointDiagnostics,
                maxAngleDegrees,
                yawCorrectionAngle,
                upperLegRotationDeltaAngle,
                applied: 1f);

            targetUpperLeg.rotation = nextWorldRotation;
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            out Vector3 nextFootPosition)
        {
            return TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight: 1f,
                out nextFootPosition);
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            out Vector3 nextFootPosition)
        {
            return TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                out nextFootPosition,
                out _);
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            out Vector3 nextFootPosition,
            out PostSetHumanPoseEndpointPositionDiagnostics diagnostics)
        {
            nextFootPosition = currentFootPosition;
            diagnostics = PostSetHumanPoseEndpointPositionDiagnostics.Empty;
            diagnostics.DesiredFootPosition = desiredFootPosition;
            diagnostics.DesiredToesPosition = desiredToesPosition;
            diagnostics.CurrentFootPosition = currentFootPosition;
            diagnostics.CurrentToesPosition = currentToesPosition;

            if (!IsFinite(desiredFootPosition) ||
                !IsFinite(currentFootPosition))
            {
                return false;
            }

            Vector3 footDelta = desiredFootPosition - currentFootPosition;
            footDelta.y = 0f;
            Vector3 endpointDelta = footDelta;
            if (IsFinite(desiredToesPosition) && IsFinite(currentToesPosition))
            {
                Vector3 toesDelta = desiredToesPosition - currentToesPosition;
                toesDelta.y = 0f;
                Vector3 averagedEndpointDelta = (footDelta + toesDelta) * 0.5f;
                endpointDelta = Vector3.Lerp(footDelta, averagedEndpointDelta, Mathf.Clamp01(toesBlendWeight));
            }
            diagnostics.EndpointDeltaBeforeClamp = endpointDelta;

            if (!IsFinite(endpointDelta) || endpointDelta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                endpointDelta = Vector3.ClampMagnitude(endpointDelta, clampedMaxOffset);
            }
            diagnostics.EndpointDeltaAfterClamp = endpointDelta;

            if (endpointDelta.z > 0f)
            {
                endpointDelta.z *= Mathf.Clamp01(positiveZScale);
            }
            diagnostics.EndpointDeltaAfterPositiveZScale = endpointDelta;

            Vector3 correction = endpointDelta * Mathf.Clamp01(weight);
            correction.y = 0f;
            diagnostics.Correction = correction;
            if (!IsFinite(correction) || correction.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            nextFootPosition = currentFootPosition + correction;
            nextFootPosition.y = currentFootPosition.y;
            diagnostics.NextFootPosition = nextFootPosition;
            return IsFinite(nextFootPosition);
        }

        private bool TryResolvePostSetHumanPoseRightFootEvaluatorXzFirstOffset(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            out Vector3 firstOffset)
        {
            firstOffset = _postSetHumanPoseRightFootEvaluatorXzFirstOffset;
            if (!IsFinite(referenceFootPosition) || !IsFinite(currentFootPosition))
            {
                return false;
            }

            if (!_hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset ||
                !IsFinite(_postSetHumanPoseRightFootEvaluatorXzFirstOffset))
            {
                _postSetHumanPoseRightFootEvaluatorXzFirstOffset = currentFootPosition - referenceFootPosition;
                _postSetHumanPoseRightFootEvaluatorXzFirstOffset.y = 0f;
                _hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset =
                    IsFinite(_postSetHumanPoseRightFootEvaluatorXzFirstOffset);
            }

            firstOffset = _postSetHumanPoseRightFootEvaluatorXzFirstOffset;
            return _hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset && IsFinite(firstOffset);
        }

        private static bool TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition)
        {
            return TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
                referenceFootPosition,
                currentFootPosition,
                firstMatchedFootOffset,
                targetMagnitude,
                weight,
                maxOffset,
                out nextFootPosition,
                out _);
        }

        private static bool TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition,
            out PostSetHumanPoseEndpointPositionDiagnostics diagnostics)
        {
            nextFootPosition = currentFootPosition;
            diagnostics = PostSetHumanPoseEndpointPositionDiagnostics.Empty;
            diagnostics.CurrentFootPosition = currentFootPosition;
            diagnostics.CurrentToesPosition = BuildNaNVector3();
            diagnostics.EvaluatorXzReferenceEnabled = 1f;
            diagnostics.EvaluatorXzFirstOffset = firstMatchedFootOffset;
            diagnostics.EvaluatorXzTargetMagnitude = Mathf.Max(0f, targetMagnitude);

            if (!IsFinite(referenceFootPosition) ||
                !IsFinite(currentFootPosition) ||
                !IsFinite(firstMatchedFootOffset))
            {
                return false;
            }

            Vector3 normalizedDelta = currentFootPosition - referenceFootPosition - firstMatchedFootOffset;
            normalizedDelta.y = 0f;
            diagnostics.EvaluatorXzNormalizedDelta = normalizedDelta;
            diagnostics.DesiredToesPosition = BuildNaNVector3();
            if (!IsFinite(normalizedDelta) || normalizedDelta.sqrMagnitude <= 0.00000001f)
            {
                diagnostics.DesiredFootPosition = currentFootPosition;
                return false;
            }

            float magnitude = normalizedDelta.magnitude;
            float clampedTargetMagnitude = Mathf.Max(0f, targetMagnitude);
            if (!IsFinite(magnitude) || magnitude <= clampedTargetMagnitude || magnitude <= 0f)
            {
                diagnostics.DesiredFootPosition = currentFootPosition;
                return false;
            }

            Vector3 desiredNormalizedDelta = normalizedDelta * (clampedTargetMagnitude / magnitude);
            diagnostics.EvaluatorXzDesiredNormalizedDelta = desiredNormalizedDelta;
            Vector3 correction = desiredNormalizedDelta - normalizedDelta;
            correction.y = 0f;
            diagnostics.EndpointDeltaBeforeClamp = correction;

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                correction = Vector3.ClampMagnitude(correction, clampedMaxOffset);
            }
            diagnostics.EndpointDeltaAfterClamp = correction;
            diagnostics.EndpointDeltaAfterPositiveZScale = correction;

            correction *= Mathf.Clamp01(weight);
            correction.y = 0f;
            diagnostics.Correction = correction;
            if (!IsFinite(correction) || correction.sqrMagnitude <= 0.00000001f)
            {
                diagnostics.DesiredFootPosition = currentFootPosition;
                return false;
            }

            nextFootPosition = currentFootPosition + correction;
            nextFootPosition.y = currentFootPosition.y;
            diagnostics.DesiredFootPosition = nextFootPosition;
            diagnostics.NextFootPosition = nextFootPosition;
            return IsFinite(nextFootPosition);
        }

        private static float CalculateEndpointPositionMaxYawAngle(
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            float maxOffset)
        {
            Vector3 currentOffset = currentFootPosition - pivotPosition;
            currentOffset.y = 0f;
            float radius = currentOffset.magnitude;
            if (!IsFinite(currentOffset) || radius <= 0.0001f)
            {
                return 0f;
            }

            float normalizedOffset = Mathf.Clamp01(Mathf.Max(0f, maxOffset) / radius);
            if (normalizedOffset <= 0f)
            {
                return 0f;
            }

            return Mathf.Asin(normalizedOffset) * Mathf.Rad2Deg;
        }

        private void ApplyEditorHumanoidBipedIkFootPositionReference()
        {
            if (!useManualAnimatorBipedIkFootPositionReference ||
                manualAnimatorBipedIkFootPositionReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                DisableOwnedEditorManualFootBipedIk();
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            BipedIK bipedIk = EnsureEditorManualFootBipedIk();
            if (bipedIk == null)
            {
                return;
            }

            Transform referenceHips = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (referenceHips == null || targetHips == null)
            {
                return;
            }

            int changed = 0;
            changed += ApplyEditorHumanoidBipedIkFootPositionReferenceGoal(
                bipedIk,
                AvatarIKGoal.LeftFoot,
                HumanBodyBones.LeftFoot,
                referenceHips,
                targetHips);
            changed += ApplyEditorHumanoidBipedIkFootPositionReferenceGoal(
                bipedIk,
                AvatarIKGoal.RightFoot,
                HumanBodyBones.RightFoot,
                referenceHips,
                targetHips);

            if (changed <= 0)
            {
                return;
            }

            bipedIk.UpdateSolverExternal();
            if (!_editorFootIkPositionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator BipedIK foot position reference applied. feet={changed}, weight={manualAnimatorBipedIkFootPositionReferenceWeight:F2}, maxOffset={manualAnimatorBipedIkFootPositionReferenceMaxOffset:F3}m");
                _editorFootIkPositionReferenceLogged = true;
            }
        }

        private BipedIK EnsureEditorManualFootBipedIk()
        {
            if (targetAnimator == null)
            {
                return null;
            }

            if (_editorManualFootBipedIk == null)
            {
                _editorManualFootBipedIk = targetAnimator.GetComponent<BipedIK>();
                if (_editorManualFootBipedIk == null)
                {
                    _editorManualFootBipedIk = targetAnimator.gameObject.AddComponent<BipedIK>();
                    _editorManualFootBipedIkCreated = true;
                }
                _editorManualFootBipedIkInitiated = false;
            }

            if (_editorManualFootBipedIk == null)
            {
                return null;
            }

            if (targetAnimator.isHuman)
            {
                BipedReferences references = _editorManualFootBipedIk.references;
                BipedReferences.AutoDetectReferences(
                    ref references,
                    targetAnimator.transform,
                    BipedReferences.AutoDetectParams.Default);
                _editorManualFootBipedIk.references = references;
            }

            _editorManualFootBipedIk.enabled = true;
            _editorManualFootBipedIk.fixTransforms = false;
            if (!_editorManualFootBipedIkInitiated)
            {
                _editorManualFootBipedIk.InitiateBipedIK();
                _editorManualFootBipedIkInitiated = true;
            }
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            _editorManualFootBipedIk.solvers.leftFoot.maintainRotationWeight = 1f;
            _editorManualFootBipedIk.solvers.rightFoot.maintainRotationWeight = 1f;
            return _editorManualFootBipedIk;
        }

        private void DisableOwnedEditorManualFootBipedIk()
        {
            if (_editorManualFootBipedIk == null)
            {
                return;
            }

            _editorManualFootBipedIk.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            _editorManualFootBipedIk.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            if (_editorManualFootBipedIkCreated)
            {
                _editorManualFootBipedIk.fixTransforms = false;
                _editorManualFootBipedIk.enabled = false;
            }
        }

        private int ApplyEditorHumanoidBipedIkFootPositionReferenceGoal(
            BipedIK bipedIk,
            AvatarIKGoal goal,
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips)
        {
            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            if (referenceFoot == null || targetFoot == null)
            {
                bipedIk.SetIKPositionWeight(goal, 0f);
                return 0;
            }

            if (!TryCalculateEditorFootIkPositionReference(
                    referenceFoot.position,
                    referenceHips.position,
                    targetFoot.position,
                    targetHips.position,
                    manualAnimatorBipedIkFootPositionReferenceWeight,
                    manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                    out Vector3 nextPosition))
            {
                bipedIk.SetIKPositionWeight(goal, 0f);
                return 0;
            }

            bipedIk.SetIKPosition(goal, nextPosition);
            bipedIk.SetIKPositionWeight(goal, 1f);
            return 1;
        }

        private static bool TryCalculateEditorFootIkPositionReference(
            Vector3 referenceFootPosition,
            Vector3 referenceHipsPosition,
            Vector3 currentFootPosition,
            Vector3 targetHipsPosition,
            float weight,
            float maxOffset,
            out Vector3 nextPosition)
        {
            nextPosition = currentFootPosition;
            if (!IsFinite(referenceFootPosition) ||
                !IsFinite(referenceHipsPosition) ||
                !IsFinite(currentFootPosition) ||
                !IsFinite(targetHipsPosition))
            {
                return false;
            }

            Vector3 desiredPosition = targetHipsPosition + (referenceFootPosition - referenceHipsPosition);
            Vector3 delta = desiredPosition - currentFootPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                delta = Vector3.ClampMagnitude(delta, clampedMaxOffset);
            }

            nextPosition = currentFootPosition + delta * Mathf.Clamp01(weight);
            if (!IsFinite(nextPosition))
            {
                nextPosition = currentFootPosition;
                return false;
            }

            return true;
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

        private bool ShouldSuppressCompetingManualThumbOverrideEditor(bool leftHand)
        {
            if (!TryEvaluateThumbManualOverrideRisk(leftHand, out float risk) ||
                risk < ManualThumbOverrideSuppressRiskThreshold)
            {
                return false;
            }

            return !ShouldKeepDetachedHelperManualThumbOverrides(leftHand);
        }

        private bool ShouldKeepDetachedHelperManualThumbOverridesEditor(bool leftHand)
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

        private bool TryGetHighRiskManualThumbPoseConstraintOverridesEditor(
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

        private string BuildThumbHelperRelationshipDebugSummaryEditor(bool leftHand)
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
            float currentDistance = Vector3.Distance(effectiveHelperWorldPosition, sourceTransform.position);

            Quaternion relativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
            float rotationDelta = float.NaN;
            if (IsFinite(relativeRotation))
            {
                rotationDelta = Quaternion.Angle(initialRelativeRotation, relativeRotation);
            }

            return TryCalculateThumbHelperRelationshipRisk(
                currentDistance,
                initialDistance,
                rotationDelta,
                spreadRisk,
                projectionRisk,
                out helperDistanceRisk,
                out helperRotationRisk,
                out webbingRisk);
        }

        private static bool TryCalculateThumbHelperRelationshipRisk(
            float currentDistance,
            float initialDistance,
            float rotationDelta,
            float spreadRisk,
            float projectionRisk,
            out float helperDistanceRisk,
            out float helperRotationRisk,
            out float webbingRisk)
        {
            helperDistanceRisk = float.NaN;
            helperRotationRisk = float.NaN;
            webbingRisk = float.NaN;

            if (IsFinite(currentDistance) && IsFinite(initialDistance))
            {
                helperDistanceRisk = RiskAbove(
                    Mathf.Abs(currentDistance - initialDistance),
                    ManualThumbHelperDistanceDeltaWarning,
                    ManualThumbHelperDistanceDeltaFullRisk);
            }

            if (IsFinite(rotationDelta))
            {
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

        private Transform GetCachedThumbBaseHelperEditor(bool leftHand)
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

        private Transform GetCachedExplicitThumbBaseSourceEditor(bool leftHand)
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
            _editorFootLocalRotationReferenceLogged = false;
            _editorLowerBodySegmentDirectionReferenceLogged = false;
            _editorFootHipsAlignedResidualYawReferenceLogged = false;
            _editorHipsLocalPositionReferenceLogged = false;
            _editorBodyPositionXzReferenceLogged = false;
            _hasEditorReferenceBodyPosition = false;
            _hasEditorReferenceHipsRestLocalPosition = false;
            _hasEditorReferenceLowestFootRestY = false;
            _allowEditorFootHeightGroundingReference = false;
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

        private bool ShouldApplyManualFullBodyPoseReferenceMuscle(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return true;
            }

            string muscleName = HumanTrait.MuscleName[muscleIndex];
            if (manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly)
            {
                return IsRightSleeveChainPoseMuscle(muscleName);
            }

            if (manualAnimatorFullBodyPoseRightArmMusclesOnly)
            {
                return IsRightArmPoseMuscle(muscleName);
            }

            if (manualAnimatorFullBodyPoseLeftArmMusclesOnly)
            {
                return IsLeftArmPoseMuscle(muscleName);
            }

            bool isLowerBody = IsLowerBodyMuscle(muscleName);
            if (ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly)
            {
                return IsLegTwistOrInOutMuscle(muscleName);
            }

            if (ShouldApplyManualAnimatorFullBodyLowerMusclesOnly)
            {
                return isLowerBody;
            }

            return !ShouldExcludeManualAnimatorFullBodyLowerMuscles || !isLowerBody;
        }

        private bool ShouldApplyManualFullBodyPoseReferenceFrameGate()
        {
            float start = Mathf.Max(0f, manualAnimatorFullBodyPoseFrameGateStart);
            float end = Mathf.Max(0f, manualAnimatorFullBodyPoseFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end <= 0f || end < start)
            {
                end = start;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 1f, 240f);
            int currentFrame = Mathf.RoundToInt(_legacyAnimationDriver.CurrentTime * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private bool ShouldApplyYybRightSleeveSilhouetteLocalOffsetFrameGate()
        {
            float start = Mathf.Max(0f, yybRightSleeveSilhouetteLocalOffsetFrameGateStart);
            float end = Mathf.Max(0f, yybRightSleeveSilhouetteLocalOffsetFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end <= 0f || end < start)
            {
                end = start;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 1f, 240f);
            int currentFrame = Mathf.RoundToInt(_legacyAnimationDriver.CurrentTime * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private void ApplyYybRightSleeveSilhouetteLocalOffsetReference()
        {
            RestoreYybRightSleeveSilhouetteLocalOffsetReference();
            if (!useYybRightSleeveSilhouetteLocalOffsetReference ||
                !ShouldApplyYybRightSleeveSilhouetteLocalOffsetFrameGate())
            {
                return;
            }

            float offsetX = Mathf.Clamp(yybRightSleeveSilhouetteLocalOffsetX, -0.2f, 0.2f);
            if (Mathf.Abs(offsetX) <= 0.00001f)
            {
                return;
            }

            Vector3 offset = new Vector3(offsetX, 0f, 0f);
            ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(
                targetAnimator != null ? targetAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null,
                offset);
            ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(
                targetAnimator != null ? targetAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null,
                offset);
            ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(
                targetAnimator != null ? targetAnimator.GetBoneTransform(HumanBodyBones.RightHand) : null,
                offset);
            for (int i = 0; i < RightSleeveSilhouetteLocalOffsetTransformSuffixes.Length; i++)
            {
                Transform target = FindTargetTransformByNameSuffix(
                    RightSleeveSilhouetteLocalOffsetTransformSuffixes[i]);
                ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(target, offset);
            }
        }

        private void ApplyYybRightSleeveSilhouetteLocalOffsetToTransform(Transform target, Vector3 offset)
        {
            if (target == null ||
                !IsFinite(target.localPosition) ||
                !IsFinite(offset) ||
                _rightSleeveSilhouetteLocalOffsetBaseLocalPositions.ContainsKey(target))
            {
                return;
            }

            _rightSleeveSilhouetteLocalOffsetBaseLocalPositions[target] = target.localPosition;
            target.localPosition += offset;
        }

        private void RestoreYybRightSleeveSilhouetteLocalOffsetReference()
        {
            if (_rightSleeveSilhouetteLocalOffsetBaseLocalPositions.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Vector3> entry in _rightSleeveSilhouetteLocalOffsetBaseLocalPositions)
            {
                if (entry.Key != null && IsFinite(entry.Value))
                {
                    entry.Key.localPosition = entry.Value;
                }
            }

            _rightSleeveSilhouetteLocalOffsetBaseLocalPositions.Clear();
        }

        private static bool IsLowerBodyMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            return normalized.Contains("upperleg") ||
                   normalized.Contains("lowerleg") ||
                   normalized.Contains("foot") ||
                   normalized.Contains("toes");
        }

        private static bool IsLegTwistOrInOutMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            bool isLeg = normalized.Contains("upperleg") ||
                         normalized.Contains("lowerleg") ||
                         normalized.Contains("foot");
            if (!isLeg)
            {
                return false;
            }

            return normalized.Contains("inout") ||
                   normalized.Contains("twist");
        }

        private static bool IsRightArmPoseMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            if (!normalized.Contains("right"))
            {
                return false;
            }

            if (normalized.Contains("thumb") ||
                normalized.Contains("index") ||
                normalized.Contains("middle") ||
                normalized.Contains("ring") ||
                normalized.Contains("little"))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private static bool IsLeftArmPoseMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            if (!normalized.Contains("left"))
            {
                return false;
            }

            if (normalized.Contains("thumb") ||
                normalized.Contains("index") ||
                normalized.Contains("middle") ||
                normalized.Contains("ring") ||
                normalized.Contains("little"))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private static bool IsRightSleeveChainPoseMuscle(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(muscleName);
            if (normalized.Contains("spine") ||
                normalized.Contains("chest") ||
                normalized.Contains("upperchest"))
            {
                return true;
            }

            if (!normalized.Contains("right"))
            {
                return false;
            }

            if (normalized.Contains("thumb") ||
                normalized.Contains("index") ||
                normalized.Contains("middle") ||
                normalized.Contains("ring") ||
                normalized.Contains("little") ||
                normalized.Contains("hand"))
            {
                return false;
            }

            return normalized.Contains("shoulder") ||
                   normalized.Contains("arm") ||
                   normalized.Contains("forearm");
        }

        private void AlignRetargetPoseInputWithEditorHumanoidMuscleReference(ref HumanPose pose)
        {
            if (!_useEditorHumanoidMuscleReference || pose.muscles == null || _editorHumanoidMuscleCurves.Count == 0)
            {
                return;
            }

            float time = _legacyAnimationDriver.CurrentTime;
            foreach (KeyValuePair<int, AnimationCurve> pair in _editorHumanoidMuscleCurves)
            {
                if (pair.Key < 0 || pair.Key >= pose.muscles.Length || pair.Value == null)
                {
                    continue;
                }

                float referenceValue = pair.Value.Evaluate(time);
                pose.muscles[pair.Key] = AlignRetargetPoseInputWithEditorReference(
                    pair.Key,
                    pose.muscles[pair.Key],
                    referenceValue);
            }
        }

        private static bool ShouldUseEditorHumanoidMuscleReference(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            if (IsLeftUpperArmTwistMuscle(normalized))
            {
                return false;
            }

            if (normalized.Contains("forearm") && normalized.Contains("stretch"))
            {
                return false;
            }

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

        private static bool ShouldApplyEditorHumanoidMuscleReferenceValue(int muscleIndex, float referenceValue)
        {
            if (!ShouldUseEditorHumanoidMuscleReference(muscleIndex) || !IsFinite(referenceValue))
            {
                return false;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            if (IsRightUpperArmTwistMuscle(normalized) && Mathf.Abs(referenceValue) > 1f)
            {
                return false;
            }

            return true;
        }

        private static void TransformRetargetPoseInputMuscles(ref HumanPose pose)
        {
            if (pose.muscles == null)
            {
                return;
            }

            for (int i = 0; i < pose.muscles.Length; i++)
            {
                pose.muscles[i] = TransformRetargetPoseInputMuscleValue(i, pose.muscles[i]);
            }
        }

        private static float TransformRetargetPoseInputMuscleValue(int muscleIndex, float value)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return value;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            if (IsLeftUpperArmTwistMuscle(normalized))
            {
                return -value;
            }

            return value;
        }

        private static float AlignRetargetPoseInputWithEditorReference(int muscleIndex, float value, float referenceValue)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return value;
            }

            string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
            bool isLeftUpperArmTwist = IsLeftUpperArmTwistMuscle(normalized);
            bool isRightUpperArmTwist = IsRightUpperArmTwistMuscle(normalized);
            if ((!isLeftUpperArmTwist && !isRightUpperArmTwist) ||
                !IsFinite(value) ||
                !IsFinite(referenceValue) ||
                Mathf.Approximately(value, 0f) ||
                Mathf.Approximately(referenceValue, 0f))
            {
                return value;
            }

            float absReference = Mathf.Abs(referenceValue);
            float magnitudeTolerance = absReference <= 1f
                ? UpperArmTwistReferenceSignMagnitudeTolerance
                : UpperArmTwistOverrangeReferenceSignMagnitudeTolerance;
            if (absReference > UpperArmTwistReferenceSignMaxAbs ||
                Mathf.Abs(Mathf.Abs(value) - absReference) > magnitudeTolerance)
            {
                return value;
            }

            if (isLeftUpperArmTwist && Mathf.Sign(value) != Mathf.Sign(referenceValue))
            {
                return -value;
            }

            if (isRightUpperArmTwist &&
                absReference >= RightUpperArmTwistReferenceSignMinAbs &&
                Mathf.Sign(value) == Mathf.Sign(referenceValue))
            {
                return -value;
            }

            return value;
        }

        private static bool IsLeftUpperArmTwistMuscle(string normalizedMuscleName)
        {
            return !string.IsNullOrEmpty(normalizedMuscleName) &&
                normalizedMuscleName.Contains("left") &&
                normalizedMuscleName.Contains("arm") &&
                normalizedMuscleName.Contains("twist") &&
                !normalizedMuscleName.Contains("forearm");
        }

        private static bool IsRightUpperArmTwistMuscle(string normalizedMuscleName)
        {
            return !string.IsNullOrEmpty(normalizedMuscleName) &&
                normalizedMuscleName.Contains("right") &&
                normalizedMuscleName.Contains("arm") &&
                normalizedMuscleName.Contains("twist") &&
                !normalizedMuscleName.Contains("forearm");
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
#endif

        private float CalculateSafeScaleRatio(Transform ghostHip, Transform targetHip)
        {
            bool hasAnimatorScale = ghostAnimator != null && targetAnimator != null;
            float ghostHumanScale = hasAnimatorScale ? ghostAnimator.humanScale : 0f;
            float targetHumanScale = hasAnimatorScale ? targetAnimator.humanScale : 0f;
            bool hasHipPositions = ghostHip != null && targetHip != null;
            float ghostHipY = hasHipPositions ? ghostHip.position.y : 0f;
            float targetHipY = hasHipPositions ? targetHip.position.y : 0f;

            float ratio = RetargetingScaleRatioCalculator.CalculateSafeScaleRatio(
                _scaleRatio,
                hasAnimatorScale,
                ghostHumanScale,
                targetHumanScale,
                _initialGhostHipHeight,
                _initialTargetHipHeight,
                hasHipPositions,
                ghostHipY,
                targetHipY,
                out bool usedInvalidFallback);
            if (usedInvalidFallback)
            {
                LogPoseWarning("Invalid retarget scale ratio. Falling back to 1.0.");
            }

            return ratio;
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

        private static Vector3 SelectBodyPositionRootMotionSource(
            Vector3 poseBodyPosition,
            Vector3 manualReferenceBodyPosition,
            bool hasManualReferenceBodyPosition,
            bool preferManualReferenceXZ)
        {
            if (IsFinite(poseBodyPosition))
            {
                return poseBodyPosition;
            }

            if (preferManualReferenceXZ &&
                hasManualReferenceBodyPosition &&
                IsFinite(manualReferenceBodyPosition))
            {
                return manualReferenceBodyPosition;
            }

            return poseBodyPosition;
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
            if (!ShouldUseEditorHumanoidRootTranslationReference ||
                !_useEditorRootTranslationReference ||
                _editorRootTranslationX == null ||
                _editorRootTranslationZ == null)
            {
                ResetEditorHumanoidRootTranslationReferenceState();
                return Vector3.zero;
            }

            float time = _legacyAnimationDriver.CurrentTime;
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
            Vector3 editorRootDelta = CalculateEditorRootTranslationReferenceDelta(
                delta,
                ghostDelta,
                editorHumanoidRootTranslationWeight,
                editorHumanoidRootTranslationCurrentWeight,
                _hasSmoothedEditorRootTranslationDelta,
                _smoothedEditorRootTranslationDelta,
                out _smoothedEditorRootTranslationDelta,
                out _hasSmoothedEditorRootTranslationDelta,
                out bool skippedByGhostDelta,
                out bool skippedByNonFinite);
            if (skippedByGhostDelta || skippedByNonFinite)
            {
                return Vector3.zero;
            }

            if (!_editorRootTranslationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid RootT translation reference applied at t={time:F3}s.");
                _editorRootTranslationReferenceLogged = true;
            }

            return editorRootDelta;
#else
            return Vector3.zero;
#endif
        }

        private static Vector3 CalculateEditorRootTranslationReferenceDelta(
            Vector3 rawEditorDelta,
            Vector3 ghostDelta,
            float editorRootTranslationWeight,
            float editorRootTranslationCurrentWeight,
            bool hasSmoothedEditorRootTranslationDelta,
            Vector3 previousSmoothedEditorRootTranslationDelta,
            out Vector3 nextSmoothedEditorRootTranslationDelta,
            out bool nextHasSmoothedEditorRootTranslationDelta,
            out bool skippedByGhostDelta,
            out bool skippedByNonFinite)
        {
            nextSmoothedEditorRootTranslationDelta = previousSmoothedEditorRootTranslationDelta;
            nextHasSmoothedEditorRootTranslationDelta = hasSmoothedEditorRootTranslationDelta;
            skippedByGhostDelta = false;
            skippedByNonFinite = false;

            if (!IsFinite(rawEditorDelta))
            {
                skippedByNonFinite = true;
                return Vector3.zero;
            }

            if (FlattenXZ(ghostDelta).sqrMagnitude > 0.00000025f)
            {
                skippedByGhostDelta = true;
                return Vector3.zero;
            }

            Vector3 weightedDelta = rawEditorDelta;
            weightedDelta.y = 0f;
            weightedDelta *= Mathf.Clamp01(editorRootTranslationWeight);

            if (!hasSmoothedEditorRootTranslationDelta)
            {
                nextSmoothedEditorRootTranslationDelta = weightedDelta;
                nextHasSmoothedEditorRootTranslationDelta = true;
                return weightedDelta;
            }

            float currentWeight = Mathf.Clamp(editorRootTranslationCurrentWeight, 0.05f, 1f);
            nextSmoothedEditorRootTranslationDelta = Vector3.Lerp(previousSmoothedEditorRootTranslationDelta, weightedDelta, currentWeight);
            nextHasSmoothedEditorRootTranslationDelta = true;
            return nextSmoothedEditorRootTranslationDelta;
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

        private static bool TryCalculateManualAnimatorBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 referenceBodyPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            out Vector3 nextBodyPosition)
        {
            nextBodyPosition = currentBodyPosition;
            if (!IsFinite(currentBodyPosition) || !IsFinite(referenceBodyPosition))
            {
                return false;
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight <= 0f)
            {
                return false;
            }

            Vector3 delta = new Vector3(
                (referenceBodyPosition.x - currentBodyPosition.x) * Mathf.Clamp01(axisXScale),
                0f,
                (referenceBodyPosition.z - currentBodyPosition.z) * Mathf.Clamp01(axisZScale));
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                float magnitude = delta.magnitude;
                if (magnitude > clampedMaxOffset)
                {
                    delta = delta / magnitude * clampedMaxOffset;
                }
            }

            nextBodyPosition = new Vector3(
                currentBodyPosition.x + delta.x * clampedWeight,
                currentBodyPosition.y,
                currentBodyPosition.z + delta.z * clampedWeight);
            return IsFinite(nextBodyPosition);
        }

        private static bool TryCalculateSignCorrectedRowLocalBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 ghostFootPosition,
            Vector3 currentFootPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            out Vector3 nextBodyPosition)
        {
            return TryCalculateSignCorrectedRowLocalBodyPositionXzReference(
                currentBodyPosition,
                ghostFootPosition,
                currentFootPosition,
                weight,
                maxOffset,
                axisXScale,
                axisZScale,
                invertX: false,
                invertZ: false,
                out nextBodyPosition);
        }

        private static bool TryCalculateSignCorrectedRowLocalBodyPositionXzReference(
            Vector3 currentBodyPosition,
            Vector3 ghostFootPosition,
            Vector3 currentFootPosition,
            float weight,
            float maxOffset,
            float axisXScale,
            float axisZScale,
            bool invertX,
            bool invertZ,
            out Vector3 nextBodyPosition)
        {
            nextBodyPosition = currentBodyPosition;
            if (!IsFinite(currentBodyPosition) ||
                !IsFinite(ghostFootPosition) ||
                !IsFinite(currentFootPosition))
            {
                return false;
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight <= 0f)
            {
                return false;
            }

            Vector3 delta = ghostFootPosition - currentFootPosition;
            delta = new Vector3(
                delta.x * Mathf.Clamp01(axisXScale),
                0f,
                delta.z * Mathf.Clamp01(axisZScale));
            if (invertX)
            {
                delta.x = -delta.x;
            }
            if (invertZ)
            {
                delta.z = -delta.z;
            }
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                float magnitude = delta.magnitude;
                if (magnitude > clampedMaxOffset)
                {
                    delta = delta / magnitude * clampedMaxOffset;
                }
            }

            nextBodyPosition = new Vector3(
                currentBodyPosition.x + delta.x * clampedWeight,
                currentBodyPosition.y,
                currentBodyPosition.z + delta.z * clampedWeight);
            return IsFinite(nextBodyPosition);
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

        private void ResetRetargetPoseStageDiagnostics()
        {
            _lastPoseInputLeftShoulderFrontBackMuscle = float.NaN;
            _lastAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle = float.NaN;
            _lastAfterClampPoseMusclesLeftShoulderFrontBackMuscle = float.NaN;
            _lastAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle = float.NaN;
            _lastAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputLeftShoulderFrontBackMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftShoulderFrontBackMuscle = float.NaN;
            _lastPoseInputLeftArmTwistMuscle = float.NaN;
            _lastAfterEditorMuscleReferenceLeftArmTwistMuscle = float.NaN;
            _lastAfterClampPoseMusclesLeftArmTwistMuscle = float.NaN;
            _lastAfterAnatomicalArmGuardLeftArmTwistMuscle = float.NaN;
            _lastAfterVisualSpikeSmoothingLeftArmTwistMuscle = float.NaN;
            _lastSetHumanPoseInputLeftArmTwistMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftArmTwistMuscle = float.NaN;
            _lastPoseInputLeftForearmStretchMuscle = float.NaN;
            _lastAfterEditorMuscleReferenceLeftForearmStretchMuscle = float.NaN;
            _lastAfterClampPoseMusclesLeftForearmStretchMuscle = float.NaN;
            _lastAfterAnatomicalArmGuardLeftForearmStretchMuscle = float.NaN;
            _lastAfterVisualSpikeSmoothingLeftForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseInputLeftForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftForearmStretchMuscle = float.NaN;
            _lastPoseInputRightForearmStretchMuscle = float.NaN;
            _lastAfterEditorMuscleReferenceRightForearmStretchMuscle = float.NaN;
            _lastAfterClampPoseMusclesRightForearmStretchMuscle = float.NaN;
            _lastAfterAnatomicalArmGuardRightForearmStretchMuscle = float.NaN;
            _lastAfterVisualSpikeSmoothingRightForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseInputRightForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputRightForearmStretchMuscle = float.NaN;
            _lastPoseInputRightArmTwistMuscle = float.NaN;
            _lastAfterEditorMuscleReferenceRightArmTwistMuscle = float.NaN;
            _lastAfterClampPoseMusclesRightArmTwistMuscle = float.NaN;
            _lastAfterAnatomicalArmGuardRightArmTwistMuscle = float.NaN;
            _lastAfterVisualSpikeSmoothingRightArmTwistMuscle = float.NaN;
            _lastSetHumanPoseInputRightArmTwistMuscle = float.NaN;
            _lastSetHumanPoseOutputRightArmTwistMuscle = float.NaN;
            _lastSetHumanPoseInputLeftUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputRightUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseOutputRightUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputLeftLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseInputRightLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputRightLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseInputLeftFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseInputRightFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseOutputRightFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseInputBodyPosition = BuildNaNVector3();
            _lastSetHumanPoseOutputBodyPosition = BuildNaNVector3();
            _lastSetHumanPoseInputBodyRotation = BuildNaNQuaternion();
            _lastSetHumanPoseOutputBodyRotation = BuildNaNQuaternion();
            ResetSetHumanPosePreSolveBasisDiagnostics();
            ResetSetHumanPoseExtendedInputDiagnostics();
            _lastEditorFootLocalRotationLeftFootXzDelta = float.NaN;
            _lastEditorFootLocalRotationRightFootXzDelta = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftFootXzDelta = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightFootXzDelta = float.NaN;
            ResetEditorLowerBodySegmentDirectionDetailedDiagnostics();
            _lastEditorFootHipsAlignedResidualYawLeftFootXzDelta = float.NaN;
            _lastEditorFootHipsAlignedResidualYawRightFootXzDelta = float.NaN;
            ResetPostSetHumanPoseRightEndpointPositionDiagnostics();
            _lastRetargetStageGhostEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastSetHumanPosePreSolveGhostEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastSetHumanPosePreSolveTargetEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastRetargetStageAfterSetHumanPoseEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastRetargetStageAfterManualReferencesEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastRetargetStageAfterRootRestoreEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastRetargetStageAfterRootDeltaEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastRetargetStageAfterGroundingEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastRetargetStageAfterBipedIKEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastRetargetStageAfterLateVisualGroundingEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            ResetRetargetEndpointStageAttributionDiagnostics();
        }

        private void CapturePoseInputDiagnostics(HumanPose pose)
        {
            _lastPoseInputLeftShoulderFrontBackMuscle = ReadLeftShoulderFrontBackMuscle(pose);
            _lastPoseInputLeftArmTwistMuscle = ReadLeftArmTwistMuscle(pose);
            _lastPoseInputLeftForearmStretchMuscle = ReadLeftForearmStretchMuscle(pose);
            _lastPoseInputRightForearmStretchMuscle = ReadRightForearmStretchMuscle(pose);
            _lastPoseInputRightArmTwistMuscle = ReadRightArmTwistMuscle(pose);
        }

        private void CaptureAfterEditorMuscleReferenceDiagnostics(HumanPose pose)
        {
            _lastAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle = ReadLeftShoulderFrontBackMuscle(pose);
            _lastAfterEditorMuscleReferenceLeftArmTwistMuscle = ReadLeftArmTwistMuscle(pose);
            _lastAfterEditorMuscleReferenceLeftForearmStretchMuscle = ReadLeftForearmStretchMuscle(pose);
            _lastAfterEditorMuscleReferenceRightForearmStretchMuscle = ReadRightForearmStretchMuscle(pose);
            _lastAfterEditorMuscleReferenceRightArmTwistMuscle = ReadRightArmTwistMuscle(pose);
        }

        private void CaptureAfterClampPoseMusclesDiagnostics(HumanPose pose)
        {
            _lastAfterClampPoseMusclesLeftShoulderFrontBackMuscle = ReadLeftShoulderFrontBackMuscle(pose);
            _lastAfterClampPoseMusclesLeftArmTwistMuscle = ReadLeftArmTwistMuscle(pose);
            _lastAfterClampPoseMusclesLeftForearmStretchMuscle = ReadLeftForearmStretchMuscle(pose);
            _lastAfterClampPoseMusclesRightForearmStretchMuscle = ReadRightForearmStretchMuscle(pose);
            _lastAfterClampPoseMusclesRightArmTwistMuscle = ReadRightArmTwistMuscle(pose);
        }

        private void CaptureAfterAnatomicalArmGuardDiagnostics(HumanPose pose)
        {
            _lastAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle = ReadLeftShoulderFrontBackMuscle(pose);
            _lastAfterAnatomicalArmGuardLeftArmTwistMuscle = ReadLeftArmTwistMuscle(pose);
            _lastAfterAnatomicalArmGuardLeftForearmStretchMuscle = ReadLeftForearmStretchMuscle(pose);
            _lastAfterAnatomicalArmGuardRightForearmStretchMuscle = ReadRightForearmStretchMuscle(pose);
            _lastAfterAnatomicalArmGuardRightArmTwistMuscle = ReadRightArmTwistMuscle(pose);
        }

        private void CaptureAfterVisualSpikeSmoothingDiagnostics(HumanPose pose)
        {
            _lastAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle = ReadLeftShoulderFrontBackMuscle(pose);
            _lastAfterVisualSpikeSmoothingLeftArmTwistMuscle = ReadLeftArmTwistMuscle(pose);
            _lastAfterVisualSpikeSmoothingLeftForearmStretchMuscle = ReadLeftForearmStretchMuscle(pose);
            _lastAfterVisualSpikeSmoothingRightForearmStretchMuscle = ReadRightForearmStretchMuscle(pose);
            _lastAfterVisualSpikeSmoothingRightArmTwistMuscle = ReadRightArmTwistMuscle(pose);
        }

        private void CaptureSetHumanPoseInputDiagnostics(HumanPose pose)
        {
            _lastSetHumanPoseInputLeftShoulderFrontBackMuscle = ReadLeftShoulderFrontBackMuscle(pose);
            _lastSetHumanPoseOutputLeftShoulderFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputLeftArmTwistMuscle = ReadLeftArmTwistMuscle(pose);
            _lastSetHumanPoseOutputLeftArmTwistMuscle = float.NaN;
            _lastSetHumanPoseInputLeftForearmStretchMuscle = ReadLeftForearmStretchMuscle(pose);
            _lastSetHumanPoseOutputLeftForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseInputRightForearmStretchMuscle = ReadRightForearmStretchMuscle(pose);
            _lastSetHumanPoseOutputRightForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseInputRightArmTwistMuscle = ReadRightArmTwistMuscle(pose);
            _lastSetHumanPoseOutputRightArmTwistMuscle = float.NaN;
            _lastSetHumanPoseInputLeftUpperLegFrontBackMuscle = ReadLeftUpperLegFrontBackMuscle(pose);
            _lastSetHumanPoseOutputLeftUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputRightUpperLegFrontBackMuscle = ReadRightUpperLegFrontBackMuscle(pose);
            _lastSetHumanPoseOutputRightUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputLeftLowerLegStretchMuscle = ReadLeftLowerLegStretchMuscle(pose);
            _lastSetHumanPoseOutputLeftLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseInputRightLowerLegStretchMuscle = ReadRightLowerLegStretchMuscle(pose);
            _lastSetHumanPoseOutputRightLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseInputLeftFootUpDownMuscle = ReadLeftFootUpDownMuscle(pose);
            _lastSetHumanPoseOutputLeftFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseInputRightFootUpDownMuscle = ReadRightFootUpDownMuscle(pose);
            _lastSetHumanPoseOutputRightFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseInputBodyPosition = IsFinite(pose.bodyPosition) ? pose.bodyPosition : BuildNaNVector3();
            _lastSetHumanPoseOutputBodyPosition = BuildNaNVector3();
            _lastSetHumanPoseInputBodyRotation = IsFinite(pose.bodyRotation) ? pose.bodyRotation : BuildNaNQuaternion();
            _lastSetHumanPoseOutputBodyRotation = BuildNaNQuaternion();
            CaptureSetHumanPosePreSolveBasisDiagnostics(pose);
            CaptureSetHumanPoseExtendedInputDiagnostics(pose);
        }

        private void ResetSetHumanPosePreSolveBasisDiagnostics()
        {
            _lastSetHumanPosePreSolveGhostRootWorldPosition = BuildNaNVector3();
            _lastSetHumanPosePreSolveGhostRootWorldRotation = BuildNaNQuaternion();
            _lastSetHumanPosePreSolveTargetRootWorldPosition = BuildNaNVector3();
            _lastSetHumanPosePreSolveTargetRootWorldRotation = BuildNaNQuaternion();
            _lastSetHumanPosePreSolveTargetHipsWorldPosition = BuildNaNVector3();
            _lastSetHumanPosePreSolveTargetHipsLocalPosition = BuildNaNVector3();
            _lastSetHumanPosePreSolveBodyPosition = BuildNaNVector3();
            _lastSetHumanPosePreSolveBodyRotation = BuildNaNQuaternion();
            _lastSetHumanPosePreSolveGhostEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastSetHumanPosePreSolveCurrentEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            _lastSetHumanPosePreSolveTargetEndpointPositions = RetargetEndpointStageWorldPositions.Empty;
            ResetPreSetHumanPoseEndpointBodyPositionDiagnostics();
        }

        private void CaptureSetHumanPosePreSolveBasisDiagnostics(HumanPose pose)
        {
            _lastSetHumanPosePreSolveGhostRootWorldPosition = ReadAnimatorRootWorldPosition(ghostAnimator);
            _lastSetHumanPosePreSolveGhostRootWorldRotation = ReadAnimatorRootWorldRotation(ghostAnimator);
            _lastSetHumanPosePreSolveTargetRootWorldPosition = ReadAnimatorRootWorldPosition(targetAnimator);
            _lastSetHumanPosePreSolveTargetRootWorldRotation = ReadAnimatorRootWorldRotation(targetAnimator);
            _lastSetHumanPosePreSolveTargetHipsWorldPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.Hips);
            _lastSetHumanPosePreSolveTargetHipsLocalPosition = ReadAnimatorBoneLocalPosition(targetAnimator, HumanBodyBones.Hips);
            _lastSetHumanPosePreSolveBodyPosition = IsFinite(pose.bodyPosition) ? pose.bodyPosition : BuildNaNVector3();
            _lastSetHumanPosePreSolveBodyRotation = IsFinite(pose.bodyRotation) ? pose.bodyRotation : BuildNaNQuaternion();
            _lastSetHumanPosePreSolveGhostEndpointPositions = CaptureEndpointStageWorldPositions(ghostAnimator);
            _lastSetHumanPosePreSolveTargetEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);
        }

        private void ResetSetHumanPoseExtendedInputDiagnostics()
        {
            _lastSetHumanPoseInputSpineFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputSpineLeftRightMuscle = float.NaN;
            _lastSetHumanPoseInputSpineTwistLeftRightMuscle = float.NaN;
            _lastSetHumanPoseInputChestFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputChestLeftRightMuscle = float.NaN;
            _lastSetHumanPoseInputChestTwistLeftRightMuscle = float.NaN;
            _lastSetHumanPoseInputUpperChestFrontBackMuscle = float.NaN;
            _lastSetHumanPoseInputUpperChestLeftRightMuscle = float.NaN;
            _lastSetHumanPoseInputUpperChestTwistLeftRightMuscle = float.NaN;
            _lastSetHumanPoseInputLeftUpperLegInOutMuscle = float.NaN;
            _lastSetHumanPoseInputRightUpperLegInOutMuscle = float.NaN;
            _lastSetHumanPoseInputLeftUpperLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseInputRightUpperLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseInputLeftLowerLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseInputRightLowerLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseInputLeftFootTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseInputRightFootTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseInputLeftToesUpDownMuscle = float.NaN;
            _lastSetHumanPoseInputRightToesUpDownMuscle = float.NaN;
            _lastSetHumanPoseOutputRightUpperLegInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightUpperLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightLowerLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightFootTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightToesUpDownMuscle = float.NaN;
        }

        private void CaptureSetHumanPoseExtendedInputDiagnostics(HumanPose pose)
        {
            _lastSetHumanPoseInputSpineFrontBackMuscle = ReadSpineFrontBackMuscle(pose);
            _lastSetHumanPoseInputSpineLeftRightMuscle = ReadSpineLeftRightMuscle(pose);
            _lastSetHumanPoseInputSpineTwistLeftRightMuscle = ReadSpineTwistLeftRightMuscle(pose);
            _lastSetHumanPoseInputChestFrontBackMuscle = ReadChestFrontBackMuscle(pose);
            _lastSetHumanPoseInputChestLeftRightMuscle = ReadChestLeftRightMuscle(pose);
            _lastSetHumanPoseInputChestTwistLeftRightMuscle = ReadChestTwistLeftRightMuscle(pose);
            _lastSetHumanPoseInputUpperChestFrontBackMuscle = ReadUpperChestFrontBackMuscle(pose);
            _lastSetHumanPoseInputUpperChestLeftRightMuscle = ReadUpperChestLeftRightMuscle(pose);
            _lastSetHumanPoseInputUpperChestTwistLeftRightMuscle = ReadUpperChestTwistLeftRightMuscle(pose);
            _lastSetHumanPoseInputLeftUpperLegInOutMuscle = ReadLeftUpperLegInOutMuscle(pose);
            _lastSetHumanPoseInputRightUpperLegInOutMuscle = ReadRightUpperLegInOutMuscle(pose);
            _lastSetHumanPoseInputLeftUpperLegTwistInOutMuscle = ReadLeftUpperLegTwistInOutMuscle(pose);
            _lastSetHumanPoseInputRightUpperLegTwistInOutMuscle = ReadRightUpperLegTwistInOutMuscle(pose);
            _lastSetHumanPoseInputLeftLowerLegTwistInOutMuscle = ReadLeftLowerLegTwistInOutMuscle(pose);
            _lastSetHumanPoseInputRightLowerLegTwistInOutMuscle = ReadRightLowerLegTwistInOutMuscle(pose);
            _lastSetHumanPoseInputLeftFootTwistInOutMuscle = ReadLeftFootTwistInOutMuscle(pose);
            _lastSetHumanPoseInputRightFootTwistInOutMuscle = ReadRightFootTwistInOutMuscle(pose);
            _lastSetHumanPoseInputLeftToesUpDownMuscle = ReadLeftToesUpDownMuscle(pose);
            _lastSetHumanPoseInputRightToesUpDownMuscle = ReadRightToesUpDownMuscle(pose);
        }

        private void CaptureSetHumanPoseOutputDiagnostics()
        {
            _lastSetHumanPoseOutputLeftShoulderFrontBackMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftArmTwistMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputRightForearmStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputRightArmTwistMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseOutputRightUpperLegFrontBackMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputRightLowerLegStretchMuscle = float.NaN;
            _lastSetHumanPoseOutputLeftFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseOutputRightFootUpDownMuscle = float.NaN;
            _lastSetHumanPoseOutputRightUpperLegInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightUpperLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightLowerLegTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightFootTwistInOutMuscle = float.NaN;
            _lastSetHumanPoseOutputRightToesUpDownMuscle = float.NaN;
            _lastSetHumanPoseOutputBodyPosition = BuildNaNVector3();
            _lastSetHumanPoseOutputBodyRotation = BuildNaNQuaternion();
            if (_targetHandler == null)
            {
                return;
            }

            _targetHandler.GetHumanPose(ref _appliedTargetPose);
            _lastSetHumanPoseOutputLeftShoulderFrontBackMuscle = ReadLeftShoulderFrontBackMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputLeftArmTwistMuscle = ReadLeftArmTwistMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputLeftForearmStretchMuscle = ReadLeftForearmStretchMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightForearmStretchMuscle = ReadRightForearmStretchMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightArmTwistMuscle = ReadRightArmTwistMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputLeftUpperLegFrontBackMuscle = ReadLeftUpperLegFrontBackMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightUpperLegFrontBackMuscle = ReadRightUpperLegFrontBackMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputLeftLowerLegStretchMuscle = ReadLeftLowerLegStretchMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightLowerLegStretchMuscle = ReadRightLowerLegStretchMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputLeftFootUpDownMuscle = ReadLeftFootUpDownMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightFootUpDownMuscle = ReadRightFootUpDownMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightUpperLegInOutMuscle = ReadRightUpperLegInOutMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightUpperLegTwistInOutMuscle = ReadRightUpperLegTwistInOutMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightLowerLegTwistInOutMuscle = ReadRightLowerLegTwistInOutMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightFootTwistInOutMuscle = ReadRightFootTwistInOutMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputRightToesUpDownMuscle = ReadRightToesUpDownMuscle(_appliedTargetPose);
            _lastSetHumanPoseOutputBodyPosition = IsFinite(_appliedTargetPose.bodyPosition)
                ? _appliedTargetPose.bodyPosition
                : BuildNaNVector3();
            _lastSetHumanPoseOutputBodyRotation = IsFinite(_appliedTargetPose.bodyRotation)
                ? _appliedTargetPose.bodyRotation
                : BuildNaNQuaternion();
        }

        private float ReadLeftShoulderFrontBackMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftShoulderFrontBackMuscleIndex());
        }

        private float ReadRightForearmStretchMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightForearmStretchMuscleIndex());
        }

        private float ReadLeftForearmStretchMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftForearmStretchMuscleIndex());
        }

        private float ReadLeftArmTwistMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftArmTwistMuscleIndex());
        }

        private float ReadRightArmTwistMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightArmTwistMuscleIndex());
        }

        private float ReadLeftUpperLegFrontBackMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftUpperLegFrontBackMuscleIndex());
        }

        private float ReadRightUpperLegFrontBackMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightUpperLegFrontBackMuscleIndex());
        }

        private float ReadLeftLowerLegStretchMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftLowerLegStretchMuscleIndex());
        }

        private float ReadRightLowerLegStretchMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightLowerLegStretchMuscleIndex());
        }

        private float ReadLeftFootUpDownMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftFootUpDownMuscleIndex());
        }

        private float ReadRightFootUpDownMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightFootUpDownMuscleIndex());
        }

        private float ReadSpineFrontBackMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseSpineFrontBackMuscleIndex());
        }

        private float ReadSpineLeftRightMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseSpineLeftRightMuscleIndex());
        }

        private float ReadSpineTwistLeftRightMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseSpineTwistLeftRightMuscleIndex());
        }

        private float ReadChestFrontBackMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseChestFrontBackMuscleIndex());
        }

        private float ReadChestLeftRightMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseChestLeftRightMuscleIndex());
        }

        private float ReadChestTwistLeftRightMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseChestTwistLeftRightMuscleIndex());
        }

        private float ReadUpperChestFrontBackMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseUpperChestFrontBackMuscleIndex());
        }

        private float ReadUpperChestLeftRightMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseUpperChestLeftRightMuscleIndex());
        }

        private float ReadUpperChestTwistLeftRightMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseUpperChestTwistLeftRightMuscleIndex());
        }

        private float ReadLeftUpperLegInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftUpperLegInOutMuscleIndex());
        }

        private float ReadRightUpperLegInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightUpperLegInOutMuscleIndex());
        }

        private float ReadLeftUpperLegTwistInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftUpperLegTwistInOutMuscleIndex());
        }

        private float ReadRightUpperLegTwistInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightUpperLegTwistInOutMuscleIndex());
        }

        private float ReadLeftLowerLegTwistInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftLowerLegTwistInOutMuscleIndex());
        }

        private float ReadRightLowerLegTwistInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightLowerLegTwistInOutMuscleIndex());
        }

        private float ReadLeftFootTwistInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftFootTwistInOutMuscleIndex());
        }

        private float ReadRightFootTwistInOutMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightFootTwistInOutMuscleIndex());
        }

        private float ReadLeftToesUpDownMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseLeftToesUpDownMuscleIndex());
        }

        private float ReadRightToesUpDownMuscle(HumanPose pose)
        {
            return ReadHumanMuscleValue(pose, GetSetHumanPoseRightToesUpDownMuscleIndex());
        }

        private int GetSetHumanPoseLeftShoulderFrontBackMuscleIndex()
        {
            if (_setHumanPoseLeftShoulderFrontBackMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftShoulderFrontBackMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "shoulder", "frontback");
            }

            return _setHumanPoseLeftShoulderFrontBackMuscleIndex;
        }

        private int GetSetHumanPoseLeftArmTwistMuscleIndex()
        {
            if (_setHumanPoseLeftArmTwistMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftArmTwistMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "arm", "twist");
            }

            return _setHumanPoseLeftArmTwistMuscleIndex;
        }

        private int GetSetHumanPoseRightForearmStretchMuscleIndex()
        {
            if (_setHumanPoseRightForearmStretchMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightForearmStretchMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "forearm", "stretch");
            }

            return _setHumanPoseRightForearmStretchMuscleIndex;
        }

        private int GetSetHumanPoseLeftForearmStretchMuscleIndex()
        {
            if (_setHumanPoseLeftForearmStretchMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftForearmStretchMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "forearm", "stretch");
            }

            return _setHumanPoseLeftForearmStretchMuscleIndex;
        }

        private int GetSetHumanPoseRightArmTwistMuscleIndex()
        {
            if (_setHumanPoseRightArmTwistMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightArmTwistMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "arm", "twist");
            }

            return _setHumanPoseRightArmTwistMuscleIndex;
        }

        private int GetSetHumanPoseLeftUpperLegFrontBackMuscleIndex()
        {
            if (_setHumanPoseLeftUpperLegFrontBackMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftUpperLegFrontBackMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "upperleg", "frontback");
            }

            return _setHumanPoseLeftUpperLegFrontBackMuscleIndex;
        }

        private int GetSetHumanPoseRightUpperLegFrontBackMuscleIndex()
        {
            if (_setHumanPoseRightUpperLegFrontBackMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightUpperLegFrontBackMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "upperleg", "frontback");
            }

            return _setHumanPoseRightUpperLegFrontBackMuscleIndex;
        }

        private int GetSetHumanPoseLeftLowerLegStretchMuscleIndex()
        {
            if (_setHumanPoseLeftLowerLegStretchMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftLowerLegStretchMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "lowerleg", "stretch");
            }

            return _setHumanPoseLeftLowerLegStretchMuscleIndex;
        }

        private int GetSetHumanPoseRightLowerLegStretchMuscleIndex()
        {
            if (_setHumanPoseRightLowerLegStretchMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightLowerLegStretchMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "lowerleg", "stretch");
            }

            return _setHumanPoseRightLowerLegStretchMuscleIndex;
        }

        private int GetSetHumanPoseLeftFootUpDownMuscleIndex()
        {
            if (_setHumanPoseLeftFootUpDownMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftFootUpDownMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "foot", "updown");
            }

            return _setHumanPoseLeftFootUpDownMuscleIndex;
        }

        private int GetSetHumanPoseRightFootUpDownMuscleIndex()
        {
            if (_setHumanPoseRightFootUpDownMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightFootUpDownMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "foot", "updown");
            }

            return _setHumanPoseRightFootUpDownMuscleIndex;
        }

        private int GetSetHumanPoseSpineFrontBackMuscleIndex()
        {
            if (_setHumanPoseSpineFrontBackMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseSpineFrontBackMuscleIndex =
                    FindHumanMuscleIndexByTokens("spine", "frontback");
            }

            return _setHumanPoseSpineFrontBackMuscleIndex;
        }

        private int GetSetHumanPoseSpineLeftRightMuscleIndex()
        {
            if (_setHumanPoseSpineLeftRightMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseSpineLeftRightMuscleIndex =
                    FindHumanMuscleIndexByTokens("spine", "leftright");
            }

            return _setHumanPoseSpineLeftRightMuscleIndex;
        }

        private int GetSetHumanPoseSpineTwistLeftRightMuscleIndex()
        {
            if (_setHumanPoseSpineTwistLeftRightMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseSpineTwistLeftRightMuscleIndex =
                    FindHumanMuscleIndexByTokens("spine", "twist");
            }

            return _setHumanPoseSpineTwistLeftRightMuscleIndex;
        }

        private int GetSetHumanPoseChestFrontBackMuscleIndex()
        {
            if (_setHumanPoseChestFrontBackMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseChestFrontBackMuscleIndex =
                    FindHumanMuscleIndexByTokens("chest", "frontback");
            }

            return _setHumanPoseChestFrontBackMuscleIndex;
        }

        private int GetSetHumanPoseChestLeftRightMuscleIndex()
        {
            if (_setHumanPoseChestLeftRightMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseChestLeftRightMuscleIndex =
                    FindHumanMuscleIndexByTokens("chest", "leftright");
            }

            return _setHumanPoseChestLeftRightMuscleIndex;
        }

        private int GetSetHumanPoseChestTwistLeftRightMuscleIndex()
        {
            if (_setHumanPoseChestTwistLeftRightMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseChestTwistLeftRightMuscleIndex =
                    FindHumanMuscleIndexByTokens("chest", "twist");
            }

            return _setHumanPoseChestTwistLeftRightMuscleIndex;
        }

        private int GetSetHumanPoseUpperChestFrontBackMuscleIndex()
        {
            if (_setHumanPoseUpperChestFrontBackMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseUpperChestFrontBackMuscleIndex =
                    FindHumanMuscleIndexByTokens("upperchest", "frontback");
            }

            return _setHumanPoseUpperChestFrontBackMuscleIndex;
        }

        private int GetSetHumanPoseUpperChestLeftRightMuscleIndex()
        {
            if (_setHumanPoseUpperChestLeftRightMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseUpperChestLeftRightMuscleIndex =
                    FindHumanMuscleIndexByTokens("upperchest", "leftright");
            }

            return _setHumanPoseUpperChestLeftRightMuscleIndex;
        }

        private int GetSetHumanPoseUpperChestTwistLeftRightMuscleIndex()
        {
            if (_setHumanPoseUpperChestTwistLeftRightMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseUpperChestTwistLeftRightMuscleIndex =
                    FindHumanMuscleIndexByTokens("upperchest", "twist");
            }

            return _setHumanPoseUpperChestTwistLeftRightMuscleIndex;
        }

        private int GetSetHumanPoseLeftUpperLegInOutMuscleIndex()
        {
            if (_setHumanPoseLeftUpperLegInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftUpperLegInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "upperleg", "inout");
            }

            return _setHumanPoseLeftUpperLegInOutMuscleIndex;
        }

        private int GetSetHumanPoseRightUpperLegInOutMuscleIndex()
        {
            if (_setHumanPoseRightUpperLegInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightUpperLegInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "upperleg", "inout");
            }

            return _setHumanPoseRightUpperLegInOutMuscleIndex;
        }

        private int GetSetHumanPoseLeftUpperLegTwistInOutMuscleIndex()
        {
            if (_setHumanPoseLeftUpperLegTwistInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftUpperLegTwistInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "upperleg", "twist");
            }

            return _setHumanPoseLeftUpperLegTwistInOutMuscleIndex;
        }

        private int GetSetHumanPoseRightUpperLegTwistInOutMuscleIndex()
        {
            if (_setHumanPoseRightUpperLegTwistInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightUpperLegTwistInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "upperleg", "twist");
            }

            return _setHumanPoseRightUpperLegTwistInOutMuscleIndex;
        }

        private int GetSetHumanPoseLeftLowerLegTwistInOutMuscleIndex()
        {
            if (_setHumanPoseLeftLowerLegTwistInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftLowerLegTwistInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "lowerleg", "twist");
            }

            return _setHumanPoseLeftLowerLegTwistInOutMuscleIndex;
        }

        private int GetSetHumanPoseRightLowerLegTwistInOutMuscleIndex()
        {
            if (_setHumanPoseRightLowerLegTwistInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightLowerLegTwistInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "lowerleg", "twist");
            }

            return _setHumanPoseRightLowerLegTwistInOutMuscleIndex;
        }

        private int GetSetHumanPoseLeftFootTwistInOutMuscleIndex()
        {
            if (_setHumanPoseLeftFootTwistInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftFootTwistInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "foot", "twist");
            }

            return _setHumanPoseLeftFootTwistInOutMuscleIndex;
        }

        private int GetSetHumanPoseRightFootTwistInOutMuscleIndex()
        {
            if (_setHumanPoseRightFootTwistInOutMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightFootTwistInOutMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "foot", "twist");
            }

            return _setHumanPoseRightFootTwistInOutMuscleIndex;
        }

        private int GetSetHumanPoseLeftToesUpDownMuscleIndex()
        {
            if (_setHumanPoseLeftToesUpDownMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseLeftToesUpDownMuscleIndex =
                    FindHumanMuscleIndexByTokens("left", "toes", "updown");
            }

            return _setHumanPoseLeftToesUpDownMuscleIndex;
        }

        private int GetSetHumanPoseRightToesUpDownMuscleIndex()
        {
            if (_setHumanPoseRightToesUpDownMuscleIndex == UnresolvedHumanMuscleIndex)
            {
                _setHumanPoseRightToesUpDownMuscleIndex =
                    FindHumanMuscleIndexByTokens("right", "toes", "updown");
            }

            return _setHumanPoseRightToesUpDownMuscleIndex;
        }

        private static float ReadHumanMuscleValue(HumanPose pose, int muscleIndex)
        {
            if (pose.muscles == null || muscleIndex < 0 || muscleIndex >= pose.muscles.Length)
            {
                return float.NaN;
            }

            float value = pose.muscles[muscleIndex];
            return IsFinite(value) ? value : float.NaN;
        }

        private static float CalculateFiniteAbsDelta(float a, float b)
        {
            return IsFinite(a) && IsFinite(b) ? Mathf.Abs(a - b) : float.NaN;
        }

        private static float CalculateFiniteXzDelta(Vector3 a, Vector3 b)
        {
            if (!IsFinite(a) || !IsFinite(b))
            {
                return float.NaN;
            }

            float deltaX = b.x - a.x;
            float deltaZ = b.z - a.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        private static float CalculateFiniteAngleDelta(Quaternion a, Quaternion b)
        {
            return IsFinite(a) && IsFinite(b) ? Quaternion.Angle(a, b) : float.NaN;
        }

        private static float ReadBodyRotationYaw(Quaternion rotation)
        {
            return IsFinite(rotation) ? rotation.eulerAngles.y : float.NaN;
        }

        private static int FindHumanMuscleIndexByTokens(params string[] tokens)
        {
            if (tokens == null || tokens.Length == 0)
            {
                return -1;
            }

            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string normalized = NormalizeHumanMuscleNameForDiagnostics(HumanTrait.MuscleName[i]);
                bool matched = true;
                foreach (string token in tokens)
                {
                    if (!normalized.Contains(NormalizeHumanMuscleNameForDiagnostics(token)))
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string NormalizeHumanMuscleNameForDiagnostics(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace(" ", string.Empty)
                    .Replace(".", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace("_", string.Empty)
                    .ToLowerInvariant();
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

        private void ApplySetHumanPoseRightLegTwistOutputReference(HumanPose inputPose)
        {
            if (!ShouldUseSetHumanPoseRightLegTwistOutputReference ||
                _targetHandler == null ||
                inputPose.muscles == null)
            {
                return;
            }

            _targetHandler.GetHumanPose(ref _appliedTargetPose);
            if (_appliedTargetPose.muscles == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(setHumanPoseRightLegTwistOutputReferenceWeight);
            float maxDelta = Mathf.Max(0f, setHumanPoseRightLegTwistOutputReferenceMaxDelta);
            if (weight <= 0f || maxDelta <= 0f)
            {
                return;
            }

            bool changed = ApplyBoundedSetHumanPoseRightLegTwistOutput(
                ref _appliedTargetPose,
                inputPose,
                GetSetHumanPoseRightUpperLegTwistInOutMuscleIndex(),
                weight,
                maxDelta);
            changed |= ApplyBoundedSetHumanPoseRightLegTwistOutput(
                ref _appliedTargetPose,
                inputPose,
                GetSetHumanPoseRightLowerLegTwistInOutMuscleIndex(),
                weight,
                maxDelta);

            if (changed)
            {
                _targetHandler.SetHumanPose(ref _appliedTargetPose);
            }
        }

        private static bool ApplyBoundedSetHumanPoseRightLegTwistOutput(
            ref HumanPose outputPose,
            HumanPose inputPose,
            int muscleIndex,
            float weight,
            float maxDelta)
        {
            if (muscleIndex < 0 ||
                inputPose.muscles == null ||
                outputPose.muscles == null ||
                muscleIndex >= inputPose.muscles.Length ||
                muscleIndex >= outputPose.muscles.Length)
            {
                return false;
            }

            float currentValue = outputPose.muscles[muscleIndex];
            float nextValue = CalculateBoundedSetHumanPoseRightLegTwistOutput(
                inputPose.muscles[muscleIndex],
                currentValue,
                weight,
                maxDelta,
                currentValue);
            if (!IsFinite(nextValue) || Mathf.Approximately(nextValue, currentValue))
            {
                return false;
            }

            outputPose.muscles[muscleIndex] = nextValue;
            return true;
        }

        private static float CalculateBoundedSetHumanPoseRightLegTwistOutput(
            float inputValue,
            float outputValue,
            float weight,
            float maxDelta,
            float fallbackValue)
        {
            if (!IsFinite(outputValue))
            {
                return IsFinite(fallbackValue) ? fallbackValue : outputValue;
            }

            if (!IsFinite(inputValue))
            {
                return outputValue;
            }

            float clampedCorrection = Mathf.Clamp(inputValue - outputValue, -Mathf.Max(0f, maxDelta), Mathf.Max(0f, maxDelta));
            return outputValue + clampedCorrection * Mathf.Clamp01(weight);
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

#if UNITY_EDITOR
        private bool TryEvaluateThumbManualReferenceFrameDeviationEditor(
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

        private bool TryEvaluateCurrentThumbReferenceFrameDeltaEditor(
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

        private bool TryEvaluateThumbLocalRotationOverrideRiskEditor(
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
#endif

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

            Vector3 currentPosition = targetAnimator.transform.position;
            bool shouldClamp = TryCalculateRootPositionSpikeClamp(
                positionBeforePose,
                currentPosition,
                maxRootDeltaPerFrame,
                out Vector3 clampedPosition,
                out float poseDeltaMagnitude);

            _lastRootPositionPoseDeltaMagnitude = poseDeltaMagnitude;
            if (!IsFinite(poseDeltaMagnitude))
            {
                return;
            }

            _maxRootPositionPoseDeltaMagnitude = Mathf.Max(_maxRootPositionPoseDeltaMagnitude, _lastRootPositionPoseDeltaMagnitude);
            if (!shouldClamp)
            {
                return;
            }

            _rootPositionSpikeClampedCount++;
            if (logRootDeltaSpikes && !_rootDeltaSpikeWarningLogged)
            {
                Debug.LogWarning($"[PoseSpaceRetargeter] {source} root position spike {_lastRootPositionPoseDeltaMagnitude:F3}m clamped. limit={maxRootDeltaPerFrame:F3}m");
                _rootDeltaSpikeWarningLogged = true;
            }

            targetAnimator.transform.position = clampedPosition;
        }

        private static Vector3 ApplyImplicitBodyPositionRootGuard(
            Vector3 positionBeforePose,
            Vector3 currentPosition,
            bool allowBodyPositionXZRootMotion)
        {
            return ApplyImplicitBodyPositionRootGuard(
                positionBeforePose,
                currentPosition,
                allowBodyPositionXZRootMotion,
                Vector3.zero);
        }

        private static Vector3 ApplyImplicitBodyPositionRootGuard(
            Vector3 positionBeforePose,
            Vector3 currentPosition,
            bool allowBodyPositionXZRootMotion,
            Vector3 explicitBodyRootDelta)
        {
            bool hasExplicitBodyRootMotion =
                IsFinite(explicitBodyRootDelta) &&
                FlattenXZ(explicitBodyRootDelta).sqrMagnitude > 0.0000000001f;

            if ((allowBodyPositionXZRootMotion && !hasExplicitBodyRootMotion) ||
                !IsFinite(positionBeforePose) ||
                !IsFinite(currentPosition))
            {
                return currentPosition;
            }

            return new Vector3(positionBeforePose.x, currentPosition.y, positionBeforePose.z);
        }

        private static Vector3 SelectImplicitRootGuardReference(
            Vector3 rootAnchorPosition,
            Vector3 positionBeforePose,
            float movementScaleMultiplier)
        {
            if (movementScaleMultiplier <= 0f && IsFinite(rootAnchorPosition))
            {
                return rootAnchorPosition;
            }

            return positionBeforePose;
        }

        private static Vector3 SelectPoseSolveRootPosition(
            Vector3 currentRootPosition,
            Vector3 rootAnchorPosition,
            bool isolateRootMotionFromPoseSolve)
        {
            if (!isolateRootMotionFromPoseSolve ||
                !IsFinite(currentRootPosition) ||
                !IsFinite(rootAnchorPosition))
            {
                return currentRootPosition;
            }

            return new Vector3(rootAnchorPosition.x, currentRootPosition.y, rootAnchorPosition.z);
        }

        private static Vector3 RestoreRootMotionCarrierPositionAfterPose(
            Vector3 rootMotionCarrierPositionBeforePose,
            Vector3 poseSolvedPosition,
            bool isolateRootMotionFromPoseSolve)
        {
            if (!isolateRootMotionFromPoseSolve ||
                !IsFinite(rootMotionCarrierPositionBeforePose) ||
                !IsFinite(poseSolvedPosition))
            {
                return poseSolvedPosition;
            }

            return new Vector3(
                rootMotionCarrierPositionBeforePose.x,
                poseSolvedPosition.y,
                rootMotionCarrierPositionBeforePose.z);
        }

        private static bool TryCalculateRootPositionSpikeClamp(
            Vector3 positionBeforePose,
            Vector3 currentPosition,
            float maxRootDeltaPerFrame,
            out Vector3 clampedPosition,
            out float deltaMagnitude)
        {
            clampedPosition = currentPosition;
            Vector3 poseDelta = currentPosition - positionBeforePose;
            if (!IsFinite(poseDelta))
            {
                deltaMagnitude = float.NaN;
                return false;
            }

            deltaMagnitude = poseDelta.magnitude;
            if (deltaMagnitude <= maxRootDeltaPerFrame)
            {
                return false;
            }

            clampedPosition = positionBeforePose + Vector3.ClampMagnitude(poseDelta, maxRootDeltaPerFrame);
            return true;
        }

        private void ClampTargetHipsLocalPositionSpike()
        {
            if (!clampTargetHipsLocalPositionSpikes || targetAnimator == null || !targetAnimator.isHuman)
            {
                ResetTargetHipsLocalPositionSpikeState();
                return;
            }

            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (targetHips == null)
            {
                ResetTargetHipsLocalPositionSpikeState();
                return;
            }

            Vector3 currentLocalPosition = targetHips.localPosition;
            if (!_hasPreviousTargetHipsLocalPosition)
            {
                RememberTargetHipsLocalPosition(currentLocalPosition);
                return;
            }

            bool shouldClamp = TryCalculateHipsLocalPositionSpikeClamp(
                _previousTargetHipsLocalPosition,
                currentLocalPosition,
                maxTargetHipsLocalPositionDeltaPerFrame,
                out Vector3 clampedLocalPosition,
                out float deltaMagnitude);

            _lastTargetHipsLocalPositionDelta = deltaMagnitude;
            if (!IsFinite(deltaMagnitude))
            {
                return;
            }

            _maxTargetHipsLocalPositionDelta = Mathf.Max(_maxTargetHipsLocalPositionDelta, deltaMagnitude);
            if (shouldClamp)
            {
                targetHips.localPosition = clampedLocalPosition;
                _targetHipsLocalPositionSpikeClampedCount++;
            }

            RememberTargetHipsLocalPosition(targetHips.localPosition);
        }

        private void RememberTargetHipsLocalPosition(Vector3 localPosition)
        {
            if (!IsFinite(localPosition))
            {
                return;
            }

            _previousTargetHipsLocalPosition = localPosition;
            _hasPreviousTargetHipsLocalPosition = true;
        }

        private void ResetTargetHipsLocalPositionSpikeState()
        {
            _previousTargetHipsLocalPosition = Vector3.zero;
            _hasPreviousTargetHipsLocalPosition = false;
            _lastTargetHipsLocalPositionDelta = float.NaN;
            _maxTargetHipsLocalPositionDelta = 0f;
            _targetHipsLocalPositionSpikeClampedCount = 0;
        }

        private static bool TryCalculateHipsLocalPositionSpikeClamp(
            Vector3 previousLocalPosition,
            Vector3 currentLocalPosition,
            float maxDeltaPerFrame,
            out Vector3 clampedPosition,
            out float deltaMagnitude)
        {
            clampedPosition = currentLocalPosition;
            Vector3 delta = currentLocalPosition - previousLocalPosition;
            if (!IsFinite(delta))
            {
                deltaMagnitude = float.NaN;
                return false;
            }

            deltaMagnitude = delta.magnitude;
            float clampedMaxDelta = Mathf.Max(0f, maxDeltaPerFrame);
            if (clampedMaxDelta <= 0f || deltaMagnitude <= clampedMaxDelta)
            {
                return false;
            }

            clampedPosition = previousLocalPosition + Vector3.ClampMagnitude(delta, clampedMaxDelta);
            return true;
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
            ResetTargetHipsLocalPositionSpikeState();
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
            _legacyAnimationDriver.ResetStabilityMetrics();
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
            _lastEditorFootHeightGroundingReferenceLift = float.NaN;
            _lateVisualGroundingWarningLogged = false;
            _rendererGroundingOutlierWarningLogged = false;
            _lateVisualGroundingInitialized = false;
            _appliedPoseClampWarningLogged = false;
            _targetHipsRestLocalPosition = Vector3.zero;
            _hasTargetHipsRestLocalPosition = false;

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
                Transform targetBone = targetAnimator.GetBoneTransform(bone);
                if (bone == HumanBodyBones.Hips)
                {
                    if (targetBone != null && IsFinite(targetBone.localPosition))
                    {
                        _targetHipsRestLocalPosition = targetBone.localPosition;
                        _hasTargetHipsRestLocalPosition = true;
                    }

                    continue;
                }

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
            if (!ShouldLockTargetHumanoidBonePositions)
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
            if (!TryCalculateFootBottomY(lFoot.position.y, footRadius, out float lBottom) ||
                !TryCalculateFootBottomY(rFoot.position.y, footRadius, out float rBottom))
            {
                LogPoseWarning("Foot position became non-finite. Skipping grounding for this frame.");
                return;
            }

            // 현재 가장 낮은 발바닥 높이. Renderer bounds가 발에서 너무 멀면
            // 옷/머리카락/소매 outlier로 보고 발 기준을 유지한다.
            float lowestFootCurrentY = Mathf.Min(lBottom, rBottom);
            float contactBottomY = ResolveGroundingContactBottomY(lowestFootCurrentY);

            // 목표는 지면(0) + Offset
            // Raycast를 사용하여 실제 지면을 찾을 수도 있으나, 현재는 평면(Plane) 위라고 가정하고 0.0f 사용
            // 만약 계단이나 경사면이라면 Physics.Raycast로 hit.point.y를 구해야 함.
            float targetGroundY = 0.0f; // 평면 가정
            float targetHeight = ResolveEditorFootHeightGroundingReferenceTarget(targetGroundY + groundOffset);
            _lastGroundingTargetY = targetGroundY;
            _lastGroundingLowestFootBottomY = contactBottomY;

            // 보정값 계산 (목표 - 현재)
            // 양수면 들어 올리고, 음수면 내림 (양방향)
            if (!TryCalculateGroundingAdjustment(targetHeight, contactBottomY, out float adjustment))
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

            bool wasGroundingInitialized = _groundingInitialized;
            float appliedVerticalStep = CalculateGroundingVerticalStep(
                currentPos.y,
                adjustment,
                wasGroundingInitialized,
                smoothGrounding,
                groundingSmoothing,
                maxGroundingVerticalStepPerFrame,
                groundingDeadZone,
                _lastGroundingVerticalStep,
                out bool skippedByDeadZone,
                out bool smoothedGrounding,
                out bool clampedGroundingStep);
            if (skippedByDeadZone)
            {
                _lastGroundingVerticalStep = 0f;
                return;
            }

            if (!wasGroundingInitialized)
            {
                _groundingInitialized = true;
            }

            if (smoothedGrounding)
            {
                _groundingSmoothedCount++;
            }

            if (clampedGroundingStep)
            {
                _groundingStepClampedCount++;
            }

            float clampedNextY = currentPos.y + appliedVerticalStep;
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

        private static bool TryCalculateGroundingAdjustment(
            float targetHeight,
            float contactBottomY,
            out float adjustment)
        {
            adjustment = targetHeight - contactBottomY;
            if (!IsFinite(adjustment))
            {
                adjustment = 0f;
                return false;
            }

            return true;
        }

        private float ResolveEditorFootHeightGroundingReferenceTarget(float baseTargetHeight)
        {
#if UNITY_EDITOR
            _lastEditorFootHeightGroundingReferenceLift = 0f;
            if (!ShouldUseManualAnimatorFootHeightGroundingReference ||
                !_allowEditorFootHeightGroundingReference ||
                manualAnimatorFootHeightGroundingReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null)
            {
                return baseTargetHeight;
            }

            if (!UpdateEditorManualReferenceAnimator() ||
                !TryGetAnimatorLowestFootY(_editorFingerReferenceAnimator, out float referenceCurrentLowestFootY))
            {
                return baseTargetHeight;
            }

            if (!_hasEditorReferenceLowestFootRestY)
            {
                _editorReferenceLowestFootRestY = referenceCurrentLowestFootY;
                _hasEditorReferenceLowestFootRestY = true;
                return baseTargetHeight;
            }

            if (TryCalculateEditorFootHeightGroundingReferenceTarget(
                    baseTargetHeight,
                    referenceCurrentLowestFootY,
                    _editorReferenceLowestFootRestY,
                    manualAnimatorFootHeightGroundingReferenceWeight,
                    manualAnimatorFootHeightGroundingReferenceMaxLift,
                    out float targetHeight))
            {
                _lastEditorFootHeightGroundingReferenceLift = targetHeight - baseTargetHeight;
                return targetHeight;
            }

            _lastEditorFootHeightGroundingReferenceLift = float.NaN;
            return baseTargetHeight;
#else
            return baseTargetHeight;
#endif
        }

        private static bool TryCalculateEditorFootHeightGroundingReferenceTarget(
            float baseTargetHeight,
            float referenceCurrentLowestFootY,
            float referenceRestLowestFootY,
            float weight,
            float maxLift,
            out float targetHeight)
        {
            targetHeight = baseTargetHeight;
            if (!IsFinite(baseTargetHeight) ||
                !IsFinite(referenceCurrentLowestFootY) ||
                !IsFinite(referenceRestLowestFootY) ||
                !IsFinite(weight) ||
                !IsFinite(maxLift))
            {
                return false;
            }

            float referenceLift = referenceCurrentLowestFootY - referenceRestLowestFootY;
            if (referenceLift <= 0f)
            {
                return true;
            }

            float weightedLift = referenceLift * Mathf.Clamp01(weight);
            if (maxLift > 0f)
            {
                weightedLift = Mathf.Min(weightedLift, maxLift);
            }

            targetHeight = baseTargetHeight + weightedLift;
            if (!IsFinite(targetHeight))
            {
                targetHeight = baseTargetHeight;
                return false;
            }

            return true;
        }

        private static float CalculateGroundingVerticalStep(
            float currentY,
            float adjustment,
            bool wasGroundingInitialized,
            bool smoothGrounding,
            float groundingSmoothing,
            float maxGroundingVerticalStepPerFrame,
            float groundingDeadZone,
            float previousGroundingVerticalStep,
            out bool skippedByDeadZone,
            out bool smoothed,
            out bool clamped)
        {
            skippedByDeadZone = false;
            smoothed = false;
            clamped = false;

            float deadZone = Mathf.Max(0f, groundingDeadZone);
            if (wasGroundingInitialized && Mathf.Abs(adjustment) <= deadZone)
            {
                skippedByDeadZone = true;
                return 0f;
            }

            float effectiveAdjustment = adjustment;
            if (wasGroundingInitialized && deadZone > 0f)
            {
                // Subtracting the dead zone prevents the root from chasing small foot noise.
                effectiveAdjustment = Mathf.Sign(adjustment) * Mathf.Max(0f, Mathf.Abs(adjustment) - deadZone);
            }

            float desiredY = currentY + effectiveAdjustment;
            float nextY = desiredY;
            if (wasGroundingInitialized && smoothGrounding)
            {
                float smoothing = Mathf.Clamp01(groundingSmoothing);
                if (smoothing < 1f)
                {
                    nextY = Mathf.Lerp(currentY, desiredY, smoothing);
                    smoothed = true;
                }

                float maxStep = Mathf.Max(0.001f, maxGroundingVerticalStepPerFrame);
                float verticalStep = nextY - currentY;
                if (IsGroundingDirectionReversal(verticalStep, previousGroundingVerticalStep))
                {
                    maxStep = Mathf.Max(0.001f, maxStep * GroundingDirectionReversalStepScale);
                }

                if (Mathf.Abs(verticalStep) > maxStep)
                {
                    nextY = currentY + Mathf.Sign(verticalStep) * maxStep;
                    clamped = true;
                }
            }

            return nextY - currentY;
        }

        private bool IsGroundingDirectionReversal(float verticalStep)
        {
            return IsGroundingDirectionReversal(verticalStep, _lastGroundingVerticalStep);
        }

        private static bool IsGroundingDirectionReversal(float verticalStep, float previousGroundingVerticalStep)
        {
            if (!IsFinite(previousGroundingVerticalStep) || Mathf.Abs(verticalStep) <= 0.0005f || Mathf.Abs(previousGroundingVerticalStep) <= 0.0005f)
            {
                return false;
            }

            return Mathf.Sign(verticalStep) != Mathf.Sign(previousGroundingVerticalStep);
        }

        public void ApplyLateVisualGroundingCorrection()
        {
            try
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
                float targetHeight = ResolveEditorFootHeightGroundingReferenceTarget(targetGroundY + groundOffset);
                _lastGroundingTargetY = targetGroundY;
                _lastGroundingLowestFootBottomY = rendererMinY;

                if (!TryCalculateGroundingAdjustment(targetHeight, rendererMinY, out float residual))
                {
                    LogPoseWarning("Late visual grounding residual became non-finite. Skipping final grounding for this frame.");
                    return;
                }

                if (ShouldSkipLateVisualGroundingForActiveVerticalStep(
                    residual,
                    smoothLateVisualGroundingCorrection,
                    _lastGroundingVerticalStep))
                {
                    _lateVisualGroundingInitialized = true;
                    return;
                }

                if (!TryCalculateLateVisualGroundingEffectiveResidual(
                    residual,
                    smoothLateVisualGroundingCorrection,
                    groundingDeadZone,
                    maxLateVisualGroundingCorrection,
                    out float effectiveResidual,
                    out bool exceededMaxCorrection))
                {
                    if (exceededMaxCorrection && !_lateVisualGroundingWarningLogged)
                    {
                        float maxCorrection = Mathf.Max(0.001f, maxLateVisualGroundingCorrection);
                        Debug.LogWarning($"[PoseSpaceRetargeter] Late visual grounding residual {residual:F3}m exceeded max {maxCorrection:F3}m. Skipping this frame to avoid collapsing a real jump.");
                        _lateVisualGroundingWarningLogged = true;
                    }

                    _lateVisualGroundingInitialized = true;
                    return;
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

                if (!TryCalculateLateVisualGroundingAppliedPosition(currentPos, appliedResidual, out Vector3 appliedPosition))
                {
                    LogPoseWarning("Target position became non-finite after late visual grounding. Skipping final grounding for this frame.");
                    return;
                }

                targetAnimator.transform.position = appliedPosition;
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
            finally
            {
                if (targetAnimator != null)
                {
                    _lastRetargetStageAfterLateVisualGroundingEndpointPositions = CaptureEndpointStageWorldPositions(targetAnimator);
                    CaptureRetargetEndpointStageAttributionDiagnostics();
                }
            }
        }

        private float CalculateLateVisualGroundingStep(float residual)
        {
            return CalculateLateVisualGroundingStep(
                residual,
                smoothLateVisualGroundingCorrection,
                _lateVisualGroundingInitialized,
                lateVisualGroundingSnapThreshold,
                lateVisualGroundingSmoothing,
                maxLateVisualGroundingStepPerFrame);
        }

        private static bool TryCalculateLateVisualGroundingEffectiveResidual(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            float groundingDeadZone,
            float maxLateVisualGroundingCorrection,
            out float effectiveResidual,
            out bool exceededMaxCorrection)
        {
            effectiveResidual = 0f;
            exceededMaxCorrection = false;

            bool isPenetrationResidual = residual > 0.0001f;
            bool isFloatingResidual = residual < -0.0001f;
            bool isVisualFloorResidual = isPenetrationResidual || isFloatingResidual;
            float deadZone = Mathf.Max(0.001f, groundingDeadZone);
            float skipDeadZone = isVisualFloorResidual ? 0.001f : deadZone;
            if (Mathf.Abs(residual) <= skipDeadZone)
            {
                return false;
            }

            float maxCorrection = Mathf.Max(0.001f, maxLateVisualGroundingCorrection);
            if (Mathf.Abs(residual) > maxCorrection)
            {
                exceededMaxCorrection = true;
                return false;
            }

            effectiveResidual = residual;
            if (smoothLateVisualGroundingCorrection && deadZone > 0f && !isVisualFloorResidual)
            {
                effectiveResidual = Mathf.Sign(residual) * Mathf.Max(0f, Mathf.Abs(residual) - deadZone);
                if (Mathf.Abs(effectiveResidual) <= 0.0001f)
                {
                    effectiveResidual = 0f;
                    return false;
                }
            }

            return true;
        }

        private static bool ShouldSkipLateVisualGroundingForActiveVerticalStep(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            float lastGroundingVerticalStep)
        {
            if (!smoothLateVisualGroundingCorrection ||
                !IsFinite(residual) ||
                !IsFinite(lastGroundingVerticalStep) ||
                Mathf.Abs(residual) <= 0.0005f ||
                Mathf.Abs(lastGroundingVerticalStep) <= 0.0005f)
            {
                return false;
            }

            return Mathf.Sign(residual) != Mathf.Sign(lastGroundingVerticalStep);
        }

        private static bool TryCalculateLateVisualGroundingAppliedPosition(
            Vector3 currentPosition,
            float appliedResidual,
            out Vector3 appliedPosition)
        {
            appliedPosition = Vector3.zero;
            if (!IsFinite(currentPosition))
            {
                return false;
            }

            appliedPosition = currentPosition;
            appliedPosition.y += appliedResidual;
            if (!IsFinite(appliedPosition))
            {
                appliedPosition = Vector3.zero;
                return false;
            }

            return true;
        }

        private static float CalculateLateVisualGroundingStep(
            float residual,
            bool smoothLateVisualGroundingCorrection,
            bool lateVisualGroundingInitialized,
            float lateVisualGroundingSnapThreshold,
            float lateVisualGroundingSmoothing,
            float maxLateVisualGroundingStepPerFrame)
        {
            if (!smoothLateVisualGroundingCorrection)
            {
                return residual;
            }

            if (!lateVisualGroundingInitialized)
            {
                return residual;
            }

            float snapThreshold = Mathf.Max(0.005f, lateVisualGroundingSnapThreshold);
            if (residual > 0.0001f && residual <= snapThreshold)
            {
                return residual;
            }

            bool isFloorPenetration = residual > 0.0001f;
            float smoothing = Mathf.Clamp01(lateVisualGroundingSmoothing);
            if (isFloorPenetration)
            {
                smoothing = Mathf.Max(smoothing, LateVisualGroundingPenetrationRecoverySmoothing);
            }

            float step = Mathf.Abs(residual) > snapThreshold
                ? residual * Mathf.Max(0.1f, smoothing)
                : residual * smoothing;
            float maxStep = Mathf.Max(0.001f, maxLateVisualGroundingStepPerFrame);
            if (isFloorPenetration)
            {
                maxStep = Mathf.Max(maxStep, LateVisualGroundingPenetrationRecoveryMaxStep);
            }

            if (Mathf.Abs(step) > maxStep)
            {
                step = Mathf.Sign(step) * maxStep;
            }

            return step;
        }

        private static bool TryGetAnimatorLowestFootY(Animator animator, out float lowestFootY)
        {
            lowestFootY = 0f;
            if (animator == null || !animator.isHuman)
            {
                return false;
            }

            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null || rightFoot == null)
            {
                return false;
            }

            Vector3 leftLocal = animator.transform.InverseTransformPoint(leftFoot.position);
            Vector3 rightLocal = animator.transform.InverseTransformPoint(rightFoot.position);
            lowestFootY = Mathf.Min(leftLocal.y, rightLocal.y);
            if (!IsFinite(lowestFootY))
            {
                lowestFootY = 0f;
                return false;
            }

            return true;
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
            return TryCalculateLowestFootBottomY(leftFoot.position.y, rightFoot.position.y, footRadius, out lowestFootBottomY);
        }

        private static bool TryCalculateLowestFootBottomY(
            float leftFootY,
            float rightFootY,
            float footRadius,
            out float lowestFootBottomY)
        {
            lowestFootBottomY = 0f;
            if (!TryCalculateFootBottomY(leftFootY, footRadius, out float leftBottom) ||
                !TryCalculateFootBottomY(rightFootY, footRadius, out float rightBottom))
            {
                return false;
            }

            lowestFootBottomY = Mathf.Min(leftBottom, rightBottom);
            return true;
        }

        private static bool TryCalculateFootBottomY(
            float footY,
            float footRadius,
            out float footBottomY)
        {
            footBottomY = footY - footRadius;
            if (!IsFinite(footBottomY))
            {
                footBottomY = 0f;
                return false;
            }

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

            if (!TryCalculateEstimatedFootRadius(leftFoot.position.y, rightFoot.position.y, rendererMinY, out float estimatedRadius))
            {
                return;
            }

            _estimatedFootRadius = estimatedRadius;
            _hasEstimatedFootRadius = true;
        }

        private static bool TryCalculateEstimatedFootRadius(
            float leftFootY,
            float rightFootY,
            float rendererMinY,
            out float estimatedRadius)
        {
            float lowestFootY = Mathf.Min(leftFootY, rightFootY);
            estimatedRadius = lowestFootY - rendererMinY;
            if (!IsFinite(estimatedRadius))
            {
                return false;
            }

            estimatedRadius = Mathf.Clamp(estimatedRadius, 0.02f, 0.16f);
            return true;
        }

        private float ResolveGroundingContactBottomY(float lowestFootBottomY)
        {
            bool hasRendererBounds = TryGetRendererBoundsMinY(out float rendererMinY);
            float contactBottomY = ResolvePrimaryGroundingContactBottomY(
                lowestFootBottomY,
                _hasEstimatedFootRadius,
                hasRendererBounds,
                rendererMinY,
                rejectRendererGroundingOutliers,
                maxRendererFootGroundingSeparation,
                out bool rendererGroundingOutlier);

            if (rendererGroundingOutlier && !_rendererGroundingOutlierWarningLogged)
            {
                float separation = Mathf.Abs(rendererMinY - lowestFootBottomY);
                float maxSeparation = Mathf.Max(0.02f, maxRendererFootGroundingSeparation);
                Debug.LogWarning($"[PoseSpaceRetargeter] Renderer bounds grounding outlier ignored. rendererMinY={rendererMinY:F3}, footBottomY={lowestFootBottomY:F3}, separation={separation:F3}, limit={maxSeparation:F3}");
                _rendererGroundingOutlierWarningLogged = true;
            }

            return contactBottomY;
        }

        private static float ResolvePrimaryGroundingContactBottomY(
            float lowestFootBottomY,
            bool hasEstimatedFootRadius,
            bool hasRendererBounds,
            float rendererMinY,
            bool rejectRendererGroundingOutliers,
            float maxRendererFootGroundingSeparation,
            out bool rendererGroundingOutlier)
        {
            // Estimated foot radius changes the foot-bottom input; it must not bypass nearby renderer contact.
            return ResolveGroundingContactBottomY(
                lowestFootBottomY,
                hasRendererBounds,
                rendererMinY,
                rejectRendererGroundingOutliers,
                maxRendererFootGroundingSeparation,
                out rendererGroundingOutlier);
        }

        private static float ResolveGroundingContactBottomY(
            float lowestFootBottomY,
            bool hasRendererBounds,
            float rendererMinY,
            bool rejectRendererGroundingOutliers,
            float maxRendererFootGroundingSeparation,
            out bool rendererGroundingOutlier)
        {
            rendererGroundingOutlier = false;
            if (!hasRendererBounds)
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

            rendererGroundingOutlier = true;
            return lowestFootBottomY;
        }

        private void ApplyGroundedFootLockXZ(Transform leftFoot, Transform rightFoot, float targetHeight, float footRadius)
        {
            if (!ShouldStabilizeGroundedFootXZ || groundedFootLockWeight <= 0f || targetAnimator == null)
            {
                _leftFootLocked = false;
                _rightFootLocked = false;
                return;
            }

            Vector3 correctionSum = Vector3.zero;
            int correctionCount = 0;
            AddFootLockCorrection(leftFoot, targetHeight, footRadius, ref _leftFootLocked, ref _leftFootLockPosition, ref correctionSum, ref correctionCount);
            AddFootLockCorrection(rightFoot, targetHeight, footRadius, ref _rightFootLocked, ref _rightFootLockPosition, ref correctionSum, ref correctionCount);
            if (!TryCalculateGroundedFootLockRootCorrection(
                correctionSum,
                correctionCount,
                groundedFootLockWeight,
                maxGroundedFootLockStep,
                out Vector3 correction))
            {
                return;
            }

            Vector3 rootPosition = targetAnimator.transform.position + correction;
            if (IsFinite(rootPosition))
            {
                targetAnimator.transform.position = rootPosition;
            }
        }

        private static bool TryCalculateGroundedFootLockRootCorrection(
            Vector3 correctionSum,
            int correctionCount,
            float groundedFootLockWeight,
            float maxGroundedFootLockStep,
            out Vector3 correction)
        {
            correction = Vector3.zero;
            if (correctionCount <= 0)
            {
                return false;
            }

            correction = correctionSum / correctionCount;
            correction.y = 0f;
            correction *= Mathf.Clamp01(groundedFootLockWeight);

            float maxStep = Mathf.Max(0.001f, maxGroundedFootLockStep);
            if (correction.magnitude > maxStep)
            {
                correction = correction.normalized * maxStep;
            }

            return IsFinite(correction) && correction.sqrMagnitude > 0.00000001f;
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

            if (!TryCalculateFootBottomY(foot.position.y, footRadius, out float bottomY))
            {
                locked = false;
                return;
            }

            bool shouldAccumulate = TryCalculateFootLockCorrection(
                bottomY,
                foot.position,
                targetHeight,
                locked,
                lockPosition,
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction);
            locked = nextLocked;
            lockPosition = nextLockPosition;
            if (!shouldAccumulate)
            {
                return;
            }

            correctionSum += correction;
            correctionCount++;
        }

        private static bool TryCalculateFootLockCorrection(
            float bottomY,
            Vector3 footPosition,
            float targetHeight,
            bool locked,
            Vector3 lockPosition,
            out bool nextLocked,
            out Vector3 nextLockPosition,
            out Vector3 correction)
        {
            const float contactHeight = 0.08f;
            const float releaseHeight = 0.14f;
            const float resetDistance = 0.25f;

            nextLocked = locked;
            nextLockPosition = lockPosition;
            correction = Vector3.zero;

            if (!IsFinite(bottomY))
            {
                nextLocked = false;
                return false;
            }

            if (bottomY > targetHeight + releaseHeight)
            {
                nextLocked = false;
                return false;
            }

            footPosition.y = 0f;
            if (!IsFinite(footPosition))
            {
                nextLocked = false;
                return false;
            }

            if (!locked || bottomY > targetHeight + contactHeight)
            {
                nextLockPosition = footPosition;
                nextLocked = bottomY <= targetHeight + contactHeight;
                return false;
            }

            correction = lockPosition - footPosition;
            correction.y = 0f;
            if (!IsFinite(correction))
            {
                nextLocked = false;
                return false;
            }

            if (correction.magnitude > resetDistance)
            {
                nextLockPosition = footPosition;
                correction = Vector3.zero;
            }

            return true;
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

}
