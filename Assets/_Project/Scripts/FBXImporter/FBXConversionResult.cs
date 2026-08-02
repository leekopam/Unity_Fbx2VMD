namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBX에서 VMD로 변환한 결과를 전달하는 DTO임.
    /// </summary>
    public class FBXConversionResult
    {
        public bool Success { get; }
        public string OutputBaseName { get; }
        public string ErrorMessage { get; }
        public string VmdFilePath { get; }

        private FBXConversionResult(bool success, string outputBaseName, string errorMessage, string vmdFilePath)
        {
            Success = success;
            OutputBaseName = outputBaseName ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            VmdFilePath = vmdFilePath ?? string.Empty;
        }

        public static FBXConversionResult Succeed(string outputBaseName, string vmdFilePath = "")
            => new FBXConversionResult(true, outputBaseName, string.Empty, vmdFilePath);

        public static FBXConversionResult Fail(string errorMessage)
            => new FBXConversionResult(false, string.Empty, errorMessage, string.Empty);
    }
}
