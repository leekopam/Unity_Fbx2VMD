
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Fbx2Vmd.FBXImporter;
using UniGLTF;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRM;

namespace Fbx2Vmd.FBXImporter
{
    [InitializeOnLoad]
    public static class FbxPlaybackSmokeRunner
    {
        private const string MenuRoot = "Machine Spirit/FBX Smoke/";
        private const string MainAutoSceneName = "Main_Auto";
        private const string MainAutoScenePath = "Assets/_Project/Scene/Main_Auto.unity";
        private const string E2eModelName = "YYB Hatsune Miku";
        private const string MainRecordingSceneName = "Main_recoding";
        private const string ImportFbxRelativeDirectory = "Resources/Import_FBX";
        private const string RunAllImportFbxHeadCommand = "run_all_import_fbx_31s";
        private const string RunAllImportFbxMiddleCommand = "run_all_import_fbx_middle_31s";
        private const string RunAllImportFbxTailCommand = "run_all_import_fbx_tail_31s";
        private const string CaptureSatisfactionQuickVmdSmokeCommand = "capture_satisfaction_quick_vmd_smoke_2s";
        private const string CapturePreselectionStateCommand = "capture_preselection_state";
        private const string CapturePlaybackSeekEvidenceCommand = "capture_playback_seek_evidence";
        private const string CaptureTetorisLiveFootEvidenceCommand = "capture_tetoris_live_foot_evidence";
        private const string CaptureSatisfactionFullClipMetricsCommand = "capture_satisfaction_full_clip_metrics";
        private const string CaptureTetorisTestPrefabFullClipCommand = "capture_tetoris_testprefab_full_clip_metrics";
        private const string CaptureVrmBaselineCommand = "capture_vrm_mmd_head_metrics";
        private const string CaptureVrmLoadedCommand = "capture_vrm_univrm_head_metrics";
        private const string CaptureProductUiFlowCommand = "capture_product_ui_flow";
        private const string CaptureInvalidInputEvidenceCommand = "capture_invalid_input_evidence";
        private const string CaptureE2eEnvironmentCommand = "capture_e2e_environment";
        private const string EnterE2ePlayCommand = "enter_e2e_play";
        private const string ExitE2ePlayCommand = "exit_e2e_play";
        private const string CaptureSatisfactionThumbEvidenceCommand = "capture_satisfaction_thumb_evidence_14s";
        private const string CaptureSatisfactionFullRegressionEvidenceCommand = "capture_satisfaction_full_regression_evidence_208s_4k";
        private const string CaptureSatisfactionFullNamedVmdCommand = "capture_satisfaction_full_named_vmd";
        private const string CaptureSatisfactionVrmCommand = "capture_satisfaction_vrm";
        private const string CaptureSatisfactionHead31Command = "capture_satisfaction_head_31s";
        private const string CaptureSatisfactionMiddle31Command = "capture_satisfaction_middle_31s";
        private const string CaptureSatisfactionTail31Command = "capture_satisfaction_tail_31s";
        private const string CaptureAntennaTailHelperEvidenceCommand = "capture_antenna_tail_helper_evidence";
        private const string CaptureAntennaTailHelperEvidenceCleanCommand = "capture_antenna_tail_helper_evidence_clean";
        private const string CaptureAntennaTailHelperEvidenceResumeAfterCleanCommand = "capture_antenna_tail_helper_evidence_resume_after_clean";
        private const string SatisfactionFbxFileName = "satisfaction_2.fbx";
        private const float QuickVmdSmokeDurationSeconds = 2f;
        private const int QuickVmdSmokeTargetFrameCount = 60;
        private const float SmokeDurationSeconds = 31f;
        private const float SmokeFrameRate = 30f;
        private const float SmokeStartDelaySeconds = 0.2f;
        private const float SatisfactionFullRegressionEvidenceDurationSeconds = 207.7833f;
        private const float FullRegressionEvidenceRecordingStartTimeOverrideSeconds = 0f;
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
        private static readonly FbxPlaybackSmokeAutomationStore AutomationStore;
        private static FBXVmdPipeline _batchFBXVmdPipeline;
        private static FBXVmdPipeline _singleFBXVmdPipeline;
        private static string _activeSingleFbxFileName;
        private static string _singleSmokeMode;
        private static string _activeBatchFbxFileName;
        private static int _batchTotalCount;
        private static FBXVmdPipeline.EditorDiagnosticSmokeSegment _batchSegment = FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head;
        private static string _activeAutomationRequestId;
        private static string _activeAutomationCommand;
        private static string _activeAutomationRequestedCommand;
        private static DateTime _nextAutomationPollUtc = DateTime.MinValue;
        private static DateTime _preselectionStartedUtc;
        private static string _preselectionCapturePath;
        private static string _preselectionStatePath;
        private static Transform[] _preselectionBones;
        private static Vector3[] _preselectionBonePositions;
        private static FBXVmdPipeline _preselectionPipeline;
        private enum PlaybackEvidencePhase
        {
            None,
            Importing,
            Preparing,
            Playing,
            FirstCapture,
            SecondCapture,
            RepeatCapture,
            FootCaptures,
            Resumed
        }

        private static readonly int[] FootEvidenceFrames =
        {
            0, 165, 166, 167, 543, 544, 545, 789, 790, 791,
            1323, 1324, 1325, 1326, 1327, 1328, 1329, 1330, 1331, 1332, 1333,
            2404, 2405, 2406, 8019, 8020, 8021, 11615, 11616, 11617
        };
        private static readonly HumanBodyBones[] F11Bones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot, HumanBodyBones.RightToes
        };

        private static PlaybackEvidencePhase _playbackEvidencePhase;
        private static FBXVmdPipeline _playbackEvidencePipeline;
        private static PlaybackEvidence _playbackEvidence;
        private static DateTime _playbackEvidenceStartedUtc;
        private static DateTime _playbackCaptureStartedUtc;
        private static string _playbackEvidencePath;
        private static string _playbackCapturePath;
        private static FbxFootLiveEvidenceCapture _footLiveEvidence;
        private static FbxFullClipFootMetricsCapture _fullClipMetrics;
        private static FbxProductUiFlowCapture _productUiFlow;
        private static FBXVmdPipeline _alternateModelPipeline;
        private static GameObject _originalModel;
        private static GameObject _alternateModel;
        private static bool _originalModelWasActive;
        private static bool _alternateModelWasActive;
        private static Task<RuntimeGltfInstance> _vrmLoadTask;
        private static FbxPlaybackSmokeAutomationRequest _vrmLoadRequest;
        private static FBXVmdPipeline _vrmLoadPipeline;
        private static GameObject _loadedVrmRoot;
        private static string _vrmImportCapturePath;
        private static int _footEvidenceFrameIndex;
        private static readonly List<string> PlaybackStageLog = new List<string>();
        private enum InvalidInputPhase { None, MissingPath, InvalidAvatar }
        private static InvalidInputPhase _invalidInputPhase;
        private static FBXVmdPipeline _invalidInputPipeline;
        private static Animator _invalidInputAnimator;
        private static Avatar _originalAvatar;
        private static InvalidInputEvidence _invalidInputEvidence;
        private static string _invalidInputEvidencePath;
        private static DateTime _invalidInputStartedUtc;

        static FbxPlaybackSmokeRunner()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            AutomationStore = new FbxPlaybackSmokeAutomationStore(projectRoot);
            EditorApplication.update -= PollAutomationRequest;
            EditorApplication.update += PollAutomationRequest;
        }

        [MenuItem(MenuRoot + "Run All Import_FBX 31s", false, 2090)]
        private static void RunAllImportFbxSmoke()
        {
            if (!TryGetFBXVmdPipeline(null, out FBXVmdPipeline fileManager))
            {
                return;
            }

            string[] fbxFileNames = GetImportFbxFileNames();
            if (fbxFileNames.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Import_FBX 폴더에 FBX 파일이 없습니다.", "확인");
                return;
            }

            StartSmokeBatch(fileManager, fbxFileNames, FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head);
        }

        [MenuItem(MenuRoot + "Run All Import_FBX Middle 31s", false, 2091)]
        private static void RunAllImportFbxMiddleSmoke()
        {
            if (!TryGetFBXVmdPipeline(null, out FBXVmdPipeline fileManager))
            {
                return;
            }

            string[] fbxFileNames = GetImportFbxFileNames();
            if (fbxFileNames.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Import_FBX 폴더에 FBX 파일이 없습니다.", "확인");
                return;
            }

            StartSmokeBatch(fileManager, fbxFileNames, FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle);
        }

        [MenuItem(MenuRoot + "Run All Import_FBX Tail 31s", false, 2092)]
        private static void RunAllImportFbxTailSmoke()
        {
            if (!TryGetFBXVmdPipeline(null, out FBXVmdPipeline fileManager))
            {
                return;
            }

            string[] fbxFileNames = GetImportFbxFileNames();
            if (fbxFileNames.Length == 0)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Import_FBX 폴더에 FBX 파일이 없습니다.", "확인");
                return;
            }

            StartSmokeBatch(fileManager, fbxFileNames, FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail);
        }

        [MenuItem(MenuRoot + "Run satisfaction_2 Quick VMD Smoke 2s", false, 2099)]
        private static void RunSatisfactionQuickVmdSmoke()
        {
            RunSingleSmoke(
                SatisfactionFbxFileName,
                QuickVmdSmokeDurationSeconds,
                enableFingerCloseups: false,
                sampleTimesOverride: null,
                mode: "quick-vmd-smoke");
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
                segment: FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
                captureWidthOverride: FullRegressionEvidenceCaptureWidth,
                captureHeightOverride: FullRegressionEvidenceCaptureHeight,
                recordingStartTimeOverrideSeconds: FullRegressionEvidenceRecordingStartTimeOverrideSeconds);
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
                segment: FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle);
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
                segment: FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail);
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
                segment: FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle);
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
                segment: FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail);
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
                segment: FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail);
        }

        [MenuItem(MenuRoot + "Dump Active FBXVmdPipeline Probe", false, 2112)]
        private static void DumpActiveFBXVmdPipelineProbe()
        {
            if (!ValidateRuntimeContext(null, interactive: true, out _))
            {
                return;
            }

            FBXVmdPipeline runtimeFBXVmdPipeline = FindRuntimeFBXVmdPipeline();
            string fileManagerState = runtimeFBXVmdPipeline != null
                ? $"found={GetHierarchyPath(runtimeFBXVmdPipeline.transform)}"
                : "found=<none>";
            Debug.Log($"[FbxPlaybackSmokeRunner] FBXVmdPipeline probe: {BuildMainAutoRuntimeSummary()}, runtimeLookup={fileManagerState}, thumbReference[{BuildRetargeterThumbReferenceSummary(runtimeFBXVmdPipeline)}]");
        }

        [MenuItem(MenuRoot + "Run All Import_FBX 31s", true)]
        [MenuItem(MenuRoot + "Run All Import_FBX Middle 31s", true)]
        [MenuItem(MenuRoot + "Run All Import_FBX Tail 31s", true)]
        [MenuItem(MenuRoot + "Run satisfaction_2 31s", true)]
        [MenuItem(MenuRoot + "Run Antenna39 31s", true)]
        [MenuItem(MenuRoot + "Run Snake 31s", true)]
        [MenuItem(MenuRoot + "Run mikumikuni_retake_000 31s", true)]
        [MenuItem(MenuRoot + "Run neo_1_001 31s", true)]
        [MenuItem(MenuRoot + "Run satisfaction_2 Quick VMD Smoke 2s", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Thumb Evidence 0.6s+1.0s+12.6s+13.1s", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Full Regression Evidence 208s 4K", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Middle Helper Evidence 102.125s", true)]
        [MenuItem(MenuRoot + "Capture satisfaction_2 Tail Helper Evidence 181.25s", true)]
        [MenuItem(MenuRoot + "Capture neo_1_001 Middle Helper Evidence 98.575s", true)]
        [MenuItem(MenuRoot + "Capture neo_1_001 Tail Helper Evidence 183.85s", true)]
        [MenuItem(MenuRoot + "Capture Antenna39 Tail Helper Evidence 188.567s", true)]
        [MenuItem(MenuRoot + "Dump Active FBXVmdPipeline Probe", true)]
        private static bool ValidateSmokeMenu()
        {
            return EditorApplication.isPlaying && !EditorApplication.isPaused && !EditorApplication.isCompiling;
        }

        private static void PollAutomationRequest()
        {
            if (_vrmLoadTask != null)
            {
                PollVrmLoading();
                return;
            }

            if (_productUiFlow != null)
            {
                PollProductUiFlow();
                return;
            }

            if (_fullClipMetrics != null)
            {
                PollFullClipMetrics();
                return;
            }

            if (_footLiveEvidence != null)
            {
                PollFootLiveEvidence();
                return;
            }

            if (_invalidInputPhase != InvalidInputPhase.None)
            {
                PollInvalidInputEvidence();
                return;
            }

            if (_playbackEvidencePhase != PlaybackEvidencePhase.None)
            {
                PollPlaybackEvidence();
                return;
            }

            if (!string.IsNullOrEmpty(_preselectionCapturePath))
            {
                PollPreselectionCapture();
                return;
            }

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

            if (!AutomationStore.HasPendingRequest)
            {
                return;
            }

            FbxPlaybackSmokeAutomationRequest request;
            try
            {
                request = AutomationStore.ReadRequest();
            }
            catch (Exception ex)
            {
                TraceAutomation($"request read failed: {ex.Message}");
                TryDeleteRequestFile();
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
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
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
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

            if (TryHandleE2eControlRequest(request))
            {
                return;
            }

            if (TryBootstrapCleanAutomationRequest(request))
            {
                return;
            }

            if (!TryStartAutomationRequest(request, out string startMessage))
            {
                TraceAutomation($"request start failed id={request.request_id} command={request.command} requested={request.requested_command} message={startMessage}");
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = request.requested_command ?? request.command,
                    message = startMessage,
                    failure_stage = "preflight",
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
            WriteStatus(new FbxPlaybackSmokeAutomationStatus
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

        [Serializable]
        private sealed class E2eEnvironmentEvidence
        {
            public bool play_mode;
            public string scene;
            public string scene_path;
            public bool scene_dirty;
            public string model_name;
            public bool model_active;
            public Vector3 model_position;
            public Quaternion model_rotation;
            public Vector3 model_scale;
            public string avatar_name;
            public bool avatar_valid;
            public bool is_processing;
            public bool has_prepared_motion;
            public bool is_playing_motion;
            public bool is_recording;
            public bool recorder_recording;
            public string recorder_output_name;
            public string recorder_last_saved_path;
            public int capture_framerate;
            public float time_scale;
        }

        private static bool TryHandleE2eControlRequest(FbxPlaybackSmokeAutomationRequest request)
        {
            bool isCapture = request.command == CaptureE2eEnvironmentCommand;
            bool isEnter = request.command == EnterE2ePlayCommand;
            bool isExit = request.command == ExitE2ePlayCommand;
            bool isVrmExport = request.command == CaptureSatisfactionVrmCommand;
            if (!isCapture && !isEnter && !isExit && !isVrmExport) return false;

            try
            {
                if (isCapture)
                {
                    if (!Guid.TryParse(request.request_id, out Guid id))
                        throw new InvalidOperationException("환경 기록 요청 ID가 유효하지 않습니다.");

                    string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                    string directory = Path.Combine(projectRoot, "Docs", "Workflow", "Local",
                        "evidence", "boogle", "e2e-environment", id.ToString("D"));
                    Directory.CreateDirectory(directory);
                    string statePath = Path.Combine(directory, "state.json");
                    Scene scene = SceneManager.GetActiveScene();
                    FBXVmdPipeline pipeline = FindRuntimeFBXVmdPipeline();
                    GameObject model = pipeline != null ? pipeline.targetCharacter : null;
                    Animator animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
                    UnityHumanoidVMDRecorder recorder = model != null
                        ? model.GetComponentInChildren<UnityHumanoidVMDRecorder>(true) : null;
                    HumanoidSampleCode recordingControl = model != null
                        ? model.GetComponentInChildren<HumanoidSampleCode>(true) : null;
                    E2eEnvironmentEvidence state = new E2eEnvironmentEvidence
                    {
                        play_mode = EditorApplication.isPlaying,
                        scene = scene.name,
                        scene_path = scene.path,
                        scene_dirty = scene.isDirty,
                        model_name = model != null ? model.name : string.Empty,
                        model_active = model != null && model.activeSelf,
                        model_position = model != null ? model.transform.position : Vector3.zero,
                        model_rotation = model != null ? model.transform.rotation : Quaternion.identity,
                        model_scale = model != null ? model.transform.localScale : Vector3.zero,
                        avatar_name = animator != null && animator.avatar != null
                            ? animator.avatar.name : string.Empty,
                        avatar_valid = animator != null && animator.avatar != null &&
                            animator.avatar.isValid && animator.avatar.isHuman,
                        is_processing = pipeline != null && pipeline.IsProcessing,
                        has_prepared_motion = pipeline != null && pipeline.HasPreparedImportedMotion,
                        is_playing_motion = pipeline != null && pipeline.IsImportedMotionPlaying,
                        is_recording = pipeline != null && pipeline.IsImportedMotionRecording,
                        recorder_recording = recorder != null && recorder.IsRecording,
                        recorder_output_name = recordingControl != null
                            ? recordingControl.HumanoidVMDName : string.Empty,
                        recorder_last_saved_path = recordingControl != null
                            ? recordingControl.LastSavedFilePath : string.Empty,
                        capture_framerate = Time.captureFramerate,
                        time_scale = Time.timeScale
                    };
                    File.WriteAllText(statePath, JsonUtility.ToJson(state, true));
                    WriteStatus(new FbxPlaybackSmokeAutomationStatus
                    {
                        request_id = request.request_id,
                        status = "completed",
                        updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                        command = request.requested_command,
                        passed = true,
                        environment_state_path = statePath,
                        manifest_path = statePath,
                        failures = Array.Empty<string>()
                    });
                    TraceAutomation($"environment id={request.request_id} play={state.play_mode} scene={state.scene} recording={state.is_recording}");
                }
                else if (isVrmExport)
                {
                    Scene scene = SceneManager.GetActiveScene();
                    FBXVmdPipeline pipeline = FindRuntimeFBXVmdPipeline();
                    GameObject model = pipeline != null ? pipeline.targetCharacter : null;
                    Animator animator = model != null ? model.GetComponent<Animator>() : null;
                    if (EditorApplication.isPlaying || scene.path != MainAutoScenePath ||
                        scene.isDirty || model == null || model.name != E2eModelName ||
                        animator == null || animator.avatar == null ||
                        !animator.avatar.isValid || !animator.avatar.isHuman)
                        throw new InvalidOperationException("F12 VRM 대상 모델과 Edit Mode 상태가 유효하지 않습니다.");

                    GameObject exportModel = UnityEngine.Object.Instantiate(model);
                    byte[] bytes;
                    try
                    {
                        IKControl ikControl = exportModel.GetComponent<IKControl>();
                        if (ikControl != null) UnityEngine.Object.DestroyImmediate(ikControl);
                        bytes = YybVrmDiagnosticExporter.Export(exportModel);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(exportModel);
                    }
                    if (bytes == null || bytes.Length < 20)
                        throw new InvalidDataException("VRM 내보내기 결과가 비어 있습니다.");
                    string outputPath = Path.Combine(Application.dataPath,
                        "VMDRecorderSample", "satisfaction_2.vrm");
                    File.WriteAllBytes(outputPath, bytes);
                    WriteStatus(new FbxPlaybackSmokeAutomationStatus
                    {
                        request_id = request.request_id,
                        status = "completed",
                        updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                        command = request.requested_command,
                        passed = true,
                        vrm_output_path = outputPath,
                        vrm_file_size_bytes = bytes.LongLength,
                        failures = Array.Empty<string>()
                    });
                    TraceAutomation($"vrm-export id={request.request_id} bytes={bytes.LongLength}");
                }
                else
                {
                    bool targetPlay = isEnter;
                    Scene scene = SceneManager.GetActiveScene();
                    FBXVmdPipeline pipeline = FindRuntimeFBXVmdPipeline();
                    if (targetPlay && (scene.path != MainAutoScenePath || scene.isDirty ||
                        pipeline == null || pipeline.targetCharacter == null ||
                        pipeline.targetCharacter.name != E2eModelName))
                        throw new InvalidOperationException("Main_Auto 씬을 저장된 Edit 상태로 준비해야 합니다.");

                    if (EditorApplication.isPlaying != targetPlay)
                    {
                        WriteStatus(new FbxPlaybackSmokeAutomationStatus
                        {
                            request_id = request.request_id,
                            status = "running",
                            updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                            command = request.requested_command,
                            message = targetPlay ? "Play 진입 중" : "Play 종료 중",
                            passed = false,
                            failures = Array.Empty<string>()
                        });
                        EditorApplication.isPlaying = targetPlay;
                        return true;
                    }

                    WriteStatus(new FbxPlaybackSmokeAutomationStatus
                    {
                        request_id = request.request_id,
                        status = "completed",
                        updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                        command = request.requested_command,
                        passed = true,
                        failures = Array.Empty<string>()
                    });
                    TraceAutomation($"play-state id={request.request_id} play={targetPlay}");
                }
            }
            catch (Exception ex)
            {
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = request.requested_command,
                    failure_stage = isVrmExport ? "output" : "environment",
                    message = ex.Message,
                    passed = false,
                    failures = new[] { ex.Message }
                });
                TraceAutomation($"environment failed id={request.request_id} message={ex.Message}");
                if (isVrmExport) Debug.LogException(ex);
            }

            TryDeleteRequestFile();
            return true;
        }

        private static bool TryStartAutomationRequest(FbxPlaybackSmokeAutomationRequest request, out string message)
        {
            message = string.Empty;
            if (request == null || string.IsNullOrWhiteSpace(request.command))
            {
                message = "request command is missing";
                return false;
            }

            if (IsBatchRunning() || _singleFBXVmdPipeline != null || _vrmLoadTask != null)
            {
                message = "smoke runner is already active";
                return false;
            }

            switch (request.command)
            {
                case CapturePreselectionStateCommand:
                    return TryStartPreselectionCapture(request.request_id, out message);
                case CapturePlaybackSeekEvidenceCommand:
                    return TryStartPlaybackEvidence(request.request_id, out message);
                case CaptureTetorisLiveFootEvidenceCommand:
                    if (!TryGetFBXVmdPipeline("tetoris_001.fbx", out FBXVmdPipeline footPipeline,
                            interactive: false, out message)) return false;
                    return FbxFootLiveEvidenceCapture.TryStart(footPipeline, request.request_id,
                        request.run_id, out _footLiveEvidence, out message);
                case CaptureSatisfactionFullClipMetricsCommand:
                    if (!TryGetFBXVmdPipeline(SatisfactionFbxFileName, out FBXVmdPipeline fullPipeline,
                            interactive: false, out message)) return false;
                    return FbxFullClipFootMetricsCapture.TryStart(fullPipeline, request.request_id,
                        request.run_id, out _fullClipMetrics, out message);
                case CaptureTetorisTestPrefabFullClipCommand:
                    return TryStartTestPrefabFullClip(request, out message);
                case CaptureVrmBaselineCommand:
                    if (!TryGetFBXVmdPipeline(SatisfactionFbxFileName,
                            out FBXVmdPipeline vrmBaselinePipeline, interactive: false,
                            out message)) return false;
                    return FbxFullClipFootMetricsCapture.TryStart(vrmBaselinePipeline,
                        request.request_id, request.run_id, out _fullClipMetrics, out message,
                        SatisfactionFbxFileName, "VRM_MMD", frameLimit: 91, captureViews: true);
                case CaptureVrmLoadedCommand:
                    return TryStartVrmLoadedCapture(request, out message);
                case CaptureProductUiFlowCommand:
                    if (!TryGetFBXVmdPipeline(SatisfactionFbxFileName,
                            out FBXVmdPipeline uiPipeline, interactive: false,
                            out message)) return false;
                    return FbxProductUiFlowCapture.TryStart(uiPipeline, request.request_id,
                        request.run_id, out _productUiFlow, out message);
                case CaptureInvalidInputEvidenceCommand:
                    return TryStartInvalidInputEvidence(request.request_id, out message);
                case CaptureSatisfactionQuickVmdSmokeCommand:
                    return TryStartAutomationSingleSmoke(
                        SatisfactionFbxFileName,
                        QuickVmdSmokeDurationSeconds,
                        false,
                        null,
                        "quick-vmd-smoke",
                        FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
                        out message,
                        useInputBaseName: true);
                case CaptureSatisfactionThumbEvidenceCommand:
                    return TryStartAutomationSingleSmoke(
                        "satisfaction_2.fbx",
                        ThumbEvidenceDurationSeconds,
                        true,
                        ThumbEvidenceSampleTimes,
                        "thumb-evidence",
                        FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
                        out message);
                case CaptureSatisfactionFullRegressionEvidenceCommand:
                    return TryStartAutomationSingleSmoke(
                        "satisfaction_2.fbx",
                        SatisfactionFullRegressionEvidenceDurationSeconds,
                        true,
                        SatisfactionFullRegressionEvidenceSampleTimes,
                        "full-regression-evidence",
                        FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
                        out message,
                        FullRegressionEvidenceCaptureWidth,
                        FullRegressionEvidenceCaptureHeight,
                        FullRegressionEvidenceRecordingStartTimeOverrideSeconds);
                case CaptureSatisfactionFullNamedVmdCommand:
                    return TryStartAutomationSingleSmoke(
                        SatisfactionFbxFileName,
                        SatisfactionFullRegressionEvidenceDurationSeconds,
                        false,
                        null,
                        "full-named-vmd",
                        FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
                        out message,
                        recordingStartTimeOverrideSeconds: FullRegressionEvidenceRecordingStartTimeOverrideSeconds,
                        useInputBaseName: true);
                case CaptureSatisfactionHead31Command:
                case CaptureSatisfactionMiddle31Command:
                case CaptureSatisfactionTail31Command:
                    FBXVmdPipeline.EditorDiagnosticSmokeSegment segment =
                        request.command == CaptureSatisfactionMiddle31Command
                            ? FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle
                            : request.command == CaptureSatisfactionTail31Command
                                ? FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail
                                : FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head;
                    return TryStartAutomationSingleSmoke(
                        SatisfactionFbxFileName,
                        SmokeDurationSeconds,
                        false,
                        null,
                        "single",
                        segment,
                        out message);
                case CaptureAntennaTailHelperEvidenceCommand:
                    return TryStartAutomationSingleSmoke(
                        "Antenna39 try_006 g.fbx",
                        SmokeDurationSeconds,
                        true,
                        new[] { AntennaTailHelperEvidenceTimeSeconds },
                        "helper-evidence-tail",
                        FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail,
                        out message);
                case RunAllImportFbxHeadCommand:
                    return TryStartAutomationBatch(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head, out message);
                case RunAllImportFbxMiddleCommand:
                    return TryStartAutomationBatch(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Middle, out message);
                case RunAllImportFbxTailCommand:
                    return TryStartAutomationBatch(FBXVmdPipeline.EditorDiagnosticSmokeSegment.Tail, out message);
                default:
                    message = $"unsupported command: {request.command}";
                    return false;
            }
        }

        [Serializable]
        private sealed class PreselectionEvidence
        {
            public string scene;
            public string model;
            public string avatar;
            public bool avatar_valid;
            public float plane_y;
            public int plane_layer;
            public Vector3 plane_normal;
            public string camera;
            public Vector3 camera_position;
            public Vector3 camera_forward;
            public bool camera_orthographic;
            public int capture_width;
            public int capture_height;
            public int capture_framerate;
            public bool has_prepared_motion;
            public bool is_recording;
            public float max_pose_drift_mm;
            public string capture_path;
            public bool structural_passed;
            public bool visual_review_required;
        }

        private static bool TryStartPreselectionCapture(string requestId, out string message)
        {
            message = string.Empty;
            if (!Guid.TryParse(requestId, out Guid id) ||
                !ValidateRuntimeContext(null, interactive: false, out message))
            {
                if (string.IsNullOrEmpty(message)) message = "request id is invalid";
                return false;
            }

            Scene scene = SceneManager.GetActiveScene();
            FBXVmdPipeline pipeline = FindRuntimeFBXVmdPipeline();
            GameObject plane = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Plane");
            Collider planeCollider = plane != null ? plane.GetComponent<Collider>() : null;
            Camera camera = Camera.main;
            Animator animator = pipeline != null && pipeline.targetCharacter != null
                ? pipeline.targetCharacter.GetComponentInChildren<Animator>(true)
                : null;
            UnityHumanoidVMDRecorder recorder = pipeline != null && pipeline.targetCharacter != null
                ? pipeline.targetCharacter.GetComponentInChildren<UnityHumanoidVMDRecorder>(true)
                : null;
            if (scene.name != MainAutoSceneName || pipeline == null || pipeline.IsProcessing ||
                pipeline.HasPreparedImportedMotion || pipeline.IsImportedMotionPlaying ||
                pipeline.IsImportedMotionRecording || (recorder != null && recorder.IsRecording) ||
                Time.captureFramerate != 0 ||
                animator == null || !animator.gameObject.activeInHierarchy || !animator.isHuman ||
                animator.avatar == null || !animator.avatar.isValid ||
                plane == null || !plane.activeInHierarchy || planeCollider == null ||
                !planeCollider.enabled || planeCollider.isTrigger ||
                Mathf.Abs(plane.transform.position.y) > 0.001f ||
                Vector3.Angle(plane.transform.up, Vector3.up) > 1f ||
                camera == null || !camera.isActiveAndEnabled || camera.targetTexture != null ||
                Screen.width <= 0 || Screen.height <= 0)
            {
                message = "Main_Auto의 선택 전 모델·Avatar·바닥·카메라·녹화 상태를 확인하세요.";
                return false;
            }

            _preselectionBones = new[]
            {
                animator.GetBoneTransform(HumanBodyBones.Head),
                animator.GetBoneTransform(HumanBodyBones.Hips),
                animator.GetBoneTransform(HumanBodyBones.LeftFoot),
                animator.GetBoneTransform(HumanBodyBones.RightFoot)
            };
            if (_preselectionBones.Any(bone => bone == null))
            {
                message = "기본 자세를 측정할 Humanoid 본이 누락되었습니다.";
                return false;
            }

            string sessionPath = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "evidence", "boogle", "preselection", id.ToString("D"));
            try
            {
                Directory.CreateDirectory(sessionPath);
                string capturedAt = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                _preselectionCapturePath = Path.Combine(sessionPath,
                    $"when-{capturedAt}_where-Main_Auto_who-auto_what-preselection-GameView_why-F01_how-ScreenCapture.png");
                _preselectionStatePath = Path.Combine(sessionPath, "state.json");
                _preselectionBonePositions = _preselectionBones.Select(bone => bone.position).ToArray();
                _preselectionStartedUtc = DateTime.UtcNow;
                ScreenCapture.CaptureScreenshot(_preselectionCapturePath);
                _preselectionPipeline = pipeline;
                return true;
            }
            catch (Exception ex)
            {
                _preselectionCapturePath = null;
                _preselectionStatePath = null;
                _preselectionBones = null;
                _preselectionBonePositions = null;
                message = $"선택 전 캡처 시작 실패: {ex.Message}";
                return false;
            }
        }

        private static void PollPreselectionCapture()
        {
            if (DateTime.UtcNow - _preselectionStartedUtc < TimeSpan.FromMilliseconds(500)) return;

            bool screenshotReady = HasCompletedPng(_preselectionCapturePath);
            if (!screenshotReady && DateTime.UtcNow - _preselectionStartedUtc < TimeSpan.FromSeconds(15)) return;

            bool contextReady = EditorApplication.isPlaying &&
                SceneManager.GetActiveScene().name == MainAutoSceneName &&
                _preselectionPipeline != null && !_preselectionPipeline.IsProcessing &&
                !_preselectionPipeline.HasPreparedImportedMotion &&
                !_preselectionPipeline.IsImportedMotionRecording && Time.captureFramerate == 0;
            float drift = 0f;
            for (int index = 0; index < _preselectionBones.Length; index++)
            {
                if (_preselectionBones[index] == null)
                {
                    contextReady = false;
                    break;
                }
                drift = Mathf.Max(drift, Vector3.Distance(
                    _preselectionBonePositions[index], _preselectionBones[index].position) * 1000f);
            }

            GameObject plane = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(root => root.name == "Plane");
            Collider planeCollider = plane != null ? plane.GetComponent<Collider>() : null;
            Camera camera = Camera.main;
            Animator animator = _preselectionPipeline != null && _preselectionPipeline.targetCharacter != null
                ? _preselectionPipeline.targetCharacter.GetComponentInChildren<Animator>(true)
                : null;
            UnityHumanoidVMDRecorder recorder = _preselectionPipeline != null && _preselectionPipeline.targetCharacter != null
                ? _preselectionPipeline.targetCharacter.GetComponentInChildren<UnityHumanoidVMDRecorder>(true)
                : null;
            bool passed = screenshotReady && contextReady && drift <= 0.5f &&
                plane != null && plane.activeInHierarchy && planeCollider != null &&
                planeCollider.enabled && !planeCollider.isTrigger &&
                Mathf.Abs(plane.transform.position.y) <= 0.001f &&
                Vector3.Angle(plane.transform.up, Vector3.up) <= 1f &&
                camera != null && camera.isActiveAndEnabled && camera.targetTexture == null &&
                animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid &&
                (recorder == null || !recorder.IsRecording);
            var evidence = new PreselectionEvidence
            {
                scene = SceneManager.GetActiveScene().name,
                model = animator != null ? animator.gameObject.name : string.Empty,
                avatar = animator != null && animator.avatar != null ? animator.avatar.name : string.Empty,
                avatar_valid = animator != null && animator.avatar != null && animator.avatar.isValid,
                plane_y = plane != null ? plane.transform.position.y : 0f,
                plane_layer = plane != null ? plane.layer : -1,
                plane_normal = plane != null ? plane.transform.up : Vector3.zero,
                camera = camera != null ? camera.name : string.Empty,
                camera_position = camera != null ? camera.transform.position : Vector3.zero,
                camera_forward = camera != null ? camera.transform.forward : Vector3.zero,
                camera_orthographic = camera != null && camera.orthographic,
                capture_width = Screen.width,
                capture_height = Screen.height,
                capture_framerate = Time.captureFramerate,
                has_prepared_motion = _preselectionPipeline != null && _preselectionPipeline.HasPreparedImportedMotion,
                is_recording = (_preselectionPipeline != null && _preselectionPipeline.IsImportedMotionRecording) ||
                    (recorder != null && recorder.IsRecording),
                max_pose_drift_mm = drift,
                capture_path = screenshotReady ? _preselectionCapturePath : string.Empty,
                structural_passed = passed,
                visual_review_required = passed
            };
            File.WriteAllText(_preselectionStatePath, JsonUtility.ToJson(evidence, true));
            WriteStatus(new FbxPlaybackSmokeAutomationStatus
            {
                request_id = _activeAutomationRequestId,
                status = passed ? "completed" : "failed",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                command = _activeAutomationRequestedCommand,
                message = passed ? "선택 전 상태 수집 완료, Game View 수동 검토 필요" : "선택 전 상태 또는 Game View 캡처 실패",
                passed = passed,
                failure_stage = passed ? string.Empty : "preselection",
                manifest_path = _preselectionStatePath,
                preselection_state_path = _preselectionStatePath,
                capture_path = screenshotReady ? _preselectionCapturePath : string.Empty,
                total_jobs = 1,
                success_jobs = passed ? 1 : 0,
                failures = passed ? Array.Empty<string>() : new[] { "preselection structure, pose drift or capture failed" }
            });
            TraceAutomation($"preselection id={_activeAutomationRequestId} passed={passed} driftMm={drift:F3} screenshot={screenshotReady}");
            _preselectionPipeline = null;
            _preselectionCapturePath = null;
            _preselectionStatePath = null;
            _preselectionBones = null;
            _preselectionBonePositions = null;
            ClearAutomationRequestState();
            TryDeleteRequestFile();
        }

        private static bool HasCompletedPng(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length < 20) return false;
                    stream.Seek(-8, SeekOrigin.End);
                    byte[] trailer = new byte[8];
                    return stream.Read(trailer, 0, trailer.Length) == trailer.Length &&
                        trailer[0] == 0x49 && trailer[1] == 0x45 &&
                        trailer[2] == 0x4e && trailer[3] == 0x44 &&
                        trailer[4] == 0xae && trailer[5] == 0x42 &&
                        trailer[6] == 0x60 && trailer[7] == 0x82;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        [Serializable]
        private sealed class PlaybackPoseEvidence
        {
            public int requested_frame;
            public int actual_frame;
            public float time_seconds;
            public string state;
            public Vector3 body_position;
            public Quaternion body_rotation;
            public Vector3 hips_position;
            public Vector3 head_position;
            public Vector3 left_foot_position;
            public Vector3 right_foot_position;
            public Quaternion hips_rotation;
            public Quaternion head_rotation;
            public Quaternion left_foot_rotation;
            public Quaternion right_foot_rotation;
            public float[] muscles;
        }

        [Serializable]
        private sealed class PlaybackEvidence
        {
            public string scene;
            public string input_path;
            public string source_asset_path;
            public string model;
            public string avatar;
            public string clip_name;
            public string importer_clip_name;
            public int importer_clip_count;
            public float importer_first_frame;
            public float importer_last_frame;
            public string importer_animation_type;
            public string importer_avatar_setup;
            public string[] stage_log;
            public float clip_length_seconds;
            public float clip_frame_rate;
            public int last_frame;
            public PlaybackPoseEvidence paused;
            public PlaybackPoseEvidence first_seek;
            public PlaybackPoseEvidence second_seek;
            public PlaybackPoseEvidence repeat_seek;
            public PlaybackPoseEvidence resumed;
            public string first_capture_path;
            public string second_capture_path;
            public string repeat_capture_path;
            public float repeat_max_position_delta_mm;
            public float repeat_max_rotation_delta_degrees;
            public float repeat_max_muscle_delta;
            public Vector3 pre_import_root_position;
            public Vector3 pre_import_hips_position;
            public bool apply_root_motion;
            public bool lock_root_height_y;
            public bool keep_original_position_y;
            public bool lock_root_position_xz;
            public bool keep_original_position_xz;
            public FootFrameEvidence[] foot_frames;
            public bool structural_passed;
            public bool visual_review_required;
            public string failure_stage;
            public string failure_message;
        }

        [Serializable]
        private sealed class FootFrameEvidence
        {
            public int requested_frame;
            public int actual_frame;
            public float time_seconds;
            public string grounding_status;
            public Vector3 root_position;
            public Vector3 hips_position;
            public Vector3 body_position;
            public Vector3 left_knee_position;
            public Vector3 right_knee_position;
            public Quaternion left_ankle_rotation;
            public Quaternion right_ankle_rotation;
            public Quaternion left_toe_rotation;
            public Quaternion right_toe_rotation;
            public HumanoidFootGroundingSnapshot left;
            public HumanoidFootGroundingSnapshot right;
            public StageBoneEvidence[] f11_before_foot_stabilization;
            public StageBoneEvidence[] f11_after_foot_stabilization;
            public string game_view_path;
            public string side_view_path;
        }

        [Serializable]
        private sealed class StageBoneEvidence
        {
            public string bone;
            public bool present;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 local_position;
            public Vector3 local_scale;
        }

        private static bool TryStartPlaybackEvidence(string requestId, out string message)
        {
            message = string.Empty;
            if (!Guid.TryParse(requestId, out Guid id) ||
                !TryGetFBXVmdPipeline(SatisfactionFbxFileName, out FBXVmdPipeline pipeline,
                    interactive: false, out message))
            {
                if (string.IsNullOrEmpty(message)) message = "request id is invalid";
                return false;
            }

            if (SceneManager.GetActiveScene().name != MainAutoSceneName ||
                pipeline.IsProcessing || pipeline.IsImportedMotionRecording ||
                Time.captureFramerate != 0)
            {
                message = "Main_Auto의 비녹화 Play 상태가 필요합니다.";
                return false;
            }

            string inputPath = Path.Combine(GetImportFbxDirectory(), SatisfactionFbxFileName);
            Animator animator = pipeline.targetCharacter != null
                ? pipeline.targetCharacter.GetComponentInChildren<Animator>(true) : null;
            Transform hips = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (animator == null || hips == null)
            {
                message = "기본 모델의 Humanoid Hips를 찾을 수 없습니다.";
                return false;
            }
            string sessionPath = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "evidence", "boogle", "playback", id.ToString("D"));
            try
            {
                Directory.CreateDirectory(sessionPath);
                _playbackEvidencePath = Path.Combine(sessionPath, "state.json");
                _playbackEvidence = new PlaybackEvidence
                {
                    scene = MainAutoSceneName,
                    input_path = inputPath,
                    pre_import_root_position = pipeline.targetCharacter.transform.position,
                    pre_import_hips_position = hips.position,
                    apply_root_motion = animator.applyRootMotion,
                    foot_frames = new FootFrameEvidence[FootEvidenceFrames.Length]
                };
                _playbackEvidencePipeline = pipeline;
                _playbackEvidenceStartedUtc = DateTime.UtcNow;
                PlaybackStageLog.Clear();
                Application.logMessageReceived -= CapturePlaybackStageLog;
                Application.logMessageReceived += CapturePlaybackStageLog;
                if (!pipeline.TryStartFbxImportFromSharedSettings(inputPath))
                {
                    message = "제품 FBX 가져오기 요청이 거부되었습니다.";
                    ClearPlaybackEvidence();
                    return false;
                }

                _playbackEvidencePhase = PlaybackEvidencePhase.Importing;
                return true;
            }
            catch (Exception ex)
            {
                message = $"F03 실행 시작 실패: {ex.Message}";
                ClearPlaybackEvidence();
                return false;
            }
        }

        private static void CapturePlaybackStageLog(string message, string stackTrace, LogType type)
        {
            if (message.StartsWith("[FBXImport] 상태 변경됨.", StringComparison.Ordinal) &&
                PlaybackStageLog.Count < 64)
                PlaybackStageLog.Add(message);
        }

        private static void PollFullClipMetrics()
        {
            _fullClipMetrics.Poll();
            if (!_fullClipMetrics.IsFinished) return;
            try
            {
                bool hasEvidence = _fullClipMetrics.HasEvidence;
                string message = hasEvidence
                    ? $"{_fullClipMetrics.CaseId} 구간 수집 완료, 화면 검토 필요"
                    : _fullClipMetrics.FailureMessage;
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = _activeAutomationRequestId,
                    status = hasEvidence ? "completed" : "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = _activeAutomationRequestedCommand,
                    message = message,
                    passed = hasEvidence,
                    failure_stage = _fullClipMetrics.FailureStage,
                    capture_path = _vrmImportCapturePath,
                    full_clip_state_path = _fullClipMetrics.StatePath,
                    manifest_path = _fullClipMetrics.StatePath,
                    total_jobs = 1,
                    success_jobs = hasEvidence ? 1 : 0,
                    failures = hasEvidence ? Array.Empty<string>() : new[] { message }
                });
                TraceAutomation($"full clip id={_activeAutomationRequestId} evidence={hasEvidence} stage={_fullClipMetrics.FailureStage}");
            }
            finally
            {
                _fullClipMetrics.Dispose();
                _fullClipMetrics = null;
                RestoreAlternateModel();
                ClearAutomationRequestState();
                TryDeleteRequestFile();
            }
        }

        private static void PollProductUiFlow()
        {
            _productUiFlow.Poll();
            if (!_productUiFlow.IsFinished) return;
            try
            {
                bool hasEvidence = _productUiFlow.HasEvidence;
                string message = hasEvidence
                    ? "F15 제품 UI 이벤트 수집 완료, Game View 사람 검토 필요"
                    : _productUiFlow.FailureMessage;
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = _activeAutomationRequestId,
                    status = hasEvidence ? "completed" : "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = _activeAutomationRequestedCommand,
                    message = message,
                    passed = hasEvidence,
                    failure_stage = _productUiFlow.FailureStage,
                    manifest_path = _productUiFlow.StatePath,
                    total_jobs = 1,
                    success_jobs = hasEvidence ? 1 : 0,
                    failures = hasEvidence ? Array.Empty<string>() : new[] { message }
                });
            }
            finally
            {
                _productUiFlow.Dispose();
                _productUiFlow = null;
                ClearAutomationRequestState();
                TryDeleteRequestFile();
            }
        }

        private static bool TryStartTestPrefabFullClip(
            FbxPlaybackSmokeAutomationRequest request, out string message)
        {
            if (!TryGetFBXVmdPipeline("tetoris_001.fbx", out FBXVmdPipeline pipeline,
                    interactive: false, out message)) return false;
            GameObject model = SceneManager.GetActiveScene().GetRootGameObjects()
                .FirstOrDefault(root => root.name == "testPrefab");
            Animator animator = model != null ? model.GetComponentInChildren<Animator>(true) : null;
            if (pipeline.targetCharacter == null ||
                pipeline.targetCharacter.name != E2eModelName || model == null ||
                animator == null || animator.avatar == null || !animator.avatar.isValid)
            {
                message = "F14는 Main_Auto의 YYB 대상과 유효한 testPrefab Avatar가 필요합니다.";
                return false;
            }

            _alternateModelPipeline = pipeline;
            _originalModel = pipeline.targetCharacter;
            _alternateModel = model;
            _originalModelWasActive = _originalModel.activeSelf;
            _alternateModelWasActive = model.activeSelf;
            try
            {
                _originalModel.SetActive(false);
                model.SetActive(true);
                pipeline.targetCharacter = model;
                if (FbxFullClipFootMetricsCapture.TryStart(pipeline, request.request_id,
                    request.run_id, out _fullClipMetrics, out message,
                    "tetoris_001.fbx", "F14")) return true;
            }
            finally
            {
                if (_fullClipMetrics == null) RestoreAlternateModel();
            }
            return false;
        }

        private static bool TryStartVrmLoadedCapture(
            FbxPlaybackSmokeAutomationRequest request, out string message)
        {
            message = string.Empty;
            if (request == null || !Guid.TryParse(request.run_id, out _) ||
                !Guid.TryParse(request.request_id, out _) ||
                string.IsNullOrWhiteSpace(request.vrm_file) ||
                !string.Equals(Path.GetExtension(request.vrm_file), ".vrm",
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "UniVRM 비교용 VRM 파일과 실행 ID가 필요합니다.";
                return false;
            }

            string vrmPath;
            try { vrmPath = Path.GetFullPath(request.vrm_file); }
            catch (Exception error)
            {
                message = $"VRM 파일 경로가 유효하지 않습니다: {error.Message}";
                return false;
            }
            if (!File.Exists(vrmPath))
            {
                message = $"VRM 파일이 없습니다: {vrmPath}";
                return false;
            }
            if (!TryGetFBXVmdPipeline(SatisfactionFbxFileName,
                    out FBXVmdPipeline pipeline, interactive: false,
                    out message)) return false;
            if (pipeline.targetCharacter == null || pipeline.targetCharacter.name != E2eModelName)
            {
                message = "Main_Auto의 MMD4Mecanim 기준 모델이 필요합니다.";
                return false;
            }

            _vrmLoadRequest = request;
            _vrmLoadPipeline = pipeline;
            try
            {
                _vrmLoadTask = VrmUtility.LoadAsync(vrmPath, new RuntimeOnlyAwaitCaller());
            }
            catch (Exception error)
            {
                _vrmLoadRequest = null;
                _vrmLoadPipeline = null;
                message = $"UniVRM 파일 로딩을 시작할 수 없습니다: {error.Message}";
                return false;
            }
            return true;
        }

        private static void PollVrmLoading()
        {
            if (!_vrmLoadTask.IsCompleted) return;
            Task<RuntimeGltfInstance> task = _vrmLoadTask;
            FbxPlaybackSmokeAutomationRequest request = _vrmLoadRequest;
            FBXVmdPipeline pipeline = _vrmLoadPipeline;
            _vrmLoadTask = null;
            _vrmLoadRequest = null;
            _vrmLoadPipeline = null;
            string stage = "vrm_import";
            try
            {
                RuntimeGltfInstance loaded = task.GetAwaiter().GetResult();
                _loadedVrmRoot = loaded != null ? loaded.Root : null;
                Animator animator = _loadedVrmRoot != null
                    ? _loadedVrmRoot.GetComponent<Animator>() : null;
                stage = "vrm_avatar";
                if (animator == null || animator.avatar == null ||
                    !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("UniVRM Humanoid Avatar가 유효하지 않습니다.");

                loaded.ShowMeshes();
                _loadedVrmRoot.name = "UniVRM Character";
                _loadedVrmRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _alternateModelPipeline = pipeline;
                _originalModel = pipeline.targetCharacter;
                _alternateModel = _loadedVrmRoot;
                _originalModelWasActive = _originalModel.activeSelf;
                _alternateModelWasActive = _alternateModel.activeSelf;
                _originalModel.SetActive(false);
                _alternateModel.SetActive(true);
                pipeline.targetCharacter = _alternateModel;
                stage = "vrm_evidence";
                string evidenceDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                    "Docs", "Workflow", "Local", "evidence", "boogle", "vrm-character",
                    Guid.Parse(request.run_id).ToString("D"),
                    Guid.Parse(request.request_id).ToString("D"));
                Directory.CreateDirectory(evidenceDirectory);
                string importCapturePath = Path.Combine(evidenceDirectory, "import.png");
                FbxFullClipFootMetricsCapture.WriteGameViewPng(importCapturePath);
                _vrmImportCapturePath = importCapturePath;
                stage = "vrm_capture_start";
                if (!FbxFullClipFootMetricsCapture.TryStart(pipeline, request.request_id,
                        request.run_id, out _fullClipMetrics, out string message,
                        SatisfactionFbxFileName, "VRM_UNIVRM", frameLimit: 91,
                        captureViews: true))
                    throw new InvalidOperationException(message);
                TraceAutomation($"UniVRM loaded id={request.request_id} model={_alternateModel.name}");
            }
            catch (Exception error)
            {
                string importCapturePath = _vrmImportCapturePath;
                RestoreAlternateModel();
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = request.request_id,
                    status = "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = _activeAutomationRequestedCommand,
                    failure_stage = stage,
                    capture_path = importCapturePath,
                    message = error.Message,
                    passed = false,
                    failures = new[] { error.Message }
                });
                TraceAutomation($"UniVRM failed id={request.request_id} stage={stage} message={error.Message}");
                ClearAutomationRequestState();
                TryDeleteRequestFile();
            }
        }

        private static void RestoreAlternateModel()
        {
            if (_alternateModelPipeline != null)
                _alternateModelPipeline.targetCharacter = _originalModel;
            if (_alternateModel != null && _alternateModel != _loadedVrmRoot)
                _alternateModel.SetActive(_alternateModelWasActive);
            if (_originalModel != null) _originalModel.SetActive(_originalModelWasActive);
            if (_loadedVrmRoot != null)
            {
                _loadedVrmRoot.SetActive(false);
                UnityEngine.Object.Destroy(_loadedVrmRoot);
                _loadedVrmRoot = null;
            }
            _alternateModelPipeline = null;
            _originalModel = null;
            _alternateModel = null;
            _vrmImportCapturePath = null;
        }

        private static void PollFootLiveEvidence()
        {
            _footLiveEvidence.Poll();
            if (!_footLiveEvidence.IsFinished) return;
            try
            {
                bool hasEvidence = _footLiveEvidence.HasEvidence;
                string message = hasEvidence
                    ? "F09 실제 시간 측정과 영상 수집, 사람 검토 필요"
                    : _footLiveEvidence.FailureMessage;
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = _activeAutomationRequestId,
                    status = hasEvidence ? "completed" : "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = _activeAutomationRequestedCommand,
                    message = message,
                    passed = hasEvidence,
                    failure_stage = _footLiveEvidence.FailureStage,
                    foot_live_state_path = _footLiveEvidence.StatePath,
                    manifest_path = _footLiveEvidence.StatePath,
                    total_jobs = 1,
                    success_jobs = hasEvidence ? 1 : 0,
                    failures = hasEvidence ? Array.Empty<string>() : new[] { message }
                });
                TraceAutomation($"foot live id={_activeAutomationRequestId} evidence={hasEvidence} stage={_footLiveEvidence.FailureStage}");
            }
            finally
            {
                _footLiveEvidence.Dispose();
                _footLiveEvidence = null;
                ClearAutomationRequestState();
                TryDeleteRequestFile();
            }
        }

        private static void PollPlaybackEvidence()
        {
            try
            {
                if (!EditorApplication.isPlaying ||
                    SceneManager.GetActiveScene().name != MainAutoSceneName ||
                    _playbackEvidencePipeline == null)
                {
                    CompletePlaybackEvidence(false, "environment", "Play 상태 또는 Main_Auto 씬이 변경되었습니다.");
                    return;
                }

                if (DateTime.UtcNow - _playbackEvidenceStartedUtc > TimeSpan.FromMinutes(20))
                {
                    CompletePlaybackEvidence(false, "timeout", "F03 가져오기 또는 재생 준비 시간이 초과되었습니다.");
                    return;
                }

                if (_playbackEvidencePipeline.SessionState == FBXVmdPipeline.FBXSessionState.Failed)
                {
                    CompletePlaybackEvidence(false, "import_or_playback",
                        _playbackEvidencePipeline.LastSessionMessage);
                    return;
                }

                switch (_playbackEvidencePhase)
                {
                    case PlaybackEvidencePhase.Importing:
                        if (_playbackEvidencePipeline.IsProcessing ||
                            !_playbackEvidencePipeline.HasPreparedImportedMotion ||
                            _playbackEvidencePipeline.SessionState != FBXVmdPipeline.FBXSessionState.Ready)
                            return;

                        _playbackEvidence.clip_length_seconds =
                            _playbackEvidencePipeline.ImportedMotionClipLengthSeconds;
                        _playbackEvidence.clip_frame_rate =
                            _playbackEvidencePipeline.ImportedMotionFrameRate;
                        _playbackEvidence.last_frame =
                            _playbackEvidencePipeline.ImportedMotionLastFrameIndex;
                        if (!TryCaptureImportedClipSettings())
                        {
                            CompletePlaybackEvidence(false, "import_settings",
                                "선택된 모델·클립 또는 FBX 임포트 설정을 확인하지 못했습니다.");
                            return;
                        }
                        if (_playbackEvidence.last_frame < FootEvidenceFrames[FootEvidenceFrames.Length - 1] ||
                            _playbackEvidence.clip_frame_rate <= 0f ||
                            !_playbackEvidencePipeline.TryPlayImportedMotion())
                        {
                            CompletePlaybackEvidence(false, "playback", "모션 길이 또는 재생 시작이 유효하지 않습니다.");
                            return;
                        }

                        _playbackEvidencePhase = PlaybackEvidencePhase.Preparing;
                        break;
                    case PlaybackEvidencePhase.Preparing:
                        if (_playbackEvidencePipeline.IsImportedMotionPlaying &&
                            !_playbackEvidencePipeline.IsPreparingImportedMotionCorrection)
                            _playbackEvidencePhase = PlaybackEvidencePhase.Playing;
                        break;
                    case PlaybackEvidencePhase.Playing:
                        if (_playbackEvidencePipeline.ImportedMotionCurrentTimeSeconds < 0.1f) return;
                        if (!_playbackEvidencePipeline.TryPauseImportedMotion() ||
                            !TryCapturePlaybackPose(-1, out _playbackEvidence.paused))
                        {
                            CompletePlaybackEvidence(false, "pause", "일시정지 상태 또는 표시 포즈를 읽지 못했습니다.");
                            return;
                        }

                        if (!TryBeginSeekCapture(166, PlaybackEvidencePhase.FirstCapture))
                            CompletePlaybackEvidence(false, "seek", "166프레임 탐색 또는 캡처를 시작하지 못했습니다.");
                        break;
                    case PlaybackEvidencePhase.FirstCapture:
                        if (!IsPlaybackCaptureReady()) return;
                        if (!TryBeginSeekCapture(544, PlaybackEvidencePhase.SecondCapture))
                            CompletePlaybackEvidence(false, "seek", "544프레임 탐색 또는 캡처를 시작하지 못했습니다.");
                        break;
                    case PlaybackEvidencePhase.SecondCapture:
                        if (!IsPlaybackCaptureReady()) return;
                        if (!TryBeginSeekCapture(166, PlaybackEvidencePhase.RepeatCapture))
                            CompletePlaybackEvidence(false, "seek", "166프레임 재탐색 또는 캡처를 시작하지 못했습니다.");
                        break;
                    case PlaybackEvidencePhase.RepeatCapture:
                        if (!IsPlaybackCaptureReady()) return;
                        CompareRepeatedPose();
                        _footEvidenceFrameIndex = 0;
                        if (!TryBeginFootFrameCapture())
                            CompletePlaybackEvidence(false, "foot_surface", "첫 하체 프레임 계측 또는 캡처에 실패했습니다.");
                        break;
                    case PlaybackEvidencePhase.FootCaptures:
                        if (!IsPlaybackCaptureReady()) return;
                        _footEvidenceFrameIndex++;
                        if (_footEvidenceFrameIndex < FootEvidenceFrames.Length)
                        {
                            if (!TryBeginFootFrameCapture())
                                CompletePlaybackEvidence(false, "foot_surface", "하체 연속 프레임 계측 또는 캡처에 실패했습니다.");
                            break;
                        }
                        if (!_playbackEvidencePipeline.TryPlayImportedMotion())
                        {
                            CompletePlaybackEvidence(false, "resume", "하체 프레임 탐색 뒤 재생하지 못했습니다.");
                            return;
                        }

                        _playbackEvidencePhase = PlaybackEvidencePhase.Resumed;
                        break;
                    case PlaybackEvidencePhase.Resumed:
                        if (!_playbackEvidencePipeline.IsImportedMotionPlaying ||
                            _playbackEvidencePipeline.ImportedMotionCurrentTimeSeconds <=
                                _playbackEvidence.repeat_seek.time_seconds + 0.05f) return;
                        if (!TryCapturePlaybackPose(-1, out _playbackEvidence.resumed))
                        {
                            CompletePlaybackEvidence(false, "resume", "재개된 표시 포즈를 읽지 못했습니다.");
                            return;
                        }

                        bool passed = _playbackEvidence.repeat_max_position_delta_mm <= 0.5f &&
                            _playbackEvidence.repeat_max_rotation_delta_degrees <= 0.5f &&
                            _playbackEvidence.repeat_max_muscle_delta <= 0.0001f &&
                            _playbackEvidence.foot_frames.All(frame => frame != null);
                        CompletePlaybackEvidence(passed, passed ? string.Empty : "reproducibility",
                            passed ? "" : "같은 프레임의 반복 탐색 포즈가 다릅니다.");
                        break;
                }
            }
            catch (Exception ex)
            {
                CompletePlaybackEvidence(false, "infrastructure", ex.Message);
            }
        }

        private static bool TryBeginSeekCapture(int frame, PlaybackEvidencePhase phase)
        {
            if (!_playbackEvidencePipeline.TrySeekImportedMotionFrame(frame) ||
                !TryCapturePlaybackPose(frame, out PlaybackPoseEvidence pose) ||
                pose.actual_frame != frame ||
                Mathf.Abs(pose.time_seconds - frame / _playbackEvidence.clip_frame_rate) >
                    0.5f / _playbackEvidence.clip_frame_rate ||
                pose.state != FBXVmdPipeline.FBXSessionState.PreviewPaused.ToString())
                return false;

            string capturedAt = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            _playbackCapturePath = Path.Combine(Path.GetDirectoryName(_playbackEvidencePath),
                $"when-{capturedAt}_where-Main_Auto_who-auto_what-frame-{frame}-{phase}_why-F03_how-GameView.png");
            if (phase == PlaybackEvidencePhase.FirstCapture)
            {
                _playbackEvidence.first_seek = pose;
                _playbackEvidence.first_capture_path = _playbackCapturePath;
            }
            else if (phase == PlaybackEvidencePhase.SecondCapture)
            {
                _playbackEvidence.second_seek = pose;
                _playbackEvidence.second_capture_path = _playbackCapturePath;
            }
            else
            {
                _playbackEvidence.repeat_seek = pose;
                _playbackEvidence.repeat_capture_path = _playbackCapturePath;
            }

            ScreenCapture.CaptureScreenshot(_playbackCapturePath);
            _playbackCaptureStartedUtc = DateTime.UtcNow;
            _playbackEvidencePhase = phase;
            return true;
        }

        private static bool IsPlaybackCaptureReady()
        {
            if (HasCompletedPng(_playbackCapturePath)) return true;
            if (DateTime.UtcNow - _playbackCaptureStartedUtc <= TimeSpan.FromSeconds(15)) return false;
            throw new IOException($"Game View 캡처가 완료되지 않았습니다: {_playbackCapturePath}");
        }

        private static bool TryCaptureImportedClipSettings()
        {
            const string assetPath = "Assets/" + ImportFbxRelativeDirectory + "/" + SatisfactionFbxFileName;
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            ModelImporterClipAnimation[] clips = importer?.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer?.defaultClipAnimations;
            Animator animator = _playbackEvidencePipeline.targetCharacter != null
                ? _playbackEvidencePipeline.targetCharacter.GetComponentInChildren<Animator>(true) : null;
            if (importer == null || clips == null || clips.Length == 0 || animator == null ||
                animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman ||
                string.IsNullOrWhiteSpace(_playbackEvidencePipeline.ImportedMotionClipName)) return false;

            string clipName = _playbackEvidencePipeline.ImportedMotionClipName;
            ModelImporterClipAnimation clip = clips.FirstOrDefault(candidate => candidate.name == clipName)
                ?? clips[0];
            _playbackEvidence.source_asset_path = assetPath;
            _playbackEvidence.model = _playbackEvidencePipeline.targetCharacter.name;
            _playbackEvidence.avatar = animator.avatar.name;
            _playbackEvidence.clip_name = clipName;
            _playbackEvidence.importer_clip_name = clip.name;
            _playbackEvidence.importer_clip_count = clips.Length;
            _playbackEvidence.importer_first_frame = clip.firstFrame;
            _playbackEvidence.importer_last_frame = clip.lastFrame;
            _playbackEvidence.importer_animation_type = importer.animationType.ToString();
            _playbackEvidence.importer_avatar_setup = importer.avatarSetup.ToString();
            _playbackEvidence.lock_root_height_y = clip.lockRootHeightY;
            _playbackEvidence.keep_original_position_y = clip.keepOriginalPositionY;
            _playbackEvidence.lock_root_position_xz = clip.lockRootPositionXZ;
            _playbackEvidence.keep_original_position_xz = clip.keepOriginalPositionXZ;
            return true;
        }

        private static bool TryBeginFootFrameCapture()
        {
            int frame = FootEvidenceFrames[_footEvidenceFrameIndex];
            if (!_playbackEvidencePipeline.TrySeekImportedMotionFrame(frame)) return false;

            Animator animator = _playbackEvidencePipeline.targetCharacter != null
                ? _playbackEvidencePipeline.targetCharacter.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || !animator.isHuman) return false;
            StageBoneEvidence[] originalFinalPose = CaptureStageBones(animator);
            var controller = ReadMemberValue(typeof(FBXVmdPipeline), _playbackEvidencePipeline,
                "_humanoidMotionPlaybackController") as HumanoidMotionPlaybackController;
            StageBoneEvidence[] beforeFootStabilization = null;
            if (controller == null || !controller.TryCapturePoseBeforeFootStabilization(
                    () => beforeFootStabilization = CaptureStageBones(animator))) return false;
            StageBoneEvidence[] afterFootStabilization = CaptureStageBones(animator);
            if (originalFinalPose.Where((bone, index) => bone.present &&
                    (Vector3.Distance(bone.position, afterFootStabilization[index].position) > 0.0001f ||
                     Quaternion.Angle(bone.rotation, afterFootStabilization[index].rotation) > 0.01f))
                .Any()) return false;
            if (!_playbackEvidencePipeline.TryCaptureImportedMotionFootSurface(
                    out HumanoidFootGroundingSnapshot left,
                    out HumanoidFootGroundingSnapshot right,
                    out HumanoidFootGroundingStatus status) ||
                !_playbackEvidencePipeline.TryCaptureImportedMotionPose(out HumanPose pose)) return false;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            Transform rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform leftToe = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightToe = animator.GetBoneTransform(HumanBodyBones.RightToes);
            if (hips == null || leftKnee == null || rightKnee == null || leftFoot == null ||
                rightFoot == null || leftToe == null || rightToe == null) return false;

            string capturedAt = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string prefix = $"when-{capturedAt}_where-Main_Auto_who-auto_what-frame-{frame}_why-F04-F05";
            string directory = Path.GetDirectoryName(_playbackEvidencePath);
            _playbackCapturePath = Path.Combine(directory, prefix + "_how-GameView.png");
            var sample = new FootFrameEvidence
            {
                requested_frame = frame,
                actual_frame = _playbackEvidencePipeline.ImportedMotionCurrentFrameIndex,
                time_seconds = _playbackEvidencePipeline.ImportedMotionCurrentTimeSeconds,
                grounding_status = status.ToString(),
                root_position = _playbackEvidencePipeline.targetCharacter.transform.position,
                hips_position = hips.position,
                body_position = pose.bodyPosition,
                left_knee_position = leftKnee.position,
                right_knee_position = rightKnee.position,
                left_ankle_rotation = leftFoot.rotation,
                right_ankle_rotation = rightFoot.rotation,
                left_toe_rotation = leftToe.rotation,
                right_toe_rotation = rightToe.rotation,
                left = left,
                right = right,
                f11_before_foot_stabilization = beforeFootStabilization,
                f11_after_foot_stabilization = afterFootStabilization,
                game_view_path = _playbackCapturePath
            };
            if (sample.actual_frame != frame) return false;
            if (frame == 0 || frame == 166 || frame == 544)
            {
                sample.side_view_path = Path.Combine(directory, prefix + "_how-side-camera.png");
                if (!TryCaptureSideView(animator, hips.position, sample.side_view_path)) return false;
            }

            _playbackEvidence.foot_frames[_footEvidenceFrameIndex] = sample;
            ScreenCapture.CaptureScreenshot(_playbackCapturePath);
            _playbackCaptureStartedUtc = DateTime.UtcNow;
            _playbackEvidencePhase = PlaybackEvidencePhase.FootCaptures;
            return true;
        }

        private static StageBoneEvidence[] CaptureStageBones(Animator animator)
        {
            return F11Bones.Select(bone =>
            {
                Transform transform = animator.GetBoneTransform(bone);
                return new StageBoneEvidence
                {
                    bone = bone.ToString(),
                    present = transform != null,
                    position = transform != null ? transform.position : Vector3.zero,
                    rotation = transform != null ? transform.rotation : Quaternion.identity,
                    local_position = transform != null ? transform.localPosition : Vector3.zero,
                    local_scale = transform != null ? transform.localScale : Vector3.zero
                };
            }).ToArray();
        }

        private static bool TryCaptureSideView(Animator animator, Vector3 center, string path)
        {
            Camera source = Camera.main;
            if (source == null) return false;
            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                cameraObject = new GameObject("F04 Side View") { hideFlags = HideFlags.HideAndDontSave };
                Camera side = cameraObject.AddComponent<Camera>();
                side.CopyFrom(source);
                float distance = Vector3.Distance(source.transform.position, center);
                side.transform.position = center + animator.transform.right * distance +
                    Vector3.up * (source.transform.position.y - center.y);
                side.transform.LookAt(center);
                target = new RenderTexture(1024, 768, 24);
                side.targetTexture = target;
                side.Render();
                RenderTexture.active = target;
                image = new Texture2D(1024, 768, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                return HasCompletedPng(path);
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                if (target != null) UnityEngine.Object.DestroyImmediate(target);
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static bool TryCapturePlaybackPose(int requestedFrame, out PlaybackPoseEvidence sample)
        {
            sample = null;
            Animator animator = _playbackEvidencePipeline.targetCharacter != null
                ? _playbackEvidencePipeline.targetCharacter.GetComponentInChildren<Animator>(true)
                : null;
            if (animator == null || !animator.isHuman ||
                !_playbackEvidencePipeline.TryCaptureImportedMotionPose(out HumanPose pose)) return false;

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (hips == null || head == null || leftFoot == null || rightFoot == null) return false;

            sample = new PlaybackPoseEvidence
            {
                requested_frame = requestedFrame,
                actual_frame = _playbackEvidencePipeline.ImportedMotionCurrentFrameIndex,
                time_seconds = _playbackEvidencePipeline.ImportedMotionCurrentTimeSeconds,
                state = _playbackEvidencePipeline.SessionState.ToString(),
                body_position = pose.bodyPosition,
                body_rotation = pose.bodyRotation,
                hips_position = hips.position,
                head_position = head.position,
                left_foot_position = leftFoot.position,
                right_foot_position = rightFoot.position,
                hips_rotation = hips.rotation,
                head_rotation = head.rotation,
                left_foot_rotation = leftFoot.rotation,
                right_foot_rotation = rightFoot.rotation,
                muscles = pose.muscles != null ? (float[])pose.muscles.Clone() : Array.Empty<float>()
            };
            return true;
        }

        private static void CompareRepeatedPose()
        {
            PlaybackPoseEvidence first = _playbackEvidence.first_seek;
            PlaybackPoseEvidence repeat = _playbackEvidence.repeat_seek;
            _playbackEvidence.repeat_max_position_delta_mm = 1000f * new[]
            {
                Vector3.Distance(first.hips_position, repeat.hips_position),
                Vector3.Distance(first.head_position, repeat.head_position),
                Vector3.Distance(first.left_foot_position, repeat.left_foot_position),
                Vector3.Distance(first.right_foot_position, repeat.right_foot_position)
            }.Max();
            _playbackEvidence.repeat_max_rotation_delta_degrees = new[]
            {
                Quaternion.Angle(first.hips_rotation, repeat.hips_rotation),
                Quaternion.Angle(first.head_rotation, repeat.head_rotation),
                Quaternion.Angle(first.left_foot_rotation, repeat.left_foot_rotation),
                Quaternion.Angle(first.right_foot_rotation, repeat.right_foot_rotation)
            }.Max();
            _playbackEvidence.repeat_max_muscle_delta = first.muscles.Length == repeat.muscles.Length
                ? first.muscles.Zip(repeat.muscles, (left, right) => Mathf.Abs(left - right)).DefaultIfEmpty(0f).Max()
                : float.MaxValue;
        }

        private static void CompletePlaybackEvidence(bool passed, string failureStage, string message)
        {
            if (_playbackEvidence == null) return;
            _playbackEvidence.failure_stage = failureStage;
            _playbackEvidence.failure_message = message;
            _playbackEvidence.stage_log = PlaybackStageLog.ToArray();
            _playbackEvidence.structural_passed = passed;
            _playbackEvidence.visual_review_required = passed;
            string statePath = _playbackEvidencePath;
            try
            {
                if (_playbackEvidencePipeline != null &&
                    (_playbackEvidencePipeline.IsImportedMotionPlaying ||
                     _playbackEvidencePipeline.IsPreparingImportedMotionCorrection ||
                     _playbackEvidencePipeline.SessionState == FBXVmdPipeline.FBXSessionState.PreviewPaused))
                    _playbackEvidencePipeline.TryStopImportedMotion();
                File.WriteAllText(statePath, JsonUtility.ToJson(_playbackEvidence, true));
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = _activeAutomationRequestId,
                    status = passed ? "completed" : "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = _activeAutomationRequestedCommand,
                    message = passed ? "F03 상태 수집 완료, Game View 검토 필요" : message,
                    passed = passed,
                    failure_stage = failureStage,
                    playback_state_path = statePath,
                    manifest_path = statePath,
                    total_jobs = 1,
                    success_jobs = passed ? 1 : 0,
                    failures = passed ? Array.Empty<string>() : new[] { message }
                });
                TraceAutomation($"playback id={_activeAutomationRequestId} passed={passed} stage={failureStage} message={message}");
            }
            finally
            {
                ClearPlaybackEvidence();
                ClearAutomationRequestState();
                TryDeleteRequestFile();
            }
        }

        private static void ClearPlaybackEvidence()
        {
            Application.logMessageReceived -= CapturePlaybackStageLog;
            PlaybackStageLog.Clear();
            _playbackEvidencePhase = PlaybackEvidencePhase.None;
            _playbackEvidencePipeline = null;
            _playbackEvidence = null;
            _playbackEvidencePath = null;
            _playbackCapturePath = null;
            _footEvidenceFrameIndex = 0;
        }

        [Serializable]
        private sealed class InvalidInputCaseEvidence
        {
            public string case_id;
            public string input_path;
            public string product_status;
            public string failure_stage;
            public string product_error;
            public bool expected_rejection_observed;
        }

        [Serializable]
        private sealed class InvalidInputEvidence
        {
            public string scene;
            public InvalidInputCaseEvidence missing_fbx;
            public InvalidInputCaseEvidence invalid_avatar;
            public bool original_avatar_restored;
            public bool is_recording;
            public int capture_framerate;
            public bool test_passed;
            public string test_failure_stage;
            public string test_failure_message;
        }

        private static bool TryStartInvalidInputEvidence(string requestId, out string message)
        {
            message = string.Empty;
            if (!Guid.TryParse(requestId, out Guid id) ||
                !TryGetFBXVmdPipeline(null, out FBXVmdPipeline pipeline,
                    interactive: false, out message))
            {
                if (string.IsNullOrEmpty(message)) message = "request id is invalid";
                return false;
            }

            Animator animator = pipeline.targetCharacter != null
                ? pipeline.targetCharacter.GetComponent<Animator>() : null;
            if (SceneManager.GetActiveScene().name != MainAutoSceneName ||
                pipeline.IsProcessing || pipeline.HasPreparedImportedMotion ||
                pipeline.IsImportedMotionRecording || Time.captureFramerate != 0 ||
                animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                message = "F07은 Main_Auto의 새 Play와 유효한 기본 Avatar가 필요합니다.";
                return false;
            }

            string missingPath = Path.Combine(GetImportFbxDirectory(),
                $"missing-F07-{id:N}.fbx");
            if (File.Exists(missingPath))
            {
                message = "의도적 실패용 FBX 경로에 파일이 이미 있습니다.";
                return false;
            }

            string sessionPath = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
                "Docs", "Workflow", "Local", "evidence", "boogle", "invalid-input", id.ToString("D"));
            try
            {
                Directory.CreateDirectory(sessionPath);
                _invalidInputEvidencePath = Path.Combine(sessionPath, "state.json");
                _invalidInputEvidence = new InvalidInputEvidence
                {
                    scene = MainAutoSceneName,
                    missing_fbx = new InvalidInputCaseEvidence
                    {
                        case_id = "missing_fbx_path",
                        input_path = missingPath,
                        failure_stage = "input_validation"
                    },
                    invalid_avatar = new InvalidInputCaseEvidence
                    {
                        case_id = "invalid_avatar",
                        input_path = Path.Combine(GetImportFbxDirectory(), SatisfactionFbxFileName),
                        failure_stage = "avatar_preparation"
                    }
                };
                _invalidInputPipeline = pipeline;
                _invalidInputAnimator = animator;
                _originalAvatar = animator.avatar;
                _invalidInputStartedUtc = DateTime.UtcNow;
                if (!pipeline.TryStartFbxImportFromSharedSettings(missingPath))
                {
                    message = "없는 FBX 경로의 제품 가져오기 요청이 시작되지 않았습니다.";
                    ClearInvalidInputEvidence();
                    return false;
                }

                _invalidInputPhase = InvalidInputPhase.MissingPath;
                return true;
            }
            catch (Exception ex)
            {
                message = $"F07 실행 시작 실패: {ex.Message}";
                ClearInvalidInputEvidence();
                return false;
            }
        }

        private static void PollInvalidInputEvidence()
        {
            try
            {
                if (!EditorApplication.isPlaying ||
                    SceneManager.GetActiveScene().name != MainAutoSceneName ||
                    _invalidInputPipeline == null || _invalidInputAnimator == null)
                {
                    CompleteInvalidInputEvidence(false, "environment", "Play 상태 또는 대상 모델이 변경되었습니다.");
                    return;
                }

                if (DateTime.UtcNow - _invalidInputStartedUtc > TimeSpan.FromSeconds(90))
                {
                    CompleteInvalidInputEvidence(false, "timeout", "의도적 실패 확인 시간이 초과되었습니다.");
                    return;
                }

                if (_invalidInputPipeline.IsProcessing) return;

                if (_invalidInputPhase == InvalidInputPhase.MissingPath)
                {
                    InvalidInputCaseEvidence sample = _invalidInputEvidence.missing_fbx;
                    sample.product_status = _invalidInputPipeline.SessionState.ToString();
                    sample.product_error = _invalidInputPipeline.LastSessionMessage;
                    sample.expected_rejection_observed =
                        _invalidInputPipeline.SessionState == FBXVmdPipeline.FBXSessionState.Failed &&
                        sample.product_error.Contains("FBX 파일을 찾을 수 없습니다") &&
                        sample.product_error.Contains(sample.input_path);
                    if (!sample.expected_rejection_observed)
                    {
                        CompleteInvalidInputEvidence(false, "input_validation",
                            "없는 FBX 경로가 예상한 제품 실패로 기록되지 않았습니다.");
                        return;
                    }

                    _invalidInputAnimator.avatar = null;
                    if (!_invalidInputPipeline.TryStartFbxImportFromSharedSettings(
                            _invalidInputEvidence.invalid_avatar.input_path))
                    {
                        CompleteInvalidInputEvidence(false, "avatar_preparation",
                            "무효 Avatar의 제품 가져오기 요청이 시작되지 않았습니다.");
                        return;
                    }

                    _invalidInputPhase = InvalidInputPhase.InvalidAvatar;
                    return;
                }

                InvalidInputCaseEvidence avatarSample = _invalidInputEvidence.invalid_avatar;
                avatarSample.product_status = _invalidInputPipeline.SessionState.ToString();
                avatarSample.product_error = _invalidInputPipeline.LastSessionMessage;
                avatarSample.expected_rejection_observed =
                    _invalidInputPipeline.SessionState == FBXVmdPipeline.FBXSessionState.Failed &&
                    avatarSample.product_error.Contains("유효한 Humanoid Avatar가 없습니다");
                CompleteInvalidInputEvidence(avatarSample.expected_rejection_observed,
                    avatarSample.expected_rejection_observed ? string.Empty : "avatar_preparation",
                    avatarSample.expected_rejection_observed ? string.Empty :
                        "무효 Avatar가 예상한 제품 실패로 기록되지 않았습니다.");
            }
            catch (Exception ex)
            {
                CompleteInvalidInputEvidence(false, "infrastructure", ex.Message);
            }
        }

        private static void CompleteInvalidInputEvidence(bool passed, string failureStage, string message)
        {
            if (_invalidInputEvidence == null) return;
            if (_invalidInputAnimator != null) _invalidInputAnimator.avatar = _originalAvatar;
            _invalidInputEvidence.original_avatar_restored =
                _invalidInputAnimator != null && _invalidInputAnimator.avatar == _originalAvatar &&
                _originalAvatar != null && _originalAvatar.isValid;
            _invalidInputEvidence.is_recording =
                _invalidInputPipeline != null && _invalidInputPipeline.IsImportedMotionRecording;
            _invalidInputEvidence.capture_framerate = Time.captureFramerate;
            passed = passed && _invalidInputEvidence.original_avatar_restored &&
                !_invalidInputEvidence.is_recording && Time.captureFramerate == 0;
            _invalidInputEvidence.test_passed = passed;
            _invalidInputEvidence.test_failure_stage = failureStage;
            _invalidInputEvidence.test_failure_message = message;
            string statePath = _invalidInputEvidencePath;
            try
            {
                File.WriteAllText(statePath, JsonUtility.ToJson(_invalidInputEvidence, true));
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
                {
                    request_id = _activeAutomationRequestId,
                    status = passed ? "completed" : "failed",
                    updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                    command = _activeAutomationRequestedCommand,
                    message = passed ? "의도적 제품 실패 2건을 확인했습니다." : message,
                    passed = passed,
                    failure_stage = failureStage,
                    failure_evidence_path = statePath,
                    manifest_path = statePath,
                    total_jobs = 2,
                    success_jobs = (int)(_invalidInputEvidence.missing_fbx.expected_rejection_observed ? 1 : 0) +
                        (int)(_invalidInputEvidence.invalid_avatar.expected_rejection_observed ? 1 : 0),
                    failures = passed ? Array.Empty<string>() : new[] { message }
                });
                TraceAutomation($"invalid-input id={_activeAutomationRequestId} passed={passed} stage={failureStage}");
            }
            finally
            {
                ClearInvalidInputEvidence();
                ClearAutomationRequestState();
                TryDeleteRequestFile();
            }
        }

        private static void ClearInvalidInputEvidence()
        {
            if (_invalidInputAnimator != null && _originalAvatar != null)
                _invalidInputAnimator.avatar = _originalAvatar;
            _invalidInputPhase = InvalidInputPhase.None;
            _invalidInputPipeline = null;
            _invalidInputAnimator = null;
            _originalAvatar = null;
            _invalidInputEvidence = null;
            _invalidInputEvidencePath = null;
        }

        private static bool TryBootstrapCleanAutomationRequest(FbxPlaybackSmokeAutomationRequest request)
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
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
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
                WriteStatus(new FbxPlaybackSmokeAutomationStatus
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
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment,
            out string message,
            int captureWidthOverride = 0,
            int captureHeightOverride = 0,
            float recordingStartTimeOverrideSeconds = float.NaN,
            bool useInputBaseName = false)
        {
            message = string.Empty;
            if (!TryGetFBXVmdPipeline(fbxFileName, out FBXVmdPipeline fileManager, interactive: false, out message))
            {
                return false;
            }

            if (!StartSmoke(fileManager, fbxFileName, mode, segment, durationSeconds,
                    enableFingerCloseups, sampleTimesOverride, captureWidthOverride,
                    captureHeightOverride, recordingStartTimeOverrideSeconds, useInputBaseName))
            {
                message = $"smoke start failed: {fbxFileName}";
                return false;
            }

            TrackSingleSmoke(fileManager, fbxFileName, mode);
            return true;
        }

        private static bool TryStartAutomationBatch(FBXVmdPipeline.EditorDiagnosticSmokeSegment segment, out string message)
        {
            message = string.Empty;
            if (!TryGetFBXVmdPipeline(null, out FBXVmdPipeline fileManager, interactive: false, out message))
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
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment = FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
            int captureWidthOverride = 0,
            int captureHeightOverride = 0,
            float recordingStartTimeOverrideSeconds = float.NaN)
        {
            if (IsBatchRunning())
            {
                EditorUtility.DisplayDialog("FBX Smoke", "전체 smoke 배치가 진행 중입니다. 완료 후 다시 실행하세요.", "확인");
                return;
            }

            if (!TryGetFBXVmdPipeline(fbxFileName, out FBXVmdPipeline fileManager))
            {
                return;
            }

            if (StartSmoke(fileManager, fbxFileName, mode, segment, durationSeconds, enableFingerCloseups, sampleTimesOverride, captureWidthOverride, captureHeightOverride, recordingStartTimeOverrideSeconds))
            {
                TrackSingleSmoke(fileManager, fbxFileName, mode);
            }
        }

        private static void StartSmokeBatch(FBXVmdPipeline fileManager, IEnumerable<string> fbxFileNames, FBXVmdPipeline.EditorDiagnosticSmokeSegment segment)
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

            _batchFBXVmdPipeline = fileManager;
            _batchTotalCount = PendingSmokeFiles.Count;
            _activeBatchFbxFileName = null;
            _batchSegment = segment;

            _batchFBXVmdPipeline.EditorDiagnosticSmokeFinished -= HandleBatchSmokeFinished;
            _batchFBXVmdPipeline.EditorDiagnosticSmokeFinished += HandleBatchSmokeFinished;
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;

            string segmentLabel = VisualComparisonCaptureSegmentPlanner.GetSegmentLabel(segment);
            Debug.Log($"[FbxPlaybackSmokeRunner] Import_FBX 전체 smoke 시작됨: segment={segmentLabel}, {_batchTotalCount} files, {SmokeDurationSeconds:F0}s cap");
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

            if (_batchFBXVmdPipeline == null)
            {
                FinishBatch("FBXVmdPipeline 참조가 없어 전체 smoke 배치를 종료했습니다.", forceFailure: true);
                return;
            }

            if (_batchFBXVmdPipeline.IsProcessing)
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
            string segmentLabel = VisualComparisonCaptureSegmentPlanner.GetSegmentLabel(_batchSegment);
            Debug.Log($"[FbxPlaybackSmokeRunner] 전체 smoke 진행 {currentIndex}/{_batchTotalCount}: segment={segmentLabel}, {_activeBatchFbxFileName}");

            if (!StartSmoke(_batchFBXVmdPipeline, _activeBatchFbxFileName, "batch", _batchSegment, SmokeDurationSeconds, enableFingerCloseups: false, sampleTimesOverride: null))
            {
                BatchFailures.Add($"{_activeBatchFbxFileName}: start failed");
                _activeBatchFbxFileName = null;
                EditorApplication.delayCall += StartNextBatchSmoke;
            }
        }

        private static bool StartSmoke(
            FBXVmdPipeline fileManager,
            string fbxFileName,
            string mode,
            FBXVmdPipeline.EditorDiagnosticSmokeSegment segment = FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head,
            float durationSeconds = SmokeDurationSeconds,
            bool enableFingerCloseups = false,
            float[] sampleTimesOverride = null,
            int captureWidthOverride = 0,
            int captureHeightOverride = 0,
            float recordingStartTimeOverrideSeconds = float.NaN,
            bool useInputBaseName = false)
        {
            if (fileManager == null)
            {
                Debug.LogError("[FbxPlaybackSmokeRunner] FBXVmdPipeline을 찾을 수 없습니다, smoke를 시작할 수 없습니다");
                return false;
            }

            if (fileManager.IsProcessing)
            {
                Debug.LogWarning($"[FbxPlaybackSmokeRunner] FBXVmdPipeline이 사용 중입니다, smoke 시작 안 됨: {fbxFileName}");
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
                captureHeightOverride: captureHeightOverride,
                recordingStartTimeOverrideSeconds: recordingStartTimeOverrideSeconds,
                useInputBaseName: useInputBaseName);

            if (started)
            {
                string segmentLabel = VisualComparisonCaptureSegmentPlanner.GetSegmentLabel(segment);
                string sampleSummary = sampleTimesOverride != null && sampleTimesOverride.Length > 0
                    ? string.Join("/", sampleTimesOverride.Select(time => time.ToString("0.###")))
                    : "default";
                Debug.Log($"[FbxPlaybackSmokeRunner] {fbxFileName} smoke 시작됨: mode={mode}, segment={segmentLabel}, {safeDuration:F1}s, {targetFrameCount} frames, fingerCloseups={enableFingerCloseups}, samples={sampleSummary}");
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

        private static float GetFullRegressionEvidenceRecordingStartTimeOverrideSecondsForTest()
        {
            return FullRegressionEvidenceRecordingStartTimeOverrideSeconds;
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

        private static string GetQuickVmdSmokeCommandForTest()
        {
            return CaptureSatisfactionQuickVmdSmokeCommand;
        }

        private static string GetQuickVmdSmokeFbxFileNameForTest()
        {
            return SatisfactionFbxFileName;
        }

        private static float GetQuickVmdSmokeDurationSecondsForTest()
        {
            return QuickVmdSmokeDurationSeconds;
        }

        private static int GetQuickVmdSmokeTargetFrameCountForTest()
        {
            return QuickVmdSmokeTargetFrameCount;
        }

        private static void TrackSingleSmoke(FBXVmdPipeline fileManager, string fbxFileName, string mode)
        {
            ClearSingleSmokeTracking();
            _singleFBXVmdPipeline = fileManager;
            _activeSingleFbxFileName = fbxFileName;
            _singleSmokeMode = mode;
            _singleFBXVmdPipeline.EditorDiagnosticSmokeFinished += HandleSingleSmokeFinished;
        }

        private static void HandleSingleSmokeFinished(string fbxFileName, VmdSaveResult result)
        {
            if (_singleFBXVmdPipeline == null)
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
            string evidenceSummary = BuildSingleSmokeEvidenceSummary(_singleFBXVmdPipeline);

            if (result.Success)
            {
                Debug.Log($"[FbxPlaybackSmokeRunner] 단일 smoke 성공: mode={_singleSmokeMode}, {resultLabel}{evidenceSummary}");
            }
            else
            {
                Debug.LogError($"[FbxPlaybackSmokeRunner] 단일 smoke 실패: mode={_singleSmokeMode}, {resultLabel}{evidenceSummary}");
            }

            if (!string.IsNullOrEmpty(_activeAutomationRequestId))
            {
                CompleteAutomationRequest(
                    result.Success,
                    result.Success ? "single smoke completed" : result.ErrorMessage,
                    GetLastManifestPath(_singleFBXVmdPipeline),
                    result.Success ? Array.Empty<string>() : new[] { result.ErrorMessage },
                    totalJobs: 1,
                    successJobs: result.Success ? 1 : 0,
                    saveResult: result);
            }

            ClearSingleSmokeTracking();
        }

        private static string BuildSingleSmokeEvidenceSummary(FBXVmdPipeline fileManager)
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
            if (_batchFBXVmdPipeline != null)
            {
                _batchFBXVmdPipeline.ScheduleEditorDiagnosticBatchAdvance(StartNextBatchSmoke);
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
            if (_batchFBXVmdPipeline != null)
            {
                _batchFBXVmdPipeline.EditorDiagnosticSmokeFinished -= HandleBatchSmokeFinished;
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
            _batchFBXVmdPipeline = null;
            _activeBatchFbxFileName = null;
            _batchTotalCount = 0;
            _batchSegment = FBXVmdPipeline.EditorDiagnosticSmokeSegment.Head;
        }

        private static void ClearSingleSmokeTracking()
        {
            if (_singleFBXVmdPipeline != null)
            {
                _singleFBXVmdPipeline.EditorDiagnosticSmokeFinished -= HandleSingleSmokeFinished;
            }

            _singleFBXVmdPipeline = null;
            _activeSingleFbxFileName = null;
            _singleSmokeMode = null;
        }

        private static bool IsBatchRunning()
        {
            return _batchFBXVmdPipeline != null;
        }

        private static string GetLastManifestPath(FBXVmdPipeline fileManager)
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
            int successJobs,
            VmdSaveResult? saveResult = null)
        {
            WriteStatus(new FbxPlaybackSmokeAutomationStatus
            {
                request_id = _activeAutomationRequestId ?? string.Empty,
                status = passed ? "completed" : "failed",
                updated_at = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                command = _activeAutomationRequestedCommand ?? _activeAutomationCommand ?? string.Empty,
                message = message ?? string.Empty,
                passed = passed,
                manifest_path = manifestPath ?? string.Empty,
                failure_stage = passed ? string.Empty : "product_path",
                output_path = saveResult?.FilePath ?? string.Empty,
                frame_count = saveResult?.FrameCount ?? 0,
                file_size_bytes = saveResult?.FileSizeBytes ?? 0,
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

        private static void WriteStatus(FbxPlaybackSmokeAutomationStatus status)
        {
            AutomationStore.SaveStatus(status);
        }

        private static void TraceAutomation(string message)
        {
            try
            {
                AutomationStore.AppendTrace(message);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FbxPlaybackSmokeRunner] 트레이스 쓰기 실패: {ex.Message}");
            }
        }

        private static void PersistRequest(FbxPlaybackSmokeAutomationRequest request)
        {
            AutomationStore.SaveRequest(request);
        }

        private static void TryDeleteRequestFile()
        {
            try
            {
                AutomationStore.DeleteRequest();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[FbxPlaybackSmokeRunner] 요청 파일 삭제 실패: {ex.Message}");
            }
        }

        private static bool TryGetFBXVmdPipeline(string fbxFileName, out FBXVmdPipeline fileManager)
        {
            return TryGetFBXVmdPipeline(fbxFileName, out fileManager, interactive: true, out _);
        }

        private static bool TryGetFBXVmdPipeline(string fbxFileName, out FBXVmdPipeline fileManager, bool interactive, out string errorMessage)
        {
            fileManager = null;
            errorMessage = string.Empty;
            if (!ValidateRuntimeContext(fbxFileName, interactive, out errorMessage))
            {
                return false;
            }

            fileManager = FindRuntimeFBXVmdPipeline();
            if (fileManager != null)
            {
                return true;
            }

            errorMessage = "현재 Play Mode 씬에서 FBXVmdPipeline를 찾지 못했습니다.";
            Debug.LogWarning($"[FbxPlaybackSmokeRunner] FBXVmdPipeline 조회 실패: {BuildMainAutoRuntimeSummary()}, thumbReference[{BuildRetargeterThumbReferenceSummary(null)}]");
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

        private static FBXVmdPipeline FindRuntimeFBXVmdPipeline()
        {
            Scene runtimeScene = SceneManager.GetActiveScene();
            if (runtimeScene.IsValid())
            {
                foreach (GameObject rootObject in runtimeScene.GetRootGameObjects())
                {
                    if (rootObject == null)
                    {
                        continue;
                    }

                    FBXVmdPipeline sceneMatch = rootObject.GetComponentInChildren<FBXVmdPipeline>(true);
                    if (sceneMatch != null)
                    {
                        return sceneMatch;
                    }
                }
            }

            return Resources.FindObjectsOfTypeAll<FBXVmdPipeline>()
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.gameObject.scene.IsValid() &&
                    string.Equals(candidate.gameObject.scene.name, runtimeScene.name, StringComparison.Ordinal));
        }

        private static string BuildMainAutoRuntimeSummary()
        {
            Scene editorScene = EditorSceneManager.GetActiveScene();
            Scene runtimeScene = SceneManager.GetActiveScene();
            FBXVmdPipeline directMatch = FindRuntimeFBXVmdPipeline();
            FBXVmdPipeline[] allFBXVmdPipelines = Resources.FindObjectsOfTypeAll<FBXVmdPipeline>();
            int runtimeSceneFBXVmdPipelineCount = allFBXVmdPipelines.Count(candidate =>
                candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                string.Equals(candidate.gameObject.scene.name, runtimeScene.name, StringComparison.Ordinal));

            return
                $"editorScene[{SummarizeScene(editorScene)}], runtimeScene[{SummarizeScene(runtimeScene)}], " +
                $"editorFBXVmdPipelineRoot[{SummarizeNamedRoot(editorScene, "FBXVmdPipeline")}], " +
                $"runtimeFBXVmdPipelineRoot[{SummarizeNamedRoot(runtimeScene, "FBXVmdPipeline")}], " +
                $"findObjectOfType={(directMatch != null ? GetHierarchyPath(directMatch.transform) : "<none>")}, " +
                $"allFBXVmdPipelines={allFBXVmdPipelines.Length}, runtimeSceneFBXVmdPipelines={runtimeSceneFBXVmdPipelineCount}, " +
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
            int childFBXVmdPipelineCount = rootObject.GetComponentsInChildren<FBXVmdPipeline>(true).Length;

            return
                $"path={GetHierarchyPath(rootObject.transform)}, direct=[{SummarizeComponents(directComponents)}], " +
                $"directMissing={directMissingCount}, childCount={childComponents.Length}, childMissing={childMissingCount}, " +
                $"childFBXVmdPipelines={childFBXVmdPipelineCount}, childSample=[{SummarizeComponents(childComponents, 16)}]";
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

        private static string BuildRetargeterThumbReferenceSummary(FBXVmdPipeline runtimeFBXVmdPipeline)
        {
            Component[] retargeters = UnityEngine.Object.FindObjectsOfType<Component>()
                .Where(component => component != null && component.GetType().Name == "PoseSpaceRetargeter")
                .ToArray();
            if (retargeters.Length == 0)
            {
                return "retargeters=<none>";
            }

            Animator fileManagerTargetAnimator = runtimeFBXVmdPipeline != null && runtimeFBXVmdPipeline.targetCharacter != null
                ? runtimeFBXVmdPipeline.targetCharacter.GetComponent<Animator>()
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
                $"manualFingerConfig={ReadBoolMember(retargeterType, retargeter, "ShouldUseManualAnimatorFingerPoseReference")}, " +
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

