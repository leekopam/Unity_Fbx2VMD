using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

[DefaultExecutionOrder(29970)]
[DisallowMultipleComponent]
public sealed class VmdPlaybackProbe : MonoBehaviour
{
    public bool PlaybackEnabled = false;

    public bool ApplyIkTargets = false;

    public string MotionFilePath = string.Empty;

    public bool UseCenterAsParentOfAll = false;

    public bool RouteCenterBoneToGroove = false;

    public bool AnchorCarrierPositionsToInitialPose = false;

    public bool LockParentOfAllPosition = false;

    public bool UseExplicitParentOfAllLockPosition = false;

    public Vector3 ParentOfAllLockPosition = Vector3.zero;

    public string CenterNameString = VmdUnityTransformConverter.CenterBoneName;

    public string GrooveNameString = VmdUnityTransformConverter.GrooveBoneName;

    private readonly Dictionary<HumanBodyBones, Transform> _humanoidTargets =
        new Dictionary<HumanBodyBones, Transform>();
    private readonly Dictionary<string, Vector3> _ikSourceWorldPositions =
        new Dictionary<string, Vector3>();

    private Animator _animator;
    private UnityHumanoidVMDRecorder _recorder;
    private Transform _parentOfAllTarget;
    private Transform _centerTarget;
    private VmdMotionData _motion;
    private string _loadedMotionFilePath = string.Empty;
    private int _lastAppliedFrame = -1;
    private VmdPlaybackCarrierReference _carrierReference = VmdPlaybackCarrierReference.Empty;
    private bool _carrierReferenceCaptured = false;

    public void ConfigureRuntimePlayback(
        string motionFilePath,
        bool useCenterAsParentOfAll,
        bool routeCenterBoneToGroove,
        bool applyIkTargets = false)
    {
        MotionFilePath = motionFilePath ?? string.Empty;
        UseCenterAsParentOfAll = useCenterAsParentOfAll;
        RouteCenterBoneToGroove = routeCenterBoneToGroove;
        AnchorCarrierPositionsToInitialPose = true;
        LockParentOfAllPosition = false;
        UseExplicitParentOfAllLockPosition = false;
        ParentOfAllLockPosition = Vector3.zero;
        ApplyIkTargets = applyIkTargets;
        PlaybackEnabled = true;
        _motion = null;
        _loadedMotionFilePath = string.Empty;
        _lastAppliedFrame = -1;
        _carrierReference = VmdPlaybackCarrierReference.Empty;
        _carrierReferenceCaptured = false;
        _ikSourceWorldPositions.Clear();
    }

    public void PrepareForMotionComparisonSample()
    {
        if (!PlaybackEnabled)
        {
            return;
        }

        if (LockParentOfAllPosition && UseExplicitParentOfAllLockPosition)
        {
            transform.localPosition = ParentOfAllLockPosition;
        }
    }

    private void Awake()
    {
        CacheRuntimeTargets();
    }

    private void LateUpdate()
    {
        if (!PlaybackEnabled)
        {
            return;
        }

        if (!EnsureMotionLoaded())
        {
            return;
        }

        CacheRuntimeTargets();
        int frameIndex = _recorder != null ? Mathf.Max(0, _recorder.FrameNumber) : Time.frameCount;
        VmdPlaybackProbeOptions options = new VmdPlaybackProbeOptions(
            enabled: true,
            applyIkTargets: ApplyIkTargets,
            useCenterAsParentOfAll: UseCenterAsParentOfAll,
            routeCenterBoneToGroove: RouteCenterBoneToGroove,
            centerNameString: CenterNameString,
            grooveNameString: GrooveNameString,
            anchorCarrierPositionsToInitialPose: AnchorCarrierPositionsToInitialPose,
            lockParentOfAllPosition: LockParentOfAllPosition,
            useExplicitParentOfAllLockPosition: UseExplicitParentOfAllLockPosition,
            parentOfAllLockPosition: ParentOfAllLockPosition);
        EnsureCarrierReferenceCaptured(options);
        if (frameIndex == _lastAppliedFrame)
        {
            EnforceLockedParentOfAllPosition(options);
            return;
        }

        ApplyFrame(
            _motion,
            (uint)frameIndex,
            _humanoidTargets,
            _parentOfAllTarget,
            _centerTarget,
            options,
            AnchorCarrierPositionsToInitialPose ? _carrierReference : VmdPlaybackCarrierReference.Empty,
            _ikSourceWorldPositions);
        _lastAppliedFrame = frameIndex;
    }

    private void EnforceLockedParentOfAllPosition(VmdPlaybackProbeOptions options)
    {
        if (!options.LockParentOfAllPosition || _parentOfAllTarget == null)
        {
            return;
        }

        if (options.UseExplicitParentOfAllLockPosition)
        {
            _parentOfAllTarget.localPosition = options.ParentOfAllLockPosition;
            return;
        }

        if (_carrierReferenceCaptured && _carrierReference.HasParentOfAll)
        {
            _parentOfAllTarget.localPosition = _carrierReference.ParentOfAllTargetInitialPosition;
        }
    }

    internal static VmdPlaybackApplyResult ApplyFrame(
        VmdMotionData motion,
        uint frameIndex,
        IReadOnlyDictionary<HumanBodyBones, Transform> humanoidTargets,
        Transform parentOfAllTarget,
        Transform centerTarget,
        VmdPlaybackProbeOptions options)
    {
        return ApplyFrame(
            motion,
            frameIndex,
            humanoidTargets,
            parentOfAllTarget,
            centerTarget,
            options,
            VmdPlaybackCarrierReference.Empty);
    }

    internal static VmdPlaybackApplyResult ApplyFrame(
        VmdMotionData motion,
        uint frameIndex,
        IReadOnlyDictionary<HumanBodyBones, Transform> humanoidTargets,
        Transform parentOfAllTarget,
        Transform centerTarget,
        VmdPlaybackProbeOptions options,
        VmdPlaybackCarrierReference carrierReference,
        IReadOnlyDictionary<string, Vector3> ikSourceWorldPositions = null)
    {
        if (!options.Enabled)
        {
            return VmdPlaybackApplyResult.Disabled();
        }

        if (motion == null)
        {
            return VmdPlaybackApplyResult.NoMotion();
        }

        int matchedBoneFrames = 0;
        int appliedCarrierPositions = 0;
        int appliedHumanoidRotations = 0;
        int appliedIkTargetFrames = 0;
        int skippedIkTargetFrames = 0;
        int skippedMorphFrames = CountMorphFrames(motion, frameIndex);
        int unresolvedBoneFrames = 0;

        foreach (VmdBoneFrame frame in motion.BoneFrames)
        {
            if (frame.FrameIndex != frameIndex)
            {
                continue;
            }

            matchedBoneFrames++;
            if (!VmdHumanoidBoneMap.TryResolveWriterBoneName(
                frame.BoneName,
                options.UseCenterAsParentOfAll,
                options.RouteCenterBoneToGroove,
                options.CenterNameString,
                options.GrooveNameString,
                out VmdHumanoidBoneBinding binding))
            {
                unresolvedBoneFrames++;
                continue;
            }

            Vector3 unityPosition = VmdUnityTransformConverter.ConvertVmdPositionToUnityMeters(frame.Position);
            Quaternion unityRotation = VmdUnityTransformConverter.ConvertVmdRotationToUnityRotation(frame.Rotation);

            if (binding.IsIkTarget)
            {
                if (options.ApplyIkTargets &&
                    TryApplyIkTarget(
                        binding.RecorderBoneName,
                        unityPosition,
                        unityRotation,
                        humanoidTargets,
                        parentOfAllTarget,
                        centerTarget,
                        frameIndex,
                        ikSourceWorldPositions))
                {
                    appliedIkTargetFrames++;
                }
                else
                {
                    skippedIkTargetFrames++;
                }

                continue;
            }

            if (TryApplyCarrierPosition(
                    binding.RecorderBoneName,
                    unityPosition,
                    parentOfAllTarget,
                    centerTarget,
                    options,
                    carrierReference))
            {
                appliedCarrierPositions++;
                continue;
            }

            if (binding.HasHumanBodyBone
                && humanoidTargets != null
                && humanoidTargets.TryGetValue(binding.HumanBodyBone, out Transform target)
                && target != null)
            {
                target.localRotation = unityRotation;
                appliedHumanoidRotations++;
            }
        }

        if (matchedBoneFrames == 0)
        {
            return VmdPlaybackApplyResult.NoFrame(skippedMorphFrames);
        }

        return VmdPlaybackApplyResult.Applied(
            appliedCarrierPositions,
            appliedHumanoidRotations,
            appliedIkTargetFrames,
            skippedIkTargetFrames,
            skippedMorphFrames,
            unresolvedBoneFrames);
    }

    internal static VmdPlaybackCarrierReference CaptureCarrierReference(
        VmdMotionData motion,
        uint frameIndex,
        Transform parentOfAllTarget,
        Transform centerTarget,
        VmdPlaybackProbeOptions options)
    {
        if (motion == null)
        {
            return VmdPlaybackCarrierReference.Empty;
        }

        bool hasParentOfAll = false;
        bool hasCenter = false;
        Vector3 parentOfAllSourcePosition = Vector3.zero;
        Vector3 parentOfAllTargetPosition = Vector3.zero;
        Vector3 centerSourcePosition = Vector3.zero;
        Vector3 centerTargetPosition = Vector3.zero;

        foreach (VmdBoneFrame frame in motion.BoneFrames)
        {
            if (frame.FrameIndex != frameIndex)
            {
                continue;
            }

            if (!VmdHumanoidBoneMap.TryResolveWriterBoneName(
                frame.BoneName,
                options.UseCenterAsParentOfAll,
                options.RouteCenterBoneToGroove,
                options.CenterNameString,
                options.GrooveNameString,
                out VmdHumanoidBoneBinding binding))
            {
                continue;
            }

            Vector3 unityPosition = VmdUnityTransformConverter.ConvertVmdPositionToUnityMeters(frame.Position);
            int ordinal = (int)binding.RecorderBoneName;
            if (ordinal == 0 && parentOfAllTarget != null)
            {
                parentOfAllSourcePosition = unityPosition;
                parentOfAllTargetPosition = parentOfAllTarget.localPosition;
                hasParentOfAll = true;
                continue;
            }

            if (ordinal == 1 && centerTarget != null)
            {
                centerSourcePosition = unityPosition;
                centerTargetPosition = centerTarget.localPosition;
                hasCenter = true;
            }
        }

        return new VmdPlaybackCarrierReference(
            hasParentOfAll,
            parentOfAllSourcePosition,
            parentOfAllTargetPosition,
            hasCenter,
            centerSourcePosition,
            centerTargetPosition);
    }

    private void CacheRuntimeTargets()
    {
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }

        if (_recorder == null)
        {
            _recorder = GetComponent<UnityHumanoidVMDRecorder>();
        }

        _parentOfAllTarget = transform;
        _centerTarget = _animator != null
            ? _animator.GetBoneTransform(HumanBodyBones.Hips)
            : null;

        if (_humanoidTargets.Count > 0 || _animator == null || !_animator.isHuman)
        {
            return;
        }

        for (HumanBodyBones bone = 0; bone < HumanBodyBones.LastBone; bone++)
        {
            Transform target = _animator.GetBoneTransform(bone);
            if (target != null)
            {
                _humanoidTargets[bone] = target;
            }
        }
    }

    private void EnsureCarrierReferenceCaptured(VmdPlaybackProbeOptions options)
    {
        if (!options.AnchorCarrierPositionsToInitialPose || _carrierReferenceCaptured)
        {
            return;
        }

        _carrierReference = CaptureCarrierReference(
            _motion,
            frameIndex: 0,
            parentOfAllTarget: _parentOfAllTarget,
            centerTarget: _centerTarget,
            options: options);
        _carrierReferenceCaptured = true;
    }

    private bool EnsureMotionLoaded()
    {
        if (_motion != null && _loadedMotionFilePath == MotionFilePath)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(MotionFilePath) || !File.Exists(MotionFilePath))
        {
            Debug.LogWarning($"[VmdPlaybackProbe] Motion file is not available: {MotionFilePath}");
            return false;
        }

        try
        {
            _motion = VmdMotionReader.Read(MotionFilePath);
            _loadedMotionFilePath = MotionFilePath;
            _ikSourceWorldPositions.Clear();
            LoadIkSourceWorldPositions(MotionFilePath, _ikSourceWorldPositions);
            _carrierReference = VmdPlaybackCarrierReference.Empty;
            _carrierReferenceCaptured = false;
            return true;
        }
        catch (System.Exception ex)
        {
            PlaybackEnabled = false;
            Debug.LogError($"[VmdPlaybackProbe] Motion read failed: {MotionFilePath} / {ex.Message}");
            return false;
        }
    }

    private static bool TryApplyCarrierPosition(
        BoneNames recorderBoneName,
        Vector3 unityPosition,
        Transform parentOfAllTarget,
        Transform centerTarget)
    {
        return TryApplyCarrierPosition(
            recorderBoneName,
            unityPosition,
            parentOfAllTarget,
            centerTarget,
            VmdPlaybackProbeOptions.DefaultEnabled,
            VmdPlaybackCarrierReference.Empty);
    }

    private static bool TryApplyCarrierPosition(
        BoneNames recorderBoneName,
        Vector3 unityPosition,
        Transform parentOfAllTarget,
        Transform centerTarget,
        VmdPlaybackProbeOptions options,
        VmdPlaybackCarrierReference carrierReference)
    {
        int ordinal = (int)recorderBoneName;
        if (ordinal == 0 && parentOfAllTarget != null)
        {
            if (options.LockParentOfAllPosition)
            {
                parentOfAllTarget.localPosition = options.UseExplicitParentOfAllLockPosition
                    ? options.ParentOfAllLockPosition
                    : carrierReference.HasParentOfAll
                        ? carrierReference.ParentOfAllTargetInitialPosition
                        : parentOfAllTarget.localPosition;
            }
            else
            {
                parentOfAllTarget.localPosition =
                    carrierReference.TryResolve(recorderBoneName, unityPosition, out Vector3 targetPosition)
                        ? targetPosition
                        : unityPosition;
            }

            return true;
        }

        if (ordinal == 1 && centerTarget != null)
        {
            centerTarget.localPosition = carrierReference.TryResolve(recorderBoneName, unityPosition, out Vector3 targetPosition)
                ? targetPosition
                : unityPosition;
            return true;
        }

        return false;
    }

    private static bool TryApplyIkTarget(
        BoneNames recorderBoneName,
        Vector3 unityPosition,
        Quaternion unityRotation,
        IReadOnlyDictionary<HumanBodyBones, Transform> humanoidTargets,
        Transform parentOfAllTarget,
        Transform centerTarget,
        uint frameIndex,
        IReadOnlyDictionary<string, Vector3> ikSourceWorldPositions)
    {
        if (humanoidTargets == null)
        {
            return false;
        }

        if (!TryResolveIkTargetBone(recorderBoneName, out HumanBodyBones targetBone))
        {
            return false;
        }

        if (!humanoidTargets.TryGetValue(targetBone, out Transform target) || target == null)
        {
            return false;
        }

        target.position = ResolveIkTargetWorldPosition(
            recorderBoneName,
            unityPosition,
            humanoidTargets,
            parentOfAllTarget,
            centerTarget,
            frameIndex,
            ikSourceWorldPositions);
        target.localRotation = unityRotation;
        return true;
    }

    private static Vector3 ResolveIkTargetWorldPosition(
        BoneNames recorderBoneName,
        Vector3 unityPosition,
        IReadOnlyDictionary<HumanBodyBones, Transform> humanoidTargets,
        Transform parentOfAllTarget,
        Transform centerTarget,
        uint frameIndex,
        IReadOnlyDictionary<string, Vector3> ikSourceWorldPositions)
    {
        int ordinal = (int)recorderBoneName;
        if ((ordinal == 2 || ordinal == 3) &&
            TryGetIkSourceWorldPosition(
                ikSourceWorldPositions,
                frameIndex,
                recorderBoneName,
                out Vector3 sourceWorldPosition))
        {
            return sourceWorldPosition;
        }

        if (ordinal == 4 || ordinal == 5)
        {
            HumanBodyBones parentFootBone = ordinal == 4
                ? HumanBodyBones.LeftFoot
                : HumanBodyBones.RightFoot;
            if (humanoidTargets != null &&
                humanoidTargets.TryGetValue(parentFootBone, out Transform parentFoot) &&
                parentFoot != null)
            {
                Vector3 toeOffset = parentOfAllTarget != null
                    ? parentOfAllTarget.TransformVector(unityPosition)
                    : unityPosition;
                return parentFoot.position + toeOffset;
            }
        }

        return parentOfAllTarget != null
            ? parentOfAllTarget.TransformPoint(unityPosition)
            : unityPosition;
    }

    internal static void LoadIkSourceWorldPositions(
        string motionFilePath,
        IDictionary<string, Vector3> target)
    {
        if (target == null || string.IsNullOrWhiteSpace(motionFilePath))
        {
            return;
        }

        string diagnosticsPath = Path.ChangeExtension(motionFilePath, ".export_ik_source_samples.csv");
        if (!File.Exists(diagnosticsPath))
        {
            return;
        }

        string[] lines = File.ReadAllLines(diagnosticsPath);
        if (lines.Length < 2)
        {
            return;
        }

        string[] headers = lines[0].Split(',');
        int recorderFrameIndex = FindColumn(headers, "recorderFrame");
        int boneIndexIndex = FindColumn(headers, "boneIndex");
        int sourceWorldPositionIndex = FindColumn(headers, "sourceWorldPosition");
        if (recorderFrameIndex < 0 || boneIndexIndex < 0 || sourceWorldPositionIndex < 0)
        {
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] values = line.Split(',');
            if (values.Length <= sourceWorldPositionIndex ||
                !uint.TryParse(values[recorderFrameIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint frameIndex) ||
                !int.TryParse(values[boneIndexIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int boneIndex) ||
                (boneIndex != 2 && boneIndex != 3) ||
                !TryParsePipeVector(values[sourceWorldPositionIndex], out Vector3 sourceWorldPosition))
            {
                continue;
            }

            target[BuildIkSourceWorldPositionKey(frameIndex, boneIndex)] = sourceWorldPosition;
        }
    }

    private static bool TryGetIkSourceWorldPosition(
        IReadOnlyDictionary<string, Vector3> sourceWorldPositions,
        uint frameIndex,
        BoneNames recorderBoneName,
        out Vector3 sourceWorldPosition)
    {
        sourceWorldPosition = Vector3.zero;
        if (sourceWorldPositions == null)
        {
            return false;
        }

        return sourceWorldPositions.TryGetValue(
            BuildIkSourceWorldPositionKey(frameIndex, (int)recorderBoneName),
            out sourceWorldPosition);
    }

    private static string BuildIkSourceWorldPositionKey(uint frameIndex, int boneIndex)
    {
        return frameIndex.ToString(CultureInfo.InvariantCulture) + "|" + boneIndex.ToString(CultureInfo.InvariantCulture);
    }

    private static int FindColumn(string[] headers, string columnName)
    {
        if (headers == null)
        {
            return -1;
        }

        for (int i = 0; i < headers.Length; i++)
        {
            if (string.Equals(headers[i], columnName, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParsePipeVector(string value, out Vector3 vector)
    {
        vector = Vector3.zero;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string[] components = value.Split('|');
        if (components.Length != 3 ||
            !float.TryParse(components[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(components[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
            !float.TryParse(components[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            return false;
        }

        vector = new Vector3(x, y, z);
        return true;
    }

    private static bool TryResolveIkTargetBone(BoneNames recorderBoneName, out HumanBodyBones targetBone)
    {
        switch ((int)recorderBoneName)
        {
            case 2:
                targetBone = HumanBodyBones.LeftFoot;
                return true;
            case 3:
                targetBone = HumanBodyBones.RightFoot;
                return true;
            case 4:
                targetBone = HumanBodyBones.LeftToes;
                return true;
            case 5:
                targetBone = HumanBodyBones.RightToes;
                return true;
            default:
                targetBone = HumanBodyBones.LastBone;
                return false;
        }
    }

    private static int CountMorphFrames(VmdMotionData motion, uint frameIndex)
    {
        int count = 0;
        foreach (VmdMorphFrame morphFrame in motion.MorphFrames)
        {
            if (morphFrame.FrameIndex == frameIndex)
            {
                count++;
            }
        }

        return count;
    }
}

internal enum VmdPlaybackApplyStatus
{
    Disabled,
    NoMotion,
    NoFrame,
    Applied
}

internal readonly struct VmdPlaybackCarrierReference
{
    internal VmdPlaybackCarrierReference(
        bool hasParentOfAll,
        Vector3 parentOfAllSourcePosition,
        Vector3 parentOfAllTargetInitialPosition,
        bool hasCenter,
        Vector3 centerSourcePosition,
        Vector3 centerTargetInitialPosition)
    {
        HasParentOfAll = hasParentOfAll;
        ParentOfAllSourcePosition = parentOfAllSourcePosition;
        ParentOfAllTargetInitialPosition = parentOfAllTargetInitialPosition;
        HasCenter = hasCenter;
        CenterSourcePosition = centerSourcePosition;
        CenterTargetInitialPosition = centerTargetInitialPosition;
    }

    internal static VmdPlaybackCarrierReference Empty =>
        new VmdPlaybackCarrierReference(
            hasParentOfAll: false,
            parentOfAllSourcePosition: Vector3.zero,
            parentOfAllTargetInitialPosition: Vector3.zero,
            hasCenter: false,
            centerSourcePosition: Vector3.zero,
            centerTargetInitialPosition: Vector3.zero);

    internal bool HasParentOfAll { get; }

    internal Vector3 ParentOfAllSourcePosition { get; }

    internal Vector3 ParentOfAllTargetInitialPosition { get; }

    internal bool HasCenter { get; }

    internal Vector3 CenterSourcePosition { get; }

    internal Vector3 CenterTargetInitialPosition { get; }

    internal bool TryResolve(BoneNames recorderBoneName, Vector3 sourcePosition, out Vector3 targetPosition)
    {
        int ordinal = (int)recorderBoneName;
        if (ordinal == 0 && HasParentOfAll)
        {
            targetPosition = ParentOfAllTargetInitialPosition + (sourcePosition - ParentOfAllSourcePosition);
            return true;
        }

        if (ordinal == 1 && HasCenter)
        {
            targetPosition = CenterTargetInitialPosition + (sourcePosition - CenterSourcePosition);
            return true;
        }

        targetPosition = sourcePosition;
        return false;
    }
}

internal readonly struct VmdPlaybackProbeOptions
{
    internal VmdPlaybackProbeOptions(
        bool enabled,
        bool applyIkTargets,
        bool useCenterAsParentOfAll,
        bool routeCenterBoneToGroove,
        string centerNameString,
        string grooveNameString,
        bool anchorCarrierPositionsToInitialPose = false,
        bool lockParentOfAllPosition = false,
        bool useExplicitParentOfAllLockPosition = false,
        Vector3 parentOfAllLockPosition = default)
    {
        Enabled = enabled;
        ApplyIkTargets = applyIkTargets;
        UseCenterAsParentOfAll = useCenterAsParentOfAll;
        RouteCenterBoneToGroove = routeCenterBoneToGroove;
        CenterNameString = string.IsNullOrEmpty(centerNameString)
            ? VmdUnityTransformConverter.CenterBoneName
            : centerNameString;
        GrooveNameString = string.IsNullOrEmpty(grooveNameString)
            ? VmdUnityTransformConverter.GrooveBoneName
            : grooveNameString;
        AnchorCarrierPositionsToInitialPose = anchorCarrierPositionsToInitialPose;
        LockParentOfAllPosition = lockParentOfAllPosition;
        UseExplicitParentOfAllLockPosition = useExplicitParentOfAllLockPosition;
        ParentOfAllLockPosition = parentOfAllLockPosition;
    }

    internal static VmdPlaybackProbeOptions Disabled =>
        new VmdPlaybackProbeOptions(
            enabled: false,
            applyIkTargets: false,
            useCenterAsParentOfAll: false,
            routeCenterBoneToGroove: false,
            centerNameString: VmdUnityTransformConverter.CenterBoneName,
            grooveNameString: VmdUnityTransformConverter.GrooveBoneName);

    internal static VmdPlaybackProbeOptions DefaultEnabled =>
        new VmdPlaybackProbeOptions(
            enabled: true,
            applyIkTargets: false,
            useCenterAsParentOfAll: false,
            routeCenterBoneToGroove: false,
            centerNameString: VmdUnityTransformConverter.CenterBoneName,
            grooveNameString: VmdUnityTransformConverter.GrooveBoneName);

    internal bool Enabled { get; }

    internal bool ApplyIkTargets { get; }

    internal bool UseCenterAsParentOfAll { get; }

    internal bool RouteCenterBoneToGroove { get; }

    internal string CenterNameString { get; }

    internal string GrooveNameString { get; }

    internal bool AnchorCarrierPositionsToInitialPose { get; }

    internal bool LockParentOfAllPosition { get; }

    internal bool UseExplicitParentOfAllLockPosition { get; }

    internal Vector3 ParentOfAllLockPosition { get; }
}

internal readonly struct VmdPlaybackApplyResult
{
    private VmdPlaybackApplyResult(
        VmdPlaybackApplyStatus status,
        int appliedCarrierPositions,
        int appliedHumanoidRotations,
        int appliedIkTargetFrames,
        int skippedIkTargetFrames,
        int skippedMorphFrames,
        int unresolvedBoneFrames)
    {
        Status = status;
        AppliedCarrierPositions = appliedCarrierPositions;
        AppliedHumanoidRotations = appliedHumanoidRotations;
        AppliedIkTargetFrames = appliedIkTargetFrames;
        SkippedIkTargetFrames = skippedIkTargetFrames;
        SkippedMorphFrames = skippedMorphFrames;
        UnresolvedBoneFrames = unresolvedBoneFrames;
    }

    internal VmdPlaybackApplyStatus Status { get; }

    internal int AppliedCarrierPositions { get; }

    internal int AppliedHumanoidRotations { get; }

    internal int AppliedIkTargetFrames { get; }

    internal int SkippedIkTargetFrames { get; }

    internal int SkippedMorphFrames { get; }

    internal int UnresolvedBoneFrames { get; }

    internal static VmdPlaybackApplyResult Disabled()
    {
        return new VmdPlaybackApplyResult(
            VmdPlaybackApplyStatus.Disabled,
            appliedCarrierPositions: 0,
            appliedHumanoidRotations: 0,
            appliedIkTargetFrames: 0,
            skippedIkTargetFrames: 0,
            skippedMorphFrames: 0,
            unresolvedBoneFrames: 0);
    }

    internal static VmdPlaybackApplyResult NoMotion()
    {
        return new VmdPlaybackApplyResult(
            VmdPlaybackApplyStatus.NoMotion,
            appliedCarrierPositions: 0,
            appliedHumanoidRotations: 0,
            appliedIkTargetFrames: 0,
            skippedIkTargetFrames: 0,
            skippedMorphFrames: 0,
            unresolvedBoneFrames: 0);
    }

    internal static VmdPlaybackApplyResult NoFrame(int skippedMorphFrames)
    {
        return new VmdPlaybackApplyResult(
            VmdPlaybackApplyStatus.NoFrame,
            appliedCarrierPositions: 0,
            appliedHumanoidRotations: 0,
            appliedIkTargetFrames: 0,
            skippedIkTargetFrames: 0,
            skippedMorphFrames: skippedMorphFrames,
            unresolvedBoneFrames: 0);
    }

    internal static VmdPlaybackApplyResult Applied(
        int appliedCarrierPositions,
        int appliedHumanoidRotations,
        int appliedIkTargetFrames,
        int skippedIkTargetFrames,
        int skippedMorphFrames,
        int unresolvedBoneFrames)
    {
        return new VmdPlaybackApplyResult(
            VmdPlaybackApplyStatus.Applied,
            appliedCarrierPositions,
            appliedHumanoidRotations,
            appliedIkTargetFrames,
            skippedIkTargetFrames,
            skippedMorphFrames,
            unresolvedBoneFrames);
    }
}
