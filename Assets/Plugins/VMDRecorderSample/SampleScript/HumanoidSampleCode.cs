using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Slider용
using TMPro;          // TextMeshPro용

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

    // 기존 AutoStartRecording 변수는 인스펙터에서 끄거나, 코드에서 무시
    [HideInInspector] public int StopRecordingTime = 0; // 자동으로 설정될 것임
    [HideInInspector] public bool AutoStartRecording = false;

    private bool _isRecordingSessionActive = false;
    private float _totalDuration = 0f;
    private float _currentTimer = 0f;

    void Start()
    {
        // 앱 실행 시 1초짜리 빈 파일이 생성되는 것을 방지
        if (vmdRecorder == null)
        {
            vmdRecorder = GetComponent<UnityHumanoidVMDRecorder>();
        }

        if (vmdRecorder != null)
        {
            vmdRecorder.StopRecording(); // Ensure it's stopped initially
        }
        
        UpdateUI(0, 0, "Ready to Load");
    }

    // 외부(FileManager)에서 호출하는 녹화 시작 함수
    public void StartAutoRecording(float clipLength, string fileName = "")
    {
        if (vmdRecorder == null)
        {
            vmdRecorder = GetComponent<UnityHumanoidVMDRecorder>();
            if (vmdRecorder == null)
            {
                Debug.LogError("[HumanoidSampleCode] ❌ UnityHumanoidVMDRecorder Missing!");
                return;
            }
        }

        // 이름 및 경로 설정
        if (!string.IsNullOrEmpty(fileName))
        {
            ModelName = fileName;
            HumanoidVMDName = fileName + ".vmd";
        }

        // 시간 설정 (소수점 반올림)
        _totalDuration = clipLength;
        StopRecordingTime = Mathf.CeilToInt(clipLength);
        
        Debug.Log($"[Recorder] 🎬 녹화 시퀀스 시작! 파일: {fileName}, 길이: {_totalDuration:F2}초");

        // 레코더 초기화 및 시작
        _currentTimer = 0f;
        vmdRecorder.StopRecording(); // 안전하게 정지 후
        vmdRecorder.StartRecording(); // 녹화 시작

        _isRecordingSessionActive = true;
    }

    void Update()
    {
        if (!_isRecordingSessionActive || vmdRecorder == null) return;

        // 진행 시간 업데이트
        _currentTimer += Time.deltaTime;

        // UI 갱신 (ModelName을 사용하여 현재 녹화중인 파일명 표시)
        float progress = Mathf.Clamp01(_currentTimer / _totalDuration);
        string statusText = $"[{ModelName}]";
        UpdateUI(progress, _currentTimer, statusText);

        // 종료 조건 체크 (시간 도달)
        if (_currentTimer >= _totalDuration)
        {
            FinishRecording();
        }
    }

    // 녹화 종료 및 저장 로직 완전 수정
    private void FinishRecording()
    {
        // 중복 실행 방지
        if (!_isRecordingSessionActive) return;
        _isRecordingSessionActive = false;

        Debug.Log("녹화 종료 시간 도달. 저장 프로세스 시작...");

        // 녹화 중지 (버퍼 플러시)
        if (vmdRecorder != null)
        {
            vmdRecorder.StopRecording();
        }

        // 저장 경로 생성 (절대 경로 보장)
        // Application.dataPath는 에디터에서는 "Assets", 빌드에서는 "Game_Data" 폴더를 가리킴
        string folderName = "VMDRecorderSample";
        string folderPath = System.IO.Path.Combine(Application.dataPath, folderName);

        // 폴더가 없으면 생성
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        // 전체 파일 경로 조합 (예: C:/Project/Assets/VMDRecorderSample/fbxToVMD.vmd)
        // 파일명에 확장자가 없으면 붙여줌
        string fileName = HumanoidVMDName;
        if (!fileName.EndsWith(".vmd")) fileName += ".vmd";
        
        string fullFilePath = System.IO.Path.Combine(folderPath, fileName);

        // VMD 파일 저장 수행
        if (vmdRecorder != null)
        {
            // 모델 이름과 전체 경로를 넘겨줍니다.
            vmdRecorder.SaveVMD(ModelName, fullFilePath);
            Debug.Log($"파일 저장 완료: {fullFilePath}");
        }

        // UI 업데이트
        UpdateUI(1.0f, StopRecordingTime, "Saved");

        // 폴더 열기
        // 약간의 지연 시간을 두어 파일 시스템이 쓰기를 마칠 시간을 줌
        Invoke("OpenTargetFolder", 0.5f);
    }

    // 폴더 열기 헬퍼 함수
    private void OpenTargetFolder()
    {
        string folderName = "VMDRecorderSample";
        string folderPath = System.IO.Path.Combine(Application.dataPath, folderName);
        
        // 경로 구분자 통일 (윈도우/맥 호환성)
        folderPath = folderPath.Replace("/", "\\"); 

        Debug.Log($"탐색기 열기: {folderPath}");
        Application.OpenURL(folderPath);
    }

    private void UpdateUI(float progress, float currentTime, string status)
    {
        if (_progressSlider != null) _progressSlider.value = progress;
        
        if (_progressText != null)
        {
            _progressText.text = $"{status} {currentTime:F1}s / {StopRecordingTime}s";
        }
    }
    public void StartProcessing(AnimationClip clip)
    {
        if (clip != null)
        {
            HumanoidVMDName = clip.name + ".vmd";
            StartAutoRecording(clip.length);
        }
    }

    public void OnManualRecordButtonClick()
    {
         Animator animator = GetComponent<Animator>();
         if (animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Length > 0)
         {
             var clip = animator.runtimeAnimatorController.animationClips[0];
             StartProcessing(clip);
         }
    }
}