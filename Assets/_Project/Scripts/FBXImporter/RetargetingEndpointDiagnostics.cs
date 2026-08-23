using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 끝점 보정 후보와 중간 진단값을 입력 값만으로 계산함.
    /// </summary>
    internal static class RetargetingEndpointDiagnostics
    {
        private const float FootHipsAlignedResidualYawGateMeters = 0.12f;
        private const float FootHipsAlignedResidualYawSideGapMeters = 0.005f;
        private const float FootHipsAlignedResidualYawProtectedMaxAngle = 20f;

        internal static bool TryCalculateEditorFootHipsAlignedResidualYawReference(
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

        internal static bool TryCalculateEditorFootHipsAlignedResidualYawReference(
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

        internal static float ResolveEditorFootHipsAlignedResidualYawSideAwareMaxAngle(
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

        internal static float CalculateEndpointPositionMaxYawAngle(
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

        internal static bool TryCalculateReferencePosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            out Vector3 nextFootPosition)
        {
            return TryCalculateReferencePosition(
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

        internal static bool TryCalculateReferencePosition(
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
            return TryCalculateReferencePosition(
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

        internal static bool TryCalculateReferencePosition(
            Vector3 desiredFootPosition,
            Vector3 desiredToesPosition,
            Vector3 currentFootPosition,
            Vector3 currentToesPosition,
            float weight,
            float maxOffset,
            float positiveZScale,
            float toesBlendWeight,
            out Vector3 nextFootPosition,
            out RetargetingEndpointDiagnosticSnapshot diagnostics)
        {
            nextFootPosition = currentFootPosition;
            diagnostics = RetargetingEndpointDiagnosticSnapshot.Empty;
            diagnostics.DesiredFootPosition = desiredFootPosition;
            diagnostics.DesiredToesPosition = desiredToesPosition;
            diagnostics.CurrentFootPosition = currentFootPosition;
            diagnostics.CurrentToesPosition = currentToesPosition;

            if (!IsFinite(desiredFootPosition) || !IsFinite(currentFootPosition))
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
                endpointDelta = Vector3.Lerp(
                    footDelta,
                    averagedEndpointDelta,
                    Mathf.Clamp01(toesBlendWeight));
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

        internal static bool TryCalculateEvaluatorXzReferencePosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition)
        {
            return TryCalculateEvaluatorXzReferencePosition(
                referenceFootPosition,
                currentFootPosition,
                firstMatchedFootOffset,
                targetMagnitude,
                weight,
                maxOffset,
                out nextFootPosition,
                out _);
        }

        internal static bool TryCalculateEvaluatorXzReferencePosition(
            Vector3 referenceFootPosition,
            Vector3 currentFootPosition,
            Vector3 firstMatchedFootOffset,
            float targetMagnitude,
            float weight,
            float maxOffset,
            out Vector3 nextFootPosition,
            out RetargetingEndpointDiagnosticSnapshot diagnostics)
        {
            nextFootPosition = currentFootPosition;
            diagnostics = RetargetingEndpointDiagnosticSnapshot.Empty;
            diagnostics.CurrentFootPosition = currentFootPosition;
            diagnostics.CurrentToesPosition = NaNVector3;
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
            diagnostics.DesiredToesPosition = NaNVector3;
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

        private static Vector3 NaNVector3 => new Vector3(float.NaN, float.NaN, float.NaN);

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
    }

    /// <summary>
    /// 끝점 보정 단계별 계산값을 호출자에게 전달함.
    /// </summary>
    internal struct RetargetingEndpointDiagnosticSnapshot
    {
        internal Vector3 DesiredFootPosition;
        internal Vector3 DesiredToesPosition;
        internal Vector3 CurrentFootPosition;
        internal Vector3 CurrentToesPosition;
        internal Vector3 EndpointDeltaBeforeClamp;
        internal Vector3 EndpointDeltaAfterClamp;
        internal Vector3 EndpointDeltaAfterPositiveZScale;
        internal Vector3 Correction;
        internal Vector3 NextFootPosition;
        internal float EvaluatorXzReferenceEnabled;
        internal Vector3 EvaluatorXzFirstOffset;
        internal Vector3 EvaluatorXzNormalizedDelta;
        internal Vector3 EvaluatorXzDesiredNormalizedDelta;
        internal float EvaluatorXzTargetMagnitude;

        internal static RetargetingEndpointDiagnosticSnapshot Empty =>
            new RetargetingEndpointDiagnosticSnapshot
            {
                DesiredFootPosition = NaNVector3,
                DesiredToesPosition = NaNVector3,
                CurrentFootPosition = NaNVector3,
                CurrentToesPosition = NaNVector3,
                EndpointDeltaBeforeClamp = NaNVector3,
                EndpointDeltaAfterClamp = NaNVector3,
                EndpointDeltaAfterPositiveZScale = NaNVector3,
                Correction = NaNVector3,
                NextFootPosition = NaNVector3,
                EvaluatorXzReferenceEnabled = float.NaN,
                EvaluatorXzFirstOffset = NaNVector3,
                EvaluatorXzNormalizedDelta = NaNVector3,
                EvaluatorXzDesiredNormalizedDelta = NaNVector3,
                EvaluatorXzTargetMagnitude = float.NaN
            };

        private static Vector3 NaNVector3 => new Vector3(float.NaN, float.NaN, float.NaN);
    }
}
