using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// BakeMesh와 동일한 선형 블렌드 스키닝을 순수 수학으로 계산함.
    /// Unity API를 쓰지 않으므로 워커 스레드에서 호출 가능하다.
    /// </summary>
    internal static class NativeSkinningLinearBlendCalculator
    {
        // 정점별 가중치 합산 후 최대 오차 — 캘리브레이션 판정용.
        internal const float BaselineMaxDeviationMeters = 0.0001f;

        // v' = Σ w_k · (M_{b_k} · v_rest) — 렌더러 로컬 공간의 스키닝 행렬로 계산한다.
        internal static bool TryComputeBaseline(
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<BoneWeight> boneWeights,
            IReadOnlyList<Matrix4x4> skinningMatrices,
            Vector3[] output)
        {
            if (restVertices == null ||
                boneWeights == null ||
                skinningMatrices == null ||
                output == null ||
                output.Length != restVertices.Count ||
                boneWeights.Count != restVertices.Count)
            {
                return false;
            }

            for (int vertexIndex = 0;
                 vertexIndex < restVertices.Count;
                 vertexIndex++)
            {
                Vector3 rest = restVertices[vertexIndex];
                BoneWeight weights = boneWeights[vertexIndex];
                Vector3 sum = Vector3.zero;
                if (!Accumulate(
                        rest,
                        weights.boneIndex0,
                        weights.weight0,
                        skinningMatrices,
                        ref sum) ||
                    !Accumulate(
                        rest,
                        weights.boneIndex1,
                        weights.weight1,
                        skinningMatrices,
                        ref sum) ||
                    !Accumulate(
                        rest,
                        weights.boneIndex2,
                        weights.weight2,
                        skinningMatrices,
                        ref sum) ||
                    !Accumulate(
                        rest,
                        weights.boneIndex3,
                        weights.weight3,
                        skinningMatrices,
                        ref sum))
                {
                    return false;
                }
                output[vertexIndex] = sum;
            }
            return true;
        }

        // 두 정점 배열의 최대 유클리드 편차 — 캘리브레이션 비교에 쓴다.
        internal static float MaxDeviation(
            IReadOnlyList<Vector3> first,
            IReadOnlyList<Vector3> second)
        {
            if (first == null || second == null || first.Count != second.Count)
            {
                return float.MaxValue;
            }
            float maximum = 0f;
            for (int index = 0; index < first.Count; index++)
            {
                float deviation = Vector3.Distance(first[index], second[index]);
                if (deviation > maximum)
                {
                    maximum = deviation;
                }
            }
            return maximum;
        }

        private static bool Accumulate(
            Vector3 rest,
            int boneIndex,
            float weight,
            IReadOnlyList<Matrix4x4> skinningMatrices,
            ref Vector3 sum)
        {
            if (weight <= 0f)
            {
                return true;
            }
            if (boneIndex < 0 || boneIndex >= skinningMatrices.Count)
            {
                return false;
            }
            sum += skinningMatrices[boneIndex].MultiplyPoint3x4(rest) * weight;
            return IsFinite(sum);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
