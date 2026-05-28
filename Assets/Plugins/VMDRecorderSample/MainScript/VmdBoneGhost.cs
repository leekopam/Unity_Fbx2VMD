using System;
using System.Collections.Generic;
using UnityEngine;
using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

internal readonly struct VmdBoneRotationDiagnostic
{
    public VmdBoneRotationDiagnostic(
        BoneNames boneName,
        string sourceMode,
        Quaternion sourceWorldRotation,
        Quaternion sourceOriginalLocalRotation,
        Quaternion sourceCurrentLocalRotation,
        Quaternion sourceLocalDeltaRotation,
        BoneNames parentBoneName,
        Quaternion sourceParentOriginalLocalRotation,
        Quaternion ghostWorldRotation,
        Quaternion ghostLocalRotation,
        Quaternion vmdRotation,
        float ghostVsSourceLocalDeltaAngleDegrees,
        Quaternion parentRestBasisCorrectedGhostLocalRotation,
        Quaternion parentRestBasisCorrectedVmdRotation,
        float parentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees,
        string exportSourceMode,
        Quaternion exportLocalRotation,
        Quaternion exportVmdRotation,
        float exportVsSourceLocalDeltaAngleDegrees)
    {
        BoneName = boneName;
        SourceMode = sourceMode;
        SourceWorldRotation = sourceWorldRotation;
        SourceOriginalLocalRotation = sourceOriginalLocalRotation;
        SourceCurrentLocalRotation = sourceCurrentLocalRotation;
        SourceLocalDeltaRotation = sourceLocalDeltaRotation;
        ParentBoneName = parentBoneName;
        SourceParentOriginalLocalRotation = sourceParentOriginalLocalRotation;
        GhostWorldRotation = ghostWorldRotation;
        GhostLocalRotation = ghostLocalRotation;
        VmdRotation = vmdRotation;
        GhostVsSourceLocalDeltaAngleDegrees = ghostVsSourceLocalDeltaAngleDegrees;
        ParentRestBasisCorrectedGhostLocalRotation = parentRestBasisCorrectedGhostLocalRotation;
        ParentRestBasisCorrectedVmdRotation = parentRestBasisCorrectedVmdRotation;
        ParentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees = parentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees;
        ExportSourceMode = exportSourceMode;
        ExportLocalRotation = exportLocalRotation;
        ExportVmdRotation = exportVmdRotation;
        ExportVsSourceLocalDeltaAngleDegrees = exportVsSourceLocalDeltaAngleDegrees;
    }

    public BoneNames BoneName { get; }
    public string SourceMode { get; }
    public Quaternion SourceWorldRotation { get; }
    public Quaternion SourceOriginalLocalRotation { get; }
    public Quaternion SourceCurrentLocalRotation { get; }
    public Quaternion SourceLocalDeltaRotation { get; }
    public BoneNames ParentBoneName { get; }
    public Quaternion SourceParentOriginalLocalRotation { get; }
    public Quaternion GhostWorldRotation { get; }
    public Quaternion GhostLocalRotation { get; }
    public Quaternion VmdRotation { get; }
    public float GhostVsSourceLocalDeltaAngleDegrees { get; }
    public Quaternion ParentRestBasisCorrectedGhostLocalRotation { get; }
    public Quaternion ParentRestBasisCorrectedVmdRotation { get; }
    public float ParentRestBasisCorrectedGhostVsSourceLocalDeltaAngleDegrees { get; }
    public string ExportSourceMode { get; }
    public Quaternion ExportLocalRotation { get; }
    public Quaternion ExportVmdRotation { get; }
    public float ExportVsSourceLocalDeltaAngleDegrees { get; }
}

//裏で正規化されたモデル
//(初期ポーズで各ボーンのlocalRotationがQuaternion.identityのモデル)を疑似的にアニメーションさせる
internal sealed class VmdBoneGhost
{
    public Dictionary<BoneNames, (Transform ghost, bool enabled)> GhostDictionary { get; private set; } = new Dictionary<BoneNames, (Transform ghost, bool enabled)>();
    public Dictionary<BoneNames, Vector3> GhostOriginalLocalPositionDictionary { get; private set; } = new Dictionary<BoneNames, Vector3>();
    public Dictionary<BoneNames, Quaternion> GhostOriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();
    public Dictionary<BoneNames, Quaternion> OriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();
    public Dictionary<BoneNames, Quaternion> OriginalLocalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();

    public bool UseBottomCenter { get; private set; } = false;

    const string GhostSalt = "Ghost";
    private Dictionary<BoneNames, Transform> boneDictionary = new Dictionary<BoneNames, Transform>();
    private readonly Dictionary<BoneNames, BoneNames> ghostParentBoneDictionary = new Dictionary<BoneNames, BoneNames>();
    float centerBottomVerticalOffset = 0;

    public VmdBoneGhost(Animator animator, Dictionary<BoneNames, Transform> boneDictionary, bool useBottomCenter)
    {
        this.boneDictionary = boneDictionary;
        UseBottomCenter = useBottomCenter;

        Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)> boneParentDictionary
            = new Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)>()
        {
            { BoneNames.センター, (BoneNames.None, BoneNames.None, BoneNames.全ての親) },
            { BoneNames.下半身,   (BoneNames.None, BoneNames.None, BoneNames.センター) },
            { BoneNames.左足,     (BoneNames.None, BoneNames.None, BoneNames.下半身) },
            { BoneNames.左ひざ,   (BoneNames.None, BoneNames.None, BoneNames.左足) },
            { BoneNames.左足首,   (BoneNames.None, BoneNames.None, BoneNames.左ひざ) },
            { BoneNames.右足,     (BoneNames.None, BoneNames.None, BoneNames.下半身) },
            { BoneNames.右ひざ,   (BoneNames.None, BoneNames.None, BoneNames.右足) },
            { BoneNames.右足首,   (BoneNames.None, BoneNames.None, BoneNames.右ひざ) },
            { BoneNames.上半身,   (BoneNames.None, BoneNames.None, BoneNames.下半身) },
            { BoneNames.上半身2,  (BoneNames.None, BoneNames.None, BoneNames.上半身) },
            { BoneNames.首,       (BoneNames.上半身2, BoneNames.None, BoneNames.上半身) },
            { BoneNames.頭,       (BoneNames.首, BoneNames.上半身2, BoneNames.上半身) },
            { BoneNames.左肩,     (BoneNames.上半身2, BoneNames.None, BoneNames.上半身) },
            { BoneNames.左腕,     (BoneNames.左肩, BoneNames.上半身2, BoneNames.上半身) },
            { BoneNames.左ひじ,   (BoneNames.None, BoneNames.None, BoneNames.左腕) },
            { BoneNames.左手首,   (BoneNames.None, BoneNames.None, BoneNames.左ひじ) },
            { BoneNames.左親指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
            { BoneNames.左親指２, (BoneNames.左親指１, BoneNames.None, BoneNames.None) },
            { BoneNames.左人指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
            { BoneNames.左人指２, (BoneNames.左人指１, BoneNames.None, BoneNames.None) },
            { BoneNames.左人指３, (BoneNames.左人指２, BoneNames.None, BoneNames.None) },
            { BoneNames.左中指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
            { BoneNames.左中指２, (BoneNames.左中指１, BoneNames.None, BoneNames.None) },
            { BoneNames.左中指３, (BoneNames.左中指２, BoneNames.None, BoneNames.None) },
            { BoneNames.左薬指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
            { BoneNames.左薬指２, (BoneNames.左薬指１, BoneNames.None, BoneNames.None) },
            { BoneNames.左薬指３, (BoneNames.左薬指２, BoneNames.None, BoneNames.None) },
            { BoneNames.左小指１, (BoneNames.左手首, BoneNames.None, BoneNames.None) },
            { BoneNames.左小指２, (BoneNames.左小指１, BoneNames.None, BoneNames.None) },
            { BoneNames.左小指３, (BoneNames.左小指２, BoneNames.None, BoneNames.None) },
            { BoneNames.右肩,     (BoneNames.上半身2, BoneNames.None, BoneNames.上半身) },
            { BoneNames.右腕,     (BoneNames.右肩, BoneNames.上半身2, BoneNames.上半身) },
            { BoneNames.右ひじ,   (BoneNames.None, BoneNames.None, BoneNames.右腕) },
            { BoneNames.右手首,   (BoneNames.None, BoneNames.None, BoneNames.右ひじ) },
            { BoneNames.右親指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
            { BoneNames.右親指２, (BoneNames.右親指１, BoneNames.None, BoneNames.None) },
            { BoneNames.右人指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
            { BoneNames.右人指２, (BoneNames.右人指１, BoneNames.None, BoneNames.None) },
            { BoneNames.右人指３, (BoneNames.右人指２, BoneNames.None, BoneNames.None) },
            { BoneNames.右中指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
            { BoneNames.右中指２, (BoneNames.右中指１, BoneNames.None, BoneNames.None) },
            { BoneNames.右中指３, (BoneNames.右中指２, BoneNames.None, BoneNames.None) },
            { BoneNames.右薬指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
            { BoneNames.右薬指２, (BoneNames.右薬指１, BoneNames.None, BoneNames.None) },
            { BoneNames.右薬指３, (BoneNames.右薬指２, BoneNames.None, BoneNames.None) },
            { BoneNames.右小指１, (BoneNames.右手首, BoneNames.None, BoneNames.None) },
            { BoneNames.右小指２, (BoneNames.右小指１, BoneNames.None, BoneNames.None) },
            { BoneNames.右小指３, (BoneNames.右小指２, BoneNames.None, BoneNames.None) },
        };

        // Ghostの生成
        foreach (BoneNames boneName in boneDictionary.Keys)
        {
            // Ignore 全ての親, 足IK, toe IK
            if (boneName == BoneNames.全ての親 ||
                boneName == BoneNames.左足ＩＫ ||
                boneName == BoneNames.右足ＩＫ ||
                boneName == BoneNames.左つま先ＩＫ ||
                boneName == BoneNames.右つま先ＩＫ)
            {
                continue;
            }

            if (boneDictionary[boneName] == null)
            {
                GhostDictionary.Add(boneName, (null, false));
                continue;
            }

            Transform ghost = new GameObject(boneDictionary[boneName].name + GhostSalt).transform;
            if (boneName == BoneNames.センター && UseBottomCenter)
            {
                ghost.position = boneDictionary[BoneNames.全ての親].position;
            }
            else
            {
                ghost.position = boneDictionary[boneName].position;
            }
            ghost.rotation = animator.transform.rotation;
            GhostDictionary.Add(boneName, (ghost, true));
        }

        // Ghostの親子構造を設定
        foreach (BoneNames boneName in boneDictionary.Keys)
        {
            if (boneName == BoneNames.全ての親 ||
                boneName == BoneNames.左足ＩＫ ||
                boneName == BoneNames.右足ＩＫ ||
                boneName == BoneNames.左つま先ＩＫ ||
                boneName == BoneNames.右つま先ＩＫ)
            {
                continue;
            }

            if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled)
            {
                continue;
            }

            if (boneName == BoneNames.センター)
            {
                GhostDictionary[boneName].ghost.SetParent(animator.transform);
                ghostParentBoneDictionary[boneName] = BoneNames.None;
                continue;
            }

            if (boneParentDictionary[boneName].optionParent1 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent1] != null)
            {
                BoneNames parentBoneName = boneParentDictionary[boneName].optionParent1;
                GhostDictionary[boneName].ghost.SetParent(GhostDictionary[parentBoneName].ghost);
                ghostParentBoneDictionary[boneName] = parentBoneName;
            }
            else if (boneParentDictionary[boneName].optionParent2 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent2] != null)
            {
                BoneNames parentBoneName = boneParentDictionary[boneName].optionParent2;
                GhostDictionary[boneName].ghost.SetParent(GhostDictionary[parentBoneName].ghost);
                ghostParentBoneDictionary[boneName] = parentBoneName;
            }
            else if (boneParentDictionary[boneName].necessaryParent != BoneNames.None && boneDictionary[boneParentDictionary[boneName].necessaryParent] != null)
            {
                BoneNames parentBoneName = boneParentDictionary[boneName].necessaryParent;
                GhostDictionary[boneName].ghost.SetParent(GhostDictionary[parentBoneName].ghost);
                ghostParentBoneDictionary[boneName] = parentBoneName;
            }
            else
            {
                GhostDictionary[boneName] = (GhostDictionary[boneName].ghost, false);
                ghostParentBoneDictionary[boneName] = BoneNames.None;
            }
        }

        // 初期状態を保存
        foreach (BoneNames boneName in GhostDictionary.Keys)
        {
            if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled)
            {
                GhostOriginalLocalPositionDictionary.Add(boneName, Vector3.zero);
                GhostOriginalRotationDictionary.Add(boneName, Quaternion.identity);
                OriginalRotationDictionary.Add(boneName, Quaternion.identity);
                OriginalLocalRotationDictionary.Add(boneName, Quaternion.identity);
            }
            else
            {
                GhostOriginalRotationDictionary.Add(boneName, GhostDictionary[boneName].ghost.rotation);
                OriginalRotationDictionary.Add(boneName, boneDictionary[boneName].rotation);
                OriginalLocalRotationDictionary.Add(boneName, boneDictionary[boneName].localRotation);
                if (boneName == BoneNames.センター && UseBottomCenter)
                {
                    GhostOriginalLocalPositionDictionary.Add(boneName, Vector3.zero);
                    continue;
                }
                GhostOriginalLocalPositionDictionary.Add(boneName, GhostDictionary[boneName].ghost.localPosition);
            }
        }

        centerBottomVerticalOffset = Mathf.Max(
            0f,
            Vector3.Dot(
                boneDictionary[BoneNames.センター].position - boneDictionary[BoneNames.全ての親].position,
                Vector3.up));
    }

    public void GhostAll()
    {
        foreach (BoneNames boneName in GhostDictionary.Keys)
        {
            if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled) { continue; }
            Quaternion transQuaternion = boneDictionary[boneName].rotation * Quaternion.Inverse(OriginalRotationDictionary[boneName]);
            GhostDictionary[boneName].ghost.rotation = transQuaternion * GhostOriginalRotationDictionary[boneName];
            if (boneName == BoneNames.センター)
            {
                GhostDictionary[boneName].ghost.rotation = boneDictionary[BoneNames.全ての親].rotation;
                if (UseBottomCenter)
                {
                    GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position - centerBottomVerticalOffset * Vector3.up;
                    continue;
                }
            }

            if (boneName == BoneNames.センター && UseBottomCenter)
            {
                GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position - centerBottomVerticalOffset * Vector3.up;
                continue;
            }
            GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position;
        }
    }

    internal VmdBoneRotationDiagnostic CaptureRotationDiagnostic(BoneNames boneName)
    {
        if (!boneDictionary.TryGetValue(boneName, out Transform source) || source == null)
        {
            throw new ArgumentException($"No source bone transform exists for {boneName}.", nameof(boneName));
        }

        if (!GhostDictionary.TryGetValue(boneName, out var entry) || entry.ghost == null || !entry.enabled)
        {
            throw new InvalidOperationException($"No enabled ghost transform exists for {boneName}.");
        }

        Quaternion ghostLocalRotation = entry.ghost.localRotation;
        Quaternion sourceOriginalLocalRotation = OriginalLocalRotationDictionary.TryGetValue(boneName, out Quaternion originalLocalRotation)
            ? originalLocalRotation
            : Quaternion.identity;
        Quaternion sourceCurrentLocalRotation = source.localRotation;
        Quaternion sourceLocalDeltaRotation = sourceCurrentLocalRotation * Quaternion.Inverse(sourceOriginalLocalRotation);
        BoneNames parentBoneName = ghostParentBoneDictionary.TryGetValue(boneName, out BoneNames mappedParentBoneName)
            ? mappedParentBoneName
            : BoneNames.None;
        Quaternion sourceParentOriginalLocalRotation = parentBoneName != BoneNames.None &&
            OriginalLocalRotationDictionary.TryGetValue(parentBoneName, out Quaternion parentOriginalLocalRotation)
                ? parentOriginalLocalRotation
                : Quaternion.identity;
        Quaternion parentRestBasisCorrectedGhostLocalRotation =
            Quaternion.Inverse(sourceParentOriginalLocalRotation) * ghostLocalRotation * sourceParentOriginalLocalRotation;
        Quaternion exportLocalRotation = parentRestBasisCorrectedGhostLocalRotation;
        return new VmdBoneRotationDiagnostic(
            boneName,
            "ghost_local",
            source.rotation,
            sourceOriginalLocalRotation,
            sourceCurrentLocalRotation,
            sourceLocalDeltaRotation,
            parentBoneName,
            sourceParentOriginalLocalRotation,
            entry.ghost.rotation,
            ghostLocalRotation,
            UnityHumanoidVMDRecorder.ConvertUnityRotationToVmdRotation(ghostLocalRotation),
            Quaternion.Angle(ghostLocalRotation, sourceLocalDeltaRotation),
            parentRestBasisCorrectedGhostLocalRotation,
            UnityHumanoidVMDRecorder.ConvertUnityRotationToVmdRotation(parentRestBasisCorrectedGhostLocalRotation),
            Quaternion.Angle(parentRestBasisCorrectedGhostLocalRotation, sourceLocalDeltaRotation),
            "parent_rest_basis_corrected_ghost_local",
            exportLocalRotation,
            UnityHumanoidVMDRecorder.ConvertUnityRotationToVmdRotation(exportLocalRotation),
            Quaternion.Angle(exportLocalRotation, sourceLocalDeltaRotation));
    }

    internal Quaternion GetExportVmdRotation(BoneNames boneName)
    {
        return CaptureRotationDiagnostic(boneName).ExportVmdRotation;
    }
}
