using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
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
        private const float RestSmoothAngleDegrees =
            NativeSkinningSurfaceDefectDetector.MaximumCorrectableRestAngleDegrees;
        private const float SharpFoldAngleDegrees =
            NativeSkinningSurfaceDefectDetector.MinimumSharpFoldAngleDegrees;
        private const float ProjectionAngleDegrees = 145f;
        private const float RestShapeRecoveryAngleDegrees = 140f;
        private const float RestShapeMinimumAngleImprovementDegrees = 5f;
        private const float RestShapeActiveSetActivationMargin = 0.0005f;
        private const float RestShapeMaximumProjectionCorrection = 0.000025f;
        private const int RestShapeActiveSetProjectionPasses = 8;
        private const int DefaultWorksetRingCount = 2;
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
        // 상대 에너지 개선이 이 값 미만이면 LG 반복을 조기 종료한다.
        private const double EarlyConvergenceRelativeImprovement = 0.001;
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
            new CorrectionConfiguration(640, 64, 32, 0.03f, 0.055f);
        // 예산 크기에 따라 수렴 지점이 달라지므로 국소 최적해 정체 시 축소 예산으로 재시도한다.
        private static readonly CorrectionConfiguration StrongRetryConfiguration =
            new CorrectionConfiguration(640, 64, 32, 0.03f, 0.02f);
        // 반복 투영이 신장 경계에서 정체하는 경우가 있어 충분한 헤드룸과 넓은 workset으로 재검색한다.
        // 2링은 변위가 좁은 영역에 집중돼 신장 한계에 먼저 도달하므로 3링으로 넓혀 분산한다.
        // 경계 근처(0.035)는 다시 경계에 갇히므로 0.05까지 벌려 경계 끌림을 벗어난다.
        // 수용 결과는 엄격한 게이트로 다시 검증하므로 실질 품질 기준은 유지된다.
        private static readonly CorrectionConfiguration StrongHeadroomConfiguration =
            new CorrectionConfiguration(640, 64, 32, 0.05f, 0.055f, 3);
        // 조기 수렴·웜스타트는 fast 경로에만 허용한다. strong은 정밀 fallback이라 기준 궤적을 유지한다.
        // Burst 경로는 계측에서 회귀(CG 호출당 네이티브 포장 비용이 커널 이득을 상쇄)가
        // 확인돼 비활성으로 둔다 — 코드는 fast 전용 게이트 뒤에 보존한다.
        private static readonly CorrectionConfiguration FastConfiguration =
            new CorrectionConfiguration(80, 16, 0, 0.02f, 0.002512f,
                DefaultWorksetRingCount,
                allowsEarlyConvergence: true,
                allowsWarmStart: true,
                allowsBurst: false);

        internal static bool TryCalculateFast(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateTimed(
                baselineVertices,
                baselineVertices,
                contract,
                FastConfiguration,
                false,
                true,
                null,
                out result);
        }

        // 계측 경로 — 리플렉션 호출과 이름 충돌하지 않게 별도 명명한다.
        internal static bool TryCalculateFastTimed(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            NativeSkinningSolverTiming timing,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateTimed(
                baselineVertices,
                baselineVertices,
                contract,
                FastConfiguration,
                false,
                true,
                timing,
                out result);
        }

        internal static bool TryCalculateFastValidatedFrame(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateTimed(
                baselineVertices,
                baselineVertices,
                contract,
                FastConfiguration,
                false,
                false,
                null,
                out result);
        }

        internal static bool TryCalculateFastValidatedFrameTimed(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            NativeSkinningSolverTiming timing,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateTimed(
                baselineVertices,
                baselineVertices,
                contract,
                FastConfiguration,
                false,
                false,
                timing,
                out result);
        }

        internal static bool TryCalculateStrong(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateTimed(
                baselineVertices,
                baselineVertices,
                contract,
                StrongConfiguration,
                false,
                true,
                null,
                out result);
        }

        internal static bool TryCalculateStrongTimed(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            NativeSkinningSolverTiming timing,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateTimed(
                baselineVertices,
                baselineVertices,
                contract,
                StrongConfiguration,
                false,
                true,
                timing,
                out result);
        }

        internal static bool TryCalculateStrongValidatedFrame(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateStrongValidatedFrameTimed(
                baselineVertices, contract, null, out result);
        }

        internal static bool TryCalculateStrongValidatedFrameTimed(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            NativeSkinningSolverTiming timing,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            if (!TryCalculateTimed(
                    baselineVertices,
                    baselineVertices,
                    contract,
                    StrongConfiguration,
                    false,
                    false,
                    timing,
                    out result))
            {
                return false;
            }
            if (IsWithinQuality(result, contract, StrongConfiguration))
            {
                return true;
            }
            // 축소 예산 재시도가 더 엄격한 품질 게이트를 통과하면 그 결과를 채택한다.
            if (TryCalculateTimed(
                    baselineVertices,
                    baselineVertices,
                    contract,
                    StrongRetryConfiguration,
                    false,
                    false,
                    timing,
                    out NativeSkinningSurfaceCorrectionResult retry) &&
                IsWithinQuality(retry, contract, StrongRetryConfiguration))
            {
                result = retry;
            }
            if (!IsWithinQuality(result, contract, StrongConfiguration) &&
                TryCalculateTimed(
                    baselineVertices,
                    baselineVertices,
                    contract,
                    StrongHeadroomConfiguration,
                    false,
                    false,
                    timing,
                    out NativeSkinningSurfaceCorrectionResult headroom) &&
                IsWithinQuality(headroom, contract, StrongConfiguration))
            {
                result = headroom;
            }
            // 예산과 무관하게 국소 최적해에 정체하는 입력이 있어
            // 직전 결과를 초기 추정으로 재투입해 basin 이탈을 시도한다.
            if (!IsWithinQuality(result, contract, StrongConfiguration) &&
                result?.CorrectedVertices != null &&
                TryCalculateTimed(
                    baselineVertices,
                    result.CorrectedVertices,
                    contract,
                    StrongConfiguration,
                    false,
                    false,
                    timing,
                    out NativeSkinningSurfaceCorrectionResult chained) &&
                IsWithinQuality(chained, contract, StrongConfiguration))
            {
                result = chained;
            }
            return true;
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

        // 진단 테스트가 이 7인자 시그니처를 리플렉션으로 호출하므로 이름·인수를 유지한다.
        private static bool TryCalculate(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> initialVertices,
            NativeSkinningSurfaceContract contract,
            CorrectionConfiguration configuration,
            bool allowsStrainRecovery,
            bool shouldValidateVertices,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            return TryCalculateTimed(
                baselineVertices,
                initialVertices,
                contract,
                configuration,
                allowsStrainRecovery,
                shouldValidateVertices,
                null,
                out result);
        }

        private static bool TryCalculateTimed(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> initialVertices,
            NativeSkinningSurfaceContract contract,
            CorrectionConfiguration configuration,
            bool allowsStrainRecovery,
            bool shouldValidateVertices,
            NativeSkinningSolverTiming timing,
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
            var buffers = new SolverBuffers();
            if (restShapeRecoveryPairIndices.Length > 0)
            {
                vertices = ApplyCorrectionPass(
                    baselineVertices,
                    vertices,
                    contract,
                    configuration,
                    restShapeRecoveryPairIndices,
                    true,
                    allowsStrainRecovery,
                    frameEdges,
                    buffers,
                    timing);
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
                    restShapeRecoveryPairIndices.Length > 0,
                    frameEdges,
                    buffers,
                    timing);
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
                    buffers,
                    timing,
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
            SolverBuffers buffers,
            NativeSkinningSolverTiming timing,
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
                    allowsStrainRecovery,
                    frameEdges,
                    buffers,
                    timing)
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
            bool allowsStrainRecovery,
            IReadOnlyList<FrameEdge> frameEdges,
            SolverBuffers buffers,
            NativeSkinningSolverTiming timing)
        {
            Vector3[] vertices = initialVertices;
            var activePairIndices = new HashSet<int>(seedPairIndices);
            long sectionStamp = NativeSkinningSolverTiming.Stamp();
            LocalWorkset workset = BuildLocalWorkset(
                baselineVertices,
                contract,
                activePairIndices,
                configuration.WorksetRingCount,
                frameEdges,
                buffers);
            timing?.AddSetup(sectionStamp);
            buffers.EnsureLocalSize(workset.ConstrainedVertices.Length);
            buffers.EnsureVertexSize(vertices.Length);
            for (int iteration = 0;
                 iteration < configuration.LocalGlobalIterations;
                 iteration++)
            {
                timing?.AddLgIteration();
                sectionStamp = NativeSkinningSolverTiming.Stamp();
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
                timing?.AddPairEnergy(sectionStamp);
                if (beforeEnergy <= 0d)
                {
                    break;
                }

                sectionStamp = NativeSkinningSolverTiming.Stamp();
                BuildDisplacementSystem(
                    baselineVertices,
                    vertices,
                    contract,
                    workset,
                    activePairIndices,
                    shouldRecoverRestShape,
                    buffers,
                    out NativeSkinningMaterialStrainConstraint[]
                        materialStrainConstraints,
                    out int activeConstraintCount);
                timing?.AddBuildSystem(sectionStamp);
                if (activeConstraintCount == 0)
                {
                    break;
                }

                sectionStamp = NativeSkinningSolverTiming.Stamp();
                IReadOnlyList<LocalEdge> cgEdges = shouldRecoverRestShape
                    ? (IReadOnlyList<LocalEdge>)Array.Empty<LocalEdge>()
                    : workset.Edges;
                bool warmStart =
                    configuration.AllowsWarmStart && iteration > 0;
                Vector3[] displacement = configuration.AllowsBurst
                    ? SolveConjugateGradientBurst(
                        buffers,
                        cgEdges,
                        materialStrainConstraints,
                        workset.ConstrainedVertices.Length,
                        configuration.ConjugateGradientIterations,
                        warmStart,
                        timing)
                    : SolveConjugateGradient(
                        buffers,
                        cgEdges,
                        materialStrainConstraints,
                        workset.ConstrainedVertices.Length,
                        configuration.ConjugateGradientIterations,
                        warmStart,
                        timing);
                timing?.AddCgSolve(sectionStamp);
                sectionStamp = NativeSkinningSolverTiming.Stamp();
                LimitDisplacement(
                    displacement,
                    workset.ConstrainedVertices.Length,
                    vertices,
                    contract,
                    activePairIndices);
                Vector3[] proposedLocalPositions = buffers.ProposedLocal;
                for (int localIndex = 0;
                     localIndex < workset.ConstrainedVertices.Length;
                     localIndex++)
                {
                    proposedLocalPositions[localIndex] =
                        vertices[workset.ConstrainedVertices[localIndex]] +
                        displacement[localIndex];
                }
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
                        buffers.CandidateFor(vertices),
                        out Vector3[] acceptedVertices,
                        out double acceptedEnergy))
                {
                    timing?.AddProjectAccept(sectionStamp);
                    break;
                }
                timing?.AddProjectAccept(sectionStamp);
                vertices = acceptedVertices;
                // 상대 개선폭이 수렴 임계 미만이면 꼬리 반복을 생략한다.
                if (configuration.AllowsEarlyConvergence &&
                    (beforeEnergy - acceptedEnergy) / beforeEnergy <
                    EarlyConvergenceRelativeImprovement)
                {
                    break;
                }
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
                   contract.RestShapeRecoveryMaximumCosines != null &&
                   contract.RestShapeRecoveryMaximumCosines.Length ==
                       contract.FacePairs.Length &&
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
            // vertices는 재사용 버퍼를 별칭할 수 있어 fallback 덮어쓰기를 막기 위해 스냅샷한다.
            return new NativeSkinningSurfaceCorrectionResult(
                (Vector3[])vertices.Clone(),
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
            SolverBuffers buffers,
            out NativeSkinningMaterialStrainConstraint[] materialStrainConstraints,
            out int activeConstraintCount)
        {
            int count = workset.ConstrainedVertices.Length;
            float[] diagonal = buffers.Diagonal;
            Array.Clear(diagonal, 0, count);
            Vector3[] rightHandSide = buffers.RightHandSide;
            Array.Clear(rightHandSide, 0, count);
            List<BendingLinearization> bendingTerms = buffers.BendingTerms;
            bendingTerms.Clear();
            List<EdgeStrainLinearization> edgeStrainTerms = buffers.EdgeStrainTerms;
            edgeStrainTerms.Clear();
            for (int localIndex = 0; localIndex < count; localIndex++)
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
                if (!activePairIndices.Contains(pairIndex) ||
                    !IsSharpFoldCorrectionEligible(
                        baselineVertices,
                        contract,
                        pairIndex))
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
                bendingTerms.Add(new BendingLinearization(
                    workset.LocalIndexByVertex[pair.FirstOpposite],
                    workset.LocalIndexByVertex[pair.SecondOpposite],
                    workset.LocalIndexByVertex[pair.FirstEdge],
                    workset.LocalIndexByVertex[pair.SecondEdge],
                    correction0 * gradientScale,
                    correction1 * gradientScale,
                    correction2 * gradientScale,
                    correction3 * gradientScale));
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
            float[] preconditionerDiagonal = buffers.Preconditioner;
            Array.Copy(diagonal, preconditionerDiagonal, count);
            foreach (BendingLinearization term in bendingTerms)
            {
                for (int index = 0; index < 4; index++)
                {
                    int localIndex = term.LocalIndexAt(index);
                    if (localIndex >= 0)
                    {
                        preconditionerDiagonal[localIndex] +=
                            BendingWeight * term.GradientAt(index).sqrMagnitude;
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
            SolverBuffers buffers,
            IReadOnlyList<LocalEdge> edges,
            IReadOnlyList<NativeSkinningMaterialStrainConstraint>
                materialStrainConstraints,
            int count,
            int maximumIterations,
            bool warmStart,
            NativeSkinningSolverTiming timing)
        {
            timing?.AddCgCall();
            float[] diagonal = buffers.Diagonal;
            float[] preconditionerDiagonal = buffers.Preconditioner;
            Vector3[] rightHandSide = buffers.RightHandSide;
            Vector3[] solution = buffers.Solution;
            Vector3[] residual = buffers.Residual;
            Vector3[] direction = buffers.Direction;
            Vector3[] preconditionedResidual = buffers.PreconditionedResidual;
            Vector3[] matrixDirection = buffers.MatrixDirection;
            List<BendingLinearization> bendingLinearizations =
                buffers.BendingTerms;
            List<EdgeStrainLinearization> edgeStrainLinearizations =
                buffers.EdgeStrainTerms;
            if (warmStart)
            {
                // 직전 반복 해법을 x0로 — 잔차는 우변−A·x0를 실제 계산한다.
                ApplyMatrixInto(
                    solution,
                    diagonal,
                    edges,
                    bendingLinearizations,
                    edgeStrainLinearizations,
                    materialStrainConstraints,
                    matrixDirection,
                    count);
                for (int index = 0; index < count; index++)
                {
                    residual[index] =
                        rightHandSide[index] - matrixDirection[index];
                }
            }
            else
            {
                // x0=0이면 A·x0=0이라 잔차는 우변 그대로 — 첫 ApplyMatrix를 생략한다.
                Array.Clear(solution, 0, count);
                Array.Copy(rightHandSide, residual, count);
            }
            ApplyDiagonalPreconditionerInto(
                residual,
                preconditionerDiagonal,
                direction,
                count);
            double residualDot = Dot(residual, direction, count);
            for (int iteration = 0;
                 iteration < maximumIterations && residualDot > 1e-20d;
                 iteration++)
            {
                timing?.AddCgIteration();
                ApplyMatrixInto(
                    direction,
                    diagonal,
                    edges,
                    bendingLinearizations,
                    edgeStrainLinearizations,
                    materialStrainConstraints,
                    matrixDirection,
                    count);
                double denominator = Dot(direction, matrixDirection, count);
                if (Math.Abs(denominator) <= 1e-20d)
                {
                    break;
                }
                float step = (float)(residualDot / denominator);
                for (int index = 0; index < count; index++)
                {
                    solution[index] += direction[index] * step;
                    residual[index] -= matrixDirection[index] * step;
                }
                if (Dot(residual, residual, count) <= 1e-18d)
                {
                    break;
                }
                ApplyDiagonalPreconditionerInto(
                    residual,
                    preconditionerDiagonal,
                    preconditionedResidual,
                    count);
                double nextResidualDot = Dot(
                    residual,
                    preconditionedResidual,
                    count);
                float beta = (float)(nextResidualDot / residualDot);
                for (int index = 0; index < count; index++)
                {
                    direction[index] = preconditionedResidual[index] +
                        direction[index] * beta;
                }
                residualDot = nextResidualDot;
            }
            return solution;
        }

        // fast 전용 Burst 경로 — CG 한 호출을 하나의 Job으로 실행해
        // 반복마다 스케줄 비용을 치르지 않는다. 포장·해제는 호출당 1회.
        // 포장이나 실행이 실패하면 managed 경로로 되돌아가 결과 계약을 유지한다.
        private static Vector3[] SolveConjugateGradientBurst(
            SolverBuffers buffers,
            IReadOnlyList<LocalEdge> edges,
            IReadOnlyList<NativeSkinningMaterialStrainConstraint>
                materialStrainConstraints,
            int count,
            int maximumIterations,
            bool warmStart,
            NativeSkinningSolverTiming timing)
        {
            timing?.AddCgCall();
            int edgeCount = edges.Count;
            int bendCount = buffers.BendingTerms.Count;
            int strainCount = buffers.EdgeStrainTerms.Count;
            int materialCount = materialStrainConstraints.Count;
            int materialTermTotal = 0;
            for (int index = 0; index < materialCount; index++)
            {
                materialTermTotal +=
                    materialStrainConstraints[index].LocalIndices.Length;
            }

            NativeArray<float> diagonal = default;
            NativeArray<float> preconditioner = default;
            NativeArray<Vector3> rightHandSide = default;
            NativeArray<Vector3> solution = default;
            NativeArray<int> iterationsRun = default;
            NativeArray<int2> edgeIndices = default;
            NativeArray<int> bendIndices = default;
            NativeArray<Vector3> bendGradients = default;
            NativeArray<int2> strainIndices = default;
            NativeArray<Vector3> strainFirstGradients = default;
            NativeArray<Vector3> strainSecondGradients = default;
            NativeArray<int> materialOffsets = default;
            NativeArray<int> materialIndices = default;
            NativeArray<float> materialCoefficients = default;
            NativeArray<float> materialWeights = default;
            try
            {
                diagonal = new NativeArray<float>(count, Allocator.TempJob);
                preconditioner =
                    new NativeArray<float>(count, Allocator.TempJob);
                rightHandSide =
                    new NativeArray<Vector3>(count, Allocator.TempJob);
                solution = new NativeArray<Vector3>(count, Allocator.TempJob);
                iterationsRun = new NativeArray<int>(1, Allocator.TempJob);
                edgeIndices =
                    new NativeArray<int2>(edgeCount, Allocator.TempJob);
                bendIndices =
                    new NativeArray<int>(bendCount * 4, Allocator.TempJob);
                bendGradients = new NativeArray<Vector3>(
                    bendCount * 4,
                    Allocator.TempJob);
                strainIndices =
                    new NativeArray<int2>(strainCount, Allocator.TempJob);
                strainFirstGradients = new NativeArray<Vector3>(
                    strainCount,
                    Allocator.TempJob);
                strainSecondGradients = new NativeArray<Vector3>(
                    strainCount,
                    Allocator.TempJob);
                materialOffsets =
                    new NativeArray<int>(materialCount + 1, Allocator.TempJob);
                materialIndices =
                    new NativeArray<int>(materialTermTotal, Allocator.TempJob);
                materialCoefficients =
                    new NativeArray<float>(materialTermTotal, Allocator.TempJob);
                materialWeights =
                    new NativeArray<float>(materialCount, Allocator.TempJob);

                NativeArray<float>.Copy(
                    buffers.Diagonal, 0, diagonal, 0, count);
                NativeArray<float>.Copy(
                    buffers.Preconditioner, 0, preconditioner, 0, count);
                NativeArray<Vector3>.Copy(
                    buffers.RightHandSide, 0, rightHandSide, 0, count);
                if (warmStart)
                {
                    NativeArray<Vector3>.Copy(
                        buffers.Solution, 0, solution, 0, count);
                }
                for (int index = 0; index < edgeCount; index++)
                {
                    LocalEdge edge = edges[index];
                    edgeIndices[index] = new int2(
                        edge.FirstLocalIndex,
                        edge.SecondLocalIndex);
                }
                for (int index = 0; index < bendCount; index++)
                {
                    BendingLinearization term = buffers.BendingTerms[index];
                    bendIndices[index * 4] = term.Index0;
                    bendIndices[index * 4 + 1] = term.Index1;
                    bendIndices[index * 4 + 2] = term.Index2;
                    bendIndices[index * 4 + 3] = term.Index3;
                    bendGradients[index * 4] = term.Gradient0;
                    bendGradients[index * 4 + 1] = term.Gradient1;
                    bendGradients[index * 4 + 2] = term.Gradient2;
                    bendGradients[index * 4 + 3] = term.Gradient3;
                }
                for (int index = 0; index < strainCount; index++)
                {
                    EdgeStrainLinearization term =
                        buffers.EdgeStrainTerms[index];
                    strainIndices[index] = new int2(
                        term.FirstLocalIndex,
                        term.SecondLocalIndex);
                    strainFirstGradients[index] = term.FirstGradient;
                    strainSecondGradients[index] = term.SecondGradient;
                }
                int materialOffset = 0;
                for (int index = 0; index < materialCount; index++)
                {
                    NativeSkinningMaterialStrainConstraint constraint =
                        materialStrainConstraints[index];
                    materialOffsets[index] = materialOffset;
                    materialWeights[index] = constraint.Weight;
                    for (int term = 0;
                         term < constraint.LocalIndices.Length;
                         term++)
                    {
                        materialIndices[materialOffset + term] =
                            constraint.LocalIndices[term];
                        materialCoefficients[materialOffset + term] =
                            constraint.Coefficients[term];
                    }
                    materialOffset += constraint.LocalIndices.Length;
                }
                materialOffsets[materialCount] = materialOffset;

                new CgSolveJob
                {
                    Count = count,
                    MaximumIterations = maximumIterations,
                    WarmStart = warmStart ? (byte)1 : (byte)0,
                    Diagonal = diagonal,
                    Preconditioner = preconditioner,
                    RightHandSide = rightHandSide,
                    Solution = solution,
                    IterationsRun = iterationsRun,
                    EdgeIndices = edgeIndices,
                    BendIndices = bendIndices,
                    BendGradients = bendGradients,
                    StrainIndices = strainIndices,
                    StrainFirstGradients = strainFirstGradients,
                    StrainSecondGradients = strainSecondGradients,
                    MaterialOffsets = materialOffsets,
                    MaterialIndices = materialIndices,
                    MaterialCoefficients = materialCoefficients,
                    MaterialWeights = materialWeights,
                }.Schedule().Complete();
                timing?.AddCgIterations(iterationsRun[0]);
                NativeArray<Vector3>.Copy(
                    solution, 0, buffers.Solution, 0, count);
            }
            catch (Exception)
            {
                // 네이티브 경로 실패 시 managed 솔버로 동일 입력을 다시 푼다.
                return SolveConjugateGradient(
                    buffers,
                    edges,
                    materialStrainConstraints,
                    count,
                    maximumIterations,
                    warmStart,
                    timing);
            }
            finally
            {
                if (diagonal.IsCreated) diagonal.Dispose();
                if (preconditioner.IsCreated) preconditioner.Dispose();
                if (rightHandSide.IsCreated) rightHandSide.Dispose();
                if (solution.IsCreated) solution.Dispose();
                if (iterationsRun.IsCreated) iterationsRun.Dispose();
                if (edgeIndices.IsCreated) edgeIndices.Dispose();
                if (bendIndices.IsCreated) bendIndices.Dispose();
                if (bendGradients.IsCreated) bendGradients.Dispose();
                if (strainIndices.IsCreated) strainIndices.Dispose();
                if (strainFirstGradients.IsCreated)
                {
                    strainFirstGradients.Dispose();
                }
                if (strainSecondGradients.IsCreated)
                {
                    strainSecondGradients.Dispose();
                }
                if (materialOffsets.IsCreated) materialOffsets.Dispose();
                if (materialIndices.IsCreated) materialIndices.Dispose();
                if (materialCoefficients.IsCreated)
                {
                    materialCoefficients.Dispose();
                }
                if (materialWeights.IsCreated) materialWeights.Dispose();
            }
            return buffers.Solution;
        }

        [BurstCompile]
        private struct CgSolveJob : IJob
        {
            internal int Count;
            internal int MaximumIterations;
            internal byte WarmStart;
            [ReadOnly] internal NativeArray<float> Diagonal;
            [ReadOnly] internal NativeArray<float> Preconditioner;
            [ReadOnly] internal NativeArray<Vector3> RightHandSide;
            internal NativeArray<Vector3> Solution;
            internal NativeArray<int> IterationsRun;
            [ReadOnly] internal NativeArray<int2> EdgeIndices;
            [ReadOnly] internal NativeArray<int> BendIndices;
            [ReadOnly] internal NativeArray<Vector3> BendGradients;
            [ReadOnly] internal NativeArray<int2> StrainIndices;
            [ReadOnly] internal NativeArray<Vector3> StrainFirstGradients;
            [ReadOnly] internal NativeArray<Vector3> StrainSecondGradients;
            [ReadOnly] internal NativeArray<int> MaterialOffsets;
            [ReadOnly] internal NativeArray<int> MaterialIndices;
            [ReadOnly] internal NativeArray<float> MaterialCoefficients;
            [ReadOnly] internal NativeArray<float> MaterialWeights;

            public void Execute()
            {
                var residual =
                    new NativeArray<Vector3>(Count, Allocator.Temp);
                var direction =
                    new NativeArray<Vector3>(Count, Allocator.Temp);
                var preconditionedResidual =
                    new NativeArray<Vector3>(Count, Allocator.Temp);
                var matrixDirection =
                    new NativeArray<Vector3>(Count, Allocator.Temp);
                if (WarmStart != 0)
                {
                    ApplyMatrix(Solution, matrixDirection);
                    for (int index = 0; index < Count; index++)
                    {
                        residual[index] = RightHandSide[index] -
                            matrixDirection[index];
                    }
                }
                else
                {
                    for (int index = 0; index < Count; index++)
                    {
                        Solution[index] = Vector3.zero;
                        residual[index] = RightHandSide[index];
                    }
                }
                ApplyPreconditioner(residual, direction);
                double residualDot = Dot(residual, direction);
                int run = 0;
                for (int iteration = 0;
                     iteration < MaximumIterations && residualDot > 1e-20d;
                     iteration++)
                {
                    run++;
                    ApplyMatrix(direction, matrixDirection);
                    double denominator = Dot(direction, matrixDirection);
                    if (math.abs(denominator) <= 1e-20d)
                    {
                        break;
                    }
                    float step = (float)(residualDot / denominator);
                    for (int index = 0; index < Count; index++)
                    {
                        Solution[index] += direction[index] * step;
                        residual[index] -= matrixDirection[index] * step;
                    }
                    if (Dot(residual, residual) <= 1e-18d)
                    {
                        break;
                    }
                    ApplyPreconditioner(residual, preconditionedResidual);
                    double nextResidualDot =
                        Dot(residual, preconditionedResidual);
                    float beta = (float)(nextResidualDot / residualDot);
                    for (int index = 0; index < Count; index++)
                    {
                        direction[index] = preconditionedResidual[index] +
                            direction[index] * beta;
                    }
                    residualDot = nextResidualDot;
                }
                IterationsRun[0] = run;
                residual.Dispose();
                direction.Dispose();
                preconditionedResidual.Dispose();
                matrixDirection.Dispose();
            }

            private void ApplyMatrix(
                NativeArray<Vector3> values,
                NativeArray<Vector3> result)
            {
                for (int index = 0; index < Count; index++)
                {
                    result[index] = values[index] * Diagonal[index];
                }
                for (int index = 0; index < EdgeIndices.Length; index++)
                {
                    int2 edge = EdgeIndices[index];
                    if (edge.x >= 0 && edge.y >= 0)
                    {
                        result[edge.x] -= values[edge.y] *
                            DisplacementSmoothnessWeight;
                        result[edge.y] -= values[edge.x] *
                            DisplacementSmoothnessWeight;
                    }
                }
                int bendCount = BendIndices.Length / 4;
                for (int term = 0; term < bendCount; term++)
                {
                    float projectedDisplacement = 0f;
                    for (int index = 0; index < 4; index++)
                    {
                        int localIndex = BendIndices[term * 4 + index];
                        if (localIndex >= 0)
                        {
                            projectedDisplacement += Vector3.Dot(
                                BendGradients[term * 4 + index],
                                values[localIndex]);
                        }
                    }
                    for (int index = 0; index < 4; index++)
                    {
                        int localIndex = BendIndices[term * 4 + index];
                        if (localIndex >= 0)
                        {
                            result[localIndex] += BendingWeight *
                                BendGradients[term * 4 + index] *
                                projectedDisplacement;
                        }
                    }
                }
                for (int term = 0; term < StrainIndices.Length; term++)
                {
                    int2 indices = StrainIndices[term];
                    float projectedDisplacement = 0f;
                    if (indices.x >= 0)
                    {
                        projectedDisplacement += Vector3.Dot(
                            StrainFirstGradients[term],
                            values[indices.x]);
                    }
                    if (indices.y >= 0)
                    {
                        projectedDisplacement += Vector3.Dot(
                            StrainSecondGradients[term],
                            values[indices.y]);
                    }
                    if (indices.x >= 0)
                    {
                        result[indices.x] += EdgeStrainBarrierWeight *
                            StrainFirstGradients[term] *
                            projectedDisplacement;
                    }
                    if (indices.y >= 0)
                    {
                        result[indices.y] += EdgeStrainBarrierWeight *
                            StrainSecondGradients[term] *
                            projectedDisplacement;
                    }
                }
                for (int constraint = 0;
                     constraint < MaterialWeights.Length;
                     constraint++)
                {
                    int start = MaterialOffsets[constraint];
                    int end = MaterialOffsets[constraint + 1];
                    Vector3 projectedDisplacement = Vector3.zero;
                    for (int index = start; index < end; index++)
                    {
                        projectedDisplacement +=
                            values[MaterialIndices[index]] *
                            MaterialCoefficients[index];
                    }
                    float weight = MaterialWeights[constraint];
                    for (int index = start; index < end; index++)
                    {
                        result[MaterialIndices[index]] +=
                            projectedDisplacement *
                            MaterialCoefficients[index] * weight;
                    }
                }
            }

            private void ApplyPreconditioner(
                NativeArray<Vector3> values,
                NativeArray<Vector3> result)
            {
                for (int index = 0; index < Count; index++)
                {
                    result[index] = values[index] /
                        math.max(Preconditioner[index], 0.000001f);
                }
            }

            private double Dot(
                NativeArray<Vector3> first,
                NativeArray<Vector3> second)
            {
                double result = 0d;
                for (int index = 0; index < Count; index++)
                {
                    result += Vector3.Dot(first[index], second[index]);
                }
                return result;
            }
        }

        private static void ApplyMatrixInto(
            IReadOnlyList<Vector3> values,
            IReadOnlyList<float> diagonal,
            IEnumerable<LocalEdge> edges,
            IEnumerable<BendingLinearization> bendingLinearizations,
            IEnumerable<EdgeStrainLinearization> edgeStrainLinearizations,
            IEnumerable<NativeSkinningMaterialStrainConstraint>
                materialStrainConstraints,
            Vector3[] result,
            int count)
        {
            for (int index = 0; index < count; index++)
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
                for (int index = 0; index < 4; index++)
                {
                    int localIndex = term.LocalIndexAt(index);
                    if (localIndex >= 0)
                    {
                        projectedDisplacement += Vector3.Dot(
                            term.GradientAt(index),
                            values[localIndex]);
                    }
                }
                for (int index = 0; index < 4; index++)
                {
                    int localIndex = term.LocalIndexAt(index);
                    if (localIndex >= 0)
                    {
                        result[localIndex] += BendingWeight *
                            term.GradientAt(index) * projectedDisplacement;
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
        }

        private static void ApplyDiagonalPreconditionerInto(
            IReadOnlyList<Vector3> values,
            IReadOnlyList<float> diagonal,
            Vector3[] result,
            int count)
        {
            for (int index = 0; index < count; index++)
            {
                result[index] = values[index] /
                    Mathf.Max(diagonal[index], 0.000001f);
            }
        }

        private static double Dot(
            IReadOnlyList<Vector3> first,
            IReadOnlyList<Vector3> second,
            int count)
        {
            double result = 0d;
            for (int index = 0; index < count; index++)
            {
                result += Vector3.Dot(first[index], second[index]);
            }
            return result;
        }

        private static void LimitDisplacement(
            IList<Vector3> displacement,
            int count,
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
            // 버퍼는 이전 패스의 잔여를 담을 수 있으므로 논리 크기까지만 스캔한다.
            float requestedStep = 0f;
            for (int index = 0; index < count; index++)
            {
                float magnitude = displacement[index].magnitude;
                if (magnitude > requestedStep)
                {
                    requestedStep = magnitude;
                }
            }
            if (requestedStep <= maximumStep || requestedStep <= 0f)
            {
                return;
            }
            float scale = maximumStep / requestedStep;
            for (int index = 0; index < count; index++)
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
            Vector3[] candidate,
            out Vector3[] acceptedVertices,
            out double acceptedEnergy)
        {
            acceptedVertices = null;
            acceptedEnergy = 0d;
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
            Array.Copy(
                (Vector3[])currentVertices,
                candidate,
                currentVertices.Count);
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
                double candidateEnergy = CalculateConstraintEnergy(
                    baselineVertices,
                    candidate,
                    contract,
                    activePairIndices,
                    workset.FacePairIndices,
                    workset.FaceIndices,
                    workset.Edges,
                    shouldRecoverRestShape);
                if (!HasOrientationDefect(
                        baselineVertices,
                        candidate,
                        contract,
                        workset.FaceIndices) &&
                    !hasStrainDefect &&
                    !hasRestShapeSharpFoldDefect &&
                    candidateEnergy < beforeEnergy)
                {
                    acceptedVertices = candidate;
                    acceptedEnergy = candidateEnergy;
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
                if (!IsSharpFoldCorrectionEligible(
                        baselineVertices,
                        contract,
                        pairIndex) ||
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
                if (NativeSkinningSurfaceDefectDetector.IsCorrectable(
                        vertices,
                        contract.FacePairs[pairIndex],
                        contract.FacePairRestLengths[pairIndex],
                        contract.RestAnglesDegrees[pairIndex],
                        contract.RestShapeRecoveryMaximumCosines[pairIndex]))
                {
                    activePairIndices.Add(pairIndex);
                }
            }
        }

        // rest 각도 상한을 넘는 자연 주름도 baseline에서 접히지 않았다면
        // 보정 도중 임계값을 넘었을 때 되돌릴 제약 대상으로 인정한다.
        // baseline이 이미 접힌 쌍은 계약이 허용한 상태이므로 건드리지 않는다.
        private static bool IsSharpFoldCorrectionEligible(
            IReadOnlyList<Vector3> baselineVertices,
            NativeSkinningSurfaceContract contract,
            int pairIndex)
        {
            if (contract.RestAnglesDegrees[pairIndex] <= RestSmoothAngleDegrees)
            {
                return true;
            }
            return NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                       baselineVertices,
                       contract.FacePairs[pairIndex],
                       out float baselineAngleDegrees) &&
                   baselineAngleDegrees <= SharpFoldAngleDegrees;
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

                if (NativeSkinningSurfaceContractBuilder.TryMeasureAngle(
                        currentVertices,
                        contract.FacePairs[pairIndex],
                        out float angleDegrees) &&
                    angleDegrees > SharpFoldAngleDegrees &&
                    IsSharpFoldCorrectionEligible(
                        baselineVertices,
                        contract,
                        pairIndex))
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
            int ringCount,
            IReadOnlyList<FrameEdge> frameEdges,
            SolverBuffers buffers)
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
            int vertexCount = baselineVertices.Count;
            int faceCount = contract.Triangles.Length / 3;
            buffers.EnsureWorksetSize(
                vertexCount,
                contract.FacePairs.Length,
                faceCount);
            int[] localIndexByVertex = buffers.LocalIndexByVertex;
            int[] faceMark = buffers.FaceMark;
            int[] facePairMark = buffers.FacePairMark;
            var frontier = new HashSet<int>(constrainedVertexSet);
            for (int ring = 0; ring < ringCount && frontier.Count > 0; ring++)
            {
                // 같은 면을 두 번 순회하지 않게 스탬프로 표시한다.
                int ringStamp = buffers.NextMarkStamp();
                var nextFrontier = new HashSet<int>();
                foreach (int index in frontier)
                {
                    foreach (int faceIndex in
                             contract.AffectedFaceIndicesByVertex[index])
                    {
                        if (faceMark[faceIndex] == ringStamp)
                        {
                            continue;
                        }
                        faceMark[faceIndex] = ringStamp;
                        int offset = faceIndex * 3;
                        for (int corner = 0; corner < 3; corner++)
                        {
                            int vertexIndex =
                                contract.Triangles[offset + corner];
                            if (constrainedVertexSet.Add(vertexIndex))
                            {
                                nextFrontier.Add(vertexIndex);
                            }
                        }
                    }
                }
                frontier = nextFrontier;
            }

            // 멤버십 표시 후 오름차순 스캔 — 정렬된 인덱스와 로컬 인덱스를 동시에 얻는다.
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                localIndexByVertex[vertexIndex] =
                    constrainedVertexSet.Contains(vertexIndex) ? 0 : -1;
            }
            int[] constrainedVertices = new int[constrainedVertexSet.Count];
            int constrainedCount = 0;
            for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
            {
                if (localIndexByVertex[vertexIndex] >= 0)
                {
                    localIndexByVertex[vertexIndex] = constrainedCount;
                    constrainedVertices[constrainedCount] = vertexIndex;
                    constrainedCount++;
                }
            }

            int pairStamp = buffers.NextMarkStamp();
            foreach (int vertexIndex in constrainedVertices)
            {
                foreach (int pairIndex in
                         contract.FacePairIndicesByVertex[vertexIndex])
                {
                    facePairMark[pairIndex] = pairStamp;
                }
            }
            int facePairCount = 0;
            for (int pairIndex = 0;
                 pairIndex < contract.FacePairs.Length;
                 pairIndex++)
            {
                if (facePairMark[pairIndex] == pairStamp)
                {
                    facePairCount++;
                }
            }
            int[] facePairIndices = new int[facePairCount];
            int facePairWriteIndex = 0;
            for (int pairIndex = 0;
                 pairIndex < contract.FacePairs.Length;
                 pairIndex++)
            {
                if (facePairMark[pairIndex] == pairStamp)
                {
                    facePairIndices[facePairWriteIndex] = pairIndex;
                    facePairWriteIndex++;
                }
            }

            int faceStamp = buffers.NextMarkStamp();
            foreach (int vertexIndex in constrainedVertices)
            {
                foreach (int faceIndex in
                         contract.AffectedFaceIndicesByVertex[vertexIndex])
                {
                    faceMark[faceIndex] = faceStamp;
                }
            }
            int markedFaceCount = 0;
            for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                if (faceMark[faceIndex] == faceStamp)
                {
                    markedFaceCount++;
                }
            }
            int[] faceIndices = new int[markedFaceCount];
            int faceWriteIndex = 0;
            for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
            {
                if (faceMark[faceIndex] == faceStamp)
                {
                    faceIndices[faceWriteIndex] = faceIndex;
                    faceWriteIndex++;
                }
            }

            int edgeCount = 0;
            foreach (FrameEdge edge in frameEdges)
            {
                if (localIndexByVertex[edge.FirstVertex] >= 0 ||
                    localIndexByVertex[edge.SecondVertex] >= 0)
                {
                    edgeCount++;
                }
            }
            var edges = new LocalEdge[edgeCount];
            int edgeWriteIndex = 0;
            foreach (FrameEdge edge in frameEdges)
            {
                int firstLocalIndex = localIndexByVertex[edge.FirstVertex];
                int secondLocalIndex = localIndexByVertex[edge.SecondVertex];
                if (firstLocalIndex < 0 && secondLocalIndex < 0)
                {
                    continue;
                }
                edges[edgeWriteIndex] = new LocalEdge(
                    edge.FirstVertex,
                    edge.SecondVertex,
                    firstLocalIndex,
                    secondLocalIndex,
                    edge.BaselineLength,
                    edge.RestLength);
                edgeWriteIndex++;
            }
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
            // 진단 테스트가 이 6인자 시그니처로 인스턴스를 만들므로 유지한다.
            internal CorrectionConfiguration(
                int localGlobalIterations,
                int conjugateGradientIterations,
                int edgeLengthProjectionIterations,
                float maximumEdgeLengthStrain,
                float maximumTotalDisplacementToArmLengthRatio,
                int worksetRingCount = DefaultWorksetRingCount)
                : this(
                    localGlobalIterations,
                    conjugateGradientIterations,
                    edgeLengthProjectionIterations,
                    maximumEdgeLengthStrain,
                    maximumTotalDisplacementToArmLengthRatio,
                    worksetRingCount,
                    false,
                    false,
                    false)
            {
            }

            internal CorrectionConfiguration(
                int localGlobalIterations,
                int conjugateGradientIterations,
                int edgeLengthProjectionIterations,
                float maximumEdgeLengthStrain,
                float maximumTotalDisplacementToArmLengthRatio,
                int worksetRingCount,
                bool allowsEarlyConvergence,
                bool allowsWarmStart,
                bool allowsBurst = false)
            {
                LocalGlobalIterations = localGlobalIterations;
                ConjugateGradientIterations = conjugateGradientIterations;
                EdgeLengthProjectionIterations = edgeLengthProjectionIterations;
                MaximumEdgeLengthStrain = maximumEdgeLengthStrain;
                MaximumTotalDisplacementToArmLengthRatio =
                    maximumTotalDisplacementToArmLengthRatio;
                WorksetRingCount = worksetRingCount;
                AllowsEarlyConvergence = allowsEarlyConvergence;
                AllowsWarmStart = allowsWarmStart;
                AllowsBurst = allowsBurst;
            }

            internal int LocalGlobalIterations { get; }
            internal int ConjugateGradientIterations { get; }
            internal int EdgeLengthProjectionIterations { get; }
            internal float MaximumEdgeLengthStrain { get; }
            internal float MaximumTotalDisplacementToArmLengthRatio { get; }
            internal int WorksetRingCount { get; }
            internal bool AllowsEarlyConvergence { get; }
            internal bool AllowsWarmStart { get; }
            internal bool AllowsBurst { get; }
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

        // 쌍당 4정점 고정 — 배열 대신 인라인 필드로 반복 할당을 없앤다.
        private readonly struct BendingLinearization
        {
            internal BendingLinearization(
                int index0,
                int index1,
                int index2,
                int index3,
                Vector3 gradient0,
                Vector3 gradient1,
                Vector3 gradient2,
                Vector3 gradient3)
            {
                Index0 = index0;
                Index1 = index1;
                Index2 = index2;
                Index3 = index3;
                Gradient0 = gradient0;
                Gradient1 = gradient1;
                Gradient2 = gradient2;
                Gradient3 = gradient3;
            }

            internal int Index0 { get; }
            internal int Index1 { get; }
            internal int Index2 { get; }
            internal int Index3 { get; }
            internal Vector3 Gradient0 { get; }
            internal Vector3 Gradient1 { get; }
            internal Vector3 Gradient2 { get; }
            internal Vector3 Gradient3 { get; }

            internal int LocalIndexAt(int index)
            {
                switch (index)
                {
                    case 0: return Index0;
                    case 1: return Index1;
                    case 2: return Index2;
                    default: return Index3;
                }
            }

            internal Vector3 GradientAt(int index)
            {
                switch (index)
                {
                    case 0: return Gradient0;
                    case 1: return Gradient1;
                    case 2: return Gradient2;
                    default: return Gradient3;
                }
            }
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

        // TryCalculate 호출 1회분의 재사용 스크래치 — 워커 스레드 한정(공유 없음)이라
        // 별도 동기화 없이 반복 할당만 제거한다.
        private sealed class SolverBuffers
        {
            internal float[] Diagonal = Array.Empty<float>();
            internal float[] Preconditioner = Array.Empty<float>();
            internal Vector3[] RightHandSide = Array.Empty<Vector3>();
            internal Vector3[] Solution = Array.Empty<Vector3>();
            internal Vector3[] Residual = Array.Empty<Vector3>();
            internal Vector3[] Direction = Array.Empty<Vector3>();
            internal Vector3[] PreconditionedResidual = Array.Empty<Vector3>();
            internal Vector3[] MatrixDirection = Array.Empty<Vector3>();
            internal Vector3[] ProposedLocal = Array.Empty<Vector3>();
            // 수락된 후보가 다음 반복의 current로 이어지므로 핑퐁 2개가 필요하다.
            internal Vector3[] CandidateA = Array.Empty<Vector3>();
            internal Vector3[] CandidateB = Array.Empty<Vector3>();
            internal readonly List<BendingLinearization> BendingTerms =
                new List<BendingLinearization>();
            internal readonly List<EdgeStrainLinearization> EdgeStrainTerms =
                new List<EdgeStrainLinearization>();
            // 워크셋 구축용 스탬프 마크 — 매번 클리어 대신 스탬프를 올려 재사용한다.
            internal int[] LocalIndexByVertex = Array.Empty<int>();
            internal int[] FacePairMark = Array.Empty<int>();
            internal int[] FaceMark = Array.Empty<int>();
            private int _markStamp;

            internal void EnsureLocalSize(int count)
            {
                Ensure(ref Diagonal, count);
                Ensure(ref Preconditioner, count);
                Ensure(ref RightHandSide, count);
                Ensure(ref Solution, count);
                Ensure(ref Residual, count);
                Ensure(ref Direction, count);
                Ensure(ref PreconditionedResidual, count);
                Ensure(ref MatrixDirection, count);
                Ensure(ref ProposedLocal, count);
            }

            internal void EnsureVertexSize(int count)
            {
                Ensure(ref CandidateA, count);
                Ensure(ref CandidateB, count);
            }

            internal void EnsureWorksetSize(
                int vertexCount,
                int facePairCount,
                int faceCount)
            {
                Ensure(ref LocalIndexByVertex, vertexCount);
                Ensure(ref FacePairMark, facePairCount);
                Ensure(ref FaceMark, faceCount);
            }

            internal int NextMarkStamp() => ++_markStamp;

            internal Vector3[] CandidateFor(IReadOnlyList<Vector3> current) =>
                ReferenceEquals(current, CandidateA) ? CandidateB : CandidateA;

            private static void Ensure(ref int[] array, int count)
            {
                if (array.Length < count)
                {
                    array = new int[count];
                }
            }

            private static void Ensure(ref float[] array, int count)
            {
                if (array.Length < count)
                {
                    array = new float[count];
                }
            }

            private static void Ensure(ref Vector3[] array, int count)
            {
                if (array.Length < count)
                {
                    array = new Vector3[count];
                }
            }
        }
    }

    /// <summary>
    /// 표면 솔버 커널 구간별 누적 계측 — 워커 병렬 호출에서 Interlocked로만 쓰는 진단 전용 싱크.
    /// </summary>
    internal sealed class NativeSkinningSolverTiming
    {
        internal long PairEnergyTicks;
        internal long BuildSystemTicks;
        internal long CgSolveTicks;
        internal long ProjectAcceptTicks;
        internal long SetupTicks;
        internal long LgIterations;
        internal long CgCalls;
        internal long CgIterations;

        internal static long Stamp() =>
            System.Diagnostics.Stopwatch.GetTimestamp();

        internal void AddPairEnergy(long since) =>
            System.Threading.Interlocked.Add(
                ref PairEnergyTicks, Stamp() - since);

        internal void AddBuildSystem(long since) =>
            System.Threading.Interlocked.Add(
                ref BuildSystemTicks, Stamp() - since);

        internal void AddCgSolve(long since) =>
            System.Threading.Interlocked.Add(
                ref CgSolveTicks, Stamp() - since);

        internal void AddProjectAccept(long since) =>
            System.Threading.Interlocked.Add(
                ref ProjectAcceptTicks, Stamp() - since);

        internal void AddSetup(long since) =>
            System.Threading.Interlocked.Add(
                ref SetupTicks, Stamp() - since);

        internal void AddLgIteration() =>
            System.Threading.Interlocked.Increment(ref LgIterations);

        internal void AddCgCall() =>
            System.Threading.Interlocked.Increment(ref CgCalls);

        internal void AddCgIteration() =>
            System.Threading.Interlocked.Increment(ref CgIterations);

        internal void AddCgIterations(int iterations) =>
            System.Threading.Interlocked.Add(ref CgIterations, iterations);

        internal string Describe()
        {
            double frequency = System.Diagnostics.Stopwatch.Frequency;
            return $"pairEnergy={PairEnergyTicks * 1000.0 / frequency:F0}ms " +
                $"buildSystem={BuildSystemTicks * 1000.0 / frequency:F0}ms " +
                $"cgSolve={CgSolveTicks * 1000.0 / frequency:F0}ms " +
                $"projectAccept={ProjectAcceptTicks * 1000.0 / frequency:F0}ms " +
                $"setup={SetupTicks * 1000.0 / frequency:F0}ms " +
                $"lgIter={LgIterations} cgCalls={CgCalls} " +
                $"cgIter={CgIterations}";
        }
    }
}
