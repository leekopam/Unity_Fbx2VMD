using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class HumanoidSampleCode : MonoBehaviour
{
    [Header("Core References")]
    public UnityHumanoidVMDRecorder vmdRecorder;

    [Header("UI References")]
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private TextMeshProUGUI _progressText;

    [Header("Recording Settings")]
    public string ModelName = "fbxToVMD";
    public string HumanoidVMDName = "fbxToVMD.vmd";

    [SerializeField, Min(0f)] private float StartRecordingTime = 0.1f;
    [SerializeField] private bool resetAnimatorOnManualStart = true;
    [SerializeField] private bool finishByRecordedFrameCount = true;
    [SerializeField] private bool enableMotionComparisonProbe = false;
    [SerializeField] private bool probeFingerCloseups = true;
    [SerializeField, Min(128)] private int probeScreenshotWidth = 960;
    [SerializeField, Min(128)] private int probeScreenshotHeight = 960;
    [SerializeField, Range(MinProbeScreenshotPadding, MaxProbeScreenshotPadding)] private float probeScreenshotPadding = DefaultProbeScreenshotPadding;
    [SerializeField, Range(MinProbeScreenshotVerticalViewportCenter, MaxProbeScreenshotVerticalViewportCenter)] private float probeScreenshotVerticalViewportCenter = DefaultProbeScreenshotVerticalViewportCenter;

    [HideInInspector] public int StopRecordingTime = 0;
    [HideInInspector] public bool AutoStartRecording = false;

    public event Action<VmdSaveResult> RecordingFinished;

    public bool IsRecordingSessionActive => _isRecordingSessionActive || _isSaving;
    public string LastSavedFilePath => _lastSavedFilePath;
    public int ProbeScreenshotWidth => probeScreenshotWidth;
    public int ProbeScreenshotHeight => probeScreenshotHeight;
    public float ProbeScreenshotPadding => probeScreenshotPadding;
    public float ProbeScreenshotVerticalViewportCenter => probeScreenshotVerticalViewportCenter;

    private bool _isRecordingSessionActive = false;
    private bool _isSaving = false;
    private float _totalDuration = 0f;
    private float _currentTimer = 0f;
    private int _targetFrameCount = 0;
    private bool _finishCurrentSessionByRecordedFrameCount = true;
    private string _outputFolderPath = "";
    private string _outputFilePath = "";
    private string _lastSavedFilePath = "";
    private float[] _probeSampleTimesOverride;
    private Coroutine _manualRecordingCoroutine;
    private TransformJitterProbe _jitterProbe;
    private float _nextUiUpdateTime;
    private float _lastUiProgress = -1f;
    private int _lastUiTimeTenths = -1;
    private string _lastUiStatus = "";
    private Text _progressFallbackText;
    private bool _usingLegacyProgressText;
    private HumanoidRecordingSession _recordingSession;

    private const float RecordingFrameRate = 30f;
    private const float UiUpdateInterval = 0.1f;
    private const int MinProbeScreenshotWidth = 128;
    private const int MinProbeScreenshotHeight = 128;
    private const int MaxProbeScreenshotWidth = 7680;
    private const int MaxProbeScreenshotHeight = 4320;
    private const float DefaultProbeScreenshotPadding = 1.8f;
    private const float DefaultProbeScreenshotVerticalViewportCenter = 0.28f;
    private const float MinProbeScreenshotPadding = 0.25f;
    private const float MaxProbeScreenshotPadding = 2f;
    private const float MinProbeScreenshotVerticalViewportCenter = 0f;
    private const float MaxProbeScreenshotVerticalViewportCenter = 1f;
    private const string KoreanProgressTextSample = "가나다파일선택녹화저장완료오류";
    private static readonly string[] KoreanUiFontNames =
    {
        "Malgun Gothic",
        "맑은 고딕",
        "Noto Sans KR",
        "Noto Sans CJK KR",
        "NanumGothic",
        "Nanum Gothic"
    };
    private static TMP_FontAsset _cachedKoreanUiFont;
    private static Font _cachedKoreanLegacyUiFont;
    private bool _progressTextFontChecked;
    private const string EditorAutoStartSuppressionKey = "Fbx2Vmd.YybVisualComparison.SuppressManualAutoStart";

    private void Start()
    {
        _recordingSession ??= new HumanoidRecordingSession(RecordingFrameRate);
        EnsureRecorder();
        EnsureProgressTextKoreanFont();

        if (!_isRecordingSessionActive && !_isSaving && vmdRecorder != null && vmdRecorder.IsRecording)
        {
            vmdRecorder.StopRecording();
        }

        if (!_isRecordingSessionActive && !_isSaving)
        {
            SetReady("FBX를 선택하세요");
        }

        if (ShouldAutoStartRecording() && HasAnimatorClip())
        {
            _manualRecordingCoroutine = StartCoroutine(StartManualRecordingSequence());
        }
    }

#if UNITY_EDITOR
    public static void SetEditorAutoStartSuppressed(bool suppressed)
    {
        SessionState.SetBool(EditorAutoStartSuppressionKey, suppressed);
    }
#endif

    private bool ShouldAutoStartRecording()
    {
#if UNITY_EDITOR
        if (SessionState.GetBool(EditorAutoStartSuppressionKey, false))
        {
            return false;
        }
#endif
        return AutoStartRecording;
    }

    public bool StartAutoRecording(
        float clipLength,
        string fileName = "",
        string outputDirectory = null,
        int targetFrameCount = 0,
        string comparisonLabel = "",
        bool overwriteExistingOutput = false)
    {
        if (!EnsureRecorder())
        {
            SetError("VMD 레코더가 없습니다.");
            return false;
        }

        if (_isRecordingSessionActive || _isSaving)
        {
            SetError("이전 녹화가 아직 끝나지 않았습니다.");
            return false;
        }

        if (clipLength <= 0f || float.IsNaN(clipLength) || float.IsInfinity(clipLength))
        {
            SetError("애니메이션 길이가 올바르지 않습니다.");
            return false;
        }

        string outputBaseName = SanitizeFileName(string.IsNullOrWhiteSpace(fileName) ? "fbxToVMD" : fileName);
        ModelName = outputBaseName;
        HumanoidVMDName = EnsureVmdExtension(outputBaseName);
        _outputFolderPath = ResolveOutputFolder(outputDirectory);
        _outputFilePath = overwriteExistingOutput
            ? BuildExactOutputPath(_outputFolderPath, HumanoidVMDName)
            : BuildUniqueOutputPath(_outputFolderPath, HumanoidVMDName);

        _totalDuration = clipLength;
        _targetFrameCount = targetFrameCount > 0 ? targetFrameCount : Mathf.CeilToInt(clipLength * RecordingFrameRate);
        _finishCurrentSessionByRecordedFrameCount = ShouldFinishCurrentSessionByRecordedFrameCount();
        _recordingSession ??= new HumanoidRecordingSession(RecordingFrameRate);
        _recordingSession.Start(_totalDuration, _targetFrameCount, _finishCurrentSessionByRecordedFrameCount);
        _currentTimer = 0f;
        StopRecordingTime = Mathf.CeilToInt(clipLength);

        string labelText = string.IsNullOrWhiteSpace(comparisonLabel) ? "(파일명과 동일)" : comparisonLabel;
        Debug.Log($"[Recorder] Recording started: VMD={Path.GetFileName(_outputFilePath)}, comparison_label={labelText}, overwrite={overwriteExistingOutput}, duration={_totalDuration:F2}s, target={_targetFrameCount}frames");

        vmdRecorder.StopRecording();
        vmdRecorder.ResetRecordingBuffersForNewSession(_targetFrameCount + 4);
        vmdRecorder.StartRecording();
        _isRecordingSessionActive = true;
        StartComparisonProbe(comparisonLabel);
        UpdateUI(0f, 0f, $"녹화 중: {ModelName}");
        return true;
    }

    public void SetReady(string message)
    {
        _isRecordingSessionActive = false;
        _isSaving = false;
        UpdateUI(0f, 0f, message);
    }

    public void SetLoading(string message)
    {
        UpdateUI(0f, 0f, message);
    }

    public void SetProcessingStatus(string message, float progress = 0f)
    {
        UpdateUI(Mathf.Clamp01(progress), 0f, message);
    }

    public void SetError(string message)
    {
        _isRecordingSessionActive = false;
        _isSaving = false;
        Debug.LogError($"[Recorder] {message}");
        UpdateUI(0f, 0f, $"오류: {message}");
    }

    private void Update()
    {
        if (!_isRecordingSessionActive || vmdRecorder == null) return;

        if (_recordingSession == null)
        {
            _recordingSession = new HumanoidRecordingSession(RecordingFrameRate);
            _recordingSession.Start(_totalDuration, _targetFrameCount, _finishCurrentSessionByRecordedFrameCount);
            Debug.LogWarning("[Recorder] Recording session state was restored after a script reload.");
        }

        HumanoidRecordingTick tick = _recordingSession.Tick(Time.deltaTime, vmdRecorder.FrameNumber);
        _currentTimer = _recordingSession.CurrentTimerSeconds;

        float displayTime = tick.DisplayTimeSeconds;
        float progress = tick.Progress01;
        UpdateUI(progress, displayTime, $"녹화 중: {ModelName}");

        if (tick.ShouldFinish)
        {
            FinishRecording();
        }
    }

    private async void FinishRecording()
    {
        if (!_isRecordingSessionActive || _isSaving) return;

        _isRecordingSessionActive = false;
        _isSaving = true;
        _manualRecordingCoroutine = null;
        _recordingSession?.Stop();

        Debug.Log("[Recorder] Recording duration reached. Starting save.");
        UpdateUI(1f, _totalDuration, "VMD 저장 중");

        StopComparisonProbe(resultReason: "finish");
        vmdRecorder.StopRecording();
        VmdSaveResult result = await vmdRecorder.SaveVMDAsync(ModelName, _outputFilePath);

        _isSaving = false;

        if (result.Success)
        {
            _lastSavedFilePath = result.FilePath;
            Debug.Log($"[Recorder] Save completed: {result.FilePath} ({result.FileSizeBytes} bytes, {result.FrameCount} frames)");
            if (!string.IsNullOrEmpty(result.ExportRotationDiagnosticsCsvPath))
            {
                Debug.Log($"[Recorder] export rotation diagnostics: {result.ExportRotationDiagnosticsCsvPath}");
            }
            UpdateUI(1f, _totalDuration, $"저장 완료됨: {Path.GetFileName(result.FilePath)}");
            Invoke(nameof(OpenTargetFolder), 0.5f);
        }
        else
        {
            string error = string.IsNullOrEmpty(result.ErrorMessage) ? "Unknown save error" : result.ErrorMessage;
            Debug.LogError($"[Recorder] Save failed: {error}");
            UpdateUI(0f, _currentTimer, $"Save failed: {error}");
        }

        RecordingFinished?.Invoke(result);
    }

    private bool EnsureRecorder()
    {
        if (vmdRecorder == null)
        {
            vmdRecorder = GetComponent<UnityHumanoidVMDRecorder>();
        }

        return vmdRecorder != null;
    }

    public void SetRecordingDiagnostics(
        bool enableProbe,
        bool enableFingerCloseups,
        bool useCaptureFramerateForRegression,
        float[] sampleTimesOverride = null,
        int screenshotWidth = 960,
        int screenshotHeight = 960,
        float screenshotPadding = DefaultProbeScreenshotPadding,
        float screenshotVerticalViewportCenter = DefaultProbeScreenshotVerticalViewportCenter)
    {
        enableMotionComparisonProbe = enableProbe;
        probeFingerCloseups = enableFingerCloseups;
        SetProbeScreenshotCaptureResolution(screenshotWidth, screenshotHeight);
        SetProbeScreenshotFraming(screenshotPadding, screenshotVerticalViewportCenter);
        _probeSampleTimesOverride = sampleTimesOverride != null && sampleTimesOverride.Length > 0
            ? (float[])sampleTimesOverride.Clone()
            : null;

        if (EnsureRecorder())
        {
            vmdRecorder.EnableExportRotationDiagnostics = enableProbe;
            vmdRecorder.EnableExportIkSourceDiagnostics = enableProbe;
            vmdRecorder.UseCaptureFramerateDuringRecording = useCaptureFramerateForRegression;
            vmdRecorder.MaxRecordedFramesPerLateUpdate = useCaptureFramerateForRegression
                ? Mathf.Max(1, vmdRecorder.MaxRecordedFramesPerLateUpdate)
                : 2;
            vmdRecorder.DropLateFrameBacklogWhenNotUsingCaptureFramerate = !useCaptureFramerateForRegression;
        }
    }

    public void SetProbeScreenshotCaptureResolution(int width, int height)
    {
        probeScreenshotWidth = Mathf.Clamp(width, MinProbeScreenshotWidth, MaxProbeScreenshotWidth);
        probeScreenshotHeight = Mathf.Clamp(height, MinProbeScreenshotHeight, MaxProbeScreenshotHeight);
    }

    public void SetProbeScreenshotFraming(float padding, float verticalViewportCenter)
    {
        probeScreenshotPadding = NormalizeProbeScreenshotPadding(padding);
        probeScreenshotVerticalViewportCenter = NormalizeProbeScreenshotVerticalViewportCenter(verticalViewportCenter);
    }

    private static float NormalizeProbeScreenshotPadding(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultProbeScreenshotPadding;
        }

        return Mathf.Clamp(value, MinProbeScreenshotPadding, MaxProbeScreenshotPadding);
    }

    private static float NormalizeProbeScreenshotVerticalViewportCenter(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultProbeScreenshotVerticalViewportCenter;
        }

        return Mathf.Clamp(value, MinProbeScreenshotVerticalViewportCenter, MaxProbeScreenshotVerticalViewportCenter);
    }

    private bool ShouldFinishCurrentSessionByRecordedFrameCount()
    {
        if (!finishByRecordedFrameCount || _targetFrameCount <= 0)
        {
            return false;
        }

        if (vmdRecorder == null)
        {
            return true;
        }

        return vmdRecorder.UseCaptureFramerateDuringRecording ||
            !vmdRecorder.DropLateFrameBacklogWhenNotUsingCaptureFramerate;
    }

    private void StartComparisonProbe(string label)
    {
        if (!enableMotionComparisonProbe)
        {
            return;
        }

        MotionComparisonProbe probe = GetComponent<MotionComparisonProbe>();
        if (probe == null)
        {
            probe = gameObject.AddComponent<MotionComparisonProbe>();
        }

        string probeLabel = string.IsNullOrWhiteSpace(label) ? ModelName : label;
        probe.SetScreenshotCaptureResolution(probeScreenshotWidth, probeScreenshotHeight);
        probe.SetScreenshotFraming(probeScreenshotPadding, probeScreenshotVerticalViewportCenter);
        probe.SetFingerCloseups(probeFingerCloseups);
        if (_probeSampleTimesOverride != null && _probeSampleTimesOverride.Length > 0)
        {
            probe.SetSampleTimes(_probeSampleTimesOverride);
        }
        else
        {
            probe.ResetSampleTimesToDefault();
        }

        probe.StartSampling(probeLabel);
        StartJitterProbe(probeLabel);
    }

    private void StopComparisonProbe(string resultReason)
    {
        MotionComparisonProbe probe = GetComponent<MotionComparisonProbe>();
        if (probe != null && probe.IsSampling)
        {
            probe.StopSampling(resultReason);
        }

        if (_jitterProbe != null && _jitterProbe.IsSampling)
        {
            _jitterProbe.StopSampling(resultReason);
        }
    }

    private void StartJitterProbe(string label)
    {
        _jitterProbe = GetComponent<TransformJitterProbe>();
        if (_jitterProbe == null)
        {
            _jitterProbe = gameObject.AddComponent<TransformJitterProbe>();
        }

        _jitterProbe.StartSampling(label);
    }

    private static string ResolveOutputFolder(string outputDirectory)
    {
        string folderPath = string.IsNullOrWhiteSpace(outputDirectory)
            ? GetDefaultOutputFolder()
            : outputDirectory;

        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    private static string GetDefaultOutputFolder()
    {
        return Path.Combine(Application.dataPath, "VMDRecorderSample");
    }

    private static string BuildExactOutputPath(string folderPath, string fileName)
    {
        return Path.Combine(folderPath, EnsureVmdExtension(fileName));
    }

    private static string BuildUniqueOutputPath(string folderPath, string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = Path.Combine(folderPath, fileName);
        int index = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folderPath, $"{baseName}_{index:000}{extension}");
            index++;
        }

        return candidate;
    }

    private static string EnsureVmdExtension(string fileName)
    {
        string cleanName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            cleanName = "fbxToVMD";
        }

        return cleanName + ".vmd";
    }

    private static string SanitizeFileName(string fileName)
    {
        string cleanName = string.IsNullOrWhiteSpace(fileName) ? "fbxToVMD" : fileName.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            cleanName = cleanName.Replace(invalidChar, '_');
        }

        return cleanName;
    }

    private void OpenTargetFolder()
    {
        if (string.IsNullOrWhiteSpace(_outputFolderPath) || !Directory.Exists(_outputFolderPath))
        {
            return;
        }

        string folderPath = _outputFolderPath.Replace("/", "\\");
        Debug.Log($"[Recorder] Open explorer: {folderPath}");
        Application.OpenURL(folderPath);
    }

    private void UpdateUI(float progress, float currentTime, string status)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        int timeTenths = Mathf.FloorToInt(Mathf.Max(0f, currentTime) * 10f);
        bool statusChanged = !string.Equals(_lastUiStatus, status, StringComparison.Ordinal);
        bool forceUpdate = statusChanged || clampedProgress <= 0f || clampedProgress >= 1f;
        bool shouldUpdate = forceUpdate ||
            Time.unscaledTime >= _nextUiUpdateTime ||
            Mathf.Abs(clampedProgress - _lastUiProgress) >= 0.01f ||
            timeTenths != _lastUiTimeTenths;

        if (!shouldUpdate)
        {
            return;
        }

        _nextUiUpdateTime = Time.unscaledTime + UiUpdateInterval;
        _lastUiProgress = clampedProgress;
        _lastUiTimeTenths = timeTenths;
        _lastUiStatus = status;

        if (_progressSlider != null)
        {
            _progressSlider.value = clampedProgress;
        }

        if (_progressText != null)
        {
            EnsureProgressTextKoreanFont();
            string totalText = StopRecordingTime > 0 ? StopRecordingTime.ToString() : "-";
            SetProgressText($"{status} {currentTime:F1}s / {totalText}s");
        }
    }

    private void SetProgressText(string text)
    {
        string displayText = NormalizeUiDisplayText(text);

        if (_progressText != null)
        {
            _progressText.text = displayText;
        }

        if (_progressFallbackText != null)
        {
            _progressFallbackText.text = displayText;
        }
    }

    private static string NormalizeUiDisplayText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string repaired = TryRepairUtf8Mojibake(text);
        return IsBetterKoreanText(text, repaired) ? repaired : text;
    }

    private static string TryRepairUtf8Mojibake(string text)
    {
        try
        {
            byte[] bytes = Encoding.GetEncoding(1252).GetBytes(text);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return text;
        }
    }

    private static bool IsBetterKoreanText(string original, string candidate)
    {
        if (string.IsNullOrEmpty(candidate) || candidate.IndexOf('\uFFFD') >= 0)
        {
            return false;
        }

        int originalHangul = CountHangulCharacters(original);
        int candidateHangul = CountHangulCharacters(candidate);
        if (candidateHangul <= originalHangul)
        {
            return false;
        }

        return CountMojibakeSignals(candidate) < CountMojibakeSignals(original);
    }

    private static int CountHangulCharacters(string text)
    {
        int count = 0;
        foreach (char character in text)
        {
            if ((character >= '\uAC00' && character <= '\uD7A3') ||
                (character >= '\u1100' && character <= '\u11FF') ||
                (character >= '\u3130' && character <= '\u318F'))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountMojibakeSignals(string text)
    {
        int count = 0;
        foreach (char character in text)
        {
            if (character == '\uFFFD' ||
                (character >= '\u00C0' && character <= '\u00FF') ||
                (character >= '\u2018' && character <= '\u201E'))
            {
                count++;
            }
        }

        return count;
    }

    private void EnsureProgressTextKoreanFont()
    {
        if (_progressText == null || _progressTextFontChecked)
        {
            return;
        }

        _progressTextFontChecked = true;
        if (FontAssetSupportsKorean(_progressText.font))
        {
            return;
        }

        if (TryEnableLegacyKoreanProgressText())
        {
            return;
        }

        TMP_FontAsset koreanFont = GetOrCreateKoreanUiFont();
        if (koreanFont != null)
        {
            _progressText.font = koreanFont;
            _progressText.SetAllDirty();
            return;
        }

        Debug.LogWarning("[Recorder] Could not find Korean font for Current_File UI. Check Windows Korean font installation.");
    }

    private bool TryEnableLegacyKoreanProgressText()
    {
        if (_usingLegacyProgressText)
        {
            return _progressFallbackText != null;
        }

        Font legacyFont = GetOrCreateKoreanLegacyUiFont();
        if (legacyFont == null)
        {
            return false;
        }

        Transform existing = _progressText.transform.Find("Current_File_KoreanTextFallback");
        GameObject fallbackObject = existing != null
            ? existing.gameObject
            : new GameObject("Current_File_KoreanTextFallback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));

        fallbackObject.transform.SetParent(_progressText.transform, false);
        RectTransform rectTransform = fallbackObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        _progressFallbackText = fallbackObject.GetComponent<Text>();
        _progressFallbackText.font = legacyFont;
        _progressFallbackText.fontSize = Mathf.Max(1, Mathf.RoundToInt(_progressText.fontSize));
        _progressFallbackText.fontStyle = FontStyle.Bold;
        _progressFallbackText.alignment = TextAnchor.MiddleCenter;
        _progressFallbackText.color = _progressText.color;
        _progressFallbackText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _progressFallbackText.verticalOverflow = VerticalWrapMode.Truncate;
        _progressFallbackText.raycastTarget = false;
        _progressFallbackText.supportRichText = true;

        _progressText.enabled = false;
        _usingLegacyProgressText = true;
        return true;
    }

    private static bool FontAssetSupportsKorean(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            return false;
        }

        foreach (char character in KoreanProgressTextSample)
        {
            if (!fontAsset.HasCharacter(character, true, true))
            {
                return false;
            }
        }

        return true;
    }

    private static TMP_FontAsset GetOrCreateKoreanUiFont()
    {
        if (_cachedKoreanUiFont != null)
        {
            return _cachedKoreanUiFont;
        }

        Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
        if (osFont == null)
        {
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(osFont);
        if (fontAsset == null)
        {
            return null;
        }

        fontAsset.name = "Runtime Korean UI Font";
        if (!FontAssetSupportsKorean(fontAsset))
        {
            return null;
        }

        _cachedKoreanUiFont = fontAsset;
        return _cachedKoreanUiFont;
    }

    private static Font GetOrCreateKoreanLegacyUiFont()
    {
        if (_cachedKoreanLegacyUiFont != null)
        {
            return _cachedKoreanLegacyUiFont;
        }

        Font osFont = Font.CreateDynamicFontFromOSFont(KoreanUiFontNames, 32);
        if (osFont != null)
        {
            _cachedKoreanLegacyUiFont = osFont;
        }

        return _cachedKoreanLegacyUiFont;
    }

    public void StartProcessing(AnimationClip clip)
    {
        if (clip != null)
        {
            StartAutoRecording(clip.length, clip.name, null, Mathf.CeilToInt(clip.length * RecordingFrameRate), BuildComparisonLabel("manual", clip.name));
        }
        else
        {
            SetError("재생할 애니메이션 클립이 없습니다.");
        }
    }

    public void OnManualRecordButtonClick()
    {
        if (_manualRecordingCoroutine != null)
        {
            return;
        }

        _manualRecordingCoroutine = StartCoroutine(StartManualRecordingSequence());
    }

    private bool HasAnimatorClip()
    {
        Animator animator = GetComponent<Animator>();
        return animator != null
            && animator.runtimeAnimatorController != null
            && animator.runtimeAnimatorController.animationClips.Length > 0;
    }

    private IEnumerator StartManualRecordingSequence()
    {
        Animator animator = GetComponent<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null || animator.runtimeAnimatorController.animationClips.Length == 0)
        {
            SetError("수동 녹화에 사용할 애니메이션 클립이 없습니다.");
            _manualRecordingCoroutine = null;
            yield break;
        }

        if (_isRecordingSessionActive || _isSaving)
        {
            SetError("이전 녹화가 아직 끝나지 않았습니다.");
            _manualRecordingCoroutine = null;
            yield break;
        }

        AnimationClip clip = animator.runtimeAnimatorController.animationClips[0];
        float originalSpeed = Mathf.Approximately(animator.speed, 0f) ? 1f : animator.speed;
        if (resetAnimatorOnManualStart)
        {
            animator.speed = 0f;
            animator.Rebind();
            animator.Update(0f);
        }

        if (StartRecordingTime > 0f)
        {
            SetLoading($"수동 녹화 대기: {clip.name}");
            yield return new WaitForSeconds(StartRecordingTime);
        }

        yield return null;

        string outputName = !string.IsNullOrWhiteSpace(HumanoidVMDName)
            ? Path.GetFileNameWithoutExtension(HumanoidVMDName)
            : (!string.IsNullOrWhiteSpace(ModelName) ? ModelName : clip.name);
        int targetFrameCount = Mathf.CeilToInt(clip.length * RecordingFrameRate);
        if (StartAutoRecording(clip.length, outputName, null, targetFrameCount, BuildComparisonLabel("manual", outputName)))
        {
            animator.speed = originalSpeed;
            Debug.Log($"[Recorder] Manual reference recording started: {clip.name}, target={targetFrameCount}frames");
        }
        else
        {
            animator.speed = originalSpeed;
            _manualRecordingCoroutine = null;
        }
    }

    private static string BuildComparisonLabel(string prefix, string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return prefix;
        }

        return baseName.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)
            ? baseName
            : $"{prefix}_{baseName}";
    }
}

[DefaultExecutionOrder(30010)]
internal class TransformJitterProbe : MonoBehaviour
{
    private const string OutputFolder = "Docs/Workflow/Local/JitterLogs";
    private const float AnomalyScreenDeltaThreshold = 20f;
    private const int MaxAnomalyScreenshots = 6;
    private const float RootScreenTeleportThreshold = 5f;
    private const float HipsScreenTeleportThreshold = 25f;
    private const float HeadScreenTeleportThreshold = 40f;
    private const float FootScreenTeleportThreshold = 120f;
    private const float BoundsScreenTeleportThreshold = 120f;
    private static readonly JitterJoint[] DirectJoints =
    {
        new JitterJoint(HumanBodyBones.Hips, "hipsDirect"),
        new JitterJoint(HumanBodyBones.Spine, "spine"),
        new JitterJoint(HumanBodyBones.Chest, "chest"),
        new JitterJoint(HumanBodyBones.UpperChest, "upperChest"),
        new JitterJoint(HumanBodyBones.Neck, "neck"),
        new JitterJoint(HumanBodyBones.LeftUpperLeg, "leftUpperLeg"),
        new JitterJoint(HumanBodyBones.RightUpperLeg, "rightUpperLeg"),
        new JitterJoint(HumanBodyBones.LeftLowerLeg, "leftLowerLeg"),
        new JitterJoint(HumanBodyBones.RightLowerLeg, "rightLowerLeg"),
        new JitterJoint(HumanBodyBones.LeftFoot, "leftFootDirect"),
        new JitterJoint(HumanBodyBones.RightFoot, "rightFootDirect"),
        new JitterJoint(HumanBodyBones.LeftToes, "leftToes"),
        new JitterJoint(HumanBodyBones.RightToes, "rightToes"),
        new JitterJoint(HumanBodyBones.LeftUpperArm, "leftUpperArm"),
        new JitterJoint(HumanBodyBones.RightUpperArm, "rightUpperArm"),
        new JitterJoint(HumanBodyBones.LeftLowerArm, "leftLowerArm"),
        new JitterJoint(HumanBodyBones.RightLowerArm, "rightLowerArm"),
        new JitterJoint(HumanBodyBones.LeftHand, "leftHand"),
        new JitterJoint(HumanBodyBones.RightHand, "rightHand")
    };
    private static readonly JitterNamedTransform[] NamedTransforms =
    {
        new JitterNamedTransform("461.!Root", "modelRootNode"),
        new JitterNamedTransform("109.!joint_koshikyanseruhidari", "leftHipCancel"),
        new JitterNamedTransform("105.!joint_koshikyanserumigi", "rightHipCancel")
    };

    private readonly List<Row> rows = new List<Row>(4096);
    private readonly List<RendererBoundsRow> rendererBoundsRows = new List<RendererBoundsRow>(16384);
    private Camera observedCamera;
    private Animator observedAnimator;
    private HumanPoseHandler observedPoseHandler;
    private HumanPose observedHumanPose;
    private Transform[] observedNamedTransforms = Array.Empty<Transform>();
    private Transform observedRetargetGhostRoot;
    private Transform observedRetargetGhostHips;
    private string label;
    private float startTime;
    private string sessionFolder;
    private bool isSampling;
    private bool hasPrevious;
    private int anomalyScreenshotCount;
    private Vector3 previousCameraPosition;
    private Quaternion previousCameraRotation;
    private Vector3 previousRootPosition;
    private Vector3 previousMeshCenter;
    private float previousMeshMinY;
    private Vector3 previousHipsPosition;
    private Vector3 previousHumanBodyPosition;
    private Quaternion previousHumanBodyRotation;
    private Vector3 previousHeadPosition;
    private Vector3 previousLeftFootPosition;
    private Vector3 previousRightFootPosition;
    private Vector2 previousRootScreenCenter;
    private Vector2 previousScreenCenter;
    private Vector2 previousHipsScreenCenter;
    private Vector2 previousHeadScreenCenter;
    private Vector2 previousLeftFootScreenCenter;
    private Vector2 previousRightFootScreenCenter;

    public bool IsSampling => isSampling;

    public void StartSampling(string labelOverride)
    {
        observedCamera = Camera.main;
        observedAnimator = GetComponent<Animator>();
        DisposeObservedPoseHandler();
        observedPoseHandler = CreateObservedPoseHandler(observedAnimator);
        observedHumanPose = new HumanPose();
        observedNamedTransforms = ResolveNamedTransforms();
        ResolveRetargetSourceTransforms();
        label = SanitizeFileName(string.IsNullOrWhiteSpace(labelOverride) ? gameObject.name : labelOverride);
        rows.Clear();
        rendererBoundsRows.Clear();
        startTime = Time.time;
        string sessionId =
            $"when-{DateTime.Now:yyyyMMdd-HHmmss}_where-{SanitizeFileName(SceneManager.GetActiveScene().name)}_who-{ShortSlug(label, 32)}_what-jitter";
        sessionFolder = Path.Combine(GetProjectRoot(), OutputFolder, sessionId);
        Directory.CreateDirectory(sessionFolder);
        hasPrevious = false;
        anomalyScreenshotCount = 0;
        isSampling = true;
    }

    public void StopSampling(string reason)
    {
        if (!isSampling)
        {
            return;
        }

        isSampling = false;
        WriteResults(string.IsNullOrWhiteSpace(reason) ? "stop" : reason);
        DisposeObservedPoseHandler();
    }

    private void LateUpdate()
    {
        if (!isSampling)
        {
            return;
        }

        Camera camera = observedCamera != null ? observedCamera : Camera.main;
        Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
        Quaternion cameraRotation = camera != null ? camera.transform.rotation : Quaternion.identity;
        Vector3 rootPosition = transform.position;
        Bounds bounds;
        string lowestRendererName;
        float lowestRendererMinY;
        bool hasBounds = TryGetRendererBounds(out bounds, out lowestRendererName, out lowestRendererMinY);
        Vector3 meshCenter = hasBounds ? bounds.center : rootPosition;
        float meshMinY = hasBounds ? bounds.min.y : rootPosition.y;
        Vector3 hipsPosition = GetBonePosition(HumanBodyBones.Hips, rootPosition);
        Vector3 headPosition = GetBonePosition(HumanBodyBones.Head, rootPosition);
        Vector3 leftFootPosition = GetBonePosition(HumanBodyBones.LeftFoot, rootPosition);
        Vector3 rightFootPosition = GetBonePosition(HumanBodyBones.RightFoot, rootPosition);
        Vector3 retargetGhostRootPosition = GetRetargetGhostRootPosition();
        Vector3 retargetGhostHipsPosition = GetRetargetGhostHipsPosition();
        Vector3 retargetGhostHipsLocalPosition = GetRetargetGhostHipsLocalPosition();
        AnimatorStateSnapshot animatorState = CaptureAnimatorState();
        Vector3 humanBodyPosition = MissingVector();
        Quaternion humanBodyRotation = Quaternion.identity;
        Vector3 humanBodyRotationEuler = MissingVector();
        bool hasHumanPose = TryCaptureHumanPose(out humanBodyPosition, out humanBodyRotation, out humanBodyRotationEuler);
        Vector3[] directJointPositions = CaptureDirectJointPositions();
        Vector3[] directJointLocalPositions = CaptureDirectJointLocalPositions();
        Vector3[] directJointLocalEulers = CaptureDirectJointLocalEulers();
        Vector3[] namedTransformPositions = CaptureNamedTransformPositions();
        Vector3[] namedTransformLocalPositions = CaptureNamedTransformLocalPositions();
        Vector3[] namedTransformLocalEulers = CaptureNamedTransformLocalEulers();
        Vector2 rootScreenCenter = Vector2.zero;
        Vector2 screenCenter = Vector2.zero;
        Vector2 hipsScreenCenter = Vector2.zero;
        Vector2 headScreenCenter = Vector2.zero;
        Vector2 leftFootScreenCenter = Vector2.zero;
        Vector2 rightFootScreenCenter = Vector2.zero;

        if (camera != null)
        {
            rootScreenCenter = WorldToScreenCenter(camera, rootPosition);
            screenCenter = WorldToScreenCenter(camera, meshCenter);
            hipsScreenCenter = WorldToScreenCenter(camera, hipsPosition);
            headScreenCenter = WorldToScreenCenter(camera, headPosition);
            leftFootScreenCenter = WorldToScreenCenter(camera, leftFootPosition);
            rightFootScreenCenter = WorldToScreenCenter(camera, rightFootPosition);
        }

        Row row = new Row
        {
            Frame = Time.frameCount,
            Time = Time.time - startTime,
            DeltaTime = Time.deltaTime,
            AnimatorClipName = animatorState.ClipName,
            AnimatorStateFullPathHash = animatorState.FullPathHash,
            AnimatorNormalizedTime = animatorState.NormalizedTime,
            AnimatorLoopCount = animatorState.LoopCount,
            AnimatorClipTime = animatorState.ClipTime,
            AnimatorClipLength = animatorState.ClipLength,
            CameraPosition = cameraPosition,
            RootPosition = rootPosition,
            MeshCenter = meshCenter,
            MeshMinY = meshMinY,
            HipsPosition = hipsPosition,
            HumanBodyPosition = humanBodyPosition,
            HumanBodyRotationEuler = humanBodyRotationEuler,
            HeadPosition = headPosition,
            LeftFootPosition = leftFootPosition,
            RightFootPosition = rightFootPosition,
            RetargetGhostRootPosition = retargetGhostRootPosition,
            RetargetGhostHipsPosition = retargetGhostHipsPosition,
            RetargetGhostHipsLocalPosition = retargetGhostHipsLocalPosition,
            DirectJointPositions = directJointPositions,
            DirectJointLocalPositions = directJointLocalPositions,
            DirectJointLocalEulers = directJointLocalEulers,
            NamedTransformPositions = namedTransformPositions,
            NamedTransformLocalPositions = namedTransformLocalPositions,
            NamedTransformLocalEulers = namedTransformLocalEulers,
            RootScreenCenter = rootScreenCenter,
            ScreenCenter = screenCenter,
            HipsScreenCenter = hipsScreenCenter,
            HeadScreenCenter = headScreenCenter,
            LeftFootScreenCenter = leftFootScreenCenter,
            RightFootScreenCenter = rightFootScreenCenter,
            LowestRendererName = lowestRendererName,
            LowestRendererMinY = lowestRendererMinY
        };
        CaptureRendererBoundsRows(row);

        if (hasPrevious)
        {
            row.CameraPositionDelta = (cameraPosition - previousCameraPosition).magnitude;
            row.CameraRotationDelta = Quaternion.Angle(previousCameraRotation, cameraRotation);
            row.RootPositionDelta = (rootPosition - previousRootPosition).magnitude;
            row.RootYDelta = Mathf.Abs(rootPosition.y - previousRootPosition.y);
            row.MeshCenterDelta = (meshCenter - previousMeshCenter).magnitude;
            row.MeshMinYDelta = Mathf.Abs(meshMinY - previousMeshMinY);
            row.HipsPositionDelta = (hipsPosition - previousHipsPosition).magnitude;
            if (hasHumanPose && IsFinite(previousHumanBodyPosition))
            {
                row.HumanBodyPositionDelta = (humanBodyPosition - previousHumanBodyPosition).magnitude;
                row.HumanBodyRotationDelta = Quaternion.Angle(previousHumanBodyRotation, humanBodyRotation);
            }
            row.HeadPositionDelta = (headPosition - previousHeadPosition).magnitude;
            row.LeftFootPositionDelta = (leftFootPosition - previousLeftFootPosition).magnitude;
            row.RightFootPositionDelta = (rightFootPosition - previousRightFootPosition).magnitude;
            row.RootScreenCenterDelta = (rootScreenCenter - previousRootScreenCenter).magnitude;
            row.ScreenCenterDelta = (screenCenter - previousScreenCenter).magnitude;
            row.HipsScreenCenterDelta = (hipsScreenCenter - previousHipsScreenCenter).magnitude;
            row.HeadScreenCenterDelta = (headScreenCenter - previousHeadScreenCenter).magnitude;
            row.LeftFootScreenCenterDelta = (leftFootScreenCenter - previousLeftFootScreenCenter).magnitude;
            row.RightFootScreenCenterDelta = (rightFootScreenCenter - previousRightFootScreenCenter).magnitude;
        }

        rows.Add(row);
        CaptureAnomalyScreenshot(row);
        previousCameraPosition = cameraPosition;
        previousCameraRotation = cameraRotation;
        previousRootPosition = rootPosition;
        previousMeshCenter = meshCenter;
        previousMeshMinY = meshMinY;
        previousHipsPosition = hipsPosition;
        previousHumanBodyPosition = humanBodyPosition;
        previousHumanBodyRotation = humanBodyRotation;
        previousHeadPosition = headPosition;
        previousLeftFootPosition = leftFootPosition;
        previousRightFootPosition = rightFootPosition;
        previousRootScreenCenter = rootScreenCenter;
        previousScreenCenter = screenCenter;
        previousHipsScreenCenter = hipsScreenCenter;
        previousHeadScreenCenter = headScreenCenter;
        previousLeftFootScreenCenter = leftFootScreenCenter;
        previousRightFootScreenCenter = rightFootScreenCenter;
        hasPrevious = true;
    }

    private void OnDisable()
    {
        if (isSampling)
        {
            StopSampling("disabled");
        }

        DisposeObservedPoseHandler();
    }

    private HumanPoseHandler CreateObservedPoseHandler(Animator animator)
    {
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
        {
            return null;
        }

        return new HumanPoseHandler(animator.avatar, animator.transform);
    }

    private void DisposeObservedPoseHandler()
    {
        if (observedPoseHandler == null)
        {
            return;
        }

        observedPoseHandler.Dispose();
        observedPoseHandler = null;
    }

    private bool TryCaptureHumanPose(out Vector3 bodyPosition, out Quaternion bodyRotation, out Vector3 bodyRotationEuler)
    {
        bodyPosition = MissingVector();
        bodyRotation = Quaternion.identity;
        bodyRotationEuler = MissingVector();
        if (observedPoseHandler == null)
        {
            return false;
        }

        observedPoseHandler.GetHumanPose(ref observedHumanPose);
        if (!IsFinite(observedHumanPose.bodyPosition) || !IsFinite(observedHumanPose.bodyRotation))
        {
            return false;
        }

        bodyPosition = observedHumanPose.bodyPosition;
        bodyRotation = observedHumanPose.bodyRotation;
        bodyRotationEuler = bodyRotation.eulerAngles;
        return true;
    }

    private Vector3 GetBonePosition(HumanBodyBones bone, Vector3 fallback)
    {
        Transform boneTransform = GetBoneTransform(bone);
        return boneTransform != null && IsFinite(boneTransform.position) ? boneTransform.position : fallback;
    }

    private Vector3[] CaptureDirectJointPositions()
    {
        Vector3[] values = new Vector3[DirectJoints.Length];
        for (int i = 0; i < DirectJoints.Length; i++)
        {
            values[i] = GetBonePosition(DirectJoints[i].Bone, MissingVector());
        }

        return values;
    }

    private Vector3[] CaptureDirectJointLocalPositions()
    {
        Vector3[] values = new Vector3[DirectJoints.Length];
        for (int i = 0; i < DirectJoints.Length; i++)
        {
            Transform boneTransform = GetBoneTransform(DirectJoints[i].Bone);
            values[i] = boneTransform != null && IsFinite(boneTransform.localPosition)
                ? boneTransform.localPosition
                : MissingVector();
        }

        return values;
    }

    private Vector3[] CaptureDirectJointLocalEulers()
    {
        Vector3[] values = new Vector3[DirectJoints.Length];
        for (int i = 0; i < DirectJoints.Length; i++)
        {
            Transform boneTransform = GetBoneTransform(DirectJoints[i].Bone);
            values[i] = boneTransform != null && IsFinite(boneTransform.localEulerAngles)
                ? boneTransform.localEulerAngles
                : MissingVector();
        }

        return values;
    }

    private Transform GetBoneTransform(HumanBodyBones bone)
    {
        Animator animator = observedAnimator != null ? observedAnimator : GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            return null;
        }

        return animator.GetBoneTransform(bone);
    }

    private Transform[] ResolveNamedTransforms()
    {
        Transform[] resolved = new Transform[NamedTransforms.Length];
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < NamedTransforms.Length; i++)
        {
            string targetName = NamedTransforms[i].Name;
            for (int j = 0; j < allTransforms.Length; j++)
            {
                if (string.Equals(allTransforms[j].name, targetName, StringComparison.Ordinal))
                {
                    resolved[i] = allTransforms[j];
                    break;
                }
            }
        }

        return resolved;
    }

    private void ResolveRetargetSourceTransforms()
    {
        observedRetargetGhostRoot = null;
        observedRetargetGhostHips = null;
        if (observedAnimator == null)
        {
            return;
        }

        Type retargeterType = Type.GetType("Fbx2Vmd.FBXImporter.PoseSpaceRetargeter, Assembly-CSharp");
        if (retargeterType == null)
        {
            return;
        }

        var bindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        System.Reflection.FieldInfo targetAnimatorField = retargeterType.GetField("targetAnimator", bindingFlags);
        System.Reflection.FieldInfo ghostAnimatorField = retargeterType.GetField("ghostAnimator", bindingFlags);
        if (targetAnimatorField == null || ghostAnimatorField == null)
        {
            return;
        }

        UnityEngine.Object[] retargeters = UnityEngine.Object.FindObjectsOfType(retargeterType, true);
        for (int i = 0; i < retargeters.Length; i++)
        {
            Animator target = targetAnimatorField.GetValue(retargeters[i]) as Animator;
            if (target != observedAnimator)
            {
                continue;
            }

            Animator ghost = ghostAnimatorField.GetValue(retargeters[i]) as Animator;
            if (ghost == null)
            {
                return;
            }

            observedRetargetGhostRoot = ghost.transform;
            observedRetargetGhostHips = ghost.GetBoneTransform(HumanBodyBones.Hips);
            return;
        }
    }

    private Vector3[] CaptureNamedTransformPositions()
    {
        Vector3[] values = new Vector3[NamedTransforms.Length];
        for (int i = 0; i < NamedTransforms.Length; i++)
        {
            Transform target = GetNamedTransformAt(i);
            values[i] = target != null && IsFinite(target.position) ? target.position : MissingVector();
        }

        return values;
    }

    private Vector3 GetRetargetGhostRootPosition()
    {
        return observedRetargetGhostRoot != null && IsFinite(observedRetargetGhostRoot.position)
            ? observedRetargetGhostRoot.position
            : MissingVector();
    }

    private Vector3 GetRetargetGhostHipsPosition()
    {
        return observedRetargetGhostHips != null && IsFinite(observedRetargetGhostHips.position)
            ? observedRetargetGhostHips.position
            : MissingVector();
    }

    private Vector3 GetRetargetGhostHipsLocalPosition()
    {
        return observedRetargetGhostHips != null && IsFinite(observedRetargetGhostHips.localPosition)
            ? observedRetargetGhostHips.localPosition
            : MissingVector();
    }

    private AnimatorStateSnapshot CaptureAnimatorState()
    {
        Animator animator = observedAnimator != null ? observedAnimator : GetComponent<Animator>();
        if (animator == null || !animator.isInitialized)
        {
            return AnimatorStateSnapshot.Missing();
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
        AnimationClip clip = null;
        float clipWeight = -1f;
        for (int i = 0; i < clipInfos.Length; i++)
        {
            if (clipInfos[i].clip != null && clipInfos[i].weight >= clipWeight)
            {
                clip = clipInfos[i].clip;
                clipWeight = clipInfos[i].weight;
            }
        }

        float normalizedTime = stateInfo.normalizedTime;
        int loopCount = IsFinite(normalizedTime) ? Mathf.FloorToInt(normalizedTime) : 0;
        float fractionalTime = IsFinite(normalizedTime) ? normalizedTime - loopCount : float.NaN;
        if (IsFinite(fractionalTime))
        {
            fractionalTime = Mathf.Clamp01(fractionalTime);
        }

        float clipLength = clip != null ? clip.length : stateInfo.length;
        float clipTime = IsFinite(fractionalTime) && IsFinite(clipLength) && clipLength > 0f
            ? fractionalTime * clipLength
            : float.NaN;

        return new AnimatorStateSnapshot
        {
            ClipName = clip != null ? clip.name : "",
            FullPathHash = stateInfo.fullPathHash,
            NormalizedTime = normalizedTime,
            LoopCount = loopCount,
            ClipTime = clipTime,
            ClipLength = clipLength
        };
    }

    private Vector3[] CaptureNamedTransformLocalPositions()
    {
        Vector3[] values = new Vector3[NamedTransforms.Length];
        for (int i = 0; i < NamedTransforms.Length; i++)
        {
            Transform target = GetNamedTransformAt(i);
            values[i] = target != null && IsFinite(target.localPosition) ? target.localPosition : MissingVector();
        }

        return values;
    }

    private Vector3[] CaptureNamedTransformLocalEulers()
    {
        Vector3[] values = new Vector3[NamedTransforms.Length];
        for (int i = 0; i < NamedTransforms.Length; i++)
        {
            Transform target = GetNamedTransformAt(i);
            values[i] = target != null && IsFinite(target.localEulerAngles) ? target.localEulerAngles : MissingVector();
        }

        return values;
    }

    private Transform GetNamedTransformAt(int index)
    {
        if (observedNamedTransforms == null || index < 0 || index >= observedNamedTransforms.Length)
        {
            return null;
        }

        return observedNamedTransforms[index];
    }

    private static Vector2 WorldToScreenCenter(Camera camera, Vector3 worldPosition)
    {
        Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
        return IsFinite(screenPoint) ? new Vector2(screenPoint.x, screenPoint.y) : Vector2.zero;
    }

    private bool TryGetRendererBounds(out Bounds bounds, out string lowestRendererName, out float lowestRendererMinY)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
        bounds = new Bounds(transform.position, Vector3.zero);
        lowestRendererName = "";
        lowestRendererMinY = float.NaN;
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !IsFinite(renderer.bounds.center) || !IsFinite(renderer.bounds.size))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }

            float rendererMinY = renderer.bounds.min.y;
            if (!IsFinite(rendererMinY))
            {
                continue;
            }

            if (float.IsNaN(lowestRendererMinY) || rendererMinY < lowestRendererMinY)
            {
                lowestRendererMinY = rendererMinY;
                lowestRendererName = renderer.name;
            }
        }

        return hasBounds;
    }

    private void CaptureRendererBoundsRows(Row row)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        Vector3 leftUpperArm = GetBonePosition(HumanBodyBones.LeftUpperArm, row.RootPosition);
        Vector3 rightUpperArm = GetBonePosition(HumanBodyBones.RightUpperArm, row.RootPosition);
        bool hasBodyBasis = TryBuildBodyBasis(row.HipsPosition, row.HeadPosition, leftUpperArm, rightUpperArm, out Vector3 basisX, out Vector3 basisY, out Vector3 basisZ);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !IsFinite(renderer.bounds.center) || !IsFinite(renderer.bounds.size))
            {
                continue;
            }

            Bounds rendererBounds = renderer.bounds;
            Vector3 center = rendererBounds.center;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;
            Vector3 centerFromHips = center - row.HipsPosition;
            Vector3 centerFromMeshCenter = center - row.MeshCenter;

            rendererBoundsRows.Add(new RendererBoundsRow
            {
                Frame = row.Frame,
                Time = row.Time,
                RendererPath = GetTransformPath(renderer.transform),
                RendererName = renderer.name,
                Center = center,
                Size = rendererBounds.size,
                Min = min,
                Max = max,
                CenterFromRoot = center - row.RootPosition,
                CenterFromHips = centerFromHips,
                CenterFromMeshCenter = centerFromMeshCenter,
                CenterBodyLocal = hasBodyBasis ? ToBodyLocal(centerFromHips, basisX, basisY, basisZ) : MissingVector(),
                CenterFromMeshCenterBodyLocal = hasBodyBasis ? ToBodyLocal(centerFromMeshCenter, basisX, basisY, basisZ) : MissingVector(),
                Volume = GetBoundsVolume(rendererBounds.size)
            });
        }
    }

    private static bool TryBuildBodyBasis(
        Vector3 hips,
        Vector3 head,
        Vector3 leftUpperArm,
        Vector3 rightUpperArm,
        out Vector3 xAxis,
        out Vector3 yAxis,
        out Vector3 zAxis)
    {
        xAxis = Vector3.zero;
        yAxis = Vector3.zero;
        zAxis = Vector3.zero;
        if (!IsFinite(hips) || !IsFinite(head) || !IsFinite(leftUpperArm) || !IsFinite(rightUpperArm))
        {
            return false;
        }

        Vector3 x = leftUpperArm - rightUpperArm;
        Vector3 roughUp = head - hips;
        if (x.sqrMagnitude < 0.00000001f || roughUp.sqrMagnitude < 0.00000001f)
        {
            return false;
        }

        xAxis = x.normalized;
        Vector3 up = roughUp.normalized;
        zAxis = Vector3.Cross(xAxis, up);
        if (zAxis.sqrMagnitude < 0.00000001f)
        {
            return false;
        }

        zAxis.Normalize();
        yAxis = Vector3.Cross(zAxis, xAxis);
        if (yAxis.sqrMagnitude < 0.00000001f)
        {
            return false;
        }

        yAxis.Normalize();
        return IsFinite(xAxis) && IsFinite(yAxis) && IsFinite(zAxis);
    }

    private static Vector3 ToBodyLocal(Vector3 relativeWorld, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis)
    {
        return new Vector3(
            Vector3.Dot(relativeWorld, xAxis),
            Vector3.Dot(relativeWorld, yAxis),
            Vector3.Dot(relativeWorld, zAxis));
    }

    private static float GetBoundsVolume(Vector3 size)
    {
        if (!IsFinite(size))
        {
            return float.NaN;
        }

        return Mathf.Abs(size.x * size.y * size.z);
    }

    private void CaptureAnomalyScreenshot(Row row)
    {
        if (anomalyScreenshotCount >= MaxAnomalyScreenshots || string.IsNullOrEmpty(sessionFolder))
        {
            return;
        }

        float maxScreenDelta = Mathf.Max(
            row.ScreenCenterDelta,
            row.HipsScreenCenterDelta,
            row.HeadScreenCenterDelta,
            row.LeftFootScreenCenterDelta,
            row.RightFootScreenCenterDelta);
        if (maxScreenDelta < AnomalyScreenDeltaThreshold)
        {
            return;
        }

        anomalyScreenshotCount++;
        string path = Path.Combine(
            sessionFolder,
            $"frame-{row.Frame:000000}_screen-delta-{maxScreenDelta:0.###}_why-visible-jitter_how-gameview.png");
        ScreenCapture.CaptureScreenshot(path);
    }

    private void WriteResults(string reason)
    {
        if (string.IsNullOrEmpty(sessionFolder))
        {
            string sessionId =
                $"when-{DateTime.Now:yyyyMMdd-HHmmss}_where-{SanitizeFileName(SceneManager.GetActiveScene().name)}_who-{ShortSlug(label, 32)}_what-jitter";
            sessionFolder = Path.Combine(GetProjectRoot(), OutputFolder, sessionId);
            Directory.CreateDirectory(sessionFolder);
        }

        string csvPath = Path.Combine(sessionFolder, "jitter-frames.csv");
        string rendererBoundsCsvPath = Path.Combine(sessionFolder, "renderer-bounds.csv");
        string indexPath = Path.Combine(sessionFolder, "index.md");
        Summary summary = BuildSummary(reason, csvPath, rendererBoundsCsvPath);
        WriteCsv(csvPath);
        WriteRendererBoundsCsv(rendererBoundsCsvPath);
        File.WriteAllText(indexPath, summary.ToMarkdown(), Encoding.UTF8);

        Debug.Log($"[TransformJitterProbe] cameraMax={summary.MaxCameraPositionDelta:F6}m/{summary.MaxCameraRotationDelta:F4}deg, rootMax={summary.MaxRootPositionDelta:F6}m, meshMinYMax={summary.MaxMeshMinYDelta:F6}m, rootScreenMax={summary.MaxRootScreenCenterDelta:F3}px, boundsScreenMax={summary.MaxScreenCenterDelta:F3}px, path={MakeProjectRelativePath(indexPath)}");
    }

    private void WriteCsv(string path)
    {
        StringBuilder builder = new StringBuilder(rows.Count * 128);
        builder.AppendLine(BuildCsvHeader());
        foreach (Row row in rows)
        {
            List<string> fields = new List<string>
            {
                row.Frame.ToString(CultureInfo.InvariantCulture),
                F(row.Time),
                F(row.DeltaTime),
                Escape(row.AnimatorClipName),
                row.AnimatorStateFullPathHash.ToString(CultureInfo.InvariantCulture),
                F(row.AnimatorNormalizedTime),
                row.AnimatorLoopCount.ToString(CultureInfo.InvariantCulture),
                F(row.AnimatorClipTime),
                F(row.AnimatorClipLength),
                V(row.CameraPosition),
                F(row.CameraPositionDelta),
                F(row.CameraRotationDelta),
                V(row.RootPosition),
                F(row.RootPositionDelta),
                F(row.RootYDelta),
                V(row.MeshCenter),
                F(row.MeshMinY),
                F(row.MeshCenterDelta),
                F(row.MeshMinYDelta),
                V(row.HipsPosition),
                F(row.HipsPositionDelta),
                V(row.HumanBodyPosition),
                F(row.HumanBodyPositionDelta),
                V(row.HumanBodyRotationEuler),
                F(row.HumanBodyRotationDelta),
                V(row.HeadPosition),
                F(row.HeadPositionDelta),
                V(row.LeftFootPosition),
                F(row.LeftFootPositionDelta),
                V(row.RightFootPosition),
                F(row.RightFootPositionDelta),
                V(row.RetargetGhostRootPosition),
                V(row.RetargetGhostHipsPosition),
                V(row.RetargetGhostHipsLocalPosition),
                V2(row.RootScreenCenter),
                F(row.RootScreenCenterDelta),
                V2(row.ScreenCenter),
                F(row.ScreenCenterDelta),
                V2(row.HipsScreenCenter),
                F(row.HipsScreenCenterDelta),
                V2(row.HeadScreenCenter),
                F(row.HeadScreenCenterDelta),
                V2(row.LeftFootScreenCenter),
                F(row.LeftFootScreenCenterDelta),
                V2(row.RightFootScreenCenter),
                F(row.RightFootScreenCenterDelta),
                Escape(row.LowestRendererName),
                F(row.LowestRendererMinY)
            };

            for (int i = 0; i < DirectJoints.Length; i++)
            {
                fields.Add(V(GetVectorAt(row.DirectJointPositions, i)));
                fields.Add(V(GetVectorAt(row.DirectJointLocalPositions, i)));
                fields.Add(V(GetVectorAt(row.DirectJointLocalEulers, i)));
            }
            for (int i = 0; i < NamedTransforms.Length; i++)
            {
                fields.Add(V(GetVectorAt(row.NamedTransformPositions, i)));
                fields.Add(V(GetVectorAt(row.NamedTransformLocalPositions, i)));
                fields.Add(V(GetVectorAt(row.NamedTransformLocalEulers, i)));
            }

            builder.AppendLine(string.Join(",", fields));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    private static string ShortSlug(string value, int maxLength)
    {
        string slug = SanitizeFileName(string.IsNullOrWhiteSpace(value) ? "motion" : value);
        return slug.Length <= maxLength ? slug : slug.Substring(0, maxLength);
    }

    private void WriteRendererBoundsCsv(string path)
    {
        StringBuilder builder = new StringBuilder(rendererBoundsRows.Count * 160);
        builder.AppendLine("frame,time,rendererPath,rendererName,center,size,min,max,centerFromRoot,centerFromHips,centerFromMeshCenter,centerBodyLocal,centerFromMeshCenterBodyLocal,volume");
        foreach (RendererBoundsRow row in rendererBoundsRows)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                row.Frame.ToString(CultureInfo.InvariantCulture),
                F(row.Time),
                Escape(row.RendererPath),
                Escape(row.RendererName),
                V(row.Center),
                V(row.Size),
                V(row.Min),
                V(row.Max),
                V(row.CenterFromRoot),
                V(row.CenterFromHips),
                V(row.CenterFromMeshCenter),
                V(row.CenterBodyLocal),
                V(row.CenterFromMeshCenterBodyLocal),
                F(row.Volume)
            }));
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    private static string BuildCsvHeader()
    {
        List<string> columns = new List<string>
        {
            "frame", "time", "deltaTime", "animatorClipName", "animatorStateFullPathHash",
            "animatorNormalizedTime", "animatorLoopCount", "animatorClipTime", "animatorClipLength",
            "cameraPos", "cameraPosDelta", "cameraRotDelta",
            "rootPos", "rootPosDelta", "rootYDelta", "meshCenter", "meshMinY", "meshCenterDelta",
            "meshMinYDelta", "hipsPos", "hipsPosDelta", "humanBodyPos", "humanBodyPosDelta",
            "humanBodyRotEuler", "humanBodyRotDelta", "headPos", "headPosDelta",
            "leftFootPos", "leftFootPosDelta", "rightFootPos", "rightFootPosDelta",
            "retargetGhostRootPos", "retargetGhostHipsPos", "retargetGhostHipsLocalPos",
            "rootScreenCenter", "rootScreenCenterDelta", "screenCenter", "screenCenterDelta",
            "hipsScreenCenter", "hipsScreenCenterDelta", "headScreenCenter", "headScreenCenterDelta",
            "leftFootScreenCenter", "leftFootScreenCenterDelta", "rightFootScreenCenter",
            "rightFootScreenCenterDelta", "lowestRendererName", "lowestRendererMinY"
        };

        foreach (JitterJoint joint in DirectJoints)
        {
            columns.Add($"{joint.ColumnPrefix}Pos");
            columns.Add($"{joint.ColumnPrefix}LocalPos");
            columns.Add($"{joint.ColumnPrefix}LocalEuler");
        }
        foreach (JitterNamedTransform target in NamedTransforms)
        {
            columns.Add($"{target.ColumnPrefix}Pos");
            columns.Add($"{target.ColumnPrefix}LocalPos");
            columns.Add($"{target.ColumnPrefix}LocalEuler");
        }

        return string.Join(",", columns);
    }

    private Summary BuildSummary(string reason, string csvPath, string rendererBoundsCsvPath)
    {
        Summary summary = new Summary
        {
            Label = label,
            Scene = SceneManager.GetActiveScene().name,
            Reason = reason,
            CsvPath = MakeProjectRelativePath(csvPath),
            RendererBoundsCsvPath = MakeProjectRelativePath(rendererBoundsCsvPath),
            RendererBoundsRowCount = rendererBoundsRows.Count,
            FrameCount = rows.Count
        };

        float totalDeltaTime = 0f;
        foreach (Row row in rows)
        {
            totalDeltaTime += row.DeltaTime;
            summary.MaxDeltaTime = Mathf.Max(summary.MaxDeltaTime, row.DeltaTime);
            summary.MaxCameraPositionDelta = Mathf.Max(summary.MaxCameraPositionDelta, row.CameraPositionDelta);
            summary.MaxCameraRotationDelta = Mathf.Max(summary.MaxCameraRotationDelta, row.CameraRotationDelta);
            summary.MaxRootPositionDelta = Mathf.Max(summary.MaxRootPositionDelta, row.RootPositionDelta);
            summary.MaxRootYDelta = Mathf.Max(summary.MaxRootYDelta, row.RootYDelta);
            summary.MaxMeshCenterDelta = Mathf.Max(summary.MaxMeshCenterDelta, row.MeshCenterDelta);
            summary.MaxMeshMinYDelta = Mathf.Max(summary.MaxMeshMinYDelta, row.MeshMinYDelta);
            summary.MaxHipsPositionDelta = Mathf.Max(summary.MaxHipsPositionDelta, row.HipsPositionDelta);
            summary.MaxHumanBodyPositionDelta = Mathf.Max(summary.MaxHumanBodyPositionDelta, row.HumanBodyPositionDelta);
            summary.MaxHumanBodyRotationDelta = Mathf.Max(summary.MaxHumanBodyRotationDelta, row.HumanBodyRotationDelta);
            summary.MaxHeadPositionDelta = Mathf.Max(summary.MaxHeadPositionDelta, row.HeadPositionDelta);
            summary.MaxLeftFootPositionDelta = Mathf.Max(summary.MaxLeftFootPositionDelta, row.LeftFootPositionDelta);
            summary.MaxRightFootPositionDelta = Mathf.Max(summary.MaxRightFootPositionDelta, row.RightFootPositionDelta);
            summary.MaxRootScreenCenterDelta = Mathf.Max(summary.MaxRootScreenCenterDelta, row.RootScreenCenterDelta);
            summary.MaxScreenCenterDelta = Mathf.Max(summary.MaxScreenCenterDelta, row.ScreenCenterDelta);
            summary.MaxHipsScreenCenterDelta = Mathf.Max(summary.MaxHipsScreenCenterDelta, row.HipsScreenCenterDelta);
            summary.MaxHeadScreenCenterDelta = Mathf.Max(summary.MaxHeadScreenCenterDelta, row.HeadScreenCenterDelta);
            summary.MaxLeftFootScreenCenterDelta = Mathf.Max(summary.MaxLeftFootScreenCenterDelta, row.LeftFootScreenCenterDelta);
            summary.MaxRightFootScreenCenterDelta = Mathf.Max(summary.MaxRightFootScreenCenterDelta, row.RightFootScreenCenterDelta);
        }

        summary.AverageDeltaTime = rows.Count > 0 ? totalDeltaTime / rows.Count : 0f;
        summary.CameraMoved = summary.MaxCameraPositionDelta > 0.0005f || summary.MaxCameraRotationDelta > 0.01f;
        summary.ModelMoved = summary.MaxRootPositionDelta > 0.002f || summary.MaxMeshCenterDelta > 0.002f || summary.MaxMeshMinYDelta > 0.002f;
        summary.VisibleScreenJitter = summary.MaxRootScreenCenterDelta > RootScreenTeleportThreshold;
        bool bodyAnchorScreenTeleport =
            summary.MaxHipsScreenCenterDelta > HipsScreenTeleportThreshold ||
            summary.MaxHeadScreenCenterDelta > HeadScreenTeleportThreshold;
        bool footScreenTeleport =
            summary.MaxLeftFootScreenCenterDelta > FootScreenTeleportThreshold ||
            summary.MaxRightFootScreenCenterDelta > FootScreenTeleportThreshold;
        bool boundsDrivenTeleport =
            summary.MaxScreenCenterDelta > BoundsScreenTeleportThreshold &&
            (summary.VisibleScreenJitter || bodyAnchorScreenTeleport);
        summary.VisibleBoundsScreenJitter = bodyAnchorScreenTeleport || footScreenTeleport || boundsDrivenTeleport;
        return summary;
    }

    private static string GetProjectRoot()
    {
        DirectoryInfo current = Directory.GetParent(Application.dataPath);
        return current != null ? current.FullName : Application.dataPath;
    }

    private static string MakeProjectRelativePath(string path)
    {
        string root = GetProjectRoot();
        string full = Path.GetFullPath(path);
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("\\", "/")
            : full.Replace("\\", "/");
    }

    private string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return "";
        }

        List<string> parts = new List<string>();
        Transform current = target;
        while (current != null)
        {
            parts.Add(current.name);
            if (current == transform)
            {
                break;
            }

            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                 float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
    }

    private static bool IsFinite(Quaternion value)
    {
        return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) || float.IsNaN(value.w) ||
                 float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z) || float.IsInfinity(value.w));
    }

    private static Vector3 MissingVector()
    {
        return new Vector3(float.NaN, float.NaN, float.NaN);
    }

    private static Vector3 GetVectorAt(Vector3[] values, int index)
    {
        return values != null && index >= 0 && index < values.Length
            ? values[index]
            : MissingVector();
    }

    private static bool IsFinite(float value)
    {
        return !(float.IsNaN(value) || float.IsInfinity(value));
    }

    private static string F(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? ""
            : value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string V(Vector3 value)
    {
        return Escape($"{F(value.x)}|{F(value.y)}|{F(value.z)}");
    }

    private static string V2(Vector2 value)
    {
        return Escape($"{F(value.x)}|{F(value.y)}");
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string escaped = value.Replace("\"", "\"\"");
        return escaped.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? $"\"{escaped}\"" : escaped;
    }

    private static string SanitizeFileName(string value)
    {
        string safe = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '_');
        }

        return safe.Replace(' ', '_');
    }

    private struct Row
    {
        public int Frame;
        public float Time;
        public float DeltaTime;
        public string AnimatorClipName;
        public int AnimatorStateFullPathHash;
        public float AnimatorNormalizedTime;
        public int AnimatorLoopCount;
        public float AnimatorClipTime;
        public float AnimatorClipLength;
        public Vector3 CameraPosition;
        public float CameraPositionDelta;
        public float CameraRotationDelta;
        public Vector3 RootPosition;
        public float RootPositionDelta;
        public float RootYDelta;
        public Vector3 MeshCenter;
        public float MeshMinY;
        public float MeshCenterDelta;
        public float MeshMinYDelta;
        public Vector3 HipsPosition;
        public float HipsPositionDelta;
        public Vector3 HumanBodyPosition;
        public float HumanBodyPositionDelta;
        public Vector3 HumanBodyRotationEuler;
        public float HumanBodyRotationDelta;
        public Vector3 HeadPosition;
        public float HeadPositionDelta;
        public Vector3 LeftFootPosition;
        public float LeftFootPositionDelta;
        public Vector3 RightFootPosition;
        public float RightFootPositionDelta;
        public Vector3 RetargetGhostRootPosition;
        public Vector3 RetargetGhostHipsPosition;
        public Vector3 RetargetGhostHipsLocalPosition;
        public Vector3[] DirectJointPositions;
        public Vector3[] DirectJointLocalPositions;
        public Vector3[] DirectJointLocalEulers;
        public Vector3[] NamedTransformPositions;
        public Vector3[] NamedTransformLocalPositions;
        public Vector3[] NamedTransformLocalEulers;
        public Vector2 RootScreenCenter;
        public float RootScreenCenterDelta;
        public Vector2 ScreenCenter;
        public float ScreenCenterDelta;
        public Vector2 HipsScreenCenter;
        public float HipsScreenCenterDelta;
        public Vector2 HeadScreenCenter;
        public float HeadScreenCenterDelta;
        public Vector2 LeftFootScreenCenter;
        public float LeftFootScreenCenterDelta;
        public Vector2 RightFootScreenCenter;
        public float RightFootScreenCenterDelta;
        public string LowestRendererName;
        public float LowestRendererMinY;
    }

    private struct AnimatorStateSnapshot
    {
        public string ClipName;
        public int FullPathHash;
        public float NormalizedTime;
        public int LoopCount;
        public float ClipTime;
        public float ClipLength;

        public static AnimatorStateSnapshot Missing()
        {
            return new AnimatorStateSnapshot
            {
                ClipName = "",
                FullPathHash = 0,
                NormalizedTime = float.NaN,
                LoopCount = 0,
                ClipTime = float.NaN,
                ClipLength = float.NaN
            };
        }
    }

    private struct RendererBoundsRow
    {
        public int Frame;
        public float Time;
        public string RendererPath;
        public string RendererName;
        public Vector3 Center;
        public Vector3 Size;
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 CenterFromRoot;
        public Vector3 CenterFromHips;
        public Vector3 CenterFromMeshCenter;
        public Vector3 CenterBodyLocal;
        public Vector3 CenterFromMeshCenterBodyLocal;
        public float Volume;
    }

    private readonly struct JitterJoint
    {
        public JitterJoint(HumanBodyBones bone, string columnPrefix)
        {
            Bone = bone;
            ColumnPrefix = columnPrefix;
        }

        public HumanBodyBones Bone { get; }
        public string ColumnPrefix { get; }
    }

    private readonly struct JitterNamedTransform
    {
        public JitterNamedTransform(string name, string columnPrefix)
        {
            Name = name;
            ColumnPrefix = columnPrefix;
        }

        public string Name { get; }
        public string ColumnPrefix { get; }
    }

    private sealed class Summary
    {
        public string Label;
        public string Scene;
        public string Reason;
        public string CsvPath;
        public string RendererBoundsCsvPath;
        public int FrameCount;
        public int RendererBoundsRowCount;
        public float AverageDeltaTime;
        public float MaxDeltaTime;
        public float MaxCameraPositionDelta;
        public float MaxCameraRotationDelta;
        public float MaxRootPositionDelta;
        public float MaxRootYDelta;
        public float MaxMeshCenterDelta;
        public float MaxMeshMinYDelta;
        public float MaxHipsPositionDelta;
        public float MaxHumanBodyPositionDelta;
        public float MaxHumanBodyRotationDelta;
        public float MaxHeadPositionDelta;
        public float MaxLeftFootPositionDelta;
        public float MaxRightFootPositionDelta;
        public float MaxRootScreenCenterDelta;
        public float MaxScreenCenterDelta;
        public float MaxHipsScreenCenterDelta;
        public float MaxHeadScreenCenterDelta;
        public float MaxLeftFootScreenCenterDelta;
        public float MaxRightFootScreenCenterDelta;
        public bool CameraMoved;
        public bool ModelMoved;
        public bool VisibleScreenJitter;
        public bool VisibleBoundsScreenJitter;

        public string ToMarkdown()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Camera/Model Jitter Probe");
            builder.AppendLine();
            builder.AppendLine($"- label: `{Label}`");
            builder.AppendLine($"- scene: `{Scene}`");
            builder.AppendLine($"- reason: `{Reason}`");
            builder.AppendLine($"- csv: `{CsvPath}`");
            builder.AppendLine($"- renderer bounds csv: `{RendererBoundsCsvPath}`");
            builder.AppendLine($"- frames: `{FrameCount}`");
            builder.AppendLine($"- renderer bounds rows: `{RendererBoundsRowCount}`");
            builder.AppendLine($"- avg dt: `{F(AverageDeltaTime)}`");
            builder.AppendLine($"- max dt: `{F(MaxDeltaTime)}`");
            builder.AppendLine($"- max camera position delta: `{F(MaxCameraPositionDelta)}m`");
            builder.AppendLine($"- max camera rotation delta: `{F(MaxCameraRotationDelta)}deg`");
            builder.AppendLine($"- max root position delta: `{F(MaxRootPositionDelta)}m`");
            builder.AppendLine($"- max root Y delta: `{F(MaxRootYDelta)}m`");
            builder.AppendLine($"- max mesh minY delta: `{F(MaxMeshMinYDelta)}m`");
            builder.AppendLine($"- max hips position delta: `{F(MaxHipsPositionDelta)}m`");
            builder.AppendLine($"- max HumanPose bodyPosition delta: `{F(MaxHumanBodyPositionDelta)}m`");
            builder.AppendLine($"- max HumanPose bodyRotation delta: `{F(MaxHumanBodyRotationDelta)}deg`");
            builder.AppendLine($"- max head position delta: `{F(MaxHeadPositionDelta)}m`");
            builder.AppendLine($"- max left foot position delta: `{F(MaxLeftFootPositionDelta)}m`");
            builder.AppendLine($"- max right foot position delta: `{F(MaxRightFootPositionDelta)}m`");
            builder.AppendLine($"- max root screen center delta: `{F(MaxRootScreenCenterDelta)}px`");
            builder.AppendLine($"- max bounds screen center delta: `{F(MaxScreenCenterDelta)}px`");
            builder.AppendLine($"- max hips screen center delta: `{F(MaxHipsScreenCenterDelta)}px`");
            builder.AppendLine($"- max head screen center delta: `{F(MaxHeadScreenCenterDelta)}px`");
            builder.AppendLine($"- max left foot screen center delta: `{F(MaxLeftFootScreenCenterDelta)}px`");
            builder.AppendLine($"- max right foot screen center delta: `{F(MaxRightFootScreenCenterDelta)}px`");
            builder.AppendLine($"- camera moved: `{CameraMoved}`");
            builder.AppendLine($"- model moved: `{ModelMoved}`");
            builder.AppendLine($"- visible root screen jitter threshold: `>{F(RootScreenTeleportThreshold)}px`");
            builder.AppendLine($"- visible bounds screen jitter thresholds: `hips>{F(HipsScreenTeleportThreshold)}px, head>{F(HeadScreenTeleportThreshold)}px, foot>{F(FootScreenTeleportThreshold)}px, bounds>{F(BoundsScreenTeleportThreshold)}px with body/root anchor`");
            builder.AppendLine($"- visible root screen jitter: `{VisibleScreenJitter}`");
            builder.AppendLine($"- visible bounds screen jitter: `{VisibleBoundsScreenJitter}`");
            return builder.ToString();
        }
    }
}
