namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBX에서 VMD로 변환하는 요청을 전달하는 DTO임.
    /// </summary>
    public class FBXConversionRequest
    {
        /// <summary>원본 FBX 파일의 절대 경로임.</summary>
        public string SourcePath { get; }

        public FBXConversionRequest(string sourcePath)
        {
            SourcePath = sourcePath;
        }
    }
}
