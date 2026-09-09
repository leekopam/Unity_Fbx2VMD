using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 급접힘과 휴식 형상 붕괴를 교대 투영으로 함께 완화함.
    /// </summary>
    internal static class NativeSkinningCoupledDihedralProjectionCalculator
    {
        private const int ProjectionIterations = 128;
        private const float ProjectionStiffness = 1f;
        private const float RestLengthRatioSafetyMargin = 0.0001f;
        private const float RestStrainSafetyMargin = 0.0001f;
        private const float MinimumSignedAreaRatio = 0.05f;
        private const float MinimumEdgeLength = 0.00000001f;
        private const float MinimumNormalSquaredMagnitude = 0.0000000000000001f;

        internal static bool TryProject(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> initialVertices,
            NativeSkinningSurfaceContract contract,
            IReadOnlyList<int> restShapeRecoveryPairIndices,
            IReadOnlyList<int> sharpFoldPairIndices,
            float maximumRestSmoothAngleDegrees,
            float sharpFoldActivationAngleDegrees,
            float sharpFoldTargetAngleDegrees,
            out Vector3[] projectedVertices)
        {
            projectedVertices = null;
            if (baselineVertices == null ||
                initialVertices == null ||
                contract == null ||
                baselineVertices.Count != contract.VertexCount ||
                initialVertices.Count != contract.VertexCount ||
                restShapeRecoveryPairIndices == null ||
                restShapeRecoveryPairIndices.Count == 0 ||
                sharpFoldPairIndices == null)
            {
                return false;
            }

            projectedVertices = initialVertices.ToArray();
            int[] localPairIndices = BuildLocalPairIndices(
                contract,
                restShapeRecoveryPairIndices.Concat(sharpFoldPairIndices));
            int[] localFaceIndices = BuildLocalFaceIndices(
                contract,
                localPairIndices);
            var activeSharpFoldPairIndices = new HashSet<int>(
                sharpFoldPairIndices);
            RestStrainEdge[] restStrainEdges = BuildRestStrainEdges(
                baselineVertices,
                contract,
                localFaceIndices);
            for (int iteration = 0;
                 iteration < ProjectionIterations;
                 iteration++)
            {
                bool projectedConstraint = false;
                if (!TryProjectCollapsedEdges(
                        projectedVertices,
                        contract,
                        restShapeRecoveryPairIndices,
                        ref projectedConstraint))
                {
                    projectedVertices = null;
                    return false;
                }
                ActivateSharpFoldPairs(
                    projectedVertices,
                    contract,
                    localPairIndices,
                    maximumRestSmoothAngleDegrees,
                    sharpFoldActivationAngleDegrees,
                    activeSharpFoldPairIndices);
                if (!TryProjectPairs(
                        projectedVertices,
                        contract,
                        activeSharpFoldPairIndices,
                        _ => sharpFoldTargetAngleDegrees,
                        ref projectedConstraint) ||
                    !TryProjectRestStrainEdges(
                        projectedVertices,
                        restStrainEdges,
                        ref projectedConstraint) ||
                    !TryProjectFaceOrientations(
                        baselineVertices,
                        projectedVertices,
                        contract,
                        localFaceIndices,
                        ref projectedConstraint))
                {
                    projectedVertices = null;
                    return false;
                }
                if (!projectedConstraint)
                {
                    break;
                }
            }
            return projectedVertices.All(IsFinite);
        }

        private static int[] BuildLocalFaceIndices(
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> constrainedPairIndices)
        {
            return constrainedPairIndices
                .SelectMany(pairIndex =>
                {
                    NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                    return new[]
                    {
                        pair.FirstOpposite,
                        pair.SecondOpposite,
                        pair.FirstEdge,
                        pair.SecondEdge
                    };
                })
                .Distinct()
                .SelectMany(vertexIndex =>
                    contract.AffectedFaceIndicesByVertex[vertexIndex])
                .Distinct()
                .OrderBy(faceIndex => faceIndex)
                .ToArray();
        }

        private static int[] BuildLocalPairIndices(
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> seedPairIndices)
        {
            return seedPairIndices
                .SelectMany(pairIndex =>
                {
                    NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                    return new[]
                    {
                        pair.FirstOpposite,
                        pair.SecondOpposite,
                        pair.FirstEdge,
                        pair.SecondEdge
                    };
                })
                .Distinct()
                .SelectMany(vertexIndex =>
                    contract.FacePairIndicesByVertex[vertexIndex])
                .Distinct()
                .OrderBy(pairIndex => pairIndex)
                .ToArray();
        }

        private static void ActivateSharpFoldPairs(
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> localPairIndices,
            float maximumRestSmoothAngleDegrees,
            float sharpFoldActivationAngleDegrees,
            ISet<int> activePairIndices)
        {
            foreach (int pairIndex in localPairIndices)
            {
                if (contract.RestAnglesDegrees[pairIndex] >
                        maximumRestSmoothAngleDegrees ||
                    !NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        contract.FacePairs[pairIndex],
                        out float angleDegrees))
                {
                    continue;
                }
                if (angleDegrees > sharpFoldActivationAngleDegrees)
                {
                    activePairIndices.Add(pairIndex);
                }
            }
        }

        private static RestStrainEdge[] BuildRestStrainEdges(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> constrainedFaceIndices)
        {
            return constrainedFaceIndices
                .SelectMany(faceIndex =>
                {
                    int offset = faceIndex * 3;
                    int first = contract.Triangles[offset];
                    int second = contract.Triangles[offset + 1];
                    int third = contract.Triangles[offset + 2];
                    return new[]
                    {
                        new NativeSkinningEdgeKey(first, second),
                        new NativeSkinningEdgeKey(second, third),
                        new NativeSkinningEdgeKey(third, first)
                    };
                })
                .Distinct()
                .Select(edge => new RestStrainEdge(
                    edge.First,
                    edge.Second,
                    Vector3.Distance(
                        baselineVertices[edge.First],
                        baselineVertices[edge.Second]),
                    Vector3.Distance(
                        contract.RestVertices[edge.First],
                        contract.RestVertices[edge.Second])))
                .Where(edge => edge.RestLength > MinimumEdgeLength)
                .ToArray();
        }

        private static bool TryProjectFaceOrientations(
            IReadOnlyList<Vector3> baselineVertices,
            IList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> faceIndices,
            ref bool projectedConstraint)
        {
            foreach (int faceIndex in faceIndices)
            {
                int offset = faceIndex * 3;
                int firstIndex = contract.Triangles[offset];
                int secondIndex = contract.Triangles[offset + 1];
                int thirdIndex = contract.Triangles[offset + 2];
                Vector3 baselineNormal = Vector3.Cross(
                    baselineVertices[secondIndex] - baselineVertices[firstIndex],
                    baselineVertices[thirdIndex] - baselineVertices[firstIndex]);
                float baselineArea = baselineNormal.magnitude;
                if (baselineNormal.sqrMagnitude < MinimumNormalSquaredMagnitude)
                {
                    continue;
                }

                baselineNormal /= baselineArea;
                Vector3 first = vertices[firstIndex];
                Vector3 second = vertices[secondIndex];
                Vector3 third = vertices[thirdIndex];
                float signedArea = Vector3.Dot(
                    Vector3.Cross(second - first, third - first),
                    baselineNormal);
                float minimumSignedArea = baselineArea * MinimumSignedAreaRatio;
                if (signedArea >= minimumSignedArea)
                {
                    continue;
                }

                Vector3 firstGradient = Vector3.Cross(
                    second - third,
                    baselineNormal);
                Vector3 secondGradient = Vector3.Cross(
                    third - first,
                    baselineNormal);
                Vector3 thirdGradient = Vector3.Cross(
                    baselineNormal,
                    second - first);
                float squaredGradientMagnitude =
                    firstGradient.sqrMagnitude +
                    secondGradient.sqrMagnitude +
                    thirdGradient.sqrMagnitude;
                if (squaredGradientMagnitude < MinimumNormalSquaredMagnitude)
                {
                    return false;
                }

                float correctionScale =
                    (minimumSignedArea - signedArea) /
                    squaredGradientMagnitude;
                vertices[firstIndex] += firstGradient * correctionScale;
                vertices[secondIndex] += secondGradient * correctionScale;
                vertices[thirdIndex] += thirdGradient * correctionScale;
                projectedConstraint = true;
            }
            return true;
        }

        private static bool TryProjectRestStrainEdges(
            IList<Vector3> vertices,
            IEnumerable<RestStrainEdge> edges,
            ref bool projectedConstraint)
        {
            foreach (RestStrainEdge edge in edges)
            {
                float maximumAbsoluteStrain = Mathf.Abs(
                    edge.BaselineLength / edge.RestLength - 1f) +
                    NativeSkinningRestShapeRecoveryCalculator
                        .MaximumRestStrainIncrease -
                    RestStrainSafetyMargin;
                float minimumLength = Mathf.Max(
                    0f,
                    edge.RestLength * (1f - maximumAbsoluteStrain));
                float maximumLength = edge.RestLength *
                    (1f + maximumAbsoluteStrain);
                float currentLength = Vector3.Distance(
                    vertices[edge.FirstVertex],
                    vertices[edge.SecondVertex]);
                if (currentLength < minimumLength)
                {
                    if (!TryProjectEdgeToLength(
                            vertices,
                            edge.FirstVertex,
                            edge.SecondVertex,
                            minimumLength,
                            ref projectedConstraint))
                    {
                        return false;
                    }
                }
                else if (currentLength > maximumLength &&
                         !TryProjectEdgeToLength(
                             vertices,
                             edge.FirstVertex,
                             edge.SecondVertex,
                             maximumLength,
                             ref projectedConstraint))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryProjectCollapsedEdges(
            Vector3[] vertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> pairIndices,
            ref bool projectedConstraint)
        {
            float minimumRestLengthRatio =
                NativeSkinningRestShapeCollapseDetector
                    .CalculateMinimumNonSevereRestLengthRatio() +
                RestLengthRatioSafetyMargin;
            foreach (int pairIndex in pairIndices)
            {
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                NativeSkinningFacePairRestLengths restLengths =
                    contract.FacePairRestLengths[pairIndex];
                if (!TryProjectEdge(
                        vertices,
                        pair.FirstEdge,
                        pair.SecondEdge,
                        restLengths.SharedEdge,
                        minimumRestLengthRatio,
                        ref projectedConstraint) ||
                    !TryProjectEdge(
                        vertices,
                        pair.FirstOpposite,
                        pair.FirstEdge,
                        restLengths.FirstOppositeToFirstEdge,
                        minimumRestLengthRatio,
                        ref projectedConstraint) ||
                    !TryProjectEdge(
                        vertices,
                        pair.FirstOpposite,
                        pair.SecondEdge,
                        restLengths.FirstOppositeToSecondEdge,
                        minimumRestLengthRatio,
                        ref projectedConstraint) ||
                    !TryProjectEdge(
                        vertices,
                        pair.SecondOpposite,
                        pair.FirstEdge,
                        restLengths.SecondOppositeToFirstEdge,
                        minimumRestLengthRatio,
                        ref projectedConstraint) ||
                    !TryProjectEdge(
                        vertices,
                        pair.SecondOpposite,
                        pair.SecondEdge,
                        restLengths.SecondOppositeToSecondEdge,
                        minimumRestLengthRatio,
                        ref projectedConstraint))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryProjectEdge(
            IList<Vector3> vertices,
            int firstVertex,
            int secondVertex,
            float restLength,
            float minimumRestLengthRatio,
            ref bool projectedConstraint)
        {
            float minimumLength = restLength * minimumRestLengthRatio;
            if (Vector3.Distance(vertices[firstVertex], vertices[secondVertex]) >=
                minimumLength)
            {
                return true;
            }
            return TryProjectEdgeToLength(
                vertices,
                firstVertex,
                secondVertex,
                minimumLength,
                ref projectedConstraint);
        }

        private static bool TryProjectEdgeToLength(
            IList<Vector3> vertices,
            int firstVertex,
            int secondVertex,
            float targetLength,
            ref bool projectedConstraint)
        {
            Vector3 edge = vertices[secondVertex] - vertices[firstVertex];
            float currentLength = edge.magnitude;
            if (Mathf.Approximately(currentLength, targetLength))
            {
                return true;
            }
            if (targetLength < 0f || currentLength <= MinimumEdgeLength)
            {
                return false;
            }

            Vector3 correction = edge *
                ((targetLength / currentLength - 1f) * 0.5f);
            vertices[firstVertex] -= correction;
            vertices[secondVertex] += correction;
            projectedConstraint = true;
            return true;
        }

        private static bool TryProjectPairs(
            Vector3[] vertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> pairIndices,
            System.Func<int, float> resolveTargetAngleDegrees,
            ref bool projectedConstraint)
        {
            foreach (int pairIndex in pairIndices)
            {
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                float targetAngleDegrees = resolveTargetAngleDegrees(pairIndex);
                if (!NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        pair,
                        out float angleDegrees))
                {
                    return false;
                }
                if (angleDegrees <= targetAngleDegrees)
                {
                    continue;
                }
                if (!MeshDihedralConstraintCalculator.TryCalculateCorrections(
                        vertices[pair.FirstOpposite],
                        vertices[pair.SecondOpposite],
                        vertices[pair.FirstEdge],
                        vertices[pair.SecondEdge],
                        targetAngleDegrees,
                        ProjectionStiffness,
                        out Vector3 firstOppositeCorrection,
                        out Vector3 secondOppositeCorrection,
                        out Vector3 firstEdgeCorrection,
                        out Vector3 secondEdgeCorrection))
                {
                    return false;
                }

                vertices[pair.FirstOpposite] += firstOppositeCorrection;
                vertices[pair.SecondOpposite] += secondOppositeCorrection;
                vertices[pair.FirstEdge] += firstEdgeCorrection;
                vertices[pair.SecondEdge] += secondEdgeCorrection;
                projectedConstraint = true;
            }
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct RestStrainEdge
        {
            internal RestStrainEdge(
                int firstVertex,
                int secondVertex,
                float baselineLength,
                float restLength)
            {
                FirstVertex = firstVertex;
                SecondVertex = secondVertex;
                BaselineLength = baselineLength;
                RestLength = restLength;
            }

            internal int FirstVertex { get; }

            internal int SecondVertex { get; }

            internal float BaselineLength { get; }

            internal float RestLength { get; }
        }
    }
}
