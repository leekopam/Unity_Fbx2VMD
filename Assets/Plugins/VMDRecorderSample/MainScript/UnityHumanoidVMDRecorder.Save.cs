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
            return VmdSaveResult.Fail(filePath, "VMD 저장 전에 녹화를 먼저 중지해야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return VmdSaveResult.Fail(filePath, "저장 경로가 비어 있습니다.");
        }

        string directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return VmdSaveResult.Fail(filePath, "저장 폴더를 확인할 수 없습니다.");
        }

        if (frameNumberSaved <= 0)
        {
            return VmdSaveResult.Fail(filePath, "저장할 녹화 프레임이 없습니다.");
        }

        if (positionDictionarySaved == null || rotationDictionarySaved == null || BoneDictionary == null)
        {
            return VmdSaveResult.Fail(filePath, "녹화 데이터가 초기화되지 않았습니다.");
        }

        int keyReductionLevel = Mathf.Max(1, KeyReductionLevel);
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
            Debug.Log($"{transform.name} VMD 파일 생성 시작: {filePath}");
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
                    CenterNameString,
                    GrooveNameString));

            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists || fileInfo.Length <= 0)
            {
                return VmdSaveResult.Fail(filePath, "VMD 파일이 생성되지 않았거나 비어 있습니다.");
            }

            Debug.Log($"{transform.name} VMD 파일 생성 완료: {filePath}");
            return VmdSaveResult.Ok(filePath, safeFrameCount, fileInfo.Length);
        }
        catch (Exception ex)
        {
            Debug.LogError($"VMD 쓰기 오류: {ex.Message}\n{ex.StackTrace}");
            return VmdSaveResult.Fail(filePath, ex.Message);
        }
    }

    public async void SaveVMD(string modelName, string filePath)
    {
        VmdSaveResult result = await SaveVMDAsync(modelName, filePath);
        if (!result.Success)
        {
            Debug.LogError($"VMD 저장 실패: {result.ErrorMessage}");
        }
    }

    public async void SaveVMD(string modelName, string filePath, int keyReductionLevel)
    {
        KeyReductionLevel = keyReductionLevel;
        VmdSaveResult result = await SaveVMDAsync(modelName, filePath);
        if (!result.Success)
        {
            Debug.LogError($"VMD 저장 실패: {result.ErrorMessage}");
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
            Debug.LogWarning($"[VMDRecorder] 프레임 수가 일부 본 데이터와 맞지 않아 {frameNumberSaved} -> {safeFrameCount}로 저장합니다.");
        }

        return safeFrameCount;
    }

}
