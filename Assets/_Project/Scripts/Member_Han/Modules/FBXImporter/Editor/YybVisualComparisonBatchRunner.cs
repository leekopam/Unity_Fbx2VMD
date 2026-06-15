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
        private const string MainRecordingScenePath = "Assets/_Project/Scene/Main_Recoding.unity";
        private const string SubManualScenePath = "Assets/_Project/Scene/Sub_Manual.unity";
        private const string DefaultFbxFileName = "satisfaction_2.fbx";
        private const string SatisfactionReferenceOutputBaseName = "satisfaction_2";
        private const int SatisfactionReferenceMaxMmdFrame = 6000;
        private const string ManualControllerPath = "Assets/_ManualReference/SampleAnimation/TestAnimator1_Manual.controller";
        private const string FallbackControllerPath = "Assets/Plugins/VMDRecorderSample/SampleAnimation/TestAnimator1.controller";
        private const string ProjectFbxDirectory = "Assets/_Project/FBX";
        private const string ImportFbxDirectory = "Assets/Resources/Import_FBX";
        private const string OutputRootDirectory = "Docs/Machine_Spirit/Local/ComparisonSessions";
        private const string MmdAutomationRunsRelativePath = "Docs/Machine_Spirit/Local/MMDQASessions/automation_runs";
        private const string LatestSummaryJsonRelativePath = "Docs/Machine_Spirit/Local/progress/evidence/yyb_visual_compare_latest.json";
        private const string LatestSummaryMarkdownRelativePath = "Docs/Machine_Spirit/Local/progress/evidence/yyb_visual_compare_latest.md";
        private const string SummaryJsonFileName = "yyb_visual_compare_summary.json";
        private const string SummaryMarkdownFileName = "yyb_visual_compare_summary.md";
        private const string RunnerTraceRelativePath = "Docs/Machine_Spirit/Local/runtime/yyb_visual_compare_runner_trace.log";
        private const int EvidenceSafeMaxFullPathLength = 240;
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
            MainRecording,
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
            public bool enableRecorderParentFrameIkOffsetsWhenCenterParented;
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
        private static string _referenceClipAssetPath = string.Empty;
        private static RuntimeAnimatorController _fallbackController;
        private static string _fbxFileName = DefaultFbxFileName;
        private static float _durationSeconds = DefaultDurationSeconds;
        private static int _targetFrameCount = Mathf.CeilToInt(DefaultDurationSeconds * DefaultFrameRate);
        private static bool _enableFingerCloseups;
        private static bool _enableRecorderParentFrameIkOffsetsWhenCenterParented = true;
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
            StartRun(
                DefaultFbxFileName,
                DefaultDurationSeconds,
                enableFingerCloseups: false,
                enableRecorderParentFrameIkOffsetsWhenCenterParented: true);
        }

        [MenuItem(MenuRoot + "Clear Stale Run State", false, 2139)]
        private static void ClearStaleRunStateMenu()
        {
            ClearStaleRunState("menu");
        }

        public static void ClearStaleRunState(string reason)
        {
            HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
            RestoreEnterPlayModeOptions();
            CleanupActiveSubscriptions();
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update -= TryAdvanceAfterPlayStop;
            PendingJobs.Clear();
            Results.Clear();
            Failures.Clear();
            _activeJob = null;
            _activeFileManager = null;
            _activeRecorder = null;
            _activeJobFinished = false;
            _advanceAfterPlayStopPending = false;
            _playModeEntryPending = false;
            _playModeEntryRequestedAt = 0d;
            _activeJobStartedInPlayMode = false;
            _isRunning = false;
            ClearPersistedState();
            AppendRunnerTrace($"stale run state cleared reason={reason}");
        }

        public static void RunBatch()
        {
            string fbxFileName = GetCommandLineValue("-yybCompareFbx", DefaultFbxFileName);
            float durationSeconds = GetCommandLineFloat("-yybCompareDuration", DefaultDurationSeconds);
            bool enableFingerCloseups = GetCommandLineBool("-yybCompareFingerCloseups", false);
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented =
                GetCommandLineBool("-yybCompareRecorderParentFrameIkOffsetsWhenCenterParented", true);
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented);
        }

        public static void RunWithOptions(string fbxFileName, float durationSeconds, bool enableFingerCloseups)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented: true);
        }

        public static void RunWithOptions(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented)
        {
            StartRun(
                fbxFileName,
                durationSeconds,
                enableFingerCloseups,
                enableRecorderParentFrameIkOffsetsWhenCenterParented);
        }

        private static void StartRun(
            string fbxFileName,
            float durationSeconds,
            bool enableFingerCloseups,
            bool enableRecorderParentFrameIkOffsetsWhenCenterParented)
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
            _enableRecorderParentFrameIkOffsetsWhenCenterParented = enableRecorderParentFrameIkOffsetsWhenCenterParented;

            try
            {
                _referenceClipAssetPath = ResolveReferenceClipAssetPath(
                    _fbxFileName,
                    assetPath => LoadFirstAnimationClip(assetPath) != null);
                _referenceClip = LoadFirstAnimationClip(_referenceClipAssetPath);
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
                Mode = CaptureMode.SubManualYyb,
                ScenePath = SubManualScenePath,
                SceneName = "Sub_Manual",
                DisplayName = "Sub_Manual YYB 수동 기준",
                ManualTargetNameToken = ManualYybNameToken
            });
            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.MainRecording,
                ScenePath = MainRecordingScenePath,
                SceneName = "Main_Recoding",
                DisplayName = "Main_Recoding YYB 자동 경로",
                ManualTargetNameToken = string.Empty
            });
            PendingJobs.Enqueue(new CaptureJob
            {
                Mode = CaptureMode.MainAuto,
                ScenePath = MainAutoScenePath,
                SceneName = "Main_Auto",
                DisplayName = "Main_Auto YYB 자동 경로",
                ManualTargetNameToken = string.Empty
            });

            string rawSummarySessionId =
                $"when-{DateTime.Now:yyyyMMdd-HHmmss}_where-MainAuto_vs_SubManual_who-testprefab-vs-yyb_what-visual-compare_why-runtime-match_how-unity-batch";
            _summarySessionId = BuildSafeSummarySessionId(rawSummarySessionId);
            string summaryRoot = Path.Combine(_projectRoot, OutputRootDirectory);
            _summaryDirectory = Path.Combine(summaryRoot, _summarySessionId);
            Directory.CreateDirectory(_summaryDirectory);

            _isRunning = true;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            SavePersistedState();

            Debug.Log(
                $"[YybVisualComparisonBatchRunner] 시작: fbx={_fbxFileName}, duration={_durationSeconds:F2}s, " +
                $"targetFrames={_targetFrameCount}, fingerCloseups={_enableFingerCloseups}, " +
                $"recorderParentIkOffsets={_enableRecorderParentFrameIkOffsetsWhenCenterParented}, batchMode={Application.isBatchMode}");
            AppendRunnerTrace(
                $"run started fbx={_fbxFileName} duration={_durationSeconds:F2}s " +
                $"fingerCloseups={_enableFingerCloseups} recorderParentIkOffsets={_enableRecorderParentFrameIkOffsetsWhenCenterParented}");

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
            return UnityManualRefreshGuard.RequestRefreshForAssets(
                RuntimeDiagnosticScriptPaths,
                "yyb_visual_comparison_runtime_diagnostics");
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
            if (!CanStartNextJob(_isRunning, _activeJob != null, _activeJobFinished))
            {
                if (_isRunning)
                {
                    AppendRunnerTrace(
                        $"start next ignored active={_activeJob?.DisplayName ?? "<none>"} " +
                        $"finished={_activeJobFinished} pendingJobs={PendingJobs.Count}");
                }

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

        private static bool CanStartNextJob(bool isRunning, bool hasActiveJob, bool activeJobFinished)
        {
            return isRunning && (!hasActiveJob || activeJobFinished);
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
                    case CaptureMode.MainRecording:
                        StartMainSceneJob();
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
                _activeJobFinished = true;
                RecordFailure($"{_activeJob.DisplayName} 시작 실패: {ex.Message}");
                RequestPlayModeStop();
            }
        }

        private static void StartMainSceneJob()
        {
            _activeFileManager = UnityEngine.Object.FindObjectOfType<FileManager>();
            if (_activeFileManager == null)
            {
                throw new InvalidOperationException($"{_activeJob.SceneName} 씬에서 FileManager를 찾지 못했습니다.");
            }

            _activeRecorder = _activeFileManager.targetCharacter != null
                ? _activeFileManager.targetCharacter.GetComponent<HumanoidSampleCode>()
                : null;
            if (_activeRecorder != null)
            {
                UnityHumanoidVMDRecorder vmdRecorder = _activeRecorder.GetComponent<UnityHumanoidVMDRecorder>();
                if (vmdRecorder != null)
                {
                    vmdRecorder.EnableParentFrameIkOffsetCompensationWhenCenterParented =
                        _enableRecorderParentFrameIkOffsetsWhenCenterParented;
                    vmdRecorder.IgnoreInitialPosition = true;
                }
            }

            _activeFileManager.EditorDiagnosticSmokeFinished -= HandleMainSceneFinished;
            _activeFileManager.EditorDiagnosticSmokeFinished += HandleMainSceneFinished;
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
            _activeRecorder = SelectActiveManualRecorder(targetNameToken);
            if (_activeRecorder == null)
            {
                throw new InvalidOperationException($"Sub_Manual 수동 기준 대상을 찾지 못했습니다: {targetNameToken}");
            }
            if (!_activeRecorder.gameObject.activeInHierarchy)
            {
                throw new InvalidOperationException($"Sub_Manual 수동 기준 대상이 비활성 상태입니다: {GetHierarchyPath(_activeRecorder.transform)}");
            }

            Animator animator = _activeRecorder.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException($"Animator가 없습니다: {GetHierarchyPath(_activeRecorder.transform)}");
            }

            PrepareManualAnimator(animator, _referenceClip);
            UnityHumanoidVMDRecorder vmdRecorder = _activeRecorder.GetComponent<UnityHumanoidVMDRecorder>();
            if (vmdRecorder != null)
            {
                vmdRecorder.IgnoreInitialPosition = true;
            }
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

        private static HumanoidSampleCode SelectActiveManualRecorder(string targetNameToken)
        {
            HumanoidSampleCode[] recorders = UnityEngine.Object.FindObjectsOfType<HumanoidSampleCode>(true);
            HumanoidSampleCode selected = null;
            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                string hierarchyPath = GetHierarchyPath(recorder.transform);
                if (hierarchyPath.IndexOf(targetNameToken, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    selected = recorder;
                    break;
                }
            }

            if (selected == null)
            {
                return null;
            }

            foreach (HumanoidSampleCode recorder in recorders)
            {
                if (recorder == null)
                {
                    continue;
                }

                recorder.gameObject.SetActive(ReferenceEquals(recorder, selected));
            }

            return selected;
        }

        private static void HandleMainSceneFinished(string fbxFileName, VmdSaveResult result)
        {
            if (_activeJob == null ||
                (_activeJob.Mode != CaptureMode.MainAuto && _activeJob.Mode != CaptureMode.MainRecording) ||
                _activeJobFinished)
            {
                return;
            }

            MotionComparisonProbe probe = _activeRecorder != null
                ? _activeRecorder.GetComponent<MotionComparisonProbe>()
                : UnityEngine.Object.FindObjectOfType<MotionComparisonProbe>();
            FinalizeActiveJob(
                BuildStableCandidateResult(result),
                probe,
                targetName: _activeFileManager != null && _activeFileManager.targetCharacter != null
                ? _activeFileManager.targetCharacter.name
                : $"{_activeJob.SceneName} Target");
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

        private static VmdSaveResult BuildStableCandidateResult(VmdSaveResult result)
        {
            if (!result.Success ||
                _activeJob == null ||
                _activeJob.Mode == CaptureMode.MainAuto ||
                string.IsNullOrWhiteSpace(result.FilePath) ||
                !File.Exists(result.FilePath))
            {
                return result;
            }

            string copyPath = BuildCandidateVmdEvidencePath(_activeJob, result.FilePath);
            if (string.IsNullOrWhiteSpace(copyPath))
            {
                return result;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(copyPath) ?? _summaryDirectory);
            File.Copy(result.FilePath, copyPath, overwrite: true);
            string exportRotationDiagnosticsCsvPath = CopyStableCandidateSiblingArtifact(
                result.FilePath,
                result.ExportRotationDiagnosticsCsvPath,
                copyPath);
            string exportIkSourceDiagnosticsCsvPath = CopyStableCandidateSiblingArtifact(
                result.FilePath,
                result.ExportIkSourceDiagnosticsCsvPath,
                copyPath);
            return VmdSaveResult.Ok(
                copyPath,
                result.FrameCount,
                new FileInfo(copyPath).Length,
                exportRotationDiagnosticsCsvPath,
                exportIkSourceDiagnosticsCsvPath);
        }

        private static string CopyStableCandidateSiblingArtifact(
            string sourceVmdPath,
            string sourceArtifactPath,
            string candidateVmdPath)
        {
            if (string.IsNullOrWhiteSpace(sourceArtifactPath) ||
                !File.Exists(sourceArtifactPath) ||
                string.IsNullOrWhiteSpace(candidateVmdPath))
            {
                return string.Empty;
            }

            string destinationPath = BuildStableCandidateSiblingArtifactPath(
                sourceVmdPath,
                sourceArtifactPath,
                candidateVmdPath);
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return string.Empty;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? _summaryDirectory);
            File.Copy(sourceArtifactPath, destinationPath, overwrite: true);
            return destinationPath;
        }

        private static string BuildStableCandidateSiblingArtifactPath(
            string sourceVmdPath,
            string sourceArtifactPath,
            string candidateVmdPath)
        {
            string artifactFileName = Path.GetFileName(sourceArtifactPath);
            string candidateDirectory = Path.GetDirectoryName(candidateVmdPath) ?? _summaryDirectory;
            string candidateBaseName = Path.GetFileNameWithoutExtension(candidateVmdPath);
            if (string.IsNullOrWhiteSpace(artifactFileName) ||
                string.IsNullOrWhiteSpace(candidateDirectory) ||
                string.IsNullOrWhiteSpace(candidateBaseName))
            {
                return string.Empty;
            }

            string sourceBaseName = Path.GetFileNameWithoutExtension(sourceVmdPath);
            string suffix = !string.IsNullOrWhiteSpace(sourceBaseName) &&
                artifactFileName.StartsWith(sourceBaseName, StringComparison.OrdinalIgnoreCase)
                    ? artifactFileName.Substring(sourceBaseName.Length)
                    : $".{SanitizeFileName(Path.GetFileNameWithoutExtension(sourceArtifactPath))}{Path.GetExtension(sourceArtifactPath)}";
            if (string.IsNullOrWhiteSpace(suffix))
            {
                suffix = Path.GetExtension(sourceArtifactPath);
            }

            return Path.Combine(candidateDirectory, $"{candidateBaseName}{suffix}");
        }

        private static string BuildCandidateVmdEvidencePath(CaptureJob job, string sourceVmdPath)
        {
            if (job == null || string.IsNullOrWhiteSpace(_summaryDirectory))
            {
                return string.Empty;
            }

            string sourceExtension = Path.GetExtension(sourceVmdPath);
            if (string.IsNullOrWhiteSpace(sourceExtension))
            {
                sourceExtension = ".vmd";
            }

            string fileName = BuildCandidateVmdEvidenceFileName(job.Mode, sourceExtension);
            return Path.Combine(_summaryDirectory, fileName);
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
                _activeFileManager.EditorDiagnosticSmokeFinished -= HandleMainSceneFinished;
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
            AppendRunnerTrace($"finalize started results={Results.Count} failures={Failures.Count}");
            HumanoidSampleCode.SetEditorAutoStartSuppressed(false);
            RestoreEnterPlayModeOptions();
            CleanupActiveSubscriptions();
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.update -= TryEnterPlayModeForActiveJob;
            EditorApplication.update -= TryAdvanceAfterPlayStop;

            string summaryJsonPath = Path.Combine(_summaryDirectory, SummaryJsonFileName);
            string summaryMarkdownPath = Path.Combine(_summaryDirectory, SummaryMarkdownFileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(summaryJsonPath) ?? _summaryDirectory);
                WriteSummaryJson(summaryJsonPath);
                WriteSummaryMarkdown(summaryMarkdownPath);
                CopyLatestSummary(summaryJsonPath, LatestSummaryJsonRelativePath);
                CopyLatestSummary(summaryMarkdownPath, LatestSummaryMarkdownRelativePath);
            }
            catch (Exception ex)
            {
                string message = $"summary finalize failed: {ex.Message}";
                if (!Failures.Contains(message))
                {
                    Failures.Add(message);
                }

                Debug.LogError($"[YybVisualComparisonBatchRunner] {message}\n{ex.StackTrace}");
                AppendRunnerTrace(message);
            }

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
            _isRunning = false;
            ClearPersistedState();
            AppendRunnerTrace($"finalize completed passed={Failures.Count == 0} results={Results.Count}");

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
                enableRecorderParentFrameIkOffsetsWhenCenterParented = _enableRecorderParentFrameIkOffsetsWhenCenterParented,
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
            _enableRecorderParentFrameIkOffsetsWhenCenterParented = state.enableRecorderParentFrameIkOffsetsWhenCenterParented;
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
                StartNextJob();
            }
            else
            {
                FinalizeRun();
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
            _referenceClipAssetPath = ResolveReferenceClipAssetPath(
                _fbxFileName,
                assetPath => LoadFirstAnimationClip(assetPath) != null);
            _referenceClip = LoadFirstAnimationClip(_referenceClipAssetPath);
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
            MotionComparisonFrameQualitySummary[] frameQualitySummaries = BuildFrameQualitySummaries();
            int summaryTargetFrameCount = ResolveSummaryTargetFrameCount();
            SummaryFrameRoleDiagnostics frameRoleDiagnostics = BuildSummaryFrameRoleDiagnostics(
                summaryTargetFrameCount,
                ResolveFrameCount(CaptureMode.SubManualTestPrefab),
                ResolveFrameCount(CaptureMode.MainAuto));
            SummaryContainer summary = new SummaryContainer
            {
                session_id = _summarySessionId,
                generated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                fbx_file = _fbxFileName,
                duration_seconds = _durationSeconds,
                target_frame_count = summaryTargetFrameCount,
                finger_closeups = _enableFingerCloseups,
                recorder_parent_ik_offsets_when_center_parented = _enableRecorderParentFrameIkOffsetsWhenCenterParented,
                reference_clip_name = _referenceClip != null ? _referenceClip.name : string.Empty,
                reference_clip_asset_path = _referenceClipAssetPath,
                results = Results.ToArray(),
                frame_count_roles = frameRoleDiagnostics,
                sample_ordering_diagnostics = BuildSampleOrderingDiagnostics(),
                selected_candidate_artifact = BuildCandidateArtifactSelection(frameQualitySummaries),
                frame_quality_summaries = frameQualitySummaries,
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
            builder.AppendLine($"- target frames: `{ResolveSummaryTargetFrameCount()}`");
            builder.AppendLine($"- finger closeups: `{_enableFingerCloseups}`");
            builder.AppendLine($"- recorder parent IK offsets (center-parented): `{_enableRecorderParentFrameIkOffsetsWhenCenterParented}`");
            builder.AppendLine($"- reference clip: `{(_referenceClip != null ? _referenceClip.name : "")}`");
            builder.AppendLine($"- reference clip asset: `{EscapeMarkdown(_referenceClipAssetPath)}`");
            builder.AppendLine();

            SummaryFrameRoleDiagnostics frameRoleDiagnostics = BuildSummaryFrameRoleDiagnostics(
                ResolveSummaryTargetFrameCount(),
                ResolveFrameCount(CaptureMode.SubManualTestPrefab),
                ResolveFrameCount(CaptureMode.MainAuto));
            builder.AppendLine("## Frame Count Roles");
            builder.AppendLine();
            builder.AppendLine($"- ref target: `{frameRoleDiagnostics.reference_target_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.target_frame_count_role)})");
            builder.AppendLine($"- Sub_Manual baseline recorded frames: `{frameRoleDiagnostics.baseline_recorded_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.baseline_recorded_frame_count_role)})");
            builder.AppendLine($"- Main_Auto candidate recorded frames: `{frameRoleDiagnostics.candidate_recorded_frame_count}` ({EscapeMarkdown(frameRoleDiagnostics.candidate_recorded_frame_count_role)})");
            builder.AppendLine($"- metric basis: {EscapeMarkdown(frameRoleDiagnostics.frame_quality_metric_basis)}");
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

            SummarySampleOrderingDiagnostic[] sampleOrderingDiagnostics = BuildSampleOrderingDiagnostics();
            if (sampleOrderingDiagnostics.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Sample Ordering Diagnostics");
                builder.AppendLine();
                builder.AppendLine("| job | scene | rows | first reason | first recorderFrame | first engine frame | recorder span | engine span | first clip time | first grounding step | first step/max | first step at max | grounding clamp delta | grounding smooth delta | finish recorderFrame | finish engine frame |");
                builder.AppendLine("|---|---|---:|---|---:|---:|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|");
                foreach (SummarySampleOrderingDiagnostic diagnostic in sampleOrderingDiagnostics)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(diagnostic.job_mode)} | {EscapeMarkdown(diagnostic.scene_name)} | {diagnostic.metric_row_count} | " +
                        $"{EscapeMarkdown(diagnostic.first_metric_reason)} | {diagnostic.first_metric_recorder_frame} | " +
                        $"{diagnostic.first_metric_engine_frame_count} | {diagnostic.recording_metric_recorder_frame_span} | " +
                        $"{diagnostic.recording_metric_engine_frame_span} | {FormatQualityFloat(diagnostic.first_metric_animation_clip_time)} | " +
                        $"{FormatQualityFloat(diagnostic.first_metric_grounding_vertical_step_last)} | " +
                        $"{FormatQualityFloat(diagnostic.first_metric_grounding_vertical_step_to_max_ratio)} | " +
                        $"{diagnostic.first_metric_grounding_vertical_step_at_max_step} | " +
                        $"{diagnostic.recording_grounding_step_clamp_delta} | {diagnostic.recording_grounding_smoothed_delta} | " +
                        $"{diagnostic.finish_metric_recorder_frame} | {diagnostic.finish_metric_engine_frame_count} |");
                }
            }

            MotionComparisonFrameQualitySummary[] frameQualitySummaries = BuildFrameQualitySummaries();
            SummaryCandidateArtifactSelection selectedCandidate = BuildCandidateArtifactSelection(frameQualitySummaries);
            if (selectedCandidate != null && !string.IsNullOrWhiteSpace(selectedCandidate.selected_candidate_vmd_path))
            {
                builder.AppendLine();
                builder.AppendLine("## Selected Candidate Artifact");
                builder.AppendLine();
                builder.AppendLine("| selected role | output role | status | acceptance artifact | metrics | vmd | manifest | files | raw status | corrected status | preserves raw diagnostic | basis |");
                builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
                builder.AppendLine(
                    $"| {EscapeMarkdown(selectedCandidate.selected_candidate_role)} | {EscapeMarkdown(selectedCandidate.selected_candidate_output_role)} | " +
                    $"{EscapeMarkdown(selectedCandidate.selected_candidate_status)} | {selectedCandidate.selected_candidate_is_acceptance_artifact} | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_metrics_csv)}` | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_vmd_path)}` | " +
                    $"`{EscapeMarkdown(selectedCandidate.selected_candidate_manifest_path)}` | " +
                    $"vmd={selectedCandidate.selected_candidate_vmd_exists}, metrics={selectedCandidate.selected_candidate_metrics_exists}, manifest={selectedCandidate.selected_candidate_manifest_exists}, rawDiff={selectedCandidate.selected_candidate_differs_from_raw_vmd} | " +
                    $"{EscapeMarkdown(selectedCandidate.raw_candidate_status)} | {EscapeMarkdown(selectedCandidate.corrected_candidate_status)} | " +
                    $"{selectedCandidate.selected_candidate_preserves_raw_diagnostic} | " +
                    $"{EscapeMarkdown(selectedCandidate.selected_candidate_acceptance_basis)}; {EscapeMarkdown(selectedCandidate.selection_basis)} |");
            }

            if (frameQualitySummaries.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("## Frame Quality Gate");
                builder.AppendLine();
                builder.AppendLine("| baseline | candidate | evaluation | status | mmd | compared frames | foot min Y | root delta | center step | local foot IK min Y | effective foot IK min Y | metrics | mmd screenshot | mmd report | vmd | reason |");
                builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|---:|---:|---|---|---|---|---|---|");
                foreach (MotionComparisonFrameQualitySummary summary in frameQualitySummaries)
                {
                    builder.AppendLine(
                        $"| {EscapeMarkdown(summary.baseline_label)} | {EscapeMarkdown(summary.candidate_label)} | {EscapeMarkdown(summary.frame_quality_evaluation_role)} | {EscapeMarkdown(summary.status)} | " +
                        $"{EscapeMarkdown(summary.mmd_result_status)} | {summary.compared_frames} | {FormatQualityFloat(summary.min_candidate_foot_bottom_y)} | " +
                        $"{FormatQualityFloat(summary.max_same_frame_root_position_delta)} | {FormatQualityFloat(summary.max_candidate_vmd_center_step)} | " +
                        $"{FormatQualityFloat(summary.min_candidate_vmd_foot_ik_y)} | {FormatQualityFloat(summary.min_candidate_vmd_effective_foot_ik_y)} | " +
                        $"`{EscapeMarkdown(summary.candidate_metrics_csv)}` | " +
                        $"`{EscapeMarkdown(summary.mmd_after_play_screenshot_path)}` | `{EscapeMarkdown(summary.mmd_report_path)}` | " +
                        $"`{EscapeMarkdown(summary.candidate_vmd_path)}` | " +
                        $"{EscapeMarkdown(summary.status_reason)} |");
                }
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

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummaries()
        {
            CaptureResult baseline = Results.FirstOrDefault(result =>
                string.Equals(result.jobMode, CaptureMode.SubManualTestPrefab.ToString(), StringComparison.Ordinal));
            if (baseline == null)
            {
                return Array.Empty<MotionComparisonFrameQualitySummary>();
            }

            List<MotionComparisonFrameQualitySummary> frameQualitySummaries = new List<MotionComparisonFrameQualitySummary>();
            foreach (CaptureResult candidate in EnumerateMainSceneCandidates())
            {
                frameQualitySummaries.AddRange(BuildFrameQualitySummariesForCandidate(baseline, candidate));
            }

            foreach (MotionComparisonFrameQualitySummary frameQualitySummary in frameQualitySummaries)
            {
                MotionComparisonProbeReportWriter.AttachLatestMmdAutomationEvidence(
                    frameQualitySummary,
                    _projectRoot,
                    Path.Combine(_projectRoot, MmdAutomationRunsRelativePath));
            }

            return frameQualitySummaries.ToArray();
        }

        private static IEnumerable<CaptureResult> EnumerateMainSceneCandidates()
        {
            return Results.Where(result =>
                result != null &&
                IsMainSceneCandidateMode(result.jobMode) &&
                result.success);
        }

        private static bool IsMainSceneCandidateMode(string jobMode)
        {
            return string.Equals(jobMode, CaptureMode.MainRecording.ToString(), StringComparison.Ordinal) ||
                string.Equals(jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal);
        }

        private static MotionComparisonFrameQualitySummary[] BuildFrameQualitySummariesForCandidate(
            CaptureResult baseline,
            CaptureResult candidate)
        {
            if (baseline == null || candidate == null)
            {
                return Array.Empty<MotionComparisonFrameQualitySummary>();
            }

            ResolveShortCandidateVmdPath(candidate);
            MotionComparisonFrameQualitySummary summary = MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                baseline.jobDisplayName,
                ToAbsoluteProjectPath(baseline.comparisonMetricsCsvPath),
                candidate.jobDisplayName,
                ToAbsoluteProjectPath(candidate.comparisonMetricsCsvPath),
                ToAbsoluteProjectPath(candidate.vmdPath),
                baseline.frameCount,
                candidate.frameCount,
                ResolveSummaryTargetFrameCount());
            if (string.Equals(candidate.jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal) &&
                MotionComparisonProbeReportWriter.TryPromoteVerticalSolveCorrectedCandidateToPrimaryExport(
                    summary,
                    out VerticalSolvePrimaryExportPromotion promotion))
            {
                candidate.fileSizeBytes = promotion.promoted_vmd_bytes;
                summary = MotionComparisonProbeReportWriter.BuildFrameQualitySummary(
                    baseline.jobDisplayName,
                    ToAbsoluteProjectPath(baseline.comparisonMetricsCsvPath),
                    candidate.jobDisplayName,
                    ToAbsoluteProjectPath(candidate.comparisonMetricsCsvPath),
                    ToAbsoluteProjectPath(candidate.vmdPath),
                    baseline.frameCount,
                    candidate.frameCount,
                    ResolveSummaryTargetFrameCount());
                summary.frame_quality_evaluation_role = "main_auto_integrated_vertical_solve_metrics";
                summary.frame_quality_evaluation_basis =
                    "primary Main_Auto result paths after bounded vertical solve promotion; raw metrics/VMD were preserved as raw_vertical_solve_diagnostic artifacts";
                summary.vertical_solve_corrected_candidate_manifest_path = promotion.integrated_manifest_path;
            }
            else if (string.Equals(candidate.jobMode, CaptureMode.MainRecording.ToString(), StringComparison.Ordinal))
            {
                MotionComparisonProbeReportWriter.MarkIntentionalMovingRootStageMotion(summary);
            }

            MotionComparisonFrameQualitySummary[] summaries =
                MotionComparisonProbeReportWriter.BuildFrameQualityEvaluationEntries(summary);
            return summaries;
        }

        private static void ResolveShortCandidateVmdPath(CaptureResult candidate)
        {
            if (candidate == null ||
                !IsMainSceneCandidateMode(candidate.jobMode) ||
                string.Equals(candidate.jobMode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(_summaryDirectory))
            {
                return;
            }

            string sourceExtension = Path.GetExtension(candidate.vmdPath);
            if (string.IsNullOrWhiteSpace(sourceExtension))
            {
                sourceExtension = ".vmd";
            }

            string shortPath = Path.Combine(
                _summaryDirectory,
                BuildCandidateVmdEvidenceFileName(candidate.jobMode, sourceExtension));
            if (!File.Exists(shortPath))
            {
                return;
            }

            string currentAbsolutePath = ToAbsoluteProjectPath(candidate.vmdPath);
            if (string.Equals(currentAbsolutePath, shortPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            candidate.vmdPath = MakeProjectRelativePath(shortPath);
            candidate.fileSizeBytes = new FileInfo(shortPath).Length;
            SavePersistedState();
        }

        private static int ResolveFrameCount(CaptureMode mode)
        {
            CaptureResult result = Results.FirstOrDefault(captureResult =>
                string.Equals(captureResult.jobMode, mode.ToString(), StringComparison.Ordinal));
            return result != null ? result.frameCount : 0;
        }

        private static int ResolveMainAutoFrameCount()
        {
            return ResolveFrameCount(CaptureMode.MainAuto);
        }

        private static int ResolveSummaryTargetFrameCount()
        {
            return ResolveSummaryTargetFrameCount(
                ResolveReferenceMmdTargetFrameCount(
                    _fbxFileName,
                    _durationSeconds,
                    _targetFrameCount,
                    _referenceClip != null ? _referenceClip.length : 0f,
                    DefaultFrameRate),
                ResolveMainAutoFrameCount());
        }

        private static int ResolveSummaryTargetFrameCount(int referenceTargetFrameCount, int mainAutoFrameCount)
        {
            _ = mainAutoFrameCount;
            return Mathf.Max(0, referenceTargetFrameCount);
        }

        private static int ResolveReferenceMmdTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate)
        {
            if (TryResolveKnownMmdReferenceTargetFrameCount(
                    fbxFileName,
                    requestedDurationSeconds,
                    configuredTargetFrameCount,
                    referenceClipLengthSeconds,
                    recordingFrameRate,
                    out int referenceTargetFrameCount))
            {
                return referenceTargetFrameCount;
            }

            return Mathf.Max(0, configuredTargetFrameCount);
        }

        private static bool TryResolveKnownMmdReferenceTargetFrameCount(
            string fbxFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate,
            out int referenceTargetFrameCount)
        {
            referenceTargetFrameCount = 0;
            if (recordingFrameRate <= 0f ||
                float.IsNaN(recordingFrameRate) ||
                float.IsInfinity(recordingFrameRate) ||
                requestedDurationSeconds <= 0f ||
                float.IsNaN(requestedDurationSeconds) ||
                float.IsInfinity(requestedDurationSeconds) ||
                configuredTargetFrameCount <= 0 ||
                referenceClipLengthSeconds <= 0f ||
                float.IsNaN(referenceClipLengthSeconds) ||
                float.IsInfinity(referenceClipLengthSeconds))
            {
                return false;
            }

            string cleanBaseName = Path.GetFileNameWithoutExtension(fbxFileName ?? string.Empty);
            if (!string.Equals(cleanBaseName, SatisfactionReferenceOutputBaseName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            int knownReferenceFrameCount = SatisfactionReferenceMaxMmdFrame + 1;
            float knownReferenceDurationSeconds = knownReferenceFrameCount / recordingFrameRate;
            float frameToleranceSeconds = 0.5f / recordingFrameRate;
            bool clipCoversReference = referenceClipLengthSeconds + frameToleranceSeconds >= knownReferenceDurationSeconds;
            bool requestCoversReference = requestedDurationSeconds + frameToleranceSeconds >= knownReferenceDurationSeconds;
            bool configuredFramesCoverReference = configuredTargetFrameCount >= knownReferenceFrameCount;
            if (!clipCoversReference || !requestCoversReference || !configuredFramesCoverReference)
            {
                return false;
            }

            referenceTargetFrameCount = knownReferenceFrameCount;
            return true;
        }

        private static SummaryFrameRoleDiagnostics BuildSummaryFrameRoleDiagnostics(
            int referenceTargetFrameCount,
            int baselineRecordedFrameCount,
            int candidateRecordedFrameCount)
        {
            return new SummaryFrameRoleDiagnostics
            {
                reference_target_frame_count = Mathf.Max(0, referenceTargetFrameCount),
                baseline_recorded_frame_count = Mathf.Max(0, baselineRecordedFrameCount),
                candidate_recorded_frame_count = Mathf.Max(0, candidateRecordedFrameCount),
                baseline_frame_count_delta_from_reference_target = referenceTargetFrameCount > 0
                    ? baselineRecordedFrameCount - referenceTargetFrameCount
                    : 0,
                candidate_frame_count_delta_from_reference_target = referenceTargetFrameCount > 0
                    ? candidateRecordedFrameCount - referenceTargetFrameCount
                    : 0,
                target_frame_count_role = "ref_mmd_mp4 expected frame range for the full satisfaction_2 reference",
                baseline_recorded_frame_count_role = "Sub_Manual recorded comparison baseline; reported separately and not used as target_frame_count",
                candidate_recorded_frame_count_role = "Main_Auto candidate capture under test",
                frame_quality_metric_basis = "Unity pose metrics compare Sub_Manual and Main_Auto rows by recorderFrame; the ref_mmd_mp4 count is only the frame-count target",
                vmd_export_metric_basis = "VMD export spike and floor metrics are evaluated on the Main_Auto candidate VMD"
            };
        }

        private static SummaryCandidateArtifactSelection BuildCandidateArtifactSelection(
            MotionComparisonFrameQualitySummary[] frameQualitySummaries)
        {
            SummaryCandidateArtifactSelection selection = new SummaryCandidateArtifactSelection();
            if (frameQualitySummaries == null || frameQualitySummaries.Length == 0)
            {
                selection.selection_basis = "no frame_quality summary is available";
                return selection;
            }

            MotionComparisonFrameQualitySummary mainAutoIntegrated = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "main_auto_integrated_vertical_solve_metrics", StringComparison.Ordinal));
            MotionComparisonFrameQualitySummary raw = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (raw == null)
            {
                raw = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(summary.frame_quality_evaluation_role, "evaluation_candidate_metrics", StringComparison.Ordinal));
            }

            if (raw == null)
            {
                raw = mainAutoIntegrated;
            }

            if (raw == null)
            {
                raw = frameQualitySummaries.FirstOrDefault(summary => summary != null);
            }

            MotionComparisonFrameQualitySummary corrected = frameQualitySummaries.FirstOrDefault(summary =>
                summary != null &&
                string.Equals(summary.frame_quality_evaluation_role, "corrected_candidate_metrics", StringComparison.Ordinal) &&
                IsMainAutoSummary(summary));
            if (corrected == null)
            {
                corrected = frameQualitySummaries.FirstOrDefault(summary =>
                    summary != null &&
                    string.Equals(summary.frame_quality_evaluation_role, "corrected_candidate_metrics", StringComparison.Ordinal));
            }

            FillRawCandidateSelectionFields(selection, raw);
            FillCorrectedCandidateSelectionFields(selection, corrected);

            bool correctedPasses = corrected != null &&
                string.Equals(corrected.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(corrected.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(corrected.candidate_metrics_csv);
            bool integratedPrimaryPasses = mainAutoIntegrated != null &&
                string.Equals(mainAutoIntegrated.status, "pass", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(mainAutoIntegrated.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(mainAutoIntegrated.candidate_metrics_csv);
            if (integratedPrimaryPasses)
            {
                FillSelectedCandidateFields(selection, mainAutoIntegrated);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = true;
                FillSelectedCandidateAcceptanceEvidence(selection, raw, mainAutoIntegrated, mainAutoIntegrated.vertical_solve_corrected_candidate_manifest_path);
                selection.selection_basis =
                    "primary Main_Auto export paths passed after bounded vertical solve integration; raw diagnostic artifacts remain preserved";
                return selection;
            }

            if (correctedPasses)
            {
                FillSelectedCandidateFields(selection, corrected);
                selection.selected_candidate_output_role = "user_facing_export_artifact";
                selection.selected_candidate_preserves_raw_diagnostic = raw != null;
                FillSelectedCandidateAcceptanceEvidence(selection, raw, corrected, raw != null ? raw.vertical_solve_corrected_candidate_manifest_path : string.Empty);
                selection.selection_basis =
                    "corrected candidate passed frame-quality gates and is selected for user-facing export; raw candidate remains recorded for diagnostics";
                return selection;
            }

            if (raw != null)
            {
                FillSelectedCandidateFields(selection, raw);
                selection.selection_basis = corrected == null
                    ? "no corrected candidate is available; selected raw/evaluation candidate for diagnostics"
                    : "corrected candidate is not passing; selected raw/evaluation candidate for diagnostics";
            }

            return selection;
        }

        private static bool IsMainAutoSummary(MotionComparisonFrameQualitySummary summary)
        {
            return summary != null &&
                !string.IsNullOrWhiteSpace(summary.candidate_label) &&
                summary.candidate_label.IndexOf("Main_Auto", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void FillSelectedCandidateFields(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary summary)
        {
            if (selection == null || summary == null)
            {
                return;
            }

            selection.selected_candidate_role = summary.frame_quality_evaluation_role ?? string.Empty;
            selection.selected_candidate_status = summary.status ?? string.Empty;
            selection.selected_candidate_status_reason = summary.status_reason ?? string.Empty;
            selection.selected_candidate_metrics_csv = summary.candidate_metrics_csv ?? string.Empty;
            selection.selected_candidate_vmd_path = summary.candidate_vmd_path ?? string.Empty;
        }

        private static void FillSelectedCandidateAcceptanceEvidence(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary raw,
            MotionComparisonFrameQualitySummary selected,
            string selectedManifestPath)
        {
            if (selection == null || selected == null)
            {
                return;
            }

            selection.selected_candidate_manifest_path = selectedManifestPath ?? string.Empty;
            selection.selected_candidate_vmd_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(selection.selected_candidate_vmd_path);
            selection.selected_candidate_metrics_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_metrics_csv) &&
                File.Exists(selection.selected_candidate_metrics_csv);
            selection.selected_candidate_manifest_exists =
                !string.IsNullOrWhiteSpace(selection.selected_candidate_manifest_path) &&
                File.Exists(selection.selected_candidate_manifest_path);
            selection.selected_candidate_differs_from_raw_vmd =
                raw != null &&
                !string.IsNullOrWhiteSpace(raw.candidate_vmd_path) &&
                !string.IsNullOrWhiteSpace(selection.selected_candidate_vmd_path) &&
                File.Exists(raw.candidate_vmd_path) &&
                selection.selected_candidate_vmd_exists &&
                !PathsReferToSameFile(raw.candidate_vmd_path, selection.selected_candidate_vmd_path) &&
                FilesDiffer(raw.candidate_vmd_path, selection.selected_candidate_vmd_path);

            bool selectedPasses = string.Equals(selected.status, "pass", StringComparison.OrdinalIgnoreCase);
            bool selectedCorrectedArtifact = string.Equals(
                selected.frame_quality_evaluation_role,
                "corrected_candidate_metrics",
                StringComparison.Ordinal);
            bool selectedIntegratedPrimary = string.Equals(
                selected.frame_quality_evaluation_role,
                "main_auto_integrated_vertical_solve_metrics",
                StringComparison.Ordinal);
            bool hasRequiredFiles = selectedCorrectedArtifact
                ? selection.selected_candidate_vmd_exists &&
                  selection.selected_candidate_metrics_exists &&
                  selection.selected_candidate_manifest_exists &&
                  selection.selected_candidate_differs_from_raw_vmd
                : selectedIntegratedPrimary
                    ? selection.selected_candidate_vmd_exists &&
                      selection.selected_candidate_metrics_exists &&
                      selection.selected_candidate_manifest_exists
                    : selection.selected_candidate_vmd_exists && selection.selected_candidate_metrics_exists;
            selection.selected_candidate_is_acceptance_artifact =
                selectedPasses &&
                (selectedCorrectedArtifact || selectedIntegratedPrimary) &&
                string.Equals(selection.selected_candidate_output_role, "user_facing_export_artifact", StringComparison.Ordinal) &&
                selection.selected_candidate_preserves_raw_diagnostic &&
                hasRequiredFiles;
            selection.selected_candidate_acceptance_basis = selection.selected_candidate_is_acceptance_artifact
                ? selectedIntegratedPrimary
                    ? "selected primary Main_Auto export VMD/metrics/manifest is the final acceptance/export candidate; raw diagnostic files remain preserved"
                    : "selected corrected VMD/metrics/manifest is the final acceptance/export candidate; raw candidate remains diagnostic"
                : "selected candidate is not a final acceptance/export artifact yet; raw candidate remains the diagnostic baseline";
        }

        private static bool PathsReferToSameFile(string leftPath, string rightPath)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(leftPath),
                    Path.GetFullPath(rightPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool FilesDiffer(string leftPath, string rightPath)
        {
            FileInfo left = new FileInfo(leftPath);
            FileInfo right = new FileInfo(rightPath);
            if (left.Length != right.Length)
            {
                return true;
            }

            byte[] leftBytes = File.ReadAllBytes(leftPath);
            byte[] rightBytes = File.ReadAllBytes(rightPath);
            if (leftBytes.Length != rightBytes.Length)
            {
                return true;
            }

            for (int i = 0; i < leftBytes.Length; i++)
            {
                if (leftBytes[i] != rightBytes[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static void FillRawCandidateSelectionFields(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary raw)
        {
            if (selection == null || raw == null)
            {
                return;
            }

            selection.raw_candidate_status = raw.status ?? string.Empty;
            selection.raw_candidate_status_reason = raw.status_reason ?? string.Empty;
            selection.raw_candidate_metrics_csv = raw.candidate_metrics_csv ?? string.Empty;
            selection.raw_candidate_vmd_path = raw.candidate_vmd_path ?? string.Empty;
        }

        private static void FillCorrectedCandidateSelectionFields(
            SummaryCandidateArtifactSelection selection,
            MotionComparisonFrameQualitySummary corrected)
        {
            if (selection == null || corrected == null)
            {
                return;
            }

            selection.corrected_candidate_status = corrected.status ?? string.Empty;
            selection.corrected_candidate_status_reason = corrected.status_reason ?? string.Empty;
            selection.corrected_candidate_metrics_csv = corrected.candidate_metrics_csv ?? string.Empty;
            selection.corrected_candidate_vmd_path = corrected.candidate_vmd_path ?? string.Empty;
        }

        private static SummarySampleOrderingDiagnostic[] BuildSampleOrderingDiagnostics()
        {
            return Results
                .Select(result => BuildSampleOrderingDiagnostic(
                    result.jobMode,
                    result.sceneName,
                    result.comparisonMetricsCsvPath))
                .ToArray();
        }

        private static SummarySampleOrderingDiagnostic BuildSampleOrderingDiagnostic(
            string jobMode,
            string sceneName,
            string metricsCsvPath)
        {
            SummarySampleOrderingDiagnostic diagnostic = new SummarySampleOrderingDiagnostic
            {
                job_mode = jobMode ?? string.Empty,
                scene_name = sceneName ?? string.Empty,
                metrics_csv = metricsCsvPath ?? string.Empty
            };

            string absolutePath = ToAbsoluteProjectPath(metricsCsvPath);
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return diagnostic;
            }

            string[] lines = File.ReadAllLines(absolutePath, Encoding.UTF8);
            if (lines.Length <= 1)
            {
                return diagnostic;
            }

            string[] headers = SplitSimpleCsvLine(lines[0]);
            Dictionary<string, int> indices = BuildCsvIndexMap(headers);
            List<string[]> rows = new List<string[]>();
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    continue;
                }

                rows.Add(SplitSimpleCsvLine(lines[lineIndex]));
            }

            diagnostic.metric_row_count = rows.Count;
            if (rows.Count == 0)
            {
                return diagnostic;
            }

            string[] first = rows[0];
            string[] finish = rows.LastOrDefault(row =>
                string.Equals(GetCsvString(row, indices, "reason"), "finish", StringComparison.OrdinalIgnoreCase))
                ?? rows[rows.Count - 1];

            diagnostic.first_metric_reason = GetCsvString(first, indices, "reason");
            diagnostic.first_metric_recorder_frame = GetCsvInt(first, indices, "recorderFrame");
            diagnostic.first_metric_engine_frame_count = GetCsvInt(first, indices, "frameCount");
            diagnostic.first_metric_time_since_level_load = GetCsvFloat(first, indices, "timeSinceLevelLoad");
            diagnostic.first_metric_animation_clip_time = GetCsvFloat(first, indices, "animationClipTime");
            diagnostic.first_metric_grounding_vertical_step_last = GetCsvFloat(first, indices, "retargetGroundingVerticalStepLast");
            diagnostic.first_metric_grounding_initial_vertical_step = GetCsvFloat(first, indices, "retargetGroundingInitialVerticalStep");
            diagnostic.first_metric_grounding_step_clamp_count = GetCsvInt(first, indices, "retargetGroundingStepClampCount");
            diagnostic.first_metric_grounding_smoothed_count = GetCsvInt(first, indices, "retargetGroundingSmoothedCount");
            diagnostic.first_metric_grounding_max_step_per_frame = GetCsvFloat(first, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.first_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                first,
                indices,
                diagnostic.first_metric_grounding_vertical_step_last,
                diagnostic.first_metric_grounding_max_step_per_frame);
            diagnostic.first_metric_grounding_vertical_step_at_max_step =
                IsGroundingVerticalStepAtMax(diagnostic.first_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.finish_metric_reason = GetCsvString(finish, indices, "reason");
            diagnostic.finish_metric_recorder_frame = GetCsvInt(finish, indices, "recorderFrame");
            diagnostic.finish_metric_engine_frame_count = GetCsvInt(finish, indices, "frameCount");
            diagnostic.finish_metric_time_since_level_load = GetCsvFloat(finish, indices, "timeSinceLevelLoad");
            diagnostic.finish_metric_animation_clip_time = GetCsvFloat(finish, indices, "animationClipTime");
            diagnostic.finish_metric_grounding_vertical_step_last = GetCsvFloat(finish, indices, "retargetGroundingVerticalStepLast");
            diagnostic.finish_metric_grounding_step_clamp_count = GetCsvInt(finish, indices, "retargetGroundingStepClampCount");
            diagnostic.finish_metric_grounding_smoothed_count = GetCsvInt(finish, indices, "retargetGroundingSmoothedCount");
            diagnostic.finish_metric_grounding_max_step_per_frame = GetCsvFloat(finish, indices, "retargetGroundingMaxStepPerFrame");
            diagnostic.finish_metric_grounding_vertical_step_to_max_ratio = ResolveGroundingStepToMaxRatio(
                finish,
                indices,
                diagnostic.finish_metric_grounding_vertical_step_last,
                diagnostic.finish_metric_grounding_max_step_per_frame);
            diagnostic.finish_metric_grounding_vertical_step_at_max_step =
                IsGroundingVerticalStepAtMax(diagnostic.finish_metric_grounding_vertical_step_to_max_ratio);
            diagnostic.recording_metric_recorder_frame_span = CalculateMetricIntSpan(
                diagnostic.first_metric_recorder_frame,
                diagnostic.finish_metric_recorder_frame);
            diagnostic.recording_metric_engine_frame_span = CalculateMetricIntSpan(
                diagnostic.first_metric_engine_frame_count,
                diagnostic.finish_metric_engine_frame_count);
            diagnostic.recording_metric_time_since_level_load_span = CalculateMetricFloatSpan(
                diagnostic.first_metric_time_since_level_load,
                diagnostic.finish_metric_time_since_level_load);
            diagnostic.recording_grounding_step_clamp_delta = CalculateMetricIntSpan(
                diagnostic.first_metric_grounding_step_clamp_count,
                diagnostic.finish_metric_grounding_step_clamp_count);
            diagnostic.recording_grounding_smoothed_delta = CalculateMetricIntSpan(
                diagnostic.first_metric_grounding_smoothed_count,
                diagnostic.finish_metric_grounding_smoothed_count);
            diagnostic.recording_phase_span_role =
                "finish-first recording phase metrics; absolute first engine frame includes scene load/import/prewarm startup offset and can vary between Unity batch runs";
            diagnostic.grounding_step_limit_role =
                "prewarm residual is identified by the first recorder-frame grounding step reaching its configured max; recording clamp/smoothed deltas are finish-first counters inside the captured phase";
            return diagnostic;
        }

        private static float ResolveGroundingStepToMaxRatio(
            string[] row,
            Dictionary<string, int> indices,
            float step,
            float maxStep)
        {
            float reportedRatio = GetCsvFloat(row, indices, "retargetGroundingLastStepToMaxStepRatio");
            if (!float.IsNaN(reportedRatio) && !float.IsInfinity(reportedRatio))
            {
                return reportedRatio;
            }

            if (float.IsNaN(step) ||
                float.IsInfinity(step) ||
                float.IsNaN(maxStep) ||
                float.IsInfinity(maxStep) ||
                maxStep <= 0f)
            {
                return float.NaN;
            }

            return Mathf.Abs(step) / maxStep;
        }

        private static bool IsGroundingVerticalStepAtMax(float stepToMaxRatio)
        {
            return !float.IsNaN(stepToMaxRatio) &&
                !float.IsInfinity(stepToMaxRatio) &&
                stepToMaxRatio >= 0.95f;
        }

        private static int CalculateMetricIntSpan(int first, int finish)
        {
            if (first < 0 || finish < 0)
            {
                return -1;
            }

            return finish - first;
        }

        private static float CalculateMetricFloatSpan(float first, float finish)
        {
            if (float.IsNaN(first) ||
                float.IsNaN(finish) ||
                float.IsInfinity(first) ||
                float.IsInfinity(finish))
            {
                return float.NaN;
            }

            return finish - first;
        }

        private static string[] SplitSimpleCsvLine(string line)
        {
            return (line ?? string.Empty).Split(',');
        }

        private static Dictionary<string, int> BuildCsvIndexMap(string[] headers)
        {
            Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
            {
                if (!indices.ContainsKey(headers[index]))
                {
                    indices.Add(headers[index], index);
                }
            }

            return indices;
        }

        private static string GetCsvString(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            if (row == null ||
                indices == null ||
                string.IsNullOrEmpty(column) ||
                !indices.TryGetValue(column, out int index) ||
                index < 0 ||
                index >= row.Length)
            {
                return string.Empty;
            }

            return row[index] ?? string.Empty;
        }

        private static int GetCsvInt(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return int.TryParse(
                GetCsvString(row, indices, column),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : 0;
        }

        private static float GetCsvFloat(
            string[] row,
            Dictionary<string, int> indices,
            string column)
        {
            return float.TryParse(
                GetCsvString(row, indices, column),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : float.NaN;
        }

        private static string ToAbsoluteProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string normalized = path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(_projectRoot, normalized);
        }

        private static string FormatQualityFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? "n/a"
                : value.ToString("0.######", CultureInfo.InvariantCulture);
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

        private static string ResolveReferenceClipAssetPath(string fbxFileName, Func<string, bool> hasReferenceClip)
        {
            string normalizedFileName = NormalizeFbxFileName(fbxFileName);
            string importCandidate = Path.Combine(ImportFbxDirectory, normalizedFileName).Replace('\\', '/');
            if (hasReferenceClip(importCandidate))
            {
                return importCandidate;
            }

            string projectCandidate = Path.Combine(ProjectFbxDirectory, normalizedFileName).Replace('\\', '/');
            if (hasReferenceClip(projectCandidate))
            {
                return projectCandidate;
            }

            return importCandidate;
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

        private static string BuildSafeSummarySessionId(string sessionId)
        {
            string safeSessionId = SanitizeFileName(sessionId);
            string rootFolder = Path.Combine(_projectRoot, OutputRootDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int leafFileNameLength = Mathf.Max(SummaryJsonFileName.Length, SummaryMarkdownFileName.Length);
            int maxSessionIdLength = EvidenceSafeMaxFullPathLength
                                     - rootFolder.Length
                                     - 1
                                     - 1
                                     - leafFileNameLength;
            maxSessionIdLength = Mathf.Max(16, maxSessionIdLength);
            return ShortenFileNameToLength(safeSessionId, maxSessionIdLength);
        }

        private static string SanitizeFileName(string fileName)
        {
            string safeName = string.IsNullOrWhiteSpace(fileName) ? "yyb_visual_compare" : fileName.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalidChar, '_');
            }

            return safeName.Replace(' ', '_');
        }

        private static string BuildCandidateVmdEvidenceFileName(CaptureMode mode, string extension)
        {
            return BuildCandidateVmdEvidenceFileName(mode.ToString(), extension);
        }

        private static string BuildCandidateVmdEvidenceFileName(string mode, string extension)
        {
            string safeExtension = string.IsNullOrWhiteSpace(extension) ? ".vmd" : extension;
            string shortMode = string.Equals(mode, CaptureMode.MainRecording.ToString(), StringComparison.Ordinal)
                ? "rec"
                : string.Equals(mode, CaptureMode.MainAuto.ToString(), StringComparison.Ordinal)
                    ? "auto"
                    : SanitizeFileName(mode);
            return $"vmd-{shortMode}{safeExtension}";
        }

        private static string ShortenFileNameToLength(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            int safeMaxLength = Mathf.Max(10, maxLength);
            if (value.Length <= safeMaxLength)
            {
                return value;
            }

            const int hashLength = 8;
            int prefixLength = Mathf.Max(1, safeMaxLength - hashLength - 1);
            return $"{value.Substring(0, prefixLength)}_{CalculateStableHash(value):x8}";
        }

        private static uint CalculateStableHash(string value)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            uint hash = offsetBasis;
            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
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
            public bool recorder_parent_ik_offsets_when_center_parented;
            public string reference_clip_name;
            public string reference_clip_asset_path;
            public CaptureResult[] results;
            public SummaryFrameRoleDiagnostics frame_count_roles;
            public SummarySampleOrderingDiagnostic[] sample_ordering_diagnostics;
            public SummaryCandidateArtifactSelection selected_candidate_artifact;
            public MotionComparisonFrameQualitySummary[] frame_quality_summaries;
            public string[] failures;
        }

        [Serializable]
        private sealed class SummaryCandidateArtifactSelection
        {
            public string selected_candidate_role;
            public string selected_candidate_output_role;
            public string selected_candidate_status;
            public string selected_candidate_status_reason;
            public string selected_candidate_metrics_csv;
            public string selected_candidate_vmd_path;
            public bool selected_candidate_preserves_raw_diagnostic;
            public string selected_candidate_manifest_path;
            public bool selected_candidate_vmd_exists;
            public bool selected_candidate_metrics_exists;
            public bool selected_candidate_manifest_exists;
            public bool selected_candidate_differs_from_raw_vmd;
            public bool selected_candidate_is_acceptance_artifact;
            public string selected_candidate_acceptance_basis;
            public string raw_candidate_status;
            public string raw_candidate_status_reason;
            public string raw_candidate_metrics_csv;
            public string raw_candidate_vmd_path;
            public string corrected_candidate_status;
            public string corrected_candidate_status_reason;
            public string corrected_candidate_metrics_csv;
            public string corrected_candidate_vmd_path;
            public string selection_basis;
        }

        [Serializable]
        private sealed class SummaryFrameRoleDiagnostics
        {
            public int reference_target_frame_count;
            public int baseline_recorded_frame_count;
            public int candidate_recorded_frame_count;
            public int baseline_frame_count_delta_from_reference_target;
            public int candidate_frame_count_delta_from_reference_target;
            public string target_frame_count_role;
            public string baseline_recorded_frame_count_role;
            public string candidate_recorded_frame_count_role;
            public string frame_quality_metric_basis;
            public string vmd_export_metric_basis;
        }

        [Serializable]
        private sealed class SummarySampleOrderingDiagnostic
        {
            public string job_mode;
            public string scene_name;
            public string metrics_csv;
            public int metric_row_count;
            public string first_metric_reason;
            public int first_metric_recorder_frame;
            public int first_metric_engine_frame_count;
            public float first_metric_time_since_level_load;
            public float first_metric_animation_clip_time;
            public float first_metric_grounding_vertical_step_last;
            public float first_metric_grounding_initial_vertical_step;
            public int first_metric_grounding_step_clamp_count;
            public int first_metric_grounding_smoothed_count;
            public float first_metric_grounding_max_step_per_frame;
            public float first_metric_grounding_vertical_step_to_max_ratio;
            public bool first_metric_grounding_vertical_step_at_max_step;
            public string finish_metric_reason;
            public int finish_metric_recorder_frame;
            public int finish_metric_engine_frame_count;
            public float finish_metric_time_since_level_load;
            public float finish_metric_animation_clip_time;
            public float finish_metric_grounding_vertical_step_last;
            public int finish_metric_grounding_step_clamp_count;
            public int finish_metric_grounding_smoothed_count;
            public float finish_metric_grounding_max_step_per_frame;
            public float finish_metric_grounding_vertical_step_to_max_ratio;
            public bool finish_metric_grounding_vertical_step_at_max_step;
            public int recording_metric_recorder_frame_span;
            public int recording_metric_engine_frame_span;
            public float recording_metric_time_since_level_load_span;
            public int recording_grounding_step_clamp_delta;
            public int recording_grounding_smoothed_delta;
            public string recording_phase_span_role;
            public string grounding_step_limit_role;
        }
    }
}
#endif
