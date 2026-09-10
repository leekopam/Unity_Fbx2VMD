using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct NativeSkinningDualQuaternionBlendVertex
    {
        internal NativeSkinningDualQuaternionBlendVertex(
            int vertexIndex,
            BoneWeight boneWeight,
            float transitionInfluence)
        {
            VertexIndex = vertexIndex;
            BoneWeight = boneWeight;
            TransitionInfluence = transitionInfluence;
        }

        internal int VertexIndex { get; }
        internal BoneWeight BoneWeight { get; }
        internal float TransitionInfluence { get; }
    }

    /// <summary>
    /// 프레임마다 변하지 않는 체적 혼합 정점과 품질 검사 토폴로지를 보관함.
    /// </summary>
    internal sealed class NativeSkinningDualQuaternionBlendContract
    {
        internal NativeSkinningDualQuaternionBlendContract(
            NativeSkinningSurfaceContract surfaceContract,
            NativeSkinningDualQuaternionBlendVertex[] vertices,
            int[] qualityFaceIndices,
            NativeSkinningEdgeKey[] qualityEdges)
        {
            SurfaceContract = surfaceContract;
            Vertices = vertices;
            QualityFaceIndices = qualityFaceIndices;
            QualityEdges = qualityEdges;
        }

        internal NativeSkinningSurfaceContract SurfaceContract { get; }
        internal NativeSkinningDualQuaternionBlendVertex[] Vertices { get; }
        internal int[] QualityFaceIndices { get; }
        internal NativeSkinningEdgeKey[] QualityEdges { get; }
    }

    internal static class NativeSkinningDualQuaternionBlendContractBuilder
    {
        private const float MinimumWeight = 0.000001f;

        internal static bool TryBuild(
            IReadOnlyList<BoneWeight> boneWeights,
            NativeSkinningSurfaceContract surfaceContract,
            out NativeSkinningDualQuaternionBlendContract blendContract)
        {
            blendContract = null;
            if (boneWeights == null ||
                surfaceContract == null ||
                boneWeights.Count != surfaceContract.VertexCount ||
                surfaceContract.LowerArmBoneIndex < 0)
            {
                return false;
            }

            NativeSkinningDualQuaternionBlendVertex[] vertices =
                surfaceContract.EvaluatedVertexIndices
                    .Select(vertexIndex => new NativeSkinningDualQuaternionBlendVertex(
                        vertexIndex,
                        boneWeights[vertexIndex],
                        CalculateTransitionInfluence(
                            boneWeights[vertexIndex],
                            surfaceContract.LowerArmBoneIndex)))
                    .Where(vertex => vertex.TransitionInfluence > MinimumWeight)
                    .ToArray();
            if (vertices.Length == 0)
            {
                return false;
            }

            var vertexSet = new HashSet<int>(
                vertices.Select(vertex => vertex.VertexIndex));
            int[] qualityFaceIndices = surfaceContract.AffectedFaceIndices
                .Where(faceIndex => IsConnectedToCandidate(
                    surfaceContract,
                    faceIndex,
                    vertexSet))
                .ToArray();
            NativeSkinningEdgeKey[] qualityEdges = qualityFaceIndices
                .SelectMany(faceIndex => CreateFaceEdges(
                    surfaceContract,
                    faceIndex))
                .Distinct()
                .OrderBy(edge => edge.First)
                .ThenBy(edge => edge.Second)
                .ToArray();
            if (qualityFaceIndices.Length == 0 || qualityEdges.Length == 0)
            {
                return false;
            }

            blendContract = new NativeSkinningDualQuaternionBlendContract(
                surfaceContract,
                vertices,
                qualityFaceIndices,
                qualityEdges);
            return true;
        }

        private static float CalculateTransitionInfluence(
            BoneWeight weights,
            int lowerArmBoneIndex)
        {
            float lowerArmWeight = FindWeight(weights, lowerArmBoneIndex);
            float totalWeight = weights.weight0 +
                weights.weight1 +
                weights.weight2 +
                weights.weight3;
            float otherWeight = Mathf.Max(0f, totalWeight - lowerArmWeight);
            if (lowerArmWeight <= MinimumWeight ||
                otherWeight <= MinimumWeight ||
                totalWeight <= MinimumWeight)
            {
                return 0f;
            }

            float normalizedProduct = 4f * lowerArmWeight * otherWeight /
                (totalWeight * totalWeight);
            return Mathf.Clamp01(normalizedProduct) * Mathf.Clamp01(totalWeight);
        }

        private static float FindWeight(BoneWeight weights, int boneIndex)
        {
            float total = 0f;
            AddWeight(ref total, weights.boneIndex0, weights.weight0, boneIndex);
            AddWeight(ref total, weights.boneIndex1, weights.weight1, boneIndex);
            AddWeight(ref total, weights.boneIndex2, weights.weight2, boneIndex);
            AddWeight(ref total, weights.boneIndex3, weights.weight3, boneIndex);
            return total;
        }

        private static void AddWeight(
            ref float total,
            int candidateBoneIndex,
            float weight,
            int targetBoneIndex)
        {
            if (candidateBoneIndex == targetBoneIndex && weight > 0f)
            {
                total += weight;
            }
        }

        private static bool IsConnectedToCandidate(
            NativeSkinningSurfaceContract contract,
            int faceIndex,
            ISet<int> candidateVertices)
        {
            int offset = faceIndex * 3;
            return candidateVertices.Contains(contract.Triangles[offset]) ||
                candidateVertices.Contains(contract.Triangles[offset + 1]) ||
                candidateVertices.Contains(contract.Triangles[offset + 2]);
        }

        private static IEnumerable<NativeSkinningEdgeKey> CreateFaceEdges(
            NativeSkinningSurfaceContract contract,
            int faceIndex)
        {
            int offset = faceIndex * 3;
            int first = contract.Triangles[offset];
            int second = contract.Triangles[offset + 1];
            int third = contract.Triangles[offset + 2];
            yield return new NativeSkinningEdgeKey(first, second);
            yield return new NativeSkinningEdgeKey(second, third);
            yield return new NativeSkinningEdgeKey(third, first);
        }
    }
}
