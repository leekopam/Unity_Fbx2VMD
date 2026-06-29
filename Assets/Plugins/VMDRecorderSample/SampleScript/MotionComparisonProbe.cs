using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(30000)]
public class MotionComparisonProbe : MonoBehaviour
{
    private static readonly float[] DefaultSampleTimes = { 0f, 3f, 6f, 10f, 13.2f, 20f, 30f, 60f, 120f };
    private const string PoseSpaceRetargeterLegacyClipStateName = "__PoseSpaceRetargeter_GhostClip";
    private const int MinScreenshotWidth = 128;
    private const int MinScreenshotHeight = 128;
    private const int MaxScreenshotWidth = 7680;
    private const int MaxScreenshotHeight = 4320;
    private const float DefaultScreenshotPadding = 1.8f;
    private const float DefaultScreenshotVerticalViewportCenter = 0.28f;
    private const float MinScreenshotPadding = 0.25f;
    private const float MaxScreenshotPadding = 2f;
    private const float MinScreenshotVerticalViewportCenter = 0f;
    private const float MaxScreenshotVerticalViewportCenter = 1f;

    [SerializeField] private string comparisonLabel = "";
    [SerializeField] private float[] sampleTimes = { 0f, 3f, 6f, 10f, 13.2f, 20f, 30f, 60f, 120f };
    [SerializeField] private bool sampleByAnimationClipTime = true;
    [SerializeField] private bool logSamples = true;
    [SerializeField] private bool captureSampleScreenshots = true;
    [SerializeField] private bool captureFingerCloseups = true;
    [SerializeField] private bool captureYybDiagnosticOnlyMetrics = true;
    [SerializeField, Min(MinScreenshotWidth)] private int screenshotWidth = 960;
    [SerializeField, Min(MinScreenshotHeight)] private int screenshotHeight = 960;
    [SerializeField, Range(MinScreenshotPadding, MaxScreenshotPadding)] private float screenshotPadding = DefaultScreenshotPadding;
    [SerializeField, Range(MinScreenshotVerticalViewportCenter, MaxScreenshotVerticalViewportCenter)] private float screenshotVerticalViewportCenter = DefaultScreenshotVerticalViewportCenter;
    [SerializeField, Range(1f, 4f)] private float fingerCloseupPadding = 1.6f;

    private const float DiagnosticFootRadius = 0.04f;
    private const float DiagnosticThumbIndexMaxSpreadAngle = 42f;
    private const float DiagnosticThumbIndexFullRiskAngle = 72f;
    // Keep probe thresholds aligned with the runtime thumb guard defaults.
    private const float DiagnosticThumbPalmProjectionMin = 0.358f;
    private const float DiagnosticThumbPalmProjectionMax = 0.5f;
    private const float DiagnosticThumbHelperDistanceDeltaWarning = 0.003f;
    private const float DiagnosticThumbHelperDistanceDeltaFullRisk = 0.008f;
    private const float DiagnosticThumbHelperRotationWarning = 28f;
    private const float DiagnosticThumbHelperRotationFullRisk = 70f;
    private const float DiagnosticThumbWebbingRotationWarning = 18f;
    private const float DiagnosticThumbWebbingRotationFullRisk = 45f;
    private const float DiagnosticArmTwistWarningMuscle = 1.2f;
    private const float DiagnosticArmTwistFullRiskMuscle = 1.6f;
    private const float DiagnosticSleeveAnchorWarningDegrees = 85f;
    private const float DiagnosticSleeveAnchorFullRiskDegrees = 120f;
    private const float DiagnosticSleeveThicknessWarningRatio = 0.7f;
    private const float DiagnosticSleeveThicknessFullRiskRatio = 0.45f;
    private static readonly HumanBodyBones[] LeftFingerBones =
    {
        HumanBodyBones.LeftHand,
        HumanBodyBones.LeftThumbProximal,
        HumanBodyBones.LeftThumbIntermediate,
        HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.LeftIndexProximal,
        HumanBodyBones.LeftIndexIntermediate,
        HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftMiddleProximal,
        HumanBodyBones.LeftMiddleIntermediate,
        HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal,
        HumanBodyBones.LeftRingIntermediate,
        HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal,
        HumanBodyBones.LeftLittleIntermediate,
        HumanBodyBones.LeftLittleDistal
    };

    private static readonly HumanBodyBones[] RightFingerBones =
    {
        HumanBodyBones.RightHand,
        HumanBodyBones.RightThumbProximal,
        HumanBodyBones.RightThumbIntermediate,
        HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightIndexProximal,
        HumanBodyBones.RightIndexIntermediate,
        HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightMiddleProximal,
        HumanBodyBones.RightMiddleIntermediate,
        HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal,
        HumanBodyBones.RightRingIntermediate,
        HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal,
        HumanBodyBones.RightLittleIntermediate,
        HumanBodyBones.RightLittleDistal
    };

    private Animator _animator;
    private UnityHumanoidVMDRecorder _recorder;
    private Camera _camera;
    private string _csvPath = "";
    private string _screenshotFolder = "";
    private string _screenshotIndexPath = "";
    private string _screenshotSessionIndexPath = "";
    private int _nonBlankScreenshotCount;
    private string _sessionFolder = "";
    private string _sessionManifestPath = "";
    private string _sessionId = "";
    private string _sessionStamp = "";
    private string _evidenceBaseName = "";
    private float _startTime;
    private int _nextSampleIndex;
    private bool _isSampling;
    private Camera _captureCamera;
    private HumanPoseHandler _poseHandler;
    private HumanPose _humanPose;
    private bool _poseWarningLogged;
    private readonly Dictionary<string, Transform> _diagnosticTransformCache = new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _diagnosticInitialDistances = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, Quaternion> _diagnosticInitialRelativeRotations = new Dictionary<string, Quaternion>(StringComparer.Ordinal);
    private bool _isYybDiagnosticTarget;
    private float _maxThumbSpreadRisk = float.NaN;
    private float _maxThumbProjectionRisk = float.NaN;
    private float _maxThumbHelperSeparationRisk = float.NaN;
    private float _maxThumbWebbingRisk = float.NaN;
    private float _maxGenericThumbAnatomyRisk = float.NaN;
    private float _maxYybDeformationRisk = float.NaN;
    private float _maxGenericThumbAnatomyRiskClipTime = float.NaN;
    private float _maxYybDeformationRiskClipTime = float.NaN;
    private int _maxGenericThumbAnatomyRiskRecorderFrame = -1;
    private int _maxYybDeformationRiskRecorderFrame = -1;
    private string _maxGenericThumbAnatomyRiskReason = "";
    private string _maxYybDeformationRiskReason = "";
    private int _riskEvaluationFrameCount;
    private int _leftCoreThumbDiagnosticFrameCount;
    private int _rightCoreThumbDiagnosticFrameCount;
    private int _leftHelperRelationshipFrameCount;
    private int _rightHelperRelationshipFrameCount;
    private bool _leftHelperCoverageRequired;
    private bool _rightHelperCoverageRequired;

    public string LastCsvPath => _csvPath;
    public string LastScreenshotFolder => _screenshotFolder;
    public string LastSessionManifestPath => _sessionManifestPath;
    public int NonBlankScreenshotCount => _nonBlankScreenshotCount;
    public bool HasNonBlankScreenshots => _nonBlankScreenshotCount > 0;
    public bool IsSampling => _isSampling;
    public int ScreenshotWidth => screenshotWidth;
    public int ScreenshotHeight => screenshotHeight;
    public float ScreenshotPadding => screenshotPadding;
    public float ScreenshotVerticalViewportCenter => screenshotVerticalViewportCenter;
    public float MaxThumbSpreadRisk => _maxThumbSpreadRisk;
    public float MaxThumbProjectionRisk => _maxThumbProjectionRisk;
    public float MaxThumbHelperSeparationRisk => _maxThumbHelperSeparationRisk;
    public float MaxThumbWebbingRisk => _maxThumbWebbingRisk;
    public float MaxGenericThumbAnatomyRisk => _maxGenericThumbAnatomyRisk;
    public float MaxYybDeformationRisk => _maxYybDeformationRisk;
    public bool RiskDiagnosticsEnabled => captureYybDiagnosticOnlyMetrics;
    public int RiskEvaluationFrameCount => _riskEvaluationFrameCount;
    public bool LeftThumbCoreAnatomyObserved => _leftCoreThumbDiagnosticFrameCount > 0;
    public bool RightThumbCoreAnatomyObserved => _rightCoreThumbDiagnosticFrameCount > 0;
    public bool HasFullThumbAnatomyCoverage => LeftThumbCoreAnatomyObserved && RightThumbCoreAnatomyObserved;
    public bool LeftThumbHelperCoverageRequired => _leftHelperCoverageRequired;
    public bool RightThumbHelperCoverageRequired => _rightHelperCoverageRequired;
    public bool LeftThumbHelperCoverageSatisfied => !_leftHelperCoverageRequired || _leftHelperRelationshipFrameCount > 0;
    public bool RightThumbHelperCoverageSatisfied => !_rightHelperCoverageRequired || _rightHelperRelationshipFrameCount > 0;
    public bool HasResolvedThumbHelperCoverage => LeftThumbHelperCoverageSatisfied && RightThumbHelperCoverageSatisfied;

    public void SetFingerCloseups(bool enabled) => captureFingerCloseups = enabled;
    public void ResetSampleTimesToDefault() => sampleTimes = (float[])DefaultSampleTimes.Clone();
    public void SetSampleTimes(float[] customSampleTimes) => sampleTimes = NormalizeSampleTimes(customSampleTimes);
    public void SetScreenshotCaptureResolution(int width, int height)
    {
        screenshotWidth = Mathf.Clamp(width, MinScreenshotWidth, MaxScreenshotWidth);
        screenshotHeight = Mathf.Clamp(height, MinScreenshotHeight, MaxScreenshotHeight);
    }

    public void SetScreenshotFraming(float padding, float verticalViewportCenter)
    {
        screenshotPadding = NormalizeScreenshotPadding(padding);
        screenshotVerticalViewportCenter = NormalizeScreenshotVerticalViewportCenter(verticalViewportCenter);
    }

    private static float NormalizeScreenshotPadding(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultScreenshotPadding;
        }

        return Mathf.Clamp(value, MinScreenshotPadding, MaxScreenshotPadding);
    }

    private static float NormalizeScreenshotVerticalViewportCenter(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return DefaultScreenshotVerticalViewportCenter;
        }

        return Mathf.Clamp(value, MinScreenshotVerticalViewportCenter, MaxScreenshotVerticalViewportCenter);
    }

    private static float[] NormalizeSampleTimes(IEnumerable<float> customSampleTimes)
    {
        if (customSampleTimes == null)
        {
            return (float[])DefaultSampleTimes.Clone();
        }

        List<float> normalized = new List<float>();
        foreach (float sampleTime in customSampleTimes)
        {
            if (float.IsNaN(sampleTime) || float.IsInfinity(sampleTime) || sampleTime < 0f)
            {
                continue;
            }

            normalized.Add(sampleTime);
        }

        if (normalized.Count == 0)
        {
            return (float[])DefaultSampleTimes.Clone();
        }

        normalized.Sort();
        List<float> deduplicated = new List<float>(normalized.Count);
        for (int i = 0; i < normalized.Count; i++)
        {
            if (deduplicated.Count > 0 && Mathf.Abs(deduplicated[deduplicated.Count - 1] - normalized[i]) <= 0.0001f)
            {
                continue;
            }

            deduplicated.Add(normalized[i]);
        }

        return deduplicated.ToArray();
    }

    internal static float ResolveDiagnosticSampleClock(
        bool sampleByAnimationClipTime,
        bool recorderUsesCaptureFramerate,
        float[] configuredSampleTimes,
        int recorderFrame,
        float animationClipTime,
        float elapsedFallback)
    {
        if (!sampleByAnimationClipTime)
        {
            return elapsedFallback;
        }

        if (float.IsNaN(animationClipTime) || float.IsInfinity(animationClipTime))
        {
            return elapsedFallback;
        }

        if (elapsedFallback > 0.25f && animationClipTime <= 0.0001f)
        {
            return elapsedFallback;
        }

        return animationClipTime;
    }

    public void StartSampling(string labelOverride = "")
    {
        _animator = GetComponent<Animator>();
        _recorder = GetComponent<UnityHumanoidVMDRecorder>();
        _camera = Camera.main;
        _isYybDiagnosticTarget = IsYybDiagnosticTarget();

        if (_animator == null)
        {
            Debug.LogWarning(MotionComparisonProbeReportWriter.BuildAnimatorMissingWarningMessage());
            return;
        }

        PrepareHumanPoseCapture();
        ResetDiagnosticBaselines();
        ResetRiskSummary();

        comparisonLabel = MotionComparisonProbeReportWriter.BuildComparisonLabel(
            comparisonLabel,
            labelOverride,
            gameObject.name);

        _sessionStamp = MotionComparisonProbeReportWriter.BuildSessionStamp(DateTime.Now);
        string sceneName = SceneManager.GetActiveScene().name;
        MotionComparisonProbeSamplingSessionOutputPaths samplingOutputPaths =
            MotionComparisonProbeOutputPaths.BuildSamplingSessionOutputPaths(
                Application.dataPath,
                _sessionStamp,
                sceneName,
                comparisonLabel);
        _evidenceBaseName = samplingOutputPaths.EvidenceBaseName;
        _sessionId = samplingOutputPaths.SessionId;
        _csvPath = samplingOutputPaths.MetricsCsvPath;
        MotionComparisonProbeReportWriter.WriteMetricsCsvHeader(_csvPath);
        PrepareSessionOutput();
        WriteSessionManifest(MotionComparisonProbeReportWriter.BuildSessionStartedReason());

        _startTime = Time.time;
        _nextSampleIndex = 0;
        _isSampling = true;
        if (_recorder != null && _recorder.FrameNumber != 0)
        {
            Debug.LogWarning(MotionComparisonProbeReportWriter.BuildNonZeroRecorderFrameStartWarningMessage(_recorder.FrameNumber));
        }

        PreparePlaybackProbeForMotionComparisonSample();
        SampleNow(MotionComparisonProbeReportWriter.BuildSamplingStartReason());
        SkipElapsedSampleTimes(GetCurrentSampleClock(0f));
    }

    public void StopSampling()
    {
        StopSampling(MotionComparisonProbeReportWriter.BuildSamplingStopReason());
    }

    public void StopSampling(string reason)
    {
        if (!_isSampling)
        {
            return;
        }

        SampleNow(reason);
        _isSampling = false;
        WriteSessionManifest(reason);
    }

    public void SampleNow()
    {
        SampleNow(MotionComparisonProbeReportWriter.BuildSamplingDefaultReason());
    }

    public void SampleNow(string reason)
    {
        if (_animator == null || string.IsNullOrEmpty(_csvPath))
        {
            return;
        }

        PreparePlaybackProbeForMotionComparisonSample();
        PoseMetrics metrics = CaptureMetrics(reason);
        UpdateRiskSummary(metrics.YybDiagnostics, false, reason, metrics.AnimationClipTime, metrics.RecorderFrame);
        MotionComparisonProbeReportWriter.AppendMetricsCsvLine(_csvPath, metrics.ToCsvLine());
        CaptureSampleScreenshots(reason, metrics);
        WriteSessionManifest(reason);

        if (logSamples)
        {
            Debug.Log(MotionComparisonProbeReportWriter.BuildSampleLogMessage(
                comparisonLabel,
                reason,
                metrics.Elapsed,
                metrics.AnimationClipTime,
                metrics.RecorderFrame,
                metrics.HipsY,
                metrics.CameraFacingDot,
                metrics.MaxScaleDelta,
                metrics.YybDiagnostics.MaxDeformationRisk));
        }
    }

    private void LateUpdate()
    {
        if (!_isSampling)
        {
            return;
        }

        UpdateRealtimeRiskSummary();

        if (sampleTimes == null || sampleTimes.Length == 0)
        {
            return;
        }

        float elapsed = Time.time - _startTime;
        float sampleClock = GetCurrentSampleClock(elapsed);
        while (_nextSampleIndex < sampleTimes.Length && sampleClock >= sampleTimes[_nextSampleIndex])
        {
            SampleNow(MotionComparisonProbeReportWriter.BuildSampleTimeReason(sampleTimes[_nextSampleIndex]));
            _nextSampleIndex++;
        }
    }

    private void OnDisable()
    {
        if (!_isSampling)
        {
            return;
        }

        if (!Application.isPlaying || Time.time < _startTime)
        {
            _isSampling = false;
            DestroyCaptureCamera();
            return;
        }

        StopSampling(MotionComparisonProbeReportWriter.BuildSamplingDisabledReason());
    }

    private void OnDestroy()
    {
        DisposeHumanPoseCapture();
        DestroyCaptureCamera();
    }

    private void SkipElapsedSampleTimes(float elapsed)
    {
        if (sampleTimes == null)
        {
            return;
        }

        while (_nextSampleIndex < sampleTimes.Length && sampleTimes[_nextSampleIndex] <= elapsed)
        {
            _nextSampleIndex++;
        }
    }

    private void PreparePlaybackProbeForMotionComparisonSample()
    {
        VmdPlaybackProbe playbackProbe = GetComponent<VmdPlaybackProbe>();
        if (playbackProbe != null)
        {
            playbackProbe.PrepareForMotionComparisonSample();
        }
    }

    private float GetCurrentSampleClock(float elapsedFallback)
    {
        if (!sampleByAnimationClipTime)
        {
            return elapsedFallback;
        }

        AnimationTimeMetrics animationTime = CaptureAnimationTimeMetrics();
        return ResolveDiagnosticSampleClock(
            sampleByAnimationClipTime,
            _recorder != null && _recorder.UseCaptureFramerateDuringRecording,
            sampleTimes,
            _recorder != null ? _recorder.FrameNumber : -1,
            animationTime.ClipTime,
            elapsedFallback);
    }

    private PoseMetrics CaptureMetrics(string reason)
    {
        Transform root = _animator.transform;
        Transform hips = GetBone(HumanBodyBones.Hips);
        Transform leftFoot = GetBone(HumanBodyBones.LeftFoot);
        Transform rightFoot = GetBone(HumanBodyBones.RightFoot);
        Vector3 leftFootPosition = leftFoot != null ? leftFoot.position : EmptyVector();
        Vector3 rightFootPosition = rightFoot != null ? rightFoot.position : EmptyVector();

        float lowestFootY = float.NaN;
        if (leftFoot != null && rightFoot != null)
        {
            lowestFootY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        }
        else if (leftFoot != null)
        {
            lowestFootY = leftFoot.position.y;
        }
        else if (rightFoot != null)
        {
            lowestFootY = rightFoot.position.y;
        }

        ArmMuscleMetrics armMuscles = CaptureArmMuscles();
        FingerMetrics fingers = CaptureFingerMetrics();
        AnimationTimeMetrics animationTime = CaptureAnimationTimeMetrics();
        RootSpikeMetrics rootSpikeMetrics = CaptureRootSpikeMetrics();
        float lowestFootBottomY = float.IsNaN(lowestFootY) ? float.NaN : lowestFootY - DiagnosticFootRadius;
        float groundY = float.IsNaN(rootSpikeMetrics.LastGroundingTargetY) ? 0f : rootSpikeMetrics.LastGroundingTargetY;
        float bodyPositionY = CaptureBodyPositionY();
        float hipsLocalY = hips != null ? hips.localPosition.y : float.NaN;
        Vector3 hipsPosition = hips != null ? hips.position : EmptyVector();
        float meshBoundsMinY = float.NaN;
        float meshBoundsMaxY = float.NaN;
        if (TryGetRendererBounds(out Bounds rendererBounds))
        {
            meshBoundsMinY = rendererBounds.min.y;
            meshBoundsMaxY = rendererBounds.max.y;
        }

        ThumbGuardDiagnostics thumbGuardDiagnostics = CaptureThumbGuardDiagnostics();
        ArmSwingGuardDiagnostics armSwingGuardDiagnostics = CaptureArmSwingGuardDiagnostics();
        YybDiagnosticMetrics yybDiagnostics = captureYybDiagnosticOnlyMetrics
            ? CaptureYybDiagnosticMetrics(armMuscles)
            : YybDiagnosticMetrics.Empty;
        HandTorsoClearanceMetrics handTorsoClearance = CaptureHandTorsoClearanceMetrics(root);

        return new PoseMetrics
        {
            Label = comparisonLabel,
            Scene = SceneManager.GetActiveScene().name,
            Reason = reason,
            Elapsed = Time.time - _startTime,
            TimeSinceLevelLoad = Time.timeSinceLevelLoad,
            FrameCount = Time.frameCount,
            RecorderFrame = _recorder != null ? _recorder.FrameNumber : -1,
            AnimationTimeSource = animationTime.Source,
            AnimationClipName = animationTime.ClipName,
            AnimationClipTime = animationTime.ClipTime,
            AnimationClipLength = animationTime.ClipLength,
            AnimationNormalizedTime = animationTime.NormalizedTime,
            RootPosition = root.position,
            RootYaw = root.eulerAngles.y,
            RootSpike = rootSpikeMetrics,
            BodyPositionY = bodyPositionY,
            HipsLocalY = hipsLocalY,
            HipsPosition = hipsPosition,
            HipsY = hips != null ? hips.position.y : float.NaN,
            LowestFootY = lowestFootY,
            LowestFootBottomY = lowestFootBottomY,
            LeftFootPosition = leftFootPosition,
            RightFootPosition = rightFootPosition,
            MeshBoundsMinY = meshBoundsMinY,
            MeshBoundsMaxY = meshBoundsMaxY,
            FootBottomGroundGap = float.IsNaN(lowestFootBottomY) ? float.NaN : lowestFootBottomY - groundY,
            MeshBoundsGroundGap = float.IsNaN(meshBoundsMinY) ? float.NaN : meshBoundsMinY - groundY,
            CameraFacingDot = CalculateCameraFacingDot(root),
            MaxScaleDelta = CalculateMaxScaleDelta(),
            LeftUpperArmScale = GetLocalScale(HumanBodyBones.LeftUpperArm),
            RightUpperArmScale = GetLocalScale(HumanBodyBones.RightUpperArm),
            LeftUpperLegScale = GetLocalScale(HumanBodyBones.LeftUpperLeg),
            RightUpperLegScale = GetLocalScale(HumanBodyBones.RightUpperLeg),
            SpineLocalEuler = GetLocalEuler(HumanBodyBones.Spine),
            ChestLocalEuler = GetLocalEuler(HumanBodyBones.Chest),
            UpperChestLocalEuler = GetLocalEuler(HumanBodyBones.UpperChest),
            LeftShoulderLocalEuler = GetLocalEuler(HumanBodyBones.LeftShoulder),
            RightShoulderLocalEuler = GetLocalEuler(HumanBodyBones.RightShoulder),
            LeftUpperArmLocalEuler = GetLocalEuler(HumanBodyBones.LeftUpperArm),
            RightUpperArmLocalEuler = GetLocalEuler(HumanBodyBones.RightUpperArm),
            LeftLowerArmLocalEuler = GetLocalEuler(HumanBodyBones.LeftLowerArm),
            RightLowerArmLocalEuler = GetLocalEuler(HumanBodyBones.RightLowerArm),
            LeftHandLocalEuler = GetLocalEuler(HumanBodyBones.LeftHand),
            RightHandLocalEuler = GetLocalEuler(HumanBodyBones.RightHand),
            LeftThumbProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftThumbProximal),
            LeftIndexProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftIndexProximal),
            LeftMiddleProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftMiddleProximal),
            LeftRingProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftRingProximal),
            LeftLittleProximalLocalEuler = GetLocalEuler(HumanBodyBones.LeftLittleProximal),
            RightThumbProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightThumbProximal),
            RightIndexProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightIndexProximal),
            RightMiddleProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightMiddleProximal),
            RightRingProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightRingProximal),
            RightLittleProximalLocalEuler = GetLocalEuler(HumanBodyBones.RightLittleProximal),
            LeftArmLength = CalculateChainLength(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            RightArmLength = CalculateChainLength(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            LeftLegLength = CalculateChainLength(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            RightLegLength = CalculateChainLength(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            LeftElbowAngle = CalculateJointAngle(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
            RightElbowAngle = CalculateJointAngle(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
            LeftKneeAngle = CalculateJointAngle(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot),
            RightKneeAngle = CalculateJointAngle(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot),
            LeftElbowBendForward = CalculateBendForwardDot(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightElbowBendForward = CalculateBendForwardDot(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftKneeBendForward = CalculateBendForwardDot(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, root),
            RightKneeBendForward = CalculateBendForwardDot(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, root),
            LeftElbowBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightElbowBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftKneeBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, root),
            RightKneeBendOffsetForward = CalculateBendOffsetForwardDot(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, root),
            LeftUpperArmDownDot = CalculateUpperArmDownDot(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, root),
            RightUpperArmDownDot = CalculateUpperArmDownDot(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, root),
            LeftHandHorizontalRatio = CalculateHandHorizontalRatio(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightHandHorizontalRatio = CalculateHandHorizontalRatio(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftHandBelowShoulderRatio = CalculateHandBelowShoulderRatio(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, root),
            RightHandBelowShoulderRatio = CalculateHandBelowShoulderRatio(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, root),
            LeftHandTorsoSignedClearance = handTorsoClearance.LeftSignedClearance,
            RightHandTorsoSignedClearance = handTorsoClearance.RightSignedClearance,
            MinHandTorsoSignedClearance = handTorsoClearance.MinSignedClearance,
            HandTorsoPenetrationRisk = handTorsoClearance.PenetrationRisk,
            LeftShoulderDownUpMuscle = armMuscles.LeftShoulderDownUp,
            LeftShoulderFrontBackMuscle = armMuscles.LeftShoulderFrontBack,
            LeftArmDownUpMuscle = armMuscles.LeftArmDownUp,
            LeftArmFrontBackMuscle = armMuscles.LeftArmFrontBack,
            LeftArmTwistMuscle = armMuscles.LeftArmTwist,
            LeftForearmStretchMuscle = armMuscles.LeftForearmStretch,
            LeftForearmTwistMuscle = armMuscles.LeftForearmTwist,
            RightShoulderDownUpMuscle = armMuscles.RightShoulderDownUp,
            RightShoulderFrontBackMuscle = armMuscles.RightShoulderFrontBack,
            RightArmDownUpMuscle = armMuscles.RightArmDownUp,
            RightArmFrontBackMuscle = armMuscles.RightArmFrontBack,
            RightArmTwistMuscle = armMuscles.RightArmTwist,
            RightForearmStretchMuscle = armMuscles.RightForearmStretch,
            RightForearmTwistMuscle = armMuscles.RightForearmTwist,
            ArmSwingGuard = armSwingGuardDiagnostics,
            LeftThumb1StretchMuscle = fingers.LeftThumb1Stretch,
            LeftThumbSpreadMuscle = fingers.LeftThumbSpread,
            LeftIndex1StretchMuscle = fingers.LeftIndex1Stretch,
            LeftIndexSpreadMuscle = fingers.LeftIndexSpread,
            LeftMiddle1StretchMuscle = fingers.LeftMiddle1Stretch,
            LeftMiddleSpreadMuscle = fingers.LeftMiddleSpread,
            LeftRing1StretchMuscle = fingers.LeftRing1Stretch,
            LeftRingSpreadMuscle = fingers.LeftRingSpread,
            LeftLittle1StretchMuscle = fingers.LeftLittle1Stretch,
            LeftLittleSpreadMuscle = fingers.LeftLittleSpread,
            RightThumb1StretchMuscle = fingers.RightThumb1Stretch,
            RightThumbSpreadMuscle = fingers.RightThumbSpread,
            RightIndex1StretchMuscle = fingers.RightIndex1Stretch,
            RightIndexSpreadMuscle = fingers.RightIndexSpread,
            RightMiddle1StretchMuscle = fingers.RightMiddle1Stretch,
            RightMiddleSpreadMuscle = fingers.RightMiddleSpread,
            RightRing1StretchMuscle = fingers.RightRing1Stretch,
            RightRingSpreadMuscle = fingers.RightRingSpread,
            RightLittle1StretchMuscle = fingers.RightLittle1Stretch,
            RightLittleSpreadMuscle = fingers.RightLittleSpread,
            ThumbGuard = thumbGuardDiagnostics,
            YybDiagnostics = yybDiagnostics
        };
    }

    private float CaptureBodyPositionY()
    {
        if (_animator == null || _animator.avatar == null || !_animator.avatar.isHuman)
        {
            return float.NaN;
        }

        HumanPoseHandler handler = null;
        try
        {
            handler = new HumanPoseHandler(_animator.avatar, _animator.transform);
            HumanPose pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            return pose.bodyPosition.y;
        }
        catch
        {
            return float.NaN;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    private AnimationTimeMetrics CaptureAnimationTimeMetrics()
    {
        if (TryCaptureRetargeterAnimationTime(out AnimationTimeMetrics retargeterMetrics))
        {
            return retargeterMetrics;
        }

        if (TryCaptureAnimatorAnimationTime(out AnimationTimeMetrics animatorMetrics))
        {
            return animatorMetrics;
        }

        return AnimationTimeMetrics.Empty;
    }

    private bool TryCaptureRetargeterAnimationTime(out AnimationTimeMetrics metrics)
    {
        metrics = AnimationTimeMetrics.Empty;

        Component retargeter = FindRetargeterForCurrentAnimator();
        if (retargeter == null)
        {
            return false;
        }

        FieldInfo legacyAnimationField = retargeter.GetType().GetField("_legacyAnim", BindingFlags.Instance | BindingFlags.NonPublic);
        Animation legacyAnimation = legacyAnimationField != null ? legacyAnimationField.GetValue(retargeter) as Animation : null;
        if (legacyAnimation == null || legacyAnimation.clip == null)
        {
            return false;
        }

        AnimationClip clip = legacyAnimation.clip;
        AnimationState state = legacyAnimation[PoseSpaceRetargeterLegacyClipStateName] ?? legacyAnimation[clip.name];
        float clipLength = state != null ? state.length : clip.length;
        if (clipLength <= 0f || float.IsNaN(clipLength) || float.IsInfinity(clipLength))
        {
            return false;
        }

        float clipTime = state != null ? state.time : 0f;
        string source = MotionComparisonProbeReportWriter.BuildRetargeterLegacyAnimationTimeSourceLabel();
        if (clipTime <= 0.0001f && _recorder != null && _recorder.FrameNumber > 0)
        {
            clipTime = _recorder.FrameNumber / 30f;
            source = MotionComparisonProbeReportWriter.BuildRetargeterLegacyRecorderFrameAnimationTimeSourceLabel();
        }

        clipTime = Mathf.Clamp(clipTime, 0f, clipLength);

        metrics = new AnimationTimeMetrics
        {
            Source = source,
            ClipName = clip.name,
            ClipTime = clipTime,
            ClipLength = clipLength,
            NormalizedTime = clipLength > 0f ? clipTime / clipLength : float.NaN
        };
        return true;
    }

    private Component FindRetargeterForCurrentAnimator()
    {
        Component fallback = null;
        int retargeterCount = 0;
        Component[] components = UnityEngine.Object.FindObjectsOfType<Component>();
        foreach (Component component in components)
        {
            if (component == null || component.GetType().Name != "PoseSpaceRetargeter")
            {
                continue;
            }

            retargeterCount++;
            fallback ??= component;

            FieldInfo targetAnimatorField = component.GetType().GetField("targetAnimator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Animator targetAnimator = targetAnimatorField != null ? targetAnimatorField.GetValue(component) as Animator : null;
            if (targetAnimator == _animator)
            {
                return component;
            }
        }

        return retargeterCount == 1 ? fallback : null;
    }

    private bool TryCaptureAnimatorAnimationTime(out AnimationTimeMetrics metrics)
    {
        metrics = AnimationTimeMetrics.Empty;
        if (_animator == null || !_animator.isInitialized)
        {
            return false;
        }

        const int layerIndex = 0;
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (float.IsNaN(stateInfo.normalizedTime) || float.IsInfinity(stateInfo.normalizedTime))
        {
            return false;
        }

        AnimationClip clip = ResolveCurrentAnimatorClip(layerIndex);
        float clipLength = clip != null ? clip.length : stateInfo.length;
        if (clipLength <= 0f || float.IsNaN(clipLength) || float.IsInfinity(clipLength))
        {
            return false;
        }

        float rawClipTime = stateInfo.normalizedTime * clipLength;
        float clipTime = stateInfo.loop
            ? Mathf.Repeat(rawClipTime, clipLength)
            : Mathf.Clamp(rawClipTime, 0f, clipLength);

        metrics = new AnimationTimeMetrics
        {
            Source = MotionComparisonProbeReportWriter.BuildAnimatorStateAnimationTimeSourceLabel(),
            ClipName = clip != null ? clip.name : "",
            ClipTime = clipTime,
            ClipLength = clipLength,
            NormalizedTime = stateInfo.normalizedTime
        };
        return true;
    }

    private RootSpikeMetrics CaptureRootSpikeMetrics()
    {
        Component retargeter = FindRetargeterForCurrentAnimator();
        if (retargeter == null)
        {
            return RootSpikeMetrics.Empty;
        }

        Type type = retargeter.GetType();
        float lastGroundingVerticalStep = ReadFloatProperty(type, retargeter, "LastGroundingVerticalStep");
        float groundingMaxStepPerFrame = ReadFloatMember(type, retargeter, "maxGroundingVerticalStepPerFrame");
        return new RootSpikeMetrics
        {
            LastRootDeltaMagnitude = ReadFloatProperty(type, retargeter, "LastRootDeltaMagnitude"),
            MaxRootDeltaMagnitude = ReadFloatProperty(type, retargeter, "MaxRootDeltaMagnitude"),
            RootDeltaSpikeSkippedCount = ReadIntProperty(type, retargeter, "RootDeltaSpikeSkippedCount"),
            LastRootPositionPoseDeltaMagnitude = ReadFloatProperty(type, retargeter, "LastRootPositionPoseDeltaMagnitude"),
            MaxRootPositionPoseDeltaMagnitude = ReadFloatProperty(type, retargeter, "MaxRootPositionPoseDeltaMagnitude"),
            RootPositionSpikeClampedCount = ReadIntProperty(type, retargeter, "RootPositionSpikeClampedCount"),
            LastGroundingAdjustment = ReadFloatProperty(type, retargeter, "LastGroundingAdjustment"),
            MaxGroundingAdjustment = ReadFloatProperty(type, retargeter, "MaxGroundingAdjustment"),
            GroundingStepClampedCount = ReadIntProperty(type, retargeter, "GroundingStepClampedCount"),
            GroundingSmoothedCount = ReadIntProperty(type, retargeter, "GroundingSmoothedCount"),
            LastGroundingVerticalStep = lastGroundingVerticalStep,
            MaxGroundingVerticalStep = ReadFloatProperty(type, retargeter, "MaxGroundingVerticalStep"),
            InitialGroundingVerticalStep = ReadFloatProperty(type, retargeter, "InitialGroundingVerticalStep"),
            MaxGroundingVerticalStepAfterInitial = ReadFloatProperty(type, retargeter, "MaxGroundingVerticalStepAfterInitial"),
            LastGroundingTargetY = ReadFloatProperty(type, retargeter, "LastGroundingTargetY"),
            LastGroundingLowestFootBottomY = ReadFloatProperty(type, retargeter, "LastGroundingLowestFootBottomY"),
            FootHeightReferenceLift = ReadFloatProperty(type, retargeter, "LastEditorFootHeightGroundingReferenceLift"),
            RecordingStartRootY = ReadFloatProperty(type, retargeter, "RecordingStartRootY"),
            RecordingStartBodyPositionY = ReadFloatProperty(type, retargeter, "RecordingStartBodyPositionY"),
            RecordingStartHipsLocalY = ReadFloatProperty(type, retargeter, "RecordingStartHipsLocalY"),
            RecordingStartHipsY = ReadFloatProperty(type, retargeter, "RecordingStartHipsY"),
            RecordingStartHipsReferenceBeforeLocalY = ReadFloatProperty(type, retargeter, "RecordingStartHipsReferenceBeforeLocalY"),
            RecordingStartHipsReferenceAfterLocalY = ReadFloatProperty(type, retargeter, "RecordingStartHipsReferenceAfterLocalY"),
            RecordingStartHipsReferenceDeltaY = ReadFloatProperty(type, retargeter, "RecordingStartHipsReferenceDeltaY"),
            RecordingStartHipsReferenceFlipDetected = ReadIntProperty(type, retargeter, "RecordingStartHipsReferenceFlipDetected"),
            RecordingStartHipsReferenceStage = ReadStringProperty(type, retargeter, "RecordingStartHipsReferenceStage"),
            PoseInputLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputLeftShoulderFrontBackMuscle"),
            AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceLeftShoulderFrontBackMuscle"),
            AfterClampPoseMusclesLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesLeftShoulderFrontBackMuscle"),
            AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardLeftShoulderFrontBackMuscle"),
            AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle"),
            SetHumanPoseInputLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftShoulderFrontBackMuscle"),
            SetHumanPoseOutputLeftShoulderFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftShoulderFrontBackMuscle"),
            SetHumanPoseLeftShoulderFrontBackDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftShoulderFrontBackDelta"),
            PoseInputLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputLeftArmTwistMuscle"),
            AfterEditorMuscleReferenceLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceLeftArmTwistMuscle"),
            AfterClampPoseMusclesLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesLeftArmTwistMuscle"),
            AfterAnatomicalArmGuardLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardLeftArmTwistMuscle"),
            AfterVisualSpikeSmoothingLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingLeftArmTwistMuscle"),
            SetHumanPoseInputLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftArmTwistMuscle"),
            SetHumanPoseOutputLeftArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftArmTwistMuscle"),
            SetHumanPoseLeftArmTwistDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftArmTwistDelta"),
            PoseInputLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputLeftForearmStretchMuscle"),
            AfterEditorMuscleReferenceLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceLeftForearmStretchMuscle"),
            AfterClampPoseMusclesLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesLeftForearmStretchMuscle"),
            AfterAnatomicalArmGuardLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardLeftForearmStretchMuscle"),
            AfterVisualSpikeSmoothingLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingLeftForearmStretchMuscle"),
            SetHumanPoseInputLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftForearmStretchMuscle"),
            SetHumanPoseOutputLeftForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftForearmStretchMuscle"),
            SetHumanPoseLeftForearmStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftForearmStretchDelta"),
            PoseInputRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputRightForearmStretchMuscle"),
            AfterEditorMuscleReferenceRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceRightForearmStretchMuscle"),
            AfterClampPoseMusclesRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesRightForearmStretchMuscle"),
            AfterAnatomicalArmGuardRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardRightForearmStretchMuscle"),
            AfterVisualSpikeSmoothingRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingRightForearmStretchMuscle"),
            SetHumanPoseInputRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightForearmStretchMuscle"),
            SetHumanPoseOutputRightForearmStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightForearmStretchMuscle"),
            SetHumanPoseRightForearmStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightForearmStretchDelta"),
            PoseInputRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastPoseInputRightArmTwistMuscle"),
            AfterEditorMuscleReferenceRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterEditorMuscleReferenceRightArmTwistMuscle"),
            AfterClampPoseMusclesRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterClampPoseMusclesRightArmTwistMuscle"),
            AfterAnatomicalArmGuardRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterAnatomicalArmGuardRightArmTwistMuscle"),
            AfterVisualSpikeSmoothingRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastAfterVisualSpikeSmoothingRightArmTwistMuscle"),
            SetHumanPoseInputRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightArmTwistMuscle"),
            SetHumanPoseOutputRightArmTwistMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightArmTwistMuscle"),
            SetHumanPoseRightArmTwistDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightArmTwistDelta"),
            SetHumanPoseInputLeftUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftUpperLegFrontBackMuscle"),
            SetHumanPoseOutputLeftUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftUpperLegFrontBackMuscle"),
            SetHumanPoseLeftUpperLegFrontBackDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftUpperLegFrontBackDelta"),
            SetHumanPoseInputRightUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightUpperLegFrontBackMuscle"),
            SetHumanPoseOutputRightUpperLegFrontBackMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightUpperLegFrontBackMuscle"),
            SetHumanPoseRightUpperLegFrontBackDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightUpperLegFrontBackDelta"),
            SetHumanPoseInputLeftLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftLowerLegStretchMuscle"),
            SetHumanPoseOutputLeftLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftLowerLegStretchMuscle"),
            SetHumanPoseLeftLowerLegStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftLowerLegStretchDelta"),
            SetHumanPoseInputRightLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightLowerLegStretchMuscle"),
            SetHumanPoseOutputRightLowerLegStretchMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightLowerLegStretchMuscle"),
            SetHumanPoseRightLowerLegStretchDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightLowerLegStretchDelta"),
            SetHumanPoseInputLeftFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputLeftFootUpDownMuscle"),
            SetHumanPoseOutputLeftFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputLeftFootUpDownMuscle"),
            SetHumanPoseLeftFootUpDownDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseLeftFootUpDownDelta"),
            SetHumanPoseInputRightFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseInputRightFootUpDownMuscle"),
            SetHumanPoseOutputRightFootUpDownMuscle = ReadFloatProperty(type, retargeter, "LastSetHumanPoseOutputRightFootUpDownMuscle"),
            SetHumanPoseRightFootUpDownDelta = ReadFloatProperty(type, retargeter, "LastSetHumanPoseRightFootUpDownDelta"),
            RetargetStageGhost = ReadRetargetEndpointStage(type, retargeter, "Ghost"),
            RetargetStageAfterSetHumanPose = ReadRetargetEndpointStage(type, retargeter, "AfterSetHumanPose"),
            RetargetStageAfterManualReferences = ReadRetargetEndpointStage(type, retargeter, "AfterManualReferences"),
            RetargetStageAfterRootRestore = ReadRetargetEndpointStage(type, retargeter, "AfterRootRestore"),
            RetargetStageAfterRootDelta = ReadRetargetEndpointStage(type, retargeter, "AfterRootDelta"),
            RetargetStageAfterGrounding = ReadRetargetEndpointStage(type, retargeter, "AfterGrounding"),
            EditorFootLocalRotationLeftFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootLocalRotationLeftFootXzDelta"),
            EditorFootLocalRotationRightFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootLocalRotationRightFootXzDelta"),
            EditorLowerBodySegmentDirectionLeftFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootXzDelta"),
            EditorLowerBodySegmentDirectionRightFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootXzDelta"),
            EditorLowerBodySegmentDirectionMaxCorrectionSegment = ReadStringProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionSegment"),
            EditorLowerBodySegmentDirectionMaxCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAngle"),
            EditorLowerBodySegmentDirectionMaxPreAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreAngle"),
            EditorLowerBodySegmentDirectionMaxPostAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostAngle"),
            EditorLowerBodySegmentDirectionMaxCorrectionAxisX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAxisX"),
            EditorLowerBodySegmentDirectionMaxCorrectionAxisY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAxisY"),
            EditorLowerBodySegmentDirectionMaxCorrectionAxisZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxCorrectionAxisZ"),
            EditorLowerBodySegmentDirectionMaxReferenceDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxReferenceDirectionX"),
            EditorLowerBodySegmentDirectionMaxReferenceDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxReferenceDirectionY"),
            EditorLowerBodySegmentDirectionMaxReferenceDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxReferenceDirectionZ"),
            EditorLowerBodySegmentDirectionMaxPreDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreDirectionX"),
            EditorLowerBodySegmentDirectionMaxPreDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreDirectionY"),
            EditorLowerBodySegmentDirectionMaxPreDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPreDirectionZ"),
            EditorLowerBodySegmentDirectionMaxPostDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostDirectionX"),
            EditorLowerBodySegmentDirectionMaxPostDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostDirectionY"),
            EditorLowerBodySegmentDirectionMaxPostDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionMaxPostDirectionZ"),
            EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle"),
            EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle"),
            EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle"),
            EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle"),
            EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle"),
            EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle"),
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX"),
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY"),
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ"),
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX"),
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY"),
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ"),
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX"),
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY"),
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ"),
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX"),
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY"),
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ"),
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionX"),
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionY"),
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ"),
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionX"),
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionY"),
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ"),
            EditorLowerBodySegmentDirectionLeftLowerLegWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegWorldX"),
            EditorLowerBodySegmentDirectionLeftLowerLegWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegWorldY"),
            EditorLowerBodySegmentDirectionLeftLowerLegWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegWorldZ"),
            EditorLowerBodySegmentDirectionLeftFootWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootWorldX"),
            EditorLowerBodySegmentDirectionLeftFootWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootWorldY"),
            EditorLowerBodySegmentDirectionLeftFootWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootWorldZ"),
            EditorLowerBodySegmentDirectionLeftToesWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftToesWorldX"),
            EditorLowerBodySegmentDirectionLeftToesWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftToesWorldY"),
            EditorLowerBodySegmentDirectionLeftToesWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftToesWorldZ"),
            EditorLowerBodySegmentDirectionRightLowerLegWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegWorldX"),
            EditorLowerBodySegmentDirectionRightLowerLegWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegWorldY"),
            EditorLowerBodySegmentDirectionRightLowerLegWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegWorldZ"),
            EditorLowerBodySegmentDirectionRightFootWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootWorldX"),
            EditorLowerBodySegmentDirectionRightFootWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootWorldY"),
            EditorLowerBodySegmentDirectionRightFootWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootWorldZ"),
            EditorLowerBodySegmentDirectionRightToesWorldX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightToesWorldX"),
            EditorLowerBodySegmentDirectionRightToesWorldY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightToesWorldY"),
            EditorLowerBodySegmentDirectionRightToesWorldZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightToesWorldZ"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY"),
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY"),
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ"),
            EditorLowerBodySegmentDirectionLeftFootForwardX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootForwardX"),
            EditorLowerBodySegmentDirectionLeftFootForwardY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootForwardY"),
            EditorLowerBodySegmentDirectionLeftFootForwardZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootForwardZ"),
            EditorLowerBodySegmentDirectionLeftFootUpX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootUpX"),
            EditorLowerBodySegmentDirectionLeftFootUpY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootUpY"),
            EditorLowerBodySegmentDirectionLeftFootUpZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionLeftFootUpZ"),
            EditorLowerBodySegmentDirectionRightFootForwardX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootForwardX"),
            EditorLowerBodySegmentDirectionRightFootForwardY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootForwardY"),
            EditorLowerBodySegmentDirectionRightFootForwardZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootForwardZ"),
            EditorLowerBodySegmentDirectionRightFootUpX = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootUpX"),
            EditorLowerBodySegmentDirectionRightFootUpY = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootUpY"),
            EditorLowerBodySegmentDirectionRightFootUpZ = ReadFloatProperty(type, retargeter, "LastEditorLowerBodySegmentDirectionRightFootUpZ"),
            EditorFootHipsAlignedResidualYawLeftFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootHipsAlignedResidualYawLeftFootXzDelta"),
            EditorFootHipsAlignedResidualYawRightFootXzDelta = ReadFloatProperty(type, retargeter, "LastEditorFootHipsAlignedResidualYawRightFootXzDelta"),
            PostSetRightEndpointDesiredFootWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredFootWorldX"),
            PostSetRightEndpointDesiredFootWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredFootWorldZ"),
            PostSetRightEndpointDesiredToesWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredToesWorldX"),
            PostSetRightEndpointDesiredToesWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDesiredToesWorldZ"),
            PostSetRightEndpointCurrentFootWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentFootWorldX"),
            PostSetRightEndpointCurrentFootWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentFootWorldZ"),
            PostSetRightEndpointCurrentToesWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentToesWorldX"),
            PostSetRightEndpointCurrentToesWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCurrentToesWorldZ"),
            PostSetRightEndpointDeltaBeforeClampX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaBeforeClampX"),
            PostSetRightEndpointDeltaBeforeClampZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaBeforeClampZ"),
            PostSetRightEndpointDeltaAfterClampX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterClampX"),
            PostSetRightEndpointDeltaAfterClampZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterClampZ"),
            PostSetRightEndpointDeltaAfterPositiveZScaleX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScaleX"),
            PostSetRightEndpointDeltaAfterPositiveZScaleZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScaleZ"),
            PostSetRightEndpointCorrectionX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCorrectionX"),
            PostSetRightEndpointCorrectionZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointCorrectionZ"),
            PostSetRightEndpointNextFootWorldX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointNextFootWorldX"),
            PostSetRightEndpointNextFootWorldZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointNextFootWorldZ"),
            PostSetRightEndpointMaxYawAngle = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointMaxYawAngle"),
            PostSetRightEndpointYawCorrectionAngle = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointYawCorrectionAngle"),
            PostSetRightEndpointUpperLegRotationDeltaAngle = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle"),
            PostSetRightEndpointApplied = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointApplied"),
            PostSetRightEndpointEvaluatorXzReferenceEnabled = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled"),
            PostSetRightEndpointEvaluatorXzFirstOffsetX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffsetX"),
            PostSetRightEndpointEvaluatorXzFirstOffsetZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffsetZ"),
            PostSetRightEndpointEvaluatorXzNormalizedDeltaX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDeltaX"),
            PostSetRightEndpointEvaluatorXzNormalizedDeltaZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDeltaZ"),
            PostSetRightEndpointEvaluatorXzNormalizedMagnitude = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedMagnitude"),
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDeltaX"),
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDeltaZ"),
            PostSetRightEndpointEvaluatorXzTargetMagnitude = ReadFloatProperty(type, retargeter, "LastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude"),
            GroundingMaxStepPerFrame = groundingMaxStepPerFrame,
            GroundingLastStepToMaxStepRatio = CalculateStepToMaxRatio(lastGroundingVerticalStep, groundingMaxStepPerFrame),
            GroundingLastStepAtMaxStep = IsStepAtMax(lastGroundingVerticalStep, groundingMaxStepPerFrame) ? 1 : 0
        };
    }

    private static RetargetEndpointStageMetrics ReadRetargetEndpointStage(Type type, object retargeter, string stageName)
    {
        string prefix = "LastRetargetStage" + stageName;
        return new RetargetEndpointStageMetrics
        {
            LeftFootWorldX = ReadFloatProperty(type, retargeter, prefix + "LeftFootWorldX"),
            LeftFootWorldZ = ReadFloatProperty(type, retargeter, prefix + "LeftFootWorldZ"),
            LeftToesWorldX = ReadFloatProperty(type, retargeter, prefix + "LeftToesWorldX"),
            LeftToesWorldZ = ReadFloatProperty(type, retargeter, prefix + "LeftToesWorldZ"),
            RightFootWorldX = ReadFloatProperty(type, retargeter, prefix + "RightFootWorldX"),
            RightFootWorldZ = ReadFloatProperty(type, retargeter, prefix + "RightFootWorldZ"),
            RightToesWorldX = ReadFloatProperty(type, retargeter, prefix + "RightToesWorldX"),
            RightToesWorldZ = ReadFloatProperty(type, retargeter, prefix + "RightToesWorldZ")
        };
    }

    private static float CalculateStepToMaxRatio(float step, float maxStep)
    {
        if (float.IsNaN(step) ||
            float.IsInfinity(step) ||
            float.IsNaN(maxStep) ||
            float.IsInfinity(maxStep) ||
            maxStep <= 0f)
        {
            return float.NaN;
        }

        return Mathf.Abs(step) / maxStep;
    }

    private static bool IsStepAtMax(float step, float maxStep)
    {
        float ratio = CalculateStepToMaxRatio(step, maxStep);
        return !float.IsNaN(ratio) && !float.IsInfinity(ratio) && ratio >= 0.95f;
    }

    private ThumbGuardDiagnostics CaptureThumbGuardDiagnostics()
    {
        ThumbGuardDiagnostics metrics = ThumbGuardDiagnostics.Empty;
        Component thumbGuard = FindThumbDeformationGuardForCurrentAnimator();
        if (thumbGuard == null)
        {
            return metrics;
        }

        Type guardType = thumbGuard.GetType();
        metrics.ManualThumbReferenceConfigured = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "suppressPoseShapingWithManualThumbReference");
        metrics.ProjectionGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveThumbProjectionGuardWeight");
        metrics.LeftProjectionGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveLeftThumbProjectionGuardWeight");
        metrics.RightProjectionGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveRightThumbProjectionGuardWeight");
        metrics.IndexSpreadGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveThumbIndexSpreadGuardWeight");
        metrics.LeftIndexSpreadGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveLeftThumbIndexSpreadGuardWeight");
        metrics.RightIndexSpreadGuardWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveRightThumbIndexSpreadGuardWeight");
        metrics.SegmentStraightenWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveThumbSegmentStraightenWeight");
        metrics.LeftSegmentStraightenWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveLeftThumbSegmentStraightenWeight");
        metrics.RightSegmentStraightenWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "EffectiveRightThumbSegmentStraightenWeight");
        metrics.LeftProjectionCorrectionApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbProjectionCorrectionApplyCount");
        metrics.RightProjectionCorrectionApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbProjectionCorrectionApplyCount");
        metrics.LeftProjectionCorrectionPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbProjectionCorrectionPreserveCount");
        metrics.RightProjectionCorrectionPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbProjectionCorrectionPreserveCount");
        metrics.LeftSegmentStraightenApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbSegmentStraightenApplyCount");
        metrics.RightSegmentStraightenApplyCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbSegmentStraightenApplyCount");
        metrics.LeftSegmentStraightenPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastLeftThumbSegmentStraightenPreserveCount");
        metrics.RightSegmentStraightenPreserveCount = ReadIntMemberAsFloat(
            guardType,
            thumbGuard,
            "LastRightThumbSegmentStraightenPreserveCount");
        metrics.HelperSyncEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "syncDetachedThumbBaseHelpers");
        metrics.HelperPositionSyncEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "syncDetachedThumbBaseHelperPositions");
        metrics.HelperSyncWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBaseHelperSyncWeight");
        metrics.HelperMaxLocalAngle = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBaseHelperMaxLocalAngle");
        metrics.PalmStabilizeEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "stabilizeDetachedThumbBasePalm");
        metrics.PalmStabilizeWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBasePalmStabilizeWeight");
        metrics.PalmStabilizeMaxLocalAngle = ReadFloatMember(
            guardType,
            thumbGuard,
            "detachedThumbBasePalmMaxLocalAngle");
        metrics.WebbingStabilizeEnabled = ReadBoolMemberAsFloat(
            guardType,
            thumbGuard,
            "stabilizeThumbWebbingCrease");
        metrics.WebbingStabilizeWeight = ReadFloatMember(
            guardType,
            thumbGuard,
            "thumbWebbingCreaseStabilizeWeight");
        metrics.WebbingMaxLocalAngle = ReadFloatMember(
            guardType,
            thumbGuard,
            "thumbWebbingCreaseMaxLocalAngle");
        metrics.WebbingMaxPositionOffset = ReadFloatMember(
            guardType,
            thumbGuard,
            "thumbWebbingCreaseMaxPositionOffset");

        Component retargeter = FindRetargeterForCurrentAnimator();
        if (retargeter == null)
        {
            return metrics;
        }

        Type retargeterType = retargeter.GetType();
        metrics.ManualThumbReferenceActive = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "IsManualThumbLocalRotationReferenceActive");
        metrics.PoseShapingSuppressed = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "ShouldSuppressThumbPoseShapingGuard");
        metrics.LeftPoseShapingSuppressed = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "ShouldSuppressLeftThumbPoseShapingGuard");
        metrics.RightPoseShapingSuppressed = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "ShouldSuppressRightThumbPoseShapingGuard");
        metrics.LeftLocalRotationGuardClampCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardClampCount");
        metrics.RightLocalRotationGuardClampCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardClampCount");
        metrics.LeftLocalRotationGuardPreserveCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardPreserveCount");
        metrics.RightLocalRotationGuardPreserveCount = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardPreserveCount");
        metrics.LeftLocalRotationGuardCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardCurrentRisk");
        metrics.RightLocalRotationGuardCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardCurrentRisk");
        metrics.LeftLocalRotationGuardLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbLocalRotationGuardLimitedRisk");
        metrics.RightLocalRotationGuardLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbLocalRotationGuardLimitedRisk");
        metrics.LeftWorldRotationSuppressCompetingOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationSuppressCompetingOverride");
        metrics.RightWorldRotationSuppressCompetingOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationSuppressCompetingOverride");
        metrics.LeftWorldRotationKeepDetachedHelperOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationKeepDetachedHelperOverride");
        metrics.RightWorldRotationKeepDetachedHelperOverride = ReadBoolMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationKeepDetachedHelperOverride");
        metrics.LeftWorldRotationCurrentReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationCurrentReferenceFrameDeviation");
        metrics.RightWorldRotationCurrentReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationCurrentReferenceFrameDeviation");
        metrics.LeftWorldRotationCandidateReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbWorldRotationCandidateReferenceFrameDeviation");
        metrics.RightWorldRotationCandidateReferenceFrameDeviation = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbWorldRotationCandidateReferenceFrameDeviation");
        metrics.LeftProximalWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationPreserveReason");
        metrics.RightProximalWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationPreserveReason");
        metrics.LeftIntermediateWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationPreserveReason");
        metrics.RightIntermediateWorldRotationPreserveReason = ReadIntMemberAsFloat(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationPreserveReason");
        metrics.LeftProximalWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationCurrentReferenceAngle");
        metrics.RightProximalWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationCurrentReferenceAngle");
        metrics.LeftIntermediateWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationCurrentReferenceAngle");
        metrics.RightIntermediateWorldRotationCurrentReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationCurrentReferenceAngle");
        metrics.LeftProximalWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationCandidateReferenceAngle");
        metrics.RightProximalWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationCandidateReferenceAngle");
        metrics.LeftIntermediateWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationCandidateReferenceAngle");
        metrics.RightIntermediateWorldRotationCandidateReferenceAngle = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationCandidateReferenceAngle");
        metrics.LeftProximalWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationPreserveCurrentRisk");
        metrics.RightProximalWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationPreserveCurrentRisk");
        metrics.LeftIntermediateWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationPreserveCurrentRisk");
        metrics.RightIntermediateWorldRotationPreserveCurrentRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationPreserveCurrentRisk");
        metrics.LeftProximalWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbProximalWorldRotationPreserveLimitedRisk");
        metrics.RightProximalWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbProximalWorldRotationPreserveLimitedRisk");
        metrics.LeftIntermediateWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastLeftThumbIntermediateWorldRotationPreserveLimitedRisk");
        metrics.RightIntermediateWorldRotationPreserveLimitedRisk = ReadFloatMember(
            retargeterType,
            retargeter,
            "LastRightThumbIntermediateWorldRotationPreserveLimitedRisk");
        return metrics;
    }

    private Component FindThumbDeformationGuardForCurrentAnimator()
    {
        if (_animator == null)
        {
            return null;
        }

        Component[] components = _animator.gameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().Name == "HumanoidThumbDeformationGuard")
            {
                return component;
            }
        }

        return null;
    }

    private ArmSwingGuardDiagnostics CaptureArmSwingGuardDiagnostics()
    {
        ArmSwingGuardDiagnostics metrics = ArmSwingGuardDiagnostics.Empty;
        Component guard = FindArmSwingLimitGuardForCurrentAnimator();
        if (guard == null)
        {
            return metrics;
        }

        Type guardType = guard.GetType();
        metrics.LeftApplied = ReadIntMemberAsFloat(guardType, guard, "LastLeftApplied");
        metrics.LeftHorizontalReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastLeftHorizontalReachApplied");
        metrics.LeftRaisedReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastLeftRaisedReachApplied");
        metrics.LeftForearmStretchBefore = ReadFloatMember(guardType, guard, "LastLeftForearmStretchBefore");
        metrics.LeftForearmStretchAfter = ReadFloatMember(guardType, guard, "LastLeftForearmStretchAfter");
        metrics.LeftForearmStretchDelta = ReadFloatMember(guardType, guard, "LastLeftForearmStretchDelta");
        metrics.RightApplied = ReadIntMemberAsFloat(guardType, guard, "LastRightApplied");
        metrics.RightHorizontalReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastRightHorizontalReachApplied");
        metrics.RightRaisedReachApplied = ReadIntMemberAsFloat(guardType, guard, "LastRightRaisedReachApplied");
        metrics.RightForearmStretchBefore = ReadFloatMember(guardType, guard, "LastRightForearmStretchBefore");
        metrics.RightForearmStretchAfter = ReadFloatMember(guardType, guard, "LastRightForearmStretchAfter");
        metrics.RightForearmStretchDelta = ReadFloatMember(guardType, guard, "LastRightForearmStretchDelta");
        return metrics;
    }

    private Component FindArmSwingLimitGuardForCurrentAnimator()
    {
        if (_animator == null)
        {
            return null;
        }

        Component[] components = _animator.gameObject.GetComponents<Component>();
        foreach (Component component in components)
        {
            if (component != null && component.GetType().Name == "HumanoidArmSwingLimitGuard")
            {
                return component;
            }
        }

        return null;
    }

    private static float ReadFloatProperty(Type type, object instance, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return float.NaN;
        }

        object value = property.GetValue(instance);
        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        return float.NaN;
    }

    private static float ReadFloatMember(Type type, object instance, string memberName)
    {
        object value = ReadMemberValue(type, instance, memberName);
        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is double doubleValue)
        {
            return (float)doubleValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return float.NaN;
    }

    private static float ReadBoolMemberAsFloat(Type type, object instance, string memberName)
    {
        object value = ReadMemberValue(type, instance, memberName);
        if (value is bool boolValue)
        {
            return boolValue ? 1f : 0f;
        }

        return float.NaN;
    }

    private static float ReadIntMemberAsFloat(Type type, object instance, string memberName)
    {
        object value = ReadMemberValue(type, instance, memberName);
        if (value is int intValue)
        {
            return intValue;
        }

        return float.NaN;
    }

    private static object ReadMemberValue(Type type, object instance, string memberName)
    {
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            return property.GetValue(instance);
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            return field.GetValue(instance);
        }

        return null;
    }

    private static int ReadIntProperty(Type type, object instance, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return -1;
        }

        object value = property.GetValue(instance);
        return value is int intValue ? intValue : -1;
    }

    private static string ReadStringProperty(Type type, object instance, string propertyName)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            return "";
        }

        return property.GetValue(instance) as string ?? "";
    }

    private AnimationClip ResolveCurrentAnimatorClip(int layerIndex)
    {
        AnimationClip bestClip = null;
        float bestWeight = float.MinValue;
        AnimatorClipInfo[] clipInfos = _animator.GetCurrentAnimatorClipInfo(layerIndex);
        foreach (AnimatorClipInfo clipInfo in clipInfos)
        {
            if (clipInfo.clip == null || clipInfo.weight < bestWeight)
            {
                continue;
            }

            bestClip = clipInfo.clip;
            bestWeight = clipInfo.weight;
        }

        if (bestClip != null)
        {
            return bestClip;
        }

        RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
        if (controller != null && controller.animationClips != null && controller.animationClips.Length == 1)
        {
            return controller.animationClips[0];
        }

        return null;
    }

    private void PrepareHumanPoseCapture()
    {
        DisposeHumanPoseCapture();
        _poseWarningLogged = false;

        if (_animator == null || _animator.avatar == null || !_animator.avatar.isValid || !_animator.avatar.isHuman)
        {
            return;
        }

        _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
        _humanPose = new HumanPose();
    }

    private void DisposeHumanPoseCapture()
    {
        if (_poseHandler == null)
        {
            return;
        }

        _poseHandler.Dispose();
        _poseHandler = null;
    }

    private ArmMuscleMetrics CaptureArmMuscles()
    {
        ArmMuscleMetrics metrics = ArmMuscleMetrics.Empty;
        if (_poseHandler == null)
        {
            return metrics;
        }

        _poseHandler.GetHumanPose(ref _humanPose);
        if (_humanPose.muscles == null || _humanPose.muscles.Length == 0)
        {
            return metrics;
        }

        metrics.LeftShoulderDownUp = GetMuscleValue(_humanPose, "left", "shoulder", "downup");
        metrics.LeftShoulderFrontBack = GetMuscleValue(_humanPose, "left", "shoulder", "frontback");
        metrics.LeftArmDownUp = GetMuscleValue(_humanPose, "left", "arm", "downup");
        metrics.LeftArmFrontBack = GetMuscleValue(_humanPose, "left", "arm", "frontback");
        metrics.LeftArmTwist = GetMuscleValue(_humanPose, "left", "arm", "twist");
        metrics.LeftForearmStretch = GetMuscleValue(_humanPose, "left", "forearm", "stretch");
        metrics.LeftForearmTwist = GetMuscleValue(_humanPose, "left", "forearm", "twist");
        metrics.RightShoulderDownUp = GetMuscleValue(_humanPose, "right", "shoulder", "downup");
        metrics.RightShoulderFrontBack = GetMuscleValue(_humanPose, "right", "shoulder", "frontback");
        metrics.RightArmDownUp = GetMuscleValue(_humanPose, "right", "arm", "downup");
        metrics.RightArmFrontBack = GetMuscleValue(_humanPose, "right", "arm", "frontback");
        metrics.RightArmTwist = GetMuscleValue(_humanPose, "right", "arm", "twist");
        metrics.RightForearmStretch = GetMuscleValue(_humanPose, "right", "forearm", "stretch");
        metrics.RightForearmTwist = GetMuscleValue(_humanPose, "right", "forearm", "twist");
        return metrics;
    }

    private FingerMetrics CaptureFingerMetrics()
    {
        FingerMetrics metrics = FingerMetrics.Empty;
        if (_poseHandler == null)
        {
            return metrics;
        }

        _poseHandler.GetHumanPose(ref _humanPose);
        if (_humanPose.muscles == null || _humanPose.muscles.Length == 0)
        {
            return metrics;
        }

        metrics.LeftThumb1Stretch = GetMuscleValue(_humanPose, "left", "thumb", "1", "stretch");
        metrics.LeftThumbSpread = GetMuscleValue(_humanPose, "left", "thumb", "spread");
        metrics.LeftIndex1Stretch = GetMuscleValue(_humanPose, "left", "index", "1", "stretch");
        metrics.LeftIndexSpread = GetMuscleValue(_humanPose, "left", "index", "spread");
        metrics.LeftMiddle1Stretch = GetMuscleValue(_humanPose, "left", "middle", "1", "stretch");
        metrics.LeftMiddleSpread = GetMuscleValue(_humanPose, "left", "middle", "spread");
        metrics.LeftRing1Stretch = GetMuscleValue(_humanPose, "left", "ring", "1", "stretch");
        metrics.LeftRingSpread = GetMuscleValue(_humanPose, "left", "ring", "spread");
        metrics.LeftLittle1Stretch = GetMuscleValue(_humanPose, "left", "little", "1", "stretch");
        metrics.LeftLittleSpread = GetMuscleValue(_humanPose, "left", "little", "spread");
        metrics.RightThumb1Stretch = GetMuscleValue(_humanPose, "right", "thumb", "1", "stretch");
        metrics.RightThumbSpread = GetMuscleValue(_humanPose, "right", "thumb", "spread");
        metrics.RightIndex1Stretch = GetMuscleValue(_humanPose, "right", "index", "1", "stretch");
        metrics.RightIndexSpread = GetMuscleValue(_humanPose, "right", "index", "spread");
        metrics.RightMiddle1Stretch = GetMuscleValue(_humanPose, "right", "middle", "1", "stretch");
        metrics.RightMiddleSpread = GetMuscleValue(_humanPose, "right", "middle", "spread");
        metrics.RightRing1Stretch = GetMuscleValue(_humanPose, "right", "ring", "1", "stretch");
        metrics.RightRingSpread = GetMuscleValue(_humanPose, "right", "ring", "spread");
        metrics.RightLittle1Stretch = GetMuscleValue(_humanPose, "right", "little", "1", "stretch");
        metrics.RightLittleSpread = GetMuscleValue(_humanPose, "right", "little", "spread");
        return metrics;
    }

    private YybDiagnosticMetrics CaptureYybDiagnosticMetrics(ArmMuscleMetrics armMuscles)
    {
        YybSideDiagnosticMetrics left = CaptureYybSideDiagnosticMetrics(false);
        YybSideDiagnosticMetrics right = CaptureYybSideDiagnosticMetrics(true);

        if (!_isYybDiagnosticTarget)
        {
            left.ClearYybOnlyRiskScores();
            right.ClearYybOnlyRiskScores();
            return new YybDiagnosticMetrics
            {
                Left = left,
                Right = right,
                MaxDeformationRisk = float.NaN
            };
        }

        left.ArmTwistRisk = CalculateArmTwistRisk(armMuscles.LeftArmTwist, armMuscles.LeftForearmTwist);
        right.ArmTwistRisk = CalculateArmTwistRisk(armMuscles.RightArmTwist, armMuscles.RightForearmTwist);
        left.SleeveAnchorRisk = CalculateSleeveAnchorRisk(false);
        right.SleeveAnchorRisk = CalculateSleeveAnchorRisk(true);
        left.SleeveThicknessRisk = CalculateSleeveThicknessRisk(false, out left.SleeveAnchorDistance, out left.SleeveThicknessRatio);
        right.SleeveThicknessRisk = CalculateSleeveThicknessRisk(true, out right.SleeveAnchorDistance, out right.SleeveThicknessRatio);
        float leftArmSleeveRisk = MaxFinite(
            CalculateArmSleeveDeformationRisk(left.ArmTwistRisk, left.SleeveAnchorRisk),
            left.SleeveThicknessRisk);
        float rightArmSleeveRisk = MaxFinite(
            CalculateArmSleeveDeformationRisk(right.ArmTwistRisk, right.SleeveAnchorRisk),
            right.SleeveThicknessRisk);
        left.DeformationRisk = MaxFinite(
            left.ThumbSpreadRisk,
            left.ThumbProjectionRisk,
            left.ThumbHelperSeparationRisk,
            left.WebbingRisk,
            leftArmSleeveRisk);
        right.DeformationRisk = MaxFinite(
            right.ThumbSpreadRisk,
            right.ThumbProjectionRisk,
            right.ThumbHelperSeparationRisk,
            right.WebbingRisk,
            rightArmSleeveRisk);

        return new YybDiagnosticMetrics
        {
            Left = left,
            Right = right,
            MaxDeformationRisk = MaxFinite(left.DeformationRisk, right.DeformationRisk)
        };
    }

    private YybSideDiagnosticMetrics CaptureYybSideDiagnosticMetrics(bool isRightSide)
    {
        YybSideDiagnosticMetrics metrics = YybSideDiagnosticMetrics.Empty;
        metrics.HelperCoverageRequired = RequiresExplicitThumbBaseHelperCoverage(isRightSide);

        if (TryCalculateThumbAndIndexDirections(isRightSide, out Vector3 thumbDirection, out Vector3 indexDirection))
        {
            metrics.ThumbDirectionAvailable = true;
            metrics.ThumbIndexSpreadAngle = Vector3.Angle(thumbDirection, indexDirection);
            metrics.ThumbSpreadRisk = RiskAbove(
                metrics.ThumbIndexSpreadAngle,
                DiagnosticThumbIndexMaxSpreadAngle,
                DiagnosticThumbIndexFullRiskAngle);

            if (TryBuildDiagnosticPalmFrame(isRightSide, out _, out Vector3 palmNormal, out _))
            {
                metrics.PalmFrameAvailable = true;
                metrics.ThumbPalmProjection = Vector3.Dot(thumbDirection, palmNormal);
                metrics.ThumbProjectionRisk = RiskOutsideRange(
                    metrics.ThumbPalmProjection,
                    DiagnosticThumbPalmProjectionMin,
                    DiagnosticThumbPalmProjectionMax,
                    1f);
            }
        }

        if (TryResolveExplicitThumbBaseHelperRelationship(isRightSide, out Transform helper, out Transform source))
        {
            metrics.HelperRelationshipAvailable = true;
            string distanceKey = MotionComparisonProbeReportWriter.BuildTransformPairKey(
                MotionComparisonProbeReportWriter.BuildThumbHelperDistancePairKeyLabel(isRightSide), helper, source);
            metrics.ThumbHelperSourceDistanceDelta = CalculateDistanceDeltaFromInitial(helper, source, distanceKey, out float distance);
            metrics.ThumbHelperSourceDistance = distance;

            string rotationKey = MotionComparisonProbeReportWriter.BuildTransformPairKey(
                MotionComparisonProbeReportWriter.BuildThumbHelperRotationPairKeyLabel(isRightSide), source, helper);
            metrics.ThumbHelperSourceRotationDelta = CalculateRelativeRotationDeltaFromInitial(source, helper, rotationKey);

            metrics.ThumbHelperSeparationRisk = MaxFinite(
                RiskAbove(
                    metrics.ThumbHelperSourceDistanceDelta,
                    DiagnosticThumbHelperDistanceDeltaWarning,
                    DiagnosticThumbHelperDistanceDeltaFullRisk),
                RiskAbove(
                    metrics.ThumbHelperSourceRotationDelta,
                    DiagnosticThumbHelperRotationWarning,
                    DiagnosticThumbHelperRotationFullRisk));
        }

        metrics.WebbingRisk = MaxFinite(
            metrics.ThumbProjectionRisk,
            metrics.ThumbSpreadRisk,
            RiskAbove(
                metrics.ThumbHelperSourceDistanceDelta,
                DiagnosticThumbHelperDistanceDeltaWarning,
                DiagnosticThumbHelperDistanceDeltaFullRisk),
            RiskAbove(
                metrics.ThumbHelperSourceRotationDelta,
                DiagnosticThumbWebbingRotationWarning,
                DiagnosticThumbWebbingRotationFullRisk));

        return metrics;
    }

    private bool TryResolveExplicitThumbBaseHelperRelationship(
        bool isRightSide,
        out Transform helper,
        out Transform source)
    {
        helper = null;
        source = null;

        if (!TryFindExplicitThumbBaseSource(isRightSide, out Transform explicitSource))
        {
            return false;
        }

        if (!TryFindThumbBaseHelperCandidate(isRightSide, out Transform helperCandidate))
        {
            return false;
        }

        if (helperCandidate == explicitSource)
        {
            return false;
        }

        helper = helperCandidate;
        source = explicitSource;
        return true;
    }

    private bool RequiresExplicitThumbBaseHelperCoverage(bool isRightSide)
    {
        return TryFindThumbBaseHelperCandidate(isRightSide, out _);
    }

    private bool TryFindExplicitThumbBaseSource(bool isRightSide, out Transform source)
    {
        source = FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildExplicitThumbBaseSourceCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceTransformName(candidate.name, isRightSide));
        return source != null;
    }

    private bool TryFindThumbBaseHelperCandidate(bool isRightSide, out Transform helper)
    {
        helper = FindThumbBaseHelper(isRightSide);
        if (helper != null)
        {
            return true;
        }

        Transform hand = GetBone(isRightSide ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        if (hand == null)
        {
            return false;
        }

        Transform thumbProximal = GetBone(isRightSide ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
        Transform thumbIntermediate = GetBone(isRightSide ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);
        Transform thumbDistal = GetBone(isRightSide ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);
        Transform explicitSource = null;
        TryFindExplicitThumbBaseSource(isRightSide, out explicitSource);

        float bestDistance = float.PositiveInfinity;
        foreach (Transform candidate in hand.GetComponentsInChildren<Transform>(true))
        {
            if (!IsAmbiguousThumbExtraTransformCandidate(candidate, hand, thumbProximal, thumbIntermediate, thumbDistal))
            {
                continue;
            }

            float distance = explicitSource != null
                ? (candidate.position - explicitSource.position).sqrMagnitude
                : thumbProximal != null
                    ? (candidate.position - thumbProximal.position).sqrMagnitude
                    : (candidate.position - hand.position).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            helper = candidate;
        }

        return helper != null;
    }

    private bool IsAmbiguousThumbExtraTransformCandidate(
        Transform candidate,
        Transform hand,
        Transform thumbProximal,
        Transform thumbIntermediate,
        Transform thumbDistal)
    {
        if (candidate == null || candidate == hand || candidate == thumbProximal || candidate == thumbIntermediate || candidate == thumbDistal)
        {
            return false;
        }

        if (!MotionComparisonProbeReportWriter.MatchesAmbiguousThumbExtraTransformCandidateName(candidate.name))
        {
            return false;
        }

        if (IsAncestorWithinHand(candidate, thumbProximal, hand) ||
            IsAncestorWithinHand(candidate, thumbIntermediate, hand) ||
            IsAncestorWithinHand(candidate, thumbDistal, hand) ||
            IsAncestorWithinHand(thumbProximal, candidate, hand) ||
            IsAncestorWithinHand(thumbIntermediate, candidate, hand) ||
            IsAncestorWithinHand(thumbDistal, candidate, hand))
        {
            return false;
        }

        return true;
    }

    private static bool IsAncestorWithinHand(Transform ancestor, Transform descendant, Transform hand)
    {
        if (ancestor == null || descendant == null || hand == null || ancestor == descendant)
        {
            return false;
        }

        Transform current = descendant.parent;
        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }

            if (current == hand)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private void UpdateRealtimeRiskSummary()
    {
        AnimationTimeMetrics animationTime = CaptureAnimationTimeMetrics();
        int recorderFrame = _recorder != null ? _recorder.FrameNumber : -1;
        YybDiagnosticMetrics diagnostics = captureYybDiagnosticOnlyMetrics
            ? CaptureYybDiagnosticMetrics(CaptureArmMuscles())
            : YybDiagnosticMetrics.Empty;
        UpdateRiskSummary(
            diagnostics,
            true,
            MotionComparisonProbeReportWriter.BuildRealtimeRiskEvaluationReason(),
            animationTime.ClipTime,
            recorderFrame);
    }

    private void ResetRiskSummary()
    {
        _maxThumbSpreadRisk = float.NaN;
        _maxThumbProjectionRisk = float.NaN;
        _maxThumbHelperSeparationRisk = float.NaN;
        _maxThumbWebbingRisk = float.NaN;
        _maxGenericThumbAnatomyRisk = float.NaN;
        _maxYybDeformationRisk = float.NaN;
        _maxGenericThumbAnatomyRiskClipTime = float.NaN;
        _maxYybDeformationRiskClipTime = float.NaN;
        _maxGenericThumbAnatomyRiskRecorderFrame = -1;
        _maxYybDeformationRiskRecorderFrame = -1;
        _maxGenericThumbAnatomyRiskReason = "";
        _maxYybDeformationRiskReason = "";
        _riskEvaluationFrameCount = 0;
        _leftCoreThumbDiagnosticFrameCount = 0;
        _rightCoreThumbDiagnosticFrameCount = 0;
        _leftHelperRelationshipFrameCount = 0;
        _rightHelperRelationshipFrameCount = 0;
        _leftHelperCoverageRequired = false;
        _rightHelperCoverageRequired = false;
    }

    private void UpdateRiskSummary(
        YybDiagnosticMetrics diagnostics,
        bool countEvaluationFrame,
        string reason,
        float animationClipTime,
        int recorderFrame)
    {
        if (countEvaluationFrame)
        {
            _riskEvaluationFrameCount++;
            if (diagnostics.Left.HasCoreThumbAnatomy)
            {
                _leftCoreThumbDiagnosticFrameCount++;
            }

            if (diagnostics.Right.HasCoreThumbAnatomy)
            {
                _rightCoreThumbDiagnosticFrameCount++;
            }

            if (diagnostics.Left.HelperRelationshipAvailable)
            {
                _leftHelperRelationshipFrameCount++;
            }

            if (diagnostics.Right.HelperRelationshipAvailable)
            {
                _rightHelperRelationshipFrameCount++;
            }

            _leftHelperCoverageRequired |= diagnostics.Left.HelperCoverageRequired;
            _rightHelperCoverageRequired |= diagnostics.Right.HelperCoverageRequired;
        }

        float genericThumbRisk = MaxFinite(
            diagnostics.Left.ThumbSpreadRisk,
            diagnostics.Right.ThumbSpreadRisk,
            diagnostics.Left.ThumbProjectionRisk,
            diagnostics.Right.ThumbProjectionRisk,
            diagnostics.Left.ThumbHelperSeparationRisk,
            diagnostics.Right.ThumbHelperSeparationRisk,
            diagnostics.Left.WebbingRisk,
            diagnostics.Right.WebbingRisk);

        _maxThumbSpreadRisk = MaxFinite(
            _maxThumbSpreadRisk,
            diagnostics.Left.ThumbSpreadRisk,
            diagnostics.Right.ThumbSpreadRisk);
        _maxThumbProjectionRisk = MaxFinite(
            _maxThumbProjectionRisk,
            diagnostics.Left.ThumbProjectionRisk,
            diagnostics.Right.ThumbProjectionRisk);
        _maxThumbHelperSeparationRisk = MaxFinite(
            _maxThumbHelperSeparationRisk,
            diagnostics.Left.ThumbHelperSeparationRisk,
            diagnostics.Right.ThumbHelperSeparationRisk);
        _maxThumbWebbingRisk = MaxFinite(
            _maxThumbWebbingRisk,
            diagnostics.Left.WebbingRisk,
            diagnostics.Right.WebbingRisk);
        if (IsFinite(genericThumbRisk) &&
            (!IsFinite(_maxGenericThumbAnatomyRisk) || genericThumbRisk >= _maxGenericThumbAnatomyRisk))
        {
            _maxGenericThumbAnatomyRiskClipTime = animationClipTime;
            _maxGenericThumbAnatomyRiskRecorderFrame = recorderFrame;
            _maxGenericThumbAnatomyRiskReason = reason ?? "";
        }

        _maxGenericThumbAnatomyRisk = MaxFinite(_maxGenericThumbAnatomyRisk, genericThumbRisk);
        if (IsFinite(diagnostics.MaxDeformationRisk) &&
            (!IsFinite(_maxYybDeformationRisk) || diagnostics.MaxDeformationRisk >= _maxYybDeformationRisk))
        {
            _maxYybDeformationRiskClipTime = animationClipTime;
            _maxYybDeformationRiskRecorderFrame = recorderFrame;
            _maxYybDeformationRiskReason = reason ?? "";
        }

        _maxYybDeformationRisk = MaxFinite(_maxYybDeformationRisk, diagnostics.MaxDeformationRisk);
    }

    private bool IsYybDiagnosticTarget()
    {
        return MotionComparisonProbeReportWriter.MatchesYybModelName(gameObject.name) ||
            MotionComparisonProbeReportWriter.MatchesYybModelName(comparisonLabel);
    }

    private bool TryCalculateThumbAndIndexDirections(
        bool isRightSide,
        out Vector3 thumbDirection,
        out Vector3 indexDirection)
    {
        thumbDirection = Vector3.zero;
        indexDirection = Vector3.zero;

        Transform hand = GetBone(isRightSide ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform thumbProximal = GetBone(isRightSide ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
        Transform thumbIntermediate = GetBone(isRightSide ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);
        Transform indexProximal = GetBone(isRightSide ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform indexIntermediate = GetBone(isRightSide ? HumanBodyBones.RightIndexIntermediate : HumanBodyBones.LeftIndexIntermediate);

        if (thumbProximal != null && thumbIntermediate != null)
        {
            thumbDirection = thumbIntermediate.position - thumbProximal.position;
        }
        else if (hand != null && thumbProximal != null)
        {
            thumbDirection = thumbProximal.position - hand.position;
        }

        if (hand != null && indexProximal != null)
        {
            indexDirection = indexProximal.position - hand.position;
        }
        else if (indexProximal != null && indexIntermediate != null)
        {
            indexDirection = indexIntermediate.position - indexProximal.position;
        }

        return TryNormalize(thumbDirection, out thumbDirection) &&
            TryNormalize(indexDirection, out indexDirection);
    }

    private bool TryBuildDiagnosticPalmFrame(
        bool isRightSide,
        out Vector3 sideAxis,
        out Vector3 palmNormal,
        out Vector3 forwardAxis)
    {
        sideAxis = Vector3.zero;
        palmNormal = Vector3.zero;
        forwardAxis = Vector3.zero;

        Transform hand = GetBone(isRightSide ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
        Transform index = GetBone(isRightSide ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Transform middle = GetBone(isRightSide ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
        Transform little = GetBone(isRightSide ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        if (hand == null || index == null || middle == null || little == null)
        {
            return false;
        }

        Vector3 rawSide = index.position - little.position;
        if (isRightSide)
        {
            rawSide = -rawSide;
        }

        Vector3 rawForward = ((index.position + middle.position + little.position) / 3f) - hand.position;
        return TryNormalize(rawSide, out sideAxis) &&
            TryNormalize(rawForward, out forwardAxis) &&
            TryNormalize(Vector3.Cross(sideAxis, forwardAxis), out palmNormal) &&
            TryNormalize(Vector3.Cross(palmNormal, sideAxis), out forwardAxis);
    }

    private Transform FindThumbBaseHelper(bool isRightSide)
    {
        return FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildThumbBaseHelperCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesDetachedThumbBaseHelperTransformName(candidate.name, isRightSide));
    }

    private Transform FindThumbBaseSource(bool isRightSide)
    {
        Transform source = FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildThumbBaseSourceCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesActiveThumbBaseSourceTransformName(candidate.name, isRightSide));

        if (source != null)
        {
            return source;
        }

        return GetBone(isRightSide ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
    }

    private Transform FindSleeveAnchor(bool isRightSide)
    {
        return FindDiagnosticTransform(MotionComparisonProbeReportWriter.BuildSleeveAnchorTransformCacheKey(isRightSide), candidate =>
            MotionComparisonProbeReportWriter.MatchesSleeveAnchorTransformName(candidate.name, isRightSide));
    }

    private Transform FindDiagnosticTransform(string cacheKey, Func<Transform, bool> predicate)
    {
        if (_animator == null || _animator.gameObject == null || string.IsNullOrEmpty(cacheKey) || predicate == null)
        {
            return null;
        }

        if (_diagnosticTransformCache.TryGetValue(cacheKey, out Transform cachedTransform))
        {
            return cachedTransform;
        }

        foreach (Transform candidate in _animator.gameObject.GetComponentsInChildren<Transform>(true))
        {
            if (candidate != null && predicate(candidate))
            {
                _diagnosticTransformCache[cacheKey] = candidate;
                return candidate;
            }
        }

        _diagnosticTransformCache[cacheKey] = null;
        return null;
    }

    private float CalculateArmTwistRisk(float armTwistMuscle, float forearmTwistMuscle)
    {
        return MaxFinite(
            RiskMagnitude(
                armTwistMuscle,
                DiagnosticArmTwistWarningMuscle,
                DiagnosticArmTwistFullRiskMuscle),
            RiskMagnitude(
                forearmTwistMuscle,
                DiagnosticArmTwistWarningMuscle,
                DiagnosticArmTwistFullRiskMuscle));
    }

    private static float CalculateArmSleeveDeformationRisk(float armTwistRisk, float sleeveAnchorRisk)
    {
        if (!IsFinite(armTwistRisk))
        {
            return sleeveAnchorRisk;
        }

        if (!IsFinite(sleeveAnchorRisk))
        {
            return armTwistRisk;
        }

        return Mathf.Clamp01(sleeveAnchorRisk + (sleeveAnchorRisk * armTwistRisk));
    }

    private float CalculateSleeveAnchorRisk(bool isRightSide)
    {
        Transform source = GetBone(isRightSide ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
        Transform anchor = FindSleeveAnchor(isRightSide);
        if (source == null || anchor == null)
        {
            return float.NaN;
        }

        string key = MotionComparisonProbeReportWriter.BuildTransformPairKey(
            MotionComparisonProbeReportWriter.BuildSleeveAnchorRotationPairKeyLabel(isRightSide), source, anchor);
        float rotationDelta = CalculateRelativeRotationDeltaFromInitial(source, anchor, key);
        return RiskAbove(
            rotationDelta,
            DiagnosticSleeveAnchorWarningDegrees,
            DiagnosticSleeveAnchorFullRiskDegrees);
    }

    private float CalculateSleeveThicknessRisk(bool isRightSide, out float distance, out float thicknessRatio)
    {
        distance = float.NaN;
        thicknessRatio = float.NaN;

        Transform source = GetBone(isRightSide ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
        Transform anchor = FindSleeveAnchor(isRightSide);
        if (source == null || anchor == null)
        {
            return float.NaN;
        }

        string key = MotionComparisonProbeReportWriter.BuildTransformPairKey(
            MotionComparisonProbeReportWriter.BuildSleeveThicknessPairKeyLabel(isRightSide), source, anchor);
        thicknessRatio = CalculateDistanceRatioFromInitial(source, anchor, key, out distance);
        return RiskBelow(
            thicknessRatio,
            DiagnosticSleeveThicknessWarningRatio,
            DiagnosticSleeveThicknessFullRiskRatio);
    }

    private float CalculateDistanceDeltaFromInitial(Transform a, Transform b, string key, out float distance)
    {
        distance = float.NaN;
        if (a == null || b == null || string.IsNullOrEmpty(key))
        {
            return float.NaN;
        }

        distance = Vector3.Distance(a.position, b.position);
        if (!IsFinite(distance))
        {
            return float.NaN;
        }

        if (!_diagnosticInitialDistances.TryGetValue(key, out float initialDistance))
        {
            _diagnosticInitialDistances[key] = distance;
            return 0f;
        }

        return Mathf.Abs(distance - initialDistance);
    }

    private float CalculateDistanceRatioFromInitial(Transform a, Transform b, string key, out float distance)
    {
        distance = float.NaN;
        if (a == null || b == null || string.IsNullOrEmpty(key))
        {
            return float.NaN;
        }

        distance = Vector3.Distance(a.position, b.position);
        if (!IsFinite(distance))
        {
            return float.NaN;
        }

        if (!_diagnosticInitialDistances.TryGetValue(key, out float initialDistance))
        {
            _diagnosticInitialDistances[key] = distance;
            return 1f;
        }

        if (!IsFinite(initialDistance) || initialDistance <= 0.000001f)
        {
            return float.NaN;
        }

        return distance / initialDistance;
    }

    private float CalculateRelativeRotationDeltaFromInitial(Transform source, Transform target, string key)
    {
        if (source == null || target == null || string.IsNullOrEmpty(key))
        {
            return float.NaN;
        }

        Quaternion relativeRotation = Quaternion.Inverse(source.rotation) * target.rotation;
        if (!IsFinite(relativeRotation))
        {
            return float.NaN;
        }

        if (!_diagnosticInitialRelativeRotations.TryGetValue(key, out Quaternion initialRelativeRotation))
        {
            _diagnosticInitialRelativeRotations[key] = relativeRotation;
            return 0f;
        }

        return Quaternion.Angle(initialRelativeRotation, relativeRotation);
    }

    private void ResetDiagnosticBaselines()
    {
        _diagnosticTransformCache.Clear();
        _diagnosticInitialDistances.Clear();
        _diagnosticInitialRelativeRotations.Clear();
    }

    private static bool TryNormalize(Vector3 value, out Vector3 normalized)
    {
        normalized = Vector3.zero;
        if (!IsFinite(value) || value.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        normalized = value.normalized;
        return IsFinite(normalized);
    }

    private static float RiskAbove(float value, float warningValue, float fullRiskValue)
    {
        if (!IsFinite(value))
        {
            return float.NaN;
        }

        if (value <= warningValue)
        {
            return 0f;
        }

        if (fullRiskValue <= warningValue)
        {
            return 1f;
        }

        return Mathf.Clamp01((value - warningValue) / (fullRiskValue - warningValue));
    }

    private static float RiskBelow(float value, float warningValue, float fullRiskValue)
    {
        if (!IsFinite(value))
        {
            return float.NaN;
        }

        if (value >= warningValue)
        {
            return 0f;
        }

        if (warningValue <= fullRiskValue)
        {
            return 1f;
        }

        return Mathf.Clamp01((warningValue - value) / (warningValue - fullRiskValue));
    }

    private static float RiskMagnitude(float value, float warningValue, float fullRiskValue)
    {
        return RiskAbove(Mathf.Abs(value), warningValue, fullRiskValue);
    }

    private static float RiskOutsideRange(float value, float minValue, float maxValue, float fullRiskDistance)
    {
        if (!IsFinite(value))
        {
            return float.NaN;
        }

        if (value < minValue)
        {
            return RiskAbove(minValue - value, 0f, fullRiskDistance);
        }

        if (value > maxValue)
        {
            return RiskAbove(value - maxValue, 0f, fullRiskDistance);
        }

        return 0f;
    }

    private static float MaxFinite(params float[] values)
    {
        float result = float.NaN;
        if (values == null)
        {
            return result;
        }

        foreach (float value in values)
        {
            if (!IsFinite(value))
            {
                continue;
            }

            result = IsFinite(result) ? Mathf.Max(result, value) : value;
        }

        return result;
    }

    private static float MinFinite(params float[] values)
    {
        float result = float.NaN;
        if (values == null)
        {
            return result;
        }

        foreach (float value in values)
        {
            if (!IsFinite(value))
            {
                continue;
            }

            result = IsFinite(result) ? Mathf.Min(result, value) : value;
        }

        return result;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
    }

    private float GetMuscleValue(HumanPose pose, params string[] tokens)
    {
        int index = FindMuscleIndex(tokens);
        if (index < 0 || pose.muscles == null || index >= pose.muscles.Length)
        {
            if (!_poseWarningLogged)
            {
                Debug.LogWarning(MotionComparisonProbeReportWriter.BuildMissingHumanoidArmMusclesWarningMessage());
                _poseWarningLogged = true;
            }

            return float.NaN;
        }

        return pose.muscles[index];
    }

    private static int FindMuscleIndex(params string[] tokens)
    {
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
        {
            string muscleName = NormalizeMuscleName(HumanTrait.MuscleName[i]);
            bool matched = true;
            foreach (string token in tokens)
            {
                if (!muscleName.Contains(NormalizeMuscleName(token)))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizeMuscleName(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace(" ", "").Replace("-", "").Replace("_", "").ToLowerInvariant();
    }

    private float CalculateCameraFacingDot(Transform root)
    {
        if (_camera == null || root == null)
        {
            return float.NaN;
        }

        Vector3 toCamera = _camera.transform.position - root.position;
        if (toCamera.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        return Vector3.Dot(root.forward, toCamera.normalized);
    }

    private float CalculateMaxScaleDelta()
    {
        HumanBodyBones[] bones =
        {
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightFoot
        };

        float maxDelta = 0f;
        foreach (HumanBodyBones bone in bones)
        {
            Transform target = GetBone(bone);
            if (target == null)
            {
                continue;
            }

            Vector3 scale = target.localScale;
            maxDelta = Mathf.Max(maxDelta, Mathf.Abs(scale.x - 1f), Mathf.Abs(scale.y - 1f), Mathf.Abs(scale.z - 1f));
        }

        return maxDelta;
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private Vector3 GetLocalScale(HumanBodyBones bone)
    {
        Transform target = GetBone(bone);
        return target != null ? target.localScale : EmptyVector();
    }

    private Vector3 GetLocalEuler(HumanBodyBones bone)
    {
        Transform target = GetBone(bone);
        return target != null ? NormalizeEuler(target.localEulerAngles) : EmptyVector();
    }

    private static Vector3 EmptyVector()
    {
        return new Vector3(float.NaN, float.NaN, float.NaN);
    }

    private static Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z));
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle + 180f, 360f) - 180f;
    }

    private float CalculateChainLength(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null)
        {
            return float.NaN;
        }

        return Vector3.Distance(first.position, second.position) + Vector3.Distance(second.position, third.position);
    }

    private float CalculateJointAngle(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null)
        {
            return float.NaN;
        }

        Vector3 upper = first.position - second.position;
        Vector3 lower = third.position - second.position;
        if (upper.sqrMagnitude <= 0.000001f || lower.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        return Vector3.Angle(upper, lower);
    }

    private float CalculateBendForwardDot(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c, Transform root)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null || root == null)
        {
            return float.NaN;
        }

        Vector3 upper = first.position - second.position;
        Vector3 lower = third.position - second.position;
        Vector3 normal = Vector3.Cross(upper, lower);
        if (normal.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        return Vector3.Dot(normal.normalized, root.forward);
    }

    private float CalculateBendOffsetForwardDot(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c, Transform root)
    {
        Transform first = GetBone(a);
        Transform second = GetBone(b);
        Transform third = GetBone(c);
        if (first == null || second == null || third == null || root == null)
        {
            return float.NaN;
        }

        Vector3 chain = third.position - first.position;
        if (chain.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        float t = Vector3.Dot(second.position - first.position, chain) / chain.sqrMagnitude;
        Vector3 closestPointOnChain = first.position + chain * Mathf.Clamp01(t);
        Vector3 bendOffset = second.position - closestPointOnChain;
        if (bendOffset.sqrMagnitude <= 0.000001f)
        {
            return 0f;
        }

        return Vector3.Dot(bendOffset.normalized, root.forward);
    }

    private float CalculateUpperArmDownDot(HumanBodyBones upperBone, HumanBodyBones lowerBone, Transform root)
    {
        Transform upper = GetBone(upperBone);
        Transform lower = GetBone(lowerBone);
        if (upper == null || lower == null || root == null)
        {
            return float.NaN;
        }

        Vector3 upperToLower = lower.position - upper.position;
        if (upperToLower.sqrMagnitude <= 0.000001f)
        {
            return float.NaN;
        }

        Vector3 localDirection = root.InverseTransformDirection(upperToLower.normalized);
        return Mathf.Clamp01(-localDirection.y);
    }

    private float CalculateHandHorizontalRatio(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Transform root)
    {
        if (!TryGetArmOffsetRatios(upperBone, lowerBone, handBone, root, out float horizontalRatio, out _))
        {
            return float.NaN;
        }

        return horizontalRatio;
    }

    private float CalculateHandBelowShoulderRatio(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, Transform root)
    {
        if (!TryGetArmOffsetRatios(upperBone, lowerBone, handBone, root, out _, out float belowShoulderRatio))
        {
            return float.NaN;
        }

        return belowShoulderRatio;
    }

    private bool TryGetArmOffsetRatios(
        HumanBodyBones upperBone,
        HumanBodyBones lowerBone,
        HumanBodyBones handBone,
        Transform root,
        out float horizontalRatio,
        out float belowShoulderRatio)
    {
        horizontalRatio = float.NaN;
        belowShoulderRatio = float.NaN;

        Transform upper = GetBone(upperBone);
        Transform lower = GetBone(lowerBone);
        Transform hand = GetBone(handBone);
        if (upper == null || lower == null || hand == null || root == null)
        {
            return false;
        }

        float armLength = Vector3.Distance(upper.position, lower.position) +
                          Vector3.Distance(lower.position, hand.position);
        if (armLength <= 0.000001f)
        {
            return false;
        }

        Vector3 localOffset = root.InverseTransformPoint(hand.position) -
                              root.InverseTransformPoint(upper.position);
        horizontalRatio = new Vector2(localOffset.x, localOffset.z).magnitude / armLength;
        belowShoulderRatio = Mathf.Max(0f, -localOffset.y) / armLength;
        return true;
    }

    private HandTorsoClearanceMetrics CaptureHandTorsoClearanceMetrics(Transform root)
    {
        float left = CalculateHandTorsoSignedClearance(LeftFingerBones, root);
        float right = CalculateHandTorsoSignedClearance(RightFingerBones, root);
        float minClearance = MinFinite(left, right);
        float penetrationDepth = IsFinite(minClearance) ? Mathf.Max(0f, -minClearance) : float.NaN;
        return new HandTorsoClearanceMetrics
        {
            LeftSignedClearance = left,
            RightSignedClearance = right,
            MinSignedClearance = minClearance,
            PenetrationRisk = IsFinite(penetrationDepth) ? RiskAbove(penetrationDepth, 0.015f, 0.08f) : float.NaN
        };
    }

    private float CalculateHandTorsoSignedClearance(HumanBodyBones[] handBones, Transform root)
    {
        Transform hips = GetBone(HumanBodyBones.Hips);
        Transform chest = GetBone(HumanBodyBones.Chest) ?? GetBone(HumanBodyBones.UpperChest) ?? GetBone(HumanBodyBones.Spine);
        Transform leftShoulder = GetBone(HumanBodyBones.LeftShoulder) ?? GetBone(HumanBodyBones.LeftUpperArm);
        Transform rightShoulder = GetBone(HumanBodyBones.RightShoulder) ?? GetBone(HumanBodyBones.RightUpperArm);
        if (root == null || hips == null || chest == null || leftShoulder == null || rightShoulder == null || handBones == null)
        {
            return float.NaN;
        }

        Vector3 localHips = root.InverseTransformPoint(hips.position);
        Vector3 localChest = root.InverseTransformPoint(chest.position);
        float yMin = Mathf.Min(localHips.y, localChest.y);
        float yMax = Mathf.Max(localHips.y, localChest.y);
        float shoulderWidth = Vector3.Distance(
            root.InverseTransformPoint(leftShoulder.position),
            root.InverseTransformPoint(rightShoulder.position));
        float radiusX = Mathf.Max(0.05f, shoulderWidth * 0.42f);
        float radiusZ = Mathf.Max(0.035f, shoulderWidth * 0.24f);
        float signedClearance = float.NaN;

        foreach (HumanBodyBones handBone in handBones)
        {
            Transform bone = GetBone(handBone);
            if (bone == null)
            {
                continue;
            }

            Vector3 point = root.InverseTransformPoint(bone.position);
            float dy = point.y < yMin ? yMin - point.y : point.y > yMax ? point.y - yMax : 0f;
            float nx = point.x / radiusX;
            float nz = point.z / radiusZ;
            float radialClearance = (Mathf.Sqrt(nx * nx + nz * nz) - 1f) * Mathf.Min(radiusX, radiusZ);
            float pointClearance = dy > 0f
                ? Mathf.Sqrt(radialClearance * radialClearance + dy * dy)
                : radialClearance;
            signedClearance = IsFinite(signedClearance) ? Mathf.Min(signedClearance, pointClearance) : pointClearance;
        }

        return signedClearance;
    }

    private Transform GetBone(HumanBodyBones bone)
    {
        return _animator != null ? _animator.GetBoneTransform(bone) : null;
    }

    private void PrepareSessionOutput()
    {
        _sessionFolder = "";
        _sessionManifestPath = "";
        _screenshotFolder = "";
        _screenshotIndexPath = "";
        _screenshotSessionIndexPath = "";
        _nonBlankScreenshotCount = 0;

        if (string.IsNullOrWhiteSpace(_sessionId))
        {
            return;
        }

        MotionComparisonProbeSessionArtifactOutputPaths paths =
            MotionComparisonProbeOutputPaths.BuildSessionArtifactOutputPaths(
                Application.dataPath,
                _sessionStamp,
                _sessionId,
                _csvPath,
                captureSampleScreenshots);
        _sessionFolder = paths.SessionFolder;
        _sessionManifestPath = paths.SessionManifestPath;

        if (!captureSampleScreenshots || string.IsNullOrEmpty(paths.ScreenshotFolder))
        {
            return;
        }

        _screenshotFolder = paths.ScreenshotFolder;
        _screenshotIndexPath = paths.ScreenshotIndexPath;
        _screenshotSessionIndexPath = paths.ScreenshotSessionIndexPath;
        MotionComparisonProbeReportWriter.WriteScreenshotSessionFiles(
            _screenshotIndexPath,
            _screenshotSessionIndexPath,
            paths.FrameSessionIndexData);
    }

    private void CaptureSampleScreenshots(string reason, PoseMetrics metrics)
    {
        if (!captureSampleScreenshots || string.IsNullOrEmpty(_screenshotFolder))
        {
            return;
        }

        if (Application.isBatchMode)
        {
            CaptureSampleScreenshotsNow(reason, metrics);
            return;
        }

        StartCoroutine(CaptureSampleScreenshotsAtEndOfFrame(reason, metrics));
    }

    private IEnumerator CaptureSampleScreenshotsAtEndOfFrame(string reason, PoseMetrics metrics)
    {
        yield return new WaitForEndOfFrame();

        CaptureSampleScreenshotsNow(reason, metrics);
    }

    private void CaptureSampleScreenshotsNow(string reason, PoseMetrics metrics)
    {
        if (!captureSampleScreenshots || string.IsNullOrEmpty(_screenshotFolder))
        {
            return;
        }

        if (!TryCalculateRenderBounds(out Bounds bounds))
        {
            Debug.LogWarning(MotionComparisonProbeReportWriter.BuildScreenshotBoundsUnavailableWarningMessage(comparisonLabel, reason));
            return;
        }

        MotionComparisonProbeScreenshotCaptureNames captureNames =
            MotionComparisonProbeOutputPaths.BuildScreenshotCaptureNames(metrics.RecorderFrame, Time.frameCount);

        CaptureView(bounds, transform.forward, reason, captureNames.FrameName, captureNames.FrontViewName, metrics);
        CaptureView(bounds, transform.right, reason, captureNames.FrameName, captureNames.RightViewName, metrics);
        CaptureFingerCloseups(reason, captureNames, metrics);
    }

    private void CaptureView(Bounds bounds, Vector3 viewDirection, string reason, string frameName, string viewName, PoseMetrics metrics, float paddingOverride = -1f)
    {
        if (viewDirection.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Camera captureCamera = EnsureCaptureCamera();
        if (captureCamera == null)
        {
            return;
        }

        Vector3 normalizedDirection = viewDirection.normalized;
        float distance = Mathf.Max(bounds.size.magnitude * 2f, 2f);
        captureCamera.transform.position = bounds.center + normalizedDirection * distance;
        captureCamera.transform.rotation = Quaternion.LookRotation(-normalizedDirection, Vector3.up);
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = CalculateOrthographicSize(bounds, captureCamera);
        if (paddingOverride > 0f)
        {
            captureCamera.orthographicSize = CalculateOrthographicSize(bounds, captureCamera, paddingOverride);
        }
        else
        {
            float verticalOffset = (0.5f - screenshotVerticalViewportCenter) * 2f * captureCamera.orthographicSize;
            captureCamera.transform.position += Vector3.up * verticalOffset;
        }

        captureCamera.nearClipPlane = 0.01f;
        captureCamera.farClipPlane = distance + bounds.size.magnitude + 10f;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        MotionComparisonProbeScreenshotCaptureOutputPaths outputPaths =
            MotionComparisonProbeOutputPaths.BuildScreenshotCaptureOutputPaths(
                Application.dataPath,
                _screenshotFolder,
                comparisonLabel,
                SceneManager.GetActiveScene().name,
                reason,
                metrics.RecorderFrame,
                viewName,
                frameName);

        if (!RenderCameraToPng(captureCamera, outputPaths.ScreenshotPath))
        {
            Debug.LogWarning(MotionComparisonProbeReportWriter.BuildScreenshotBlankWarningMessage(outputPaths.ScreenshotPath));
            return;
        }

        _nonBlankScreenshotCount++;
        MotionComparisonProbeReportWriter.AppendScreenshotIndexRow(
            _screenshotIndexPath,
            outputPaths.IndexRow);
    }

    private void CaptureFingerCloseups(string reason, MotionComparisonProbeScreenshotCaptureNames captureNames, PoseMetrics metrics)
    {
        if (!captureFingerCloseups)
        {
            return;
        }

        if (TryCalculateFingerBounds(true, out Bounds leftHandBounds))
        {
            CaptureView(leftHandBounds, transform.forward, reason, captureNames.FrameName, captureNames.LeftHandFrontViewName, metrics, fingerCloseupPadding);
            CaptureView(leftHandBounds, transform.right, reason, captureNames.FrameName, captureNames.LeftHandRightViewName, metrics, fingerCloseupPadding);
        }

        if (TryCalculateFingerBounds(false, out Bounds rightHandBounds))
        {
            CaptureView(rightHandBounds, transform.forward, reason, captureNames.FrameName, captureNames.RightHandFrontViewName, metrics, fingerCloseupPadding);
            CaptureView(rightHandBounds, transform.right, reason, captureNames.FrameName, captureNames.RightHandRightViewName, metrics, fingerCloseupPadding);
        }
    }

    private Camera EnsureCaptureCamera()
    {
        if (_captureCamera != null)
        {
            return _captureCamera;
        }

        GameObject cameraObject = new GameObject(MotionComparisonProbeReportWriter.BuildCaptureCameraObjectName(comparisonLabel));
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        _captureCamera = cameraObject.AddComponent<Camera>();
        _captureCamera.enabled = false;

        _captureCamera.cullingMask = ~0;
        _captureCamera.allowHDR = _camera != null && _camera.allowHDR;
        _captureCamera.allowMSAA = _camera != null && _camera.allowMSAA;

        return _captureCamera;
    }

    private void DestroyCaptureCamera()
    {
        if (_captureCamera == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_captureCamera.gameObject);
        }
        else
        {
            DestroyImmediate(_captureCamera.gameObject);
        }

        _captureCamera = null;
    }

    private bool TryCalculateRenderBounds(out Bounds bounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = new Bounds(transform.position + Vector3.up, Vector3.one);

        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = targetRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(targetRenderer.bounds);
            }
        }

        if (hasBounds && bounds.size.sqrMagnitude > 0.000001f)
        {
            return true;
        }

        Transform hips = GetBone(HumanBodyBones.Hips);
        if (hips != null)
        {
            bounds = new Bounds(hips.position, new Vector3(1f, 2f, 1f));
            return true;
        }

        return false;
    }

    private float CalculateOrthographicSize(Bounds bounds, Camera captureCamera)
    {
        return CalculateOrthographicSize(bounds, captureCamera, screenshotPadding);
    }

    private float CalculateOrthographicSize(Bounds bounds, Camera captureCamera, float padding)
    {
        float aspect = screenshotWidth > 0 && screenshotHeight > 0
            ? (float)screenshotWidth / screenshotHeight
            : 1f;
        float verticalSize = bounds.extents.y;
        float horizontalSize = Mathf.Max(bounds.extents.x, bounds.extents.z) / Mathf.Max(aspect, 0.0001f);
        return Mathf.Max(0.08f, Mathf.Max(verticalSize, horizontalSize) * padding);
    }

    private bool TryCalculateFingerBounds(bool leftSide, out Bounds bounds)
    {
        HumanBodyBones[] bones = leftSide ? LeftFingerBones : RightFingerBones;
        bool hasBounds = false;
        bounds = new Bounds(transform.position, Vector3.one * 0.2f);

        foreach (HumanBodyBones bone in bones)
        {
            Transform boneTransform = GetBone(bone);
            if (boneTransform == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = new Bounds(boneTransform.position, Vector3.one * 0.03f);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(boneTransform.position);
            }
        }

        if (!hasBounds)
        {
            return false;
        }

        bounds.Expand(0.12f);
        return true;
    }

    private bool RenderCameraToPng(Camera captureCamera, string path)
    {
        RenderTexture previousRenderTexture = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(screenshotWidth, screenshotHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = null;

        try
        {
            captureCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, captureCamera.backgroundColor);
            captureCamera.Render();

            texture = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
            texture.Apply();
            return MotionComparisonProbeReportWriter.WriteNonBlankScreenshotPng(path, texture);
        }
        finally
        {
            captureCamera.targetTexture = null;
            RenderTexture.active = previousRenderTexture;
            RenderTexture.ReleaseTemporary(renderTexture);

            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }

    private void WriteSessionManifest(string stateReason)
    {
        if (string.IsNullOrEmpty(_sessionManifestPath))
        {
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        string updatedAt = MotionComparisonProbeReportWriter.BuildSessionUpdatedAt(DateTime.Now);
        MotionComparisonProbeSessionManifestOutputPaths outputPaths =
            MotionComparisonProbeOutputPaths.BuildSessionManifestOutputPaths(
            Application.dataPath, _csvPath, _screenshotFolder, _screenshotIndexPath, _screenshotSessionIndexPath);
        ThumbGuardDiagnostics thumbGuardDiagnostics = CaptureThumbGuardDiagnostics();
        MotionComparisonProbeReportWriter.WriteSessionManifestMarkdown(
            _sessionManifestPath,
            new MotionComparisonProbeSessionManifestData(
                sessionId: _sessionId,
                comparisonLabel: comparisonLabel,
                sceneName: sceneName,
                stateReason: stateReason,
                createdAt: _sessionStamp,
                updatedAt: updatedAt,
                screenshotsEnabled: captureSampleScreenshots,
                sampleClock: MotionComparisonProbeReportWriter.BuildSampleClockLabel(sampleByAnimationClipTime),
                sampleTimes: MotionComparisonProbeReportWriter.FormatSampleTimes(sampleTimes),
                yybDiagnosticOnlyMetrics: captureYybDiagnosticOnlyMetrics,
                riskEvaluationFrameCount: _riskEvaluationFrameCount,
                leftThumbCoreCoverageFrameCount: _leftCoreThumbDiagnosticFrameCount,
                rightThumbCoreCoverageFrameCount: _rightCoreThumbDiagnosticFrameCount,
                leftThumbHelperCoverageRequired: _leftHelperCoverageRequired,
                rightThumbHelperCoverageRequired: _rightHelperCoverageRequired,
                leftThumbHelperCoverageFrameCount: _leftHelperRelationshipFrameCount,
                rightThumbHelperCoverageFrameCount: _rightHelperRelationshipFrameCount,
                maxGenericThumbAnatomyRisk: _maxGenericThumbAnatomyRisk,
                maxGenericThumbAnatomyRiskReason: _maxGenericThumbAnatomyRiskReason,
                maxGenericThumbAnatomyRiskClipTime: _maxGenericThumbAnatomyRiskClipTime,
                maxGenericThumbAnatomyRiskRecorderFrame: _maxGenericThumbAnatomyRiskRecorderFrame,
                maxThumbSpreadRisk: _maxThumbSpreadRisk,
                maxThumbProjectionRisk: _maxThumbProjectionRisk,
                maxThumbHelperSeparationRisk: _maxThumbHelperSeparationRisk,
                maxThumbWebbingRisk: _maxThumbWebbingRisk,
                maxYybDeformationRisk: _maxYybDeformationRisk,
                maxYybDeformationRiskReason: _maxYybDeformationRiskReason,
                maxYybDeformationRiskClipTime: _maxYybDeformationRiskClipTime,
                maxYybDeformationRiskRecorderFrame: _maxYybDeformationRiskRecorderFrame,
                leftThumbProjectionGuardWeight: thumbGuardDiagnostics.LeftProjectionGuardWeight,
                rightThumbProjectionGuardWeight: thumbGuardDiagnostics.RightProjectionGuardWeight,
                leftThumbIndexSpreadGuardWeight: thumbGuardDiagnostics.LeftIndexSpreadGuardWeight,
                rightThumbIndexSpreadGuardWeight: thumbGuardDiagnostics.RightIndexSpreadGuardWeight,
                leftThumbSegmentStraightenGuardWeight: thumbGuardDiagnostics.LeftSegmentStraightenWeight,
                rightThumbSegmentStraightenGuardWeight: thumbGuardDiagnostics.RightSegmentStraightenWeight,
                artifactPaths: outputPaths));
    }

    private struct ArmMuscleMetrics
    {
        public float LeftShoulderDownUp;
        public float LeftShoulderFrontBack;
        public float LeftArmDownUp;
        public float LeftArmFrontBack;
        public float LeftArmTwist;
        public float LeftForearmStretch;
        public float LeftForearmTwist;
        public float RightShoulderDownUp;
        public float RightShoulderFrontBack;
        public float RightArmDownUp;
        public float RightArmFrontBack;
        public float RightArmTwist;
        public float RightForearmStretch;
        public float RightForearmTwist;

        public static ArmMuscleMetrics Empty => new ArmMuscleMetrics
        {
            LeftShoulderDownUp = float.NaN,
            LeftShoulderFrontBack = float.NaN,
            LeftArmDownUp = float.NaN,
            LeftArmFrontBack = float.NaN,
            LeftArmTwist = float.NaN,
            LeftForearmStretch = float.NaN,
            LeftForearmTwist = float.NaN,
            RightShoulderDownUp = float.NaN,
            RightShoulderFrontBack = float.NaN,
            RightArmDownUp = float.NaN,
            RightArmFrontBack = float.NaN,
            RightArmTwist = float.NaN,
            RightForearmStretch = float.NaN,
            RightForearmTwist = float.NaN
        };
    }

    private struct ArmSwingGuardDiagnostics
    {
        public float LeftApplied;
        public float LeftHorizontalReachApplied;
        public float LeftRaisedReachApplied;
        public float LeftForearmStretchBefore;
        public float LeftForearmStretchAfter;
        public float LeftForearmStretchDelta;
        public float RightApplied;
        public float RightHorizontalReachApplied;
        public float RightRaisedReachApplied;
        public float RightForearmStretchBefore;
        public float RightForearmStretchAfter;
        public float RightForearmStretchDelta;

        public static ArmSwingGuardDiagnostics Empty => new ArmSwingGuardDiagnostics
        {
            LeftApplied = float.NaN,
            LeftHorizontalReachApplied = float.NaN,
            LeftRaisedReachApplied = float.NaN,
            LeftForearmStretchBefore = float.NaN,
            LeftForearmStretchAfter = float.NaN,
            LeftForearmStretchDelta = float.NaN,
            RightApplied = float.NaN,
            RightHorizontalReachApplied = float.NaN,
            RightRaisedReachApplied = float.NaN,
            RightForearmStretchBefore = float.NaN,
            RightForearmStretchAfter = float.NaN,
            RightForearmStretchDelta = float.NaN
        };
    }

    private struct AnimationTimeMetrics
    {
        public string Source;
        public string ClipName;
        public float ClipTime;
        public float ClipLength;
        public float NormalizedTime;

        public static AnimationTimeMetrics Empty => new AnimationTimeMetrics
        {
            Source = MotionComparisonProbeReportWriter.BuildUnknownAnimationTimeSourceLabel(),
            ClipName = "",
            ClipTime = float.NaN,
            ClipLength = float.NaN,
            NormalizedTime = float.NaN
        };
    }

    private struct FingerMetrics
    {
        public float LeftThumb1Stretch;
        public float LeftThumbSpread;
        public float LeftIndex1Stretch;
        public float LeftIndexSpread;
        public float LeftMiddle1Stretch;
        public float LeftMiddleSpread;
        public float LeftRing1Stretch;
        public float LeftRingSpread;
        public float LeftLittle1Stretch;
        public float LeftLittleSpread;
        public float RightThumb1Stretch;
        public float RightThumbSpread;
        public float RightIndex1Stretch;
        public float RightIndexSpread;
        public float RightMiddle1Stretch;
        public float RightMiddleSpread;
        public float RightRing1Stretch;
        public float RightRingSpread;
        public float RightLittle1Stretch;
        public float RightLittleSpread;

        public static FingerMetrics Empty => new FingerMetrics
        {
            LeftThumb1Stretch = float.NaN,
            LeftThumbSpread = float.NaN,
            LeftIndex1Stretch = float.NaN,
            LeftIndexSpread = float.NaN,
            LeftMiddle1Stretch = float.NaN,
            LeftMiddleSpread = float.NaN,
            LeftRing1Stretch = float.NaN,
            LeftRingSpread = float.NaN,
            LeftLittle1Stretch = float.NaN,
            LeftLittleSpread = float.NaN,
            RightThumb1Stretch = float.NaN,
            RightThumbSpread = float.NaN,
            RightIndex1Stretch = float.NaN,
            RightIndexSpread = float.NaN,
            RightMiddle1Stretch = float.NaN,
            RightMiddleSpread = float.NaN,
            RightRing1Stretch = float.NaN,
            RightRingSpread = float.NaN,
            RightLittle1Stretch = float.NaN,
            RightLittleSpread = float.NaN
        };
    }

    private struct YybDiagnosticMetrics
    {
        public YybSideDiagnosticMetrics Left;
        public YybSideDiagnosticMetrics Right;
        public float MaxDeformationRisk;

        public static YybDiagnosticMetrics Empty => new YybDiagnosticMetrics
        {
            Left = YybSideDiagnosticMetrics.Empty,
            Right = YybSideDiagnosticMetrics.Empty,
            MaxDeformationRisk = float.NaN
        };
    }

    private struct ThumbGuardDiagnostics
    {
        public float ManualThumbReferenceConfigured;
        public float ManualThumbReferenceActive;
        public float PoseShapingSuppressed;
        public float LeftPoseShapingSuppressed;
        public float RightPoseShapingSuppressed;
        public float ProjectionGuardWeight;
        public float LeftProjectionGuardWeight;
        public float RightProjectionGuardWeight;
        public float IndexSpreadGuardWeight;
        public float LeftIndexSpreadGuardWeight;
        public float RightIndexSpreadGuardWeight;
        public float SegmentStraightenWeight;
        public float LeftSegmentStraightenWeight;
        public float RightSegmentStraightenWeight;
        public float LeftProjectionCorrectionApplyCount;
        public float RightProjectionCorrectionApplyCount;
        public float LeftProjectionCorrectionPreserveCount;
        public float RightProjectionCorrectionPreserveCount;
        public float LeftSegmentStraightenApplyCount;
        public float RightSegmentStraightenApplyCount;
        public float LeftSegmentStraightenPreserveCount;
        public float RightSegmentStraightenPreserveCount;
        public float LeftLocalRotationGuardClampCount;
        public float RightLocalRotationGuardClampCount;
        public float LeftLocalRotationGuardPreserveCount;
        public float RightLocalRotationGuardPreserveCount;
        public float LeftLocalRotationGuardCurrentRisk;
        public float RightLocalRotationGuardCurrentRisk;
        public float LeftLocalRotationGuardLimitedRisk;
        public float RightLocalRotationGuardLimitedRisk;
        public float LeftWorldRotationSuppressCompetingOverride;
        public float RightWorldRotationSuppressCompetingOverride;
        public float LeftWorldRotationKeepDetachedHelperOverride;
        public float RightWorldRotationKeepDetachedHelperOverride;
        public float LeftWorldRotationCurrentReferenceFrameDeviation;
        public float RightWorldRotationCurrentReferenceFrameDeviation;
        public float LeftWorldRotationCandidateReferenceFrameDeviation;
        public float RightWorldRotationCandidateReferenceFrameDeviation;
        public float LeftProximalWorldRotationPreserveReason;
        public float RightProximalWorldRotationPreserveReason;
        public float LeftIntermediateWorldRotationPreserveReason;
        public float RightIntermediateWorldRotationPreserveReason;
        public float LeftProximalWorldRotationCurrentReferenceAngle;
        public float RightProximalWorldRotationCurrentReferenceAngle;
        public float LeftIntermediateWorldRotationCurrentReferenceAngle;
        public float RightIntermediateWorldRotationCurrentReferenceAngle;
        public float LeftProximalWorldRotationCandidateReferenceAngle;
        public float RightProximalWorldRotationCandidateReferenceAngle;
        public float LeftIntermediateWorldRotationCandidateReferenceAngle;
        public float RightIntermediateWorldRotationCandidateReferenceAngle;
        public float LeftProximalWorldRotationPreserveCurrentRisk;
        public float RightProximalWorldRotationPreserveCurrentRisk;
        public float LeftIntermediateWorldRotationPreserveCurrentRisk;
        public float RightIntermediateWorldRotationPreserveCurrentRisk;
        public float LeftProximalWorldRotationPreserveLimitedRisk;
        public float RightProximalWorldRotationPreserveLimitedRisk;
        public float LeftIntermediateWorldRotationPreserveLimitedRisk;
        public float RightIntermediateWorldRotationPreserveLimitedRisk;
        public float HelperSyncEnabled;
        public float HelperPositionSyncEnabled;
        public float HelperSyncWeight;
        public float HelperMaxLocalAngle;
        public float PalmStabilizeEnabled;
        public float PalmStabilizeWeight;
        public float PalmStabilizeMaxLocalAngle;
        public float WebbingStabilizeEnabled;
        public float WebbingStabilizeWeight;
        public float WebbingMaxLocalAngle;
        public float WebbingMaxPositionOffset;

        public static ThumbGuardDiagnostics Empty => new ThumbGuardDiagnostics
        {
            ManualThumbReferenceConfigured = float.NaN,
            ManualThumbReferenceActive = float.NaN,
            PoseShapingSuppressed = float.NaN,
            LeftPoseShapingSuppressed = float.NaN,
            RightPoseShapingSuppressed = float.NaN,
            ProjectionGuardWeight = float.NaN,
            LeftProjectionGuardWeight = float.NaN,
            RightProjectionGuardWeight = float.NaN,
            IndexSpreadGuardWeight = float.NaN,
            LeftIndexSpreadGuardWeight = float.NaN,
            RightIndexSpreadGuardWeight = float.NaN,
            SegmentStraightenWeight = float.NaN,
            LeftSegmentStraightenWeight = float.NaN,
            RightSegmentStraightenWeight = float.NaN,
            LeftProjectionCorrectionApplyCount = float.NaN,
            RightProjectionCorrectionApplyCount = float.NaN,
            LeftProjectionCorrectionPreserveCount = float.NaN,
            RightProjectionCorrectionPreserveCount = float.NaN,
            LeftSegmentStraightenApplyCount = float.NaN,
            RightSegmentStraightenApplyCount = float.NaN,
            LeftSegmentStraightenPreserveCount = float.NaN,
            RightSegmentStraightenPreserveCount = float.NaN,
            LeftLocalRotationGuardClampCount = float.NaN,
            RightLocalRotationGuardClampCount = float.NaN,
            LeftLocalRotationGuardPreserveCount = float.NaN,
            RightLocalRotationGuardPreserveCount = float.NaN,
            LeftLocalRotationGuardCurrentRisk = float.NaN,
            RightLocalRotationGuardCurrentRisk = float.NaN,
            LeftLocalRotationGuardLimitedRisk = float.NaN,
            RightLocalRotationGuardLimitedRisk = float.NaN,
            LeftWorldRotationSuppressCompetingOverride = float.NaN,
            RightWorldRotationSuppressCompetingOverride = float.NaN,
            LeftWorldRotationKeepDetachedHelperOverride = float.NaN,
            RightWorldRotationKeepDetachedHelperOverride = float.NaN,
            LeftWorldRotationCurrentReferenceFrameDeviation = float.NaN,
            RightWorldRotationCurrentReferenceFrameDeviation = float.NaN,
            LeftWorldRotationCandidateReferenceFrameDeviation = float.NaN,
            RightWorldRotationCandidateReferenceFrameDeviation = float.NaN,
            LeftProximalWorldRotationPreserveReason = float.NaN,
            RightProximalWorldRotationPreserveReason = float.NaN,
            LeftIntermediateWorldRotationPreserveReason = float.NaN,
            RightIntermediateWorldRotationPreserveReason = float.NaN,
            LeftProximalWorldRotationCurrentReferenceAngle = float.NaN,
            RightProximalWorldRotationCurrentReferenceAngle = float.NaN,
            LeftIntermediateWorldRotationCurrentReferenceAngle = float.NaN,
            RightIntermediateWorldRotationCurrentReferenceAngle = float.NaN,
            LeftProximalWorldRotationCandidateReferenceAngle = float.NaN,
            RightProximalWorldRotationCandidateReferenceAngle = float.NaN,
            LeftIntermediateWorldRotationCandidateReferenceAngle = float.NaN,
            RightIntermediateWorldRotationCandidateReferenceAngle = float.NaN,
            LeftProximalWorldRotationPreserveCurrentRisk = float.NaN,
            RightProximalWorldRotationPreserveCurrentRisk = float.NaN,
            LeftIntermediateWorldRotationPreserveCurrentRisk = float.NaN,
            RightIntermediateWorldRotationPreserveCurrentRisk = float.NaN,
            LeftProximalWorldRotationPreserveLimitedRisk = float.NaN,
            RightProximalWorldRotationPreserveLimitedRisk = float.NaN,
            LeftIntermediateWorldRotationPreserveLimitedRisk = float.NaN,
            RightIntermediateWorldRotationPreserveLimitedRisk = float.NaN,
            HelperSyncEnabled = float.NaN,
            HelperPositionSyncEnabled = float.NaN,
            HelperSyncWeight = float.NaN,
            HelperMaxLocalAngle = float.NaN,
            PalmStabilizeEnabled = float.NaN,
            PalmStabilizeWeight = float.NaN,
            PalmStabilizeMaxLocalAngle = float.NaN,
            WebbingStabilizeEnabled = float.NaN,
            WebbingStabilizeWeight = float.NaN,
            WebbingMaxLocalAngle = float.NaN,
            WebbingMaxPositionOffset = float.NaN
        };
    }

    private struct RetargetEndpointStageMetrics
    {
        public float LeftFootWorldX;
        public float LeftFootWorldZ;
        public float LeftToesWorldX;
        public float LeftToesWorldZ;
        public float RightFootWorldX;
        public float RightFootWorldZ;
        public float RightToesWorldX;
        public float RightToesWorldZ;

        public static RetargetEndpointStageMetrics Empty => new RetargetEndpointStageMetrics
        {
            LeftFootWorldX = float.NaN,
            LeftFootWorldZ = float.NaN,
            LeftToesWorldX = float.NaN,
            LeftToesWorldZ = float.NaN,
            RightFootWorldX = float.NaN,
            RightFootWorldZ = float.NaN,
            RightToesWorldX = float.NaN,
            RightToesWorldZ = float.NaN
        };
    }

    private struct RootSpikeMetrics
    {
        public float LastRootDeltaMagnitude;
        public float MaxRootDeltaMagnitude;
        public int RootDeltaSpikeSkippedCount;
        public float LastRootPositionPoseDeltaMagnitude;
        public float MaxRootPositionPoseDeltaMagnitude;
        public int RootPositionSpikeClampedCount;
        public float LastGroundingAdjustment;
        public float MaxGroundingAdjustment;
        public int GroundingStepClampedCount;
        public int GroundingSmoothedCount;
        public float LastGroundingVerticalStep;
        public float MaxGroundingVerticalStep;
        public float InitialGroundingVerticalStep;
        public float MaxGroundingVerticalStepAfterInitial;
        public float LastGroundingTargetY;
        public float LastGroundingLowestFootBottomY;
        public float FootHeightReferenceLift;
        public float RecordingStartRootY;
        public float RecordingStartBodyPositionY;
        public float RecordingStartHipsLocalY;
        public float RecordingStartHipsY;
        public float RecordingStartHipsReferenceBeforeLocalY;
        public float RecordingStartHipsReferenceAfterLocalY;
        public float RecordingStartHipsReferenceDeltaY;
        public int RecordingStartHipsReferenceFlipDetected;
        public string RecordingStartHipsReferenceStage;
        public float PoseInputLeftShoulderFrontBackMuscle;
        public float AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle;
        public float AfterClampPoseMusclesLeftShoulderFrontBackMuscle;
        public float AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle;
        public float AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle;
        public float SetHumanPoseInputLeftShoulderFrontBackMuscle;
        public float SetHumanPoseOutputLeftShoulderFrontBackMuscle;
        public float SetHumanPoseLeftShoulderFrontBackDelta;
        public float PoseInputLeftArmTwistMuscle;
        public float AfterEditorMuscleReferenceLeftArmTwistMuscle;
        public float AfterClampPoseMusclesLeftArmTwistMuscle;
        public float AfterAnatomicalArmGuardLeftArmTwistMuscle;
        public float AfterVisualSpikeSmoothingLeftArmTwistMuscle;
        public float SetHumanPoseInputLeftArmTwistMuscle;
        public float SetHumanPoseOutputLeftArmTwistMuscle;
        public float SetHumanPoseLeftArmTwistDelta;
        public float PoseInputLeftForearmStretchMuscle;
        public float AfterEditorMuscleReferenceLeftForearmStretchMuscle;
        public float AfterClampPoseMusclesLeftForearmStretchMuscle;
        public float AfterAnatomicalArmGuardLeftForearmStretchMuscle;
        public float AfterVisualSpikeSmoothingLeftForearmStretchMuscle;
        public float SetHumanPoseInputLeftForearmStretchMuscle;
        public float SetHumanPoseOutputLeftForearmStretchMuscle;
        public float SetHumanPoseLeftForearmStretchDelta;
        public float PoseInputRightForearmStretchMuscle;
        public float AfterEditorMuscleReferenceRightForearmStretchMuscle;
        public float AfterClampPoseMusclesRightForearmStretchMuscle;
        public float AfterAnatomicalArmGuardRightForearmStretchMuscle;
        public float AfterVisualSpikeSmoothingRightForearmStretchMuscle;
        public float SetHumanPoseInputRightForearmStretchMuscle;
        public float SetHumanPoseOutputRightForearmStretchMuscle;
        public float SetHumanPoseRightForearmStretchDelta;
        public float PoseInputRightArmTwistMuscle;
        public float AfterEditorMuscleReferenceRightArmTwistMuscle;
        public float AfterClampPoseMusclesRightArmTwistMuscle;
        public float AfterAnatomicalArmGuardRightArmTwistMuscle;
        public float AfterVisualSpikeSmoothingRightArmTwistMuscle;
        public float SetHumanPoseInputRightArmTwistMuscle;
        public float SetHumanPoseOutputRightArmTwistMuscle;
        public float SetHumanPoseRightArmTwistDelta;
        public float SetHumanPoseInputLeftUpperLegFrontBackMuscle;
        public float SetHumanPoseOutputLeftUpperLegFrontBackMuscle;
        public float SetHumanPoseLeftUpperLegFrontBackDelta;
        public float SetHumanPoseInputRightUpperLegFrontBackMuscle;
        public float SetHumanPoseOutputRightUpperLegFrontBackMuscle;
        public float SetHumanPoseRightUpperLegFrontBackDelta;
        public float SetHumanPoseInputLeftLowerLegStretchMuscle;
        public float SetHumanPoseOutputLeftLowerLegStretchMuscle;
        public float SetHumanPoseLeftLowerLegStretchDelta;
        public float SetHumanPoseInputRightLowerLegStretchMuscle;
        public float SetHumanPoseOutputRightLowerLegStretchMuscle;
        public float SetHumanPoseRightLowerLegStretchDelta;
        public float SetHumanPoseInputLeftFootUpDownMuscle;
        public float SetHumanPoseOutputLeftFootUpDownMuscle;
        public float SetHumanPoseLeftFootUpDownDelta;
        public float SetHumanPoseInputRightFootUpDownMuscle;
        public float SetHumanPoseOutputRightFootUpDownMuscle;
        public float SetHumanPoseRightFootUpDownDelta;
        public RetargetEndpointStageMetrics RetargetStageGhost;
        public RetargetEndpointStageMetrics RetargetStageAfterSetHumanPose;
        public RetargetEndpointStageMetrics RetargetStageAfterManualReferences;
        public RetargetEndpointStageMetrics RetargetStageAfterRootRestore;
        public RetargetEndpointStageMetrics RetargetStageAfterRootDelta;
        public RetargetEndpointStageMetrics RetargetStageAfterGrounding;
        public float EditorFootLocalRotationLeftFootXzDelta;
        public float EditorFootLocalRotationRightFootXzDelta;
        public float EditorLowerBodySegmentDirectionLeftFootXzDelta;
        public float EditorLowerBodySegmentDirectionRightFootXzDelta;
        public string EditorLowerBodySegmentDirectionMaxCorrectionSegment;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAngle;
        public float EditorLowerBodySegmentDirectionMaxPreAngle;
        public float EditorLowerBodySegmentDirectionMaxPostAngle;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAxisX;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAxisY;
        public float EditorLowerBodySegmentDirectionMaxCorrectionAxisZ;
        public float EditorLowerBodySegmentDirectionMaxReferenceDirectionX;
        public float EditorLowerBodySegmentDirectionMaxReferenceDirectionY;
        public float EditorLowerBodySegmentDirectionMaxReferenceDirectionZ;
        public float EditorLowerBodySegmentDirectionMaxPreDirectionX;
        public float EditorLowerBodySegmentDirectionMaxPreDirectionY;
        public float EditorLowerBodySegmentDirectionMaxPreDirectionZ;
        public float EditorLowerBodySegmentDirectionMaxPostDirectionX;
        public float EditorLowerBodySegmentDirectionMaxPostDirectionY;
        public float EditorLowerBodySegmentDirectionMaxPostDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle;
        public float EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle;
        public float EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle;
        public float EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle;
        public float EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle;
        public float EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle;
        public float EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX;
        public float EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY;
        public float EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY;
        public float EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ;
        public float EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX;
        public float EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY;
        public float EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ;
        public float EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX;
        public float EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY;
        public float EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ;
        public float EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX;
        public float EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY;
        public float EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ;
        public float EditorLowerBodySegmentDirectionLeftLowerLegWorldX;
        public float EditorLowerBodySegmentDirectionLeftLowerLegWorldY;
        public float EditorLowerBodySegmentDirectionLeftLowerLegWorldZ;
        public float EditorLowerBodySegmentDirectionLeftFootWorldX;
        public float EditorLowerBodySegmentDirectionLeftFootWorldY;
        public float EditorLowerBodySegmentDirectionLeftFootWorldZ;
        public float EditorLowerBodySegmentDirectionLeftToesWorldX;
        public float EditorLowerBodySegmentDirectionLeftToesWorldY;
        public float EditorLowerBodySegmentDirectionLeftToesWorldZ;
        public float EditorLowerBodySegmentDirectionRightLowerLegWorldX;
        public float EditorLowerBodySegmentDirectionRightLowerLegWorldY;
        public float EditorLowerBodySegmentDirectionRightLowerLegWorldZ;
        public float EditorLowerBodySegmentDirectionRightFootWorldX;
        public float EditorLowerBodySegmentDirectionRightFootWorldY;
        public float EditorLowerBodySegmentDirectionRightFootWorldZ;
        public float EditorLowerBodySegmentDirectionRightToesWorldX;
        public float EditorLowerBodySegmentDirectionRightToesWorldY;
        public float EditorLowerBodySegmentDirectionRightToesWorldZ;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY;
        public float EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY;
        public float EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ;
        public float EditorLowerBodySegmentDirectionLeftFootForwardX;
        public float EditorLowerBodySegmentDirectionLeftFootForwardY;
        public float EditorLowerBodySegmentDirectionLeftFootForwardZ;
        public float EditorLowerBodySegmentDirectionLeftFootUpX;
        public float EditorLowerBodySegmentDirectionLeftFootUpY;
        public float EditorLowerBodySegmentDirectionLeftFootUpZ;
        public float EditorLowerBodySegmentDirectionRightFootForwardX;
        public float EditorLowerBodySegmentDirectionRightFootForwardY;
        public float EditorLowerBodySegmentDirectionRightFootForwardZ;
        public float EditorLowerBodySegmentDirectionRightFootUpX;
        public float EditorLowerBodySegmentDirectionRightFootUpY;
        public float EditorLowerBodySegmentDirectionRightFootUpZ;
        public float EditorFootHipsAlignedResidualYawLeftFootXzDelta;
        public float EditorFootHipsAlignedResidualYawRightFootXzDelta;
        public float PostSetRightEndpointDesiredFootWorldX;
        public float PostSetRightEndpointDesiredFootWorldZ;
        public float PostSetRightEndpointDesiredToesWorldX;
        public float PostSetRightEndpointDesiredToesWorldZ;
        public float PostSetRightEndpointCurrentFootWorldX;
        public float PostSetRightEndpointCurrentFootWorldZ;
        public float PostSetRightEndpointCurrentToesWorldX;
        public float PostSetRightEndpointCurrentToesWorldZ;
        public float PostSetRightEndpointDeltaBeforeClampX;
        public float PostSetRightEndpointDeltaBeforeClampZ;
        public float PostSetRightEndpointDeltaAfterClampX;
        public float PostSetRightEndpointDeltaAfterClampZ;
        public float PostSetRightEndpointDeltaAfterPositiveZScaleX;
        public float PostSetRightEndpointDeltaAfterPositiveZScaleZ;
        public float PostSetRightEndpointCorrectionX;
        public float PostSetRightEndpointCorrectionZ;
        public float PostSetRightEndpointNextFootWorldX;
        public float PostSetRightEndpointNextFootWorldZ;
        public float PostSetRightEndpointMaxYawAngle;
        public float PostSetRightEndpointYawCorrectionAngle;
        public float PostSetRightEndpointUpperLegRotationDeltaAngle;
        public float PostSetRightEndpointApplied;
        public float PostSetRightEndpointEvaluatorXzReferenceEnabled;
        public float PostSetRightEndpointEvaluatorXzFirstOffsetX;
        public float PostSetRightEndpointEvaluatorXzFirstOffsetZ;
        public float PostSetRightEndpointEvaluatorXzNormalizedDeltaX;
        public float PostSetRightEndpointEvaluatorXzNormalizedDeltaZ;
        public float PostSetRightEndpointEvaluatorXzNormalizedMagnitude;
        public float PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX;
        public float PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ;
        public float PostSetRightEndpointEvaluatorXzTargetMagnitude;
        public float GroundingMaxStepPerFrame;
        public float GroundingLastStepToMaxStepRatio;
        public int GroundingLastStepAtMaxStep;

        public static RootSpikeMetrics Empty => new RootSpikeMetrics
        {
            LastRootDeltaMagnitude = float.NaN,
            MaxRootDeltaMagnitude = float.NaN,
            RootDeltaSpikeSkippedCount = -1,
            LastRootPositionPoseDeltaMagnitude = float.NaN,
            MaxRootPositionPoseDeltaMagnitude = float.NaN,
            RootPositionSpikeClampedCount = -1,
            LastGroundingAdjustment = float.NaN,
            MaxGroundingAdjustment = float.NaN,
            GroundingStepClampedCount = -1,
            GroundingSmoothedCount = -1,
            LastGroundingVerticalStep = float.NaN,
            MaxGroundingVerticalStep = float.NaN,
            InitialGroundingVerticalStep = float.NaN,
            MaxGroundingVerticalStepAfterInitial = float.NaN,
            LastGroundingTargetY = float.NaN,
            LastGroundingLowestFootBottomY = float.NaN,
            FootHeightReferenceLift = float.NaN,
            RecordingStartRootY = float.NaN,
            RecordingStartBodyPositionY = float.NaN,
            RecordingStartHipsLocalY = float.NaN,
            RecordingStartHipsY = float.NaN,
            RecordingStartHipsReferenceBeforeLocalY = float.NaN,
            RecordingStartHipsReferenceAfterLocalY = float.NaN,
            RecordingStartHipsReferenceDeltaY = float.NaN,
            RecordingStartHipsReferenceFlipDetected = -1,
            RecordingStartHipsReferenceStage = "",
            PoseInputLeftShoulderFrontBackMuscle = float.NaN,
            AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle = float.NaN,
            AfterClampPoseMusclesLeftShoulderFrontBackMuscle = float.NaN,
            AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle = float.NaN,
            AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle = float.NaN,
            SetHumanPoseInputLeftShoulderFrontBackMuscle = float.NaN,
            SetHumanPoseOutputLeftShoulderFrontBackMuscle = float.NaN,
            SetHumanPoseLeftShoulderFrontBackDelta = float.NaN,
            PoseInputLeftArmTwistMuscle = float.NaN,
            AfterEditorMuscleReferenceLeftArmTwistMuscle = float.NaN,
            AfterClampPoseMusclesLeftArmTwistMuscle = float.NaN,
            AfterAnatomicalArmGuardLeftArmTwistMuscle = float.NaN,
            AfterVisualSpikeSmoothingLeftArmTwistMuscle = float.NaN,
            SetHumanPoseInputLeftArmTwistMuscle = float.NaN,
            SetHumanPoseOutputLeftArmTwistMuscle = float.NaN,
            SetHumanPoseLeftArmTwistDelta = float.NaN,
            PoseInputLeftForearmStretchMuscle = float.NaN,
            AfterEditorMuscleReferenceLeftForearmStretchMuscle = float.NaN,
            AfterClampPoseMusclesLeftForearmStretchMuscle = float.NaN,
            AfterAnatomicalArmGuardLeftForearmStretchMuscle = float.NaN,
            AfterVisualSpikeSmoothingLeftForearmStretchMuscle = float.NaN,
            SetHumanPoseInputLeftForearmStretchMuscle = float.NaN,
            SetHumanPoseOutputLeftForearmStretchMuscle = float.NaN,
            SetHumanPoseLeftForearmStretchDelta = float.NaN,
            PoseInputRightForearmStretchMuscle = float.NaN,
            AfterEditorMuscleReferenceRightForearmStretchMuscle = float.NaN,
            AfterClampPoseMusclesRightForearmStretchMuscle = float.NaN,
            AfterAnatomicalArmGuardRightForearmStretchMuscle = float.NaN,
            AfterVisualSpikeSmoothingRightForearmStretchMuscle = float.NaN,
            SetHumanPoseInputRightForearmStretchMuscle = float.NaN,
            SetHumanPoseOutputRightForearmStretchMuscle = float.NaN,
            SetHumanPoseRightForearmStretchDelta = float.NaN,
            PoseInputRightArmTwistMuscle = float.NaN,
            AfterEditorMuscleReferenceRightArmTwistMuscle = float.NaN,
            AfterClampPoseMusclesRightArmTwistMuscle = float.NaN,
            AfterAnatomicalArmGuardRightArmTwistMuscle = float.NaN,
            AfterVisualSpikeSmoothingRightArmTwistMuscle = float.NaN,
            SetHumanPoseInputRightArmTwistMuscle = float.NaN,
            SetHumanPoseOutputRightArmTwistMuscle = float.NaN,
            SetHumanPoseRightArmTwistDelta = float.NaN,
            SetHumanPoseInputLeftUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseOutputLeftUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseLeftUpperLegFrontBackDelta = float.NaN,
            SetHumanPoseInputRightUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseOutputRightUpperLegFrontBackMuscle = float.NaN,
            SetHumanPoseRightUpperLegFrontBackDelta = float.NaN,
            SetHumanPoseInputLeftLowerLegStretchMuscle = float.NaN,
            SetHumanPoseOutputLeftLowerLegStretchMuscle = float.NaN,
            SetHumanPoseLeftLowerLegStretchDelta = float.NaN,
            SetHumanPoseInputRightLowerLegStretchMuscle = float.NaN,
            SetHumanPoseOutputRightLowerLegStretchMuscle = float.NaN,
            SetHumanPoseRightLowerLegStretchDelta = float.NaN,
            SetHumanPoseInputLeftFootUpDownMuscle = float.NaN,
            SetHumanPoseOutputLeftFootUpDownMuscle = float.NaN,
            SetHumanPoseLeftFootUpDownDelta = float.NaN,
            SetHumanPoseInputRightFootUpDownMuscle = float.NaN,
            SetHumanPoseOutputRightFootUpDownMuscle = float.NaN,
            SetHumanPoseRightFootUpDownDelta = float.NaN,
            RetargetStageGhost = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterSetHumanPose = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterManualReferences = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterRootRestore = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterRootDelta = RetargetEndpointStageMetrics.Empty,
            RetargetStageAfterGrounding = RetargetEndpointStageMetrics.Empty,
            EditorFootLocalRotationLeftFootXzDelta = float.NaN,
            EditorFootLocalRotationRightFootXzDelta = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootXzDelta = float.NaN,
            EditorLowerBodySegmentDirectionRightFootXzDelta = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionSegment = "",
            EditorLowerBodySegmentDirectionMaxCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreAngle = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostAngle = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionAxisX = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionAxisY = float.NaN,
            EditorLowerBodySegmentDirectionMaxCorrectionAxisZ = float.NaN,
            EditorLowerBodySegmentDirectionMaxReferenceDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionMaxReferenceDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionMaxReferenceDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionMaxPreDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionMaxPostDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegWorldX = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegWorldY = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootWorldX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootWorldY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftToesWorldX = float.NaN,
            EditorLowerBodySegmentDirectionLeftToesWorldY = float.NaN,
            EditorLowerBodySegmentDirectionLeftToesWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegWorldX = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegWorldY = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootWorldX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootWorldY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionRightToesWorldX = float.NaN,
            EditorLowerBodySegmentDirectionRightToesWorldY = float.NaN,
            EditorLowerBodySegmentDirectionRightToesWorldZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY = float.NaN,
            EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY = float.NaN,
            EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootForwardX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootForwardY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootForwardZ = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootUpX = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootUpY = float.NaN,
            EditorLowerBodySegmentDirectionLeftFootUpZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootForwardX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootForwardY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootForwardZ = float.NaN,
            EditorLowerBodySegmentDirectionRightFootUpX = float.NaN,
            EditorLowerBodySegmentDirectionRightFootUpY = float.NaN,
            EditorLowerBodySegmentDirectionRightFootUpZ = float.NaN,
            EditorFootHipsAlignedResidualYawLeftFootXzDelta = float.NaN,
            EditorFootHipsAlignedResidualYawRightFootXzDelta = float.NaN,
            PostSetRightEndpointDesiredFootWorldX = float.NaN,
            PostSetRightEndpointDesiredFootWorldZ = float.NaN,
            PostSetRightEndpointDesiredToesWorldX = float.NaN,
            PostSetRightEndpointDesiredToesWorldZ = float.NaN,
            PostSetRightEndpointCurrentFootWorldX = float.NaN,
            PostSetRightEndpointCurrentFootWorldZ = float.NaN,
            PostSetRightEndpointCurrentToesWorldX = float.NaN,
            PostSetRightEndpointCurrentToesWorldZ = float.NaN,
            PostSetRightEndpointDeltaBeforeClampX = float.NaN,
            PostSetRightEndpointDeltaBeforeClampZ = float.NaN,
            PostSetRightEndpointDeltaAfterClampX = float.NaN,
            PostSetRightEndpointDeltaAfterClampZ = float.NaN,
            PostSetRightEndpointDeltaAfterPositiveZScaleX = float.NaN,
            PostSetRightEndpointDeltaAfterPositiveZScaleZ = float.NaN,
            PostSetRightEndpointCorrectionX = float.NaN,
            PostSetRightEndpointCorrectionZ = float.NaN,
            PostSetRightEndpointNextFootWorldX = float.NaN,
            PostSetRightEndpointNextFootWorldZ = float.NaN,
            PostSetRightEndpointMaxYawAngle = float.NaN,
            PostSetRightEndpointYawCorrectionAngle = float.NaN,
            PostSetRightEndpointUpperLegRotationDeltaAngle = float.NaN,
            PostSetRightEndpointApplied = float.NaN,
            PostSetRightEndpointEvaluatorXzReferenceEnabled = float.NaN,
            PostSetRightEndpointEvaluatorXzFirstOffsetX = float.NaN,
            PostSetRightEndpointEvaluatorXzFirstOffsetZ = float.NaN,
            PostSetRightEndpointEvaluatorXzNormalizedDeltaX = float.NaN,
            PostSetRightEndpointEvaluatorXzNormalizedDeltaZ = float.NaN,
            PostSetRightEndpointEvaluatorXzNormalizedMagnitude = float.NaN,
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX = float.NaN,
            PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ = float.NaN,
            PostSetRightEndpointEvaluatorXzTargetMagnitude = float.NaN,
            GroundingMaxStepPerFrame = float.NaN,
            GroundingLastStepToMaxStepRatio = float.NaN,
            GroundingLastStepAtMaxStep = -1
        };
    }

    private struct HandTorsoClearanceMetrics
    {
        public float LeftSignedClearance;
        public float RightSignedClearance;
        public float MinSignedClearance;
        public float PenetrationRisk;

        public static HandTorsoClearanceMetrics Empty => new HandTorsoClearanceMetrics
        {
            LeftSignedClearance = float.NaN,
            RightSignedClearance = float.NaN,
            MinSignedClearance = float.NaN,
            PenetrationRisk = float.NaN
        };
    }

    private struct YybSideDiagnosticMetrics
    {
        public bool ThumbDirectionAvailable;
        public bool PalmFrameAvailable;
        public bool HelperCoverageRequired;
        public bool HelperRelationshipAvailable;
        public float ThumbIndexSpreadAngle;
        public float ThumbPalmProjection;
        public float ThumbSpreadRisk;
        public float ThumbProjectionRisk;
        public float ThumbHelperSourceDistance;
        public float ThumbHelperSourceDistanceDelta;
        public float ThumbHelperSourceRotationDelta;
        public float ThumbHelperSeparationRisk;
        public float WebbingRisk;
        public float ArmTwistRisk;
        public float SleeveAnchorRisk;
        public float SleeveAnchorDistance;
        public float SleeveThicknessRatio;
        public float SleeveThicknessRisk;
        public float DeformationRisk;
        public bool HasCoreThumbAnatomy => ThumbDirectionAvailable && PalmFrameAvailable;

        public static YybSideDiagnosticMetrics Empty => new YybSideDiagnosticMetrics
        {
            ThumbDirectionAvailable = false,
            PalmFrameAvailable = false,
            HelperCoverageRequired = false,
            HelperRelationshipAvailable = false,
            ThumbIndexSpreadAngle = float.NaN,
            ThumbPalmProjection = float.NaN,
            ThumbSpreadRisk = float.NaN,
            ThumbProjectionRisk = float.NaN,
            ThumbHelperSourceDistance = float.NaN,
            ThumbHelperSourceDistanceDelta = float.NaN,
            ThumbHelperSourceRotationDelta = float.NaN,
            ThumbHelperSeparationRisk = float.NaN,
            WebbingRisk = float.NaN,
            ArmTwistRisk = float.NaN,
            SleeveAnchorRisk = float.NaN,
            SleeveAnchorDistance = float.NaN,
            SleeveThicknessRatio = float.NaN,
            SleeveThicknessRisk = float.NaN,
            DeformationRisk = float.NaN
        };

        public void ClearYybOnlyRiskScores()
        {
            ArmTwistRisk = float.NaN;
            SleeveAnchorRisk = float.NaN;
            SleeveAnchorDistance = float.NaN;
            SleeveThicknessRatio = float.NaN;
            SleeveThicknessRisk = float.NaN;
            DeformationRisk = float.NaN;
        }
    }

    private struct PoseMetrics
    {
        public string Label;
        public string Scene;
        public string Reason;
        public float Elapsed;
        public float TimeSinceLevelLoad;
        public int FrameCount;
        public int RecorderFrame;
        public string AnimationTimeSource;
        public string AnimationClipName;
        public float AnimationClipTime;
        public float AnimationClipLength;
        public float AnimationNormalizedTime;
        public Vector3 RootPosition;
        public float RootYaw;
        public RootSpikeMetrics RootSpike;
        public float BodyPositionY;
        public float HipsLocalY;
        public Vector3 HipsPosition;
        public float HipsY;
        public float LowestFootY;
        public float LowestFootBottomY;
        public Vector3 LeftFootPosition;
        public Vector3 RightFootPosition;
        public float MeshBoundsMinY;
        public float MeshBoundsMaxY;
        public float FootBottomGroundGap;
        public float MeshBoundsGroundGap;
        public float CameraFacingDot;
        public float MaxScaleDelta;
        public Vector3 LeftUpperArmScale;
        public Vector3 RightUpperArmScale;
        public Vector3 LeftUpperLegScale;
        public Vector3 RightUpperLegScale;
        public Vector3 SpineLocalEuler;
        public Vector3 ChestLocalEuler;
        public Vector3 UpperChestLocalEuler;
        public Vector3 LeftShoulderLocalEuler;
        public Vector3 RightShoulderLocalEuler;
        public Vector3 LeftUpperArmLocalEuler;
        public Vector3 RightUpperArmLocalEuler;
        public Vector3 LeftLowerArmLocalEuler;
        public Vector3 RightLowerArmLocalEuler;
        public Vector3 LeftHandLocalEuler;
        public Vector3 RightHandLocalEuler;
        public Vector3 LeftThumbProximalLocalEuler;
        public Vector3 LeftIndexProximalLocalEuler;
        public Vector3 LeftMiddleProximalLocalEuler;
        public Vector3 LeftRingProximalLocalEuler;
        public Vector3 LeftLittleProximalLocalEuler;
        public Vector3 RightThumbProximalLocalEuler;
        public Vector3 RightIndexProximalLocalEuler;
        public Vector3 RightMiddleProximalLocalEuler;
        public Vector3 RightRingProximalLocalEuler;
        public Vector3 RightLittleProximalLocalEuler;
        public float LeftArmLength;
        public float RightArmLength;
        public float LeftLegLength;
        public float RightLegLength;
        public float LeftElbowAngle;
        public float RightElbowAngle;
        public float LeftKneeAngle;
        public float RightKneeAngle;
        public float LeftElbowBendForward;
        public float RightElbowBendForward;
        public float LeftKneeBendForward;
        public float RightKneeBendForward;
        public float LeftElbowBendOffsetForward;
        public float RightElbowBendOffsetForward;
        public float LeftKneeBendOffsetForward;
        public float RightKneeBendOffsetForward;
        public float LeftUpperArmDownDot;
        public float RightUpperArmDownDot;
        public float LeftHandHorizontalRatio;
        public float RightHandHorizontalRatio;
        public float LeftHandBelowShoulderRatio;
        public float RightHandBelowShoulderRatio;
        public float LeftHandTorsoSignedClearance;
        public float RightHandTorsoSignedClearance;
        public float MinHandTorsoSignedClearance;
        public float HandTorsoPenetrationRisk;
        public float LeftShoulderDownUpMuscle;
        public float LeftShoulderFrontBackMuscle;
        public float LeftArmDownUpMuscle;
        public float LeftArmFrontBackMuscle;
        public float LeftArmTwistMuscle;
        public float LeftForearmStretchMuscle;
        public float LeftForearmTwistMuscle;
        public float RightShoulderDownUpMuscle;
        public float RightShoulderFrontBackMuscle;
        public float RightArmDownUpMuscle;
        public float RightArmFrontBackMuscle;
        public float RightArmTwistMuscle;
        public float RightForearmStretchMuscle;
        public float RightForearmTwistMuscle;
        public ArmSwingGuardDiagnostics ArmSwingGuard;
        public float LeftThumb1StretchMuscle;
        public float LeftThumbSpreadMuscle;
        public float LeftIndex1StretchMuscle;
        public float LeftIndexSpreadMuscle;
        public float LeftMiddle1StretchMuscle;
        public float LeftMiddleSpreadMuscle;
        public float LeftRing1StretchMuscle;
        public float LeftRingSpreadMuscle;
        public float LeftLittle1StretchMuscle;
        public float LeftLittleSpreadMuscle;
        public float RightThumb1StretchMuscle;
        public float RightThumbSpreadMuscle;
        public float RightIndex1StretchMuscle;
        public float RightIndexSpreadMuscle;
        public float RightMiddle1StretchMuscle;
        public float RightMiddleSpreadMuscle;
        public float RightRing1StretchMuscle;
        public float RightRingSpreadMuscle;
        public float RightLittle1StretchMuscle;
        public float RightLittleSpreadMuscle;
        public ThumbGuardDiagnostics ThumbGuard;
        public YybDiagnosticMetrics YybDiagnostics;

        public string ToCsvLine()
        {
            return MotionComparisonProbeReportWriter.BuildMetricsCsvLine(
                Escape(Label),
                Escape(Scene),
                Escape(Reason),
                F(Elapsed),
                F(TimeSinceLevelLoad),
                I(FrameCount),
                I(RecorderFrame),
                Escape(AnimationTimeSource),
                Escape(AnimationClipName),
                F(AnimationClipTime),
                F(AnimationClipLength),
                F(AnimationNormalizedTime),
                F(RootPosition.x),
                F(RootPosition.y),
                F(RootPosition.z),
                F(RootYaw),
                F(RootSpike.LastRootDeltaMagnitude),
                F(RootSpike.MaxRootDeltaMagnitude),
                I(RootSpike.RootDeltaSpikeSkippedCount),
                F(RootSpike.LastRootPositionPoseDeltaMagnitude),
                F(RootSpike.MaxRootPositionPoseDeltaMagnitude),
                I(RootSpike.RootPositionSpikeClampedCount),
                F(RootSpike.LastGroundingAdjustment),
                F(RootSpike.MaxGroundingAdjustment),
                I(RootSpike.GroundingStepClampedCount),
                I(RootSpike.GroundingSmoothedCount),
                F(RootSpike.LastGroundingVerticalStep),
                F(RootSpike.MaxGroundingVerticalStep),
                F(RootSpike.InitialGroundingVerticalStep),
                F(RootSpike.MaxGroundingVerticalStepAfterInitial),
                F(RootSpike.LastGroundingTargetY),
                F(RootSpike.LastGroundingLowestFootBottomY),
                F(RootSpike.GroundingMaxStepPerFrame),
                F(RootSpike.GroundingLastStepToMaxStepRatio),
                I(RootSpike.GroundingLastStepAtMaxStep),
                F(RootSpike.RecordingStartRootY),
                F(RootSpike.RecordingStartBodyPositionY),
                F(RootSpike.RecordingStartHipsLocalY),
                F(RootSpike.RecordingStartHipsY),
                F(RootSpike.RecordingStartHipsReferenceBeforeLocalY),
                F(RootSpike.RecordingStartHipsReferenceAfterLocalY),
                F(RootSpike.RecordingStartHipsReferenceDeltaY),
                I(RootSpike.RecordingStartHipsReferenceFlipDetected),
                Escape(RootSpike.RecordingStartHipsReferenceStage),
                F(RootSpike.PoseInputLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterEditorMuscleReferenceLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterClampPoseMusclesLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterAnatomicalArmGuardLeftShoulderFrontBackMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingLeftShoulderFrontBackMuscle),
                F(RootSpike.SetHumanPoseInputLeftShoulderFrontBackMuscle),
                F(RootSpike.SetHumanPoseOutputLeftShoulderFrontBackMuscle),
                F(RootSpike.SetHumanPoseLeftShoulderFrontBackDelta),
                F(RootSpike.PoseInputLeftArmTwistMuscle),
                F(RootSpike.AfterEditorMuscleReferenceLeftArmTwistMuscle),
                F(RootSpike.AfterClampPoseMusclesLeftArmTwistMuscle),
                F(RootSpike.AfterAnatomicalArmGuardLeftArmTwistMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingLeftArmTwistMuscle),
                F(RootSpike.SetHumanPoseInputLeftArmTwistMuscle),
                F(RootSpike.SetHumanPoseOutputLeftArmTwistMuscle),
                F(RootSpike.SetHumanPoseLeftArmTwistDelta),
                F(RootSpike.PoseInputLeftForearmStretchMuscle),
                F(RootSpike.AfterEditorMuscleReferenceLeftForearmStretchMuscle),
                F(RootSpike.AfterClampPoseMusclesLeftForearmStretchMuscle),
                F(RootSpike.AfterAnatomicalArmGuardLeftForearmStretchMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingLeftForearmStretchMuscle),
                F(RootSpike.SetHumanPoseInputLeftForearmStretchMuscle),
                F(RootSpike.SetHumanPoseOutputLeftForearmStretchMuscle),
                F(RootSpike.SetHumanPoseLeftForearmStretchDelta),
                F(RootSpike.PoseInputRightForearmStretchMuscle),
                F(RootSpike.AfterEditorMuscleReferenceRightForearmStretchMuscle),
                F(RootSpike.AfterClampPoseMusclesRightForearmStretchMuscle),
                F(RootSpike.AfterAnatomicalArmGuardRightForearmStretchMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingRightForearmStretchMuscle),
                F(RootSpike.SetHumanPoseInputRightForearmStretchMuscle),
                F(RootSpike.SetHumanPoseOutputRightForearmStretchMuscle),
                F(RootSpike.SetHumanPoseRightForearmStretchDelta),
                F(RootSpike.PoseInputRightArmTwistMuscle),
                F(RootSpike.AfterEditorMuscleReferenceRightArmTwistMuscle),
                F(RootSpike.AfterClampPoseMusclesRightArmTwistMuscle),
                F(RootSpike.AfterAnatomicalArmGuardRightArmTwistMuscle),
                F(RootSpike.AfterVisualSpikeSmoothingRightArmTwistMuscle),
                F(RootSpike.SetHumanPoseInputRightArmTwistMuscle),
                F(RootSpike.SetHumanPoseOutputRightArmTwistMuscle),
                F(RootSpike.SetHumanPoseRightArmTwistDelta),
                F(RootSpike.SetHumanPoseInputLeftUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseOutputLeftUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseLeftUpperLegFrontBackDelta),
                F(RootSpike.SetHumanPoseInputRightUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseOutputRightUpperLegFrontBackMuscle),
                F(RootSpike.SetHumanPoseRightUpperLegFrontBackDelta),
                F(RootSpike.SetHumanPoseInputLeftLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseOutputLeftLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseLeftLowerLegStretchDelta),
                F(RootSpike.SetHumanPoseInputRightLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseOutputRightLowerLegStretchMuscle),
                F(RootSpike.SetHumanPoseRightLowerLegStretchDelta),
                F(RootSpike.SetHumanPoseInputLeftFootUpDownMuscle),
                F(RootSpike.SetHumanPoseOutputLeftFootUpDownMuscle),
                F(RootSpike.SetHumanPoseLeftFootUpDownDelta),
                F(RootSpike.SetHumanPoseInputRightFootUpDownMuscle),
                F(RootSpike.SetHumanPoseOutputRightFootUpDownMuscle),
                F(RootSpike.SetHumanPoseRightFootUpDownDelta),
                F(BodyPositionY),
                F(HipsLocalY),
                F(RootSpike.FootHeightReferenceLift),
                F(HipsPosition.x),
                F(HipsPosition.z),
                F(HipsY),
                F(LowestFootY),
                F(LowestFootBottomY),
                F(LeftFootPosition.x),
                F(LeftFootPosition.z),
                F(RightFootPosition.x),
                F(RightFootPosition.z),
                F(MeshBoundsMinY),
                F(MeshBoundsMaxY),
                F(FootBottomGroundGap),
                F(MeshBoundsGroundGap),
                F(CameraFacingDot),
                F(MaxScaleDelta),
                V(LeftUpperArmScale),
                V(RightUpperArmScale),
                V(LeftUpperLegScale),
                V(RightUpperLegScale),
                F(LeftArmLength),
                F(RightArmLength),
                F(LeftLegLength),
                F(RightLegLength),
                F(LeftElbowAngle),
                F(RightElbowAngle),
                F(LeftKneeAngle),
                F(RightKneeAngle),
                F(LeftElbowBendForward),
                F(RightElbowBendForward),
                F(LeftKneeBendForward),
                F(RightKneeBendForward),
                F(LeftElbowBendOffsetForward),
                F(RightElbowBendOffsetForward),
                F(LeftKneeBendOffsetForward),
                F(RightKneeBendOffsetForward),
                F(LeftUpperArmDownDot),
                F(RightUpperArmDownDot),
                F(LeftHandHorizontalRatio),
                F(RightHandHorizontalRatio),
                F(LeftHandBelowShoulderRatio),
                F(RightHandBelowShoulderRatio),
                F(LeftHandTorsoSignedClearance),
                F(RightHandTorsoSignedClearance),
                F(MinHandTorsoSignedClearance),
                F(HandTorsoPenetrationRisk),
                F(LeftShoulderDownUpMuscle),
                F(LeftShoulderFrontBackMuscle),
                F(LeftArmDownUpMuscle),
                F(LeftArmFrontBackMuscle),
                F(LeftArmTwistMuscle),
                F(ArmSwingGuard.LeftApplied),
                F(ArmSwingGuard.LeftHorizontalReachApplied),
                F(ArmSwingGuard.LeftRaisedReachApplied),
                F(ArmSwingGuard.LeftForearmStretchBefore),
                F(ArmSwingGuard.LeftForearmStretchAfter),
                F(ArmSwingGuard.LeftForearmStretchDelta),
                F(LeftForearmStretchMuscle),
                F(LeftForearmTwistMuscle),
                F(RightShoulderDownUpMuscle),
                F(RightShoulderFrontBackMuscle),
                F(RightArmDownUpMuscle),
                F(RightArmFrontBackMuscle),
                F(RightArmTwistMuscle),
                F(ArmSwingGuard.RightApplied),
                F(ArmSwingGuard.RightHorizontalReachApplied),
                F(ArmSwingGuard.RightRaisedReachApplied),
                F(ArmSwingGuard.RightForearmStretchBefore),
                F(ArmSwingGuard.RightForearmStretchAfter),
                F(ArmSwingGuard.RightForearmStretchDelta),
                F(RightForearmStretchMuscle),
                F(RightForearmTwistMuscle),
                F(LeftThumb1StretchMuscle),
                F(LeftThumbSpreadMuscle),
                F(LeftIndex1StretchMuscle),
                F(LeftIndexSpreadMuscle),
                F(LeftMiddle1StretchMuscle),
                F(LeftMiddleSpreadMuscle),
                F(LeftRing1StretchMuscle),
                F(LeftRingSpreadMuscle),
                F(LeftLittle1StretchMuscle),
                F(LeftLittleSpreadMuscle),
                F(RightThumb1StretchMuscle),
                F(RightThumbSpreadMuscle),
                F(RightIndex1StretchMuscle),
                F(RightIndexSpreadMuscle),
                F(RightMiddle1StretchMuscle),
                F(RightMiddleSpreadMuscle),
                F(RightRing1StretchMuscle),
                F(RightRingSpreadMuscle),
                F(RightLittle1StretchMuscle),
                F(RightLittleSpreadMuscle),
                V(SpineLocalEuler),
                V(ChestLocalEuler),
                V(UpperChestLocalEuler),
                V(LeftShoulderLocalEuler),
                V(RightShoulderLocalEuler),
                V(LeftUpperArmLocalEuler),
                V(RightUpperArmLocalEuler),
                V(LeftLowerArmLocalEuler),
                V(RightLowerArmLocalEuler),
                V(LeftHandLocalEuler),
                V(RightHandLocalEuler),
                V(LeftThumbProximalLocalEuler),
                V(LeftIndexProximalLocalEuler),
                V(LeftMiddleProximalLocalEuler),
                V(LeftRingProximalLocalEuler),
                V(LeftLittleProximalLocalEuler),
                V(RightThumbProximalLocalEuler),
                V(RightIndexProximalLocalEuler),
                V(RightMiddleProximalLocalEuler),
                V(RightRingProximalLocalEuler),
                V(RightLittleProximalLocalEuler),
                F(YybDiagnostics.Left.ThumbIndexSpreadAngle),
                F(YybDiagnostics.Right.ThumbIndexSpreadAngle),
                F(YybDiagnostics.Left.ThumbPalmProjection),
                F(YybDiagnostics.Right.ThumbPalmProjection),
                F(YybDiagnostics.Left.ThumbSpreadRisk),
                F(YybDiagnostics.Right.ThumbSpreadRisk),
                F(YybDiagnostics.Left.ThumbProjectionRisk),
                F(YybDiagnostics.Right.ThumbProjectionRisk),
                F(YybDiagnostics.Left.ThumbHelperSourceDistance),
                F(YybDiagnostics.Right.ThumbHelperSourceDistance),
                F(YybDiagnostics.Left.ThumbHelperSourceDistanceDelta),
                F(YybDiagnostics.Right.ThumbHelperSourceDistanceDelta),
                F(YybDiagnostics.Left.ThumbHelperSourceRotationDelta),
                F(YybDiagnostics.Right.ThumbHelperSourceRotationDelta),
                F(YybDiagnostics.Left.ThumbHelperSeparationRisk),
                F(YybDiagnostics.Right.ThumbHelperSeparationRisk),
                F(YybDiagnostics.Left.WebbingRisk),
                F(YybDiagnostics.Right.WebbingRisk),
                F(YybDiagnostics.Left.ArmTwistRisk),
                F(YybDiagnostics.Right.ArmTwistRisk),
                F(YybDiagnostics.Left.SleeveAnchorRisk),
                F(YybDiagnostics.Right.SleeveAnchorRisk),
                F(YybDiagnostics.Left.SleeveAnchorDistance),
                F(YybDiagnostics.Right.SleeveAnchorDistance),
                F(YybDiagnostics.Left.SleeveThicknessRatio),
                F(YybDiagnostics.Right.SleeveThicknessRatio),
                F(YybDiagnostics.Left.SleeveThicknessRisk),
                F(YybDiagnostics.Right.SleeveThicknessRisk),
                F(YybDiagnostics.Left.DeformationRisk),
                F(YybDiagnostics.Right.DeformationRisk),
                F(YybDiagnostics.MaxDeformationRisk),
                F(ThumbGuard.ManualThumbReferenceConfigured),
                F(ThumbGuard.ManualThumbReferenceActive),
                F(ThumbGuard.PoseShapingSuppressed),
                F(ThumbGuard.LeftPoseShapingSuppressed),
                F(ThumbGuard.RightPoseShapingSuppressed),
                F(ThumbGuard.ProjectionGuardWeight),
                F(ThumbGuard.LeftProjectionGuardWeight),
                F(ThumbGuard.RightProjectionGuardWeight),
                F(ThumbGuard.IndexSpreadGuardWeight),
                F(ThumbGuard.LeftIndexSpreadGuardWeight),
                F(ThumbGuard.RightIndexSpreadGuardWeight),
                F(ThumbGuard.SegmentStraightenWeight),
                F(ThumbGuard.LeftSegmentStraightenWeight),
                F(ThumbGuard.RightSegmentStraightenWeight),
                F(ThumbGuard.LeftProjectionCorrectionApplyCount),
                F(ThumbGuard.RightProjectionCorrectionApplyCount),
                F(ThumbGuard.LeftProjectionCorrectionPreserveCount),
                F(ThumbGuard.RightProjectionCorrectionPreserveCount),
                F(ThumbGuard.LeftSegmentStraightenApplyCount),
                F(ThumbGuard.RightSegmentStraightenApplyCount),
                F(ThumbGuard.LeftSegmentStraightenPreserveCount),
                F(ThumbGuard.RightSegmentStraightenPreserveCount),
                F(ThumbGuard.LeftLocalRotationGuardClampCount),
                F(ThumbGuard.RightLocalRotationGuardClampCount),
                F(ThumbGuard.LeftLocalRotationGuardPreserveCount),
                F(ThumbGuard.RightLocalRotationGuardPreserveCount),
                F(ThumbGuard.LeftLocalRotationGuardCurrentRisk),
                F(ThumbGuard.RightLocalRotationGuardCurrentRisk),
                F(ThumbGuard.LeftLocalRotationGuardLimitedRisk),
                F(ThumbGuard.RightLocalRotationGuardLimitedRisk),
                F(ThumbGuard.LeftWorldRotationSuppressCompetingOverride),
                F(ThumbGuard.RightWorldRotationSuppressCompetingOverride),
                F(ThumbGuard.LeftWorldRotationKeepDetachedHelperOverride),
                F(ThumbGuard.RightWorldRotationKeepDetachedHelperOverride),
                F(ThumbGuard.LeftWorldRotationCurrentReferenceFrameDeviation),
                F(ThumbGuard.RightWorldRotationCurrentReferenceFrameDeviation),
                F(ThumbGuard.LeftWorldRotationCandidateReferenceFrameDeviation),
                F(ThumbGuard.RightWorldRotationCandidateReferenceFrameDeviation),
                F(ThumbGuard.LeftProximalWorldRotationPreserveReason),
                F(ThumbGuard.RightProximalWorldRotationPreserveReason),
                F(ThumbGuard.LeftIntermediateWorldRotationPreserveReason),
                F(ThumbGuard.RightIntermediateWorldRotationPreserveReason),
                F(ThumbGuard.LeftProximalWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.RightProximalWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.LeftIntermediateWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.RightIntermediateWorldRotationCurrentReferenceAngle),
                F(ThumbGuard.LeftProximalWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.RightProximalWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.LeftIntermediateWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.RightIntermediateWorldRotationCandidateReferenceAngle),
                F(ThumbGuard.LeftProximalWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.RightProximalWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.LeftIntermediateWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.RightIntermediateWorldRotationPreserveCurrentRisk),
                F(ThumbGuard.LeftProximalWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.RightProximalWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.LeftIntermediateWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.RightIntermediateWorldRotationPreserveLimitedRisk),
                F(ThumbGuard.HelperSyncEnabled),
                F(ThumbGuard.HelperPositionSyncEnabled),
                F(ThumbGuard.HelperSyncWeight),
                F(ThumbGuard.HelperMaxLocalAngle),
                F(ThumbGuard.PalmStabilizeEnabled),
                F(ThumbGuard.PalmStabilizeWeight),
                F(ThumbGuard.PalmStabilizeMaxLocalAngle),
                F(ThumbGuard.WebbingStabilizeEnabled),
                F(ThumbGuard.WebbingStabilizeWeight),
                F(ThumbGuard.WebbingMaxLocalAngle),
                F(ThumbGuard.WebbingMaxPositionOffset),
                F(RootSpike.RetargetStageGhost.LeftFootWorldX),
                F(RootSpike.RetargetStageGhost.LeftFootWorldZ),
                F(RootSpike.RetargetStageGhost.LeftToesWorldX),
                F(RootSpike.RetargetStageGhost.LeftToesWorldZ),
                F(RootSpike.RetargetStageGhost.RightFootWorldX),
                F(RootSpike.RetargetStageGhost.RightFootWorldZ),
                F(RootSpike.RetargetStageGhost.RightToesWorldX),
                F(RootSpike.RetargetStageGhost.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightToesWorldX),
                F(RootSpike.RetargetStageAfterSetHumanPose.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.RightFootWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterManualReferences.RightToesWorldX),
                F(RootSpike.RetargetStageAfterManualReferences.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.RightFootWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterRootRestore.RightToesWorldX),
                F(RootSpike.RetargetStageAfterRootRestore.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.RightFootWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterRootDelta.RightToesWorldX),
                F(RootSpike.RetargetStageAfterRootDelta.RightToesWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.LeftFootWorldX),
                F(RootSpike.RetargetStageAfterGrounding.LeftFootWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.LeftToesWorldX),
                F(RootSpike.RetargetStageAfterGrounding.LeftToesWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.RightFootWorldX),
                F(RootSpike.RetargetStageAfterGrounding.RightFootWorldZ),
                F(RootSpike.RetargetStageAfterGrounding.RightToesWorldX),
                F(RootSpike.RetargetStageAfterGrounding.RightToesWorldZ),
                F(RootSpike.EditorFootLocalRotationLeftFootXzDelta),
                F(RootSpike.EditorFootLocalRotationRightFootXzDelta),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootXzDelta),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootXzDelta),
                Escape(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionSegment),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAxisX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAxisY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxCorrectionAxisZ),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxReferenceDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxReferenceDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxReferenceDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPreDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionMaxPostDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToesCorrectionAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesReferenceDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPreDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootToToesPostDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesReferenceDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPreDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPreDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPreDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPostDirectionX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPostDirectionY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootToToesPostDirectionZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftToesWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftToesWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftToesWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightToesWorldX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightToesWorldY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightToesWorldZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxisZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxisZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootForwardX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootForwardY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootForwardZ),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootUpX),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootUpY),
                F(RootSpike.EditorLowerBodySegmentDirectionLeftFootUpZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootForwardX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootForwardY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootForwardZ),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootUpX),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootUpY),
                F(RootSpike.EditorLowerBodySegmentDirectionRightFootUpZ),
                F(RootSpike.EditorFootHipsAlignedResidualYawLeftFootXzDelta),
                F(RootSpike.EditorFootHipsAlignedResidualYawRightFootXzDelta),
                F(RootSpike.PostSetRightEndpointDesiredFootWorldX),
                F(RootSpike.PostSetRightEndpointDesiredFootWorldZ),
                F(RootSpike.PostSetRightEndpointDesiredToesWorldX),
                F(RootSpike.PostSetRightEndpointDesiredToesWorldZ),
                F(RootSpike.PostSetRightEndpointCurrentFootWorldX),
                F(RootSpike.PostSetRightEndpointCurrentFootWorldZ),
                F(RootSpike.PostSetRightEndpointCurrentToesWorldX),
                F(RootSpike.PostSetRightEndpointCurrentToesWorldZ),
                F(RootSpike.PostSetRightEndpointDeltaBeforeClampX),
                F(RootSpike.PostSetRightEndpointDeltaBeforeClampZ),
                F(RootSpike.PostSetRightEndpointDeltaAfterClampX),
                F(RootSpike.PostSetRightEndpointDeltaAfterClampZ),
                F(RootSpike.PostSetRightEndpointDeltaAfterPositiveZScaleX),
                F(RootSpike.PostSetRightEndpointDeltaAfterPositiveZScaleZ),
                F(RootSpike.PostSetRightEndpointCorrectionX),
                F(RootSpike.PostSetRightEndpointCorrectionZ),
                F(RootSpike.PostSetRightEndpointNextFootWorldX),
                F(RootSpike.PostSetRightEndpointNextFootWorldZ),
                F(RootSpike.PostSetRightEndpointMaxYawAngle),
                F(RootSpike.PostSetRightEndpointYawCorrectionAngle),
                F(RootSpike.PostSetRightEndpointUpperLegRotationDeltaAngle),
                F(RootSpike.PostSetRightEndpointApplied),
                F(RootSpike.PostSetRightEndpointEvaluatorXzReferenceEnabled),
                F(RootSpike.PostSetRightEndpointEvaluatorXzFirstOffsetX),
                F(RootSpike.PostSetRightEndpointEvaluatorXzFirstOffsetZ),
                F(RootSpike.PostSetRightEndpointEvaluatorXzNormalizedDeltaX),
                F(RootSpike.PostSetRightEndpointEvaluatorXzNormalizedDeltaZ),
                F(RootSpike.PostSetRightEndpointEvaluatorXzNormalizedMagnitude),
                F(RootSpike.PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaX),
                F(RootSpike.PostSetRightEndpointEvaluatorXzDesiredNormalizedDeltaZ),
                F(RootSpike.PostSetRightEndpointEvaluatorXzTargetMagnitude));
        }

        private static string F(float value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvFloat(value);
        }

        private static string I(int value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvInt(value);
        }

        private static string V(Vector3 value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvVector(value);
        }

        private static string Escape(string value)
        {
            return MotionComparisonProbeReportWriter.FormatMetricsCsvText(value);
        }
    }
}
