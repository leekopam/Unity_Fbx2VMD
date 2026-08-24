using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBX runtime load 단계의 성공 결과 또는 오류를 전달함.
    /// </summary>
    internal sealed class FBXModelImportResult
    {
        internal bool IsSuccess { get; }
        internal GameObject ImportedModel { get; }
        internal string ControlledImportPath { get; }
        internal string OutputBaseName { get; }
        internal string ErrorMessage { get; }

        private FBXModelImportResult(
            bool isSuccess,
            GameObject importedModel,
            string controlledImportPath,
            string outputBaseName,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            ImportedModel = importedModel;
            ControlledImportPath = controlledImportPath ?? string.Empty;
            OutputBaseName = outputBaseName ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        internal static FBXModelImportResult Succeed(
            GameObject importedModel,
            string controlledImportPath,
            string outputBaseName)
        {
            return new FBXModelImportResult(
                true,
                importedModel,
                controlledImportPath,
                outputBaseName,
                string.Empty);
        }

        internal static FBXModelImportResult Fail(string errorMessage)
        {
            return new FBXModelImportResult(false, null, string.Empty, string.Empty, errorMessage);
        }
    }
}
