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
    /// <summary>
    /// 새 녹화 세션을 시작하기 전에 활성 녹화 버퍼와 프레임 번호를 초기화한다.
    /// StopRecording은 저장용 백업을 만들기 때문에, 비교 QA처럼 같은 프레임 기준이 중요한 새 세션에서는
    /// StartRecording 직전에 이 메서드로 활성 버퍼가 반드시 0프레임에서 시작하도록 보장한다.
    /// </summary>
    public void ResetRecordingBuffersForNewSession(int expectedFrameCapacity = 0)
    {
        EnsureRecorderInitialized();

        FrameNumber = 0;
        ResetRecordingCadenceStats();
        positionDictionary = new Dictionary<BoneNames, List<Vector3>>();
        rotationDictionary = new Dictionary<BoneNames, List<Quaternion>>();
        int listCapacity = Mathf.Max(0, expectedFrameCapacity);

        if (BoneDictionary != null)
        {
            foreach (BoneNames boneName in BoneDictionary.Keys)
            {
                if (BoneDictionary[boneName] == null)
                {
                    continue;
                }

                positionDictionary[boneName] = listCapacity > 0 ? new List<Vector3>(listCapacity) : new List<Vector3>();
                rotationDictionary[boneName] = listCapacity > 0 ? new List<Quaternion>(listCapacity) : new List<Quaternion>();
            }
        }

        if (transform != null)
        {
            morphRecorder = new VmdMorphRecorder(transform, listCapacity);
        }
    }

    private void ResetRecordingCadenceStats()
    {
        recordingFrameAccumulator = 0f;
        lastSavedUnityFrame = -1;
        sameUnityFrameSaveCount = 0;
        maxFramesSavedInSingleLateUpdate = 0;
        droppedLateFrameBacklogCount = 0;
    }

    private void ApplyRecordingCaptureFramerate()
    {
        if (!RecordAfterLateVisualPose || !UseCaptureFramerateDuringRecording || captureFramerateApplied)
        {
            return;
        }

        previousCaptureFramerate = Time.captureFramerate;
        Time.captureFramerate = Mathf.RoundToInt(1f / FPSs);
        captureFramerateApplied = true;
    }

    private void RestoreRecordingCaptureFramerate()
    {
        if (!captureFramerateApplied)
        {
            return;
        }

        Time.captureFramerate = previousCaptureFramerate;
        captureFramerateApplied = false;
    }


}
