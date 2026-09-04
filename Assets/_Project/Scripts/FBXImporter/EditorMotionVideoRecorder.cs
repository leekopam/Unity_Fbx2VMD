#if UNITY_EDITOR

using System;
using System.IO;
using Fbx2Vmd.Recording;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Unity Editor의 주 카메라 출력을 H.264 MP4로 기록함.
    /// </summary>
    internal sealed class EditorMotionVideoRecorder : IMotionVideoRecorder
    {
        private const string OutputDirectoryName = "Recordings";
        private RecorderController _recorderController;
        private RecorderControllerSettings _controllerSettings;
        private MovieRecorderSettings _movieSettings;

        public bool IsRecording =>
            _recorderController != null && _recorderController.IsRecording();

        public string OutputFilePath { get; private set; } = string.Empty;

        public bool TryPrepare(
            MotionVideoRecordingSettings settings,
            out string errorMessage)
        {
            DisposeRecorderObjects();

            if (!TryValidate(settings, out errorMessage))
            {
                return false;
            }

            try
            {
                string outputFileWithoutExtension = CreateOutputFilePath(settings.MotionName);
                OutputFilePath = outputFileWithoutExtension + ".mp4";

                _controllerSettings =
                    ScriptableObject.CreateInstance<RecorderControllerSettings>();
                _movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                _movieSettings.name = "FBX Motion Video Recorder";
                _movieSettings.Enabled = true;
                _movieSettings.EncoderSettings = new CoreEncoderSettings
                {
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
                    Codec = CoreEncoderSettings.OutputCodec.MP4
                };
                _movieSettings.CaptureAudio = settings.CaptureAudio;
                _movieSettings.ImageInputSettings = new CameraInputSettings
                {
                    Source = ImageSource.MainCamera,
                    OutputWidth = settings.Width,
                    OutputHeight = settings.Height
                };
                _movieSettings.OutputFile = outputFileWithoutExtension;

                _controllerSettings.AddRecorderSettings(_movieSettings);
                _controllerSettings.SetRecordModeToManual();
                _controllerSettings.FrameRate = settings.FrameRate;
                RecorderOptions.VerboseMode = false;

                _recorderController = new RecorderController(_controllerSettings);
                _recorderController.PrepareRecording();
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"영상 녹화 준비에 실패했습니다: {exception.Message}";
                DisposeRecorderObjects();
                return false;
            }
        }

        public bool TryStart(out string errorMessage)
        {
            if (_recorderController == null)
            {
                errorMessage = "영상 녹화기가 준비되지 않았습니다.";
                return false;
            }

            try
            {
                if (_recorderController.StartRecording())
                {
                    errorMessage = string.Empty;
                    return true;
                }

                errorMessage = "Unity Recorder가 영상 녹화를 시작하지 못했습니다.";
                return false;
            }
            catch (Exception exception)
            {
                errorMessage = $"영상 녹화 시작에 실패했습니다: {exception.Message}";
                return false;
            }
        }

        public void Stop()
        {
            if (_recorderController == null)
            {
                return;
            }

            try
            {
                _recorderController.StopRecording();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FBXImport] 영상 녹화 종료 중 문제가 발생했습니다: {exception.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            DisposeRecorderObjects();
        }

        private static bool TryValidate(
            MotionVideoRecordingSettings settings,
            out string errorMessage)
        {
            if (!Application.isPlaying)
            {
                errorMessage = "영상 녹화는 Unity Play Mode에서만 사용할 수 있습니다.";
                return false;
            }

            if (settings.Width <= 0 || settings.Height <= 0)
            {
                errorMessage = "영상 녹화 해상도는 0보다 커야 합니다.";
                return false;
            }

            if (float.IsNaN(settings.FrameRate) ||
                float.IsInfinity(settings.FrameRate) ||
                settings.FrameRate <= 0f)
            {
                errorMessage = "영상 녹화 프레임률은 유효한 양수여야 합니다.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static string CreateOutputFilePath(string motionName)
        {
            string projectDirectory = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(projectDirectory, OutputDirectoryName);
            Directory.CreateDirectory(outputDirectory);

            string safeMotionName = SanitizeFileName(motionName);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(outputDirectory, $"{safeMotionName}_{timestamp}");
        }

        private static string SanitizeFileName(string value)
        {
            string candidate = string.IsNullOrWhiteSpace(value) ? "motion" : value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                candidate = candidate.Replace(invalidCharacter, '_');
            }

            return string.IsNullOrWhiteSpace(candidate) ? "motion" : candidate;
        }

        private void DisposeRecorderObjects()
        {
            _recorderController = null;
            DestroyImmediate(_movieSettings);
            DestroyImmediate(_controllerSettings);
            _movieSettings = null;
            _controllerSettings = null;
        }

        private static void DestroyImmediate(UnityEngine.Object value)
        {
            if (value != null)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
        }
    }
}

#endif
