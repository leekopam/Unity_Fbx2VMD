using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class NativeSkinningDualQuaternionBlendResult
    {
        internal NativeSkinningDualQuaternionBlendResult(
            IReadOnlyList<Vector3> blendedVertices,
            int[] changedVertexIndices,
            float blend,
            float maximumVertexDisplacement,
            float maximumRestEdgeStrainIncrease,
            int newDegenerateFaceCount,
            int reversedFaceCount)
        {
            BlendedVertices = blendedVertices;
            ChangedVertexIndices = changedVertexIndices;
            Blend = blend;
            MaximumVertexDisplacement = maximumVertexDisplacement;
            MaximumRestEdgeStrainIncrease = maximumRestEdgeStrainIncrease;
            NewDegenerateFaceCount = newDegenerateFaceCount;
            ReversedFaceCount = reversedFaceCount;
        }

        internal IReadOnlyList<Vector3> BlendedVertices { get; }

        internal int[] ChangedVertexIndices { get; }

        internal float Blend { get; }

        internal float MaximumVertexDisplacement { get; }

        internal float MaximumRestEdgeStrainIncrease { get; }

        internal int NewDegenerateFaceCount { get; }

        internal int ReversedFaceCount { get; }
    }

    /// <summary>
    /// 혼합 가중치 경계의 체적 손실만 제한적 이중 쿼터니언 혼합으로 완화함.
    /// </summary>
    internal static class NativeSkinningDualQuaternionBlendCalculator
    {
        private const float MaximumBlend = 0.3f;
        private const float MinimumAcceptedBlend = 0.025f;
        private const int BlendSearchStepCount = 5;
        private const float MinimumStoredCorrectionSquaredMagnitude =
            0.0000000000000001f;

        internal static bool TryCalculate(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<BoneWeight> boneWeights,
            IReadOnlyList<Matrix4x4> skinningMatrices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningDualQuaternionBlendResult result)
        {
            result = null;
            if (!NativeSkinningDualQuaternionBlendContractBuilder.TryBuild(
                    boneWeights,
                    contract,
                    out NativeSkinningDualQuaternionBlendContract blendContract))
            {
                return false;
            }
            return TryCalculatePrepared(
                baselineVertices,
                restVertices,
                skinningMatrices,
                blendContract,
                out result);
        }

        internal static bool TryCalculatePrepared(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<Matrix4x4> skinningMatrices,
            NativeSkinningDualQuaternionBlendContract blendContract,
            out NativeSkinningDualQuaternionBlendResult result)
        {
            result = null;
            if (!HasValidInput(
                    baselineVertices,
                    restVertices,
                    skinningMatrices,
                    blendContract))
            {
                return false;
            }

            if (!NativeSkinningDualQuaternionVertexCalculator.TryBuildTransforms(
                    skinningMatrices,
                    out NativeSkinningDualQuaternionTransform[] boneTransforms))
            {
                return false;
            }

            return TryCalculateWithPreparedTransforms(
                baselineVertices,
                restVertices,
                boneTransforms,
                blendContract,
                out result);
        }

        internal static bool TryCalculateWithPreparedTransforms(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<NativeSkinningDualQuaternionTransform> boneTransforms,
            NativeSkinningDualQuaternionBlendContract blendContract,
            out NativeSkinningDualQuaternionBlendResult result)
        {
            result = null;
            if (!HasValidPreparedInput(
                    baselineVertices,
                    restVertices,
                    boneTransforms,
                    blendContract))
            {
                return false;
            }

            var candidateVertexIndices = new List<int>();
            var candidateOffsets = new List<Vector3>();
            foreach (NativeSkinningDualQuaternionBlendVertex blendVertex in
                     blendContract.Vertices)
            {
                int vertexIndex = blendVertex.VertexIndex;
                if (!NativeSkinningDualQuaternionVertexCalculator.TryTransformVertex(
                        restVertices[vertexIndex],
                        blendVertex.BoneWeight,
                        boneTransforms,
                        out Vector3 dualQuaternionVertex))
                {
                    continue;
                }

                Vector3 offset =
                    (dualQuaternionVertex - baselineVertices[vertexIndex]) *
                    blendVertex.TransitionInfluence;
                if ((offset * MaximumBlend).sqrMagnitude <=
                    MinimumStoredCorrectionSquaredMagnitude)
                {
                    continue;
                }
                candidateVertexIndices.Add(vertexIndex);
                candidateOffsets.Add(offset);
            }

            if (candidateVertexIndices.Count == 0)
            {
                result = CreateNoCorrectionResult(baselineVertices);
                return true;
            }

            Vector3[] candidateVertices = baselineVertices.ToArray();
            var changedVertexIndices = new List<int>(candidateVertexIndices.Count);
            if (!NativeSkinningDualQuaternionQualityEvaluator.TryCreate(
                    baselineVertices,
                    restVertices,
                    blendContract,
                    out NativeSkinningDualQuaternionQualityEvaluator qualityEvaluator))
            {
                return false;
            }
            float blend = MaximumBlend;
            for (int step = 0; step < BlendSearchStepCount; step++)
            {
                if (blend < MinimumAcceptedBlend)
                {
                    break;
                }

                changedVertexIndices.Clear();
                for (int candidateIndex = 0;
                     candidateIndex < candidateVertexIndices.Count;
                     candidateIndex++)
                {
                    int vertexIndex = candidateVertexIndices[candidateIndex];
                    Vector3 candidate = baselineVertices[vertexIndex] +
                        candidateOffsets[candidateIndex] * blend;
                    if (!IsFinite(candidate))
                    {
                        return false;
                    }
                    candidateVertices[vertexIndex] = candidate;
                    if ((candidate - baselineVertices[vertexIndex]).sqrMagnitude >
                        MinimumStoredCorrectionSquaredMagnitude)
                    {
                        changedVertexIndices.Add(vertexIndex);
                    }
                }

                qualityEvaluator.Evaluate(
                    candidateVertices,
                    out float maximumRestEdgeStrainIncrease,
                    out int newDegenerateFaceCount,
                    out int reversedFaceCount);
                if (newDegenerateFaceCount == 0 &&
                    reversedFaceCount == 0 &&
                    maximumRestEdgeStrainIncrease <=
                        NativeSkinningRestShapeRecoveryCalculator
                            .MaximumRestStrainIncrease)
                {
                    float maximumVertexDisplacement = changedVertexIndices.Count == 0
                        ? 0f
                        : changedVertexIndices.Max(vertexIndex => Vector3.Distance(
                            baselineVertices[vertexIndex],
                            candidateVertices[vertexIndex]));
                    result = new NativeSkinningDualQuaternionBlendResult(
                        candidateVertices,
                        changedVertexIndices.ToArray(),
                        blend,
                        maximumVertexDisplacement,
                        maximumRestEdgeStrainIncrease,
                        newDegenerateFaceCount,
                        reversedFaceCount);
                    return true;
                }

                blend *= 0.5f;
            }

            result = CreateNoCorrectionResult(baselineVertices);
            return true;
        }

        private static bool HasValidPreparedInput(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<NativeSkinningDualQuaternionTransform> boneTransforms,
            NativeSkinningDualQuaternionBlendContract blendContract)
        {
            NativeSkinningSurfaceContract contract = blendContract?.SurfaceContract;
            return baselineVertices != null &&
                   restVertices != null &&
                   boneTransforms != null &&
                   contract != null &&
                   baselineVertices.Count == contract.VertexCount &&
                   restVertices.Count == contract.VertexCount &&
                   blendContract.Vertices.Length > 0 &&
                   contract.LowerArmBoneIndex >= 0 &&
                   contract.LowerArmBoneIndex < boneTransforms.Count;
        }

        private static NativeSkinningDualQuaternionBlendResult
            CreateNoCorrectionResult(IReadOnlyList<Vector3> baselineVertices)
        {
            return new NativeSkinningDualQuaternionBlendResult(
                baselineVertices,
                System.Array.Empty<int>(),
                0f,
                0f,
                0f,
                0,
                0);
        }

        private static bool HasValidInput(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<Matrix4x4> skinningMatrices,
            NativeSkinningDualQuaternionBlendContract blendContract)
        {
            NativeSkinningSurfaceContract contract = blendContract?.SurfaceContract;
            return baselineVertices != null &&
                   restVertices != null &&
                   skinningMatrices != null &&
                   contract != null &&
                   baselineVertices.Count == contract.VertexCount &&
                   restVertices.Count == contract.VertexCount &&
                   blendContract.Vertices.Length > 0 &&
                   contract.LowerArmBoneIndex >= 0 &&
                   contract.LowerArmBoneIndex < skinningMatrices.Count &&
                   NativeSkinningSurfaceCorrectionCalculator.AreVerticesFinite(
                       baselineVertices) &&
                   NativeSkinningSurfaceCorrectionCalculator.AreVerticesFinite(
                       restVertices);
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
}
