using System.Collections.Generic;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

internal static class VmdUnityTransformConverter
{
    internal const float WriterPositionScale = 12.5f;
    internal const string ParentOfAllBoneName = "\u5168\u3066\u306e\u89aa";
    internal const string CenterBoneName = "\u30bb\u30f3\u30bf\u30fc";
    internal const string GrooveBoneName = "\u30b0\u30eb\u30fc\u30d6";

    internal static Vector3 ConvertUnityMetersToVmdPosition(Vector3 unityPosition)
    {
        return new Vector3(
            -unityPosition.x * WriterPositionScale,
            unityPosition.y * WriterPositionScale,
            -unityPosition.z * WriterPositionScale);
    }

    internal static Vector3 ConvertVmdPositionToUnityMeters(Vector3 vmdPosition)
    {
        return new Vector3(
            -vmdPosition.x / WriterPositionScale,
            vmdPosition.y / WriterPositionScale,
            -vmdPosition.z / WriterPositionScale);
    }

    internal static Quaternion ConvertUnityRotationToVmdRotation(Quaternion unityRotation)
    {
        return new Quaternion(-unityRotation.x, unityRotation.y, -unityRotation.z, unityRotation.w);
    }

    internal static Quaternion ConvertVmdRotationToUnityRotation(Quaternion vmdRotation)
    {
        return new Quaternion(-vmdRotation.x, vmdRotation.y, -vmdRotation.z, vmdRotation.w);
    }

    internal static string ResolveParentOfAllCarrierName(
        bool useCenterAsParentOfAll,
        bool routeCenterBoneToGroove,
        string centerNameString = CenterBoneName)
    {
        return useCenterAsParentOfAll && routeCenterBoneToGroove
            ? centerNameString
            : ParentOfAllBoneName;
    }

    internal static string ResolveCenterCarrierName(
        bool useCenterAsParentOfAll,
        bool routeCenterBoneToGroove,
        string grooveNameString = GrooveBoneName)
    {
        return useCenterAsParentOfAll && routeCenterBoneToGroove
            ? grooveNameString
            : CenterBoneName;
    }
}

internal readonly struct VmdHumanoidBoneBinding
{
    internal VmdHumanoidBoneBinding(
        string writerBoneName,
        BoneNames recorderBoneName,
        bool hasHumanBodyBone,
        HumanBodyBones humanBodyBone,
        bool isIkTarget,
        bool isMotionCarrier)
    {
        WriterBoneName = writerBoneName;
        RecorderBoneName = recorderBoneName;
        HasHumanBodyBone = hasHumanBodyBone;
        HumanBodyBone = humanBodyBone;
        IsIkTarget = isIkTarget;
        IsMotionCarrier = isMotionCarrier;
    }

    internal string WriterBoneName { get; }

    internal BoneNames RecorderBoneName { get; }

    internal bool HasHumanBodyBone { get; }

    internal HumanBodyBones HumanBodyBone { get; }

    internal bool IsIkTarget { get; }

    internal bool IsMotionCarrier { get; }
}

internal static class VmdHumanoidBoneMap
{
    private const int ParentOfAllOrdinal = 0;
    private const int LeftFootIkOrdinal = 2;
    private const int RightToeIkOrdinal = 5;

    private static readonly IReadOnlyDictionary<string, BoneNames> RecorderBonesByWriterName =
        new Dictionary<string, BoneNames>
        {
            ["全ての親"] = BoneNames.全ての親,
            ["センター"] = BoneNames.センター,
            ["左足ＩＫ"] = BoneNames.左足ＩＫ,
            ["右足ＩＫ"] = BoneNames.右足ＩＫ,
            ["左つま先ＩＫ"] = BoneNames.左つま先ＩＫ,
            ["右つま先ＩＫ"] = BoneNames.右つま先ＩＫ,
            ["上半身"] = BoneNames.上半身,
            ["上半身2"] = BoneNames.上半身2,
            ["首"] = BoneNames.首,
            ["頭"] = BoneNames.頭,
            ["左肩"] = BoneNames.左肩,
            ["左腕"] = BoneNames.左腕,
            ["左ひじ"] = BoneNames.左ひじ,
            ["左手首"] = BoneNames.左手首,
            ["右肩"] = BoneNames.右肩,
            ["右腕"] = BoneNames.右腕,
            ["右ひじ"] = BoneNames.右ひじ,
            ["右手首"] = BoneNames.右手首,
            ["左親指１"] = BoneNames.左親指１,
            ["左親指２"] = BoneNames.左親指２,
            ["左人指１"] = BoneNames.左人指１,
            ["左人指２"] = BoneNames.左人指２,
            ["左人指３"] = BoneNames.左人指３,
            ["左中指１"] = BoneNames.左中指１,
            ["左中指２"] = BoneNames.左中指２,
            ["左中指３"] = BoneNames.左中指３,
            ["左薬指１"] = BoneNames.左薬指１,
            ["左薬指２"] = BoneNames.左薬指２,
            ["左薬指３"] = BoneNames.左薬指３,
            ["左小指１"] = BoneNames.左小指１,
            ["左小指２"] = BoneNames.左小指２,
            ["左小指３"] = BoneNames.左小指３,
            ["右親指１"] = BoneNames.右親指１,
            ["右親指２"] = BoneNames.右親指２,
            ["右人指１"] = BoneNames.右人指１,
            ["右人指２"] = BoneNames.右人指２,
            ["右人指３"] = BoneNames.右人指３,
            ["右中指１"] = BoneNames.右中指１,
            ["右中指２"] = BoneNames.右中指２,
            ["右中指３"] = BoneNames.右中指３,
            ["右薬指１"] = BoneNames.右薬指１,
            ["右薬指２"] = BoneNames.右薬指２,
            ["右薬指３"] = BoneNames.右薬指３,
            ["右小指１"] = BoneNames.右小指１,
            ["右小指２"] = BoneNames.右小指２,
            ["右小指３"] = BoneNames.右小指３,
            ["左足"] = BoneNames.左足,
            ["右足"] = BoneNames.右足,
            ["左ひざ"] = BoneNames.左ひざ,
            ["右ひざ"] = BoneNames.右ひざ,
            ["左足首"] = BoneNames.左足首,
            ["右足首"] = BoneNames.右足首,
            ["下半身"] = BoneNames.下半身,
        };

    private static readonly IReadOnlyDictionary<BoneNames, HumanBodyBones> HumanBonesByRecorderBone =
        new Dictionary<BoneNames, HumanBodyBones>
        {
            [BoneNames.センター] = HumanBodyBones.Hips,
            [BoneNames.下半身] = HumanBodyBones.Hips,
            [BoneNames.左足ＩＫ] = HumanBodyBones.LeftFoot,
            [BoneNames.右足ＩＫ] = HumanBodyBones.RightFoot,
            [BoneNames.左つま先ＩＫ] = HumanBodyBones.LeftToes,
            [BoneNames.右つま先ＩＫ] = HumanBodyBones.RightToes,
            [BoneNames.上半身] = HumanBodyBones.Spine,
            [BoneNames.上半身2] = HumanBodyBones.Chest,
            [BoneNames.首] = HumanBodyBones.Neck,
            [BoneNames.頭] = HumanBodyBones.Head,
            [BoneNames.左肩] = HumanBodyBones.LeftShoulder,
            [BoneNames.右肩] = HumanBodyBones.RightShoulder,
            [BoneNames.左腕] = HumanBodyBones.LeftUpperArm,
            [BoneNames.右腕] = HumanBodyBones.RightUpperArm,
            [BoneNames.左ひじ] = HumanBodyBones.LeftLowerArm,
            [BoneNames.右ひじ] = HumanBodyBones.RightLowerArm,
            [BoneNames.左手首] = HumanBodyBones.LeftHand,
            [BoneNames.右手首] = HumanBodyBones.RightHand,
            [BoneNames.左親指１] = HumanBodyBones.LeftThumbProximal,
            [BoneNames.右親指１] = HumanBodyBones.RightThumbProximal,
            [BoneNames.左親指２] = HumanBodyBones.LeftThumbIntermediate,
            [BoneNames.右親指２] = HumanBodyBones.RightThumbIntermediate,
            [BoneNames.左人指１] = HumanBodyBones.LeftIndexProximal,
            [BoneNames.右人指１] = HumanBodyBones.RightIndexProximal,
            [BoneNames.左人指２] = HumanBodyBones.LeftIndexIntermediate,
            [BoneNames.右人指２] = HumanBodyBones.RightIndexIntermediate,
            [BoneNames.左人指３] = HumanBodyBones.LeftIndexDistal,
            [BoneNames.右人指３] = HumanBodyBones.RightIndexDistal,
            [BoneNames.左中指１] = HumanBodyBones.LeftMiddleProximal,
            [BoneNames.右中指１] = HumanBodyBones.RightMiddleProximal,
            [BoneNames.左中指２] = HumanBodyBones.LeftMiddleIntermediate,
            [BoneNames.右中指２] = HumanBodyBones.RightMiddleIntermediate,
            [BoneNames.左中指３] = HumanBodyBones.LeftMiddleDistal,
            [BoneNames.右中指３] = HumanBodyBones.RightMiddleDistal,
            [BoneNames.左薬指１] = HumanBodyBones.LeftRingProximal,
            [BoneNames.右薬指１] = HumanBodyBones.RightRingProximal,
            [BoneNames.左薬指２] = HumanBodyBones.LeftRingIntermediate,
            [BoneNames.右薬指２] = HumanBodyBones.RightRingIntermediate,
            [BoneNames.左薬指３] = HumanBodyBones.LeftRingDistal,
            [BoneNames.右薬指３] = HumanBodyBones.RightRingDistal,
            [BoneNames.左小指１] = HumanBodyBones.LeftLittleProximal,
            [BoneNames.右小指１] = HumanBodyBones.RightLittleProximal,
            [BoneNames.左小指２] = HumanBodyBones.LeftLittleIntermediate,
            [BoneNames.右小指２] = HumanBodyBones.RightLittleIntermediate,
            [BoneNames.左小指３] = HumanBodyBones.LeftLittleDistal,
            [BoneNames.右小指３] = HumanBodyBones.RightLittleDistal,
            [BoneNames.左足] = HumanBodyBones.LeftUpperLeg,
            [BoneNames.右足] = HumanBodyBones.RightUpperLeg,
            [BoneNames.左ひざ] = HumanBodyBones.LeftLowerLeg,
            [BoneNames.右ひざ] = HumanBodyBones.RightLowerLeg,
            [BoneNames.左足首] = HumanBodyBones.LeftFoot,
            [BoneNames.右足首] = HumanBodyBones.RightFoot,
        };

    internal static bool TryResolveWriterBoneName(
        string writerBoneName,
        out VmdHumanoidBoneBinding binding)
    {
        return TryResolveWriterBoneName(
            writerBoneName,
            useCenterAsParentOfAll: false,
            routeCenterBoneToGroove: false,
            centerNameString: VmdUnityTransformConverter.CenterBoneName,
            grooveNameString: VmdUnityTransformConverter.GrooveBoneName,
            out binding);
    }

    internal static bool TryResolveWriterBoneName(
        string writerBoneName,
        bool useCenterAsParentOfAll,
        bool routeCenterBoneToGroove,
        string centerNameString,
        string grooveNameString,
        out VmdHumanoidBoneBinding binding)
    {
        if (string.IsNullOrEmpty(writerBoneName))
        {
            binding = CreateUnresolvedBinding();
            return false;
        }

        if (useCenterAsParentOfAll && routeCenterBoneToGroove)
        {
            if (writerBoneName == centerNameString)
            {
                binding = CreateBinding(writerBoneName, BoneNames.全ての親);
                return true;
            }

            if (writerBoneName == grooveNameString)
            {
                binding = CreateBinding(writerBoneName, BoneNames.センター);
                return true;
            }
        }

        if (!RecorderBonesByWriterName.TryGetValue(writerBoneName, out BoneNames recorderBoneName))
        {
            binding = CreateUnresolvedBinding();
            return false;
        }

        binding = CreateBinding(writerBoneName, recorderBoneName);
        return true;
    }

    private static VmdHumanoidBoneBinding CreateBinding(string writerBoneName, BoneNames recorderBoneName)
    {
        bool hasHumanBodyBone = HumanBonesByRecorderBone.TryGetValue(recorderBoneName, out HumanBodyBones humanBodyBone);
        int ordinal = (int)recorderBoneName;
        bool isIkTarget = ordinal >= LeftFootIkOrdinal && ordinal <= RightToeIkOrdinal;
        bool isMotionCarrier = ordinal >= ParentOfAllOrdinal && ordinal <= RightToeIkOrdinal;

        return new VmdHumanoidBoneBinding(
            writerBoneName,
            recorderBoneName,
            hasHumanBodyBone,
            hasHumanBodyBone ? humanBodyBone : HumanBodyBones.LastBone,
            isIkTarget,
            isMotionCarrier);
    }

    private static VmdHumanoidBoneBinding CreateUnresolvedBinding()
    {
        return new VmdHumanoidBoneBinding(
            string.Empty,
            BoneNames.None,
            hasHumanBodyBone: false,
            humanBodyBone: HumanBodyBones.LastBone,
            isIkTarget: false,
            isMotionCarrier: false);
    }
}
