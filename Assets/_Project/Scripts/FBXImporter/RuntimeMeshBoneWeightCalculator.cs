using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class RuntimeMeshBoneWeightCalculator
    {
        private const int MaximumBoneWeightCount = 4;

        internal static void Add(
            ref BoneWeight boneWeight,
            ref int count,
            int boneIndex,
            float weight)
        {
            if (weight <= 0f)
            {
                return;
            }

            if (count < MaximumBoneWeightCount)
            {
                switch (count)
                {
                    case 0:
                        boneWeight.boneIndex0 = boneIndex;
                        boneWeight.weight0 = weight;
                        break;
                    case 1:
                        boneWeight.boneIndex1 = boneIndex;
                        boneWeight.weight1 = weight;
                        break;
                    case 2:
                        boneWeight.boneIndex2 = boneIndex;
                        boneWeight.weight2 = weight;
                        break;
                    case 3:
                        boneWeight.boneIndex3 = boneIndex;
                        boneWeight.weight3 = weight;
                        break;
                }
            }

            count++;
        }

        internal static void Normalize(BoneWeight[] weights)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight weight = weights[i];
                float total = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                if (total <= 0f)
                {
                    continue;
                }

                weight.weight0 /= total;
                weight.weight1 /= total;
                weight.weight2 /= total;
                weight.weight3 /= total;
                weights[i] = weight;
            }
        }
    }
}
