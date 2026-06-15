using System;
using Member_Han.Modules.FBXImporter;
using UnityEngine;

namespace Member_Han.Modules.Graphics
{
    public static class MainRecordingSettingsActions
    {
        public static bool CanExecute(
            MainRecordingSettingsActionType action,
            RecodingSetting recodingSetting = null,
            FileManager fileManager = null)
        {
            switch (action)
            {
                case MainRecordingSettingsActionType.ImportFbx:
                    return ResolveFileManager(recodingSetting, fileManager) != null;
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
            FileManager fileManager = null,
            Action<string> notify = null)
        {
            switch (action)
            {
                case MainRecordingSettingsActionType.ImportFbx:
                    FileManager resolvedFileManager = ResolveFileManager(recodingSetting, fileManager);
                    if (resolvedFileManager == null)
                    {
                        const string message = "FBX 가져오기 컨트롤러를 찾지 못했습니다.";
                        notify?.Invoke(message);
                        Debug.LogWarning($"[MainRecordingSettingsActions] {message}");
                        return false;
                    }

                    resolvedFileManager.OnClickImportButton();
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
            FileManager fileManager = null,
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
                return MainRecordingSettingsActionResult.Failure("FBX 설정을 적용할 FileManager를 찾지 못했습니다.");
            }

            return MainRecordingSettingsActionResult.Success("공유 설정을 적용했습니다.");
        }

        public static MainRecordingSettingsActionResult ApplyForTests(
            MainRecordingSettingsDocument document,
            RecodingSetting recodingSetting,
            FileManager fileManager)
        {
            return ApplySharedSettings(document, recodingSetting, fileManager, false);
        }

        public static FileManager ResolveFileManager(
            RecodingSetting recodingSetting = null,
            FileManager fileManager = null)
        {
            if (fileManager != null)
            {
                return fileManager;
            }

            if (recodingSetting != null && recodingSetting.RecordingFileManager != null)
            {
                return recodingSetting.RecordingFileManager;
            }

            return UnityEngine.Object.FindObjectOfType<FileManager>();
        }
    }
}
