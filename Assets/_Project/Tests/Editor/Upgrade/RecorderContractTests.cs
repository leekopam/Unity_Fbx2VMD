using Fbx2Vmd.FBXImporter;
using Fbx2Vmd.Recording;
using NUnit.Framework;
using UnityEditor.Recorder;
using UnityEngine;

namespace Tests.Editor.Upgrade
{
    /// <summary>
    /// Unity Recorder 패키지 API 계약. 녹화 파이프라인이 Recorder API에 직접 의존하므로
    /// 4.x→5.x 버전 점프 시 설정 생성과 PrepareRecording이 계속 동작해야 한다.
    /// </summary>
    [Category("UpgradeReadiness")]
    public class RecorderContractTests
    {
        [Test]
        public void EditorMotionVideoRecorder_TryPrepare_Completes()
        {
            var recorder = new EditorMotionVideoRecorder();
            var settings = new MotionVideoRecordingSettings(
                motionName: "upgrade_readiness_probe",
                width: 320,
                height: 240,
                frameRate: 30f,
                captureAudio: false,
                outputDirectory: null,
                format: MotionVideoFileFormat.Mp4);

            // PrepareRecording은 카메라 부재 등으로 실패할 수 있다 — 계약은
            // "호출이 완료되고 실패 시 에러 채널이 채워진다"이다.
            bool prepared = recorder.TryPrepare(settings, out string errorMessage);

            Assert.That(prepared || !string.IsNullOrEmpty(errorMessage), Is.True,
                "TryPrepare이 성공도 실패 메시지도 없이 반환됨");
        }

        [Test]
        public void RecorderControllerApi_ConstructsWithoutException()
        {
            RecorderControllerSettings controllerSettings = null;
            MovieRecorderSettings movieSettings = null;
            RecorderController controller = null;

            try
            {
                controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
                movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                movieSettings.Enabled = true;

                controllerSettings.AddRecorderSettings(movieSettings);
                controllerSettings.SetRecordModeToManual();
                controllerSettings.FrameRate = 30f;

                controller = new RecorderController(controllerSettings);

                Assert.That(controller, Is.Not.Null, "RecorderController 생성 실패");
            }
            finally
            {
                if (controllerSettings != null)
                {
                    Object.DestroyImmediate(controllerSettings);
                }
                if (movieSettings != null)
                {
                    Object.DestroyImmediate(movieSettings);
                }
            }
        }
    }
}
