using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private bool enableMotionComparisonProbe = true;
    [SerializeField] private bool probeFingerCloseups = true;

    [HideInInspector] public int StopRecordingTime = 0;
    [HideInInspector] public bool AutoStartRecording = false;

    public event Action<VmdSaveResult> RecordingFinished;

    public bool IsRecordingSessionActive => _isRecordingSessionActive || _isSaving;
    public string LastSavedFilePath => _lastSavedFilePath;

    private bool _isRecordingSessionActive = false;
    private bool _isSaving = false;
    private float _totalDuration = 0f;
    private float _currentTimer = 0f;
    private int _targetFrameCount = 0;
    private string _outputFolderPath = "";
    private string _outputFilePath = "";
    private string _lastSavedFilePath = "";
    private Coroutine _manualRecordingCoroutine;

    private const float RecordingFrameRate = 30f;

    private void Start()
    {
        EnsureRecorder();

        if (vmdRecorder != null && vmdRecorder.IsRecording)
        {
            vmdRecorder.StopRecording();
        }

        SetReady("FBX를 선택하세요");

        if (AutoStartRecording && HasAnimatorClip())
        {
            _manualRecordingCoroutine = StartCoroutine(StartManualRecordingSequence());
        }
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
        _currentTimer = 0f;
        _targetFrameCount = targetFrameCount > 0 ? targetFrameCount : Mathf.CeilToInt(clipLength * RecordingFrameRate);
        StopRecordingTime = Mathf.CeilToInt(clipLength);

        string labelText = string.IsNullOrWhiteSpace(comparisonLabel) ? "(파일명과 동일)" : comparisonLabel;
        Debug.Log($"[Recorder] 녹화 시작: VMD={Path.GetFileName(_outputFilePath)}, 비교라벨={labelText}, 덮어쓰기={overwriteExistingOutput}, 길이 {_totalDuration:F2}초, 목표 {_targetFrameCount}프레임");

        vmdRecorder.StopRecording();
        vmdRecorder.ResetRecordingBuffersForNewSession();
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

        _currentTimer += Time.deltaTime;

        float displayTime = finishByRecordedFrameCount && _targetFrameCount > 0
            ? vmdRecorder.FrameNumber / RecordingFrameRate
            : _currentTimer;
        float progress = finishByRecordedFrameCount && _targetFrameCount > 0
            ? Mathf.Clamp01((float)vmdRecorder.FrameNumber / _targetFrameCount)
            : (_totalDuration > 0f ? Mathf.Clamp01(_currentTimer / _totalDuration) : 0f);
        UpdateUI(progress, displayTime, $"녹화 중: {ModelName}");

        if (finishByRecordedFrameCount && _targetFrameCount > 0)
        {
            if (vmdRecorder.FrameNumber >= _targetFrameCount)
            {
                FinishRecording();
            }
        }
        else if (_currentTimer >= _totalDuration)
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

        Debug.Log("[Recorder] 녹화 시간 도달. 저장을 시작합니다.");
        UpdateUI(1f, _totalDuration, "VMD 저장 중");

        StopComparisonProbe(resultReason: "finish");
        vmdRecorder.StopRecording();
        VmdSaveResult result = await vmdRecorder.SaveVMDAsync(ModelName, _outputFilePath);

        _isSaving = false;

        if (result.Success)
        {
            _lastSavedFilePath = result.FilePath;
            Debug.Log($"[Recorder] 저장 완료: {result.FilePath} ({result.FileSizeBytes} bytes, {result.FrameCount} frames)");
            UpdateUI(1f, _totalDuration, $"저장 완료: {Path.GetFileName(result.FilePath)}");
            Invoke(nameof(OpenTargetFolder), 0.5f);
        }
        else
        {
            string error = string.IsNullOrEmpty(result.ErrorMessage) ? "알 수 없는 저장 오류" : result.ErrorMessage;
            Debug.LogError($"[Recorder] 저장 실패: {error}");
            UpdateUI(0f, _currentTimer, $"저장 실패: {error}");
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
        probe.SetFingerCloseups(probeFingerCloseups);
        probe.StartSampling(probeLabel);
    }

    private void StopComparisonProbe(string resultReason)
    {
        MotionComparisonProbe probe = GetComponent<MotionComparisonProbe>();
        if (probe != null && probe.IsSampling)
        {
            probe.StopSampling(resultReason);
        }
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
        Debug.Log($"[Recorder] 탐색기 열기: {folderPath}");
        Application.OpenURL(folderPath);
    }

    private void UpdateUI(float progress, float currentTime, string status)
    {
        if (_progressSlider != null)
        {
            _progressSlider.value = Mathf.Clamp01(progress);
        }

        if (_progressText != null)
        {
            string totalText = StopRecordingTime > 0 ? StopRecordingTime.ToString() : "-";
            _progressText.text = $"{status} {currentTime:F1}s / {totalText}s";
        }
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
            Debug.Log($"[Recorder] 수동 기준 녹화 시작: {clip.name}, 목표 {targetFrameCount}프레임");
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
