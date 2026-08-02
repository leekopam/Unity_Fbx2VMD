using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.Serialization;

public partial class UnityHumanoidVMDRecorder
{
    public async Task<VmdSaveResult> SaveVMDAsync(string modelName, string filePath)
    {
        if (IsRecording)
        {
            return VmdSaveResult.Fail(filePath, "Recording must be stopped before saving VMD.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return VmdSaveResult.Fail(filePath, "Save path is empty.");
        }

        string directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return VmdSaveResult.Fail(filePath, "Cannot verify save folder.");
        }

        if (frameNumberSaved <= 0)
        {
            return VmdSaveResult.Fail(filePath, "No recording frames to save.");
        }

        if (positionDictionarySaved == null || rotationDictionarySaved == null || BoneDictionary == null)
        {
            return VmdSaveResult.Fail(filePath, "녹화 데이터가 초기화되지 않았습니다.");
        }

        // MMD character export must keep every recorded frame. Sparse humanoid
        // keys make MMD interpolate IK targets across frames and can look like
        // jitter or root-locking even when the recorded Unity pose is stable.
        int keyReductionLevel = 1;
        List<BoneNames> activeBones = GetActiveRecordedBones();
        if (activeBones.Count == 0)
        {
            return VmdSaveResult.Fail(filePath, "유효한 본 프레임 데이터가 없습니다.");
        }

        int safeFrameCount = CalculateSafeFrameCount(activeBones);
        if (safeFrameCount <= 0)
        {
            return VmdSaveResult.Fail(filePath, "유효한 본 프레임 데이터가 없습니다.");
        }

        ApplyMmdExportSafetyGuards(safeFrameCount);
        IReadOnlyList<VmdIkFrame> ikFrames = UseMmdIkDynamicToggleOnLargeExportSteps
            ? BuildMmdIkToggleFramesFromExportSteps(
                positionDictionarySaved,
                safeFrameCount,
                MmdIkDynamicToggleFootStepThreshold,
                MmdIkDynamicToggleToeStepThreshold)
            : null;
        exportIkSourceDiagnosticSamplesSaved = BuildFinalExportIkSourceDiagnosticSamples(
            exportIkSourceDiagnosticSamplesSaved,
            positionDictionarySaved,
            safeFrameCount);

        VmdMorphRecorder morphSnapshot = morphRecorderSaved;
        if (morphSnapshot != null)
        {
            morphSnapshot.DisableIntron();
            if (TrimMorphNumber)
            {
                morphSnapshot.TrimMorphNumber();
            }
        }

        Directory.CreateDirectory(directory);
        string safeModelName = string.IsNullOrWhiteSpace(modelName) ? "fbxToVMD" : modelName;

        try
        {
            Debug.Log($"{transform.name} VMD file creation started: {filePath}");
            await Task.Run(() =>
                VmdFileWriter.WriteVmdFile(
                    safeModelName,
                    filePath,
                    activeBones,
                    safeFrameCount,
                    keyReductionLevel,
                    positionDictionarySaved,
                    rotationDictionarySaved,
                    morphSnapshot,
                    UseCenterAsParentOfAll,
                    RouteHumanoidCenterToGroove,
                    CenterNameString,
                    GrooveNameString,
                    ikFrames));

            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
            {
                return VmdSaveResult.Fail(filePath, "VMD 파일이 생성되지 않았거나 비어 있습니다.");
            }

            string exportRotationDiagnosticsCsvPath = WriteExportRotationDiagnosticsCsv(filePath);
            string exportIkSourceDiagnosticsCsvPath = WriteExportIkSourceDiagnosticsCsv(filePath);
            Debug.Log($"{transform.name} VMD file creation completed: {filePath}");
            return VmdSaveResult.Ok(filePath, safeFrameCount, fileInfo.Length, exportRotationDiagnosticsCsvPath, exportIkSourceDiagnosticsCsvPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"VMD write error: {ex.Message}\n{ex.StackTrace}");
            return VmdSaveResult.Fail(filePath, ex.Message);
        }
    }

    public async void SaveVMD(string modelName, string filePath)
    {
        VmdSaveResult result = await SaveVMDAsync(modelName, filePath);
        if (!result.Success)
        {
            Debug.LogError($"VMD save failed: {result.ErrorMessage}");
        }
    }

    public async void SaveVMD(string modelName, string filePath, int keyReductionLevel)
    {
        KeyReductionLevel = keyReductionLevel;
        VmdSaveResult result = await SaveVMDAsync(modelName, filePath);
        if (!result.Success)
        {
            Debug.LogError($"VMD save failed: {result.ErrorMessage}");
        }
    }

    private List<BoneNames> GetActiveRecordedBones()
    {
        List<BoneNames> activeBones = new List<BoneNames>();
        foreach (BoneNames boneName in Enum.GetValues(typeof(BoneNames)))
        {
            if (!BoneDictionary.Keys.Contains(boneName)) { continue; }
            if (BoneDictionary[boneName] == null) { continue; }
            if (!UseParentOfAll && boneName == BoneNames.全ての親) { continue; }
            if (!positionDictionarySaved.ContainsKey(boneName)) { continue; }
            if (!rotationDictionarySaved.ContainsKey(boneName)) { continue; }

            activeBones.Add(boneName);
        }

        return activeBones;
    }

    private void ApplyMmdExportSafetyGuards(int safeFrameCount)
    {
        ApplyMmdIkExportDeltaSpikeGuard(safeFrameCount);
        ApplyMmdCenterExportDeltaSpikeGuard(safeFrameCount);
        ApplyMmdCenterFloorLiftGuard(safeFrameCount);
        if (!LiftMmdCenterYToKeepFeetAboveFloor)
        {
            ApplyMmdCenterAwareFootFloorGuard(safeFrameCount);
        }
    }

    private int CalculateSafeFrameCount(List<BoneNames> activeBones)
    {
        if (activeBones == null || activeBones.Count == 0)
        {
            return 0;
        }

        int safeFrameCount = frameNumberSaved;
        foreach (BoneNames boneName in activeBones)
        {
            safeFrameCount = Math.Min(safeFrameCount, positionDictionarySaved[boneName].Count);
            safeFrameCount = Math.Min(safeFrameCount, rotationDictionarySaved[boneName].Count);
        }

        if (safeFrameCount < frameNumberSaved)
        {
            Debug.LogWarning($"[VMDRecorder] Frame count mismatch with some bone data; saving {frameNumberSaved} -> {safeFrameCount}.");
        }

        return safeFrameCount;
    }

    private void ApplyMmdCenterAwareFootFloorGuard(int safeFrameCount)
    {
        if (!ClampMmdFootIkYToFloor || LiftMmdCenterYToKeepFeetAboveFloor || safeFrameCount <= 0 || positionDictionarySaved == null)
        {
            return;
        }

        BoneNames centerBone = (BoneNames)1;
        BoneNames leftFootIk = (BoneNames)2;
        BoneNames rightFootIk = (BoneNames)3;
        BoneNames leftToeIk = (BoneNames)4;
        BoneNames rightToeIk = (BoneNames)5;

        if (!positionDictionarySaved.TryGetValue(centerBone, out var centerPositions))
        {
            return;
        }

        ApplyMmdCenterAwareFootFloorGuard(leftFootIk, centerPositions, safeFrameCount, MinMmdFootIkY);
        ApplyMmdCenterAwareFootFloorGuard(rightFootIk, centerPositions, safeFrameCount, MinMmdFootIkY);
        ApplyMmdCenterAwareToeFloorGuard(leftToeIk, leftFootIk, centerPositions, safeFrameCount, MinMmdFootIkY);
        ApplyMmdCenterAwareToeFloorGuard(rightToeIk, rightFootIk, centerPositions, safeFrameCount, MinMmdFootIkY);
    }

    private void ApplyMmdCenterAwareFootFloorGuard(
        BoneNames footIkBone,
        List<Vector3> centerPositions,
        int safeFrameCount,
        float minY)
    {
        if (!positionDictionarySaved.TryGetValue(footIkBone, out var footPositions))
        {
            return;
        }

        int count = Math.Min(safeFrameCount, Math.Min(centerPositions.Count, footPositions.Count));
        for (int i = 0; i < count; i++)
        {
            Vector3 foot = footPositions[i];
            float centerY = centerPositions[i].y;
            if (float.IsNaN(foot.y) || float.IsNaN(centerY) || foot.y + centerY >= minY)
            {
                continue;
            }

            foot.y = minY - centerY;
            footPositions[i] = foot;
        }
    }

    private void ApplyMmdCenterFloorLiftGuard(int safeFrameCount)
    {
        LastMmdCenterFloorLiftAdjustedFrameCount = 0;
        LastMmdCenterFloorLiftMinEffectiveYBefore = 0f;
        LastMmdCenterFloorLiftMinEffectiveYAfter = 0f;
        LastMmdCenterFloorLiftMaxY = 0f;

        if (!LiftMmdCenterYToKeepFeetAboveFloor || safeFrameCount <= 0 || positionDictionarySaved == null)
        {
            return;
        }

        BoneNames centerBone = (BoneNames)1;
        if (!positionDictionarySaved.TryGetValue(centerBone, out var centerPositions))
        {
            return;
        }

        positionDictionarySaved.TryGetValue((BoneNames)2, out var leftFootIkPositions);
        positionDictionarySaved.TryGetValue((BoneNames)3, out var rightFootIkPositions);
        positionDictionarySaved.TryGetValue((BoneNames)4, out var leftToeIkPositions);
        positionDictionarySaved.TryGetValue((BoneNames)5, out var rightToeIkPositions);

        LastMmdCenterFloorLiftAdjustedFrameCount = ApplyMmdCenterFloorLiftFromIkPositions(
            centerPositions,
            leftFootIkPositions,
            rightFootIkPositions,
            leftToeIkPositions,
            rightToeIkPositions,
            safeFrameCount,
            MinMmdFootIkY,
            ClampMmdCenterExportDeltaSpikes ? MaxMmdCenterExportDeltaPerFrame : float.PositiveInfinity,
            out float minBefore,
            out float minAfter,
            out float maxCenterLift);

        LastMmdCenterFloorLiftMinEffectiveYBefore = minBefore;
        LastMmdCenterFloorLiftMinEffectiveYAfter = minAfter;
        LastMmdCenterFloorLiftMaxY = maxCenterLift;
    }

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

    private void ApplyMmdCenterAwareToeFloorGuard(
        BoneNames toeIkBone,
        BoneNames footIkBone,
        List<Vector3> centerPositions,
        int safeFrameCount,
        float minY)
    {
        if (!positionDictionarySaved.TryGetValue(toeIkBone, out var toePositions) ||
            !positionDictionarySaved.TryGetValue(footIkBone, out var footPositions))
        {
            return;
        }

        int count = Math.Min(safeFrameCount, Math.Min(centerPositions.Count, Math.Min(footPositions.Count, toePositions.Count)));
        for (int i = 0; i < count; i++)
        {
            Vector3 toe = toePositions[i];
            float effectiveY = centerPositions[i].y + footPositions[i].y + toe.y;
            if (float.IsNaN(toe.y) || float.IsNaN(effectiveY) || effectiveY >= minY)
            {
                continue;
            }

            toe.y = minY - centerPositions[i].y - footPositions[i].y;
            toePositions[i] = toe;
        }
    }

    private void ApplyMmdIkExportDeltaSpikeGuard(int safeFrameCount)
    {
        LastMmdIkExportDeltaClampCount = 0;
        LastMmdIkExportMaxDeltaBefore = 0f;
        LastMmdIkExportMaxDeltaAfter = 0f;

        if (!ClampMmdIkExportDeltaSpikes || safeFrameCount <= 1 || positionDictionarySaved == null)
        {
            return;
        }

        if (UseMmdIkDynamicToggleOnLargeExportSteps)
        {
            return;
        }

        ApplyMmdIkExportDeltaSpikeGuard((BoneNames)2, MaxMmdFootIkExportDeltaPerFrame, safeFrameCount);
        ApplyMmdIkExportDeltaSpikeGuard((BoneNames)3, MaxMmdFootIkExportDeltaPerFrame, safeFrameCount);
        ApplyMmdIkExportDeltaSpikeGuard((BoneNames)4, MaxMmdToeIkExportDeltaPerFrame, safeFrameCount);
        ApplyMmdIkExportDeltaSpikeGuard((BoneNames)5, MaxMmdToeIkExportDeltaPerFrame, safeFrameCount);
    }

    private void ApplyMmdCenterExportDeltaSpikeGuard(int safeFrameCount)
    {
        LastMmdCenterExportDeltaClampCount = 0;
        LastMmdCenterExportMaxDeltaBefore = 0f;
        LastMmdCenterExportMaxDeltaAfter = 0f;

        if (!ClampMmdCenterExportDeltaSpikes || safeFrameCount <= 1 || positionDictionarySaved == null)
        {
            return;
        }

        if (!positionDictionarySaved.TryGetValue((BoneNames)1, out var positions))
        {
            return;
        }

        LastMmdCenterExportDeltaClampCount = ClampMmdCenterExportDeltaSpikePositions(
            positions,
            safeFrameCount,
            MaxMmdCenterExportDeltaPerFrame,
            out float maxBefore,
            out float maxAfter);

        LastMmdCenterExportMaxDeltaBefore = maxBefore;
        LastMmdCenterExportMaxDeltaAfter = maxAfter;
    }

    private void ApplyMmdIkExportDeltaSpikeGuard(BoneNames boneName, float maxDeltaPerFrame, int safeFrameCount)
    {
        if (!positionDictionarySaved.TryGetValue(boneName, out var positions))
        {
            return;
        }

        int clamped;
        float maxBefore;
        float maxAfter;
        if (UseMmdIkExportDeltaRecoveryLimit)
        {
            clamped = ClampMmdIkExportDeltaSpikePositions(
                positions,
                safeFrameCount,
                maxDeltaPerFrame,
                MmdIkExportDeltaRecoveryLimitPerFrame,
                MmdIkExportDeltaRecoveryTriggerPerFrame,
                MmdIkExportDeltaRecoveryDebtThresholdPerFrame,
                MmdIkExportDeltaRecoveryHoldFrames,
                out maxBefore,
                out maxAfter);
        }
        else
        {
            clamped = ClampMmdIkExportDeltaSpikePositions(
                positions,
                safeFrameCount,
                maxDeltaPerFrame,
                out maxBefore,
                out maxAfter);
        }

        LastMmdIkExportDeltaClampCount += clamped;
        LastMmdIkExportMaxDeltaBefore = Mathf.Max(LastMmdIkExportMaxDeltaBefore, maxBefore);
        LastMmdIkExportMaxDeltaAfter = Mathf.Max(LastMmdIkExportMaxDeltaAfter, maxAfter);
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

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

}
