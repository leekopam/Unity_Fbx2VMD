using System.IO;

namespace Fbx2Vmd.Recording
{
    /// <summary>
    /// VMD 출력 파일명 정책.
    /// FBXVmdPipeline에서 추출 (청사진 단계 1-1).
    /// ponytail: 순수 정적 클래스, 의존성 없음.
    /// </summary>
    public static class VMDOutputNamePolicy
    {
        public const string DefaultOutputBaseName = "fbxToVMD";
        public const string SatisfactionReferenceBaseName = "satisfaction_2";
        public const int SatisfactionReferenceMaxMmdFrame = 6000;

        /// <summary>
        /// FBX 파일 경로에서 확장자를 제외한 Base name을 반환한다.
        /// </summary>
        public static string GetBaseName(string fbxPath)
        {
            if (string.IsNullOrWhiteSpace(fbxPath))
            {
                return DefaultOutputBaseName;
            }

            string baseName = Path.GetFileNameWithoutExtension(fbxPath);
            return string.IsNullOrWhiteSpace(baseName) ? DefaultOutputBaseName : baseName;
        }

        /// <summary>
        /// 주어진 base name이 satisfaction_2 참조 출력인지 확인한다.
        /// </summary>
        public static bool IsSatisfactionReference(string baseName)
        {
            return string.Equals(
                baseName,
                SatisfactionReferenceBaseName,
                System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
