using System;
using System.IO;
using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Settings;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RecordingSetting : MonoBehaviour
{
    private const string ManualRecordButtonName = "MMD_Record_Button";

    [Header("수동 녹화")]
    [InspectorName("녹화 FBXVmdPipeline")]
    [Tooltip("FBX 처리 상태와 VMD 녹화 파이프라인을 보유한 FBXVmdPipeline입니다.")]
    [SerializeField] private FBXVmdPipeline recordingFBXVmdPipeline;

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

    [InspectorName("녹화 캡처 해상도")]
    [Tooltip("MotionComparisonProbe PNG 산출물 해상도입니다. VMD frame/key 품질에는 영향을 주지 않습니다.")]
    [SerializeField] private RecordingCaptureQualityPreset recordingCaptureQuality = RecordingCaptureQualityPreset.Uhd4K;

    [InspectorName("사용자 지정 캡처 폭")]
    [Tooltip("사용자 지정 preset을 선택했을 때 사용할 PNG 캡처 폭입니다.")]
    [SerializeField, Min(RecordingCaptureResolution.MinWidth)] private int customRecordingCaptureWidth = 3840;

    [InspectorName("사용자 지정 캡처 높이")]
    [Tooltip("사용자 지정 preset을 선택했을 때 사용할 PNG 캡처 높이입니다.")]
    [SerializeField, Min(RecordingCaptureResolution.MinHeight)] private int customRecordingCaptureHeight = 2160;

    [InspectorName("실행 시작 시 FBXVmdPipeline에 적용")]
    [Tooltip("Play Mode 시작 시 위 진단 설정을 FBXVmdPipeline에 반영합니다.")]
    [SerializeField] private bool applyDiagnosticsToFBXVmdPipelineOnAwake = true;

    [Header("설정 팝업")]
    [InspectorName("런타임 설정 팝업")]
    [Tooltip("Main_Recoding 실행 중 자동으로 열 설정 팝업입니다. 비어 있으면 UI_Canvas 아래에 생성합니다.")]
    [SerializeField] private MainRecordingSettingsPopup settingsPopup;

    [InspectorName("시작 시 설정 팝업 열기")]
    [Tooltip("빌드 실행 또는 Play Mode 시작 시 설정 팝업을 자동으로 한 번 엽니다.")]
    [SerializeField] private bool openSettingsPopupOnStart = true;

    [Header("공유 설정 파일")]
    [InspectorName("시작 시 공유 설정 로드")]
    [Tooltip("Play 시작 시 companion/Editor mirror가 저장한 공유 설정 파일을 읽습니다.")]
    [SerializeField] private bool loadSharedSettingsOnAwake = true;

    [InspectorName("공유 설정 파일 override")]
    [Tooltip("비워 두면 기본 공유 설정 파일 경로를 사용합니다. 테스트와 진단에서만 직접 지정합니다.")]
    [SerializeField] private string sharedSettingsFilePathOverride;

    [InspectorName("공유 설정 polling 간격")]
    [Tooltip("실행 중 공유 설정 파일의 LastWriteTimeUtc 변경을 확인하는 간격입니다.")]
    [SerializeField, Min(0.1f)] private float sharedSettingsPollingIntervalSeconds = 1f;

    public FBXVmdPipeline RecordingFBXVmdPipeline => recordingFBXVmdPipeline;
    public Button ManualRecordButton => manualRecordButton;
    public HumanoidSampleCode RecordingController => recordingController;
    public bool EnableRecordingDiagnostics => enableRecordingDiagnostics;
    public bool UseDeterministicCaptureFramerateForDiagnostics => useDeterministicCaptureFramerateForDiagnostics;
    public bool EnableDiagnosticFingerCloseups => enableDiagnosticFingerCloseups;
    public RecordingCaptureQualityPreset RecordingCaptureQuality => recordingCaptureQuality;
    public int CustomRecordingCaptureWidth => customRecordingCaptureWidth;
    public int CustomRecordingCaptureHeight => customRecordingCaptureHeight;
    public MainRecordingSettingsPopup SettingsPopup => settingsPopup;
    public bool OpenSettingsPopupOnStart => openSettingsPopupOnStart;
    public bool LoadSharedSettingsOnAwake => loadSharedSettingsOnAwake;
    public float SharedSettingsPollingIntervalSeconds => sharedSettingsPollingIntervalSeconds;

    private MainRecordingSettingsStore sharedSettingsStore;
    private string resolvedSharedSettingsFilePath = string.Empty;
    private DateTime lastSharedSettingsWriteTimeUtc = DateTime.MinValue;
    private float nextSharedSettingsPollTime;
    private readonly MainRecordingSettingsFbxImportCommandProcessor sharedSettingsFbxImportCommandProcessor =
        new MainRecordingSettingsFbxImportCommandProcessor();
    private static readonly Func<FBXVmdPipeline, string, bool> DefaultSharedSettingsFbxImportStarter =
        (fileManager, path) => fileManager.TryStartFbxImportFromSharedSettings(path);
    private Func<FBXVmdPipeline, string, bool> sharedSettingsFbxImportStarter =
        DefaultSharedSettingsFbxImportStarter;

#if UNITY_EDITOR
    public Func<FBXVmdPipeline, string, bool> SharedSettingsFbxImportStarterForTests
    {
        get => sharedSettingsFbxImportStarter;
        set => sharedSettingsFbxImportStarter = value ?? DefaultSharedSettingsFbxImportStarter;
    }
#endif

    private void Reset()
    {
        manualRecordButton = ResolveManualRecordButton();
        recordingController = ResolveRecordingController();
        PullDiagnosticsFromFBXVmdPipeline();
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            WriteRuntimePlayModeStateQuietly(MainRecordingSettingsState.Playing);
        }

        if (loadSharedSettingsOnAwake)
        {
            LoadSharedSettings();
        }

        if (applyDiagnosticsToFBXVmdPipelineOnAwake)
        {
            ApplyDiagnosticsToFBXVmdPipeline();
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            WriteRuntimePlayModeStateQuietly(MainRecordingSettingsState.Stopped);
            if (!Application.isEditor)
            {
                MainRecordingSettingsLauncher.CloseStartedProcessQuietly();
            }
        }
    }

    private void Start()
    {
        if (MainRecordingSettingsLauncher.ShouldAutoLaunchForPlayer(
                openSettingsPopupOnStart,
                Application.isEditor,
                Application.isBatchMode))
        {
            MainRecordingSettingsActionResult launchResult =
                MainRecordingSettingsLauncher.TryLaunchForPlayer(
                    openSettingsPopupOnStart,
                    ResolveSharedSettingsFilePathForExternalLauncher());
            if (!launchResult.Succeeded)
            {
                Debug.LogWarning($"[RecordingSetting] {launchResult.UserMessage}");
                OpenSettingsPopup();
            }

            return;
        }

        if (MainRecordingSettingsSurfacePolicy.ShouldAutoOpenRuntimePopup(
                openSettingsPopupOnStart,
                Application.isEditor,
                Application.isBatchMode))
        {
            OpenSettingsPopup();
        }
    }

    private void Update()
    {
        if (!loadSharedSettingsOnAwake)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (now < nextSharedSettingsPollTime)
        {
            return;
        }

        nextSharedSettingsPollTime = now + Mathf.Max(0.1f, sharedSettingsPollingIntervalSeconds);
        PollSharedSettingsIfChanged();
    }

    public void StartManualRecording()
    {
        FBXVmdPipeline fileManager = recordingFBXVmdPipeline;
        HumanoidSampleCode controller = ResolveRecordingController();

        if (fileManager != null && fileManager.IsProcessing)
        {
            const string message = "FBX 처리 중에는 수동 녹화를 시작할 수 없습니다.";
            controller?.SetProcessingStatus(message, 0.1f);
            Debug.LogWarning($"[RecordingSetting] {message}");
            return;
        }

        if (controller == null)
        {
            Debug.LogWarning("[RecordingSetting] 수동 녹화를 시작할 HumanoidSampleCode가 연결되어 있지 않습니다.");
            return;
        }

        ApplyDiagnosticsToFBXVmdPipeline();
        controller.OnManualRecordButtonClick();
    }

    public void SetRecordingFBXVmdPipeline(FBXVmdPipeline fileManager)
    {
        recordingFBXVmdPipeline = fileManager;
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

    public MainRecordingSettingsPopup EnsureSettingsPopup()
    {
        if (settingsPopup == null)
        {
            settingsPopup = MainRecordingSettingsPopup.EnsurePopupForScene(this);
        }

        settingsPopup.Bind(this, recordingFBXVmdPipeline);
        return settingsPopup;
    }

    public void OpenSettingsPopup()
    {
        EnsureSettingsPopup()?.Open();
    }

    public void CloseSettingsPopup()
    {
        if (settingsPopup != null)
        {
            settingsPopup.Close();
        }
    }

    public void ApplyDiagnosticsToFBXVmdPipeline()
    {
        FBXVmdPipeline fileManager = recordingFBXVmdPipeline;
        ApplyDiagnosticsToResolvedFBXVmdPipeline(fileManager);
    }

    public MainRecordingSettingsActionResult LoadSharedSettings()
    {
        try
        {
            sharedSettingsStore = CreateSharedSettingsStore();
            resolvedSharedSettingsFilePath = sharedSettingsStore.SettingsFilePath;
            MainRecordingSettingsDocument document = sharedSettingsStore.LoadOrCreateDefault();
            lastSharedSettingsWriteTimeUtc = sharedSettingsStore.ResolveLastWriteTimeUtc();
            return ApplySharedSettingsDocument(
                document,
                recordingFBXVmdPipeline,
                startFbxImport: false,
                clearPendingCommandWhenSkipped: true);
        }
        catch (Exception exception)
        {
            string message = "공유 설정 로드에 실패했습니다.";
            Debug.LogWarning($"[RecordingSetting] {message} {exception.Message}");
            return MainRecordingSettingsActionResult.Failure(message);
        }
    }

    public MainRecordingSettingsActionResult PollSharedSettingsIfChanged()
    {
        try
        {
            EnsureSharedSettingsStore();
            DateTime currentWriteTime = sharedSettingsStore.ResolveLastWriteTimeUtc();
            if (currentWriteTime <= lastSharedSettingsWriteTimeUtc)
            {
                return MainRecordingSettingsActionResult.Success("공유 설정 변경 없음");
            }

            MainRecordingSettingsDocument document = sharedSettingsStore.LoadOrCreateDefault();
            lastSharedSettingsWriteTimeUtc = currentWriteTime;
            return ApplySharedSettingsDocument(document, recordingFBXVmdPipeline, true);
        }
        catch (Exception exception)
        {
            string message = "공유 설정 갱신에 실패했습니다.";
            Debug.LogWarning($"[RecordingSetting] {message} {exception.Message}");
            return MainRecordingSettingsActionResult.Failure(message);
        }
    }

    private void WriteRuntimePlayModeStateQuietly(string playMode)
    {
        MainRecordingSettingsActionResult result = WriteRuntimePlayModeState(playMode);
        if (!result.Succeeded)
        {
            Debug.LogWarning($"[RecordingSetting] {result.UserMessage}");
        }
    }

    private MainRecordingSettingsActionResult WriteRuntimePlayModeState(string playMode)
    {
        try
        {
            EnsureSharedSettingsStore();
            MainRecordingSettingsDocument document = sharedSettingsStore.LoadOrCreateDefault();
            document.runtimeState = MainRecordingSettingsState.Create(playMode, DateTime.UtcNow);
            sharedSettingsStore.Save(document);
            lastSharedSettingsWriteTimeUtc = sharedSettingsStore.ResolveLastWriteTimeUtc();
            return MainRecordingSettingsActionResult.Success("Play Mode 상태를 기록했습니다.");
        }
        catch (Exception exception)
        {
            return MainRecordingSettingsActionResult.Failure($"Play Mode 상태 기록에 실패했습니다: {exception.Message}");
        }
    }

    public MainRecordingSettingsActionResult ApplySharedSettingsDocument(
        MainRecordingSettingsDocument document,
        FBXVmdPipeline fileManager = null,
        bool startFbxImport = true,
        bool clearPendingCommandWhenSkipped = false)
    {
        if (document == null)
        {
            return MainRecordingSettingsActionResult.Failure("공유 설정 문서가 비어 있습니다.");
        }

        openSettingsPopupOnStart = document.openSettingsOnStart;
        RecordingCaptureResolutionPlan capturePlan = RecordingCaptureResolution.CreateCustomPlan(
            document.captureWidth,
            document.captureHeight);
        recordingCaptureQuality = RecordingCaptureQualityPreset.Custom;
        customRecordingCaptureWidth = capturePlan.Width;
        customRecordingCaptureHeight = capturePlan.Height;

        FBXVmdPipeline resolvedFBXVmdPipeline = fileManager != null ? fileManager : recordingFBXVmdPipeline;
        if (resolvedFBXVmdPipeline != null)
        {
            ApplyDiagnosticsToResolvedFBXVmdPipeline(resolvedFBXVmdPipeline);
        }

        Func<string, bool> tryStartFbxImport = resolvedFBXVmdPipeline == null
            ? null
            : path => sharedSettingsFbxImportStarter(resolvedFBXVmdPipeline, path);
        if (sharedSettingsFbxImportCommandProcessor.TryProcess(
                document,
                startFbxImport,
                clearPendingCommandWhenSkipped,
                File.Exists,
                tryStartFbxImport,
                PersistConsumedSharedSettingsDocumentQuietly,
                out MainRecordingSettingsActionResult commandResult))
        {
            return commandResult;
        }

        return MainRecordingSettingsActionResult.Success("공유 설정을 적용했습니다.");
    }

    private void PersistConsumedSharedSettingsDocumentQuietly(MainRecordingSettingsDocument document)
    {
        if (sharedSettingsStore == null)
        {
            return;
        }

        try
        {
            sharedSettingsStore.Save(document);
            lastSharedSettingsWriteTimeUtc = sharedSettingsStore.ResolveLastWriteTimeUtc();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RecordingSetting] 처리한 FBX 명령을 지우지 못했습니다: {exception.Message}");
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

    private void PullDiagnosticsFromFBXVmdPipeline()
    {
        FBXVmdPipeline fileManager = recordingFBXVmdPipeline;
        if (fileManager == null)
        {
            return;
        }

        enableRecordingDiagnostics = fileManager.enableRecordingDiagnostics;
        useDeterministicCaptureFramerateForDiagnostics =
            fileManager.useDeterministicCaptureFramerateForDiagnostics;
        enableDiagnosticFingerCloseups = fileManager.enableDiagnosticFingerCloseups;
        recordingCaptureQuality = fileManager.recordingCaptureQuality;
        customRecordingCaptureWidth = fileManager.customRecordingCaptureWidth;
        customRecordingCaptureHeight = fileManager.customRecordingCaptureHeight;
    }

    private void ApplyDiagnosticsToResolvedFBXVmdPipeline(FBXVmdPipeline fileManager)
    {
        if (fileManager == null)
        {
            return;
        }

        fileManager.enableRecordingDiagnostics = enableRecordingDiagnostics;
        fileManager.useDeterministicCaptureFramerateForDiagnostics =
            useDeterministicCaptureFramerateForDiagnostics;
        fileManager.enableDiagnosticFingerCloseups = enableDiagnosticFingerCloseups;
        fileManager.recordingCaptureQuality = recordingCaptureQuality;
        fileManager.customRecordingCaptureWidth = customRecordingCaptureWidth;
        fileManager.customRecordingCaptureHeight = customRecordingCaptureHeight;
    }

    private MainRecordingSettingsStore CreateSharedSettingsStore()
    {
        return new MainRecordingSettingsStore(sharedSettingsFilePathOverride);
    }

    private string ResolveSharedSettingsFilePathForExternalLauncher()
    {
        if (!string.IsNullOrWhiteSpace(resolvedSharedSettingsFilePath))
        {
            return resolvedSharedSettingsFilePath;
        }

        if (sharedSettingsStore != null)
        {
            return sharedSettingsStore.SettingsFilePath;
        }

        if (!string.IsNullOrWhiteSpace(sharedSettingsFilePathOverride))
        {
            return sharedSettingsFilePathOverride;
        }

        return MainRecordingSettingsPathResolver.ResolveSettingsFilePath();
    }

    private void EnsureSharedSettingsStore()
    {
        if (sharedSettingsStore != null)
        {
            return;
        }

        sharedSettingsStore = CreateSharedSettingsStore();
        resolvedSharedSettingsFilePath = sharedSettingsStore.SettingsFilePath;
        lastSharedSettingsWriteTimeUtc = sharedSettingsStore.ResolveLastWriteTimeUtc();
    }

    private HumanoidSampleCode ResolveRecordingController()
    {
        if (recordingController != null)
        {
            return recordingController;
        }

        FBXVmdPipeline fileManager = recordingFBXVmdPipeline;
        if (fileManager != null && fileManager.targetCharacter != null)
        {
            recordingController = fileManager.targetCharacter.GetComponent<HumanoidSampleCode>();
        }

        return recordingController;
    }

    private static Button ResolveManualRecordButton()
    {
        GameObject buttonObject = GameObject.Find(ManualRecordButtonName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    public MainRecordingSettingsActionResult LoadSharedSettingsFromPathForTests(string path)
    {
        sharedSettingsFilePathOverride = path;
        return LoadSharedSettings();
    }

#if UNITY_EDITOR
    public MainRecordingSettingsActionResult WriteRuntimePlayModeStateForTests(string playMode)
    {
        return WriteRuntimePlayModeState(playMode);
    }
#endif
}
