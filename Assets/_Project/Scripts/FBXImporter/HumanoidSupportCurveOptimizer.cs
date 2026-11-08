using System;
using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    internal static class HumanoidSupportCurveOptimizer
    {
        private const double ConstraintTolerance = 1e-9;
        private const double RecoveryWeight = .2;
        private const int MaximumIterations = 100;
        private static readonly double[] SearchScales = { 1, .75, .5, .25, .1, .05, .02, .01, .002 };
        private static readonly double[] FeasibleScales = { 1, .95, .85, .7, .5, .25 };

        internal sealed class Evaluation
        {
            internal Evaluation(double cost, double[] slack, double[] gradient = null,
                double[,] metric = null, double[][] constraintGradients = null)
            {
                Cost = cost;
                Slack = slack;
                Gradient = gradient;
                Metric = metric;
                ConstraintGradients = constraintGradients;
            }

            internal double Cost { get; }
            internal double[] Slack { get; }
            internal double[] Gradient { get; }
            internal double[,] Metric { get; }
            internal double[][] ConstraintGradients { get; }
        }

        internal static bool TrySolve(int count, Func<double[], bool, Evaluation> evaluate, out double[] best)
        {
            var current = new double[count];
            best = current;
            Evaluation initial = evaluate(current, false);
            if (!IsFeasible(initial.Slack) || !double.IsFinite(initial.Cost)) return false;
            double bestCost = initial.Cost;
            for (int iteration = 0; iteration < MaximumIterations; iteration++)
            {
                Evaluation sample = evaluate(current, true);
                if (!TryGetDirection(sample, out double[] direction, out double[] rowNorm, out double meritWeight)) break;
                double currentMerit = Merit(sample, rowNorm, meritWeight);
                bool improved = false;
                foreach (double scale in SearchScales)
                {
                    double[] trial = Move(current, direction, scale);
                    Evaluation result = evaluate(trial, false);
                    if (!(Merit(result, rowNorm, meritWeight) < currentMerit - 1e-9)) continue;

                    // 탐색 중 제약 초과는 허용하되 정확한 비선형 조건을 통과한 곡선만 반환함.
                    double[] difference = Move(trial, best, -1);
                    foreach (double fraction in FeasibleScales)
                    {
                        double[] feasible = fraction == 1 ? trial : Move(best, difference, fraction);
                        Evaluation checkedPose = fraction == 1 ? result : evaluate(feasible, false);
                        if (!IsFeasible(checkedPose.Slack) || !(checkedPose.Cost < bestCost - 1e-9)) continue;
                        best = feasible;
                        bestCost = checkedPose.Cost;
                        break;
                    }
                    current = trial;
                    improved = true;
                    break;
                }
                if (!improved) break;
            }
            return bestCost < initial.Cost - 1e-9;
        }

        private static bool TryGetDirection(Evaluation sample, out double[] direction, out double[] rowNorm, out double meritWeight)
        {
            direction = null;
            rowNorm = null;
            meritWeight = 0;
            int count = sample.Gradient.Length;
            if (!TryFactor(sample.Metric, count, out double[,] lower)) return false;

            double[] desired = Solve(lower, sample.Gradient, false);
            meritWeight = 8 * Math.Max(1e-6, Norm(desired));
            double extent = Norm(Solve(lower, desired, true));
            if (!(extent >= 1e-8) || !double.IsFinite(extent)) return false;
            double limit = Math.Min(1, 16 / extent);
            for (int axis = 0; axis < count; axis++) desired[axis] *= -limit;
            double radius = Norm(desired);
            rowNorm = new double[sample.Slack.Length];
            var normals = new List<double[]>();
            var offsets = new List<double>();
            double recovery = 0;
            for (int row = 0; row < sample.Slack.Length; row++)
            {
                double[] normal = Solve(lower, sample.ConstraintGradients[row], false);
                double length = Norm(normal);
                rowNorm[row] = length;
                if (!(length > 1e-12 && sample.Slack[row] < radius * length)) continue;

                double distance = sample.Slack[row] / length;
                double augmentedLength = Math.Sqrt(1 + RecoveryWeight * RecoveryWeight);
                var augmented = new double[count + 1];
                for (int axis = 0; axis < count; axis++) augmented[axis] = normal[axis] / (length * augmentedLength);
                augmented[count] = RecoveryWeight / augmentedLength;
                normals.Add(augmented);
                offsets.Add(distance / augmentedLength);
                recovery = Math.Max(recovery, -distance / RecoveryWeight);
            }

            var vertical = new double[count + 1];
            vertical[count] = 1;
            normals.Add(vertical);
            offsets.Add(0);
            var target = new double[count + 1];
            Array.Copy(desired, target, count);
            target[count] = -8 * Math.Max(1e-6, radius);
            var feasible = new double[count + 1];
            feasible[count] = recovery;
            if (!HumanoidSupportConstraintProjection.TryProject(target, normals.ToArray(), offsets.ToArray(), feasible, out double[] projected))
                return false;
            direction = Solve(lower, projected, true);
            return true;
        }

        private static bool TryFactor(double[,] matrix, int count, out double[,] lower)
        {
            lower = new double[count, count];
            for (int row = 0; row < count; row++)
            for (int column = 0; column <= row; column++)
            {
                double value = matrix[row, column];
                for (int index = 0; index < column; index++) value -= lower[row, index] * lower[column, index];
                if (row == column && (!(value > 0) || !double.IsFinite(value))) return false;
                lower[row, column] = row == column ? Math.Sqrt(value) : value / lower[column, column];
            }
            return true;
        }

        private static double[] Solve(double[,] lower, double[] value, bool transpose)
        {
            int count = lower.GetLength(0);
            var result = new double[count];
            for (int step = 0; step < count; step++)
            {
                int row = transpose ? count - 1 - step : step;
                double remaining = value[row];
                for (int before = 0; before < step; before++)
                {
                    int column = transpose ? count - 1 - before : before;
                    remaining -= (transpose ? lower[column, row] : lower[row, column]) * result[column];
                }
                result[row] = remaining / lower[row, row];
            }
            return result;
        }

        private static bool IsFeasible(double[] slack)
        {
            foreach (double value in slack)
                if (!(value >= -ConstraintTolerance)) return false;
            return true;
        }

        private static double Merit(Evaluation sample, double[] rowNorm, double weight)
        {
            double violation = 0;
            for (int row = 0; row < rowNorm.Length; row++)
                violation = Math.Max(violation, -sample.Slack[row] / Math.Max(rowNorm[row], 1e-6));
            return sample.Cost + weight * violation;
        }

        private static double[] Move(double[] origin, double[] direction, double scale)
        {
            var result = new double[origin.Length];
            for (int index = 0; index < result.Length; index++) result[index] = origin[index] + scale * direction[index];
            return result;
        }

        private static double Norm(double[] value)
        {
            double squared = 0;
            foreach (double item in value) squared += item * item;
            return Math.Sqrt(squared);
        }
    }
}
