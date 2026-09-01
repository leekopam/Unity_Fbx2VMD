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
    }
}
