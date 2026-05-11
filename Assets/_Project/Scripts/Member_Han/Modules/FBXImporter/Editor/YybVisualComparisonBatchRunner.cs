#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Member_Han.Modules.FBXImporter.EditorTools
{
    [InitializeOnLoad]
    public static class YybVisualComparisonBatchRunner
    {
        private const string MenuRoot = "Machine Spirit/YYB Compare/";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string SubManualScenePath = "Assets/_Project/Scene/Sub_Manual.unity";
        private const string DefaultFbxFileName = "satisfaction_2.fbx";
        private const string ManualControllerPath = "Assets/_ManualReference/SampleAnimation/TestAnimator1_Manual.controller";
        private const string FallbackControllerPath = "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller";
        private const string ProjectFbxDirectory = "Assets/_Project/FBX";
        private const string ImportFbxDirectory = "Assets/Resources/Import_FBX";
        private const string OutputRootDirectory = "Docs/Machine_Spirit/Local/ComparisonSessions";
        private const string LatestSummaryJsonRelativePath = "Docs/Machine_Spirit/Local/progress/evidence/yyb_visual_compare_latest.json";
        private const string LatestSummaryMarkdownRelativePath = "Docs/Machine_Spirit/Local/progress/evidence/yyb_visual_compare_latest.md";
        private const string SummaryJsonFileName = "yyb_visual_compare_summary.json";
        private const string SummaryMarkdownFileName = "yyb_visual_compare_summary.md";
        private const string RunnerTraceRelativePath = "Docs/Machine_Spirit/Local/runtime/yyb_visual_compare_runner_trace.log";
        private static readonly string[] RuntimeDiagnosticScriptPaths =
        {
            "Assets/Plugins/VMDRecorderSample/SampleScript/MotionComparisonProbe.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/FileManager.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/HumanoidArmDeformationGuard.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/PoseSpaceRetargeter.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/Editor/YybVisualComparisonBatchRunner.cs",
            "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/Editor/YybVisualComparisonRequestWatcher.cs"
        };
        private const float DefaultDurationSeconds = 31f;
        private const float DefaultFrameRate = 30f;
        private const float DefaultStartDelaySeconds = 0.2f;
        private const double PlayModeEntryTimeoutSeconds = 15d;
        private const string RunnerStateSessionKey = "Member_Han.YybVisualComparison.RunnerStateJson";
        private const string ManualTestPrefabNameToken = "testPrefab";
        private const string ManualYybNameToken = "YYB Hatsune Miku_default_1.0ver";
        private const string ManualTestPrefabLabelSuffix = "testPrefab";
        private const string ManualYybLabelSuffix = "yyb";

        private enum CaptureMode
        {
            MainAuto,
            SubManualTestPrefab,
            SubManualYyb
        }

        private sealed class CaptureJob
        {
            public CaptureMode Mode;
            public string ScenePath;
            public string SceneName;
            public string DisplayName;
            public string ManualTargetNameToken;
        }

        [Serializable]
        private sealed class CaptureResult
        {
            public string jobMode;
            public string jobDisplayName;
            public string sceneName;
            public string comparisonLabel;
            public string targetName;
            public bool success;
            public string error;
            public string vmdPath;
            public int frameCount;
            public long fileSizeBytes;
            public string comparisonSessionManifestPath;
            public string comparisonMetricsCsvPath;
            public string comparisonFrameFolderPath;
            public string comparisonFrameIndexPath;
            public string comparisonSessionId;
        }

        [Serializable]
        private sealed class PersistedCaptureJob
        {
            public int mode;
            public string scenePath;
            public string sceneName;
            public string displayName;
            public string manualTargetNameToken;
        }

        [Serializable]
        private sealed class PersistedCaptureResult
        {
            public string jobMode;
            public string jobDisplayName;
            public string sceneName;
            public string comparisonLabel;
            public string targetName;
            public bool success;
            public string error;
            public string vmdPath;
            public int frameCount;
            public long fileSizeBytes;
            public string comparisonSessionManifestPath;
            public string comparisonMetricsCsvPath;
            public string comparisonFrameFolderPath;
            public string comparisonFrameIndexPath;
            public string comparisonSessionId;
        }

        [Serializable]
        private sealed class PersistedState
        {
            public string fbxFileName;
            public float durationSeconds;
            public int targetFrameCount;
            public bool enableFingerCloseups;
            public bool isRunning;
            public bool activeJobFinished;
            public bool advanceAfterPlayStopPending;
            public bool playModeEntryPending;
            public string summarySessionId;
            public string summaryDirectory;
            public string projectRoot;
            public PersistedCaptureJob activeJob;
            public PersistedCaptureJob[] pendingJobs;
            public PersistedCaptureResult[] results;
            public string[] failures;
        }

        private static readonly Queue<CaptureJob> PendingJobs = new Queue<CaptureJob>();
        private static readonly List<CaptureResult> Results = new List<CaptureResult>();
        private static readonly List<string> Failures = new List<string>();

        private static CaptureJob _activeJob;
        private static FileManager _activeFileManager;
        private static HumanoidSampleCode _activeRecorder;
        private static AnimationClip _referenceClip;
        private static RuntimeAnimatorController _fallbackController;
        private static string _fbxFileName = DefaultFbxFileName;
        private static float _durationSeconds = DefaultDurationSeconds;
        private static int _targetFrameCount = Mathf.CeilToInt(DefaultDurationSeconds * DefaultFrameRate);
        private static bool _enableFingerCloseups;
        private static bool _isRunning;
        private static bool _activeJobFinished;
        private static bool _activeJobStartedInPlayMode;
        private static bool _advanceAfterPlayStopPending;
        private static bool _playModeEntryPending;
        private static double _playModeEntryRequestedAt;
        private static string _summarySessionId = string.Empty;
        private static string _summaryDirectory = string.Empty;
        private static string _projectRoot = string.Empty;
        private static bool _enterPlayModeOptionsCaptured;
        private static bool _previousEnterPlayModeOptionsEnabled;
        private static EnterPlayModeOptions _previousEnterPlayModeOptions;

        public sealed class RunCompletionInfo
        {
            public bool passed;
            public string sessionId;
            public string summaryJsonPath;
            public string summaryMarkdownPath;
            public string latestSummaryJsonPath;
            public string latestSummaryMarkdownPath;
            public string[] failures;
            public int totalJobs;
            public int successJobs;
        }

        public static bool IsRunning => _isRunning;
        public static bool HasPersistedRunState()
        {
            string json = SessionState.GetString(RunnerStateSessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            PersistedState state = JsonUtility.FromJson<PersistedState>(json);
            if (state == null || !state.isRunning)
            {
                ClearPersistedState();
                return false;
            }

            return true;
        }

        public static bool TryResumePersistedRun()
        {
            if (_isRunning)
            {
                return true;
            }

            if (!HasPersistedRunState())
            {
                return false;
            }

            TryResumeRunAfterDomainReload();
            return _isRunning || HasPersistedRunState();
        }

        public static event Action<RunCompletionInfo> RunCompleted;

        static YybVisualComparisonBatchRunner()
        {
            EditorApplication.delayCall += TryResumeRunAfterDomainReload;
        }

        [MenuItem(MenuRoot + "Run satisfaction_2 testPrefab vs Main_Auto", false, 2130)]
        private static void RunDefaultMenu()
        {
            StartRun(DefaultFbxFileName, DefaultDurationSeconds, enableFingerCloseups: false);
        }

        public static void RunBatch()
        {
            string fbxFileName = GetCommandLineValue("-yybCompareFbx", DefaultFbxFileName);
            float durationSeconds = GetCommandLineFloat("-yybCompareDuration", DefaultDurationSeconds);
            bool enableFingerCloseups = GetCommandLineBool("-yybCompareFingerCloseups", false);
            StartRun(fbxFileName, durationSeconds, enableFingerCloseups);
        }

        public static void RunWithOptions(string fbxFileName, float durationSeconds, bool enableFingerCloseups)
        {
            StartRun(fbxFileName, durationSeconds, enableFingerCloseups);
        }

        private static void StartRun(string fbxFileName, float durationSeconds, bool enableFingerCloseups)
        {
            if (_isRunning)
            {
                Debug.LogWarning("[YybVisualComparisonBatchRunner] 이미 실행 중입니다.");
                return;
            }

            HumanoidSampleCode.SetEditorAutoStartSuppressed(true);
            ApplyTemporaryEnterPlayModeOptions();

            _projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            _fbxFileName = NormalizeFbxFileName(fbxFileName);
            _durationSeconds = Mathf.Max(0.1f, durationSeconds);
            _targetFrameCount = Mathf.Max(1, Mathf.CeilToInt(_durationSeconds * DefaultFrameRate));
            _enableFingerCloseups = enableFingerCloseups;

            try
            {
                _referenceClip = LoadFirstAnimationClip(Path.Combine(ProjectFbxDirectory, _fbxFileName)) ??
                                 LoadFirstAnimationClip(Path.Combine(ImportFbxDirectory, _fbxFileName));
                if (_referenceClip == null)
                {
                    throw new InvalidOperationException($"비교 기준 AnimationClip을 찾지 못했습니다: {_fbxFileName}");
                }

                _fallbackController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ManualControllerPath) ??
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackControllerPath);
                if (_fallbackController == null)
                {
                    throw new InvalidOperationException("수동 비교용 Animator Controller를 찾지 못했습니다.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YybVisualComparisonBatchRunner] 준비 실패: {ex.Message}\n{ex.StackTrace}");
                RestoreEnterPlayModeOptions();
                EmitCompletion(
                    passed: false,
                    summaryJsonPath: string.Empty,
                    summaryMarkdownPath: string.Empty,
                    failures: new[] { ex.Message });
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
                return;
            }

            PendingJobs.Clear();
            Results.Clear();
            Failures.Clear();
            _activeJob = null;
            _activeFileManager = null;
            _activeRecorder = null;
            _activeJobFinished = false;
            _activeJobStartedInPlayMode = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;

            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.SubManualTestPrefab,
                ScenePath = SubManualScenePath,
                SceneName = "Sub_Manual",
                DisplayName = "Sub_Manual testPrefab 수동 기준",
                ManualTargetNameToken = ManualTestPrefabNameToken
            });
            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.MainAuto,
                ScenePath = MainAutoScenePath,
                SceneName = "Main_Auto",
                DisplayName = "Main_Auto YYB 자동 경로",
                ManualTargetNameToken = string.Empty
            });

            _summarySessionId =
                $"when-{DateTime.Now:yyyyMMdd-HHmmss}_where-MainAuto_vs_SubManual_who-testprefab-vs-yyb_what-visual-compare_why-runtime-match_how-unity-batch";
            _summaryDirectory = Path.Combine(_projectRoot, OutputRootDirectory, _summarySessionId);
            Directory.CreateDirectory(_summaryDirectory);

            _isRunning = true;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            SavePersistedState();

            Debug.Log(
                $"[YybVisualComparisonBatchRunner] 시작: fbx={_fbxFileName}, duration={_durationSeconds:F2}s, " +
                $"targetFrames={_targetFrameCount}, fingerCloseups={_enableFingerCloseups}, batchMode={Application.isBatchMode}");
            AppendRunnerTrace($"run started fbx={_fbxFileName} duration={_durationSeconds:F2}s fingerCloseups={_enableFingerCloseups}");

            if (!Application.isBatchMode && RequestRuntimeDiagnosticScriptRefresh())
            {
                Debug.Log("[YybVisualComparisonBatchRunner] runtime diagnostics script refresh 대기 중입니다.");
                AppendRunnerTrace("runtime diagnostics script refresh requested; waiting before first job");
                EditorApplication.delayCall += ContinueStartRunAfterRefresh;
                return;
            }

            if (Application.isBatchMode)
            {
                AppendRunnerTrace("batch mode start skipping runtime diagnostics refresh");
            }

            StartNextJob();
        }

        private static void ContinueStartRunAfterRefresh()
        {
            if (!_isRunning || _activeJob != null || PendingJobs.Count == 0)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += ContinueStartRunAfterRefresh;
                return;
            }

            AppendRunnerTrace("runtime diagnostics script refresh settled; continuing run");
            StartNextJob();
        }

        private static bool RequestRuntimeDiagnosticScriptRefresh()
        {
            bool importedAny = false;
            foreach (string assetPath in RuntimeDiagnosticScriptPaths)
            {
                string fullPath = Path.Combine(_projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                importedAny = true;
            }

            if (importedAny)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            return EditorApplication.isCompiling || EditorApplication.isUpdating;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (!_isRunning)
            {
                return;
            }

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    _playModeEntryPending = false;
                    _playModeEntryRequestedAt = 0d;
                    SavePersistedState();
                    EditorApplication.update -= TryEnterPlayModeForActiveJob;
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"}");
                    EditorApplication.delayCall += StartCurrentJobInPlayMode;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished} pending={_advanceAfterPlayStopPending}");
                    CleanupActiveSubscriptions();
                    if (_advanceAfterPlayStopPending)
                    {
                        QueueAdvanceAfterPlayStop("EnteredEditMode");
                    }
                    else if (_activeJob != null && !_activeJobFinished)
                    {
                        QueuePlayModeEntryForActiveJob("EnteredEditModeWithoutCompletion");
                    }
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    AppendRunnerTrace($"playModeState={state} active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished} pending={_advanceAfterPlayStopPending}");
                    if (_activeJob != null && !_activeJobFinished)
                    {
                        RecordFailure($"Play Mode가 작업 완료 전에 종료되었습니다: {_activeJob.DisplayName}");
                    }
                    break;
            }
        }

        private static void StartNextJob()
        {
            if (!_isRunning)
            {
                return;
            }

            _activeJob = null;
            _activeFileManager = null;
            _activeRecorder = null;
            _activeJobFinished = false;
            _activeJobStartedInPlayMode = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;

            if (PendingJobs.Count == 0)
            {
                FinalizeRun();
                return;
            }

            _activeJob = PendingJobs.Dequeue();
            Debug.Log($"[YybVisualComparisonBatchRunner] 다음 작업: {_activeJob.DisplayName}");
            AppendRunnerTrace($"next job={_activeJob.DisplayName} pendingJobs={PendingJobs.Count}");
            SavePersistedState();

            try
            {
                if (!string.Equals(EditorSceneManager.GetActiveScene().path, _activeJob.ScenePath, StringComparison.Ordinal))
                {
                    EditorSceneManager.OpenScene(_activeJob.ScenePath, OpenSceneMode.Single);
                }
            }
            catch (Exception ex)
            {
                RecordFailure($"씬 열기 실패: {_activeJob.ScenePath} / {ex.Message}");
                EditorApplication.delayCall += StartNextJob;
                return;
            }

            QueuePlayModeEntryForActiveJob("StartNextJob");
        }

        private static void StartCurrentJobInPlayMode()
        {
            if (!_isRunning || _activeJob == null || !EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
            {
                return;
            }

            if (_activeJobStartedInPlayMode)
            {
                return;
            }

            _activeJobStartedInPlayMode = true;

            try
            {
                switch (_activeJob.Mode)
                {
                    case CaptureMode.MainAuto:
                        StartMainAutoJob();
                        break;
                    case CaptureMode.SubManualTestPrefab:
                    case CaptureMode.SubManualYyb:
                        StartSubManualJob(_activeJob.ManualTargetNameToken);
                        break;
                    default:
                        throw new NotSupportedException($"지원하지 않는 작업 모드: {_activeJob.Mode}");
                }
            }
            catch (Exception ex)
            {
                _activeJobStartedInPlayMode = false;
                RecordFailure($"{_activeJob.DisplayName} 시작 실패: {ex.Message}");
                RequestPlayModeStop();
            }
        }

        private static void StartMainAutoJob()
        {
            _activeFileManager = UnityEngine.Object.FindObjectOfType<FileManager>();
            if (_activeFileManager == null)
            {
                throw new InvalidOperationException("Main_Auto 씬에서 FileManager를 찾지 못했습니다.");
            }

            _activeRecorder = _activeFileManager.targetCharacter != null
                ? _activeFileManager.targetCharacter.GetComponent<HumanoidSampleCode>()
                : null;

            _activeFileManager.EditorDiagnosticSmokeFinished -= HandleMainAutoFinished;
            _activeFileManager.EditorDiagnosticSmokeFinished += HandleMainAutoFinished;
            SavePersistedState();

            bool started = _activeFileManager.StartEditorDiagnosticSmoke(
                _fbxFileName,
                _durationSeconds,
                _targetFrameCount,
                enableDiagnostics: true,
                enableFingerCloseups: _enableFingerCloseups,
                useDeterministicCaptureFramerate: true,
                diagnosticStartDelay: DefaultStartDelaySeconds,
                segment: FileManager.EditorDiagnosticSmokeSegment.Head);

            if (!started)
            {
                throw new InvalidOperationException("FileManager.StartEditorDiagnosticSmoke가 false를 반환했습니다.");
            }

            Debug.Log($"[YybVisualComparisonBatchRunner] 시작됨: {_activeJob.DisplayName}");
            AppendRunnerTrace($"job started scene={_activeJob.SceneName} display={_activeJob.DisplayName}");
        }

        private static void StartSubManualJob(string targetNameToken)
        {
            _activeRecorder = FindManualRecorder(targetNameToken);
            if (_activeRecorder == null)
            {
                throw new InvalidOperationException($"Sub_Manual 수동 기준 대상을 찾지 못했습니다: {targetNameToken}");
            }

            Animator animator = _activeRecorder.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException($"Animator가 없습니다: {GetHierarchyPath(_activeRecorder.transform)}");
            }

            PrepareManualAnimator(animator, _referenceClip);
            _activeRecorder.SetRecordingDiagnostics(
                enableProbe: true,
                enableFingerCloseups: _enableFingerCloseups,
                useCaptureFramerateForRegression: true);
            _activeRecorder.SetReady($"{_activeJob.DisplayName} 준비");

            _activeRecorder.RecordingFinished -= HandleManualFinished;
            _activeRecorder.RecordingFinished += HandleManualFinished;
            SavePersistedState();

            float captureDuration = Mathf.Min(_durationSeconds, Mathf.Max(0.1f, _referenceClip.length));
            int targetFrameCount = Mathf.Max(1, Mathf.CeilToInt(captureDuration * DefaultFrameRate));
            string labelSuffix = _activeJob.Mode == CaptureMode.SubManualTestPrefab
                ? ManualTestPrefabLabelSuffix
                : ManualYybLabelSuffix;
            string outputBaseName = $"{labelSuffix}_{Path.GetFileNameWithoutExtension(_fbxFileName)}_{Mathf.CeilToInt(captureDuration)}s_animtime";
            string comparisonLabel = $"manual_{outputBaseName}";

            if (!_activeRecorder.StartAutoRecording(
                    captureDuration,
                    outputBaseName,
                    null,
                    targetFrameCount,
                    comparisonLabel,
                    overwriteExistingOutput: true))
            {
                throw new InvalidOperationException("HumanoidSampleCode.StartAutoRecording이 false를 반환했습니다.");
            }

            animator.speed = 1f;
            Debug.Log($"[YybVisualComparisonBatchRunner] 시작됨: {_activeJob.DisplayName} / {comparisonLabel}");
            AppendRunnerTrace($"job started scene={_activeJob.SceneName} display={_activeJob.DisplayName} label={comparisonLabel}");
        }

        private static void PrepareManualAnimator(Animator animator, AnimationClip clip)
        {
            RuntimeAnimatorController baseController = animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController
                : _fallbackController;
            if (baseController == null)
            {
                throw new InvalidOperationException("Animator Override 기준 Controller가 없습니다.");
            }

            AnimatorOverrideController overrideController = new AnimatorOverrideController(baseController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            if (overrides.Count > 0 && overrides[0].Key != null)
            {
                overrideController[overrides[0].Key] = clip;
            }

            animator.runtimeAnimatorController = overrideController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            animator.speed = 0f;
            animator.Rebind();
            animator.Update(0f);
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }

        private static HumanoidSampleCode FindManualRecorder(string targetNameToken)
        {
            HumanoidSampleCode[] recorders = UnityEngine.Object.FindObjectsOfType<HumanoidSampleCode>(true);
            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                string hierarchyPath = GetHierarchyPath(recorder.transform);
                if (hierarchyPath.IndexOf(targetNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return recorder;
                }
            }

            return null;
        }

        private static void HandleMainAutoFinished(string fbxFileName, VmdSaveResult result)
        {
            if (_activeJob == null || _activeJob.Mode != CaptureMode.MainAuto || _activeJobFinished)
            {
                return;
            }

            MotionComparisonProbe probe = _activeRecorder != null
                ? _activeRecorder.GetComponent<MotionComparisonProbe>()
                : UnityEngine.Object.FindObjectOfType<MotionComparisonProbe>();
            FinalizeActiveJob(result, probe, targetName: _activeFileManager != null && _activeFileManager.targetCharacter != null
                ? _activeFileManager.targetCharacter.name
                : "Main_Auto Target");
        }

        private static void HandleManualFinished(VmdSaveResult result)
        {
            if (_activeJob == null || _activeJobFinished)
            {
                return;
            }

            MotionComparisonProbe probe = _activeRecorder != null
                ? _activeRecorder.GetComponent<MotionComparisonProbe>()
                : null;
            string targetName = _activeRecorder != null ? _activeRecorder.gameObject.name : _activeJob.ManualTargetNameToken;
            FinalizeActiveJob(result, probe, targetName);
        }

        private static void FinalizeActiveJob(VmdSaveResult result, MotionComparisonProbe probe, string targetName)
        {
            _activeJobFinished = true;
            _activeJobStartedInPlayMode = false;

            var captureResult = new CaptureResult
            {
                jobMode = _activeJob.Mode.ToString(),
                jobDisplayName = _activeJob.DisplayName,
                sceneName = _activeJob.SceneName,
                comparisonLabel = probe != null ? probe.name : string.Empty,
                targetName = targetName,
                success = result.Success,
                error = result.Success ? string.Empty : result.ErrorMessage,
                vmdPath = MakeProjectRelativePath(result.FilePath),
                frameCount = result.FrameCount,
                fileSizeBytes = result.FileSizeBytes,
                comparisonSessionManifestPath = probe != null ? MakeProjectRelativePath(probe.LastSessionManifestPath) : string.Empty,
                comparisonMetricsCsvPath = probe != null ? MakeProjectRelativePath(probe.LastCsvPath) : string.Empty,
                comparisonFrameFolderPath = probe != null ? MakeProjectRelativePath(probe.LastScreenshotFolder) : string.Empty,
                comparisonFrameIndexPath = probe != null && !string.IsNullOrEmpty(probe.LastScreenshotFolder)
                    ? MakeProjectRelativePath(Path.Combine(probe.LastScreenshotFolder, "index.csv"))
                    : string.Empty,
                comparisonSessionId = probe != null && !string.IsNullOrEmpty(probe.LastSessionManifestPath)
                    ? Path.GetFileName(Path.GetDirectoryName(probe.LastSessionManifestPath))
                    : string.Empty
            };
            Results.Add(captureResult);
            SavePersistedState();

            if (result.Success)
            {
                Debug.Log(
                    $"[YybVisualComparisonBatchRunner] 완료: {_activeJob.DisplayName}, " +
                    $"frames={result.FrameCount}, bytes={result.FileSizeBytes}, " +
                    $"session={captureResult.comparisonSessionId}");
                AppendRunnerTrace($"job completed display={_activeJob.DisplayName} session={captureResult.comparisonSessionId}");
            }
            else
            {
                RecordFailure($"{_activeJob.DisplayName} 실패: {result.ErrorMessage}");
            }

            RequestPlayModeStop();
        }

        private static void RequestPlayModeStop()
        {
            CleanupActiveSubscriptions();
            _activeJobStartedInPlayMode = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;
            _advanceAfterPlayStopPending = true;
            SavePersistedState();
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            QueueAdvanceAfterPlayStop("RequestPlayModeStop");
            AppendRunnerTrace($"request play stop active={_activeJob?.DisplayName ?? "<none>"} playing={EditorApplication.isPlaying}");

            if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += () => { EditorApplication.isPlaying = false; };
            }
            else
            {
                QueueAdvanceAfterPlayStop("AlreadyInEditMode");
            }
        }

        private static void QueueAdvanceAfterPlayStop(string reason)
        {
            if (!_advanceAfterPlayStopPending)
            {
                return;
            }

            EditorApplication.update -= TryAdvanceAfterPlayStop;
            EditorApplication.update += TryAdvanceAfterPlayStop;
            AppendRunnerTrace(
                $"advance queued reason={reason} active={_activeJob?.DisplayName ?? "<none>"} " +
                $"playing={EditorApplication.isPlaying} willChange={EditorApplication.isPlayingOrWillChangePlaymode}");
        }

        private static void TryAdvanceAfterPlayStop()
        {
            if (!_advanceAfterPlayStopPending)
            {
                EditorApplication.update -= TryAdvanceAfterPlayStop;
                return;
            }

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            _advanceAfterPlayStopPending = false;
            SavePersistedState();
            EditorApplication.update -= TryAdvanceAfterPlayStop;
            AppendRunnerTrace($"advance firing active={_activeJob?.DisplayName ?? "<none>"} finished={_activeJobFinished}");
            StartNextJob();
        }

        private static void QueuePlayModeEntryForActiveJob(string reason)
        {
            if (!_isRunning || _activeJob == null)
            {
                return;
            }

            if (!_playModeEntryPending)
            {
                _playModeEntryPending = true;
                SavePersistedState();
            }

            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update += TryEnterPlayModeForActiveJob;
            AppendRunnerTrace(
                $"playmode entry queued reason={reason} active={_activeJob.DisplayName} " +
                $"playing={EditorApplication.isPlaying} willChange={EditorApplication.isPlayingOrWillChangePlaymode} " +
                $"compiling={EditorApplication.isCompiling} updating={EditorApplication.isUpdating}");
        }

        private static void TryEnterPlayModeForActiveJob()
        {
            if (!_playModeEntryPending)
            {
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                return;
            }

            if (!_isRunning || _activeJob == null)
            {
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                RecoverFromMissingActiveJob(_isRunning
                    ? "TryEnterPlayModeForActiveJob"
                    : "TryEnterPlayModeForActiveJobNotRunning");
                return;
            }

            if (_advanceAfterPlayStopPending)
            {
                return;
            }

            if (EditorApplication.isPlaying)
            {
                _playModeEntryPending = false;
                _playModeEntryRequestedAt = 0d;
                SavePersistedState();
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                EditorApplication.delayCall += StartCurrentJobInPlayMode;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            if (!string.Equals(EditorSceneManager.GetActiveScene().path, _activeJob.ScenePath, StringComparison.Ordinal))
            {
                return;
            }

            if (_playModeEntryRequestedAt <= 0d)
            {
                _playModeEntryRequestedAt = EditorApplication.timeSinceStartup;
                SavePersistedState();
            }
            else if (EditorApplication.timeSinceStartup - _playModeEntryRequestedAt > PlayModeEntryTimeoutSeconds)
            {
                _playModeEntryPending = false;
                _playModeEntryRequestedAt = 0d;
                EditorApplication.update -= TryEnterPlayModeForActiveJob;
                RecordFailure($"Play Mode 진입 시간 초과: {_activeJob.DisplayName}");
                _activeJobFinished = true;
                RequestPlayModeStop();
                return;
            }

            EditorApplication.isPlaying = true;
        }

        private static void CleanupActiveSubscriptions()
        {
            if (_activeFileManager != null)
            {
                _activeFileManager.EditorDiagnosticSmokeFinished -= HandleMainAutoFinished;
            }

            if (_activeRecorder != null)
            {
                _activeRecorder.RecordingFinished -= HandleManualFinished;
            }
        }

        private static void RecordFailure(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            Failures.Add(message);
            Debug.LogError($"[YybVisualComparisonBatchRunner] {message}");
            AppendRunnerTrace($"failure={message}");
            SavePersistedState();
        }

        private static void FinalizeRun()
        {
            _isRunning = false;
            HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
            RestoreEnterPlayModeOptions();
            CleanupActiveSubscriptions();
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update -= TryAdvanceAfterPlayStop;

            string summaryJsonPath = Path.Combine(_summaryDirectory, SummaryJsonFileName);
            string summaryMarkdownPath = Path.Combine(_summaryDirectory, SummaryMarkdownFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(summaryJsonPath) ?? _summaryDirectory);

            WriteSummaryJson(summaryJsonPath);
            WriteSummaryMarkdown(summaryMarkdownPath);
            CopyLatestSummary(summaryJsonPath, LatestSummaryJsonRelativePath);
            CopyLatestSummary(summaryMarkdownPath, LatestSummaryMarkdownRelativePath);

            string resultMessage =
                $"[YybVisualComparisonBatchRunner] 종료: success={Results.Count(result => result.success)}/{Results.Count}, " +
                $"failures={Failures.Count}, summary={MakeProjectRelativePath(summaryJsonPath)}";
            if (Failures.Count > 0)
            {
                Debug.LogWarning(resultMessage);
            }
            else
            {
                Debug.Log(resultMessage);
            }

            EmitCompletion(
                passed: Failures.Count == 0,
                summaryJsonPath: summaryJsonPath,
                summaryMarkdownPath: summaryMarkdownPath,
                failures: Failures.ToArray());
            ClearPersistedState();

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(Failures.Count > 0 ? 1 : 0);
            }
        }

        private static void EmitCompletion(bool passed, string summaryJsonPath, string summaryMarkdownPath, string[] failures)
        {
            if (!passed)
            {
                HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
                RestoreEnterPlayModeOptions();
                ClearPersistedState();
            }

            RunCompleted?.Invoke(new RunCompletionInfo
            {
                passed = passed,
                sessionId = _summarySessionId,
                summaryJsonPath = MakeProjectRelativePath(summaryJsonPath),
                summaryMarkdownPath = MakeProjectRelativePath(summaryMarkdownPath),
                latestSummaryJsonPath = LatestSummaryJsonRelativePath,
                latestSummaryMarkdownPath = LatestSummaryMarkdownRelativePath,
                failures = failures ?? Array.Empty<string>(),
                totalJobs = Results.Count,
                successJobs = Results.Count(result => result.success)
            });
        }

        private static void ApplyTemporaryEnterPlayModeOptions()
        {
            if (Application.isBatchMode || _enterPlayModeOptionsCaptured)
            {
                return;
            }

            _previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            _enterPlayModeOptionsCaptured = true;

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = _previousEnterPlayModeOptions | EnterPlayModeOptions.DisableDomainReload;
        }

        private static void RestoreEnterPlayModeOptions()
        {
            if (!_enterPlayModeOptionsCaptured)
            {
                return;
            }

            EditorSettings.enterPlayModeOptions = _previousEnterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _previousEnterPlayModeOptionsEnabled;
            _enterPlayModeOptionsCaptured = false;
        }

        private static void SavePersistedState()
        {
            PersistedState state = new PersistedState
            {
                fbxFileName = _fbxFileName,
                durationSeconds = _durationSeconds,
                targetFrameCount = _targetFrameCount,
                enableFingerCloseups = _enableFingerCloseups,
                isRunning = _isRunning,
                activeJobFinished = _activeJobFinished,
                advanceAfterPlayStopPending = _advanceAfterPlayStopPending,
                playModeEntryPending = _playModeEntryPending,
                summarySessionId = _summarySessionId,
                summaryDirectory = _summaryDirectory,
                projectRoot = _projectRoot,
                activeJob = ToPersistedJob(_activeJob),
                pendingJobs = PendingJobs.Select(ToPersistedJob).ToArray(),
                results = Results.Select(ToPersistedResult).ToArray(),
                failures = Failures.ToArray()
            };

            SessionState.SetString(RunnerStateSessionKey, JsonUtility.ToJson(state));
        }

        private static void ClearPersistedState()
        {
            SessionState.EraseString(RunnerStateSessionKey);
        }

        private static void TryResumeRunAfterDomainReload()
        {
            if (_isRunning)
            {
                return;
            }

            if (!HasPersistedRunState())
            {
                return;
            }

            string json = SessionState.GetString(RunnerStateSessionKey, string.Empty);
            PersistedState state = JsonUtility.FromJson<PersistedState>(json);
            try
            {
                RestoreFromPersistedState(state);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YybVisualComparisonBatchRunner] 상태 복구 실패: {ex.Message}\n{ex.StackTrace}");
                RecordFailure($"상태 복구 실패: {ex.Message}");
                ClearPersistedState();
                _isRunning = false;
                HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
                RestoreEnterPlayModeOptions();
            }
        }

        private static void RestoreFromPersistedState(PersistedState state)
        {
            _fbxFileName = string.IsNullOrWhiteSpace(state.fbxFileName) ? DefaultFbxFileName : state.fbxFileName;
            _durationSeconds = Mathf.Max(0.1f, state.durationSeconds);
            _targetFrameCount = Mathf.Max(1, state.targetFrameCount);
            _enableFingerCloseups = state.enableFingerCloseups;
            _summarySessionId = state.summarySessionId ?? string.Empty;
            _summaryDirectory = state.summaryDirectory ?? string.Empty;
            _projectRoot = state.projectRoot ?? (Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath);
            _activeJobFinished = state.activeJobFinished;
            _advanceAfterPlayStopPending = state.advanceAfterPlayStopPending;
            _playModeEntryPending = state.playModeEntryPending;
            _playModeEntryRequestedAt = 0d;
            _activeJobStartedInPlayMode = false;
            _activeJob = FromPersistedJob(state.activeJob);

            PendingJobs.Clear();
            int skippedPendingJobs = 0;
            if (state.pendingJobs != null)
            {
                foreach (PersistedCaptureJob job in state.pendingJobs)
                {
                    CaptureJob restoredJob = FromPersistedJob(job);
                    if (restoredJob == null)
                    {
                        skippedPendingJobs++;
                        continue;
                    }

                    PendingJobs.Enqueue(restoredJob);
                }
            }

            Results.Clear();
            int skippedResults = 0;
            if (state.results != null)
            {
                foreach (PersistedCaptureResult result in state.results)
                {
                    CaptureResult restoredResult = FromPersistedResult(result);
                    if (restoredResult == null)
                    {
                        skippedResults++;
                        continue;
                    }

                    Results.Add(restoredResult);
                }
            }

            Failures.Clear();
            if (state.failures != null)
            {
                Failures.AddRange(state.failures.Where(message => !string.IsNullOrWhiteSpace(message)));
            }

            _activeFileManager = null;
            _activeRecorder = null;
            _isRunning = true;
            HumanoidSampleCode.SetEditorAutoStartSuppressed(true);
            ApplyTemporaryEnterPlayModeOptions();
            LoadRunAssetsForResume();

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update -= TryAdvanceAfterPlayStop;
            if (_playModeEntryPending)
            {
                EditorApplication.update += TryEnterPlayModeForActiveJob;
            }
            if (_advanceAfterPlayStopPending)
            {
                EditorApplication.update += TryAdvanceAfterPlayStop;
            }

            Debug.Log(
                $"[YybVisualComparisonBatchRunner] 상태 복구: active={_activeJob?.DisplayName ?? "<none>"}, " +
                $"pending={PendingJobs.Count}, playing={EditorApplication.isPlaying}");
            AppendRunnerTrace(
                $"state restored active={_activeJob?.DisplayName ?? "<none>"} pendingJobs={PendingJobs.Count} " +
                $"activeFinished={_activeJobFinished} advancePending={_advanceAfterPlayStopPending} playing={EditorApplication.isPlaying} " +
                $"skippedPendingJobs={skippedPendingJobs} skippedResults={skippedResults}");

            if (_advanceAfterPlayStopPending)
            {
                QueueAdvanceAfterPlayStop("RestoreFromPersistedState");
            }
            else if (_playModeEntryPending)
            {
                if (_activeJob != null)
                {
                    QueuePlayModeEntryForActiveJob("RestoreFromPersistedState");
                }
                else
                {
                    RecoverFromMissingActiveJob("RestoreFromPersistedState");
                }
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += StartCurrentJobInPlayMode;
            }
            else if (_activeJob != null && _activeJobFinished)
            {
                EditorApplication.delayCall += StartNextJob;
            }
            else if (_activeJob != null)
            {
                EditorApplication.delayCall += () => { QueuePlayModeEntryForActiveJob("RestoreActiveJob"); };
            }
            else if (PendingJobs.Count > 0)
            {
                EditorApplication.delayCall += StartNextJob;
            }
            else
            {
                EditorApplication.delayCall += FinalizeRun;
            }
        }

        private static void RecoverFromMissingActiveJob(string reason)
        {
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;
            SavePersistedState();

            if (!_isRunning)
            {
                return;
            }

            if (_advanceAfterPlayStopPending)
            {
                AppendRunnerTrace($"missing active job deferred reason={reason} advancePending=True pendingJobs={PendingJobs.Count}");
                return;
            }

            if (PendingJobs.Count > 0)
            {
                AppendRunnerTrace($"missing active job recovered reason={reason} pendingJobs={PendingJobs.Count}");
                EditorApplication.delayCall += StartNextJob;
                return;
            }

            AppendRunnerTrace($"missing active job finalizing reason={reason}");
            EditorApplication.delayCall += FinalizeRun;
        }

        private static void AppendRunnerTrace(string message)
        {
            try
            {
                string projectRoot = string.IsNullOrWhiteSpace(_projectRoot)
                    ? (Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath)
                    : _projectRoot;
                string path = Path.Combine(projectRoot, RunnerTraceRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? projectRoot);
                File.AppendAllText(
                    path,
                    $"{DateTime.Now.ToString("o", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}");
            }
            catch
            {
                // Ignore trace write failures. The runner must keep going even if diagnostics cannot be written.
            }
        }

        private static void LoadRunAssetsForResume()
        {
            _referenceClip = LoadFirstAnimationClip(Path.Combine(ProjectFbxDirectory, _fbxFileName)) ??
                             LoadFirstAnimationClip(Path.Combine(ImportFbxDirectory, _fbxFileName));
            if (_referenceClip == null)
            {
                throw new InvalidOperationException($"비교 기준 AnimationClip을 찾지 못했습니다: {_fbxFileName}");
            }

            _fallbackController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ManualControllerPath) ??
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FallbackControllerPath);
            if (_fallbackController == null)
            {
                throw new InvalidOperationException("수동 비교용 Animator Controller를 찾지 못했습니다.");
            }
        }

        private static PersistedCaptureJob ToPersistedJob(CaptureJob job)
        {
            if (job == null)
            {
                return null;
            }

            return new PersistedCaptureJob
            {
                mode = (int)job.Mode,
                scenePath = job.ScenePath,
                sceneName = job.SceneName,
                displayName = job.DisplayName,
                manualTargetNameToken = job.ManualTargetNameToken
            };
        }

        private static CaptureJob FromPersistedJob(PersistedCaptureJob job)
        {
            if (job == null)
            {
                return null;
            }

            bool hasScenePath = !string.IsNullOrWhiteSpace(job.scenePath);
            bool hasDisplayName = !string.IsNullOrWhiteSpace(job.displayName);
            bool modeInRange = Enum.IsDefined(typeof(CaptureMode), job.mode);
            if (!hasScenePath || !hasDisplayName || !modeInRange)
            {
                return null;
            }

            return new CaptureJob
            {
                Mode = (CaptureMode)job.mode,
                ScenePath = job.scenePath,
                SceneName = job.sceneName,
                DisplayName = job.displayName,
                ManualTargetNameToken = job.manualTargetNameToken
            };
        }

        private static PersistedCaptureResult ToPersistedResult(CaptureResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new PersistedCaptureResult
            {
                jobMode = result.jobMode,
                jobDisplayName = result.jobDisplayName,
                sceneName = result.sceneName,
                comparisonLabel = result.comparisonLabel,
                targetName = result.targetName,
                success = result.success,
                error = result.error,
                vmdPath = result.vmdPath,
                frameCount = result.frameCount,
                fileSizeBytes = result.fileSizeBytes,
                comparisonSessionManifestPath = result.comparisonSessionManifestPath,
                comparisonMetricsCsvPath = result.comparisonMetricsCsvPath,
                comparisonFrameFolderPath = result.comparisonFrameFolderPath,
                comparisonFrameIndexPath = result.comparisonFrameIndexPath,
                comparisonSessionId = result.comparisonSessionId
            };
        }

        private static CaptureResult FromPersistedResult(PersistedCaptureResult result)
        {
            if (result == null)
            {
                return null;
            }

            return new CaptureResult
            {
                jobMode = result.jobMode,
                jobDisplayName = result.jobDisplayName,
                sceneName = result.sceneName,
                comparisonLabel = result.comparisonLabel,
                targetName = result.targetName,
                success = result.success,
                error = result.error,
                vmdPath = result.vmdPath,
                frameCount = result.frameCount,
                fileSizeBytes = result.fileSizeBytes,
                comparisonSessionManifestPath = result.comparisonSessionManifestPath,
                comparisonMetricsCsvPath = result.comparisonMetricsCsvPath,
                comparisonFrameFolderPath = result.comparisonFrameFolderPath,
                comparisonFrameIndexPath = result.comparisonFrameIndexPath,
                comparisonSessionId = result.comparisonSessionId
            };
        }

        private static void WriteSummaryJson(string path)
        {
            SummaryContainer summary = new SummaryContainer
            {
                session_id = _summarySessionId,
                generated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                fbx_file = _fbxFileName,
                duration_seconds = _durationSeconds,
                target_frame_count = _targetFrameCount,
                finger_closeups = _enableFingerCloseups,
                reference_clip_name = _referenceClip != null ? _referenceClip.name : string.Empty,
                results = Results.ToArray(),
                failures = Failures.ToArray()
            };

            string json = JsonUtility.ToJson(summary, true);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static void WriteSummaryMarkdown(string path)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# YYB Visual Comparison Batch");
            builder.AppendLine();
            builder.AppendLine($"- session id: `{_summarySessionId}`");
            builder.AppendLine($"- generated at: `{DateTime.Now:yyyy-MM-dd HH:mm:ss}`");
            builder.AppendLine($"- fbx file: `{_fbxFileName}`");
            builder.AppendLine($"- duration seconds: `{_durationSeconds:F2}`");
            builder.AppendLine($"- target frames: `{_targetFrameCount}`");
            builder.AppendLine($"- finger closeups: `{_enableFingerCloseups}`");
            builder.AppendLine($"- reference clip: `{(_referenceClip != null ? _referenceClip.name : "")}`");
            builder.AppendLine();
            builder.AppendLine("## Results");
            builder.AppendLine();
            builder.AppendLine("| job | scene | target | success | session | csv | frames | vmd |");
            builder.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (CaptureResult result in Results)
            {
                builder.AppendLine(
                    $"| {EscapeMarkdown(result.jobDisplayName)} | {EscapeMarkdown(result.sceneName)} | {EscapeMarkdown(result.targetName)} | {result.success} | " +
                    $"`{EscapeMarkdown(result.comparisonSessionId)}` | `{EscapeMarkdown(result.comparisonMetricsCsvPath)}` | " +
                    $"`{EscapeMarkdown(result.comparisonFrameFolderPath)}` | `{EscapeMarkdown(result.vmdPath)}` |");
            }

            if (Failures.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Failures");
                builder.AppendLine();
                foreach (string failure in Failures)
                {
                    builder.AppendLine($"- {EscapeMarkdown(failure)}");
                }
            }

            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static void CopyLatestSummary(string sourcePath, string relativeTargetPath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            string targetPath = Path.Combine(_projectRoot, relativeTargetPath);
            string targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        private static AnimationClip LoadFirstAnimationClip(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip && clip.humanMotion)
                {
                    return clip;
                }
            }

            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is AnimationClip clip)
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        }

        private static string NormalizeFbxFileName(string fbxFileName)
        {
            string name = string.IsNullOrWhiteSpace(fbxFileName) ? DefaultFbxFileName : fbxFileName.Trim();
            return string.Equals(Path.GetExtension(name), ".fbx", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(name)
                : Path.GetFileNameWithoutExtension(name) + ".fbx";
        }

        private static string GetCommandLineValue(string name, string fallbackValue)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return fallbackValue;
        }

        private static float GetCommandLineFloat(string name, float fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? parsed
                : fallbackValue;
        }

        private static bool GetCommandLineBool(string name, bool fallbackValue)
        {
            string value = GetCommandLineValue(name, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallbackValue;
            }

            if (bool.TryParse(value, out bool parsedBool))
            {
                return parsedBool;
            }

            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                ? true
                : string.Equals(value, "0", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : fallbackValue;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string MakeProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            string normalizedProjectRoot = _projectRoot.Replace('\\', '/').TrimEnd('/');
            string normalizedAbsolute = absolutePath.Replace('\\', '/');
            if (normalizedAbsolute.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return normalizedAbsolute.Substring(normalizedProjectRoot.Length + 1);
            }

            return normalizedAbsolute;
        }

        private static string EscapeMarkdown(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("|", "\\|");
        }

        [Serializable]
        private sealed class SummaryContainer
        {
            public string session_id;
            public string generated_at;
            public string fbx_file;
            public float duration_seconds;
            public int target_frame_count;
            public bool finger_closeups;
            public string reference_clip_name;
            public CaptureResult[] results;
            public string[] failures;
        }
    }
}
#endif
