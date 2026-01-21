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
    public string ModelName;           // VMD 파일에 기록될 모델명 (예: "MyCharacter")
    public string HumanoidVMDName;     // 생성될 VMD 파일명 (예: "motion.vmd")

    private float StartRecordingTime = 0.1f;  // 레코딩 시작 대기 시간 (초기화 대기)
    public float StopRecordingTime = 30f;     // 레코딩 종료 시간 (0이면 애니메이션 클립 길이 자동 사용)

    string humanoidVMDPath = "";  // VMD 파일 저장 경로

    // [실행 순서 1-1] 씬 시작 시 초기화
    void Start()
    {
        // VMD 파일 저장 경로 설정
        humanoidVMDPath = Application.dataPath + "/VMDRecorderSample/" + HumanoidVMDName;
        
        // 0.1초 후 레코딩 시작 예약 (초기화 대기)
        Invoke("StartRecording", StartRecordingTime);

        // 애니메이션 클립 길이 자동 감지
        Animator aniCtr = this.GetComponent<Animator>();
        float clipLength = aniCtr.GetCurrentAnimatorClipInfo(0)[0].clip.length;
        Debug.Log(clipLength);
        
        // 종료 시간 설정 (0이면 클립 길이 사용, 아니면 지정된 시간 사용)
        StopRecordingTime = StopRecordingTime == 0 ? clipLength : StopRecordingTime;
        
        // 지정된 시간 후 저장 예약
        Invoke("SaveRecord", StopRecordingTime);
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
}