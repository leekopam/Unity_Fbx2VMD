using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Serialization;

public struct VmdSaveResult
{
    public bool Success;
    public string FilePath;
    public string ErrorMessage;
    public int FrameCount;
    public long FileSizeBytes;
    public string ExportRotationDiagnosticsCsvPath;
    public string ExportIkSourceDiagnosticsCsvPath;

    public static VmdSaveResult Ok(
        string filePath,
        int frameCount,
        long fileSizeBytes,
        string exportRotationDiagnosticsCsvPath = "",
        string exportIkSourceDiagnosticsCsvPath = "")
    {
        return new VmdSaveResult
        {
            Success = true,
            FilePath = filePath,
            ErrorMessage = "",
            FrameCount = frameCount,
            FileSizeBytes = fileSizeBytes,
            ExportRotationDiagnosticsCsvPath = exportRotationDiagnosticsCsvPath ?? "",
            ExportIkSourceDiagnosticsCsvPath = exportIkSourceDiagnosticsCsvPath ?? ""
        };
    }

    public static VmdSaveResult Fail(string filePath, string errorMessage)
    {
        return new VmdSaveResult
        {
            Success = false,
            FilePath = filePath,
            ErrorMessage = errorMessage,
            FrameCount = 0,
            FileSizeBytes = 0,
            ExportRotationDiagnosticsCsvPath = "",
            ExportIkSourceDiagnosticsCsvPath = ""
        };
    }
}

// ============================================
// [실행 순서 2] VMD 레코딩 메인 엔진
// 역할: FBX 애니메이션 → VMD 파일 변환 핵심 로직
// 실행 시점: Start에서 초기화, LateUpdate 후반에서 레코딩
// ============================================

//初期ポーズ(T,Aポーズ)の時点でアタッチ、有効化されている必要がある
// [한글] 초기 포즈(T포즈, A포즈) 상태에서 컴포넌트가 부착되고 활성화되어 있어야 함
[DefaultExecutionOrder(29980)]
public partial class UnityHumanoidVMDRecorder : MonoBehaviour
{
    // === VMD 변환 설정 옵션 ===
    public bool UseParentOfAll = true;                    // "全ての親" 본 사용 여부
    public bool UseCenterAsParentOfAll = true;            // 센터를 "全ての親"으로 사용

    [Tooltip("UseCenterAsParentOfAll 사용 시 Humanoid 센터 본을 グルーブ로 내보냅니다. 대상 MMD 모델에 グルーブ 본이 없으면 끄고 センター에 실제 이동을 기록해야 합니다.")]
    public bool RouteHumanoidCenterToGroove = false;
    
    /// <summary>
    /// 全ての親の座標・回転を絶対座標系で計算する
    /// UseParentOfAllがTrueでないと意味がない
    /// </summary>
    /// [한글] "全ての親"의 좌표와 회전을 절대 좌표계로 계산 (UseParentOfAll이 true여야 의미 있음)
    public bool UseAbsoluteCoordinateSystem = true;

    [Header("IK Offsets")]
    [Tooltip("UseCenterAsParentOfAll=1 & UseAbsoluteCoordinateSystem=0(그리고 parent가 존재)일 때, 발/발끝 IK offset을 같은(parent) 좌표계 기준으로 계산/적용합니다. 회귀 비교용으로 끌 수 있습니다.")]
    public bool EnableParentFrameIkOffsetCompensationWhenCenterParented = true;
    
    public bool IgnoreInitialPosition = false;            // 초기 위치 무시 여부
    public bool IgnoreInitialRotation = false;            // 초기 회전 무시 여부

    [Tooltip("IgnoreInitialPosition이 켜진 export에서 scene/root carrier 이동을 全ての親과 발 IK 좌표에 쓰지 않습니다. Main_Auto처럼 Unity 화면에서는 제자리인데 carrier transform만 움직이는 경로의 MMD 날아다님을 방지합니다.")]
    public bool FreezeParentOfAllMotionWhenIgnoringInitialPosition = false;
    
    /// <summary>
    /// 一部のモデルではMMD上ではセンターが足元にある
    /// Start前に設定されている必要がある
    /// </summary>
    /// [한글] 일부 모델은 MMD에서 센터가 발 위치에 있음 (Start 전에 설정 필요)
    public bool UseBottomCenter = false;
    
    /// <summary>
    /// Unity上のモーフ名に1.まばたきなど番号が振られている場合、番号を除去する
    /// </summary>
    /// [한글] Unity 모프 이름에 "1.まばたき" 같은 번호가 있으면 제거
    public bool TrimMorphNumber = true;

    [Header("Deformation Guard")]
    [Tooltip("센터/전체부모/IK를 제외한 일반 본의 위치 키를 0으로 저장해 MMD 모델 팔/몸통 변형을 방지합니다.")]
    public bool ZeroNonRootBonePositions = true;

    [Tooltip("레코더 시작 시 Avatar 기준 초기 포즈를 실제 모델 Transform에 강제 적용합니다. YYB/MMD 모델은 기본 꺼짐을 권장합니다.")]
    public bool ApplyRecorderInitialPose = false;

    [Tooltip("초기 포즈 강제 적용 시 팔을 A 포즈로 추가 회전합니다.")]
    public bool ApplyRecorderAPose = false;

    [Header("Recording Timing")]
    [Tooltip("최종 retarget/grounding LateUpdate가 끝난 뒤 VMD 프레임을 저장합니다. Main_Auto YYB 자동 경로의 끊김/이전 포즈 저장을 줄이기 위해 기본 활성화합니다.")]
    public bool RecordAfterLateVisualPose = true;

    [Tooltip("테스트/회귀 검증 전용입니다. 켜면 녹화 중 Unity 시간을 VMD 기준 fps로 고정합니다. 일반 GameView 재생에서는 배속/멈칫 체감이 생길 수 있어 기본값은 끕니다.")]
    public bool UseCaptureFramerateDuringRecording = false;

    [Tooltip("한 LateUpdate에서 저장할 수 있는 최대 VMD 프레임 수입니다. 일반 재생에서는 1로 두어 저장 burst로 인한 미세 멈춤을 줄입니다.")]
    [Range(1, 8)] public int MaxRecordedFramesPerLateUpdate = 1;

    [Tooltip("captureFramerate를 쓰지 않는 일반 재생에서 렌더 프레임이 밀렸을 때 backlog를 버려 저장 burst를 막습니다. 테스트용 결정론 녹화에서는 끄거나 captureFramerate를 켭니다.")]
    public bool DropLateFrameBacklogWhenNotUsingCaptureFramerate = true;
    
    public int KeyReductionLevel = 3;                     // 키 리덕션 레벨 (파일 크기 감소)
    // === 레코딩 상태 변수 ===
    public bool IsRecording { get; private set; } = false;  // 현재 레코딩 중인지 여부
    public int FrameNumber { get; private set; } = 0;       // 현재 레코딩 중인 프레임 번호
    int frameNumberSaved = 0;                                // 저장된 총 프레임 수
    const float FPSs = 0.03333f;                             // 30 FPS (1/30 = 0.03333초)
    const string CenterNameString = "センター";              // VMD 센터 본 이름
    const string GrooveNameString = "グルーブ";              // VMD 그루브 본 이름

    public Transform ForceLeftToeEnd;
    public Transform ForceRightToeEnd;
    
    public enum BoneNames
    {
        全ての親,
        センター,
        左足ＩＫ,
        右足ＩＫ,
        // Added toe IK bones:
        左つま先ＩＫ,
        右つま先ＩＫ,
        上半身,
        上半身2,
        首,
        頭,
        左肩,
        左腕,
        左ひじ,
        左手首,
        右肩,
        右腕,
        右ひじ,
        右手首,
        左親指１,
        左親指２,
        左人指１,
        左人指２,
        左人指３,
        左中指１,
        左中指２,
        左中指３,
        左薬指１,
        左薬指２,
        左薬指３,
        左小指１,
        左小指２,
        左小指３,
        右親指１,
        右親指２,
        右人指１,
        右人指２,
        右人指３,
        右中指１,
        右中指２,
        右中指３,
        右薬指１,
        右薬指２,
        右薬指３,
        右小指１,
        右小指２,
        右小指３,
        左足,
        右足,
        左ひざ,
        右ひざ,
        左足首,
        右足首,
        下半身,
        None
        // 左つま先, 右つま先は情報付けると足首の回転、位置との矛盾が生じかねない（今回はIKとして記録します）
    }
    // === 본 매핑 데이터 (Unity HumanBodyBones → VMD BoneNames) ===
    //コンストラクタにて初期化
    //全てのボーンを名前で引く辞書
    // [한글] 생성자에서 초기화 - 모든 본을 이름으로 검색하는 딕셔너리
    Dictionary<string, Transform> transformDictionary = new Dictionary<string, Transform>();
    public Dictionary<BoneNames, Transform> BoneDictionary { get; private set; }
    Vector3 parentInitialPosition = Vector3.zero;        // 초기 위치 저장용
    Quaternion parentInitialRotation = Quaternion.identity;  // 초기 회전 저장용
    Dictionary<BoneNames, List<Vector3>> positionDictionary = new Dictionary<BoneNames, List<Vector3>>();
    Dictionary<BoneNames, List<Vector3>> positionDictionarySaved = new Dictionary<BoneNames, List<Vector3>>();
    Dictionary<BoneNames, List<Quaternion>> rotationDictionary = new Dictionary<BoneNames, List<Quaternion>>();
    Dictionary<BoneNames, List<Quaternion>> rotationDictionarySaved = new Dictionary<BoneNames, List<Quaternion>>();
    //ボーン移動量の補正係数
    //この値は大体の値、正確ではない
    // [한글] 본 이동량 보정 계수 (대략적인 값, 정확하지 않음)
    const float DefaultBoneAmplifier = 12.5f;

    public Vector3 ParentOfAllOffset = new Vector3(0, 0, 0);  // "全ての親" 오프셋

    [Header("MMD Export Floor Guard")]
    [Tooltip("VMD로 기록하는 발/발끝 IK에만 더하는 MMD 바닥 보정 오프셋입니다. 몸통/팔/센터/全ての親에는 적용하지 않습니다.")]
    public Vector3 MmdFootIkExportOffset = Vector3.zero;

    [Tooltip("VMD로 기록하는 발/발끝 IK keyframe의 Y가 MMD 바닥선 아래로 내려가지 않도록 제한합니다.")]
    public bool ClampMmdFootIkYToFloor = false;

    [Tooltip("Raises only the exported center/root Y per frame when effective foot IK height would go below the MMD floor.")]
    public bool LiftMmdCenterYToKeepFeetAboveFloor = false;

    [Tooltip("MMD VMD 발/발끝 IK keyframe에 허용할 최소 Y 값입니다. 0이면 MMD 바닥선 아래 음수 Y를 내보내지 않습니다.")]
    public float MinMmdFootIkY = 0f;

    [Header("MMD Export Delta Guard")]
    [Tooltip("Limits one-frame center-bone export jumps so MMD playback does not teleport through root motion spikes.")]
    public bool ClampMmdCenterExportDeltaSpikes = false;

    [Tooltip("Maximum VMD-space movement allowed per frame for center-bone keys.")]
    [Range(0.02f, 1f)] public float MaxMmdCenterExportDeltaPerFrame = 0.12f;

    [Tooltip("Limits one-frame foot/toe IK export jumps so MMD playback does not snap the model through IK targets.")]
    public bool ClampMmdIkExportDeltaSpikes = false;

    [Tooltip("Maximum VMD-space movement allowed per frame for foot IK keys.")]
    [Range(0.05f, 2f)] public float MaxMmdFootIkExportDeltaPerFrame = 0.12f;

    [Tooltip("Maximum VMD-space movement allowed per frame for toe IK keys.")]
    [Range(0.05f, 2f)] public float MaxMmdToeIkExportDeltaPerFrame = 0.12f;

    [Tooltip("Allows a slightly higher IK export delta only when the raw source foot step is large enough to be a recovery segment. Disabled by default.")]
    public bool UseMmdIkExportDeltaRecoveryLimit = false;

    [Tooltip("VMD-space raw source foot step that activates the conditional IK recovery limit.")]
    [Range(0.05f, 2f)] public float MmdIkExportDeltaRecoveryTriggerPerFrame = 0.30f;

    [Tooltip("VMD-space IK export delta used only while the conditional recovery trigger is active.")]
    [Range(0.05f, 2f)] public float MmdIkExportDeltaRecoveryLimitPerFrame = 0.12f;

    [Tooltip("VMD-space accumulated lag from source IK target that also activates the conditional recovery limit. 0 disables lag-debt recovery.")]
    [Range(0f, 2f)] public float MmdIkExportDeltaRecoveryDebtThresholdPerFrame = 0f;

    [Tooltip("Total frames in the conditional IK recovery hold window, including the triggering raw source foot step. 0 disables hold-window recovery.")]
    [Range(0, 30)] public int MmdIkExportDeltaRecoveryHoldFrames = 0;

    [Tooltip("Writes per-frame MMD IK ON/OFF keys so large swing-phase foot IK target steps do not pull the model through the target.")]
    public bool UseMmdIkDynamicToggleOnLargeExportSteps = false;

    [Tooltip("VMD-space foot IK step that temporarily disables the matching foot/toe IK pair when dynamic IK toggles are enabled.")]
    [Range(0.05f, 2f)] public float MmdIkDynamicToggleFootStepThreshold = 0.12f;

    [Tooltip("VMD-space toe IK step that temporarily disables the matching foot/toe IK pair when dynamic IK toggles are enabled.")]
    [Range(0.05f, 2f)] public float MmdIkDynamicToggleToeStepThreshold = 0.12f;

    public int LastMmdIkExportDeltaClampCount { get; private set; }
    public float LastMmdIkExportMaxDeltaBefore { get; private set; }
    public float LastMmdIkExportMaxDeltaAfter { get; private set; }
    public int LastMmdCenterExportDeltaClampCount { get; private set; }
    public float LastMmdCenterExportMaxDeltaBefore { get; private set; }
    public float LastMmdCenterExportMaxDeltaAfter { get; private set; }
    public int LastMmdCenterFloorLiftAdjustedFrameCount { get; private set; }
    public float LastMmdCenterFloorLiftMinEffectiveYBefore { get; private set; }
    public float LastMmdCenterFloorLiftMinEffectiveYAfter { get; private set; }
    public float LastMmdCenterFloorLiftMaxY { get; private set; }

    [Header("Export Diagnostics")]
    [Tooltip("Writes per-bone export rotation residual diagnostics next to the generated VMD file.")]
    public bool EnableExportRotationDiagnostics = false;
    [Tooltip("Writes per-frame foot IK source samples next to the generated VMD file for recorder-side path probes.")]
    public bool EnableExportIkSourceDiagnostics = false;
    public string LastExportRotationDiagnosticsCsvPath { get; private set; } = "";
    public string LastExportRotationDiagnosticSamplesCsvPath { get; private set; } = "";
    public string LastExportIkSourceDiagnosticsCsvPath { get; private set; } = "";

    public Vector3 LeftFootIKOffset = Vector3.zero;           // 왼발 IK 오프셋
    public Vector3 RightFootIKOffset = Vector3.zero;          // 오른발 IK 오프셋
    // New: Toe IK Offset
    public Vector3 LeftToeIKOffset = Vector3.zero;            // 왼발끝 IK 오프셋
    public Vector3 RightToeIKOffset = Vector3.zero;           // 오른발끝 IK 오프셋

    Animator animator;             // 애니메이터 참조
    VmdBoneGhost boneGhost;           // 정규화된 본 구조 (가상 스켈레톤)
    VmdMorphRecorder morphRecorder;   // 모프 레코더 (현재 레코딩용)
    VmdMorphRecorder morphRecorderSaved;  // 모프 레코더 (저장용)
    Dictionary<BoneNames, ExportRotationDiagnosticAggregate> exportRotationDiagnosticAggregates = new Dictionary<BoneNames, ExportRotationDiagnosticAggregate>();
    Dictionary<BoneNames, ExportRotationDiagnosticAggregate> exportRotationDiagnosticAggregatesSaved = new Dictionary<BoneNames, ExportRotationDiagnosticAggregate>();
    List<ExportRotationDiagnosticSample> exportRotationDiagnosticSamples = new List<ExportRotationDiagnosticSample>();
    List<ExportRotationDiagnosticSample> exportRotationDiagnosticSamplesSaved = new List<ExportRotationDiagnosticSample>();
    List<ExportIkSourceDiagnosticSample> exportIkSourceDiagnosticSamples = new List<ExportIkSourceDiagnosticSample>();
    List<ExportIkSourceDiagnosticSample> exportIkSourceDiagnosticSamplesSaved = new List<ExportIkSourceDiagnosticSample>();
    bool recorderInitializationWarningLogged;
    float recordingFrameAccumulator;
    int lastSavedUnityFrame = -1;
    int sameUnityFrameSaveCount;
    int maxFramesSavedInSingleLateUpdate;
    int droppedLateFrameBacklogCount;
    int previousCaptureFramerate;
    bool captureFramerateApplied;
    Transform cachedIkRootReferenceTransform;
    static readonly string[] MovingIkRootReferenceNames =
    {
        "461.!Root",
        "modelRootNode"
    };

    public int SameUnityFrameSaveCount => sameUnityFrameSaveCount;
    public int MaxFramesSavedInSingleLateUpdate => maxFramesSavedInSingleLateUpdate;
    public int DroppedLateFrameBacklogCount => droppedLateFrameBacklogCount;

    internal sealed class ExportRotationDiagnosticAggregate
    {
        public ExportRotationDiagnosticAggregate(BoneNames boneName)
        {
            BoneName = boneName;
            ExportSourceMode = "";
            MaxGhostVsSourceLocalDeltaFrame = -1;
            MaxParentRestBasisCorrectedVsSourceLocalDeltaFrame = -1;
            MaxExportVsSourceLocalDeltaFrame = -1;
        }

        public BoneNames BoneName { get; }
        public int SampleCount;
        public string ExportSourceMode;
        public int MaxGhostVsSourceLocalDeltaFrame;
        public float MaxGhostVsSourceLocalDeltaDegrees;
        public int MaxParentRestBasisCorrectedVsSourceLocalDeltaFrame;
        public float MaxParentRestBasisCorrectedVsSourceLocalDeltaDegrees;
        public int MaxExportVsSourceLocalDeltaFrame;
        public float MaxExportVsSourceLocalDeltaDegrees;

        public void Add(int frameNumber, VmdBoneRotationDiagnostic diagnostic)
        {
            SampleCount++;
            if (string.IsNullOrEmpty(ExportSourceMode))
            {
                ExportSourceMode = diagnostic.ExportSourceMode ?? "";
            }

            UpdateMax(
                frameNumber,
                diagnostic.GhostVsSourceLocalDeltaAngleDegrees,
                ref MaxGhostVsSourceLocalDeltaFrame,
                ref MaxGhostVsSourceLocalDeltaDegrees);
            UpdateMax(
                frameNumber,
                diagnostic.ParentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees,
                ref MaxParentRestBasisCorrectedVsSourceLocalDeltaFrame,
                ref MaxParentRestBasisCorrectedVsSourceLocalDeltaDegrees);
            UpdateMax(
                frameNumber,
                diagnostic.ExportVsSourceLocalDeltaAngleDegrees,
                ref MaxExportVsSourceLocalDeltaFrame,
                ref MaxExportVsSourceLocalDeltaDegrees);
        }

        private static void UpdateMax(int frameNumber, float value, ref int frame, ref float maxValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return;
            }

            if (frame < 0 || value > maxValue)
            {
                frame = frameNumber;
                maxValue = value;
            }
        }
    }

    internal readonly struct ExportRotationDiagnosticSample
    {
        public ExportRotationDiagnosticSample(int frameNumber, VmdBoneRotationDiagnostic diagnostic)
        {
            FrameNumber = frameNumber;
            Diagnostic = diagnostic;
        }

        public int FrameNumber { get; }
        public VmdBoneRotationDiagnostic Diagnostic { get; }
    }

    internal readonly struct ExportIkSourceDiagnosticSample
    {
        public ExportIkSourceDiagnosticSample(
            int recorderFrameNumber,
            int unityFrameNumber,
            float sampleTime,
            BoneNames boneName,
            Vector3 rootReferencePosition,
            Vector3 sourceWorldPosition,
            Vector3 sourceRelativePosition,
            Vector3 exportedUnityPosition,
            Vector3 directFootWorldPosition = default(Vector3),
            Vector3 directFootRootPosition = default(Vector3),
            Vector3 recorderRootPosition = default(Vector3),
            Vector3 sourceRecorderRootPosition = default(Vector3),
            Vector3 directFootRecorderRootPosition = default(Vector3))
        {
            RecorderFrameNumber = recorderFrameNumber;
            UnityFrameNumber = unityFrameNumber;
            SampleTime = sampleTime;
            BoneName = boneName;
            RootReferencePosition = rootReferencePosition;
            SourceWorldPosition = sourceWorldPosition;
            SourceRelativePosition = sourceRelativePosition;
            ExportedUnityPosition = exportedUnityPosition;
            DirectFootWorldPosition = directFootWorldPosition;
            DirectFootRootPosition = directFootRootPosition;
            RecorderRootPosition = recorderRootPosition;
            SourceRecorderRootPosition = sourceRecorderRootPosition;
            DirectFootRecorderRootPosition = directFootRecorderRootPosition;
        }

        public int RecorderFrameNumber { get; }
        public int UnityFrameNumber { get; }
        public float SampleTime { get; }
        public BoneNames BoneName { get; }
        public Vector3 RootReferencePosition { get; }
        public Vector3 SourceWorldPosition { get; }
        public Vector3 SourceRelativePosition { get; }
        public Vector3 ExportedUnityPosition { get; }
        public Vector3 DirectFootWorldPosition { get; }
        public Vector3 DirectFootRootPosition { get; }
        public Vector3 RecorderRootPosition { get; }
        public Vector3 SourceRecorderRootPosition { get; }
        public Vector3 DirectFootRecorderRootPosition { get; }
    }

    internal void RecordExportRotationDiagnostic(int frameNumber, VmdBoneRotationDiagnostic diagnostic)
    {
        if (!exportRotationDiagnosticAggregates.TryGetValue(diagnostic.BoneName, out var aggregate))
        {
            aggregate = new ExportRotationDiagnosticAggregate(diagnostic.BoneName);
            exportRotationDiagnosticAggregates[diagnostic.BoneName] = aggregate;
        }

        aggregate.Add(frameNumber, diagnostic);
        exportRotationDiagnosticSamples.Add(new ExportRotationDiagnosticSample(frameNumber, diagnostic));
    }

    internal IReadOnlyCollection<ExportRotationDiagnosticAggregate> GetExportRotationDiagnosticAggregates()
    {
        return exportRotationDiagnosticAggregates.Values.ToArray();
    }

    internal IReadOnlyCollection<ExportRotationDiagnosticSample> GetExportRotationDiagnosticSamples()
    {
        return exportRotationDiagnosticSamples.ToArray();
    }

    internal void RecordExportIkSourceDiagnostic(
        int recorderFrameNumber,
        int unityFrameNumber,
        float sampleTime,
        BoneNames boneName,
        Vector3 rootReferencePosition,
        Vector3 sourceWorldPosition,
        Vector3 sourceRelativePosition,
        Vector3 exportedUnityPosition,
        Vector3 directFootWorldPosition = default(Vector3),
        Vector3 directFootRootPosition = default(Vector3),
        Vector3 recorderRootPosition = default(Vector3),
        Vector3 sourceRecorderRootPosition = default(Vector3),
        Vector3 directFootRecorderRootPosition = default(Vector3))
    {
        exportIkSourceDiagnosticSamples.Add(new ExportIkSourceDiagnosticSample(
            recorderFrameNumber,
            unityFrameNumber,
            sampleTime,
            boneName,
            rootReferencePosition,
            sourceWorldPosition,
            sourceRelativePosition,
            exportedUnityPosition,
            directFootWorldPosition,
            directFootRootPosition,
            recorderRootPosition,
            sourceRecorderRootPosition,
            directFootRecorderRootPosition));
    }

    internal IReadOnlyCollection<ExportIkSourceDiagnosticSample> GetExportIkSourceDiagnosticSamples()
    {
        return exportIkSourceDiagnosticSamples.ToArray();
    }

    internal static List<ExportIkSourceDiagnosticSample> BuildFinalExportIkSourceDiagnosticSamples(
        IEnumerable<ExportIkSourceDiagnosticSample> samples,
        IReadOnlyDictionary<BoneNames, List<Vector3>> finalVmdPositions,
        int safeFrameCount)
    {
        var finalSamples = new List<ExportIkSourceDiagnosticSample>();
        if (samples == null)
        {
            return finalSamples;
        }

        foreach (ExportIkSourceDiagnosticSample sample in samples)
        {
            Vector3 exportedUnityPosition = sample.ExportedUnityPosition;
            if (finalVmdPositions != null &&
                sample.RecorderFrameNumber >= 0 &&
                sample.RecorderFrameNumber < safeFrameCount &&
                finalVmdPositions.TryGetValue(sample.BoneName, out var finalPositions) &&
                finalPositions != null &&
                sample.RecorderFrameNumber < finalPositions.Count)
            {
                exportedUnityPosition = ConvertVmdExportPositionToUnityMeters(finalPositions[sample.RecorderFrameNumber]);
            }

            finalSamples.Add(new ExportIkSourceDiagnosticSample(
                sample.RecorderFrameNumber,
                sample.UnityFrameNumber,
                sample.SampleTime,
                sample.BoneName,
                sample.RootReferencePosition,
                sample.SourceWorldPosition,
                sample.SourceRelativePosition,
                exportedUnityPosition,
                sample.DirectFootWorldPosition,
                sample.DirectFootRootPosition,
                sample.RecorderRootPosition,
                sample.SourceRecorderRootPosition,
                sample.DirectFootRecorderRootPosition));
        }

        return finalSamples;
    }

    internal static string BuildExportRotationDiagnosticsCsv(IEnumerable<ExportRotationDiagnosticAggregate> aggregates)
    {
        var builder = new StringBuilder();
        builder.AppendLine("boneName,boneIndex,sampleCount,maxGhostVsSourceLocalDeltaFrame,maxGhostVsSourceLocalDeltaDegrees,maxParentRestBasisCorrectedVsSourceLocalDeltaFrame,maxParentRestBasisCorrectedVsSourceLocalDeltaDegrees,maxExportVsSourceLocalDeltaFrame,maxExportVsSourceLocalDeltaDegrees,exportSourceMode");

        if (aggregates == null)
        {
            return builder.ToString();
        }

        foreach (ExportRotationDiagnosticAggregate aggregate in aggregates.OrderBy(row => (int)row.BoneName))
        {
            builder.Append(CsvEscape(aggregate.BoneName.ToString()));
            builder.Append(',');
            builder.Append(((int)aggregate.BoneName).ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(aggregate.SampleCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(aggregate.MaxGhostVsSourceLocalDeltaFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(aggregate.MaxGhostVsSourceLocalDeltaDegrees));
            builder.Append(',');
            builder.Append(aggregate.MaxParentRestBasisCorrectedVsSourceLocalDeltaFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(aggregate.MaxParentRestBasisCorrectedVsSourceLocalDeltaDegrees));
            builder.Append(',');
            builder.Append(aggregate.MaxExportVsSourceLocalDeltaFrame.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(aggregate.MaxExportVsSourceLocalDeltaDegrees));
            builder.Append(',');
            builder.Append(CsvEscape(aggregate.ExportSourceMode));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    internal static string BuildExportRotationDiagnosticSamplesCsv(IEnumerable<ExportRotationDiagnosticSample> samples)
    {
        var builder = new StringBuilder();
        builder.AppendLine("frameNumber,boneName,boneIndex,sourceMode,exportSourceMode,ghostVsSourceLocalDeltaDegrees,parentRestBasisCorrectedVsSourceLocalDeltaDegrees,exportVsSourceLocalDeltaDegrees,sourceLocalDeltaX,sourceLocalDeltaY,sourceLocalDeltaZ,sourceLocalDeltaW,exportLocalX,exportLocalY,exportLocalZ,exportLocalW,exportVmdX,exportVmdY,exportVmdZ,exportVmdW");

        if (samples == null)
        {
            return builder.ToString();
        }

        foreach (ExportRotationDiagnosticSample sample in samples.OrderBy(row => row.FrameNumber).ThenBy(row => (int)row.Diagnostic.BoneName))
        {
            VmdBoneRotationDiagnostic diagnostic = sample.Diagnostic;
            builder.Append(sample.FrameNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(diagnostic.BoneName.ToString()));
            builder.Append(',');
            builder.Append(((int)diagnostic.BoneName).ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(CsvEscape(diagnostic.SourceMode));
            builder.Append(',');
            builder.Append(CsvEscape(diagnostic.ExportSourceMode));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(diagnostic.GhostVsSourceLocalDeltaAngleDegrees));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(diagnostic.ParentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(diagnostic.ExportVsSourceLocalDeltaAngleDegrees));
            AppendQuaternion(builder, diagnostic.SourceLocalDeltaRotation);
            AppendQuaternion(builder, diagnostic.ExportLocalRotation);
            AppendQuaternion(builder, diagnostic.ExportVmdRotation);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    internal static string BuildExportIkSourceDiagnosticsCsv(IEnumerable<ExportIkSourceDiagnosticSample> samples)
    {
        var builder = new StringBuilder();
        builder.AppendLine("recorderFrame,unityFrame,sampleTime,boneName,boneIndex,rootReferencePosition,sourceWorldPosition,sourceRelativePosition,exportedUnityPosition,directFootWorldPosition,directFootRootPosition,recorderRootPosition,sourceRecorderRootPosition,directFootRecorderRootPosition,sourceRelativeVsSourceRecorderRootDelta,sourceRelativeVsDirectFootRecorderRootDelta,exportedUnityVsSourceRelativeDelta,exportedUnityVsSourceRecorderRootDelta");

        if (samples == null)
        {
            return builder.ToString();
        }

        foreach (ExportIkSourceDiagnosticSample sample in samples.OrderBy(row => row.RecorderFrameNumber).ThenBy(row => (int)row.BoneName))
        {
            builder.Append(sample.RecorderFrameNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(sample.UnityFrameNumber.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticFloat(sample.SampleTime));
            builder.Append(',');
            builder.Append(CsvEscape(sample.BoneName.ToString()));
            builder.Append(',');
            builder.Append(((int)sample.BoneName).ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.RootReferencePosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceWorldPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRelativePosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.ExportedUnityPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.DirectFootWorldPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.DirectFootRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.RecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.DirectFootRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRelativePosition - sample.SourceRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.SourceRelativePosition - sample.DirectFootRecorderRootPosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.ExportedUnityPosition - sample.SourceRelativePosition));
            builder.Append(',');
            builder.Append(FormatDiagnosticVector3(sample.ExportedUnityPosition - sample.SourceRecorderRootPosition));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private string WriteExportRotationDiagnosticsCsv(string vmdFilePath)
    {
        LastExportRotationDiagnosticsCsvPath = "";
        LastExportRotationDiagnosticSamplesCsvPath = "";
        if (!EnableExportRotationDiagnostics ||
            exportRotationDiagnosticAggregatesSaved == null ||
            exportRotationDiagnosticAggregatesSaved.Count == 0)
        {
            return "";
        }

        string csvPath = Path.ChangeExtension(vmdFilePath, ".export_rotation_diagnostics.csv");
        File.WriteAllText(csvPath, BuildExportRotationDiagnosticsCsv(exportRotationDiagnosticAggregatesSaved.Values), Encoding.UTF8);
        LastExportRotationDiagnosticsCsvPath = csvPath;
        Debug.Log($"[VMDRecorder] export rotation diagnostics: {csvPath}");
        if (exportRotationDiagnosticSamplesSaved != null && exportRotationDiagnosticSamplesSaved.Count > 0)
        {
            string samplesCsvPath = Path.ChangeExtension(vmdFilePath, ".export_rotation_diagnostic_samples.csv");
            File.WriteAllText(samplesCsvPath, BuildExportRotationDiagnosticSamplesCsv(exportRotationDiagnosticSamplesSaved), Encoding.UTF8);
            LastExportRotationDiagnosticSamplesCsvPath = samplesCsvPath;
            Debug.Log($"[VMDRecorder] export rotation diagnostic samples: {samplesCsvPath}");
        }
        return csvPath;
    }

    private string WriteExportIkSourceDiagnosticsCsv(string vmdFilePath)
    {
        LastExportIkSourceDiagnosticsCsvPath = "";
        if (!EnableExportIkSourceDiagnostics ||
            exportIkSourceDiagnosticSamplesSaved == null ||
            exportIkSourceDiagnosticSamplesSaved.Count == 0)
        {
            return "";
        }

        string csvPath = Path.ChangeExtension(vmdFilePath, ".export_ik_source_samples.csv");
        File.WriteAllText(csvPath, BuildExportIkSourceDiagnosticsCsv(exportIkSourceDiagnosticSamplesSaved), Encoding.UTF8);
        LastExportIkSourceDiagnosticsCsvPath = csvPath;
        Debug.Log($"[VMDRecorder] export IK source diagnostics: {csvPath}");
        return csvPath;
    }

    private static string FormatDiagnosticFloat(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string FormatDiagnosticVector3(Vector3 value)
    {
        return CsvEscape(
            FormatDiagnosticFloat(value.x) + "|" +
            FormatDiagnosticFloat(value.y) + "|" +
            FormatDiagnosticFloat(value.z));
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void AppendQuaternion(StringBuilder builder, Quaternion value)
    {
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.x));
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.y));
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.z));
        builder.Append(',');
        builder.Append(FormatDiagnosticFloat(value.w));
    }

}
