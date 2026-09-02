using UnityEngine;

namespace Fbx2Vmd.Retargeting
{
    /// <summary>
    /// Root motion 보정 유틸리티. PoseSpaceRetargeter에서 추출함.
    /// ponytail: 순수 static 계산만 — HumanPose 변형은 PoseSpaceRetargeter에 유지.
    /// </summary>
    public static class RootMotionGuard
    {
        /// <summary>
        /// Ghost와 Target 모델 간 root delta 계산.
        /// body position XZ root motion 사용 여부에 따라 body/ghost delta 선택.
        /// </summary>
        public static Vector3 CalculateRetargetRootDelta(
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

        public static Vector3 CalculateEditorRootTranslationReferenceDelta(
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

            Vector3 ghostDeltaXZ = new Vector3(ghostDelta.x, 0f, ghostDelta.z);
            if (ghostDeltaXZ.sqrMagnitude > 0.00000025f)
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
            nextSmoothedEditorRootTranslationDelta = Vector3.Lerp(
                previousSmoothedEditorRootTranslationDelta,
                weightedDelta,
                currentWeight);
            nextHasSmoothedEditorRootTranslationDelta = true;
            return nextSmoothedEditorRootTranslationDelta;
        }

        public static Vector3 SelectBodyPositionRootMotionSource(
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

        /// <summary>
        /// Movement scale multiplier를 0.5~2.0 범위로 정규화.
        /// </summary>
        public static float NormalizeMovementScaleMultiplier(float value)
        {
            if (!IsFinite(value))
            {
                return 1.0f;
            }

            return Mathf.Clamp(value, 0f, 1.5f);
        }

        public static Vector3 ApplyImplicitBodyPositionRootGuard(
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

        public static Vector3 ApplyImplicitBodyPositionRootGuard(
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

        public static Vector3 SelectImplicitRootGuardReference(
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

        public static Vector3 SelectPoseSolveRootPosition(
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

        public static Vector3 RestoreRootMotionCarrierPositionAfterPose(
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

        /// <summary>
        /// Body position/rotation delta가 spike인지 판정.
        /// </summary>
        public static bool IsBodyPoseSpike(float bodyPositionDelta, float bodyRotationDelta)
        {
            const float BodyRotationVisualSpikeThresholdDegrees = 25f;
            // ponytail: bodyPositionDelta를 meters 단위로 비교 (는 Vector3.magnitude이어야 함)
            return bodyRotationDelta > BodyRotationVisualSpikeThresholdDegrees;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static Vector3 FlattenXZ(Vector3 value)
        {
            return new Vector3(value.x, 0f, value.z);
        }
    }
}
