#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    [Serializable]
    internal sealed class FbxPlaybackSmokeAutomationRequest
    {
        public string request_id;
        public string command;
        public string requested_command;
        public string run_id;
        public string scene_path;
        public string fbx_file;
        public string model_name;
        public int[] segment_starts;
        public int[] segment_ends;
        public string vrm_file;
        public int[] capture_frames;
        public int[] side_capture_frames;
        public int screen_width;
        public int screen_height;
        public bool lower_body_only;
        // -1이면 미지정으로 파이프라인 기본값을 따름, 0이면 명시 해제, 양수면 해당 알파로 활성.
        public float ik_goal_alpha = -1f;
        // 영상 녹화 증거 수집(capture_motion_recording_evidence) 파라미터.
        public bool recording_range_enabled;
        public int recording_start_frame;
        public int recording_end_frame;
        // -1이면 미지정으로 파이프라인 직렬화 값 유지, 0이면 끔, 1이면 켬.
        public int recording_skip_still = -1;
        // 0 이하면 구간 끝 자동 정지까지 대기, 양수면 해당 실시간 경과 후 수동 정지.
        public float recording_stop_after_seconds;
        public bool recording_try_import_during;
    }

    [Serializable]
    internal sealed class FbxPlaybackSmokeAutomationStatus
    {
        public string request_id;
        public string status;
        public string updated_at;
        public string command;
        public string message;
        public bool passed;
        public string manifest_path;
        public string failure_stage;
        public string output_path;
        public int frame_count;
        public long file_size_bytes;
        public string vrm_output_path;
        public long vrm_file_size_bytes;
        public string capture_path;
        public string preselection_state_path;
        public string playback_state_path;
        public string foot_live_state_path;
        public string full_clip_state_path;
        public string failure_evidence_path;
        public string environment_state_path;
        public int total_jobs;
        public int success_jobs;
        public string[] failures;
    }

    /// <summary>
    /// FBX 재생 smoke 자동화의 요청·상태·추적 파일을 저장함.
    /// </summary>
    internal sealed class FbxPlaybackSmokeAutomationStore
    {
        private const string RequestFileName = "fbx_smoke_request.json";
        private const string StatusFileName = "fbx_smoke_status.json";
        private const string TraceFileName = "fbx_smoke_trace.log";

        private readonly string _runtimeDirectoryPath;

        internal FbxPlaybackSmokeAutomationStore(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException("프로젝트 루트 경로가 필요합니다.", nameof(projectRoot));
            }

            _runtimeDirectoryPath = Path.Combine(
                projectRoot,
                "Docs",
                "Workflow",
                "Local",
                "runtime");
            RequestPath = Path.Combine(_runtimeDirectoryPath, RequestFileName);
            StatusPath = Path.Combine(_runtimeDirectoryPath, StatusFileName);
            TracePath = Path.Combine(_runtimeDirectoryPath, TraceFileName);
            Directory.CreateDirectory(_runtimeDirectoryPath);
        }

        internal string RequestPath { get; }

        internal string StatusPath { get; }

        internal string TracePath { get; }

        internal bool HasPendingRequest => File.Exists(RequestPath);

        internal FbxPlaybackSmokeAutomationRequest ReadRequest()
        {
            return JsonUtility.FromJson<FbxPlaybackSmokeAutomationRequest>(File.ReadAllText(RequestPath));
        }

        internal void SaveRequest(FbxPlaybackSmokeAutomationRequest request)
        {
            if (request == null)
            {
                return;
            }

            EnsureRuntimeDirectory();
            File.WriteAllText(RequestPath, JsonUtility.ToJson(request, true));
        }

        internal void SaveStatus(FbxPlaybackSmokeAutomationStatus status)
        {
            EnsureRuntimeDirectory();
            File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
        }

        internal void AppendTrace(string message)
        {
            EnsureRuntimeDirectory();
            File.AppendAllText(
                TracePath,
                $"{DateTime.Now.ToString("o", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
        }

        internal void DeleteRequest()
        {
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }
        }

        private void EnsureRuntimeDirectory()
        {
            Directory.CreateDirectory(_runtimeDirectoryPath);
        }
    }
}
#endif
