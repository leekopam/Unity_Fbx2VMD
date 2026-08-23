using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Fbx2Vmd.FileSystem;
using Fbx2Vmd.Settings;
using Fbx2Vmd.Recording;
using Fbx2Vmd.Character;
using RootMotion;
using RootMotion.FinalIK;

namespace Fbx2Vmd.FBXImporter
{
    public class FBXVmdPipeline : MonoBehaviour
    {
        internal const string IMPORT_FBX_FOLDER = "Import_FBX";
        internal const string FBX_EXTENSION = "fbx";
        private const string BONE_MAPPING_FILE = "BoneMapping_Data.txt";
        private const float GHOST_CONTAINER_SCALE = 0.01f;
        private const float THUMB_PROXIMAL_SAFE_MAX_LOCAL_ANGLE = 30f;
        private const float DEFAULT_THUMB_STRETCH_OFFSET = -0.1f;
        private const float LEGACY_THUMB_PROJECTION_MIN_PALM_NORMAL = 0.36f;
        private const float DEFAULT_THUMB_PROJECTION_MIN_PALM_NORMAL = 0.358f;
        internal const float MMD_REFERENCE_FRAME_RATE = 30f;
        internal const int MAX_RETARGET_PREWARM_FRAME_COUNT = 120;
#if UNITY_EDITOR
        internal const float EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE = 30f;
#endif
        private static Func<IFileBrowserService> fileBrowserServiceFactory = () => new FileBrowserService();
        private static Func<AssimpFBXImporter> fbxImporterFactory = () => new AssimpFBXImporter();

        internal enum FBXSessionState
        {
            Idle,
            Selected,
            Copied,
            LoadingFbx,
            AvatarReady,
            GhostReady,
            Retargeting,
            Recording,
            Saving,
            Success,
            Failed,
            Cancelled
        }


        #region Public 필드
        [Header("FBX 임포트 설정")]
        [Tooltip("체크 시 선택한 FBX 파일을 Import_FBX 폴더에 복사하여 저장")]
        [FormerlySerializedAs("saveToImportFolder")] [SerializeField] private bool _shouldSaveToImportFolder = false;

        [Tooltip("체크 시 FBX 임포트 후 VMD 녹화를 자동 시작합니다. 끄면 Unity 재생/촬영 준비까지만 수행합니다.")]
        [FormerlySerializedAs("recordVmdAfterImport")] [SerializeField] private bool _shouldRecordVmdAfterImport = true;

        [Header("Ghost Retargeting 설정")]
        [Tooltip("애니메이션을 적용할 대상 캐릭터 (Humanoid Avatar 필요)")]
        [FormerlySerializedAs("targetCharacter")]
        [SerializeField] private GameObject _targetCharacter;
        public GameObject targetCharacter { get => _targetCharacter; set => _targetCharacter = value; }

        [Tooltip("이전 수동 프로젝트와 같은 180도 PoseSpace 방향 보정을 사용합니다. 현재 씬에서는 카메라 정면 조건을 깨므로 비교/롤백용으로만 켭니다.")]
        [FormerlySerializedAs("useLegacyPoseSpaceFacingCorrection")] [SerializeField] private bool _shouldUseLegacyPoseSpaceFacingCorrection = false;

        [Tooltip("Main_Auto가 Sub_Manual 직접 Animator 재생처럼 FBX의 body/root 회전을 따르도록 합니다. 끄면 기존처럼 Ghost Transform 기준 회전 보정을 강제합니다.")]
        [FormerlySerializedAs("preserveFbxRootRotation")] [SerializeField] private bool _shouldPreserveFbxRootRotation = false;

        [Tooltip("HumanPose bodyPosition copied from FBX can jump on some clips. Keep the target body position stable like the manual Animator path.")]
        [FormerlySerializedAs("preserveRetargetBodyPosition")] [SerializeField] private bool _shouldPreserveRetargetBodyPosition = true;

        [Tooltip("Use FBX HumanPose bodyPosition X/Z delta as target root motion to reduce foot sliding.")]
        [FormerlySerializedAs("useRetargetBodyPositionXZRootMotion")] [SerializeField] private bool _shouldUseRetargetBodyPositionXZRootMotion = false;

        [Tooltip("When a foot is visually grounded, add a small X/Z root correction to reduce skating.")]
        [FormerlySerializedAs("stabilizeGroundedFootXZ")] [SerializeField] private bool _shouldStabilizeGroundedFootXZ = false;

        [Tooltip("Foot-lock correction strength. Lower values preserve dance motion, higher values reduce skating.")]
        [Range(0f, 1f)] [FormerlySerializedAs("GroundedFootLockWeight")]
        [SerializeField] private float _GroundedFootLockWeight= 0.45f;
        public float GroundedFootLockWeight { get => _GroundedFootLockWeight; private set => _GroundedFootLockWeight = value; }

        [Tooltip("Maximum X/Z root correction per frame for grounded foot lock.")]
        [Range(0.001f, 0.1f)] [FormerlySerializedAs("MaxGroundedFootLockStep")]
        [SerializeField] private float _MaxGroundedFootLockStep= 0.025f;
        public float MaxGroundedFootLockStep { get => _MaxGroundedFootLockStep; private set => _MaxGroundedFootLockStep = value; }

        [Tooltip("Editor 자동 경로에서 Unity가 임포트한 Humanoid clip의 muscle curve를 기준으로 사용합니다. Assimp Ghost 회전 curve가 수동 기준과 다를 때 팔/상체 포즈 차이를 줄이기 위한 안전 경로입니다.")]
        [FormerlySerializedAs("useEditorHumanoidClipMuscleReference")] [SerializeField] private bool _shouldUseEditorHumanoidClipMuscleReference = true;

        [Tooltip("Editor-only experimental RootT X/Z root motion reference. Keep disabled until visual_body_arc_jitter passes without increasing jitter.")]
        [FormerlySerializedAs("useEditorHumanoidRootTranslationReference")] [SerializeField] private bool _shouldUseEditorHumanoidRootTranslationReference = false;

        [Tooltip("Weight for Editor Humanoid RootT translation reference.")]
        [Range(0f, 1f)] [FormerlySerializedAs("editorHumanoidRootTranslationWeight")]
        [SerializeField] private float _editorHumanoidRootTranslationWeight= 0.25f;
        public float editorHumanoidRootTranslationWeight { get => _editorHumanoidRootTranslationWeight; private set => _editorHumanoidRootTranslationWeight = value; }

        [Tooltip("Current-frame blend for smoothed Editor Humanoid RootT translation delta.")]
        [Range(0.05f, 1f)] [FormerlySerializedAs("editorHumanoidRootTranslationCurrentWeight")]
        [SerializeField] private float _editorHumanoidRootTranslationCurrentWeight= 0.35f;
        public float editorHumanoidRootTranslationCurrentWeight { get => _editorHumanoidRootTranslationCurrentWeight; private set => _editorHumanoidRootTranslationCurrentWeight = value; }

        [Tooltip("손가락은 Sub_Manual/testPrefab Animator가 평가한 HumanPose 값을 기준으로 덮어씁니다.")]
        [FormerlySerializedAs("useManualAnimatorFingerPoseReference")] [SerializeField] private bool _shouldUseManualAnimatorFingerPoseReference = false;

        [FormerlySerializedAs("useManualAnimatorFullBodyPoseReference")] [SerializeField] private bool _shouldUseManualAnimatorFullBodyPoseReference = false;

        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorFullBodyPoseReferenceWeight")]
        [SerializeField] private float _manualAnimatorFullBodyPoseReferenceWeight= 1f;
        public float manualAnimatorFullBodyPoseReferenceWeight { get => _manualAnimatorFullBodyPoseReferenceWeight; set => _manualAnimatorFullBodyPoseReferenceWeight = value; }

        [Tooltip("Runtime diagnostic: keep the manual Animator full-body reference active but skip lower-body muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseExcludeLowerBodyMuscles")] [SerializeField] private bool _shouldExcludeManualAnimatorFullBodyLowerMuscles = false;

        [Tooltip("Runtime diagnostic: apply the manual Animator full-body reference only to lower-body muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseLowerBodyMusclesOnly")] [SerializeField] private bool _shouldApplyManualAnimatorFullBodyLowerMusclesOnly = false;

        [Tooltip("Runtime diagnostic: apply the manual Animator full-body reference only to leg in-out/twist muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseLegTwistMusclesOnly")] [SerializeField] private bool _shouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = false;

        [Tooltip("Runtime diagnostic: apply the manual Animator full-body reference only to right arm and shoulder muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseRightArmMusclesOnly")]
        [SerializeField] private bool _manualAnimatorFullBodyPoseRightArmMusclesOnly= false;
        public bool manualAnimatorFullBodyPoseRightArmMusclesOnly { get => _manualAnimatorFullBodyPoseRightArmMusclesOnly; set => _manualAnimatorFullBodyPoseRightArmMusclesOnly = value; }

        [Tooltip("Runtime diagnostic: apply the manual Animator full-body reference only to left arm and shoulder muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseLeftArmMusclesOnly")]
        [SerializeField] private bool _manualAnimatorFullBodyPoseLeftArmMusclesOnly= false;
        public bool manualAnimatorFullBodyPoseLeftArmMusclesOnly { get => _manualAnimatorFullBodyPoseLeftArmMusclesOnly; set => _manualAnimatorFullBodyPoseLeftArmMusclesOnly = value; }

        [Tooltip("Runtime diagnostic: apply the manual Animator full-body reference only to spine and right sleeve chain muscles.")]
        [FormerlySerializedAs("manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly")]
        [SerializeField] private bool _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly= false;
        public bool manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly { get => _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly; set => _manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly = value; }

        [Tooltip("Runtime diagnostic: first recorder frame for manual Animator full-body pose reference. Zero with end zero keeps the full clip.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("manualAnimatorFullBodyPoseFrameGateStart")]
        [SerializeField] private float _manualAnimatorFullBodyPoseFrameGateStart= 0f;
        public float manualAnimatorFullBodyPoseFrameGateStart { get => _manualAnimatorFullBodyPoseFrameGateStart; set => _manualAnimatorFullBodyPoseFrameGateStart = value; }

        [Tooltip("Runtime diagnostic: last recorder frame for manual Animator full-body pose reference. Zero with start zero keeps the full clip.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("manualAnimatorFullBodyPoseFrameGateEnd")]
        [SerializeField] private float _manualAnimatorFullBodyPoseFrameGateEnd= 0f;
        public float manualAnimatorFullBodyPoseFrameGateEnd { get => _manualAnimatorFullBodyPoseFrameGateEnd; set => _manualAnimatorFullBodyPoseFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic: after SetHumanPose, blend only right upper/lower leg twist output muscles back toward the solver input within a small cap.")]
        [FormerlySerializedAs("useSetHumanPoseRightLegTwistOutputReference")] [SerializeField] private bool _shouldUseSetHumanPoseRightLegTwistOutputReference = false;

        [Tooltip("Blend weight for bounded right leg twist output preservation after SetHumanPose.")]
        [Range(0f, 1f)] [FormerlySerializedAs("setHumanPoseRightLegTwistOutputReferenceWeight")]
        [SerializeField] private float _setHumanPoseRightLegTwistOutputReferenceWeight= 1f;
        public float setHumanPoseRightLegTwistOutputReferenceWeight { get => _setHumanPoseRightLegTwistOutputReferenceWeight; set => _setHumanPoseRightLegTwistOutputReferenceWeight = value; }

        [Tooltip("Maximum SetHumanPose right leg twist output correction per muscle.")]
        [Range(0f, 0.1f)] [FormerlySerializedAs("setHumanPoseRightLegTwistOutputReferenceMaxDelta")]
        [SerializeField] private float _setHumanPoseRightLegTwistOutputReferenceMaxDelta= 0.02f;
        public float setHumanPoseRightLegTwistOutputReferenceMaxDelta { get => _setHumanPoseRightLegTwistOutputReferenceMaxDelta; set => _setHumanPoseRightLegTwistOutputReferenceMaxDelta = value; }

        [Tooltip("Sub_Manual/testPrefab Animator의 HumanPose bodyRotation을 retarget pose 기준으로 사용해 팔꿈치 bend plane 기준축 차이를 줄입니다.")]
        [FormerlySerializedAs("useManualAnimatorBodyRotationReference")] [SerializeField] private bool _shouldUseManualAnimatorBodyRotationReference = false;

        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorBodyRotationReferenceWeight")]
        [SerializeField] private float _manualAnimatorBodyRotationReferenceWeight= 1f;
        public float manualAnimatorBodyRotationReferenceWeight { get => _manualAnimatorBodyRotationReferenceWeight; set => _manualAnimatorBodyRotationReferenceWeight = value; }

        [Tooltip("preserveRetargetBodyPosition=true 일 때 body Y를 수동 기준 Animator bodyPosition.y로 대체합니다. ghost Legacy-animation bodyPos.y 스파이크 없이 상체 높이를 애니메이션에 맞게 따라가도록 합니다.")]
        [FormerlySerializedAs("useManualAnimatorBodyPositionYReference")] [SerializeField] private bool _shouldUseManualAnimatorBodyPositionYReference = false;

        [Tooltip("Runtime diagnostic: blend HumanPose bodyPosition X/Z toward the manual Animator reference before SetHumanPose.")]
        [FormerlySerializedAs("useManualAnimatorBodyPositionXzReference")] [SerializeField] private bool _shouldUseManualAnimatorBodyPositionXzReference = false;

        [Tooltip("Blend weight for the manual Animator bodyPosition X/Z solver-input reference.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceWeight")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceWeight= 1f;
        public float manualAnimatorBodyPositionXzReferenceWeight { get => _manualAnimatorBodyPositionXzReferenceWeight; set => _manualAnimatorBodyPositionXzReferenceWeight = value; }

        [Tooltip("Maximum per-frame bodyPosition X/Z offset for the manual Animator solver-input reference.")]
        [Range(0f, 0.2f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceMaxOffset")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceMaxOffset= 0.025f;
        public float manualAnimatorBodyPositionXzReferenceMaxOffset { get => _manualAnimatorBodyPositionXzReferenceMaxOffset; set => _manualAnimatorBodyPositionXzReferenceMaxOffset = value; }

        [Tooltip("Runtime diagnostic start recorder frame for manual Animator bodyPosition X/Z reference. Zero disables frame gating.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("manualAnimatorBodyPositionXzReferenceFrameGateStart")]
        [SerializeField] private float _manualAnimatorBodyPositionXzReferenceFrameGateStart= 0f;
        public float manualAnimatorBodyPositionXzReferenceFrameGateStart { get => _manualAnimatorBodyPositionXzReferenceFrameGateStart; set => _manualAnimatorBodyPositionXzReferenceFrameGateStart = value; }

        [Tooltip("Runtime diagnostic end recorder frame for manual Animator bodyPosition X/Z reference. Zero disables frame gating.")]
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

        [Tooltip("수동 기준 Animator의 Hips localPosition을 target Hips에 선택적으로 적용해 Main_Auto의 몸통 경로 편차를 A/B 검증합니다. 활성 시 testprefab Hips delta가 YYB에 전달되어 오히려 발 호 궤적이 심해지므로 기본 비활성화합니다.")]
        [FormerlySerializedAs("useManualAnimatorHipsLocalPositionReference")] [SerializeField] private bool _shouldUseManualAnimatorHipsLocalPositionReference = false;

        [Tooltip("수동 기준 Hips localPosition 보정 강도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorHipsLocalPositionWeight")]
        [SerializeField] private float _manualAnimatorHipsLocalPositionWeight= 1f;
        public float manualAnimatorHipsLocalPositionWeight { get => _manualAnimatorHipsLocalPositionWeight; set => _manualAnimatorHipsLocalPositionWeight = value; }

        [Tooltip("프레임당 수동 기준 Hips localPosition으로 이동할 수 있는 최대 보정 거리입니다.")]
        [Range(0.001f, 0.5f)] [FormerlySerializedAs("manualAnimatorHipsLocalPositionMaxOffset")]
        [SerializeField] private float _manualAnimatorHipsLocalPositionMaxOffset= 0.12f;
        public float manualAnimatorHipsLocalPositionMaxOffset { get => _manualAnimatorHipsLocalPositionMaxOffset; set => _manualAnimatorHipsLocalPositionMaxOffset = value; }

        [Tooltip("수동 기준 Animator의 lowest-foot 상승량을 접지 목표 높이에 반영해 Main_Auto가 점프/발 높이 호를 바닥으로 평탄화하지 않도록 합니다.")]
        [FormerlySerializedAs("useManualAnimatorFootHeightGroundingReference")] [SerializeField] private bool _shouldUseManualAnimatorFootHeightGroundingReference = false;

        [Tooltip("수동 기준 lowest-foot 접지 높이 보정 강도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorFootHeightGroundingReferenceWeight")]
        [SerializeField] private float _manualAnimatorFootHeightGroundingReferenceWeight= 1f;
        public float manualAnimatorFootHeightGroundingReferenceWeight { get => _manualAnimatorFootHeightGroundingReferenceWeight; private set => _manualAnimatorFootHeightGroundingReferenceWeight = value; }

        [Tooltip("수동 기준 lowest-foot에서 접지 목표 높이로 반영할 수 있는 최대 양수 상승량입니다.")]
        [Range(0f, 0.12f)] [FormerlySerializedAs("manualAnimatorFootHeightGroundingReferenceMaxLift")]
        [SerializeField] private float _manualAnimatorFootHeightGroundingReferenceMaxLift= 0.08f;
        public float manualAnimatorFootHeightGroundingReferenceMaxLift { get => _manualAnimatorFootHeightGroundingReferenceMaxLift; private set => _manualAnimatorFootHeightGroundingReferenceMaxLift = value; }

        [Tooltip("Apply the manual Animator lower-body leg-chain localRotation to the target as an isolated runtime candidate.")]
        [FormerlySerializedAs("useManualAnimatorFootLocalRotationReference")] [SerializeField] private bool _shouldUseManualAnimatorFootLocalRotationReference = false;

        [Tooltip("Blend weight for the manual Animator lower-body leg-chain localRotation reference.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorFootLocalRotationReferenceWeight")]
        [SerializeField] private float _manualAnimatorFootLocalRotationReferenceWeight= 1f;
        public float manualAnimatorFootLocalRotationReferenceWeight { get => _manualAnimatorFootLocalRotationReferenceWeight; set => _manualAnimatorFootLocalRotationReferenceWeight = value; }

        [Tooltip("Apply manual Animator lower-body segment directions as an isolated runtime candidate without changing bone lengths or scale.")]
        [FormerlySerializedAs("useManualAnimatorLowerBodySegmentDirectionReference")] [SerializeField] private bool _shouldUseManualAnimatorLowerBodySegmentDirectionReference = false;

        [Tooltip("Blend weight for the manual Animator lower-body segment direction correction.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorLowerBodySegmentDirectionReferenceWeight")]
        [SerializeField] private float _manualAnimatorLowerBodySegmentDirectionReferenceWeight= 1f;
        public float manualAnimatorLowerBodySegmentDirectionReferenceWeight { get => _manualAnimatorLowerBodySegmentDirectionReferenceWeight; set => _manualAnimatorLowerBodySegmentDirectionReferenceWeight = value; }

        [Tooltip("Maximum per-frame lower-body segment direction correction angle in degrees.")]
        [Range(0f, 20f)] [FormerlySerializedAs("manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle= 6.2f;
        public float manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle { get => _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle; set => _manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Skip only the upper-leg-to-lower-leg segments from the manual Animator lower-body segment direction correction.")]
        [FormerlySerializedAs("disableManualAnimatorUpperLegToLowerLegSegmentDirectionReference")] [SerializeField] private bool _shouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference = false;

        [Tooltip("Optional upper-leg-to-lower-leg segment direction max angle in degrees. Zero keeps the shared lower-body segment cap.")]
        [Range(0f, 20f)] [FormerlySerializedAs("manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle { get => _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle; set => _manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Skip only the lower-leg-to-foot segments from the manual Animator lower-body segment direction correction.")]
        [FormerlySerializedAs("disableManualAnimatorLowerLegToFootSegmentDirectionReference")] [SerializeField] private bool _shouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference = false;

        [Tooltip("Optional lower-leg-to-foot segment direction max angle in degrees. Zero keeps the shared lower-body segment cap.")]
        [Range(0f, 20f)] [FormerlySerializedAs("manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle { get => _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle; set => _manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Optional left lower-leg-to-foot segment direction max angle in degrees. Zero keeps the lower-leg-to-foot segment cap.")]
        [Range(0f, 20f)] [FormerlySerializedAs("manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle { get => _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle; set => _manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Optional right lower-leg-to-foot segment direction max angle in degrees. Zero keeps the lower-leg-to-foot segment cap.")]
        [Range(0f, 20f)] [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Runtime diagnostic scale for right lower-leg-to-foot correction axis X/Z components. One keeps the original axis.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale= 1f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale = value; }

        [Tooltip("Blend for right lower-leg-to-foot correction strength. The measured default reduces right-foot X/Z residual without worsening hips-aligned foot residual.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight= 0.125f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight = value; }

        [Tooltip("Runtime diagnostic start recorder frame for right lower-leg-to-foot cap. Zero disables frame gating.")]
        [Range(0f, 2000f)] [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart= 0f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart = value; }

        [Tooltip("Runtime diagnostic end recorder frame for right lower-leg-to-foot cap. Zero disables frame gating.")]
        [Range(0f, 2000f)] [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd= 0f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic blend for preserving right foot world rotation after lower-leg-to-foot correction. One keeps the existing endpoint drift.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight")]
        [SerializeField] private float _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight= 1f;
        public float manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight { get => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight; set => _manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight = value; }

        [Tooltip("Skip only the foot-to-toes segment from the manual Animator lower-body segment direction correction.")]
        [FormerlySerializedAs("disableManualAnimatorFootToToesSegmentDirectionReference")] [SerializeField] private bool _shouldDisableManualAnimatorFootToToesSegmentDirectionReference = false;

        [Tooltip("Optional foot-to-toes-only segment direction max angle in degrees. Zero keeps the shared lower-body segment cap.")]
        [Range(0f, 20f)] [FormerlySerializedAs("manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle= 0f;
        public float manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle { get => _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle; set => _manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle = value; }

        [Tooltip("Apply a yaw-only upper-leg correction toward the manual Animator hips-relative foot X/Z path.")]
        [FormerlySerializedAs("useManualAnimatorFootHipsAlignedResidualYawReference")] [SerializeField] private bool _shouldUseManualAnimatorFootHipsAlignedResidualYawReference = false;

        [Tooltip("Blend weight for the hips-aligned foot X/Z residual yaw correction.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorFootHipsAlignedResidualYawReferenceWeight")]
        [SerializeField] private float _manualAnimatorFootHipsAlignedResidualYawReferenceWeight= 1f;
        public float manualAnimatorFootHipsAlignedResidualYawReferenceWeight { get => _manualAnimatorFootHipsAlignedResidualYawReferenceWeight; set => _manualAnimatorFootHipsAlignedResidualYawReferenceWeight = value; }

        [Tooltip("Maximum per-frame yaw correction angle for each upper leg in degrees.")]
        [Range(0f, 45f)] [FormerlySerializedAs("manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle")]
        [SerializeField] private float _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle= 15f;
        public float manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle { get => _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle; set => _manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle = value; }

        [Tooltip("Apply manual Animator hips-relative foot positions through BipedIK as an isolated runtime candidate.")]
        [FormerlySerializedAs("useManualAnimatorBipedIkFootPositionReference")]
        [SerializeField] private bool _useManualAnimatorBipedIkFootPositionReference= false;
        public bool useManualAnimatorBipedIkFootPositionReference { get => _useManualAnimatorBipedIkFootPositionReference; set => _useManualAnimatorBipedIkFootPositionReference = value; }

        [Tooltip("Blend weight for manual Animator BipedIK foot position targets.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorBipedIkFootPositionReferenceWeight")]
        [SerializeField] private float _manualAnimatorBipedIkFootPositionReferenceWeight= 0.65f;
        public float manualAnimatorBipedIkFootPositionReferenceWeight { get => _manualAnimatorBipedIkFootPositionReferenceWeight; set => _manualAnimatorBipedIkFootPositionReferenceWeight = value; }

        [Tooltip("Maximum per-frame BipedIK foot target correction distance from the current target foot position.")]
        [Range(0f, 0.2f)] [FormerlySerializedAs("manualAnimatorBipedIkFootPositionReferenceMaxOffset")]
        [SerializeField] private float _manualAnimatorBipedIkFootPositionReferenceMaxOffset= 0.12f;
        public float manualAnimatorBipedIkFootPositionReferenceMaxOffset { get => _manualAnimatorBipedIkFootPositionReferenceMaxOffset; set => _manualAnimatorBipedIkFootPositionReferenceMaxOffset = value; }

        [Tooltip("Runtime diagnostic: apply a bounded right foot/toes endpoint X/Z correction immediately after SetHumanPose.")]
        [FormerlySerializedAs("usePostSetHumanPoseRightEndpointPositionReference")]
        [SerializeField] private bool _usePostSetHumanPoseRightEndpointPositionReference= false;
        public bool usePostSetHumanPoseRightEndpointPositionReference { get => _usePostSetHumanPoseRightEndpointPositionReference; set => _usePostSetHumanPoseRightEndpointPositionReference = value; }

        [Tooltip("Blend weight for post-SetHumanPose right-foot endpoint X/Z correction.")]
        [Range(0f, 1f)] [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceWeight")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceWeight= 1f;
        public float postSetHumanPoseRightEndpointPositionReferenceWeight { get => _postSetHumanPoseRightEndpointPositionReferenceWeight; set => _postSetHumanPoseRightEndpointPositionReferenceWeight = value; }

        [Tooltip("Maximum per-frame post-SetHumanPose right-foot endpoint X/Z correction distance.")]
        [Range(0f, 0.2f)] [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceMaxOffset")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceMaxOffset= 0.04f;
        public float postSetHumanPoseRightEndpointPositionReferenceMaxOffset { get => _postSetHumanPoseRightEndpointPositionReferenceMaxOffset; set => _postSetHumanPoseRightEndpointPositionReferenceMaxOffset = value; }

        [Tooltip("Scale applied only to positive world-Z endpoint correction after SetHumanPose; 1 keeps existing behavior.")]
        [Range(0f, 1f)] [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferencePositiveZScale")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferencePositiveZScale= 1f;
        public float postSetHumanPoseRightEndpointPositionReferencePositiveZScale { get => _postSetHumanPoseRightEndpointPositionReferencePositiveZScale; set => _postSetHumanPoseRightEndpointPositionReferencePositiveZScale = value; }

        [Tooltip("Blend from foot-only endpoint delta to the existing foot/toes average after SetHumanPose; 1 keeps existing behavior.")]
        [Range(0f, 1f)] [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight= 1f;
        public float postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight { get => _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight; set => _postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = value; }

        [Tooltip("First legacy animation frame for post-SetHumanPose right-foot endpoint correction; 0 with end 0 keeps existing behavior.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceFrameGateStart")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart= 0f;
        public float postSetHumanPoseRightEndpointPositionReferenceFrameGateStart { get => _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart; set => _postSetHumanPoseRightEndpointPositionReferenceFrameGateStart = value; }

        [Tooltip("Last legacy animation frame for post-SetHumanPose right-foot endpoint correction; 0 with start 0 keeps existing behavior.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd")]
        [SerializeField] private float _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd= 0f;
        public float postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd { get => _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd; set => _postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic: apply the post-SetHumanPose endpoint correction to the left foot row instead of the right foot row.")]
        [FormerlySerializedAs("postSetHumanPoseEndpointPositionUseLeftSide")] [SerializeField] private bool _shouldUseLeftSideForPostSetHumanPoseEndpointPosition = false;

        [Tooltip("Use the first matched reference foot X/Z offset as the post-SetHumanPose right-foot correction basis.")]
        [FormerlySerializedAs("usePostSetHumanPoseRightFootEvaluatorXzReference")]
        [SerializeField] private bool _usePostSetHumanPoseRightFootEvaluatorXzReference= false;
        public bool usePostSetHumanPoseRightFootEvaluatorXzReference { get => _usePostSetHumanPoseRightFootEvaluatorXzReference; set => _usePostSetHumanPoseRightFootEvaluatorXzReference = value; }

        [Tooltip("Target normalized right-foot X/Z magnitude for the first-offset evaluator-basis post-SetHumanPose prototype.")]
        [Range(0f, 0.2f)] [FormerlySerializedAs("postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude")]
        [SerializeField] private float _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude= 0.049f;
        public float postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude { get => _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude; set => _postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude = value; }

        [Tooltip("Apply a right-foot endpoint X/Z correction immediately before SetHumanPose as an isolated runtime candidate.")]
        [FormerlySerializedAs("usePreSetHumanPoseRightEndpointPositionReference")]
        [SerializeField] private bool _usePreSetHumanPoseRightEndpointPositionReference= false;
        public bool usePreSetHumanPoseRightEndpointPositionReference { get => _usePreSetHumanPoseRightEndpointPositionReference; set => _usePreSetHumanPoseRightEndpointPositionReference = value; }

        [Tooltip("Blend weight for pre-SetHumanPose right-foot endpoint X/Z correction.")]
        [Range(0f, 1f)] [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceWeight")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceWeight= 1f;
        public float preSetHumanPoseRightEndpointPositionReferenceWeight { get => _preSetHumanPoseRightEndpointPositionReferenceWeight; set => _preSetHumanPoseRightEndpointPositionReferenceWeight = value; }

        [Tooltip("Maximum per-frame pre-SetHumanPose right-foot endpoint X/Z correction distance.")]
        [Range(0f, 0.2f)] [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceMaxOffset")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceMaxOffset= 0.025f;
        public float preSetHumanPoseRightEndpointPositionReferenceMaxOffset { get => _preSetHumanPoseRightEndpointPositionReferenceMaxOffset; set => _preSetHumanPoseRightEndpointPositionReferenceMaxOffset = value; }

        [Tooltip("Scale applied only to positive world-Z endpoint correction before SetHumanPose; 1 keeps existing behavior.")]
        [Range(0f, 1f)] [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferencePositiveZScale")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferencePositiveZScale= 1f;
        public float preSetHumanPoseRightEndpointPositionReferencePositiveZScale { get => _preSetHumanPoseRightEndpointPositionReferencePositiveZScale; set => _preSetHumanPoseRightEndpointPositionReferencePositiveZScale = value; }

        [Tooltip("Blend from foot-only endpoint delta to the foot/toes average before SetHumanPose.")]
        [Range(0f, 1f)] [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight= 1f;
        public float preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight { get => _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight; set => _preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight = value; }

        [Tooltip("First legacy animation frame for pre-SetHumanPose right-foot endpoint correction; 0 with end 0 keeps existing behavior.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceFrameGateStart")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart= 0f;
        public float preSetHumanPoseRightEndpointPositionReferenceFrameGateStart { get => _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart; set => _preSetHumanPoseRightEndpointPositionReferenceFrameGateStart = value; }

        [Tooltip("Last legacy animation frame for pre-SetHumanPose right-foot endpoint correction; 0 with start 0 keeps existing behavior.")]
        [Range(0f, 6000f)] [FormerlySerializedAs("preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd")]
        [SerializeField] private float _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd= 0f;
        public float preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd { get => _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd; set => _preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd = value; }

        [Tooltip("Runtime diagnostic: apply the pre-SetHumanPose endpoint correction to the left foot row instead of the right foot row.")]
        [FormerlySerializedAs("preSetHumanPoseEndpointPositionUseLeftSide")] [SerializeField] private bool _shouldUseLeftSideForPreSetHumanPoseEndpointPosition = false;

        [Tooltip("Runtime diagnostic: use ghost/current endpoint rows as a sign-corrected bodyPosition X/Z translation basis before SetHumanPose.")]
        [FormerlySerializedAs("preSetHumanPoseEndpointPositionUseGhostCurrentBasis")]
        [SerializeField] private bool _preSetHumanPoseEndpointPositionUseGhostCurrentBasis= false;
        public bool preSetHumanPoseEndpointPositionUseGhostCurrentBasis { get => _preSetHumanPoseEndpointPositionUseGhostCurrentBasis; set => _preSetHumanPoseEndpointPositionUseGhostCurrentBasis = value; }

        [Tooltip("Runtime diagnostic: invert the pre-SetHumanPose endpoint bodyPosition X input delta.")]
        [FormerlySerializedAs("preSetHumanPoseEndpointPositionInvertBodyPositionX")] [SerializeField] private bool _shouldInvertPreSetHumanPoseEndpointPositionBodyX = false;

        [Tooltip("Runtime diagnostic: invert the pre-SetHumanPose endpoint bodyPosition Z input delta.")]
        [FormerlySerializedAs("preSetHumanPoseEndpointPositionInvertBodyPositionZ")] [SerializeField] private bool _shouldInvertPreSetHumanPoseEndpointPositionBodyZ = false;

        [Tooltip("엄지 체인의 localRotation도 Sub_Manual/testPrefab Animator가 같은 FBX clip에서 평가한 값을 기준으로 덮어씁니다. YYB와 testPrefab의 Humanoid muscle은 같지만 엄지 로컬 축 해석이 달라 보일 때 사용합니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbLocalRotationReference")]
        [SerializeField] private bool _useManualAnimatorThumbLocalRotationReference= false;
        public bool useManualAnimatorThumbLocalRotationReference { get => _useManualAnimatorThumbLocalRotationReference; set => _useManualAnimatorThumbLocalRotationReference = value; }

        [Tooltip("손목 localRotation을 Sub_Manual/testPrefab Animator가 같은 FBX clip에서 평가한 값을 기준으로 덮어씁니다. t13.2 hand pose parity 회귀 보호용입니다.")]
        [FormerlySerializedAs("useManualAnimatorHandLocalRotationReference")]
        [SerializeField] private bool _useManualAnimatorHandLocalRotationReference= false;
        public bool useManualAnimatorHandLocalRotationReference { get => _useManualAnimatorHandLocalRotationReference; set => _useManualAnimatorHandLocalRotationReference = value; }

        [Tooltip("엄지 세그먼트 방향을 Sub_Manual/testPrefab의 손 기준 방향에 맞춥니다. 모델별 bind axis 차이 때문에 localRotation 숫자가 같아도 손 모양이 달라질 때 사용합니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbSegmentDirectionReference")]
        [SerializeField] private bool _useManualAnimatorThumbSegmentDirectionReference= false;
        public bool useManualAnimatorThumbSegmentDirectionReference { get => _useManualAnimatorThumbSegmentDirectionReference; private set => _useManualAnimatorThumbSegmentDirectionReference = value; }

        [Tooltip("엄지 세그먼트 방향 보정 강도입니다. 1이면 testPrefab 손 기준 방향에 맞추고, 0이면 보정하지 않습니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorThumbSegmentDirectionWeight")]
        [SerializeField] private float _manualAnimatorThumbSegmentDirectionWeight= 1f;
        public float manualAnimatorThumbSegmentDirectionWeight { get => _manualAnimatorThumbSegmentDirectionWeight; private set => _manualAnimatorThumbSegmentDirectionWeight = value; }

        [Tooltip("손바닥 기준 Hand->ThumbIntermediate 방향을 Sub_Manual/testPrefab의 손 기준 방향에 맞춥니다. 엄지 시작 방향이 손바닥 밖으로 탈골된 것처럼 보일 때 사용합니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbHandDirectionReference")]
        [SerializeField] private bool _useManualAnimatorThumbHandDirectionReference= false;
        public bool useManualAnimatorThumbHandDirectionReference { get => _useManualAnimatorThumbHandDirectionReference; private set => _useManualAnimatorThumbHandDirectionReference = value; }

        [Tooltip("손바닥 기준 엄지 시작 방향 보정 강도입니다. 1이면 testPrefab의 Hand->ThumbIntermediate 방향에 맞춥니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorThumbHandDirectionWeight")]
        [SerializeField] private float _manualAnimatorThumbHandDirectionWeight= 1f;
        public float manualAnimatorThumbHandDirectionWeight { get => _manualAnimatorThumbHandDirectionWeight; private set => _manualAnimatorThumbHandDirectionWeight = value; }

        [Tooltip("손바닥 전체 프레임을 Sub_Manual/testPrefab의 손바닥 방향에 맞춥니다. MMD 기준과 손목/엄지 뿌리 실루엣이 다를 때 사용합니다.")]
        [FormerlySerializedAs("useManualAnimatorHandPalmFrameReference")]
        [SerializeField] private bool _useManualAnimatorHandPalmFrameReference= false;
        public bool useManualAnimatorHandPalmFrameReference { get => _useManualAnimatorHandPalmFrameReference; set => _useManualAnimatorHandPalmFrameReference = value; }

        [Tooltip("손바닥 프레임 보정 강도입니다. MMD 396프레임 직접 비교 기준으로 기본값은 1.00입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorHandPalmFrameWeight")]
        [SerializeField] private float _manualAnimatorHandPalmFrameWeight= 1f;
        public float manualAnimatorHandPalmFrameWeight { get => _manualAnimatorHandPalmFrameWeight; set => _manualAnimatorHandPalmFrameWeight = value; }

        [Tooltip("엄지 첫 본 위치를 Sub_Manual/testPrefab의 손 기준 위치 비율로 맞춥니다. YYB 엄지 시작부가 손바닥 안쪽으로 붙어 보일 때 사용합니다.")]
        [FormerlySerializedAs("useManualAnimatorThumbBasePositionReference")]
        [SerializeField] private bool _useManualAnimatorThumbBasePositionReference= false;
        public bool useManualAnimatorThumbBasePositionReference { get => _useManualAnimatorThumbBasePositionReference; private set => _useManualAnimatorThumbBasePositionReference = value; }

        [Tooltip("엄지 첫 본 위치 보정 강도입니다. 1이면 testPrefab 손 기준 위치 비율을 그대로 적용합니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("manualAnimatorThumbBasePositionWeight")]
        [SerializeField] private float _manualAnimatorThumbBasePositionWeight= 1f;
        public float manualAnimatorThumbBasePositionWeight { get => _manualAnimatorThumbBasePositionWeight; private set => _manualAnimatorThumbBasePositionWeight = value; }

        [Tooltip("엄지 첫 본 위치가 원본 위치에서 한 프레임에 벗어날 수 있는 최대 거리입니다.")]
        [Range(0f, 0.03f)] [FormerlySerializedAs("manualAnimatorThumbBasePositionMaxOffset")]
        [SerializeField] private float _manualAnimatorThumbBasePositionMaxOffset= 0.03f;
        public float manualAnimatorThumbBasePositionMaxOffset { get => _manualAnimatorThumbBasePositionMaxOffset; private set => _manualAnimatorThumbBasePositionMaxOffset = value; }

        [Tooltip("손가락 기준 평가에 사용할 수동 기준 프리팹입니다. 비워두면 VMDRecorderSample testPrefab을 자동으로 찾습니다.")]
        [FormerlySerializedAs("manualFingerReferencePrefab")]
        [SerializeField] private GameObject _manualFingerReferencePrefab;
        public GameObject manualFingerReferencePrefab { get => _manualFingerReferencePrefab; private set => _manualFingerReferencePrefab = value; }

        [Tooltip("손가락 기준 평가에 사용할 수동 Animator Controller입니다. 비워두면 TestAnimator1_Manual 또는 TestAnimator1을 자동으로 찾습니다.")]
        [FormerlySerializedAs("manualFingerReferenceController")]
        [SerializeField] private RuntimeAnimatorController _manualFingerReferenceController;
        public RuntimeAnimatorController manualFingerReferenceController { get => _manualFingerReferenceController; private set => _manualFingerReferenceController = value; }

        [Tooltip("Retarget/녹화 중 MMD4Mecanim의 어깨 PPH 보정을 잠시 끕니다. 수동 기준 모션과 어깨 형태가 달라질 수 있어 기본값은 끕니다.")]
        [FormerlySerializedAs("disableMmdShoulderPostPoseDuringRetarget")]
        [SerializeField] private bool _disableMmdShoulderPostPoseDuringRetarget= false;
        public bool disableMmdShoulderPostPoseDuringRetarget { get => _disableMmdShoulderPostPoseDuringRetarget; private set => _disableMmdShoulderPostPoseDuringRetarget = value; }

        [Header("Anatomical Retarget Guard")]
        [Tooltip("FBX Humanoid muscle 값을 [-1, 1] 범위로 제한합니다.")]
        [FormerlySerializedAs("clampRetargetMusclesToHumanRange")]
        [SerializeField] private bool _clampRetargetMusclesToHumanRange= true;
        public bool clampRetargetMusclesToHumanRange { get => _clampRetargetMusclesToHumanRange; private set => _clampRetargetMusclesToHumanRange = value; }

        [Tooltip("팔이 늘어나거나 비정상적으로 비틀리는 Humanoid arm muscle 값을 제한합니다.")]
        [FormerlySerializedAs("enableAnatomicalArmGuard")]
        [SerializeField] private bool _enableAnatomicalArmGuard= true;
        public bool enableAnatomicalArmGuard { get => _enableAnatomicalArmGuard; set => _enableAnatomicalArmGuard = value; }

        [Tooltip("Humanoid 팔 Stretch muscle 허용치입니다. Forearm Stretch는 팔꿈치 굽힘에 가까우므로 기본적으로 제한하지 않습니다.")]
        [Range(0f, 0.5f)] [FormerlySerializedAs("ArmStretchMuscleLimit")]
        [SerializeField] private float _ArmStretchMuscleLimit= 0f;
        public float ArmStretchMuscleLimit { get => _ArmStretchMuscleLimit; set => _ArmStretchMuscleLimit = value; }

        [Tooltip("Retarget 단계에서 Forearm Stretch muscle을 제한합니다. 팔 길이가 아니라 팔꿈치 굽힘에 가까워 기본값은 꺼야 합니다.")]
        [FormerlySerializedAs("clampRetargetArmStretchMuscles")]
        [SerializeField] private bool _clampRetargetArmStretchMuscles= false;
        public bool clampRetargetArmStretchMuscles { get => _clampRetargetArmStretchMuscles; set => _clampRetargetArmStretchMuscles = value; }

        [Tooltip("상완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)] [FormerlySerializedAs("UpperArmTwistMuscleLimit")]
        [SerializeField] private float _UpperArmTwistMuscleLimit= 0.75f;
        public float UpperArmTwistMuscleLimit { get => _UpperArmTwistMuscleLimit; private set => _UpperArmTwistMuscleLimit = value; }

        [Tooltip("전완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)] [FormerlySerializedAs("LowerArmTwistMuscleLimit")]
        [SerializeField] private float _LowerArmTwistMuscleLimit= 0.65f;
        public float LowerArmTwistMuscleLimit { get => _LowerArmTwistMuscleLimit; private set => _LowerArmTwistMuscleLimit = value; }

        [Tooltip("Retarget 중 Target Humanoid 본의 localPosition을 초기값으로 복구해 팔/다리 길이 변형을 막습니다.")]
        [FormerlySerializedAs("lockTargetHumanoidBonePositions")] [SerializeField] private bool _shouldLockTargetHumanoidBonePositions = true;

        [Tooltip("팔/다리 하위의 비-Humanoid 보조본 localPosition도 초기값으로 복구해 소매/팔 메시가 가늘어지는 변형을 막습니다.")]
        [FormerlySerializedAs("lockTargetLimbChildLocalPositions")]
        [SerializeField] private bool _lockTargetLimbChildLocalPositions= true;
        public bool lockTargetLimbChildLocalPositions { get => _lockTargetLimbChildLocalPositions; private set => _lockTargetLimbChildLocalPositions = value; }

        [Tooltip("팔/다리 하위의 비-Humanoid 보조본 localRotation을 초기값으로 복구합니다. YYB 소매/팔 twist 보조본 움직임을 막을 수 있어 기본값은 끕니다.")]
        [FormerlySerializedAs("lockTargetLimbChildLocalRotations")]
        [SerializeField] private bool _lockTargetLimbChildLocalRotations= false;
        public bool lockTargetLimbChildLocalRotations { get => _lockTargetLimbChildLocalRotations; private set => _lockTargetLimbChildLocalRotations = value; }

        [Tooltip("Target 캐릭터에 HumanoidArmDeformationGuard를 자동 부착해 Retarget 외 경로에서도 팔 가늘어짐을 막습니다.")]
        [FormerlySerializedAs("attachTargetArmDeformationGuard")]
        [SerializeField] private bool _attachTargetArmDeformationGuard= true;
        public bool attachTargetArmDeformationGuard { get => _attachTargetArmDeformationGuard; private set => _attachTargetArmDeformationGuard = value; }

        [Tooltip("자동 부착된 Target 가드에서 HumanPose arm muscle도 제한합니다. 기본 Retargeter 보정과 중복될 수 있어 기본값은 끕니다.")]
        [FormerlySerializedAs("targetGuardClampAnatomicalArmMuscles")]
        [SerializeField] private bool _targetGuardClampAnatomicalArmMuscles= false;
        public bool targetGuardClampAnatomicalArmMuscles { get => _targetGuardClampAnatomicalArmMuscles; set => _targetGuardClampAnatomicalArmMuscles = value; }

        [Tooltip("자동 부착된 Target 가드도 arm stretch muscle을 제한합니다. 직접 Animator 모션이 굳을 수 있어 기본값은 끕니다.")]
        [FormerlySerializedAs("targetGuardClampArmStretchMuscles")]
        [SerializeField] private bool _targetGuardClampArmStretchMuscles= false;
        public bool targetGuardClampArmStretchMuscles { get => _targetGuardClampArmStretchMuscles; set => _targetGuardClampArmStretchMuscles = value; }

        [Tooltip("팔 변형 가드가 제한/복구를 처음 수행할 때 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logArmDeformationGuardCorrections")]
        [SerializeField] private bool _logArmDeformationGuardCorrections= false;
        public bool logArmDeformationGuardCorrections { get => _logArmDeformationGuardCorrections; private set => _logArmDeformationGuardCorrections = value; }

        [Header("Animation Rigging Arm Twist Correction")]
        [Tooltip("YYB 팔 twist 보조본을 Animation Rigging TwistCorrection으로 보정합니다. 현재 자동 리타겟 경로에서는 RigBuilder가 SetHumanPose 포즈를 초기화할 수 있어 기본 비활성화합니다.")]
        [FormerlySerializedAs("enableAnimationRiggingArmTwistCorrection")]
        [SerializeField] private bool _enableAnimationRiggingArmTwistCorrection= false;
        public bool enableAnimationRiggingArmTwistCorrection { get => _enableAnimationRiggingArmTwistCorrection; private set => _enableAnimationRiggingArmTwistCorrection = value; }

        [Tooltip("Animation Rigging 팔 twist 보정 전체 영향도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("AnimationRiggingArmTwistRigWeight")]
        [SerializeField] private float _AnimationRiggingArmTwistRigWeight= 0.65f;
        public float AnimationRiggingArmTwistRigWeight { get => _AnimationRiggingArmTwistRigWeight; private set => _AnimationRiggingArmTwistRigWeight = value; }

        [Tooltip("상완 twist 보조본 영향도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("AnimationRiggingUpperArmTwistWeight")]
        [SerializeField] private float _AnimationRiggingUpperArmTwistWeight= 0.45f;
        public float AnimationRiggingUpperArmTwistWeight { get => _AnimationRiggingUpperArmTwistWeight; private set => _AnimationRiggingUpperArmTwistWeight = value; }

        [Tooltip("전완/손목 twist 보조본 영향도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("AnimationRiggingForearmTwistWeight")]
        [SerializeField] private float _AnimationRiggingForearmTwistWeight= 0.85f;
        public float AnimationRiggingForearmTwistWeight { get => _AnimationRiggingForearmTwistWeight; private set => _AnimationRiggingForearmTwistWeight = value; }

        [Tooltip("Animation Rigging 팔 twist 보정 구성 로그를 출력합니다.")]
        [FormerlySerializedAs("logAnimationRiggingArmTwistCorrection")]
        [SerializeField] private bool _logAnimationRiggingArmTwistCorrection= false;
        public bool logAnimationRiggingArmTwistCorrection { get => _logAnimationRiggingArmTwistCorrection; private set => _logAnimationRiggingArmTwistCorrection = value; }

        [Header("YYB Arm Direction Retarget Correction")]
        [Tooltip("실험 옵션입니다. Ghost 팔 세그먼트 방향을 Target YYB 팔 방향에 제한적으로 다시 맞춥니다. 현재는 일부 정상 프레임을 망가뜨릴 수 있어 기본 비활성화합니다.")]
        [FormerlySerializedAs("enableYybArmDirectionRetargetCorrection")]
        [SerializeField] private bool _enableYybArmDirectionRetargetCorrection= false;
        public bool enableYybArmDirectionRetargetCorrection { get => _enableYybArmDirectionRetargetCorrection; set => _enableYybArmDirectionRetargetCorrection = value; }

        [Tooltip("상완 방향 보정 영향도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmDirectionUpperArmWeight")]
        [SerializeField] private float _YybArmDirectionUpperArmWeight= 0.65f;
        public float YybArmDirectionUpperArmWeight { get => _YybArmDirectionUpperArmWeight; set => _YybArmDirectionUpperArmWeight = value; }

        [Tooltip("전완 방향 보정 영향도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmDirectionForearmWeight")]
        [SerializeField] private float _YybArmDirectionForearmWeight= 0.75f;
        public float YybArmDirectionForearmWeight { get => _YybArmDirectionForearmWeight; set => _YybArmDirectionForearmWeight = value; }

        [Tooltip("상완이 한 프레임에 따라갈 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] [FormerlySerializedAs("YybArmDirectionUpperArmMaxDegrees")]
        [SerializeField] private float _YybArmDirectionUpperArmMaxDegrees= 65f;
        public float YybArmDirectionUpperArmMaxDegrees { get => _YybArmDirectionUpperArmMaxDegrees; set => _YybArmDirectionUpperArmMaxDegrees = value; }

        [Tooltip("전완이 한 프레임에 따라갈 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] [FormerlySerializedAs("YybArmDirectionForearmMaxDegrees")]
        [SerializeField] private float _YybArmDirectionForearmMaxDegrees= 85f;
        public float YybArmDirectionForearmMaxDegrees { get => _YybArmDirectionForearmMaxDegrees; set => _YybArmDirectionForearmMaxDegrees = value; }

        [Tooltip("YYB 팔 방향 보정의 왼쪽 팔 영향도 배율입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmDirectionLeftSideWeightScale")]
        [SerializeField] private float _YybArmDirectionLeftSideWeightScale= 1f;
        public float YybArmDirectionLeftSideWeightScale { get => _YybArmDirectionLeftSideWeightScale; set => _YybArmDirectionLeftSideWeightScale = value; }

        [Tooltip("YYB 팔 방향 보정의 오른쪽 팔 영향도 배율입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmDirectionRightSideWeightScale")]
        [SerializeField] private float _YybArmDirectionRightSideWeightScale= 1f;
        public float YybArmDirectionRightSideWeightScale { get => _YybArmDirectionRightSideWeightScale; set => _YybArmDirectionRightSideWeightScale = value; }

        [Tooltip("YYB 팔 방향 보정 구성 로그를 출력합니다.")]
        [FormerlySerializedAs("logYybArmDirectionRetargetCorrection")]
        [SerializeField] private bool _logYybArmDirectionRetargetCorrection= false;
        public bool logYybArmDirectionRetargetCorrection { get => _logYybArmDirectionRetargetCorrection; private set => _logYybArmDirectionRetargetCorrection = value; }

        [Header("YYB Arm Visual Twist Correction")]
        [Tooltip("RigBuilder 없이 YYB 팔/소매 보조본을 전완-손목 회전에 맞춰 직접 분배해 소매가 가늘어 보이는 현상을 줄입니다.")]
        [FormerlySerializedAs("enableYybArmVisualTwistCorrection")]
        [SerializeField] private bool _enableYybArmVisualTwistCorrection= true;
        public bool enableYybArmVisualTwistCorrection { get => _enableYybArmVisualTwistCorrection; set => _enableYybArmVisualTwistCorrection = value; }

        [Tooltip("상완 보조본 회전 분배 영향도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmVisualUpperArmInfluence")]
        [SerializeField] private float _YybArmVisualUpperArmInfluence= 0.35f;
        public float YybArmVisualUpperArmInfluence { get => _YybArmVisualUpperArmInfluence; set => _YybArmVisualUpperArmInfluence = value; }

        [Tooltip("전완/손목 보조본 회전 분배 영향도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmVisualForearmInfluence")]
        [SerializeField] private float _YybArmVisualForearmInfluence= 0.75f;
        public float YybArmVisualForearmInfluence { get => _YybArmVisualForearmInfluence; set => _YybArmVisualForearmInfluence = value; }

        [Tooltip("상완 보조본에 허용할 최대 회전각입니다.")]
        [Range(0f, 120f)] [FormerlySerializedAs("YybArmVisualUpperArmMaxDegrees")]
        [SerializeField] private float _YybArmVisualUpperArmMaxDegrees= 45f;
        public float YybArmVisualUpperArmMaxDegrees { get => _YybArmVisualUpperArmMaxDegrees; set => _YybArmVisualUpperArmMaxDegrees = value; }

        [Tooltip("전완/손목 보조본에 허용할 최대 회전각입니다.")]
        [Range(0f, 120f)] [FormerlySerializedAs("YybArmVisualForearmMaxDegrees")]
        [SerializeField] private float _YybArmVisualForearmMaxDegrees= 75f;
        public float YybArmVisualForearmMaxDegrees { get => _YybArmVisualForearmMaxDegrees; set => _YybArmVisualForearmMaxDegrees = value; }

        [Tooltip("YYB 팔 시각 보정 구성 로그를 출력합니다.")]
        [FormerlySerializedAs("logYybArmVisualTwistCorrection")]
        [SerializeField] private bool _logYybArmVisualTwistCorrection= false;
        public bool logYybArmVisualTwistCorrection { get => _logYybArmVisualTwistCorrection; private set => _logYybArmVisualTwistCorrection = value; }

        [Header("YYB Arm Anatomical Swing Correction")]
        [Tooltip("손이 몸 밖/어깨 근처에 있는데 상완만 아래로 크게 떨어지는 포즈를 제한합니다.")]
        [FormerlySerializedAs("enableYybArmSwingLimitCorrection")]
        [SerializeField] private bool _enableYybArmSwingLimitCorrection= false;
        public bool enableYybArmSwingLimitCorrection { get => _enableYybArmSwingLimitCorrection; set => _enableYybArmSwingLimitCorrection = value; }

        [Tooltip("상완 하강 제한 보정 강도입니다. Target Humanoid 포즈를 직접 바꾸므로 기본 자동 경로에서는 끄고, 진단/긴급 보정 때만 사용합니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmSwingLimitWeight")]
        [SerializeField] private float _YybArmSwingLimitWeight= 0.85f;
        public float YybArmSwingLimitWeight { get => _YybArmSwingLimitWeight; set => _YybArmSwingLimitWeight = value; }

        [Tooltip("상완 방향이 아래 방향과 이 값보다 더 가까우면 보정 후보로 봅니다. 0.68은 수동 기준 모션과의 차이를 줄이는 균형값입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmSwingMaxDownDot")]
        [SerializeField] private float _YybArmSwingMaxDownDot= 0.68f;
        public float YybArmSwingMaxDownDot { get => _YybArmSwingMaxDownDot; set => _YybArmSwingMaxDownDot = value; }

        [Tooltip("손이 어깨에서 팔 길이 대비 이 비율 이상 옆/앞으로 떨어져 있을 때만 보정합니다. YYB는 몸 가까이에서도 소매가 무너질 수 있어 낮게 둡니다.")]
        [Range(0f, 1.5f)] [FormerlySerializedAs("YybArmSwingMinHandHorizontalRatio")]
        [SerializeField] private float _YybArmSwingMinHandHorizontalRatio= 0.05f;
        public float YybArmSwingMinHandHorizontalRatio { get => _YybArmSwingMinHandHorizontalRatio; set => _YybArmSwingMinHandHorizontalRatio = value; }

        [Tooltip("손이 어깨보다 팔 길이 대비 이 비율보다 더 낮으면 자연스럽게 내려간 팔로 보고 보정하지 않습니다.")]
        [Range(0f, 1.5f)] [FormerlySerializedAs("YybArmSwingMaxHandBelowShoulderRatio")]
        [SerializeField] private float _YybArmSwingMaxHandBelowShoulderRatio= 0.75f;
        public float YybArmSwingMaxHandBelowShoulderRatio { get => _YybArmSwingMaxHandBelowShoulderRatio; set => _YybArmSwingMaxHandBelowShoulderRatio = value; }

        [Tooltip("손이 몸 밖으로 과하게 벌어진 경우 수평 reach를 제한하는 보정 강도입니다. 0이면 비활성화합니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmSwingHorizontalReachLimitWeight")]
        [SerializeField] private float _YybArmSwingHorizontalReachLimitWeight= 0f;
        public float YybArmSwingHorizontalReachLimitWeight { get => _YybArmSwingHorizontalReachLimitWeight; set => _YybArmSwingHorizontalReachLimitWeight = value; }

        [Tooltip("팔 길이 대비 허용할 최대 손 수평 reach입니다. 0이면 수평 reach 제한을 사용하지 않습니다.")]
        [Range(0f, 1.5f)] [FormerlySerializedAs("YybArmSwingMaxHandHorizontalReachRatio")]
        [SerializeField] private float _YybArmSwingMaxHandHorizontalReachRatio= 0f;
        public float YybArmSwingMaxHandHorizontalReachRatio { get => _YybArmSwingMaxHandHorizontalReachRatio; set => _YybArmSwingMaxHandHorizontalReachRatio = value; }

        [Tooltip("Horizontal reach only below-shoulder gate. 0 keeps using YybArmSwingMaxHandBelowShoulderRatio.")]
        [Range(0f, 1.5f)] [FormerlySerializedAs("YybArmSwingHorizontalReachMaxHandBelowShoulderRatio")]
        [SerializeField] private float _YybArmSwingHorizontalReachMaxHandBelowShoulderRatio= 0f;
        public float YybArmSwingHorizontalReachMaxHandBelowShoulderRatio { get => _YybArmSwingHorizontalReachMaxHandBelowShoulderRatio; set => _YybArmSwingHorizontalReachMaxHandBelowShoulderRatio = value; }

        [Tooltip("Horizontal reach 적용 뒤 팔꿈치 각도가 이 값보다 작으면 해당 reach 보정을 되돌립니다. 0이면 비활성화합니다.")]
        [Range(0f, 180f)] [FormerlySerializedAs("YybArmSwingHorizontalReachMinElbowAngleAfterApply")]
        [SerializeField] private float _YybArmSwingHorizontalReachMinElbowAngleAfterApply= 0f;
        public float YybArmSwingHorizontalReachMinElbowAngleAfterApply { get => _YybArmSwingHorizontalReachMinElbowAngleAfterApply; set => _YybArmSwingHorizontalReachMinElbowAngleAfterApply = value; }

        [Range(0f, 1f)] [FormerlySerializedAs("YybArmSwingRaisedPoseHorizontalReachLimitWeight")]
        [SerializeField] private float _YybArmSwingRaisedPoseHorizontalReachLimitWeight= 0f;
        public float YybArmSwingRaisedPoseHorizontalReachLimitWeight { get => _YybArmSwingRaisedPoseHorizontalReachLimitWeight; set => _YybArmSwingRaisedPoseHorizontalReachLimitWeight = value; }

        [Range(0f, 1f)] [FormerlySerializedAs("YybArmSwingRaisedPoseMinUpperArmDownDot")]
        [SerializeField] private float _YybArmSwingRaisedPoseMinUpperArmDownDot= 0.55f;
        public float YybArmSwingRaisedPoseMinUpperArmDownDot { get => _YybArmSwingRaisedPoseMinUpperArmDownDot; set => _YybArmSwingRaisedPoseMinUpperArmDownDot = value; }

        [Range(0f, 1.5f)] [FormerlySerializedAs("YybArmSwingRaisedPoseMaxHandBelowShoulderRatio")]
        [SerializeField] private float _YybArmSwingRaisedPoseMaxHandBelowShoulderRatio= 0.05f;
        public float YybArmSwingRaisedPoseMaxHandBelowShoulderRatio { get => _YybArmSwingRaisedPoseMaxHandBelowShoulderRatio; set => _YybArmSwingRaisedPoseMaxHandBelowShoulderRatio = value; }

        [Range(0f, 1.5f)] [FormerlySerializedAs("YybArmSwingRaisedPoseMaxHandHorizontalReachRatio")]
        [SerializeField] private float _YybArmSwingRaisedPoseMaxHandHorizontalReachRatio= 0f;
        public float YybArmSwingRaisedPoseMaxHandHorizontalReachRatio { get => _YybArmSwingRaisedPoseMaxHandHorizontalReachRatio; set => _YybArmSwingRaisedPoseMaxHandHorizontalReachRatio = value; }

        [Tooltip("YYB 상완 하강 제한 보정 로그를 출력합니다.")]
        [FormerlySerializedAs("logYybArmSwingLimitCorrection")]
        [SerializeField] private bool _logYybArmSwingLimitCorrection= false;
        public bool logYybArmSwingLimitCorrection { get => _logYybArmSwingLimitCorrection; private set => _logYybArmSwingLimitCorrection = value; }

        [Header("YYB Arm Sleeve Anchor Correction")]
        [Tooltip("YYB 소매/어깨 보조본이 상완 본을 따라가지 않아 소매가 어깨에서 무너져 보이는 현상을 줄입니다.")]
        [FormerlySerializedAs("enableYybArmSleeveAnchorCorrection")]
        [SerializeField] private bool _enableYybArmSleeveAnchorCorrection= true;
        public bool enableYybArmSleeveAnchorCorrection { get => _enableYybArmSleeveAnchorCorrection; set => _enableYybArmSleeveAnchorCorrection = value; }

        [Tooltip("소매 상단 보조본이 상완 회전을 따라가는 강도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmSleeveAnchorInfluence")]
        [SerializeField] private float _YybArmSleeveAnchorInfluence= 0.85f;
        public float YybArmSleeveAnchorInfluence { get => _YybArmSleeveAnchorInfluence; set => _YybArmSleeveAnchorInfluence = value; }

        [Tooltip("어깨 캡 보조본이 상완 회전을 따라가는 강도입니다. MMD4Mecanim PPH와 겹치면 어깨가 둥글게 무너질 수 있어 기본값은 0입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("YybArmShoulderCapAnchorInfluence")]
        [SerializeField] private float _YybArmShoulderCapAnchorInfluence= 0f;
        public float YybArmShoulderCapAnchorInfluence { get => _YybArmShoulderCapAnchorInfluence; set => _YybArmShoulderCapAnchorInfluence = value; }

        [Tooltip("소매/어깨 보조본이 한 프레임에 따라갈 수 있는 최대 회전각입니다.")]
        [Range(0f, 120f)] [FormerlySerializedAs("YybArmSleeveAnchorMaxDegrees")]
        [SerializeField] private float _YybArmSleeveAnchorMaxDegrees= 85f;
        public float YybArmSleeveAnchorMaxDegrees { get => _YybArmSleeveAnchorMaxDegrees; set => _YybArmSleeveAnchorMaxDegrees = value; }

        [Tooltip("YYB 소매/어깨 보조본 anchor 보정 로그를 출력합니다.")]
        [FormerlySerializedAs("logYybArmSleeveAnchorCorrection")]
        [SerializeField] private bool _logYybArmSleeveAnchorCorrection= false;
        public bool logYybArmSleeveAnchorCorrection { get => _logYybArmSleeveAnchorCorrection; private set => _logYybArmSleeveAnchorCorrection = value; }

        [Header("디버그 설정 (HumanoidPoseRetargeter에 적용됨)")]
        [Tooltip("본 매핑 관련 디버그 로그 출력")]
        [FormerlySerializedAs("showBoneMappingLog")]
        [SerializeField] private bool _showBoneMappingLog= false;
        public bool showBoneMappingLog { get => _showBoneMappingLog; private set => _showBoneMappingLog = value; }
        [Tooltip("런타임 애니메이션 디버그 로그 출력")]
        [FormerlySerializedAs("showRuntimeAnimationLog")]
        [SerializeField] private bool _showRuntimeAnimationLog= false;
        public bool showRuntimeAnimationLog { get => _showRuntimeAnimationLog; private set => _showRuntimeAnimationLog = value; }

        [Tooltip("Ghost 모델 보이기 (디버깅용)")]
        [FormerlySerializedAs("showGhostModel")]
        [SerializeField] private bool _showGhostModel= false;
        public bool showGhostModel { get => _showGhostModel; private set => _showGhostModel = value; }

        [Tooltip("When the imported Ghost has no renderers, draw a simple skeleton fallback so the debug display is still visible.")]
        [FormerlySerializedAs("showGhostSkeletonWhenNoRenderers")]
        [SerializeField] private bool _showGhostSkeletonWhenNoRenderers= false;
        public bool showGhostSkeletonWhenNoRenderers { get => _showGhostSkeletonWhenNoRenderers; private set => _showGhostSkeletonWhenNoRenderers = value; }

        [Header("Golden Hand Settings")]
        [Tooltip("Finger Stretch Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] [FormerlySerializedAs("FingerStretchScale")]
        [SerializeField] private float _FingerStretchScale= 1.0f;
        public float FingerStretchScale { get => _FingerStretchScale; private set => _FingerStretchScale = value; }
        [Tooltip("Finger Spread Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] [FormerlySerializedAs("FingerSpreadScale")]
        [SerializeField] private float _FingerSpreadScale= 1.0f;
        public float FingerSpreadScale { get => _FingerSpreadScale; private set => _FingerSpreadScale = value; }

        [Space(5)]
        [Tooltip("Thumb Stretch Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] [FormerlySerializedAs("ThumbStretchScale")]
        [SerializeField] private float _ThumbStretchScale= 1.0f;
        public float ThumbStretchScale { get => _ThumbStretchScale; private set => _ThumbStretchScale = value; }
        [Tooltip("Thumb Spread Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] [FormerlySerializedAs("ThumbSpreadScale")]
        [SerializeField] private float _ThumbSpreadScale= 1.0f;
        public float ThumbSpreadScale { get => _ThumbSpreadScale; private set => _ThumbSpreadScale = value; }

        [Header("Thumb Anatomical Guard")]
        [Tooltip("YYB 모델에서 엄지 관절이 손바닥 뒤로 꺾이거나 과하게 벌어지는 현상을 막기 위해 엄지 Humanoid muscle만 제한합니다.")]
        [FormerlySerializedAs("enableThumbAnatomicalGuard")]
        [SerializeField] private bool _enableThumbAnatomicalGuard= true;
        public bool enableThumbAnatomicalGuard { get => _enableThumbAnatomicalGuard; private set => _enableThumbAnatomicalGuard = value; }

        [Tooltip("Manual Animator finger reference를 사용할 때는 엄지 stretch offset을 추가하지 않고 수동 기준 엄지 muscle을 보존합니다.")]
        [FormerlySerializedAs("preserveManualFingerReferenceThumbMuscles")]
        [SerializeField] private bool _preserveManualFingerReferenceThumbMuscles= false;
        public bool preserveManualFingerReferenceThumbMuscles { get => _preserveManualFingerReferenceThumbMuscles; private set => _preserveManualFingerReferenceThumbMuscles = value; }

        [Tooltip("엄지 굽힘 muscle 최소값입니다. 값이 너무 낮으면 엄지가 손바닥 안쪽으로 과하게 접힐 수 있습니다.")]
        [Range(-2.5f, 0f)] [FormerlySerializedAs("ThumbStretchMin")]
        [SerializeField] private float _ThumbStretchMin= -2.1f;
        public float ThumbStretchMin { get => _ThumbStretchMin; private set => _ThumbStretchMin = value; }

        [Tooltip("엄지 굽힘 muscle 최대값입니다. 값이 너무 높으면 엄지가 뒤로 젖혀질 수 있습니다.")]
        [Range(0f, 2.5f)] [FormerlySerializedAs("ThumbStretchMax")]
        [SerializeField] private float _ThumbStretchMax= 1.0f;
        public float ThumbStretchMax { get => _ThumbStretchMax; private set => _ThumbStretchMax = value; }

        [Tooltip("엄지 벌림 muscle 최소값입니다. 값이 너무 낮으면 엄지가 손 구조상 불가능한 방향으로 벌어질 수 있습니다.")]
        [Range(-1.5f, 0f)] [FormerlySerializedAs("ThumbSpreadMin")]
        [SerializeField] private float _ThumbSpreadMin= -0.9f;
        public float ThumbSpreadMin { get => _ThumbSpreadMin; private set => _ThumbSpreadMin = value; }

        [Tooltip("엄지 벌림 muscle 최대값입니다. 값이 너무 높으면 엄지가 손바닥 바깥 방향으로 과하게 벌어질 수 있습니다.")]
        [Range(0f, 1.5f)] [FormerlySerializedAs("ThumbSpreadMax")]
        [SerializeField] private float _ThumbSpreadMax= 0.9f;
        public float ThumbSpreadMax { get => _ThumbSpreadMax; private set => _ThumbSpreadMax = value; }

        [Tooltip("엄지 해부학적 제한이 실제로 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logThumbAnatomicalGuardCorrections")]
        [SerializeField] private bool _logThumbAnatomicalGuardCorrections= false;
        public bool logThumbAnatomicalGuardCorrections { get => _logThumbAnatomicalGuardCorrections; private set => _logThumbAnatomicalGuardCorrections = value; }

        [Tooltip("엄지 muscle 제한 이후에도 YYB 엄지 본이 손 구조상 이상하게 꺾이면, 실제 엄지 본 localRotation을 기준 자세 근처로 제한합니다.")]
        [FormerlySerializedAs("enableThumbLocalRotationGuard")]
        [SerializeField] private bool _enableThumbLocalRotationGuard= true;
        public bool enableThumbLocalRotationGuard { get => _enableThumbLocalRotationGuard; private set => _enableThumbLocalRotationGuard = value; }

        [Tooltip("Manual Animator finger reference를 사용할 때는 최종 엄지 localRotation 가드를 끄고 수동 기준 손 모양을 우선합니다.")]
        [FormerlySerializedAs("disableThumbLocalRotationGuardWithManualFingerReference")]
        [SerializeField] private bool _disableThumbLocalRotationGuardWithManualFingerReference= true;
        public bool disableThumbLocalRotationGuardWithManualFingerReference { get => _disableThumbLocalRotationGuardWithManualFingerReference; private set => _disableThumbLocalRotationGuardWithManualFingerReference = value; }

        [Tooltip("엄지 첫 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다. 너무 낮으면 정상적인 엄지 벌림까지 잘려 reference보다 엄지가 덜 펼쳐질 수 있습니다.")]
        [Range(0f, 90f)] [FormerlySerializedAs("ThumbProximalMaxLocalAngle")]
        [SerializeField] private float _ThumbProximalMaxLocalAngle= 28f;
        public float ThumbProximalMaxLocalAngle { get => _ThumbProximalMaxLocalAngle; private set => _ThumbProximalMaxLocalAngle = value; }

        [Tooltip("엄지 두 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] [FormerlySerializedAs("ThumbIntermediateMaxLocalAngle")]
        [SerializeField] private float _ThumbIntermediateMaxLocalAngle= 55f;
        public float ThumbIntermediateMaxLocalAngle { get => _ThumbIntermediateMaxLocalAngle; private set => _ThumbIntermediateMaxLocalAngle = value; }

        [Tooltip("엄지 끝 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] [FormerlySerializedAs("ThumbDistalMaxLocalAngle")]
        [SerializeField] private float _ThumbDistalMaxLocalAngle= 55f;
        public float ThumbDistalMaxLocalAngle { get => _ThumbDistalMaxLocalAngle; private set => _ThumbDistalMaxLocalAngle = value; }

        [Tooltip("엄지 본 localRotation 제한이 실제로 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logThumbLocalRotationGuardCorrections")]
        [SerializeField] private bool _logThumbLocalRotationGuardCorrections= false;
        public bool logThumbLocalRotationGuardCorrections { get => _logThumbLocalRotationGuardCorrections; private set => _logThumbLocalRotationGuardCorrections = value; }

        [Tooltip("YYB처럼 스킨용 Thumb0 보조 본과 Humanoid Thumb0M 본이 분리된 모델에서 보조 본을 실제 엄지 본 회전에 맞춰 손바닥/엄지 뿌리 메시 변형을 줄입니다.")]
        [FormerlySerializedAs("syncDetachedThumbBaseHelpers")]
        [SerializeField] private bool _syncDetachedThumbBaseHelpers= true;
        public bool syncDetachedThumbBaseHelpers { get => _syncDetachedThumbBaseHelpers; private set => _syncDetachedThumbBaseHelpers = value; }

        [Tooltip("분리된 Thumb0 보조본 위치를 실제 Humanoid Thumb0M 본 쪽으로 제한적으로 맞춥니다. YYB 엄지 뿌리 메시가 손바닥에서 분리되어 보이는 현상을 줄입니다.")]
        [FormerlySerializedAs("syncDetachedThumbBaseHelperPositions")]
        [SerializeField] private bool _syncDetachedThumbBaseHelperPositions= true;
        public bool syncDetachedThumbBaseHelperPositions { get => _syncDetachedThumbBaseHelperPositions; private set => _syncDetachedThumbBaseHelperPositions = value; }

        [Tooltip("분리된 Thumb0 보조 본이 실제 엄지 본을 따라가는 비율입니다. 너무 높으면 엄지 뿌리 스킨이 눌려 짧아 보일 수 있어 YYB 기본값은 제한 추종입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("detachedThumbBaseHelperSyncWeight")]
        [SerializeField] private float _detachedThumbBaseHelperSyncWeight= 0.8f;
        public float detachedThumbBaseHelperSyncWeight { get => _detachedThumbBaseHelperSyncWeight; private set => _detachedThumbBaseHelperSyncWeight = value; }

        [Tooltip("Thumb0 보조본이 실제 엄지 구동본을 따라갈 수 있는 최대 각도입니다. 낮출수록 손꿈치 스킨을 덜 움직입니다.")]
        [Range(0f, 45f)] [FormerlySerializedAs("detachedThumbBaseHelperMaxLocalAngle")]
        [SerializeField] private float _detachedThumbBaseHelperMaxLocalAngle= 28f;
        public float detachedThumbBaseHelperMaxLocalAngle { get => _detachedThumbBaseHelperMaxLocalAngle; private set => _detachedThumbBaseHelperMaxLocalAngle = value; }

        [Tooltip("Thumb0 보조본 위치가 기본 손바닥 앵커에서 벗어날 수 있는 최대 거리입니다. 높이면 실제 엄지 구동본 위치를 더 따르고, 낮추면 손바닥 실루엣을 더 보존합니다.")]
        [Range(0f, 0.02f)] [FormerlySerializedAs("detachedThumbBaseHelperMaxPositionOffset")]
        [SerializeField] private float _detachedThumbBaseHelperMaxPositionOffset= 0.008f;
        public float detachedThumbBaseHelperMaxPositionOffset { get => _detachedThumbBaseHelperMaxPositionOffset; private set => _detachedThumbBaseHelperMaxPositionOffset = value; }

        [Tooltip("YYB처럼 joint_*Thumb0 보조본과 !joint_*Thumb0M 실제 엄지 구동본의 로컬 기준축이 어긋난 모델에서, 왼손 Thumb0 보조본이 source delta를 자기 축에 맞게 다시 해석하도록 추가 축 보정을 적용합니다.")]
        [FormerlySerializedAs("LeftDetachedThumbBaseHelperDeltaAxisOffset")]
        [SerializeField] private Vector3 _LeftDetachedThumbBaseHelperDeltaAxisOffset= Vector3.zero;
        public Vector3 LeftDetachedThumbBaseHelperDeltaAxisOffset { get => _LeftDetachedThumbBaseHelperDeltaAxisOffset; private set => _LeftDetachedThumbBaseHelperDeltaAxisOffset = value; }

        [Tooltip("YYB처럼 joint_*Thumb0 보조본과 !joint_*Thumb0M 실제 엄지 구동본의 로컬 기준축이 어긋난 모델에서, 오른손 Thumb0 보조본이 source delta를 자기 축에 맞게 다시 해석하도록 추가 축 보정을 적용합니다.")]
        [FormerlySerializedAs("RightDetachedThumbBaseHelperDeltaAxisOffset")]
        [SerializeField] private Vector3 _RightDetachedThumbBaseHelperDeltaAxisOffset= Vector3.zero;
        public Vector3 RightDetachedThumbBaseHelperDeltaAxisOffset { get => _RightDetachedThumbBaseHelperDeltaAxisOffset; private set => _RightDetachedThumbBaseHelperDeltaAxisOffset = value; }

        [Tooltip("YYB처럼 Thumb0 보조본 기본 자세가 넓게 벌어진 모델에서, 왼손 Thumb0 helper 목표 회전에 정적 보정치를 직접 더해 webbing 벌어짐/각짐을 줄입니다.")]
        [FormerlySerializedAs("LeftDetachedThumbBaseHelperTargetRotationOffset")]
        [SerializeField] private Vector3 _LeftDetachedThumbBaseHelperTargetRotationOffset= Vector3.zero;
        public Vector3 LeftDetachedThumbBaseHelperTargetRotationOffset { get => _LeftDetachedThumbBaseHelperTargetRotationOffset; private set => _LeftDetachedThumbBaseHelperTargetRotationOffset = value; }

        [Tooltip("YYB처럼 Thumb0 보조본 기본 자세가 넓게 벌어진 모델에서, 오른손 Thumb0 helper 목표 회전에 정적 보정치를 직접 더해 webbing 벌어짐/각짐을 줄입니다.")]
        [FormerlySerializedAs("RightDetachedThumbBaseHelperTargetRotationOffset")]
        [SerializeField] private Vector3 _RightDetachedThumbBaseHelperTargetRotationOffset= Vector3.zero;
        public Vector3 RightDetachedThumbBaseHelperTargetRotationOffset { get => _RightDetachedThumbBaseHelperTargetRotationOffset; private set => _RightDetachedThumbBaseHelperTargetRotationOffset = value; }

        [Tooltip("YYB 손꿈치/엄지 뿌리 스킨용 Thumb0 보조본을 기본 손바닥 자세 쪽으로 안정화합니다. 엄지 움직임보다 손바닥 실루엣 보존을 우선합니다.")]
        [FormerlySerializedAs("stabilizeDetachedThumbBasePalm")]
        [SerializeField] private bool _stabilizeDetachedThumbBasePalm= false;
        public bool stabilizeDetachedThumbBasePalm { get => _stabilizeDetachedThumbBasePalm; private set => _stabilizeDetachedThumbBasePalm = value; }

        [Tooltip("Thumb0 보조본을 기본 자세로 되돌리는 강도입니다. 높이면 손꿈치가 고정되고, 낮추면 엄지 뿌리가 실제 엄지 구동본을 더 따라갑니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("detachedThumbBasePalmStabilizeWeight")]
        [SerializeField] private float _detachedThumbBasePalmStabilizeWeight= 0f;
        public float detachedThumbBasePalmStabilizeWeight { get => _detachedThumbBasePalmStabilizeWeight; private set => _detachedThumbBasePalmStabilizeWeight = value; }

        [Tooltip("손꿈치 안정화 상태에서 Thumb0 보조본이 기본 자세에서 벗어날 수 있는 최대 각도입니다. 낮을수록 엄지 뿌리 메시가 덜 벌어집니다.")]
        [Range(0f, 45f)] [FormerlySerializedAs("detachedThumbBasePalmMaxLocalAngle")]
        [SerializeField] private float _detachedThumbBasePalmMaxLocalAngle= 45f;
        public float detachedThumbBasePalmMaxLocalAngle { get => _detachedThumbBasePalmMaxLocalAngle; private set => _detachedThumbBasePalmMaxLocalAngle = value; }

        [Tooltip("YYB 엄지와 손바닥 경계선이 딱딱하게 찢겨 보일 때 Thumb0 보조본을 기본 손바닥 웹빙 형태 쪽으로 약하게 안정화합니다.")]
        [FormerlySerializedAs("stabilizeThumbWebbingCrease")]
        [SerializeField] private bool _stabilizeThumbWebbingCrease= true;
        public bool stabilizeThumbWebbingCrease { get => _stabilizeThumbWebbingCrease; private set => _stabilizeThumbWebbingCrease = value; }

        [Tooltip("엄지 웹빙 라인을 안정화하는 강도입니다. 높이면 엄지-손바닥 경계가 덜 찢기지만 엄지 뿌리 움직임이 둔해질 수 있습니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("thumbWebbingCreaseStabilizeWeight")]
        [SerializeField] private float _thumbWebbingCreaseStabilizeWeight= 0.35f;
        public float thumbWebbingCreaseStabilizeWeight { get => _thumbWebbingCreaseStabilizeWeight; private set => _thumbWebbingCreaseStabilizeWeight = value; }

        [Tooltip("엄지 웹빙 안정화 상태에서 Thumb0 보조본이 기본 자세에서 벗어날 수 있는 최대 회전각입니다.")]
        [Range(0f, 45f)] [FormerlySerializedAs("thumbWebbingCreaseMaxLocalAngle")]
        [SerializeField] private float _thumbWebbingCreaseMaxLocalAngle= 18f;
        public float thumbWebbingCreaseMaxLocalAngle { get => _thumbWebbingCreaseMaxLocalAngle; private set => _thumbWebbingCreaseMaxLocalAngle = value; }

        [Tooltip("엄지 웹빙 안정화 상태에서 Thumb0 보조본 위치가 기본 위치에서 벗어날 수 있는 최대 거리입니다.")]
        [Range(0f, 0.02f)] [FormerlySerializedAs("thumbWebbingCreaseMaxPositionOffset")]
        [SerializeField] private float _thumbWebbingCreaseMaxPositionOffset= 0.005f;
        public float thumbWebbingCreaseMaxPositionOffset { get => _thumbWebbingCreaseMaxPositionOffset; private set => _thumbWebbingCreaseMaxPositionOffset = value; }

        [Tooltip("엄지가 손바닥 normal 방향으로 너무 서거나 중간 관절이 과하게 말려 화면상 짧아 보이는 현상을 줄입니다.")]
        [FormerlySerializedAs("enableThumbVisualLengthGuard")]
        [SerializeField] private bool _enableThumbVisualLengthGuard= true;
        public bool enableThumbVisualLengthGuard { get => _enableThumbVisualLengthGuard; private set => _enableThumbVisualLengthGuard = value; }

        [Tooltip("엄지 첫 마디가 손바닥 normal 방향으로 최소한 앞으로 나와야 하는 성분입니다. 너무 낮으면 엄지가 손바닥 뒤로 누운 것처럼 보입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("ThumbProjectionMinPalmNormal")]
        [SerializeField] private float _ThumbProjectionMinPalmNormal= DEFAULT_THUMB_PROJECTION_MIN_PALM_NORMAL;
        public float ThumbProjectionMinPalmNormal { get => _ThumbProjectionMinPalmNormal; private set => _ThumbProjectionMinPalmNormal = value; }

        [Tooltip("엄지 첫 마디가 손바닥 normal 방향으로 나갈 수 있는 최대 성분입니다. 너무 높으면 카메라 정면에서 엄지가 짧게 보입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("ThumbProjectionMaxPalmNormal")]
        [SerializeField] private float _ThumbProjectionMaxPalmNormal= 0.58f;
        public float ThumbProjectionMaxPalmNormal { get => _ThumbProjectionMaxPalmNormal; private set => _ThumbProjectionMaxPalmNormal = value; }

        [Tooltip("엄지 첫 마디 투영 보정 강도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("ThumbProjectionGuardWeight")]
        [SerializeField] private float _ThumbProjectionGuardWeight= 1f;
        public float ThumbProjectionGuardWeight { get => _ThumbProjectionGuardWeight; private set => _ThumbProjectionGuardWeight = value; }

        [Tooltip("엄지 첫 마디와 검지 시작 방향 사이의 최대 벌어짐 각도입니다. 높으면 엄지가 손바닥 바깥으로 과하게 벌어질 수 있습니다.")]
        [Range(0f, 90f)] [FormerlySerializedAs("ThumbIndexMaxSpreadAngle")]
        [SerializeField] private float _ThumbIndexMaxSpreadAngle= 70f;
        public float ThumbIndexMaxSpreadAngle { get => _ThumbIndexMaxSpreadAngle; private set => _ThumbIndexMaxSpreadAngle = value; }

        [Tooltip("엄지-검지 벌어짐 제한 강도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("ThumbIndexSpreadGuardWeight")]
        [SerializeField] private float _ThumbIndexSpreadGuardWeight= 1f;
        public float ThumbIndexSpreadGuardWeight { get => _ThumbIndexSpreadGuardWeight; private set => _ThumbIndexSpreadGuardWeight = value; }

        [Tooltip("엄지 첫 마디와 둘째 마디 사이 허용 굽힘 각도입니다. 이 값을 넘으면 끝 마디를 더 펴서 짧아 보이는 현상을 줄입니다.")]
        [Range(0f, 60f)] [FormerlySerializedAs("ThumbMaxSegmentBendAngle")]
        [SerializeField] private float _ThumbMaxSegmentBendAngle= 10f;
        public float ThumbMaxSegmentBendAngle { get => _ThumbMaxSegmentBendAngle; private set => _ThumbMaxSegmentBendAngle = value; }

        [Tooltip("엄지 둘째 마디를 첫 마디 방향에 맞춰 펴는 강도입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("ThumbSegmentStraightenWeight")]
        [SerializeField] private float _ThumbSegmentStraightenWeight= 0.9f;
        public float ThumbSegmentStraightenWeight { get => _ThumbSegmentStraightenWeight; private set => _ThumbSegmentStraightenWeight = value; }

        [Header("VMD Recording Orchestration")]
        [Tooltip("FBX 로드 후 녹화 시작까지의 대기 시간 (초)")]
        [Range(0f, 10f)] [FormerlySerializedAs("startDelay")]
        [SerializeField] private float _startDelay= 3.0f;
        public float startDelay { get => _startDelay; set => _startDelay = value; }

        [Tooltip("VMD recording playback speed. 1 is normal speed.")]
        [Range(0.1f, 4f)] [FormerlySerializedAs("vmdRecordingPlaybackSpeed")]
        [SerializeField] private float _vmdRecordingPlaybackSpeed= 1f;
        public float vmdRecordingPlaybackSpeed { get => _vmdRecordingPlaybackSpeed; private set => _vmdRecordingPlaybackSpeed = value; }

        [Tooltip("Opt-in legacy reference timing for satisfaction_2 regression evidence. Off by default so normal VMD export records at 1x.")]
        [FormerlySerializedAs("useKnownMmdReferenceTiming")]
        [SerializeField] private bool _useKnownMmdReferenceTiming= false;
        public bool useKnownMmdReferenceTiming { get => _useKnownMmdReferenceTiming; private set => _useKnownMmdReferenceTiming = value; }

        [Tooltip("녹화 시작 직전에 clip time 0을 고정 샘플링해 retarget/grounding 첫 프레임을 안정화하는 프레임 수입니다.")]
        [Range(0, MAX_RETARGET_PREWARM_FRAME_COUNT)] [FormerlySerializedAs("RetargetPrewarmFrameCount")]
        [SerializeField] private int _RetargetPrewarmFrameCount= 6;
        public int RetargetPrewarmFrameCount { get => _RetargetPrewarmFrameCount; private set => _RetargetPrewarmFrameCount = value; }

        [Tooltip("VMD 저장이 성공하면 추가로 이 폴더에도 같은 VMD 파일을 복사합니다. 비워두면 복사하지 않습니다. (예: C:/Users/flzhv/Desktop/MMD/MikuMikuDance_v932x64/SaveFile)")]
        [FormerlySerializedAs("additionalVmdCopyFolder")]
        [SerializeField] private string _additionalVmdCopyFolder= "";
        public string additionalVmdCopyFolder { get => _additionalVmdCopyFolder; private set => _additionalVmdCopyFolder = value; }

        [Tooltip("비교 CSV/프레임 캡처 Probe를 켭니다. 일반 변환에서는 미세 멈춤을 줄이기 위해 끄고, 회귀 테스트 때만 켭니다.")]
        [HideInInspector]
        [FormerlySerializedAs("enableRecordingDiagnostics")]
        [SerializeField] private bool _enableRecordingDiagnostics= false;
        public bool enableRecordingDiagnostics { get => _enableRecordingDiagnostics; set => _enableRecordingDiagnostics = value; }

        [Tooltip("회귀 테스트 때 녹화 중 Unity 시간을 30fps로 고정합니다. 일반 GameView 재생에서는 배속/멈칫 체감이 생길 수 있어 끕니다.")]
        [HideInInspector]
        [FormerlySerializedAs("useDeterministicCaptureFramerateForDiagnostics")]
        [SerializeField] private bool _useDeterministicCaptureFramerateForDiagnostics= false;
        public bool useDeterministicCaptureFramerateForDiagnostics { get => _useDeterministicCaptureFramerateForDiagnostics; set => _useDeterministicCaptureFramerateForDiagnostics = value; }

        [Tooltip("Recording Diagnostics를 켰을 때 손 close-up 캡처도 함께 남깁니다.")]
        [HideInInspector]
        [FormerlySerializedAs("enableDiagnosticFingerCloseups")]
        [SerializeField] private bool _enableDiagnosticFingerCloseups= true;
        public bool enableDiagnosticFingerCloseups { get => _enableDiagnosticFingerCloseups; set => _enableDiagnosticFingerCloseups = value; }

        [Tooltip("MotionComparisonProbe PNG 캡처 해상도 preset입니다. VMD frame/key 품질에는 영향을 주지 않습니다.")]
        [HideInInspector]
        [FormerlySerializedAs("recordingCaptureQuality")]
        [SerializeField] private RecordingCaptureQualityPreset _recordingCaptureQuality= RecordingCaptureQualityPreset.Existing960Square;
        public RecordingCaptureQualityPreset recordingCaptureQuality { get => _recordingCaptureQuality; set => _recordingCaptureQuality = value; }

        [Tooltip("Custom preset을 선택했을 때 사용할 PNG 캡처 폭입니다.")]
        [HideInInspector]
        [FormerlySerializedAs("customRecordingCaptureWidth")]
        [SerializeField] private int _customRecordingCaptureWidth= 3840;
        public int customRecordingCaptureWidth { get => _customRecordingCaptureWidth; set => _customRecordingCaptureWidth = value; }

        [Tooltip("Custom preset을 선택했을 때 사용할 PNG 캡처 높이입니다.")]
        [HideInInspector]
        [FormerlySerializedAs("customRecordingCaptureHeight")]
        [SerializeField] private int _customRecordingCaptureHeight= 2160;
        public int customRecordingCaptureHeight { get => _customRecordingCaptureHeight; set => _customRecordingCaptureHeight = value; }

        [Tooltip("Editor smoke에서 MotionComparisonProbe 엄지 리스크가 임계치를 넘으면 VMD 저장 성공도 smoke 실패로 승격합니다.")]
        [FormerlySerializedAs("failEditorSmokeOnThumbRisk")]
        [SerializeField] private bool _failEditorSmokeOnThumbRisk= true;
        public bool failEditorSmokeOnThumbRisk { get => _failEditorSmokeOnThumbRisk; private set => _failEditorSmokeOnThumbRisk = value; }

        [Tooltip("Editor smoke에서 일반 FBX용 엄지 해부학 리스크의 허용 최대값입니다. 이 값을 넘으면 smoke를 실패로 봅니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("editorSmokeMaxGenericThumbAnatomyRisk")]
        [SerializeField] private float _editorSmokeMaxGenericThumbAnatomyRisk= 0.4f;
        public float editorSmokeMaxGenericThumbAnatomyRisk { get => _editorSmokeMaxGenericThumbAnatomyRisk; private set => _editorSmokeMaxGenericThumbAnatomyRisk = value; }

        [Tooltip("Editor smoke에서 YYB 전용 변형 리스크의 허용 최대값입니다. YYB 타깃일 때만 사용합니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("editorSmokeMaxYybDeformationRisk")]
        [SerializeField] private float _editorSmokeMaxYybDeformationRisk= 0.35f;
        public float editorSmokeMaxYybDeformationRisk { get => _editorSmokeMaxYybDeformationRisk; private set => _editorSmokeMaxYybDeformationRisk = value; }

        [Header("Visual Jitter Guard")]
        [Tooltip("Editor/GameView 프레임이 밀려도 Ghost clip time이 한 프레임에 크게 건너뛰지 않게 제한합니다.")]
        [FormerlySerializedAs("clampRetargetVisualClipStep")]
        [SerializeField] private bool _clampRetargetVisualClipStep= false;
        public bool clampRetargetVisualClipStep { get => _clampRetargetVisualClipStep; private set => _clampRetargetVisualClipStep = value; }

        [Tooltip("Ghost clip time이 한 렌더 프레임에 전진할 수 있는 기준 FPS입니다. 30이면 한 번에 1/30초 이상 건너뛰지 않습니다.")]
        [Range(15f, 120f)] [FormerlySerializedAs("RetargetVisualClipFrameRate")]
        [SerializeField] private float _RetargetVisualClipFrameRate= 30f;
        public float RetargetVisualClipFrameRate { get => _RetargetVisualClipFrameRate; private set => _RetargetVisualClipFrameRate = value; }

        [Tooltip("프레임 지연으로 retarget pose가 한 번에 크게 바뀔 때 애니메이션 시간은 보존하고 target pose만 부드럽게 따라가게 합니다.")]
        [FormerlySerializedAs("smoothRetargetPoseOnVisualStepSpike")]
        [SerializeField] private bool _smoothRetargetPoseOnVisualStepSpike= true;
        public bool smoothRetargetPoseOnVisualStepSpike { get => _smoothRetargetPoseOnVisualStepSpike; set => _smoothRetargetPoseOnVisualStepSpike = value; }

        [Tooltip("pose spike smoothing 때 현재 FBX pose를 반영하는 비율입니다. 1에 가까울수록 원본 모션을 더 보존하고, 낮을수록 pop을 더 줄입니다.")]
        [Range(0.1f, 1f)] [FormerlySerializedAs("RetargetPoseVisualSpikeCurrentWeight")]
        [SerializeField] private float _RetargetPoseVisualSpikeCurrentWeight= 0.65f;
        public float RetargetPoseVisualSpikeCurrentWeight { get => _RetargetPoseVisualSpikeCurrentWeight; set => _RetargetPoseVisualSpikeCurrentWeight = value; }

        [Tooltip("Optional forearm stretch clamp around the current pose during visual spike smoothing. 0 disables the clamp.")]
        [Range(0f, 1f)] [FormerlySerializedAs("RetargetPoseVisualSpikeForearmStretchClampMaxOffset")]
        [SerializeField] private float _RetargetPoseVisualSpikeForearmStretchClampMaxOffset= 0f;
        public float RetargetPoseVisualSpikeForearmStretchClampMaxOffset { get => _RetargetPoseVisualSpikeForearmStretchClampMaxOffset; set => _RetargetPoseVisualSpikeForearmStretchClampMaxOffset = value; }

        [Tooltip("이 값보다 큰 muscle delta가 발생하면 frame-time spike가 아니어도 pose smoothing을 적용합니다.")]
        [Range(0.05f, 1f)] [FormerlySerializedAs("RetargetPoseVisualMuscleDeltaThreshold")]
        [SerializeField] private float _RetargetPoseVisualMuscleDeltaThreshold= 0.35f;
        public float RetargetPoseVisualMuscleDeltaThreshold { get => _RetargetPoseVisualMuscleDeltaThreshold; private set => _RetargetPoseVisualMuscleDeltaThreshold = value; }

        [Header("Final Tuning")]
        [Tooltip("높이 보정 (미터 단위). 0.02 = 2cm 올림")]
        [Range(-0.5f, 0.5f)] [FormerlySerializedAs("HeightOffset")]
        [SerializeField] private float _HeightOffset= 0.0f;
        public float HeightOffset { get => _HeightOffset; private set => _HeightOffset = value; }

        [Tooltip("보폭 비율 (1.0 = 자동, 미끄러지면 조절)")]
        [Range(0f, 1.5f)] [FormerlySerializedAs("MovementScaleMultiplier")]
        [SerializeField] private float _MovementScaleMultiplier= 1.0f;
        public float MovementScaleMultiplier { get => _MovementScaleMultiplier; private set => _MovementScaleMultiplier = value; }

        [Header("Root Motion Spike Guard")]
        [Tooltip("FBX root delta가 한 프레임에 과도하게 튀면 순간이동으로 보고 해당 프레임의 추가 root 이동을 무시합니다.")]
        [FormerlySerializedAs("clampRetargetRootDeltaSpikes")]
        [SerializeField] private bool _clampRetargetRootDeltaSpikes= true;
        public bool clampRetargetRootDeltaSpikes { get => _clampRetargetRootDeltaSpikes; private set => _clampRetargetRootDeltaSpikes = value; }

        [Tooltip("한 프레임에 허용할 최대 root 이동량입니다. 일반 춤 동작보다 훨씬 큰 값만 순간이동 후보로 처리합니다.")]
        [Range(0.001f, 1.0f)] [FormerlySerializedAs("MaxRetargetRootDeltaPerFrame")]
        [SerializeField] private float _MaxRetargetRootDeltaPerFrame= 0.25f;
        public float MaxRetargetRootDeltaPerFrame { get => _MaxRetargetRootDeltaPerFrame; private set => _MaxRetargetRootDeltaPerFrame = value; }

        [Tooltip("root delta spike를 무시했을 때 최초 1회 진단 로그를 출력합니다.")]
        [FormerlySerializedAs("logRetargetRootDeltaSpikes")]
        [SerializeField] private bool _logRetargetRootDeltaSpikes= false;
        public bool logRetargetRootDeltaSpikes { get => _logRetargetRootDeltaSpikes; private set => _logRetargetRootDeltaSpikes = value; }

        [Header("Hips Local Position Spike Guard")]
        [Tooltip("Target Hips localPosition outliers are clamped per frame to prevent one-frame body teleport artifacts in exported VMD.")]
        [FormerlySerializedAs("clampRetargetHipsLocalPositionSpikes")]
        [SerializeField] private bool _clampRetargetHipsLocalPositionSpikes= false;
        public bool clampRetargetHipsLocalPositionSpikes { get => _clampRetargetHipsLocalPositionSpikes; private set => _clampRetargetHipsLocalPositionSpikes = value; }

        [Tooltip("Maximum allowed target Hips localPosition movement per frame before it is treated as a visual teleport artifact.")]
        [Range(0.005f, 0.25f)] [FormerlySerializedAs("MaxRetargetHipsLocalPositionDeltaPerFrame")]
        [SerializeField] private float _MaxRetargetHipsLocalPositionDeltaPerFrame= 0.02f;
        public float MaxRetargetHipsLocalPositionDeltaPerFrame { get => _MaxRetargetHipsLocalPositionDeltaPerFrame; private set => _MaxRetargetHipsLocalPositionDeltaPerFrame = value; }

        [Header("Grounding Stability Guard")]
        [Tooltip("발바닥 접지 보정이 한 프레임에 크게 튀지 않도록 부드럽게 반영합니다.")]
        [FormerlySerializedAs("smoothRetargetGrounding")]
        [SerializeField] private bool _smoothRetargetGrounding= true;
        public bool smoothRetargetGrounding { get => _smoothRetargetGrounding; private set => _smoothRetargetGrounding = value; }

        [Tooltip("한 프레임에 허용할 최대 수직 접지 보정값입니다.")]
        [Range(0.001f, 0.2f)] [FormerlySerializedAs("MaxGroundingVerticalStepPerFrame")]
        [SerializeField] private float _MaxGroundingVerticalStepPerFrame= 0.01f;
        public float MaxGroundingVerticalStepPerFrame { get => _MaxGroundingVerticalStepPerFrame; private set => _MaxGroundingVerticalStepPerFrame = value; }

        [Tooltip("접지 보정 목표값을 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("GroundingSmoothing")]
        [SerializeField] private float _GroundingSmoothing= 0.25f;
        public float GroundingSmoothing { get => _GroundingSmoothing; private set => _GroundingSmoothing = value; }

        [Tooltip("이 값보다 작은 발바닥 떨림은 무시합니다.")]
        [Range(0f, 0.05f)] [FormerlySerializedAs("GroundingDeadZone")]
        [SerializeField] private float _GroundingDeadZone= 0.005f;
        public float GroundingDeadZone { get => _GroundingDeadZone; private set => _GroundingDeadZone = value; }

        [Tooltip("초기 접지 확정 뒤에 타깃 root Y를 고정합니다. MMD VMD export에서는 이후 프레임의 발 빠짐을 막기 위해 기본 비활성화합니다.")]
        [FormerlySerializedAs("FreezeRootYAfterInitialGrounding")]
        [SerializeField] private bool _FreezeRootYAfterInitialGrounding= true;
        public bool FreezeRootYAfterInitialGrounding { get => _FreezeRootYAfterInitialGrounding; private set => _FreezeRootYAfterInitialGrounding = value; }

        [Tooltip("전체 renderer bounds 하단이 발바닥 추정치에서 과하게 멀어지면 옷/머리카락/소매 outlier로 보고 접지 기준에서 제외합니다.")]
        [FormerlySerializedAs("rejectRendererGroundingOutliers")]
        [SerializeField] private bool _rejectRendererGroundingOutliers= true;
        public bool rejectRendererGroundingOutliers { get => _rejectRendererGroundingOutliers; private set => _rejectRendererGroundingOutliers = value; }

        [Tooltip("renderer bounds 하단과 발바닥 추정치 사이에 허용할 최대 거리입니다. 이 값을 넘으면 foot 기준 접지로 되돌립니다.")]
        [Range(0.02f, 0.3f)] [FormerlySerializedAs("MaxRendererFootGroundingSeparation")]
        [SerializeField] private float _MaxRendererFootGroundingSeparation= 0.12f;
        public float MaxRendererFootGroundingSeparation { get => _MaxRendererFootGroundingSeparation; private set => _MaxRendererFootGroundingSeparation = value; }

        [Tooltip("최종 메시 bounds 접지 보정의 작은 잔여 오차를 부드럽게 반영해 모델 전체 떨림을 줄입니다.")]
        [FormerlySerializedAs("smoothLateVisualGroundingCorrection")]
        [SerializeField] private bool _smoothLateVisualGroundingCorrection= true;
        public bool smoothLateVisualGroundingCorrection { get => _smoothLateVisualGroundingCorrection; private set => _smoothLateVisualGroundingCorrection = value; }

        [Tooltip("Late visual grounding 잔여 오차가 이 값보다 작으면 smoothing 대상으로 봅니다. 큰 오차는 공중 부유 방지를 위해 즉시 보정합니다.")]
        [Range(0.005f, 0.1f)] [FormerlySerializedAs("LateVisualGroundingSnapThreshold")]
        [SerializeField] private float _LateVisualGroundingSnapThreshold= 0.03f;
        public float LateVisualGroundingSnapThreshold { get => _LateVisualGroundingSnapThreshold; private set => _LateVisualGroundingSnapThreshold = value; }

        [Tooltip("작은 late visual grounding 잔여 오차를 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)] [FormerlySerializedAs("LateVisualGroundingSmoothing")]
        [SerializeField] private float _LateVisualGroundingSmoothing= 0.25f;
        public float LateVisualGroundingSmoothing { get => _LateVisualGroundingSmoothing; private set => _LateVisualGroundingSmoothing = value; }

        [Tooltip("작은 late visual grounding smoothing 보정이 한 프레임에 움직일 수 있는 최대 Y 이동량입니다.")]
        [Range(0.001f, 0.05f)] [FormerlySerializedAs("MaxLateVisualGroundingStepPerFrame")]
        [SerializeField] private float _MaxLateVisualGroundingStepPerFrame= 0.003f;
        public float MaxLateVisualGroundingStepPerFrame { get => _MaxLateVisualGroundingStepPerFrame; private set => _MaxLateVisualGroundingStepPerFrame = value; }

        [Header("Final IK Foot Grounding Experiment")]
        [Tooltip("Opt-in only. Adds BipedIK + GrounderBipedIK as a low-weight foot contact experiment after PoseSpaceRetargeter.")]
        [FormerlySerializedAs("enableFinalIkFootGroundingExperiment")]
        [SerializeField] private bool _enableFinalIkFootGroundingExperiment= false;
        public bool enableFinalIkFootGroundingExperiment { get => _enableFinalIkFootGroundingExperiment; set => _enableFinalIkFootGroundingExperiment = value; }

        [Tooltip("GrounderBipedIK master weight. Keep low so it cannot replace PoseSpaceRetargeter output.")]
        [Range(0f, 0.25f)] [FormerlySerializedAs("finalIkFootGroundingWeight")]
        [SerializeField] private float _finalIkFootGroundingWeight= 0.15f;
        public float finalIkFootGroundingWeight { get => _finalIkFootGroundingWeight; private set => _finalIkFootGroundingWeight = value; }

        [Tooltip("Maximum vertical step searched by Final IK grounding.")]
        [Range(0f, 0.08f)] [FormerlySerializedAs("finalIkFootGroundingMaxStep")]
        [SerializeField] private float _finalIkFootGroundingMaxStep= 0.05f;
        public float finalIkFootGroundingMaxStep { get => _finalIkFootGroundingMaxStep; private set => _finalIkFootGroundingMaxStep = value; }

        [Tooltip("Approximate foot radius for Final IK grounding ray/capsule casts.")]
        [Range(0.01f, 0.2f)] [FormerlySerializedAs("finalIkFootGroundingFootRadius")]
        [SerializeField] private float _finalIkFootGroundingFootRadius= 0.06f;
        public float finalIkFootGroundingFootRadius { get => _finalIkFootGroundingFootRadius; private set => _finalIkFootGroundingFootRadius = value; }

        [Tooltip("Velocity prediction for Final IK grounding. Keep zero until visual evidence proves pre-echo is safe.")]
        [Range(0f, 0.2f)] [FormerlySerializedAs("finalIkFootGroundingPrediction")]
        [SerializeField] private float _finalIkFootGroundingPrediction= 0f;
        public float finalIkFootGroundingPrediction { get => _finalIkFootGroundingPrediction; private set => _finalIkFootGroundingPrediction = value; }

        [Tooltip("Foot rotation correction weight. Keep zero for the first contact-only experiment.")]
        [Range(0f, 1f)] [FormerlySerializedAs("finalIkFootGroundingFootRotationWeight")]
        [SerializeField] private float _finalIkFootGroundingFootRotationWeight= 0f;
        public float finalIkFootGroundingFootRotationWeight { get => _finalIkFootGroundingFootRotationWeight; private set => _finalIkFootGroundingFootRotationWeight = value; }

        [Tooltip("Pelvis smoothing damper used by the Final IK grounding solver.")]
        [Range(0f, 1f)] [FormerlySerializedAs("finalIkFootGroundingPelvisDamper")]
        [SerializeField] private float _finalIkFootGroundingPelvisDamper= 0.1f;
        public float finalIkFootGroundingPelvisDamper { get => _finalIkFootGroundingPelvisDamper; private set => _finalIkFootGroundingPelvisDamper = value; }

        [Tooltip("Log when the opt-in Final IK foot grounding experiment is configured.")]
        [FormerlySerializedAs("logFinalIkFootGroundingExperiment")]
        [SerializeField] private bool _logFinalIkFootGroundingExperiment= false;
        public bool logFinalIkFootGroundingExperiment { get => _logFinalIkFootGroundingExperiment; private set => _logFinalIkFootGroundingExperiment = value; }

        [Header("Thumb Digital Orthopedics (Offset)")]
        [Tooltip("양손 공통 엄지 회전 Offset입니다. YYB 엄지 기준축이 수동 기준과 다를 때 최종 렌더 포즈의 엄지 첫 관절 기준축을 보정합니다.")]
        [FormerlySerializedAs("ThumbRotationOffset")]
        [SerializeField] private Vector3 _ThumbRotationOffset= new Vector3(-10f, -30f, 0f);
        public Vector3 ThumbRotationOffset { get => _ThumbRotationOffset; private set => _ThumbRotationOffset = value; }

        [Tooltip("오른손 엄지에는 공통 Offset의 Y/Z축을 반전해 좌우 mirror 축 차이를 보정합니다.")]
        [FormerlySerializedAs("mirrorRightThumbRotationOffset")]
        [SerializeField] private bool _mirrorRightThumbRotationOffset= true;
        public bool mirrorRightThumbRotationOffset { get => _mirrorRightThumbRotationOffset; private set => _mirrorRightThumbRotationOffset = value; }

        [Tooltip("왼손 엄지에 공통 Offset 이후 추가로 더하는 회전 Offset입니다.")]
        [FormerlySerializedAs("LeftThumbRotationOffset")]
        [SerializeField] private Vector3 _LeftThumbRotationOffset= Vector3.zero;
        public Vector3 LeftThumbRotationOffset { get => _LeftThumbRotationOffset; private set => _LeftThumbRotationOffset = value; }

        [Tooltip("오른손 엄지에 공통 Offset 이후 추가로 더하는 회전 Offset입니다.")]
        [FormerlySerializedAs("RightThumbRotationOffset")]
        [SerializeField] private Vector3 _RightThumbRotationOffset= Vector3.zero;
        public Vector3 RightThumbRotationOffset { get => _RightThumbRotationOffset; private set => _RightThumbRotationOffset = value; }

        [Tooltip("켜면 오래된 씬에서 ThumbStretchOffset이 0으로 저장되어 있어도 기본 보정값(-0.1)을 사용합니다.")]
        [FormerlySerializedAs("useDefaultThumbStretchOffsetWhenUnset")]
        [SerializeField] private bool _useDefaultThumbStretchOffsetWhenUnset= true;
        public bool useDefaultThumbStretchOffsetWhenUnset { get => _useDefaultThumbStretchOffsetWhenUnset; private set => _useDefaultThumbStretchOffsetWhenUnset = value; }

        [Tooltip("Muscle Offset (Stretch). Default: -0.1")]
        [Range(-0.5f, 0.5f)] [FormerlySerializedAs("ThumbStretchOffset")]
        [SerializeField] private float _ThumbStretchOffset= DEFAULT_THUMB_STRETCH_OFFSET;
        public float ThumbStretchOffset { get => _ThumbStretchOffset; private set => _ThumbStretchOffset = value; }

        [Header("Smart Curve (Dynamics)")]
        [FormerlySerializedAs("EnableSmartCurve")]
        [SerializeField] private bool _EnableSmartCurve= true;
        public bool EnableSmartCurve { get => _EnableSmartCurve; private set => _EnableSmartCurve = value; }
        [Tooltip("Standard Finger Dampen Strength (0.1 ~ 0.5)")]
        [Range(0.0f, 1.0f)] [FormerlySerializedAs("SmartCurveStrength")]
        [SerializeField] private float _SmartCurveStrength= 0.5f;
        public float SmartCurveStrength { get => _SmartCurveStrength; private set => _SmartCurveStrength = value; }
        [FormerlySerializedAs("StretchThreshold")]
        [SerializeField] private float _StretchThreshold= 0.7f;
        public float StretchThreshold { get => _StretchThreshold; private set => _StretchThreshold = value; }

        [FormerlySerializedAs("EnableThumbSmartCurve")]
        [SerializeField] private bool _EnableThumbSmartCurve= true;
        public bool EnableThumbSmartCurve { get => _EnableThumbSmartCurve; private set => _EnableThumbSmartCurve = value; }
        [Range(0.0f, 1.0f)] [FormerlySerializedAs("ThumbSmartCurveStrength")]
        [SerializeField] private float _ThumbSmartCurveStrength= 0.5f;
        public float ThumbSmartCurveStrength { get => _ThumbSmartCurveStrength; private set => _ThumbSmartCurveStrength = value; }

        [Space(10)]

        #endregion

        #region Private 필드
        internal IFileBrowserService _fileBrowserService;
        private AssimpFBXImporter _fbxImporter;
        private FBXImportController _importController;
        private FBXConversionCoordinator _conversionCoordinator;
        internal bool _isProcessing;
        private GameObject _activeGhostContainer;
        internal HumanoidSampleCode _activeRecorderController;
        private PoseSpaceRetargeter _activeRetargeter;
        private TargetIdlePoseGuard _idlePoseGuard;
        private readonly List<BooleanFieldSnapshot> _retargetBooleanSnapshots = new List<BooleanFieldSnapshot>();
#if UNITY_EDITOR
        private struct EditorSmokeSettingsSnapshot
        {
            public bool enableRecordingDiagnostics;
            public bool enableDiagnosticFingerCloseups;
            public bool useDeterministicCaptureFramerateForDiagnostics;
            public float startDelay;
        }

        internal bool _editorSmokeRecordingOverrideActive;
        internal int _editorSmokeTargetFrameCount;
        internal float _editorSmokeDurationSeconds;
        internal float[] _editorSmokeSampleTimesOverride;
        private EditorSmokeSettingsSnapshot _editorSmokeSettingsSnapshot;
        private bool _editorSmokeSettingsSnapshotActive;
        internal EditorDiagnosticSmokeSegment _editorSmokeSegment;
        private string _editorSmokeCurrentFbxFileName;
        internal bool _editorSmokeCaptureResolutionOverrideActive;
        internal int _editorSmokeCaptureWidth;
        internal int _editorSmokeCaptureHeight;
        internal float _editorSmokeDiagnosticScreenshotPaddingOverride = float.NaN;
        internal float _editorSmokeDiagnosticScreenshotVerticalViewportCenterOverride = float.NaN;
        internal bool _editorSmokeUseKnownMmdReferenceTiming;
        internal float _editorSmokeRecordingStartTimeOverrideSeconds = float.NaN;
        internal float _editorSmokeRecordingPlaybackSpeedOverride = float.NaN;
        private Coroutine _editorDiagnosticBatchAdvanceCoroutine;
#endif
        #endregion

#if UNITY_EDITOR
        public enum EditorDiagnosticSmokeSegment
        {
            Head,
            Middle,
            Tail
        }

        public event Action<string, VmdSaveResult> EditorDiagnosticSmokeFinished;
#endif

        public bool IsProcessing => _isProcessing;

        public float EffectiveThumbProximalMaxLocalAngle
        {
            get { return Mathf.Min(ThumbProximalMaxLocalAngle, THUMB_PROXIMAL_SAFE_MAX_LOCAL_ANGLE); }
        }

        public bool EffectiveThumbLocalRotationGuard
        {
            get
            {
                return enableThumbLocalRotationGuard &&
                    (!ShouldSuppressFinalThumbGuardsWithManualReference ||
                     !disableThumbLocalRotationGuardWithManualFingerReference);
            }
        }

        public bool PreserveManualThumbPoseWithReference
        {
            get
            {
                return _shouldUseManualAnimatorFingerPoseReference &&
                    useManualAnimatorThumbLocalRotationReference &&
                    preserveManualFingerReferenceThumbMuscles &&
                    ShouldSuppressFinalThumbGuardsWithManualReference;
            }
        }

        private bool ShouldSuppressFinalThumbGuardsWithManualReference
        {
            get
            {
                if (!_shouldUseManualAnimatorFingerPoseReference)
                {
                    return false;
                }

                bool helperSyncActive = syncDetachedThumbBaseHelpers &&
                                        detachedThumbBaseHelperSyncWeight > 0f;
                bool palmStabilizeActive = stabilizeDetachedThumbBasePalm &&
                                           detachedThumbBasePalmStabilizeWeight > 0f;
                bool webbingStabilizeActive = stabilizeThumbWebbingCrease &&
                                              thumbWebbingCreaseStabilizeWeight > 0f;
                bool visualLengthGuardActive = enableThumbVisualLengthGuard &&
                                               (ThumbProjectionGuardWeight > 0f ||
                                                ThumbIndexSpreadGuardWeight > 0f ||
                                                ThumbSegmentStraightenWeight > 0f);

                return !(helperSyncActive ||
                         palmStabilizeActive ||
                         webbingStabilizeActive ||
                         visualLengthGuardActive);
            }
        }

        public bool ShouldSaveToImportFolder => _shouldSaveToImportFolder;
        public bool ShouldRecordVmdAfterImport => _shouldRecordVmdAfterImport;
        public bool ShouldUseLegacyPoseSpaceFacingCorrection => _shouldUseLegacyPoseSpaceFacingCorrection;
        public bool ShouldPreserveFbxRootRotation => _shouldPreserveFbxRootRotation;
        public bool ShouldPreserveRetargetBodyPosition => _shouldPreserveRetargetBodyPosition;
        public bool ShouldUseRetargetBodyPositionXZRootMotion { get => _shouldUseRetargetBodyPositionXZRootMotion; set => _shouldUseRetargetBodyPositionXZRootMotion = value; }
        public bool ShouldStabilizeGroundedFootXZ => _shouldStabilizeGroundedFootXZ;
        public bool ShouldUseEditorHumanoidClipMuscleReference => _shouldUseEditorHumanoidClipMuscleReference;
        public bool ShouldUseEditorHumanoidRootTranslationReference => _shouldUseEditorHumanoidRootTranslationReference;
        public bool ShouldUseManualAnimatorFingerPoseReference { get => _shouldUseManualAnimatorFingerPoseReference; set => _shouldUseManualAnimatorFingerPoseReference = value; }
        public bool ShouldUseManualAnimatorFullBodyPoseReference { get => _shouldUseManualAnimatorFullBodyPoseReference; set => _shouldUseManualAnimatorFullBodyPoseReference = value; }
        public bool ShouldExcludeManualAnimatorFullBodyLowerMuscles { get => _shouldExcludeManualAnimatorFullBodyLowerMuscles; set => _shouldExcludeManualAnimatorFullBodyLowerMuscles = value; }
        public bool ShouldApplyManualAnimatorFullBodyLowerMusclesOnly { get => _shouldApplyManualAnimatorFullBodyLowerMusclesOnly; set => _shouldApplyManualAnimatorFullBodyLowerMusclesOnly = value; }
        public bool ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly { get => _shouldApplyManualAnimatorFullBodyLegTwistMusclesOnly; set => _shouldApplyManualAnimatorFullBodyLegTwistMusclesOnly = value; }
        public bool ShouldUseSetHumanPoseRightLegTwistOutputReference { get => _shouldUseSetHumanPoseRightLegTwistOutputReference; set => _shouldUseSetHumanPoseRightLegTwistOutputReference = value; }
        public bool ShouldUseManualAnimatorBodyRotationReference { get => _shouldUseManualAnimatorBodyRotationReference; set => _shouldUseManualAnimatorBodyRotationReference = value; }
        public bool ShouldUseManualAnimatorBodyPositionYReference => _shouldUseManualAnimatorBodyPositionYReference;
        public bool ShouldUseManualAnimatorBodyPositionXzReference { get => _shouldUseManualAnimatorBodyPositionXzReference; set => _shouldUseManualAnimatorBodyPositionXzReference = value; }
        public bool ShouldUseManualAnimatorHipsLocalPositionReference { get => _shouldUseManualAnimatorHipsLocalPositionReference; set => _shouldUseManualAnimatorHipsLocalPositionReference = value; }
        public bool ShouldUseManualAnimatorFootHeightGroundingReference { get => _shouldUseManualAnimatorFootHeightGroundingReference; set => _shouldUseManualAnimatorFootHeightGroundingReference = value; }
        public bool ShouldUseManualAnimatorFootLocalRotationReference { get => _shouldUseManualAnimatorFootLocalRotationReference; set => _shouldUseManualAnimatorFootLocalRotationReference = value; }
        public bool ShouldUseManualAnimatorLowerBodySegmentDirectionReference { get => _shouldUseManualAnimatorLowerBodySegmentDirectionReference; set => _shouldUseManualAnimatorLowerBodySegmentDirectionReference = value; }
        public bool ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference { get => _shouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference; set => _shouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference = value; }
        public bool ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference { get => _shouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference; set => _shouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference = value; }
        public bool ShouldDisableManualAnimatorFootToToesSegmentDirectionReference { get => _shouldDisableManualAnimatorFootToToesSegmentDirectionReference; set => _shouldDisableManualAnimatorFootToToesSegmentDirectionReference = value; }
        public bool ShouldUseManualAnimatorFootHipsAlignedResidualYawReference { get => _shouldUseManualAnimatorFootHipsAlignedResidualYawReference; set => _shouldUseManualAnimatorFootHipsAlignedResidualYawReference = value; }
        public bool ShouldUseLeftSideForPostSetHumanPoseEndpointPosition { get => _shouldUseLeftSideForPostSetHumanPoseEndpointPosition; set => _shouldUseLeftSideForPostSetHumanPoseEndpointPosition = value; }
        public bool ShouldUseLeftSideForPreSetHumanPoseEndpointPosition { get => _shouldUseLeftSideForPreSetHumanPoseEndpointPosition; set => _shouldUseLeftSideForPreSetHumanPoseEndpointPosition = value; }
        public bool ShouldInvertPreSetHumanPoseEndpointPositionBodyX { get => _shouldInvertPreSetHumanPoseEndpointPositionBodyX; set => _shouldInvertPreSetHumanPoseEndpointPositionBodyX = value; }
        public bool ShouldInvertPreSetHumanPoseEndpointPositionBodyZ { get => _shouldInvertPreSetHumanPoseEndpointPositionBodyZ; set => _shouldInvertPreSetHumanPoseEndpointPositionBodyZ = value; }
        public bool ShouldLockTargetHumanoidBonePositions { get => _shouldLockTargetHumanoidBonePositions; set => _shouldLockTargetHumanoidBonePositions = value; }
        public bool ShouldClampRetargetMusclesToHumanRange => clampRetargetMusclesToHumanRange;
        public bool ShouldEnableAnatomicalArmGuard => enableAnatomicalArmGuard;
        public bool ShouldEnableThumbAnatomicalGuard => enableThumbAnatomicalGuard;
        public bool ShouldClampRetargetRootDeltaSpikes => clampRetargetRootDeltaSpikes;
        public bool ShouldFreezeRootYAfterInitialGrounding => FreezeRootYAfterInitialGrounding;
        public bool ShouldEnableThumbLocalRotationGuard => enableThumbLocalRotationGuard;
        public bool ShouldEnableThumbVisualLengthGuard => enableThumbVisualLengthGuard;

                public float EffectiveThumbStretchOffset
        {
            get
            {
                if (useDefaultThumbStretchOffsetWhenUnset && Mathf.Approximately(ThumbStretchOffset, 0f))
                {
                    return DEFAULT_THUMB_STRETCH_OFFSET;
                }

                return ThumbStretchOffset;
            }
        }

        public float EffectiveThumbProjectionMinPalmNormal
        {
            get
            {
                if (Mathf.Approximately(ThumbProjectionMinPalmNormal, LEGACY_THUMB_PROJECTION_MIN_PALM_NORMAL))
                {
                    return DEFAULT_THUMB_PROJECTION_MIN_PALM_NORMAL;
                }

                return Mathf.Clamp01(ThumbProjectionMinPalmNormal);
            }
        }

        public RecordingCaptureResolutionPlan CreateRecordingCaptureResolutionPlan()
        {
            if (recordingCaptureQuality == RecordingCaptureQualityPreset.Custom)
            {
                return RecordingCaptureResolution.CreateCustomPlan(
                    customRecordingCaptureWidth,
                    customRecordingCaptureHeight);
            }

            return RecordingCaptureResolution.CreatePlan(recordingCaptureQuality);
        }

#if UNITY_EDITOR
        public bool StartEditorDiagnosticSmoke(
            string fbxFileName,
            float durationSeconds,
            int targetFrameCount,
            bool enableDiagnostics,
            bool enableFingerCloseups,
            bool useDeterministicCaptureFramerate,
            float diagnosticStartDelay,
            EditorDiagnosticSmokeSegment segment = EditorDiagnosticSmokeSegment.Head,
            float[] sampleTimesOverride = null,
            int captureWidthOverride = 0,
            int captureHeightOverride = 0,
            float diagnosticScreenshotPaddingOverride = float.NaN,
            float diagnosticScreenshotVerticalViewportCenterOverride = float.NaN,
            float recordingStartTimeOverrideSeconds = float.NaN,
            float recordingPlaybackSpeedOverride = float.NaN)
        {
            if (_isProcessing)
            {
                Debug.LogWarning("[FBXImport] 다른 FBX 처리가 진행 중이라 smoke 진단을 시작하지 않았습니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(fbxFileName))
            {
                Debug.LogError("[FBXImport] smoke 진단 FBX 파일명이 비어 있습니다.");
                return false;
            }

            string sourcePath = ResolveEditorSmokeFbxPath(fbxFileName);
            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"[FBXImport] smoke 진단 FBX를 찾을 수 없습니다: {sourcePath}");
                return false;
            }

            float safeDuration = Mathf.Max(0.1f, durationSeconds);
            int safeTargetFrameCount = targetFrameCount > 0
                ? targetFrameCount
                : Mathf.CeilToInt(safeDuration * EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE);

            CaptureEditorSmokeSettings();
            enableRecordingDiagnostics = enableDiagnostics;
            enableDiagnosticFingerCloseups = enableFingerCloseups;
            useDeterministicCaptureFramerateForDiagnostics = useDeterministicCaptureFramerate;
            startDelay = Mathf.Clamp(diagnosticStartDelay, 0f, 10f);

            _editorSmokeRecordingOverrideActive = true;
            _editorSmokeDurationSeconds = safeDuration;
            _editorSmokeTargetFrameCount = safeTargetFrameCount;
            _editorSmokeSampleTimesOverride = CloneEditorSmokeSampleTimes(sampleTimesOverride);
            _editorSmokeSegment = segment;
            _editorSmokeCurrentFbxFileName = Path.GetFileName(sourcePath);
            _editorSmokeCaptureResolutionOverrideActive = TryBuildEditorSmokeCaptureResolutionOverride(
                captureWidthOverride,
                captureHeightOverride,
                out _editorSmokeCaptureWidth,
                out _editorSmokeCaptureHeight);
            _editorSmokeDiagnosticScreenshotPaddingOverride =
                NormalizeEditorSmokeDiagnosticScreenshotPaddingOverride(diagnosticScreenshotPaddingOverride);
            _editorSmokeDiagnosticScreenshotVerticalViewportCenterOverride =
                NormalizeEditorSmokeDiagnosticScreenshotVerticalViewportCenterOverride(
                    diagnosticScreenshotVerticalViewportCenterOverride);
            _editorSmokeUseKnownMmdReferenceTiming = ShouldUseKnownMmdReferenceTimingForEditorSmoke(
                Path.GetFileNameWithoutExtension(sourcePath),
                safeDuration,
                safeTargetFrameCount,
                EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE,
                useKnownMmdReferenceTiming);
            _editorSmokeRecordingStartTimeOverrideSeconds =
                NormalizeEditorSmokeStartTimeOverride(recordingStartTimeOverrideSeconds);
            _editorSmokeRecordingPlaybackSpeedOverride =
                NormalizeEditorSmokePlaybackSpeedOverride(recordingPlaybackSpeedOverride);

            Debug.Log(
                $"[FBXImport] Editor smoke 진단 시작: FBX={Path.GetFileName(sourcePath)}, " +
                $"duration={safeDuration:F2}s, targetFrameCount={safeTargetFrameCount}, " +
                $"segment={GetEditorSmokeSegmentLabel(segment)}, diagnostics={enableDiagnostics}");
            LogEditorSmokeThumbState("smoke-start-before-process");
            if (_editorSmokeCaptureResolutionOverrideActive)
            {
                Debug.Log($"[FBXImport] Editor smoke capture override: {_editorSmokeCaptureWidth}x{_editorSmokeCaptureHeight}");
            }

            ProcessFBXAsync(sourcePath);
            return true;
        }

        internal static bool TryBuildEditorSmokeCaptureResolutionOverride(
            int requestedWidth,
            int requestedHeight,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;
            if (requestedWidth <= 0 || requestedHeight <= 0)
            {
                return false;
            }

            RecordingCaptureResolutionPlan plan = RecordingCaptureResolution.CreateCustomPlan(
                requestedWidth,
                requestedHeight);
            width = plan.Width;
            height = plan.Height;
            return true;
        }

        private string ResolveEditorSmokeFbxPath(string fbxFileName)
        {
            return ResolveEditorSmokeFbxPath(
                fbxFileName,
                GetControlledImportDirectory(),
                Application.dataPath,
                File.Exists);
        }

        private static string ResolveEditorSmokeFbxPath(
            string fbxFileName,
            string controlledImportDirectory,
            string dataPath,
            Func<string, bool> fileExists)
        {
            string normalizedFileName = Path.GetFileName(fbxFileName.Trim().Replace("\\", "/"));
            if (!string.Equals(Path.GetExtension(normalizedFileName), ".fbx", StringComparison.OrdinalIgnoreCase))
            {
                normalizedFileName += ".fbx";
            }

            string controlledPath = Path.Combine(controlledImportDirectory, normalizedFileName);
            if (fileExists(controlledPath))
            {
                return controlledPath;
            }

            string projectFallbackPath = Path.Combine(dataPath, "_Project", "FBX", normalizedFileName);
            if (fileExists(projectFallbackPath))
            {
                return projectFallbackPath;
            }

            return controlledPath;
        }

        internal static string BuildEditorSmokeOutputBaseName(string outputBaseName, float durationSeconds, EditorDiagnosticSmokeSegment segment)
        {
            string cleanBaseName = SanitizeFileName(
                string.IsNullOrWhiteSpace(outputBaseName) ? VMDOutputNamePolicy.DefaultOutputBaseName : outputBaseName);
            int roundedSeconds = Mathf.Max(1, Mathf.CeilToInt(durationSeconds));
            string prefix;
            switch (segment)
            {
                case EditorDiagnosticSmokeSegment.Middle:
                    prefix = "smoke_middle";
                    break;
                case EditorDiagnosticSmokeSegment.Tail:
                    prefix = "smoke_tail";
                    break;
                default:
                    prefix = "smoke";
                    break;
            }

            return $"{prefix}_{cleanBaseName}_{roundedSeconds}s";
        }

        internal static float CalculateEditorSmokeStartTime(AnimationClip clip, float requestedDuration, EditorDiagnosticSmokeSegment segment)
        {
            if (clip == null)
            {
                return 0f;
            }

            float clipLength = Mathf.Max(0f, clip.length);
            float safeDuration = Mathf.Max(0.1f, requestedDuration);
            switch (segment)
            {
                case EditorDiagnosticSmokeSegment.Middle:
                    return Mathf.Max(0f, (clipLength - safeDuration) * 0.5f);
                case EditorDiagnosticSmokeSegment.Tail:
                    return Mathf.Max(0f, clipLength - safeDuration);
                default:
                    return 0f;
            }
        }

        internal static string GetEditorSmokeSegmentLabel(EditorDiagnosticSmokeSegment segment)
        {
            switch (segment)
            {
                case EditorDiagnosticSmokeSegment.Middle:
                    return "middle";
                case EditorDiagnosticSmokeSegment.Tail:
                    return "tail";
                default:
                    return "head";
            }
        }

        private static float[] CloneEditorSmokeSampleTimes(float[] sampleTimesOverride)
        {
            return sampleTimesOverride != null && sampleTimesOverride.Length > 0
                ? (float[])sampleTimesOverride.Clone()
                : null;
        }

        private void CaptureEditorSmokeSettings()
        {
            _editorSmokeSettingsSnapshot = new EditorSmokeSettingsSnapshot
            {
                enableRecordingDiagnostics = enableRecordingDiagnostics,
                enableDiagnosticFingerCloseups = enableDiagnosticFingerCloseups,
                useDeterministicCaptureFramerateForDiagnostics = useDeterministicCaptureFramerateForDiagnostics,
                startDelay = startDelay
            };
            _editorSmokeSettingsSnapshotActive = true;
        }

        private void ClearEditorSmokeOverride()
        {
            if (_editorSmokeSettingsSnapshotActive)
            {
                enableRecordingDiagnostics = _editorSmokeSettingsSnapshot.enableRecordingDiagnostics;
                enableDiagnosticFingerCloseups = _editorSmokeSettingsSnapshot.enableDiagnosticFingerCloseups;
                useDeterministicCaptureFramerateForDiagnostics = _editorSmokeSettingsSnapshot.useDeterministicCaptureFramerateForDiagnostics;
                startDelay = _editorSmokeSettingsSnapshot.startDelay;
            }

            _editorSmokeRecordingOverrideActive = false;
            _editorSmokeTargetFrameCount = 0;
            _editorSmokeDurationSeconds = 0f;
            _editorSmokeSampleTimesOverride = null;
            _editorSmokeSegment = EditorDiagnosticSmokeSegment.Head;
            _editorSmokeCurrentFbxFileName = null;
            _editorSmokeCaptureResolutionOverrideActive = false;
            _editorSmokeCaptureWidth = 0;
            _editorSmokeCaptureHeight = 0;
            _editorSmokeDiagnosticScreenshotPaddingOverride = float.NaN;
            _editorSmokeDiagnosticScreenshotVerticalViewportCenterOverride = float.NaN;
            _editorSmokeUseKnownMmdReferenceTiming = false;
            _editorSmokeRecordingStartTimeOverrideSeconds = float.NaN;
            _editorSmokeRecordingPlaybackSpeedOverride = float.NaN;
            _editorSmokeSettingsSnapshot = default(EditorSmokeSettingsSnapshot);
            _editorSmokeSettingsSnapshotActive = false;
        }

        private static float NormalizeEditorSmokeStartTimeOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return float.NaN;
            }

            return value;
        }

        private static float NormalizeEditorSmokePlaybackSpeedOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return float.NaN;
            }

            return Mathf.Max(0.0001f, value);
        }

        private static float NormalizeEditorSmokeDiagnosticScreenshotPaddingOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return float.NaN;
            }

            return Mathf.Clamp(value, 0.25f, 2f);
        }

        private static float NormalizeEditorSmokeDiagnosticScreenshotVerticalViewportCenterOverride(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return float.NaN;
            }

            return Mathf.Clamp01(value);
        }

        public void ScheduleEditorDiagnosticBatchAdvance(Action continuation)
        {
            if (_editorDiagnosticBatchAdvanceCoroutine != null)
            {
                StopCoroutine(_editorDiagnosticBatchAdvanceCoroutine);
                _editorDiagnosticBatchAdvanceCoroutine = null;
            }

            if (!Application.isPlaying || !isActiveAndEnabled)
            {
                ResetTargetStateAfterSession(recaptureGuardBaselines: false);
                continuation?.Invoke();
                return;
            }

            _editorDiagnosticBatchAdvanceCoroutine = StartCoroutine(EditorDiagnosticBatchAdvanceRoutine(continuation));
        }

        private IEnumerator EditorDiagnosticBatchAdvanceRoutine(Action continuation)
        {
            Debug.Log("[FBXImport] Editor smoke batch advance reset: target idle 상태를 다음 FBX 시작 전 다시 고정합니다.");
            LogEditorSmokeThumbState("batch-advance-before-reset");
            ResetTargetStateAfterSession(recaptureGuardBaselines: false);
            yield return null;
            ResetTargetStateAfterSession(recaptureGuardBaselines: false);
            yield return null;
            ResetTargetStateAfterSession(recaptureGuardBaselines: false);
            LogEditorSmokeThumbState("batch-advance-after-reset");
            _editorDiagnosticBatchAdvanceCoroutine = null;
            continuation?.Invoke();
        }

        private void NotifyEditorSmokeFinished(VmdSaveResult result)
        {
            if (string.IsNullOrEmpty(_editorSmokeCurrentFbxFileName))
            {
                return;
            }

            EditorDiagnosticSmokeFinished?.Invoke(_editorSmokeCurrentFbxFileName, result);
        }
#endif

        internal static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            return TryBuildKnownMmdReferenceRecordingPlan(
                outputBaseName,
                clipLengthSeconds,
                recordingFrameRate,
                useKnownReferenceTiming: true,
                out recordingLengthSeconds,
                out targetFrameCount,
                out playbackSpeed);
        }

        internal static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            bool useKnownReferenceTiming,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            recordingLengthSeconds = clipLengthSeconds;
            targetFrameCount = 0;
            playbackSpeed = 1f;

            if (!useKnownReferenceTiming)
            {
                return false;
            }

            if (recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate) ||
                clipLengthSeconds <= 0f ||
                float.IsNaN(clipLengthSeconds) ||
                float.IsInfinity(clipLengthSeconds))
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(outputBaseName ?? string.Empty);
            if (!string.Equals(cleanBaseName, VMDOutputNamePolicy.SatisfactionReferenceBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int referenceFrameCount = VMDOutputNamePolicy.SatisfactionReferenceMaxMmdFrame + 1;
            float referenceDurationSeconds = referenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;

            if (clipLengthSeconds + frameToleranceSeconds < referenceDurationSeconds)
            {
                return false;
            }

            recordingLengthSeconds = referenceDurationSeconds;
            targetFrameCount = referenceFrameCount;
            playbackSpeed = Mathf.Max(0.0001f, clipLengthSeconds / referenceDurationSeconds);
            return true;
        }

        internal static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            return TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                outputBaseName,
                clipLengthSeconds,
                requestedDurationSeconds,
                requestedTargetFrameCount,
                recordingFrameRate,
                useKnownReferenceTiming: true,
                out recordingLengthSeconds,
                out targetFrameCount,
                out playbackSpeed);
        }

        internal static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            bool useKnownReferenceTiming,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            recordingLengthSeconds = requestedDurationSeconds;
            targetFrameCount = requestedTargetFrameCount;
            playbackSpeed = 1f;

            if (!useKnownReferenceTiming)
            {
                return false;
            }

            if (requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                requestedTargetFrameCount <= 0 ||
                recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate))
            {
                return false;
            }

            if (!TryBuildKnownMmdReferenceRecordingPlan(
                outputBaseName,
                clipLengthSeconds,
                recordingFrameRate,
                out float referenceRecordingLengthSeconds,
                out int referenceTargetFrameCount,
                out float referencePlaybackSpeed))
            {
                return false;
            }

            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            bool coversFullReferenceDuration =
                requestedDurationSeconds + frameToleranceSeconds >= referenceRecordingLengthSeconds;
            bool coversFullReferenceFrames = requestedTargetFrameCount >= referenceTargetFrameCount;

            if (!coversFullReferenceDuration || !coversFullReferenceFrames)
            {
                return false;
            }

            recordingLengthSeconds = referenceRecordingLengthSeconds;
            targetFrameCount = referenceTargetFrameCount;
            playbackSpeed = referencePlaybackSpeed;
            return true;
        }

        private static bool ShouldUseKnownMmdReferenceTimingForEditorSmoke(
            string outputBaseName,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            bool sceneUseKnownReferenceTiming)
        {
            if (sceneUseKnownReferenceTiming)
            {
                return true;
            }

            if (requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                requestedTargetFrameCount <= 0 ||
                recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate))
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(outputBaseName ?? string.Empty);
            if (!string.Equals(cleanBaseName, VMDOutputNamePolicy.SatisfactionReferenceBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int referenceFrameCount = VMDOutputNamePolicy.SatisfactionReferenceMaxMmdFrame + 1;
            float referenceDurationSeconds = referenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            bool coversFullReferenceDuration =
                requestedDurationSeconds + frameToleranceSeconds >= referenceDurationSeconds;
            bool coversFullReferenceFrames = requestedTargetFrameCount >= referenceFrameCount;

            return coversFullReferenceDuration && coversFullReferenceFrames;
        }

        internal static float ResolveVmdRecordingPlaybackSpeed(float configuredPlaybackSpeed)
        {
            if (configuredPlaybackSpeed <= 0f ||
                float.IsNaN(configuredPlaybackSpeed) ||
                float.IsInfinity(configuredPlaybackSpeed))
            {
                return 1f;
            }

            return Mathf.Max(0.0001f, configuredPlaybackSpeed);
        }

        internal static float ResolveRecordingLengthForPlaybackSpeed(float clipLengthSeconds, float playbackSpeed)
        {
            if (clipLengthSeconds <= 0f ||
                float.IsNaN(clipLengthSeconds) ||
                float.IsInfinity(clipLengthSeconds))
            {
                return 0f;
            }

            return clipLengthSeconds / ResolveVmdRecordingPlaybackSpeed(playbackSpeed);
        }

        private struct BooleanFieldSnapshot
        {
            public object Target;
            public FieldInfo Field;
            public bool Value;

            public BooleanFieldSnapshot(object target, FieldInfo field, bool value)
            {
                Target = target;
                Field = field;
                Value = value;
            }
        }

        #region Unity 생명주기
        private void Awake()
        {
            EnsureServicesInitialized();
            _idlePoseGuard = GetComponent<TargetIdlePoseGuard>();
            if (_idlePoseGuard != null)
            {
                _idlePoseGuard.SetTargetCharacter(targetCharacter);
                _idlePoseGuard.Initialize();
            }
        }

        private void LateUpdate()
        {
            _idlePoseGuard?.TryApply(_isProcessing, _activeRetargeter != null);
        }

        private void OnDestroy()
        {
            RestoreMmdPostPoseCorrectionForRetarget();
            _idlePoseGuard?.RestoreAnimatorController();
        }
        #endregion

        #region 초기화
        private void InitializeServices()
        {
            if (_fileBrowserService == null)
            {
                _fileBrowserService = fileBrowserServiceFactory?.Invoke();
            }

            if (_fbxImporter == null)
            {
                _fbxImporter = fbxImporterFactory?.Invoke();
            }

            if (_fileBrowserService == null || _fbxImporter == null)
            {
                throw new InvalidOperationException("FBX 임포트 의존성 초기화에 실패했습니다.");
            }

            if (_importController == null)
            {
                _importController = new FBXImportController(this, _fileBrowserService);
            }

            if (_conversionCoordinator == null)
            {
                _conversionCoordinator = new FBXConversionCoordinator(this);
            }
        }

        internal void EnsureServicesInitialized()
        {
            if (_fileBrowserService == null ||
                _fbxImporter == null ||
                _importController == null ||
                _conversionCoordinator == null)
            {
                InitializeServices();
            }
        }
        #endregion


        #region 이벤트 핸들러
        public void OnClickImportButton()
        {
            EnsureServicesInitialized();
            _importController.ImportFromDialog();
        }

        /// <summary>
        /// Import_FBX 폴더에 있는 FBX 파일 목록 로드
        /// 에디터와 빌드 환경 모두에서 작동
        /// </summary>
        public void OnClickLoadFromImportFolder()
        {
            EnsureServicesInitialized();
            _importController.LoadFromImportFolder();
        }

        public bool TryStartFbxImportFromSharedSettings(string sourcePath)
        {
            EnsureServicesInitialized();
            return _importController.TryImportFromSharedSettings(sourcePath);
        }



        #endregion

        #region 파일 처리 로직
        internal async void ProcessFBXAsync(string sourcePath)
        {
            if (_isProcessing)
            {
                SetSessionState(FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return;
            }

            await ProcessFBXSessionAsync(sourcePath);
        }

        /// <summary>
        /// 지연 후 애니메이션 재생 및 VMD 녹화를 동기화하는 코루틴
        /// </summary>
        /// <param name="ghostModel">임포트된 Ghost 모델</param>
        /// <param name="ghostAnim">Ghost의 Animation 컴포넌트</param>
        /// <param name="targetCharacter">리타겟 대상 캐릭터</param>
        /// <param name="clip">재생할 AnimationClip</param>
        /// <param name="retargeter">Pose Space Retargeter 컴포넌트</param>
        internal async Task<FBXConversionResult> ProcessFBXSessionAsync(string sourcePath)
        {
            EnsureServicesInitialized();
            _idlePoseGuard?.Apply();
            _isProcessing = true;
            ClearActiveRecordingSubscription();
            CleanupActiveGhost();

            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    FailSession($"FBX 파일을 찾을 수 없습니다: {sourcePath}");
                    return FBXConversionResult.Fail($"FBX 파일을 찾을 수 없습니다: {sourcePath}");
                }

                if (!string.Equals(Path.GetExtension(sourcePath), ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    FailSession("FBX 파일만 선택할 수 있습니다.");
                    return FBXConversionResult.Fail("FBX 파일만 선택할 수 있습니다.");
                }

                SetSessionState(FBXSessionState.Selected, $"선택됨: {Path.GetFileName(sourcePath)}", 0.05f);
                string targetPath = CopyToControlledImportFolder(sourcePath);
                string outputBaseName = Path.GetFileNameWithoutExtension(targetPath);
                if (_shouldRecordVmdAfterImport)
                {
                    Debug.Log($"[Recording] 자동 VMD 출력명 고정됨. VMD={outputBaseName}.vmd, 입력 FBX={Path.GetFileName(sourcePath)}");
                }
                else
                {
                    Debug.Log($"[FBXImport] Unity 촬영 전용 모드 선택됨. 출력={outputBaseName}, VMD 자동 녹화=생략");
                }
                SetSessionState(FBXSessionState.Copied, $"복제 완료: {Path.GetFileName(targetPath)}", 0.15f);

#if UNITY_EDITOR
                if (ShouldConfigureEditorImportSettings(sourcePath, targetPath, Application.dataPath))
                {
                    ConfigureImportSettings(targetPath);
                }
                else
                {
                    Debug.Log($"[FBXImport] 제어된 Import_FBX 가져오기 설정 유지됨. 경로={targetPath}");
                }
#endif

                SetSessionState(FBXSessionState.LoadingFbx, "FBX 로드 중", 0.25f);
                GameObject importedModel = await _fbxImporter.ImportAsync(targetPath);
                if (importedModel == null)
                {
                    FailSession("FBX 로드에 실패했습니다.");
                    return FBXConversionResult.Fail("FBX 로드에 실패했습니다.");
                }

                GameObject ghostContainer = CreateGhostContainer(importedModel);
                _activeGhostContainer = ghostContainer;
                SetGhostVisibility(importedModel, showGhostModel, showGhostSkeletonWhenNoRenderers);

                Dictionary<string, string> boneMapping = LoadBoneMappingRuntime();
                if (boneMapping == null)
                {
                    boneMapping = new Dictionary<string, string>();
                }

                // BoneMapping_Data.txt는 특정 리그에 종속될 수 있으므로, 실패 시 자동 매핑으로 폴백합니다.
                HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);
                if (!ValidateGhostAvatar(importedModel))
                {
                    Debug.LogWarning("[FBXImport] Ghost Humanoid Avatar 생성 실패함. 자동 본 매핑으로 재시도함.");
                    boneMapping = HumanoidAvatarBuilder.BuildAutoMapping(importedModel);
                    HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);

                    if (!ValidateGhostAvatar(importedModel))
                    {
                        FailSession("Ghost Humanoid Avatar 생성에 실패했습니다.");
                        return FBXConversionResult.Fail("Ghost Humanoid Avatar 생성에 실패했습니다.");
                    }
                }

                SetSessionState(FBXSessionState.AvatarReady, "Humanoid Avatar 준비 완료", 0.45f);

                Animation ghostAnim = importedModel.GetComponent<Animation>();
                AnimationClip targetClip = ExtractPrimaryClip(ghostAnim);
                if (targetClip == null)
                {
                    FailSession("FBX에서 유효한 애니메이션 클립을 찾지 못했습니다.");
                    return FBXConversionResult.Fail("FBX에서 유효한 애니메이션 클립을 찾지 못했습니다.");
                }

                GameObject targetObject = targetCharacter;
                if (!FBXConversionCoordinator.TryResolveTargetAnimator(
                        targetObject,
                        out Animator targetAnimator,
                        out string targetErrorMessage))
                {
                    FailSession(targetErrorMessage);
                    return FBXConversionResult.Fail(targetErrorMessage);
                }

                Animator ghostAnimator = importedModel.GetComponent<Animator>();
                PrepareTargetCharacter(targetObject, targetAnimator, ghostAnimator);

                PoseSpaceRetargeter retargeter = importedModel.AddComponent<PoseSpaceRetargeter>();
                _activeRetargeter = retargeter;
                RetargetingContext context = new RetargetingContext(importedModel, targetObject, boneMapping, targetClip);
                RetargetingSettings settings = RetargetingSettings.CreateSnapshot(this);
                retargeter.Initialize(context, settings);
                ConfigureTargetThumbDeformationGuard(targetObject, targetAnimator, retargeter);
#if UNITY_EDITOR
                ConfigureEditorHumanoidMuscleReference(retargeter, targetPath, sourcePath);
#endif
                SetSessionState(FBXSessionState.GhostReady, "Ghost Retarget 준비 완료", 0.6f);

                if (ghostAnim != null)
                {
                    ghostAnim.Stop();
                }

                StartCoroutine(StartRecordingSequenceStable(
                    ghostAnim,
                    retargeter,
                    targetObject,
                    targetClip,
                    outputBaseName
                ));

                return FBXConversionResult.Succeed(outputBaseName);
            }
            catch (Exception e)
            {
                string errorMessage = $"FBX 처리 실패: {e.Message}";
                FailSession(errorMessage, e);
                return FBXConversionResult.Fail(errorMessage);
            }
        }

        private IEnumerator StartRecordingSequenceStable(
            Animation ghostAnim,
            PoseSpaceRetargeter retargeter,
            GameObject targetObject,
            AnimationClip clip,
            string outputBaseName)
        {
#if UNITY_EDITOR
            bool earlyEditorSmokeRecordingOverrideActive = _editorSmokeRecordingOverrideActive;
#else
            bool earlyEditorSmokeRecordingOverrideActive = false;
#endif
            bool earlyShouldStartVmdRecording = ShouldStartVmdRecordingAfterImport(
                _shouldRecordVmdAfterImport,
                earlyEditorSmokeRecordingOverrideActive);
            float resolvedStartDelay = ResolveStartDelayForRecordingMode(startDelay, earlyShouldStartVmdRecording);
            SetSessionState(FBXSessionState.Retargeting, $"녹화 시작 전 {resolvedStartDelay:F1}초 대기", 0.7f);
            if (resolvedStartDelay > 0f)
            {
                yield return new WaitForSeconds(resolvedStartDelay);
            }

            if (ghostAnim == null || clip == null)
            {
                FailSession("녹화 시작에 필요한 Ghost 애니메이션이 없습니다.");
                yield break;
            }

            float recordingStartTime = 0f;
            float recordingPlaybackSpeed = ResolveVmdRecordingPlaybackSpeed(vmdRecordingPlaybackSpeed);
            float recordingLength = ResolveRecordingLengthForPlaybackSpeed(clip.length, recordingPlaybackSpeed);
            int recordingTargetFrameCount = 0;
            string recordingOutputBaseName = outputBaseName;
            string comparisonLabel = $"auto_{recordingOutputBaseName}";

#if UNITY_EDITOR
            bool editorSmokeRecordingOverrideActive = _editorSmokeRecordingOverrideActive;
            float[] diagnosticSampleTimesOverride = _editorSmokeSampleTimesOverride;
#else
            bool editorSmokeRecordingOverrideActive = false;
            float[] diagnosticSampleTimesOverride = null;
#endif
            bool shouldStartVmdRecording = ShouldStartVmdRecordingAfterImport(
                _shouldRecordVmdAfterImport,
                editorSmokeRecordingOverrideActive);

            HumanoidSampleCode recorderController = null;
            if (shouldStartVmdRecording)
            {
                recorderController = targetObject.GetComponent<HumanoidSampleCode>();
                if (recorderController == null)
                {
                    FailSession("Target Character에 HumanoidSampleCode가 없습니다.");
                    yield break;
                }

                _activeRecorderController = recorderController;
                _activeRecorderController.RecordingFinished += OnRecordingFinished;
                RecordingCaptureResolutionPlan recordingCapturePlan;
                float diagnosticScreenshotPadding = 1.8f;
                float diagnosticScreenshotVerticalViewportCenter = 0.28f;
#if UNITY_EDITOR
                recordingCapturePlan = _editorSmokeCaptureResolutionOverrideActive
                    ? RecordingCaptureResolution.CreateCustomPlan(_editorSmokeCaptureWidth, _editorSmokeCaptureHeight)
                    : CreateRecordingCaptureResolutionPlan();
                diagnosticScreenshotPadding = float.IsNaN(_editorSmokeDiagnosticScreenshotPaddingOverride)
                    ? diagnosticScreenshotPadding
                    : _editorSmokeDiagnosticScreenshotPaddingOverride;
                diagnosticScreenshotVerticalViewportCenter = float.IsNaN(_editorSmokeDiagnosticScreenshotVerticalViewportCenterOverride)
                    ? diagnosticScreenshotVerticalViewportCenter
                    : _editorSmokeDiagnosticScreenshotVerticalViewportCenterOverride;
#else
                recordingCapturePlan = CreateRecordingCaptureResolutionPlan();
#endif
                _activeRecorderController.SetRecordingDiagnostics(
                    enableRecordingDiagnostics,
                    enableRecordingDiagnostics && enableDiagnosticFingerCloseups,
                    enableRecordingDiagnostics && useDeterministicCaptureFramerateForDiagnostics,
                    diagnosticSampleTimesOverride,
                    recordingCapturePlan.Width,
                    recordingCapturePlan.Height,
                    diagnosticScreenshotPadding,
                    diagnosticScreenshotVerticalViewportCenter);
#if UNITY_EDITOR
                if (_editorSmokeRecordingOverrideActive)
                {
                    float requestedDuration = Mathf.Max(0.1f, _editorSmokeDurationSeconds);
                    bool hasEditorSmokeTimingOverride =
                        !float.IsNaN(_editorSmokeRecordingStartTimeOverrideSeconds) ||
                        !float.IsNaN(_editorSmokeRecordingPlaybackSpeedOverride);
                    recordingStartTime = CalculateEditorSmokeStartTime(clip, requestedDuration, _editorSmokeSegment);
                    if (!float.IsNaN(_editorSmokeRecordingStartTimeOverrideSeconds))
                    {
                        recordingStartTime = Mathf.Clamp(
                            _editorSmokeRecordingStartTimeOverrideSeconds,
                            0f,
                            Mathf.Max(0f, clip.length));
                    }

                    if (!float.IsNaN(_editorSmokeRecordingPlaybackSpeedOverride))
                    {
                        recordingPlaybackSpeed = _editorSmokeRecordingPlaybackSpeedOverride;
                    }

                    float safePlaybackSpeed = Mathf.Max(0.0001f, recordingPlaybackSpeed);
                    float remainingLength = Mathf.Max(0.1f, (clip.length - recordingStartTime) / safePlaybackSpeed);
                    recordingLength = Mathf.Min(requestedDuration, remainingLength);
                    recordingTargetFrameCount = Mathf.Min(
                        Mathf.Max(1, _editorSmokeTargetFrameCount),
                        Mathf.CeilToInt(recordingLength * EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE));
                    recordingOutputBaseName = BuildEditorSmokeOutputBaseName(outputBaseName, recordingLength, _editorSmokeSegment);
                    comparisonLabel = $"auto_{recordingOutputBaseName}";
                    Debug.Log(
                        $"[Recording] 에디터 스모크 녹화 제한 적용됨. VMD={recordingOutputBaseName}.vmd, " +
                        $"segment={GetEditorSmokeSegmentLabel(_editorSmokeSegment)}, " +
                        $"start={recordingStartTime:F2}s, duration={recordingLength:F2}s, " +
                        $"targetFrameCount={recordingTargetFrameCount}");

                    if (!hasEditorSmokeTimingOverride &&
                        TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                        outputBaseName,
                        clip.length,
                        recordingLength,
                        recordingTargetFrameCount,
                        EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE,
                        _editorSmokeUseKnownMmdReferenceTiming,
                        out float referenceRecordingLength,
                        out int referenceTargetFrameCount,
                        out float referencePlaybackSpeed))
                    {
                        recordingLength = referenceRecordingLength;
                        recordingTargetFrameCount = referenceTargetFrameCount;
                        recordingPlaybackSpeed = referencePlaybackSpeed;

                        if (recorderController.vmdRecorder != null)
                        {
                            recorderController.vmdRecorder.UseCaptureFramerateDuringRecording = true;
                            recorderController.vmdRecorder.DropLateFrameBacklogWhenNotUsingCaptureFramerate = false;
                        }

                        Debug.Log(
                            $"[Recording] 에디터 스모크 기준 녹화 시간이 적용됨. " +
                            $"clipLength={clip.length:F3}s, recordingLength={recordingLength:F3}s, " +
                            $"targetFrameCount={recordingTargetFrameCount}, playbackSpeed={recordingPlaybackSpeed:F5}");
                    }
                }
                else
#endif
                if (TryBuildKnownMmdReferenceRecordingPlan(
                    outputBaseName,
                    clip.length,
                    MMD_REFERENCE_FRAME_RATE,
                    useKnownMmdReferenceTiming,
                    out float referenceRecordingLength,
                    out int referenceTargetFrameCount,
                    out float referencePlaybackSpeed))
                {
                    recordingLength = referenceRecordingLength;
                    recordingTargetFrameCount = referenceTargetFrameCount;
                    recordingPlaybackSpeed = referencePlaybackSpeed;

                    if (recorderController.vmdRecorder != null)
                    {
                        recorderController.vmdRecorder.UseCaptureFramerateDuringRecording = true;
                        recorderController.vmdRecorder.DropLateFrameBacklogWhenNotUsingCaptureFramerate = false;
                    }

                    Debug.Log(
                        $"[Recording] 기준 녹화 시간이 적용됨. " +
                        $"clipLength={clip.length:F3}s, recordingLength={recordingLength:F3}s, " +
                        $"targetFrameCount={recordingTargetFrameCount}, playbackSpeed={recordingPlaybackSpeed:F5}");
                }
            }

            int prewarmFrameCount = ResolveRetargetPrewarmFrameCountForRecordingMode(
                RetargetPrewarmFrameCount,
                shouldStartVmdRecording);
            int visiblePrewarmYieldFrameCount = ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode(
                RetargetPrewarmFrameCount,
                shouldStartVmdRecording);
            yield return PrewarmRetargetStartPose(
                ghostAnim,
                clip,
                retargeter,
                recordingStartTime,
                recordingPlaybackSpeed,
                prewarmFrameCount,
                visiblePrewarmYieldFrameCount);
            retargeter?.CaptureRecordingStartBaselineSnapshot();
            retargeter?.ResetPlaybackStabilityMetrics();

            if (!shouldStartVmdRecording)
            {
                SetSessionState(FBXSessionState.Success, $"FBX 임포트/촬영 준비 완료: {outputBaseName}", 1f);
                Debug.Log($"[Recording] FBX 임포트 및 Unity 촬영 준비 완료됨. 출력={outputBaseName}, VMD 자동 녹화=생략");
                _isProcessing = false;
                yield break;
            }

            SetSessionState(FBXSessionState.Recording, $"녹화 중: {recordingOutputBaseName}", 0.75f);
            Debug.Log($"[Recording] 자동 녹화 시작됨. VMD={recordingOutputBaseName}.vmd, 비교 라벨={comparisonLabel}");
            bool started = recorderController.StartAutoRecording(
                recordingLength,
                recordingOutputBaseName,
                null,
                recordingTargetFrameCount,
                comparisonLabel: comparisonLabel,
                overwriteExistingOutput: true);
            if (!started)
            {
                FailSession("VMD 녹화를 시작하지 못했습니다.");
            }
        }

        private IEnumerator PrewarmRetargetStartPose(
            Animation ghostAnim,
            AnimationClip clip,
            PoseSpaceRetargeter retargeter,
            float startTimeSeconds,
            float playbackSpeed,
            int configuredPrewarmFrameCount,
            int visiblePrewarmYieldFrameCount)
        {
            ghostAnim.clip = clip;
            if (ghostAnim.GetClip(clip.name) == null)
            {
                ghostAnim.AddClip(clip, clip.name);
            }

            ghostAnim.Play(clip.name);
            AnimationState state = ghostAnim[clip.name];
            if (state == null)
            {
                yield return new WaitForEndOfFrame();
                yield break;
            }

            float sampleTime = Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, state.length));
            float safePlaybackSpeed = Mathf.Max(0.0001f, playbackSpeed);
            int prewarmFrames = ResolveRetargetPrewarmFrameCount(configuredPrewarmFrameCount);
            int visibleYieldFrames = Mathf.Max(0, visiblePrewarmYieldFrameCount);
            if (prewarmFrames <= 0)
            {
                if (!PrepareRetargeterRecordingStartPose(retargeter, sampleTime, safePlaybackSpeed, holdPose: false))
                {
                    state.time = sampleTime;
                    state.speed = safePlaybackSpeed;
                    ghostAnim.Sample();
                }

                retargeter?.ApplyLateVisualGroundingCorrection();
                if (visibleYieldFrames > 0)
                {
                    yield return YieldRetargetPrewarmFrame();
                }

                yield break;
            }

            state.enabled = true;
            state.wrapMode = WrapMode.Once;
            state.time = sampleTime;
            state.speed = 0f;
            for (int i = 0; i < prewarmFrames; i++)
            {
                if (!PrepareRetargeterRecordingStartPose(retargeter, sampleTime, safePlaybackSpeed, holdPose: true))
                {
                    state.time = sampleTime;
                    ghostAnim.Sample();
                }

                yield return YieldRetargetPrewarmFrame();
            }

            if (!PrepareRetargeterRecordingStartPose(retargeter, sampleTime, safePlaybackSpeed, holdPose: false))
            {
                state.time = sampleTime;
                state.speed = safePlaybackSpeed;
                ghostAnim.Sample();
                ghostAnim.Play(clip.name);
                state.time = sampleTime;
                state.speed = safePlaybackSpeed;
                ghostAnim.Sample();
            }

            retargeter?.ApplyLateVisualGroundingCorrection();
            Debug.Log($"[Retargeting] 리타게팅 프리웜 완료됨. 프레임={prewarmFrames}, 클립 시간={sampleTime:F2}초");
        }

        internal static bool PrepareRetargeterRecordingStartPose(PoseSpaceRetargeter retargeter, float sampleTime, float playbackSpeed, bool holdPose)
        {
            return retargeter != null && retargeter.PrepareRecordingStartPose(sampleTime, playbackSpeed, holdPose);
        }

        internal static object YieldRetargetPrewarmFrame()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode)
            {
                // WaitForEndOfFrame can stall in batchmode because no render loop is advancing.
                return null;
            }
#endif
            return new WaitForEndOfFrame();
        }

        private static int ResolveRetargetPrewarmFrameCount(int configuredFrameCount)
        {
            return Mathf.Clamp(configuredFrameCount, 0, MAX_RETARGET_PREWARM_FRAME_COUNT);
        }

        internal static int ResolveRetargetPrewarmFrameCountForRecordingMode(int configuredFrameCount, bool shouldStartVmdRecording)
        {
            if (!shouldStartVmdRecording)
            {
                return 0;
            }

            return ResolveRetargetPrewarmFrameCount(configuredFrameCount);
        }

        internal static int ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode(int configuredFrameCount, bool shouldStartVmdRecording)
        {
            if (!shouldStartVmdRecording)
            {
                return 0;
            }

            int prewarmFrames = ResolveRetargetPrewarmFrameCount(configuredFrameCount);
            return prewarmFrames > 0 ? prewarmFrames : 1;
        }

        private static float ResolveStartDelayForRecordingMode(float configuredStartDelay, bool shouldStartVmdRecording)
        {
            if (!shouldStartVmdRecording)
            {
                return 0f;
            }

            if (float.IsNaN(configuredStartDelay) || float.IsInfinity(configuredStartDelay))
            {
                return 0f;
            }

            return Mathf.Clamp(configuredStartDelay, 0f, 10f);
        }

        internal static bool ShouldStartVmdRecordingAfterImport(
            bool _shouldRecordVmdAfterImport,
            bool editorSmokeRecordingOverrideActive)
        {
            return _shouldRecordVmdAfterImport || editorSmokeRecordingOverrideActive;
        }

        internal void OnRecordingFinished(VmdSaveResult result)
        {
            MotionComparisonProbe probe = _activeRecorderController != null
                ? _activeRecorderController.GetComponent<MotionComparisonProbe>()
                : null;
            VmdSaveResult effectiveResult = ApplyEditorSmokeThumbRiskFailure(result, probe);
            TryAppendVmdArtifactToComparisonSessionManifest(probe, effectiveResult);
            TryCopyVmdToAdditionalFolder(effectiveResult);
            ClearActiveRecordingSubscription();
            LogRetargetPlaybackStabilitySummary();

            if (effectiveResult.Success)
            {
                SetSessionState(FBXSessionState.Success, $"VMD 저장 완료: {Path.GetFileName(effectiveResult.FilePath)}", 1f);
            }
            else
            {
                string errorMessage = string.IsNullOrWhiteSpace(effectiveResult.ErrorMessage) ? "VMD 저장 실패" : effectiveResult.ErrorMessage;
                SetSessionState(FBXSessionState.Failed, errorMessage, 0f);
            }

            CleanupActiveGhost();
            ResetTargetStateAfterSession(recaptureGuardBaselines: false);
#if UNITY_EDITOR
            NotifyEditorSmokeFinished(effectiveResult);
            ClearEditorSmokeOverride();
#endif
            _isProcessing = false;
        }

        private static void TryAppendVmdArtifactToComparisonSessionManifest(MotionComparisonProbe probe, VmdSaveResult result)
        {
            if (probe == null ||
                result.Success == false ||
                string.IsNullOrWhiteSpace(probe.LastSessionManifestPath) ||
                string.IsNullOrWhiteSpace(result.FilePath))
            {
                return;
            }

            MotionComparisonProbeSessionManifestPatcher.TryAppendExportedVmdToSessionManifest(
                probe.LastSessionManifestPath,
                MakeProjectRelativePath(result.FilePath),
                result.FrameCount,
                result.FileSizeBytes);
        }

        private static string MakeProjectRelativePath(string path)
        {
            string rootPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return MakeProjectRelativePath(path, rootPath);
        }

        private static string MakeProjectRelativePath(string path, string projectRoot)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool isProjectPath = string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
                || (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                    && fullPath.Length > fullRoot.Length
                    && (fullPath[fullRoot.Length] == Path.DirectorySeparatorChar
                        || fullPath[fullRoot.Length] == Path.AltDirectorySeparatorChar));

            if (!isProjectPath)
            {
                return path.Replace("\\", "/");
            }

            return fullPath.Substring(fullRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace("\\", "/");
        }

        private void TryCopyVmdToAdditionalFolder(VmdSaveResult result)
        {
            if (!result.Success)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(additionalVmdCopyFolder))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(result.FilePath) || !File.Exists(result.FilePath))
            {
                return;
            }

            try
            {
                string targetFolder = additionalVmdCopyFolder.Trim();
                if (!Path.IsPathRooted(targetFolder))
                {
                    string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                    targetFolder = Path.Combine(projectRoot, targetFolder);
                }

                Directory.CreateDirectory(targetFolder);
                string targetPath = Path.Combine(targetFolder, Path.GetFileName(result.FilePath));
                File.Copy(result.FilePath, targetPath, overwrite: true);
                Debug.Log($"[Recording] VMD 추가 복사 완료됨. 경로={targetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Recording] VMD 추가 복사 실패함. 오류={ex.Message}");
            }
        }

        private VmdSaveResult ApplyEditorSmokeThumbRiskFailure(VmdSaveResult result, MotionComparisonProbe probe)
        {
#if UNITY_EDITOR
            if (!_editorSmokeRecordingOverrideActive || !result.Success || !failEditorSmokeOnThumbRisk)
            {
                return result;
            }

            if (probe == null)
            {
                return BuildEditorSmokeFailureResult(
                    result,
                    $"Editor smoke 실패: {GetEditorSmokeFbxName()} - MotionComparisonProbe가 없어 엄지 리스크 검증을 수행하지 못했습니다.");
            }

            if (!probe.RiskDiagnosticsEnabled ||
                probe.RiskEvaluationFrameCount <= 0 ||
                !probe.HasFullThumbAnatomyCoverage ||
                !probe.HasResolvedThumbHelperCoverage)
            {
                return BuildEditorSmokeFailureResult(
                    result,
                    BuildEditorSmokeThumbDiagnosticUnavailableMessage(probe));
            }

            float maxGenericRisk = probe.MaxGenericThumbAnatomyRisk;
            float maxYybRisk = probe.MaxYybDeformationRisk;
            bool genericExceeded = IsFiniteDiagnosticRisk(maxGenericRisk) &&
                                   maxGenericRisk > editorSmokeMaxGenericThumbAnatomyRisk;
            bool yybExceeded = IsFiniteDiagnosticRisk(maxYybRisk) &&
                               maxYybRisk > editorSmokeMaxYybDeformationRisk;
            if (!genericExceeded && !yybExceeded)
            {
                return result;
            }

            string diagnosticMessage = BuildEditorSmokeThumbRiskFailureMessage(probe, genericExceeded, yybExceeded);
            if (probe.NonBlankScreenshotCount < 8)
            {
                return BuildEditorSmokeFailureResult(
                    result,
                    $"{diagnosticMessage}; same-frame visual evidence incomplete (nonblankScreenshots={probe.NonBlankScreenshotCount})");
            }

            Debug.LogWarning($"[FBXImport] Editor smoke diagnostic only: {diagnosticMessage}");
            return result;
#else
            return result;
#endif
        }

        private VmdSaveResult BuildEditorSmokeFailureResult(VmdSaveResult result, string errorMessage)
        {
            Debug.LogWarning($"[FBXImport] {errorMessage}");
            return new VmdSaveResult
            {
                Success = false,
                FilePath = result.FilePath,
                ErrorMessage = errorMessage,
                FrameCount = result.FrameCount,
                FileSizeBytes = result.FileSizeBytes
            };
        }

#if UNITY_EDITOR
        private string BuildEditorSmokeThumbDiagnosticUnavailableMessage(MotionComparisonProbe probe)
        {
            string fbxName = GetEditorSmokeFbxName();
            return
                $"Editor smoke 실패: {fbxName} - 엄지 리스크 진단 범위가 부족합니다 " +
                $"(enabled={probe.RiskDiagnosticsEnabled}, frames={probe.RiskEvaluationFrameCount}, " +
                $"leftCore={probe.LeftThumbCoreAnatomyObserved}, rightCore={probe.RightThumbCoreAnatomyObserved}, " +
                $"leftHelperRequired={probe.LeftThumbHelperCoverageRequired}, rightHelperRequired={probe.RightThumbHelperCoverageRequired}, " +
                $"leftHelperOk={probe.LeftThumbHelperCoverageSatisfied}, rightHelperOk={probe.RightThumbHelperCoverageSatisfied})";
        }

        private string BuildEditorSmokeThumbRiskFailureMessage(
            MotionComparisonProbe probe,
            bool genericExceeded,
            bool yybExceeded)
        {
            List<string> reasons = new List<string>();
            if (genericExceeded)
            {
                reasons.Add(
                    $"thumb anatomy risk {FormatDiagnosticRisk(probe.MaxGenericThumbAnatomyRisk)} > {FormatDiagnosticRisk(editorSmokeMaxGenericThumbAnatomyRisk)} " +
                    $"(spread={FormatDiagnosticRisk(probe.MaxThumbSpreadRisk)}, projection={FormatDiagnosticRisk(probe.MaxThumbProjectionRisk)}, " +
                    $"helper={FormatDiagnosticRisk(probe.MaxThumbHelperSeparationRisk)}, webbing={FormatDiagnosticRisk(probe.MaxThumbWebbingRisk)})");
            }

            if (yybExceeded)
            {
                reasons.Add(
                    $"YYB deformation risk {FormatDiagnosticRisk(probe.MaxYybDeformationRisk)} > {FormatDiagnosticRisk(editorSmokeMaxYybDeformationRisk)}");
            }

            string fbxName = GetEditorSmokeFbxName();
            return $"Editor smoke 실패: {fbxName} - {string.Join("; ", reasons)}";
        }

        private string GetEditorSmokeFbxName()
        {
            return string.IsNullOrWhiteSpace(_editorSmokeCurrentFbxFileName)
                ? "unknown.fbx"
                : _editorSmokeCurrentFbxFileName;
        }
#endif

        private static bool IsFiniteDiagnosticRisk(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string FormatDiagnosticRisk(float value)
        {
            return IsFiniteDiagnosticRisk(value)
                ? value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                : "n/a";
        }

        private string CopyToControlledImportFolder(string sourcePath)
        {
            string targetDir = GetControlledImportDirectory();
            Directory.CreateDirectory(targetDir);

            string safeFileName = SanitizeFileName(Path.GetFileName(sourcePath));
            string targetPath = Path.Combine(targetDir, safeFileName);

            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, targetPath, true);
            }

            return targetPath;
        }

        internal string GetControlledImportDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "Resources", IMPORT_FBX_FOLDER);
#else
            return Path.Combine(Application.persistentDataPath, IMPORT_FBX_FOLDER);
#endif
        }

        private static string SanitizeFileName(string fileName)
        {
            string safeName = string.IsNullOrWhiteSpace(fileName) ? "motion.fbx" : fileName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return safeName;
        }

        private void ResetTargetStateAfterSession(bool recaptureGuardBaselines)
        {
            RestoreMmdPostPoseCorrectionForRetarget();
            _idlePoseGuard?.Apply();

            if (!recaptureGuardBaselines || targetCharacter == null)
            {
                return;
            }

            HumanoidArmDeformationGuard armGuard = targetCharacter.GetComponent<HumanoidArmDeformationGuard>();
            if (armGuard != null && armGuard.enabled)
            {
                armGuard.RecaptureBaseline();
            }

            HumanoidThumbDeformationGuard thumbGuard = targetCharacter.GetComponent<HumanoidThumbDeformationGuard>();
            if (thumbGuard != null && thumbGuard.enabled)
            {
                thumbGuard.RecaptureBaseline();
            }
        }

        private GameObject CreateGhostContainer(GameObject importedModel)
        {
            GameObject ghostContainer = new GameObject($"GhostContainer_{importedModel.name}");
            ghostContainer.transform.position = Vector3.zero;
            ghostContainer.transform.rotation = Quaternion.identity;
            ghostContainer.transform.localScale = Vector3.one * GHOST_CONTAINER_SCALE;
            importedModel.transform.SetParent(ghostContainer.transform, false);
            importedModel.transform.localPosition = Vector3.zero;
            return ghostContainer;
        }

        private static void SetGhostVisibility(GameObject importedModel, bool visible)
        {
            SetGhostVisibility(importedModel, visible, useSkeletonFallbackWhenRendererless: true);
        }

        private static void SetGhostVisibility(GameObject importedModel, bool visible, bool useSkeletonFallbackWhenRendererless)
        {
            if (importedModel == null)
            {
                return;
            }

            Renderer[] renderers = importedModel.GetComponentsInChildren<Renderer>(true);
            int controlledRendererCount = 0;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.GetComponentInParent<GhostSkeletonDebugRenderer>() != null)
                {
                    continue;
                }

                renderer.enabled = visible;
                controlledRendererCount++;
            }

            SetGhostSkeletonDebugRenderer(
                importedModel,
                visible && useSkeletonFallbackWhenRendererless,
                controlledRendererCount);
        }

        private static bool ShouldAttachGhostSkeletonDebugRenderer(bool visible, int rendererCount)
        {
            return visible;
        }

        private static void SetGhostSkeletonDebugRenderer(GameObject importedModel, bool visible, int rendererCount)
        {
            GhostSkeletonDebugRenderer debugRenderer = importedModel.GetComponent<GhostSkeletonDebugRenderer>();
            bool shouldAttach = ShouldAttachGhostSkeletonDebugRenderer(visible, rendererCount);

            if (shouldAttach)
            {
                if (debugRenderer == null)
                {
                    debugRenderer = importedModel.AddComponent<GhostSkeletonDebugRenderer>();
                }

                debugRenderer.SetVisible(true);
                return;
            }

            if (debugRenderer != null)
            {
                debugRenderer.SetVisible(false);
            }
        }

        private bool ValidateBoneMapping(Dictionary<string, string> boneMapping)
        {
            if (boneMapping == null || boneMapping.Count == 0)
            {
                Debug.LogError("[FBXImport] BoneMapping_Data.txt가 비어 있습니다.");
                return false;
            }

            string[] requiredBones =
            {
                "Hips",
                "Spine",
                "Chest",
                "Head",
                "LeftUpperLeg",
                "RightUpperLeg"
            };

            bool valid = true;
            foreach (string boneName in requiredBones)
            {
                if (!boneMapping.ContainsKey(boneName))
                {
                    Debug.LogError($"[FBXImport] 필수 본 매핑 누락: {boneName}");
                    valid = false;
                }
            }

            if (boneMapping.TryGetValue("Chest", out string chestBone) && chestBone != "Skeleton_Spine1")
            {
                Debug.LogWarning($"[FBXImport] Chest 매핑이 문서 기준과 다릅니다. 현재값: {chestBone}, 기대값: Skeleton_Spine1");
            }

            if (showBoneMappingLog)
            {
                foreach (KeyValuePair<string, string> mapping in boneMapping)
                {
                    Debug.Log($"[FBXImport] BoneMapping: {mapping.Key} -> {mapping.Value}");
                }
            }

            return valid;
        }

        private bool ValidateGhostAvatar(GameObject importedModel)
        {
            Animator ghostAnimator = importedModel.GetComponent<Animator>();
            if (ghostAnimator == null || ghostAnimator.avatar == null)
            {
                Debug.LogError("[FBXImport] Ghost Animator 또는 Avatar가 없습니다.");
                return false;
            }

            if (!ghostAnimator.avatar.isValid || !ghostAnimator.avatar.isHuman)
            {
                Debug.LogError($"[FBXImport] Ghost Avatar가 유효하지 않습니다. valid={ghostAnimator.avatar.isValid}, human={ghostAnimator.avatar.isHuman}");
                return false;
            }

            return true;
        }

        private AnimationClip ExtractPrimaryClip(Animation ghostAnim)
        {
            if (ghostAnim == null || ghostAnim.clip == null)
            {
                return null;
            }

            AnimationClip targetClip = ghostAnim.clip;
            if (targetClip.length <= 0f || float.IsNaN(targetClip.length) || float.IsInfinity(targetClip.length))
            {
                Debug.LogError($"[FBXImport] 애니메이션 길이가 올바르지 않습니다: {targetClip.length}");
                return null;
            }

            if (targetClip.length > 1000f)
            {
                Debug.LogWarning("[FBXImport] 애니메이션 길이가 비정상적으로 깁니다. Assimp timeScale을 확인하세요.");
            }

            if (showRuntimeAnimationLog)
            {
                Debug.Log($"[FBXImport] Clip: {targetClip.name}, Length: {targetClip.length:F3}s, FrameRate: {targetClip.frameRate}");
            }

            return targetClip;
        }

        private void PrepareTargetCharacter(GameObject targetObject, Animator targetAnimator, Animator ghostAnimator)
        {
            targetObject.transform.position = Vector3.zero;
            if (_idlePoseGuard != null && _idlePoseGuard.ShouldFaceTargetToCameraOnIdle)
            {
                CameraFacingController.FaceTargetToCamera(targetObject, Camera.main);
            }
            else
            {
                targetObject.transform.rotation = Quaternion.identity;
            }
            targetAnimator.applyRootMotion = false;
            targetAnimator.runtimeAnimatorController = null;
            DisableMmdPostPoseCorrectionForRetarget(targetObject);
            HumanoidArmTwistRiggingGuard twistRiggingGuard = ConfigureTargetAnimationRiggingArmTwistCorrection(targetObject, targetAnimator);
            _conversionCoordinator.ConfigureArmDirectionGuard(
                targetObject,
                targetAnimator,
                ghostAnimator);
            _conversionCoordinator.ConfigureArmSwingLimitGuard(
                targetObject,
                targetAnimator);
            HumanoidArmSleeveAnchorGuard sleeveAnchorGuard =
                _conversionCoordinator.ConfigureArmSleeveAnchorGuard(targetObject, targetAnimator);
            HumanoidArmVisualTwistGuard visualTwistGuard =
                _conversionCoordinator.ConfigureArmVisualTwistGuard(targetObject, targetAnimator);
            ConfigureTargetArmDeformationGuard(targetObject, BuildLimbChildRotationExclusions(twistRiggingGuard, sleeveAnchorGuard, visualTwistGuard));

            // Batch smoke can leave detached thumb helpers visually drifted until the next frame.
            // Restore the captured target idle pose immediately before the next session captures
            // retargeter/thumb-guard baselines so every FBX starts from the same common pose.
            _idlePoseGuard?.Apply();
#if UNITY_EDITOR
            if (_editorSmokeRecordingOverrideActive)
            {
                LogEditorSmokeThumbState("prepare-target-after-idle-restore");
            }
#endif

            IKControl ikControl = targetObject.GetComponent<IKControl>();
            if (ikControl != null)
            {
                Debug.Log("[FBXImport] 자동 retarget 경로에서 IKControl을 제거합니다.");
                Destroy(ikControl);
            }

            ConfigureFinalIkFootGroundingExperiment(targetObject);
        }

        /// Legacy reflection entry point for FBXVmdPipelineEditorSmokePathTests.
        /// </summary>
        internal static bool ShouldApplyTargetIdlePoseGuardThisFrame(bool isProcessing, bool hasActiveRetargeter)
        {
            return !isProcessing && !hasActiveRetargeter;
        }

        private void ConfigureFinalIkFootGroundingExperiment(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            GrounderBipedIK existingGrounder = targetObject.GetComponent<GrounderBipedIK>();
            BipedIK existingBipedIk = targetObject.GetComponent<BipedIK>();
            if (!enableFinalIkFootGroundingExperiment)
            {
                if (existingGrounder != null)
                {
                    existingGrounder.weight = 0f;
                    existingGrounder.enabled = false;
                }

                if (existingBipedIk != null)
                {
                    existingBipedIk.fixTransforms = false;
                    existingBipedIk.enabled = false;
                }

                return;
            }

            BipedIK bipedIk = existingBipedIk;
            if (bipedIk == null)
            {
                bipedIk = targetObject.AddComponent<BipedIK>();
            }

            Animator animator = targetObject.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                BipedReferences references = bipedIk.references;
                BipedReferences.AutoDetectReferences(
                    ref references,
                    targetObject.transform,
                    BipedReferences.AutoDetectParams.Default);
                bipedIk.references = references;
            }

            bipedIk.SetToDefaults();
            bipedIk.fixTransforms = true;
            bipedIk.enabled = true;

            GrounderBipedIK grounder = existingGrounder;
            if (grounder == null)
            {
                grounder = targetObject.AddComponent<GrounderBipedIK>();
            }

            grounder.ik = bipedIk;
            grounder.weight = Mathf.Clamp(finalIkFootGroundingWeight, 0f, 0.25f);
            grounder.spineBend = 0f;
            grounder.spineSpeed = 0f;
            grounder.solver.maxStep = Mathf.Clamp(finalIkFootGroundingMaxStep, 0f, 0.08f);
            grounder.solver.footRadius = Mathf.Clamp(finalIkFootGroundingFootRadius, 0.01f, 0.2f);
            grounder.solver.prediction = Mathf.Clamp(finalIkFootGroundingPrediction, 0f, 0.2f);
            grounder.solver.footRotationWeight = Mathf.Clamp01(finalIkFootGroundingFootRotationWeight);
            grounder.solver.pelvisDamper = Mathf.Clamp01(finalIkFootGroundingPelvisDamper);
            grounder.enabled = grounder.weight > 0f;

            if (logFinalIkFootGroundingExperiment)
            {
                Debug.Log(
                    $"[FBXImport] Final IK foot grounding experiment configured: " +
                    $"weight={grounder.weight:F3}, maxStep={grounder.solver.maxStep:F3}, " +
                    $"footRadius={grounder.solver.footRadius:F3}");
            }
        }

        private HumanoidArmTwistRiggingGuard ConfigureTargetAnimationRiggingArmTwistCorrection(GameObject targetObject, Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmTwistRiggingGuard twistRiggingGuard = targetObject.GetComponent<HumanoidArmTwistRiggingGuard>();
            if (!enableAnimationRiggingArmTwistCorrection)
            {
                if (twistRiggingGuard != null)
                {
                    twistRiggingGuard.DisableRigging();
                    twistRiggingGuard.enabled = false;
                }

                DisableTargetRigBuilder(targetObject);

                return null;
            }

            if (twistRiggingGuard == null)
            {
                twistRiggingGuard = targetObject.AddComponent<HumanoidArmTwistRiggingGuard>();
            }

            twistRiggingGuard.enableTwistRigging = true;
            twistRiggingGuard.enabled = true;
            bool configured = twistRiggingGuard.Configure(
                targetAnimator,
                AnimationRiggingArmTwistRigWeight,
                AnimationRiggingUpperArmTwistWeight,
                AnimationRiggingForearmTwistWeight,
                logAnimationRiggingArmTwistCorrection);

            return configured ? twistRiggingGuard : null;
        }

        private static void DisableTargetRigBuilder(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            var rigBuilder = targetObject.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
            if (rigBuilder == null)
            {
                return;
            }

            if (rigBuilder.graph.IsValid())
            {
                rigBuilder.Clear();
            }

            rigBuilder.enabled = false;
        }

        private void ConfigureTargetArmDeformationGuard(GameObject targetObject, IEnumerable<Transform> limbChildRotationExclusions)
        {
            if (!attachTargetArmDeformationGuard || targetObject == null)
            {
                return;
            }

            HumanoidArmDeformationGuard guard = targetObject.GetComponent<HumanoidArmDeformationGuard>();
            if (guard == null)
            {
                guard = targetObject.AddComponent<HumanoidArmDeformationGuard>();
            }

            bool clampTargetArmMuscles = targetGuardClampAnatomicalArmMuscles;
            guard.Configure(new ArmDeformationSettings(
                clampMusclesToHumanRange: false,
                enableAnatomicalArmGuard: clampTargetArmMuscles,
                stretchMuscleLimit: ArmStretchMuscleLimit,
                upperArmTwistMuscleLimit: UpperArmTwistMuscleLimit,
                lowerArmTwistMuscleLimit: LowerArmTwistMuscleLimit,
                lockHumanoidBonePositions: _shouldLockTargetHumanoidBonePositions,
                logCorrections: logArmDeformationGuardCorrections,
                clampArmStretchMuscles: targetGuardClampArmStretchMuscles,
                lockLimbChildLocalPositions: lockTargetLimbChildLocalPositions,
                lockLimbChildLocalRotations: lockTargetLimbChildLocalRotations));
            guard.SetLimbChildRotationExclusions(limbChildRotationExclusions);
            guard.enabled = true;
            guard.RecaptureBaseline();
        }

        private void ConfigureTargetThumbDeformationGuard(
            GameObject targetObject,
            Animator targetAnimator,
            PoseSpaceRetargeter linkedRetargeter)
        {
            if (targetObject == null)
            {
                return;
            }

            // The thumb guard must be bound after the current session retargeter exists.
            // During batched smokes the previous ghost can survive until end-of-frame, so
            // resolving by FindObjectsOfType here can accidentally reuse the last session.
            if (targetAnimator == null)
            {
                return;
            }

            HumanoidThumbDeformationGuard thumbGuard = targetObject.GetComponent<HumanoidThumbDeformationGuard>();
            bool clampHumanoidThumbRotations = EffectiveThumbLocalRotationGuard;
            bool syncThumbBaseHelpers = syncDetachedThumbBaseHelpers && detachedThumbBaseHelperSyncWeight > 0f;
            bool stabilizeThumbBasePalm = stabilizeDetachedThumbBasePalm && detachedThumbBasePalmStabilizeWeight > 0f;
            bool stabilizeThumbWebbing = stabilizeThumbWebbingCrease && thumbWebbingCreaseStabilizeWeight > 0f;
            bool preserveManualThumbPose = PreserveManualThumbPoseWithReference;
            if (!clampHumanoidThumbRotations && !syncThumbBaseHelpers && !stabilizeThumbBasePalm && !stabilizeThumbWebbing)
            {
                if (thumbGuard != null)
                {
                    thumbGuard.enabled = false;
                }

                return;
            }

            if (thumbGuard == null)
            {
                thumbGuard = targetObject.AddComponent<HumanoidThumbDeformationGuard>();
            }

            thumbGuard.Configure(
                targetAnimator,
                linkedRetargeter,
                EffectiveThumbProximalMaxLocalAngle,
                ThumbIntermediateMaxLocalAngle,
                ThumbDistalMaxLocalAngle,
                ThumbRotationOffset,
                mirrorRightThumbRotationOffset,
                LeftThumbRotationOffset,
                RightThumbRotationOffset,
                logThumbLocalRotationGuardCorrections,
                clampHumanoidThumbRotations,
                syncThumbBaseHelpers,
                EffectiveDetachedThumbBaseHelperPositionSync,
                detachedThumbBaseHelperSyncWeight,
                detachedThumbBaseHelperMaxLocalAngle,
                detachedThumbBaseHelperMaxPositionOffset,
                LeftDetachedThumbBaseHelperDeltaAxisOffset,
                RightDetachedThumbBaseHelperDeltaAxisOffset,
                LeftDetachedThumbBaseHelperTargetRotationOffset,
                RightDetachedThumbBaseHelperTargetRotationOffset,
                stabilizeThumbBasePalm,
                detachedThumbBasePalmStabilizeWeight,
                detachedThumbBasePalmMaxLocalAngle,
                enableThumbVisualLengthGuard,
                EffectiveThumbProjectionMinPalmNormal,
                ThumbProjectionMaxPalmNormal,
                ThumbProjectionGuardWeight,
                ThumbIndexMaxSpreadAngle,
                ThumbIndexSpreadGuardWeight,
                ThumbMaxSegmentBendAngle,
                ThumbSegmentStraightenWeight,
                preserveManualThumbPose,
                stabilizeThumbWebbing,
                thumbWebbingCreaseStabilizeWeight,
                thumbWebbingCreaseMaxLocalAngle,
                thumbWebbingCreaseMaxPositionOffset);
            thumbGuard.enabled = true;
            thumbGuard.RecaptureBaseline();
#if UNITY_EDITOR
            if (_editorSmokeRecordingOverrideActive)
            {
                LogEditorSmokeThumbState("thumb-guard-bound");
            }
#endif
        }

        private bool EffectiveDetachedThumbBaseHelperPositionSync
        {
            get
            {
                return syncDetachedThumbBaseHelperPositions &&
                    syncDetachedThumbBaseHelpers &&
                    detachedThumbBaseHelperSyncWeight > 0f;
            }
        }

        private static IEnumerable<Transform> BuildLimbChildRotationExclusions(
            HumanoidArmTwistRiggingGuard twistRiggingGuard,
            HumanoidArmSleeveAnchorGuard sleeveAnchorGuard,
            HumanoidArmVisualTwistGuard visualTwistGuard)
        {
            if (twistRiggingGuard != null)
            {
                foreach (Transform controlledTransform in twistRiggingGuard.ControlledTransforms)
                {
                    yield return controlledTransform;
                }
            }

            if (sleeveAnchorGuard != null)
            {
                foreach (Transform controlledTransform in sleeveAnchorGuard.ControlledTransforms)
                {
                    yield return controlledTransform;
                }
            }

            if (visualTwistGuard != null)
            {
                foreach (Transform controlledTransform in visualTwistGuard.ControlledTransforms)
                {
                    yield return controlledTransform;
                }
            }
        }

        private void DisableMmdPostPoseCorrectionForRetarget(GameObject targetObject)
        {
            if (!disableMmdShoulderPostPoseDuringRetarget || targetObject == null)
            {
                return;
            }

            RestoreMmdPostPoseCorrectionForRetarget();

            int changed = 0;
            Component[] components = targetObject.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null)
                {
                    continue;
                }

                Type componentType = component.GetType();
                if (!componentType.Name.Contains("MMD4Mecanim"))
                {
                    continue;
                }

                if (TrySetBooleanField(component, "pphShoulderEnabled", false))
                {
                    changed++;
                }
            }

            if (changed > 0)
            {
                Debug.Log($"[FBXImport] Retarget 중 MMD4Mecanim 어깨 PPH 보정 {changed}개를 일시 비활성화했습니다.");
            }
        }

        private bool TrySetBooleanField(object target, string fieldName, bool value)
        {
            FieldInfo field = FindFieldInHierarchy(target.GetType(), fieldName);
            if (field == null || field.FieldType != typeof(bool))
            {
                return false;
            }

            bool currentValue = (bool)field.GetValue(target);
            if (currentValue == value)
            {
                return false;
            }

            _retargetBooleanSnapshots.Add(new BooleanFieldSnapshot(target, field, currentValue));
            field.SetValue(target, value);
            return true;
        }

        private static FieldInfo FindFieldInHierarchy(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type currentType = type; currentType != null; currentType = currentType.BaseType)
            {
                FieldInfo field = currentType.GetField(fieldName, flags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private void RestoreMmdPostPoseCorrectionForRetarget()
        {
            for (int i = _retargetBooleanSnapshots.Count - 1; i >= 0; i--)
            {
                BooleanFieldSnapshot snapshot = _retargetBooleanSnapshots[i];
                if (snapshot.Target == null || snapshot.Field == null)
                {
                    continue;
                }

                snapshot.Field.SetValue(snapshot.Target, snapshot.Value);
            }

            _retargetBooleanSnapshots.Clear();
        }

        internal void FailSession(string message, Exception exception = null)
        {
            string exceptionDetails = exception == null ? string.Empty : $"\n{exception}";
            Debug.LogError($"[FBXImport] FBX 처리 실패함. 메시지={message}{exceptionDetails}");

            SetSessionState(FBXSessionState.Failed, message, 0f, shouldLog: false);
            ClearActiveRecordingSubscription();
            CleanupActiveGhost();
            ResetTargetStateAfterSession(recaptureGuardBaselines: false);
#if UNITY_EDITOR
            NotifyEditorSmokeFinished(VmdSaveResult.Fail("", message));
            ClearEditorSmokeOverride();
#endif
            _isProcessing = false;
        }

        private void LogRetargetPlaybackStabilitySummary()
        {
            if (_activeRetargeter == null)
            {
                return;
            }

            Debug.Log(
                $"[FBXImport] Retarget playback stability: " +
                $"clipTimeClamp={_activeRetargeter.clampLegacyAnimationVisualStep}, " +
                $"maxClipStep={_activeRetargeter.MaxLegacyAnimationStep:F4}s, " +
                $"stepSpikes={_activeRetargeter.LegacyAnimationStepSpikeCount}, " +
                $"poseSmooth={_activeRetargeter.PoseVisualSmoothingCount}, " +
                $"muscleOnlySmoothSkipped={_activeRetargeter.PoseVisualMuscleDeltaOnlySkippedCount}, " +
                $"maxPoseMuscleDelta={_activeRetargeter.MaxPoseVisualMaxMuscleDelta:F4}, " +
                $"hipsLocalClamp={_activeRetargeter.TargetHipsLocalPositionSpikeClampedCount}, " +
                $"maxHipsLocalDelta={_activeRetargeter.MaxTargetHipsLocalPositionDelta:F4}m, " +
                $"thumbReference[{BuildActiveRetargeterThumbReferenceSummary()}]");
        }

#if UNITY_EDITOR
        private void LogEditorSmokeThumbState(string stage)
        {
            if (!_editorSmokeRecordingOverrideActive || targetCharacter == null)
            {
                return;
            }

            HumanoidThumbDeformationGuard thumbGuard = targetCharacter.GetComponent<HumanoidThumbDeformationGuard>();
            string leftGuard = thumbGuard != null ? thumbGuard.BuildThumbHelperDebugSummary(false) : "thumbGuard=<none>";
            string rightGuard = thumbGuard != null ? thumbGuard.BuildThumbHelperDebugSummary(true) : "thumbGuard=<none>";
            string leftRetargeter = _activeRetargeter != null ? _activeRetargeter.BuildThumbHelperRelationshipDebugSummary(true) : "retargeter=<none>";
            string rightRetargeter = _activeRetargeter != null ? _activeRetargeter.BuildThumbHelperRelationshipDebugSummary(false) : "retargeter=<none>";

            Debug.Log(
                $"[FBXImport] Editor smoke thumb state ({stage}): " +
                $"fbx={_editorSmokeCurrentFbxFileName ?? "<none>"}, " +
                $"segment={GetEditorSmokeSegmentLabel(_editorSmokeSegment)}, " +
                $"projectionMin={EffectiveThumbProjectionMinPalmNormal:F3}, " +
                $"thumbReference[{BuildActiveRetargeterThumbReferenceSummary()}], " +
                $"guardLeft[{leftGuard}], guardRight[{rightGuard}], " +
                $"retargeterLeft[{leftRetargeter}], retargeterRight[{rightRetargeter}]");
        }
#endif

        private string BuildActiveRetargeterThumbReferenceSummary()
        {
            if (_activeRetargeter == null)
            {
                return "retargeter=<none>";
            }

            Animator referenceAnimator = ReadRetargeterPrivateField<Animator>(_activeRetargeter, "_editorFingerReferenceAnimator");
            bool editorFingerRuntime = ReadRetargeterPrivateField<bool>(_activeRetargeter, "_useEditorFingerPoseReference");
            return
                $"retargeter={GetHierarchyPath(_activeRetargeter.transform)}, " +
                $"targetAnimator={GetHierarchyPath(_activeRetargeter.targetAnimator != null ? _activeRetargeter.targetAnimator.transform : null)}, " +
                $"thumbLocalRefConfig={_activeRetargeter.useManualAnimatorThumbLocalRotationReference}, " +
                $"preserveThumbMuscles={_activeRetargeter.preserveManualFingerReferenceThumbMuscles}, " +
                $"editorFingerRuntime={editorFingerRuntime}, " +
                $"referenceAnimator={GetHierarchyPath(referenceAnimator != null ? referenceAnimator.transform : null)}, " +
                $"manualThumbActive={_activeRetargeter.IsManualThumbLocalRotationReferenceActive}, " +
                $"suppressLeft={_activeRetargeter.ShouldSuppressLeftThumbPoseShapingGuard}, " +
                $"suppressRight={_activeRetargeter.ShouldSuppressRightThumbPoseShapingGuard}, " +
                $"leftLocalGuardClamp={_activeRetargeter.LastLeftThumbLocalRotationGuardClampCount}, " +
                $"rightLocalGuardClamp={_activeRetargeter.LastRightThumbLocalRotationGuardClampCount}, " +
                $"leftLocalGuardPreserve={_activeRetargeter.LastLeftThumbLocalRotationGuardPreserveCount}, " +
                $"rightLocalGuardPreserve={_activeRetargeter.LastRightThumbLocalRotationGuardPreserveCount}";
        }

        private static T ReadRetargeterPrivateField<T>(PoseSpaceRetargeter retargeter, string fieldName)
        {
            if (retargeter == null)
            {
                return default;
            }

            FieldInfo field = typeof(PoseSpaceRetargeter).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                return default;
            }

            object value = field.GetValue(retargeter);
            return value is T typedValue ? typedValue : default;
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        internal void SetSessionState(FBXSessionState state, string message, float progress, bool shouldLog = true)
        {
            if (shouldLog)
            {
                Debug.Log($"[FBXImport] 상태 변경됨. 상태={state}, 메시지={message}");
            }

            HumanoidSampleCode recorder = GetRecorderController();
            if (recorder == null)
            {
                return;
            }

            switch (state)
            {
                case FBXSessionState.Failed:
                    recorder.SetError(message);
                    break;
                case FBXSessionState.Cancelled:
                    recorder.SetReady(message);
                    break;
                case FBXSessionState.Success:
                    recorder.SetProcessingStatus(message, 1f);
                    break;
                default:
                    recorder.SetProcessingStatus(message, progress);
                    break;
            }
        }

        private HumanoidSampleCode GetRecorderController()
        {
            return targetCharacter != null ? targetCharacter.GetComponent<HumanoidSampleCode>() : null;
        }

        private void CleanupActiveGhost()
        {
            if (_activeGhostContainer == null)
            {
                _activeRetargeter = null;
                return;
            }

            bool destroyImmediately = false;
#if UNITY_EDITOR
            // Diagnostic smokes chain multiple sessions in one play session. Deferred
            // destroy leaves the previous ghost alive until end-of-frame, which can
            // contaminate the next session's thumb/helper baselines.
            destroyImmediately = _editorSmokeRecordingOverrideActive;
#endif

            if (destroyImmediately)
            {
                DestroyImmediate(_activeGhostContainer);
            }
            else
            {
                Destroy(_activeGhostContainer);
            }
            _activeGhostContainer = null;
            _activeRetargeter = null;
        }

        private void ClearActiveRecordingSubscription()
        {
            if (_activeRecorderController != null)
            {
                _activeRecorderController.RecordingFinished -= OnRecordingFinished;
                _activeRecorderController = null;
            }
        }

        private IEnumerator StartRecordingSequence(
            GameObject ghostModel,
            Animation ghostAnim,
            GameObject targetCharacter,
            AnimationClip clip,
            PoseSpaceRetargeter retargeter,
            string outputBaseName = "")
        {
            Debug.Log($"[FBXImport]  녹화 시작까지 {startDelay}초 대기 중...");

            // 지연 대기
            yield return new WaitForSeconds(startDelay);

            // Ghost 애니메이션 재생 시작
            if (ghostAnim != null && clip != null)
            {
                ghostAnim.clip = clip;
                ghostAnim.Play();
                Debug.Log($"[FBXImport]  Ghost 애니메이션 재생 시작: {clip.name}");
            }

            // VMD 녹화 시작
            var recorderController = targetCharacter.GetComponent<HumanoidSampleCode>();
            if (recorderController != null)
            {
                float clipLen = clip.length;
                string recordingBaseName = !string.IsNullOrWhiteSpace(outputBaseName) ? outputBaseName : clip.name;
                recorderController.StartAutoRecording(
                    clipLen,
                    recordingBaseName,
                    null,
                    0,
                    comparisonLabel: $"auto_{recordingBaseName}",
                    overwriteExistingOutput: true);
                Debug.Log($"[FBXImport]  VMD 녹화 동시 시작! (길이: {clipLen:F2}초)");
            }
            else
            {
                Debug.LogWarning("[FBXImport]  HumanoidSampleCode 컴포넌트가 Target에 없습니다. 녹화 건너뜀.");
            }
        }

        /// <summary>
        /// 런타임에서도 BoneMapping_Data.txt를 읽어오는 함수
        /// </summary>
        private Dictionary<string, string> LoadBoneMappingRuntime()
        {
            var mapping = new Dictionary<string, string>();

            // Resources 폴더에서 로드 (확장자 .txt 제외)
            string loadName = Path.GetFileNameWithoutExtension(BONE_MAPPING_FILE);
            TextAsset mappingAsset = Resources.Load<TextAsset>(loadName);

            if (mappingAsset != null)
            {
                Debug.Log($"[FBXImport] BoneMapping 로드 성공 (Resources/{loadName})");
                string[] lines = mappingAsset.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                bool insideBoneTemplate = false;

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("m_BoneTemplate:"))
                    {
                        insideBoneTemplate = true;
                        continue;
                    }

                    if (insideBoneTemplate)
                    {
                        if (trimmedLine.StartsWith("m_")) break; // 섹션 종료

                        int colonIndex = trimmedLine.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string key = trimmedLine[..colonIndex].Trim();
                            string value = trimmedLine[(colonIndex + 1)..].Trim();
                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                            {
                                mapping[key] = value;
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[FBXImport] BoneMapping 로드 실패: Resources/{loadName}.txt (자동 본 매핑으로 폴백합니다.)");
            }

            return mapping;
        }

        private void SetupGhostRetargeting(GameObject ghostObject, AnimationClip ghostClip, GameObject targetPrefab)
        {
            Debug.Log("[FBXImport] Native AnimatorOverride 설정 중...");
             Animator targetAnimator = targetPrefab.GetComponent<Animator>();
            if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null) return;

            AnimatorOverrideController overrideController = new AnimatorOverrideController(targetAnimator.runtimeAnimatorController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            if (overrides.Count > 0)
            {
                overrideController[overrides[0].Key] = ghostClip;
            }

            targetAnimator.runtimeAnimatorController = overrideController;

            var sampleCode = targetPrefab.GetComponent<HumanoidSampleCode>();
            if (sampleCode != null)
            {
                sampleCode.StartProcessing(ghostClip);
            }
            else
            {
                targetAnimator.enabled = true;
                string stateName = overrides.Count > 0 && overrides[0].Key != null
                    ? overrides[0].Key.name
                    : (ghostClip != null ? ghostClip.name : "");

                if (!string.IsNullOrEmpty(stateName))
                {
                    targetAnimator.Play(stateName, 0, 0f);
                }
            }

            if (ghostObject != null) Destroy(ghostObject);
        }
        #endregion

#if UNITY_EDITOR
        private void ConfigureEditorHumanoidMuscleReference(PoseSpaceRetargeter retargeter, string importedFilePath, string sourceFilePath)
        {
            if (!_shouldUseEditorHumanoidClipMuscleReference || retargeter == null)
            {
                return;
            }

            string relativePath = ResolveEditorHumanoidReferencePath(importedFilePath, sourceFilePath);
            if (string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            AnimationClip referenceClip = LoadEditorHumanoidAnimationClip(relativePath);
            if (referenceClip == null)
            {
                Debug.LogWarning($"[FBXImport] Unity Editor Humanoid 기준 클립을 찾지 못했습니다: {relativePath}");
                return;
            }

            Debug.Log($"[FBXImport] Editor Humanoid muscle 기준 clip: {relativePath}/{referenceClip.name}");
            retargeter.ConfigureEditorHumanoidMuscleReference(referenceClip);
            if (_shouldUseEditorHumanoidRootTranslationReference)
            {
                retargeter.ConfigureEditorHumanoidRootTranslationReference(referenceClip);
            }
            ConfigureEditorManualFingerPoseReference(retargeter, referenceClip);
        }

        private void ConfigureEditorManualFingerPoseReference(PoseSpaceRetargeter retargeter, AnimationClip referenceClip)
        {
            if ((!_shouldUseManualAnimatorFingerPoseReference &&
                    !_shouldUseManualAnimatorFullBodyPoseReference &&
                    !_shouldUseManualAnimatorHipsLocalPositionReference &&
                    !_shouldUseManualAnimatorBodyRotationReference &&
                    !useManualAnimatorHandLocalRotationReference &&
                    !_shouldUseManualAnimatorFootLocalRotationReference &&
                    !_shouldUseManualAnimatorLowerBodySegmentDirectionReference &&
                    !_shouldUseManualAnimatorFootHipsAlignedResidualYawReference &&
                    !usePostSetHumanPoseRightEndpointPositionReference &&
                    !usePostSetHumanPoseRightFootEvaluatorXzReference &&
                    !usePreSetHumanPoseRightEndpointPositionReference &&
                    !_shouldUseManualAnimatorBodyPositionXzReference &&
                    !useYybRightSleeveSilhouetteLocalOffsetReference &&
                    !useManualAnimatorBipedIkFootPositionReference) ||
                retargeter == null ||
                referenceClip == null)
            {
                return;
            }

            GameObject referencePrefab = manualFingerReferencePrefab;
            if (referencePrefab == null)
            {
                referencePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Plugins/VMDRecorderSample/Models/TestModel/testPrefab.prefab");
            }

            RuntimeAnimatorController referenceController = manualFingerReferenceController;
            if (referenceController == null)
            {
                referenceController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_ManualReference/SampleAnimation/TestAnimator1_Manual.controller");
            }

            if (referenceController == null)
            {
                referenceController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller");
            }

            if (referencePrefab == null || referenceController == null)
            {
                Debug.LogWarning("[FBXImport] 수동 기준 손가락 Reference prefab/controller를 찾지 못해 raw FBX finger curve를 사용합니다.");
                return;
            }

            retargeter.ConfigureEditorHumanoidFingerPoseReference(
                referencePrefab,
                referenceController,
                referenceClip,
                _shouldUseManualAnimatorFingerPoseReference,
                _shouldUseManualAnimatorFullBodyPoseReference,
                manualAnimatorFullBodyPoseReferenceWeight,
                _shouldExcludeManualAnimatorFullBodyLowerMuscles,
                _shouldApplyManualAnimatorFullBodyLowerMusclesOnly,
                _shouldApplyManualAnimatorFullBodyLegTwistMusclesOnly,
                manualAnimatorFullBodyPoseRightArmMusclesOnly,
                manualAnimatorFullBodyPoseLeftArmMusclesOnly,
                manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly,
                manualAnimatorFullBodyPoseFrameGateStart,
                manualAnimatorFullBodyPoseFrameGateEnd);
            retargeter.useYybRightSleeveSilhouetteLocalOffsetReference =
                useYybRightSleeveSilhouetteLocalOffsetReference;
            retargeter.yybRightSleeveSilhouetteLocalOffsetX =
                Mathf.Clamp(yybRightSleeveSilhouetteLocalOffsetX, -0.2f, 0.2f);
            retargeter.yybRightSleeveSilhouetteLocalOffsetFrameGateStart =
                Mathf.Max(0f, yybRightSleeveSilhouetteLocalOffsetFrameGateStart);
            retargeter.yybRightSleeveSilhouetteLocalOffsetFrameGateEnd =
                Mathf.Max(0f, yybRightSleeveSilhouetteLocalOffsetFrameGateEnd);
        }

        private static string ResolveEditorHumanoidReferencePath(string importedFilePath, string sourceFilePath)
        {
            string sourceRelativePath = ToAssetRelativePath(sourceFilePath);
            string importedRelativePath = ToAssetRelativePath(importedFilePath);
            string sourceFileName = string.IsNullOrEmpty(sourceFilePath) ? importedFilePath : sourceFilePath;
            return ResolveEditorHumanoidReferencePath(
                importedRelativePath,
                sourceRelativePath,
                sourceFileName,
                HasEditorHumanoidAnimationClip);
        }

        private static string ResolveEditorHumanoidReferencePath(
            string importedRelativePath,
            string sourceRelativePath,
            string sourceFileName,
            Func<string, bool> hasHumanoidAnimationClip)
        {
            if (!IsControlledImportAssetPath(sourceRelativePath) && hasHumanoidAnimationClip(sourceRelativePath))
            {
                return sourceRelativePath;
            }

            string fileName = Path.GetFileName(string.IsNullOrEmpty(sourceFileName) ? importedRelativePath : sourceFileName);
            if (!string.IsNullOrEmpty(fileName))
            {
                string manualReferencePath = Path.Combine("Assets", "_Project", "FBX", fileName).Replace("\\", "/");
                if (hasHumanoidAnimationClip(manualReferencePath))
                {
                    return manualReferencePath;
                }
            }

            return hasHumanoidAnimationClip(importedRelativePath) ? importedRelativePath : "";
        }

        private static bool IsControlledImportAssetPath(string relativePath)
        {
            return !string.IsNullOrEmpty(relativePath)
                && relativePath.Replace("\\", "/").StartsWith($"Assets/Resources/{IMPORT_FBX_FOLDER}/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldConfigureEditorImportSettings(string sourcePath, string targetPath, string dataPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return true;
            }

            if (!PathsEqual(sourcePath, targetPath))
            {
                return true;
            }

            string targetRelativePath = ToAssetRelativePath(targetPath, dataPath);
            return !IsControlledImportAssetPath(targetRelativePath);
        }

        private static bool HasEditorHumanoidAnimationClip(string relativePath)
        {
            return !string.IsNullOrEmpty(relativePath) && LoadEditorHumanoidAnimationClip(relativePath) != null;
        }

        private static AnimationClip LoadEditorHumanoidAnimationClip(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativePath);
            foreach (UnityEngine.Object asset in assets)
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip == null || clip.name.StartsWith("__", StringComparison.Ordinal))
                {
                    continue;
                }

                if (clip.humanMotion)
                {
                    return clip;
                }
            }

            return null;
        }

        private static string ToAssetRelativePath(string filePath)
        {
            return ToAssetRelativePath(filePath, Application.dataPath);
        }

        private static string ToAssetRelativePath(string filePath, string dataPath)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(dataPath))
            {
                return "";
            }

            string standardizedFilePath = filePath.Replace("\\", "/");
            string standardizedDataPath = dataPath.Replace("\\", "/");

            if (!standardizedFilePath.StartsWith(standardizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            return "Assets" + standardizedFilePath[standardizedDataPath.Length..];
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ConfigureImportSettings(string filePath)
        {
            // 절대 경로를 "Assets/..." 상대 경로로 변환
            string standardizedFilePath = filePath.Replace("\\", "/");
            string standardizedDataPath = Application.dataPath.Replace("\\", "/");

            if (!standardizedFilePath.StartsWith(standardizedDataPath))
            {
                Debug.LogError($"파일 경로가 Assets 폴더 내에 있지 않습니다: {filePath}");
                return;
            }

            string relativePath = "Assets" + standardizedFilePath[standardizedDataPath.Length..];

            // FBX 파일 Import (기본 설정으로)
            Debug.Log($"[1단계] FBX Import 시작: {relativePath}");
            UnityEditor.AssetDatabase.ImportAsset(relativePath, UnityEditor.ImportAssetOptions.ForceUpdate);

            // FBX 정보 가져오기
            Debug.Log($"[2단계] FBX 정보 가져오기");
            UnityEditor.ModelImporter importer = UnityEditor.AssetImporter.GetAtPath(relativePath) as UnityEditor.ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[2단계 실패] ModelImporter를 가져올 수 없습니다: {relativePath}");
                return;
            }

            Debug.Log($"[2단계 완료] ModelImporter 정보:");
            Debug.Log($"  - 현재 Animation Type: {importer.animationType}");
            Debug.Log($"  - 현재 Import Animation: {importer.importAnimation}");
            Debug.Log($"  - 현재 Optimize Bones: {importer.optimizeBones}");

            Debug.Log($"[3단계] Rig 설정 적용 중...");

            // Animation Import 활성화
            importer.importAnimation = true;
            importer.animationCompression = UnityEditor.ModelImporterAnimationCompression.Off;

            // Animation Type = Humanoid
            importer.animationType = UnityEditor.ModelImporterAnimationType.Human;

            // Avatar Definition = "Create From This Model"
            // Avatar를 모델에서 생성하도록 명시적 설정 (AnimationClip 보존을 위해 필수)
            importer.avatarSetup = UnityEditor.ModelImporterAvatarSetup.CreateFromThisModel;

            // Strip Bones 해제 (Optimize Game Objects 아님)
            importer.optimizeBones = false;
            try
            {
                // SerializedObject를 사용하여 "optimizeBones" 또는 관련 속성 해제
                UnityEditor.SerializedObject serializedImporter = new UnityEditor.SerializedObject(importer);
                serializedImporter.Update();

                // 가능한 내부 속성명들 (m_OptimizeBones 등)
                string[] propNames = new string[] { "m_OptimizeBones", "optimizeBones" };
                bool found = false;

                foreach (string name in propNames)
                {
                    UnityEditor.SerializedProperty prop = serializedImporter.FindProperty(name);
                    if (prop != null && prop.propertyType == UnityEditor.SerializedPropertyType.Boolean)
                    {
                        prop.boolValue = false;
                        found = true;
                        Debug.Log($"[Strip Bones Fix] SerializedObject를 통해 '{name}' 비활성화 성공");
                    }
                }

                if (found)
                {
                    serializedImporter.ApplyModifiedProperties();
                }
                else
                {
                    Debug.LogWarning("[Strip Bones Fix] 'optimizeBones' 관련 속성을 찾지 못했습니다.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Strip Bones Fix] 오류: {e.Message}");
            }

            // AnimationCompression 추가 설정
            importer.animationWrapMode = WrapMode.ClampForever; // Once: 재생 후 초기화, ClampForever: 마지막 프레임 유지
            importer.importBlendShapes = true;
            importer.importVisibility = true;
            importer.importCameras = false;
            importer.importLights = false;

            // Bone Mapping 적용
            Debug.Log($"[3단계] Bone Mapping 적용 시작");

            string mappingFilePath = Path.Combine(Application.dataPath, "Resources", BONE_MAPPING_FILE);
            if (File.Exists(mappingFilePath))
            {
                var mappingDict = ParseBoneMappingFile(mappingFilePath);
                if (mappingDict != null && mappingDict.Count > 0)
                {
                    Debug.Log($"[3단계] Bone Mapping 파일 파싱 완료: {mappingDict.Count}개 매핑");

                    HumanDescription description = importer.humanDescription;

                    // Human Bones 설정
                    List<HumanBone> humanBones = new List<HumanBone>();
                    foreach (var kvp in mappingDict)
                    {
                        HumanBone bone = new HumanBone
                        {
                            humanName = HumanoidAvatarBuilder.NormalizeHumanBoneName(kvp.Key),
                            boneName = kvp.Value
                        };
                        bone.limit.useDefaultValues = true;
                        humanBones.Add(bone);
                    }
                    description.human = humanBones.ToArray();

                    // Skeleton 배열도 설정
                    List<SkeletonBone> skeletonBones = new List<SkeletonBone>();
                    var allTransformPaths = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(relativePath)
                        .OfType<UnityEngine.Transform>();

                    foreach (var transform in allTransformPaths)
                    {
                        if (transform != null)
                        {
                            SkeletonBone skelBone = new SkeletonBone
                            {
                                name = transform.name,
                                position = transform.localPosition,
                                rotation = transform.localRotation,
                                scale = transform.localScale
                            };
                            skeletonBones.Add(skelBone);
                        }
                    }

                    if (skeletonBones.Count > 0)
                    {
                        description.skeleton = skeletonBones.ToArray();
                        Debug.Log($"[3단계] Skeleton 배열 설정: {skeletonBones.Count}개 본");
                    }

                    importer.humanDescription = description;

                    Debug.Log($"[3단계] Bone Mapping 적용: {humanBones.Count}개 본");
                }
                else
                {
                    Debug.LogWarning($"[3단계] Bone Mapping 데이터가 비어있습니다.");
                }
            }
            else
            {
                Debug.LogWarning($"[3단계] Bone Mapping 파일을 찾을 수 없습니다: {mappingFilePath}");
            }

            // Animation Clip 추출 설정
            Debug.Log($"[3단계] Animation Clip 추출 시작");

            if (importer.defaultClipAnimations != null && importer.defaultClipAnimations.Length > 0)
            {
                // 기본 클립을 custom clipAnimations 없이 사용
                // custom clipAnimations는 bodyMask를 저장하며 손가락 muscle curve를 0으로 만들 수 있다.
                // 수동 기준처럼 기본 Humanoid clip을 그대로 쓰기 위해 명시 clip 설정을 비운다.
                importer.clipAnimations = Array.Empty<UnityEditor.ModelImporterClipAnimation>();
                Debug.Log($"[3단계] Animation Clip 추출: {importer.defaultClipAnimations.Length}개");

                foreach (var clip in importer.defaultClipAnimations)
                {
                    Debug.Log($"  - Clip: {clip.name} (Start: {clip.firstFrame}, End: {clip.lastFrame})");
                }
            }
            else
            {
                Debug.LogWarning("[3단계] defaultClipAnimations가 비어있습니다. 자동 경로에서는 임의 Take 001을 만들지 않습니다.");
                importer.clipAnimations = Array.Empty<UnityEditor.ModelImporterClipAnimation>();
            }

            Debug.Log($"[3단계 완료] 최종 설정:");
            Debug.Log($"  - Animation Type: Humanoid");
            Debug.Log($"  - Import Animation: {importer.importAnimation}");
            Debug.Log($"  - Optimize Game Objects (Strip Bones): {importer.optimizeGameObjects}");
            Debug.Log($"  - Bone Mapping: 적용 완료");
            Debug.Log($"  - Animation Clips: {(importer.clipAnimations != null ? importer.clipAnimations.Length : 0)}개");

            // 최종 저장 및 Reimport (한 번만!)
            UnityEditor.AssetDatabase.WriteImportSettingsIfDirty(relativePath);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.ImportAsset(relativePath, UnityEditor.ImportAssetOptions.ForceUpdate);

            Debug.Log($"[3단계] 최종 Reimport 완료");
            Debug.Log($"===========================================");
        }

        #region Editor 전용: Bone Mapping 파싱

        /// <summary>
        /// BoneMapping_Data.txt 파일을 읽어서 ModelImporter에 HumanDescription으로 적용
        /// </summary>
        private void ApplyBoneMapping(UnityEditor.ModelImporter importer)
        {
            try
            {
                string mappingFilePath = Path.Combine(Application.dataPath, "Resources", BONE_MAPPING_FILE);
                if (!File.Exists(mappingFilePath))
                {
                    Debug.LogWarning($"Bone Mapping 파일을 찾을 수 없습니다: {mappingFilePath}");
                    return;
                }

                // 파일 읽기 및 파싱
                var mappingDict = ParseBoneMappingFile(mappingFilePath);
                if (mappingDict.Count == 0)
                {
                    Debug.LogWarning("Bone Mapping 데이터가 비어있습니다.");
                    return;
                }

                Debug.Log($"[Editor Only] Bone Mapping 파일 파싱 완료: {mappingDict.Count}개 매핑 발견");

                // 기존 HumanDescription 가져오기 (새로 생성할 경우 기본값 사용)
                HumanDescription description = importer.humanDescription;

                // HumanBone 배열 생성
                List<HumanBone> humanBones = new List<HumanBone>();
                foreach (var kvp in mappingDict)
                {
                    HumanBone bone = new HumanBone
                    {
                        humanName = HumanoidAvatarBuilder.NormalizeHumanBoneName(kvp.Key),      // Unity Humanoid bone name (e.g., "Hips")
                        boneName = kvp.Value      // Actual bone name in FBX (e.g., "Skeleton_Hips")
                    };

                    // Limit 설정은 기본값 사용
                    bone.limit.useDefaultValues = true;

                    humanBones.Add(bone);
                }

                // HumanDescription 업데이트
                description.human = humanBones.ToArray();

                // Skeleton 설정: 보통은 모든 Transform을 포함하도록 비워두거나,
                // 필요한 경우 특정 본만 지정할 수 있음 (일단 기본 설정 유지)
                // description.skeleton = ... (필요시 설정)

                // 새로운 HumanDescription 적용
                importer.humanDescription = description;

                Debug.Log($"[Editor Only] Bone Mapping 적용 완료: {humanBones.Count}개 본 매핑됨");

                // 매핑된 본 목록 출력 (디버그용)
                foreach (var bone in humanBones)
                {
                    Debug.Log($"  - {bone.humanName} -> {bone.boneName}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Bone Mapping 적용 중 오류 발생: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// YAML 형식의 BoneMapping_Data.txt 파싱
        /// </summary>
        private Dictionary<string, string> ParseBoneMappingFile(string path)
        {
            var mapping = new Dictionary<string, string>();
            string[] lines = File.ReadAllLines(path);
            bool insideBoneTemplate = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // m_BoneTemplate 섹션 시작 확인
                if (trimmedLine.StartsWith("m_BoneTemplate:"))
                {
                    insideBoneTemplate = true;
                    continue;
                }

                // 섹션이 끝나거나 다른 속성이 나오면 중단
                if (insideBoneTemplate)
                {
                    if (trimmedLine.StartsWith("m_")) break; // 다른 속성 시작

                    // "HumanBoneName: ActualBoneName" 형식 파싱
                    int colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string key = trimmedLine[..colonIndex].Trim();
                        string value = trimmedLine[(colonIndex + 1)..].Trim();

                        // 값이 비어있지 않은 경우에만 추가
                        if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                        {
                            mapping[key] = value;
                        }
                    }
                }
            }
            return mapping;
        }
        #endregion
#endif

        // 힙 높이 측정
        private float GetHipsHeight(GameObject model)
        {
            Animator anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();

            // Hips 찾기 (이름 기반)
            Transform hips = model.transform.Find("Hips");
            if (hips == null) hips = model.transform.Find("mixamorig:Hips");
            if (hips == null)
            {
                // 깊이 탐색
                foreach(var t in model.GetComponentsInChildren<Transform>())
                    if(t.name.Contains("Hips") || t.name.Contains("Pelvis")) { hips = t; break; }
            }

            return (hips != null) ? hips.position.y : 0f;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GhostSkeletonDebugRenderer : MonoBehaviour
    {
        private static readonly HumanBodyBones[,] BonePairs =
        {
            { HumanBodyBones.Hips, HumanBodyBones.Spine },
            { HumanBodyBones.Spine, HumanBodyBones.Chest },
            { HumanBodyBones.Chest, HumanBodyBones.Neck },
            { HumanBodyBones.Neck, HumanBodyBones.Head },
            { HumanBodyBones.Chest, HumanBodyBones.LeftUpperArm },
            { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm },
            { HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
            { HumanBodyBones.Chest, HumanBodyBones.RightUpperArm },
            { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm },
            { HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
            { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg },
            { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg },
            { HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
            { HumanBodyBones.Hips, HumanBodyBones.RightUpperLeg },
            { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg },
            { HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot },
        };

        private const float LineWidth = 0.018f;
        private const float RootMarkerHalfSize = 0.16f;
        private const float MinScaleForUncompensatedDebugLines = 0.25f;
        private const float MaxDisplayScaleCompensation = 100f;
        private const float MinLossyScaleForDebugLines = 0.0001f;
        private static readonly Color LineColor = new Color(0.05f, 0.95f, 1f, 0.92f);
        private static readonly Color RootMarkerColor = new Color(1f, 0.85f, 0.05f, 0.95f);

        private readonly List<LineRenderer> boneLines = new List<LineRenderer>();
        private readonly List<LineRenderer> rootMarkerLines = new List<LineRenderer>();
        private Animator animator;
        private Material lineMaterial;
        private bool initialized;
        private bool visible;

        public void SetVisible(bool value)
        {
            visible = value;
            enabled = value;
            EnsureInitialized();
            SetLinesEnabled(value);
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void LateUpdate()
        {
            if (!visible)
            {
                return;
            }

            EnsureInitialized();
            UpdateRootMarker();
            UpdateBoneLines();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }

        private void EnsureInitialized()
        {
            animator = animator != null ? animator : GetComponent<Animator>();
            if (initialized)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            Shader fallbackShader = Shader.Find("Unlit/Color");
            lineMaterial = new Material(shader != null ? shader : fallbackShader);
            lineMaterial.color = LineColor;

            for (int i = 0; i < BonePairs.GetLength(0); i++)
            {
                boneLines.Add(CreateLine($"GhostBoneLine_{i:00}", LineColor));
            }

            for (int i = 0; i < 3; i++)
            {
                rootMarkerLines.Add(CreateLine($"GhostRootMarker_{i:00}", RootMarkerColor));
            }

            initialized = true;
        }

        private LineRenderer CreateLine(string lineName, Color color)
        {
            var lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.material = lineMaterial;
            line.startColor = color;
            line.endColor = color;
            line.enabled = visible;
            return line;
        }

        private void SetLinesEnabled(bool enabledValue)
        {
            foreach (LineRenderer line in boneLines)
            {
                if (line != null)
                {
                    line.enabled = enabledValue;
                }
            }

            foreach (LineRenderer line in rootMarkerLines)
            {
                if (line != null)
                {
                    line.enabled = enabledValue;
                }
            }
        }

        private void UpdateRootMarker()
        {
            Vector3 center = transform.position;
            SetLine(rootMarkerLines[0], center - Vector3.right * RootMarkerHalfSize, center + Vector3.right * RootMarkerHalfSize, true);
            SetLine(rootMarkerLines[1], center - Vector3.up * RootMarkerHalfSize, center + Vector3.up * RootMarkerHalfSize, true);
            SetLine(rootMarkerLines[2], center - Vector3.forward * RootMarkerHalfSize, center + Vector3.forward * RootMarkerHalfSize, true);
        }

        private void UpdateBoneLines()
        {
            for (int i = 0; i < BonePairs.GetLength(0); i++)
            {
                Transform from = GetBone(BonePairs[i, 0]);
                Transform to = GetBone(BonePairs[i, 1]);
                bool hasPair = from != null && to != null;
                SetLine(
                    boneLines[i],
                    hasPair ? GetDebugWorldPosition(from) : Vector3.zero,
                    hasPair ? GetDebugWorldPosition(to) : Vector3.zero,
                    hasPair);
            }
        }

        private Transform GetBone(HumanBodyBones bone)
        {
            if (animator == null || !animator.isHuman)
            {
                return null;
            }

            return animator.GetBoneTransform(bone);
        }

        private Vector3 GetDebugWorldPosition(Transform bone)
        {
            Vector3 center = transform.position;
            float displayScale = CalculateDisplayScaleCompensation(transform.lossyScale);
            return center + (bone.position - center) * displayScale;
        }

        private static float CalculateDisplayScaleCompensation(Vector3 lossyScale)
        {
            float maxScale = Mathf.Max(
                Mathf.Abs(lossyScale.x),
                Mathf.Abs(lossyScale.y),
                Mathf.Abs(lossyScale.z));

            if (maxScale <= MinLossyScaleForDebugLines || maxScale >= MinScaleForUncompensatedDebugLines)
            {
                return 1f;
            }

            return Mathf.Min(MaxDisplayScaleCompensation, 1f / maxScale);
        }

        private static void SetLine(LineRenderer line, Vector3 from, Vector3 to, bool enabledValue)
        {
            if (line == null)
            {
                return;
            }

            line.enabled = enabledValue;
            if (!enabledValue)
            {
                return;
            }

            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }
    }
}
