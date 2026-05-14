using System.Collections.Generic;
using UnityEngine;
using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

//裏で正規化されたモデル
//(初期ポーズで各ボーンのlocalRotationがQuaternion.identityのモデル)を疑似的にアニメーションさせる
internal sealed class VmdBoneGhost
{
    public Dictionary<BoneNames, (Transform ghost, bool enabled)> GhostDictionary { get; private set; } = new Dictionary<BoneNames, (Transform ghost, bool enabled)>();
    public Dictionary<BoneNames, Vector3> GhostOriginalLocalPositionDictionary { get; private set; } = new Dictionary<BoneNames, Vector3>();
    public Dictionary<BoneNames, Quaternion> GhostOriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();
    public Dictionary<BoneNames, Quaternion> OriginalRotationDictionary { get; private set; } = new Dictionary<BoneNames, Quaternion>();

    public bool UseBottomCenter { get; private set; } = false;

    const string GhostSalt = "Ghost";
    private Dictionary<BoneNames, Transform> boneDictionary = new Dictionary<BoneNames, Transform>();
    float centerOffsetLength = 0;

    public VmdBoneGhost(Animator animator, Dictionary<BoneNames, Transform> boneDictionary, bool useBottomCenter)
    {
        this.boneDictionary = boneDictionary;
        UseBottomCenter = useBottomCenter;

        Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)> boneParentDictionary
            = new Dictionary<BoneNames, (BoneNames optionParent1, BoneNames optionParent2, BoneNames necessaryParent)>()
        {
            { BoneNames.センター, (BoneNames.None, BoneNames.None, BoneNames.全ての親) },
            { BoneNames.左足,     (BoneNames.None, BoneNames.None, BoneNames.センター) },
            { BoneNames.左ひざ,   (BoneNames.None, BoneNames.None, BoneNames.左足) },
            { BoneNames.左足首,   (BoneNames.None, BoneNames.None, BoneNames.左ひざ) },
            { BoneNames.右足,     (BoneNames.None, BoneNames.None, BoneNames.センター) },
            { BoneNames.右ひざ,   (BoneNames.None, BoneNames.None, BoneNames.右足) },
            { BoneNames.右足首,   (BoneNames.None, BoneNames.None, BoneNames.右ひざ) },
            { BoneNames.上半身,   (BoneNames.None, BoneNames.None, BoneNames.センター) },
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
                continue;
            }

            if (boneParentDictionary[boneName].optionParent1 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent1] != null)
            {
                GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].optionParent1].ghost);
            }
            else if (boneParentDictionary[boneName].optionParent2 != BoneNames.None && boneDictionary[boneParentDictionary[boneName].optionParent2] != null)
            {
                GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].optionParent2].ghost);
            }
            else if (boneParentDictionary[boneName].necessaryParent != BoneNames.None && boneDictionary[boneParentDictionary[boneName].necessaryParent] != null)
            {
                GhostDictionary[boneName].ghost.SetParent(GhostDictionary[boneParentDictionary[boneName].necessaryParent].ghost);
            }
            else
            {
                GhostDictionary[boneName] = (GhostDictionary[boneName].ghost, false);
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
            }
            else
            {
                GhostOriginalRotationDictionary.Add(boneName, GhostDictionary[boneName].ghost.rotation);
                OriginalRotationDictionary.Add(boneName, boneDictionary[boneName].rotation);
                if (boneName == BoneNames.センター && UseBottomCenter)
                {
                    GhostOriginalLocalPositionDictionary.Add(boneName, Vector3.zero);
                    continue;
                }
                GhostOriginalLocalPositionDictionary.Add(boneName, GhostDictionary[boneName].ghost.localPosition);
            }
        }

        centerOffsetLength = Vector3.Distance(boneDictionary[BoneNames.全ての親].position, boneDictionary[BoneNames.センター].position);
    }

    public void GhostAll()
    {
        foreach (BoneNames boneName in GhostDictionary.Keys)
        {
            if (GhostDictionary[boneName].ghost == null || !GhostDictionary[boneName].enabled) { continue; }
            Quaternion transQuaternion = boneDictionary[boneName].rotation * Quaternion.Inverse(OriginalRotationDictionary[boneName]);
            GhostDictionary[boneName].ghost.rotation = transQuaternion * GhostOriginalRotationDictionary[boneName];
            if (boneName == BoneNames.センター && UseBottomCenter)
            {
                GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position - centerOffsetLength * GhostDictionary[boneName].ghost.up;
                continue;
            }
            GhostDictionary[boneName].ghost.position = boneDictionary[boneName].position;
        }
    }
}
