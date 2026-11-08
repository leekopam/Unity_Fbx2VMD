using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class HumanoidSupportCurveProblem
    {
        private const int SamplesPerFrame = 4;
        private const double DegreesPerRadian = 180d / Math.PI;
        private const double MaximumShiftMm = 20;
        private readonly Sample[] _samples;
        private readonly double[,] _basis;
        private readonly double[] _baselineRms;
        private readonly double[] _stepLimits;
        private readonly double[] _accelerationLimits;
        private readonly double[] _hipLimits;
        private readonly int _first;
        private readonly int _last;
        private readonly int _controls;
        private readonly HumanoidSupportCurveBasis _curveBasis;
        private readonly int _startFrame;
        private readonly int _endFrame;
        private bool _balanceAbsoluteErrors;

        internal readonly struct LegGeometry
        {
            internal LegGeometry(double3 upper, double3 knee, double3 foot, double referenceDegrees)
            {
                HipToAnkle = upper - foot;
                double upperLength = math.distance(upper, knee);
                double lowerLength = math.distance(knee, foot);
                LengthProduct = upperLength * lowerLength;
                SquaredLengths = upperLength * upperLength + lowerLength * lowerLength;
                ReferenceDegrees = referenceDegrees;
            }

            internal double3 HipToAnkle { get; }
            internal double LengthProduct { get; }
            internal double SquaredLengths { get; }
            internal double ReferenceDegrees { get; }
            internal bool IsValid => LengthProduct > 0 && double.IsFinite(LengthProduct) &&
                math.all(math.isfinite(HipToAnkle)) && double.IsFinite(ReferenceDegrees);

            internal double MeasureFlexion(double3 reach, out double cosine)
            {
                cosine = (math.lengthsq(reach) - SquaredLengths) / (2 * LengthProduct);
                return Math.Acos(Math.Clamp(cosine, -1, 1)) * DegreesPerRadian;
            }
        }

        internal sealed class Curve
        {
            private readonly HumanoidSupportCurveBasis _basis;
            private readonly double[] _values;

            internal Curve(int start, int end, HumanoidSupportCurveBasis basis, double[] values)
            {
                Start = start;
                End = end;
                _basis = basis;
                _values = values;
            }

            internal int Start { get; }
            internal int End { get; }

            internal void WriteCacheIdentity(BinaryWriter writer)
            {
                writer.Write(Start);
                writer.Write(End);
                writer.Write(_basis.Spacing);
                writer.Write(_values.Length);
                foreach (double value in _values) writer.Write(value);
            }

            internal double3 GetShiftMm(double frame)
            {
                Span<double> weights = stackalloc double[4];
                int control = _basis.Evaluate(frame, weights);
                double3 shift = double3.zero;
                for (int index = 0; index < weights.Length; index++)
                {
                    int offset = (control + index) * 3;
                    shift += weights[index] * new double3(_values[offset], _values[offset + 1], _values[offset + 2]);
                }
                double length = math.length(shift);
                return length > MaximumShiftMm ? shift * (MaximumShiftMm / length) : shift;
            }
        }

        internal readonly struct Sample
        {
            internal Sample(double3 hip, LegGeometry left, LegGeometry right)
            {
                Hip = hip;
                Left = left;
                Right = right;
            }

            internal double3 Hip { get; }
            internal LegGeometry Left { get; }
            internal LegGeometry Right { get; }
            internal LegGeometry Leg(int side) => side == 0 ? Left : Right;
        }

        internal HumanoidSupportCurveProblem(Sample[] samples, int captureStart, int first, int last,
            int spacing, IReadOnlyList<int> boundaries)
        {
            _samples = samples;
            _startFrame = first;
            _endFrame = last;
            _first = (first - captureStart) * SamplesPerFrame;
            _last = (last - captureStart) * SamplesPerFrame;
            _curveBasis = new HumanoidSupportCurveBasis(first, last, spacing);
            _controls = _curveBasis.ControlCount;
            _basis = new double[samples.Length, _controls];
            var baseline = new double[samples.Length, 2];
            var sourceError = new double[2];
            var steps = new double[(samples.Length - 1) * 2];
            var acceleration = new double[(samples.Length - 2) * 2];
            var hipAcceleration = new double[samples.Length - 2];
            Span<double> local = stackalloc double[4];
            for (int index = 0; index < samples.Length; index++)
            {
                int control = _curveBasis.Evaluate(captureStart + index / (double)SamplesPerFrame, local);
                for (int offset = 0; offset < local.Length; offset++) _basis[index, control + offset] = local[offset];
                for (int side = 0; side < 2; side++)
                {
                    var leg = samples[index].Leg(side);
                    baseline[index, side] = leg.MeasureFlexion(leg.HipToAnkle, out _);
                    double error = baseline[index, side] - leg.ReferenceDegrees;
                    if (index >= _first && index <= _last) sourceError[side] += error * error;
                    if (index >= 1) steps[2 * (index - 1) + side] = baseline[index, side] - baseline[index - 1, side];
                    if (index >= 2) acceleration[2 * (index - 2) + side] = baseline[index, side] -
                        2 * baseline[index - 1, side] + baseline[index - 2, side];
                }
                if (index >= 2) hipAcceleration[index - 2] = math.length(samples[index].Hip - 2 * samples[index - 1].Hip + samples[index - 2].Hip);
            }
            _baselineRms = Array.ConvertAll(sourceError, value => Math.Sqrt(value / (_last - _first + 1)));
            _stepLimits = Limits(steps, 2, 1, .00125, captureStart, boundaries);
            _accelerationLimits = Limits(acceleration, 2, 2, .0003125, captureStart, boundaries);
            _hipLimits = Limits(hipAcceleration, 1, 2, .0005, captureStart, boundaries);
        }

        internal bool TryGenerate(out Curve curve)
        {
            curve = null;
            if (_baselineRms[0] <= 1e-6 || _baselineRms[1] <= 1e-6) return false;
            if (!TryGenerateControls(out var controls))
            {
                _balanceAbsoluteErrors = true;
                if (!TryGenerateControls(out controls)) return false;
            }
            curve = new Curve(_startFrame, _endFrame, _curveBasis, controls);
            return true;
        }

        private bool TryGenerateControls(out double[] controls)
        {
            if (!HumanoidSupportCurveOptimizer.TrySolve(_controls * 3, Evaluate, out controls)) return false;
            var rms = ReadRms(controls);
            return rms[0] < _baselineRms[0] - .001 && rms[1] < _baselineRms[1] - .001;
        }

        private double3 ShiftAt(int index, double[] controls)
        {
            double3 shift = double3.zero;
            for (int control = 0; control < _controls; control++)
            {
                int offset = 3 * control;
                shift += _basis[index, control] * new double3(controls[offset], controls[offset + 1], controls[offset + 2]);
            }
            return shift;
        }

        private double[] ReadRms(double[] controls)
        {
            var squared = new double[2];
            for (int index = _first; index <= _last; index++)
            {
                double3 shift = ShiftAt(index, controls);
                for (int side = 0; side < 2; side++)
                {
                    var leg = _samples[index].Leg(side);
                    double error = leg.MeasureFlexion(leg.HipToAnkle + shift, out _) - leg.ReferenceDegrees;
                    squared[side] += error * error;
                }
            }
            return Array.ConvertAll(squared, value => Math.Sqrt(value / (_last - _first + 1)));
        }

        private HumanoidSupportCurveOptimizer.Evaluation Evaluate(double[] controls, bool derivatives)
        {
            int frameCount = _samples.Length;
            int count = controls.Length;
            int activeCount = _last - _first + 1;
            var shifts = new double3[frameCount];
            var angles = new double[frameCount, 2];
            var cosines = new double[frameCount, 2];
            var angleGradient = new double3[frameCount, 2];
            var cosineGradient = new double3[frameCount, 2];
            var errors = new double[2];
            double leftSquared = _baselineRms[0] * _baselineRms[0];
            double rightSquared = _baselineRms[1] * _baselineRms[1];
            double leftWeight = _balanceAbsoluteErrors ? 2 * leftSquared / (leftSquared + rightSquared) : 1;
            double rightWeight = 2 - leftWeight;
            double[] gradient = derivatives ? new double[count] : null;
            double[,] metric = derivatives ? new double[count, count] : null;
            double[][] sideGradient = derivatives ? new[] { new double[count], new double[count] } : null;
            var slack = new List<double>();
            var jacobian = derivatives ? new List<double[]>() : null;
            for (int index = 0; index < frameCount; index++)
            {
                shifts[index] = ShiftAt(index, controls);
                for (int side = 0; side < 2; side++)
                {
                    var leg = _samples[index].Leg(side);
                    double3 reach = leg.HipToAnkle + shifts[index];
                    angles[index, side] = leg.MeasureFlexion(reach, out double cosine);
                    cosines[index, side] = cosine;
                    if (derivatives)
                    {
                        cosineGradient[index, side] = reach / leg.LengthProduct;
                        angleGradient[index, side] = -cosineGradient[index, side] *
                            (DegreesPerRadian / Math.Sqrt(Math.Max(1e-12, 1 - cosine * cosine)));
                    }
                    if (index < _first || index > _last) continue;

                    double relative = (angles[index, side] - leg.ReferenceDegrees) / _baselineRms[side];
                    errors[side] += relative * relative;
                    if (!derivatives) continue;
                    var row = new double[count];
                    Add(row, index, angleGradient[index, side] / _baselineRms[side]);
                    for (int axis = 0; axis < count; axis++)
                    {
                        if (row[axis] == 0) continue;
                        sideGradient[side][axis] += 2 * relative * row[axis] / activeCount;
                        double weight = side == 0 ? leftWeight : rightWeight;
                        for (int column = 0; column <= axis; column++)
                            metric[axis, column] += weight * row[axis] * row[column] / activeCount;
                    }
                }
            }

            AddAngularConstraints(_stepLimits, 1);
            AddAngularConstraints(_accelerationLimits, 2);
            for (int index = 0; index + 2 < frameCount; index++)
            {
                double3 acceleration = _samples[index + 2].Hip - 2 * _samples[index + 1].Hip + _samples[index].Hip +
                    shifts[index + 2] - 2 * shifts[index + 1] + shifts[index];
                double length = math.length(acceleration);
                double[] row = MakeRow(_hipLimits[index] - length);
                if (derivatives)
                {
                    double3 normal = -acceleration / Math.Max(1e-12, length);
                    Add(row, index, normal);
                    Add(row, index + 1, -2 * normal);
                    Add(row, index + 2, normal);
                }
            }
            double maximumSquared = MaximumShiftMm * MaximumShiftMm;
            for (int index = 0; index < frameCount; index++)
            {
                double[] row = MakeRow(1 - math.lengthsq(shifts[index]) / maximumSquared);
                if (derivatives) Add(row, index, -2 * shifts[index] / maximumSquared);
            }
            for (int sign = -1; sign <= 1; sign += 2)
            for (int index = _first; index <= _last; index++)
            for (int side = 0; side < 2; side++)
            {
                double[] row = MakeRow(1 + sign * cosines[index, side]);
                if (derivatives) Add(row, index, sign * cosineGradient[index, side]);
            }
            for (int side = 0; side < 2; side++)
            {
                double[] row = MakeRow(1 - errors[side] / activeCount);
                if (derivatives)
                    for (int axis = 0; axis < count; axis++) row[axis] = -sideGradient[side][axis];
            }
            if (derivatives)
            {
                for (int axis = 0; axis < count; axis++)
                {
                    gradient[axis] = (leftWeight * sideGradient[0][axis] + rightWeight * sideGradient[1][axis]) * .5;
                    metric[axis, axis] += 1e-5;
                }
            }
            return new HumanoidSupportCurveOptimizer.Evaluation((leftWeight * errors[0] + rightWeight * errors[1]) / (2 * activeCount),
                slack.ToArray(), gradient, metric, jacobian?.ToArray());

            void AddAngularConstraints(double[] limits, int order)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                for (int index = 0; index + order < frameCount; index++)
                for (int side = 0; side < 2; side++)
                {
                    double difference = order == 1 ? angles[index + 1, side] - angles[index, side] :
                        angles[index + 2, side] - 2 * angles[index + 1, side] + angles[index, side];
                    double[] row = MakeRow(limits[2 * index + side] + sign * difference);
                    if (!derivatives) continue;
                    Add(row, index, (order == 1 ? -sign : sign) * angleGradient[index, side]);
                    Add(row, index + 1, (order == 1 ? sign : -2 * sign) * angleGradient[index + 1, side]);
                    if (order == 2) Add(row, index + 2, sign * angleGradient[index + 2, side]);
                }
            }

            double[] MakeRow(double value)
            {
                slack.Add(value);
                if (!derivatives) return null;
                var row = new double[count];
                jacobian.Add(row);
                return row;
            }
        }

        private void Add(double[] row, int index, double3 gradient)
        {
            for (int control = 0; control < _controls; control++)
            {
                double weight = _basis[index, control];
                if (weight == 0) continue;
                int offset = 3 * control;
                row[offset] += weight * gradient.x;
                row[offset + 1] += weight * gradient.y;
                row[offset + 2] += weight * gradient.z;
            }
        }

        private static double[] Limits(double[] values, int channels, int order, double tolerance,
            int captureStart, IReadOnlyList<int> boundaries)
        {
            var limits = new double[values.Length];
            Apply(0, values.Length / channels, false);
            foreach (int boundary in boundaries)
            {
                int first = (boundary - 1 - captureStart) * SamplesPerFrame;
                int end = (boundary + 1 - captureStart) * SamplesPerFrame + 1 - order;
                if (first >= 0 && end <= values.Length / channels) Apply(first, end, true);
            }
            return limits;

            void Apply(int first, int end, bool tighten)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    double maximum = 0;
                    for (int index = first; index < end; index++) maximum = Math.Max(maximum, Math.Abs(values[index * channels + channel]));
                    for (int index = first; index < end; index++)
                    {
                        int offset = index * channels + channel;
                        limits[offset] = tighten ? Math.Min(limits[offset], maximum + tolerance) : maximum + tolerance;
                    }
                }
            }
        }
    }
}
