using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CameraSampleCode : MonoBehaviour
{
    public string CameraVMDName = "testCameraVMD.vmd";

    public float StartRecordingTime = 0.5f;
    public float StopRecordingTime = 30f;

    string cameraVMDPath = "";
    // Start is called before the first frame update
    void Start()
    {
        string outputFolder = GetDefaultOutputFolder();
        Directory.CreateDirectory(outputFolder);
        cameraVMDPath = Path.Combine(outputFolder, CameraVMDName);
        Invoke("StartRecording", StartRecordingTime);
        Invoke("SaveRecord", StopRecordingTime);
    }

    void StartRecording()
    {
        Camera.main.gameObject.GetComponent<UnityCameraVMDRecorder>().StartRecording();
    }

    void SaveRecord()
    {
        Camera.main.gameObject.GetComponent<UnityCameraVMDRecorder>().StopRecording();
        Camera.main.gameObject.GetComponent<UnityCameraVMDRecorder>().SaveVMD(cameraVMDPath);
    }

    private static string GetDefaultOutputFolder()
    {
        return Path.Combine(Application.dataPath, "VMDRecorderSample");
    }
}
