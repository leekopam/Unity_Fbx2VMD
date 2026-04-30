using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(30000)]
public class MotionComparisonProbe : MonoBehaviour
{
    [SerializeField] private string comparisonLabel = "";
    [SerializeField] private float[] sampleTimes = { 0f, 3f, 10f, 30f, 60f, 120f };
    [SerializeField] private bool sampleByAnimationClipTime = true;
    [SerializeField] private bool logSamples = true;
    [SerializeField] private bool captureSampleScreenshots = true;
    [SerializeField] private bool captureFingerCloseups = true;
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

    public string LastCsvPath => _csvPath;
    public string LastScreenshotFolder => _screenshotFolder;
    public string LastSessionManifestPath => _sessionManifestPath;
    public bool IsSampling => _isSampling;

    public void SetFingerCloseups(bool enabled) => captureFingerCloseups = enabled;

    public void StartSampling(string labelOverride = "")
    {
        _animator = GetComponent<Animator>();
        _recorder = GetComponent<UnityHumanoidVMDRecorder>();
        _camera = Camera.main;

        if (_animator == null)
        {
            Debug.LogWarning("[MotionComparisonProbe] Animator가 없어 비교 샘플링을 시작하지 못했습니다.");
            return;
        }

        PrepareHumanPoseCapture();

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
        File.AppendAllText(_csvPath, metrics.ToCsvLine() + Environment.NewLine, Encoding.UTF8);
        CaptureSampleScreenshots(reason, metrics);
        WriteSessionManifest(reason);

        if (logSamples)
        {
            Debug.Log($"[MotionComparisonProbe] {comparisonLabel} {reason} t={metrics.Elapsed:F2}s clip={metrics.AnimationClipTime:F3}s frame={metrics.RecorderFrame} hipsY={metrics.HipsY:F3} facing={metrics.CameraFacingDot:F3} scaleDelta={metrics.MaxScaleDelta:F4}");
        }
    }

    private void LateUpdate()
    {
        if (!_isSampling || sampleTimes == null || sampleTimes.Length == 0)
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
            HipsY = hips != null ? hips.position.y : float.NaN,
            LowestFootY = lowestFootY,
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
            RightLittleSpreadMuscle = fingers.RightLittleSpread
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
        AnimationState state = legacyAnimation[clip.name];
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

    private static void WriteHeader(string path)
    {
        const string header = "label,scene,reason,elapsed,timeSinceLevelLoad,frameCount,recorderFrame,animationTimeSource,animationClipName,animationClipTime,animationClipLength,animationNormalizedTime,rootX,rootY,rootZ,rootYaw,hipsY,lowestFootY,cameraFacingDot,maxScaleDelta,leftUpperArmScale,rightUpperArmScale,leftUpperLegScale,rightUpperLegScale,leftArmLength,rightArmLength,leftLegLength,rightLegLength,leftElbowAngle,rightElbowAngle,leftKneeAngle,rightKneeAngle,leftElbowBendForward,rightElbowBendForward,leftKneeBendForward,rightKneeBendForward,leftElbowBendOffsetForward,rightElbowBendOffsetForward,leftKneeBendOffsetForward,rightKneeBendOffsetForward,leftUpperArmDownDot,rightUpperArmDownDot,leftHandHorizontalRatio,rightHandHorizontalRatio,leftHandBelowShoulderRatio,rightHandBelowShoulderRatio,leftShoulderDownUpMuscle,leftShoulderFrontBackMuscle,leftArmDownUpMuscle,leftArmFrontBackMuscle,leftArmTwistMuscle,leftForearmStretchMuscle,leftForearmTwistMuscle,rightShoulderDownUpMuscle,rightShoulderFrontBackMuscle,rightArmDownUpMuscle,rightArmFrontBackMuscle,rightArmTwistMuscle,rightForearmStretchMuscle,rightForearmTwistMuscle,leftThumb1StretchMuscle,leftThumbSpreadMuscle,leftIndex1StretchMuscle,leftIndexSpreadMuscle,leftMiddle1StretchMuscle,leftMiddleSpreadMuscle,leftRing1StretchMuscle,leftRingSpreadMuscle,leftLittle1StretchMuscle,leftLittleSpreadMuscle,rightThumb1StretchMuscle,rightThumbSpreadMuscle,rightIndex1StretchMuscle,rightIndexSpreadMuscle,rightMiddle1StretchMuscle,rightMiddleSpreadMuscle,rightRing1StretchMuscle,rightRingSpreadMuscle,rightLittle1StretchMuscle,rightLittleSpreadMuscle,spineLocalEuler,chestLocalEuler,upperChestLocalEuler,leftShoulderLocalEuler,rightShoulderLocalEuler,leftUpperArmLocalEuler,rightUpperArmLocalEuler,leftLowerArmLocalEuler,rightLowerArmLocalEuler,leftHandLocalEuler,rightHandLocalEuler,leftThumbProximalLocalEuler,leftIndexProximalLocalEuler,leftMiddleProximalLocalEuler,leftRingProximalLocalEuler,leftLittleProximalLocalEuler,rightThumbProximalLocalEuler,rightIndexProximalLocalEuler,rightMiddleProximalLocalEuler,rightRingProximalLocalEuler,rightLittleProximalLocalEuler";
        File.WriteAllText(path, header + Environment.NewLine, Encoding.UTF8);
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
        public float HipsY;
        public float LowestFootY;
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
                F(HipsY),
                F(LowestFootY),
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
                V(RightLittleProximalLocalEuler));
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
