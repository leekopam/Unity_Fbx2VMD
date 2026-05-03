#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Member_Han.Modules.FBXImporter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Member_Han.Modules.FBXImporter.EditorTools
{
    public static class FbxPlaybackSmokeRunner
    {
        private const string MenuRoot = "Machine Spirit/FBX Smoke/";
        private const string MainAutoSceneName = "Main_Auto";
        private const string ImportFbxRelativeDirectory = "Resources/Import_FBX";
        private const float SmokeDurationSeconds = 31f;
        private const float SmokeFrameRate = 30f;
        private const float SmokeStartDelaySeconds = 0.2f;

        private static readonly Queue<string> PendingSmokeFiles = new Queue<string>();
        private static readonly List<string> BatchSuccesses = new List<string>();
        private static readonly List<string> BatchFailures = new List<string>();
        private static FileManager _batchFileManager;
        private static string _activeBatchFbxFileName;
        private static int _batchTotalCount;
        private static FileManager.EditorDiagnosticSmokeSegment _batchSegment = FileManager.EditorDiagnosticSmokeSegment.Head;

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
            RunSingleSmoke("satisfaction_2.fbx");
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

        [MenuItem(MenuRoot + "Run All Import_FBX 31s", true)]
        [MenuItem(MenuRoot + "Run All Import_FBX Middle 31s", true)]
        [MenuItem(MenuRoot + "Run All Import_FBX Tail 31s", true)]
        [MenuItem(MenuRoot + "Run satisfaction_2 31s", true)]
        [MenuItem(MenuRoot + "Run Antenna39 31s", true)]
        [MenuItem(MenuRoot + "Run Snake 31s", true)]
        [MenuItem(MenuRoot + "Run mikumikuni_retake_000 31s", true)]
        [MenuItem(MenuRoot + "Run neo_1_001 31s", true)]
        private static bool ValidateSmokeMenu()
        {
            return EditorApplication.isPlaying && !EditorApplication.isPaused && !EditorApplication.isCompiling;
        }

        private static void RunSingleSmoke(string fbxFileName)
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

            StartSmoke(fileManager, fbxFileName, "single");
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

            if (!StartSmoke(_batchFileManager, _activeBatchFbxFileName, "batch", _batchSegment))
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
            FileManager.EditorDiagnosticSmokeSegment segment = FileManager.EditorDiagnosticSmokeSegment.Head)
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

            int targetFrameCount = Mathf.CeilToInt(SmokeDurationSeconds * SmokeFrameRate);
            bool started = fileManager.StartEditorDiagnosticSmoke(
                fbxFileName,
                SmokeDurationSeconds,
                targetFrameCount,
                enableDiagnostics: true,
                enableFingerCloseups: false,
                useDeterministicCaptureFramerate: true,
                diagnosticStartDelay: SmokeStartDelaySeconds,
                segment: segment);

            if (started)
            {
                string segmentLabel = GetSegmentLabel(segment);
                Debug.Log($"[FbxPlaybackSmokeRunner] {fbxFileName} smoke 시작: mode={mode}, segment={segmentLabel}, {SmokeDurationSeconds:F0}s, {targetFrameCount} frames");
            }

            return started;
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
            EditorApplication.delayCall += StartNextBatchSmoke;
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

            PendingSmokeFiles.Clear();
            BatchSuccesses.Clear();
            BatchFailures.Clear();
            _batchFileManager = null;
            _activeBatchFbxFileName = null;
            _batchTotalCount = 0;
            _batchSegment = FileManager.EditorDiagnosticSmokeSegment.Head;
        }

        private static bool IsBatchRunning()
        {
            return _batchFileManager != null;
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
            fileManager = null;
            if (!ValidateRuntimeContext(fbxFileName))
            {
                return false;
            }

            fileManager = UnityEngine.Object.FindObjectOfType<FileManager>();
            if (fileManager != null)
            {
                return true;
            }

            EditorUtility.DisplayDialog("FBX Smoke", "현재 Play Mode 씬에서 FileManager를 찾지 못했습니다.", "확인");
            return false;
        }

        private static bool ValidateRuntimeContext(string fbxFileName)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Main_Auto 씬을 Play Mode로 실행한 뒤 smoke 메뉴를 사용하세요.", "확인");
                return false;
            }

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("FBX Smoke", "Unity 컴파일이 끝난 뒤 다시 실행하세요.", "확인");
                return false;
            }

            if (!string.Equals(EditorSceneManager.GetActiveScene().name, MainAutoSceneName, StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("FBX Smoke", "활성 씬이 Main_Auto가 아닙니다.", "확인");
                return false;
            }

            if (string.IsNullOrWhiteSpace(fbxFileName))
            {
                return true;
            }

            string fbxPath = Path.Combine(GetImportFbxDirectory(), fbxFileName);
            if (!File.Exists(fbxPath))
            {
                EditorUtility.DisplayDialog("FBX Smoke", $"FBX 파일을 찾지 못했습니다.\n{fbxPath}", "확인");
                return false;
            }

            return true;
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
    }
}
#endif
