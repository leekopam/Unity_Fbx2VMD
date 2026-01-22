using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================
// [실행 순서 1] 휴머노이드 레코딩 제어 스크립트
// 역할: VMD 레코딩 시작/종료 타이밍 제어
// 실행 시점: 씬 시작 시 (Start)
// ============================================
public class HumanoidSampleCode : MonoBehaviour
{
    [Header("기본 설정")]
    public string ModelName;           // VMD 파일에 기록될 모델명 (예: "MyCharacter")
    public string HumanoidVMDName;     // 생성될 VMD 파일명 (예: "motion.vmd")

    [Header("v27 자동 녹화 설정")]
    [Tooltip("체크 시 Play 버튼을 누르면 즉시 현재 클립으로 녹화를 시작합니다.\n체크 해제 시 FBX Import 또는 수동 버튼으로만 녹화가 시작됩니다.")]
    public bool AutoStartRecording = false;

    private float StartRecordingTime = 0.1f;  // 레코딩 시작 대기 시간 (초기화 대기)
    public float StopRecordingTime = 30f;     // 레코딩 종료 시간 (0이면 애니메이션 클립 길이 자동 사용)

    string humanoidVMDPath = "";  // VMD 파일 저장 경로

    // [실행 순서 1-1] 씬 시작 시 초기화
    void Start()
    {
        // VMD 파일 저장 경로 설정
        humanoidVMDPath = Application.dataPath + "/VMDRecorderSample/" + HumanoidVMDName;
        
        // [v22] FBX Import 테스트를 위해 자동 레코딩 비활성화
        // 0.1초 후 레코딩 시작 예약 (초기화 대기)
        // Invoke("StartRecording", StartRecordingTime);

        // [v22] 클립 접근 시도도 비활성화 (Start 시점에 클립이 없을 수 있음)
        // Animator aniCtr = this.GetComponent<Animator>();
        // float clipLength = aniCtr.GetCurrentAnimatorClipInfo(0)[0].clip.length;
        // Debug.Log(clipLength);
        
        // 종료 시간 설정 (0이면 클립 길이 사용, 아니면 지정된 시간 사용)
        // StopRecordingTime = StopRecordingTime == 0 ? clipLength : StopRecordingTime;
        
        // 지정된 시간 후 저장 예약
        // Invoke("SaveRecord", StopRecordingTime);
        
        // [v27] AutoStartRecording 옵션에 따라 자동 녹화 시작
        if (AutoStartRecording)
        {
            Debug.Log("[HumanoidSampleCode] ⚡ AutoStartRecording 옵션이 켜져 있습니다. 즉시 녹화를 시작합니다.");
            // 약간의 초기화 시간을 위해 0.1초 지연 호출
            Invoke("OnManualRecordButtonClick", StartRecordingTime);
        }
        else
        {
            Debug.Log("[HumanoidSampleCode] AutoStartRecording이 꺼져 있습니다. FBX Import 또는 수동 버튼을 사용하세요.");
        }
    }

    // [실행 순서 1-2] 레코딩 시작 (0.1초 후 실행)
    void StartRecording()
    {
        // UnityHumanoidVMDRecorder의 레코딩 시작
        GetComponent<UnityHumanoidVMDRecorder>().StartRecording();
    }

    // [실행 순서 1-3] 레코딩 종료 및 저장 (StopRecordingTime초 후 실행)
    void SaveRecord()
    {
        // 레코딩 중지
        GetComponent<UnityHumanoidVMDRecorder>().StopRecording();
        
        // VMD 파일 생성 및 저장
        GetComponent<UnityHumanoidVMDRecorder>().SaveVMD(ModelName, humanoidVMDPath);
        
        // 저장 폴더 자동 열기
        Application.OpenURL(Application.dataPath + "/VMDRecorderSample/");
    }

    // ============================================
    // [v25] 외부에서 호출 가능한 통합 처리 함수
    // FBX Import 완료 후 FileManager가 호출
    // ============================================
    /// <summary>
    /// [v25] 애니메이션 재생과 VMD 레코딩을 동시에 시작
    /// Project_Info.md 요구사항 준수:
    /// 1. Length 올림 처리하여 StopRecordingTime에 할당
    /// 2. VMD 레코딩 시작과 동시에 애니메이션 재생
    /// </summary>
    /// <param name="clip">재생할 AnimationClip (Unity Import된 클립)</param>
    public void StartProcessing(AnimationClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("[HumanoidSampleCode] ❌ AnimationClip이 null입니다!");
            return;
        }

        // 1. [요구사항] Length 올림하여 StopRecordingTime 할당
        StopRecordingTime = Mathf.Ceil(clip.length);
        Debug.Log($"[HumanoidSampleCode] [v25] 레코딩 시간 설정: {StopRecordingTime}초 (올림 처리: {clip.length} → {StopRecordingTime})");

        // [v25 Fix] 지정된 폴더가 없으면 생성하여 DirectoryNotFoundException 방지
        string folderPath = Application.dataPath + "/VMDRecorderSample";
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
            Debug.Log($"[HumanoidSampleCode] 폴더가 없어 새로 생성했습니다: {folderPath}");
        }

        // VMD 경로 재설정 (클립 이름 기반)
        humanoidVMDPath = folderPath + "/" + clip.name + ".vmd";
        Debug.Log($"[HumanoidSampleCode] VMD 저장 경로: {humanoidVMDPath}");

        // 2. [요구사항] VMD 레코딩 시작
        var vmdRecorder = GetComponent<UnityHumanoidVMDRecorder>();
        if (vmdRecorder != null)
        {
            vmdRecorder.StartRecording();
            Debug.Log("[HumanoidSampleCode] ✅ VMD 레코딩 시작");
        }
        else
        {
            Debug.LogError("[HumanoidSampleCode] ❌ UnityHumanoidVMDRecorder 컴포넌트가 없습니다!");
            return;
        }

        // 3. [요구사항] 동시에 애니메이션 재생 시작
        /*
        Animator ani = GetComponent<Animator>();
        if (ani != null)
        {
            ani.enabled = true;
            // Project_Info.md에 명시된 State 이름 "satisfaction_2_FBX"
            ani.Play("satisfaction_2_FBX", 0, 0f);
            ani.Update(0f); // 즉시 갱신하여 첫 프레임 보장
            Debug.Log("[HumanoidSampleCode] ✅ 애니메이션 재생 시작 (satisfaction_2_FBX)");
        }
        else
        {
            Debug.LogError("[HumanoidSampleCode] ❌ Animator 컴포넌트가 없습니다!");
        }
        */

        // 4. 저장 예약
        Invoke("SaveRecord", StopRecordingTime);
        Debug.Log($"[HumanoidSampleCode] ✅ {StopRecordingTime}초 후 VMD 저장 예약됨");
    }

    // ============================================
    // [v26] 수동 VMD 녹화 버튼용 public 메서드
    // Canvas Button의 OnClick() 이벤트에 할당하여 사용
    // ============================================
    /// <summary>
    /// Canvas Button의 OnClick에 할당하여 수동으로 VMD 녹화를 시작합니다.
    /// 현재 Animator에 할당된 첫 번째 클립을 기준으로 녹화 시간을 자동 계산합니다.
    /// </summary>
    public void OnManualRecordButtonClick()
    {
        Debug.Log("[HumanoidSampleCode] 🔴 수동 녹화 버튼 클릭됨");
        
        Animator animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[HumanoidSampleCode] ❌ Animator 컴포넌트가 없습니다!");
            return;
        }
        
        if (animator.runtimeAnimatorController == null)
        {
            Debug.LogError("[HumanoidSampleCode] ❌ Animator에 Controller가 할당되지 않았습니다!");
            return;
        }
        
        // 현재 Controller에 할당된 클립들 가져오기
        var clips = animator.runtimeAnimatorController.animationClips;
        if (clips == null || clips.Length == 0)
        {
            Debug.LogError("[HumanoidSampleCode] ❌ Animator에 AnimationClip이 없습니다!");
            return;
        }
        
        // 첫 번째 클립으로 녹화 시작
        AnimationClip targetClip = clips[0];
        Debug.Log($"[HumanoidSampleCode] 사용할 클립: {targetClip.name} ({targetClip.length}초)");
        
        // 기존 StartProcessing 로직 재사용
        StartProcessing(targetClip);
    }
}