using UnityEngine;
using System.IO;
using Fbx2Vmd.FileSystem;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBXVmdPipeline의 FBX 파일 선택 및 임포트 진입점을 담당하는 컴패니언 컨트롤러입니다.
    /// 파일 다이얼로그, Import_FBX 폴더 로드, SharedSettings 연동을 캡슐화합니다.
    /// </summary>
    public class FBXImportController
    {
        private readonly FBXVmdPipeline _pipeline;

        public FBXImportController(FBXVmdPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        public void ImportFromDialog()
        {
            if (TryImportFromDialog()) return;

            string[] paths = _pipeline._fileBrowserService.OpenFilePanel(
                "Import FBX", "", FBXVmdPipeline.FBX_EXTENSION, false);
            if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            {
                string sourcePath = paths[0];
                Debug.Log($"선택된 파일: {sourcePath}");
                _pipeline.ProcessFBXAsync(sourcePath);
            }
        }

        internal bool TryImportFromDialog()
        {
            _pipeline.EnsureServicesInitialized();

            if (_pipeline._isProcessing)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return true;
            }

            _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Idle, "FBX 파일 선택 대기", 0f);
            string[] paths = _pipeline._fileBrowserService.OpenFilePanel(
                "Import FBX", "", FBXVmdPipeline.FBX_EXTENSION, false);
            if (paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Cancelled, "파일 선택이 취소됨.", 0f);
                return true;
            }

            string sourcePath = paths[0];
            Debug.Log($"[FBXVmdPipeline] 선택된 파일: {sourcePath}");
            _pipeline.ProcessFBXAsync(sourcePath);
            return true;
        }

        public void LoadFromImportFolder()
        {
            if (TryLoadFromImportFolder()) return;

            string targetDir = Path.Combine(Application.dataPath, "Resources", FBXVmdPipeline.IMPORT_FBX_FOLDER);

            if (!Directory.Exists(targetDir))
            {
                Debug.LogWarning($"Import_FBX 폴더가 존재하지 않습니다: {targetDir}");
                return;
            }

            string[] fbxFiles = Directory.GetFiles(targetDir, "*.fbx", SearchOption.TopDirectoryOnly);

            if (fbxFiles.Length == 0)
            {
                Debug.LogWarning("Import_FBX 폴더에 FBX 파일이 없습니다");
                return;
            }

            string selectedFile = fbxFiles[0];
            _pipeline.ProcessFBXAsync(selectedFile);
        }

        internal bool TryLoadFromImportFolder()
        {
            if (_pipeline._isProcessing)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return true;
            }

            string targetDir = _pipeline.GetControlledImportDirectory();
            if (!Directory.Exists(targetDir))
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Failed, $"Import_FBX 폴더가 없습니다: {targetDir}", 0f);
                return true;
            }

            string[] fbxFiles = Directory.GetFiles(targetDir, "*.fbx", SearchOption.TopDirectoryOnly);
            if (fbxFiles.Length == 0)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Failed, "Import_FBX 폴더에 FBX 파일이 없습니다", 0f);
                return true;
            }

            string selectedFile = fbxFiles[0];
            _pipeline.ProcessFBXAsync(selectedFile);
            return true;
        }

        public bool TryImportFromSharedSettings(string sourcePath)
        {
            if (_pipeline._isProcessing)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.LoadingFbx, "이미 FBX 처리 중입니다.", 0.1f);
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            _pipeline.ProcessFBXAsync(sourcePath.Trim());
            return true;
        }
    }
}
