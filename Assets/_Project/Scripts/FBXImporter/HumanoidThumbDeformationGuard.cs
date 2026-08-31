using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Fbx2Vmd.FBXImporter
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(29900)]
    public class HumanoidThumbDeformationGuard : MonoBehaviour
    {
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private PoseSpaceRetargeter linkedPoseSpaceRetargeter;
        [SerializeField, Range(0f, 90f)] private float proximalMaxLocalAngle = 28f;
        [SerializeField, Range(0f, 120f)] private float intermediateMaxLocalAngle = 55f;
        [SerializeField, Range(0f, 120f)] private float distalMaxLocalAngle = 55f;
        [SerializeField] private Vector3 proximalLocalRotationOffset;
        [SerializeField] private bool mirrorRightProximalLocalRotationOffset = true;
        [SerializeField] private Vector3 leftProximalLocalRotationOffset;
        [SerializeField] private Vector3 rightProximalLocalRotationOffset;
        [SerializeField] private bool logCorrections;
        [SerializeField] private bool clampHumanoidThumbRotations = true;
        [SerializeField] private bool syncDetachedThumbBaseHelpers = true;
        [SerializeField] private bool syncDetachedThumbBaseHelperPositions = true;
        [SerializeField, Range(0f, 1f)] private float detachedThumbBaseHelperSyncWeight = 0.8f;
        [SerializeField, Range(0f, 45f)] private float detachedThumbBaseHelperMaxLocalAngle = 28f;
        [SerializeField, Range(0f, 0.02f)] private float detachedThumbBaseHelperMaxPositionOffset = 0.008f;
        [SerializeField] private Vector3 leftDetachedThumbBaseHelperDeltaAxisOffset;
        [SerializeField] private Vector3 rightDetachedThumbBaseHelperDeltaAxisOffset;
        [SerializeField] private Vector3 leftDetachedThumbBaseHelperTargetRotationOffset;
        [SerializeField] private Vector3 rightDetachedThumbBaseHelperTargetRotationOffset;
        [SerializeField] private bool stabilizeDetachedThumbBasePalm = false;
        [SerializeField, Range(0f, 1f)] private float detachedThumbBasePalmStabilizeWeight = 0f;
        [SerializeField, Range(0f, 45f)] private float detachedThumbBasePalmMaxLocalAngle = 45f;
        [SerializeField] private bool stabilizeThumbWebbingCrease = true;
        [SerializeField, Range(0f, 1f)] private float thumbWebbingCreaseStabilizeWeight = 0.35f;
        [SerializeField, Range(0f, 45f)] private float thumbWebbingCreaseMaxLocalAngle = 18f;
        [SerializeField, Range(0f, 0.02f)] private float thumbWebbingCreaseMaxPositionOffset = 0.005f;
        [SerializeField] private bool enableThumbVisualLengthGuard = true;
        [SerializeField, Range(0f, 1f)] private float thumbProjectionMinPalmNormal = 0.358f;
        [SerializeField, Range(0f, 1f)] private float thumbProjectionMaxPalmNormal = 0.58f;
        [SerializeField, Range(0f, 1f)] private float thumbProjectionGuardWeight = 1f;
        [SerializeField, Range(0f, 90f)] private float thumbIndexMaxSpreadAngle = 70f;
        [SerializeField, Range(0f, 1f)] private float thumbIndexSpreadGuardWeight = 1f;
        [SerializeField, Range(0f, 60f)] private float thumbMaxSegmentBendAngle = 10f;
        [SerializeField, Range(0f, 1f)] private float thumbSegmentStraightenWeight = 0.9f;
        [SerializeField] private bool suppressPoseShapingWithManualThumbReference = false;

        private readonly Dictionary<Transform, Quaternion> _initialLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Vector3> _initialLocalPositions = new Dictionary<Transform, Vector3>();
        private readonly HashSet<Transform> _thumbBaseHelperTransforms = new HashSet<Transform>();
        private readonly Dictionary<Transform, Transform> _detachedThumbBaseHelperSources = new Dictionary<Transform, Transform>();
        private readonly Dictionary<Transform, Quaternion> _detachedThumbBaseSourceInitialLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> _detachedThumbBaseInitialRelativeRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, float> _detachedThumbBaseInitialDistances = new Dictionary<Transform, float>();
        private readonly Dictionary<Transform, Quaternion> _lastRawLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, Quaternion> _lastCorrectedLocalRotations = new Dictionary<Transform, Quaternion>();
        private readonly Dictionary<Transform, bool> _cachedThumbSides = new Dictionary<Transform, bool>();
        private bool _warningLogged;
        private const float ThumbWebbingSpreadFullRiskAngle = 72f;
        private const float ThumbWebbingProjectionFullRiskDistance = 1f;
        private const float ThumbWebbingHelperDistanceWarning = 0.003f;
        private const float ThumbWebbingHelperDistanceFullRisk = 0.008f;
        private const float ThumbWebbingHelperRotationWarning = 18f;
        private const float ThumbWebbingHelperRotationFullRisk = 45f;
        private const float ThumbWebbingDynamicMinLocalAngle = 4f;
        private const float ThumbWebbingDynamicMinPositionOffset = 0.0015f;
        private const float ManualThumbReferenceMinProximalMaxLocalAngle = 28f;
        private const float ManualThumbProjectionPreserveSmokeRiskLimit = 0.35f;

        private static readonly HumanBodyBones[] ThumbBones =
        {
            HumanBodyBones.LeftThumbProximal,
            HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate,
            HumanBodyBones.RightThumbDistal
        };

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
        }

        private void LateUpdate()
        {
            if (!InitializeIfNeeded())
            {
                return;
            }

            int changed = 0;
            if (clampHumanoidThumbRotations)
            {
                foreach (HumanBodyBones thumbBone in ThumbBones)
                {
                    Transform thumbTransform = targetAnimator.GetBoneTransform(thumbBone);
                    if (thumbTransform == null ||
                        !_initialLocalRotations.TryGetValue(thumbTransform, out Quaternion initialRotation))
                    {
                        continue;
                    }

                    float limit = GetLimit(thumbBone);
                    Quaternion rawRotation = GetCurrentRawLocalRotation(thumbTransform);
                    if (!IsFinite(rawRotation))
                    {
                        SetCorrectedLocalRotation(thumbTransform, initialRotation, initialRotation);
                        changed++;
                        continue;
                    }

                    Quaternion offsetRotation = GetThumbRotationOffset(thumbBone);
                    if (!ThumbLocalRotationCalculator.TryCalculateCorrection(
                            initialRotation,
                            rawRotation,
                            thumbTransform.localRotation,
                            offsetRotation,
                            limit,
                            out Quaternion correctedRotation))
                    {
                        continue;
                    }

                    SetCorrectedLocalRotation(thumbTransform, rawRotation, correctedRotation);
                    changed++;
                }

                changed += ClampThumbBaseHelperTransforms();
            }

            if (enableThumbVisualLengthGuard)
            {
                changed += PreserveThumbVisualLength();
            }

            changed += SyncDetachedThumbBaseHelperTransforms();

            if (changed > 0 && logCorrections && !_warningLogged)
            {
                Debug.LogWarning($"[HumanoidThumbDeformationGuard] 엄지 본 localRotation {changed}개를 최종 렌더 포즈에서 제한했습니다.");
                _warningLogged = true;
            }
        }

        public void Configure(
            Animator animator,
            PoseSpaceRetargeter poseSpaceRetargeter,
            float proximalLimit,
            float intermediateLimit,
            float distalLimit,
            Vector3 proximalOffset,
            bool mirrorRightOffset,
            Vector3 leftProximalOffset,
            Vector3 rightProximalOffset,
            bool logCorrectionMessages,
            bool clampHumanoidRotations = true,
            bool syncDetachedBaseHelpers = true,
            bool syncDetachedBaseHelperPositions = true,
            float detachedBaseHelperSyncWeight = 1f,
            float detachedBaseHelperMaxLocalAngle = 45f,
            float detachedBaseHelperMaxPositionOffset = 0.008f,
            Vector3 leftDetachedBaseHelperDeltaAxisOffset = default,
            Vector3 rightDetachedBaseHelperDeltaAxisOffset = default,
            Vector3 leftDetachedBaseHelperTargetRotationOffset = default,
            Vector3 rightDetachedBaseHelperTargetRotationOffset = default,
            bool stabilizeDetachedBasePalm = false,
            float detachedBasePalmStabilizeWeight = 0f,
            float detachedBasePalmMaxLocalAngle = 45f,
            bool enableVisualLengthGuard = true,
            float projectionMinPalmNormal = 0.32f,
            float projectionMaxPalmNormal = 0.58f,
            float projectionGuardWeight = 0.9f,
            float indexMaxSpreadAngle = 70f,
            float indexSpreadGuardWeight = 0.9f,
            float maxSegmentBendAngle = 10f,
            float segmentStraightenWeight = 0.9f,
            bool suppressPoseShapingWithManualReference = false,
            bool stabilizeWebbingCrease = true,
            float webbingCreaseStabilizeWeight = 0.35f,
            float webbingCreaseMaxLocalAngle = 18f,
            float webbingCreaseMaxPositionOffset = 0.005f)
        {
            targetAnimator = animator;
            linkedPoseSpaceRetargeter = poseSpaceRetargeter;
            proximalMaxLocalAngle = Mathf.Clamp(
                suppressPoseShapingWithManualReference
                    ? Mathf.Max(proximalLimit, ManualThumbReferenceMinProximalMaxLocalAngle)
                    : proximalLimit,
                0f,
                90f);
            intermediateMaxLocalAngle = Mathf.Clamp(intermediateLimit, 0f, 120f);
            distalMaxLocalAngle = Mathf.Clamp(distalLimit, 0f, 120f);
            proximalLocalRotationOffset = proximalOffset;
            mirrorRightProximalLocalRotationOffset = mirrorRightOffset;
            leftProximalLocalRotationOffset = leftProximalOffset;
            rightProximalLocalRotationOffset = rightProximalOffset;
            logCorrections = logCorrectionMessages;
            clampHumanoidThumbRotations = clampHumanoidRotations;
            syncDetachedThumbBaseHelpers = syncDetachedBaseHelpers;
            syncDetachedThumbBaseHelperPositions = syncDetachedBaseHelperPositions;
            detachedThumbBaseHelperSyncWeight = Mathf.Clamp01(detachedBaseHelperSyncWeight);
            detachedThumbBaseHelperMaxLocalAngle = Mathf.Clamp(detachedBaseHelperMaxLocalAngle, 0f, 45f);
            detachedThumbBaseHelperMaxPositionOffset = Mathf.Clamp(detachedBaseHelperMaxPositionOffset, 0f, 0.02f);
            leftDetachedThumbBaseHelperDeltaAxisOffset = leftDetachedBaseHelperDeltaAxisOffset;
            rightDetachedThumbBaseHelperDeltaAxisOffset = rightDetachedBaseHelperDeltaAxisOffset;
            leftDetachedThumbBaseHelperTargetRotationOffset = leftDetachedBaseHelperTargetRotationOffset;
            rightDetachedThumbBaseHelperTargetRotationOffset = rightDetachedBaseHelperTargetRotationOffset;
            stabilizeDetachedThumbBasePalm = stabilizeDetachedBasePalm;
            detachedThumbBasePalmStabilizeWeight = Mathf.Clamp01(detachedBasePalmStabilizeWeight);
            detachedThumbBasePalmMaxLocalAngle = Mathf.Clamp(detachedBasePalmMaxLocalAngle, 0f, 45f);
            stabilizeThumbWebbingCrease = stabilizeWebbingCrease;
            thumbWebbingCreaseStabilizeWeight = Mathf.Clamp01(webbingCreaseStabilizeWeight);
            thumbWebbingCreaseMaxLocalAngle = Mathf.Clamp(webbingCreaseMaxLocalAngle, 0f, 45f);
            thumbWebbingCreaseMaxPositionOffset = Mathf.Clamp(webbingCreaseMaxPositionOffset, 0f, 0.02f);
            enableThumbVisualLengthGuard = enableVisualLengthGuard;
            thumbProjectionMinPalmNormal = Mathf.Clamp01(projectionMinPalmNormal);
            thumbProjectionMaxPalmNormal = Mathf.Clamp01(projectionMaxPalmNormal);
            thumbProjectionGuardWeight = Mathf.Clamp01(projectionGuardWeight);
            thumbIndexMaxSpreadAngle = Mathf.Clamp(indexMaxSpreadAngle, 0f, 90f);
            thumbIndexSpreadGuardWeight = Mathf.Clamp01(indexSpreadGuardWeight);
            thumbMaxSegmentBendAngle = Mathf.Clamp(maxSegmentBendAngle, 0f, 60f);
            thumbSegmentStraightenWeight = Mathf.Clamp01(segmentStraightenWeight);
            suppressPoseShapingWithManualThumbReference = suppressPoseShapingWithManualReference;
            RecaptureBaseline();
        }

        public void RecaptureBaseline()
        {
            _initialLocalRotations.Clear();
            _initialLocalPositions.Clear();
            _thumbBaseHelperTransforms.Clear();
            _detachedThumbBaseHelperSources.Clear();
            _detachedThumbBaseSourceInitialLocalRotations.Clear();
            _detachedThumbBaseInitialRelativeRotations.Clear();
            _detachedThumbBaseInitialDistances.Clear();
            _lastRawLocalRotations.Clear();
            _lastCorrectedLocalRotations.Clear();
            _cachedThumbSides.Clear();
            _warningLogged = false;

            if (!InitializeIfNeeded())
            {
                return;
            }

            foreach (HumanBodyBones thumbBone in ThumbBones)
            {
                Transform thumbTransform = targetAnimator.GetBoneTransform(thumbBone);
                if (thumbTransform == null || _initialLocalRotations.ContainsKey(thumbTransform))
                {
                    continue;
                }

                _initialLocalRotations[thumbTransform] = thumbTransform.localRotation;
                _initialLocalPositions[thumbTransform] = thumbTransform.localPosition;
                CacheThumbSide(thumbTransform, IsRightHumanThumbBone(thumbBone));
            }

            CaptureThumbBaseHelperRotations();
            CaptureDetachedThumbBaseHelperSources();
        }

        public string BuildThumbHelperDebugSummary(bool isRightThumb)
        {
            string sideLabel = isRightThumb ? "R" : "L";
            Transform helperTransform = FindThumbBaseHelperTransformForSide(isRightThumb);
            if (helperTransform == null)
            {
                return $"side={sideLabel}, helper=<none>, source=<none>, state=missing";
            }

            _detachedThumbBaseHelperSources.TryGetValue(helperTransform, out Transform sourceTransform);

            float initialDistance = float.NaN;
            float currentDistance = float.NaN;
            float distanceDelta = float.NaN;
            if (sourceTransform != null &&
                _detachedThumbBaseInitialDistances.TryGetValue(helperTransform, out float storedDistance))
            {
                initialDistance = storedDistance;
                currentDistance = Vector3.Distance(helperTransform.position, sourceTransform.position);
                distanceDelta = Mathf.Abs(currentDistance - initialDistance);
            }

            float relativeRotationDelta = float.NaN;
            if (sourceTransform != null &&
                _detachedThumbBaseInitialRelativeRotations.TryGetValue(helperTransform, out Quaternion initialRelativeRotation))
            {
                Quaternion currentRelativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
                relativeRotationDelta = Quaternion.Angle(initialRelativeRotation, currentRelativeRotation);
            }

            float helperLocalPositionDelta = float.NaN;
            if (_initialLocalPositions.TryGetValue(helperTransform, out Vector3 initialLocalPosition))
            {
                helperLocalPositionDelta = Vector3.Distance(initialLocalPosition, helperTransform.localPosition);
            }

            float helperLocalRotationDelta = float.NaN;
            if (_initialLocalRotations.TryGetValue(helperTransform, out Quaternion initialLocalRotation))
            {
                helperLocalRotationDelta = Quaternion.Angle(initialLocalRotation, helperTransform.localRotation);
            }

            return
                $"side={sideLabel}, helper={GetTransformPath(helperTransform)}, source={GetTransformPath(sourceTransform)}, " +
                $"initDist={FormatDebugFloat(initialDistance)}, currDist={FormatDebugFloat(currentDistance)}, distDelta={FormatDebugFloat(distanceDelta)}, " +
                $"relRotDelta={FormatDebugFloat(relativeRotationDelta)}, localPosDelta={FormatDebugFloat(helperLocalPositionDelta)}, " +
                $"localRotDelta={FormatDebugFloat(helperLocalRotationDelta)}, linkedRetargeter={GetTransformPath(linkedPoseSpaceRetargeter != null ? linkedPoseSpaceRetargeter.transform : null)}";
        }

        private bool InitializeIfNeeded()
        {
            if (targetAnimator == null)
            {
                targetAnimator = GetComponent<Animator>();
            }

            ResolveLinkedPoseSpaceRetargeter();

            return targetAnimator != null &&
                targetAnimator.avatar != null &&
                targetAnimator.avatar.isValid &&
                targetAnimator.avatar.isHuman;
        }

        private bool ShouldSuppressPoseShapingWithManualThumbReference(bool isRightThumb)
        {
            ResolveLinkedPoseSpaceRetargeter();
            return suppressPoseShapingWithManualThumbReference &&
                linkedPoseSpaceRetargeter != null &&
                linkedPoseSpaceRetargeter.ShouldSuppressThumbPoseShapingGuardForHand(!isRightThumb);
        }

        private PoseSpaceRetargeter ResolveLinkedPoseSpaceRetargeter()
        {
            if (linkedPoseSpaceRetargeter != null &&
                (targetAnimator == null || linkedPoseSpaceRetargeter.targetAnimator == targetAnimator))
            {
                return linkedPoseSpaceRetargeter;
            }

            if (targetAnimator != null)
            {
                PoseSpaceRetargeter[] retargeters = FindObjectsOfType<PoseSpaceRetargeter>();
                foreach (PoseSpaceRetargeter candidate in retargeters)
                {
                    if (candidate != null && candidate.targetAnimator == targetAnimator)
                    {
                        linkedPoseSpaceRetargeter = candidate;
                        return linkedPoseSpaceRetargeter;
                    }
                }
            }

            if (linkedPoseSpaceRetargeter == null)
            {
                linkedPoseSpaceRetargeter = GetComponent<PoseSpaceRetargeter>();
            }

            return linkedPoseSpaceRetargeter;
        }

        private float GetEffectiveThumbProjectionGuardWeight(bool isRightThumb)
        {
            return ShouldSuppressPoseShapingWithManualThumbReference(isRightThumb) ? 0f : thumbProjectionGuardWeight;
        }

        private float EffectiveThumbProjectionGuardWeight
        {
            get
            {
                return Mathf.Max(
                    GetEffectiveThumbProjectionGuardWeight(false),
                    GetEffectiveThumbProjectionGuardWeight(true));
            }
        }

        private float EffectiveLeftThumbProjectionGuardWeight => GetEffectiveThumbProjectionGuardWeight(false);

        private float EffectiveRightThumbProjectionGuardWeight => GetEffectiveThumbProjectionGuardWeight(true);

        private float GetEffectiveThumbIndexSpreadGuardWeight(bool isRightThumb)
        {
            return ShouldSuppressPoseShapingWithManualThumbReference(isRightThumb) ? 0f : thumbIndexSpreadGuardWeight;
        }

        private float EffectiveThumbIndexSpreadGuardWeight
        {
            get
            {
                return Mathf.Max(
                    GetEffectiveThumbIndexSpreadGuardWeight(false),
                    GetEffectiveThumbIndexSpreadGuardWeight(true));
            }
        }

        private float EffectiveLeftThumbIndexSpreadGuardWeight => GetEffectiveThumbIndexSpreadGuardWeight(false);

        private float EffectiveRightThumbIndexSpreadGuardWeight => GetEffectiveThumbIndexSpreadGuardWeight(true);

        private float GetEffectiveThumbSegmentStraightenWeight(bool isRightThumb)
        {
            return ShouldSuppressPoseShapingWithManualThumbReference(isRightThumb) ? 0f : thumbSegmentStraightenWeight;
        }

        private float EffectiveThumbSegmentStraightenWeight
        {
            get
            {
                return Mathf.Max(
                    GetEffectiveThumbSegmentStraightenWeight(false),
                    GetEffectiveThumbSegmentStraightenWeight(true));
            }
        }

        private float EffectiveLeftThumbSegmentStraightenWeight => GetEffectiveThumbSegmentStraightenWeight(false);

        private float EffectiveRightThumbSegmentStraightenWeight => GetEffectiveThumbSegmentStraightenWeight(true);

        private int LastLeftThumbProjectionCorrectionApplyCount { get; set; }

        private int LastRightThumbProjectionCorrectionApplyCount { get; set; }

        private int LastLeftThumbProjectionCorrectionPreserveCount { get; set; }

        private int LastRightThumbProjectionCorrectionPreserveCount { get; set; }

        private int LastLeftThumbSegmentStraightenApplyCount { get; set; }

        private int LastRightThumbSegmentStraightenApplyCount { get; set; }

        private int LastLeftThumbSegmentStraightenPreserveCount { get; set; }

        private int LastRightThumbSegmentStraightenPreserveCount { get; set; }

        private void ResetThumbPoseShapingDiagnostics()
        {
            LastLeftThumbProjectionCorrectionApplyCount = 0;
            LastRightThumbProjectionCorrectionApplyCount = 0;
            LastLeftThumbProjectionCorrectionPreserveCount = 0;
            LastRightThumbProjectionCorrectionPreserveCount = 0;
            LastLeftThumbSegmentStraightenApplyCount = 0;
            LastRightThumbSegmentStraightenApplyCount = 0;
            LastLeftThumbSegmentStraightenPreserveCount = 0;
            LastRightThumbSegmentStraightenPreserveCount = 0;
            ResolveLinkedPoseSpaceRetargeter();
            linkedPoseSpaceRetargeter?.ResetThumbWorldRotationPreserveDiagnostics();
        }

        private void RecordThumbProjectionCorrection(bool isRightThumb, bool preserved)
        {
            if (isRightThumb)
            {
                if (preserved)
                {
                    LastRightThumbProjectionCorrectionPreserveCount++;
                }
                else
                {
                    LastRightThumbProjectionCorrectionApplyCount++;
                }
            }
            else
            {
                if (preserved)
                {
                    LastLeftThumbProjectionCorrectionPreserveCount++;
                }
                else
                {
                    LastLeftThumbProjectionCorrectionApplyCount++;
                }
            }
        }

        private void RecordThumbSegmentStraightenCorrection(bool isRightThumb, bool preserved)
        {
            if (isRightThumb)
            {
                if (preserved)
                {
                    LastRightThumbSegmentStraightenPreserveCount++;
                }
                else
                {
                    LastRightThumbSegmentStraightenApplyCount++;
                }
            }
            else
            {
                if (preserved)
                {
                    LastLeftThumbSegmentStraightenPreserveCount++;
                }
                else
                {
                    LastLeftThumbSegmentStraightenApplyCount++;
                }
            }
        }

        private float GetLimit(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                case HumanBodyBones.RightThumbProximal:
                    return proximalMaxLocalAngle;
                case HumanBodyBones.LeftThumbIntermediate:
                case HumanBodyBones.RightThumbIntermediate:
                    return intermediateMaxLocalAngle;
                case HumanBodyBones.LeftThumbDistal:
                case HumanBodyBones.RightThumbDistal:
                    return distalMaxLocalAngle;
                default:
                    return 0f;
            }
        }

        private int ClampThumbBaseHelperTransforms()
        {
            int changed = 0;
            foreach (Transform thumbTransform in _thumbBaseHelperTransforms)
            {
                if (thumbTransform == null ||
                    !_initialLocalRotations.TryGetValue(thumbTransform, out Quaternion initialRotation))
                {
                    continue;
                }

                Quaternion rawRotation = GetCurrentRawLocalRotation(thumbTransform);
                if (!IsFinite(rawRotation))
                {
                    SetCorrectedLocalRotation(thumbTransform, initialRotation, initialRotation);
                    changed++;
                    continue;
                }

                Quaternion offsetRotation = GetProximalRotationOffsetRotation(thumbTransform);
                float limit = proximalMaxLocalAngle;
                if (!ThumbLocalRotationCalculator.TryCalculateCorrection(
                        initialRotation,
                        rawRotation,
                        thumbTransform.localRotation,
                        offsetRotation,
                        limit,
                        out Quaternion correctedRotation))
                {
                    continue;
                }

                SetCorrectedLocalRotation(thumbTransform, rawRotation, correctedRotation);
                changed++;
            }

            return changed;
        }

        private int PreserveThumbVisualLength()
        {
            ResetThumbPoseShapingDiagnostics();

            bool leftInactive =
                (GetEffectiveThumbProjectionGuardWeight(false) <= 0f || (thumbProjectionMinPalmNormal <= 0.001f && thumbProjectionMaxPalmNormal >= 0.999f)) &&
                (GetEffectiveThumbIndexSpreadGuardWeight(false) <= 0f || thumbIndexMaxSpreadAngle >= 89.999f) &&
                (GetEffectiveThumbSegmentStraightenWeight(false) <= 0f || thumbMaxSegmentBendAngle >= 59.999f);
            bool rightInactive =
                (GetEffectiveThumbProjectionGuardWeight(true) <= 0f || (thumbProjectionMinPalmNormal <= 0.001f && thumbProjectionMaxPalmNormal >= 0.999f)) &&
                (GetEffectiveThumbIndexSpreadGuardWeight(true) <= 0f || thumbIndexMaxSpreadAngle >= 89.999f) &&
                (GetEffectiveThumbSegmentStraightenWeight(true) <= 0f || thumbMaxSegmentBendAngle >= 59.999f);

            if (leftInactive && rightInactive)
            {
                return 0;
            }

            return PreserveThumbVisualLength(false) + PreserveThumbVisualLength(true);
        }

        private int PreserveThumbVisualLength(bool isRightThumb)
        {
            Transform proximal = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            Transform intermediate = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);
            Transform distal = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbDistal : HumanBodyBones.LeftThumbDistal);

            if (proximal == null || intermediate == null)
            {
                return 0;
            }

            int changed = ProjectThumbProximalIntoPalmFrame(proximal, intermediate, isRightThumb);
            if (distal != null)
            {
                changed += StraightenThumbSegmentBend(proximal, intermediate, distal, isRightThumb);
            }

            return changed;
        }

        private int ProjectThumbProximalIntoPalmFrame(Transform proximal, Transform intermediate, bool isRightThumb)
        {
            if (!TryBuildPalmFrame(isRightThumb, out Vector3 sideAxis, out Vector3 palmNormal, out Vector3 forwardAxis))
            {
                return 0;
            }

            float minNormal = Mathf.Clamp01(thumbProjectionMinPalmNormal);
            float maxNormal = Mathf.Clamp(Mathf.Max(thumbProjectionMaxPalmNormal, minNormal), 0f, 1f);
            float configuredMaxSpreadAngle = Mathf.Clamp(thumbIndexMaxSpreadAngle, 0f, 90f);
            float maxSpreadAngle = configuredMaxSpreadAngle;
            bool useHighRiskManualOverride = false;
            ResolveLinkedPoseSpaceRetargeter();
            // 수동 기준 포즈에서 위험한 엄지 자세로 판정되면 이 메서드의 제한값을 더 강하게 적용합니다.
            if (linkedPoseSpaceRetargeter != null &&
                linkedPoseSpaceRetargeter.TryGetHighRiskManualThumbPoseConstraintOverrides(
                    !isRightThumb,
                    out float overrideMinNormal,
                    out float overrideMaxNormal,
                    out float overrideMaxSpreadAngle))
            {
                useHighRiskManualOverride = true;
                minNormal = Mathf.Clamp01(overrideMinNormal);
                maxNormal = Mathf.Clamp(Mathf.Max(overrideMaxNormal, minNormal), 0f, 1f);
                maxSpreadAngle = ResolveManualOverrideMaxSpreadAngle(configuredMaxSpreadAngle, overrideMaxSpreadAngle);
            }

            Transform hand = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform index = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Vector3 direction = intermediate.position - proximal.position;
            if (!TryNormalize(direction, out direction))
            {
                return 0;
            }

            float indexSpreadGuardWeight = GetEffectiveThumbIndexSpreadGuardWeight(isRightThumb);
            float projectionGuardWeight = GetEffectiveThumbProjectionGuardWeight(isRightThumb);
            if (useHighRiskManualOverride)
            {
                indexSpreadGuardWeight = Mathf.Max(indexSpreadGuardWeight, 1f);
                projectionGuardWeight = Mathf.Max(projectionGuardWeight, 1f);
            }
            Vector3 indexDirection = Vector3.zero;
            bool hasIndexDirection =
                hand != null &&
                index != null &&
                TryNormalize(index.position - hand.position, out indexDirection);

            if (!ThumbPoseDirectionCalculator.TryCalculateCorrectedDirection(
                    direction,
                    indexDirection,
                    hasIndexDirection,
                    sideAxis,
                    palmNormal,
                    forwardAxis,
                    minNormal,
                    maxNormal,
                    maxSpreadAngle,
                    indexSpreadGuardWeight,
                    projectionGuardWeight,
                    out Vector3 targetDirection))
            {
                return 0;
            }

            // 방향 차이만 proximal 월드 회전에 얹고, 본 길이나 위치는 직접 바꾸지 않습니다.
            bool bypassManualProjectionPreserve =
                useHighRiskManualOverride &&
                ShouldBypassManualThumbProjectionPreserveForSmokeRisk(
                    Vector3.Dot(direction, palmNormal),
                    minNormal,
                    maxNormal);

            Quaternion correctedWorldRotation = Quaternion.FromToRotation(direction, targetDirection) * proximal.rotation;
            HumanBodyBones proximalBone = isRightThumb ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal;
            if (!bypassManualProjectionPreserve &&
                ShouldPreserveManualThumbWorldRotationCorrection(proximalBone, proximal, correctedWorldRotation))
            {
                RecordThumbProjectionCorrection(isRightThumb, true);
                return 0;
            }

            ApplyWorldRotationCorrection(proximal, correctedWorldRotation);
            RecordThumbProjectionCorrection(isRightThumb, false);
            return 1;
        }

        private static float ResolveManualOverrideMaxSpreadAngle(float configuredMaxSpreadAngle, float overrideMaxSpreadAngle)
        {
            float configured = Mathf.Clamp(configuredMaxSpreadAngle, 0f, 90f);
            float overrideValue = Mathf.Clamp(overrideMaxSpreadAngle, 0f, 90f);
            return Mathf.Min(configured, overrideValue);
        }

        private static bool ShouldBypassManualThumbProjectionPreserveForSmokeRisk(
            float currentProjection,
            float minNormal,
            float maxNormal)
        {
            float minValue = Mathf.Clamp01(minNormal);
            float maxValue = Mathf.Clamp(Mathf.Max(maxNormal, minValue), 0f, 1f);
            float risk = ThumbPoseRiskCalculator.CalculateOutsideRange(
                currentProjection,
                minValue,
                maxValue,
                1f);
            return IsFinite(risk) && risk > ManualThumbProjectionPreserveSmokeRiskLimit;
        }

        private int StraightenThumbSegmentBend(Transform proximal, Transform intermediate, Transform distal, bool isRightThumb)
        {
            float segmentStraightenWeight = GetEffectiveThumbSegmentStraightenWeight(isRightThumb);
            if (segmentStraightenWeight <= 0f || thumbMaxSegmentBendAngle >= 59.999f)
            {
                return 0;
            }

            Vector3 proximalDirection = intermediate.position - proximal.position;
            Vector3 intermediateDirection = distal.position - intermediate.position;
            if (!TryNormalize(proximalDirection, out proximalDirection) ||
                !TryNormalize(intermediateDirection, out intermediateDirection))
            {
                return 0;
            }

            if (!ThumbPoseDirectionCalculator.TryCalculateStraightenedDirection(
                    proximalDirection,
                    intermediateDirection,
                    thumbMaxSegmentBendAngle,
                    segmentStraightenWeight,
                    out Vector3 targetDirection))
            {
                return 0;
            }

            Quaternion correctedWorldRotation = Quaternion.FromToRotation(intermediateDirection, targetDirection) * intermediate.rotation;
            HumanBodyBones intermediateBone = isRightThumb ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate;
            if (ShouldPreserveManualThumbWorldRotationCorrection(intermediateBone, intermediate, correctedWorldRotation))
            {
                RecordThumbSegmentStraightenCorrection(isRightThumb, true);
                return 0;
            }

            ApplyWorldRotationCorrection(intermediate, correctedWorldRotation);
            RecordThumbSegmentStraightenCorrection(isRightThumb, false);
            return 1;
        }

        private bool ShouldPreserveManualThumbWorldRotationCorrection(
            HumanBodyBones thumbBone,
            Transform targetTransform,
            Quaternion correctedWorldRotation)
        {
            ResolveLinkedPoseSpaceRetargeter();
            return linkedPoseSpaceRetargeter != null &&
                linkedPoseSpaceRetargeter.ShouldPreserveManualThumbWorldRotationCorrection(
                    thumbBone,
                    targetTransform,
                    correctedWorldRotation);
        }

        private bool TryCalculateThumbAndIndexDirections(
            bool isRightThumb,
            out Vector3 thumbDirection,
            out Vector3 indexDirection)
        {
            thumbDirection = Vector3.zero;
            indexDirection = Vector3.zero;

            Transform hand = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform thumbProximal = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
            Transform thumbIntermediate = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbIntermediate : HumanBodyBones.LeftThumbIntermediate);
            Transform indexProximal = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform indexIntermediate = targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightIndexIntermediate : HumanBodyBones.LeftIndexIntermediate);

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

        private bool TryBuildPalmFrame(bool isRightThumb, out Vector3 sideAxis, out Vector3 palmNormal, out Vector3 forwardAxis)
        {
            sideAxis = Vector3.zero;
            palmNormal = Vector3.zero;
            forwardAxis = Vector3.zero;

            Transform hand = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Transform index = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
            Transform middle = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightMiddleProximal : HumanBodyBones.LeftMiddleProximal);
            Transform little = targetAnimator.GetBoneTransform(isRightThumb ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
            if (hand == null || index == null || middle == null || little == null)
            {
                return false;
            }

            Vector3 rawSide = index.position - little.position;
            if (isRightThumb)
            {
                rawSide = -rawSide;
            }

            Vector3 rawForward = ((index.position + middle.position + little.position) / 3f) - hand.position;
            if (!TryNormalize(rawSide, out sideAxis) ||
                !TryNormalize(rawForward, out forwardAxis) ||
                !TryNormalize(Vector3.Cross(sideAxis, forwardAxis), out palmNormal) ||
                !TryNormalize(Vector3.Cross(palmNormal, sideAxis), out forwardAxis))
            {
                return false;
            }

            return true;
        }

        private void ApplyWorldRotationCorrection(Transform targetTransform, Quaternion correctedWorldRotation)
        {
            Quaternion rawLocalRotation = _lastRawLocalRotations.TryGetValue(targetTransform, out Quaternion lastRawRotation)
                ? lastRawRotation
                : targetTransform.localRotation;

            targetTransform.rotation = correctedWorldRotation;
            _lastRawLocalRotations[targetTransform] = rawLocalRotation;
            _lastCorrectedLocalRotations[targetTransform] = targetTransform.localRotation;
        }

        private int SyncDetachedThumbBaseHelperTransforms()
        {
            bool useWebbingGuard = stabilizeThumbWebbingCrease && thumbWebbingCreaseStabilizeWeight > 0f;
            if ((!syncDetachedThumbBaseHelpers || detachedThumbBaseHelperSyncWeight <= 0f) &&
                (!stabilizeDetachedThumbBasePalm || detachedThumbBasePalmStabilizeWeight <= 0f) &&
                !useWebbingGuard)
            {
                return 0;
            }

            int changed = 0;
            foreach (KeyValuePair<Transform, Transform> pair in _detachedThumbBaseHelperSources)
            {
                Transform helperTransform = pair.Key;
                Transform sourceTransform = pair.Value;
                if (helperTransform == null || sourceTransform == null)
                {
                    continue;
                }

                Quaternion targetRotation = CalculateDetachedThumbBaseHelperTargetRotation(helperTransform, sourceTransform);

                if (Quaternion.Angle(helperTransform.localRotation, targetRotation) > 0.001f)
                {
                    helperTransform.localRotation = targetRotation;
                    changed++;
                }

                if (!syncDetachedThumbBaseHelperPositions)
                {
                    if ((stabilizeDetachedThumbBasePalm || useWebbingGuard) &&
                        _initialLocalPositions.TryGetValue(helperTransform, out Vector3 anchoredPosition) &&
                        (helperTransform.localPosition - anchoredPosition).sqrMagnitude > 0.00000001f)
                    {
                        // YYB 손꿈치 스킨용 Thumb0 보조본은 위치까지 움직이면 엄지 뿌리 메시가 손바닥 밖으로 끌려간다.
                        // 실제 엄지 구동본은 별도로 움직이고, 보조본 위치는 초기 손바닥 앵커에 고정한다.
                        helperTransform.localPosition = anchoredPosition;
                        changed++;
                    }

                    continue;
                }

                Vector3 targetPosition = GetSourcePositionInHelperParentSpace(helperTransform, sourceTransform);
                if (_initialLocalPositions.TryGetValue(helperTransform, out Vector3 initialPosition) &&
                    detachedThumbBaseHelperSyncWeight < 0.999f)
                {
                    targetPosition = Vector3.Lerp(initialPosition, targetPosition, detachedThumbBaseHelperSyncWeight);
                }

                if (_initialLocalPositions.TryGetValue(helperTransform, out initialPosition))
                {
                    targetPosition = initialPosition + Vector3.ClampMagnitude(
                        targetPosition - initialPosition,
                        detachedThumbBaseHelperMaxPositionOffset);
                }

                if (useWebbingGuard)
                {
                    targetPosition = ConstrainThumbWebbingHelperPosition(helperTransform, sourceTransform, targetPosition);
                }

                if ((helperTransform.localPosition - targetPosition).sqrMagnitude <= 0.00000001f)
                {
                    continue;
                }

                helperTransform.localPosition = targetPosition;
                changed++;
            }

            return changed;
        }

        private Quaternion CalculateDetachedThumbBaseHelperTargetRotation(Transform helperTransform, Transform sourceTransform)
        {
            if (!_initialLocalRotations.TryGetValue(helperTransform, out Quaternion helperInitialRotation))
            {
                return sourceTransform.localRotation;
            }

            Quaternion sourceRotation = sourceTransform.localRotation;
            bool hasSourceInitialRotation =
                _detachedThumbBaseSourceInitialLocalRotations.TryGetValue(
                    sourceTransform,
                    out Quaternion sourceInitialRotation);
            Quaternion deltaAxisRemap = Quaternion.identity;
            if (syncDetachedThumbBaseHelpers &&
                detachedThumbBaseHelperSyncWeight > 0f &&
                hasSourceInitialRotation)
            {
                deltaAxisRemap = GetDetachedThumbBaseHelperDeltaAxisRemap(sourceTransform);
            }

            Quaternion targetRotationOffset = GetDetachedThumbBaseHelperTargetRotationOffset(sourceTransform);
            Quaternion targetRotation = ThumbBaseHelperRotationCalculator.CalculateBaseRotation(
                helperInitialRotation,
                sourceRotation,
                hasSourceInitialRotation,
                sourceInitialRotation,
                syncDetachedThumbBaseHelpers,
                detachedThumbBaseHelperSyncWeight,
                deltaAxisRemap,
                targetRotationOffset,
                stabilizeDetachedThumbBasePalm,
                detachedThumbBasePalmStabilizeWeight);

            float effectiveWebbingWeight = 0f;
            float effectiveWebbingMaxLocalAngle = thumbWebbingCreaseMaxLocalAngle;
            if (stabilizeThumbWebbingCrease && thumbWebbingCreaseStabilizeWeight > 0f)
            {
                GetEffectiveThumbWebbingCorrectiveSettings(
                    helperTransform,
                    sourceTransform,
                    targetRotation,
                    helperTransform.localPosition,
                    out effectiveWebbingWeight,
                    out effectiveWebbingMaxLocalAngle,
                    out _);

            }

            return ThumbBaseHelperRotationCalculator.FinalizeRotation(
                helperInitialRotation,
                targetRotation,
                stabilizeThumbWebbingCrease,
                effectiveWebbingWeight,
                detachedThumbBaseHelperMaxLocalAngle,
                stabilizeDetachedThumbBasePalm,
                detachedThumbBasePalmStabilizeWeight,
                detachedThumbBasePalmMaxLocalAngle,
                effectiveWebbingMaxLocalAngle);
        }

        private Quaternion GetDetachedThumbBaseHelperDeltaAxisRemap(Transform sourceTransform)
        {
            if (!IsExplicitDetachedThumbBaseSource(sourceTransform))
            {
                return Quaternion.identity;
            }

            Vector3 offset = Vector3.zero;
            if (TryResolveThumbSide(sourceTransform, out bool isRightThumb))
            {
                offset = isRightThumb
                    ? rightDetachedThumbBaseHelperDeltaAxisOffset
                    : leftDetachedThumbBaseHelperDeltaAxisOffset;
            }

            return offset.sqrMagnitude <= 0.000001f
                ? Quaternion.identity
                : Quaternion.Euler(offset);
        }

        private Quaternion GetDetachedThumbBaseHelperTargetRotationOffset(Transform sourceTransform)
        {
            if (!IsExplicitDetachedThumbBaseSource(sourceTransform))
            {
                return Quaternion.identity;
            }

            Vector3 offset = Vector3.zero;
            if (TryResolveThumbSide(sourceTransform, out bool isRightThumb))
            {
                offset = isRightThumb
                    ? rightDetachedThumbBaseHelperTargetRotationOffset
                    : leftDetachedThumbBaseHelperTargetRotationOffset;
            }

            return offset.sqrMagnitude <= 0.000001f
                ? Quaternion.identity
                : Quaternion.Euler(offset);
        }

        private Vector3 ConstrainThumbWebbingHelperPosition(
            Transform helperTransform,
            Transform sourceTransform,
            Vector3 targetPosition)
        {
            if (helperTransform == null ||
                !_initialLocalPositions.TryGetValue(helperTransform, out Vector3 initialPosition))
            {
                return targetPosition;
            }

            GetEffectiveThumbWebbingCorrectiveSettings(
                helperTransform,
                sourceTransform,
                helperTransform.localRotation,
                targetPosition,
                out float weight,
                out _,
                out float maxOffset);

            return ThumbWebbingCorrectionCalculator.ConstrainPosition(
                initialPosition,
                targetPosition,
                weight,
                maxOffset);
        }

        private void GetEffectiveThumbWebbingCorrectiveSettings(
            Transform helperTransform,
            Transform sourceTransform,
            Quaternion targetLocalRotation,
            Vector3 targetLocalPosition,
            out float weight,
            out float maxLocalAngle,
            out float maxPositionOffset)
        {
            float poseRisk = CalculateThumbWebbingPoseRisk(
                helperTransform,
                sourceTransform,
                targetLocalRotation,
                targetLocalPosition);
            ThumbWebbingCorrectionCalculator.CalculateEffectiveSettings(
                thumbWebbingCreaseStabilizeWeight,
                thumbWebbingCreaseMaxLocalAngle,
                thumbWebbingCreaseMaxPositionOffset,
                detachedThumbBaseHelperMaxLocalAngle,
                detachedThumbBaseHelperMaxPositionOffset,
                poseRisk,
                ThumbWebbingDynamicMinLocalAngle,
                ThumbWebbingDynamicMinPositionOffset,
                out weight,
                out maxLocalAngle,
                out maxPositionOffset);
        }

        private float CalculateThumbWebbingPoseRisk(
            Transform helperTransform,
            Transform sourceTransform,
            Quaternion targetLocalRotation,
            Vector3 targetLocalPosition)
        {
            float spreadRisk = float.NaN;
            float projectionRisk = float.NaN;
            float helperDistanceRisk = CalculateThumbWebbingHelperDistanceRisk(helperTransform, sourceTransform, targetLocalPosition);
            float helperRotationRisk = CalculateThumbWebbingHelperRotationRisk(helperTransform, sourceTransform, targetLocalRotation);

            Transform thumbSideReference = helperTransform != null ? helperTransform : sourceTransform;
            if (TryResolveThumbSide(thumbSideReference, out bool isRightThumb) &&
                TryCalculateThumbAndIndexDirections(isRightThumb, out Vector3 thumbDirection, out Vector3 indexDirection))
            {
                spreadRisk = ThumbPoseRiskCalculator.CalculateAboveThreshold(
                    Vector3.Angle(thumbDirection, indexDirection),
                    Mathf.Clamp(thumbIndexMaxSpreadAngle, 0f, 90f),
                    ThumbWebbingSpreadFullRiskAngle);

                if (TryBuildPalmFrame(isRightThumb, out _, out Vector3 palmNormal, out _))
                {
                    projectionRisk = ThumbPoseRiskCalculator.CalculateOutsideRange(
                        Vector3.Dot(thumbDirection, palmNormal),
                        Mathf.Clamp01(thumbProjectionMinPalmNormal),
                        Mathf.Clamp(Mathf.Max(thumbProjectionMaxPalmNormal, thumbProjectionMinPalmNormal), 0f, 1f),
                        ThumbWebbingProjectionFullRiskDistance);
                }
            }

            return ThumbPoseRiskCalculator.FindMaximumFinite(
                spreadRisk,
                projectionRisk,
                helperDistanceRisk,
                helperRotationRisk);
        }

        private float CalculateThumbWebbingHelperDistanceRisk(
            Transform helperTransform,
            Transform sourceTransform,
            Vector3 targetLocalPosition)
        {
            if (helperTransform == null ||
                sourceTransform == null ||
                !_detachedThumbBaseInitialDistances.TryGetValue(helperTransform, out float initialDistance))
            {
                return float.NaN;
            }

            Vector3 helperWorldPosition = helperTransform.parent != null
                ? helperTransform.parent.TransformPoint(targetLocalPosition)
                : targetLocalPosition;
            float distanceDelta = Mathf.Abs(Vector3.Distance(helperWorldPosition, sourceTransform.position) - initialDistance);
            return ThumbPoseRiskCalculator.CalculateAboveThreshold(
                distanceDelta,
                ThumbWebbingHelperDistanceWarning,
                ThumbWebbingHelperDistanceFullRisk);
        }

        private float CalculateThumbWebbingHelperRotationRisk(
            Transform helperTransform,
            Transform sourceTransform,
            Quaternion targetLocalRotation)
        {
            if (helperTransform == null ||
                sourceTransform == null ||
                !_detachedThumbBaseInitialRelativeRotations.TryGetValue(helperTransform, out Quaternion initialRelativeRotation))
            {
                return float.NaN;
            }

            Quaternion helperWorldRotation = helperTransform.parent != null
                ? helperTransform.parent.rotation * targetLocalRotation
                : targetLocalRotation;
            Quaternion currentRelativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperWorldRotation;
            float rotationDelta = Quaternion.Angle(initialRelativeRotation, currentRelativeRotation);
            return ThumbPoseRiskCalculator.CalculateAboveThreshold(
                rotationDelta,
                ThumbWebbingHelperRotationWarning,
                ThumbWebbingHelperRotationFullRisk);
        }

        private static Vector3 GetSourcePositionInHelperParentSpace(Transform helperTransform, Transform sourceTransform)
        {
            if (helperTransform == null || sourceTransform == null)
            {
                return Vector3.zero;
            }

            if (helperTransform.parent == sourceTransform.parent)
            {
                return sourceTransform.localPosition;
            }

            return helperTransform.parent != null
                ? helperTransform.parent.InverseTransformPoint(sourceTransform.position)
                : sourceTransform.position;
        }

        private void CaptureThumbBaseHelperRotations()
        {
            if (targetAnimator == null)
            {
                return;
            }

            foreach (Transform candidate in targetAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (!IsThumbBaseHelperTransform(candidate))
                {
                    continue;
                }

                if (_initialLocalRotations.ContainsKey(candidate))
                {
                    continue;
                }

                _initialLocalRotations[candidate] = candidate.localRotation;
                _initialLocalPositions[candidate] = candidate.localPosition;
                _thumbBaseHelperTransforms.Add(candidate);
                CacheThumbSide(candidate);
            }
        }

        private Transform FindThumbBaseHelperTransformForSide(bool isRightThumb)
        {
            foreach (Transform helperTransform in _thumbBaseHelperTransforms)
            {
                if (helperTransform == null)
                {
                    continue;
                }

                if (TryResolveThumbSide(helperTransform, out bool helperIsRightThumb) &&
                    helperIsRightThumb == isRightThumb)
                {
                    return helperTransform;
                }
            }

            return null;
        }

        private static string FormatDebugFloat(float value)
        {
            return IsFinite(value) ? value.ToString("F4") : "NaN";
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return "<none>";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(transform.name);
            Transform current = transform.parent;
            while (current != null)
            {
                builder.Insert(0, current.name + "/");
                current = current.parent;
            }

            return builder.ToString();
        }

        private void CaptureDetachedThumbBaseHelperSources()
        {
            if (targetAnimator == null)
            {
                return;
            }

            foreach (Transform helperTransform in targetAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (!IsDetachedThumbBaseHelperTransform(helperTransform))
                {
                    continue;
                }

                Transform sourceTransform = FindMatchingActiveThumbBaseSource(helperTransform);
                if (sourceTransform == null)
                {
                    continue;
                }

                CacheThumbSide(helperTransform);
                CacheThumbSide(sourceTransform);
                _detachedThumbBaseHelperSources[helperTransform] = sourceTransform;
                if (!_detachedThumbBaseSourceInitialLocalRotations.ContainsKey(sourceTransform))
                {
                    _detachedThumbBaseSourceInitialLocalRotations[sourceTransform] = sourceTransform.localRotation;
                }

                _detachedThumbBaseInitialRelativeRotations[helperTransform] =
                    Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
                _detachedThumbBaseInitialDistances[helperTransform] =
                    Vector3.Distance(helperTransform.position, sourceTransform.position);
            }
        }

        private Transform FindMatchingActiveThumbBaseSource(Transform helperTransform)
        {
            if (helperTransform == null)
            {
                return null;
            }

            if (!TryResolveThumbSide(helperTransform, out bool isRightThumb))
            {
                return GetClosestMappedThumbProximal(helperTransform);
            }

            foreach (Transform candidate in targetAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (candidate == null || candidate == helperTransform)
                {
                    continue;
                }

                if (!ThumbTransformNamePolicy.IsActiveBaseSource(candidate.name) ||
                    !TryResolveThumbSide(candidate, out bool candidateIsRightThumb) ||
                    candidateIsRightThumb != isRightThumb)
                {
                    continue;
                }

                return candidate;
            }

            Transform mappedThumbProximal = GetMappedThumbProximal(isRightThumb);
            return mappedThumbProximal != helperTransform ? mappedThumbProximal : null;
        }

        private void CacheThumbSide(Transform thumbTransform, bool? knownIsRightThumb = null)
        {
            if (thumbTransform == null || _cachedThumbSides.ContainsKey(thumbTransform))
            {
                return;
            }

            if (knownIsRightThumb.HasValue)
            {
                _cachedThumbSides[thumbTransform] = knownIsRightThumb.Value;
                return;
            }

            if (TryResolveThumbSideFromHumanMapping(thumbTransform, out bool isRightThumb) ||
                (thumbTransform != null &&
                    ThumbTransformNamePolicy.TryResolveSide(thumbTransform.name, out isRightThumb)) ||
                TryResolveThumbSideByReferenceDistance(thumbTransform, out isRightThumb))
            {
                _cachedThumbSides[thumbTransform] = isRightThumb;
            }
        }

        private bool IsThumbBaseHelperTransform(Transform candidate)
        {
            return candidate != null &&
                !IsMappedHumanThumbBone(candidate) &&
                ThumbTransformNamePolicy.IsBaseHelper(candidate.name);
        }

        private bool IsDetachedThumbBaseHelperTransform(Transform candidate)
        {
            if (!IsThumbBaseHelperTransform(candidate))
            {
                return false;
            }

            string normalizedName = candidate.name.ToLowerInvariant();
            return !normalizedName.Contains("!") &&
                !normalizedName.Contains("ghost");
        }

        private bool IsMappedHumanThumbBone(Transform candidate)
        {
            if (candidate == null || targetAnimator == null)
            {
                return false;
            }

            foreach (HumanBodyBones thumbBone in ThumbBones)
            {
                if (targetAnimator.GetBoneTransform(thumbBone) == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private Transform GetMappedThumbProximal(bool isRightThumb)
        {
            if (targetAnimator == null)
            {
                return null;
            }

            return targetAnimator.GetBoneTransform(
                isRightThumb ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
        }

        private Transform GetClosestMappedThumbProximal(Transform referenceTransform)
        {
            if (referenceTransform == null)
            {
                return null;
            }

            Transform leftThumbProximal = GetMappedThumbProximal(false);
            Transform rightThumbProximal = GetMappedThumbProximal(true);
            if (leftThumbProximal == null)
            {
                return rightThumbProximal != referenceTransform ? rightThumbProximal : null;
            }

            if (rightThumbProximal == null)
            {
                return leftThumbProximal != referenceTransform ? leftThumbProximal : null;
            }

            float leftDistance = (referenceTransform.position - leftThumbProximal.position).sqrMagnitude;
            float rightDistance = (referenceTransform.position - rightThumbProximal.position).sqrMagnitude;
            Transform closest = rightDistance < leftDistance ? rightThumbProximal : leftThumbProximal;
            return closest != referenceTransform ? closest : null;
        }

        private bool TryResolveThumbSide(Transform thumbTransform, out bool isRightThumb)
        {
            if (thumbTransform != null && _cachedThumbSides.TryGetValue(thumbTransform, out isRightThumb))
            {
                return true;
            }

            if (TryResolveThumbSideFromHumanMapping(thumbTransform, out isRightThumb) ||
                (thumbTransform != null &&
                    ThumbTransformNamePolicy.TryResolveSide(thumbTransform.name, out isRightThumb)) ||
                TryResolveThumbSideByReferenceDistance(thumbTransform, out isRightThumb))
            {
                if (thumbTransform != null)
                {
                    _cachedThumbSides[thumbTransform] = isRightThumb;
                }

                return true;
            }

            isRightThumb = false;
            return false;
        }

        private bool TryResolveThumbSideFromHumanMapping(Transform thumbTransform, out bool isRightThumb)
        {
            if (thumbTransform != null && targetAnimator != null)
            {
                foreach (HumanBodyBones thumbBone in ThumbBones)
                {
                    if (targetAnimator.GetBoneTransform(thumbBone) == thumbTransform)
                    {
                        isRightThumb = IsRightHumanThumbBone(thumbBone);
                        return true;
                    }
                }

                if (targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand) == thumbTransform)
                {
                    isRightThumb = false;
                    return true;
                }

                if (targetAnimator.GetBoneTransform(HumanBodyBones.RightHand) == thumbTransform)
                {
                    isRightThumb = true;
                    return true;
                }
            }

            isRightThumb = false;
            return false;
        }

        private bool TryResolveThumbSideByReferenceDistance(Transform thumbTransform, out bool isRightThumb)
        {
            if (thumbTransform != null && targetAnimator != null)
            {
                float leftDistance = GetThumbSideReferenceDistance(
                    thumbTransform,
                    targetAnimator.GetBoneTransform(HumanBodyBones.LeftHand),
                    GetMappedThumbProximal(false));
                float rightDistance = GetThumbSideReferenceDistance(
                    thumbTransform,
                    targetAnimator.GetBoneTransform(HumanBodyBones.RightHand),
                    GetMappedThumbProximal(true));
                if (IsFinite(leftDistance) || IsFinite(rightDistance))
                {
                    if (!IsFinite(leftDistance))
                    {
                        isRightThumb = true;
                        return true;
                    }

                    if (!IsFinite(rightDistance))
                    {
                        isRightThumb = false;
                        return true;
                    }

                    isRightThumb = rightDistance < leftDistance;
                    return true;
                }
            }

            isRightThumb = false;
            return false;
        }

        private static float GetThumbSideReferenceDistance(
            Transform thumbTransform,
            Transform handTransform,
            Transform thumbProximalTransform)
        {
            float handDistance = handTransform != null
                ? (thumbTransform.position - handTransform.position).sqrMagnitude
                : float.NaN;
            float thumbDistance = thumbProximalTransform != null
                ? (thumbTransform.position - thumbProximalTransform.position).sqrMagnitude
                : float.NaN;

            if (!IsFinite(handDistance))
            {
                return thumbDistance;
            }

            if (!IsFinite(thumbDistance))
            {
                return handDistance;
            }

            return Mathf.Min(handDistance, thumbDistance);
        }

        private static bool IsExplicitDetachedThumbBaseSource(Transform sourceTransform)
        {
            return sourceTransform != null &&
                ThumbTransformNamePolicy.IsActiveBaseSource(sourceTransform.name);
        }

        private Quaternion GetThumbRotationOffset(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.LeftThumbProximal:
                    return GetProximalRotationOffsetRotation(false);
                case HumanBodyBones.RightThumbProximal:
                    return GetProximalRotationOffsetRotation(true);
                default:
                    return Quaternion.identity;
            }
        }

        private Quaternion GetProximalRotationOffsetRotation(Transform thumbTransform)
        {
            return GetProximalRotationOffsetRotation(TryResolveThumbSide(thumbTransform, out bool isRightThumb) && isRightThumb);
        }

        private Quaternion GetProximalRotationOffsetRotation(bool isRightThumb)
        {
            Vector3 offset = GetProximalRotationOffset(isRightThumb);
            if (offset.sqrMagnitude <= 0.000001f)
            {
                return Quaternion.identity;
            }

            return Quaternion.Euler(offset);
        }

        private Vector3 GetProximalRotationOffset(bool isRightThumb)
        {
            Vector3 offset = proximalLocalRotationOffset;
            if (isRightThumb && mirrorRightProximalLocalRotationOffset)
            {
                offset = new Vector3(offset.x, -offset.y, -offset.z);
            }

            return offset + (isRightThumb ? rightProximalLocalRotationOffset : leftProximalLocalRotationOffset);
        }

        private static bool IsRightHumanThumbBone(HumanBodyBones thumbBone)
        {
            switch (thumbBone)
            {
                case HumanBodyBones.RightThumbProximal:
                case HumanBodyBones.RightThumbIntermediate:
                case HumanBodyBones.RightThumbDistal:
                    return true;
                default:
                    return false;
            }
        }

        private Quaternion GetCurrentRawLocalRotation(Transform targetTransform)
        {
            Quaternion currentRotation = targetTransform.localRotation;
            if (_lastCorrectedLocalRotations.TryGetValue(targetTransform, out Quaternion lastCorrectedRotation) &&
                _lastRawLocalRotations.TryGetValue(targetTransform, out Quaternion lastRawRotation) &&
                Quaternion.Angle(currentRotation, lastCorrectedRotation) <= 0.001f)
            {
                return lastRawRotation;
            }

            return currentRotation;
        }

        private void SetCorrectedLocalRotation(Transform targetTransform, Quaternion rawRotation, Quaternion correctedRotation)
        {
            _lastRawLocalRotations[targetTransform] = rawRotation;
            _lastCorrectedLocalRotations[targetTransform] = correctedRotation;
            targetTransform.localRotation = correctedRotation;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value) || value.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            normalized = value.normalized;
            return IsFinite(normalized);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion rotation)
        {
            return IsFinite(rotation.x) &&
                IsFinite(rotation.y) &&
                IsFinite(rotation.z) &&
                IsFinite(rotation.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
