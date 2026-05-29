using Member_Han.Modules.FBXImporter;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RecodingSetting : MonoBehaviour
{
    private const string ManualRecordButtonName = "MMD_Record_Button";

    [Header("수동 녹화")]
    [InspectorName("녹화 FileManager")]
    [Tooltip("FBX 처리 상태와 VMD 녹화 파이프라인을 보유한 FileManager입니다.")]
    [SerializeField] private FileManager recordingFileManager;

    [InspectorName("수동 녹화 버튼")]
    [Tooltip("GameView에서 수동 녹화를 시작하는 UI 버튼입니다.")]
    [SerializeField] private Button manualRecordButton;

    [InspectorName("녹화 대상")]
    [Tooltip("실제 수동 녹화를 시작할 HumanoidSampleCode입니다.")]
    [SerializeField] private HumanoidSampleCode recordingController;

    [Header("화면 녹화 진단")]
    [InspectorName("녹화 진단/캡처 사용")]
    [Tooltip("비교 CSV와 프레임 캡처를 남기는 진단 모드를 켭니다.")]
    [SerializeField] private bool enableRecordingDiagnostics;

    [InspectorName("테스트용 30fps 시간 고정")]
    [Tooltip("회귀 테스트용으로 녹화 중 Unity 시간을 30fps로 고정합니다.")]
    [SerializeField] private bool useDeterministicCaptureFramerateForDiagnostics;

    [InspectorName("손 close-up 캡처")]
    [Tooltip("진단 캡처를 사용할 때 손 close-up 캡처도 함께 남깁니다.")]
    [SerializeField] private bool enableDiagnosticFingerCloseups = true;

    [InspectorName("실행 시작 시 FileManager에 적용")]
    [Tooltip("Play Mode 시작 시 위 진단 설정을 FileManager에 반영합니다.")]
    [SerializeField] private bool applyDiagnosticsToFileManagerOnAwake = true;

    public FileManager RecordingFileManager => recordingFileManager;
    public Button ManualRecordButton => manualRecordButton;
    public HumanoidSampleCode RecordingController => recordingController;
    public bool EnableRecordingDiagnostics => enableRecordingDiagnostics;
    public bool UseDeterministicCaptureFramerateForDiagnostics => useDeterministicCaptureFramerateForDiagnostics;
    public bool EnableDiagnosticFingerCloseups => enableDiagnosticFingerCloseups;

    private void Reset()
    {
        recordingFileManager = FindObjectOfType<FileManager>();
        manualRecordButton = ResolveManualRecordButton();
        recordingController = ResolveRecordingController();
        PullDiagnosticsFromFileManager();
    }

    private void Awake()
    {
        if (applyDiagnosticsToFileManagerOnAwake)
        {
            ApplyDiagnosticsToFileManager();
        }
    }

    public void StartManualRecording()
    {
        FileManager fileManager = ResolveRecordingFileManager();
        HumanoidSampleCode controller = ResolveRecordingController();

        if (fileManager != null && fileManager.IsProcessing)
        {
            const string message = "FBX 처리 중에는 수동 녹화를 시작할 수 없습니다.";
            controller?.SetProcessingStatus(message, 0.1f);
            Debug.LogWarning($"[RecodingSetting] {message}");
            return;
        }

        if (controller == null)
        {
            Debug.LogWarning("[RecodingSetting] 수동 녹화를 시작할 HumanoidSampleCode가 연결되어 있지 않습니다.");
            return;
        }

        ApplyDiagnosticsToFileManager();
        controller.OnManualRecordButtonClick();
    }

    public void SetRecordingFileManager(FileManager fileManager)
    {
        recordingFileManager = fileManager;
        recordingController = ResolveRecordingController();
    }

    public void SetManualRecordButton(Button button)
    {
        manualRecordButton = button;
    }

    public void SetRecordingController(HumanoidSampleCode controller)
    {
        recordingController = controller;
    }

    public void ApplyDiagnosticsToFileManager()
    {
        FileManager fileManager = ResolveRecordingFileManager();
        if (fileManager == null)
        {
            return;
        }

        fileManager.enableRecordingDiagnostics = enableRecordingDiagnostics;
        fileManager.useDeterministicCaptureFramerateForDiagnostics =
            useDeterministicCaptureFramerateForDiagnostics;
        fileManager.enableDiagnosticFingerCloseups = enableDiagnosticFingerCloseups;
    }

    private void PullDiagnosticsFromFileManager()
    {
        FileManager fileManager = ResolveRecordingFileManager();
        if (fileManager == null)
        {
            return;
        }

        enableRecordingDiagnostics = fileManager.enableRecordingDiagnostics;
        useDeterministicCaptureFramerateForDiagnostics =
            fileManager.useDeterministicCaptureFramerateForDiagnostics;
        enableDiagnosticFingerCloseups = fileManager.enableDiagnosticFingerCloseups;
    }

    private FileManager ResolveRecordingFileManager()
    {
        if (recordingFileManager != null)
        {
            return recordingFileManager;
        }

        recordingFileManager = FindObjectOfType<FileManager>();
        return recordingFileManager;
    }

    private HumanoidSampleCode ResolveRecordingController()
    {
        if (recordingController != null)
        {
            return recordingController;
        }

        FileManager fileManager = ResolveRecordingFileManager();
        if (fileManager != null && fileManager.targetCharacter != null)
        {
            recordingController = fileManager.targetCharacter.GetComponent<HumanoidSampleCode>();
        }

        if (recordingController == null)
        {
            recordingController = FindObjectOfType<HumanoidSampleCode>();
        }

        return recordingController;
    }

    private static Button ResolveManualRecordButton()
    {
        GameObject buttonObject = GameObject.Find(ManualRecordButtonName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }
}
