using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Threading.Tasks;

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
        exportIkSourceDiagnosticSamplesSaved = VmdExportDiagnosticsWriter.BuildFinalExportIkSourceDiagnosticSamples(
            exportIkSourceDiagnosticSamplesSaved,
            positionDictionarySaved,
            safeFrameCount,
            ConvertVmdExportPositionToUnityMeters);

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
    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

}
