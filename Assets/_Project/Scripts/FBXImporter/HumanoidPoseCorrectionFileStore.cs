using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 모델 중립 Humanoid 보정 문서를 원자적으로 저장하고 검증해 불러옴.
    /// </summary>
    internal static class HumanoidPoseCorrectionFileStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static bool TrySave(
            string filePath,
            HumanoidPoseCorrectionDocument document,
            out string errorMessage)
        {
            string temporaryPath = string.Empty;
            try
            {
                if (!TryResolvePath(filePath, out string resolvedPath, out errorMessage))
                {
                    return false;
                }

                if (document == null)
                {
                    errorMessage = "저장할 Humanoid 보정 문서가 없습니다.";
                    return false;
                }

                if (!document.TryValidate(out errorMessage))
                {
                    return false;
                }

                string directoryPath = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                temporaryPath = resolvedPath + ".tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllText(
                    temporaryPath,
                    JsonUtility.ToJson(document, prettyPrint: true),
                    Utf8NoBom);

                if (File.Exists(resolvedPath))
                {
                    File.Replace(temporaryPath, resolvedPath, null);
                }
                else
                {
                    File.Move(temporaryPath, resolvedPath);
                }

                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Humanoid 보정 문서를 저장하지 못했습니다: {exception.Message}";
                return false;
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }

        internal static bool TryLoad(
            string filePath,
            out HumanoidPoseCorrectionDocument document,
            out string errorMessage)
        {
            document = null;
            try
            {
                if (!TryResolvePath(filePath, out string resolvedPath, out errorMessage))
                {
                    return false;
                }

                if (!File.Exists(resolvedPath))
                {
                    errorMessage = $"Humanoid 보정 문서를 찾지 못했습니다: {resolvedPath}";
                    return false;
                }

                string json = File.ReadAllText(resolvedPath, Utf8NoBom);
                if (string.IsNullOrWhiteSpace(json))
                {
                    errorMessage = "Humanoid 보정 문서가 비어 있습니다.";
                    return false;
                }

                HumanoidPoseCorrectionDocument loadedDocument =
                    JsonUtility.FromJson<HumanoidPoseCorrectionDocument>(json);
                if (loadedDocument == null ||
                    !loadedDocument.TryValidate(out errorMessage))
                {
                    errorMessage = string.IsNullOrWhiteSpace(errorMessage)
                        ? "Humanoid 보정 문서 형식이 올바르지 않습니다."
                        : errorMessage;
                    return false;
                }

                document = loadedDocument;
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Humanoid 보정 문서를 불러오지 못했습니다: {exception.Message}";
                return false;
            }
        }

        private static bool TryResolvePath(
            string filePath,
            out string resolvedPath,
            out string errorMessage)
        {
            resolvedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMessage = "Humanoid 보정 문서 경로가 필요합니다.";
                return false;
            }

            resolvedPath = Path.GetFullPath(filePath.Trim());
            errorMessage = string.Empty;
            return true;
        }

        private static void TryDeleteTemporaryFile(string temporaryPath)
        {
            if (string.IsNullOrWhiteSpace(temporaryPath) ||
                !File.Exists(temporaryPath))
            {
                return;
            }

            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // 저장 결과를 반환한 뒤 임시 파일 정리 실패로 결과를 뒤집지 않음.
            }
            catch (UnauthorizedAccessException)
            {
                // 저장 결과를 반환한 뒤 임시 파일 정리 실패로 결과를 뒤집지 않음.
            }
        }
    }
}
