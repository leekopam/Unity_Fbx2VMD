using System.Collections.Generic;
using UnityEngine;

internal sealed class VmdMotionData
{
    internal VmdMotionData(
        string header,
        string modelName,
        IReadOnlyList<VmdBoneFrame> boneFrames,
        IReadOnlyList<VmdMorphFrame> morphFrames,
        uint cameraFrameCount,
        uint lightFrameCount,
        uint selfShadowFrameCount,
        uint ikFrameCount,
        IReadOnlyList<VmdIkFrame> ikFrames = null)
    {
        Header = header;
        ModelName = modelName;
        BoneFrames = boneFrames;
        MorphFrames = morphFrames;
        CameraFrameCount = cameraFrameCount;
        LightFrameCount = lightFrameCount;
        SelfShadowFrameCount = selfShadowFrameCount;
        IkFrameCount = ikFrameCount;
        IkFrames = ikFrames ?? new List<VmdIkFrame>();
    }

    internal string Header { get; }

    internal string ModelName { get; }

    internal IReadOnlyList<VmdBoneFrame> BoneFrames { get; }

    internal IReadOnlyList<VmdMorphFrame> MorphFrames { get; }

    internal uint CameraFrameCount { get; }

    internal uint LightFrameCount { get; }

    internal uint SelfShadowFrameCount { get; }

    internal uint IkFrameCount { get; }

    internal IReadOnlyList<VmdIkFrame> IkFrames { get; }
}

internal readonly struct VmdBoneFrame
{
    internal VmdBoneFrame(string boneName, uint frameIndex, Vector3 position, Quaternion rotation, byte[] interpolation)
    {
        BoneName = boneName;
        FrameIndex = frameIndex;
        Position = position;
        Rotation = rotation;
        Interpolation = interpolation;
    }

    internal string BoneName { get; }

    internal uint FrameIndex { get; }

    internal Vector3 Position { get; }

    internal Quaternion Rotation { get; }

    internal byte[] Interpolation { get; }
}

internal readonly struct VmdMorphFrame
{
    internal VmdMorphFrame(string morphName, uint frameIndex, float weight)
    {
        MorphName = morphName;
        FrameIndex = frameIndex;
        Weight = weight;
    }

    internal string MorphName { get; }

    internal uint FrameIndex { get; }

    internal float Weight { get; }
}

internal readonly struct VmdIkFrame
{
    internal const string LeftFootIkName = "\u5de6\u8db3\uff29\uff2b";
    internal const string LeftToeIkName = "\u5de6\u3064\u307e\u5148\uff29\uff2b";
    internal const string RightFootIkName = "\u53f3\u8db3\uff29\uff2b";
    internal const string RightToeIkName = "\u53f3\u3064\u307e\u5148\uff29\uff2b";

    internal VmdIkFrame(
        uint frameIndex,
        bool leftFootEnabled,
        bool leftToeEnabled,
        bool rightFootEnabled,
        bool rightToeEnabled)
    {
        FrameIndex = frameIndex;
        LeftFootEnabled = leftFootEnabled;
        LeftToeEnabled = leftToeEnabled;
        RightFootEnabled = rightFootEnabled;
        RightToeEnabled = rightToeEnabled;
    }

    internal uint FrameIndex { get; }

    internal bool LeftFootEnabled { get; }

    internal bool LeftToeEnabled { get; }

    internal bool RightFootEnabled { get; }

    internal bool RightToeEnabled { get; }

    internal static VmdIkFrame Enabled(uint frameIndex)
    {
        return new VmdIkFrame(
            frameIndex,
            leftFootEnabled: true,
            leftToeEnabled: true,
            rightFootEnabled: true,
            rightToeEnabled: true);
    }

    internal bool GetEnabled(string ikName)
    {
        return ikName switch
        {
            LeftFootIkName => LeftFootEnabled,
            LeftToeIkName => LeftToeEnabled,
            RightFootIkName => RightFootEnabled,
            RightToeIkName => RightToeEnabled,
            _ => false
        };
    }
}
