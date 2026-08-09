using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(30000)]
public partial class MotionComparisonProbe : MonoBehaviour
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

}
