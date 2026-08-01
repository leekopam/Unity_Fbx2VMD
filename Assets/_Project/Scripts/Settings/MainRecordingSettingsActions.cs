using System;
using Fbx2Vmd.Modules.FBXImporter;
using UnityEngine;

namespace Fbx2Vmd.Settings
{
    public static class MainRecordingSettingsActions
    {
        public static bool CanExecute(
            MainRecordingSettingsActionType action,
            RecodingSetting recodingSetting = null,
            FBXVmdPipeline fileManager = null)
        {
            switch (action)
            {
                case MainRecordingSettingsActionType.ImportFbx:
                    return ResolveFBXVmdPipeline(recodingSetting, fileManager) != null;
                case MainRecordingSettingsActionType.Close:
                case MainRecordingSettingsActionType.ComingSoon:
                    return true;
                default:
                    return false;
            }
        }

        public static bool Execute(
            MainRecordingSettingsActionType action,
            RecodingSetting recodingSetting = null,
            FBXVmdPipeline fileManager = null,
            Action<string> notify = null)
        {
            switch (action)
            {
                case MainRecordingSettingsActionType.ImportFbx:
                    FBXVmdPipeline resolvedFBXVmdPipeline = ResolveFBXVmdPipeline(recodingSetting, fileManager);
                    if (resolvedFBXVmdPipeline == null)
                    {
                        const string message = "FBX 가져오기 컨트롤러를 찾지 못했습니다.";
                        notify?.Invoke(message);
                        Debug.LogWarning($"[MainRecordingSettingsActions] {message}");
                        return false;
                    }

                    resolvedFBXVmdPipeline.OnClickImportButton();
                    return true;

                case MainRecordingSettingsActionType.ComingSoon:
                    notify?.Invoke("아직 연결되지 않은 기능입니다.");
                    return false;

                case MainRecordingSettingsActionType.Close:
                    return true;

                default:
                    return false;
            }
        }

        public static MainRecordingSettingsActionResult ApplySharedSettings(
            MainRecordingSettingsDocument document,
            RecodingSetting recodingSetting,
            FBXVmdPipeline fileManager = null,
            bool startFbxImport = true)
        {
            if (document == null)
            {
                return MainRecordingSettingsActionResult.Failure("공유 설정 문서가 비어 있습니다.");
            }

            if (recodingSetting != null)
            {
                return recodingSetting.ApplySharedSettingsDocument(
                    document,
                    fileManager,
                    startFbxImport);
            }

            if (!string.IsNullOrWhiteSpace(document.fbxPath) && fileManager == null)
            {
                return MainRecordingSettingsActionResult.Failure("FBX 설정을 적용할 FBXVmdPipeline를 찾지 못했습니다.");
            }

            return MainRecordingSettingsActionResult.Success("공유 설정을 적용했습니다.");
        }

        public static MainRecordingSettingsActionResult ApplyForTests(
            MainRecordingSettingsDocument document,
            RecodingSetting recodingSetting,
            FBXVmdPipeline fileManager)
        {
            return ApplySharedSettings(document, recodingSetting, fileManager, false);
        }

        public static FBXVmdPipeline ResolveFBXVmdPipeline(
            RecodingSetting recodingSetting = null,
            FBXVmdPipeline fileManager = null)
        {
            if (fileManager != null)
            {
                return fileManager;
            }

            if (recodingSetting != null && recodingSetting.RecordingFBXVmdPipeline != null)
            {
                return recodingSetting.RecordingFBXVmdPipeline;
            }

            return UnityEngine.Object.FindObjectOfType<FBXVmdPipeline>();
        }
    }
}
