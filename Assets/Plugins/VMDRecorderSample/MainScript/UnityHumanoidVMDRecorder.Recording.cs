using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Serialization;

public partial class UnityHumanoidVMDRecorder
{
    // [한글] [실행 순서 2-2] 레거시 호환 경로. 기본 자동 경로는 LateUpdate 저장을 사용한다.
    private void FixedUpdate()
    {
        if (IsRecording && !RecordAfterLateVisualPose)  // 레코딩 중일 때만
        {
            SaveRecordingFrame();      // 현재 프레임 데이터 저장
        }
    }

    // [한글] [실행 순서 2-2B] retarget/grounding LateUpdate가 끝난 뒤 30fps 간격으로 저장한다.
    private void LateUpdate()
    {
        if (!IsRecording || !RecordAfterLateVisualPose)
        {
            return;
        }

        int framesSavedThisLateUpdate = 0;
        bool shouldRecordFirstFrame = FrameNumber == 0;

        if (shouldRecordFirstFrame)
        {
            SaveRecordingFrame();
            framesSavedThisLateUpdate++;
            recordingFrameAccumulator = 0f;
            maxFramesSavedInSingleLateUpdate = Mathf.Max(maxFramesSavedInSingleLateUpdate, framesSavedThisLateUpdate);
            return;
        }

        recordingFrameAccumulator += Time.deltaTime;
        int maxFramesThisLateUpdate = Mathf.Max(1, MaxRecordedFramesPerLateUpdate);
        while (recordingFrameAccumulator + 0.0001f >= FPSs && framesSavedThisLateUpdate < maxFramesThisLateUpdate)
        {
            recordingFrameAccumulator = Mathf.Max(0f, recordingFrameAccumulator - FPSs);
            SaveRecordingFrame();
            framesSavedThisLateUpdate++;
        }

        if (recordingFrameAccumulator + 0.0001f >= FPSs &&
            DropLateFrameBacklogWhenNotUsingCaptureFramerate &&
            !UseCaptureFramerateDuringRecording)
        {
            recordingFrameAccumulator = Mathf.Min(recordingFrameAccumulator, FPSs - 0.0001f);
            droppedLateFrameBacklogCount++;
        }

        maxFramesSavedInSingleLateUpdate = Mathf.Max(maxFramesSavedInSingleLateUpdate, framesSavedThisLateUpdate);
    }

    private void SaveRecordingFrame()
    {
        SaveFrame();
        if (!IsRecording)
        {
            return;
        }

        if (lastSavedUnityFrame == Time.frameCount)
        {
            sameUnityFrameSaveCount++;
        }

        lastSavedUnityFrame = Time.frameCount;
        FrameNumber++;    // 프레임 번호 증가
    }


    // [한글] [실행 순서 2-3] 현재 프레임의 본/IK/모프 데이터 저장
    void SaveFrame()
    {
        if (!EnsureRecorderInitialized())
        {
            IsRecording = false;
            return;
        }

        // BoneGhost: 정규화된 본 구조 업데이트 (Unity 본 → VMD 본 변환)
        if (boneGhost != null) { boneGhost.GhostAll(); }
        
        // MorphRecorder: 모든 BlendShape(모프) 값 기록
        if (morphRecorder != null) { morphRecorder.RecrodAllMorph(); }

        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null)
            {
                continue;  // 본이 없으면 건너뛔
            }

            // Process foot IK and toe IK together.
            // [한글] 발 IK 처리 (왼발, 오른발)
            if (boneName == BoneNames.右足ＩＫ ||
                boneName == BoneNames.左足ＩＫ )
            {
                Vector3 targetVector = Vector3.zero;
                if (UseCenterAsParentOfAll)
                {
                    if ((!UseAbsoluteCoordinateSystem && transform.parent != null) && IgnoreInitialPosition )
                    {
                        targetVector = Quaternion.Inverse(transform.parent.rotation)
                            * (BoneDictionary[boneName].position - transform.parent.position)
                            - parentInitialPosition;
                    }
                    else if ((!UseAbsoluteCoordinateSystem && transform.parent != null) && !IgnoreInitialPosition)
                    {
                        targetVector = Quaternion.Inverse(transform.parent.rotation)
                            * (BoneDictionary[boneName].position - transform.parent.position);
                    }
                    else if ((UseAbsoluteCoordinateSystem || transform.parent == null) && IgnoreInitialPosition)
                    {
                        targetVector = BoneDictionary[boneName].position - parentInitialPosition;
                    }
                    else if ((UseAbsoluteCoordinateSystem || transform.parent == null) && transform.parent && !IgnoreInitialPosition)
                    {
                        targetVector = BoneDictionary[boneName].position;
                    }
                    else
                    {
                        // Make IK bone get global position data
                        targetVector = BoneDictionary[boneName].position;
                        // Cancel IK bone default position data wrt right and left
                        if (boneName == BoneNames.右足ＩＫ)
                            targetVector -= new Vector3(0.05238038f, 0.115296f, -0.02825557f);
                        else
                            targetVector -= new Vector3(-0.05238038f, 0.115296f, -0.02825557f);
                    }
                }
                else
                {
                    targetVector = BoneDictionary[boneName].position - transform.position;
                    targetVector = Quaternion.Inverse(transform.rotation) * targetVector;
                }
                // Now subtract the appropriate IK offset
                //if (boneName == BoneNames.左足ＩＫ)
                //{
                //    targetVector -= LeftFootIKOffset;
                //}
                //else if (boneName == BoneNames.右足ＩＫ)
                //{
                //    targetVector -= RightFootIKOffset;
                //}


                // Unity 좌표계 → VMD 좌표계 변환 (X축, Z축 반전)
                Vector3 ikPosition = new Vector3(-targetVector.x, targetVector.y, -targetVector.z);
                
                // 스케일 보정 및 저장
                positionDictionary[boneName].Add(ikPosition * DefaultBoneAmplifier);
                
                //回転は全部足首／つま先に持たせる（今回はidentity）
                // [한글] 회전은 모두 발목/발끝에 맡김 (지금은 identity)
                Quaternion ikRotation = Quaternion.identity;
                rotationDictionary[boneName].Add(ikRotation);
                continue;
            }

            // 발끝 IK
            // [한글] 발끝 IK 처리 (왼발끝, 오른발끝)
            if (boneName == BoneNames.左つま先ＩＫ ||
                boneName == BoneNames.右つま先ＩＫ)
            {
                Vector3 targetVector = Vector3.zero;

                if (boneName == BoneNames.左つま先ＩＫ)
                {
                    targetVector = BoneDictionary[boneName].position - BoneDictionary[BoneNames.左足ＩＫ].position;
                    //targetVector = Quaternion.Inverse(transform.rotation) * targetVector;
                    //targetVector -= LeftToeIKOffset;
                    targetVector -= new Vector3(-0.001641536f, -0.07096878f, 0.1238693f);
                }
                else if (boneName == BoneNames.右つま先ＩＫ)
                {
                    targetVector = BoneDictionary[boneName].position - BoneDictionary[BoneNames.右足ＩＫ].position;
                    //targetVector = Quaternion.Inverse(transform.rotation) * targetVector;
                    //targetVector -= RightToeIKOffset;
                    targetVector -= new Vector3(0.001641536f, -0.07096878f, 0.1238693f);
                }
                Vector3 ikPosition = new Vector3(-targetVector.x, targetVector.y, -targetVector.z);
                positionDictionary[boneName].Add(ikPosition * DefaultBoneAmplifier);
                //回転は全部足首／つま先に持たせる（今回はidentity）
                Quaternion ikRotation = Quaternion.identity;
                rotationDictionary[boneName].Add(ikRotation);
                continue;
            }

            if (boneGhost != null && boneGhost.GhostDictionary.TryGetValue(boneName, out var ghostEntry))
            {
                if (ghostEntry.ghost == null || !ghostEntry.enabled)
                {
                    rotationDictionary[boneName].Add(Quaternion.identity);
                    positionDictionary[boneName].Add(Vector3.zero);
                    continue;
                }

                Vector3 boneVector = ghostEntry.ghost.localPosition;
                Quaternion boneQuatenion = ghostEntry.ghost.localRotation;
                rotationDictionary[boneName].Add(new Quaternion(-boneQuatenion.x, boneQuatenion.y, -boneQuatenion.z, boneQuatenion.w));

                boneVector -= boneGhost.GhostOriginalLocalPositionDictionary[boneName];

                Vector3 ghostPosition = new Vector3(-boneVector.x, boneVector.y, -boneVector.z) * DefaultBoneAmplifier;
                positionDictionary[boneName].Add(ShouldWriteLocalPosition(boneName) ? ghostPosition : Vector3.zero);
                continue;
            }

            Quaternion fixedQuatenion = Quaternion.identity;
            Quaternion vmdRotation = Quaternion.identity;

            if (boneName == BoneNames.全ての親 && UseAbsoluteCoordinateSystem)
            {
                fixedQuatenion = BoneDictionary[boneName].rotation;
            }
            else
            {
                fixedQuatenion = BoneDictionary[boneName].localRotation;
            }

            if (boneName == BoneNames.全ての親 && IgnoreInitialRotation)
            {
                fixedQuatenion = BoneDictionary[boneName].localRotation.MinusRotation(parentInitialRotation);
            }

            vmdRotation = new Quaternion(-fixedQuatenion.x, fixedQuatenion.y, -fixedQuatenion.z, fixedQuatenion.w);

            rotationDictionary[boneName].Add(vmdRotation);

            Vector3 fixedPosition = Vector3.zero;
            Vector3 vmdPosition = Vector3.zero;

            if (boneName == BoneNames.全ての親 && UseAbsoluteCoordinateSystem)
            {
                fixedPosition = BoneDictionary[boneName].position;
            }
            else
            {
                fixedPosition = BoneDictionary[boneName].localPosition;
            }

            if (boneName == BoneNames.全ての親 && IgnoreInitialPosition)
            {
                fixedPosition -= parentInitialPosition;
            }

            vmdPosition = new Vector3(-fixedPosition.x, fixedPosition.y, -fixedPosition.z);

            if (boneName == BoneNames.全ての親)
            {
                positionDictionary[boneName].Add(vmdPosition * DefaultBoneAmplifier + ParentOfAllOffset);
            }
            else
            {
                positionDictionary[boneName].Add(ShouldWriteLocalPosition(boneName) ? vmdPosition * DefaultBoneAmplifier : Vector3.zero);
            }
        }
    }

    private bool ShouldWriteLocalPosition(BoneNames boneName)
    {
        if (!ZeroNonRootBonePositions)
        {
            return true;
        }

        return boneName == BoneNames.全ての親 || boneName == BoneNames.センター;
    }

    // [한글] 초기 위치/회전 저장 (레코딩 시작 시 기준점 설정)
    void SetInitialPositionAndRotation()
    {
        if (UseAbsoluteCoordinateSystem)  // 절대 좌표계 사용 시
        {
            parentInitialPosition = transform.position;       // 글로벌 위치
            parentInitialRotation = transform.rotation;       // 글로벌 회전
        }
        else  // 상대 좌표계 사용 시
        {
            parentInitialPosition = transform.localPosition;  // 로컬 위치
            parentInitialRotation = transform.localRotation;  // 로컬 회전
        }
    }


    // [한글] FPS 설정 (정적 메서드)
    public static void SetFPS(int fps)
    {
        Time.fixedDeltaTime = 1 / (float)fps;  // FixedUpdate 호출 간격 설정
    }


    /// <summary>
    /// レコーディングを開始または再開
    /// </summary>
    /// [한글] 레코딩 시작 또는 재개
    // [한글] [실행 순서 2-4] HumanoidSampleCode에서 호출 - 레코딩 활성화
    public void StartRecording()
    {
        if (!EnsureRecorderInitialized())
        {
            IsRecording = false;
            return;
        }

        SetInitialPositionAndRotation();  // 현재 위치를 기준점으로 설정
        ResetRecordingCadenceStats();
        ApplyRecordingCaptureFramerate();
        IsRecording = true;                // 레코딩 플래그 활성화
    }

    private bool EnsureRecorderInitialized()
    {
        if (BoneDictionary != null && animator != null && positionDictionary != null && rotationDictionary != null)
        {
            return true;
        }

        Start();

        bool ready = BoneDictionary != null && animator != null && positionDictionary != null && rotationDictionary != null;
        if (!ready && !recorderInitializationWarningLogged)
        {
            Debug.LogError("[UnityHumanoidVMDRecorder] 녹화 본 딕셔너리가 초기화되지 않아 현재 프레임 저장을 건너뜁니다.");
            recorderInitializationWarningLogged = true;
        }

        return ready;
    }

    /// <summary>
    /// レコーディングを一時停止
    /// </summary>
    /// [한글] 레코딩 일시정지
    public void PauseRecording() { IsRecording = false; }  // 레코딩 플래그만 비활성화


    /// <summary>
    /// レコーディングを終了
    /// </summary>
    /// [한글] 레코딩 종료 (데이터 백업 및 초기화)
    // [한글] [실행 순서 2-5] HumanoidSampleCode에서 호출 - 레코딩 중지 및 데이터 백업
    // [한글] [실행 순서 2-5] HumanoidSampleCode에서 호출 - 레코딩 중지 및 데이터 백업
    public void StopRecording()
    {
        IsRecording = false;  // 레코딩 중지
        RestoreRecordingCaptureFramerate();
        
        // [Safety Check] 초기화 전에 Stop이 호출될 경우 방어
        if (BoneDictionary == null) return;
        
        // 현재 레코딩 데이터를 "Saved" 버전으로 백업
        frameNumberSaved = FrameNumber;
        Debug.Log($"[VMDRecorder] 녹화 종료: frames={frameNumberSaved}, afterLate={RecordAfterLateVisualPose}, captureFps={UseCaptureFramerateDuringRecording}, sameUnityFrameSaves={sameUnityFrameSaveCount}, maxLateBurst={maxFramesSavedInSingleLateUpdate}, droppedBacklog={droppedLateFrameBacklogCount}");
        morphRecorderSaved = morphRecorder;
        FrameNumber = 0;
        ResetRecordingCadenceStats();
        positionDictionarySaved = positionDictionary;
        positionDictionary = new Dictionary<BoneNames, List<Vector3>>();
        rotationDictionarySaved = rotationDictionary;
        rotationDictionary = new Dictionary<BoneNames, List<Quaternion>>();
        
        // 다음 레코딩을 위해 딕셔너리 초기화
        foreach (BoneNames boneName in BoneDictionary.Keys)
        {
            if (BoneDictionary[boneName] == null) { continue; }

            positionDictionary.Add(boneName, new List<Vector3>());
            rotationDictionary.Add(boneName, new List<Quaternion>());
        }
        morphRecorder = new VmdMorphRecorder(transform);
    }


}
