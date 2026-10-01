using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// JSON 파일의 원자적 저장·손상 백업·읽기를 담당하는 공용 IO.
    /// CharacterLibraryStore와 CharacterPresetStore가 함께 사용한다.
    /// </summary>
    internal static class AtomicJsonFile
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>
        /// 파일을 읽는다. 없으면 exists=false, IO 오류면 ioFailed=true.
        /// 반환값이 false일 때 json은 null이다.
        /// </summary>
        public static bool TryReadAllText(string path, out string json, out bool ioFailed)
        {
            json = null;
            ioFailed = false;

            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                json = File.ReadAllText(path, Utf8NoBom);
                return true;
            }
            catch (Exception error)
            {
                ioFailed = true;
                Debug.LogError(
                    $"[CharacterLibrary] 파일을 읽지 못했습니다: {path} ({error.Message})");
                return false;
            }
        }

        /// <summary>
        /// 임시 파일에 쓴 뒤 File.Replace/Move로 원자적으로 교체한다.
        /// 실패 시 임시 파일을 정리한다.
        /// </summary>
        public static void SaveAtomic(string path, string json)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(tempPath, json, Utf8NoBom);
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // 임시 파일 정리 실패는 저장 결과를 덮지 않는다.
                    }
                }
            }
        }

        /// <summary>
        /// 손상된 파일을 <path>.corrupt-타임스탬프로 옮긴다. 이동 실패는 로그만 남긴다.
        /// </summary>
        public static void TryBackupCorrupt(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            string backupPath = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            try
            {
                File.Move(path, backupPath);
            }
            catch (Exception error)
            {
                // 백업 이동 실패 시에도 기본 문서로 계속 진행한다(원본은 그대로 남는다).
                Debug.LogError(
                    $"[CharacterLibrary] 손상 파일 백업에 실패했습니다: {error.Message}");
            }
        }

        public static DateTime LastWriteTimeUtc(string path)
        {
            return File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;
        }
    }
}
