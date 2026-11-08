using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static partial class NativeSkinningSurfaceCorrectionCalculator
    {
        private const int JointProjectionRingCount = 2;
        private const int JointProjectionOuterIterations = 24;
        private const int JointProjectionInnerIterations = 2000;
        private const int JointProjectionLineSearchSteps = 6;
        private const double JointProjectionPositionScale = 0.001;
        private const double JointProjectionAngleMargin = 0.1;
        private const double JointProjectionTolerance = 1e-7;

        private static bool TryProjectJointConstraints(
            IReadOnlyList<Vector3> baseline,
            NativeSkinningSurfaceContract contract,
            NativeSkinningSurfaceCorrectionResult initial,
            HashSet<int> originalPairs,
            int[] recoveryPairs,
            FrameEdge[] frameEdges,
            out NativeSkinningSurfaceCorrectionResult result)
        {
            result = null;
            IReadOnlyList<Vector3> seed = initial.CorrectedVertices;
            LocalWorkset workset = BuildLocalWorkset(
                baseline, contract, originalPairs, StrongExtendedConfiguration.WorksetRingCount,
                frameEdges, new SolverBuffers());
            var active = new HashSet<int>();
            double maxStrain = StrongConfiguration.MaximumEdgeLengthStrain - EdgeLengthProjectionMargin;
            double minLength = 1 - maxStrain;
            double maxLength = 1 + maxStrain;
            double minCosine = Math.Cos((SharpFoldAngleDegrees - JointProjectionAngleMargin) * Math.PI / 180);
            LocalEdge[] edges = workset.Edges.Where(edge => edge.BaselineLength > 1e-7f).ToArray();
            NativeSkinningFacePair[] pairs = workset.FacePairIndices
                .Where(pair => IsSharpFoldCorrectionEligible(baseline, contract, pair))
                .Select(pair => contract.FacePairs[pair]).ToArray();
            foreach (LocalEdge edge in edges)
            {
                double length = math.distance(ToDouble(seed[edge.FirstVertex]), ToDouble(seed[edge.SecondVertex]));
                if (Math.Abs(length / edge.BaselineLength - 1) > maxStrain)
                {
                    active.Add(edge.FirstVertex);
                    active.Add(edge.SecondVertex);
                }
            }
            foreach (NativeSkinningFacePair pair in pairs)
            {
                double3 first = ToDouble(seed[pair.FirstOpposite]);
                double3 second = ToDouble(seed[pair.SecondOpposite]);
                double3 edge0 = ToDouble(seed[pair.FirstEdge]);
                double3 edge1 = ToDouble(seed[pair.SecondEdge]);
                if (MeasureFoldCosine(first, second, edge0, edge1, out _, out _) < minCosine)
                {
                    active.Add(pair.FirstOpposite);
                    active.Add(pair.SecondOpposite);
                    active.Add(pair.FirstEdge);
                    active.Add(pair.SecondEdge);
                }
            }
            for (int ring = 0; ring < JointProjectionRingCount; ring++)
            {
                var frontier = new HashSet<int>(active);
                foreach (LocalEdge edge in edges)
                {
                    if (frontier.Contains(edge.FirstVertex) || frontier.Contains(edge.SecondVertex))
                    {
                        active.Add(edge.FirstVertex);
                        active.Add(edge.SecondVertex);
                    }
                }
            }

            int[] vertices = workset.ConstrainedVertices.Where(active.Contains).ToArray();
            if (vertices.Length == 0)
            {
                return false;
            }
            var local = new int[baseline.Count];
            Array.Fill(local, -1);
            for (int index = 0; index < vertices.Length; index++)
            {
                local[vertices[index]] = index;
            }
            edges = edges.Where(edge => local[edge.FirstVertex] >= 0 || local[edge.SecondVertex] >= 0).ToArray();
            pairs = pairs.Where(pair => local[pair.FirstOpposite] >= 0 || local[pair.SecondOpposite] >= 0 ||
                local[pair.FirstEdge] >= 0 || local[pair.SecondEdge] >= 0).ToArray();
            var faces = new List<(int First, int Second, int Third, double3 Reference)>();
            foreach (int face in workset.FaceIndices)
            {
                int offset = face * 3;
                int first = contract.Triangles[offset];
                int second = contract.Triangles[offset + 1];
                int third = contract.Triangles[offset + 2];
                if (local[first] < 0 && local[second] < 0 && local[third] < 0)
                {
                    continue;
                }
                double3 normal = FaceNormal(
                    ToDouble(baseline[first]), ToDouble(baseline[second]), ToDouble(baseline[third]));
                double lengthSquared = math.lengthsq(normal);
                if (lengthSquared >= MinimumNormalSquaredMagnitude)
                {
                    faces.Add((first, second, third, normal / lengthSquared));
                }
            }

            double maxDisplacement = contract.ArmChainLength * StrongConfiguration.MaximumTotalDisplacementToArmLengthRatio
                + QualityComparisonTolerance;
            double maxDisplacementSquared = maxDisplacement * maxDisplacement;
            var rows = Enumerable.Range(0, 2 * edges.Length + pairs.Length + faces.Count + vertices.Length)
                .Select(_ => new JointProjectionRow()).ToArray();
            var multipliers = new double[rows.Length];
            var search = new double[rows.Length];
            var point = new double3[vertices.Length];
            var proposal = new double3[vertices.Length];
            var trial = new double3[vertices.Length];
            var columnSums = new double3[vertices.Length];
            // 각도와 길이 한도를 같은 선형 제약으로 풀어 한쪽 투영이 다른 쪽의 개선을 되돌리지 않게 함.
            for (int round = 0; round < JointProjectionOuterIterations; round++)
            {
                double violation = Measure(point, linearize: true);
                if (!double.IsFinite(violation) || violation >= -JointProjectionTolerance ||
                    !ProjectLinearConstraints(rows, multipliers, search, proposal, columnSums))
                {
                    break;
                }
                bool accepted = false;
                double fraction = 1;
                for (int step = 0; step < JointProjectionLineSearchSteps; step++, fraction *= 0.5)
                {
                    for (int index = 0; index < trial.Length; index++)
                    {
                        trial[index] = math.lerp(point[index], proposal[index], fraction);
                    }
                    if (Measure(trial, linearize: false) > violation)
                    {
                        (point, trial) = (trial, point);
                        accepted = true;
                        break;
                    }
                }
                if (!accepted)
                {
                    break;
                }
            }

            Vector3[] corrected = seed.ToArray();
            for (int index = 0; index < vertices.Length; index++)
            {
                int vertex = vertices[index];
                double3 value = Position(vertex, point);
                corrected[vertex] = new Vector3((float)value.x, (float)value.y, (float)value.z);
            }
            if (!AreVerticesFinite(corrected))
            {
                return false;
            }
            NativeSkinningSurfaceCorrectionResult candidate = EvaluateResult(
                baseline, corrected, contract, frameEdges, originalPairs.Count, recoveryPairs);
            if (!IsWithinStrongQuality(candidate, contract) || !HasDisplayStrainMargin(candidate))
            {
                return false;
            }
            result = candidate;
            return true;

            double3 Position(int vertex, double3[] movement)
            {
                int index = local[vertex];
                double3 origin = ToDouble(seed[vertex]);
                return index >= 0 ? origin + movement[index] * JointProjectionPositionScale : origin;
            }

            double Measure(double3[] movement, bool linearize)
            {
                int rowIndex = 0;
                double minimum = double.PositiveInfinity;
                foreach (LocalEdge edge in edges)
                {
                    double3 difference = Position(edge.SecondVertex, movement) - Position(edge.FirstVertex, movement);
                    double lengthSquared = (double)edge.BaselineLength * edge.BaselineLength;
                    double ratioSquared = math.lengthsq(difference) / lengthSquared;
                    AddEdge(ratioSquared - minLength * minLength, 2 * difference / lengthSquared);
                    AddEdge(maxLength * maxLength - ratioSquared, -2 * difference / lengthSquared);

                    void AddEdge(double value, double3 gradient)
                    {
                        JointProjectionRow row = Begin(value);
                        if (linearize)
                        {
                            row.Add(local[edge.FirstVertex], -gradient);
                            row.Add(local[edge.SecondVertex], gradient);
                            row.Normalize(value, movement);
                        }
                    }
                }
                foreach (NativeSkinningFacePair pair in pairs)
                {
                    double3 first = Position(pair.FirstOpposite, movement);
                    double3 second = Position(pair.SecondOpposite, movement);
                    double3 edge0 = Position(pair.FirstEdge, movement);
                    double3 edge1 = Position(pair.SecondEdge, movement);
                    double value = MeasureFoldCosine(first, second, edge0, edge1,
                        out double3 firstGradient, out double3 secondGradient) - minCosine;
                    JointProjectionRow row = Begin(value);
                    if (linearize)
                    {
                        AddNormalGradient(row, local[pair.FirstOpposite], local[pair.FirstEdge],
                            local[pair.SecondEdge], first, edge0, edge1, firstGradient);
                        AddNormalGradient(row, local[pair.SecondOpposite], local[pair.SecondEdge],
                            local[pair.FirstEdge], second, edge1, edge0, secondGradient);
                        row.Normalize(value, movement);
                    }
                }
                foreach (var face in faces)
                {
                    double3 first = Position(face.First, movement);
                    double3 second = Position(face.Second, movement);
                    double3 third = Position(face.Third, movement);
                    double value = math.dot(FaceNormal(first, second, third), face.Reference) - MinimumSignedAreaRatio;
                    JointProjectionRow row = Begin(value);
                    if (linearize)
                    {
                        AddNormalGradient(row, local[face.First], local[face.Second], local[face.Third],
                            first, second, third, face.Reference);
                        row.Normalize(value, movement);
                    }
                }
                for (int index = 0; index < vertices.Length; index++)
                {
                    int vertex = vertices[index];
                    double3 delta = Position(vertex, movement) - ToDouble(baseline[vertex]);
                    double value = 1 - math.lengthsq(delta) / maxDisplacementSquared;
                    JointProjectionRow row = Begin(value);
                    if (linearize)
                    {
                        row.Add(index, -2 * delta / maxDisplacementSquared);
                        row.Normalize(value, movement);
                    }
                }
                return minimum;

                JointProjectionRow Begin(double value)
                {
                    minimum = Math.Min(minimum, value);
                    JointProjectionRow row = rows[rowIndex++];
                    if (linearize)
                    {
                        row.Clear();
                    }
                    return row;
                }
            }
        }

        private static bool ProjectLinearConstraints(
            JointProjectionRow[] rows, double[] multipliers, double[] search,
            double3[] proposal, double3[] columnSums)
        {
            Array.Clear(columnSums, 0, columnSums.Length);
            foreach (JointProjectionRow row in rows)
            {
                row.AddColumnMagnitudes(columnSums);
            }
            double upperBound = 0;
            foreach (JointProjectionRow row in rows)
            {
                upperBound = Math.Max(upperBound, row.Dot(columnSums, absolute: true));
            }
            if (!double.IsFinite(upperBound) || upperBound <= 0)
            {
                return false;
            }

            // 희소 제약의 쌍대 문제에서 가속 투영을 수행하고 행합 상한으로 스텝을 제한함.
            Array.Copy(multipliers, search, search.Length);
            for (int iteration = 0; iteration < JointProjectionInnerIterations; iteration++)
            {
                Accumulate(search);
                double acceleration = (double)iteration / (iteration + 3);
                double maximumChange = 0;
                for (int index = 0; index < rows.Length; index++)
                {
                    JointProjectionRow row = rows[index];
                    double next = row.HasGradient
                        ? Math.Max(0, search[index] - (row.Dot(proposal) - row.RightHandSide) / upperBound)
                        : 0;
                    double change = next - multipliers[index];
                    maximumChange = Math.Max(maximumChange, Math.Abs(change));
                    search[index] = next + acceleration * change;
                    multipliers[index] = next;
                }
                if (maximumChange < 1e-9)
                {
                    break;
                }
            }
            Accumulate(multipliers);
            return true;

            void Accumulate(double[] values)
            {
                Array.Clear(proposal, 0, proposal.Length);
                for (int index = 0; index < rows.Length; index++)
                {
                    if (values[index] != 0)
                    {
                        rows[index].Accumulate(values[index], proposal);
                    }
                }
            }
        }

        private static double3 ToDouble(Vector3 value) => new double3(value.x, value.y, value.z);

        private static double3 FaceNormal(double3 first, double3 second, double3 third) =>
            math.cross(second - first, third - first);

        private static double MeasureFoldCosine(
            double3 first, double3 second, double3 edge0, double3 edge1,
            out double3 firstGradient, out double3 secondGradient)
        {
            double3 firstNormal = FaceNormal(first, edge0, edge1);
            double3 secondNormal = FaceNormal(second, edge1, edge0);
            double firstLength = Math.Max(math.length(firstNormal), 1e-16);
            double secondLength = Math.Max(math.length(secondNormal), 1e-16);
            double3 firstUnit = firstNormal / firstLength;
            double3 secondUnit = secondNormal / secondLength;
            double cosine = math.dot(firstUnit, secondUnit);
            firstGradient = (secondUnit - cosine * firstUnit) / firstLength;
            secondGradient = (firstUnit - cosine * secondUnit) / secondLength;
            return cosine;
        }

        private static void AddNormalGradient(
            JointProjectionRow row, int firstIndex, int secondIndex, int thirdIndex,
            double3 first, double3 second, double3 third, double3 gradient)
        {
            double3 secondGradient = math.cross(third - first, gradient);
            double3 thirdGradient = math.cross(gradient, second - first);
            row.Add(firstIndex, -secondGradient - thirdGradient);
            row.Add(secondIndex, secondGradient);
            row.Add(thirdIndex, thirdGradient);
        }

        private sealed class JointProjectionRow
        {
            private readonly int[] _indices = new int[4];
            private readonly double3[] _gradients = new double3[4];
            private int _count;

            internal double RightHandSide { get; private set; }
            internal bool HasGradient => _count > 0;

            internal void Clear()
            {
                _count = 0;
                RightHandSide = 0;
            }

            internal void Add(int index, double3 gradient)
            {
                if (index < 0)
                {
                    return;
                }
                for (int entry = 0; entry < _count; entry++)
                {
                    if (_indices[entry] == index)
                    {
                        _gradients[entry] += gradient;
                        return;
                    }
                }
                _indices[_count] = index;
                _gradients[_count++] = gradient;
            }

            internal void Normalize(double value, double3[] current)
            {
                double lengthSquared = 0;
                for (int entry = 0; entry < _count; entry++)
                {
                    lengthSquared += math.lengthsq(_gradients[entry]);
                }
                double norm = Math.Sqrt(lengthSquared);
                if (!double.IsFinite(norm) || norm * JointProjectionPositionScale <= 1e-10)
                {
                    Clear();
                    return;
                }
                for (int entry = 0; entry < _count; entry++)
                {
                    _gradients[entry] /= norm;
                }
                RightHandSide = Dot(current) - value / (norm * JointProjectionPositionScale);
            }

            internal double Dot(double3[] point, bool absolute = false)
            {
                double total = 0;
                for (int entry = 0; entry < _count; entry++)
                {
                    double3 gradient = _gradients[entry];
                    total += math.dot(absolute ? math.abs(gradient) : gradient, point[_indices[entry]]);
                }
                return total;
            }

            internal void AddColumnMagnitudes(double3[] columns)
            {
                for (int entry = 0; entry < _count; entry++)
                {
                    columns[_indices[entry]] += math.abs(_gradients[entry]);
                }
            }

            internal void Accumulate(double amount, double3[] point)
            {
                for (int entry = 0; entry < _count; entry++)
                {
                    point[_indices[entry]] += amount * _gradients[entry];
                }
            }
        }
    }
}
