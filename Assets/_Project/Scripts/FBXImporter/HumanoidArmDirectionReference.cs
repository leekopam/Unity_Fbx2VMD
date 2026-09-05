using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Humanoid 루트 공간에서 샘플링한 상완·전완 방향을 전달함.
    /// </summary>
    internal readonly struct HumanoidArmDirectionReference
    {
        internal HumanoidArmDirectionReference(
            Vector3 leftUpperArm,
            Vector3 leftForearm,
            Vector3 rightUpperArm,
            Vector3 rightForearm)
        {
            LeftUpperArm = leftUpperArm;
            LeftForearm = leftForearm;
            RightUpperArm = rightUpperArm;
            RightForearm = rightForearm;
        }

        internal Vector3 LeftUpperArm { get; }

        internal Vector3 LeftForearm { get; }

        internal Vector3 RightUpperArm { get; }

        internal Vector3 RightForearm { get; }
    }
}
