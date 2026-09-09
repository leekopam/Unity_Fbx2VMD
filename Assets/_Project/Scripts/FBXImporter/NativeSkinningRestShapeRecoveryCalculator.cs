using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct NativeSkinningMaterialStrainConstraint
    {
        internal NativeSkinningMaterialStrainConstraint(
            int[] localIndices,
            float[] coefficients,
            Vector3 projectedDelta,
            float weight)
        {
            LocalIndices = localIndices;
            Coefficients = coefficients;
            ProjectedDelta = projectedDelta;
            Weight = weight;
        }

        internal int[] LocalIndices { get; }

        internal float[] Coefficients { get; }

        internal Vector3 ProjectedDelta { get; }

        internal float Weight { get; }
    }

    /// <summary>
    /// 휴식 삼각형의 재질 좌표계를 기준으로 심한 스키닝 압축 복원 제약을 계산함.
    /// </summary>
    internal static class NativeSkinningRestShapeRecoveryCalculator
    {
        internal const float MinimumRestLengthRatio = 0.8f;
        internal const float MaximumRestStrainIncrease = 0.05f;
        private const float MinimumLength = 0.00000001f;

        internal static NativeSkinningMaterialStrainConstraint[] BuildConstraints(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<Vector3> currentVertices,
            IReadOnlyList<int> triangles,
            IEnumerable<int> faceIndices,
            IReadOnlyList<int> localIndexByVertex)
        {
            var constraints = new List<NativeSkinningMaterialStrainConstraint>();
            foreach (int faceIndex in faceIndices)
            {
                int offset = faceIndex * 3;
                int first = triangles[offset];
                int second = triangles[offset + 1];
                int third = triangles[offset + 2];
                OrderByLongestRestEdge(
                    restVertices,
                    ref first,
                    ref second,
                    ref third);
                int[] localIndices =
                {
                    localIndexByVertex[first],
                    localIndexByVertex[second],
                    localIndexByVertex[third]
                };
                if (localIndices.Any(localIndex => localIndex < 0) ||
                    !TryBuildDeformationGradient(
                        restVertices,
                        baselineVertices,
                        currentVertices,
                        first,
                        second,
                        third,
                        out DeformationGradient gradient))
                {
                    continue;
                }

                foreach (Vector2 materialDirection in
                         BuildMaterialDirections(gradient))
                {
                    Vector3 baselineDirection = CalculateDirectionalDeformation(
                        gradient.BaselineFirstColumn,
                        gradient.BaselineSecondColumn,
                        materialDirection);
                    Vector3 currentDirection = CalculateDirectionalDeformation(
                        gradient.CurrentFirstColumn,
                        gradient.CurrentSecondColumn,
                        materialDirection);
                    float currentStretch = currentDirection.magnitude;
                    if (currentStretch <= MinimumLength)
                    {
                        continue;
                    }

                    float targetStretch = ClampStretch(
                        currentStretch,
                        baselineDirection.magnitude);
                    if (Mathf.Approximately(currentStretch, targetStretch))
                    {
                        continue;
                    }

                    float firstEdgeCoefficient =
                        gradient.Inverse00 * materialDirection.x +
                        gradient.Inverse01 * materialDirection.y;
                    float secondEdgeCoefficient =
                        gradient.Inverse10 * materialDirection.x +
                        gradient.Inverse11 * materialDirection.y;
                    constraints.Add(new NativeSkinningMaterialStrainConstraint(
                        localIndices,
                        new[]
                        {
                            -firstEdgeCoefficient - secondEdgeCoefficient,
                            firstEdgeCoefficient,
                            secondEdgeCoefficient
                        },
                        currentDirection *
                        (targetStretch / currentStretch - 1f),
                        gradient.RestArea));
                }
            }
            return constraints.ToArray();
        }

        internal static float CalculateStrainIncrease(
            float baselineLength,
            float candidateLength,
            float restLength)
        {
            if (restLength <= MinimumLength)
            {
                return float.PositiveInfinity;
            }

            float baselineStrain = Mathf.Abs(baselineLength / restLength - 1f);
            float candidateStrain = Mathf.Abs(candidateLength / restLength - 1f);
            return candidateStrain - baselineStrain;
        }

        internal static float CalculateSquaredStrainEnergy(
            float length,
            float restLength)
        {
            if (restLength <= MinimumLength)
            {
                return float.PositiveInfinity;
            }

            float strain = length / restLength - 1f;
            return strain * strain;
        }

        private static void OrderByLongestRestEdge(
            IReadOnlyList<Vector3> restVertices,
            ref int first,
            ref int second,
            ref int third)
        {
            float firstSecondSquared =
                (restVertices[second] - restVertices[first]).sqrMagnitude;
            float secondThirdSquared =
                (restVertices[third] - restVertices[second]).sqrMagnitude;
            float thirdFirstSquared =
                (restVertices[first] - restVertices[third]).sqrMagnitude;
            if (secondThirdSquared > firstSecondSquared &&
                secondThirdSquared >= thirdFirstSquared)
            {
                int originalFirst = first;
                first = second;
                second = third;
                third = originalFirst;
            }
            else if (thirdFirstSquared > firstSecondSquared &&
                     thirdFirstSquared > secondThirdSquared)
            {
                int originalSecond = second;
                second = first;
                first = third;
                third = originalSecond;
            }
        }

        private static bool TryBuildDeformationGradient(
            IReadOnlyList<Vector3> restVertices,
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> currentVertices,
            int first,
            int second,
            int third,
            out DeformationGradient gradient)
        {
            Vector3 restFirstEdge = restVertices[second] - restVertices[first];
            Vector3 restSecondEdge = restVertices[third] - restVertices[first];
            Vector3 restNormal = Vector3.Cross(restFirstEdge, restSecondEdge);
            float restFirstLength = restFirstEdge.magnitude;
            float restDoubleArea = restNormal.magnitude;
            if (restFirstLength <= MinimumLength ||
                restDoubleArea <= MinimumLength)
            {
                gradient = default;
                return false;
            }

            Vector3 restAxisX = restFirstEdge / restFirstLength;
            Vector3 restAxisZ = restNormal / restDoubleArea;
            Vector3 restAxisY = Vector3.Cross(restAxisZ, restAxisX);
            float restSecondX = Vector3.Dot(restSecondEdge, restAxisX);
            float restSecondY = Vector3.Dot(restSecondEdge, restAxisY);
            float determinant = restFirstLength * restSecondY;
            if (Mathf.Abs(determinant) <= MinimumLength)
            {
                gradient = default;
                return false;
            }

            float inverse00 = restSecondY / determinant;
            float inverse01 = -restSecondX / determinant;
            const float inverse10 = 0f;
            float inverse11 = restFirstLength / determinant;
            BuildGradientColumns(
                baselineVertices,
                first,
                second,
                third,
                inverse00,
                inverse01,
                inverse10,
                inverse11,
                out Vector3 baselineFirstColumn,
                out Vector3 baselineSecondColumn);
            BuildGradientColumns(
                currentVertices,
                first,
                second,
                third,
                inverse00,
                inverse01,
                inverse10,
                inverse11,
                out Vector3 currentFirstColumn,
                out Vector3 currentSecondColumn);
            gradient = new DeformationGradient(
                inverse00,
                inverse01,
                inverse10,
                inverse11,
                restDoubleArea * 0.5f,
                restFirstLength,
                new Vector2(restSecondX, restSecondY),
                baselineFirstColumn,
                baselineSecondColumn,
                currentFirstColumn,
                currentSecondColumn);
            return true;
        }

        private static void BuildGradientColumns(
            IReadOnlyList<Vector3> vertices,
            int first,
            int second,
            int third,
            float inverse00,
            float inverse01,
            float inverse10,
            float inverse11,
            out Vector3 firstColumn,
            out Vector3 secondColumn)
        {
            Vector3 firstEdge = vertices[second] - vertices[first];
            Vector3 secondEdge = vertices[third] - vertices[first];
            firstColumn = firstEdge * inverse00 + secondEdge * inverse10;
            secondColumn = firstEdge * inverse01 + secondEdge * inverse11;
        }

        private static Vector2[] BuildMaterialDirections(
            DeformationGradient gradient)
        {
            return new[]
            {
                Vector2.right,
                gradient.RestSecondCoordinates.normalized,
                (gradient.RestSecondCoordinates -
                 new Vector2(gradient.RestFirstLength, 0f)).normalized
            };
        }

        private static Vector3 CalculateDirectionalDeformation(
            Vector3 firstColumn,
            Vector3 secondColumn,
            Vector2 direction)
        {
            return firstColumn * direction.x + secondColumn * direction.y;
        }

        private static float ClampStretch(
            float currentStretch,
            float baselineStretch)
        {
            float maximumAbsoluteStrain =
                Mathf.Abs(baselineStretch - 1f) + MaximumRestStrainIncrease;
            float minimumStretch = Mathf.Max(
                MinimumRestLengthRatio,
                1f - maximumAbsoluteStrain);
            float maximumStretch = 1f + maximumAbsoluteStrain;
            return Mathf.Clamp(currentStretch, minimumStretch, maximumStretch);
        }

        private readonly struct DeformationGradient
        {
            internal DeformationGradient(
                float inverse00,
                float inverse01,
                float inverse10,
                float inverse11,
                float restArea,
                float restFirstLength,
                Vector2 restSecondCoordinates,
                Vector3 baselineFirstColumn,
                Vector3 baselineSecondColumn,
                Vector3 currentFirstColumn,
                Vector3 currentSecondColumn)
            {
                Inverse00 = inverse00;
                Inverse01 = inverse01;
                Inverse10 = inverse10;
                Inverse11 = inverse11;
                RestArea = restArea;
                RestFirstLength = restFirstLength;
                RestSecondCoordinates = restSecondCoordinates;
                BaselineFirstColumn = baselineFirstColumn;
                BaselineSecondColumn = baselineSecondColumn;
                CurrentFirstColumn = currentFirstColumn;
                CurrentSecondColumn = currentSecondColumn;
            }

            internal float Inverse00 { get; }
            internal float Inverse01 { get; }
            internal float Inverse10 { get; }
            internal float Inverse11 { get; }
            internal float RestArea { get; }
            internal float RestFirstLength { get; }
            internal Vector2 RestSecondCoordinates { get; }
            internal Vector3 BaselineFirstColumn { get; }
            internal Vector3 BaselineSecondColumn { get; }
            internal Vector3 CurrentFirstColumn { get; }
            internal Vector3 CurrentSecondColumn { get; }
        }
    }
}
