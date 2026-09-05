#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 에디터에서 FBX를 Humanoid 클립으로 준비하는 파일 임포트 경계를 담당함.
    /// </summary>
    internal sealed class EditorHumanoidMotionImportController
    {
        private readonly FBXVmdPipeline _pipeline;
        private readonly FBXImportController _importController;

        internal EditorHumanoidMotionImportController(
            FBXVmdPipeline pipeline,
            FBXImportController importController)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _importController = importController ??
                throw new ArgumentNullException(nameof(importController));
        }

        internal EditorHumanoidMotionImportResult Prepare(string sourcePath)
        {
            if (!FBXImportController.TryValidateSourcePath(
                    sourcePath,
                    out string validationError))
            {
                return EditorHumanoidMotionImportResult.Fail(validationError);
            }

            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.Selected,
                $"선택됨: {Path.GetFileName(sourcePath)}",
                0.05f);

            string controlledPath = _importController.CopyToControlledImportFolder(sourcePath);
            string assetPath = FBXImportController.ToAssetRelativePath(
                controlledPath,
                Application.dataPath);
            if (string.IsNullOrEmpty(assetPath))
            {
                return EditorHumanoidMotionImportResult.Fail(
                    $"Unity Asset 경로를 만들 수 없습니다: {controlledPath}");
            }

            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.Copied,
                $"복제 완료: {Path.GetFileName(controlledPath)}",
                0.15f);
            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.LoadingFbx,
                "Humanoid 클립 준비 중",
                0.3f);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            EditorHumanoidClipImportConfigurator.EnsureHumanoid(assetPath);
            AnimationClip clip = EditorAnimationClipAssetLoader.LoadFirst(assetPath);
            GameObject sourceModelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (clip == null ||
                !clip.humanMotion ||
                clip.length <= 0f ||
                sourceModelAsset == null)
            {
                return EditorHumanoidMotionImportResult.Fail(
                    $"유효한 Humanoid 모델과 AnimationClip을 찾을 수 없습니다: {assetPath}");
            }

            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.AvatarReady,
                "Humanoid 클립 준비 완료",
                0.7f);

            return EditorHumanoidMotionImportResult.Succeed(
                clip,
                sourceModelAsset,
                Path.GetFileNameWithoutExtension(controlledPath));
        }
    }

    internal readonly struct EditorHumanoidMotionImportResult
    {
        private EditorHumanoidMotionImportResult(
            bool isSuccess,
            AnimationClip clip,
            GameObject sourceModelAsset,
            string outputBaseName,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            Clip = clip;
            SourceModelAsset = sourceModelAsset;
            OutputBaseName = outputBaseName;
            ErrorMessage = errorMessage;
        }

        internal bool IsSuccess { get; }
        internal AnimationClip Clip { get; }
        internal GameObject SourceModelAsset { get; }
        internal string OutputBaseName { get; }
        internal string ErrorMessage { get; }

        internal static EditorHumanoidMotionImportResult Succeed(
            AnimationClip clip,
            GameObject sourceModelAsset,
            string outputBaseName)
        {
            return new EditorHumanoidMotionImportResult(
                true,
                clip,
                sourceModelAsset,
                outputBaseName,
                string.Empty);
        }

        internal static EditorHumanoidMotionImportResult Fail(string errorMessage)
        {
            return new EditorHumanoidMotionImportResult(
                false,
                null,
                null,
                string.Empty,
                errorMessage);
        }
    }
}
#endif
