using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
        private sealed class PoseSpaceRootController
        {
            private readonly PoseSpaceRetargeter _retargeter;
            private Vector3 _bodyRootDelta;
            private Vector3 _rootMotionCarrierPositionBeforePose;

            public PoseSpaceRootController(PoseSpaceRetargeter retargeter)
            {
                _retargeter = retargeter;
            }

            // ── Public entry points ──

            public void ComputeScaleRatio()
            {
                Transform ghostHip = _retargeter.ghostAnimator.GetBoneTransform(HumanBodyBones.Hips);
                Transform targetHip = _retargeter.targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
                _retargeter._scaleRatio = CalculateSafeScaleRatio(ghostHip, targetHip);
            }

            public bool ProcessBodyPosition(ref HumanPose humanPose)
            {
                // Y축은 target 기준으로 안정화하고, X/Z 체중 이동은 FBX 값을 유지한다.
                Vector3 bodyPos = humanPose.bodyPosition;
                bodyPos.x *= _retargeter._scaleRatio;
                bodyPos.z *= _retargeter._scaleRatio;
                Vector3 bodyRootMotionSource = bodyPos;
#if UNITY_EDITOR
                bodyRootMotionSource = SelectBodyPositionRootMotionSource(
                    bodyPos,
                    _retargeter._editorReferenceBodyPosition,
                    _retargeter._hasEditorReferenceBodyPosition,
                    _retargeter.ShouldUseManualAnimatorBodyRotationReference);
#endif
                _bodyRootDelta = ExtractBodyPositionXZRootDelta(bodyRootMotionSource);
                if (_retargeter.preserveTargetBodyPosition && _retargeter._hasTargetReferenceBodyPosition)
                {
                    bodyPos = _retargeter._targetReferenceBodyPosition;
                    // 수동 기준 Animator의 bodyPos.y로 Y를 대체: ghost Legacy bodyPos 스파이크 없이 애니메이션 높이를 따른다.
                    if (_retargeter.ShouldUseManualAnimatorBodyPositionYReference && _retargeter._hasEditorReferenceBodyPosition)
                    {
                        bodyPos.y = _retargeter._editorReferenceBodyPosition.y;
                    }
                }
                else
                {
                    bodyPos.y *= _retargeter._scaleRatio;
                }
#if UNITY_EDITOR
                float manualBodyPositionXzFrameGateWeight =
                    _retargeter.ResolveManualAnimatorBodyPositionXzFrameGateWeight();
                if (_retargeter.ShouldUseManualAnimatorBodyPositionXzReference &&
                    manualBodyPositionXzFrameGateWeight > 0f &&
                    _retargeter._hasEditorReferenceBodyPosition &&
                    TryCalculateManualAnimatorBodyPositionXzReference(
                        bodyPos,
                        _retargeter._editorReferenceBodyPosition,
                        _retargeter.manualAnimatorBodyPositionXzReferenceWeight * manualBodyPositionXzFrameGateWeight,
                        _retargeter.manualAnimatorBodyPositionXzReferenceMaxOffset,
                        _retargeter.manualAnimatorBodyPositionXzReferenceAxisXScale,
                        _retargeter.manualAnimatorBodyPositionXzReferenceAxisZScale,
                        out Vector3 manualBodyPositionXz))
                {
                    bodyPos = manualBodyPositionXz;
                    if (!_retargeter._editorBodyPositionXzReferenceLogged)
                    {
                        Debug.Log(
                            $"[PoseSpaceRetargeter] Manual Animator bodyPosition X/Z reference applied. " +
                            $"weight={_retargeter.manualAnimatorBodyPositionXzReferenceWeight:F2}, " +
                            $"maxOffset={_retargeter.manualAnimatorBodyPositionXzReferenceMaxOffset:F3}m, " +
                            $"frameGate={_retargeter.manualAnimatorBodyPositionXzReferenceFrameGateStart:F0}-{_retargeter.manualAnimatorBodyPositionXzReferenceFrameGateEnd:F0}, " +
                            $"blendFrames={_retargeter.manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames:F0}, " +
                            $"axisScale={_retargeter.manualAnimatorBodyPositionXzReferenceAxisXScale:F2}/{_retargeter.manualAnimatorBodyPositionXzReferenceAxisZScale:F2}");
                        _retargeter._editorBodyPositionXzReferenceLogged = true;
                    }
                }
#endif
                if (!IsFinite(bodyPos))
                {
                    _retargeter.LogPoseWarning("Retarget body position became non-finite. Skipping this retarget frame.");
                    return false;
                }
                humanPose.bodyPosition = bodyPos;
                return true;
            }

            public void BeginSetHumanPoseRoot()
            {
                _rootMotionCarrierPositionBeforePose = _retargeter.targetAnimator.transform.position;
                Vector3 poseSolveRootPosition = SelectPoseSolveRootPosition(
                    _rootMotionCarrierPositionBeforePose,
                    _retargeter._hasTargetRootPoseGuardAnchorPosition
                        ? _retargeter._targetRootPoseGuardAnchorPosition
                        : _rootMotionCarrierPositionBeforePose,
                    _retargeter.useBodyPositionXZRootMotion);
                if (IsFinite(poseSolveRootPosition))
                {
                    _retargeter.targetAnimator.transform.position = poseSolveRootPosition;
                }
            }

            public void EndSetHumanPoseRoot(Vector3 targetPositionBeforePose)
            {
                ClampTargetRootPositionSpike(targetPositionBeforePose, "SetHumanPose");
                Vector3 implicitRootGuardReference = SelectImplicitRootGuardReference(
                    _retargeter._hasTargetRootPoseGuardAnchorPosition
                        ? _retargeter._targetRootPoseGuardAnchorPosition
                        : targetPositionBeforePose,
                    targetPositionBeforePose,
                    _retargeter._movementScaleMultiplier);
                _retargeter.targetAnimator.transform.position = ApplyImplicitBodyPositionRootGuard(
                    implicitRootGuardReference,
                    _retargeter.targetAnimator.transform.position,
                    _retargeter.useBodyPositionXZRootMotion,
                    _bodyRootDelta);
                _retargeter.targetAnimator.transform.position = RestoreRootMotionCarrierPositionAfterPose(
                    _rootMotionCarrierPositionBeforePose,
                    _retargeter.targetAnimator.transform.position,
                    _retargeter.useBodyPositionXZRootMotion);
            }

            public void ApplyRootDelta()
            {
                // 루트 모션 동기화 (호 그리기 방지)
                // Ghost 이동량 계산
                Vector3 ghostDelta = _retargeter.ghostAnimator.transform.position - _retargeter._prevGhostPos;
                Vector3 editorRootTranslationDelta = ExtractEditorRootTranslationDelta(ghostDelta);

                // 내 캐릭터 크기에 맞춰 이동량 스케일링
                Vector3 targetDelta = CalculateRetargetRootDelta(
                    ghostDelta,
                    _retargeter._scaleRatio,
                    editorRootTranslationDelta,
                    _bodyRootDelta,
                    _retargeter._movementScaleMultiplier,
                    _retargeter.useBodyPositionXZRootMotion,
                    _retargeter.clampRootDeltaSpikes,
                    _retargeter.maxRootDeltaPerFrame,
                    out float targetDeltaMagnitude,
                    out bool skippedByNonFinite,
                    out bool limitedBySpike);
                if (skippedByNonFinite)
                {
                    _retargeter.LogPoseWarning("Retarget root delta became non-finite. Skipping root motion for this frame.");
                    _retargeter._lastRootDeltaMagnitude = float.NaN;
                    _retargeter._rootDeltaSpikeSkippedCount++;
                }
                else
                {
                    _retargeter._lastRootDeltaMagnitude = targetDeltaMagnitude;
                    _retargeter._maxRootDeltaMagnitude = Mathf.Max(_retargeter._maxRootDeltaMagnitude, _retargeter._lastRootDeltaMagnitude);

                    if (limitedBySpike)
                    {
                        _retargeter._rootDeltaSpikeSkippedCount++;
                        if (_retargeter.logRootDeltaSpikes && !_retargeter._rootDeltaSpikeWarningLogged)
                        {
                            Debug.LogWarning($"[PoseSpaceRetargeter] Root delta spike {_retargeter._lastRootDeltaMagnitude:F3}m limited. ghostDelta={ghostDelta.magnitude:F3}m, editorRootDelta={editorRootTranslationDelta.magnitude:F3}m, limit={_retargeter.maxRootDeltaPerFrame:F3}m");
                            _retargeter._rootDeltaSpikeWarningLogged = true;
                        }
                    }
                }

                // 이동 적용
                _retargeter.targetAnimator.transform.position += targetDelta;

                // 위치 갱신
                _retargeter._prevGhostPos = _retargeter.ghostAnimator.transform.position;
            }

            // ── Private instance helpers ──

            private float CalculateSafeScaleRatio(Transform ghostHip, Transform targetHip)
            {
                bool hasAnimatorScale = _retargeter.ghostAnimator != null && _retargeter.targetAnimator != null;
                float ghostHumanScale = hasAnimatorScale ? _retargeter.ghostAnimator.humanScale : 0f;
                float targetHumanScale = hasAnimatorScale ? _retargeter.targetAnimator.humanScale : 0f;
                bool hasHipPositions = ghostHip != null && targetHip != null;
                float ghostHipY = hasHipPositions ? ghostHip.position.y : 0f;
                float targetHipY = hasHipPositions ? targetHip.position.y : 0f;

                float ratio = CalculateSafeScaleRatio(
                    _retargeter._scaleRatio,
                    hasAnimatorScale,
                    ghostHumanScale,
                    targetHumanScale,
                    _retargeter._initialGhostHipHeight,
                    _retargeter._initialTargetHipHeight,
                    hasHipPositions,
                    ghostHipY,
                    targetHipY,
                    out bool usedInvalidFallback);
                if (usedInvalidFallback)
                {
                    _retargeter.LogPoseWarning("Invalid retarget scale ratio. Falling back to 1.0.");
                }

                return ratio;
            }

            private Vector3 ExtractBodyPositionXZRootDelta(Vector3 bodyPosition)
            {
                if (!_retargeter.useBodyPositionXZRootMotion || !_retargeter._hasTargetReferenceBodyPosition)
                {
                    _retargeter._hasPreviousBodyRootMotionPosition = false;
                    return Vector3.zero;
                }

                Vector3 current = new Vector3(bodyPosition.x, 0f, bodyPosition.z);
                if (!IsFinite(current))
                {
                    _retargeter._hasPreviousBodyRootMotionPosition = false;
                    return Vector3.zero;
                }

                if (!_retargeter._hasPreviousBodyRootMotionPosition)
                {
                    _retargeter._previousBodyRootMotionPosition = current;
                    _retargeter._hasPreviousBodyRootMotionPosition = true;
                    return Vector3.zero;
                }

                Vector3 delta = current - _retargeter._previousBodyRootMotionPosition;
                _retargeter._previousBodyRootMotionPosition = current;
                if (!IsFinite(delta))
                {
                    return Vector3.zero;
                }

                delta.y = 0f;
                return delta;
            }

            private Vector3 ExtractEditorRootTranslationDelta(Vector3 ghostDelta)
            {
#if UNITY_EDITOR
                if (!_retargeter.ShouldUseEditorHumanoidRootTranslationReference ||
                    !_retargeter._useEditorRootTranslationReference ||
                    _retargeter._editorRootTranslationX == null ||
                    _retargeter._editorRootTranslationZ == null)
                {
                    _retargeter.ResetEditorHumanoidRootTranslationReferenceState();
                    return Vector3.zero;
                }

                float time = _retargeter.GetLegacyAnimationTime();
                Vector3 current = SampleEditorRootTranslation(time);
                if (!IsFinite(current))
                {
                    _retargeter.ResetEditorHumanoidRootTranslationReferenceState();
                    return Vector3.zero;
                }

                if (!_retargeter._hasPreviousEditorRootTranslation)
                {
                    _retargeter._previousEditorRootTranslation = current;
                    _retargeter._hasPreviousEditorRootTranslation = true;
                    return Vector3.zero;
                }

                Vector3 delta = current - _retargeter._previousEditorRootTranslation;
                _retargeter._previousEditorRootTranslation = current;
                Vector3 editorRootDelta = CalculateEditorRootTranslationReferenceDelta(
                    delta,
                    ghostDelta,
                    _retargeter.editorHumanoidRootTranslationWeight,
                    _retargeter.editorHumanoidRootTranslationCurrentWeight,
                    _retargeter._hasSmoothedEditorRootTranslationDelta,
                    _retargeter._smoothedEditorRootTranslationDelta,
                    out _retargeter._smoothedEditorRootTranslationDelta,
                    out _retargeter._hasSmoothedEditorRootTranslationDelta,
                    out bool skippedByGhostDelta,
                    out bool skippedByNonFinite);
                if (skippedByGhostDelta || skippedByNonFinite)
                {
                    return Vector3.zero;
                }

                if (!_retargeter._editorRootTranslationReferenceLogged)
                {
                    Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid RootT translation reference applied at t={time:F3}s.");
                    _retargeter._editorRootTranslationReferenceLogged = true;
                }

                return editorRootDelta;
#else
                return Vector3.zero;
#endif
            }

#if UNITY_EDITOR
            private Vector3 SampleEditorRootTranslation(float time)
            {
                // Unity Humanoid RootT uses the FBX avatar basis. In this project the manual
                // reference root path matches RootT with X/Z swapped in world space.
                return new Vector3(
                    _retargeter._editorRootTranslationZ.Evaluate(time),
                    0f,
                    _retargeter._editorRootTranslationX.Evaluate(time));
            }
#endif

            private void ClampTargetRootPositionSpike(Vector3 positionBeforePose, string source)
            {
                if (!_retargeter.clampRootDeltaSpikes || _retargeter.targetAnimator == null)
                {
                    return;
                }

                Vector3 currentPosition = _retargeter.targetAnimator.transform.position;
                bool shouldClamp = TryCalculateRootPositionSpikeClamp(
                    positionBeforePose,
                    currentPosition,
                    _retargeter.maxRootDeltaPerFrame,
                    out Vector3 clampedPosition,
                    out float poseDeltaMagnitude);

                _retargeter._lastRootPositionPoseDeltaMagnitude = poseDeltaMagnitude;
                if (!IsFinite(poseDeltaMagnitude))
                {
                    return;
                }

                _retargeter._maxRootPositionPoseDeltaMagnitude = Mathf.Max(_retargeter._maxRootPositionPoseDeltaMagnitude, _retargeter._lastRootPositionPoseDeltaMagnitude);
                if (!shouldClamp)
                {
                    return;
                }

                _retargeter._rootPositionSpikeClampedCount++;
                if (_retargeter.logRootDeltaSpikes && !_retargeter._rootDeltaSpikeWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] {source} root position spike {_retargeter._lastRootPositionPoseDeltaMagnitude:F3}m clamped. limit={_retargeter.maxRootDeltaPerFrame:F3}m");
                    _retargeter._rootDeltaSpikeWarningLogged = true;
                }

                _retargeter.targetAnimator.transform.position = clampedPosition;
            }

            // ── Static helpers ──

            private static float CalculateSafeScaleRatio(
                float currentScaleRatio,
                bool hasAnimatorScale,
                float ghostHumanScale,
                float targetHumanScale,
                float initialGhostHipHeight,
                float initialTargetHipHeight,
                bool hasHipPositions,
                float ghostHipY,
                float targetHipY,
                out bool usedInvalidFallback)
            {
                usedInvalidFallback = false;
                float ratio = currentScaleRatio;

                if (hasAnimatorScale && ghostHumanScale > 0.0001f && targetHumanScale > 0.0001f)
                {
                    ratio = targetHumanScale / ghostHumanScale;
                }
                else if (initialGhostHipHeight > 0.01f)
                {
                    ratio = initialTargetHipHeight / initialGhostHipHeight;
                }
                else if (hasHipPositions && ghostHipY > 0.01f)
                {
                    ratio = targetHipY / ghostHipY;
                }

                if (!IsFinite(ratio) || ratio <= 0f)
                {
                    usedInvalidFallback = true;
                    return 1f;
                }

                return Mathf.Clamp(ratio, 0.01f, 10f);
            }

            private static Vector3 SelectBodyPositionRootMotionSource(
                Vector3 poseBodyPosition,
                Vector3 manualReferenceBodyPosition,
                bool hasManualReferenceBodyPosition,
                bool preferManualReferenceXZ)
            {
                if (IsFinite(poseBodyPosition))
                {
                    return poseBodyPosition;
                }

                if (preferManualReferenceXZ &&
                    hasManualReferenceBodyPosition &&
                    IsFinite(manualReferenceBodyPosition))
                {
                    return manualReferenceBodyPosition;
                }

                return poseBodyPosition;
            }

            private static Vector3 CalculateEditorRootTranslationReferenceDelta(
                Vector3 rawEditorDelta,
                Vector3 ghostDelta,
                float editorRootTranslationWeight,
                float editorRootTranslationCurrentWeight,
                bool hasSmoothedEditorRootTranslationDelta,
                Vector3 previousSmoothedEditorRootTranslationDelta,
                out Vector3 nextSmoothedEditorRootTranslationDelta,
                out bool nextHasSmoothedEditorRootTranslationDelta,
                out bool skippedByGhostDelta,
                out bool skippedByNonFinite)
            {
                nextSmoothedEditorRootTranslationDelta = previousSmoothedEditorRootTranslationDelta;
                nextHasSmoothedEditorRootTranslationDelta = hasSmoothedEditorRootTranslationDelta;
                skippedByGhostDelta = false;
                skippedByNonFinite = false;

                if (!IsFinite(rawEditorDelta))
                {
                    skippedByNonFinite = true;
                    return Vector3.zero;
                }

                if (FlattenXZ(ghostDelta).sqrMagnitude > 0.00000025f)
                {
                    skippedByGhostDelta = true;
                    return Vector3.zero;
                }

                Vector3 weightedDelta = rawEditorDelta;
                weightedDelta.y = 0f;
                weightedDelta *= Mathf.Clamp01(editorRootTranslationWeight);

                if (!hasSmoothedEditorRootTranslationDelta)
                {
                    nextSmoothedEditorRootTranslationDelta = weightedDelta;
                    nextHasSmoothedEditorRootTranslationDelta = true;
                    return weightedDelta;
                }

                float currentWeight = Mathf.Clamp(editorRootTranslationCurrentWeight, 0.05f, 1f);
                nextSmoothedEditorRootTranslationDelta = Vector3.Lerp(previousSmoothedEditorRootTranslationDelta, weightedDelta, currentWeight);
                nextHasSmoothedEditorRootTranslationDelta = true;
                return nextSmoothedEditorRootTranslationDelta;
            }

            private static Vector3 FlattenXZ(Vector3 value)
            {
                value.y = 0f;
                return value;
            }

            private static bool TryCalculateManualAnimatorBodyPositionXzReference(
                Vector3 currentBodyPosition,
                Vector3 referenceBodyPosition,
                float weight,
                float maxOffset,
                float axisXScale,
                float axisZScale,
                out Vector3 nextBodyPosition)
            {
                nextBodyPosition = currentBodyPosition;
                if (!IsFinite(currentBodyPosition) || !IsFinite(referenceBodyPosition))
                {
                    return false;
                }

                float clampedWeight = Mathf.Clamp01(weight);
                if (clampedWeight <= 0f)
                {
                    return false;
                }

                Vector3 delta = new Vector3(
                    (referenceBodyPosition.x - currentBodyPosition.x) * Mathf.Clamp01(axisXScale),
                    0f,
                    (referenceBodyPosition.z - currentBodyPosition.z) * Mathf.Clamp01(axisZScale));
                if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
                {
                    return false;
                }

                float clampedMaxOffset = Mathf.Max(0f, maxOffset);
                if (clampedMaxOffset > 0f)
                {
                    float magnitude = delta.magnitude;
                    if (magnitude > clampedMaxOffset)
                    {
                        delta = delta / magnitude * clampedMaxOffset;
                    }
                }

                nextBodyPosition = new Vector3(
                    currentBodyPosition.x + delta.x * clampedWeight,
                    currentBodyPosition.y,
                    currentBodyPosition.z + delta.z * clampedWeight);
                return IsFinite(nextBodyPosition);
            }

            private static Vector3 CalculateRetargetRootDelta(
                Vector3 ghostDelta,
                float scaleRatio,
                Vector3 editorRootTranslationDelta,
                Vector3 bodyRootDelta,
                float movementScaleMultiplier,
                bool useBodyPositionXZRootMotion,
                bool clampRootDeltaSpikes,
                float maxRootDeltaPerFrame,
                out float deltaMagnitude,
                out bool skippedByNonFinite,
                out bool limitedBySpike)
            {
                skippedByNonFinite = false;
                limitedBySpike = false;

                Vector3 targetDelta = useBodyPositionXZRootMotion
                    ? bodyRootDelta * movementScaleMultiplier
                    : (ghostDelta * scaleRatio + editorRootTranslationDelta) * movementScaleMultiplier;
                if (!IsFinite(targetDelta))
                {
                    deltaMagnitude = float.NaN;
                    skippedByNonFinite = true;
                    return Vector3.zero;
                }

                deltaMagnitude = targetDelta.magnitude;
                if (clampRootDeltaSpikes && deltaMagnitude > maxRootDeltaPerFrame)
                {
                    limitedBySpike = true;
                    Vector3 limitedDelta = Vector3.ClampMagnitude(targetDelta, Mathf.Max(0f, maxRootDeltaPerFrame));
                    deltaMagnitude = limitedDelta.magnitude;
                    return limitedDelta;
                }

                return targetDelta;
            }

            private static Vector3 ApplyImplicitBodyPositionRootGuard(
                Vector3 positionBeforePose,
                Vector3 currentPosition,
                bool allowBodyPositionXZRootMotion)
            {
                return ApplyImplicitBodyPositionRootGuard(
                    positionBeforePose,
                    currentPosition,
                    allowBodyPositionXZRootMotion,
                    Vector3.zero);
            }

            private static Vector3 ApplyImplicitBodyPositionRootGuard(
                Vector3 positionBeforePose,
                Vector3 currentPosition,
                bool allowBodyPositionXZRootMotion,
                Vector3 explicitBodyRootDelta)
            {
                bool hasExplicitBodyRootMotion =
                    IsFinite(explicitBodyRootDelta) &&
                    FlattenXZ(explicitBodyRootDelta).sqrMagnitude > 0.0000000001f;

                if ((allowBodyPositionXZRootMotion && !hasExplicitBodyRootMotion) ||
                    !IsFinite(positionBeforePose) ||
                    !IsFinite(currentPosition))
                {
                    return currentPosition;
                }

                return new Vector3(positionBeforePose.x, currentPosition.y, positionBeforePose.z);
            }

            private static Vector3 SelectImplicitRootGuardReference(
                Vector3 rootAnchorPosition,
                Vector3 positionBeforePose,
                float movementScaleMultiplier)
            {
                if (movementScaleMultiplier <= 0f && IsFinite(rootAnchorPosition))
                {
                    return rootAnchorPosition;
                }

                return positionBeforePose;
            }

            private static Vector3 SelectPoseSolveRootPosition(
                Vector3 currentRootPosition,
                Vector3 rootAnchorPosition,
                bool isolateRootMotionFromPoseSolve)
            {
                if (!isolateRootMotionFromPoseSolve ||
                    !IsFinite(currentRootPosition) ||
                    !IsFinite(rootAnchorPosition))
                {
                    return currentRootPosition;
                }

                return new Vector3(rootAnchorPosition.x, currentRootPosition.y, rootAnchorPosition.z);
            }

            private static Vector3 RestoreRootMotionCarrierPositionAfterPose(
                Vector3 rootMotionCarrierPositionBeforePose,
                Vector3 poseSolvedPosition,
                bool isolateRootMotionFromPoseSolve)
            {
                if (!isolateRootMotionFromPoseSolve ||
                    !IsFinite(rootMotionCarrierPositionBeforePose) ||
                    !IsFinite(poseSolvedPosition))
                {
                    return poseSolvedPosition;
                }

                return new Vector3(
                    rootMotionCarrierPositionBeforePose.x,
                    poseSolvedPosition.y,
                    rootMotionCarrierPositionBeforePose.z);
            }

            private static bool TryCalculateRootPositionSpikeClamp(
                Vector3 positionBeforePose,
                Vector3 currentPosition,
                float maxRootDeltaPerFrame,
                out Vector3 clampedPosition,
                out float deltaMagnitude)
            {
                clampedPosition = currentPosition;
                Vector3 poseDelta = currentPosition - positionBeforePose;
                if (!IsFinite(poseDelta))
                {
                    deltaMagnitude = float.NaN;
                    return false;
                }

                deltaMagnitude = poseDelta.magnitude;
                if (deltaMagnitude <= maxRootDeltaPerFrame)
                {
                    return false;
                }

                clampedPosition = positionBeforePose + Vector3.ClampMagnitude(poseDelta, maxRootDeltaPerFrame);
                return true;
            }
        }
    }
}
