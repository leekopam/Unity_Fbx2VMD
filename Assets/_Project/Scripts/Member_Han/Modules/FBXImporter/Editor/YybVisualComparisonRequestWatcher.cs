#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Member_Han.Modules.FBXImporter.EditorTools
{
    [InitializeOnLoad]
    public static class YybVisualComparisonRequestWatcher
    {
        private const string RuntimeDirectory = "Docs/Machine_Spirit/Local/runtime";
        private const string RequestFileName = "yyb_visual_compare_request.json";
        private const string StatusFileName = "yyb_visual_compare_status.json";
        private const string BootMarkerFileName = "yyb_visual_compare_watcher_boot.txt";
        private const string TraceFileName = "yyb_visual_compare_watcher_trace.log";
        private const string AwaitingCompletionSessionKey = "Member_Han.YybVisualComparison.WatcherAwaitingCompletion";
        private const string ActiveRequestIdSessionKey = "Member_Han.YybVisualComparison.WatcherActiveRequestId";
        private static readonly string ProjectRoot;
        private static readonly string RequestPath;
        private static readonly string StatusPath;
        private static readonly string TracePath;
        private static bool _awaitingCompletion;
        private static string _activeRequestId = string.Empty;
        private static DateTime _nextPollUtc = DateTime.MinValue;

        static YybVisualComparisonRequestWatcher()
        {
            ProjectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string runtimeDirectoryPath = Path.Combine(ProjectRoot, RuntimeDirectory);
            RequestPath = Path.Combine(runtimeDirectoryPath, RequestFileName);
            StatusPath = Path.Combine(runtimeDirectoryPath, StatusFileName);
            TracePath = Path.Combine(runtimeDirectoryPath, TraceFileName);
            Directory.CreateDirectory(runtimeDirectoryPath);
            File.WriteAllText(
                Path.Combine(runtimeDirectoryPath, BootMarkerFileName),
                DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
            AppendTrace($"loaded request={RequestPath} status={StatusPath}");

            _activeRequestId = SessionState.GetString(ActiveRequestIdSessionKey, string.Empty);
            _awaitingCompletion = SessionState.GetBool(AwaitingCompletionSessionKey, false) &&
                                  !string.IsNullOrWhiteSpace(_activeRequestId);

            if (_awaitingCompletion &&
                !YybVisualComparisonBatchRunner.IsRunning &&
                !YybVisualComparisonBatchRunner.HasPersistedRunState())
            {
                AppendTrace($"clearing stale watcher state activeRequestId={_activeRequestId}");
                ClearAwaitingCompletionState();
            }

            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            if (_awaitingCompletion)
            {
                YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
                YybVisualComparisonBatchRunner.RunCompleted += HandleRunCompleted;
                AppendTrace($"resubscribed activeRequestId={_activeRequestId}");
                if (!File.Exists(StatusPath))
                {
                    WriteStatus(new StatusEnvelope
                    {
                        request_id = _activeRequestId,
                        status = "running",
                        updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                        message = "watcher resumed after domain reload",
                        passed = false,
                        failures = Array.Empty<string>()
                    });
                }
            }
            Debug.Log("[YybVisualComparisonRequestWatcher] loaded");
        }

        [Serializable]
        private sealed class RequestEnvelope
        {
            public string request_id;
            public string requested_at;
            public string fbx_file;
            public float duration_seconds = 31f;
            public bool finger_closeups;
        }

        [Serializable]
        private sealed class StatusEnvelope
        {
            public string request_id;
            public string status;
            public string updated_at;
            public string message;
            public bool passed;
            public string session_id;
            public string summary_json_path;
            public string summary_markdown_path;
            public string latest_summary_json_path;
            public string latest_summary_markdown_path;
            public int total_jobs;
            public int success_jobs;
            public string[] failures;
        }

        private static void Poll()
        {
            bool runnerRunning = YybVisualComparisonBatchRunner.IsRunning;
            bool hasPersistedRunState = YybVisualComparisonBatchRunner.HasPersistedRunState();

            if (_awaitingCompletion &&
                !runnerRunning &&
                hasPersistedRunState &&
                !EditorApplication.isCompiling &&
                !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                AppendTrace($"requesting runner resume activeRequestId={_activeRequestId}");
                bool resumed = YybVisualComparisonBatchRunner.TryResumePersistedRun();
                runnerRunning = YybVisualComparisonBatchRunner.IsRunning;
                hasPersistedRunState = YybVisualComparisonBatchRunner.HasPersistedRunState();
                AppendTrace(
                    $"runner resume result activeRequestId={_activeRequestId} " +
                    $"resumed={resumed} runnerRunning={runnerRunning} persisted={hasPersistedRunState}");
            }

            if (_awaitingCompletion &&
                !runnerRunning &&
                !hasPersistedRunState)
            {
                AppendTrace($"clearing live stale watcher state activeRequestId={_activeRequestId}");
                ClearAwaitingCompletionState();
            }

            if (DateTime.UtcNow < _nextPollUtc)
            {
                return;
            }

            _nextPollUtc = DateTime.UtcNow.AddSeconds(1);

            if (_awaitingCompletion || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!File.Exists(RequestPath))
            {
                return;
            }

            RequestEnvelope request;
            try
            {
                Debug.Log("[YybVisualComparisonRequestWatcher] request detected");
                request = JsonUtility.FromJson<RequestEnvelope>(File.ReadAllText(RequestPath));
            }
            catch (Exception ex)
            {
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = string.Empty,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = $"request read failed: {ex.Message}",
                    passed = false,
                    failures = new[] { ex.Message }
                });
                return;
            }

            if (request == null)
            {
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = string.Empty,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = "request payload is null",
                    passed = false,
                    failures = new[] { "request payload is null" }
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(request.request_id))
            {
                request.request_id = Guid.NewGuid().ToString("N");
                PersistRequest(request);
            }

            StatusEnvelope existingStatus = TryReadStatus();
            if (existingStatus != null &&
                string.Equals(existingStatus.request_id, request.request_id, StringComparison.Ordinal))
            {
                if (string.Equals(existingStatus.status, "completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(existingStatus.status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    AppendTrace($"dropping settled request={request.request_id} state={existingStatus.status}");
                    TryDeleteRequestFile();
                    return;
                }

                if (string.Equals(existingStatus.status, "running", StringComparison.OrdinalIgnoreCase) &&
                    !YybVisualComparisonBatchRunner.IsRunning &&
                    !YybVisualComparisonBatchRunner.HasPersistedRunState())
                {
                    AppendTrace($"restarting orphaned request={request.request_id}");
                }
            }

            if (YybVisualComparisonBatchRunner.IsRunning)
            {
                WriteStatus(new StatusEnvelope
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = "YYB visual comparison is already running",
                    passed = false,
                    failures = new[] { "runner already active" }
                });
                return;
            }

            _awaitingCompletion = true;
            _activeRequestId = request.request_id;
            SessionState.SetBool(AwaitingCompletionSessionKey, true);
            SessionState.SetString(ActiveRequestIdSessionKey, _activeRequestId);
            YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
            YybVisualComparisonBatchRunner.RunCompleted += HandleRunCompleted;
            AppendTrace($"starting request={_activeRequestId} fbx={request.fbx_file}");

            WriteStatus(new StatusEnvelope
            {
                request_id = request.request_id,
                status = "running",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                message = $"started fbx={request.fbx_file}",
                passed = false,
                failures = Array.Empty<string>()
            });

            try
            {
                YybVisualComparisonBatchRunner.RunWithOptions(
                    request.fbx_file,
                    request.duration_seconds,
                    request.finger_closeups);
            }
            catch (Exception ex)
            {
                YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
                ClearAwaitingCompletionState();
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    message = ex.Message,
                    passed = false,
                    failures = new[] { ex.Message }
                });
            }
        }

        private static void HandleRunCompleted(YybVisualComparisonBatchRunner.RunCompletionInfo info)
        {
            string completedRequestId = _activeRequestId;
            YybVisualComparisonBatchRunner.RunCompleted -= HandleRunCompleted;
            ClearAwaitingCompletionState();
            AppendTrace($"completed request={completedRequestId} passed={info.passed} session={info.sessionId}");
            TryDeleteRequestFile();

            WriteStatus(new StatusEnvelope
            {
                request_id = completedRequestId,
                status = info.passed ? "completed" : "failed",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                message = info.passed ? "comparison finished" : "comparison finished with failures",
                passed = info.passed,
                session_id = info.sessionId,
                summary_json_path = info.summaryJsonPath,
                summary_markdown_path = info.summaryMarkdownPath,
                latest_summary_json_path = info.latestSummaryJsonPath,
                latest_summary_markdown_path = info.latestSummaryMarkdownPath,
                total_jobs = info.totalJobs,
                success_jobs = info.successJobs,
                failures = info.failures ?? Array.Empty<string>()
            });
        }

        private static void ClearAwaitingCompletionState()
        {
            _awaitingCompletion = false;
            _activeRequestId = string.Empty;
            SessionState.SetBool(AwaitingCompletionSessionKey, false);
            SessionState.EraseString(ActiveRequestIdSessionKey);
        }

        private static void WriteStatus(StatusEnvelope status)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatusPath) ?? ProjectRoot);
            string json = JsonUtility.ToJson(status, true);
            File.WriteAllText(StatusPath, json);
            AppendTrace($"status request={status.request_id} state={status.status} path={StatusPath}");
        }

        private static void PersistRequest(RequestEnvelope request)
        {
            if (request == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath) ?? ProjectRoot);
            string json = JsonUtility.ToJson(request, true);
            File.WriteAllText(RequestPath, json);
        }

        private static StatusEnvelope TryReadStatus()
        {
            if (!File.Exists(StatusPath))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<StatusEnvelope>(File.ReadAllText(StatusPath));
            }
            catch
            {
                return null;
            }
        }

        private static void TryDeleteRequestFile()
        {
            try
            {
                if (File.Exists(RequestPath))
                {
                    File.Delete(RequestPath);
                }
            }
            catch (Exception ex)
            {
                AppendTrace($"request delete skipped path={RequestPath} reason={ex.Message}");
            }
        }

        private static void AppendTrace(string message)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TracePath) ?? ProjectRoot);
            File.AppendAllText(
                TracePath,
                $"{DateTime.Now.ToString("o", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
        }
    }
}
#endif

