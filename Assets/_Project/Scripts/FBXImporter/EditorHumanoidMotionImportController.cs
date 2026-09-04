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
            if (clip == null || !clip.humanMotion || clip.length <= 0f)
            {
                return EditorHumanoidMotionImportResult.Fail(
                    $"유효한 Humanoid AnimationClip을 찾을 수 없습니다: {assetPath}");
            }

            _pipeline.SetSessionState(
                FBXVmdPipeline.FBXSessionState.AvatarReady,
                "Humanoid 클립 준비 완료",
                0.7f);

            return EditorHumanoidMotionImportResult.Succeed(
                clip,
                Path.GetFileNameWithoutExtension(controlledPath));
        }
    }

    internal readonly struct EditorHumanoidMotionImportResult
    {
        private EditorHumanoidMotionImportResult(
            bool isSuccess,
            AnimationClip clip,
            string outputBaseName,
            string errorMessage)
        {
            IsSuccess = isSuccess;
            Clip = clip;
            OutputBaseName = outputBaseName;
            ErrorMessage = errorMessage;
        }

        internal bool IsSuccess { get; }
        internal AnimationClip Clip { get; }
        internal string OutputBaseName { get; }
        internal string ErrorMessage { get; }

        internal static EditorHumanoidMotionImportResult Succeed(
            AnimationClip clip,
            string outputBaseName)
        {
            return new EditorHumanoidMotionImportResult(
                true,
                clip,
                outputBaseName,
                string.Empty);
        }

        internal static EditorHumanoidMotionImportResult Fail(string errorMessage)
        {
            return new EditorHumanoidMotionImportResult(
                false,
                null,
                string.Empty,
                errorMessage);
        }
    }
}
#endif
