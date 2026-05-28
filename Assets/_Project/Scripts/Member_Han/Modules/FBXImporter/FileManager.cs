using UnityEngine;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Member_Han.Modules.FileSystem;

namespace Member_Han.Modules.FBXImporter
{
    public class FileManager : MonoBehaviour
    {
        private const string IMPORT_FBX_FOLDER = "Import_FBX";
        private const string FBX_EXTENSION = "fbx";
        private const string BONE_MAPPING_FILE = "BoneMapping_Data.txt";
        private const float GHOST_CONTAINER_SCALE = 0.01f;
        private const float THUMB_PROXIMAL_SAFE_MAX_LOCAL_ANGLE = 30f;
        private const float DEFAULT_THUMB_STRETCH_OFFSET = -0.1f;
        private const float LEGACY_THUMB_PROJECTION_MIN_PALM_NORMAL = 0.36f;
        private const float DEFAULT_THUMB_PROJECTION_MIN_PALM_NORMAL = 0.358f;
        private const string SATISFACTION_REFERENCE_OUTPUT_BASE_NAME = "satisfaction_2";
        private const int SATISFACTION_REFERENCE_MAX_MMD_FRAME = 6000;
        private const float MMD_REFERENCE_FRAME_RATE = 30f;
        private const int MAX_RETARGET_PREWARM_FRAME_COUNT = 120;
#if UNITY_EDITOR
        private const float EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE = 30f;
#endif

        private enum FBXSessionState
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
        public bool saveToImportFolder = false;

        [Header("Ghost Retargeting 설정")]
        [Tooltip("애니메이션을 적용할 대상 캐릭터 (Humanoid Avatar 필요)")]
        public GameObject targetCharacter;

        [Tooltip("이전 수동 프로젝트와 같은 180도 PoseSpace 방향 보정을 사용합니다. 현재 씬에서는 카메라 정면 조건을 깨므로 비교/롤백용으로만 켭니다.")]
        public bool useLegacyPoseSpaceFacingCorrection = false;

        [Tooltip("Main_Auto가 Sub_Manual 직접 Animator 재생처럼 FBX의 body/root 회전을 따르도록 합니다. 끄면 기존처럼 Ghost Transform 기준 회전 보정을 강제합니다.")]
        public bool preserveFbxRootRotation = false;

        [Tooltip("HumanPose bodyPosition copied from FBX can jump on some clips. Keep the target body position stable like the manual Animator path.")]
        public bool preserveRetargetBodyPosition = true;

        [Tooltip("Use FBX HumanPose bodyPosition X/Z delta as target root motion to reduce foot sliding.")]
        public bool useRetargetBodyPositionXZRootMotion = false;

        [Tooltip("When a foot is visually grounded, add a small X/Z root correction to reduce skating.")]
        public bool stabilizeGroundedFootXZ = false;

        [Tooltip("Foot-lock correction strength. Lower values preserve dance motion, higher values reduce skating.")]
        [Range(0f, 1f)] public float GroundedFootLockWeight = 0f;

        [Tooltip("Maximum X/Z root correction per frame for grounded foot lock.")]
        [Range(0.001f, 0.1f)] public float MaxGroundedFootLockStep = 0.025f;

        [Tooltip("Editor 자동 경로에서 Unity가 임포트한 Humanoid clip의 muscle curve를 기준으로 사용합니다. Assimp Ghost 회전 curve가 수동 기준과 다를 때 팔/상체 포즈 차이를 줄이기 위한 안전 경로입니다.")]
        public bool useEditorHumanoidClipMuscleReference = true;

        [Tooltip("Editor-only experimental RootT X/Z root motion reference. Keep disabled until visual_body_arc_jitter passes without increasing jitter.")]
        public bool useEditorHumanoidRootTranslationReference = false;

        [Tooltip("Weight for Editor Humanoid RootT translation reference.")]
        [Range(0f, 1f)] public float editorHumanoidRootTranslationWeight = 0.25f;

        [Tooltip("Current-frame blend for smoothed Editor Humanoid RootT translation delta.")]
        [Range(0.05f, 1f)] public float editorHumanoidRootTranslationCurrentWeight = 0.35f;

        [Tooltip("손가락은 Sub_Manual/testPrefab Animator가 평가한 HumanPose 값을 기준으로 덮어씁니다.")]
        public bool useManualAnimatorFingerPoseReference = true;

        public bool useManualAnimatorFullBodyPoseReference = false;

        [Tooltip("Sub_Manual/testPrefab Animator의 HumanPose bodyRotation을 retarget pose 기준으로 사용해 팔꿈치 bend plane 기준축 차이를 줄입니다.")]
        public bool useManualAnimatorBodyRotationReference = true;

        [Tooltip("preserveRetargetBodyPosition=true 일 때 body Y를 수동 기준 Animator bodyPosition.y로 대체합니다. ghost Legacy-animation bodyPos.y 스파이크 없이 상체 높이를 애니메이션에 맞게 따라가도록 합니다.")]
        public bool useManualAnimatorBodyPositionYReference = true;

        [Tooltip("수동 기준 Animator의 Hips localPosition을 target Hips에 선택적으로 적용해 Main_Auto의 몸통 경로 편차를 A/B 검증합니다. 활성 시 testprefab Hips delta가 YYB에 전달되어 오히려 발 호 궤적이 심해지므로 기본 비활성화합니다.")]
        public bool useManualAnimatorHipsLocalPositionReference = false;

        [Tooltip("수동 기준 Hips localPosition 보정 강도입니다.")]
        [Range(0f, 1f)] public float manualAnimatorHipsLocalPositionWeight = 1f;

        [Tooltip("프레임당 수동 기준 Hips localPosition으로 이동할 수 있는 최대 보정 거리입니다.")]
        [Range(0.001f, 0.2f)] public float manualAnimatorHipsLocalPositionMaxOffset = 0.12f;

        [Tooltip("수동 기준 Animator의 lowest-foot 상승량을 접지 목표 높이에 반영해 Main_Auto가 점프/발 높이 호를 바닥으로 평탄화하지 않도록 합니다.")]
        public bool useManualAnimatorFootHeightGroundingReference = false;

        [Tooltip("수동 기준 lowest-foot 접지 높이 보정 강도입니다.")]
        [Range(0f, 1f)] public float manualAnimatorFootHeightGroundingReferenceWeight = 1f;

        [Tooltip("수동 기준 lowest-foot에서 접지 목표 높이로 반영할 수 있는 최대 양수 상승량입니다.")]
        [Range(0f, 0.12f)] public float manualAnimatorFootHeightGroundingReferenceMaxLift = 0.08f;

        [Tooltip("엄지 체인의 localRotation도 Sub_Manual/testPrefab Animator가 같은 FBX clip에서 평가한 값을 기준으로 덮어씁니다. YYB와 testPrefab의 Humanoid muscle은 같지만 엄지 로컬 축 해석이 달라 보일 때 사용합니다.")]
        public bool useManualAnimatorThumbLocalRotationReference = true;

        [Tooltip("손목 localRotation을 Sub_Manual/testPrefab Animator가 같은 FBX clip에서 평가한 값을 기준으로 덮어씁니다. t13.2 hand pose parity 회귀 보호용입니다.")]
        public bool useManualAnimatorHandLocalRotationReference = true;

        [Tooltip("엄지 세그먼트 방향을 Sub_Manual/testPrefab의 손 기준 방향에 맞춥니다. 모델별 bind axis 차이 때문에 localRotation 숫자가 같아도 손 모양이 달라질 때 사용합니다.")]
        public bool useManualAnimatorThumbSegmentDirectionReference = true;

        [Tooltip("엄지 세그먼트 방향 보정 강도입니다. 1이면 testPrefab 손 기준 방향에 맞추고, 0이면 보정하지 않습니다.")]
        [Range(0f, 1f)] public float manualAnimatorThumbSegmentDirectionWeight = 1f;

        [Tooltip("손바닥 기준 Hand->ThumbIntermediate 방향을 Sub_Manual/testPrefab의 손 기준 방향에 맞춥니다. 엄지 시작 방향이 손바닥 밖으로 탈골된 것처럼 보일 때 사용합니다.")]
        public bool useManualAnimatorThumbHandDirectionReference = true;

        [Tooltip("손바닥 기준 엄지 시작 방향 보정 강도입니다. 1이면 testPrefab의 Hand->ThumbIntermediate 방향에 맞춥니다.")]
        [Range(0f, 1f)] public float manualAnimatorThumbHandDirectionWeight = 1f;

        [Tooltip("손바닥 전체 프레임을 Sub_Manual/testPrefab의 손바닥 방향에 맞춥니다. MMD 기준과 손목/엄지 뿌리 실루엣이 다를 때 사용합니다.")]
        public bool useManualAnimatorHandPalmFrameReference = true;

        [Tooltip("손바닥 프레임 보정 강도입니다. MMD 396프레임 직접 비교 기준으로 기본값은 1.00입니다.")]
        [Range(0f, 1f)] public float manualAnimatorHandPalmFrameWeight = 1f;

        [Tooltip("엄지 첫 본 위치를 Sub_Manual/testPrefab의 손 기준 위치 비율로 맞춥니다. YYB 엄지 시작부가 손바닥 안쪽으로 붙어 보일 때 사용합니다.")]
        public bool useManualAnimatorThumbBasePositionReference = true;

        [Tooltip("엄지 첫 본 위치 보정 강도입니다. 1이면 testPrefab 손 기준 위치 비율을 그대로 적용합니다.")]
        [Range(0f, 1f)] public float manualAnimatorThumbBasePositionWeight = 1f;

        [Tooltip("엄지 첫 본 위치가 원본 위치에서 한 프레임에 벗어날 수 있는 최대 거리입니다.")]
        [Range(0f, 0.03f)] public float manualAnimatorThumbBasePositionMaxOffset = 0.03f;

        [Tooltip("손가락 기준 평가에 사용할 수동 기준 프리팹입니다. 비워두면 VMDRecorderSample testPrefab을 자동으로 찾습니다.")]
        public GameObject manualFingerReferencePrefab;

        [Tooltip("손가락 기준 평가에 사용할 수동 Animator Controller입니다. 비워두면 TestAnimator1_Manual 또는 TestAnimator1을 자동으로 찾습니다.")]
        public RuntimeAnimatorController manualFingerReferenceController;

        [Tooltip("Retarget/녹화 중 MMD4Mecanim의 어깨 PPH 보정을 잠시 끕니다. 수동 기준 모션과 어깨 형태가 달라질 수 있어 기본값은 끕니다.")]
        public bool disableMmdShoulderPostPoseDuringRetarget = false;

        [Header("Anatomical Retarget Guard")]
        [Tooltip("FBX Humanoid muscle 값을 [-1, 1] 범위로 제한합니다.")]
        public bool clampRetargetMusclesToHumanRange = true;

        [Tooltip("팔이 늘어나거나 비정상적으로 비틀리는 Humanoid arm muscle 값을 제한합니다.")]
        public bool enableAnatomicalArmGuard = true;

        [Tooltip("Humanoid 팔 Stretch muscle 허용치입니다. Forearm Stretch는 팔꿈치 굽힘에 가까우므로 기본적으로 제한하지 않습니다.")]
        [Range(0f, 0.5f)] public float ArmStretchMuscleLimit = 0f;

        [Tooltip("Retarget 단계에서 Forearm Stretch muscle을 제한합니다. 팔 길이가 아니라 팔꿈치 굽힘에 가까워 기본값은 꺼야 합니다.")]
        public bool clampRetargetArmStretchMuscles = false;

        [Tooltip("상완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)] public float UpperArmTwistMuscleLimit = 0.75f;

        [Tooltip("전완 Twist muscle 허용치입니다.")]
        [Range(0.1f, 1f)] public float LowerArmTwistMuscleLimit = 0.65f;

        [Tooltip("Retarget 중 Target Humanoid 본의 localPosition을 초기값으로 복구해 팔/다리 길이 변형을 막습니다.")]
        public bool lockTargetHumanoidBonePositions = true;

        [Tooltip("팔/다리 하위의 비-Humanoid 보조본 localPosition도 초기값으로 복구해 소매/팔 메시가 가늘어지는 변형을 막습니다.")]
        public bool lockTargetLimbChildLocalPositions = true;

        [Tooltip("팔/다리 하위의 비-Humanoid 보조본 localRotation을 초기값으로 복구합니다. YYB 소매/팔 twist 보조본 움직임을 막을 수 있어 기본값은 끕니다.")]
        public bool lockTargetLimbChildLocalRotations = false;

        [Tooltip("Target 캐릭터에 HumanoidArmDeformationGuard를 자동 부착해 Retarget 외 경로에서도 팔 가늘어짐을 막습니다.")]
        public bool attachTargetArmDeformationGuard = true;

        [Tooltip("자동 부착된 Target 가드에서 HumanPose arm muscle도 제한합니다. 기본 Retargeter 보정과 중복될 수 있어 기본값은 끕니다.")]
        public bool targetGuardClampAnatomicalArmMuscles = false;

        [Tooltip("자동 부착된 Target 가드도 arm stretch muscle을 제한합니다. 직접 Animator 모션이 굳을 수 있어 기본값은 끕니다.")]
        public bool targetGuardClampArmStretchMuscles = false;

        [Tooltip("팔 변형 가드가 제한/복구를 처음 수행할 때 진단 로그를 출력합니다.")]
        public bool logArmDeformationGuardCorrections = false;

        [Header("Animation Rigging Arm Twist Correction")]
        [Tooltip("YYB 팔 twist 보조본을 Animation Rigging TwistCorrection으로 보정합니다. 현재 자동 리타겟 경로에서는 RigBuilder가 SetHumanPose 포즈를 초기화할 수 있어 기본 비활성화합니다.")]
        public bool enableAnimationRiggingArmTwistCorrection = false;

        [Tooltip("Animation Rigging 팔 twist 보정 전체 영향도입니다.")]
        [Range(0f, 1f)] public float AnimationRiggingArmTwistRigWeight = 0.65f;

        [Tooltip("상완 twist 보조본 영향도입니다.")]
        [Range(0f, 1f)] public float AnimationRiggingUpperArmTwistWeight = 0.45f;

        [Tooltip("전완/손목 twist 보조본 영향도입니다.")]
        [Range(0f, 1f)] public float AnimationRiggingForearmTwistWeight = 0.85f;

        [Tooltip("Animation Rigging 팔 twist 보정 구성 로그를 출력합니다.")]
        public bool logAnimationRiggingArmTwistCorrection = false;

        [Header("YYB Arm Direction Retarget Correction")]
        [Tooltip("실험 옵션입니다. Ghost 팔 세그먼트 방향을 Target YYB 팔 방향에 제한적으로 다시 맞춥니다. 현재는 일부 정상 프레임을 망가뜨릴 수 있어 기본 비활성화합니다.")]
        public bool enableYybArmDirectionRetargetCorrection = false;

        [Tooltip("상완 방향 보정 영향도입니다.")]
        [Range(0f, 1f)] public float YybArmDirectionUpperArmWeight = 0.65f;

        [Tooltip("전완 방향 보정 영향도입니다.")]
        [Range(0f, 1f)] public float YybArmDirectionForearmWeight = 0.75f;

        [Tooltip("상완이 한 프레임에 따라갈 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] public float YybArmDirectionUpperArmMaxDegrees = 65f;

        [Tooltip("전완이 한 프레임에 따라갈 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] public float YybArmDirectionForearmMaxDegrees = 85f;

        [Tooltip("YYB 팔 방향 보정 구성 로그를 출력합니다.")]
        public bool logYybArmDirectionRetargetCorrection = false;

        [Header("YYB Arm Visual Twist Correction")]
        [Tooltip("RigBuilder 없이 YYB 팔/소매 보조본을 전완-손목 회전에 맞춰 직접 분배해 소매가 가늘어 보이는 현상을 줄입니다.")]
        public bool enableYybArmVisualTwistCorrection = true;

        [Tooltip("상완 보조본 회전 분배 영향도입니다.")]
        [Range(0f, 1f)] public float YybArmVisualUpperArmInfluence = 0.35f;

        [Tooltip("전완/손목 보조본 회전 분배 영향도입니다.")]
        [Range(0f, 1f)] public float YybArmVisualForearmInfluence = 0.75f;

        [Tooltip("상완 보조본에 허용할 최대 회전각입니다.")]
        [Range(0f, 120f)] public float YybArmVisualUpperArmMaxDegrees = 45f;

        [Tooltip("전완/손목 보조본에 허용할 최대 회전각입니다.")]
        [Range(0f, 120f)] public float YybArmVisualForearmMaxDegrees = 75f;

        [Tooltip("YYB 팔 시각 보정 구성 로그를 출력합니다.")]
        public bool logYybArmVisualTwistCorrection = false;

        [Header("YYB Arm Anatomical Swing Correction")]
        [Tooltip("손이 몸 밖/어깨 근처에 있는데 상완만 아래로 크게 떨어지는 포즈를 제한합니다.")]
        public bool enableYybArmSwingLimitCorrection = true;

        [Tooltip("상완 하강 제한 보정 강도입니다. MMD VMD export에서 손이 몸을 관통하는 포즈를 줄이기 위해 Main_Auto 기본 경로에서 사용합니다.")]
        [Range(0f, 1f)] public float YybArmSwingLimitWeight = 0.85f;

        [Tooltip("상완 방향이 아래 방향과 이 값보다 더 가까우면 보정 후보로 봅니다. 0.68은 수동 기준 모션과의 차이를 줄이는 균형값입니다.")]
        [Range(0f, 1f)] public float YybArmSwingMaxDownDot = 0.68f;

        [Tooltip("손이 어깨에서 팔 길이 대비 이 비율 이상 옆/앞으로 떨어져 있을 때만 보정합니다. YYB는 몸 가까이에서도 소매가 무너질 수 있어 낮게 둡니다.")]
        [Range(0f, 1.5f)] public float YybArmSwingMinHandHorizontalRatio = 0.05f;

        [Tooltip("손이 어깨보다 팔 길이 대비 이 비율보다 더 낮으면 자연스럽게 내려간 팔로 보고 보정하지 않습니다.")]
        [Range(0f, 1.5f)] public float YybArmSwingMaxHandBelowShoulderRatio = 0.75f;

        [Tooltip("YYB 상완 하강 제한 보정 로그를 출력합니다.")]
        public bool logYybArmSwingLimitCorrection = false;

        [Header("YYB Arm Sleeve Anchor Correction")]
        [Tooltip("YYB 소매/어깨 보조본이 상완 본을 따라가지 않아 소매가 어깨에서 무너져 보이는 현상을 줄입니다.")]
        public bool enableYybArmSleeveAnchorCorrection = true;

        [Tooltip("소매 상단 보조본이 상완 회전을 따라가는 강도입니다.")]
        [Range(0f, 1f)] public float YybArmSleeveAnchorInfluence = 0.85f;

        [Tooltip("어깨 캡 보조본이 상완 회전을 따라가는 강도입니다. MMD4Mecanim PPH와 겹치면 어깨가 둥글게 무너질 수 있어 기본값은 0입니다.")]
        [Range(0f, 1f)] public float YybArmShoulderCapAnchorInfluence = 0f;

        [Tooltip("소매/어깨 보조본이 한 프레임에 따라갈 수 있는 최대 회전각입니다.")]
        [Range(0f, 120f)] public float YybArmSleeveAnchorMaxDegrees = 85f;

        [Tooltip("YYB 소매/어깨 보조본 anchor 보정 로그를 출력합니다.")]
        public bool logYybArmSleeveAnchorCorrection = false;

        [Header("디버그 설정 (RuntimeRetargeter에 적용됨)")]
        [Tooltip("본 매핑 관련 디버그 로그 출력")]
        public bool showBoneMappingLog = false;
        [Tooltip("런타임 애니메이션 디버그 로그 출력")]
        public bool showRuntimeAnimationLog = false;

        [Tooltip("Ghost 모델 보이기 (디버깅용)")]
        public bool showGhostModel = false;

        [Header("Golden Hand Settings")]
        [Tooltip("Finger Stretch Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float FingerStretchScale = 1.0f;
        [Tooltip("Finger Spread Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float FingerSpreadScale = 1.0f;

        [Space(5)]
        [Tooltip("Thumb Stretch Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float ThumbStretchScale = 1.0f;
        [Tooltip("Thumb Spread Scale (Default 1.0)")]
        [Range(0.0f, 1.0f)] public float ThumbSpreadScale = 1.0f;

        [Header("Thumb Anatomical Guard")]
        [Tooltip("YYB 모델에서 엄지 관절이 손바닥 뒤로 꺾이거나 과하게 벌어지는 현상을 막기 위해 엄지 Humanoid muscle만 제한합니다.")]
        public bool enableThumbAnatomicalGuard = true;

        [Tooltip("Manual Animator finger reference를 사용할 때는 엄지 stretch offset을 추가하지 않고 수동 기준 엄지 muscle을 보존합니다.")]
        public bool preserveManualFingerReferenceThumbMuscles = true;

        [Tooltip("엄지 굽힘 muscle 최소값입니다. 값이 너무 낮으면 엄지가 손바닥 안쪽으로 과하게 접힐 수 있습니다.")]
        [Range(-2.5f, 0f)] public float ThumbStretchMin = -2.1f;

        [Tooltip("엄지 굽힘 muscle 최대값입니다. 값이 너무 높으면 엄지가 뒤로 젖혀질 수 있습니다.")]
        [Range(0f, 2.5f)] public float ThumbStretchMax = 1.0f;

        [Tooltip("엄지 벌림 muscle 최소값입니다. 값이 너무 낮으면 엄지가 손 구조상 불가능한 방향으로 벌어질 수 있습니다.")]
        [Range(-1.5f, 0f)] public float ThumbSpreadMin = -0.9f;

        [Tooltip("엄지 벌림 muscle 최대값입니다. 값이 너무 높으면 엄지가 손바닥 바깥 방향으로 과하게 벌어질 수 있습니다.")]
        [Range(0f, 1.5f)] public float ThumbSpreadMax = 0.9f;

        [Tooltip("엄지 해부학적 제한이 실제로 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        public bool logThumbAnatomicalGuardCorrections = false;

        [Tooltip("엄지 muscle 제한 이후에도 YYB 엄지 본이 손 구조상 이상하게 꺾이면, 실제 엄지 본 localRotation을 기준 자세 근처로 제한합니다.")]
        public bool enableThumbLocalRotationGuard = true;

        [Tooltip("Manual Animator finger reference를 사용할 때는 최종 엄지 localRotation 가드를 끄고 수동 기준 손 모양을 우선합니다.")]
        public bool disableThumbLocalRotationGuardWithManualFingerReference = true;

        [Tooltip("엄지 첫 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다. 너무 낮으면 정상적인 엄지 벌림까지 잘려 reference보다 엄지가 덜 펼쳐질 수 있습니다.")]
        [Range(0f, 90f)] public float ThumbProximalMaxLocalAngle = 28f;

        [Tooltip("엄지 두 번째 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] public float ThumbIntermediateMaxLocalAngle = 55f;

        [Tooltip("엄지 끝 관절이 기준 자세에서 벗어날 수 있는 최대 각도입니다.")]
        [Range(0f, 120f)] public float ThumbDistalMaxLocalAngle = 55f;

        [Tooltip("엄지 본 localRotation 제한이 실제로 값을 바꿨을 때 최초 1회 진단 로그를 출력합니다.")]
        public bool logThumbLocalRotationGuardCorrections = false;

        [Tooltip("YYB처럼 스킨용 Thumb0 보조 본과 Humanoid Thumb0M 본이 분리된 모델에서 보조 본을 실제 엄지 본 회전에 맞춰 손바닥/엄지 뿌리 메시 변형을 줄입니다.")]
        public bool syncDetachedThumbBaseHelpers = true;

        [Tooltip("분리된 Thumb0 보조본 위치를 실제 Humanoid Thumb0M 본 쪽으로 제한적으로 맞춥니다. YYB 엄지 뿌리 메시가 손바닥에서 분리되어 보이는 현상을 줄입니다.")]
        public bool syncDetachedThumbBaseHelperPositions = true;

        [Tooltip("분리된 Thumb0 보조 본이 실제 엄지 본을 따라가는 비율입니다. 너무 높으면 엄지 뿌리 스킨이 눌려 짧아 보일 수 있어 YYB 기본값은 제한 추종입니다.")]
        [Range(0f, 1f)] public float detachedThumbBaseHelperSyncWeight = 0.8f;

        [Tooltip("Thumb0 보조본이 실제 엄지 구동본을 따라갈 수 있는 최대 각도입니다. 낮출수록 손꿈치 스킨을 덜 움직입니다.")]
        [Range(0f, 45f)] public float detachedThumbBaseHelperMaxLocalAngle = 28f;

        [Tooltip("Thumb0 보조본 위치가 기본 손바닥 앵커에서 벗어날 수 있는 최대 거리입니다. 높이면 실제 엄지 구동본 위치를 더 따르고, 낮추면 손바닥 실루엣을 더 보존합니다.")]
        [Range(0f, 0.02f)] public float detachedThumbBaseHelperMaxPositionOffset = 0.008f;

        [Tooltip("YYB처럼 joint_*Thumb0 보조본과 !joint_*Thumb0M 실제 엄지 구동본의 로컬 기준축이 어긋난 모델에서, 왼손 Thumb0 보조본이 source delta를 자기 축에 맞게 다시 해석하도록 추가 축 보정을 적용합니다.")]
        public Vector3 LeftDetachedThumbBaseHelperDeltaAxisOffset = Vector3.zero;

        [Tooltip("YYB처럼 joint_*Thumb0 보조본과 !joint_*Thumb0M 실제 엄지 구동본의 로컬 기준축이 어긋난 모델에서, 오른손 Thumb0 보조본이 source delta를 자기 축에 맞게 다시 해석하도록 추가 축 보정을 적용합니다.")]
        public Vector3 RightDetachedThumbBaseHelperDeltaAxisOffset = Vector3.zero;

        [Tooltip("YYB처럼 Thumb0 보조본 기본 자세가 넓게 벌어진 모델에서, 왼손 Thumb0 helper 목표 회전에 정적 보정치를 직접 더해 webbing 벌어짐/각짐을 줄입니다.")]
        public Vector3 LeftDetachedThumbBaseHelperTargetRotationOffset = Vector3.zero;

        [Tooltip("YYB처럼 Thumb0 보조본 기본 자세가 넓게 벌어진 모델에서, 오른손 Thumb0 helper 목표 회전에 정적 보정치를 직접 더해 webbing 벌어짐/각짐을 줄입니다.")]
        public Vector3 RightDetachedThumbBaseHelperTargetRotationOffset = Vector3.zero;

        [Tooltip("YYB 손꿈치/엄지 뿌리 스킨용 Thumb0 보조본을 기본 손바닥 자세 쪽으로 안정화합니다. 엄지 움직임보다 손바닥 실루엣 보존을 우선합니다.")]
        public bool stabilizeDetachedThumbBasePalm = false;

        [Tooltip("Thumb0 보조본을 기본 자세로 되돌리는 강도입니다. 높이면 손꿈치가 고정되고, 낮추면 엄지 뿌리가 실제 엄지 구동본을 더 따라갑니다.")]
        [Range(0f, 1f)] public float detachedThumbBasePalmStabilizeWeight = 0f;

        [Tooltip("손꿈치 안정화 상태에서 Thumb0 보조본이 기본 자세에서 벗어날 수 있는 최대 각도입니다. 낮을수록 엄지 뿌리 메시가 덜 벌어집니다.")]
        [Range(0f, 45f)] public float detachedThumbBasePalmMaxLocalAngle = 45f;

        [Tooltip("YYB 엄지와 손바닥 경계선이 딱딱하게 찢겨 보일 때 Thumb0 보조본을 기본 손바닥 웹빙 형태 쪽으로 약하게 안정화합니다.")]
        public bool stabilizeThumbWebbingCrease = true;

        [Tooltip("엄지 웹빙 라인을 안정화하는 강도입니다. 높이면 엄지-손바닥 경계가 덜 찢기지만 엄지 뿌리 움직임이 둔해질 수 있습니다.")]
        [Range(0f, 1f)] public float thumbWebbingCreaseStabilizeWeight = 0.35f;

        [Tooltip("엄지 웹빙 안정화 상태에서 Thumb0 보조본이 기본 자세에서 벗어날 수 있는 최대 회전각입니다.")]
        [Range(0f, 45f)] public float thumbWebbingCreaseMaxLocalAngle = 18f;

        [Tooltip("엄지 웹빙 안정화 상태에서 Thumb0 보조본 위치가 기본 위치에서 벗어날 수 있는 최대 거리입니다.")]
        [Range(0f, 0.02f)] public float thumbWebbingCreaseMaxPositionOffset = 0.005f;

        [Tooltip("엄지가 손바닥 normal 방향으로 너무 서거나 중간 관절이 과하게 말려 화면상 짧아 보이는 현상을 줄입니다.")]
        public bool enableThumbVisualLengthGuard = true;

        [Tooltip("엄지 첫 마디가 손바닥 normal 방향으로 최소한 앞으로 나와야 하는 성분입니다. 너무 낮으면 엄지가 손바닥 뒤로 누운 것처럼 보입니다.")]
        [Range(0f, 1f)] public float ThumbProjectionMinPalmNormal = DEFAULT_THUMB_PROJECTION_MIN_PALM_NORMAL;

        [Tooltip("엄지 첫 마디가 손바닥 normal 방향으로 나갈 수 있는 최대 성분입니다. 너무 높으면 카메라 정면에서 엄지가 짧게 보입니다.")]
        [Range(0f, 1f)] public float ThumbProjectionMaxPalmNormal = 0.58f;

        [Tooltip("엄지 첫 마디 투영 보정 강도입니다.")]
        [Range(0f, 1f)] public float ThumbProjectionGuardWeight = 1f;

        [Tooltip("엄지 첫 마디와 검지 시작 방향 사이의 최대 벌어짐 각도입니다. 높으면 엄지가 손바닥 바깥으로 과하게 벌어질 수 있습니다.")]
        [Range(0f, 90f)] public float ThumbIndexMaxSpreadAngle = 70f;

        [Tooltip("엄지-검지 벌어짐 제한 강도입니다.")]
        [Range(0f, 1f)] public float ThumbIndexSpreadGuardWeight = 1f;

        [Tooltip("엄지 첫 마디와 둘째 마디 사이 허용 굽힘 각도입니다. 이 값을 넘으면 끝 마디를 더 펴서 짧아 보이는 현상을 줄입니다.")]
        [Range(0f, 60f)] public float ThumbMaxSegmentBendAngle = 10f;

        [Tooltip("엄지 둘째 마디를 첫 마디 방향에 맞춰 펴는 강도입니다.")]
        [Range(0f, 1f)] public float ThumbSegmentStraightenWeight = 0.9f;

        [Header("Recording Orchestration")]
        [Tooltip("FBX 로드 후 녹화 시작까지의 대기 시간 (초)")]
        [Range(0f, 10f)] public float startDelay = 3.0f;

        [Tooltip("녹화 시작 직전에 clip time 0을 고정 샘플링해 retarget/grounding 첫 프레임을 안정화하는 프레임 수입니다.")]
        [Range(0, MAX_RETARGET_PREWARM_FRAME_COUNT)] public int RetargetPrewarmFrameCount = 6;

        [Tooltip("VMD 저장이 성공하면 추가로 이 폴더에도 같은 VMD 파일을 복사합니다. 비워두면 복사하지 않습니다. (예: C:/Users/flzhv/Desktop/MMD/MikuMikuDance_v932x64/SaveFile)")]
        public string additionalVmdCopyFolder = "";

        [Tooltip("비교 CSV/프레임 캡처 Probe를 켭니다. 일반 변환에서는 미세 멈춤을 줄이기 위해 끄고, 회귀 테스트 때만 켭니다.")]
        public bool enableRecordingDiagnostics = false;

        [Tooltip("회귀 테스트 때 녹화 중 Unity 시간을 30fps로 고정합니다. 일반 GameView 재생에서는 배속/멈칫 체감이 생길 수 있어 끕니다.")]
        public bool useDeterministicCaptureFramerateForDiagnostics = false;

        [Tooltip("Recording Diagnostics를 켰을 때 손 close-up 캡처도 함께 남깁니다.")]
        public bool enableDiagnosticFingerCloseups = true;

        [Tooltip("Editor smoke에서 MotionComparisonProbe 엄지 리스크가 임계치를 넘으면 VMD 저장 성공도 smoke 실패로 승격합니다.")]
        public bool failEditorSmokeOnThumbRisk = true;

        [Tooltip("Editor smoke에서 일반 FBX용 엄지 해부학 리스크의 허용 최대값입니다. 이 값을 넘으면 smoke를 실패로 봅니다.")]
        [Range(0f, 1f)] public float editorSmokeMaxGenericThumbAnatomyRisk = 0.4f;

        [Tooltip("Editor smoke에서 YYB 전용 변형 리스크의 허용 최대값입니다. YYB 타깃일 때만 사용합니다.")]
        [Range(0f, 1f)] public float editorSmokeMaxYybDeformationRisk = 0.35f;

        [Header("Target Idle Pose Guard")]
        [Tooltip("Play 진입과 FBX 선택 전 대기 상태에서 타깃 캐릭터가 카메라를 바라보도록 고정합니다.")]
        public bool faceTargetToCameraOnIdle = true;

        [Tooltip("FBX가 들어오기 전 타깃 Animator Controller를 분리해 기본 모션이 재생되지 않도록 합니다.")]
        public bool detachTargetAnimatorControllerOnIdle = true;

        [Tooltip("FBX가 들어오기 전 타깃 캐릭터의 시작 자세를 매 프레임 복구합니다.")]
        public bool lockTargetPoseUntilImport = true;

        [Header("Visual Jitter Guard")]
        [Tooltip("Editor/GameView 프레임이 밀려도 Ghost clip time이 한 프레임에 크게 건너뛰지 않게 제한합니다.")]
        public bool clampRetargetVisualClipStep = false;

        [Tooltip("Ghost clip time이 한 렌더 프레임에 전진할 수 있는 기준 FPS입니다. 30이면 한 번에 1/30초 이상 건너뛰지 않습니다.")]
        [Range(15f, 120f)] public float RetargetVisualClipFrameRate = 30f;

        [Tooltip("프레임 지연으로 retarget pose가 한 번에 크게 바뀔 때 애니메이션 시간은 보존하고 target pose만 부드럽게 따라가게 합니다.")]
        public bool smoothRetargetPoseOnVisualStepSpike = true;

        [Tooltip("pose spike smoothing 때 현재 FBX pose를 반영하는 비율입니다. 1에 가까울수록 원본 모션을 더 보존하고, 낮을수록 pop을 더 줄입니다.")]
        [Range(0.1f, 1f)] public float RetargetPoseVisualSpikeCurrentWeight = 0.65f;

        [Tooltip("이 값보다 큰 muscle delta가 발생하면 frame-time spike가 아니어도 pose smoothing을 적용합니다.")]
        [Range(0.05f, 1f)] public float RetargetPoseVisualMuscleDeltaThreshold = 0.35f;

        [Header("Final Tuning")]
        [Tooltip("높이 보정 (미터 단위). 0.02 = 2cm 올림")]
        [Range(-0.5f, 0.5f)] public float HeightOffset = 0.0f;

        [Tooltip("보폭 비율 (1.0 = 자동, 미끄러지면 조절)")]
        [Range(0f, 1.2f)] public float MovementScaleMultiplier = 1.0f;

        [Header("Root Motion Spike Guard")]
        [Tooltip("FBX root delta가 한 프레임에 과도하게 튀면 순간이동으로 보고 해당 프레임의 추가 root 이동을 무시합니다.")]
        public bool clampRetargetRootDeltaSpikes = true;

        [Tooltip("한 프레임에 허용할 최대 root 이동량입니다. 일반 춤 동작보다 훨씬 큰 값만 순간이동 후보로 처리합니다.")]
        [Range(0.01f, 1.0f)] public float MaxRetargetRootDeltaPerFrame = 0.25f;

        [Tooltip("root delta spike를 무시했을 때 최초 1회 진단 로그를 출력합니다.")]
        public bool logRetargetRootDeltaSpikes = false;

        [Header("Hips Local Position Spike Guard")]
        [Tooltip("Target Hips localPosition outliers are clamped per frame to prevent one-frame body teleport artifacts in exported VMD.")]
        public bool clampRetargetHipsLocalPositionSpikes = false;

        [Tooltip("Maximum allowed target Hips localPosition movement per frame before it is treated as a visual teleport artifact.")]
        [Range(0.005f, 0.25f)] public float MaxRetargetHipsLocalPositionDeltaPerFrame = 0.02f;

        [Header("Grounding Stability Guard")]
        [Tooltip("발바닥 접지 보정이 한 프레임에 크게 튀지 않도록 부드럽게 반영합니다.")]
        public bool smoothRetargetGrounding = true;

        [Tooltip("한 프레임에 허용할 최대 수직 접지 보정값입니다.")]
        [Range(0.001f, 0.2f)] public float MaxGroundingVerticalStepPerFrame = 0.01f;

        [Tooltip("접지 보정 목표값을 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)] public float GroundingSmoothing = 0.25f;

        [Tooltip("이 값보다 작은 발바닥 떨림은 무시합니다.")]
        [Range(0f, 0.05f)] public float GroundingDeadZone = 0.005f;

        [Tooltip("초기 접지 확정 뒤에 타깃 root Y를 고정합니다. MMD VMD export에서는 이후 프레임의 발 빠짐을 막기 위해 기본 비활성화합니다.")]
        public bool FreezeRootYAfterInitialGrounding = false;

        [Tooltip("전체 renderer bounds 하단이 발바닥 추정치에서 과하게 멀어지면 옷/머리카락/소매 outlier로 보고 접지 기준에서 제외합니다.")]
        public bool rejectRendererGroundingOutliers = true;

        [Tooltip("renderer bounds 하단과 발바닥 추정치 사이에 허용할 최대 거리입니다. 이 값을 넘으면 foot 기준 접지로 되돌립니다.")]
        [Range(0.02f, 0.3f)] public float MaxRendererFootGroundingSeparation = 0.12f;

        [Tooltip("최종 메시 bounds 접지 보정의 작은 잔여 오차를 부드럽게 반영해 모델 전체 떨림을 줄입니다.")]
        public bool smoothLateVisualGroundingCorrection = true;

        [Tooltip("Late visual grounding 잔여 오차가 이 값보다 작으면 smoothing 대상으로 봅니다. 큰 오차는 공중 부유 방지를 위해 즉시 보정합니다.")]
        [Range(0.005f, 0.1f)] public float LateVisualGroundingSnapThreshold = 0.03f;

        [Tooltip("작은 late visual grounding 잔여 오차를 현재 위치에 반영하는 비율입니다.")]
        [Range(0f, 1f)] public float LateVisualGroundingSmoothing = 0.25f;

        [Tooltip("작은 late visual grounding smoothing 보정이 한 프레임에 움직일 수 있는 최대 Y 이동량입니다.")]
        [Range(0.001f, 0.05f)] public float MaxLateVisualGroundingStepPerFrame = 0.003f;

        [Header("Thumb Digital Orthopedics (Offset)")]
        [Tooltip("양손 공통 엄지 회전 Offset입니다. YYB 엄지 기준축이 수동 기준과 다를 때 최종 렌더 포즈의 엄지 첫 관절 기준축을 보정합니다.")]
        public Vector3 ThumbRotationOffset = new Vector3(-10f, -30f, 0f);

        [Tooltip("오른손 엄지에는 공통 Offset의 Y/Z축을 반전해 좌우 mirror 축 차이를 보정합니다.")]
        public bool mirrorRightThumbRotationOffset = true;

        [Tooltip("왼손 엄지에 공통 Offset 이후 추가로 더하는 회전 Offset입니다.")]
        public Vector3 LeftThumbRotationOffset = Vector3.zero;

        [Tooltip("오른손 엄지에 공통 Offset 이후 추가로 더하는 회전 Offset입니다.")]
        public Vector3 RightThumbRotationOffset = Vector3.zero;

        [Tooltip("켜면 오래된 씬에서 ThumbStretchOffset이 0으로 저장되어 있어도 기본 보정값(-0.1)을 사용합니다.")]
        public bool useDefaultThumbStretchOffsetWhenUnset = true;

        [Tooltip("Muscle Offset (Stretch). Default: -0.1")]
        [Range(-0.5f, 0.5f)] public float ThumbStretchOffset = DEFAULT_THUMB_STRETCH_OFFSET;

        [Header("Smart Curve (Dynamics)")]
        public bool EnableSmartCurve = true;
        [Tooltip("Standard Finger Dampen Strength (0.1 ~ 0.5)")]
        [Range(0.0f, 1.0f)] public float SmartCurveStrength = 0.5f;
        public float StretchThreshold = 0.7f;

        public bool EnableThumbSmartCurve = true;
        [Range(0.0f, 1.0f)] public float ThumbSmartCurveStrength = 0.5f;

        [Space(10)]

        #endregion

        #region Private 필드
        private IFileBrowserService _fileBrowserService;
        private RuntimeFBXImporter _fbxImporter;
        private bool _isProcessing;
        private GameObject _activeGhostContainer;
        private HumanoidSampleCode _activeRecorderController;
        private PoseSpaceRetargeter _activeRetargeter;
        private readonly List<TransformSnapshot> _targetIdlePose = new List<TransformSnapshot>();
        private readonly List<BooleanFieldSnapshot> _retargetBooleanSnapshots = new List<BooleanFieldSnapshot>();
        private RuntimeAnimatorController _cachedTargetController;
        private bool _hasCachedTargetController;
        private bool _idlePoseInitialized;
#if UNITY_EDITOR
        private bool _editorSmokeRecordingOverrideActive;
        private int _editorSmokeTargetFrameCount;
        private float _editorSmokeDurationSeconds;
        private float[] _editorSmokeSampleTimesOverride;
        private bool _editorSmokeRestoreSettingsActive;
        private bool _editorSmokePreviousEnableRecordingDiagnostics;
        private bool _editorSmokePreviousEnableDiagnosticFingerCloseups;
        private bool _editorSmokePreviousUseDeterministicCaptureFramerate;
        private float _editorSmokePreviousStartDelay;
        private EditorDiagnosticSmokeSegment _editorSmokeSegment;
        private string _editorSmokeCurrentFbxFileName;
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

        public bool IsProcessing => _isProcessing;
        public event Action<string, VmdSaveResult> EditorDiagnosticSmokeFinished;
#endif

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
                return useManualAnimatorFingerPoseReference &&
                    useManualAnimatorThumbLocalRotationReference &&
                    preserveManualFingerReferenceThumbMuscles &&
                    ShouldSuppressFinalThumbGuardsWithManualReference;
            }
        }

        private bool ShouldSuppressFinalThumbGuardsWithManualReference
        {
            get
            {
                if (!useManualAnimatorFingerPoseReference)
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
            float[] sampleTimesOverride = null)
        {
            if (_isProcessing)
            {
                Debug.LogWarning("[FileManager] 다른 FBX 처리가 진행 중이라 smoke 진단을 시작하지 않았습니다.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(fbxFileName))
            {
                Debug.LogError("[FileManager] smoke 진단 FBX 파일명이 비어 있습니다.");
                return false;
            }

            string sourcePath = ResolveEditorSmokeFbxPath(fbxFileName);
            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"[FileManager] smoke 진단 FBX를 찾을 수 없습니다: {sourcePath}");
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

            Debug.Log(
                $"[FileManager] Editor smoke 진단 시작: FBX={Path.GetFileName(sourcePath)}, " +
                $"duration={safeDuration:F2}s, targetFrameCount={safeTargetFrameCount}, " +
                $"segment={GetEditorSmokeSegmentLabel(segment)}, diagnostics={enableDiagnostics}");
            LogEditorSmokeThumbState("smoke-start-before-process");

            ProcessFBXAsync(sourcePath);
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

        private static string BuildEditorSmokeOutputBaseName(string outputBaseName, float durationSeconds, EditorDiagnosticSmokeSegment segment)
        {
            string cleanBaseName = SanitizeFileName(
                string.IsNullOrWhiteSpace(outputBaseName) ? "fbxToVMD" : outputBaseName);
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

        private static float CalculateEditorSmokeStartTime(AnimationClip clip, float requestedDuration, EditorDiagnosticSmokeSegment segment)
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

        private static string GetEditorSmokeSegmentLabel(EditorDiagnosticSmokeSegment segment)
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
            _editorSmokePreviousEnableRecordingDiagnostics = enableRecordingDiagnostics;
            _editorSmokePreviousEnableDiagnosticFingerCloseups = enableDiagnosticFingerCloseups;
            _editorSmokePreviousUseDeterministicCaptureFramerate = useDeterministicCaptureFramerateForDiagnostics;
            _editorSmokePreviousStartDelay = startDelay;
            _editorSmokeRestoreSettingsActive = true;
        }

        private void ClearEditorSmokeOverride()
        {
            if (_editorSmokeRestoreSettingsActive)
            {
                enableRecordingDiagnostics = _editorSmokePreviousEnableRecordingDiagnostics;
                enableDiagnosticFingerCloseups = _editorSmokePreviousEnableDiagnosticFingerCloseups;
                useDeterministicCaptureFramerateForDiagnostics = _editorSmokePreviousUseDeterministicCaptureFramerate;
                startDelay = _editorSmokePreviousStartDelay;
            }

            _editorSmokeRecordingOverrideActive = false;
            _editorSmokeTargetFrameCount = 0;
            _editorSmokeDurationSeconds = 0f;
            _editorSmokeSampleTimesOverride = null;
            _editorSmokeRestoreSettingsActive = false;
            _editorSmokeSegment = EditorDiagnosticSmokeSegment.Head;
            _editorSmokeCurrentFbxFileName = null;
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
            Debug.Log("[FileManager] Editor smoke batch advance reset: target idle 상태를 다음 FBX 시작 전 다시 고정합니다.");
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

        private static bool TryBuildKnownMmdReferenceRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            recordingLengthSeconds = clipLengthSeconds;
            targetFrameCount = 0;
            playbackSpeed = 1f;

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
            if (!string.Equals(cleanBaseName, SATISFACTION_REFERENCE_OUTPUT_BASE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int referenceFrameCount = SATISFACTION_REFERENCE_MAX_MMD_FRAME + 1;
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

        private static bool TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
            string outputBaseName,
            float clipLengthSeconds,
            float requestedDurationSeconds,
            int requestedTargetFrameCount,
            float recordingFrameRate,
            out float recordingLengthSeconds,
            out int targetFrameCount,
            out float playbackSpeed)
        {
            recordingLengthSeconds = requestedDurationSeconds;
            targetFrameCount = requestedTargetFrameCount;
            playbackSpeed = 1f;

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

        private struct TransformSnapshot
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;

            public TransformSnapshot(Transform transform)
            {
                Transform = transform;
                LocalPosition = transform.localPosition;
                LocalRotation = transform.localRotation;
                LocalScale = transform.localScale;
            }
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
            InitializeTargetIdlePoseGuard();
        }

        private void LateUpdate()
        {
            if (!_isProcessing)
            {
                ApplyTargetIdlePoseGuard();
            }
        }

        private void OnDestroy()
        {
            RestoreMmdPostPoseCorrectionForRetarget();
            RestoreTargetAnimatorController();
        }
        #endregion

        #region 초기화
        private void InitializeServices()
        {
            // 파일 브라우저 서비스 초기화 (StandaloneFileBrowser 사용)
            _fileBrowserService = new RuntimeFileBrowserService();

            // 런타임 FBX 임포터 초기화 (Assimp 사용)
            _fbxImporter = new RuntimeFBXImporter();
        }

        private void EnsureServicesInitialized()
        {
            if (_fileBrowserService == null || _fbxImporter == null)
            {
                InitializeServices();
            }
        }
        #endregion


        #region 이벤트 핸들러
        public void OnClickImportButton()
        {
            if (HandleImportButtonClick()) return;

            string[] paths = _fileBrowserService.OpenFilePanel("Import FBX", "", FBX_EXTENSION, false);
            if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                string sourcePath = paths[0];
                Debug.Log($"선택된 파일: {sourcePath}");

                // 비동기 실행
                ProcessFBXAsync(sourcePath);
            }
        }

        /// <summary>
        /// Import_FBX 폴더에 있는 FBX 파일 목록 로드
        /// 에디터와 빌드 환경 모두에서 작동
        /// </summary>
        public void OnClickLoadFromImportFolder()
        {
            if (HandleLoadFromImportFolderClick()) return;

            string targetDir = Path.Combine(Application.dataPath, "Resources", IMPORT_FBX_FOLDER);

            if (!Directory.Exists(targetDir))
            {
                Debug.LogWarning($"Import_FBX 폴더가 존재하지 않습니다: {targetDir}");
                return;
            }

            string[] fbxFiles = Directory.GetFiles(targetDir, "*.fbx", SearchOption.TopDirectoryOnly);

            if (fbxFiles.Length == 0)
            {
                Debug.LogWarning("Import_FBX 폴더에 FBX 파일이 없습니다.");
                return;
            }

            // 첫 번째 FBX 파일 사용
            string selectedFile = fbxFiles[0];
            Debug.Log($"Import_FBX 폴더에서 로드: {Path.GetFileName(selectedFile)}");

            // 비동기 실행
            ProcessFBXAsync(selectedFile);
        }

        private bool HandleImportButtonClick()
        {
            if (_isProcessing)
            {
                SetSessionState(FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return true;
            }

            SetSessionState(FBXSessionState.Idle, "FBX 파일 선택 대기", 0f);
            string[] paths = _fileBrowserService.OpenFilePanel("Import FBX", "", FBX_EXTENSION, false);
            if (paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
            {
                SetSessionState(FBXSessionState.Cancelled, "파일 선택이 취소되었습니다.", 0f);
                return true;
            }

            string sourcePath = paths[0];
            Debug.Log($"[FileManager] 선택된 파일: {sourcePath}");
            ProcessFBXAsync(sourcePath);
            return true;
        }

        private bool HandleLoadFromImportFolderClick()
        {
            if (_isProcessing)
            {
                SetSessionState(FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return true;
            }

            string targetDir = GetControlledImportDirectory();
            if (!Directory.Exists(targetDir))
            {
                SetSessionState(FBXSessionState.Failed, $"Import_FBX 폴더가 없습니다: {targetDir}", 0f);
                return true;
            }

            string[] fbxFiles = Directory.GetFiles(targetDir, "*.fbx", SearchOption.TopDirectoryOnly);
            if (fbxFiles.Length == 0)
            {
                SetSessionState(FBXSessionState.Failed, "Import_FBX 폴더에 FBX 파일이 없습니다.", 0f);
                return true;
            }

            string selectedFile = fbxFiles[0];
            Debug.Log($"[FileManager] Import_FBX 폴더에서 로드: {Path.GetFileName(selectedFile)}");
            ProcessFBXAsync(selectedFile);
            return true;
        }

        private void InitializeTargetIdlePoseGuard()
        {
            if (targetCharacter == null)
            {
                return;
            }

            DetachTargetAnimatorControllerForIdle();

            if (faceTargetToCameraOnIdle)
            {
                FaceTargetCharacterToCamera(targetCharacter);
            }

            CaptureTargetIdlePose();
            ApplyTargetIdlePoseGuard();
        }

        private void DetachTargetAnimatorControllerForIdle()
        {
            if (!detachTargetAnimatorControllerOnIdle || targetCharacter == null)
            {
                return;
            }

            Animator targetAnimator = targetCharacter.GetComponent<Animator>();
            if (targetAnimator == null)
            {
                return;
            }

            if (!_hasCachedTargetController)
            {
                _cachedTargetController = targetAnimator.runtimeAnimatorController;
                _hasCachedTargetController = true;
            }

            if (targetAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.runtimeAnimatorController = null;
            }

            targetAnimator.applyRootMotion = false;
            targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        private void RestoreTargetAnimatorController()
        {
            if (!_hasCachedTargetController || targetCharacter == null)
            {
                return;
            }

            Animator targetAnimator = targetCharacter.GetComponent<Animator>();
            if (targetAnimator != null && targetAnimator.runtimeAnimatorController == null)
            {
                targetAnimator.runtimeAnimatorController = _cachedTargetController;
            }
        }

        private void CaptureTargetIdlePose()
        {
            _targetIdlePose.Clear();

            if (targetCharacter == null)
            {
                _idlePoseInitialized = false;
                return;
            }

            foreach (Transform targetTransform in targetCharacter.GetComponentsInChildren<Transform>(true))
            {
                _targetIdlePose.Add(new TransformSnapshot(targetTransform));
            }

            _idlePoseInitialized = _targetIdlePose.Count > 0;
        }

        private void ApplyTargetIdlePoseGuard()
        {
            if (!lockTargetPoseUntilImport || !_idlePoseInitialized)
            {
                return;
            }

            DetachTargetAnimatorControllerForIdle();

            foreach (TransformSnapshot snapshot in _targetIdlePose)
            {
                if (snapshot.Transform == null)
                {
                    continue;
                }

                snapshot.Transform.localPosition = snapshot.LocalPosition;
                snapshot.Transform.localRotation = snapshot.LocalRotation;
                snapshot.Transform.localScale = snapshot.LocalScale;
            }
        }

        private void FaceTargetCharacterToCamera(GameObject targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            Camera targetCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (targetCamera == null)
            {
                targetObject.transform.rotation = Quaternion.identity;
                return;
            }

            Vector3 directionToCamera = targetCamera.transform.position - targetObject.transform.position;
            directionToCamera.y = 0f;

            if (directionToCamera.sqrMagnitude < 0.0001f)
            {
                targetObject.transform.rotation = Quaternion.identity;
                return;
            }

            targetObject.transform.rotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up);
        }
        #endregion

        #region 파일 처리 로직
        private async void ProcessFBXAsync(string sourcePath)
        {
            if (_isProcessing)
            {
                SetSessionState(FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return;
            }

            if (await ProcessFBXSessionAsync(sourcePath)) return;

            try
            {
                string fileName = Path.GetFileName(sourcePath);
                string targetPath = sourcePath; // 기본값: 원본 경로 사용

                // 파일 복사
                if (saveToImportFolder)
                {
                    string targetDir = Path.Combine(Application.dataPath, "Resources", IMPORT_FBX_FOLDER);
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string potentialTargetPath = Path.Combine(targetDir, fileName);
                    bool isNewFile = Path.GetFullPath(sourcePath) != Path.GetFullPath(potentialTargetPath);

                    if (isNewFile)
                    {
                        File.Copy(sourcePath, potentialTargetPath, true);
                        targetPath = potentialTargetPath;
                    }
                    else
                    {
                        targetPath = potentialTargetPath;
                    }

#if UNITY_EDITOR
                    if (isNewFile || File.Exists(targetPath))
                    {
                         // 에디터 환경이면 ImportSettings 적용 (선택 사항)
                         if (ShouldConfigureEditorImportSettings(sourcePath, targetPath, Application.dataPath))
                         {
                             ConfigureImportSettings(targetPath);
                         }
                         else
                         {
                             Debug.Log($"[FileManager] Controlled Import_FBX importer preserved: {targetPath}");
                         }
                    }
#endif
                }

                // Assimp-net을 사용하여 FBX 로드
                Debug.Log($"[FileManager] FBX 로드 시작: {targetPath}");
                GameObject importedModel = await _fbxImporter.ImportAsync(targetPath);

                if (importedModel != null)
                {
                    // Ghost Renderer 제어
                    foreach (var renderer in importedModel.GetComponentsInChildren<Renderer>())
                    {
                        renderer.enabled = showGhostModel; // Ghost 모델 보이기/숨기기
                    }

                    // Ghost Container Pattern (스케일 방어막)
                    // Legacy Animation이 실행되면 자식(importedModel)의 Scale은 무조건 1.0으로 강제 원복
                    // 이를 방어하기 위해 부모(Container)에서 0.01로 눌러버리는 구조가 필수
                    Debug.Log($"[System] 🛡️ Activating Ghost Container... (Scale Lock: 0.01)");

                    // 컨테이너 생성
                    GameObject ghostContainer = new GameObject($"GhostContainer_{importedModel.name}");
                    ghostContainer.transform.position = Vector3.zero;
                    ghostContainer.transform.rotation = Quaternion.identity;

                    // 컨테이너 스케일 고정 (0.01)
                    ghostContainer.transform.localScale = Vector3.one * 0.01f;

                    // 모델 종속시키기
                    importedModel.transform.SetParent(ghostContainer.transform, false);

                    // 매핑 데이터 로드 & Ghost 아바타 생성
                    // Avatar는 자식(importedModel)을 기준으로 bake.
                    var boneMapping = LoadBoneMappingRuntime();
                    HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);

                    // Ghost에서 애니메이션 클립 추출
                    AnimationClip targetClip = null;
                    var animComp = importedModel.GetComponent<Animation>();

                    if (animComp != null && animComp.clip != null)
                    {
                        targetClip = animComp.clip;

                        if (targetClip.length > 1000f)
                        {
                            Debug.LogError("애니메이션 시간이 비정상적으로 깁니다. RuntimeFBXImporter의 timeScale을 확인하세요.");
                        }
                    }
                    else
                    {
                        Debug.LogError("Ghost에 Animation 컴포넌트가 없거나 클립이 없습니다!");
                        return;
                    }


                    // Target 찾기
                    GameObject targetObject = this.targetCharacter;

                    if (targetObject == null)
                    {
                        Debug.LogError("Target Character가 할당되지 않았습니다! 인스펙터에서 'Target Character' 슬롯을 확인하세요.");
                        return; // 실행 중단
                    }

                    // 완전 초기화 (원점, 회전 0)
                    targetObject.transform.position = Vector3.zero;
                    targetObject.transform.rotation = Quaternion.identity;

                    // Animator의 Root Motion 옵션은 끕니다.
                    Animator anim = targetObject.GetComponent<Animator>();
                    if (anim != null) anim.applyRootMotion = false;

                    // IKControl 간섭 제거
                    var ikControl = targetObject.GetComponent<IKControl>();
                    if (ikControl != null)
                    {
                        Debug.Log("Hostile IKControl detected. Destroying...");
                        Destroy(ikControl);
                    }

                    // 포즈 공간 리타겟터 부착 및 초기화
                    var retargeter = importedModel.AddComponent<PoseSpaceRetargeter>();
                    _activeRetargeter = retargeter;

                    // FileManager 자신(this)을 넘겨서 설정을 공유함
                    retargeter.Initialize(importedModel, targetObject, boneMapping, targetClip, this);
                    if (anim != null)
                    {
                        ConfigureTargetThumbDeformationGuard(targetObject, anim, retargeter);
                    }

                    // 지연 녹화 시퀀스 시작
                    var ghostAnim = importedModel.GetComponent<Animation>();
                    if (ghostAnim != null) ghostAnim.Stop(); // 즉시 재생 방지

                    StartCoroutine(StartRecordingSequence(
                        importedModel,
                        ghostAnim,
                        targetObject,
                        targetClip,
                        retargeter,
                        Path.GetFileNameWithoutExtension(targetPath)
                    ));

                    importedModel.transform.position = Vector3.zero;
                }
                else
                {
                    Debug.LogError("FBX 로드 실패");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"파일 처리 실패: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 지연 후 애니메이션 재생 및 VMD 녹화를 동기화하는 코루틴
        /// </summary>
        /// <param name="ghostModel">임포트된 Ghost 모델</param>
        /// <param name="ghostAnim">Ghost의 Animation 컴포넌트</param>
        /// <param name="targetCharacter">리타겟 대상 캐릭터</param>
        /// <param name="clip">재생할 AnimationClip</param>
        /// <param name="retargeter">Pose Space Retargeter 컴포넌트</param>
        private async Task<bool> ProcessFBXSessionAsync(string sourcePath)
        {
            EnsureServicesInitialized();
            ApplyTargetIdlePoseGuard();
            _isProcessing = true;
            ClearActiveRecordingSubscription();
            CleanupActiveGhost();

            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    FailSession($"FBX 파일을 찾을 수 없습니다: {sourcePath}");
                    return true;
                }

                if (!string.Equals(Path.GetExtension(sourcePath), ".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    FailSession("FBX 파일만 선택할 수 있습니다.");
                    return true;
                }

                SetSessionState(FBXSessionState.Selected, $"선택됨: {Path.GetFileName(sourcePath)}", 0.05f);
                string targetPath = CopyToControlledImportFolder(sourcePath);
                string outputBaseName = Path.GetFileNameWithoutExtension(targetPath);
                Debug.Log($"[FileManager] 자동 VMD 출력명 고정: {outputBaseName}.vmd (입력 FBX: {Path.GetFileName(sourcePath)})");
                SetSessionState(FBXSessionState.Copied, $"복제 완료: {Path.GetFileName(targetPath)}", 0.15f);

#if UNITY_EDITOR
                if (ShouldConfigureEditorImportSettings(sourcePath, targetPath, Application.dataPath))
                {
                    ConfigureImportSettings(targetPath);
                }
                else
                {
                    Debug.Log($"[FileManager] Controlled Import_FBX importer preserved: {targetPath}");
                }
#endif

                SetSessionState(FBXSessionState.LoadingFbx, "FBX 로드 중", 0.25f);
                GameObject importedModel = await _fbxImporter.ImportAsync(targetPath);
                if (importedModel == null)
                {
                    FailSession("FBX 로드에 실패했습니다.");
                    return true;
                }

                GameObject ghostContainer = CreateGhostContainer(importedModel);
                _activeGhostContainer = ghostContainer;
                SetGhostVisibility(importedModel, showGhostModel);

                Dictionary<string, string> boneMapping = LoadBoneMappingRuntime();
                if (boneMapping == null)
                {
                    boneMapping = new Dictionary<string, string>();
                }

                // BoneMapping_Data.txt는 특정 리그에 종속될 수 있으므로, 실패 시 자동 매핑으로 폴백합니다.
                HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);
                if (!ValidateGhostAvatar(importedModel))
                {
                    Debug.LogWarning("[FileManager] Ghost Humanoid Avatar 생성 실패. Auto bone mapping으로 재시도합니다.");
                    boneMapping = HumanoidAvatarBuilder.BuildAutoMapping(importedModel);
                    HumanoidAvatarBuilder.SetupHumanoid(importedModel, boneMapping);

                    if (!ValidateGhostAvatar(importedModel))
                    {
                        FailSession("Ghost Humanoid Avatar 생성에 실패했습니다.");
                        return true;
                    }
                }

                SetSessionState(FBXSessionState.AvatarReady, "Humanoid Avatar 준비 완료", 0.45f);

                Animation ghostAnim = importedModel.GetComponent<Animation>();
                AnimationClip targetClip = ExtractPrimaryClip(ghostAnim);
                if (targetClip == null)
                {
                    FailSession("FBX에서 유효한 애니메이션 클립을 찾지 못했습니다.");
                    return true;
                }

                GameObject targetObject = targetCharacter;
                if (targetObject == null)
                {
                    FailSession("Target Character가 지정되어 있지 않습니다.");
                    return true;
                }

                Animator targetAnimator = targetObject.GetComponent<Animator>();
                if (targetAnimator == null || targetAnimator.avatar == null || !targetAnimator.avatar.isValid || !targetAnimator.avatar.isHuman)
                {
                    FailSession("Target Character에 유효한 Humanoid Avatar가 없습니다.");
                    return true;
                }

                Animator ghostAnimator = importedModel.GetComponent<Animator>();
                PrepareTargetCharacter(targetObject, targetAnimator, ghostAnimator);

                PoseSpaceRetargeter retargeter = importedModel.AddComponent<PoseSpaceRetargeter>();
                _activeRetargeter = retargeter;
                retargeter.Initialize(importedModel, targetObject, boneMapping, targetClip, this);
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

                return true;
            }
            catch (Exception e)
            {
                FailSession($"FBX 처리 실패: {e.Message}", e);
                return true;
            }
        }

        private IEnumerator StartRecordingSequenceStable(
            Animation ghostAnim,
            PoseSpaceRetargeter retargeter,
            GameObject targetObject,
            AnimationClip clip,
            string outputBaseName)
        {
            SetSessionState(FBXSessionState.Retargeting, $"녹화 시작 전 {startDelay:F1}초 대기", 0.7f);
            yield return new WaitForSeconds(startDelay);

            if (ghostAnim == null || clip == null)
            {
                FailSession("녹화 시작에 필요한 Ghost 애니메이션이 없습니다.");
                yield break;
            }

            HumanoidSampleCode recorderController = targetObject.GetComponent<HumanoidSampleCode>();
            if (recorderController == null)
            {
                FailSession("Target Character에 HumanoidSampleCode가 없습니다.");
                yield break;
            }

            _activeRecorderController = recorderController;
            _activeRecorderController.RecordingFinished += OnRecordingFinished;
            _activeRecorderController.SetRecordingDiagnostics(
                enableRecordingDiagnostics,
                enableRecordingDiagnostics && enableDiagnosticFingerCloseups,
                enableRecordingDiagnostics && useDeterministicCaptureFramerateForDiagnostics,
                _editorSmokeSampleTimesOverride);

            float recordingStartTime = 0f;
            float recordingLength = clip.length;
            int recordingTargetFrameCount = 0;
            float recordingPlaybackSpeed = 1f;
            string recordingOutputBaseName = outputBaseName;
            string comparisonLabel = $"auto_{recordingOutputBaseName}";
#if UNITY_EDITOR
            if (_editorSmokeRecordingOverrideActive)
            {
                float requestedDuration = Mathf.Max(0.1f, _editorSmokeDurationSeconds);
                recordingStartTime = CalculateEditorSmokeStartTime(clip, requestedDuration, _editorSmokeSegment);
                float remainingLength = Mathf.Max(0.1f, clip.length - recordingStartTime);
                recordingLength = Mathf.Min(requestedDuration, remainingLength);
                recordingTargetFrameCount = Mathf.Min(
                    Mathf.Max(1, _editorSmokeTargetFrameCount),
                    Mathf.CeilToInt(recordingLength * EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE));
                recordingOutputBaseName = BuildEditorSmokeOutputBaseName(outputBaseName, recordingLength, _editorSmokeSegment);
                comparisonLabel = $"auto_{recordingOutputBaseName}";
                Debug.Log(
                    $"[FileManager] Editor smoke cap 적용: VMD={recordingOutputBaseName}.vmd, " +
                    $"segment={GetEditorSmokeSegmentLabel(_editorSmokeSegment)}, " +
                    $"start={recordingStartTime:F2}s, duration={recordingLength:F2}s, " +
                    $"targetFrameCount={recordingTargetFrameCount}");

                if (TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                    outputBaseName,
                    clip.length,
                    recordingLength,
                    recordingTargetFrameCount,
                    EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE,
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
                        $"[FileManager] YYB Satisfaction editor smoke reference timing applied: " +
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
                    $"[FileManager] YYB Satisfaction reference timing applied: " +
                    $"clipLength={clip.length:F3}s, recordingLength={recordingLength:F3}s, " +
                    $"targetFrameCount={recordingTargetFrameCount}, playbackSpeed={recordingPlaybackSpeed:F5}");
            }

            yield return PrewarmRetargetStartPose(ghostAnim, clip, retargeter, recordingStartTime, recordingPlaybackSpeed);
            retargeter?.CaptureRecordingStartBaselineSnapshot();
            retargeter?.ResetPlaybackStabilityMetrics();

            SetSessionState(FBXSessionState.Recording, $"녹화 중: {recordingOutputBaseName}", 0.75f);
            Debug.Log($"[FileManager] 자동 녹화 시작: VMD={recordingOutputBaseName}.vmd, 비교라벨={comparisonLabel}");
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

        private IEnumerator PrewarmRetargetStartPose(Animation ghostAnim, AnimationClip clip, PoseSpaceRetargeter retargeter, float startTimeSeconds, float playbackSpeed)
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
            int prewarmFrames = ResolveRetargetPrewarmFrameCount(RetargetPrewarmFrameCount);
            if (prewarmFrames <= 0)
            {
                state.time = sampleTime;
                state.speed = safePlaybackSpeed;
                ghostAnim.Sample();
                retargeter?.ApplyLateVisualGroundingCorrection();
                yield return YieldRetargetPrewarmFrame();
                yield break;
            }

            state.enabled = true;
            state.wrapMode = WrapMode.Once;
            state.time = sampleTime;
            state.speed = 0f;
            for (int i = 0; i < prewarmFrames; i++)
            {
                state.time = sampleTime;
                ghostAnim.Sample();
                yield return YieldRetargetPrewarmFrame();
            }

            state.time = sampleTime;
            state.speed = safePlaybackSpeed;
            ghostAnim.Sample();
            ghostAnim.Play(clip.name);
            state.time = sampleTime;
            state.speed = safePlaybackSpeed;
            ghostAnim.Sample();
            retargeter?.ApplyLateVisualGroundingCorrection();
            Debug.Log($"[FileManager] Retarget prewarm 완료: {prewarmFrames} frame(s) at clip time {sampleTime:F2}.");
        }

        private static object YieldRetargetPrewarmFrame()
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

        private void OnRecordingFinished(VmdSaveResult result)
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
                Debug.Log($"[FileManager] VMD 추가 복사: {targetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FileManager] VMD 추가 복사 실패: {ex.Message}");
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

            Debug.LogWarning($"[FileManager] Editor smoke diagnostic only: {diagnosticMessage}");
            return result;
#else
            return result;
#endif
        }

        private VmdSaveResult BuildEditorSmokeFailureResult(VmdSaveResult result, string errorMessage)
        {
            Debug.LogWarning($"[FileManager] {errorMessage}");
            return new VmdSaveResult
            {
                Success = false,
                FilePath = result.FilePath,
                ErrorMessage = errorMessage,
                FrameCount = result.FrameCount,
                FileSizeBytes = result.FileSizeBytes
            };
        }

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

        private string GetControlledImportDirectory()
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
            ApplyTargetIdlePoseGuard();

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
            foreach (Renderer renderer in importedModel.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = visible;
            }
        }

        private bool ValidateBoneMapping(Dictionary<string, string> boneMapping)
        {
            if (boneMapping == null || boneMapping.Count == 0)
            {
                Debug.LogError("[FileManager] BoneMapping_Data.txt가 비어 있습니다.");
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
                    Debug.LogError($"[FileManager] 필수 본 매핑 누락: {boneName}");
                    valid = false;
                }
            }

            if (boneMapping.TryGetValue("Chest", out string chestBone) && chestBone != "Skeleton_Spine1")
            {
                Debug.LogWarning($"[FileManager] Chest 매핑이 문서 기준과 다릅니다. 현재값: {chestBone}, 기대값: Skeleton_Spine1");
            }

            if (showBoneMappingLog)
            {
                foreach (KeyValuePair<string, string> mapping in boneMapping)
                {
                    Debug.Log($"[FileManager] BoneMapping: {mapping.Key} -> {mapping.Value}");
                }
            }

            return valid;
        }

        private bool ValidateGhostAvatar(GameObject importedModel)
        {
            Animator ghostAnimator = importedModel.GetComponent<Animator>();
            if (ghostAnimator == null || ghostAnimator.avatar == null)
            {
                Debug.LogError("[FileManager] Ghost Animator 또는 Avatar가 없습니다.");
                return false;
            }

            if (!ghostAnimator.avatar.isValid || !ghostAnimator.avatar.isHuman)
            {
                Debug.LogError($"[FileManager] Ghost Avatar가 유효하지 않습니다. valid={ghostAnimator.avatar.isValid}, human={ghostAnimator.avatar.isHuman}");
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
                Debug.LogError($"[FileManager] 애니메이션 길이가 올바르지 않습니다: {targetClip.length}");
                return null;
            }

            if (targetClip.length > 1000f)
            {
                Debug.LogWarning("[FileManager] 애니메이션 길이가 비정상적으로 깁니다. Assimp timeScale을 확인하세요.");
            }

            if (showRuntimeAnimationLog)
            {
                Debug.Log($"[FileManager] Clip: {targetClip.name}, Length: {targetClip.length:F3}s, FrameRate: {targetClip.frameRate}");
            }

            return targetClip;
        }

        private void PrepareTargetCharacter(GameObject targetObject, Animator targetAnimator, Animator ghostAnimator)
        {
            targetObject.transform.position = Vector3.zero;
            if (faceTargetToCameraOnIdle)
            {
                FaceTargetCharacterToCamera(targetObject);
            }
            else
            {
                targetObject.transform.rotation = Quaternion.identity;
            }
            targetAnimator.applyRootMotion = false;
            targetAnimator.runtimeAnimatorController = null;
            DisableMmdPostPoseCorrectionForRetarget(targetObject);
            HumanoidArmTwistRiggingGuard twistRiggingGuard = ConfigureTargetAnimationRiggingArmTwistCorrection(targetObject, targetAnimator);
            ConfigureTargetArmDirectionRetargetCorrection(targetObject, targetAnimator, ghostAnimator);
            ConfigureTargetArmSwingLimitCorrection(targetObject, targetAnimator);
            HumanoidArmSleeveAnchorGuard sleeveAnchorGuard = ConfigureTargetArmSleeveAnchorCorrection(targetObject, targetAnimator);
            HumanoidArmVisualTwistGuard visualTwistGuard = ConfigureTargetArmVisualTwistCorrection(targetObject, targetAnimator);
            ConfigureTargetArmDeformationGuard(targetObject, BuildLimbChildRotationExclusions(twistRiggingGuard, sleeveAnchorGuard, visualTwistGuard));

            // Batch smoke can leave detached thumb helpers visually drifted until the next frame.
            // Restore the captured target idle pose immediately before the next session captures
            // retargeter/thumb-guard baselines so every FBX starts from the same common pose.
            ApplyTargetIdlePoseGuard();
            if (_editorSmokeRecordingOverrideActive)
            {
                LogEditorSmokeThumbState("prepare-target-after-idle-restore");
            }

            IKControl ikControl = targetObject.GetComponent<IKControl>();
            if (ikControl != null)
            {
                Debug.Log("[FileManager] 자동 retarget 경로에서 IKControl을 제거합니다.");
                Destroy(ikControl);
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
            guard.Configure(
                false,
                clampTargetArmMuscles,
                ArmStretchMuscleLimit,
                UpperArmTwistMuscleLimit,
                LowerArmTwistMuscleLimit,
                lockTargetHumanoidBonePositions,
                logArmDeformationGuardCorrections,
                targetGuardClampArmStretchMuscles,
                lockTargetLimbChildLocalPositions,
                lockTargetLimbChildLocalRotations);
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
            if (_editorSmokeRecordingOverrideActive)
            {
                LogEditorSmokeThumbState("thumb-guard-bound");
            }
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

        private HumanoidArmDirectionRetargetGuard ConfigureTargetArmDirectionRetargetCorrection(GameObject targetObject, Animator targetAnimator, Animator ghostAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmDirectionRetargetGuard directionGuard = targetObject.GetComponent<HumanoidArmDirectionRetargetGuard>();
            if (!enableYybArmDirectionRetargetCorrection)
            {
                if (directionGuard != null)
                {
                    directionGuard.DisableCorrection();
                    directionGuard.enabled = false;
                }

                return null;
            }

            if (directionGuard == null)
            {
                directionGuard = targetObject.AddComponent<HumanoidArmDirectionRetargetGuard>();
            }

            directionGuard.enableDirectionRetarget = true;
            directionGuard.enabled = true;
            bool configured = directionGuard.Configure(
                ghostAnimator,
                targetAnimator,
                YybArmDirectionUpperArmWeight,
                YybArmDirectionForearmWeight,
                YybArmDirectionUpperArmMaxDegrees,
                YybArmDirectionForearmMaxDegrees,
                logYybArmDirectionRetargetCorrection);

            if (!configured)
            {
                directionGuard.enabled = false;
                return null;
            }

            return directionGuard;
        }

        private HumanoidArmSwingLimitGuard ConfigureTargetArmSwingLimitCorrection(GameObject targetObject, Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmSwingLimitGuard swingLimitGuard = targetObject.GetComponent<HumanoidArmSwingLimitGuard>();
            if (!enableYybArmSwingLimitCorrection)
            {
                if (swingLimitGuard != null)
                {
                    swingLimitGuard.enableSwingLimit = false;
                    swingLimitGuard.enabled = false;
                }

                return null;
            }

            if (swingLimitGuard == null)
            {
                swingLimitGuard = targetObject.AddComponent<HumanoidArmSwingLimitGuard>();
            }

            swingLimitGuard.Configure(
                targetAnimator,
                true,
                YybArmSwingLimitWeight,
                YybArmSwingMaxDownDot,
                YybArmSwingMinHandHorizontalRatio,
                YybArmSwingMaxHandBelowShoulderRatio,
                logYybArmSwingLimitCorrection);

            return swingLimitGuard;
        }

        private HumanoidArmSleeveAnchorGuard ConfigureTargetArmSleeveAnchorCorrection(GameObject targetObject, Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmSleeveAnchorGuard sleeveAnchorGuard = targetObject.GetComponent<HumanoidArmSleeveAnchorGuard>();
            if (!enableYybArmSleeveAnchorCorrection)
            {
                if (sleeveAnchorGuard != null)
                {
                    sleeveAnchorGuard.DisableCorrection();
                    sleeveAnchorGuard.enabled = false;
                }

                return null;
            }

            if (sleeveAnchorGuard == null)
            {
                sleeveAnchorGuard = targetObject.AddComponent<HumanoidArmSleeveAnchorGuard>();
            }

            sleeveAnchorGuard.enableSleeveAnchor = true;
            sleeveAnchorGuard.enabled = true;
            bool configured = sleeveAnchorGuard.Configure(
                targetAnimator,
                YybArmSleeveAnchorInfluence,
                YybArmShoulderCapAnchorInfluence,
                YybArmSleeveAnchorMaxDegrees,
                logYybArmSleeveAnchorCorrection);

            if (!configured)
            {
                sleeveAnchorGuard.enabled = false;
                return null;
            }

            return sleeveAnchorGuard;
        }

        private HumanoidArmVisualTwistGuard ConfigureTargetArmVisualTwistCorrection(GameObject targetObject, Animator targetAnimator)
        {
            if (targetObject == null)
            {
                return null;
            }

            HumanoidArmVisualTwistGuard visualTwistGuard = targetObject.GetComponent<HumanoidArmVisualTwistGuard>();
            if (!enableYybArmVisualTwistCorrection)
            {
                if (visualTwistGuard != null)
                {
                    visualTwistGuard.DisableCorrection();
                    visualTwistGuard.enabled = false;
                }

                return null;
            }

            if (visualTwistGuard == null)
            {
                visualTwistGuard = targetObject.AddComponent<HumanoidArmVisualTwistGuard>();
            }

            visualTwistGuard.enableVisualTwistGuard = true;
            visualTwistGuard.enabled = true;
            bool configured = visualTwistGuard.Configure(
                targetAnimator,
                YybArmVisualUpperArmInfluence,
                YybArmVisualForearmInfluence,
                YybArmVisualUpperArmMaxDegrees,
                YybArmVisualForearmMaxDegrees,
                logYybArmVisualTwistCorrection);

            if (!configured)
            {
                visualTwistGuard.enabled = false;
                return null;
            }

            return visualTwistGuard;
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
                Debug.Log($"[FileManager] Retarget 중 MMD4Mecanim 어깨 PPH 보정 {changed}개를 일시 비활성화했습니다.");
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

        private void FailSession(string message, Exception exception = null)
        {
            if (exception != null)
            {
                Debug.LogError($"[FileManager] {message}\n{exception}");
            }
            else
            {
                Debug.LogError($"[FileManager] {message}");
            }

            SetSessionState(FBXSessionState.Failed, message, 0f);
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
                $"[FileManager] Retarget playback stability: " +
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
                $"[FileManager] Editor smoke thumb state ({stage}): " +
                $"fbx={_editorSmokeCurrentFbxFileName ?? "<none>"}, " +
                $"segment={GetEditorSmokeSegmentLabel(_editorSmokeSegment)}, " +
                $"projectionMin={EffectiveThumbProjectionMinPalmNormal:F3}, " +
                $"thumbReference[{BuildActiveRetargeterThumbReferenceSummary()}], " +
                $"guardLeft[{leftGuard}], guardRight[{rightGuard}], " +
                $"retargeterLeft[{leftRetargeter}], retargeterRight[{rightRetargeter}]");
        }

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

        private void SetSessionState(FBXSessionState state, string message, float progress)
        {
            Debug.Log($"[FileManager] {state}: {message}");
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
            Debug.Log($"[FileManager]  녹화 시작까지 {startDelay}초 대기 중...");

            // 지연 대기
            yield return new WaitForSeconds(startDelay);

            // Ghost 애니메이션 재생 시작
            if (ghostAnim != null && clip != null)
            {
                ghostAnim.clip = clip;
                ghostAnim.Play();
                Debug.Log($"[FileManager]  Ghost 애니메이션 재생 시작: {clip.name}");
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
                Debug.Log($"[FileManager]  VMD 녹화 동시 시작! (길이: {clipLen:F2}초)");
            }
            else
            {
                Debug.LogWarning("[FileManager]  HumanoidSampleCode 컴포넌트가 Target에 없습니다. 녹화 건너뜀.");
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
                Debug.Log($"[FileManager] BoneMapping 로드 성공 (Resources/{loadName})");
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
                Debug.LogWarning($"[FileManager] BoneMapping 로드 실패: Resources/{loadName}.txt (자동 본 매핑으로 폴백합니다.)");
            }

            return mapping;
        }

        private void SetupGhostRetargeting(GameObject ghostObject, AnimationClip ghostClip, GameObject targetPrefab)
        {
            Debug.Log("[FileManager] Native AnimatorOverride Setup...");
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
            if (!useEditorHumanoidClipMuscleReference || retargeter == null)
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
                Debug.LogWarning($"[FileManager] Unity Editor Humanoid 기준 클립을 찾지 못했습니다: {relativePath}");
                return;
            }

            Debug.Log($"[FileManager] Editor Humanoid muscle 기준 clip: {relativePath}/{referenceClip.name}");
            retargeter.ConfigureEditorHumanoidMuscleReference(referenceClip);
            if (useEditorHumanoidRootTranslationReference)
            {
                retargeter.ConfigureEditorHumanoidRootTranslationReference(referenceClip);
            }
            ConfigureEditorManualFingerPoseReference(retargeter, referenceClip);
        }

        private void ConfigureEditorManualFingerPoseReference(PoseSpaceRetargeter retargeter, AnimationClip referenceClip)
        {
            if ((!useManualAnimatorFingerPoseReference &&
                    !useManualAnimatorFullBodyPoseReference &&
                    !useManualAnimatorHipsLocalPositionReference &&
                    !useManualAnimatorBodyRotationReference &&
                    !useManualAnimatorHandLocalRotationReference) ||
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
                Debug.LogWarning("[FileManager] 수동 기준 손가락 Reference prefab/controller를 찾지 못해 raw FBX finger curve를 사용합니다.");
                return;
            }

            retargeter.ConfigureEditorHumanoidFingerPoseReference(
                referencePrefab,
                referenceController,
                referenceClip,
                useManualAnimatorFingerPoseReference,
                useManualAnimatorFullBodyPoseReference);
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
            if (hasHumanoidAnimationClip(sourceRelativePath))
            {
                return sourceRelativePath;
            }

            string fileName = Path.GetFileName(string.IsNullOrEmpty(sourceFileName) ? importedRelativePath : sourceFileName);
            if (!string.IsNullOrEmpty(fileName))
            {
                string manualReferencePath = $"Assets/_Project/FBX/{fileName}".Replace("\\", "/");
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
                        Debug.Log($"[Strip Bones Fix] Successfully disabled '{name}' via SerializedObject");
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
                Debug.LogError($"[Strip Bones Fix] Error: {e.Message}");
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
}
