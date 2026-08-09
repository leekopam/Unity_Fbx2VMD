using System.Collections.Generic;
using UnityEngine;
using System;

public partial class UnityHumanoidVMDRecorder
{
    internal static int ApplyMmdCenterFloorLiftFromIkPositions(
        List<Vector3> centerPositions,
        IReadOnlyList<Vector3> leftFootIkPositions,
        IReadOnlyList<Vector3> rightFootIkPositions,
        IReadOnlyList<Vector3> leftToeIkPositions,
        IReadOnlyList<Vector3> rightToeIkPositions,
        int safeFrameCount,
        float minY,
        float maxCenterDeltaPerFrame,
        out float minEffectiveYBefore,
        out float minEffectiveYAfter,
        out float maxCenterLift)
    {
        minEffectiveYBefore = 0f;
        minEffectiveYAfter = 0f;
        maxCenterLift = 0f;

        if (centerPositions == null || safeFrameCount <= 0)
        {
            return 0;
        }

        int count = Math.Min(safeFrameCount, centerPositions.Count);
        if (count <= 0)
        {
            return 0;
        }

        bool hasAnyFootSample = false;
        float minBefore = float.PositiveInfinity;
        var targetCenterY = new float[count];
        var validCenter = new bool[count];
        for (int i = 0; i < count; i++)
        {
            targetCenterY[i] = float.NaN;
        }

        const float serializedFootFloorClearance = 0.001f;
        float targetFloorY = minY + serializedFootFloorClearance;

        for (int i = 0; i < count; i++)
        {
            Vector3 center = centerPositions[i];
            if (!IsFinite(center))
            {
                continue;
            }

            targetCenterY[i] = center.y;
            validCenter[i] = true;

            if (!TryCalculateLowestEffectiveMmdFootY(
                    center.y,
                    leftFootIkPositions,
                    rightFootIkPositions,
                    leftToeIkPositions,
                    rightToeIkPositions,
                    i,
                    out float lowestEffectiveY))
            {
                continue;
            }

            hasAnyFootSample = true;
            minBefore = Mathf.Min(minBefore, lowestEffectiveY);

            float lift = Mathf.Max(0f, targetFloorY - lowestEffectiveY);
            targetCenterY[i] = center.y + lift;
        }

        SmoothCenterYTargetsForDeltaLimit(targetCenterY, validCenter, centerPositions, maxCenterDeltaPerFrame);

        float minAfter = float.PositiveInfinity;
        int adjustedFrameCount = 0;
        const float epsilon = 0.000001f;
        for (int i = 0; i < count; i++)
        {
            if (!validCenter[i])
            {
                continue;
            }

            Vector3 center = centerPositions[i];
            float lift = Mathf.Max(0f, targetCenterY[i] - center.y);
            if (lift > epsilon)
            {
                center.y = targetCenterY[i];
                centerPositions[i] = center;
                adjustedFrameCount++;
                maxCenterLift = Mathf.Max(maxCenterLift, lift);
            }

            if (TryCalculateLowestEffectiveMmdFootY(
                    center.y,
                    leftFootIkPositions,
                    rightFootIkPositions,
                    leftToeIkPositions,
                    rightToeIkPositions,
                    i,
                    out float lowestEffectiveYAfter))
            {
                minAfter = Mathf.Min(minAfter, lowestEffectiveYAfter);
            }
        }

        if (hasAnyFootSample)
        {
            minEffectiveYBefore = minBefore;
            minEffectiveYAfter = minAfter;
        }

        return adjustedFrameCount;
    }

    private static void SmoothCenterYTargetsForDeltaLimit(
        float[] targetCenterY,
        bool[] validCenter,
        IReadOnlyList<Vector3> centerPositions,
        float maxCenterDeltaPerFrame)
    {
        if (targetCenterY == null || validCenter == null || targetCenterY.Length != validCenter.Length)
        {
            return;
        }

        if (!IsFinite(maxCenterDeltaPerFrame))
        {
            return;
        }

        const float serializedFloatSafetyMargin = 0.001f;
        float limit = Mathf.Max(0f, maxCenterDeltaPerFrame - serializedFloatSafetyMargin);
        for (int i = 1; i < targetCenterY.Length; i++)
        {
            if (!validCenter[i] || !validCenter[i - 1])
            {
                continue;
            }

            float edgeYLimit = CalculateCenterEdgeYLimit(centerPositions, i, limit);
            float minimumAllowedY = targetCenterY[i - 1] - edgeYLimit;
            if (targetCenterY[i] < minimumAllowedY)
            {
                targetCenterY[i] = minimumAllowedY;
            }
        }

        for (int i = targetCenterY.Length - 2; i >= 0; i--)
        {
            if (!validCenter[i] || !validCenter[i + 1])
            {
                continue;
            }

            float edgeYLimit = CalculateCenterEdgeYLimit(centerPositions, i + 1, limit);
            float minimumAllowedY = targetCenterY[i + 1] - edgeYLimit;
            if (targetCenterY[i] < minimumAllowedY)
            {
                targetCenterY[i] = minimumAllowedY;
            }
        }
    }

    private static float CalculateCenterEdgeYLimit(
        IReadOnlyList<Vector3> centerPositions,
        int currentIndex,
        float maxCenterDeltaPerFrame)
    {
        if (centerPositions == null ||
            currentIndex <= 0 ||
            currentIndex >= centerPositions.Count ||
            !IsFinite(maxCenterDeltaPerFrame))
        {
            return Mathf.Max(0f, maxCenterDeltaPerFrame);
        }

        Vector3 previous = centerPositions[currentIndex - 1];
        Vector3 current = centerPositions[currentIndex];
        if (!IsFinite(previous) || !IsFinite(current))
        {
            return Mathf.Max(0f, maxCenterDeltaPerFrame);
        }

        float maxDelta = Mathf.Max(0f, maxCenterDeltaPerFrame);
        float xDelta = current.x - previous.x;
        float zDelta = current.z - previous.z;
        float xzDeltaSquared = xDelta * xDelta + zDelta * zDelta;
        float maxDeltaSquared = maxDelta * maxDelta;
        if (xzDeltaSquared >= maxDeltaSquared)
        {
            return 0f;
        }

        return Mathf.Sqrt(maxDeltaSquared - xzDeltaSquared);
    }

    private static bool TryCalculateLowestEffectiveMmdFootY(
        float centerY,
        IReadOnlyList<Vector3> leftFootIkPositions,
        IReadOnlyList<Vector3> rightFootIkPositions,
        IReadOnlyList<Vector3> leftToeIkPositions,
        IReadOnlyList<Vector3> rightToeIkPositions,
        int frameIndex,
        out float lowestEffectiveY)
    {
        lowestEffectiveY = float.PositiveInfinity;
        bool found = false;

        AddFootEffectiveY(centerY, leftFootIkPositions, frameIndex, ref lowestEffectiveY, ref found);
        AddFootEffectiveY(centerY, rightFootIkPositions, frameIndex, ref lowestEffectiveY, ref found);
        AddToeEffectiveY(centerY, leftFootIkPositions, leftToeIkPositions, frameIndex, ref lowestEffectiveY, ref found);
        AddToeEffectiveY(centerY, rightFootIkPositions, rightToeIkPositions, frameIndex, ref lowestEffectiveY, ref found);

        return found;
    }

    private static void AddFootEffectiveY(
        float centerY,
        IReadOnlyList<Vector3> footIkPositions,
        int frameIndex,
        ref float lowestEffectiveY,
        ref bool found)
    {
        if (!TryGetFiniteY(footIkPositions, frameIndex, out float footY))
        {
            return;
        }

        lowestEffectiveY = Mathf.Min(lowestEffectiveY, centerY + footY);
        found = true;
    }

    private static void AddToeEffectiveY(
        float centerY,
        IReadOnlyList<Vector3> footIkPositions,
        IReadOnlyList<Vector3> toeIkPositions,
        int frameIndex,
        ref float lowestEffectiveY,
        ref bool found)
    {
        if (!TryGetFiniteY(footIkPositions, frameIndex, out float footY) ||
            !TryGetFiniteY(toeIkPositions, frameIndex, out float toeY))
        {
            return;
        }

        lowestEffectiveY = Mathf.Min(lowestEffectiveY, centerY + footY + toeY);
        found = true;
    }

    private static bool TryGetFiniteY(IReadOnlyList<Vector3> positions, int frameIndex, out float y)
    {
        y = 0f;
        if (positions == null || frameIndex < 0 || frameIndex >= positions.Count)
        {
            return false;
        }

        Vector3 position = positions[frameIndex];
        if (!IsFinite(position))
        {
            return false;
        }

        y = position.y;
        return true;
    }

    internal static int ClampMmdIkExportDeltaSpikePositions(
        List<Vector3> positions,
        int safeFrameCount,
        float maxDeltaPerFrame,
        out float maxBefore,
        out float maxAfter)
    {
        return ClampMmdExportDeltaSpikePositions(positions, safeFrameCount, maxDeltaPerFrame, out maxBefore, out maxAfter);
    }

    internal static int ClampMmdIkExportDeltaSpikePositions(
        List<Vector3> positions,
        int safeFrameCount,
        float maxDeltaPerFrame,
        float recoveryMaxDeltaPerFrame,
        float recoveryTriggerDeltaPerFrame,
        out float maxBefore,
        out float maxAfter)
    {
        return ClampMmdExportDeltaSpikePositions(
            positions,
            safeFrameCount,
            maxDeltaPerFrame,
            out maxBefore,
            out maxAfter,
            recoveryMaxDeltaPerFrame,
            recoveryTriggerDeltaPerFrame);
    }

    internal static int ClampMmdIkExportDeltaSpikePositions(
        List<Vector3> positions,
        int safeFrameCount,
        float maxDeltaPerFrame,
        float recoveryMaxDeltaPerFrame,
        float recoveryTriggerDeltaPerFrame,
        float recoveryDebtThresholdDeltaPerFrame,
        out float maxBefore,
        out float maxAfter)
    {
        return ClampMmdExportDeltaSpikePositions(
            positions,
            safeFrameCount,
            maxDeltaPerFrame,
            out maxBefore,
            out maxAfter,
            recoveryMaxDeltaPerFrame,
            recoveryTriggerDeltaPerFrame,
            recoveryDebtThresholdDeltaPerFrame);
    }

    internal static int ClampMmdIkExportDeltaSpikePositions(
        List<Vector3> positions,
        int safeFrameCount,
        float maxDeltaPerFrame,
        float recoveryMaxDeltaPerFrame,
        float recoveryTriggerDeltaPerFrame,
        float recoveryDebtThresholdDeltaPerFrame,
        int recoveryHoldFrames,
        out float maxBefore,
        out float maxAfter)
    {
        return ClampMmdExportDeltaSpikePositions(
            positions,
            safeFrameCount,
            maxDeltaPerFrame,
            out maxBefore,
            out maxAfter,
            recoveryMaxDeltaPerFrame,
            recoveryTriggerDeltaPerFrame,
            recoveryDebtThresholdDeltaPerFrame,
            recoveryHoldFrames);
    }

    internal static int ClampMmdCenterExportDeltaSpikePositions(
        List<Vector3> positions,
        int safeFrameCount,
        float maxDeltaPerFrame,
        out float maxBefore,
        out float maxAfter)
    {
        return ClampMmdExportDeltaSpikePositions(positions, safeFrameCount, maxDeltaPerFrame, out maxBefore, out maxAfter);
    }

    internal static IReadOnlyList<VmdIkFrame> BuildMmdIkToggleFramesFromExportSteps(
        IReadOnlyDictionary<BoneNames, List<Vector3>> positionsByBone,
        int safeFrameCount,
        float footStepThresholdVmd,
        float toeStepThresholdVmd)
    {
        var frames = new List<VmdIkFrame> { VmdIkFrame.Enabled(0) };
        if (positionsByBone == null || safeFrameCount <= 1)
        {
            return frames;
        }

        float footThreshold = Mathf.Max(0f, footStepThresholdVmd);
        float toeThreshold = Mathf.Max(0f, toeStepThresholdVmd);
        bool previousLeftEnabled = true;
        bool previousRightEnabled = true;

        for (int i = 1; i < safeFrameCount; i++)
        {
            bool leftStepIsLarge =
                ExportStepExceedsThreshold(positionsByBone, (BoneNames)2, i, footThreshold) ||
                ExportStepExceedsThreshold(positionsByBone, (BoneNames)4, i, toeThreshold);
            bool rightStepIsLarge =
                ExportStepExceedsThreshold(positionsByBone, (BoneNames)3, i, footThreshold) ||
                ExportStepExceedsThreshold(positionsByBone, (BoneNames)5, i, toeThreshold);

            bool leftEnabled = !leftStepIsLarge;
            bool rightEnabled = !rightStepIsLarge;
            if (leftEnabled == previousLeftEnabled && rightEnabled == previousRightEnabled)
            {
                continue;
            }

            frames.Add(new VmdIkFrame(
                (uint)i,
                leftFootEnabled: leftEnabled,
                leftToeEnabled: leftEnabled,
                rightFootEnabled: rightEnabled,
                rightToeEnabled: rightEnabled));
            previousLeftEnabled = leftEnabled;
            previousRightEnabled = rightEnabled;
        }

        return frames;
    }

    private static int ClampMmdExportDeltaSpikePositions(
        List<Vector3> positions,
        int safeFrameCount,
        float maxDeltaPerFrame,
        out float maxBefore,
        out float maxAfter,
        float recoveryMaxDeltaPerFrame = 0f,
        float recoveryTriggerDeltaPerFrame = 0f,
        float recoveryDebtThresholdDeltaPerFrame = 0f,
        int recoveryHoldFrames = 0)
    {
        maxBefore = 0f;
        maxAfter = 0f;

        if (positions == null)
        {
            return 0;
        }

        int count = Math.Min(safeFrameCount, positions.Count);
        if (count <= 1)
        {
            return 0;
        }

        for (int i = 1; i < count; i++)
        {
            Vector3 delta = positions[i] - positions[i - 1];
            if (IsFinite(delta))
            {
                maxBefore = Mathf.Max(maxBefore, delta.magnitude);
            }
        }

        const float serializedFloatSafetyMargin = 0.001f;
        float limit = Mathf.Max(0f, maxDeltaPerFrame - serializedFloatSafetyMargin);
        bool useRecoveryDebtThreshold = recoveryDebtThresholdDeltaPerFrame > 0f;
        bool useRecoveryLimit = recoveryMaxDeltaPerFrame > maxDeltaPerFrame &&
                                (recoveryTriggerDeltaPerFrame > maxDeltaPerFrame || useRecoveryDebtThreshold);
        float recoveryLimit = Mathf.Max(0f, recoveryMaxDeltaPerFrame - serializedFloatSafetyMargin);
        List<Vector3> sourcePositions = useRecoveryLimit ? new List<Vector3>(positions) : null;
        int recoveryHoldFrameCount = Math.Max(0, recoveryHoldFrames);
        int remainingRecoveryHoldFrames = 0;
        int clampedCount = 0;
        for (int i = 1; i < count; i++)
        {
            Vector3 previous = positions[i - 1];
            Vector3 current = positions[i];
            Vector3 delta = current - previous;
            if (!IsFinite(delta))
            {
                continue;
            }

            float before = delta.magnitude;
            float effectiveLimit = limit;
            if (useRecoveryLimit)
            {
                Vector3 rawDelta = sourcePositions[i] - sourcePositions[i - 1];
                Vector3 lagDebt = sourcePositions[i] - previous;
                bool recoveryTriggeredByRawStep =
                    recoveryTriggerDeltaPerFrame > maxDeltaPerFrame &&
                    IsFinite(rawDelta) &&
                    rawDelta.magnitude > recoveryTriggerDeltaPerFrame;
                bool recoveryTriggeredByLagDebt =
                    useRecoveryDebtThreshold &&
                    IsFinite(lagDebt) &&
                    lagDebt.magnitude > recoveryDebtThresholdDeltaPerFrame;
                if (recoveryTriggeredByRawStep && recoveryHoldFrameCount > 0)
                {
                    remainingRecoveryHoldFrames = Math.Max(remainingRecoveryHoldFrames, recoveryHoldFrameCount);
                }

                bool recoveryTriggeredByHoldWindow = remainingRecoveryHoldFrames > 0;
                if (recoveryTriggeredByRawStep || recoveryTriggeredByLagDebt || recoveryTriggeredByHoldWindow)
                {
                    effectiveLimit = recoveryLimit;
                }
            }

            if (effectiveLimit > 0f && before > effectiveLimit)
            {
                current = previous + Vector3.ClampMagnitude(delta, effectiveLimit);
                positions[i] = current;
                clampedCount++;
            }

            if (remainingRecoveryHoldFrames > 0)
            {
                remainingRecoveryHoldFrames--;
            }
        }

        for (int i = 1; i < count; i++)
        {
            Vector3 delta = positions[i] - positions[i - 1];
            if (IsFinite(delta))
            {
                maxAfter = Mathf.Max(maxAfter, delta.magnitude);
            }
        }

        return clampedCount;
    }

    private static bool ExportStepExceedsThreshold(
        IReadOnlyDictionary<BoneNames, List<Vector3>> positionsByBone,
        BoneNames boneName,
        int frameIndex,
        float threshold)
    {
        if (threshold <= 0f ||
            positionsByBone == null ||
            !positionsByBone.TryGetValue(boneName, out var positions) ||
            positions == null ||
            frameIndex <= 0 ||
            frameIndex >= positions.Count)
        {
            return false;
        }

        Vector3 delta = positions[frameIndex] - positions[frameIndex - 1];
        return IsFinite(delta) && delta.magnitude > threshold;
    }
}
