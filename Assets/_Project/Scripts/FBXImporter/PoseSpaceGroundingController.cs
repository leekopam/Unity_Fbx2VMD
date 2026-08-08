using UnityEngine;
using Fbx2Vmd.Retargeting;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
        private sealed class PoseSpaceGroundingController
        {
            private const float DefaultFootRadius = 0.04f;
            private const float GroundingDirectionReversalStepScale = 0.4f;

            private readonly PoseSpaceRetargeter _retargeter;

            public PoseSpaceGroundingController(PoseSpaceRetargeter retargeter)
            {
                _retargeter = retargeter;
            }

            public void ApplyRaycastGrounding()
            {
                Transform lFoot = _retargeter.targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rFoot = _retargeter.targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);

                if (lFoot == null || rFoot == null) return;

                if (!_retargeter._hasEstimatedFootRadius)
                {
                    CalibrateTargetFootRadius();
                }

                float footRadius = GetEstimatedFootRadius();
                if (!TryCalculateFootBottomY(lFoot.position.y, footRadius, out float lBottom) ||
                    !TryCalculateFootBottomY(rFoot.position.y, footRadius, out float rBottom))
                {
                    _retargeter.LogPoseWarning("Foot position became non-finite. Skipping grounding for this frame.");
                    return;
                }

                float lowestFootCurrentY = Mathf.Min(lBottom, rBottom);
                float contactBottomY = ResolveGroundingContactBottomY(lowestFootCurrentY);

                float targetGroundY = 0.0f;
                float targetHeight = ResolveEditorFootHeightGroundingReferenceTarget(targetGroundY + _retargeter.groundOffset);
                _retargeter._lastGroundingTargetY = targetGroundY;
                _retargeter._lastGroundingLowestFootBottomY = contactBottomY;

                if (!GroundingStabilizer.TryCalculateAdjustment(targetHeight, contactBottomY, out float adjustment))
                {
                    _retargeter.LogPoseWarning("Grounding adjustment became non-finite. Skipping grounding for this frame.");
                    _retargeter._lastGroundingAdjustment = float.NaN;
                    return;
                }

                _retargeter._lastGroundingAdjustment = adjustment;
                _retargeter._maxGroundingAdjustment = Mathf.Max(_retargeter._maxGroundingAdjustment, Mathf.Abs(adjustment));

                Vector3 currentPos = _retargeter.targetAnimator.transform.position;
                if (!IsFinite(currentPos))
                {
                    _retargeter.LogPoseWarning("Target position became non-finite before grounding. Resetting to origin.");
                    currentPos = Vector3.zero;
                }

                if (_retargeter.freezeRootYAfterInitialGrounding && _retargeter._groundingInitialized && _retargeter._hasFrozenGroundingRootY)
                {
                    currentPos.y = _retargeter._frozenGroundingRootY;
                    if (IsFinite(currentPos))
                    {
                        _retargeter.targetAnimator.transform.position = currentPos;
                        ApplyGroundedFootLockXZ(lFoot, rFoot, targetHeight, footRadius);
                    }

                    _retargeter._lastGroundingVerticalStep = 0f;
                    return;
                }

                bool wasGroundingInitialized = _retargeter._groundingInitialized;
                float appliedVerticalStep = CalculateGroundingVerticalStep(
                    currentPos.y,
                    adjustment,
                    wasGroundingInitialized,
                    _retargeter.smoothGrounding,
                    _retargeter.groundingSmoothing,
                    _retargeter.maxGroundingVerticalStepPerFrame,
                    _retargeter.groundingDeadZone,
                    _retargeter._lastGroundingVerticalStep,
                    out bool skippedByDeadZone,
                    out bool smoothedGrounding,
                    out bool clampedGroundingStep);
                if (skippedByDeadZone)
                {
                    _retargeter._lastGroundingVerticalStep = 0f;
                    return;
                }

                if (!wasGroundingInitialized)
                {
                    _retargeter._groundingInitialized = true;
                }

                if (smoothedGrounding)
                {
                    _retargeter._groundingSmoothedCount++;
                }

                if (clampedGroundingStep)
                {
                    _retargeter._groundingStepClampedCount++;
                }

                float clampedNextY = currentPos.y + appliedVerticalStep;
                _retargeter._lastGroundingVerticalStep = appliedVerticalStep;
                _retargeter._maxGroundingVerticalStep = Mathf.Max(_retargeter._maxGroundingVerticalStep, Mathf.Abs(appliedVerticalStep));
                if (wasGroundingInitialized)
                {
                    _retargeter._maxGroundingVerticalStepAfterInitial = Mathf.Max(_retargeter._maxGroundingVerticalStepAfterInitial, Mathf.Abs(appliedVerticalStep));
                }
                else
                {
                    _retargeter._initialGroundingVerticalStep = appliedVerticalStep;
                }

                currentPos.y = clampedNextY;

                if (IsFinite(currentPos))
                {
                    _retargeter.targetAnimator.transform.position = currentPos;
                    if (_retargeter.freezeRootYAfterInitialGrounding && !_retargeter._hasFrozenGroundingRootY)
                    {
                        _retargeter._frozenGroundingRootY = currentPos.y;
                        _retargeter._hasFrozenGroundingRootY = true;
                    }

                    ApplyGroundedFootLockXZ(lFoot, rFoot, targetHeight, footRadius);
                }
            }

            public void ApplyLateVisualGroundingCorrection()
            {
                try
                {
                    if (!_retargeter._isInitialized || !_retargeter.useSmartGrounding || !_retargeter.enableLateVisualGroundingCorrection || _retargeter.targetAnimator == null)
                    {
                        return;
                    }

                    if (_retargeter.freezeRootYAfterInitialGrounding && _retargeter._groundingInitialized && _retargeter._hasFrozenGroundingRootY)
                    {
                        Vector3 frozenPos = _retargeter.targetAnimator.transform.position;
                        frozenPos.y = _retargeter._frozenGroundingRootY;
                        if (IsFinite(frozenPos))
                        {
                            _retargeter.targetAnimator.transform.position = frozenPos;
                        }

                        _retargeter._lateVisualGroundingInitialized = true;
                        _retargeter._lastGroundingVerticalStep = 0f;
                        return;
                    }

                    if (!TryGetLowestFootBottomY(out float lowestFootBottomY))
                    {
                        return;
                    }

                    float rendererMinY = ResolveGroundingContactBottomY(lowestFootBottomY);

                    float targetGroundY = 0.0f;
                    float targetHeight = ResolveEditorFootHeightGroundingReferenceTarget(targetGroundY + _retargeter.groundOffset);
                    _retargeter._lastGroundingTargetY = targetGroundY;
                    _retargeter._lastGroundingLowestFootBottomY = rendererMinY;

                    if (!GroundingStabilizer.TryCalculateAdjustment(targetHeight, rendererMinY, out float residual))
                    {
                        _retargeter.LogPoseWarning("Late visual grounding residual became non-finite. Skipping final grounding for this frame.");
                        return;
                    }

                    if (ShouldSkipLateVisualGroundingForActiveVerticalStep(
                        residual,
                        _retargeter.smoothLateVisualGroundingCorrection,
                        _retargeter._lastGroundingVerticalStep))
                    {
                        _retargeter._lateVisualGroundingInitialized = true;
                        return;
                    }

                    if (!TryCalculateLateVisualGroundingEffectiveResidual(
                        residual,
                        _retargeter.smoothLateVisualGroundingCorrection,
                        _retargeter.groundingDeadZone,
                        _retargeter.maxLateVisualGroundingCorrection,
                        out float effectiveResidual,
                        out bool exceededMaxCorrection))
                    {
                        if (exceededMaxCorrection && !_retargeter._lateVisualGroundingWarningLogged)
                        {
                            float maxCorrection = Mathf.Max(0.001f, _retargeter.maxLateVisualGroundingCorrection);
                            Debug.LogWarning($"[PoseSpaceRetargeter] Late visual grounding residual {residual:F3}m exceeded max {maxCorrection:F3}m. Skipping this frame to avoid collapsing a real jump.");
                            _retargeter._lateVisualGroundingWarningLogged = true;
                        }

                        _retargeter._lateVisualGroundingInitialized = true;
                        return;
                    }

                    Vector3 currentPos = _retargeter.targetAnimator.transform.position;
                    if (!IsFinite(currentPos))
                    {
                        _retargeter.LogPoseWarning("Target position became non-finite before late visual grounding. Skipping final grounding for this frame.");
                        return;
                    }

                    float appliedResidual = CalculateLateVisualGroundingStep(effectiveResidual);
                    if (Mathf.Abs(appliedResidual) <= 0.000001f)
                    {
                        return;
                    }

                    if (!TryCalculateLateVisualGroundingAppliedPosition(currentPos, appliedResidual, out Vector3 appliedPosition))
                    {
                        _retargeter.LogPoseWarning("Target position became non-finite after late visual grounding. Skipping final grounding for this frame.");
                        return;
                    }

                    _retargeter.targetAnimator.transform.position = appliedPosition;
                    _retargeter._lateVisualGroundingInitialized = true;

                    _retargeter._lastGroundingAdjustment = appliedResidual;
                    _retargeter._maxGroundingAdjustment = Mathf.Max(_retargeter._maxGroundingAdjustment, Mathf.Abs(appliedResidual));
                    _retargeter._lastGroundingVerticalStep = appliedResidual;
                    _retargeter._maxGroundingVerticalStep = Mathf.Max(_retargeter._maxGroundingVerticalStep, Mathf.Abs(appliedResidual));
                    if (_retargeter._groundingInitialized)
                    {
                        _retargeter._maxGroundingVerticalStepAfterInitial = Mathf.Max(_retargeter._maxGroundingVerticalStepAfterInitial, Mathf.Abs(appliedResidual));
                    }
                    else
                    {
                        _retargeter._groundingInitialized = true;
                        _retargeter._initialGroundingVerticalStep = appliedResidual;
                    }
                }
                finally
                {
                    if (_retargeter.targetAnimator != null)
                    {
                        _retargeter._lastRetargetStageAfterLateVisualGroundingEndpointPositions = _retargeter.Diagnostics.CaptureEndpointStageWorldPositions(_retargeter.targetAnimator);
                        _retargeter.Diagnostics.CaptureRetargetEndpointStageAttributionDiagnostics();
                    }
                }
            }

            // --- Private instance helpers ---

            private void ApplyGroundedFootLockXZ(Transform leftFoot, Transform rightFoot, float targetHeight, float footRadius)
            {
                if (!_retargeter.ShouldStabilizeGroundedFootXZ || _retargeter.groundedFootLockWeight <= 0f || _retargeter.targetAnimator == null)
                {
                    _retargeter._leftFootLocked = false;
                    _retargeter._rightFootLocked = false;
                    return;
                }

                Vector3 correctionSum = Vector3.zero;
                int correctionCount = 0;
                AddFootLockCorrection(leftFoot, targetHeight, footRadius, ref _retargeter._leftFootLocked, ref _retargeter._leftFootLockPosition, ref correctionSum, ref correctionCount);
                AddFootLockCorrection(rightFoot, targetHeight, footRadius, ref _retargeter._rightFootLocked, ref _retargeter._rightFootLockPosition, ref correctionSum, ref correctionCount);
                if (!TryCalculateGroundedFootLockRootCorrection(
                    correctionSum,
                    correctionCount,
                    _retargeter.groundedFootLockWeight,
                    _retargeter.maxGroundedFootLockStep,
                    out Vector3 correction))
                {
                    return;
                }

                Vector3 rootPosition = _retargeter.targetAnimator.transform.position + correction;
                if (IsFinite(rootPosition))
                {
                    _retargeter.targetAnimator.transform.position = rootPosition;
                }
            }

            private float ResolveEditorFootHeightGroundingReferenceTarget(float baseTargetHeight)
            {
#if UNITY_EDITOR
                _retargeter._lastEditorFootHeightGroundingReferenceLift = 0f;
                if (!_retargeter.ShouldUseManualAnimatorFootHeightGroundingReference ||
                    !_retargeter._allowEditorFootHeightGroundingReference ||
                    _retargeter.manualAnimatorFootHeightGroundingReferenceWeight <= 0f ||
                    _retargeter._editorFingerReferenceAnimator == null)
                {
                    return baseTargetHeight;
                }

                if (!_retargeter.UpdateEditorManualReferenceAnimator() ||
                    !TryGetAnimatorLowestFootY(_retargeter._editorFingerReferenceAnimator, out float referenceCurrentLowestFootY))
                {
                    return baseTargetHeight;
                }

                if (!_retargeter._hasEditorReferenceLowestFootRestY)
                {
                    _retargeter._editorReferenceLowestFootRestY = referenceCurrentLowestFootY;
                    _retargeter._hasEditorReferenceLowestFootRestY = true;
                    return baseTargetHeight;
                }

                if (TryCalculateEditorFootHeightGroundingReferenceTarget(
                        baseTargetHeight,
                        referenceCurrentLowestFootY,
                        _retargeter._editorReferenceLowestFootRestY,
                        _retargeter.manualAnimatorFootHeightGroundingReferenceWeight,
                        _retargeter.manualAnimatorFootHeightGroundingReferenceMaxLift,
                        out float targetHeight))
                {
                    _retargeter._lastEditorFootHeightGroundingReferenceLift = targetHeight - baseTargetHeight;
                    return targetHeight;
                }

                _retargeter._lastEditorFootHeightGroundingReferenceLift = float.NaN;
                return baseTargetHeight;
#else
                return baseTargetHeight;
#endif
            }

            private float CalculateLateVisualGroundingStep(float residual)
            {
                return CalculateLateVisualGroundingStep(
                    residual,
                    _retargeter.smoothLateVisualGroundingCorrection,
                    _retargeter._lateVisualGroundingInitialized,
                    _retargeter.lateVisualGroundingSnapThreshold,
                    _retargeter.lateVisualGroundingSmoothing,
                    _retargeter.maxLateVisualGroundingStepPerFrame);
            }

            private bool TryGetLowestFootBottomY(out float lowestFootBottomY)
            {
                lowestFootBottomY = 0f;
                if (_retargeter.targetAnimator == null)
                {
                    return false;
                }

                Transform leftFoot = _retargeter.targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = _retargeter.targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (leftFoot == null || rightFoot == null)
                {
                    return false;
                }

                float footRadius = GetEstimatedFootRadius();
                return TryCalculateLowestFootBottomY(leftFoot.position.y, rightFoot.position.y, footRadius, out lowestFootBottomY);
            }

            internal void CalibrateTargetFootRadius()
            {
                _retargeter._hasEstimatedFootRadius = false;
                _retargeter._estimatedFootRadius = DefaultFootRadius;
                if (_retargeter.targetAnimator == null || !TryGetRendererBoundsMinY(out float rendererMinY))
                {
                    return;
                }

                Transform leftFoot = _retargeter.targetAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = _retargeter.targetAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (leftFoot == null || rightFoot == null)
                {
                    return;
                }

                if (!TryCalculateEstimatedFootRadius(leftFoot.position.y, rightFoot.position.y, rendererMinY, out float estimatedRadius))
                {
                    return;
                }

                _retargeter._estimatedFootRadius = estimatedRadius;
                _retargeter._hasEstimatedFootRadius = true;
            }

            private float GetEstimatedFootRadius()
            {
                return _retargeter._hasEstimatedFootRadius ? _retargeter._estimatedFootRadius : DefaultFootRadius;
            }

            private float ResolveGroundingContactBottomY(float lowestFootBottomY)
            {
                bool hasRendererBounds = TryGetRendererBoundsMinY(out float rendererMinY);
                float contactBottomY = ResolveGroundingContactBottomY(
                    lowestFootBottomY,
                    _retargeter._hasEstimatedFootRadius,
                    hasRendererBounds,
                    rendererMinY,
                    _retargeter.rejectRendererGroundingOutliers,
                    _retargeter.maxRendererFootGroundingSeparation,
                    out bool rendererGroundingOutlier);

                if (rendererGroundingOutlier && !_retargeter._rendererGroundingOutlierWarningLogged)
                {
                    float separation = Mathf.Abs(rendererMinY - lowestFootBottomY);
                    float maxSeparation = Mathf.Max(0.02f, _retargeter.maxRendererFootGroundingSeparation);
                    Debug.LogWarning($"[PoseSpaceRetargeter] Renderer bounds grounding outlier ignored. rendererMinY={rendererMinY:F3}, footBottomY={lowestFootBottomY:F3}, separation={separation:F3}, limit={maxSeparation:F3}");
                    _retargeter._rendererGroundingOutlierWarningLogged = true;
                }

                return contactBottomY;
            }

            private void AddFootLockCorrection(
                Transform foot,
                float targetHeight,
                float footRadius,
                ref bool locked,
                ref Vector3 lockPosition,
                ref Vector3 correctionSum,
                ref int correctionCount)
            {
                if (foot == null)
                {
                    locked = false;
                    return;
                }

                if (!TryCalculateFootBottomY(foot.position.y, footRadius, out float bottomY))
                {
                    locked = false;
                    return;
                }

                bool shouldAccumulate = TryCalculateFootLockCorrection(
                    bottomY,
                    foot.position,
                    targetHeight,
                    locked,
                    lockPosition,
                    out bool nextLocked,
                    out Vector3 nextLockPosition,
                    out Vector3 correction);
                locked = nextLocked;
                lockPosition = nextLockPosition;
                if (!shouldAccumulate)
                {
                    return;
                }

                correctionSum += correction;
                correctionCount++;
            }

            private bool TryGetRendererBoundsMinY(out float minY)
            {
                minY = float.NaN;
                if (_retargeter.targetAnimator == null)
                {
                    return false;
                }

                Renderer[] renderers = _retargeter.targetAnimator.GetComponentsInChildren<Renderer>(true);
                bool hasBounds = false;
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || !renderer.enabled || renderer.bounds.size.sqrMagnitude <= 0f)
                    {
                        continue;
                    }

                    if (!IsFinite(renderer.bounds.min.y))
                    {
                        continue;
                    }

                    minY = hasBounds ? Mathf.Min(minY, renderer.bounds.min.y) : renderer.bounds.min.y;
                    hasBounds = true;
                }

                return hasBounds;
            }

            // --- Static helpers ---

            private static bool TryCalculateEditorFootHeightGroundingReferenceTarget(
                float baseTargetHeight,
                float referenceCurrentLowestFootY,
                float referenceRestLowestFootY,
                float weight,
                float maxLift,
                out float targetHeight)
            {
                targetHeight = baseTargetHeight;
                if (!IsFinite(baseTargetHeight) ||
                    !IsFinite(referenceCurrentLowestFootY) ||
                    !IsFinite(referenceRestLowestFootY) ||
                    !IsFinite(weight) ||
                    !IsFinite(maxLift))
                {
                    return false;
                }

                float referenceLift = referenceCurrentLowestFootY - referenceRestLowestFootY;
                if (referenceLift <= 0f)
                {
                    return true;
                }

                float weightedLift = referenceLift * Mathf.Clamp01(weight);
                if (maxLift > 0f)
                {
                    weightedLift = Mathf.Min(weightedLift, maxLift);
                }

                targetHeight = baseTargetHeight + weightedLift;
                if (!IsFinite(targetHeight))
                {
                    targetHeight = baseTargetHeight;
                    return false;
                }

                return true;
            }

            private static float CalculateGroundingVerticalStep(
                float currentY,
                float adjustment,
                bool wasGroundingInitialized,
                bool smoothGrounding,
                float groundingSmoothing,
                float maxGroundingVerticalStepPerFrame,
                float groundingDeadZone,
                float previousGroundingVerticalStep,
                out bool skippedByDeadZone,
                out bool smoothed,
                out bool clamped)
            {
                skippedByDeadZone = false;
                smoothed = false;
                clamped = false;

                float deadZone = Mathf.Max(0f, groundingDeadZone);
                if (wasGroundingInitialized && Mathf.Abs(adjustment) <= deadZone)
                {
                    skippedByDeadZone = true;
                    return 0f;
                }

                float effectiveAdjustment = adjustment;
                if (wasGroundingInitialized && deadZone > 0f)
                {
                    effectiveAdjustment = Mathf.Sign(adjustment) * Mathf.Max(0f, Mathf.Abs(adjustment) - deadZone);
                }

                float desiredY = currentY + effectiveAdjustment;
                float nextY = desiredY;
                if (wasGroundingInitialized && smoothGrounding)
                {
                    float smoothing = Mathf.Clamp01(groundingSmoothing);
                    if (smoothing < 1f)
                    {
                        nextY = Mathf.Lerp(currentY, desiredY, smoothing);
                        smoothed = true;
                    }

                    float maxStep = Mathf.Max(0.001f, maxGroundingVerticalStepPerFrame);
                    float verticalStep = nextY - currentY;
                    if (IsGroundingDirectionReversal(verticalStep, previousGroundingVerticalStep))
                    {
                        maxStep = Mathf.Max(0.001f, maxStep * GroundingDirectionReversalStepScale);
                    }

                    if (Mathf.Abs(verticalStep) > maxStep)
                    {
                        nextY = currentY + Mathf.Sign(verticalStep) * maxStep;
                        clamped = true;
                    }
                }

                return nextY - currentY;
            }

            private static bool IsGroundingDirectionReversal(float verticalStep, float previousGroundingVerticalStep)
            {
                if (!IsFinite(previousGroundingVerticalStep) || Mathf.Abs(verticalStep) <= 0.0005f || Mathf.Abs(previousGroundingVerticalStep) <= 0.0005f)
                {
                    return false;
                }

                return Mathf.Sign(verticalStep) != Mathf.Sign(previousGroundingVerticalStep);
            }

            private static bool TryCalculateLateVisualGroundingEffectiveResidual(
                float residual,
                bool smoothLateVisualGroundingCorrection,
                float groundingDeadZone,
                float maxLateVisualGroundingCorrection,
                out float effectiveResidual,
                out bool exceededMaxCorrection)
            {
                effectiveResidual = 0f;
                exceededMaxCorrection = false;

                bool isPenetrationResidual = residual > 0.0001f;
                bool isFloatingResidual = residual < -0.0001f;
                bool isVisualFloorResidual = isPenetrationResidual || isFloatingResidual;
                float deadZone = Mathf.Max(0.001f, groundingDeadZone);
                float skipDeadZone = isVisualFloorResidual ? 0.001f : deadZone;
                if (Mathf.Abs(residual) <= skipDeadZone)
                {
                    return false;
                }

                float maxCorrection = Mathf.Max(0.001f, maxLateVisualGroundingCorrection);
                if (Mathf.Abs(residual) > maxCorrection)
                {
                    exceededMaxCorrection = true;
                    return false;
                }

                effectiveResidual = residual;
                if (smoothLateVisualGroundingCorrection && deadZone > 0f && !isVisualFloorResidual)
                {
                    effectiveResidual = Mathf.Sign(residual) * Mathf.Max(0f, Mathf.Abs(residual) - deadZone);
                    if (Mathf.Abs(effectiveResidual) <= 0.0001f)
                    {
                        effectiveResidual = 0f;
                        return false;
                    }
                }

                return true;
            }

            private static bool ShouldSkipLateVisualGroundingForActiveVerticalStep(
                float residual,
                bool smoothLateVisualGroundingCorrection,
                float lastGroundingVerticalStep)
            {
                if (!smoothLateVisualGroundingCorrection ||
                    !IsFinite(residual) ||
                    !IsFinite(lastGroundingVerticalStep) ||
                    Mathf.Abs(residual) <= 0.0005f ||
                    Mathf.Abs(lastGroundingVerticalStep) <= 0.0005f)
                {
                    return false;
                }

                return Mathf.Sign(residual) != Mathf.Sign(lastGroundingVerticalStep);
            }

            private static bool TryCalculateLateVisualGroundingAppliedPosition(
                Vector3 currentPosition,
                float appliedResidual,
                out Vector3 appliedPosition)
            {
                appliedPosition = Vector3.zero;
                if (!IsFinite(currentPosition))
                {
                    return false;
                }

                appliedPosition = currentPosition;
                appliedPosition.y += appliedResidual;
                if (!IsFinite(appliedPosition))
                {
                    appliedPosition = Vector3.zero;
                    return false;
                }

                return true;
            }

            private static float CalculateLateVisualGroundingStep(
                float residual,
                bool smoothLateVisualGroundingCorrection,
                bool lateVisualGroundingInitialized,
                float lateVisualGroundingSnapThreshold,
                float lateVisualGroundingSmoothing,
                float maxLateVisualGroundingStepPerFrame)
            {
                if (!smoothLateVisualGroundingCorrection)
                {
                    return residual;
                }

                if (!lateVisualGroundingInitialized)
                {
                    return residual;
                }

                float snapThreshold = Mathf.Max(0.005f, lateVisualGroundingSnapThreshold);
                if (residual > 0.0001f && residual <= snapThreshold)
                {
                    return residual;
                }

                bool isFloorPenetration = residual > 0.0001f;
                float smoothing = Mathf.Clamp01(lateVisualGroundingSmoothing);
                if (isFloorPenetration)
                {
                    smoothing = Mathf.Max(smoothing, LateVisualGroundingPenetrationRecoverySmoothing);
                }

                float step = Mathf.Abs(residual) > snapThreshold
                    ? residual * Mathf.Max(0.1f, smoothing)
                    : residual * smoothing;
                float maxStep = Mathf.Max(0.001f, maxLateVisualGroundingStepPerFrame);
                if (isFloorPenetration)
                {
                    maxStep = Mathf.Max(maxStep, LateVisualGroundingPenetrationRecoveryMaxStep);
                }

                if (Mathf.Abs(step) > maxStep)
                {
                    step = Mathf.Sign(step) * maxStep;
                }

                return step;
            }

            private static bool TryGetAnimatorLowestFootY(Animator animator, out float lowestFootY)
            {
                lowestFootY = 0f;
                if (animator == null || !animator.isHuman)
                {
                    return false;
                }

                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (leftFoot == null || rightFoot == null)
                {
                    return false;
                }

                Vector3 leftLocal = animator.transform.InverseTransformPoint(leftFoot.position);
                Vector3 rightLocal = animator.transform.InverseTransformPoint(rightFoot.position);
                lowestFootY = Mathf.Min(leftLocal.y, rightLocal.y);
                if (!IsFinite(lowestFootY))
                {
                    lowestFootY = 0f;
                    return false;
                }

                return true;
            }

            private static bool TryCalculateLowestFootBottomY(
                float leftFootY,
                float rightFootY,
                float footRadius,
                out float lowestFootBottomY)
            {
                lowestFootBottomY = 0f;
                if (!TryCalculateFootBottomY(leftFootY, footRadius, out float leftBottom) ||
                    !TryCalculateFootBottomY(rightFootY, footRadius, out float rightBottom))
                {
                    return false;
                }

                lowestFootBottomY = Mathf.Min(leftBottom, rightBottom);
                return true;
            }

            private static bool TryCalculateFootBottomY(
                float footY,
                float footRadius,
                out float footBottomY)
            {
                footBottomY = footY - footRadius;
                if (!IsFinite(footBottomY))
                {
                    footBottomY = 0f;
                    return false;
                }

                return true;
            }

            private static bool TryCalculateEstimatedFootRadius(
                float leftFootY,
                float rightFootY,
                float rendererMinY,
                out float estimatedRadius)
            {
                float lowestFootY = Mathf.Min(leftFootY, rightFootY);
                estimatedRadius = lowestFootY - rendererMinY;
                if (!IsFinite(estimatedRadius))
                {
                    return false;
                }

                estimatedRadius = Mathf.Clamp(estimatedRadius, 0.02f, 0.16f);
                return true;
            }

            private static float ResolveGroundingContactBottomY(
                float lowestFootBottomY,
                bool hasEstimatedFootRadius,
                bool hasRendererBounds,
                float rendererMinY,
                bool rejectRendererGroundingOutliers,
                float maxRendererFootGroundingSeparation,
                out bool rendererGroundingOutlier)
            {
                return ResolveGroundingContactBottomY(
                    lowestFootBottomY,
                    hasRendererBounds,
                    rendererMinY,
                    rejectRendererGroundingOutliers,
                    maxRendererFootGroundingSeparation,
                    out rendererGroundingOutlier);
            }

            private static float ResolveGroundingContactBottomY(
                float lowestFootBottomY,
                bool hasRendererBounds,
                float rendererMinY,
                bool rejectRendererGroundingOutliers,
                float maxRendererFootGroundingSeparation,
                out bool rendererGroundingOutlier)
            {
                rendererGroundingOutlier = false;
                if (!hasRendererBounds)
                {
                    return lowestFootBottomY;
                }

                if (!rejectRendererGroundingOutliers)
                {
                    return rendererMinY;
                }

                float separation = Mathf.Abs(rendererMinY - lowestFootBottomY);
                float maxSeparation = Mathf.Max(0.02f, maxRendererFootGroundingSeparation);
                if (separation <= maxSeparation)
                {
                    return rendererMinY;
                }

                rendererGroundingOutlier = true;
                return lowestFootBottomY;
            }

            private static bool TryCalculateGroundedFootLockRootCorrection(
                Vector3 correctionSum,
                int correctionCount,
                float groundedFootLockWeight,
                float maxGroundedFootLockStep,
                out Vector3 correction)
            {
                correction = Vector3.zero;
                if (correctionCount <= 0)
                {
                    return false;
                }

                correction = correctionSum / correctionCount;
                correction.y = 0f;
                correction *= Mathf.Clamp01(groundedFootLockWeight);

                float maxStep = Mathf.Max(0.001f, maxGroundedFootLockStep);
                if (correction.magnitude > maxStep)
                {
                    correction = correction.normalized * maxStep;
                }

                return IsFinite(correction) && correction.sqrMagnitude > 0.00000001f;
            }

            private static bool TryCalculateFootLockCorrection(
                float bottomY,
                Vector3 footPosition,
                float targetHeight,
                bool locked,
                Vector3 lockPosition,
                out bool nextLocked,
                out Vector3 nextLockPosition,
                out Vector3 correction)
            {
                const float contactHeight = 0.08f;
                const float releaseHeight = 0.14f;
                const float resetDistance = 0.25f;

                nextLocked = locked;
                nextLockPosition = lockPosition;
                correction = Vector3.zero;

                if (!IsFinite(bottomY))
                {
                    nextLocked = false;
                    return false;
                }

                if (bottomY > targetHeight + releaseHeight)
                {
                    nextLocked = false;
                    return false;
                }

                footPosition.y = 0f;
                if (!IsFinite(footPosition))
                {
                    nextLocked = false;
                    return false;
                }

                if (!locked || bottomY > targetHeight + contactHeight)
                {
                    nextLockPosition = footPosition;
                    nextLocked = bottomY <= targetHeight + contactHeight;
                    return false;
                }

                correction = lockPosition - footPosition;
                correction.y = 0f;
                if (!IsFinite(correction))
                {
                    nextLocked = false;
                    return false;
                }

                if (correction.magnitude > resetDistance)
                {
                    nextLockPosition = footPosition;
                    correction = Vector3.zero;
                }

                return true;
            }
        }
    }
}
