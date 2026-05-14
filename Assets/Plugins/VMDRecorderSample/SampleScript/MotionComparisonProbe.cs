using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(30000)]
public class MotionComparisonProbe : MonoBehaviour
{
    private static readonly float[] DefaultSampleTimes = { 0f, 3f, 10f, 13.2f, 30f, 60f, 120f };
    private const string PoseSpaceRetargeterLegacyClipStateName = "__PoseSpaceRetargeter_GhostClip";

    [SerializeField] private string comparisonLabel = "";
    [SerializeField] private float[] sampleTimes = { 0f, 3f, 10f, 13.2f, 30f, 60f, 120f };
    [SerializeField] private bool sampleByAnimationClipTime = true;
    [SerializeField] private bool logSamples = true;
    [SerializeField] private bool captureSampleScreenshots = true;
    [SerializeField] private bool captureFingerCloseups = true;
    [SerializeField] private bool captureYybDiagnosticOnlyMetrics = true;
    [SerializeField, Min(128)] private int screenshotWidth = 960;
    [SerializeField, Min(128)] private int screenshotHeight = 960;
    [SerializeField, Range(1f, 2f)] private float screenshotPadding = 1.2f;
    [SerializeField, Range(1f, 4f)] private float fingerCloseupPadding = 1.6f;

    private const string OutputDocsFolderName = "Docs";
    private const string OutputRootFolderName = "Machine_Spirit";
    private const string OutputLocalFolderName = "Local";
    private const string ComparisonFolderName = "ComparisonLogs";
    private const string ComparisonFramesFolderName = "ComparisonFrames";
    private const string ComparisonSessionsFolderName = "ComparisonSessions";
    private const string SessionManifestFileName = "index.md";
    private const string FrameSessionIndexFileName = "session_index.md";
    private const int EvidenceFileNamePartMaxLength = 48;
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
    public bool IsSampling => _isSampling;
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

    public void StartSampling(string labelOverride = "")
    {
        _animator = GetComponent<Animator>();
        _recorder = GetComponent<UnityHumanoidVMDRecorder>();
        _camera = Camera.main;
        _isYybDiagnosticTarget = IsYybDiagnosticTarget();

        if (_animator == null)
        {
            Debug.LogWarning("[MotionComparisonProbe] Animator가 없어 비교 샘플링을 시작하지 못했습니다.");
            return;
        }

        PrepareHumanPoseCapture();
        ResetDiagnosticBaselines();
        ResetRiskSummary();

        comparisonLabel = string.IsNullOrWhiteSpace(labelOverride)
            ? SanitizeFileName(string.IsNullOrWhiteSpace(comparisonLabel) ? gameObject.name : comparisonLabel)
            : SanitizeFileName(labelOverride);

        _sessionStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        _evidenceBaseName = BuildEvidenceBaseName("metrics", "session", "probe");
        _sessionId = BuildEvidenceBaseName("comparison-session", "motion-analysis", "probe");
        _csvPath = BuildUniqueOutputPath(GetComparisonOutputFolder(), $"{_evidenceBaseName}.csv");
        WriteHeader(_csvPath);
        PrepareSessionOutput();
        PrepareScreenshotOutput();
        WriteSessionManifest("started");

        _startTime = Time.time;
        _nextSampleIndex = 0;
        _isSampling = true;
        if (_recorder != null && _recorder.FrameNumber != 0)
        {
            Debug.LogWarning($"[MotionComparisonProbe] 비교 샘플링이 recorderFrame={_recorder.FrameNumber}에서 시작했습니다. Main/Sub 동작 비교는 0프레임 시작 세션만 기준으로 사용하세요.");
        }

        SampleNow("start");
        SkipElapsedSampleTimes(GetCurrentSampleClock(0f));
    }

    public void StopSampling(string reason = "stop")
    {
        if (!_isSampling)
        {
            return;
        }

        SampleNow(reason);
        _isSampling = false;
        WriteSessionManifest(reason);
    }

    public void SampleNow(string reason = "sample")
    {
        if (_animator == null || string.IsNullOrEmpty(_csvPath))
        {
            return;
        }

        PoseMetrics metrics = CaptureMetrics(reason);
        UpdateRiskSummary(metrics.YybDiagnostics, false, reason, metrics.AnimationClipTime, metrics.RecorderFrame);
        File.AppendAllText(_csvPath, metrics.ToCsvLine() + Environment.NewLine, Encoding.UTF8);
        CaptureSampleScreenshots(reason, metrics);
        WriteSessionManifest(reason);

        if (logSamples)
        {
            Debug.Log($"[MotionComparisonProbe] {comparisonLabel} {reason} t={metrics.Elapsed:F2}s clip={metrics.AnimationClipTime:F3}s frame={metrics.RecorderFrame} hipsY={metrics.HipsY:F3} facing={metrics.CameraFacingDot:F3} scaleDelta={metrics.MaxScaleDelta:F4} yybRisk={metrics.YybDiagnostics.MaxDeformationRisk:F3}");
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
            SampleNow($"t{sampleTimes[_nextSampleIndex]:0.###}");
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

        StopSampling("disabled");
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

    private float GetCurrentSampleClock(float elapsedFallback)
    {
        if (!sampleByAnimationClipTime)
        {
            return elapsedFallback;
        }

        AnimationTimeMetrics animationTime = CaptureAnimationTimeMetrics();
        if (float.IsNaN(animationTime.ClipTime) || float.IsInfinity(animationTime.ClipTime))
        {
            return elapsedFallback;
        }

        return animationTime.ClipTime;
    }

    private PoseMetrics CaptureMetrics(string reason)
    {
        Transform root = _animator.transform;
        Transform hips = GetBone(HumanBodyBones.Hips);
        Transform leftFoot = GetBone(HumanBodyBones.LeftFoot);
        Transform rightFoot = GetBone(HumanBodyBones.RightFoot);

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
        float meshBoundsMinY = float.NaN;
        float meshBoundsMaxY = float.NaN;
        if (TryGetRendererBounds(out Bounds rendererBounds))
        {
            meshBoundsMinY = rendererBounds.min.y;
            meshBoundsMaxY = rendererBounds.max.y;
        }

        ThumbGuardDiagnostics thumbGuardDiagnostics = CaptureThumbGuardDiagnostics();
        YybDiagnosticMetrics yybDiagnostics = captureYybDiagnosticOnlyMetrics
            ? CaptureYybDiagnosticMetrics(armMuscles)
            : YybDiagnosticMetrics.Empty;

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
            HipsY = hips != null ? hips.position.y : float.NaN,
            LowestFootY = lowestFootY,
            LowestFootBottomY = lowestFootBottomY,
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
        clipTime = Mathf.Clamp(clipTime, 0f, clipLength);

        metrics = new AnimationTimeMetrics
        {
            Source = "retargeterLegacy",
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
            Source = "animatorState",
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
            LastGroundingVerticalStep = ReadFloatProperty(type, retargeter, "LastGroundingVerticalStep"),
            MaxGroundingVerticalStep = ReadFloatProperty(type, retargeter, "MaxGroundingVerticalStep"),
            InitialGroundingVerticalStep = ReadFloatProperty(type, retargeter, "InitialGroundingVerticalStep"),
            MaxGroundingVerticalStepAfterInitial = ReadFloatProperty(type, retargeter, "MaxGroundingVerticalStepAfterInitial"),
            LastGroundingTargetY = ReadFloatProperty(type, retargeter, "LastGroundingTargetY"),
            LastGroundingLowestFootBottomY = ReadFloatProperty(type, retargeter, "LastGroundingLowestFootBottomY")
        };
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
        float leftArmSleeveRisk = CalculateArmSleeveDeformationRisk(left.ArmTwistRisk, left.SleeveAnchorRisk);
        float rightArmSleeveRisk = CalculateArmSleeveDeformationRisk(right.ArmTwistRisk, right.SleeveAnchorRisk);
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
            string sideName = isRightSide ? "right" : "left";
            string distanceKey = BuildPairKey($"thumb-helper-distance-{sideName}", helper, source);
            metrics.ThumbHelperSourceDistanceDelta = CalculateDistanceDeltaFromInitial(helper, source, distanceKey, out float distance);
            metrics.ThumbHelperSourceDistance = distance;

            string rotationKey = BuildPairKey($"thumb-helper-rotation-{sideName}", source, helper);
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
        string sideToken = isRightSide ? "right" : "left";
        source = FindDiagnosticTransform($"thumb-explicit-source-{sideToken}", candidate =>
        {
            string normalizedName = NormalizeTransformName(candidate.name);
            return normalizedName.Contains(sideToken) && IsActiveThumbBaseSourceName(normalizedName);
        });
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

        string normalizedName = NormalizeTransformName(candidate.name);
        if (string.IsNullOrEmpty(normalizedName) ||
            !normalizedName.Contains("thumb") ||
            normalizedName.Contains("ghost") ||
            IsActiveThumbBaseSourceName(normalizedName))
        {
            return false;
        }

        if (normalizedName.Contains("thumb1") ||
            normalizedName.Contains("thumb2") ||
            normalizedName.Contains("thumb3") ||
            normalizedName.Contains("proximal") ||
            normalizedName.Contains("intermediate") ||
            normalizedName.Contains("distal") ||
            normalizedName.Contains("thumbtip"))
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
        UpdateRiskSummary(diagnostics, true, "realtime", animationTime.ClipTime, recorderFrame);
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
        return NameSuggestsYybModel(gameObject.name) || NameSuggestsYybModel(comparisonLabel);
    }

    private static bool NameSuggestsYybModel(string value)
    {
        return NormalizeTransformName(value).Contains("yyb");
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
        string sideToken = isRightSide ? "right" : "left";
        return FindDiagnosticTransform($"thumb-helper-{sideToken}", candidate =>
        {
            string normalizedName = NormalizeTransformName(candidate.name);
            return normalizedName.Contains(sideToken) && IsDetachedThumbBaseHelperName(normalizedName);
        });
    }

    private Transform FindThumbBaseSource(bool isRightSide)
    {
        string sideToken = isRightSide ? "right" : "left";
        Transform source = FindDiagnosticTransform($"thumb-source-{sideToken}", candidate =>
        {
            string normalizedName = NormalizeTransformName(candidate.name);
            return normalizedName.Contains(sideToken) && IsActiveThumbBaseSourceName(normalizedName);
        });

        if (source != null)
        {
            return source;
        }

        return GetBone(isRightSide ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
    }

    private Transform FindSleeveAnchor(bool isRightSide)
    {
        string suffix = isRightSide ? "joint_RightArmM" : "joint_LeftArmM";
        return FindDiagnosticTransform($"sleeve-anchor-{suffix}", candidate =>
            MatchesTransformNameSuffix(candidate.name, suffix));
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

        string sideName = isRightSide ? "right" : "left";
        string key = BuildPairKey($"sleeve-anchor-rotation-{sideName}", source, anchor);
        float rotationDelta = CalculateRelativeRotationDeltaFromInitial(source, anchor, key);
        return RiskAbove(
            rotationDelta,
            DiagnosticSleeveAnchorWarningDegrees,
            DiagnosticSleeveAnchorFullRiskDegrees);
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

    private static string BuildPairKey(string label, Transform a, Transform b)
    {
        return string.Join(":",
            label,
            a != null ? a.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "0",
            b != null ? b.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "0");
    }

    private static bool IsDetachedThumbBaseHelperName(string normalizedName)
    {
        if (string.IsNullOrEmpty(normalizedName) ||
            normalizedName.Contains("!") ||
            normalizedName.Contains("ghost") ||
            normalizedName.Contains("thumb0m"))
        {
            return false;
        }

        return IsThumbBaseName(normalizedName);
    }

    private static bool IsActiveThumbBaseSourceName(string normalizedName)
    {
        return !string.IsNullOrEmpty(normalizedName) &&
            normalizedName.Contains("thumb0m") &&
            !normalizedName.Contains("ghost") &&
            !normalizedName.Contains("thumb1") &&
            !normalizedName.Contains("thumb2") &&
            !normalizedName.Contains("thumbtip");
    }

    private static bool IsThumbBaseName(string normalizedName)
    {
        return !string.IsNullOrEmpty(normalizedName) &&
            normalizedName.Contains("thumb0") &&
            !normalizedName.Contains("thumb1") &&
            !normalizedName.Contains("thumb2") &&
            !normalizedName.Contains("thumbtip");
    }

    private static bool MatchesTransformNameSuffix(string transformName, string targetName)
    {
        if (string.IsNullOrEmpty(transformName) || string.IsNullOrEmpty(targetName))
        {
            return false;
        }

        return transformName == targetName ||
            transformName.EndsWith("." + targetName, StringComparison.Ordinal) ||
            transformName.EndsWith(targetName, StringComparison.Ordinal);
    }

    private static string NormalizeTransformName(string value)
    {
        return string.IsNullOrEmpty(value) ? "" : value.ToLowerInvariant();
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
                Debug.LogWarning("[MotionComparisonProbe] 일부 Humanoid arm muscle 인덱스를 찾지 못해 해당 CSV 값은 비워집니다.");
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

    private Transform GetBone(HumanBodyBones bone)
    {
        return _animator != null ? _animator.GetBoneTransform(bone) : null;
    }

    private static string GetComparisonOutputFolder()
    {
        DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
        string rootPath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
        string folder = Path.Combine(rootPath, OutputDocsFolderName, OutputRootFolderName, OutputLocalFolderName, ComparisonFolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string GetComparisonFrameRootFolder()
    {
        DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
        string rootPath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
        string folder = Path.Combine(rootPath, OutputDocsFolderName, OutputRootFolderName, OutputLocalFolderName, ComparisonFramesFolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string GetComparisonSessionRootFolder()
    {
        DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
        string rootPath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
        string folder = Path.Combine(rootPath, OutputDocsFolderName, OutputRootFolderName, OutputLocalFolderName, ComparisonSessionsFolderName);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private void PrepareSessionOutput()
    {
        _sessionFolder = "";
        _sessionManifestPath = "";

        if (string.IsNullOrWhiteSpace(_sessionId))
        {
            return;
        }

        _sessionFolder = BuildUniqueDirectoryPath(GetComparisonSessionRootFolder(), _sessionId);
        Directory.CreateDirectory(_sessionFolder);
        _sessionManifestPath = Path.Combine(_sessionFolder, SessionManifestFileName);
    }

    private void PrepareScreenshotOutput()
    {
        _screenshotFolder = "";
        _screenshotIndexPath = "";
        _screenshotSessionIndexPath = "";

        if (!captureSampleScreenshots)
        {
            return;
        }

        _screenshotFolder = BuildUniqueDirectoryPath(GetComparisonFrameRootFolder(), $"when-{SanitizeFileName(_sessionStamp)}");
        Directory.CreateDirectory(_screenshotFolder);
        _screenshotIndexPath = Path.Combine(_screenshotFolder, "index.csv");
        File.WriteAllText(_screenshotIndexPath, "label,scene,reason,recorderFrame,view,path" + Environment.NewLine, Encoding.UTF8);
        _screenshotSessionIndexPath = Path.Combine(_screenshotFolder, FrameSessionIndexFileName);
        WriteFrameSessionIndex();
    }

    private void CaptureSampleScreenshots(string reason, PoseMetrics metrics)
    {
        if (!captureSampleScreenshots || string.IsNullOrEmpty(_screenshotFolder))
        {
            return;
        }

        StartCoroutine(CaptureSampleScreenshotsAtEndOfFrame(reason, metrics));
    }

    private IEnumerator CaptureSampleScreenshotsAtEndOfFrame(string reason, PoseMetrics metrics)
    {
        yield return new WaitForEndOfFrame();

        if (!captureSampleScreenshots || string.IsNullOrEmpty(_screenshotFolder))
        {
            yield break;
        }

        if (!TryCalculateRenderBounds(out Bounds bounds))
        {
            yield break;
        }

        string safeReason = SanitizeFileName(reason);
        string frameName = metrics.RecorderFrame >= 0
            ? metrics.RecorderFrame.ToString("000000", CultureInfo.InvariantCulture)
            : Time.frameCount.ToString("000000", CultureInfo.InvariantCulture);

        CaptureView(bounds, transform.forward, reason, safeReason, frameName, "front", metrics);
        CaptureView(bounds, transform.right, reason, safeReason, frameName, "right", metrics);
        CaptureFingerCloseups(reason, safeReason, frameName, metrics);
    }

    private void CaptureView(Bounds bounds, Vector3 viewDirection, string reason, string safeReason, string frameName, string viewName, PoseMetrics metrics, float paddingOverride = -1f)
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

        captureCamera.nearClipPlane = 0.01f;
        captureCamera.farClipPlane = distance + bounds.size.magnitude + 10f;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        string fileName = $"pose_{ShortenFileNamePart(safeReason)}_rt-{viewName}_frame-{frameName}.png";
        string path = Path.Combine(_screenshotFolder, fileName);
        Directory.CreateDirectory(_screenshotFolder);
        RenderCameraToPng(captureCamera, path);
        AppendScreenshotIndex(reason, metrics, viewName, path);
    }

    private void CaptureFingerCloseups(string reason, string safeReason, string frameName, PoseMetrics metrics)
    {
        if (!captureFingerCloseups)
        {
            return;
        }

        if (TryCalculateFingerBounds(true, out Bounds leftHandBounds))
        {
            CaptureView(leftHandBounds, transform.forward, reason, safeReason, frameName, "left-hand-front", metrics, fingerCloseupPadding);
            CaptureView(leftHandBounds, transform.right, reason, safeReason, frameName, "left-hand-right", metrics, fingerCloseupPadding);
        }

        if (TryCalculateFingerBounds(false, out Bounds rightHandBounds))
        {
            CaptureView(rightHandBounds, transform.forward, reason, safeReason, frameName, "right-hand-front", metrics, fingerCloseupPadding);
            CaptureView(rightHandBounds, transform.right, reason, safeReason, frameName, "right-hand-right", metrics, fingerCloseupPadding);
        }
    }

    private Camera EnsureCaptureCamera()
    {
        if (_captureCamera != null)
        {
            return _captureCamera;
        }

        GameObject cameraObject = new GameObject($"MotionComparisonCapture_{comparisonLabel}");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        _captureCamera = cameraObject.AddComponent<Camera>();
        _captureCamera.enabled = false;

        if (_camera != null)
        {
            _captureCamera.cullingMask = _camera.cullingMask;
            _captureCamera.allowHDR = _camera.allowHDR;
            _captureCamera.allowMSAA = _camera.allowMSAA;
        }

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

    private void RenderCameraToPng(Camera captureCamera, string path)
    {
        RenderTexture previousRenderTexture = RenderTexture.active;
        RenderTexture renderTexture = RenderTexture.GetTemporary(screenshotWidth, screenshotHeight, 24, RenderTextureFormat.ARGB32);
        Texture2D texture = null;

        try
        {
            captureCamera.targetTexture = renderTexture;
            RenderTexture.active = renderTexture;
            captureCamera.Render();

            texture = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
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

    private void AppendScreenshotIndex(string reason, PoseMetrics metrics, string viewName, string path)
    {
        if (string.IsNullOrEmpty(_screenshotIndexPath))
        {
            return;
        }

        string relativePath = MakeProjectRelativePath(path);
        string line = string.Join(",",
            EscapeCsv(comparisonLabel),
            EscapeCsv(SceneManager.GetActiveScene().name),
            EscapeCsv(reason),
            metrics.RecorderFrame.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(viewName),
            EscapeCsv(relativePath));
        File.AppendAllText(_screenshotIndexPath, line + Environment.NewLine, Encoding.UTF8);
    }

    private void WriteFrameSessionIndex()
    {
        if (string.IsNullOrEmpty(_screenshotSessionIndexPath))
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# 비교 프레임 세션 연결");
        builder.AppendLine();
        builder.AppendLine($"- session id: `{EscapeMarkdown(_sessionId)}`");
        builder.AppendLine($"- session manifest: `{EscapeMarkdown(MakeProjectRelativePath(_sessionManifestPath))}`");
        builder.AppendLine($"- metrics csv: `{EscapeMarkdown(MakeProjectRelativePath(_csvPath))}`");
        builder.AppendLine($"- frame index: `{EscapeMarkdown(MakeProjectRelativePath(_screenshotIndexPath))}`");
        builder.AppendLine();
        builder.AppendLine("이 파일은 `ComparisonFrames`에 분리 저장된 PNG가 어떤 CSV 로그와 같은 실행에서 생성됐는지 추적하기 위한 역참조다.");

        File.WriteAllText(_screenshotSessionIndexPath, builder.ToString(), Encoding.UTF8);
    }

    private void WriteSessionManifest(string stateReason)
    {
        if (string.IsNullOrEmpty(_sessionManifestPath))
        {
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        string updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        string relativeCsvPath = MakeProjectRelativePath(_csvPath);
        string relativeScreenshotFolder = MakeProjectRelativePath(_screenshotFolder);
        string relativeScreenshotIndexPath = MakeProjectRelativePath(_screenshotIndexPath);
        string relativeFrameSessionIndexPath = MakeProjectRelativePath(_screenshotSessionIndexPath);
        ThumbGuardDiagnostics thumbGuardDiagnostics = CaptureThumbGuardDiagnostics();

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# MotionComparisonProbe 세션");
        builder.AppendLine();
        builder.AppendLine($"- session id: `{EscapeMarkdown(_sessionId)}`");
        builder.AppendLine($"- label: `{EscapeMarkdown(comparisonLabel)}`");
        builder.AppendLine($"- scene: `{EscapeMarkdown(sceneName)}`");
        builder.AppendLine($"- last state/reason: `{EscapeMarkdown(stateReason)}`");
        builder.AppendLine($"- created at: `{EscapeMarkdown(_sessionStamp)}`");
        builder.AppendLine($"- updated at: `{EscapeMarkdown(updatedAt)}`");
        builder.AppendLine($"- screenshots enabled: `{captureSampleScreenshots}`");
        builder.AppendLine($"- sample clock: `{(sampleByAnimationClipTime ? "animationClipTime" : "elapsed")}`");
        builder.AppendLine($"- sample times: `{EscapeMarkdown(FormatSampleTimes())}`");
        builder.AppendLine($"- yyb diagnostic only metrics: `{captureYybDiagnosticOnlyMetrics}`");
        builder.AppendLine();
        builder.AppendLine("## 엄지 리스크 요약");
        builder.AppendLine();
        builder.AppendLine($"- risk diagnostics enabled: `{captureYybDiagnosticOnlyMetrics}`");
        builder.AppendLine($"- risk evaluation frames: `{_riskEvaluationFrameCount}`");
        builder.AppendLine($"- left thumb core coverage frames: `{_leftCoreThumbDiagnosticFrameCount}`");
        builder.AppendLine($"- right thumb core coverage frames: `{_rightCoreThumbDiagnosticFrameCount}`");
        builder.AppendLine($"- left thumb helper coverage required: `{_leftHelperCoverageRequired}`");
        builder.AppendLine($"- right thumb helper coverage required: `{_rightHelperCoverageRequired}`");
        builder.AppendLine($"- left thumb helper coverage frames: `{_leftHelperRelationshipFrameCount}`");
        builder.AppendLine($"- right thumb helper coverage frames: `{_rightHelperRelationshipFrameCount}`");
        builder.AppendLine($"- max generic thumb anatomy risk: `{FormatManifestFloat(_maxGenericThumbAnatomyRisk)}`");
        builder.AppendLine($"- max generic thumb anatomy risk reason: `{EscapeMarkdown(_maxGenericThumbAnatomyRiskReason)}`");
        builder.AppendLine($"- max generic thumb anatomy risk clip time: `{FormatManifestFloat(_maxGenericThumbAnatomyRiskClipTime)}`");
        builder.AppendLine($"- max generic thumb anatomy risk recorder frame: `{_maxGenericThumbAnatomyRiskRecorderFrame}`");
        builder.AppendLine($"- max thumb spread risk: `{FormatManifestFloat(_maxThumbSpreadRisk)}`");
        builder.AppendLine($"- max thumb projection risk: `{FormatManifestFloat(_maxThumbProjectionRisk)}`");
        builder.AppendLine($"- max thumb helper separation risk: `{FormatManifestFloat(_maxThumbHelperSeparationRisk)}`");
        builder.AppendLine($"- max thumb webbing risk: `{FormatManifestFloat(_maxThumbWebbingRisk)}`");
        builder.AppendLine($"- max yyb deformation risk: `{FormatManifestFloat(_maxYybDeformationRisk)}`");
        builder.AppendLine($"- max yyb deformation risk reason: `{EscapeMarkdown(_maxYybDeformationRiskReason)}`");
        builder.AppendLine($"- max yyb deformation risk clip time: `{FormatManifestFloat(_maxYybDeformationRiskClipTime)}`");
        builder.AppendLine($"- max yyb deformation risk recorder frame: `{_maxYybDeformationRiskRecorderFrame}`");
        builder.AppendLine($"- left thumb projection guard weight: `{FormatManifestFloat(thumbGuardDiagnostics.LeftProjectionGuardWeight)}`");
        builder.AppendLine($"- right thumb projection guard weight: `{FormatManifestFloat(thumbGuardDiagnostics.RightProjectionGuardWeight)}`");
        builder.AppendLine($"- left thumb index-spread guard weight: `{FormatManifestFloat(thumbGuardDiagnostics.LeftIndexSpreadGuardWeight)}`");
        builder.AppendLine($"- right thumb index-spread guard weight: `{FormatManifestFloat(thumbGuardDiagnostics.RightIndexSpreadGuardWeight)}`");
        builder.AppendLine($"- left thumb segment-straighten guard weight: `{FormatManifestFloat(thumbGuardDiagnostics.LeftSegmentStraightenWeight)}`");
        builder.AppendLine($"- right thumb segment-straighten guard weight: `{FormatManifestFloat(thumbGuardDiagnostics.RightSegmentStraightenWeight)}`");
        builder.AppendLine();
        builder.AppendLine("## 산출물");
        builder.AppendLine();
        builder.AppendLine("| 역할 | 경로 |");
        builder.AppendLine("|---|---|");
        builder.AppendLine($"| metrics csv | `{EscapeMarkdown(relativeCsvPath)}` |");
        builder.AppendLine($"| frame folder | `{EscapeMarkdown(relativeScreenshotFolder)}` |");
        builder.AppendLine($"| frame index csv | `{EscapeMarkdown(relativeScreenshotIndexPath)}` |");
        builder.AppendLine($"| frame session index | `{EscapeMarkdown(relativeFrameSessionIndexPath)}` |");
        builder.AppendLine();
        builder.AppendLine("## 사용 방법");
        builder.AppendLine();
        builder.AppendLine("- 이 `index.md`를 세션 기준점으로 사용한다.");
        builder.AppendLine("- CSV 로그와 PNG 프레임은 기존 폴더 구조를 유지하되, 이 파일과 프레임 폴더의 `session_index.md`로 서로 연결한다.");
        builder.AppendLine("- 분석 문서, contact sheet, 비교 이미지를 추가로 만들면 이 세션 폴더 또는 이 manifest에 경로를 추가한다.");

        File.WriteAllText(_sessionManifestPath, builder.ToString(), Encoding.UTF8);
    }

    private static string BuildUniqueDirectoryPath(string rootFolder, string folderName)
    {
        string safeFolderName = SanitizeFileName(folderName);
        string candidate = Path.Combine(rootFolder, safeFolderName);
        int index = 1;

        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(rootFolder, $"{safeFolderName}_{index:000}");
            index++;
        }

        return candidate;
    }

    private static string MakeProjectRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "";
        }

        DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
        string rootPath = projectRoot != null ? projectRoot.FullName : Application.dataPath;
        string fullPath = Path.GetFullPath(path);
        string fullRoot = Path.GetFullPath(rootPath);

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("\\", "/");
        }

        return fullPath.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("\\", "/");
    }

    private static string BuildUniqueOutputPath(string folderPath, string fileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string candidate = Path.Combine(folderPath, fileName);
        int index = 1;

        while (File.Exists(candidate))
        {
            candidate = Path.Combine(folderPath, $"{baseName}_{index:000}{extension}");
            index++;
        }

        return candidate;
    }

    private string BuildEvidenceBaseName(string what, string why, string how)
    {
        return string.Join("_",
            $"when-{ShortenFileNamePart(SanitizeFileName(_sessionStamp))}",
            $"where-{ShortenFileNamePart(SanitizeFileName(SceneManager.GetActiveScene().name))}",
            $"who-{ShortenFileNamePart(SanitizeFileName(comparisonLabel))}",
            $"what-{ShortenFileNamePart(SanitizeFileName(what))}",
            $"why-{ShortenFileNamePart(SanitizeFileName(why))}",
            $"how-{ShortenFileNamePart(SanitizeFileName(how))}");
    }

    private static string SanitizeFileName(string fileName)
    {
        string cleanName = string.IsNullOrWhiteSpace(fileName) ? "motion_comparison" : fileName.Trim();
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            cleanName = cleanName.Replace(invalidChar, '_');
        }

        return cleanName.Replace(' ', '_');
    }

    private static string ShortenFileNamePart(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= EvidenceFileNamePartMaxLength)
        {
            return value;
        }

        const int hashLength = 8;
        int prefixLength = Mathf.Max(1, EvidenceFileNamePartMaxLength - hashLength - 1);
        return $"{value.Substring(0, prefixLength)}_{CalculateStableHash(value):x8}";
    }

    private static uint CalculateStableHash(string value)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        uint hash = offsetBasis;
        foreach (char character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private string FormatSampleTimes()
    {
        if (sampleTimes == null || sampleTimes.Length == 0)
        {
            return "";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < sampleTimes.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(sampleTimes[i].ToString("0.###", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static string EscapeMarkdown(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.Replace("`", "'").Replace("|", "\\|");
    }

    private static string FormatManifestFloat(float value)
    {
        return IsFinite(value)
            ? value.ToString("0.###", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static void WriteHeader(string path)
    {
        const string header = "label,scene,reason,elapsed,timeSinceLevelLoad,frameCount,recorderFrame,animationTimeSource,animationClipName,animationClipTime,animationClipLength,animationNormalizedTime,rootX,rootY,rootZ,rootYaw,retargetRootDeltaLast,retargetRootDeltaMax,retargetRootDeltaSkippedCount,retargetPoseRootDeltaLast,retargetPoseRootDeltaMax,retargetPoseRootClampCount,retargetGroundingAdjustmentLast,retargetGroundingAdjustmentMax,retargetGroundingStepClampCount,retargetGroundingSmoothedCount,retargetGroundingVerticalStepLast,retargetGroundingVerticalStepMax,retargetGroundingInitialVerticalStep,retargetGroundingVerticalStepAfterInitialMax,retargetGroundingTargetY,retargetGroundingLowestFootBottomY,hipsY,lowestFootY,lowestFootBottomY,meshBoundsMinY,meshBoundsMaxY,footBottomGroundGap,meshBoundsGroundGap,cameraFacingDot,maxScaleDelta,leftUpperArmScale,rightUpperArmScale,leftUpperLegScale,rightUpperLegScale,leftArmLength,rightArmLength,leftLegLength,rightLegLength,leftElbowAngle,rightElbowAngle,leftKneeAngle,rightKneeAngle,leftElbowBendForward,rightElbowBendForward,leftKneeBendForward,rightKneeBendForward,leftElbowBendOffsetForward,rightElbowBendOffsetForward,leftKneeBendOffsetForward,rightKneeBendOffsetForward,leftUpperArmDownDot,rightUpperArmDownDot,leftHandHorizontalRatio,rightHandHorizontalRatio,leftHandBelowShoulderRatio,rightHandBelowShoulderRatio,leftShoulderDownUpMuscle,leftShoulderFrontBackMuscle,leftArmDownUpMuscle,leftArmFrontBackMuscle,leftArmTwistMuscle,leftForearmStretchMuscle,leftForearmTwistMuscle,rightShoulderDownUpMuscle,rightShoulderFrontBackMuscle,rightArmDownUpMuscle,rightArmFrontBackMuscle,rightArmTwistMuscle,rightForearmStretchMuscle,rightForearmTwistMuscle,leftThumb1StretchMuscle,leftThumbSpreadMuscle,leftIndex1StretchMuscle,leftIndexSpreadMuscle,leftMiddle1StretchMuscle,leftMiddleSpreadMuscle,leftRing1StretchMuscle,leftRingSpreadMuscle,leftLittle1StretchMuscle,leftLittleSpreadMuscle,rightThumb1StretchMuscle,rightThumbSpreadMuscle,rightIndex1StretchMuscle,rightIndexSpreadMuscle,rightMiddle1StretchMuscle,rightMiddleSpreadMuscle,rightRing1StretchMuscle,rightRingSpreadMuscle,rightLittle1StretchMuscle,rightLittleSpreadMuscle,spineLocalEuler,chestLocalEuler,upperChestLocalEuler,leftShoulderLocalEuler,rightShoulderLocalEuler,leftUpperArmLocalEuler,rightUpperArmLocalEuler,leftLowerArmLocalEuler,rightLowerArmLocalEuler,leftHandLocalEuler,rightHandLocalEuler,leftThumbProximalLocalEuler,leftIndexProximalLocalEuler,leftMiddleProximalLocalEuler,leftRingProximalLocalEuler,leftLittleProximalLocalEuler,rightThumbProximalLocalEuler,rightIndexProximalLocalEuler,rightMiddleProximalLocalEuler,rightRingProximalLocalEuler,rightLittleProximalLocalEuler";
        const string yybDiagnosticHeader = "leftThumbIndexSpreadAngle,rightThumbIndexSpreadAngle,leftThumbPalmProjection,rightThumbPalmProjection,leftThumbSpreadRisk,rightThumbSpreadRisk,leftThumbProjectionRisk,rightThumbProjectionRisk,leftThumbHelperSourceDistance,rightThumbHelperSourceDistance,leftThumbHelperSourceDistanceDelta,rightThumbHelperSourceDistanceDelta,leftThumbHelperSourceRotationDelta,rightThumbHelperSourceRotationDelta,leftThumbHelperSeparationRisk,rightThumbHelperSeparationRisk,leftWebbingRisk,rightWebbingRisk,leftArmTwistRisk,rightArmTwistRisk,leftSleeveAnchorRisk,rightSleeveAnchorRisk,leftYybDeformationRisk,rightYybDeformationRisk,yybMaxDeformationRisk,thumbGuardManualReferenceConfigured,thumbGuardManualReferenceActive,thumbGuardPoseShapingSuppressed,thumbGuardLeftPoseShapingSuppressed,thumbGuardRightPoseShapingSuppressed,thumbGuardProjectionWeight,thumbGuardLeftProjectionWeight,thumbGuardRightProjectionWeight,thumbGuardIndexSpreadWeight,thumbGuardLeftIndexSpreadWeight,thumbGuardRightIndexSpreadWeight,thumbGuardSegmentStraightenWeight,thumbGuardLeftSegmentStraightenWeight,thumbGuardRightSegmentStraightenWeight,thumbGuardLeftProjectionCorrectionApplyCount,thumbGuardRightProjectionCorrectionApplyCount,thumbGuardLeftProjectionCorrectionPreserveCount,thumbGuardRightProjectionCorrectionPreserveCount,thumbGuardLeftSegmentStraightenApplyCount,thumbGuardRightSegmentStraightenApplyCount,thumbGuardLeftSegmentStraightenPreserveCount,thumbGuardRightSegmentStraightenPreserveCount,thumbGuardLeftLocalRotationGuardClampCount,thumbGuardRightLocalRotationGuardClampCount,thumbGuardLeftLocalRotationGuardPreserveCount,thumbGuardRightLocalRotationGuardPreserveCount,thumbGuardLeftLocalRotationGuardCurrentRisk,thumbGuardRightLocalRotationGuardCurrentRisk,thumbGuardLeftLocalRotationGuardLimitedRisk,thumbGuardRightLocalRotationGuardLimitedRisk,thumbGuardLeftWorldRotationSuppressCompetingOverride,thumbGuardRightWorldRotationSuppressCompetingOverride,thumbGuardLeftWorldRotationKeepDetachedHelperOverride,thumbGuardRightWorldRotationKeepDetachedHelperOverride,thumbGuardLeftWorldRotationCurrentReferenceFrameDeviation,thumbGuardRightWorldRotationCurrentReferenceFrameDeviation,thumbGuardLeftWorldRotationCandidateReferenceFrameDeviation,thumbGuardRightWorldRotationCandidateReferenceFrameDeviation,thumbGuardLeftProximalWorldRotationPreserveReason,thumbGuardRightProximalWorldRotationPreserveReason,thumbGuardLeftIntermediateWorldRotationPreserveReason,thumbGuardRightIntermediateWorldRotationPreserveReason,thumbGuardLeftProximalWorldRotationCurrentReferenceAngle,thumbGuardRightProximalWorldRotationCurrentReferenceAngle,thumbGuardLeftIntermediateWorldRotationCurrentReferenceAngle,thumbGuardRightIntermediateWorldRotationCurrentReferenceAngle,thumbGuardLeftProximalWorldRotationCandidateReferenceAngle,thumbGuardRightProximalWorldRotationCandidateReferenceAngle,thumbGuardLeftIntermediateWorldRotationCandidateReferenceAngle,thumbGuardRightIntermediateWorldRotationCandidateReferenceAngle,thumbGuardLeftProximalWorldRotationPreserveCurrentRisk,thumbGuardRightProximalWorldRotationPreserveCurrentRisk,thumbGuardLeftIntermediateWorldRotationPreserveCurrentRisk,thumbGuardRightIntermediateWorldRotationPreserveCurrentRisk,thumbGuardLeftProximalWorldRotationPreserveLimitedRisk,thumbGuardRightProximalWorldRotationPreserveLimitedRisk,thumbGuardLeftIntermediateWorldRotationPreserveLimitedRisk,thumbGuardRightIntermediateWorldRotationPreserveLimitedRisk,thumbGuardHelperSyncEnabled,thumbGuardHelperPositionSyncEnabled,thumbGuardHelperSyncWeight,thumbGuardHelperMaxLocalAngle,thumbGuardPalmStabilizeEnabled,thumbGuardPalmStabilizeWeight,thumbGuardPalmStabilizeMaxLocalAngle,thumbGuardWebbingStabilizeEnabled,thumbGuardWebbingStabilizeWeight,thumbGuardWebbingMaxLocalAngle,thumbGuardWebbingMaxPositionOffset";
        File.WriteAllText(path, header + "," + yybDiagnosticHeader + Environment.NewLine, Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        string escaped = value.Replace("\"", "\"\"");
        return escaped.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? $"\"{escaped}\"" : escaped;
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

    private struct AnimationTimeMetrics
    {
        public string Source;
        public string ClipName;
        public float ClipTime;
        public float ClipLength;
        public float NormalizedTime;

        public static AnimationTimeMetrics Empty => new AnimationTimeMetrics
        {
            Source = "",
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
            LastGroundingLowestFootBottomY = float.NaN
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
            DeformationRisk = float.NaN
        };

        public void ClearYybOnlyRiskScores()
        {
            ArmTwistRisk = float.NaN;
            SleeveAnchorRisk = float.NaN;
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
        public float HipsY;
        public float LowestFootY;
        public float LowestFootBottomY;
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
            return string.Join(",",
                Escape(Label),
                Escape(Scene),
                Escape(Reason),
                F(Elapsed),
                F(TimeSinceLevelLoad),
                FrameCount.ToString(CultureInfo.InvariantCulture),
                RecorderFrame.ToString(CultureInfo.InvariantCulture),
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
                RootSpike.RootDeltaSpikeSkippedCount.ToString(CultureInfo.InvariantCulture),
                F(RootSpike.LastRootPositionPoseDeltaMagnitude),
                F(RootSpike.MaxRootPositionPoseDeltaMagnitude),
                RootSpike.RootPositionSpikeClampedCount.ToString(CultureInfo.InvariantCulture),
                F(RootSpike.LastGroundingAdjustment),
                F(RootSpike.MaxGroundingAdjustment),
                RootSpike.GroundingStepClampedCount.ToString(CultureInfo.InvariantCulture),
                RootSpike.GroundingSmoothedCount.ToString(CultureInfo.InvariantCulture),
                F(RootSpike.LastGroundingVerticalStep),
                F(RootSpike.MaxGroundingVerticalStep),
                F(RootSpike.InitialGroundingVerticalStep),
                F(RootSpike.MaxGroundingVerticalStepAfterInitial),
                F(RootSpike.LastGroundingTargetY),
                F(RootSpike.LastGroundingLowestFootBottomY),
                F(HipsY),
                F(LowestFootY),
                F(LowestFootBottomY),
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
                F(LeftShoulderDownUpMuscle),
                F(LeftShoulderFrontBackMuscle),
                F(LeftArmDownUpMuscle),
                F(LeftArmFrontBackMuscle),
                F(LeftArmTwistMuscle),
                F(LeftForearmStretchMuscle),
                F(LeftForearmTwistMuscle),
                F(RightShoulderDownUpMuscle),
                F(RightShoulderFrontBackMuscle),
                F(RightArmDownUpMuscle),
                F(RightArmFrontBackMuscle),
                F(RightArmTwistMuscle),
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
                F(ThumbGuard.WebbingMaxPositionOffset));
        }

        private static string F(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? ""
                : value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static string V(Vector3 value)
        {
            return Escape($"{F(value.x)}|{F(value.y)}|{F(value.z)}");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            string escaped = value.Replace("\"", "\"\"");
            return escaped.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? $"\"{escaped}\"" : escaped;
        }
    }
}
