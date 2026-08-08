using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    public partial class PoseSpaceRetargeter
    {
        private sealed class PoseSpaceGuardPipeline
        {
            private readonly PoseSpaceRetargeter _retargeter;

            public PoseSpaceGuardPipeline(PoseSpaceRetargeter retargeter)
            {
                _retargeter = retargeter;
            }

            public void ClampPoseMuscles(ref HumanPose pose)
            {
                if (!_retargeter.clampMusclesToHumanRange || pose.muscles == null)
                {
                    return;
                }

                int clampedCount = HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);

                if (clampedCount > 0 && !_retargeter._muscleClampWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] Humanoid muscle 값 {clampedCount}개가 안전 범위를 벗어나 [-1, 1]로 제한되었습니다.");
                    _retargeter._muscleClampWarningLogged = true;
                }
            }

            public void ApplyAnatomicalArmGuard(ref HumanPose pose)
            {
                if (!_retargeter.enableAnatomicalArmGuard || pose.muscles == null)
                {
                    return;
                }

                int changed = HumanoidArmDeformationGuard.ClampAnatomicalArmMuscles(
                    ref pose,
                    _retargeter.armStretchMuscleLimit,
                    _retargeter.upperArmTwistMuscleLimit,
                    _retargeter.lowerArmTwistMuscleLimit,
                    _retargeter.clampArmStretchMuscles);

                if (changed > 0 && !_retargeter._anatomyGuardWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] 팔 변형 방지를 위해 Humanoid arm muscle {changed}개를 제한했습니다.");
                    _retargeter._anatomyGuardWarningLogged = true;
                }
            }

            public void SmoothPoseOnVisualSpike(ref HumanPose pose)
            {
                if (!_retargeter.smoothPoseOnLegacyAnimationStepSpike || pose.muscles == null || pose.muscles.Length == 0)
                {
                    RememberVisualPose(pose);
                    return;
                }

                if (!_retargeter._hasPreviousVisualPose ||
                    _retargeter._previousVisualPoseMuscles == null ||
                    _retargeter._previousVisualPoseMuscles.Length != pose.muscles.Length)
                {
                    RememberVisualPose(pose);
                    return;
                }

                float maxMuscleDelta = 0f;
                for (int i = 0; i < pose.muscles.Length; i++)
                {
                    float delta = Mathf.Abs(pose.muscles[i] - _retargeter._previousVisualPoseMuscles[i]);
                    if (delta > maxMuscleDelta)
                    {
                        maxMuscleDelta = delta;
                    }
                }

                _retargeter._lastPoseVisualMaxMuscleDelta = maxMuscleDelta;
                _retargeter._maxPoseVisualMaxMuscleDelta = Mathf.Max(_retargeter._maxPoseVisualMaxMuscleDelta, maxMuscleDelta);

                float bodyPositionDelta = Vector3.Distance(_retargeter._previousVisualPoseBodyPosition, pose.bodyPosition);
                float bodyRotationDelta = Quaternion.Angle(_retargeter._previousVisualPoseBodyRotation, pose.bodyRotation);
                bool shouldSmooth = ShouldSmoothVisualPoseSpike(
                    maxMuscleDelta,
                    bodyPositionDelta,
                    bodyRotationDelta,
                    _retargeter.poseVisualMuscleDeltaThreshold,
                    _retargeter._legacyAnimationStepSpikeThisFrame,
                    out bool muscleDeltaOnlySpike);

                if (shouldSmooth)
                {
                    float currentWeight = CalculateVisualPoseSpikeCurrentWeight(
                        _retargeter.poseVisualSpikeCurrentWeight,
                        bodyPositionDelta,
                        bodyRotationDelta,
                        _retargeter._legacyAnimationStepSpikeThisFrame);
                    bool useEditorHumanoidMuscleReference = false;
#if UNITY_EDITOR
                    useEditorHumanoidMuscleReference = _retargeter._useEditorHumanoidMuscleReference;
#endif
                    for (int i = 0; i < pose.muscles.Length; i++)
                    {
                        bool hasEditorHumanoidMuscleReferenceCurve = false;
#if UNITY_EDITOR
                        hasEditorHumanoidMuscleReferenceCurve = _retargeter._editorHumanoidMuscleCurves.ContainsKey(i);
#endif
                        pose.muscles[i] = BlendVisualPoseSpikeMuscle(
                            _retargeter._previousVisualPoseMuscles[i],
                            pose.muscles[i],
                            currentWeight,
                            i,
                            useEditorHumanoidMuscleReference,
                            hasEditorHumanoidMuscleReferenceCurve,
                            _retargeter.poseVisualSpikeForearmStretchClampMaxOffset);
                    }

                    pose.bodyPosition = Vector3.Lerp(_retargeter._previousVisualPoseBodyPosition, pose.bodyPosition, currentWeight);
                    pose.bodyRotation = Quaternion.Slerp(_retargeter._previousVisualPoseBodyRotation, pose.bodyRotation, currentWeight);
                    _retargeter._poseVisualSmoothingCount++;
                }
                else if (muscleDeltaOnlySpike)
                {
                    // 빠른 손/팔 동작 자체를 smoothing하면 의도한 동작이 멈칫하고 몸통이 늦게 따라오는 것처럼 보인다.
                    _retargeter._poseVisualMuscleDeltaOnlySkippedCount++;
                }

                RememberVisualPose(pose);
            }

            private static float CalculateVisualPoseSpikeCurrentWeight(
                float configuredWeight,
                float bodyPositionDelta,
                float bodyRotationDelta,
                bool legacyAnimationStepSpikeThisFrame)
            {
                float currentWeight = Mathf.Clamp(configuredWeight, 0.1f, 1f);
                if (IsBodyPoseSpike(bodyPositionDelta, bodyRotationDelta))
                {
                    return Mathf.Min(currentWeight, 0.1f);
                }

                return currentWeight;
            }

            private static float BlendVisualPoseSpikeMuscle(
                float previousValue,
                float currentValue,
                float currentWeight,
                int muscleIndex,
                bool useEditorHumanoidMuscleReference,
                bool hasEditorHumanoidMuscleReferenceCurve,
                float forearmStretchClampMaxOffset)
            {
                if (ShouldPreserveEditorHumanoidMuscleDuringVisualSmoothing(
                    muscleIndex,
                    useEditorHumanoidMuscleReference,
                    hasEditorHumanoidMuscleReferenceCurve))
                {
                    return currentValue;
                }

                float blended = Mathf.Lerp(previousValue, currentValue, currentWeight);
                return ClampForearmStretchVisualSpikeBlend(
                    previousValue,
                    currentValue,
                    blended,
                    muscleIndex,
                    forearmStretchClampMaxOffset);
            }

            private static float ClampForearmStretchVisualSpikeBlend(
                float previousValue,
                float currentValue,
                float blendedValue,
                int muscleIndex,
                float maxOffset)
            {
                if (maxOffset <= 0f ||
                    !IsForearmStretchMuscleIndex(muscleIndex) ||
                    !IsFinite(previousValue) ||
                    !IsFinite(currentValue) ||
                    !IsFinite(blendedValue))
                {
                    return blendedValue;
                }

                if (currentValue > ForearmStretchVisualClampCurrentMax)
                {
                    return blendedValue;
                }

                float safeOffset = Mathf.Clamp01(maxOffset);
                return Mathf.Clamp(
                    blendedValue,
                    currentValue - safeOffset,
                    currentValue + safeOffset);
            }

            private static bool ShouldPreserveEditorHumanoidMuscleDuringVisualSmoothing(
                int muscleIndex,
                bool useEditorHumanoidMuscleReference,
                bool hasEditorHumanoidMuscleReferenceCurve)
            {
#if UNITY_EDITOR
                return useEditorHumanoidMuscleReference &&
                    hasEditorHumanoidMuscleReferenceCurve &&
                    ShouldUseEditorHumanoidMuscleReference(muscleIndex);
#else
                return false;
#endif
            }

            private static bool IsForearmStretchMuscleIndex(int muscleIndex)
            {
                if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
                {
                    return false;
                }

                string normalized = NormalizeEditorMuscleName(HumanTrait.MuscleName[muscleIndex]);
                return normalized.Contains("forearm") && normalized.Contains("stretch");
            }

            private static bool ShouldSmoothVisualPoseSpike(
                float maxMuscleDelta,
                float bodyPositionDelta,
                float bodyRotationDelta,
                float poseVisualMuscleDeltaThreshold,
                bool legacyAnimationStepSpikeThisFrame,
                out bool muscleDeltaOnlySpike)
            {
                bool bodyPoseSpike = IsBodyPoseSpike(bodyPositionDelta, bodyRotationDelta);
                muscleDeltaOnlySpike = maxMuscleDelta > poseVisualMuscleDeltaThreshold &&
                    !legacyAnimationStepSpikeThisFrame &&
                    !bodyPoseSpike;

                return legacyAnimationStepSpikeThisFrame || bodyPoseSpike;
            }

            private static bool IsBodyPoseSpike(float bodyPositionDelta, float bodyRotationDelta)
            {
                return bodyPositionDelta > BodyPositionVisualSpikeThreshold ||
                    bodyRotationDelta > BodyRotationVisualSpikeThresholdDegrees;
            }

            private void RememberVisualPose(HumanPose pose)
            {
                if (pose.muscles == null || pose.muscles.Length == 0 || !IsFinite(pose))
                {
                    return;
                }

                if (_retargeter._previousVisualPoseMuscles == null || _retargeter._previousVisualPoseMuscles.Length != pose.muscles.Length)
                {
                    _retargeter._previousVisualPoseMuscles = new float[pose.muscles.Length];
                }

                System.Array.Copy(pose.muscles, _retargeter._previousVisualPoseMuscles, pose.muscles.Length);
                _retargeter._previousVisualPoseBodyPosition = pose.bodyPosition;
                _retargeter._previousVisualPoseBodyRotation = pose.bodyRotation;
                _retargeter._hasPreviousVisualPose = true;
            }

            public int ApplyThumbAnatomicalGuard(ref HumanPose pose, bool applyStretchOffset)
            {
                if (!_retargeter.enableThumbAnatomicalGuard || pose.muscles == null)
                {
                    return 0;
                }

                if (ShouldPreserveManualFingerReferenceThumbMuscles())
                {
                    return 0;
                }

                float safeStretchMin = Mathf.Min(_retargeter.thumbStretchMin, _retargeter.thumbStretchMax);
                float safeStretchMax = Mathf.Max(_retargeter.thumbStretchMin, _retargeter.thumbStretchMax);
                float safeSpreadMin = Mathf.Min(_retargeter.thumbSpreadMin, _retargeter.thumbSpreadMax);
                float safeSpreadMax = Mathf.Max(_retargeter.thumbSpreadMin, _retargeter.thumbSpreadMax);
                int count = Mathf.Min(pose.muscles.Length, HumanTrait.MuscleCount);
                int changed = 0;

                for (int i = 0; i < count; i++)
                {
                    string muscleName = HumanTrait.MuscleName[i];
                    if (string.IsNullOrEmpty(muscleName))
                    {
                        continue;
                    }

                    string normalizedName = muscleName.Replace(" ", "").ToLowerInvariant();
                    if (!normalizedName.Contains("thumb"))
                    {
                        continue;
                    }

                    float before = pose.muscles[i];
                    float after = before;
                    if (normalizedName.Contains("spread"))
                    {
                        after = Mathf.Clamp(before, safeSpreadMin, safeSpreadMax);
                    }
                    else if (normalizedName.Contains("stretch"))
                    {
                        float offset = applyStretchOffset ? _retargeter.thumbStretchOffset : 0f;
                        after = Mathf.Clamp(before + offset, safeStretchMin, safeStretchMax);
                    }

                    if (Mathf.Approximately(before, after))
                    {
                        continue;
                    }

                    pose.muscles[i] = after;
                    changed++;
                }

                if (changed > 0 && _retargeter.logThumbAnatomicalGuardCorrections && !_retargeter._thumbGuardWarningLogged)
                {
                    Debug.LogWarning($"[PoseSpaceRetargeter] 엄지 해부학적 제한으로 thumb muscle {changed}개를 보정했습니다.");
                    _retargeter._thumbGuardWarningLogged = true;
                }

                return changed;
            }

            public bool ShouldApplyThumbStretchOffset()
            {
                return !ShouldPreserveManualFingerReferenceThumbMuscles();
            }

            private bool ShouldPreserveManualFingerReferenceThumbMuscles()
            {
#if UNITY_EDITOR
                return _retargeter._useEditorFingerPoseReference && _retargeter.preserveManualFingerReferenceThumbMuscles;
#else
                return false;
#endif
            }
        }
    }
}
