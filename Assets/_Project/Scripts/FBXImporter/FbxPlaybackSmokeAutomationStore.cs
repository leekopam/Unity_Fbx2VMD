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
