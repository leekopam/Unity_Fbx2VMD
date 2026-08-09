using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System;
using System.Collections.Generic;
using RootMotion;
using RootMotion.FinalIK;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
#if UNITY_EDITOR
        private void ApplyEditorHumanoidMuscleReferenceEditor(ref HumanPose pose)
        {
            if (!_useEditorHumanoidMuscleReference || pose.muscles == null || _editorHumanoidMuscleCurves.Count == 0)
            {
                return;
            }

            float time = GetLegacyAnimationTime();
            foreach (KeyValuePair<int, AnimationCurve> pair in _editorHumanoidMuscleCurves)
            {
                if (pair.Key < 0 || pair.Key >= pose.muscles.Length || pair.Value == null)
                {
                    continue;
                }

                float referenceValue = pair.Value.Evaluate(time);
                if (!ShouldApplyEditorHumanoidMuscleReferenceValue(pair.Key, referenceValue))
                {
                    continue;
                }

                pose.muscles[pair.Key] = referenceValue;
            }

            if (!_editorHumanoidMuscleReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Editor Humanoid muscle reference applied at t={time:F3}s.");
                _editorHumanoidMuscleReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidFingerPoseReferenceEditor(ref HumanPose pose)
        {
            if (!_useEditorFingerPoseReference ||
                pose.muscles == null ||
                _editorFingerReferenceAnimator == null ||
                _editorFingerReferenceHandler == null ||
                _editorFingerReferenceMuscleIndices.Count == 0)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            _editorFingerReferenceHandler.GetHumanPose(ref _editorFingerReferencePose);

            if (_editorFingerReferencePose.muscles == null)
            {
                return;
            }

            if (ShouldUseManualAnimatorFullBodyPoseReference)
            {
                float weight = Mathf.Clamp01(manualAnimatorFullBodyPoseReferenceWeight);
                if (weight <= 0f)
                {
                    return;
                }

                if (!ShouldApplyManualFullBodyPoseReferenceFrameGate())
                {
                    return;
                }

                int count = Mathf.Min(pose.muscles.Length, _editorFingerReferencePose.muscles.Length);
                for (int i = 0; i < count; i++)
                {
                    if (!ShouldApplyManualFullBodyPoseReferenceMuscle(i))
                    {
                        continue;
                    }

                    pose.muscles[i] = Mathf.Lerp(pose.muscles[i], _editorFingerReferencePose.muscles[i], weight);
                }
            }
            else
            {
                foreach (int muscleIndex in _editorFingerReferenceMuscleIndices)
                {
                    if (muscleIndex < 0 || muscleIndex >= pose.muscles.Length || muscleIndex >= _editorFingerReferencePose.muscles.Length)
                    {
                        continue;
                    }

                    pose.muscles[muscleIndex] = _editorFingerReferencePose.muscles[muscleIndex];
                }

            }

            if (!_editorFingerPoseReferenceLogged)
            {
                float time = GetLegacyAnimationTime();
                string scope = ShouldUseManualAnimatorFullBodyPoseReference ? "full-body muscle" : "finger";
                string weightSuffix = ShouldUseManualAnimatorFullBodyPoseReference
                    ? $", weight={Mathf.Clamp01(manualAnimatorFullBodyPoseReferenceWeight):F2}"
                    : string.Empty;
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator {scope} reference applied at t={time:F3}s{weightSuffix}.");
                _editorFingerPoseReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidBodyRotationReferenceEditor(ref HumanPose pose)
        {
            if (!ShouldUseManualAnimatorBodyRotationReference ||
                manualAnimatorBodyRotationReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                _editorFingerReferenceHandler == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            _editorFingerReferenceHandler.GetHumanPose(ref _editorFingerReferencePose);
            Quaternion referenceBodyRotation = _editorFingerReferencePose.bodyRotation;
            if (!IsFinite(referenceBodyRotation))
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorBodyRotationReferenceWeight);
            pose.bodyRotation = Quaternion.Slerp(pose.bodyRotation, referenceBodyRotation, weight);

            Vector3 refBodyPos = _editorFingerReferencePose.bodyPosition;
            if (IsFinite(refBodyPos) && refBodyPos.y > 0.01f)
            {
                _editorReferenceBodyPosition = refBodyPos;
                _hasEditorReferenceBodyPosition = true;
            }
            if (!_editorBodyRotationReferenceLogged)
            {
                float time = GetLegacyAnimationTime();
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator bodyRotation reference applied at t={time:F3}s, weight={weight:F2}.");
                _editorBodyRotationReferenceLogged = true;
            }
        }

        private bool UpdateEditorManualReferenceAnimator()
        {
            if (_editorFingerReferenceAnimator == null || _editorFingerReferenceClipLength <= 0f)
            {
                return false;
            }

            float time = GetLegacyAnimationTime();
            float normalizedTime = Mathf.Clamp01(time / _editorFingerReferenceClipLength);
            _editorFingerReferenceAnimator.Play(_editorFingerReferenceStateHash, 0, normalizedTime);
            _editorFingerReferenceAnimator.Update(0f);
            return true;
        }

        private void ApplyEditorHumanoidHipsLocalPositionReference()
        {
            if (!ShouldUseManualAnimatorHipsLocalPositionReference ||
                manualAnimatorHipsLocalPositionWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            Transform referenceHips = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (referenceHips == null || targetHips == null)
            {
                return;
            }

            Vector3 refCurrentLocalPosition = referenceHips.localPosition;
            Vector3 currentLocalPosition = targetHips.localPosition;
            Vector3 ghostRightFootPosition = ReadAnimatorBoneWorldPosition(ghostAnimator, HumanBodyBones.RightFoot);
            Vector3 ghostRightToesPosition = ReadAnimatorBoneWorldPosition(ghostAnimator, HumanBodyBones.RightToes);
            Vector3 beforeRightFootPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightFoot);
            Vector3 beforeRightToesPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightToes);
            // Delta 방식: testprefab의 clip 시작 대비 현재 변위만 YYB 자연 위치에 더한다.
            // 절대 복사는 모델 비율 차이(YYB Hips Y≈1.024 vs testprefab≈1.056)로 인해 YYB Hips를 잘못된 높이로 강제한다.
            if (!TryCalculateEditorHipsLocalPositionReference(
                refCurrentLocalPosition,
                _editorReferenceHipsRestLocalPosition,
                _hasEditorReferenceHipsRestLocalPosition,
                _targetHipsRestLocalPosition,
                _hasTargetHipsRestLocalPosition,
                currentLocalPosition,
                manualAnimatorHipsLocalPositionWeight,
                manualAnimatorHipsLocalPositionMaxOffset,
                out Vector3 nextLocalPosition))
            {
                return;
            }

            targetHips.localPosition = nextLocalPosition;
            Vector3 afterRightFootPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightFoot);
            Vector3 afterRightToesPosition = ReadAnimatorBoneWorldPosition(targetAnimator, HumanBodyBones.RightToes);
            if (!ShouldKeepEditorHipsLocalPositionReferenceByTargetGap(
                ghostRightFootPosition,
                ghostRightToesPosition,
                beforeRightFootPosition,
                beforeRightToesPosition,
                afterRightFootPosition,
                afterRightToesPosition,
                HipsLocalPositionTargetGapGuardMaxIncreaseMeters))
            {
                targetHips.localPosition = currentLocalPosition;
                RecordEditorHipsLocalReferenceDiagnostics(currentLocalPosition, currentLocalPosition);
                return;
            }

            RecordEditorHipsLocalReferenceDiagnostics(currentLocalPosition, nextLocalPosition);
            if (!_editorHipsLocalPositionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator Hips localPosition reference applied. weight={manualAnimatorHipsLocalPositionWeight:F2}, maxOffset={manualAnimatorHipsLocalPositionMaxOffset:F3}m");
                _editorHipsLocalPositionReferenceLogged = true;
            }
        }

        private static bool TryCalculateEditorHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            return TryCalculateEditorHipsLocalPositionReference(
                referenceCurrentLocalPosition,
                referenceRestLocalPosition,
                hasReferenceRestLocalPosition,
                currentLocalPosition,
                false,
                currentLocalPosition,
                weight,
                maxOffset,
                out nextLocalPosition);
        }

        private static bool TryCalculateEditorHipsLocalPositionReference(
            Vector3 referenceCurrentLocalPosition,
            Vector3 referenceRestLocalPosition,
            bool hasReferenceRestLocalPosition,
            Vector3 targetRestLocalPosition,
            bool hasTargetRestLocalPosition,
            Vector3 currentLocalPosition,
            float weight,
            float maxOffset,
            out Vector3 nextLocalPosition)
        {
            nextLocalPosition = currentLocalPosition;
            if (!IsFinite(referenceCurrentLocalPosition) || !IsFinite(currentLocalPosition))
            {
                return false;
            }

            if (hasReferenceRestLocalPosition && !IsFinite(referenceRestLocalPosition))
            {
                return false;
            }

            Vector3 desiredLocalPosition;
            if (hasReferenceRestLocalPosition)
            {
                Vector3 referenceDelta = referenceCurrentLocalPosition - referenceRestLocalPosition;
                Vector3 anchorLocalPosition = hasTargetRestLocalPosition && IsFinite(targetRestLocalPosition)
                    ? targetRestLocalPosition
                    : currentLocalPosition;
                desiredLocalPosition = anchorLocalPosition + referenceDelta;
            }
            else
            {
                desiredLocalPosition = referenceCurrentLocalPosition;
            }

            Vector3 delta = desiredLocalPosition - currentLocalPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                delta = Vector3.ClampMagnitude(delta, clampedMaxOffset);
            }

            nextLocalPosition = currentLocalPosition + delta * Mathf.Clamp01(weight);
            if (!IsFinite(nextLocalPosition))
            {
                nextLocalPosition = currentLocalPosition;
                return false;
            }

            return true;
        }

        private static bool ShouldKeepEditorHipsLocalPositionReferenceByTargetGap(
            Vector3 ghostRightFootPosition,
            Vector3 ghostRightToesPosition,
            Vector3 beforeRightFootPosition,
            Vector3 beforeRightToesPosition,
            Vector3 afterRightFootPosition,
            Vector3 afterRightToesPosition,
            float maxAllowedIncrease)
        {
            if (!TryCalculateRightEndpointTargetGap(
                    ghostRightFootPosition,
                    ghostRightToesPosition,
                    beforeRightFootPosition,
                    beforeRightToesPosition,
                    out float beforeGap) ||
                !TryCalculateRightEndpointTargetGap(
                    ghostRightFootPosition,
                    ghostRightToesPosition,
                    afterRightFootPosition,
                    afterRightToesPosition,
                    out float afterGap))
            {
                return true;
            }

            return afterGap <= beforeGap + Mathf.Max(0f, maxAllowedIncrease);
        }

        private static bool TryCalculateRightEndpointTargetGap(
            Vector3 ghostRightFootPosition,
            Vector3 ghostRightToesPosition,
            Vector3 targetRightFootPosition,
            Vector3 targetRightToesPosition,
            out float gap)
        {
            gap = float.NaN;
            if (!TryCalculateXzDistance(ghostRightFootPosition, targetRightFootPosition, out float footGap) ||
                !TryCalculateXzDistance(ghostRightToesPosition, targetRightToesPosition, out float toesGap))
            {
                return false;
            }

            gap = Mathf.Max(footGap, toesGap);
            return IsFinite(gap);
        }

        private static bool TryCalculateXzDistance(Vector3 a, Vector3 b, out float distance)
        {
            distance = float.NaN;
            if (!IsFinite(a) || !IsFinite(b))
            {
                return false;
            }

            distance = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            return IsFinite(distance);
        }

        private void RecordEditorHipsLocalReferenceDiagnostics(Vector3 beforeLocalPosition, Vector3 afterLocalPosition)
        {
            _lastEditorHipsLocalReferenceBeforeLocalY = IsFinite(beforeLocalPosition) ? beforeLocalPosition.y : float.NaN;
            _lastEditorHipsLocalReferenceAfterLocalY = IsFinite(afterLocalPosition) ? afterLocalPosition.y : float.NaN;
            _lastEditorHipsLocalReferenceDeltaY =
                IsFinite(_lastEditorHipsLocalReferenceBeforeLocalY) && IsFinite(_lastEditorHipsLocalReferenceAfterLocalY)
                    ? _lastEditorHipsLocalReferenceAfterLocalY - _lastEditorHipsLocalReferenceBeforeLocalY
                    : float.NaN;
        }

        private void ApplyEditorHumanoidHandLocalRotationReference()
        {
            if (!useManualAnimatorHandLocalRotationReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            int changed = 0;
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += ApplyEditorHumanoidHandLocalRotationReferenceBone(HumanBodyBones.LeftHand);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += ApplyEditorHumanoidHandLocalRotationReferenceBone(HumanBodyBones.RightHand);
            }

            if (changed > 0 && !_editorHandLocalRotationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator hand localRotation reference applied. bones={changed}");
                _editorHandLocalRotationReferenceLogged = true;
            }
        }

        private int ApplyEditorHumanoidHandLocalRotationReferenceBone(HumanBodyBones handBone)
        {
            Transform source = _editorFingerReferenceAnimator.GetBoneTransform(handBone);
            Transform target = targetAnimator.GetBoneTransform(handBone);
            if (source == null || target == null)
            {
                return 0;
            }

            Quaternion sourceRotation = source.localRotation;
            if (!IsFinite(sourceRotation) || Quaternion.Angle(target.localRotation, sourceRotation) <= 0.001f)
            {
                return 0;
            }

            target.localRotation = sourceRotation;
            return 1;
        }

        private void ApplyEditorHumanoidFootLocalRotationReference()
        {
            if (!ShouldUseManualAnimatorFootLocalRotationReference ||
                manualAnimatorFootLocalRotationReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            CaptureTargetFootPositions(out Vector3 leftFootBefore, out Vector3 rightFootBefore);
            int changed = 0;
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftUpperLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightUpperLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftLowerLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightLowerLeg);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftFoot);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightFoot);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.LeftToes);
            changed += ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones.RightToes);
            RecordEditorFootLocalRotationReferenceDiagnostics(leftFootBefore, rightFootBefore);

            if (changed > 0 && !_editorFootLocalRotationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator lower-body localRotation reference applied. bones={changed}, weight={manualAnimatorFootLocalRotationReferenceWeight:F2}");
                _editorFootLocalRotationReferenceLogged = true;
            }
        }

        private int ApplyEditorHumanoidFootLocalRotationReferenceBone(HumanBodyBones footBone)
        {
            Transform source = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            Transform target = targetAnimator.GetBoneTransform(footBone);
            if (source == null || target == null)
            {
                return 0;
            }

            if (!TryCalculateEditorFootLocalRotationReference(
                    source.localRotation,
                    target.localRotation,
                    manualAnimatorFootLocalRotationReferenceWeight,
                    out Quaternion nextLocalRotation))
            {
                return 0;
            }

            target.localRotation = nextLocalRotation;
            return 1;
        }

        private static bool TryCalculateEditorFootLocalRotationReference(
            Quaternion referenceLocalRotation,
            Quaternion currentLocalRotation,
            float weight,
            out Quaternion nextLocalRotation)
        {
            nextLocalRotation = currentLocalRotation;
            if (!IsFinite(referenceLocalRotation) || !IsFinite(currentLocalRotation))
            {
                return false;
            }

            if (Quaternion.Angle(currentLocalRotation, referenceLocalRotation) <= 0.001f)
            {
                return false;
            }

            nextLocalRotation = Quaternion.Slerp(currentLocalRotation, referenceLocalRotation, Mathf.Clamp01(weight));
            if (!IsFinite(nextLocalRotation))
            {
                nextLocalRotation = currentLocalRotation;
                return false;
            }

            return true;
        }

        private void CaptureTargetFootPositions(out Vector3 leftFootPosition, out Vector3 rightFootPosition)
        {
            leftFootPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftFoot);
            rightFootPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightFoot);
        }

        private Vector3 ReadTargetBoneWorldPosition(HumanBodyBones bone)
        {
            if (targetAnimator == null)
            {
                return BuildNaNVector3();
            }

            Transform targetBone = targetAnimator.GetBoneTransform(bone);
            return targetBone != null ? targetBone.position : BuildNaNVector3();
        }

        private static bool TryFindFirstRetargetEndpointStageJump(
            string[] stageNames,
            Vector3[] positions,
            float threshold,
            out string stage,
            out Vector3 delta,
            out float magnitude)
        {
            stage = "";
            delta = BuildNaNVector3();
            magnitude = float.NaN;
            if (stageNames == null ||
                positions == null ||
                stageNames.Length != positions.Length ||
                positions.Length < 2)
            {
                return false;
            }

            float safeThreshold = Mathf.Max(0f, threshold);
            for (int i = 1; i < positions.Length; i++)
            {
                Vector3 previous = positions[i - 1];
                Vector3 current = positions[i];
                if (!IsFinite(previous) || !IsFinite(current))
                {
                    continue;
                }

                Vector3 stageDelta = current - previous;
                float stageMagnitude = stageDelta.magnitude;
                if (!IsFinite(stageDelta) || !IsFinite(stageMagnitude) || stageMagnitude <= safeThreshold)
                {
                    continue;
                }

                stage = stageNames[i] ?? "";
                delta = stageDelta;
                magnitude = stageMagnitude;
                return true;
            }

            return false;
        }

        private static readonly string[] RetargetEndpointStageNames =
        {
            "ghost",
            "after_set_human_pose",
            "after_manual_reference",
            "after_root_restore",
            "after_root_delta",
            "after_grounding",
            "after_biped_ik",
            "after_late_visual_grounding"
        };

        private const float RetargetEndpointStageJumpAttributionThreshold = 0.001f;

        private void CaptureRetargetEndpointStageAttributionDiagnostics()
        {
            ResetRetargetEndpointStageAttributionDiagnostics();
            bool hasBest = false;
            int bestStageIndex = int.MaxValue;
            TryRecordRetargetEndpointStageJump(
                "left_foot",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.LeftFoot),
                ref hasBest,
                ref bestStageIndex);
            TryRecordRetargetEndpointStageJump(
                "left_toes",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.LeftToes),
                ref hasBest,
                ref bestStageIndex);
            TryRecordRetargetEndpointStageJump(
                "right_foot",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.RightFoot),
                ref hasBest,
                ref bestStageIndex);
            TryRecordRetargetEndpointStageJump(
                "right_toes",
                BuildRetargetEndpointStagePositions(endpoint => endpoint.RightToes),
                ref hasBest,
                ref bestStageIndex);
        }

        private delegate Vector3 RetargetEndpointStageSelector(RetargetEndpointStageWorldPositions endpointPositions);

        private Vector3[] BuildRetargetEndpointStagePositions(RetargetEndpointStageSelector selector)
        {
            return new[]
            {
                selector(_lastRetargetStageGhostEndpointPositions),
                selector(_lastRetargetStageAfterSetHumanPoseEndpointPositions),
                selector(_lastRetargetStageAfterManualReferencesEndpointPositions),
                selector(_lastRetargetStageAfterRootRestoreEndpointPositions),
                selector(_lastRetargetStageAfterRootDeltaEndpointPositions),
                selector(_lastRetargetStageAfterGroundingEndpointPositions),
                selector(_lastRetargetStageAfterBipedIKEndpointPositions),
                selector(_lastRetargetStageAfterLateVisualGroundingEndpointPositions)
            };
        }

        private void TryRecordRetargetEndpointStageJump(
            string endpointName,
            Vector3[] positions,
            ref bool hasBest,
            ref int bestStageIndex)
        {
            if (!TryFindFirstRetargetEndpointStageJump(
                    RetargetEndpointStageNames,
                    positions,
                    RetargetEndpointStageJumpAttributionThreshold,
                    out string stage,
                    out Vector3 delta,
                    out float magnitude))
            {
                return;
            }

            int stageIndex = Array.IndexOf(RetargetEndpointStageNames, stage);
            if (stageIndex < 0)
            {
                return;
            }

            if (hasBest && stageIndex >= bestStageIndex)
            {
                return;
            }

            hasBest = true;
            bestStageIndex = stageIndex;
            _lastRetargetEndpointFirstJumpStage = stage;
            _lastRetargetEndpointFirstJumpEndpoint = endpointName ?? "";
            _lastRetargetEndpointFirstJumpDelta = delta;
            _lastRetargetEndpointFirstJumpMagnitude = magnitude;
        }

        private void ResetRetargetEndpointStageAttributionDiagnostics()
        {
            _lastRetargetEndpointFirstJumpStage = "";
            _lastRetargetEndpointFirstJumpEndpoint = "";
            _lastRetargetEndpointFirstJumpDelta = BuildNaNVector3();
            _lastRetargetEndpointFirstJumpMagnitude = float.NaN;
        }

        private void RecordEditorFootLocalRotationReferenceDiagnostics(Vector3 leftFootBefore, Vector3 rightFootBefore)
        {
            _lastEditorFootLocalRotationLeftFootXzDelta = CalculateTargetFootXzDelta(leftFootBefore, HumanBodyBones.LeftFoot);
            _lastEditorFootLocalRotationRightFootXzDelta = CalculateTargetFootXzDelta(rightFootBefore, HumanBodyBones.RightFoot);
        }

        private void RecordEditorLowerBodySegmentDirectionReferenceDiagnostics(Vector3 leftFootBefore, Vector3 rightFootBefore)
        {
            _lastEditorLowerBodySegmentDirectionLeftFootXzDelta = CalculateTargetFootXzDelta(leftFootBefore, HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionRightFootXzDelta = CalculateTargetFootXzDelta(rightFootBefore, HumanBodyBones.RightFoot);
            RecordEditorLowerBodySegmentDirectionEndpointDiagnostics();
        }

        private void RecordEditorFootHipsAlignedResidualYawReferenceDiagnostics(Vector3 leftFootBefore, Vector3 rightFootBefore)
        {
            _lastEditorFootHipsAlignedResidualYawLeftFootXzDelta = CalculateTargetFootXzDelta(leftFootBefore, HumanBodyBones.LeftFoot);
            _lastEditorFootHipsAlignedResidualYawRightFootXzDelta = CalculateTargetFootXzDelta(rightFootBefore, HumanBodyBones.RightFoot);
        }

        private float CalculateTargetFootXzDelta(Vector3 beforePosition, HumanBodyBones footBone)
        {
            Vector3 afterPosition = ReadTargetBoneWorldPosition(footBone);
            if (!IsFinite(beforePosition) || !IsFinite(afterPosition))
            {
                return float.NaN;
            }

            Vector2 beforeXz = new Vector2(beforePosition.x, beforePosition.z);
            Vector2 afterXz = new Vector2(afterPosition.x, afterPosition.z);
            return Vector2.Distance(beforeXz, afterXz);
        }

        private struct RetargetEndpointStageWorldPositions
        {
            public Vector3 LeftFoot;
            public Vector3 LeftToes;
            public Vector3 RightFoot;
            public Vector3 RightToes;

            public static RetargetEndpointStageWorldPositions Empty => new RetargetEndpointStageWorldPositions
            {
                LeftFoot = BuildNaNVector3(),
                LeftToes = BuildNaNVector3(),
                RightFoot = BuildNaNVector3(),
                RightToes = BuildNaNVector3()
            };
        }

        private struct PostSetHumanPoseEndpointPositionDiagnostics
        {
            public Vector3 DesiredFootPosition;
            public Vector3 DesiredToesPosition;
            public Vector3 CurrentFootPosition;
            public Vector3 CurrentToesPosition;
            public Vector3 EndpointDeltaBeforeClamp;
            public Vector3 EndpointDeltaAfterClamp;
            public Vector3 EndpointDeltaAfterPositiveZScale;
            public Vector3 Correction;
            public Vector3 NextFootPosition;
            public float EvaluatorXzReferenceEnabled;
            public Vector3 EvaluatorXzFirstOffset;
            public Vector3 EvaluatorXzNormalizedDelta;
            public Vector3 EvaluatorXzDesiredNormalizedDelta;
            public float EvaluatorXzTargetMagnitude;

            public static PostSetHumanPoseEndpointPositionDiagnostics Empty => new PostSetHumanPoseEndpointPositionDiagnostics
            {
                DesiredFootPosition = BuildNaNVector3(),
                DesiredToesPosition = BuildNaNVector3(),
                CurrentFootPosition = BuildNaNVector3(),
                CurrentToesPosition = BuildNaNVector3(),
                EndpointDeltaBeforeClamp = BuildNaNVector3(),
                EndpointDeltaAfterClamp = BuildNaNVector3(),
                EndpointDeltaAfterPositiveZScale = BuildNaNVector3(),
                Correction = BuildNaNVector3(),
                NextFootPosition = BuildNaNVector3(),
                EvaluatorXzReferenceEnabled = float.NaN,
                EvaluatorXzFirstOffset = BuildNaNVector3(),
                EvaluatorXzNormalizedDelta = BuildNaNVector3(),
                EvaluatorXzDesiredNormalizedDelta = BuildNaNVector3(),
                EvaluatorXzTargetMagnitude = float.NaN
            };
        }

        private void ResetPostSetHumanPoseRightEndpointPositionDiagnostics()
        {
            _lastPostSetHumanPoseRightEndpointDesiredFootWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDesiredToesWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointCurrentFootWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointCurrentToesWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDeltaBeforeClamp = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDeltaAfterClamp = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScale = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointCorrection = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointNextFootWorldPosition = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointMaxYawAngle = float.NaN;
            _lastPostSetHumanPoseRightEndpointYawCorrectionAngle = float.NaN;
            _lastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle = float.NaN;
            _lastPostSetHumanPoseRightEndpointApplied = float.NaN;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled = float.NaN;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffset = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDelta = BuildNaNVector3();
            _lastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude = float.NaN;
        }

        private void RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
            PostSetHumanPoseEndpointPositionDiagnostics diagnostics,
            float maxYawAngle,
            float yawCorrectionAngle,
            float upperLegRotationDeltaAngle,
            float applied)
        {
            _lastPostSetHumanPoseRightEndpointDesiredFootWorldPosition = diagnostics.DesiredFootPosition;
            _lastPostSetHumanPoseRightEndpointDesiredToesWorldPosition = diagnostics.DesiredToesPosition;
            _lastPostSetHumanPoseRightEndpointCurrentFootWorldPosition = diagnostics.CurrentFootPosition;
            _lastPostSetHumanPoseRightEndpointCurrentToesWorldPosition = diagnostics.CurrentToesPosition;
            _lastPostSetHumanPoseRightEndpointDeltaBeforeClamp = diagnostics.EndpointDeltaBeforeClamp;
            _lastPostSetHumanPoseRightEndpointDeltaAfterClamp = diagnostics.EndpointDeltaAfterClamp;
            _lastPostSetHumanPoseRightEndpointDeltaAfterPositiveZScale = diagnostics.EndpointDeltaAfterPositiveZScale;
            _lastPostSetHumanPoseRightEndpointCorrection = diagnostics.Correction;
            _lastPostSetHumanPoseRightEndpointNextFootWorldPosition = diagnostics.NextFootPosition;
            _lastPostSetHumanPoseRightEndpointMaxYawAngle = maxYawAngle;
            _lastPostSetHumanPoseRightEndpointYawCorrectionAngle = yawCorrectionAngle;
            _lastPostSetHumanPoseRightEndpointUpperLegRotationDeltaAngle = upperLegRotationDeltaAngle;
            _lastPostSetHumanPoseRightEndpointApplied = applied;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzReferenceEnabled = diagnostics.EvaluatorXzReferenceEnabled;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzFirstOffset = diagnostics.EvaluatorXzFirstOffset;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzNormalizedDelta = diagnostics.EvaluatorXzNormalizedDelta;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzDesiredNormalizedDelta = diagnostics.EvaluatorXzDesiredNormalizedDelta;
            _lastPostSetHumanPoseRightEndpointEvaluatorXzTargetMagnitude = diagnostics.EvaluatorXzTargetMagnitude;
        }

        private void RecordEditorLowerBodySegmentDirectionEndpointDiagnostics()
        {
            _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftLowerLeg);
            _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.LeftToes);
            _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightLowerLeg);
            _lastEditorLowerBodySegmentDirectionRightFootWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightFoot);
            _lastEditorLowerBodySegmentDirectionRightToesWorldPosition = ReadTargetBoneWorldPosition(HumanBodyBones.RightToes);
            _lastEditorLowerBodySegmentDirectionLeftFootForward = ReadTargetBoneWorldForward(HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionLeftFootUp = ReadTargetBoneWorldUp(HumanBodyBones.LeftFoot);
            _lastEditorLowerBodySegmentDirectionRightFootForward = ReadTargetBoneWorldForward(HumanBodyBones.RightFoot);
            _lastEditorLowerBodySegmentDirectionRightFootUp = ReadTargetBoneWorldUp(HumanBodyBones.RightFoot);
        }

        private Vector3 ReadTargetBoneWorldForward(HumanBodyBones bone)
        {
            Transform targetBone = targetAnimator != null ? targetAnimator.GetBoneTransform(bone) : null;
            return targetBone != null && IsFinite(targetBone.forward) ? targetBone.forward : BuildNaNVector3();
        }

        private Vector3 ReadTargetBoneWorldUp(HumanBodyBones bone)
        {
            Transform targetBone = targetAnimator != null ? targetAnimator.GetBoneTransform(bone) : null;
            return targetBone != null && IsFinite(targetBone.up) ? targetBone.up : BuildNaNVector3();
        }

        private void ApplyEditorHumanoidLowerBodySegmentDirectionReference()
        {
            if (!ShouldUseManualAnimatorLowerBodySegmentDirectionReference ||
                manualAnimatorLowerBodySegmentDirectionReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorLowerBodySegmentDirectionReferenceWeight);
            float maxAngle = Mathf.Max(0f, manualAnimatorLowerBodySegmentDirectionReferenceMaxAngle);
            float upperLegToLowerLegMaxAngle = ResolveManualAnimatorUpperLegToLowerLegSegmentDirectionMaxAngle(maxAngle);
            float lowerLegToFootMaxAngle = ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(maxAngle);
            float footToToesMaxAngle = ResolveManualAnimatorFootToToesSegmentDirectionMaxAngle(maxAngle);
            CaptureTargetFootPositions(out Vector3 leftFootBefore, out Vector3 rightFootBefore);
            ResetEditorLowerBodySegmentDirectionDetailedDiagnostics();
            int changed = 0;
            if (!ShouldDisableManualAnimatorUpperLegToLowerLegSegmentDirectionReference)
            {
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, weight, upperLegToLowerLegMaxAngle);
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, weight, upperLegToLowerLegMaxAngle);
            }

            if (!ShouldDisableManualAnimatorLowerLegToFootSegmentDirectionReference)
            {
                changed += AlignEditorHumanoidLowerBodySegmentDirection(
                    HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot,
                    weight,
                    ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(lowerLegToFootMaxAngle, rightSide: false));
                changed += AlignEditorHumanoidLowerBodySegmentDirection(
                    HumanBodyBones.RightLowerLeg,
                    HumanBodyBones.RightFoot,
                    ResolveManualAnimatorRightLowerLegToFootSegmentDirectionBlendWeight(weight),
                    ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(lowerLegToFootMaxAngle, rightSide: true),
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceAxisXzScale,
                    manualAnimatorRightLowerLegToFootSegmentDirectionReferenceEndpointBlendWeight);
            }

            if (!ShouldDisableManualAnimatorFootToToesSegmentDirectionReference)
            {
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, weight, footToToesMaxAngle);
                changed += AlignEditorHumanoidLowerBodySegmentDirection(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, weight, footToToesMaxAngle);
            }

            RecordEditorLowerBodySegmentDirectionReferenceDiagnostics(leftFootBefore, rightFootBefore);

            if (changed > 0 && !_editorLowerBodySegmentDirectionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator lower-body segment direction reference applied. segments={changed}, weight={weight:F2}, maxAngle={maxAngle:F1}deg");
                _editorLowerBodySegmentDirectionReferenceLogged = true;
            }
        }

        private float ResolveManualAnimatorUpperLegToLowerLegSegmentDirectionMaxAngle(float fallbackMaxAngle)
        {
            float segmentMaxAngle = Mathf.Max(0f, manualAnimatorUpperLegToLowerLegSegmentDirectionReferenceMaxAngle);
            return segmentMaxAngle > 0f ? segmentMaxAngle : fallbackMaxAngle;
        }

        private float ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(float fallbackMaxAngle)
        {
            float segmentMaxAngle = Mathf.Max(0f, manualAnimatorLowerLegToFootSegmentDirectionReferenceMaxAngle);
            return segmentMaxAngle > 0f ? segmentMaxAngle : fallbackMaxAngle;
        }

        private float ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(
            float fallbackMaxAngle,
            bool rightSide)
        {
            float sideMaxAngle = Mathf.Max(
                0f,
                rightSide
                    ? manualAnimatorRightLowerLegToFootSegmentDirectionReferenceMaxAngle
                    : manualAnimatorLeftLowerLegToFootSegmentDirectionReferenceMaxAngle);
            if (rightSide && sideMaxAngle > 0f && !ShouldApplyManualAnimatorRightLowerLegToFootFrameGate())
            {
                sideMaxAngle = 0f;
            }

            return sideMaxAngle > 0f
                ? sideMaxAngle
                : ResolveManualAnimatorLowerLegToFootSegmentDirectionMaxAngle(fallbackMaxAngle);
        }

        private bool ShouldApplyManualAnimatorRightLowerLegToFootFrameGate()
        {
            float start = Mathf.Max(0f, manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateStart);
            float end = Mathf.Max(0f, manualAnimatorRightLowerLegToFootSegmentDirectionReferenceFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end < start || end <= 0f)
            {
                return true;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            int currentFrame = Mathf.RoundToInt(GetLegacyAnimationTime() * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private bool ShouldApplyPostSetHumanPoseRightEndpointPositionFrameGate()
        {
            float start = Mathf.Max(0f, postSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            float end = Mathf.Max(0f, postSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end < start || end <= 0f)
            {
                return true;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            int currentFrame = Mathf.RoundToInt(GetLegacyAnimationTime() * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private bool ShouldApplyPreSetHumanPoseRightEndpointPositionFrameGate()
        {
            float start = Mathf.Max(0f, preSetHumanPoseRightEndpointPositionReferenceFrameGateStart);
            float end = Mathf.Max(0f, preSetHumanPoseRightEndpointPositionReferenceFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end < start || end <= 0f)
            {
                return true;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            int currentFrame = Mathf.RoundToInt(GetLegacyAnimationTime() * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private float ResolveManualAnimatorBodyPositionXzFrameGateWeight()
        {
            float start = Mathf.Max(0f, manualAnimatorBodyPositionXzReferenceFrameGateStart);
            float end = Mathf.Max(0f, manualAnimatorBodyPositionXzReferenceFrameGateEnd);
            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 15f, 120f);
            float currentFrame = Mathf.RoundToInt(GetLegacyAnimationTime() * frameRate);
            return CalculateManualAnimatorBodyPositionXzFrameGateWeight(
                currentFrame,
                start,
                end,
                Mathf.Max(0f, manualAnimatorBodyPositionXzReferenceFrameGateBlendFrames));
        }

        private static float CalculateManualAnimatorBodyPositionXzFrameGateWeight(
            float currentFrame,
            float startFrame,
            float endFrame,
            float blendFrames)
        {
            float start = Mathf.Max(0f, Mathf.Round(startFrame));
            float end = Mathf.Max(0f, Mathf.Round(endFrame));
            if (start <= 0f && end <= 0f)
            {
                return 1f;
            }

            if (end < start || end <= 0f)
            {
                return 1f;
            }

            float blend = Mathf.Max(0f, blendFrames);
            if (blend <= 0f)
            {
                return currentFrame >= start && currentFrame <= end ? 1f : 0f;
            }

            if (currentFrame >= start && currentFrame <= end)
            {
                return 1f;
            }

            if (currentFrame < start)
            {
                float fadeStart = start - blend;
                if (currentFrame <= fadeStart)
                {
                    return 0f;
                }

                return Mathf.Clamp01((currentFrame - fadeStart) / blend);
            }

            float fadeEnd = end + blend;
            if (currentFrame >= fadeEnd)
            {
                return 0f;
            }

            return Mathf.Clamp01((fadeEnd - currentFrame) / blend);
        }

        private float ResolveManualAnimatorRightLowerLegToFootSegmentDirectionBlendWeight(float fallbackWeight)
        {
            return Mathf.Clamp01(fallbackWeight) *
                Mathf.Clamp01(manualAnimatorRightLowerLegToFootSegmentDirectionReferenceBlendWeight);
        }

        private float ResolveManualAnimatorFootToToesSegmentDirectionMaxAngle(float fallbackMaxAngle)
        {
            float footToToesMaxAngle = Mathf.Max(0f, manualAnimatorFootToToesSegmentDirectionReferenceMaxAngle);
            return footToToesMaxAngle > 0f ? footToToesMaxAngle : fallbackMaxAngle;
        }

        private int AlignEditorHumanoidLowerBodySegmentDirection(
            HumanBodyBones parentBone,
            HumanBodyBones childBone,
            float weight,
            float maxAngle,
            float correctionAxisXzScale = 1f,
            float childWorldRotationBlendWeight = 1f)
        {
            Transform targetParent = targetAnimator.GetBoneTransform(parentBone);
            Transform targetChild = targetAnimator.GetBoneTransform(childBone);
            Transform referenceParent = _editorFingerReferenceAnimator.GetBoneTransform(parentBone);
            Transform referenceChild = _editorFingerReferenceAnimator.GetBoneTransform(childBone);
            if (targetParent == null || targetChild == null || referenceParent == null || referenceChild == null)
            {
                return 0;
            }

            Vector3 currentSegment = targetChild.position - targetParent.position;
            Vector3 referenceSegment = referenceChild.position - referenceParent.position;
            if (!TryNormalize(currentSegment, out Vector3 currentDirection) ||
                !TryNormalize(referenceSegment, out Vector3 referenceDirection))
            {
                return 0;
            }

            Vector3 referenceRootDirection = _editorFingerReferenceAnimator.transform.InverseTransformDirection(referenceDirection).normalized;
            Vector3 desiredWorldDirection = targetAnimator.transform.TransformDirection(referenceRootDirection).normalized;
            if (!IsFinite(referenceRootDirection) || !IsFinite(desiredWorldDirection))
            {
                return 0;
            }

            Quaternion currentParentRotation = targetParent.rotation;
            Quaternion childWorldRotationBefore = targetChild.rotation;
            Quaternion childLocalRotationBefore = targetChild.localRotation;
            float preAngle = Vector3.Angle(currentDirection, desiredWorldDirection);
            if (!TryCalculateEditorLowerBodySegmentDirectionReference(
                    desiredWorldDirection,
                    currentDirection,
                    currentParentRotation,
                    weight,
                    maxAngle,
                    correctionAxisXzScale,
                    out Quaternion nextWorldRotation))
            {
                return 0;
            }

            targetParent.rotation = nextWorldRotation;
            float clampedChildWorldRotationBlend = Mathf.Clamp01(childWorldRotationBlendWeight);
            if (clampedChildWorldRotationBlend < 0.9999f)
            {
                targetChild.rotation = Quaternion.Slerp(
                    childWorldRotationBefore,
                    targetChild.rotation,
                    clampedChildWorldRotationBlend);
            }

            Vector3 postSegment = targetChild.position - targetParent.position;
            if (TryNormalize(postSegment, out Vector3 postDirection))
            {
                Quaternion correction = nextWorldRotation * Quaternion.Inverse(currentParentRotation);
                float correctionAngle = Quaternion.Angle(Quaternion.identity, correction);
                float parentWorldRotationDeltaAngle = Quaternion.Angle(currentParentRotation, targetParent.rotation);
                float childLocalRotationDeltaAngle = Quaternion.Angle(childLocalRotationBefore, targetChild.localRotation);
                float postAngle = Vector3.Angle(postDirection, desiredWorldDirection);
                RecordEditorLowerBodySegmentDirectionSegmentDiagnostics(
                    parentBone,
                    childBone,
                    correctionAngle,
                    parentWorldRotationDeltaAngle,
                    childLocalRotationDeltaAngle,
                    preAngle,
                    postAngle,
                    ReadFiniteCorrectionAxis(correction),
                    desiredWorldDirection,
                    currentDirection,
                    postDirection);
            }

            return 1;
        }

        private void ResetEditorLowerBodySegmentDirectionDetailedDiagnostics()
        {
            _lastEditorLowerBodySegmentDirectionMaxCorrectionSegment = string.Empty;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionMaxPreAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionMaxPostAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionMaxReferenceDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionMaxPreDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionMaxPostDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = float.NaN;
            _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftLowerLegWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftToesWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightLowerLegWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightToesWorldPosition = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootForward = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionLeftFootUp = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootForward = BuildNaNVector3();
            _lastEditorLowerBodySegmentDirectionRightFootUp = BuildNaNVector3();
        }

        private void RecordEditorLowerBodySegmentDirectionSegmentDiagnostics(
            HumanBodyBones parentBone,
            HumanBodyBones childBone,
            float correctionAngle,
            float parentWorldRotationDeltaAngle,
            float childLocalRotationDeltaAngle,
            float preAngle,
            float postAngle,
            Vector3 correctionAxis,
            Vector3 referenceDirection,
            Vector3 preDirection,
            Vector3 postDirection)
        {
            string segmentName = BuildLowerBodySegmentName(parentBone, childBone);
            SetEditorLowerBodySegmentDirectionCorrectionAngle(segmentName, correctionAngle);
            SetEditorLowerBodySegmentDirectionCouplingDiagnostics(
                segmentName,
                parentWorldRotationDeltaAngle,
                childLocalRotationDeltaAngle,
                correctionAxis,
                referenceDirection,
                preDirection,
                postDirection);
            if (!IsFinite(correctionAngle) ||
                (!float.IsNaN(_lastEditorLowerBodySegmentDirectionMaxCorrectionAngle) &&
                    correctionAngle <= _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle))
            {
                return;
            }

            _lastEditorLowerBodySegmentDirectionMaxCorrectionSegment = segmentName;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAngle = correctionAngle;
            _lastEditorLowerBodySegmentDirectionMaxPreAngle = preAngle;
            _lastEditorLowerBodySegmentDirectionMaxPostAngle = postAngle;
            _lastEditorLowerBodySegmentDirectionMaxCorrectionAxis = correctionAxis;
            _lastEditorLowerBodySegmentDirectionMaxReferenceDirection = referenceDirection;
            _lastEditorLowerBodySegmentDirectionMaxPreDirection = preDirection;
            _lastEditorLowerBodySegmentDirectionMaxPostDirection = postDirection;
        }

        private void SetEditorLowerBodySegmentDirectionCouplingDiagnostics(
            string segmentName,
            float parentWorldRotationDeltaAngle,
            float childLocalRotationDeltaAngle,
            Vector3 correctionAxis,
            Vector3 referenceDirection,
            Vector3 preDirection,
            Vector3 postDirection)
        {
            switch (segmentName)
            {
                case "LeftLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootParentWorldRotationDeltaAngle = parentWorldRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootChildFootLocalRotationDeltaAngle = childLocalRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegToFootCorrectionAxis = correctionAxis;
                    break;
                case "RightLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionRightLowerLegToFootParentWorldRotationDeltaAngle = parentWorldRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionRightLowerLegToFootChildFootLocalRotationDeltaAngle = childLocalRotationDeltaAngle;
                    _lastEditorLowerBodySegmentDirectionRightLowerLegToFootCorrectionAxis = correctionAxis;
                    break;
                case "LeftFootToToes":
                    _lastEditorLowerBodySegmentDirectionLeftFootToToesReferenceDirection = referenceDirection;
                    _lastEditorLowerBodySegmentDirectionLeftFootToToesPreDirection = preDirection;
                    _lastEditorLowerBodySegmentDirectionLeftFootToToesPostDirection = postDirection;
                    break;
                case "RightFootToToes":
                    _lastEditorLowerBodySegmentDirectionRightFootToToesReferenceDirection = referenceDirection;
                    _lastEditorLowerBodySegmentDirectionRightFootToToesPreDirection = preDirection;
                    _lastEditorLowerBodySegmentDirectionRightFootToToesPostDirection = postDirection;
                    break;
            }
        }

        private void SetEditorLowerBodySegmentDirectionCorrectionAngle(string segmentName, float correctionAngle)
        {
            switch (segmentName)
            {
                case "LeftUpperLegToLowerLeg":
                    _lastEditorLowerBodySegmentDirectionLeftUpperLegLowerLegCorrectionAngle = correctionAngle;
                    break;
                case "RightUpperLegToLowerLeg":
                    _lastEditorLowerBodySegmentDirectionRightUpperLegLowerLegCorrectionAngle = correctionAngle;
                    break;
                case "LeftLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionLeftLowerLegFootCorrectionAngle = correctionAngle;
                    break;
                case "RightLowerLegToFoot":
                    _lastEditorLowerBodySegmentDirectionRightLowerLegFootCorrectionAngle = correctionAngle;
                    break;
                case "LeftFootToToes":
                    _lastEditorLowerBodySegmentDirectionLeftFootToesCorrectionAngle = correctionAngle;
                    break;
                case "RightFootToToes":
                    _lastEditorLowerBodySegmentDirectionRightFootToesCorrectionAngle = correctionAngle;
                    break;
            }
        }

        private static string BuildLowerBodySegmentName(HumanBodyBones parentBone, HumanBodyBones childBone)
        {
            if (parentBone == HumanBodyBones.LeftUpperLeg && childBone == HumanBodyBones.LeftLowerLeg)
            {
                return "LeftUpperLegToLowerLeg";
            }

            if (parentBone == HumanBodyBones.RightUpperLeg && childBone == HumanBodyBones.RightLowerLeg)
            {
                return "RightUpperLegToLowerLeg";
            }

            if (parentBone == HumanBodyBones.LeftLowerLeg && childBone == HumanBodyBones.LeftFoot)
            {
                return "LeftLowerLegToFoot";
            }

            if (parentBone == HumanBodyBones.RightLowerLeg && childBone == HumanBodyBones.RightFoot)
            {
                return "RightLowerLegToFoot";
            }

            if (parentBone == HumanBodyBones.LeftFoot && childBone == HumanBodyBones.LeftToes)
            {
                return "LeftFootToToes";
            }

            if (parentBone == HumanBodyBones.RightFoot && childBone == HumanBodyBones.RightToes)
            {
                return "RightFootToToes";
            }

            return $"{parentBone}To{childBone}";
        }

        private static Vector3 ReadFiniteCorrectionAxis(Quaternion correction)
        {
            if (!IsFinite(correction))
            {
                return BuildNaNVector3();
            }

            correction.ToAngleAxis(out float angle, out Vector3 axis);
            if (!IsFinite(angle) || angle <= 0.001f || !IsFinite(axis))
            {
                return BuildNaNVector3();
            }

            return axis.normalized;
        }

        private static bool TryCalculateEditorLowerBodySegmentDirectionReference(
            Vector3 referenceSegmentDirection,
            Vector3 currentSegmentDirection,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextParentWorldRotation)
        {
            return TryCalculateEditorLowerBodySegmentDirectionReference(
                referenceSegmentDirection,
                currentSegmentDirection,
                currentParentWorldRotation,
                weight,
                maxAngleDegrees,
                1f,
                out nextParentWorldRotation);
        }

        private static bool TryCalculateEditorLowerBodySegmentDirectionReference(
            Vector3 referenceSegmentDirection,
            Vector3 currentSegmentDirection,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            float correctionAxisXzScale,
            out Quaternion nextParentWorldRotation)
        {
            nextParentWorldRotation = currentParentWorldRotation;
            if (!IsFinite(referenceSegmentDirection) ||
                !IsFinite(currentSegmentDirection) ||
                !IsFinite(currentParentWorldRotation) ||
                !TryNormalize(referenceSegmentDirection, out Vector3 referenceDirection) ||
                !TryNormalize(currentSegmentDirection, out Vector3 currentDirection))
            {
                return false;
            }

            Quaternion correction = Quaternion.FromToRotation(currentDirection, referenceDirection);
            if (!IsFinite(correction))
            {
                return false;
            }

            float maxAngle = Mathf.Max(0f, maxAngleDegrees);
            if (maxAngle > 0f)
            {
                float angle = Quaternion.Angle(Quaternion.identity, correction);
                if (angle > maxAngle)
                {
                    correction = Quaternion.Slerp(Quaternion.identity, correction, maxAngle / angle);
                }
            }

            correction = ScaleCorrectionAxisXz(correction, correctionAxisXzScale);
            if (!IsFinite(correction))
            {
                return false;
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, clampedWeight);
            }

            nextParentWorldRotation = correction * currentParentWorldRotation;
            if (!IsFinite(nextParentWorldRotation) ||
                Quaternion.Angle(currentParentWorldRotation, nextParentWorldRotation) <= 0.001f)
            {
                nextParentWorldRotation = currentParentWorldRotation;
                return false;
            }

            return true;
        }

        private static Quaternion ScaleCorrectionAxisXz(Quaternion correction, float axisXzScale)
        {
            float scale = Mathf.Clamp01(axisXzScale);
            if (scale >= 0.999f)
            {
                return correction;
            }

            correction.ToAngleAxis(out float angle, out Vector3 axis);
            if (!IsFinite(angle) || angle <= 0.001f || !IsFinite(axis))
            {
                return correction;
            }

            Vector3 scaledAxis = new Vector3(axis.x * scale, axis.y, axis.z * scale);
            if (!TryNormalize(scaledAxis, out Vector3 normalizedAxis))
            {
                return Quaternion.identity;
            }

            return Quaternion.AngleAxis(angle, normalizedAxis);
        }

        private void ApplyEditorHumanoidFootHipsAlignedResidualYawReference()
        {
            if (!ShouldUseManualAnimatorFootHipsAlignedResidualYawReference ||
                manualAnimatorFootHipsAlignedResidualYawReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            Transform referenceHips = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (referenceHips == null || targetHips == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorFootHipsAlignedResidualYawReferenceWeight);
            float maxAngle = Mathf.Max(0f, manualAnimatorFootHipsAlignedResidualYawReferenceMaxAngle);
            float leftResidual = float.NaN;
            float rightResidual = float.NaN;
            TryCalculateEditorFootHipsAlignedResidualForBone(
                HumanBodyBones.LeftFoot,
                referenceHips,
                targetHips,
                out leftResidual);
            TryCalculateEditorFootHipsAlignedResidualForBone(
                HumanBodyBones.RightFoot,
                referenceHips,
                targetHips,
                out rightResidual);

            bool leftDominantResidual = IsFinite(leftResidual) &&
                (!IsFinite(rightResidual) || leftResidual > rightResidual);
            bool rightDominantResidual = IsFinite(rightResidual) &&
                (!IsFinite(leftResidual) || rightResidual > leftResidual);
            float leftMaxAngle = ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
                leftResidual,
                rightResidual,
                maxAngle,
                leftDominantResidual);
            float rightMaxAngle = ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
                rightResidual,
                leftResidual,
                maxAngle,
                rightDominantResidual);
            CaptureTargetFootPositions(out Vector3 leftFootBefore, out Vector3 rightFootBefore);
            int changed = 0;
            changed += ApplyEditorHumanoidFootHipsAlignedResidualYawReferenceBone(
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.LeftFoot,
                referenceHips,
                targetHips,
                weight,
                leftMaxAngle);
            changed += ApplyEditorHumanoidFootHipsAlignedResidualYawReferenceBone(
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.RightFoot,
                referenceHips,
                targetHips,
                weight,
                rightMaxAngle);
            RecordEditorFootHipsAlignedResidualYawReferenceDiagnostics(leftFootBefore, rightFootBefore);

            if (changed > 0 && !_editorFootHipsAlignedResidualYawReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator hips-aligned foot X/Z residual yaw reference applied. feet={changed}, weight={weight:F2}, maxAngle={maxAngle:F1}deg");
                _editorFootHipsAlignedResidualYawReferenceLogged = true;
            }
        }

        private bool TryCalculateEditorFootHipsAlignedResidualForBone(
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips,
            out float residual)
        {
            residual = float.NaN;
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            if (targetFoot == null ||
                !TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    footBone,
                    referenceHips,
                    targetHips,
                    targetFoot,
                    out Vector3 desiredFootPosition))
            {
                return false;
            }

            Vector3 residualVector = desiredFootPosition - targetFoot.position;
            residualVector.y = 0f;
            residual = residualVector.magnitude;
            return IsFinite(residual);
        }

        private int ApplyEditorHumanoidFootHipsAlignedResidualYawReferenceBone(
            HumanBodyBones upperLegBone,
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips,
            float weight,
            float maxAngle)
        {
            Transform targetUpperLeg = targetAnimator.GetBoneTransform(upperLegBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            if (targetUpperLeg == null || targetFoot == null || referenceFoot == null)
            {
                return 0;
            }

            if (!TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    footBone,
                    referenceHips,
                    targetHips,
                    targetFoot,
                    out Vector3 desiredFootPosition))
            {
                return 0;
            }

            if (!TryCalculateEditorFootHipsAlignedResidualYawReference(
                    desiredFootPosition,
                    targetFoot.position,
                    targetUpperLeg.position,
                    targetUpperLeg.rotation,
                    weight,
                    maxAngle,
                    out Quaternion nextWorldRotation))
            {
                return 0;
            }

            targetUpperLeg.rotation = nextWorldRotation;
            return 1;
        }

        private bool TryCalculateEditorFootHipsAlignedDesiredFootPosition(
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips,
            Transform targetFoot,
            out Vector3 desiredFootPosition)
        {
            desiredFootPosition = targetFoot != null ? targetFoot.position : Vector3.zero;
            if (_editorFingerReferenceAnimator == null ||
                targetAnimator == null ||
                referenceHips == null ||
                targetHips == null ||
                targetFoot == null)
            {
                return false;
            }

            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            if (referenceFoot == null)
            {
                return false;
            }

            Vector3 referenceOffset = referenceFoot.position - referenceHips.position;
            if (!IsFinite(referenceOffset))
            {
                return false;
            }

            Vector3 referenceRootOffset = _editorFingerReferenceAnimator.transform.InverseTransformVector(referenceOffset);
            Vector3 desiredTargetOffset = targetAnimator.transform.TransformVector(referenceRootOffset);
            if (!IsFinite(desiredTargetOffset))
            {
                return false;
            }

            desiredFootPosition = targetHips.position + desiredTargetOffset;
            desiredFootPosition.y = targetFoot.position.y;
            return IsFinite(desiredFootPosition);
        }

        private static bool TryCalculateEditorFootHipsAlignedResidualYawReference(
            Vector3 desiredFootPosition,
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextParentWorldRotation)
        {
            return TryCalculateEditorFootHipsAlignedResidualYawReference(
                desiredFootPosition,
                currentFootPosition,
                pivotPosition,
                currentParentWorldRotation,
                weight,
                maxAngleDegrees,
                out nextParentWorldRotation,
                out _);
        }

        private static bool TryCalculateEditorFootHipsAlignedResidualYawReference(
            Vector3 desiredFootPosition,
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            Quaternion currentParentWorldRotation,
            float weight,
            float maxAngleDegrees,
            out Quaternion nextParentWorldRotation,
            out float yawCorrectionAngle)
        {
            nextParentWorldRotation = currentParentWorldRotation;
            yawCorrectionAngle = float.NaN;
            if (!IsFinite(desiredFootPosition) ||
                !IsFinite(currentFootPosition) ||
                !IsFinite(pivotPosition) ||
                !IsFinite(currentParentWorldRotation))
            {
                return false;
            }

            Vector3 currentOffset = currentFootPosition - pivotPosition;
            Vector3 desiredOffset = desiredFootPosition - pivotPosition;
            currentOffset.y = 0f;
            desiredOffset.y = 0f;
            if (!TryNormalize(currentOffset, out Vector3 currentDirection) ||
                !TryNormalize(desiredOffset, out Vector3 desiredDirection))
            {
                return false;
            }

            Quaternion correction = Quaternion.FromToRotation(currentDirection, desiredDirection);
            if (!IsFinite(correction))
            {
                return false;
            }

            float maxAngle = Mathf.Max(0f, maxAngleDegrees);
            if (maxAngle > 0f)
            {
                float angle = Quaternion.Angle(Quaternion.identity, correction);
                if (angle > maxAngle)
                {
                    correction = Quaternion.Slerp(Quaternion.identity, correction, maxAngle / angle);
                }
            }

            float clampedWeight = Mathf.Clamp01(weight);
            if (clampedWeight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, clampedWeight);
            }
            yawCorrectionAngle = Quaternion.Angle(Quaternion.identity, correction);

            if (yawCorrectionAngle <= 0.001f)
            {
                return false;
            }

            nextParentWorldRotation = correction * currentParentWorldRotation;
            if (!IsFinite(nextParentWorldRotation) ||
                Quaternion.Angle(currentParentWorldRotation, nextParentWorldRotation) <= 0.001f)
            {
                nextParentWorldRotation = currentParentWorldRotation;
                return false;
            }

            return true;
        }

        private static float ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
            float thisFootResidual,
            float otherFootResidual,
            float requestedMaxAngle,
            bool isThisFootDominantResidual)
        {
            float maxAngle = Mathf.Max(0f, requestedMaxAngle);
            if (maxAngle <= FootHipsAlignedResidualYawProtectedMaxAngle ||
                !IsFinite(thisFootResidual) ||
                !IsFinite(otherFootResidual) ||
                isThisFootDominantResidual)
            {
                return maxAngle;
            }

            bool thisFootAlreadyPassing = thisFootResidual <= FootHipsAlignedResidualYawGateMeters;
            bool otherFootStillFailing = otherFootResidual > FootHipsAlignedResidualYawGateMeters;
            bool meaningfulSideGap = otherFootResidual - thisFootResidual >= FootHipsAlignedResidualYawSideGapMeters;
            if (thisFootAlreadyPassing && otherFootStillFailing && meaningfulSideGap)
            {
                return Mathf.Min(maxAngle, FootHipsAlignedResidualYawProtectedMaxAngle);
            }

            return maxAngle;
        }

        private void ApplyPreSetHumanPoseRightEndpointPositionReference()
        {
            if (!usePreSetHumanPoseRightEndpointPositionReference ||
                preSetHumanPoseRightEndpointPositionReferenceWeight <= 0f ||
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            Transform referenceHips = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            bool useLeftSide = ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            HumanBodyBones footBone = useLeftSide ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            HumanBodyBones toesBone = useLeftSide ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
            HumanBodyBones upperLegBone = useLeftSide ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            Transform targetUpperLeg = targetAnimator.GetBoneTransform(upperLegBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            Transform targetToes = targetAnimator.GetBoneTransform(toesBone);
            if (referenceHips == null ||
                targetHips == null ||
                targetUpperLeg == null ||
                targetFoot == null)
            {
                return;
            }

            if (!ShouldApplyPreSetHumanPoseRightEndpointPositionFrameGate())
            {
                return;
            }

            if (!TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    footBone,
                    referenceHips,
                    targetHips,
                    targetFoot,
                    out Vector3 desiredFootPosition))
            {
                return;
            }

            Vector3 desiredToesPosition = BuildNaNVector3();
            if (targetToes != null)
            {
                TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                    toesBone,
                    referenceHips,
                    targetHips,
                    targetToes,
                    out desiredToesPosition);
            }

            if (!TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                    desiredFootPosition,
                    desiredToesPosition,
                    targetFoot.position,
                    targetToes != null ? targetToes.position : BuildNaNVector3(),
                    preSetHumanPoseRightEndpointPositionReferenceWeight,
                    preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    preSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    preSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    out Vector3 nextFootPosition))
            {
                return;
            }

            float maxAngleDegrees = CalculateEndpointPositionMaxYawAngle(
                targetFoot.position,
                targetUpperLeg.position,
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            if (!TryCalculateEditorFootHipsAlignedResidualYawReference(
                    nextFootPosition,
                    targetFoot.position,
                    targetUpperLeg.position,
                    targetUpperLeg.rotation,
                    1f,
                    maxAngleDegrees,
                    out Quaternion nextWorldRotation,
                    out _))
            {
                return;
            }

            targetUpperLeg.rotation = nextWorldRotation;
        }

        private void ApplyPreSetHumanPoseSignCorrectedRowLocalBodyPositionReference(ref HumanPose pose)
        {
            ResetPreSetHumanPoseEndpointBodyPositionDiagnostics();
            if (!usePreSetHumanPoseRightEndpointPositionReference ||
                !preSetHumanPoseEndpointPositionUseGhostCurrentBasis ||
                preSetHumanPoseRightEndpointPositionReferenceWeight <= 0f ||
                preSetHumanPoseRightEndpointPositionReferenceMaxOffset <= 0f ||
                ghostAnimator == null ||
                targetAnimator == null ||
                !IsFinite(pose.bodyPosition))
            {
                return;
            }

            if (!ShouldApplyPreSetHumanPoseRightEndpointPositionFrameGate())
            {
                return;
            }

            RetargetEndpointStageWorldPositions ghostPositions = Diagnostics.CaptureEndpointStageWorldPositions(ghostAnimator);
            RetargetEndpointStageWorldPositions currentPositions = _lastSetHumanPosePreSolveCurrentEndpointPositions;
            bool useLeftSide = ShouldUseLeftSideForPreSetHumanPoseEndpointPosition;
            Vector3 ghostFootPosition = useLeftSide ? ghostPositions.LeftFoot : ghostPositions.RightFoot;
            Vector3 currentFootPosition = useLeftSide ? currentPositions.LeftFoot : currentPositions.RightFoot;
            Vector3 bodyPositionBefore = pose.bodyPosition;

            if (TryCalculateSignCorrectedRowLocalBodyPositionXzReference(
                    bodyPositionBefore,
                    ghostFootPosition,
                    currentFootPosition,
                    preSetHumanPoseRightEndpointPositionReferenceWeight,
                    preSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    axisXScale: 1f,
                    axisZScale: 1f,
                    invertX: ShouldInvertPreSetHumanPoseEndpointPositionBodyX,
                    invertZ: ShouldInvertPreSetHumanPoseEndpointPositionBodyZ,
                    out Vector3 nextBodyPosition))
            {
                CapturePreSetHumanPoseEndpointBodyPositionDiagnostics(bodyPositionBefore, nextBodyPosition);
                pose.bodyPosition = nextBodyPosition;
            }
        }

        private void ResetPreSetHumanPoseEndpointBodyPositionDiagnostics()
        {
            _lastPreSetHumanPoseEndpointBodyPositionBefore = BuildNaNVector3();
            _lastPreSetHumanPoseEndpointBodyPositionAfter = BuildNaNVector3();
            _lastPreSetHumanPoseEndpointBodyPositionDelta = BuildNaNVector3();
        }

        private void CapturePreSetHumanPoseEndpointBodyPositionDiagnostics(Vector3 before, Vector3 after)
        {
            if (!IsFinite(before) || !IsFinite(after))
            {
                ResetPreSetHumanPoseEndpointBodyPositionDiagnostics();
                return;
            }

            _lastPreSetHumanPoseEndpointBodyPositionBefore = before;
            _lastPreSetHumanPoseEndpointBodyPositionAfter = after;
            _lastPreSetHumanPoseEndpointBodyPositionDelta = after - before;
        }

        private void ApplyPostSetHumanPoseRightEndpointPositionReference()
        {
            ResetPostSetHumanPoseRightEndpointPositionDiagnostics();
            if (!usePostSetHumanPoseRightFootEvaluatorXzReference)
            {
                _hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset = false;
                _postSetHumanPoseRightFootEvaluatorXzFirstOffset = BuildNaNVector3();
            }

            if (!usePostSetHumanPoseRightEndpointPositionReference ||
                postSetHumanPoseRightEndpointPositionReferenceWeight <= 0f ||
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            Transform referenceHips = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            bool useLeftSide = ShouldUseLeftSideForPostSetHumanPoseEndpointPosition;
            HumanBodyBones footBone = useLeftSide ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            HumanBodyBones toesBone = useLeftSide ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
            HumanBodyBones upperLegBone = useLeftSide ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            Transform targetUpperLeg = targetAnimator.GetBoneTransform(upperLegBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            Transform targetToes = targetAnimator.GetBoneTransform(toesBone);
            if (referenceHips == null ||
                targetHips == null ||
                targetUpperLeg == null ||
                targetFoot == null)
            {
                return;
            }

            Vector3 evaluatorXzFirstOffset = BuildNaNVector3();
            if (usePostSetHumanPoseRightFootEvaluatorXzReference)
            {
                if (referenceFoot == null ||
                    !TryResolvePostSetHumanPoseRightFootEvaluatorXzFirstOffset(
                        referenceFoot.position,
                        targetFoot.position,
                        out evaluatorXzFirstOffset))
                {
                    return;
                }
            }

            if (!ShouldApplyPostSetHumanPoseRightEndpointPositionFrameGate())
            {
                return;
            }

            Vector3 nextFootPosition;
            PostSetHumanPoseEndpointPositionDiagnostics endpointDiagnostics;
            bool calculated;
            if (usePostSetHumanPoseRightFootEvaluatorXzReference)
            {
                calculated = TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
                    referenceFoot.position,
                    targetFoot.position,
                    evaluatorXzFirstOffset,
                    postSetHumanPoseRightFootEvaluatorXzReferenceTargetMagnitude,
                    postSetHumanPoseRightEndpointPositionReferenceWeight,
                    postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    out nextFootPosition,
                    out endpointDiagnostics);
            }
            else
            {
                if (!TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                        footBone,
                        referenceHips,
                        targetHips,
                        targetFoot,
                        out Vector3 desiredFootPosition))
                {
                    return;
                }

                Vector3 desiredToesPosition = BuildNaNVector3();
                if (targetToes != null)
                {
                    TryCalculateEditorFootHipsAlignedDesiredFootPosition(
                        toesBone,
                        referenceHips,
                        targetHips,
                        targetToes,
                        out desiredToesPosition);
                }

                calculated = TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                    desiredFootPosition,
                    desiredToesPosition,
                    targetFoot.position,
                    targetToes != null ? targetToes.position : BuildNaNVector3(),
                    postSetHumanPoseRightEndpointPositionReferenceWeight,
                    postSetHumanPoseRightEndpointPositionReferenceMaxOffset,
                    postSetHumanPoseRightEndpointPositionReferencePositiveZScale,
                    postSetHumanPoseRightEndpointPositionReferenceToesBlendWeight,
                    out nextFootPosition,
                    out endpointDiagnostics);
            }

            if (!calculated)
            {
                RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
                    endpointDiagnostics,
                    maxYawAngle: float.NaN,
                    yawCorrectionAngle: float.NaN,
                    upperLegRotationDeltaAngle: float.NaN,
                    applied: 0f);
                return;
            }

            float maxAngleDegrees = CalculateEndpointPositionMaxYawAngle(
                targetFoot.position,
                targetUpperLeg.position,
                postSetHumanPoseRightEndpointPositionReferenceMaxOffset);
            if (!TryCalculateEditorFootHipsAlignedResidualYawReference(
                    nextFootPosition,
                    targetFoot.position,
                    targetUpperLeg.position,
                    targetUpperLeg.rotation,
                    1f,
                    maxAngleDegrees,
                    out Quaternion nextWorldRotation,
                    out float yawCorrectionAngle))
            {
                RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
                    endpointDiagnostics,
                    maxAngleDegrees,
                    yawCorrectionAngle,
                    upperLegRotationDeltaAngle: float.NaN,
                    applied: 0f);
                return;
            }

            float upperLegRotationDeltaAngle = Quaternion.Angle(targetUpperLeg.rotation, nextWorldRotation);
            RecordPostSetHumanPoseRightEndpointPositionDiagnostics(
                endpointDiagnostics,
                maxAngleDegrees,
                yawCorrectionAngle,
                upperLegRotationDeltaAngle,
                applied: 1f);

            targetUpperLeg.rotation = nextWorldRotation;
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            out Vector3 nextFootPosition)
        {
            return TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight: 1f,
                out nextFootPosition);
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            out Vector3 nextFootPosition)
        {
            return TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
                desiredFootPosition,
                desiredToesPosition,
                currentFootPosition,
                currentToesPosition,
                weight,
                maxOffset,
                positiveZScale,
                toesBlendWeight,
                out nextFootPosition,
                out _);
        }

        private static bool TryCalculatePostSetHumanPoseEndpointDesiredFootPosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            out Vector3 nextFootPosition,
            out PostSetHumanPoseEndpointPositionDiagnostics diagnostics)
        {
            nextFootPosition = currentFootPosition;
            diagnostics = PostSetHumanPoseEndpointPositionDiagnostics.Empty;
            diagnostics.DesiredFootPosition = desiredFootPosition;
            diagnostics.DesiredToesPosition = desiredToesPosition;
            diagnostics.CurrentFootPosition = currentFootPosition;
            diagnostics.CurrentToesPosition = currentToesPosition;

            if (!IsFinite(desiredFootPosition) ||
                !IsFinite(currentFootPosition))
            {
                return false;
            }

            Vector3 footDelta = desiredFootPosition - currentFootPosition;
            footDelta.y = 0f;
            Vector3 endpointDelta = footDelta;
            if (IsFinite(desiredToesPosition) && IsFinite(currentToesPosition))
            {
                Vector3 toesDelta = desiredToesPosition - currentToesPosition;
                toesDelta.y = 0f;
                Vector3 averagedEndpointDelta = (footDelta + toesDelta) * 0.5f;
                endpointDelta = Vector3.Lerp(footDelta, averagedEndpointDelta, Mathf.Clamp01(toesBlendWeight));
            }
            diagnostics.EndpointDeltaBeforeClamp = endpointDelta;

            if (!IsFinite(endpointDelta) || endpointDelta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                endpointDelta = Vector3.ClampMagnitude(endpointDelta, clampedMaxOffset);
            }
            diagnostics.EndpointDeltaAfterClamp = endpointDelta;

            if (endpointDelta.z > 0f)
            {
                endpointDelta.z *= Mathf.Clamp01(positiveZScale);
            }
            diagnostics.EndpointDeltaAfterPositiveZScale = endpointDelta;

            Vector3 correction = endpointDelta * Mathf.Clamp01(weight);
            correction.y = 0f;
            diagnostics.Correction = correction;
            if (!IsFinite(correction) || correction.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            nextFootPosition = currentFootPosition + correction;
            nextFootPosition.y = currentFootPosition.y;
            diagnostics.NextFootPosition = nextFootPosition;
            return IsFinite(nextFootPosition);
        }

        private bool TryResolvePostSetHumanPoseRightFootEvaluatorXzFirstOffset(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            out Vector3 firstOffset)
        {
            firstOffset = _postSetHumanPoseRightFootEvaluatorXzFirstOffset;
            if (!IsFinite(referenceFootPosition) || !IsFinite(currentFootPosition))
            {
                return false;
            }

            if (!_hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset ||
                !IsFinite(_postSetHumanPoseRightFootEvaluatorXzFirstOffset))
            {
                _postSetHumanPoseRightFootEvaluatorXzFirstOffset = currentFootPosition - referenceFootPosition;
                _postSetHumanPoseRightFootEvaluatorXzFirstOffset.y = 0f;
                _hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset =
                    IsFinite(_postSetHumanPoseRightFootEvaluatorXzFirstOffset);
            }

            firstOffset = _postSetHumanPoseRightFootEvaluatorXzFirstOffset;
            return _hasPostSetHumanPoseRightFootEvaluatorXzFirstOffset && IsFinite(firstOffset);
        }

        private static bool TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition)
        {
            return TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
                referenceFootPosition,
                currentFootPosition,
                firstMatchedFootOffset,
                targetMagnitude,
                weight,
                maxOffset,
                out nextFootPosition,
                out _);
        }

        private static bool TryCalculatePostSetHumanPoseEvaluatorXzReferenceDesiredFootPosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition,
            out PostSetHumanPoseEndpointPositionDiagnostics diagnostics)
        {
            nextFootPosition = currentFootPosition;
            diagnostics = PostSetHumanPoseEndpointPositionDiagnostics.Empty;
            diagnostics.CurrentFootPosition = currentFootPosition;
            diagnostics.CurrentToesPosition = BuildNaNVector3();
            diagnostics.EvaluatorXzReferenceEnabled = 1f;
            diagnostics.EvaluatorXzFirstOffset = firstMatchedFootOffset;
            diagnostics.EvaluatorXzTargetMagnitude = Mathf.Max(0f, targetMagnitude);

            if (!IsFinite(referenceFootPosition) ||
                !IsFinite(currentFootPosition) ||
                !IsFinite(firstMatchedFootOffset))
            {
                return false;
            }

            Vector3 normalizedDelta = currentFootPosition - referenceFootPosition - firstMatchedFootOffset;
            normalizedDelta.y = 0f;
            diagnostics.EvaluatorXzNormalizedDelta = normalizedDelta;
            diagnostics.DesiredToesPosition = BuildNaNVector3();
            if (!IsFinite(normalizedDelta) || normalizedDelta.sqrMagnitude <= 0.00000001f)
            {
                diagnostics.DesiredFootPosition = currentFootPosition;
                return false;
            }

            float magnitude = normalizedDelta.magnitude;
            float clampedTargetMagnitude = Mathf.Max(0f, targetMagnitude);
            if (!IsFinite(magnitude) || magnitude <= clampedTargetMagnitude || magnitude <= 0f)
            {
                diagnostics.DesiredFootPosition = currentFootPosition;
                return false;
            }

            Vector3 desiredNormalizedDelta = normalizedDelta * (clampedTargetMagnitude / magnitude);
            diagnostics.EvaluatorXzDesiredNormalizedDelta = desiredNormalizedDelta;
            Vector3 correction = desiredNormalizedDelta - normalizedDelta;
            correction.y = 0f;
            diagnostics.EndpointDeltaBeforeClamp = correction;

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                correction = Vector3.ClampMagnitude(correction, clampedMaxOffset);
            }
            diagnostics.EndpointDeltaAfterClamp = correction;
            diagnostics.EndpointDeltaAfterPositiveZScale = correction;

            correction *= Mathf.Clamp01(weight);
            correction.y = 0f;
            diagnostics.Correction = correction;
            if (!IsFinite(correction) || correction.sqrMagnitude <= 0.00000001f)
            {
                diagnostics.DesiredFootPosition = currentFootPosition;
                return false;
            }

            nextFootPosition = currentFootPosition + correction;
            nextFootPosition.y = currentFootPosition.y;
            diagnostics.DesiredFootPosition = nextFootPosition;
            diagnostics.NextFootPosition = nextFootPosition;
            return IsFinite(nextFootPosition);
        }

        private static float CalculateEndpointPositionMaxYawAngle(
            Vector3 currentFootPosition,
            Vector3 pivotPosition,
            float maxOffset)
        {
            Vector3 currentOffset = currentFootPosition - pivotPosition;
            currentOffset.y = 0f;
            float radius = currentOffset.magnitude;
            if (!IsFinite(currentOffset) || radius <= 0.0001f)
            {
                return 0f;
            }

            float normalizedOffset = Mathf.Clamp01(Mathf.Max(0f, maxOffset) / radius);
            if (normalizedOffset <= 0f)
            {
                return 0f;
            }

            return Mathf.Asin(normalizedOffset) * Mathf.Rad2Deg;
        }

        private void ApplyEditorHumanoidBipedIkFootPositionReference()
        {
            if (!useManualAnimatorBipedIkFootPositionReference ||
                manualAnimatorBipedIkFootPositionReferenceWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                DisableOwnedEditorManualFootBipedIk();
                return;
            }

            if (!UpdateEditorManualReferenceAnimator())
            {
                return;
            }

            BipedIK bipedIk = EnsureEditorManualFootBipedIk();
            if (bipedIk == null)
            {
                return;
            }

            Transform referenceHips = _editorFingerReferenceAnimator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetHips = targetAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (referenceHips == null || targetHips == null)
            {
                return;
            }

            int changed = 0;
            changed += ApplyEditorHumanoidBipedIkFootPositionReferenceGoal(
                bipedIk,
                AvatarIKGoal.LeftFoot,
                HumanBodyBones.LeftFoot,
                referenceHips,
                targetHips);
            changed += ApplyEditorHumanoidBipedIkFootPositionReferenceGoal(
                bipedIk,
                AvatarIKGoal.RightFoot,
                HumanBodyBones.RightFoot,
                referenceHips,
                targetHips);

            if (changed <= 0)
            {
                return;
            }

            bipedIk.UpdateSolverExternal();
            if (!_editorFootIkPositionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator BipedIK foot position reference applied. feet={changed}, weight={manualAnimatorBipedIkFootPositionReferenceWeight:F2}, maxOffset={manualAnimatorBipedIkFootPositionReferenceMaxOffset:F3}m");
                _editorFootIkPositionReferenceLogged = true;
            }
        }

        private BipedIK EnsureEditorManualFootBipedIk()
        {
            if (targetAnimator == null)
            {
                return null;
            }

            if (_editorManualFootBipedIk == null)
            {
                _editorManualFootBipedIk = targetAnimator.GetComponent<BipedIK>();
                if (_editorManualFootBipedIk == null)
                {
                    _editorManualFootBipedIk = targetAnimator.gameObject.AddComponent<BipedIK>();
                    _editorManualFootBipedIkCreated = true;
                }
                _editorManualFootBipedIkInitiated = false;
            }

            if (_editorManualFootBipedIk == null)
            {
                return null;
            }

            if (targetAnimator.isHuman)
            {
                BipedReferences references = _editorManualFootBipedIk.references;
                BipedReferences.AutoDetectReferences(
                    ref references,
                    targetAnimator.transform,
                    BipedReferences.AutoDetectParams.Default);
                _editorManualFootBipedIk.references = references;
            }

            _editorManualFootBipedIk.enabled = true;
            _editorManualFootBipedIk.fixTransforms = false;
            if (!_editorManualFootBipedIkInitiated)
            {
                _editorManualFootBipedIk.InitiateBipedIK();
                _editorManualFootBipedIkInitiated = true;
            }
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            _editorManualFootBipedIk.solvers.leftFoot.maintainRotationWeight = 1f;
            _editorManualFootBipedIk.solvers.rightFoot.maintainRotationWeight = 1f;
            return _editorManualFootBipedIk;
        }

        private void DisableOwnedEditorManualFootBipedIk()
        {
            if (_editorManualFootBipedIk == null)
            {
                return;
            }

            _editorManualFootBipedIk.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            _editorManualFootBipedIk.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            _editorManualFootBipedIk.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
            if (_editorManualFootBipedIkCreated)
            {
                _editorManualFootBipedIk.fixTransforms = false;
                _editorManualFootBipedIk.enabled = false;
            }
        }

        private int ApplyEditorHumanoidBipedIkFootPositionReferenceGoal(
            BipedIK bipedIk,
            AvatarIKGoal goal,
            HumanBodyBones footBone,
            Transform referenceHips,
            Transform targetHips)
        {
            Transform referenceFoot = _editorFingerReferenceAnimator.GetBoneTransform(footBone);
            Transform targetFoot = targetAnimator.GetBoneTransform(footBone);
            if (referenceFoot == null || targetFoot == null)
            {
                bipedIk.SetIKPositionWeight(goal, 0f);
                return 0;
            }

            if (!TryCalculateEditorFootIkPositionReference(
                    referenceFoot.position,
                    referenceHips.position,
                    targetFoot.position,
                    targetHips.position,
                    manualAnimatorBipedIkFootPositionReferenceWeight,
                    manualAnimatorBipedIkFootPositionReferenceMaxOffset,
                    out Vector3 nextPosition))
            {
                bipedIk.SetIKPositionWeight(goal, 0f);
                return 0;
            }

            bipedIk.SetIKPosition(goal, nextPosition);
            bipedIk.SetIKPositionWeight(goal, 1f);
            return 1;
        }

        private static bool TryCalculateEditorFootIkPositionReference(
            Vector3 referenceFootPosition,
            Vector3 referenceHipsPosition,
            Vector3 currentFootPosition,
            Vector3 targetHipsPosition,
            float weight,
            float maxOffset,
            out Vector3 nextPosition)
        {
            nextPosition = currentFootPosition;
            if (!IsFinite(referenceFootPosition) ||
                !IsFinite(referenceHipsPosition) ||
                !IsFinite(currentFootPosition) ||
                !IsFinite(targetHipsPosition))
            {
                return false;
            }

            Vector3 desiredPosition = targetHipsPosition + (referenceFootPosition - referenceHipsPosition);
            Vector3 delta = desiredPosition - currentFootPosition;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return false;
            }

            float clampedMaxOffset = Mathf.Max(0f, maxOffset);
            if (clampedMaxOffset > 0f)
            {
                delta = Vector3.ClampMagnitude(delta, clampedMaxOffset);
            }

            nextPosition = currentFootPosition + delta * Mathf.Clamp01(weight);
            if (!IsFinite(nextPosition))
            {
                nextPosition = currentFootPosition;
                return false;
            }

            return true;
        }

        private void ApplyEditorHumanoidThumbLocalRotationReference()
        {
            if (!useManualAnimatorThumbLocalRotationReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            bool suppressLeftThumbLocalRotation = ShouldSuppressCompetingManualThumbOverride(true);
            bool suppressRightThumbLocalRotation = ShouldSuppressCompetingManualThumbOverride(false);
            int changed = 0;
            foreach (HumanBodyBones thumbBone in ThumbRotationBones)
            {
                Transform source = _editorFingerReferenceAnimator.GetBoneTransform(thumbBone);
                Transform target = targetAnimator.GetBoneTransform(thumbBone);
                if (source == null || target == null)
                {
                    continue;
                }

                if ((thumbBone == HumanBodyBones.LeftThumbProximal ||
                        thumbBone == HumanBodyBones.LeftThumbIntermediate ||
                        thumbBone == HumanBodyBones.LeftThumbDistal) &&
                    suppressLeftThumbLocalRotation)
                {
                    continue;
                }

                if ((thumbBone == HumanBodyBones.RightThumbProximal ||
                        thumbBone == HumanBodyBones.RightThumbIntermediate ||
                        thumbBone == HumanBodyBones.RightThumbDistal) &&
                    suppressRightThumbLocalRotation)
                {
                    continue;
                }

                Quaternion sourceRotation = source.localRotation;
                if (!IsFinite(sourceRotation) || Quaternion.Angle(target.localRotation, sourceRotation) <= 0.001f)
                {
                    continue;
                }

                target.localRotation = sourceRotation;
                changed++;
            }

            if (changed > 0 && !_editorThumbLocalRotationReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator thumb localRotation reference applied. bones={changed}");
                _editorThumbLocalRotationReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidThumbBasePositionReference()
        {
            if (!useManualAnimatorThumbBasePositionReference ||
                manualAnimatorThumbBasePositionWeight <= 0f ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            int changed = 0;
            changed += ApplyEditorHumanoidThumbBasePositionReferenceSide(
                HumanBodyBones.LeftHand,
                HumanBodyBones.LeftThumbProximal,
                HumanBodyBones.LeftIndexProximal,
                LeftThumbBaseHelperNameSuffix);
            changed += ApplyEditorHumanoidThumbBasePositionReferenceSide(
                HumanBodyBones.RightHand,
                HumanBodyBones.RightThumbProximal,
                HumanBodyBones.RightIndexProximal,
                RightThumbBaseHelperNameSuffix);

            if (changed > 0 && !_editorThumbBasePositionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator thumb base position reference applied. targets={changed}, weight={manualAnimatorThumbBasePositionWeight:F2}, maxOffset={manualAnimatorThumbBasePositionMaxOffset:F4}");
                _editorThumbBasePositionReferenceLogged = true;
            }
        }

        private int ApplyEditorHumanoidThumbBasePositionReferenceSide(
            HumanBodyBones handBone,
            HumanBodyBones thumbBone,
            HumanBodyBones indexBone,
            string helperNameSuffix)
        {
            Transform referenceThumb = _editorFingerReferenceAnimator.GetBoneTransform(thumbBone);
            Transform targetThumb = targetAnimator.GetBoneTransform(thumbBone);

            if (referenceThumb == null || targetThumb == null)
            {
                return 0;
            }

            bool leftHand = handBone == HumanBodyBones.LeftHand;
            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referencePalmLocalThumb = referenceFrame.InverseTransformPoint(referenceThumb.position);
            float palmScale = Mathf.Clamp(targetFrame.Scale / referenceFrame.Scale, 0.25f, 4f);
            Vector3 desiredWorldPosition = targetFrame.TransformPoint(referencePalmLocalThumb * palmScale);
            if (ShouldRejectManualThumbBasePositionOverride(leftHand, targetThumb, desiredWorldPosition))
            {
                return 0;
            }

            int changed = 0;
            changed += ApplyThumbBasePositionToTransform(
                targetThumb,
                desiredWorldPosition,
                _targetInitialHumanoidLocalPositions);

            Transform helperTransform = GetCachedThumbBaseHelper(leftHand) ?? FindTargetTransformByNameSuffix(helperNameSuffix);
            if (helperTransform != null && helperTransform != targetThumb)
            {
                changed += ApplyThumbBasePositionToTransform(
                    helperTransform,
                    desiredWorldPosition,
                    _targetInitialThumbBaseHelperLocalPositions);
            }

            return changed;
        }

        private int ApplyThumbBasePositionToTransform(
            Transform targetTransform,
            Vector3 desiredWorldPosition,
            IDictionary<Transform, Vector3> initialLocalPositions)
        {
            if (targetTransform == null || targetTransform.parent == null)
            {
                return 0;
            }

            if (!initialLocalPositions.TryGetValue(targetTransform, out Vector3 initialLocalPosition))
            {
                initialLocalPosition = targetTransform.localPosition;
                initialLocalPositions[targetTransform] = initialLocalPosition;
            }

            Vector3 desiredLocalPosition = targetTransform.parent.InverseTransformPoint(desiredWorldPosition);
            float maxOffset = Mathf.Max(0f, manualAnimatorThumbBasePositionMaxOffset);
            if (maxOffset > 0f)
            {
                desiredLocalPosition = initialLocalPosition + Vector3.ClampMagnitude(desiredLocalPosition - initialLocalPosition, maxOffset);
            }

            Vector3 targetLocalPosition = Vector3.Lerp(
                initialLocalPosition,
                desiredLocalPosition,
                Mathf.Clamp01(manualAnimatorThumbBasePositionWeight));

            if ((targetTransform.localPosition - targetLocalPosition).sqrMagnitude <= 0.00000001f)
            {
                return 0;
            }

            targetTransform.localPosition = targetLocalPosition;
            return 1;
        }

        private void ApplyEditorHumanoidThumbSegmentDirectionReference()
        {
            if (!useManualAnimatorThumbSegmentDirectionReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorThumbSegmentDirectionWeight);
            if (weight <= 0.0001f)
            {
                return;
            }

            int changed = 0;
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += AlignEditorHumanoidThumbSegmentDirection(true, HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, weight);
                changed += AlignEditorHumanoidThumbSegmentDirection(true, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal, weight);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += AlignEditorHumanoidThumbSegmentDirection(false, HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, weight);
                changed += AlignEditorHumanoidThumbSegmentDirection(false, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal, weight);
            }

            if (changed > 0 && !_editorThumbSegmentDirectionReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator thumb segment direction reference applied. segments={changed}, weight={weight:F2}");
                _editorThumbSegmentDirectionReferenceLogged = true;
            }
        }

        private void ApplyEditorHumanoidThumbHandDirectionReference()
        {
            if (!useManualAnimatorThumbHandDirectionReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorThumbHandDirectionWeight);
            if (weight <= 0.0001f)
            {
                return;
            }

            int changed = 0;
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += AlignEditorHumanoidThumbHandDirection(true, weight);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += AlignEditorHumanoidThumbHandDirection(false, weight);
            }

            if (changed <= 0)
            {
                return;
            }
        }

        private int AlignEditorHumanoidThumbHandDirection(bool leftHand, float weight)
        {
            Transform targetHand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform referenceHand = _editorFingerReferenceAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform targetProximal = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            Transform targetIntermediate = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
            Transform referenceIntermediate = _editorFingerReferenceAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);

            if (targetHand == null || referenceHand == null ||
                targetProximal == null || targetIntermediate == null ||
                referenceIntermediate == null)
            {
                return 0;
            }

            Vector3 targetDirection = targetIntermediate.position - targetHand.position;
            Vector3 referenceDirection = referenceIntermediate.position - referenceHand.position;
            if (!IsFinite(targetDirection) || !IsFinite(referenceDirection) ||
                targetDirection.sqrMagnitude <= 0.00000001f ||
                referenceDirection.sqrMagnitude <= 0.00000001f)
            {
                return 0;
            }

            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referenceHandDirection = referenceFrame.InverseTransformDirection(referenceDirection.normalized).normalized;
            Vector3 desiredWorldDirection = targetFrame.TransformDirection(referenceHandDirection).normalized;
            Vector3 currentWorldDirection = targetDirection.normalized;
            if (!IsFinite(referenceHandDirection) || !IsFinite(desiredWorldDirection) || !IsFinite(currentWorldDirection))
            {
                return 0;
            }

            Quaternion correction = Quaternion.FromToRotation(currentWorldDirection, desiredWorldDirection);
            if (!IsFinite(correction))
            {
                return 0;
            }

            if (weight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, weight);
            }

            Quaternion nextWorldRotation = correction * targetProximal.rotation;
            if (!IsFinite(nextWorldRotation) || Quaternion.Angle(targetProximal.rotation, nextWorldRotation) <= 0.001f)
            {
                return 0;
            }

            targetProximal.rotation = nextWorldRotation;
            return 1;
        }

        private void ApplyEditorHumanoidHandPalmFrameReference()
        {
            if (!useManualAnimatorHandPalmFrameReference ||
                !_useEditorFingerPoseReference ||
                _editorFingerReferenceAnimator == null ||
                targetAnimator == null)
            {
                return;
            }

            float weight = Mathf.Clamp01(manualAnimatorHandPalmFrameWeight);
            if (weight <= 0.0001f)
            {
                return;
            }

            int changed = 0;
            if (!ShouldSuppressCompetingManualThumbOverride(true))
            {
                changed += AlignEditorHumanoidHandPalmFrame(true, weight);
            }

            if (!ShouldSuppressCompetingManualThumbOverride(false))
            {
                changed += AlignEditorHumanoidHandPalmFrame(false, weight);
            }

            if (changed > 0 && !_editorHandPalmFrameReferenceLogged)
            {
                Debug.Log($"[PoseSpaceRetargeter] Manual Animator hand palm-frame reference applied. hands={changed}, weight={weight:F2}");
                _editorHandPalmFrameReferenceLogged = true;
            }
        }

        private bool ShouldSuppressCompetingManualThumbOverrideEditor(bool leftHand)
        {
            if (!TryEvaluateThumbManualOverrideRisk(leftHand, out float risk) ||
                risk < ManualThumbOverrideSuppressRiskThreshold)
            {
                return false;
            }

            return !ShouldKeepDetachedHelperManualThumbOverrides(leftHand);
        }

        private bool ShouldKeepDetachedHelperManualThumbOverridesEditor(bool leftHand)
        {
            HumanBodyBones proximalBone = leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal;
            if (!HasDetachedThumbBaseHelperRelationship(proximalBone, leftHand) ||
                !TryEvaluateCurrentThumbReferenceFrameDelta(leftHand, out float spreadDelta, out float projectionDelta))
            {
                return false;
            }

            return spreadDelta <= ManualThumbDetachedHelperOverrideKeepSpreadDeltaMax &&
                projectionDelta <= ManualThumbDetachedHelperOverrideKeepProjectionDeltaMax;
        }

        private bool TryGetHighRiskManualThumbPoseConstraintOverridesEditor(
            bool leftHand,
            out float projectionMin,
            out float projectionMax,
            out float maxSpreadAngle)
        {
            projectionMin = ManualThumbOverrideProjectionMin;
            projectionMax = ManualThumbOverrideProjectionMax;
            maxSpreadAngle = ManualThumbOverrideSpreadFullRiskAngle;

            bool manualOverridePathActive =
                ShouldSuppressCompetingManualThumbOverride(leftHand) ||
                ShouldKeepDetachedHelperManualThumbOverrides(leftHand);

            return manualOverridePathActive &&
                TryEvaluateThumbManualOverrideRisk(leftHand, out float risk) &&
                IsFinite(risk) &&
                risk >= ManualThumbPoseShapingSuppressMaxRisk;
        }

        private string BuildThumbHelperRelationshipDebugSummaryEditor(bool leftHand)
        {
            Transform helperTransform = GetCachedThumbBaseHelper(leftHand);
            Transform sourceTransform = GetCachedExplicitThumbBaseSource(leftHand);
            string sideLabel = leftHand ? "L" : "R";

            if (helperTransform == null || sourceTransform == null)
            {
                return $"side={sideLabel}, helper={GetHierarchyPath(helperTransform)}, source={GetHierarchyPath(sourceTransform)}, state=missing";
            }

            float currentDistance = Vector3.Distance(helperTransform.position, sourceTransform.position);
            float initialDistance = _initialThumbBaseHelperSourceDistances.TryGetValue(leftHand, out float storedDistance)
                ? storedDistance
                : float.NaN;
            float distanceDelta = IsFinite(initialDistance) && IsFinite(currentDistance)
                ? Mathf.Abs(currentDistance - initialDistance)
                : float.NaN;

            float relativeRotationDelta = float.NaN;
            if (_initialThumbBaseHelperSourceRelativeRotations.TryGetValue(leftHand, out Quaternion initialRelativeRotation))
            {
                Quaternion currentRelativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
                relativeRotationDelta = Quaternion.Angle(initialRelativeRotation, currentRelativeRotation);
            }

            float risk = float.NaN;
            TryEvaluateThumbManualOverrideRisk(leftHand, out risk);
            float spreadDelta = float.NaN;
            float projectionDelta = float.NaN;
            TryEvaluateCurrentThumbReferenceFrameDelta(leftHand, out spreadDelta, out projectionDelta);

            return
                $"side={sideLabel}, helper={GetHierarchyPath(helperTransform)}, source={GetHierarchyPath(sourceTransform)}, " +
                $"initDist={FormatDebugFloat(initialDistance)}, currDist={FormatDebugFloat(currentDistance)}, distDelta={FormatDebugFloat(distanceDelta)}, " +
                $"relRotDelta={FormatDebugFloat(relativeRotationDelta)}, risk={FormatDebugFloat(risk)}, " +
                $"suppress={ShouldSuppressCompetingManualThumbOverride(leftHand)}, keepDetached={ShouldKeepDetachedHelperManualThumbOverrides(leftHand)}, " +
                $"spreadDelta={FormatDebugFloat(spreadDelta)}, projectionDelta={FormatDebugFloat(projectionDelta)}";
        }

        private bool ShouldRejectManualThumbBasePositionOverride(bool leftHand, Transform targetThumb, Vector3 desiredWorldPosition)
        {
            if (targetThumb == null)
            {
                return false;
            }

            if (!TryEvaluateThumbManualOverrideRisk(leftHand, targetThumb.position, false, Vector3.zero, out float currentRisk) ||
                !TryEvaluateThumbManualOverrideRisk(leftHand, desiredWorldPosition, true, desiredWorldPosition, out float desiredRisk))
            {
                return false;
            }

            return desiredRisk >= ManualThumbOverrideSuppressRiskThreshold &&
                desiredRisk > currentRisk + ManualThumbOverrideRiskIncreaseTolerance;
        }

        private bool TryEvaluateThumbManualOverrideRisk(bool leftHand, out float risk)
        {
            Transform thumbProximal = targetAnimator != null
                ? targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal)
                : null;
            return TryEvaluateThumbManualOverrideRisk(
                leftHand,
                thumbProximal != null ? thumbProximal.position : Vector3.zero,
                false,
                Vector3.zero,
                out risk);
        }

        private bool TryEvaluateThumbManualOverrideRisk(
            bool leftHand,
            Vector3 thumbProximalWorldPosition,
            bool overrideHelperWorldPosition,
            Vector3 helperWorldPosition,
            out float risk)
        {
            risk = float.NaN;
            if (targetAnimator == null ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return false;
            }

            Transform hand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform index = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform thumbIntermediate = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbIntermediate : HumanBodyBones.RightThumbIntermediate);
            if (hand == null || index == null || thumbIntermediate == null)
            {
                return false;
            }

            Vector3 thumbDirection = thumbIntermediate.position - thumbProximalWorldPosition;
            Vector3 indexDirection = index.position - hand.position;
            if (!TryNormalize(thumbDirection, out thumbDirection) ||
                !TryNormalize(indexDirection, out indexDirection))
            {
                return false;
            }

            float spreadAngle = Vector3.Angle(thumbDirection, indexDirection);
            float spreadRisk = RiskAbove(
                spreadAngle,
                ManualThumbOverrideSpreadWarningAngle,
                ManualThumbOverrideSpreadFullRiskAngle);
            float projection = Vector3.Dot(thumbDirection, targetFrame.Normal);
            float projectionRisk = RiskOutsideRange(
                projection,
                ManualThumbOverrideProjectionMin,
                ManualThumbOverrideProjectionMax,
                1f);
            float helperSeparationRisk = float.NaN;
            float webbingRisk = float.NaN;
            if (TryEvaluateThumbHelperRelationshipRisk(
                leftHand,
                overrideHelperWorldPosition,
                helperWorldPosition,
                spreadRisk,
                projectionRisk,
                out float helperDistanceRisk,
                out float helperRotationRisk,
                out float helperWebbingRisk))
            {
                helperSeparationRisk = MaxFinite(helperDistanceRisk, helperRotationRisk);
                webbingRisk = helperWebbingRisk;
            }

            risk = MaxFinite(spreadRisk, projectionRisk, helperSeparationRisk, webbingRisk);
            return !float.IsNaN(risk) && !float.IsInfinity(risk);
        }

        private bool TryEvaluateThumbHelperRelationshipRisk(
            bool leftHand,
            bool overrideHelperWorldPosition,
            Vector3 helperWorldPosition,
            float spreadRisk,
            float projectionRisk,
            out float helperDistanceRisk,
            out float helperRotationRisk,
            out float webbingRisk)
        {
            helperDistanceRisk = float.NaN;
            helperRotationRisk = float.NaN;
            webbingRisk = float.NaN;

            Transform helperTransform = GetCachedThumbBaseHelper(leftHand);
            Transform sourceTransform = GetCachedExplicitThumbBaseSource(leftHand);
            if (helperTransform == null || sourceTransform == null)
            {
                return false;
            }

            EnsureThumbBaseHelperRelationshipBaseline(leftHand, helperTransform, sourceTransform);
            if (!_initialThumbBaseHelperSourceDistances.TryGetValue(leftHand, out float initialDistance) ||
                !_initialThumbBaseHelperSourceRelativeRotations.TryGetValue(leftHand, out Quaternion initialRelativeRotation))
            {
                return false;
            }

            Vector3 effectiveHelperWorldPosition = overrideHelperWorldPosition ? helperWorldPosition : helperTransform.position;
            float currentDistance = Vector3.Distance(effectiveHelperWorldPosition, sourceTransform.position);

            Quaternion relativeRotation = Quaternion.Inverse(sourceTransform.rotation) * helperTransform.rotation;
            float rotationDelta = float.NaN;
            if (IsFinite(relativeRotation))
            {
                rotationDelta = Quaternion.Angle(initialRelativeRotation, relativeRotation);
            }

            return TryCalculateThumbHelperRelationshipRisk(
                currentDistance,
                initialDistance,
                rotationDelta,
                spreadRisk,
                projectionRisk,
                out helperDistanceRisk,
                out helperRotationRisk,
                out webbingRisk);
        }

        private static bool TryCalculateThumbHelperRelationshipRisk(
            float currentDistance,
            float initialDistance,
            float rotationDelta,
            float spreadRisk,
            float projectionRisk,
            out float helperDistanceRisk,
            out float helperRotationRisk,
            out float webbingRisk)
        {
            helperDistanceRisk = float.NaN;
            helperRotationRisk = float.NaN;
            webbingRisk = float.NaN;

            if (IsFinite(currentDistance) && IsFinite(initialDistance))
            {
                helperDistanceRisk = RiskAbove(
                    Mathf.Abs(currentDistance - initialDistance),
                    ManualThumbHelperDistanceDeltaWarning,
                    ManualThumbHelperDistanceDeltaFullRisk);
            }

            if (IsFinite(rotationDelta))
            {
                helperRotationRisk = RiskAbove(
                    rotationDelta,
                    ManualThumbHelperRotationWarning,
                    ManualThumbHelperRotationFullRisk);
                webbingRisk = MaxFinite(
                    spreadRisk,
                    projectionRisk,
                    helperDistanceRisk,
                    RiskAbove(
                        rotationDelta,
                        ManualThumbWebbingRotationWarning,
                        ManualThumbWebbingRotationFullRisk));
            }

            return !float.IsNaN(MaxFinite(helperDistanceRisk, helperRotationRisk, webbingRisk));
        }

        private void EnsureThumbBaseHelperRelationshipBaseline(bool leftHand, Transform helperTransform, Transform sourceTransform)
        {
            if (!_initialThumbBaseHelperSourceDistances.ContainsKey(leftHand) ||
                !_initialThumbBaseHelperSourceRelativeRotations.ContainsKey(leftHand))
            {
                CaptureThumbBaseHelperRelationshipBaseline(leftHand, helperTransform, sourceTransform);
            }
        }

        private Transform GetCachedThumbBaseHelperEditor(bool leftHand)
        {
            if (_cachedThumbBaseHelpers.TryGetValue(leftHand, out Transform helperTransform) && helperTransform != null)
            {
                return helperTransform;
            }

            if (TryFindThumbBaseHelperCandidate(leftHand, out helperTransform))
            {
                _cachedThumbBaseHelpers[leftHand] = helperTransform;
                return helperTransform;
            }

            return null;
        }

        private Transform GetCachedExplicitThumbBaseSourceEditor(bool leftHand)
        {
            if (_cachedThumbBaseExplicitSources.TryGetValue(leftHand, out Transform sourceTransform) && sourceTransform != null)
            {
                return sourceTransform;
            }

            if (TryFindExplicitThumbBaseSource(leftHand, out sourceTransform))
            {
                _cachedThumbBaseExplicitSources[leftHand] = sourceTransform;
                return sourceTransform;
            }

            return null;
        }

        private static float RiskAbove(float value, float warningThreshold, float fullRiskThreshold)
        {
            if (!IsFinite(value))
            {
                return float.NaN;
            }

            if (value <= warningThreshold)
            {
                return 0f;
            }

            if (fullRiskThreshold <= warningThreshold)
            {
                return 1f;
            }

            return Mathf.Clamp01((value - warningThreshold) / (fullRiskThreshold - warningThreshold));
        }

        private static float RiskOutsideRange(float value, float minValue, float maxValue, float fullRiskDistance)
        {
            if (!IsFinite(value))
            {
                return float.NaN;
            }

            if (value < minValue)
            {
                return RiskAbove(minValue - value, 0f, Mathf.Max(0.0001f, fullRiskDistance));
            }

            if (value > maxValue)
            {
                return RiskAbove(value - maxValue, 0f, Mathf.Max(0.0001f, fullRiskDistance));
            }

            return 0f;
        }

        private static float MaxFinite(params float[] values)
        {
            float max = float.NaN;
            if (values == null)
            {
                return max;
            }

            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                max = float.IsNaN(max) ? value : Mathf.Max(max, value);
            }

            return max;
        }

        private int AlignEditorHumanoidHandPalmFrame(bool leftHand, float weight)
        {
            Transform targetHand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (targetHand == null)
            {
                return 0;
            }

            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referenceForwardLocal = _editorFingerReferenceAnimator.transform.InverseTransformDirection(referenceFrame.Forward).normalized;
            Vector3 referenceNormalLocal = _editorFingerReferenceAnimator.transform.InverseTransformDirection(referenceFrame.Normal).normalized;
            Vector3 desiredForward = targetAnimator.transform.TransformDirection(referenceForwardLocal).normalized;
            Vector3 desiredNormal = targetAnimator.transform.TransformDirection(referenceNormalLocal).normalized;
            if (!IsFinite(referenceForwardLocal) || !IsFinite(referenceNormalLocal) ||
                !IsFinite(desiredForward) || !IsFinite(desiredNormal))
            {
                return 0;
            }

            Quaternion currentFrameRotation = Quaternion.LookRotation(targetFrame.Forward, targetFrame.Normal);
            Quaternion desiredFrameRotation = Quaternion.LookRotation(desiredForward, desiredNormal);
            Quaternion correction = desiredFrameRotation * Quaternion.Inverse(currentFrameRotation);
            if (!IsFinite(currentFrameRotation) || !IsFinite(desiredFrameRotation) || !IsFinite(correction))
            {
                return 0;
            }

            if (weight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, weight);
            }

            Quaternion nextWorldRotation = correction * targetHand.rotation;
            if (!IsFinite(nextWorldRotation) || Quaternion.Angle(targetHand.rotation, nextWorldRotation) <= 0.001f)
            {
                return 0;
            }

            targetHand.rotation = nextWorldRotation;
            return 1;
        }

        private int AlignEditorHumanoidThumbSegmentDirection(bool leftHand, HumanBodyBones parentBone, HumanBodyBones childBone, float weight)
        {
            Transform targetHand = targetAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform referenceHand = _editorFingerReferenceAnimator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform targetParent = targetAnimator.GetBoneTransform(parentBone);
            Transform targetChild = targetAnimator.GetBoneTransform(childBone);
            Transform referenceParent = _editorFingerReferenceAnimator.GetBoneTransform(parentBone);
            Transform referenceChild = _editorFingerReferenceAnimator.GetBoneTransform(childBone);

            if (targetHand == null || referenceHand == null ||
                targetParent == null || targetChild == null ||
                referenceParent == null || referenceChild == null)
            {
                return 0;
            }

            Vector3 targetSegment = targetChild.position - targetParent.position;
            Vector3 referenceSegment = referenceChild.position - referenceParent.position;
            if (!IsFinite(targetSegment) || !IsFinite(referenceSegment) ||
                targetSegment.sqrMagnitude <= 0.00000001f ||
                referenceSegment.sqrMagnitude <= 0.00000001f)
            {
                return 0;
            }

            if (!TryBuildThumbPalmFrame(_editorFingerReferenceAnimator, leftHand, out ThumbPalmFrame referenceFrame) ||
                !TryBuildThumbPalmFrame(targetAnimator, leftHand, out ThumbPalmFrame targetFrame))
            {
                return 0;
            }

            Vector3 referenceHandDirection = referenceFrame.InverseTransformDirection(referenceSegment.normalized).normalized;
            Vector3 desiredWorldDirection = targetFrame.TransformDirection(referenceHandDirection).normalized;
            Vector3 currentWorldDirection = targetSegment.normalized;
            if (!IsFinite(referenceHandDirection) || !IsFinite(desiredWorldDirection) || !IsFinite(currentWorldDirection))
            {
                return 0;
            }

            Quaternion correction = Quaternion.FromToRotation(currentWorldDirection, desiredWorldDirection);
            if (!IsFinite(correction))
            {
                return 0;
            }

            if (weight < 0.999f)
            {
                correction = Quaternion.Slerp(Quaternion.identity, correction, weight);
            }

            Quaternion nextWorldRotation = correction * targetParent.rotation;
            if (!IsFinite(nextWorldRotation) || Quaternion.Angle(targetParent.rotation, nextWorldRotation) <= 0.001f)
            {
                return 0;
            }

            targetParent.rotation = nextWorldRotation;
            return 1;
        }

        private struct ThumbPalmFrame
        {
            public Vector3 Origin;
            public Vector3 Side;
            public Vector3 Normal;
            public Vector3 Forward;
            public float Scale;

            public Vector3 InverseTransformPoint(Vector3 worldPoint)
            {
                Vector3 delta = worldPoint - Origin;
                return new Vector3(
                    Vector3.Dot(delta, Side),
                    Vector3.Dot(delta, Normal),
                    Vector3.Dot(delta, Forward));
            }

            public Vector3 TransformPoint(Vector3 localPoint)
            {
                return Origin +
                    Side * localPoint.x +
                    Normal * localPoint.y +
                    Forward * localPoint.z;
            }

            public Vector3 InverseTransformDirection(Vector3 worldDirection)
            {
                return new Vector3(
                    Vector3.Dot(worldDirection, Side),
                    Vector3.Dot(worldDirection, Normal),
                    Vector3.Dot(worldDirection, Forward));
            }

            public Vector3 TransformDirection(Vector3 localDirection)
            {
                return Side * localDirection.x +
                    Normal * localDirection.y +
                    Forward * localDirection.z;
            }
        }

        private static bool TryBuildThumbPalmFrame(Animator animator, bool leftHand, out ThumbPalmFrame frame)
        {
            frame = default;
            if (animator == null)
            {
                return false;
            }

            Transform hand = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform thumb = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftThumbProximal : HumanBodyBones.RightThumbProximal);
            Transform index = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
            Transform middle = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            Transform little = animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);

            if (hand == null || thumb == null || index == null)
            {
                return false;
            }

            Vector3 origin = hand.position;
            Vector3 fingerCenter = Vector3.zero;
            int fingerCount = 0;
            AddPalmPoint(index, origin, ref fingerCenter, ref fingerCount);
            AddPalmPoint(middle, origin, ref fingerCenter, ref fingerCount);
            AddPalmPoint(little, origin, ref fingerCenter, ref fingerCount);
            if (fingerCount <= 0)
            {
                return false;
            }

            Vector3 forward = fingerCenter / fingerCount - origin;
            if (!TryNormalize(forward, out forward))
            {
                forward = index.position - origin;
                if (!TryNormalize(forward, out forward))
                {
                    return false;
                }
            }

            Vector3 side = Vector3.zero;
            if (little != null)
            {
                side = index.position - little.position;
            }

            side = Vector3.ProjectOnPlane(side, forward);
            if (!TryNormalize(side, out side))
            {
                side = Vector3.ProjectOnPlane(thumb.position - origin, forward);
                if (!TryNormalize(side, out side))
                {
                    side = Vector3.ProjectOnPlane(hand.right, forward);
                    if (!TryNormalize(side, out side))
                    {
                        return false;
                    }
                }
            }

            Vector3 thumbSide = Vector3.ProjectOnPlane(thumb.position - origin, forward);
            if (TryNormalize(thumbSide, out thumbSide) && Vector3.Dot(side, thumbSide) < 0f)
            {
                side = -side;
            }

            Vector3 normal = Vector3.Cross(side, forward);
            if (!TryNormalize(normal, out normal))
            {
                return false;
            }

            side = Vector3.Cross(forward, normal);
            if (!TryNormalize(side, out side))
            {
                return false;
            }

            frame = new ThumbPalmFrame
            {
                Origin = origin,
                Side = side,
                Normal = normal,
                Forward = forward,
                Scale = CalculatePalmScale(origin, index, middle, little)
            };

            return frame.Scale > 0.0001f &&
                IsFinite(frame.Origin) &&
                IsFinite(frame.Side) &&
                IsFinite(frame.Normal) &&
                IsFinite(frame.Forward);
        }

        private static void AddPalmPoint(Transform point, Vector3 origin, ref Vector3 sum, ref int count)
        {
            if (point == null)
            {
                return;
            }

            Vector3 delta = point.position - origin;
            if (!IsFinite(delta) || delta.sqrMagnitude <= 0.00000001f)
            {
                return;
            }

            sum += point.position;
            count++;
        }

        private static float CalculatePalmScale(Vector3 origin, params Transform[] points)
        {
            float sum = 0f;
            int count = 0;
            foreach (Transform point in points)
            {
                if (point == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(origin, point.position);
                if (!IsFinite(distance) || distance <= 0.0001f)
                {
                    continue;
                }

                sum += distance;
                count++;
            }

            return count > 0 ? sum / count : 0f;
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

        private void DisposeEditorHumanoidFingerPoseReference()
        {
            if (_editorFingerReferenceHandler != null)
            {
                _editorFingerReferenceHandler.Dispose();
                _editorFingerReferenceHandler = null;
            }

            if (_editorFingerReferenceInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_editorFingerReferenceInstance);
                }
                else
                {
                    DestroyImmediate(_editorFingerReferenceInstance);
                }
            }

            _editorFingerReferenceInstance = null;
            _editorFingerReferenceAnimator = null;
            _editorFingerReferenceMuscleIndices.Clear();
            _useEditorFingerPoseReference = false;
            _editorBodyRotationReferenceLogged = false;
            _editorHandLocalRotationReferenceLogged = false;
            _editorFootLocalRotationReferenceLogged = false;
            _editorLowerBodySegmentDirectionReferenceLogged = false;
            _editorFootHipsAlignedResidualYawReferenceLogged = false;
            _editorHipsLocalPositionReferenceLogged = false;
            _editorBodyPositionXzReferenceLogged = false;
            _hasEditorReferenceBodyPosition = false;
            _hasEditorReferenceHipsRestLocalPosition = false;
            _hasEditorReferenceLowestFootRestY = false;
            _allowEditorFootHeightGroundingReference = false;
        }


        private bool ShouldApplyManualFullBodyPoseReferenceMuscle(int muscleIndex)
        {
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return true;
            }

            string muscleName = HumanTrait.MuscleName[muscleIndex];
            if (manualAnimatorFullBodyPoseRightSleeveChainMusclesOnly)
            {
                return IsRightSleeveChainPoseMuscle(muscleName);
            }

            if (manualAnimatorFullBodyPoseRightArmMusclesOnly)
            {
                return IsRightArmPoseMuscle(muscleName);
            }

            if (manualAnimatorFullBodyPoseLeftArmMusclesOnly)
            {
                return IsLeftArmPoseMuscle(muscleName);
            }

            bool isLowerBody = IsLowerBodyMuscle(muscleName);
            if (ShouldApplyManualAnimatorFullBodyLegTwistMusclesOnly)
            {
                return IsLegTwistOrInOutMuscle(muscleName);
            }

            if (ShouldApplyManualAnimatorFullBodyLowerMusclesOnly)
            {
                return isLowerBody;
            }

            return !ShouldExcludeManualAnimatorFullBodyLowerMuscles || !isLowerBody;
        }

        private bool ShouldApplyManualFullBodyPoseReferenceFrameGate()
        {
            float start = Mathf.Max(0f, manualAnimatorFullBodyPoseFrameGateStart);
            float end = Mathf.Max(0f, manualAnimatorFullBodyPoseFrameGateEnd);
            if (start <= 0f && end <= 0f)
            {
                return true;
            }

            if (end <= 0f || end < start)
            {
                end = start;
            }

            float frameRate = Mathf.Clamp(legacyAnimationVisualFrameRate, 1f, 240f);
            int currentFrame = Mathf.RoundToInt(GetLegacyAnimationTime() * frameRate);
            return currentFrame >= Mathf.RoundToInt(start) && currentFrame <= Mathf.RoundToInt(end);
        }

        private float GetLegacyAnimationTime()
        {
            if (_legacyAnim == null)
            {
                return 0f;
            }

            AnimationState state = _legacyAnim[LegacyClipStateName];
            if (state == null)
            {
                return 0f;
            }

            return Mathf.Clamp(state.time, 0f, Mathf.Max(0f, state.length));
        }

        private void AlignRetargetPoseInputWithEditorHumanoidMuscleReference(ref HumanPose pose)
        {
            if (!_useEditorHumanoidMuscleReference || pose.muscles == null || _editorHumanoidMuscleCurves.Count == 0)
            {
                return;
            }

            float time = GetLegacyAnimationTime();
            foreach (KeyValuePair<int, AnimationCurve> pair in _editorHumanoidMuscleCurves)
            {
                if (pair.Key < 0 || pair.Key >= pose.muscles.Length || pair.Value == null)
                {
                    continue;
                }

                float referenceValue = pair.Value.Evaluate(time);
                pose.muscles[pair.Key] = AlignRetargetPoseInputWithEditorReference(
                    pair.Key,
                    pose.muscles[pair.Key],
                    referenceValue);
            }
        }

#endif
    }
}

