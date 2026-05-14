using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Serialization;

public struct VmdSaveResult
{
    public bool Success;
    public string FilePath;
    public string ErrorMessage;
    public int FrameCount;
    public long FileSizeBytes;

    public static VmdSaveResult Ok(string filePath, int frameCount, long fileSizeBytes)
    {
        return new VmdSaveResult
        {
            Success = true,
            FilePath = filePath,
            ErrorMessage = "",
            FrameCount = frameCount,
            FileSizeBytes = fileSizeBytes
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
            FileSizeBytes = 0
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
    
    /// <summary>
    /// 全ての親の座標・回転を絶対座標系で計算する
    /// UseParentOfAllがTrueでないと意味がない
    /// </summary>
    /// [한글] "全ての親"의 좌표와 회전을 절대 좌표계로 계산 (UseParentOfAll이 true여야 의미 있음)
    public bool UseAbsoluteCoordinateSystem = true;
    
    public bool IgnoreInitialPosition = false;            // 초기 위치 무시 여부
    public bool IgnoreInitialRotation = false;            // 초기 회전 무시 여부
    
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
    [Range(1, 8)] public int MaxRecordedFramesPerLateUpdate = 2;

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
    public Vector3 LeftFootIKOffset = Vector3.zero;           // 왼발 IK 오프셋
    public Vector3 RightFootIKOffset = Vector3.zero;          // 오른발 IK 오프셋
    // New: Toe IK Offset
    public Vector3 LeftToeIKOffset = Vector3.zero;            // 왼발끝 IK 오프셋
    public Vector3 RightToeIKOffset = Vector3.zero;           // 오른발끝 IK 오프셋

    Animator animator;             // 애니메이터 참조
    VmdBoneGhost boneGhost;           // 정규화된 본 구조 (가상 스켈레톤)
    VmdMorphRecorder morphRecorder;   // 모프 레코더 (현재 레코딩용)
    VmdMorphRecorder morphRecorderSaved;  // 모프 레코더 (저장용)
    bool recorderInitializationWarningLogged;
    float recordingFrameAccumulator;
    int lastSavedUnityFrame = -1;
    int sameUnityFrameSaveCount;
    int maxFramesSavedInSingleLateUpdate;
    int droppedLateFrameBacklogCount;
    int previousCaptureFramerate;
    bool captureFramerateApplied;

    public int SameUnityFrameSaveCount => sameUnityFrameSaveCount;
    public int MaxFramesSavedInSingleLateUpdate => maxFramesSavedInSingleLateUpdate;
    public int DroppedLateFrameBacklogCount => droppedLateFrameBacklogCount;

}
