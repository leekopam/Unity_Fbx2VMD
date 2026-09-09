using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class NativeSkinningSurfaceCorrectionResult
    {
        internal NativeSkinningSurfaceCorrectionResult(
            IReadOnlyList<Vector3> correctedVertices,
            int[] correctedVertexIndices,
            int initialSharpFoldCount,
            int residualSharpFoldCount,
            int newSharpFoldCount,
            int newDegenerateFaceCount,
            int reversedFaceCount,
            float maximumVertexDisplacement,
            float maximumEdgeLengthStrain,
            bool usedRestShapeRecovery,
            float maximumRestEdgeStrainIncrease,
            float minimumRestShapeAngleImprovementDegrees,
            float minimumRestEdgeLengthRatioBeforeRecovery,
            float minimumRestEdgeLengthRatioAfterRecovery,
            int unresolvedRestShapeRecoveryCount)
        {
            CorrectedVertices = correctedVertices;
            CorrectedVertexIndices = correctedVertexIndices;
            InitialSharpFoldCount = initialSharpFoldCount;
            ResidualSharpFoldCount = residualSharpFoldCount;
            NewSharpFoldCount = newSharpFoldCount;
            NewDegenerateFaceCount = newDegenerateFaceCount;
            ReversedFaceCount = reversedFaceCount;
            MaximumVertexDisplacement = maximumVertexDisplacement;
            MaximumEdgeLengthStrain = maximumEdgeLengthStrain;
            UsedRestShapeRecovery = usedRestShapeRecovery;
            MaximumRestEdgeStrainIncrease = maximumRestEdgeStrainIncrease;
            MinimumRestShapeAngleImprovementDegrees =
                minimumRestShapeAngleImprovementDegrees;
            MinimumRestEdgeLengthRatioBeforeRecovery =
                minimumRestEdgeLengthRatioBeforeRecovery;
            MinimumRestEdgeLengthRatioAfterRecovery =
                minimumRestEdgeLengthRatioAfterRecovery;
            UnresolvedRestShapeRecoveryCount = unresolvedRestShapeRecoveryCount;
        }

        internal IReadOnlyList<Vector3> CorrectedVertices { get; }

        internal int[] CorrectedVertexIndices { get; }

        internal int InitialSharpFoldCount { get; }

        internal int ResidualSharpFoldCount { get; }

        internal int NewSharpFoldCount { get; }

        internal int NewDegenerateFaceCount { get; }

        internal int ReversedFaceCount { get; }

        internal float MaximumVertexDisplacement { get; }

        internal float MaximumEdgeLengthStrain { get; }

        internal bool UsedRestShapeRecovery { get; }

        internal float MaximumRestEdgeStrainIncrease { get; }

        internal float MinimumRestShapeAngleImprovementDegrees { get; }

        internal float MinimumRestEdgeLengthRatioBeforeRecovery { get; }

        internal float MinimumRestEdgeLengthRatioAfterRecovery { get; }

        internal int UnresolvedRestShapeRecoveryCount { get; }

        internal bool IsSafe =>
            ResidualSharpFoldCount == 0 &&
            NewSharpFoldCount == 0 &&
            NewDegenerateFaceCount == 0 &&
            ReversedFaceCount == 0;
    }

    /// <summary>
    /// 스키닝된 팔 표면의 급접힘을 위치 기반 제약으로 완화함.
    /// </summary>
    internal static class NativeSkinningSurfaceCorrectionCalculator
    {
        private const float RestSmoothAngleDegrees = 30f;
        private const float SharpFoldAngleDegrees = 150f;
        private const float ProjectionAngleDegrees = 145f;
        private const float RestShapeRecoveryAngleDegrees = 140f;
        private const float RestShapeMinimumAngleImprovementDegrees = 5f;
        private const float RestShapeActiveSetActivationMargin = 0.0005f;
        private const float RestShapeMaximumProjectionCorrection = 0.000025f;
        private const int RestShapeActiveSetProjectionPasses = 8;
        private const int WorksetRingCount = 2;
        private const float DisplacementSmoothnessWeight = 1f;
        private const float DisplacementAttachmentWeight = 0.02f;
        private const float RestShapeRecoveryAttachmentWeight = 0.05f;
        private const float BendingWeight = 4f;
        private const float EdgeStrainBarrierWeight = 4f;
        private const float EdgeStrainActivation = 0.015f;
        private const float EdgeLengthProjectionMargin = 0.0001f;
        private const float MaximumAbsoluteStep = 0.00005f;
        private const float MaximumRelativeStep = 0.02f;
        private const float QualityComparisonTolerance = 0.0000001f;
        private const float MinimumStoredCorrectionSquaredMagnitude =
            0.0000000000000001f;
        private const int LineSearchIterations = 10;
        private const int CoupledBlendSearchStepCount = 32;
        private const int CoupledProjectionRoundCount = 8;
        private const int OrientationSearchIterations = 24;
        private const float OrientationBarrierWeight = 4f;
        private const float OrientationActivationRatio = 0.25f;
        private const float MinimumSignedAreaRatio = 0.05f;
        private const float OrientationSafetyFactor = 0.95f;
        private const float MinimumNormalSquaredMagnitude = 0.0000000000000001f;
        private static readonly CorrectionConfiguration StrongConfiguration =
            new CorrectionConfiguration(640, 64, 32, 0.03f, 0.0051f);
        private static readonly CorrectionConfiguration FastConfiguration =
            new CorrectionConfiguration(80, 16, 0, 0.02f, 0.002512f);

        internal static bool TryCalculateFast(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculate(
                baselineVertices,
                baselineVertices,
                contract,
                FastConfiguration,
                false,
                true,
                out result);
        }

        internal static bool TryCalculateFastValidatedFrame(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculate(
                baselineVertices,
                baselineVertices,
                contract,
                FastConfiguration,
                false,
                false,
                out result);
        }

        internal static bool TryCalculateStrong(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculate(
                baselineVertices,
                baselineVertices,
                contract,
                StrongConfiguration,
                false,
                true,
                out result);
        }

        internal static bool TryCalculateStrongValidatedFrame(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculate(
                baselineVertices,
                baselineVertices,
                contract,
                StrongConfiguration,
                false,
                false,
                out result);
        }

        internal static bool AreVerticesFinite(
            IReadOnlyList<Vector3> vertices)
        {
            return vertices != null && vertices.All(IsFinite);
        }

        internal static bool IsWithinFastQuality(
            NativeSkinningSurfaceCorrectionResult result,
            NativeSkinningSurfaceContract contract)
        {
            return IsWithinQuality(result, contract, FastConfiguration);
        }

        internal static bool IsWithinStrongQuality(
            NativeSkinningSurfaceCorrectionResult result,
            NativeSkinningSurfaceContract contract)
        {
            return IsWithinQuality(result, contract, StrongConfiguration);
        }

        private static bool IsWithinQuality(
            NativeSkinningSurfaceCorrectionResult result,
            NativeSkinningSurfaceContract contract,
            CorrectionConfiguration configuration)
        {
            bool hasAcceptableSurfaceStrain = result != null &&
                (result.UsedRestShapeRecovery
                    ? result.MaximumRestEdgeStrainIncrease <=
                        NativeSkinningRestShapeRecoveryCalculator
                            .MaximumRestStrainIncrease +
                        QualityComparisonTolerance &&
                      result.UnresolvedRestShapeRecoveryCount == 0
                    : result.MaximumEdgeLengthStrain <=
                        configuration.MaximumEdgeLengthStrain);
            return result != null &&
                   contract != null &&
                   result.IsSafe &&
                   hasAcceptableSurfaceStrain &&
                   result.MaximumVertexDisplacement <=
                       contract.ArmChainLength *
                       configuration.MaximumTotalDisplacementToArmLengthRatio +
                       QualityComparisonTolerance;
        }

        private static bool TryCalculate(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> initialVertices,
            NativeSkinningSurfaceContract contract,
            CorrectionConfiguration configuration,
            bool allowsStrainRecovery,
            bool shouldValidateVertices,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            result = null;
            if (!HasValidInput(
                    baselineVertices,
                    initialVertices,
                    contract,
                    shouldValidateVertices))
            {
                return false;
            }

            var activePairIndices = new HashSet<int>();
            UpdateActiveSharpFoldPairs(
                baselineVertices,
                contract,
                Enumerable.Range(0, contract.FacePairs.Length),
                activePairIndices);
            int initialSharpFoldCount = activePairIndices.Count;
            if (initialSharpFoldCount == 0)
            {
                result = new NativeSkinningSurfaceCorrectionResult(
                    initialVertices,
                    Array.Empty<int>(),
                    0,
                    0,
                    0,
                    0,
                    0,
                    0f,
                    0f,
                    false,
                    0f,
                    0f,
                    0f,
                    0f,
                    0);
                return true;
            }
            int[] restShapeRecoveryPairIndices = activePairIndices
                .Where(pairIndex => IsRestShapeRecoveryPair(
                    baselineVertices,
                    contract,
                    pairIndex))
                .ToArray();
            int[] sharpFoldPairIndices = activePairIndices
                .Except(restShapeRecoveryPairIndices)
                .ToArray();

            Vector3[] vertices = initialVertices.ToArray();
            FrameEdge[] frameEdges = BuildFrameEdges(baselineVertices, contract);
            if (restShapeRecoveryPairIndices.Length > 0)
            {
                vertices = ApplyCorrectionPass(
                    baselineVertices,
                    vertices,
                    contract,
                    configuration,
                    restShapeRecoveryPairIndices,
                    true,
                    allowsStrainRecovery);
            }
            if (sharpFoldPairIndices.Length > 0)
            {
                vertices = ApplyCorrectionPass(
                    baselineVertices,
                    vertices,
                    contract,
                    configuration,
                    sharpFoldPairIndices,
                    false,
                    allowsStrainRecovery ||
                    restShapeRecoveryPairIndices.Length > 0);
            }

            result = EvaluateResult(
                baselineVertices,
                vertices,
                contract,
                frameEdges,
                initialSharpFoldCount,
                restShapeRecoveryPairIndices);
            if (!IsWithinQuality(result, contract, configuration) &&
                TryCalculateCoupledProjectionFallback(
                    baselineVertices,
                    initialVertices,
                    contract,
                    configuration,
                    allowsStrainRecovery,
                    frameEdges,
                    initialSharpFoldCount,
                    restShapeRecoveryPairIndices,
                    sharpFoldPairIndices,
                    out NativeSkinningSurfaceCorrectionResult fallbackResult))
            {
                result = fallbackResult;
            }
            return true;
        }

        private static bool TryCalculateCoupledProjectionFallback(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> initialVertices,
            NativeSkinningSurfaceContract contract,
            CorrectionConfiguration configuration,
            bool allowsStrainRecovery,
            IReadOnlyList<FrameEdge> frameEdges,
            int initialSharpFoldCount,
            IReadOnlyList<int> restShapeRecoveryPairIndices,
            IReadOnlyList<int> sharpFoldPairIndices,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            result = null;
            if (restShapeRecoveryPairIndices.Count == 0)
            {
                return false;
            }

            Vector3[] sharpFoldCorrectedVertices = sharpFoldPairIndices.Count > 0
                ? ApplyCorrectionPass(
                    baselineVertices,
                    initialVertices.ToArray(),
                    contract,
                    configuration,
                    sharpFoldPairIndices,
                    false,
                    allowsStrainRecovery)
                : initialVertices.ToArray();
            Vector3[] projectionStartVertices = sharpFoldCorrectedVertices;
            for (int round = 0; round < CoupledProjectionRoundCount; round++)
            {
                if (!NativeSkinningCoupledDihedralProjectionCalculator.TryProject(
                        baselineVertices,
                        projectionStartVertices,
                        contract,
                        restShapeRecoveryPairIndices,
                        sharpFoldPairIndices,
                        RestSmoothAngleDegrees,
                        SharpFoldAngleDegrees,
                        ProjectionAngleDegrees,
                        out Vector3[] projectedVertices))
                {
                    return false;
                }

                result = EvaluateResult(
                    baselineVertices,
                    projectedVertices,
                    contract,
                    frameEdges,
                    initialSharpFoldCount,
                    restShapeRecoveryPairIndices);
                if (IsWithinQuality(result, contract, configuration))
                {
                    return true;
                }
                if (TryFindQualityPreservingCoupledBlend(
                        baselineVertices,
                        projectionStartVertices,
                        projectedVertices,
                        contract,
                        configuration,
                        frameEdges,
                        initialSharpFoldCount,
                        restShapeRecoveryPairIndices,
                        out result))
                {
                    return true;
                }
                projectionStartVertices = projectedVertices;
            }
            return false;
        }

        private static bool TryFindQualityPreservingCoupledBlend(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> projectionStartVertices,
            IReadOnlyList<Vector3> projectedVertices,
            NativeSkinningSurfaceContract contract,
            CorrectionConfiguration configuration,
            IReadOnlyList<FrameEdge> frameEdges,
            int initialSharpFoldCount,
            IReadOnlyList<int> restShapeRecoveryPairIndices,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            result = null;
            var blendedVertices = new Vector3[baselineVertices.Count];
            for (int step = CoupledBlendSearchStepCount - 1; step > 0; step--)
            {
                float blend = step / (float)CoupledBlendSearchStepCount;
                for (int vertexIndex = 0;
                     vertexIndex < blendedVertices.Length;
                     vertexIndex++)
                {
                    blendedVertices[vertexIndex] = Vector3.Lerp(
                        projectionStartVertices[vertexIndex],
                        projectedVertices[vertexIndex],
                        blend);
                }

                NativeSkinningSurfaceCorrectionResult candidate = EvaluateResult(
                    baselineVertices,
                    blendedVertices,
                    contract,
                    frameEdges,
                    initialSharpFoldCount,
                    restShapeRecoveryPairIndices);
                if (IsWithinQuality(candidate, contract, configuration))
                {
                    result = candidate;
                    return true;
                }
            }
            return false;
        }

        private static Vector3[] ApplyCorrectionPass(
            IReadOnlyList<Vector3> baselineVertices,
            Vector3[] initialVertices,
            NativeSkinningSurfaceContract contract,
            CorrectionConfiguration configuration,
            IEnumerable<int> seedPairIndices,
            bool shouldRecoverRestShape,
            bool allowsStrainRecovery)
        {
            Vector3[] vertices = initialVertices;
            var activePairIndices = new HashSet<int>(seedPairIndices);
            LocalWorkset workset = BuildLocalWorkset(
                baselineVertices,
                contract,
                activePairIndices,
                WorksetRingCount);
            for (int iteration = 0;
                 iteration < configuration.LocalGlobalIterations;
                 iteration++)
            {
                UpdateActiveCorrectionPairs(
                    baselineVertices,
                    vertices,
                    contract,
                    workset.FacePairIndices,
                    shouldRecoverRestShape,
                    activePairIndices);
                double beforeEnergy = CalculateConstraintEnergy(
                    baselineVertices,
                    vertices,
                    contract,
                    activePairIndices,
                    workset.FacePairIndices,
                    workset.FaceIndices,
                    workset.Edges,
                    shouldRecoverRestShape);
                if (beforeEnergy <= 0d)
                {
                    break;
                }

                BuildDisplacementSystem(
                    baselineVertices,
                    vertices,
                    contract,
                    workset,
                    activePairIndices,
                    shouldRecoverRestShape,
                    out float[] diagonal,
                    out float[] preconditionerDiagonal,
                    out Vector3[] rightHandSide,
                    out BendingLinearization[] bendingLinearizations,
                    out EdgeStrainLinearization[] edgeStrainLinearizations,
                    out NativeSkinningMaterialStrainConstraint[]
                        materialStrainConstraints,
                    out int activeConstraintCount);
                if (activeConstraintCount == 0)
                {
                    break;
                }

                Vector3[] displacement = SolveConjugateGradient(
                    diagonal,
                    preconditionerDiagonal,
                    shouldRecoverRestShape
                        ? Array.Empty<LocalEdge>()
                        : workset.Edges,
                    bendingLinearizations,
                    edgeStrainLinearizations,
                    materialStrainConstraints,
                    rightHandSide,
                    configuration.ConjugateGradientIterations);
                LimitDisplacement(
                    displacement,
                    vertices,
                    contract,
                    activePairIndices);
                Vector3[] proposedLocalPositions = workset.ConstrainedVertices
                    .Select((vertexIndex, localIndex) =>
                        vertices[vertexIndex] + displacement[localIndex])
                    .ToArray();
                if (shouldRecoverRestShape)
                {
                    ProjectRestShapeRecoveryConstraints(
                        baselineVertices,
                        vertices,
                        proposedLocalPositions,
                        workset,
                        contract,
                        activePairIndices);
                }
                else
                {
                    ProjectEdgeLengthBounds(
                        vertices,
                        proposedLocalPositions,
                        workset.ConstrainedVertices,
                        workset.Edges,
                        configuration.MaximumEdgeLengthStrain -
                            EdgeLengthProjectionMargin,
                        configuration.EdgeLengthProjectionIterations);
                }
                if (!TryAcceptGlobalStep(
                        baselineVertices,
                        vertices,
                        proposedLocalPositions,
                        workset,
                        contract,
                        activePairIndices,
                        beforeEnergy,
                        configuration,
                        shouldRecoverRestShape,
                        allowsStrainRecovery,
                        out Vector3[] acceptedVertices))
                {
                    break;
                }
                vertices = acceptedVertices;
            }
            return vertices;
        }

        private static bool HasValidInput(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> initialVertices,
            NativeSkinningSurfaceContract contract,
            bool shouldValidateVertices)
        {
            return baselineVertices != null &&
                   initialVertices != null &&
                   contract != null &&
                   baselineVertices.Count == contract.VertexCount &&
                   initialVertices.Count == contract.VertexCount &&
                   contract.RestVertices != null &&
                   contract.RestVertices.Length == contract.VertexCount &&
                   contract.FacePairs.Length > 0 &&
                   contract.FacePairRestLengths != null &&
                   contract.FacePairRestLengths.Length == contract.FacePairs.Length &&
                   contract.AffectedFaceIndices.Length > 0 &&
                   contract.ArmChainLength > 0f &&
                   (!shouldValidateVertices ||
                    AreVerticesFinite(baselineVertices) &&
                    (ReferenceEquals(baselineVertices, initialVertices) ||
                     AreVerticesFinite(initialVertices)));
        }

        private static NativeSkinningSurfaceCorrectionResult EvaluateResult(
            IReadOnlyList<Vector3> baselineVertices,
            Vector3[] vertices,
            NativeSkinningSurfaceContract contract,
            IReadOnlyList<FrameEdge> edges,
            int initialSharpFoldCount,
            IReadOnlyList<int> restShapeRecoveryPairIndices)
        {
            int residualSharpFoldCount = 0;
            int newSharpFoldCount = 0;
            for (int pairIndex = 0; pairIndex < contract.FacePairs.Length; pairIndex++)
            {
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                bool baselineValid = NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                    baselineVertices,
                    pair,
                    out float baselineAngle);
                if (!NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        pair,
                        out float candidateAngle))
                {
                    continue;
                }
                if (contract.RestAnglesDegrees[pairIndex] <= RestSmoothAngleDegrees &&
                    candidateAngle > SharpFoldAngleDegrees)
                {
                    residualSharpFoldCount++;
                }
                if (baselineValid &&
                    baselineAngle <= SharpFoldAngleDegrees &&
                    candidateAngle > SharpFoldAngleDegrees)
                {
                    newSharpFoldCount++;
                }
            }

            int newDegenerateFaceCount = 0;
            int reversedFaceCount = 0;
            foreach (int faceIndex in contract.AffectedFaceIndices)
            {
                bool baselineValid = TryCalculateFaceNormal(
                    baselineVertices,
                    contract.Triangles,
                    faceIndex,
                    out Vector3 baselineNormal);
                bool candidateValid = TryCalculateFaceNormal(
                    vertices,
                    contract.Triangles,
                    faceIndex,
                    out Vector3 candidateNormal);
                if (baselineValid && !candidateValid)
                {
                    newDegenerateFaceCount++;
                }
                else if (baselineValid && candidateValid &&
                         Vector3.Dot(baselineNormal, candidateNormal) < 0f)
                {
                    reversedFaceCount++;
                }
            }

            int[] affectedVertices = contract.AffectedFaceIndices
                .SelectMany(faceIndex => GetFaceVertices(contract.Triangles, faceIndex))
                .Distinct()
                .ToArray();
            int[] correctedVertexIndices = Enumerable.Range(0, vertices.Length)
                .Where(index =>
                    (vertices[index] - baselineVertices[index]).sqrMagnitude >
                    MinimumStoredCorrectionSquaredMagnitude)
                .ToArray();
            bool usedRestShapeRecovery = restShapeRecoveryPairIndices.Count > 0;
            float maximumRestEdgeStrainIncrease = usedRestShapeRecovery
                ? edges.Max(edge => Mathf.Max(
                    0f,
                    NativeSkinningRestShapeRecoveryCalculator
                        .CalculateStrainIncrease(
                            edge.BaselineLength,
                            Vector3.Distance(
                                vertices[edge.FirstVertex],
                                vertices[edge.SecondVertex]),
                            edge.RestLength)))
                : 0f;
            float minimumRestShapeAngleImprovementDegrees =
                CalculateMinimumRestShapeAngleImprovement(
                    baselineVertices,
                    vertices,
                    contract,
                    restShapeRecoveryPairIndices);
            float minimumRestEdgeLengthRatioBeforeRecovery =
                usedRestShapeRecovery
                    ? CalculateMinimumRestEdgeLengthRatio(
                        baselineVertices,
                        edges)
                    : 0f;
            float minimumRestEdgeLengthRatioAfterRecovery =
                usedRestShapeRecovery
                    ? CalculateMinimumRestEdgeLengthRatio(vertices, edges)
                    : 0f;
            int unresolvedRestShapeRecoveryCount = restShapeRecoveryPairIndices
                .Count(pairIndex => IsRestShapeRecoveryPairUnresolved(
                    vertices,
                    contract,
                    pairIndex));
            return new NativeSkinningSurfaceCorrectionResult(
                vertices,
                correctedVertexIndices,
                initialSharpFoldCount,
                residualSharpFoldCount,
                newSharpFoldCount,
                newDegenerateFaceCount,
                reversedFaceCount,
                affectedVertices.Max(index => Vector3.Distance(
                    vertices[index],
                    baselineVertices[index])),
                edges.Max(edge => Mathf.Abs(
                    Vector3.Distance(
                        vertices[edge.FirstVertex],
                        vertices[edge.SecondVertex]) /
                    edge.BaselineLength - 1f)),
                usedRestShapeRecovery,
                maximumRestEdgeStrainIncrease,
                minimumRestShapeAngleImprovementDegrees,
                minimumRestEdgeLengthRatioBeforeRecovery,
                minimumRestEdgeLengthRatioAfterRecovery,
                unresolvedRestShapeRecoveryCount);
        }

        private static bool IsRestShapeRecoveryPairUnresolved(
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            int pairIndex)
        {
            NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
            return !NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                       vertices,
                       pair,
                       out float angleDegrees) ||
                   NativeSkinningRestShapeCollapseDetector.IsSeverelyCollapsed(
                       vertices,
                       pair,
                       contract.FacePairRestLengths[pairIndex],
                       contract.RestAnglesDegrees[pairIndex],
                       angleDegrees);
        }

        private static float CalculateMinimumRestEdgeLengthRatio(
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<FrameEdge> edges)
        {
            return edges.Min(edge =>
                Vector3.Distance(
                    vertices[edge.FirstVertex],
                    vertices[edge.SecondVertex]) /
                edge.RestLength);
        }

        private static float CalculateMinimumRestShapeAngleImprovement(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> correctedVertices,
            NativeSkinningSurfaceContract contract,
            IReadOnlyList<int> recoveryPairIndices)
        {
            if (recoveryPairIndices.Count == 0)
            {
                return 0f;
            }

            float minimumImprovement = float.PositiveInfinity;
            foreach (int pairIndex in recoveryPairIndices)
            {
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                if (!NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        baselineVertices,
                        pair,
                        out float baselineAngleDegrees) ||
                    !NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        correctedVertices,
                        pair,
                        out float correctedAngleDegrees))
                {
                    return float.NegativeInfinity;
                }

                minimumImprovement = Mathf.Min(
                    minimumImprovement,
                    baselineAngleDegrees - correctedAngleDegrees);
            }
            return minimumImprovement;
        }

        private static void BuildDisplacementSystem(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            LocalWorkset workset,
            IReadOnlyCollection<int> activePairIndices,
            bool shouldRecoverRestShape,
            out float[] diagonal,
            out float[] preconditionerDiagonal,
            out Vector3[] rightHandSide,
            out BendingLinearization[] bendingLinearizations,
            out EdgeStrainLinearization[] edgeStrainLinearizations,
            out NativeSkinningMaterialStrainConstraint[] materialStrainConstraints,
            out int activeConstraintCount)
        {
            diagonal = new float[workset.ConstrainedVertices.Length];
            rightHandSide = new Vector3[workset.ConstrainedVertices.Length];
            var bendingTerms = new List<BendingLinearization>();
            var edgeStrainTerms = new List<EdgeStrainLinearization>();
            for (int localIndex = 0; localIndex < diagonal.Length; localIndex++)
            {
                diagonal[localIndex] = shouldRecoverRestShape
                    ? RestShapeRecoveryAttachmentWeight
                    : DisplacementAttachmentWeight;
                if (shouldRecoverRestShape)
                {
                    int vertexIndex = workset.ConstrainedVertices[localIndex];
                    rightHandSide[localIndex] =
                        (baselineVertices[vertexIndex] - vertices[vertexIndex]) *
                        RestShapeRecoveryAttachmentWeight;
                }
            }
            if (!shouldRecoverRestShape)
            {
                foreach (LocalEdge edge in workset.Edges)
                {
                    if (edge.FirstLocalIndex >= 0)
                    {
                        diagonal[edge.FirstLocalIndex] +=
                            DisplacementSmoothnessWeight;
                    }
                    if (edge.SecondLocalIndex >= 0)
                    {
                        diagonal[edge.SecondLocalIndex] +=
                            DisplacementSmoothnessWeight;
                    }
                }
            }

            activeConstraintCount = 0;
            foreach (int pairIndex in workset.FacePairIndices)
            {
                if (contract.RestAnglesDegrees[pairIndex] > RestSmoothAngleDegrees ||
                    !activePairIndices.Contains(pairIndex))
                {
                    continue;
                }
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                if (!NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        pair,
                        out float angle))
                {
                    continue;
                }
                float targetAngleDegrees = ResolveProjectionAngleDegrees(
                    baselineVertices,
                    contract,
                    pairIndex);
                if (angle <= targetAngleDegrees)
                {
                    continue;
                }
                if (!MeshDihedralConstraintCalculator.TryCalculateCorrections(
                        vertices[pair.FirstOpposite],
                        vertices[pair.SecondOpposite],
                        vertices[pair.FirstEdge],
                        vertices[pair.SecondEdge],
                        targetAngleDegrees,
                        1f,
                        out Vector3 correction0,
                        out Vector3 correction1,
                        out Vector3 correction2,
                        out Vector3 correction3))
                {
                    continue;
                }

                float violationRadians =
                    (angle - targetAngleDegrees) * Mathf.Deg2Rad;
                float correctionSquaredSum =
                    correction0.sqrMagnitude +
                    correction1.sqrMagnitude +
                    correction2.sqrMagnitude +
                    correction3.sqrMagnitude;
                if (correctionSquaredSum <= MinimumNormalSquaredMagnitude)
                {
                    continue;
                }
                float gradientSquaredSum =
                    violationRadians * violationRadians / correctionSquaredSum;
                float gradientScale = -gradientSquaredSum / violationRadians;
                int[] localIndices =
                {
                    workset.LocalIndexByVertex[pair.FirstOpposite],
                    workset.LocalIndexByVertex[pair.SecondOpposite],
                    workset.LocalIndexByVertex[pair.FirstEdge],
                    workset.LocalIndexByVertex[pair.SecondEdge]
                };
                Vector3[] gradients =
                {
                    correction0 * gradientScale,
                    correction1 * gradientScale,
                    correction2 * gradientScale,
                    correction3 * gradientScale
                };
                AddBendingRightHandSide(
                    pair.FirstOpposite,
                    correction0,
                    gradientSquaredSum,
                    workset.LocalIndexByVertex,
                    rightHandSide);
                AddBendingRightHandSide(
                    pair.SecondOpposite,
                    correction1,
                    gradientSquaredSum,
                    workset.LocalIndexByVertex,
                    rightHandSide);
                AddBendingRightHandSide(
                    pair.FirstEdge,
                    correction2,
                    gradientSquaredSum,
                    workset.LocalIndexByVertex,
                    rightHandSide);
                AddBendingRightHandSide(
                    pair.SecondEdge,
                    correction3,
                    gradientSquaredSum,
                    workset.LocalIndexByVertex,
                    rightHandSide);
                bendingTerms.Add(new BendingLinearization(localIndices, gradients));
                activeConstraintCount++;
            }

            materialStrainConstraints = shouldRecoverRestShape
                ? NativeSkinningRestShapeRecoveryCalculator.BuildConstraints(
                    baselineVertices,
                    contract.RestVertices,
                    vertices,
                    contract.Triangles,
                    workset.FaceIndices,
                    workset.LocalIndexByVertex)
                : Array.Empty<NativeSkinningMaterialStrainConstraint>();
            foreach (NativeSkinningMaterialStrainConstraint constraint in
                     materialStrainConstraints)
            {
                for (int index = 0; index < constraint.LocalIndices.Length; index++)
                {
                    int localIndex = constraint.LocalIndices[index];
                    float coefficient = constraint.Coefficients[index];
                    diagonal[localIndex] +=
                        constraint.Weight * coefficient * coefficient;
                    rightHandSide[localIndex] +=
                        constraint.ProjectedDelta * coefficient * constraint.Weight;
                }
                activeConstraintCount++;
            }

            AddOrientationBarrierTerms(
                baselineVertices,
                vertices,
                contract,
                workset.LocalIndexByVertex,
                workset.FaceIndices,
                diagonal,
                rightHandSide,
                ref activeConstraintCount);
            if (!shouldRecoverRestShape)
            {
                AddEdgeStrainTerms(
                    vertices,
                    workset.Edges,
                    rightHandSide,
                    edgeStrainTerms,
                    ref activeConstraintCount);
            }
            preconditionerDiagonal = diagonal.ToArray();
            foreach (BendingLinearization term in bendingTerms)
            {
                for (int index = 0; index < term.LocalIndices.Length; index++)
                {
                    int localIndex = term.LocalIndices[index];
                    if (localIndex >= 0)
                    {
                        preconditionerDiagonal[localIndex] +=
                            BendingWeight * term.Gradients[index].sqrMagnitude;
                    }
                }
            }
            foreach (EdgeStrainLinearization term in edgeStrainTerms)
            {
                if (term.FirstLocalIndex >= 0)
                {
                    preconditionerDiagonal[term.FirstLocalIndex] +=
                        EdgeStrainBarrierWeight * term.FirstGradient.sqrMagnitude;
                }
                if (term.SecondLocalIndex >= 0)
                {
                    preconditionerDiagonal[term.SecondLocalIndex] +=
                        EdgeStrainBarrierWeight * term.SecondGradient.sqrMagnitude;
                }
            }
            foreach (NativeSkinningMaterialStrainConstraint constraint in
                     materialStrainConstraints)
            {
                for (int index = 0; index < constraint.LocalIndices.Length; index++)
                {
                    int localIndex = constraint.LocalIndices[index];
                    float coefficient = constraint.Coefficients[index];
                    preconditionerDiagonal[localIndex] +=
                        constraint.Weight * coefficient * coefficient;
                }
            }
            bendingLinearizations = bendingTerms.ToArray();
            edgeStrainLinearizations = edgeStrainTerms.ToArray();
        }

        private static void AddBendingRightHandSide(
            int vertexIndex,
            Vector3 correction,
            float gradientSquaredSum,
            IReadOnlyList<int> localIndexByVertex,
            IList<Vector3> rightHandSide)
        {
            int localIndex = localIndexByVertex[vertexIndex];
            if (localIndex >= 0)
            {
                rightHandSide[localIndex] +=
                    correction * gradientSquaredSum * BendingWeight;
            }
        }

        private static void AddOrientationBarrierTerms(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            IReadOnlyList<int> localIndexByVertex,
            IEnumerable<int> faceIndices,
            IList<float> diagonal,
            IList<Vector3> rightHandSide,
            ref int activeConstraintCount)
        {
            float minimumNormalMagnitude = Mathf.Sqrt(MinimumNormalSquaredMagnitude);
            foreach (int faceIndex in faceIndices)
            {
                int offset = faceIndex * 3;
                int firstIndex = contract.Triangles[offset];
                int secondIndex = contract.Triangles[offset + 1];
                int thirdIndex = contract.Triangles[offset + 2];
                Vector3 baselineCross = Vector3.Cross(
                    baselineVertices[secondIndex] - baselineVertices[firstIndex],
                    baselineVertices[thirdIndex] - baselineVertices[firstIndex]);
                float baselineArea = baselineCross.magnitude;
                if (baselineArea < minimumNormalMagnitude)
                {
                    continue;
                }

                Vector3 baselineNormal = baselineCross / baselineArea;
                Vector3 first = vertices[firstIndex];
                Vector3 second = vertices[secondIndex];
                Vector3 third = vertices[thirdIndex];
                float signedArea = Vector3.Dot(
                    Vector3.Cross(second - first, third - first),
                    baselineNormal);
                float activationArea = baselineArea * OrientationActivationRatio;
                if (signedArea >= activationArea)
                {
                    continue;
                }

                float normalizedViolation = Mathf.Max(
                    0f,
                    (activationArea - signedArea) / activationArea);
                Vector3[] gradients =
                {
                    Vector3.Cross(second - third, baselineNormal),
                    Vector3.Cross(third - first, baselineNormal),
                    Vector3.Cross(baselineNormal, second - first)
                };
                int[] faceVertices = { firstIndex, secondIndex, thirdIndex };
                for (int index = 0; index < faceVertices.Length; index++)
                {
                    int localIndex = localIndexByVertex[faceVertices[index]];
                    if (localIndex < 0)
                    {
                        continue;
                    }
                    Vector3 normalizedGradient = gradients[index] / activationArea;
                    diagonal[localIndex] +=
                        OrientationBarrierWeight * normalizedGradient.sqrMagnitude;
                    rightHandSide[localIndex] +=
                        OrientationBarrierWeight *
                        normalizedViolation * normalizedGradient;
                }
                activeConstraintCount++;
            }
        }

        private static void AddEdgeStrainTerms(
            IReadOnlyList<Vector3> vertices,
            IEnumerable<LocalEdge> edges,
            IList<Vector3> rightHandSide,
            ICollection<EdgeStrainLinearization> terms,
            ref int activeConstraintCount)
        {
            foreach (LocalEdge edge in edges)
            {
                Vector3 edgeVector =
                    vertices[edge.SecondVertex] - vertices[edge.FirstVertex];
                float edgeLength = edgeVector.magnitude;
                if (edgeLength <= 0.00000001f ||
                    edge.BaselineLength <= 0.00000001f)
                {
                    continue;
                }
                float signedStrain = edgeLength / edge.BaselineLength - 1f;
                float excessMagnitude =
                    Mathf.Abs(signedStrain) - EdgeStrainActivation;
                if (excessMagnitude <= 0f)
                {
                    continue;
                }

                float signedExcess = Mathf.Sign(signedStrain) * excessMagnitude;
                Vector3 normalizedDirection =
                    edgeVector / (edgeLength * edge.BaselineLength);
                Vector3 firstCorrection =
                    EdgeStrainBarrierWeight * signedExcess * normalizedDirection;
                if (edge.FirstLocalIndex >= 0)
                {
                    rightHandSide[edge.FirstLocalIndex] += firstCorrection;
                }
                if (edge.SecondLocalIndex >= 0)
                {
                    rightHandSide[edge.SecondLocalIndex] -= firstCorrection;
                }
                terms.Add(new EdgeStrainLinearization(
                    edge.FirstLocalIndex,
                    edge.SecondLocalIndex,
                    -normalizedDirection,
                    normalizedDirection));
                activeConstraintCount++;
            }
        }

        private static Vector3[] SolveConjugateGradient(
            IReadOnlyList<float> diagonal,
            IReadOnlyList<float> preconditionerDiagonal,
            IReadOnlyList<LocalEdge> edges,
            IReadOnlyList<BendingLinearization> bendingLinearizations,
            IReadOnlyList<EdgeStrainLinearization> edgeStrainLinearizations,
            IReadOnlyList<NativeSkinningMaterialStrainConstraint>
                materialStrainConstraints,
            IReadOnlyList<Vector3> rightHandSide,
            int maximumIterations)
        {
            var solution = new Vector3[rightHandSide.Count];
            Vector3[] residual = Subtract(
                rightHandSide,
                ApplyMatrix(
                    solution,
                    diagonal,
                    edges,
                    bendingLinearizations,
                    edgeStrainLinearizations,
                    materialStrainConstraints));
            Vector3[] direction = ApplyDiagonalPreconditioner(
                residual,
                preconditionerDiagonal);
            Vector3[] preconditionedResidual = direction.ToArray();
            double residualDot = Dot(residual, preconditionedResidual);
            for (int iteration = 0;
                 iteration < maximumIterations && residualDot > 1e-20d;
                 iteration++)
            {
                Vector3[] matrixDirection = ApplyMatrix(
                    direction,
                    diagonal,
                    edges,
                    bendingLinearizations,
                    edgeStrainLinearizations,
                    materialStrainConstraints);
                double denominator = Dot(direction, matrixDirection);
                if (Math.Abs(denominator) <= 1e-20d)
                {
                    break;
                }
                float step = (float)(residualDot / denominator);
                for (int index = 0; index < solution.Length; index++)
                {
                    solution[index] += direction[index] * step;
                    residual[index] -= matrixDirection[index] * step;
                }
                if (Dot(residual, residual) <= 1e-18d)
                {
                    break;
                }
                preconditionedResidual = ApplyDiagonalPreconditioner(
                    residual,
                    preconditionerDiagonal);
                double nextResidualDot = Dot(residual, preconditionedResidual);
                float beta = (float)(nextResidualDot / residualDot);
                for (int index = 0; index < direction.Length; index++)
                {
                    direction[index] = preconditionedResidual[index] +
                        direction[index] * beta;
                }
                residualDot = nextResidualDot;
            }
            return solution;
        }

        private static Vector3[] ApplyMatrix(
            IReadOnlyList<Vector3> values,
            IReadOnlyList<float> diagonal,
            IEnumerable<LocalEdge> edges,
            IEnumerable<BendingLinearization> bendingLinearizations,
            IEnumerable<EdgeStrainLinearization> edgeStrainLinearizations,
            IEnumerable<NativeSkinningMaterialStrainConstraint>
                materialStrainConstraints)
        {
            var result = new Vector3[values.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = values[index] * diagonal[index];
            }
            foreach (LocalEdge edge in edges)
            {
                if (edge.FirstLocalIndex >= 0 && edge.SecondLocalIndex >= 0)
                {
                    result[edge.FirstLocalIndex] -=
                        values[edge.SecondLocalIndex] * DisplacementSmoothnessWeight;
                    result[edge.SecondLocalIndex] -=
                        values[edge.FirstLocalIndex] * DisplacementSmoothnessWeight;
                }
            }
            foreach (BendingLinearization term in bendingLinearizations)
            {
                float projectedDisplacement = 0f;
                for (int index = 0; index < term.LocalIndices.Length; index++)
                {
                    int localIndex = term.LocalIndices[index];
                    if (localIndex >= 0)
                    {
                        projectedDisplacement += Vector3.Dot(
                            term.Gradients[index],
                            values[localIndex]);
                    }
                }
                for (int index = 0; index < term.LocalIndices.Length; index++)
                {
                    int localIndex = term.LocalIndices[index];
                    if (localIndex >= 0)
                    {
                        result[localIndex] += BendingWeight *
                            term.Gradients[index] * projectedDisplacement;
                    }
                }
            }
            foreach (EdgeStrainLinearization term in edgeStrainLinearizations)
            {
                float projectedDisplacement = 0f;
                if (term.FirstLocalIndex >= 0)
                {
                    projectedDisplacement += Vector3.Dot(
                        term.FirstGradient,
                        values[term.FirstLocalIndex]);
                }
                if (term.SecondLocalIndex >= 0)
                {
                    projectedDisplacement += Vector3.Dot(
                        term.SecondGradient,
                        values[term.SecondLocalIndex]);
                }
                if (term.FirstLocalIndex >= 0)
                {
                    result[term.FirstLocalIndex] += EdgeStrainBarrierWeight *
                        term.FirstGradient * projectedDisplacement;
                }
                if (term.SecondLocalIndex >= 0)
                {
                    result[term.SecondLocalIndex] += EdgeStrainBarrierWeight *
                        term.SecondGradient * projectedDisplacement;
                }
            }
            foreach (NativeSkinningMaterialStrainConstraint constraint in
                     materialStrainConstraints)
            {
                Vector3 projectedDisplacement = Vector3.zero;
                for (int index = 0; index < constraint.LocalIndices.Length; index++)
                {
                    projectedDisplacement +=
                        values[constraint.LocalIndices[index]] *
                        constraint.Coefficients[index];
                }
                for (int index = 0; index < constraint.LocalIndices.Length; index++)
                {
                    int localIndex = constraint.LocalIndices[index];
                    result[localIndex] += projectedDisplacement *
                        constraint.Coefficients[index] * constraint.Weight;
                }
            }
            return result;
        }

        private static Vector3[] ApplyDiagonalPreconditioner(
            IReadOnlyList<Vector3> values,
            IReadOnlyList<float> diagonal)
        {
            var result = new Vector3[values.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = values[index] /
                    Mathf.Max(diagonal[index], 0.000001f);
            }
            return result;
        }

        private static Vector3[] Subtract(
            IReadOnlyList<Vector3> first,
            IReadOnlyList<Vector3> second)
        {
            var result = new Vector3[first.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = first[index] - second[index];
            }
            return result;
        }

        private static double Dot(
            IReadOnlyList<Vector3> first,
            IReadOnlyList<Vector3> second)
        {
            double result = 0d;
            for (int index = 0; index < first.Count; index++)
            {
                result += Vector3.Dot(first[index], second[index]);
            }
            return result;
        }

        private static void LimitDisplacement(
            IList<Vector3> displacement,
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            IReadOnlyCollection<int> activePairIndices)
        {
            float minimumCharacteristicLength = float.PositiveInfinity;
            foreach (int pairIndex in activePairIndices)
            {
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                if (!NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        pair,
                        out float angle) ||
                    angle <= ProjectionAngleDegrees)
                {
                    continue;
                }
                minimumCharacteristicLength = Mathf.Min(
                    minimumCharacteristicLength,
                    CalculateFacePairCharacteristicLength(vertices, pair));
            }
            float relativeLimit = float.IsPositiveInfinity(minimumCharacteristicLength)
                ? MaximumAbsoluteStep
                : minimumCharacteristicLength * MaximumRelativeStep;
            float maximumStep = Mathf.Min(MaximumAbsoluteStep, relativeLimit);
            float requestedStep = displacement.Max(value => value.magnitude);
            if (requestedStep <= maximumStep || requestedStep <= 0f)
            {
                return;
            }
            float scale = maximumStep / requestedStep;
            for (int index = 0; index < displacement.Count; index++)
            {
                displacement[index] *= scale;
            }
        }

        private static void ProjectEdgeLengthBounds(
            IReadOnlyList<Vector3> currentVertices,
            IList<Vector3> proposedLocalPositions,
            IReadOnlyList<int> constrainedVertices,
            IReadOnlyList<LocalEdge> edges,
            float maximumAbsoluteStrain,
            int projectionIterations)
        {
            for (int iteration = 0; iteration < projectionIterations; iteration++)
            {
                bool hasViolation = false;
                for (int orderedIndex = 0; orderedIndex < edges.Count; orderedIndex++)
                {
                    int edgeIndex = iteration % 2 == 0
                        ? orderedIndex
                        : edges.Count - 1 - orderedIndex;
                    LocalEdge edge = edges[edgeIndex];
                    Vector3 first = edge.FirstLocalIndex >= 0
                        ? proposedLocalPositions[edge.FirstLocalIndex]
                        : currentVertices[edge.FirstVertex];
                    Vector3 second = edge.SecondLocalIndex >= 0
                        ? proposedLocalPositions[edge.SecondLocalIndex]
                        : currentVertices[edge.SecondVertex];
                    Vector3 delta = second - first;
                    float length = delta.magnitude;
                    if (length <= 0.0000001f)
                    {
                        continue;
                    }
                    float minimumLength =
                        edge.BaselineLength * (1f - maximumAbsoluteStrain);
                    float maximumLength =
                        edge.BaselineLength * (1f + maximumAbsoluteStrain);
                    float targetLength = Mathf.Clamp(
                        length,
                        minimumLength,
                        maximumLength);
                    if (Mathf.Approximately(length, targetLength))
                    {
                        continue;
                    }

                    hasViolation = true;
                    Vector3 correction = delta * ((length - targetLength) / length);
                    bool movesFirst = edge.FirstLocalIndex >= 0;
                    bool movesSecond = edge.SecondLocalIndex >= 0;
                    float share = movesFirst && movesSecond ? 0.5f : 1f;
                    if (movesFirst)
                    {
                        proposedLocalPositions[edge.FirstLocalIndex] =
                            ClampDisplacementFromBaseline(
                                currentVertices[
                                    constrainedVertices[edge.FirstLocalIndex]],
                                first + correction * share,
                                MaximumAbsoluteStep);
                    }
                    if (movesSecond)
                    {
                        proposedLocalPositions[edge.SecondLocalIndex] =
                            ClampDisplacementFromBaseline(
                                currentVertices[
                                    constrainedVertices[edge.SecondLocalIndex]],
                                second - correction * share,
                                MaximumAbsoluteStep);
                    }
                }
                if (!hasViolation)
                {
                    break;
                }
            }
        }

        private static void ProjectRestShapeRecoveryConstraints(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> currentVertices,
            IList<Vector3> proposedLocalPositions,
            LocalWorkset workset,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> activePairIndices)
        {
            for (int pass = 0;
                 pass < RestShapeActiveSetProjectionPasses;
                 pass++)
            {
                int strainProjectionCount = ProjectRestShapeStrainBounds(
                    currentVertices,
                    proposedLocalPositions,
                    workset);
                int angleProjectionCount = ProjectRestShapeDihedralBounds(
                    baselineVertices,
                    currentVertices,
                    proposedLocalPositions,
                    workset.LocalIndexByVertex,
                    contract,
                    activePairIndices);
                if (strainProjectionCount == 0 && angleProjectionCount == 0)
                {
                    break;
                }
            }
        }

        private static int ProjectRestShapeStrainBounds(
            IReadOnlyList<Vector3> currentVertices,
            IList<Vector3> proposedLocalPositions,
            LocalWorkset workset)
        {
            int projectionCount = 0;
            foreach (LocalEdge edge in workset.Edges)
            {
                bool canMoveFirst = edge.FirstLocalIndex >= 0;
                bool canMoveSecond = edge.SecondLocalIndex >= 0;
                if (!canMoveFirst && !canMoveSecond)
                {
                    continue;
                }

                Vector3 currentEdge =
                    currentVertices[edge.SecondVertex] -
                    currentVertices[edge.FirstVertex];
                Vector3 proposedFirst = canMoveFirst
                    ? proposedLocalPositions[edge.FirstLocalIndex]
                    : currentVertices[edge.FirstVertex];
                Vector3 proposedSecond = canMoveSecond
                    ? proposedLocalPositions[edge.SecondLocalIndex]
                    : currentVertices[edge.SecondVertex];
                Vector3 proposedEdge = proposedSecond - proposedFirst;
                float currentLength = currentEdge.magnitude;
                float proposedLength = proposedEdge.magnitude;
                if (currentLength <= 0.00000001f ||
                    proposedLength <= 0.00000001f)
                {
                    continue;
                }

                float baselineRatio = edge.BaselineLength / edge.RestLength;
                float maximumAbsoluteStrain = Mathf.Abs(baselineRatio - 1f) +
                    NativeSkinningRestShapeRecoveryCalculator
                        .MaximumRestStrainIncrease;
                float minimumRatio = Mathf.Max(0f, 1f - maximumAbsoluteStrain);
                float maximumRatio = 1f + maximumAbsoluteStrain;
                float currentRatio = currentLength / edge.RestLength;
                float proposedRatio = proposedLength / edge.RestLength;
                float targetRatio;
                if (proposedRatio > maximumRatio)
                {
                    targetRatio = maximumRatio;
                }
                else if (currentRatio >=
                             maximumRatio - RestShapeActiveSetActivationMargin &&
                         proposedRatio > currentRatio)
                {
                    targetRatio = currentRatio;
                }
                else if (proposedRatio < minimumRatio)
                {
                    targetRatio = minimumRatio;
                }
                else if (currentRatio <=
                             minimumRatio + RestShapeActiveSetActivationMargin &&
                         proposedRatio < currentRatio)
                {
                    targetRatio = currentRatio;
                }
                else
                {
                    continue;
                }

                float lengthCorrection =
                    proposedLength - targetRatio * edge.RestLength;
                Vector3 correction = proposedEdge / proposedLength * lengthCorrection;
                if (canMoveFirst && canMoveSecond)
                {
                    correction *= 0.5f;
                }
                if (canMoveFirst)
                {
                    proposedLocalPositions[edge.FirstLocalIndex] += correction;
                }
                if (canMoveSecond)
                {
                    proposedLocalPositions[edge.SecondLocalIndex] -= correction;
                }
                projectionCount++;
            }
            return projectionCount;
        }

        private static int ProjectRestShapeDihedralBounds(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> currentVertices,
            IList<Vector3> proposedLocalPositions,
            IReadOnlyList<int> localIndexByVertex,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> activePairIndices)
        {
            int projectionCount = 0;
            foreach (int pairIndex in activePairIndices)
            {
                if (!IsRestShapeRecoveryPair(
                        baselineVertices,
                        contract,
                        pairIndex))
                {
                    continue;
                }

                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                Vector3 opposite0 = ReadProposedPosition(
                    pair.FirstOpposite,
                    currentVertices,
                    proposedLocalPositions,
                    localIndexByVertex);
                Vector3 opposite1 = ReadProposedPosition(
                    pair.SecondOpposite,
                    currentVertices,
                    proposedLocalPositions,
                    localIndexByVertex);
                Vector3 edge0 = ReadProposedPosition(
                    pair.FirstEdge,
                    currentVertices,
                    proposedLocalPositions,
                    localIndexByVertex);
                Vector3 edge1 = ReadProposedPosition(
                    pair.SecondEdge,
                    currentVertices,
                    proposedLocalPositions,
                    localIndexByVertex);
                if (!MeshDihedralConstraintCalculator.TryCalculateCorrections(
                        opposite0,
                        opposite1,
                        edge0,
                        edge1,
                        ResolveProjectionAngleDegrees(
                            baselineVertices,
                            contract,
                            pairIndex),
                        1f,
                        out Vector3 correction0,
                        out Vector3 correction1,
                        out Vector3 correction2,
                        out Vector3 correction3))
                {
                    continue;
                }

                float maximumCorrectionMagnitude = Mathf.Max(
                    correction0.magnitude,
                    correction1.magnitude,
                    correction2.magnitude,
                    correction3.magnitude);
                float correctionScale = maximumCorrectionMagnitude >
                                        RestShapeMaximumProjectionCorrection
                    ? RestShapeMaximumProjectionCorrection /
                      maximumCorrectionMagnitude
                    : 1f;
                ApplyProjectionCorrection(
                    pair.FirstOpposite,
                    correction0 * correctionScale,
                    proposedLocalPositions,
                    localIndexByVertex);
                ApplyProjectionCorrection(
                    pair.SecondOpposite,
                    correction1 * correctionScale,
                    proposedLocalPositions,
                    localIndexByVertex);
                ApplyProjectionCorrection(
                    pair.FirstEdge,
                    correction2 * correctionScale,
                    proposedLocalPositions,
                    localIndexByVertex);
                ApplyProjectionCorrection(
                    pair.SecondEdge,
                    correction3 * correctionScale,
                    proposedLocalPositions,
                    localIndexByVertex);
                projectionCount++;
            }
            return projectionCount;
        }

        private static Vector3 ReadProposedPosition(
            int vertexIndex,
            IReadOnlyList<Vector3> currentVertices,
            IList<Vector3> proposedLocalPositions,
            IReadOnlyList<int> localIndexByVertex)
        {
            int localIndex = localIndexByVertex[vertexIndex];
            return localIndex < 0
                ? currentVertices[vertexIndex]
                : proposedLocalPositions[localIndex];
        }

        private static void ApplyProjectionCorrection(
            int vertexIndex,
            Vector3 correction,
            IList<Vector3> proposedLocalPositions,
            IReadOnlyList<int> localIndexByVertex)
        {
            int localIndex = localIndexByVertex[vertexIndex];
            if (localIndex < 0)
            {
                return;
            }

            proposedLocalPositions[localIndex] += correction;
        }

        private static bool TryAcceptGlobalStep(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> currentVertices,
            IReadOnlyList<Vector3> proposedLocalPositions,
            LocalWorkset workset,
            NativeSkinningSurfaceContract contract,
            IReadOnlyCollection<int> activePairIndices,
            double beforeEnergy,
            CorrectionConfiguration configuration,
            bool shouldRecoverRestShape,
            bool allowsStrainRecovery,
            out Vector3[] acceptedVertices)
        {
            acceptedVertices = null;
            float blend = CalculateOrientationSafeBlend(
                baselineVertices,
                currentVertices,
                proposedLocalPositions,
                workset.LocalIndexByVertex,
                contract,
                workset.FaceIndices);
            if (blend <= 0f)
            {
                return false;
            }
            Vector3[] candidate = currentVertices.ToArray();
            for (int attempt = 0; attempt < LineSearchIterations; attempt++)
            {
                for (int localIndex = 0;
                     localIndex < workset.ConstrainedVertices.Length;
                     localIndex++)
                {
                    int vertexIndex = workset.ConstrainedVertices[localIndex];
                    candidate[vertexIndex] = Vector3.LerpUnclamped(
                        currentVertices[vertexIndex],
                        proposedLocalPositions[localIndex],
                        blend);
                    candidate[vertexIndex] = ClampDisplacementFromBaseline(
                        baselineVertices[vertexIndex],
                        candidate[vertexIndex],
                        contract.ArmChainLength *
                        configuration.MaximumTotalDisplacementToArmLengthRatio);
                }
                bool hasStrainDefect = shouldRecoverRestShape
                    ? HasRestShapeRecoveryStrainDefect(
                        baselineVertices,
                        candidate,
                        workset.Edges)
                    : allowsStrainRecovery
                        ? HasUnrecoveringEdgeStrainDefect(
                            currentVertices,
                            candidate,
                            workset.Edges,
                            configuration.MaximumEdgeLengthStrain)
                        : HasEdgeStrainDefect(
                            candidate,
                            workset.Edges,
                            configuration.MaximumEdgeLengthStrain);
                bool hasRestShapeSharpFoldDefect = shouldRecoverRestShape &&
                    HasRestShapeSharpFoldDefect(
                        baselineVertices,
                        candidate,
                        contract,
                        workset.FacePairIndices);
                if (!HasOrientationDefect(
                        baselineVertices,
                        candidate,
                        contract,
                        workset.FaceIndices) &&
                    !hasStrainDefect &&
                    !hasRestShapeSharpFoldDefect &&
                    CalculateConstraintEnergy(
                        baselineVertices,
                        candidate,
                        contract,
                        activePairIndices,
                        workset.FacePairIndices,
                        workset.FaceIndices,
                        workset.Edges,
                        shouldRecoverRestShape) < beforeEnergy)
                {
                    acceptedVertices = candidate;
                    return true;
                }
                blend *= 0.5f;
            }
            return false;
        }

        private static bool HasRestShapeSharpFoldDefect(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> candidateVertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> localPairIndices)
        {
            foreach (int pairIndex in localPairIndices)
            {
                if (contract.RestAnglesDegrees[pairIndex] > RestSmoothAngleDegrees)
                {
                    continue;
                }

                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                if (NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        baselineVertices,
                        pair,
                        out float baselineAngleDegrees) &&
                    baselineAngleDegrees <= SharpFoldAngleDegrees &&
                    NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        candidateVertices,
                        pair,
                        out float candidateAngleDegrees) &&
                    candidateAngleDegrees > SharpFoldAngleDegrees)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasRestShapeRecoveryStrainDefect(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> candidateVertices,
            IEnumerable<LocalEdge> edges)
        {
            foreach (LocalEdge edge in edges)
            {
                float strainIncrease = NativeSkinningRestShapeRecoveryCalculator
                    .CalculateStrainIncrease(
                        edge.BaselineLength,
                        Vector3.Distance(
                            candidateVertices[edge.FirstVertex],
                            candidateVertices[edge.SecondVertex]),
                        edge.RestLength);
                if (!IsFinite(strainIncrease) ||
                    strainIncrease > NativeSkinningRestShapeRecoveryCalculator
                        .MaximumRestStrainIncrease)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasUnrecoveringEdgeStrainDefect(
            IReadOnlyList<Vector3> currentVertices,
            IReadOnlyList<Vector3> candidateVertices,
            IReadOnlyList<LocalEdge> edges,
            float maximumAbsoluteStrain)
        {
            double currentViolation = CalculateEdgeStrainViolationEnergy(
                currentVertices,
                edges,
                maximumAbsoluteStrain);
            double candidateViolation = CalculateEdgeStrainViolationEnergy(
                candidateVertices,
                edges,
                maximumAbsoluteStrain);
            return double.IsNaN(candidateViolation) ||
                   double.IsInfinity(candidateViolation) ||
                   (candidateViolation > 0d &&
                    candidateViolation >= currentViolation - 0.000000000001d);
        }

        private static double CalculateEdgeStrainViolationEnergy(
            IReadOnlyList<Vector3> vertices,
            IEnumerable<LocalEdge> edges,
            float maximumAbsoluteStrain)
        {
            double energy = 0d;
            foreach (LocalEdge edge in edges)
            {
                float absoluteStrain = Mathf.Abs(
                    Vector3.Distance(
                        vertices[edge.FirstVertex],
                        vertices[edge.SecondVertex]) /
                    edge.BaselineLength - 1f);
                if (!IsFinite(absoluteStrain))
                {
                    return double.PositiveInfinity;
                }
                float excess = Mathf.Max(
                    0f,
                    absoluteStrain - maximumAbsoluteStrain);
                energy += excess * excess;
            }
            return energy;
        }

        private static bool HasEdgeStrainDefect(
            IReadOnlyList<Vector3> vertices,
            IEnumerable<LocalEdge> edges,
            float maximumAbsoluteStrain)
        {
            foreach (LocalEdge edge in edges)
            {
                float absoluteStrain = Mathf.Abs(
                    Vector3.Distance(
                        vertices[edge.FirstVertex],
                        vertices[edge.SecondVertex]) /
                    edge.BaselineLength - 1f);
                if (!IsFinite(absoluteStrain) ||
                    absoluteStrain > maximumAbsoluteStrain)
                {
                    return true;
                }
            }
            return false;
        }

        private static float CalculateOrientationSafeBlend(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> currentVertices,
            IReadOnlyList<Vector3> proposedLocalPositions,
            IReadOnlyList<int> localIndexByVertex,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> faceIndices)
        {
            float safeBlend = 1f;
            float minimumNormalMagnitude = Mathf.Sqrt(MinimumNormalSquaredMagnitude);
            foreach (int faceIndex in faceIndices)
            {
                int offset = faceIndex * 3;
                int firstIndex = contract.Triangles[offset];
                int secondIndex = contract.Triangles[offset + 1];
                int thirdIndex = contract.Triangles[offset + 2];
                Vector3 baselineCross = Vector3.Cross(
                    baselineVertices[secondIndex] - baselineVertices[firstIndex],
                    baselineVertices[thirdIndex] - baselineVertices[firstIndex]);
                float baselineArea = baselineCross.magnitude;
                if (baselineArea < minimumNormalMagnitude)
                {
                    continue;
                }
                Vector3 baselineNormal = baselineCross / baselineArea;
                float minimumSignedArea = Mathf.Max(
                    minimumNormalMagnitude,
                    baselineArea * MinimumSignedAreaRatio);
                float candidateSignedArea = CalculateBlendedSignedArea(
                    safeBlend,
                    firstIndex,
                    secondIndex,
                    thirdIndex,
                    baselineNormal,
                    currentVertices,
                    proposedLocalPositions,
                    localIndexByVertex);
                if (candidateSignedArea >= minimumSignedArea)
                {
                    continue;
                }

                float lowerBlend = 0f;
                float upperBlend = safeBlend;
                for (int iteration = 0;
                     iteration < OrientationSearchIterations;
                     iteration++)
                {
                    float middleBlend = (lowerBlend + upperBlend) * 0.5f;
                    float signedArea = CalculateBlendedSignedArea(
                        middleBlend,
                        firstIndex,
                        secondIndex,
                        thirdIndex,
                        baselineNormal,
                        currentVertices,
                        proposedLocalPositions,
                        localIndexByVertex);
                    if (signedArea >= minimumSignedArea)
                    {
                        lowerBlend = middleBlend;
                    }
                    else
                    {
                        upperBlend = middleBlend;
                    }
                }
                safeBlend = lowerBlend * OrientationSafetyFactor;
            }
            return safeBlend;
        }

        private static float CalculateBlendedSignedArea(
            float blend,
            int firstIndex,
            int secondIndex,
            int thirdIndex,
            Vector3 baselineNormal,
            IReadOnlyList<Vector3> currentVertices,
            IReadOnlyList<Vector3> proposedLocalPositions,
            IReadOnlyList<int> localIndexByVertex)
        {
            Vector3 first = ReadBlendedPosition(
                blend,
                firstIndex,
                currentVertices,
                proposedLocalPositions,
                localIndexByVertex);
            Vector3 second = ReadBlendedPosition(
                blend,
                secondIndex,
                currentVertices,
                proposedLocalPositions,
                localIndexByVertex);
            Vector3 third = ReadBlendedPosition(
                blend,
                thirdIndex,
                currentVertices,
                proposedLocalPositions,
                localIndexByVertex);
            return Vector3.Dot(
                Vector3.Cross(second - first, third - first),
                baselineNormal);
        }

        private static Vector3 ReadBlendedPosition(
            float blend,
            int vertexIndex,
            IReadOnlyList<Vector3> currentVertices,
            IReadOnlyList<Vector3> proposedLocalPositions,
            IReadOnlyList<int> localIndexByVertex)
        {
            int localIndex = localIndexByVertex[vertexIndex];
            return localIndex < 0
                ? currentVertices[vertexIndex]
                : Vector3.LerpUnclamped(
                    currentVertices[vertexIndex],
                    proposedLocalPositions[localIndex],
                    blend);
        }

        private static double CalculateConstraintEnergy(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            IReadOnlyCollection<int> activePairIndices,
            IEnumerable<int> facePairIndices,
            IEnumerable<int> faceIndices,
            IEnumerable<LocalEdge> edges,
            bool shouldRecoverRestShape)
        {
            double energy = 0d;
            if (shouldRecoverRestShape)
            {
                foreach (LocalEdge edge in edges)
                {
                    if (edge.FirstLocalIndex < 0 || edge.SecondLocalIndex < 0)
                    {
                        continue;
                    }
                    float edgeLength = Vector3.Distance(
                        vertices[edge.FirstVertex],
                        vertices[edge.SecondVertex]);
                    energy += NativeSkinningRestShapeRecoveryCalculator
                        .CalculateSquaredStrainEnergy(
                            edgeLength,
                            edge.RestLength);
                }
                return energy;
            }

            foreach (int pairIndex in facePairIndices)
            {
                if (contract.RestAnglesDegrees[pairIndex] > RestSmoothAngleDegrees ||
                    !NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        contract.FacePairs[pairIndex],
                        out float angle))
                {
                    continue;
                }
                float targetAngle = activePairIndices.Contains(pairIndex)
                    ? ResolveProjectionAngleDegrees(
                        baselineVertices,
                        contract,
                        pairIndex)
                    : SharpFoldAngleDegrees;
                float violationRadians =
                    Mathf.Max(0f, angle - targetAngle) * Mathf.Deg2Rad;
                energy += 0.5d * BendingWeight *
                    violationRadians * violationRadians;
            }
            foreach (LocalEdge edge in edges)
            {
                float edgeLength = Vector3.Distance(
                    vertices[edge.FirstVertex],
                    vertices[edge.SecondVertex]);
                float strain = edgeLength / edge.BaselineLength - 1f;
                float violation = Mathf.Max(
                    0f,
                    Mathf.Abs(strain) - EdgeStrainActivation);
                energy += 0.5d * EdgeStrainBarrierWeight *
                    violation * violation;
            }

            float minimumNormalMagnitude = Mathf.Sqrt(MinimumNormalSquaredMagnitude);
            foreach (int faceIndex in faceIndices)
            {
                int offset = faceIndex * 3;
                int firstIndex = contract.Triangles[offset];
                int secondIndex = contract.Triangles[offset + 1];
                int thirdIndex = contract.Triangles[offset + 2];
                Vector3 baselineCross = Vector3.Cross(
                    baselineVertices[secondIndex] - baselineVertices[firstIndex],
                    baselineVertices[thirdIndex] - baselineVertices[firstIndex]);
                float baselineArea = baselineCross.magnitude;
                if (baselineArea < minimumNormalMagnitude)
                {
                    continue;
                }
                Vector3 baselineNormal = baselineCross / baselineArea;
                float signedArea = Vector3.Dot(
                    Vector3.Cross(
                        vertices[secondIndex] - vertices[firstIndex],
                        vertices[thirdIndex] - vertices[firstIndex]),
                    baselineNormal);
                float activationArea = baselineArea * OrientationActivationRatio;
                float violation = Mathf.Max(
                    0f,
                    (activationArea - signedArea) / activationArea);
                energy += 0.5d * OrientationBarrierWeight * violation * violation;
            }
            return energy;
        }

        private static void UpdateActiveSharpFoldPairs(
            IReadOnlyList<Vector3> vertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> facePairIndices,
            ISet<int> activePairIndices)
        {
            foreach (int pairIndex in facePairIndices)
            {
                float restAngleDegrees = contract.RestAnglesDegrees[pairIndex];
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                if (restAngleDegrees > RestSmoothAngleDegrees ||
                    !NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        vertices,
                        pair,
                        out float angleDegrees))
                {
                    continue;
                }

                if (angleDegrees > SharpFoldAngleDegrees ||
                    NativeSkinningRestShapeCollapseDetector.IsSeverelyCollapsed(
                        vertices,
                        pair,
                        contract.FacePairRestLengths[pairIndex],
                        restAngleDegrees,
                        angleDegrees))
                {
                    activePairIndices.Add(pairIndex);
                }
            }
        }

        private static void UpdateActiveCorrectionPairs(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> currentVertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> facePairIndices,
            bool shouldRecoverRestShape,
            ISet<int> activePairIndices)
        {
            foreach (int pairIndex in facePairIndices)
            {
                if (shouldRecoverRestShape)
                {
                    if (IsRestShapeRecoveryPair(
                            baselineVertices,
                            contract,
                            pairIndex))
                    {
                        activePairIndices.Add(pairIndex);
                    }
                    continue;
                }

                if (contract.RestAnglesDegrees[pairIndex] <=
                        RestSmoothAngleDegrees &&
                    NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        currentVertices,
                        contract.FacePairs[pairIndex],
                        out float angleDegrees) &&
                    angleDegrees > SharpFoldAngleDegrees)
                {
                    activePairIndices.Add(pairIndex);
                }
            }
        }

        private static float ResolveProjectionAngleDegrees(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            int pairIndex)
        {
            if (!IsRestShapeRecoveryPair(
                    baselineVertices,
                    contract,
                    pairIndex))
            {
                return ProjectionAngleDegrees;
            }

            NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                baselineVertices,
                contract.FacePairs[pairIndex],
                out float baselineAngleDegrees);
            return Mathf.Min(
                RestShapeRecoveryAngleDegrees,
                baselineAngleDegrees - RestShapeMinimumAngleImprovementDegrees);
        }

        private static bool IsRestShapeRecoveryPair(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            int pairIndex)
        {
            NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
            return NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                       baselineVertices,
                       pair,
                       out float baselineAngleDegrees) &&
                   baselineAngleDegrees <= SharpFoldAngleDegrees &&
                   NativeSkinningRestShapeCollapseDetector.IsSeverelyCollapsed(
                       baselineVertices,
                       pair,
                       contract.FacePairRestLengths[pairIndex],
                       contract.RestAnglesDegrees[pairIndex],
                       baselineAngleDegrees);
        }

        private static LocalWorkset BuildLocalWorkset(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> seedFacePairIndices,
            int ringCount)
        {
            var constrainedVertexSet = new HashSet<int>();
            foreach (int pairIndex in seedFacePairIndices)
            {
                NativeSkinningFacePair pair = contract.FacePairs[pairIndex];
                constrainedVertexSet.Add(pair.FirstOpposite);
                constrainedVertexSet.Add(pair.SecondOpposite);
                constrainedVertexSet.Add(pair.FirstEdge);
                constrainedVertexSet.Add(pair.SecondEdge);
            }
            var frontier = new HashSet<int>(constrainedVertexSet);
            for (int ring = 0; ring < ringCount && frontier.Count > 0; ring++)
            {
                int[] adjacentFaces = frontier
                    .SelectMany(index => contract.AffectedFaceIndicesByVertex[index])
                    .Distinct()
                    .ToArray();
                var nextFrontier = new HashSet<int>();
                foreach (int faceIndex in adjacentFaces)
                {
                    foreach (int vertexIndex in GetFaceVertices(
                                 contract.Triangles,
                                 faceIndex))
                    {
                        if (constrainedVertexSet.Add(vertexIndex))
                        {
                            nextFrontier.Add(vertexIndex);
                        }
                    }
                }
                frontier = nextFrontier;
            }

            int[] constrainedVertices = constrainedVertexSet
                .OrderBy(index => index)
                .ToArray();
            int[] localIndexByVertex = Enumerable
                .Repeat(-1, baselineVertices.Count)
                .ToArray();
            for (int localIndex = 0;
                 localIndex < constrainedVertices.Length;
                 localIndex++)
            {
                localIndexByVertex[constrainedVertices[localIndex]] = localIndex;
            }
            int[] facePairIndices = constrainedVertices
                .SelectMany(index => contract.FacePairIndicesByVertex[index])
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            int[] faceIndices = constrainedVertices
                .SelectMany(index => contract.AffectedFaceIndicesByVertex[index])
                .Distinct()
                .OrderBy(index => index)
                .ToArray();
            LocalEdge[] edges = BuildFrameEdges(baselineVertices, contract)
                .Where(edge =>
                    localIndexByVertex[edge.FirstVertex] >= 0 ||
                    localIndexByVertex[edge.SecondVertex] >= 0)
                .Select(edge => new LocalEdge(
                    edge.FirstVertex,
                    edge.SecondVertex,
                    localIndexByVertex[edge.FirstVertex],
                    localIndexByVertex[edge.SecondVertex],
                    edge.BaselineLength,
                    edge.RestLength))
                .ToArray();
            return new LocalWorkset(
                constrainedVertices,
                localIndexByVertex,
                facePairIndices,
                faceIndices,
                edges);
        }

        private static FrameEdge[] BuildFrameEdges(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract)
        {
            return contract.AffectedFaceIndices
                .SelectMany(faceIndex =>
                {
                    int[] vertices = GetFaceVertices(contract.Triangles, faceIndex);
                    return new[]
                    {
                        new NativeSkinningEdgeKey(vertices[0], vertices[1]),
                        new NativeSkinningEdgeKey(vertices[1], vertices[2]),
                        new NativeSkinningEdgeKey(vertices[2], vertices[0])
                    };
                })
                .Distinct()
                .Select(edge => new FrameEdge(
                    edge.First,
                    edge.Second,
                    Vector3.Distance(
                        baselineVertices[edge.First],
                        baselineVertices[edge.Second]),
                    Vector3.Distance(
                        contract.RestVertices[edge.First],
                        contract.RestVertices[edge.Second])))
                .Where(edge =>
                    edge.BaselineLength > 0.00000001f &&
                    edge.RestLength > 0.00000001f)
                .ToArray();
        }

        private static int[] GetFaceVertices(
            IReadOnlyList<int> triangles,
            int faceIndex)
        {
            int offset = faceIndex * 3;
            return new[]
            {
                triangles[offset],
                triangles[offset + 1],
                triangles[offset + 2]
            };
        }

        private static bool HasOrientationDefect(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> candidateVertices,
            NativeSkinningSurfaceContract contract,
            IEnumerable<int> faceIndices)
        {
            foreach (int faceIndex in faceIndices)
            {
                if (!TryCalculateFaceNormal(
                        baselineVertices,
                        contract.Triangles,
                        faceIndex,
                        out Vector3 baselineNormal))
                {
                    continue;
                }
                if (!TryCalculateFaceNormal(
                        candidateVertices,
                        contract.Triangles,
                        faceIndex,
                        out Vector3 candidateNormal) ||
                    Vector3.Dot(baselineNormal, candidateNormal) <= 0f)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryCalculateFaceNormal(
            IReadOnlyList<Vector3> vertices,
            IReadOnlyList<int> triangles,
            int faceIndex,
            out Vector3 normal)
        {
            int offset = faceIndex * 3;
            Vector3 first = vertices[triangles[offset]];
            Vector3 second = vertices[triangles[offset + 1]];
            Vector3 third = vertices[triangles[offset + 2]];
            normal = Vector3.Cross(second - first, third - first);
            if (normal.sqrMagnitude < MinimumNormalSquaredMagnitude)
            {
                normal = Vector3.zero;
                return false;
            }
            normal /= Mathf.Sqrt(normal.sqrMagnitude);
            return true;
        }

        private static float CalculateFacePairCharacteristicLength(
            IReadOnlyList<Vector3> vertices,
            NativeSkinningFacePair pair)
        {
            float minimumLength = Vector3.Distance(
                vertices[pair.FirstEdge],
                vertices[pair.SecondEdge]);
            minimumLength = Mathf.Min(
                minimumLength,
                Vector3.Distance(
                    vertices[pair.FirstOpposite],
                    vertices[pair.FirstEdge]));
            minimumLength = Mathf.Min(
                minimumLength,
                Vector3.Distance(
                    vertices[pair.FirstOpposite],
                    vertices[pair.SecondEdge]));
            minimumLength = Mathf.Min(
                minimumLength,
                Vector3.Distance(
                    vertices[pair.SecondOpposite],
                    vertices[pair.FirstEdge]));
            return Mathf.Min(
                minimumLength,
                Vector3.Distance(
                    vertices[pair.SecondOpposite],
                    vertices[pair.SecondEdge]));
        }

        private static Vector3 ClampDisplacementFromBaseline(
            Vector3 baseline,
            Vector3 candidate,
            float maximumDistance)
        {
            Vector3 displacement = candidate - baseline;
            float squaredDistance = displacement.sqrMagnitude;
            float squaredMaximumDistance = maximumDistance * maximumDistance;
            if (squaredDistance <= squaredMaximumDistance || squaredDistance <= 0f)
            {
                return candidate;
            }
            return baseline + displacement *
                (maximumDistance / Mathf.Sqrt(squaredDistance));
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct CorrectionConfiguration
        {
            internal CorrectionConfiguration(
                int localGlobalIterations,
                int conjugateGradientIterations,
                int edgeLengthProjectionIterations,
                float maximumEdgeLengthStrain,
                float maximumTotalDisplacementToArmLengthRatio)
            {
                LocalGlobalIterations = localGlobalIterations;
                ConjugateGradientIterations = conjugateGradientIterations;
                EdgeLengthProjectionIterations = edgeLengthProjectionIterations;
                MaximumEdgeLengthStrain = maximumEdgeLengthStrain;
                MaximumTotalDisplacementToArmLengthRatio =
                    maximumTotalDisplacementToArmLengthRatio;
            }

            internal int LocalGlobalIterations { get; }
            internal int ConjugateGradientIterations { get; }
            internal int EdgeLengthProjectionIterations { get; }
            internal float MaximumEdgeLengthStrain { get; }
            internal float MaximumTotalDisplacementToArmLengthRatio { get; }
        }

        private readonly struct FrameEdge
        {
            internal FrameEdge(
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

        private readonly struct LocalEdge
        {
            internal LocalEdge(
                int firstVertex,
                int secondVertex,
                int firstLocalIndex,
                int secondLocalIndex,
                float baselineLength,
                float restLength)
            {
                FirstVertex = firstVertex;
                SecondVertex = secondVertex;
                FirstLocalIndex = firstLocalIndex;
                SecondLocalIndex = secondLocalIndex;
                BaselineLength = baselineLength;
                RestLength = restLength;
            }

            internal int FirstVertex { get; }
            internal int SecondVertex { get; }
            internal int FirstLocalIndex { get; }
            internal int SecondLocalIndex { get; }
            internal float BaselineLength { get; }
            internal float RestLength { get; }
        }

        private readonly struct LocalWorkset
        {
            internal LocalWorkset(
                int[] constrainedVertices,
                int[] localIndexByVertex,
                int[] facePairIndices,
                int[] faceIndices,
                LocalEdge[] edges)
            {
                ConstrainedVertices = constrainedVertices;
                LocalIndexByVertex = localIndexByVertex;
                FacePairIndices = facePairIndices;
                FaceIndices = faceIndices;
                Edges = edges;
            }

            internal int[] ConstrainedVertices { get; }
            internal int[] LocalIndexByVertex { get; }
            internal int[] FacePairIndices { get; }
            internal int[] FaceIndices { get; }
            internal LocalEdge[] Edges { get; }
        }

        private readonly struct BendingLinearization
        {
            internal BendingLinearization(int[] localIndices, Vector3[] gradients)
            {
                LocalIndices = localIndices;
                Gradients = gradients;
            }

            internal int[] LocalIndices { get; }
            internal Vector3[] Gradients { get; }
        }

        private readonly struct EdgeStrainLinearization
        {
            internal EdgeStrainLinearization(
                int firstLocalIndex,
                int secondLocalIndex,
                Vector3 firstGradient,
                Vector3 secondGradient)
            {
                FirstLocalIndex = firstLocalIndex;
                SecondLocalIndex = secondLocalIndex;
                FirstGradient = firstGradient;
                SecondGradient = secondGradient;
            }

            internal int FirstLocalIndex { get; }
            internal int SecondLocalIndex { get; }
            internal Vector3 FirstGradient { get; }
            internal Vector3 SecondGradient { get; }
        }
    }
}
