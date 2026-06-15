#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Member_Han.Modules.FBXImporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Member_Han.Modules.FBXImporter.EditorTools
{
    [InitializeOnLoad]
    public static class FbxPlaybackSmokeRunner
    {
        private const string MenuRoot = "Machine Spirit/FBX Smoke/";
        private const string MainAutoSceneName = "Main_Auto";
        private const string MainRecordingSceneName = "Main_recoding";
        private const string ImportFbxRelativeDirectory = "Resources/Import_FBX";
        private const string RuntimeDirectory = "Docs/Machine_Spirit/Local/runtime";
        private const string RequestFileName = "fbx_smoke_request.json";
        private const string StatusFileName = "fbx_smoke_status.json";
        private const string TraceFileName = "fbx_smoke_trace.log";
        private const string RunAllImportFbxHeadCommand = "run_all_import_fbx_31s";
        private const string RunAllImportFbxMiddleCommand = "run_all_import_fbx_middle_31s";
        private const string RunAllImportFbxTailCommand = "run_all_import_fbx_tail_31s";
        private const string CaptureSatisfactionThumbEvidenceCommand = "capture_satisfaction_thumb_evidence_14s";
        private const string CaptureSatisfactionFullRegressionEvidenceCommand = "capture_satisfaction_full_regression_evidence_208s_4k";
        private const string CaptureAntennaTailHelperEvidenceCommand = "capture_antenna_tail_helper_evidence";
        private const string CaptureAntennaTailHelperEvidenceCleanCommand = "capture_antenna_tail_helper_evidence_clean";
        private const string CaptureAntennaTailHelperEvidenceResumeAfterCleanCommand = "capture_antenna_tail_helper_evidence_resume_after_clean";
        private const string SatisfactionFbxFileName = "satisfaction_2.fbx";
        private const float SmokeDurationSeconds = 31f;
        private const float SmokeFrameRate = 30f;
        private const float SmokeStartDelaySeconds = 0.2f;
        private const float SatisfactionFullRegressionEvidenceDurationSeconds = 207.7833f;
        private const int FullRegressionEvidenceCaptureWidth = 3840;
        private const int FullRegressionEvidenceCaptureHeight = 2160;
        private const float SatisfactionSmokeMidPeakTimeSeconds = 16.9f;
        private const float SatisfactionSmokeLatePeakTimeSeconds = 27.2f;
        private const float SatisfactionMiddleHelperEvidenceTimeSeconds = 102.125f;
        private const float SatisfactionTailHelperEvidenceTimeSeconds = 181.25f;
        private const float NeoMiddleHelperEvidenceTimeSeconds = 98.575f;
        private const float NeoTailHelperEvidenceTimeSeconds = 183.85f;
        private const float AntennaTailHelperEvidenceTimeSeconds = 188.567f;
        private static readonly float[] SatisfactionSmokeSampleTimes =
        {
            ThumbEvidenceEarlyTimeSeconds,
            ThumbEvidenceEarlyPeakTimeSeconds,
            ThumbEvidencePeakTimeSeconds,
            ThumbEvidenceTimeSeconds,
            SatisfactionSmokeMidPeakTimeSeconds,
            SatisfactionSmokeLatePeakTimeSeconds
        };
        private const float ThumbEvidenceEarlyTimeSeconds = 0.6f;
        private const float ThumbEvidenceEarlyPeakTimeSeconds = 1f;
        private const float ThumbEvidencePeakTimeSeconds = 12.6f;
        private const float ThumbEvidenceTimeSeconds = 13.1f;
        private const float ThumbEvidenceDurationSeconds = 14f;
        private static readonly float[] ThumbEvidenceSampleTimes =
        {
            ThumbEvidenceEarlyTimeSeconds,
            ThumbEvidenceEarlyPeakTimeSeconds,
            ThumbEvidencePeakTimeSeconds,
            ThumbEvidenceTimeSeconds
        };
        private static readonly float[] SatisfactionFullRegressionEvidenceSampleTimes =
        {
            ThumbEvidenceEarlyTimeSeconds,
            ThumbEvidenceEarlyPeakTimeSeconds,
            ThumbEvidencePeakTimeSeconds,
            ThumbEvidenceTimeSeconds,
            SatisfactionMiddleHelperEvidenceTimeSeconds,
            SatisfactionTailHelperEvidenceTimeSeconds
        };
        private static readonly float[] SatisfactionMiddleHelperEvidenceSampleTimes =
        {
            SatisfactionMiddleHelperEvidenceTimeSeconds
        };
        private static readonly float[] SatisfactionTailHelperEvidenceSampleTimes =
        {
            SatisfactionTailHelperEvidenceTimeSeconds
        };
        private static readonly float[] NeoMiddleHelperEvidenceSampleTimes =
        {
            NeoMiddleHelperEvidenceTimeSeconds
        };
        private static readonly float[] NeoTailHelperEvidenceSampleTimes =
        {
            NeoTailHelperEvidenceTimeSeconds
        };

        private static readonly Queue<string> PendingSmokeFiles = new Queue<string>();
        private static readonly List<string> BatchSuccesses = new List<string>();
        private static readonly List<string> BatchFailures = new List<string>();
        private static readonly string ProjectRoot;
        private static readonly string RequestPath;
        private static readonly string StatusPath;
        private static readonly string TracePath;
        private static FileManager _batchFileManager;
        private static FileManager _singleFileManager;
        private static string _activeSingleFbxFileName;
        private static string _singleSmokeMode;
        private static string _activeBatchFbxFileName;
        private static int _batchTotalCount;
        private static FileManager.EditorDiagnosticSmokeSegment _batchSegment = FileManager.EditorDiagnosticSmokeSegment.Head;
        private static string _activeAutomationRequestId;
        private static string _activeAutomationCommand;
        private static string _activeAutomationRequestedCommand;
        private static DateTime _nextAutomationPollUtc = DateTime.MinValue;

        [Serializable]
        private sealed class RequestEnvelope
        {
            public string request_id;
            public string command;
            public string requested_command;
        }

        [Serializable]
        private sealed class StatusEnvelope
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

        static FbxPlaybackSmokeRunner()
        {
            ProjectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string runtimeDirectoryPath = Path.Combine(ProjectRoot, RuntimeDirectory);
            RequestPath = Path.Combine(runtimeDirectoryPath, RequestFileName);
            StatusPath = Path.Combine(runtimeDirectoryPath, StatusFileName);
            TracePath = Path.Combine(runtimeDirectoryPath, TraceFileName);
            Directory.CreateDirectory(runtimeDirectoryPath);
            EditorApplication.update -= PollAutomationRequest;
            EditorApplication.update += PollAutomationRequest;
        }

        [MenuItem(MenuRoot + "Run All Import_FBX 31s", false, 2090)]
        private static void RunAllImportFbxSmoke()
        {
            if (!TryGetFileManager(null, out FileManager fileManager))
            {
                return;
            }

            string[] fbxFileNames = GetImportFbxFileNames();
            if (fbxFileNames.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Import_FBX 폴더에 FBX 파일이 없습니다.", "확인");
                return;
            }

            StartSmokeBatch(fileManager, fbxFileNames, FileManager.EditorDiagnosticSmokeSegment.Head);
        }

        [MenuItem(MenuRoot + "Run All Import_FBX Middle 31s", false, 2091)]
        private static void RunAllImportFbxMiddleSmoke()
        {
            if (!TryGetFileManager(null, out FileManager fileManager))
            {
                return;
            }

            string[] fbxFileNames = GetImportFbxFileNames();
            if (fbxFileNames.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Import_FBX 폴더에 FBX 파일이 없습니다.", "확인");
                return;
            }

            StartSmokeBatch(fileManager, fbxFileNames, FileManager.EditorDiagnosticSmokeSegment.Middle);
        }

        [MenuItem(MenuRoot + "Run All Import_FBX Tail 31s", false, 2092)]
        private static void RunAllImportFbxTailSmoke()
        {
            if (!TryGetFileManager(null, out FileManager fileManager))
            {
                return;
            }

            string[] fbxFileNames = GetImportFbxFileNames();
            if (fbxFileNames.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Import_FBX 폴더에 FBX 파일이 없습니다.", "확인");
                return;
            }

            StartSmokeBatch(fileManager, fbxFileNames, FileManager.EditorDiagnosticSmokeSegment.Tail);
        }

        [MenuItem(MenuRoot + "Run satisfaction_2 31s", false, 2100)]
        private static void RunSatisfactionSmoke()
        {
            RunSingleSmoke(
                SatisfactionFbxFileName,
                SmokeDurationSeconds,
                enableFingerCloseups: false,
                sampleTimesOverride: SatisfactionSmokeSampleTimes);
        }

        [MenuItem(MenuRoot + "Run Antenna39 31s", false, 2101)]
        private static void RunAntennaSmoke()
        {
            RunSingleSmoke("Antenna39 try_006 g.fbx");
        }

        [MenuItem(MenuRoot + "Run Snake 31s", false, 2102)]
        private static void RunSnakeSmoke()
        {
            RunSingleSmoke("Snake Hip Hop Dance.fbx");
        }

        [MenuItem(MenuRoot + "Run mikumikuni_retake_000 31s", false, 2103)]
        private static void RunMikumikuniSmoke()
        {
            RunSingleSmoke("mikumikuni_retake_000.fbx");
        }

        [MenuItem(MenuRoot + "Run neo_1_001 31s", false, 2104)]
        private static void RunNeoSmoke()
        {
            RunSingleSmoke("neo_1_001.fbx");
        }

        [MenuItem(MenuRoot + "Capture satisfaction_2 Thumb Evidence 0.6s+1.0s+12.6s+13.1s", false, 2105)]
        private static void CaptureSatisfactionThumbEvidence()
        {
            RunSingleSmoke(
                SatisfactionFbxFileName,
                ThumbEvidenceDurationSeconds,
                enableFingerCloseups: true,
                sampleTimesOverride: ThumbEvidenceSampleTimes,
                mode: "thumb-evidence");
        }

        [MenuItem(MenuRoot + "Capture satisfaction_2 Full Regression Evidence 208s 4K", false, 2106)]
        private static void CaptureSatisfactionFullRegressionEvidence()
        {
            RunSingleSmoke(
                SatisfactionFbxFileName,
                SatisfactionFullRegressionEvidenceDurationSeconds,
                enableFingerCloseups: true,
                sampleTimesOverride: SatisfactionFullRegressionEvidenceSampleTimes,
                mode: "full-regression-evidence",
                segment: FileManager.EditorDiagnosticSmokeSegment.Head,
                captureWidthOverride: FullRegressionEvidenceCaptureWidth,
                captureHeightOverride: FullRegressionEvidenceCaptureHeight);
        }

        [MenuItem(MenuRoot + "Capture satisfaction_2 Middle Helper Evidence 102.125s", false, 2107)]
        private static void CaptureSatisfactionMiddleHelperEvidence()
        {
            RunSingleSmoke(
                SatisfactionFbxFileName,
                SmokeDurationSeconds,
                enableFingerCloseups: true,
                sampleTimesOverride: SatisfactionMiddleHelperEvidenceSampleTimes,
                mode: "helper-evidence-middle",
                segment: FileManager.EditorDiagnosticSmokeSegment.Middle);
        }

        [MenuItem(MenuRoot + "Capture satisfaction_2 Tail Helper Evidence 181.25s", false, 2108)]
        private static void CaptureSatisfactionTailHelperEvidence()
        {
            RunSingleSmoke(
                SatisfactionFbxFileName,
                SmokeDurationSeconds,
                enableFingerCloseups: true,
                sampleTimesOverride: SatisfactionTailHelperEvidenceSampleTimes,
                mode: "helper-evidence-tail",
                segment: FileManager.EditorDiagnosticSmokeSegment.Tail);
        }

        [MenuItem(MenuRoot + "Capture neo_1_001 Middle Helper Evidence 98.575s", false, 2109)]
        private static void CaptureNeoMiddleHelperEvidence()
        {
            RunSingleSmoke(
                "neo_1_001.fbx",
                SmokeDurationSeconds,
                enableFingerCloseups: true,
                sampleTimesOverride: NeoMiddleHelperEvidenceSampleTimes,
                mode: "helper-evidence-middle",
                segment: FileManager.EditorDiagnosticSmokeSegment.Middle);
        }

        [MenuItem(MenuRoot + "Capture neo_1_001 Tail Helper Evidence 183.85s", false, 2110)]
        private static void CaptureNeoTailHelperEvidence()
        {
            RunSingleSmoke(
                "neo_1_001.fbx",
                SmokeDurationSeconds,
                enableFingerCloseups: true,
                sampleTimesOverride: NeoTailHelperEvidenceSampleTimes,
                mode: "helper-evidence-tail",
                segment: FileManager.EditorDiagnosticSmokeSegment.Tail);
        }

        [MenuItem(MenuRoot + "Capture Antenna39 Tail Helper Evidence 188.567s", false, 2111)]
        private static void CaptureAntennaTailHelperEvidence()
        {
            RunSingleSmoke(
                "Antenna39 try_006 g.fbx",
                SmokeDurationSeconds,
                enableFingerCloseups: true,
                sampleTimesOverride: new[] { AntennaTailHelperEvidenceTimeSeconds },
                mode: "helper-evidence-tail",
                segment: FileManager.EditorDiagnosticSmokeSegment.Tail);
        }

        [MenuItem(MenuRoot + "Dump Active FileManager Probe", false, 2112)]
        private static void DumpActiveFileManagerProbe()
        {
            if (!ValidateRuntimeContext(null, interactive: true, out _))
            {
                return;
            }

            FileManager runtimeFileManager = FindRuntimeFileManager();
            string fileManagerState = runtimeFileManager != null
                ? $"found={GetHierarchyPath(runtimeFileManager.transform)}"
                : "found=<none>";
            Debug.Log($"[FbxPlaybackSmokeRunner] FileManager probe: {BuildMainAutoRuntimeSummary()}, runtimeLookup={fileManagerState}, thumbReference[{BuildRetargeterThumbReferenceSummary(runtimeFileManager)}]");
        }

        [MenuItem(MenuRoot + "Run All Import_FBX 31s", true)]
        [MenuItem(MenuRoot + "Run All Import_FBX Middle 31s", true)]
        [MenuItem(MenuRoot + "Run All Import_FBX Tail 31s", true)]
        [MenuItem(MenuRoot + "Run satisfaction_2 31s", true)]
        [MenuItem(MenuRoot + "Run Antenna39 31s", true)]
        [MenuItem(MenuRoot + "Run Snake 31s", true)]
        [MenuItem(MenuRoot + "Run mikumikuni_retake_000 31s", true)]
        [MenuItem(MenuRoot + "Run neo_1_001 31s", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Thumb Evidence 0.6s+1.0s+12.6s+13.1s", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Full Regression Evidence 208s 4K", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Middle Helper Evidence 102.125s", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Tail Helper Evidence 181.25s", true)]
        [MenuItem(MenuRoot + "Capture neo_1_001 Middle Helper Evidence 98.575s", true)]
        [MenuItem(MenuRoot + "Capture neo_1_001 Tail Helper Evidence 183.85s", true)]
        [MenuItem(MenuRoot + "Capture Antenna39 Tail Helper Evidence 188.567s", true)]
        [MenuItem(MenuRoot + "Dump Active FileManager Probe", true)]
        private static bool ValidateSmokeMenu()
        {
            return EditorApplication.isPlaying && !EditorApplication.isPaused && !EditorApplication.isCompiling;
        }

        private static void PollAutomationRequest()
        {
            if (DateTime.UtcNow < _nextAutomationPollUtc)
            {
                return;
            }

            _nextAutomationPollUtc = DateTime.UtcNow.AddSeconds(1);

            if (!string.IsNullOrEmpty(_activeAutomationRequestId) ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode))
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
                request = JsonUtility.FromJson<RequestEnvelope>(File.ReadAllText(RequestPath));
            }
            catch (Exception ex)
            {
                TraceAutomation($"request read failed: {ex.Message}");
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = string.Empty,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = string.Empty,
                    message = $"request read failed: {ex.Message}",
                    passed = false,
                    failures = new[] { ex.Message }
                });
                return;
            }

            if (request == null || string.IsNullOrWhiteSpace(request.command))
            {
                TraceAutomation("request payload is invalid");
                TryDeleteRequestFile();
                WriteStatus(new StatusEnvelope
                {
                    request_id = request != null ? request.request_id ?? string.Empty : string.Empty,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = request != null ? request.command ?? string.Empty : string.Empty,
                    message = "request payload is invalid",
                    passed = false,
                    failures = new[] { "request payload is invalid" }
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(request.request_id))
            {
                request.request_id = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(request.requested_command))
            {
                request.requested_command = request.command;
            }

            PersistRequest(request);
            TraceAutomation($"loaded request id={request.request_id} command={request.command} requested={request.requested_command}");

            if (TryBootstrapCleanAutomationRequest(request))
            {
                return;
            }

            if (!TryStartAutomationRequest(request, out string startMessage))
            {
                TraceAutomation($"request start failed id={request.request_id} command={request.command} requested={request.requested_command} message={startMessage}");
                WriteStatus(new StatusEnvelope
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = request.requested_command ?? request.command,
                    message = startMessage,
                    passed = false,
                    failures = new[] { startMessage }
                });
                TryDeleteRequestFile();
                return;
            }

            _activeAutomationRequestId = request.request_id;
            _activeAutomationCommand = request.command;
            _activeAutomationRequestedCommand = request.requested_command ?? request.command;
            TraceAutomation($"started request id={request.request_id} command={request.command} requested={_activeAutomationRequestedCommand}");
            WriteStatus(new StatusEnvelope
            {
                request_id = request.request_id,
                status = "running",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                command = _activeAutomationRequestedCommand,
                message = $"started command={request.command}",
                passed = false,
                failures = Array.Empty<string>()
            });
        }

        private static bool TryStartAutomationRequest(RequestEnvelope request, out string message)
        {
            message = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.command))
            {
                message = "request command is missing";
                return false;
            }

            if (IsBatchRunning() || _singleFileManager != null)
            {
                message = "smoke runner is already active";
                return false;
            }

            switch (request.command)
            {
                case CaptureSatisfactionThumbEvidenceCommand:
                    return TryStartAutomationSingleSmoke(
                        "satisfaction_2.fbx",
                        ThumbEvidenceDurationSeconds,
                        true,
                        ThumbEvidenceSampleTimes,
                        "thumb-evidence",
                        FileManager.EditorDiagnosticSmokeSegment.Head,
                        out message);
                case CaptureSatisfactionFullRegressionEvidenceCommand:
                    return TryStartAutomationSingleSmoke(
                        "satisfaction_2.fbx",
                        SatisfactionFullRegressionEvidenceDurationSeconds,
                        true,
                        SatisfactionFullRegressionEvidenceSampleTimes,
                        "full-regression-evidence",
                        FileManager.EditorDiagnosticSmokeSegment.Head,
                        out message,
                        FullRegressionEvidenceCaptureWidth,
                        FullRegressionEvidenceCaptureHeight);
                case CaptureAntennaTailHelperEvidenceCommand:
                    return TryStartAutomationSingleSmoke(
                        "Antenna39 try_006 g.fbx",
                        SmokeDurationSeconds,
                        true,
                        new[] { AntennaTailHelperEvidenceTimeSeconds },
                        "helper-evidence-tail",
                        FileManager.EditorDiagnosticSmokeSegment.Tail,
                        out message);
                case RunAllImportFbxHeadCommand:
                    return TryStartAutomationBatch(FileManager.EditorDiagnosticSmokeSegment.Head, out message);
                case RunAllImportFbxMiddleCommand:
                    return TryStartAutomationBatch(FileManager.EditorDiagnosticSmokeSegment.Middle, out message);
                case RunAllImportFbxTailCommand:
                    return TryStartAutomationBatch(FileManager.EditorDiagnosticSmokeSegment.Tail, out message);
                default:
                    message = $"unsupported command: {request.command}";
                    return false;
            }
        }

        private static bool TryBootstrapCleanAutomationRequest(RequestEnvelope request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.command))
            {
                return false;
            }

            if (string.Equals(request.command, CaptureAntennaTailHelperEvidenceCleanCommand, StringComparison.Ordinal))
            {
                request.command = CaptureAntennaTailHelperEvidenceResumeAfterCleanCommand;
                PersistRequest(request);
                TraceAutomation(
                    $"clean bootstrap request id={request.request_id} requested={request.requested_command} action=" +
                    (EditorApplication.isPlaying ? "restart-playmode" : "enter-playmode"));
                WriteStatus(new StatusEnvelope
                {
                    request_id = request.request_id ?? string.Empty,
                    status = "running",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = CaptureAntennaTailHelperEvidenceCleanCommand,
                    message = EditorApplication.isPlaying
                        ? "restarting play mode for clean smoke"
                        : "entering play mode for clean smoke",
                    passed = false,
                    failures = Array.Empty<string>()
                });
                EditorApplication.isPlaying = !EditorApplication.isPlaying;
                return true;
            }

            if (!string.Equals(request.command, CaptureAntennaTailHelperEvidenceResumeAfterCleanCommand, StringComparison.Ordinal))
            {
                return false;
            }

            if (!EditorApplication.isPlaying)
            {
                TraceAutomation($"clean bootstrap resume request id={request.request_id} requested={request.requested_command} action=enter-playmode");
                WriteStatus(new StatusEnvelope
                {
                    request_id = request.request_id ?? string.Empty,
                    status = "running",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = CaptureAntennaTailHelperEvidenceCleanCommand,
                    message = "entering play mode for clean smoke",
                    passed = false,
                    failures = Array.Empty<string>()
                });
                EditorApplication.isPlaying = true;
                return true;
            }

            request.command = CaptureAntennaTailHelperEvidenceCommand;
            PersistRequest(request);
            TraceAutomation($"clean bootstrap resume request id={request.request_id} requested={request.requested_command} action=run-base-command");
            return false;
        }

        private static bool TryStartAutomationSingleSmoke(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            float[] sampleTimesOverride,
            string mode,
            FileManager.EditorDiagnosticSmokeSegment segment,
            out string message,
            int captureWidthOverride = 0,
            int captureHeightOverride = 0)
        {
            message = string.Empty;
            if (!TryGetFileManager(fbxFileName, out FileManager fileManager, interactive: false, out message))
            {
                return false;
            }

            if (!StartSmoke(fileManager, fbxFileName, mode, segment, durationSeconds, enableFingerCloseups, sampleTimesOverride, captureWidthOverride, captureHeightOverride))
            {
                message = $"smoke start failed: {fbxFileName}";
                return false;
            }

            TrackSingleSmoke(fileManager, fbxFileName, mode);
            return true;
        }

        private static bool TryStartAutomationBatch(FileManager.EditorDiagnosticSmokeSegment segment, out string message)
        {
            message = string.Empty;
            if (!TryGetFileManager(null, out FileManager fileManager, interactive: false, out message))
            {
                return false;
            }

            string[] fbxFileNames = GetImportFbxFileNames();
            if (fbxFileNames.Length == 0)
            {
                message = "Import_FBX directory is empty";
                return false;
            }

            StartSmokeBatch(fileManager, fbxFileNames, segment);
            return true;
        }

        private static void RunSingleSmoke(
            string fbxFileName,
            float durationSeconds = SmokeDurationSeconds,
            bool enableFingerCloseups = false,
            float[] sampleTimesOverride = null,
            string mode = "single",
            FileManager.EditorDiagnosticSmokeSegment segment = FileManager.EditorDiagnosticSmokeSegment.Head,
            int captureWidthOverride = 0,
            int captureHeightOverride = 0)
        {
            if (IsBatchRunning())
            {
                EditorUtility.DisplayDialog("FBX Smoke", "전체 smoke 배치가 진행 중입니다. 완료 후 다시 실행하세요.", "확인");
                return;
            }

            if (!TryGetFileManager(fbxFileName, out FileManager fileManager))
            {
                return;
            }

            if (StartSmoke(fileManager, fbxFileName, mode, segment, durationSeconds, enableFingerCloseups, sampleTimesOverride, captureWidthOverride, captureHeightOverride))
            {
                TrackSingleSmoke(fileManager, fbxFileName, mode);
            }
        }

        private static void StartSmokeBatch(FileManager fileManager, IEnumerable<string> fbxFileNames, FileManager.EditorDiagnosticSmokeSegment segment)
        {
            if (IsBatchRunning())
            {
                EditorUtility.DisplayDialog("FBX Smoke", "이미 전체 smoke 배치가 진행 중입니다.", "확인");
                return;
            }

            PendingSmokeFiles.Clear();
            BatchSuccesses.Clear();
            BatchFailures.Clear();

            foreach (string fbxFileName in fbxFileNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                PendingSmokeFiles.Enqueue(fbxFileName);
            }

            _batchFileManager = fileManager;
            _batchTotalCount = PendingSmokeFiles.Count;
            _activeBatchFbxFileName = null;
            _batchSegment = segment;

            _batchFileManager.EditorDiagnosticSmokeFinished -= HandleBatchSmokeFinished;
            _batchFileManager.EditorDiagnosticSmokeFinished += HandleBatchSmokeFinished;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

            string segmentLabel = GetSegmentLabel(segment);
            Debug.Log($"[FbxPlaybackSmokeRunner] 전체 Import_FBX smoke 시작: segment={segmentLabel}, {_batchTotalCount} files, {SmokeDurationSeconds:F0}s cap");
            StartNextBatchSmoke();
        }

        private static void StartNextBatchSmoke()
        {
            if (!IsBatchRunning())
            {
                return;
            }

            if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
            {
                FinishBatch("Play Mode가 중단되어 전체 smoke 배치를 종료했습니다.", forceFailure: true);
                return;
            }

            if (_batchFileManager == null)
            {
                FinishBatch("FileManager 참조가 없어 전체 smoke 배치를 종료했습니다.", forceFailure: true);
                return;
            }

            if (_batchFileManager.IsProcessing)
            {
                EditorApplication.delayCall += StartNextBatchSmoke;
                return;
            }

            if (PendingSmokeFiles.Count == 0)
            {
                FinishBatch("전체 Import_FBX smoke 배치 완료", forceFailure: false);
                return;
            }

            _activeBatchFbxFileName = PendingSmokeFiles.Dequeue();
            int currentIndex = _batchTotalCount - PendingSmokeFiles.Count;
            string segmentLabel = GetSegmentLabel(_batchSegment);
            Debug.Log($"[FbxPlaybackSmokeRunner] 전체 smoke 진행 {currentIndex}/{_batchTotalCount}: segment={segmentLabel}, {_activeBatchFbxFileName}");

            if (!StartSmoke(_batchFileManager, _activeBatchFbxFileName, "batch", _batchSegment, SmokeDurationSeconds, enableFingerCloseups: false, sampleTimesOverride: null))
            {
                BatchFailures.Add($"{_activeBatchFbxFileName}: start failed");
                _activeBatchFbxFileName = null;
                EditorApplication.delayCall += StartNextBatchSmoke;
            }
        }

        private static bool StartSmoke(
            FileManager fileManager,
            string fbxFileName,
            string mode,
            FileManager.EditorDiagnosticSmokeSegment segment = FileManager.EditorDiagnosticSmokeSegment.Head,
            float durationSeconds = SmokeDurationSeconds,
            bool enableFingerCloseups = false,
            float[] sampleTimesOverride = null,
            int captureWidthOverride = 0,
            int captureHeightOverride = 0)
        {
            if (fileManager == null)
            {
                Debug.LogError("[FbxPlaybackSmokeRunner] FileManager가 없어 smoke를 시작하지 못했습니다.");
                return false;
            }

            if (fileManager.IsProcessing)
            {
                Debug.LogWarning($"[FbxPlaybackSmokeRunner] FileManager가 처리 중이라 smoke를 시작하지 않았습니다: {fbxFileName}");
                return false;
            }

            float safeDuration = Mathf.Max(0.1f, durationSeconds);
            int targetFrameCount = Mathf.CeilToInt(safeDuration * SmokeFrameRate);
            bool started = fileManager.StartEditorDiagnosticSmoke(
                fbxFileName,
                safeDuration,
                targetFrameCount,
                enableDiagnostics: true,
                enableFingerCloseups: enableFingerCloseups,
                useDeterministicCaptureFramerate: true,
                diagnosticStartDelay: SmokeStartDelaySeconds,
                segment: segment,
                sampleTimesOverride: sampleTimesOverride,
                captureWidthOverride: captureWidthOverride,
                captureHeightOverride: captureHeightOverride);

            if (started)
            {
                string segmentLabel = GetSegmentLabel(segment);
                string sampleSummary = sampleTimesOverride != null && sampleTimesOverride.Length > 0
                    ? string.Join("/", sampleTimesOverride.Select(time => time.ToString("0.###")))
                    : "default";
                Debug.Log($"[FbxPlaybackSmokeRunner] {fbxFileName} smoke 시작: mode={mode}, segment={segmentLabel}, {safeDuration:F1}s, {targetFrameCount} frames, fingerCloseups={enableFingerCloseups}, samples={sampleSummary}");
            }

            return started;
        }

        private static string GetFullRegressionEvidenceCommandForTest()
        {
            return CaptureSatisfactionFullRegressionEvidenceCommand;
        }

        private static float GetFullRegressionEvidenceDurationSecondsForTest()
        {
            return SatisfactionFullRegressionEvidenceDurationSeconds;
        }

        private static string GetFullRegressionEvidenceFbxFileNameForTest()
        {
            return SatisfactionFbxFileName;
        }

        private static int[] GetFullRegressionEvidenceCaptureResolutionForTest()
        {
            return new[] { FullRegressionEvidenceCaptureWidth, FullRegressionEvidenceCaptureHeight };
        }

        private static float[] GetFullRegressionEvidenceSampleTimesForTest()
        {
            return (float[])SatisfactionFullRegressionEvidenceSampleTimes.Clone();
        }

        private static void TrackSingleSmoke(FileManager fileManager, string fbxFileName, string mode)
        {
            ClearSingleSmokeTracking();
            _singleFileManager = fileManager;
            _activeSingleFbxFileName = fbxFileName;
            _singleSmokeMode = mode;
            _singleFileManager.EditorDiagnosticSmokeFinished += HandleSingleSmokeFinished;
        }

        private static void HandleSingleSmokeFinished(string fbxFileName, VmdSaveResult result)
        {
            if (_singleFileManager == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_activeSingleFbxFileName) &&
                !string.Equals(_activeSingleFbxFileName, fbxFileName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string resultLabel = result.Success
                ? $"{fbxFileName}: {result.FrameCount} frames, {result.FileSizeBytes} bytes"
                : $"{fbxFileName}: {result.ErrorMessage}";
            string evidenceSummary = BuildSingleSmokeEvidenceSummary(_singleFileManager);

            if (result.Success)
            {
                Debug.Log($"[FbxPlaybackSmokeRunner] single smoke 성공: mode={_singleSmokeMode}, {resultLabel}{evidenceSummary}");
            }
            else
            {
                Debug.LogError($"[FbxPlaybackSmokeRunner] single smoke 실패: mode={_singleSmokeMode}, {resultLabel}{evidenceSummary}");
            }

            if (!string.IsNullOrEmpty(_activeAutomationRequestId))
            {
                CompleteAutomationRequest(
                    result.Success,
                    result.Success ? "single smoke completed" : result.ErrorMessage,
                    GetLastManifestPath(_singleFileManager),
                    result.Success ? Array.Empty<string>() : new[] { result.ErrorMessage },
                    totalJobs: 1,
                    successJobs: result.Success ? 1 : 0);
            }

            ClearSingleSmokeTracking();
        }

        private static string BuildSingleSmokeEvidenceSummary(FileManager fileManager)
        {
            if (fileManager == null || fileManager.targetCharacter == null)
            {
                return string.Empty;
            }

            MotionComparisonProbe probe = fileManager.targetCharacter.GetComponent<MotionComparisonProbe>();
            if (probe == null)
            {
                return string.Empty;
            }

            List<string> details = new List<string>();
            if (!string.IsNullOrEmpty(probe.LastSessionManifestPath))
            {
                details.Add($"manifest={probe.LastSessionManifestPath}");
            }

            if (!string.IsNullOrEmpty(probe.LastScreenshotFolder))
            {
                details.Add($"folder={probe.LastScreenshotFolder}");
                string leftFront = FindRepresentativeScreenshot(probe.LastScreenshotFolder, "*left-hand-front*.png");
                if (!string.IsNullOrEmpty(leftFront))
                {
                    details.Add($"leftFront={leftFront}");
                }

                string rightFront = FindRepresentativeScreenshot(probe.LastScreenshotFolder, "*right-hand-front*.png");
                if (!string.IsNullOrEmpty(rightFront))
                {
                    details.Add($"rightFront={rightFront}");
                }
            }

            return details.Count > 0 ? $", evidence[{string.Join(", ", details)}]" : string.Empty;
        }

        private static string FindRepresentativeScreenshot(string folderPath, string pattern)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                return string.Empty;
            }

            string[] matches = Directory.GetFiles(folderPath, pattern, SearchOption.TopDirectoryOnly);
            if (matches == null || matches.Length == 0)
            {
                matches = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly);
            }

            return matches != null && matches.Length > 0
                ? matches.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? string.Empty
                : string.Empty;
        }

        private static void HandleBatchSmokeFinished(string fbxFileName, VmdSaveResult result)
        {
            if (!IsBatchRunning())
            {
                return;
            }

            string resultLabel = result.Success
                ? $"{fbxFileName}: {result.FrameCount} frames, {result.FileSizeBytes} bytes"
                : $"{fbxFileName}: {result.ErrorMessage}";

            if (result.Success)
            {
                BatchSuccesses.Add(resultLabel);
                Debug.Log($"[FbxPlaybackSmokeRunner] smoke 성공: {resultLabel}");
            }
            else
            {
                BatchFailures.Add(resultLabel);
                Debug.LogError($"[FbxPlaybackSmokeRunner] smoke 실패: {resultLabel}");
            }

            _activeBatchFbxFileName = null;
            if (_batchFileManager != null)
            {
                _batchFileManager.ScheduleEditorDiagnosticBatchAdvance(StartNextBatchSmoke);
            }
            else
            {
                EditorApplication.delayCall += StartNextBatchSmoke;
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!IsBatchRunning())
            {
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                FinishBatch("Play Mode 종료로 전체 smoke 배치를 중단했습니다.", forceFailure: true);
            }
        }

        private static void FinishBatch(string message, bool forceFailure)
        {
            if (_batchFileManager != null)
            {
                _batchFileManager.EditorDiagnosticSmokeFinished -= HandleBatchSmokeFinished;
            }

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;

            if (forceFailure && !string.IsNullOrEmpty(_activeBatchFbxFileName))
            {
                BatchFailures.Add($"{_activeBatchFbxFileName}: {message}");
            }

            string successSummary = BatchSuccesses.Count == 0 ? "없음" : string.Join("; ", BatchSuccesses);
            string failureSummary = BatchFailures.Count == 0 ? "없음" : string.Join("; ", BatchFailures);
            Debug.Log(
                $"[FbxPlaybackSmokeRunner] {message}. " +
                $"success={BatchSuccesses.Count}/{_batchTotalCount}, fail={BatchFailures.Count}. " +
                $"successes=[{successSummary}], failures=[{failureSummary}]");

            if (!string.IsNullOrEmpty(_activeAutomationRequestId))
            {
                CompleteAutomationRequest(
                    !forceFailure && BatchFailures.Count == 0,
                    message,
                    string.Empty,
                    BatchFailures.ToArray(),
                    _batchTotalCount,
                    BatchSuccesses.Count);
            }

            PendingSmokeFiles.Clear();
            BatchSuccesses.Clear();
            BatchFailures.Clear();
            _batchFileManager = null;
            _activeBatchFbxFileName = null;
            _batchTotalCount = 0;
            _batchSegment = FileManager.EditorDiagnosticSmokeSegment.Head;
        }

        private static void ClearSingleSmokeTracking()
        {
            if (_singleFileManager != null)
            {
                _singleFileManager.EditorDiagnosticSmokeFinished -= HandleSingleSmokeFinished;
            }

            _singleFileManager = null;
            _activeSingleFbxFileName = null;
            _singleSmokeMode = null;
        }

        private static bool IsBatchRunning()
        {
            return _batchFileManager != null;
        }

        private static string GetLastManifestPath(FileManager fileManager)
        {
            if (fileManager == null || fileManager.targetCharacter == null)
            {
                return string.Empty;
            }

            MotionComparisonProbe probe = fileManager.targetCharacter.GetComponent<MotionComparisonProbe>();
            return probe != null ? probe.LastSessionManifestPath ?? string.Empty : string.Empty;
        }

        private static void CompleteAutomationRequest(
            bool passed,
            string message,
            string manifestPath,
            string[] failures,
            int totalJobs,
            int successJobs)
        {
            WriteStatus(new StatusEnvelope
            {
                request_id = _activeAutomationRequestId ?? string.Empty,
                status = passed ? "completed" : "failed",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                command = _activeAutomationRequestedCommand ?? _activeAutomationCommand ?? string.Empty,
                message = message ?? string.Empty,
                passed = passed,
                manifest_path = manifestPath ?? string.Empty,
                total_jobs = totalJobs,
                success_jobs = successJobs,
                failures = failures ?? Array.Empty<string>()
            });
            TraceAutomation(
                $"completed request id={_activeAutomationRequestId} requested={_activeAutomationRequestedCommand} " +
                $"command={_activeAutomationCommand} passed={passed} message={message}");

            ClearAutomationRequestState();
            TryDeleteRequestFile();
        }

        private static void ClearAutomationRequestState()
        {
            _activeAutomationRequestId = null;
            _activeAutomationCommand = null;
            _activeAutomationRequestedCommand = null;
        }

        private static void WriteStatus(StatusEnvelope status)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatusPath) ?? ProjectRoot);
            File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
        }

        private static void TraceAutomation(string message)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(TracePath) ?? ProjectRoot);
                File.AppendAllText(
                    TracePath,
                    $"{DateTime.Now.ToString("o", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
            }
            catch
            {
            }
        }

        private static void PersistRequest(RequestEnvelope request)
        {
            if (request == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath) ?? ProjectRoot);
            File.WriteAllText(RequestPath, JsonUtility.ToJson(request, true));
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
            catch
            {
            }
        }

        private static string GetSegmentLabel(FileManager.EditorDiagnosticSmokeSegment segment)
        {
            switch (segment)
            {
                case FileManager.EditorDiagnosticSmokeSegment.Middle:
                    return "middle";
                case FileManager.EditorDiagnosticSmokeSegment.Tail:
                    return "tail";
                default:
                    return "head";
            }
        }

        private static bool TryGetFileManager(string fbxFileName, out FileManager fileManager)
        {
            return TryGetFileManager(fbxFileName, out fileManager, interactive: true, out _);
        }

        private static bool TryGetFileManager(string fbxFileName, out FileManager fileManager, bool interactive, out string errorMessage)
        {
            fileManager = null;
            errorMessage = string.Empty;
            if (!ValidateRuntimeContext(fbxFileName, interactive, out errorMessage))
            {
                return false;
            }

            fileManager = FindRuntimeFileManager();
            if (fileManager != null)
            {
                return true;
            }

            errorMessage = "현재 Play Mode 씬에서 FileManager를 찾지 못했습니다.";
            Debug.LogWarning($"[FbxPlaybackSmokeRunner] FileManager 탐색 실패: {BuildMainAutoRuntimeSummary()}, thumbReference[{BuildRetargeterThumbReferenceSummary(null)}]");
            if (interactive)
            {
                EditorUtility.DisplayDialog("FBX Smoke", $"{errorMessage} 콘솔 로그를 확인하세요.", "확인");
            }

            return false;
        }

        private static bool ValidateRuntimeContext(string fbxFileName, bool interactive, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                errorMessage = "Main_Auto 씬을 Play Mode로 실행한 뒤 smoke 메뉴를 사용하세요.";
                if (interactive)
                {
                    EditorUtility.DisplayDialog("FBX Smoke", errorMessage, "확인");
                }

                return false;
            }

            if (EditorApplication.isCompiling)
            {
                errorMessage = "Unity 컴파일이 끝난 뒤 다시 실행하세요.";
                if (interactive)
                {
                    EditorUtility.DisplayDialog("FBX Smoke", errorMessage, "확인");
                }

                return false;
            }

            if (!IsSupportedMainScene(EditorSceneManager.GetActiveScene().name))
            {
                errorMessage = "활성 씬이 Main_Auto 또는 Main_recoding이 아닙니다.";
                if (interactive)
                {
                    EditorUtility.DisplayDialog("FBX Smoke", errorMessage, "확인");
                }

                return false;
            }

            if (string.IsNullOrWhiteSpace(fbxFileName))
            {
                return true;
            }

            string fbxPath = Path.Combine(GetImportFbxDirectory(), fbxFileName);
            if (!File.Exists(fbxPath))
            {
                errorMessage = $"FBX 파일을 찾지 못했습니다.\n{fbxPath}";
                if (interactive)
                {
                    EditorUtility.DisplayDialog("FBX Smoke", errorMessage, "확인");
                }

                return false;
            }

            return true;
        }

        private static bool IsSupportedMainScene(string sceneName)
        {
            return string.Equals(sceneName, MainAutoSceneName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sceneName, MainRecordingSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] GetImportFbxFileNames()
        {
            string directory = GetImportFbxDirectory();
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(directory, "*.fbx", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string GetImportFbxDirectory()
        {
            return Path.Combine(Application.dataPath, ImportFbxRelativeDirectory);
        }

        private static FileManager FindRuntimeFileManager()
        {
            FileManager directMatch = UnityEngine.Object.FindObjectOfType<FileManager>();
            if (directMatch != null)
            {
                return directMatch;
            }

            Scene runtimeScene = SceneManager.GetActiveScene();
            if (runtimeScene.IsValid())
            {
                foreach (GameObject rootObject in runtimeScene.GetRootGameObjects())
                {
                    if (rootObject == null)
                    {
                        continue;
                    }

                    FileManager sceneMatch = rootObject.GetComponentInChildren<FileManager>(true);
                    if (sceneMatch != null)
                    {
                        return sceneMatch;
                    }
                }
            }

            return Resources.FindObjectsOfTypeAll<FileManager>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    string.Equals(candidate.gameObject.scene.name, runtimeScene.name, StringComparison.Ordinal));
        }

        private static string BuildMainAutoRuntimeSummary()
        {
            Scene editorScene = EditorSceneManager.GetActiveScene();
            Scene runtimeScene = SceneManager.GetActiveScene();
            FileManager directMatch = UnityEngine.Object.FindObjectOfType<FileManager>();
            FileManager[] allFileManagers = Resources.FindObjectsOfTypeAll<FileManager>();
            int runtimeSceneFileManagerCount = allFileManagers.Count(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                string.Equals(candidate.gameObject.scene.name, runtimeScene.name, StringComparison.Ordinal));

            return
                $"editorScene[{SummarizeScene(editorScene)}], runtimeScene[{SummarizeScene(runtimeScene)}], " +
                $"editorFileManagerRoot[{SummarizeNamedRoot(editorScene, "FileManager")}], " +
                $"runtimeFileManagerRoot[{SummarizeNamedRoot(runtimeScene, "FileManager")}], " +
                $"findObjectOfType={(directMatch != null ? GetHierarchyPath(directMatch.transform) : "<none>")}, " +
                $"allFileManagers={allFileManagers.Length}, runtimeSceneFileManagers={runtimeSceneFileManagerCount}, " +
                $"playing={EditorApplication.isPlaying}, paused={EditorApplication.isPaused}, compiling={EditorApplication.isCompiling}";
        }

        private static string SummarizeScene(Scene scene)
        {
            GameObject[] rootObjects = scene.IsValid() ? scene.GetRootGameObjects() : Array.Empty<GameObject>();
            string rootSummary = rootObjects.Length == 0
                ? "<none>"
                : string.Join(", ", rootObjects.Take(8).Select(root => root != null ? root.name : "<null>"));
            return $"name={scene.name}, path={scene.path}, rootCount={rootObjects.Length}, roots=[{rootSummary}]";
        }

        private static string SummarizeNamedRoot(Scene scene, string rootName)
        {
            if (!scene.IsValid())
            {
                return "<invalid-scene>";
            }

            GameObject rootObject = scene.GetRootGameObjects()
                .FirstOrDefault(candidate => candidate != null && string.Equals(candidate.name, rootName, StringComparison.Ordinal));
            if (rootObject == null)
            {
                return "<not-found>";
            }

            Component[] directComponents = rootObject.GetComponents<Component>();
            Component[] childComponents = rootObject.GetComponentsInChildren<Component>(true);
            int directMissingCount = directComponents.Count(component => component == null);
            int childMissingCount = childComponents.Count(component => component == null);
            int childFileManagerCount = rootObject.GetComponentsInChildren<FileManager>(true).Length;

            return
                $"path={GetHierarchyPath(rootObject.transform)}, direct=[{SummarizeComponents(directComponents)}], " +
                $"directMissing={directMissingCount}, childCount={childComponents.Length}, childMissing={childMissingCount}, " +
                $"childFileManagers={childFileManagerCount}, childSample=[{SummarizeComponents(childComponents, 16)}]";
        }

        private static string SummarizeComponents(Component[] components, int maxCount = 12)
        {
            if (components == null || components.Length == 0)
            {
                return "<none>";
            }

            IEnumerable<string> labels = components
                .Take(maxCount)
                .Select(component => component == null ? "<missing>" : component.GetType().FullName);
            string summary = string.Join(", ", labels);
            if (components.Length > maxCount)
            {
                summary += $", ...(+{components.Length - maxCount})";
            }

            return summary;
        }

        private static string BuildRetargeterThumbReferenceSummary(FileManager runtimeFileManager)
        {
            Component[] retargeters = UnityEngine.Object.FindObjectsOfType<Component>()
                .Where(component => component != null && component.GetType().Name == "PoseSpaceRetargeter")
                .ToArray();
            if (retargeters.Length == 0)
            {
                return "retargeters=<none>";
            }

            Animator fileManagerTargetAnimator = runtimeFileManager != null && runtimeFileManager.targetCharacter != null
                ? runtimeFileManager.targetCharacter.GetComponent<Animator>()
                : null;
            IEnumerable<string> labels = retargeters
                .Take(4)
                .Select(retargeter => SummarizeRetargeterThumbReference(retargeter, fileManagerTargetAnimator));
            string summary = string.Join("; ", labels);
            if (retargeters.Length > 4)
            {
                summary += $"; ...(+{retargeters.Length - 4})";
            }

            return $"count={retargeters.Length}, fileManagerTargetAnimator={(fileManagerTargetAnimator != null ? GetHierarchyPath(fileManagerTargetAnimator.transform) : "<none>")}, retargeters=[{summary}]";
        }

        private static string SummarizeRetargeterThumbReference(Component retargeter, Animator fileManagerTargetAnimator)
        {
            Type retargeterType = retargeter.GetType();
            Animator targetAnimator = ReadMemberValue(retargeterType, retargeter, "targetAnimator") as Animator;
            Animator referenceAnimator = ReadMemberValue(retargeterType, retargeter, "_editorFingerReferenceAnimator") as Animator;
            bool fileManagerTargetMatch = targetAnimator != null && targetAnimator == fileManagerTargetAnimator;

            return
                $"go={GetHierarchyPath(retargeter.transform)}, " +
                $"targetAnimator={(targetAnimator != null ? GetHierarchyPath(targetAnimator.transform) : "<none>")}, " +
                $"fileManagerMatch={fileManagerTargetMatch}, " +
                $"manualFingerConfig={ReadBoolMember(retargeterType, retargeter, "useManualAnimatorFingerPoseReference")}, " +
                $"thumbLocalRefConfig={ReadBoolMember(retargeterType, retargeter, "useManualAnimatorThumbLocalRotationReference")}, " +
                $"preserveThumbMuscles={ReadBoolMember(retargeterType, retargeter, "preserveManualFingerReferenceThumbMuscles")}, " +
                $"editorFingerRuntime={ReadBoolMember(retargeterType, retargeter, "_useEditorFingerPoseReference")}, " +
                $"referenceAnimator={(referenceAnimator != null ? GetHierarchyPath(referenceAnimator.transform) : "<none>")}, " +
                $"manualThumbActive={ReadBoolMember(retargeterType, retargeter, "IsManualThumbLocalRotationReferenceActive")}, " +
                $"suppressLeft={ReadBoolMember(retargeterType, retargeter, "ShouldSuppressLeftThumbPoseShapingGuard")}, " +
                $"suppressRight={ReadBoolMember(retargeterType, retargeter, "ShouldSuppressRightThumbPoseShapingGuard")}";
        }

        private static object ReadMemberValue(Type type, object instance, string memberName)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(memberName, Flags);
            if (property != null)
            {
                return property.GetValue(instance);
            }

            FieldInfo field = type.GetField(memberName, Flags);
            return field != null ? field.GetValue(instance) : null;
        }

        private static string ReadBoolMember(Type type, object instance, string memberName)
        {
            object value = ReadMemberValue(type, instance, memberName);
            return value is bool boolValue ? boolValue.ToString() : "n/a";
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            List<string> parts = new List<string>();
            Transform current = target;
            while (current != null)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
#endif
