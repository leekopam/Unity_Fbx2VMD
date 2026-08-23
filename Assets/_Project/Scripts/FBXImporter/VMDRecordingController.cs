using UnityEngine;
using System;
using System.Collections;
using System.IO;
using static Fbx2Vmd.FBXImporter.FBXVmdPipeline;
using Fbx2Vmd.Settings;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// FBXVmdPipeline의 VMD 녹화 수명주기를 담당하는 컴패니언 컨트롤러입니다.
    /// 프리웜, 녹화 실행, 완료 처리와 이벤트 구독 해제를 캡슐화함.
    /// </summary>
    public class VMDRecordingController
    {
        private readonly FBXVmdPipeline _pipeline;
        private HumanoidSampleCode _activeRecorderController;

        public VMDRecordingController(FBXVmdPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        /// <summary>
        /// 안정적인 녹화 시퀀스를 시작함. 지연 대기 → 프리웜 → VMD 녹화 순서로 진행함.
        /// </summary>
        public IEnumerator RecordAsync(
            Animation ghostAnim,
            PoseSpaceRetargeter retargeter,
            GameObject targetObject,
            AnimationClip clip,
            string outputBaseName)
        {
#if UNITY_EDITOR
            bool earlyEditorSmokeRecordingOverrideActive = _pipeline._editorSmokeRecordingOverrideActive;
#else
            bool earlyEditorSmokeRecordingOverrideActive = false;
#endif
            bool earlyShouldStartVmdRecording = FBXVmdPipeline.ShouldStartVmdRecordingAfterImport(
                _pipeline.ShouldRecordVmdAfterImport,
                earlyEditorSmokeRecordingOverrideActive);
            float resolvedStartDelay = VMDRecordingController.ResolveStartDelay(_pipeline.startDelay, earlyShouldStartVmdRecording);
            _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Retargeting, $"녹화 시작 전 {resolvedStartDelay:F1}초 대기", 0.7f);
            if (resolvedStartDelay > 0f)
            {
                yield return new WaitForSeconds(resolvedStartDelay);
            }

            if (ghostAnim == null || clip == null)
            {
                _pipeline.FailSession("녹화 시작에 필요한 Ghost 애니메이션이 없습니다.");
                yield break;
            }

            float recordingStartTime = 0f;
            float recordingPlaybackSpeed = FBXVmdPipeline.ResolveVmdRecordingPlaybackSpeed(_pipeline.vmdRecordingPlaybackSpeed);
            float recordingLength = FBXVmdPipeline.ResolveRecordingLengthForPlaybackSpeed(clip.length, recordingPlaybackSpeed);
            int recordingTargetFrameCount = 0;
            string recordingOutputBaseName = outputBaseName;
            string comparisonLabel = $"auto_{recordingOutputBaseName}";

#if UNITY_EDITOR
            bool editorSmokeRecordingOverrideActive = _pipeline._editorSmokeRecordingOverrideActive;
            float[] diagnosticSampleTimesOverride = _pipeline._editorSmokeSampleTimesOverride;
#else
            bool editorSmokeRecordingOverrideActive = false;
            float[] diagnosticSampleTimesOverride = null;
#endif
            bool shouldStartVmdRecording = FBXVmdPipeline.ShouldStartVmdRecordingAfterImport(
                _pipeline.ShouldRecordVmdAfterImport,
                editorSmokeRecordingOverrideActive);

            HumanoidSampleCode recorderController = null;
            if (shouldStartVmdRecording)
            {
                recorderController = targetObject.GetComponent<HumanoidSampleCode>();
                if (recorderController == null)
                {
                    _pipeline.FailSession("Target Character에 HumanoidSampleCode가 없습니다.");
                    yield break;
                }

                _activeRecorderController = recorderController;
                _activeRecorderController.RecordingFinished += OnRecordingFinished;
                RecordingCaptureResolutionPlan recordingCapturePlan;
                float diagnosticScreenshotPadding = 1.8f;
                float diagnosticScreenshotVerticalViewportCenter = 0.28f;
#if UNITY_EDITOR
                recordingCapturePlan = _pipeline._editorSmokeCaptureResolutionOverrideActive
                    ? RecordingCaptureResolution.CreateCustomPlan(_pipeline._editorSmokeCaptureWidth, _pipeline._editorSmokeCaptureHeight)
                    : RecordingCaptureResolution.CreatePlan(
                        _pipeline.recordingCaptureQuality,
                        _pipeline.customRecordingCaptureWidth,
                        _pipeline.customRecordingCaptureHeight);
                diagnosticScreenshotPadding = float.IsNaN(_pipeline._editorSmokeDiagnosticScreenshotPaddingOverride)
                    ? diagnosticScreenshotPadding
                    : _pipeline._editorSmokeDiagnosticScreenshotPaddingOverride;
                diagnosticScreenshotVerticalViewportCenter = float.IsNaN(_pipeline._editorSmokeDiagnosticScreenshotVerticalViewportCenterOverride)
                    ? diagnosticScreenshotVerticalViewportCenter
                    : _pipeline._editorSmokeDiagnosticScreenshotVerticalViewportCenterOverride;
#else
                recordingCapturePlan = RecordingCaptureResolution.CreatePlan(
                    _pipeline.recordingCaptureQuality,
                    _pipeline.customRecordingCaptureWidth,
                    _pipeline.customRecordingCaptureHeight);
#endif
                _activeRecorderController.SetRecordingDiagnostics(
                    _pipeline.enableRecordingDiagnostics,
                    _pipeline.enableRecordingDiagnostics && _pipeline.enableDiagnosticFingerCloseups,
                    _pipeline.enableRecordingDiagnostics && _pipeline.useDeterministicCaptureFramerateForDiagnostics,
                    diagnosticSampleTimesOverride,
                    recordingCapturePlan.Width,
                    recordingCapturePlan.Height,
                    diagnosticScreenshotPadding,
                    diagnosticScreenshotVerticalViewportCenter);
#if UNITY_EDITOR
                if (_pipeline._editorSmokeRecordingOverrideActive)
                {
                    float requestedDuration = Mathf.Max(0.1f, _pipeline._editorSmokeDurationSeconds);
                    bool hasEditorSmokeTimingOverride =
                        !float.IsNaN(_pipeline._editorSmokeRecordingStartTimeOverrideSeconds) ||
                        !float.IsNaN(_pipeline._editorSmokeRecordingPlaybackSpeedOverride);
                    recordingStartTime = FBXVmdPipeline.CalculateEditorSmokeStartTime(clip, requestedDuration, _pipeline._editorSmokeSegment);
                    if (!float.IsNaN(_pipeline._editorSmokeRecordingStartTimeOverrideSeconds))
                    {
                        recordingStartTime = Mathf.Clamp(
                            _pipeline._editorSmokeRecordingStartTimeOverrideSeconds,
                            0f,
                            Mathf.Max(0f, clip.length));
                    }

                    if (!float.IsNaN(_pipeline._editorSmokeRecordingPlaybackSpeedOverride))
                    {
                        recordingPlaybackSpeed = _pipeline._editorSmokeRecordingPlaybackSpeedOverride;
                    }

                    float safePlaybackSpeed = Mathf.Max(0.0001f, recordingPlaybackSpeed);
                    float remainingLength = Mathf.Max(0.1f, (clip.length - recordingStartTime) / safePlaybackSpeed);
                    recordingLength = Mathf.Min(requestedDuration, remainingLength);
                    recordingTargetFrameCount = Mathf.Min(
                        Mathf.Max(1, _pipeline._editorSmokeTargetFrameCount),
                        Mathf.CeilToInt(recordingLength * FBXVmdPipeline.EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE));
                    recordingOutputBaseName = FBXVmdPipeline.BuildEditorSmokeOutputBaseName(outputBaseName, recordingLength, _pipeline._editorSmokeSegment);
                    comparisonLabel = $"auto_{recordingOutputBaseName}";
                    Debug.Log(
                        $"[Recording] 에디터 스모크 녹화 제한 적용됨. VMD={recordingOutputBaseName}.vmd, " +
                        $"segment={FBXVmdPipeline.GetEditorSmokeSegmentLabel(_pipeline._editorSmokeSegment)}, " +
                        $"start={recordingStartTime:F2}s, duration={recordingLength:F2}s, " +
                        $"targetFrameCount={recordingTargetFrameCount}");

                    if (!hasEditorSmokeTimingOverride &&
                        FBXVmdPipeline.TryBuildKnownMmdReferenceEditorSmokeRecordingPlan(
                        outputBaseName,
                        clip.length,
                        recordingLength,
                        recordingTargetFrameCount,
                        FBXVmdPipeline.EDITOR_DIAGNOSTIC_SMOKE_FRAME_RATE,
                        _pipeline._editorSmokeUseKnownMmdReferenceTiming,
                        out float referenceRecordingLength,
                        out int referenceTargetFrameCount,
                        out float referencePlaybackSpeed))
                    {
                        recordingLength = referenceRecordingLength;
                        recordingTargetFrameCount = referenceTargetFrameCount;
                        recordingPlaybackSpeed = referencePlaybackSpeed;

                        if (recorderController.vmdRecorder != null)
                        {
                            recorderController.vmdRecorder.UseCaptureFramerateDuringRecording = true;
                            recorderController.vmdRecorder.DropLateFrameBacklogWhenNotUsingCaptureFramerate = false;
                        }

                        Debug.Log(
                            $"[Recording] 에디터 스모크 기준 녹화 시간이 적용됨. " +
                            $"clipLength={clip.length:F3}s, recordingLength={recordingLength:F3}s, " +
                            $"targetFrameCount={recordingTargetFrameCount}, playbackSpeed={recordingPlaybackSpeed:F5}");
                    }
                }
                else
#endif
                if (FBXVmdPipeline.TryBuildKnownMmdReferenceRecordingPlan(
                    outputBaseName,
                    clip.length,
                    FBXVmdPipeline.MMD_REFERENCE_FRAME_RATE,
                    _pipeline.useKnownMmdReferenceTiming,
                    out float referenceRecordingLength,
                    out int referenceTargetFrameCount,
                    out float referencePlaybackSpeed))
                {
                    recordingLength = referenceRecordingLength;
                    recordingTargetFrameCount = referenceTargetFrameCount;
                    recordingPlaybackSpeed = referencePlaybackSpeed;

                    if (recorderController.vmdRecorder != null)
                    {
                        recorderController.vmdRecorder.UseCaptureFramerateDuringRecording = true;
                        recorderController.vmdRecorder.DropLateFrameBacklogWhenNotUsingCaptureFramerate = false;
                    }

                    Debug.Log(
                        $"[Recording] 기준 녹화 시간이 적용됨. " +
                        $"clipLength={clip.length:F3}s, recordingLength={recordingLength:F3}s, " +
                        $"targetFrameCount={recordingTargetFrameCount}, playbackSpeed={recordingPlaybackSpeed:F5}");
                }
            }

            int prewarmFrameCount = FBXVmdPipeline.ResolveRetargetPrewarmFrameCountForRecordingMode(
                _pipeline.RetargetPrewarmFrameCount,
                shouldStartVmdRecording);
            int visiblePrewarmYieldFrameCount = FBXVmdPipeline.ResolveRetargetPrewarmVisibleYieldFrameCountForRecordingMode(
                _pipeline.RetargetPrewarmFrameCount,
                shouldStartVmdRecording);
            yield return PrewarmStartPose(
                ghostAnim,
                clip,
                retargeter,
                recordingStartTime,
                recordingPlaybackSpeed,
                prewarmFrameCount,
                visiblePrewarmYieldFrameCount);
            retargeter?.CaptureRecordingStartBaselineSnapshot();
            retargeter?.ResetPlaybackStabilityMetrics();

            if (!shouldStartVmdRecording)
            {
                _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Success, $"FBX 임포트/촬영 준비 완료: {outputBaseName}", 1f);
                Debug.Log($"[Recording] FBX 임포트 및 Unity 촬영 준비 완료됨. 출력={outputBaseName}, VMD 자동 녹화=생략");
                _pipeline._isProcessing = false;
                yield break;
            }

            _pipeline.SetSessionState(FBXVmdPipeline.FBXSessionState.Recording, $"녹화 중: {recordingOutputBaseName}", 0.75f);
            Debug.Log($"[Recording] 자동 녹화 시작됨. VMD={recordingOutputBaseName}.vmd, 비교 라벨={comparisonLabel}");
            bool started = recorderController.StartAutoRecording(
                recordingLength,
                recordingOutputBaseName,
                null,
                recordingTargetFrameCount,
                comparisonLabel: comparisonLabel,
                overwriteExistingOutput: true);
            if (!started)
            {
                _pipeline.FailSession("VMD 녹화를 시작하지 못했습니다.");
            }
        }

        private void OnRecordingFinished(VmdSaveResult result)
        {
            MotionComparisonProbe probe = _activeRecorderController != null
                ? _activeRecorderController.GetComponent<MotionComparisonProbe>()
                : null;
            VmdSaveResult effectiveResult = _pipeline.ApplyEditorSmokeThumbRiskFailure(result, probe);
            TryAppendVmdArtifactToComparisonSessionManifest(probe, effectiveResult);
            TryCopyVmdToAdditionalFolder(effectiveResult);
            ClearActiveRecordingSubscription();
            _pipeline.LogRetargetPlaybackStabilitySummary();

            if (effectiveResult.Success)
            {
                _pipeline.SetSessionState(
                    FBXSessionState.Success,
                    $"VMD 저장 완료: {Path.GetFileName(effectiveResult.FilePath)}",
                    1f);
            }
            else
            {
                string errorMessage = string.IsNullOrWhiteSpace(effectiveResult.ErrorMessage)
                    ? "VMD 저장 실패"
                    : effectiveResult.ErrorMessage;
                _pipeline.SetSessionState(FBXSessionState.Failed, errorMessage, 0f);
            }

            _pipeline.CleanupActiveGhost();
            _pipeline.ResetTargetStateAfterSession(recaptureGuardBaselines: false);
#if UNITY_EDITOR
            _pipeline.NotifyEditorSmokeFinished(effectiveResult);
            _pipeline.ClearEditorSmokeOverride();
#endif
            _pipeline._isProcessing = false;
        }

        private static void TryAppendVmdArtifactToComparisonSessionManifest(
            MotionComparisonProbe probe,
            VmdSaveResult result)
        {
            if (probe == null ||
                result.Success == false ||
                string.IsNullOrWhiteSpace(probe.LastSessionManifestPath) ||
                string.IsNullOrWhiteSpace(result.FilePath))
            {
                return;
            }

            MotionComparisonProbeSessionManifestPatcher.TryAppendExportedVmdToSessionManifest(
                probe.LastSessionManifestPath,
                MakeProjectRelativePath(result.FilePath),
                result.FrameCount,
                result.FileSizeBytes);
        }

        private static string MakeProjectRelativePath(string path)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return MakeProjectRelativePath(path, projectRoot);
        }

        private static string MakeProjectRelativePath(string path, string projectRoot)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string fullPath = Path.GetFullPath(path);
            string fullRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            bool isProjectPath = string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase)
                || (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                    && fullPath.Length > fullRoot.Length
                    && (fullPath[fullRoot.Length] == Path.DirectorySeparatorChar
                        || fullPath[fullRoot.Length] == Path.AltDirectorySeparatorChar));

            if (!isProjectPath)
            {
                return path.Replace("\\", "/");
            }

            return fullPath.Substring(fullRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace("\\", "/");
        }

        private void TryCopyVmdToAdditionalFolder(VmdSaveResult result)
        {
            if (!result.Success ||
                string.IsNullOrWhiteSpace(_pipeline.additionalVmdCopyFolder) ||
                string.IsNullOrWhiteSpace(result.FilePath) ||
                !File.Exists(result.FilePath))
            {
                return;
            }

            try
            {
                string targetFolder = _pipeline.additionalVmdCopyFolder.Trim();
                if (!Path.IsPathRooted(targetFolder))
                {
                    string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                    targetFolder = Path.Combine(projectRoot, targetFolder);
                }

                Directory.CreateDirectory(targetFolder);
                string targetPath = Path.Combine(targetFolder, Path.GetFileName(result.FilePath));
                File.Copy(result.FilePath, targetPath, overwrite: true);
                Debug.Log($"[Recording] VMD 추가 복사 완료됨. 경로={targetPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Recording] VMD 추가 복사 실패함. 오류={ex.Message}");
            }
        }

        internal void ClearActiveRecordingSubscription()
        {
            if (_activeRecorderController == null)
            {
                return;
            }

            _activeRecorderController.RecordingFinished -= OnRecordingFinished;
            _activeRecorderController = null;
        }

        /// <summary>
        /// 녹화 시작 포즈를 프리웜함. Ghost 애니메이터를 시작 시간으로 설정하고 프리웜 프레임만큼 샘플링함.
        /// </summary>
        public IEnumerator PrewarmStartPose(
            Animation ghostAnim,
            AnimationClip clip,
            PoseSpaceRetargeter retargeter,
            float startTimeSeconds,
            float playbackSpeed,
            int configuredPrewarmFrameCount,
            int visiblePrewarmYieldFrameCount)
        {
            ghostAnim.clip = clip;
            if (ghostAnim.GetClip(clip.name) == null)
            {
                ghostAnim.AddClip(clip, clip.name);
            }

            ghostAnim.Play(clip.name);
            AnimationState state = ghostAnim[clip.name];
            if (state == null)
            {
                yield return new WaitForEndOfFrame();
                yield break;
            }

            float sampleTime = Mathf.Clamp(startTimeSeconds, 0f, Mathf.Max(0f, state.length));
            float safePlaybackSpeed = Mathf.Max(0.0001f, playbackSpeed);
            int prewarmFrames = ResolvePrewarmFrameCount(configuredPrewarmFrameCount);
            int visibleYieldFrames = Mathf.Max(0, visiblePrewarmYieldFrameCount);
            if (prewarmFrames <= 0)
            {
                if (!FBXVmdPipeline.PrepareRetargeterRecordingStartPose(retargeter, sampleTime, safePlaybackSpeed, holdPose: false))
                {
                    state.time = sampleTime;
                    state.speed = safePlaybackSpeed;
                    ghostAnim.Sample();
                }

                retargeter?.ApplyLateVisualGroundingCorrection();
                if (visibleYieldFrames > 0)
                {
                    yield return FBXVmdPipeline.YieldRetargetPrewarmFrame();
                }

                yield break;
            }

            state.enabled = true;
            state.wrapMode = WrapMode.Once;
            state.time = sampleTime;
            state.speed = 0f;
            for (int i = 0; i < prewarmFrames; i++)
            {
                if (!FBXVmdPipeline.PrepareRetargeterRecordingStartPose(retargeter, sampleTime, safePlaybackSpeed, holdPose: true))
                {
                    state.time = sampleTime;
                    ghostAnim.Sample();
                }

                yield return FBXVmdPipeline.YieldRetargetPrewarmFrame();
            }

            if (!FBXVmdPipeline.PrepareRetargeterRecordingStartPose(retargeter, sampleTime, safePlaybackSpeed, holdPose: false))
            {
                state.time = sampleTime;
                state.speed = safePlaybackSpeed;
                ghostAnim.Sample();
                ghostAnim.Play(clip.name);
                state.time = sampleTime;
                state.speed = safePlaybackSpeed;
                ghostAnim.Sample();
            }

            retargeter?.ApplyLateVisualGroundingCorrection();
            Debug.Log($"[Retargeting] 리타게팅 프리웜 완료됨. 프레임={prewarmFrames}, 클립 시간={sampleTime:F2}초");
        }

        /// <summary>
        /// 설정된 프리웜 프레임 수를 최대치로 클램프하여 해석함.
        /// </summary>
        public static int ResolvePrewarmFrameCount(int configuredFrameCount)
        {
            return Mathf.Clamp(configuredFrameCount, 0, FBXVmdPipeline.MAX_RETARGET_PREWARM_FRAME_COUNT);
        }

        /// <summary>
        /// 녹화 모드에 따라 시작 지연 시간을 해석함. 녹화하지 않으면 0을 반환함.
        /// </summary>
        public static float ResolveStartDelay(float configuredStartDelay, bool shouldStartVmdRecording)
        {
            if (!shouldStartVmdRecording)
            {
                return 0f;
            }

            if (float.IsNaN(configuredStartDelay) || float.IsInfinity(configuredStartDelay))
            {
                return 0f;
            }

            return Mathf.Clamp(configuredStartDelay, 0f, 10f);
        }
    }
}
