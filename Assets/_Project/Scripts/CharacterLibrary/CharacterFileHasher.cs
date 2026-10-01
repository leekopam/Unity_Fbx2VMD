using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Fbx2Vmd.CharacterLibrary
{
    /// <summary>
    /// 캐릭터 파일의 내용 지문과 파일 상태를 채취한다.
    /// 해시는 경로가 아니라 내용을 식별하므로 파일 이동/교체 감지에 사용한다.
    /// </summary>
    internal static class CharacterFileHasher
    {
        internal const int ContentHashHexLength = 16;

        /// <summary>
        /// 파일 크기·수정 시각·내용 해시를 한 번에 채취한다.
        /// 읽기 실패 시 false를 돌려준다.
        /// </summary>
        internal static bool TryCaptureFileStat(
            string path,
            out long fileSize,
            out string lastWriteTimeUtc,
            out string contentHash)
        {
            fileSize = 0;
            lastWriteTimeUtc = string.Empty;
            contentHash = string.Empty;

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                {
                    return false;
                }

                fileSize = info.Length;
                lastWriteTimeUtc = info.LastWriteTimeUtc.ToString("O");
                contentHash = ComputeContentHash(path);
                return contentHash.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// "sha256:&lt;앞 16자리&gt;" 형태의 내용 지문을 계산한다.
        /// 이동/교체 감지에는 앞부분만으로 충분하고 저장 크기를 작게 유지한다.
        /// </summary>
        internal static string ComputeContentHash(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder("sha256:", ContentHashHexLength + 7);
                for (int i = 0; i < ContentHashHexLength / 2; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
