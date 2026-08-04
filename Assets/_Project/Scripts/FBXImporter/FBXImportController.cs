using System;
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
        private readonly IFileBrowserService _fileBrowserService;

        public FBXImportController(FBXVmdPipeline pipeline, IFileBrowserService fileBrowserService)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _fileBrowserService = fileBrowserService ?? throw new ArgumentNullException(nameof(fileBrowserService));
        }

        public void ImportFromDialog()
        {
            TryImportFromDialog();
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
            string[] paths = _fileBrowserService.OpenFilePanel(
                "Import FBX", "", FBXVmdPipeline.FBX_EXTENSION, false);
            if (paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Cancelled, "파일 선택이 취소됨.", 0f);
                return true;
            }

            string sourcePath = paths[0];
            Debug.Log($"[FBXImport] 파일 선택됨. 경로={sourcePath}");
            _pipeline.ProcessFBXAsync(sourcePath);
            return true;
        }

        public void LoadFromImportFolder()
        {
            TryLoadFromImportFolder();
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
