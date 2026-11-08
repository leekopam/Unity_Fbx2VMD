using System;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class HumanoidSupportCurveBasis
    {
        private const int Degree = 3;
        private readonly int _first;
        private readonly int _last;
        private readonly int _spacing;
        private readonly double _fadeWidth;
        private readonly double[] _knots;

        internal HumanoidSupportCurveBasis(int first, int last, int spacing)
        {
            if (first < 0 || last <= first || spacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(spacing));

            _first = first;
            _last = last;
            _spacing = spacing;
            _fadeWidth = Math.Min(10, (last - first) / 3d);
            int interiorCount = (last - first - 1) / spacing;
            ControlCount = interiorCount + Degree + 1;
            _knots = new double[interiorCount + 2 * (Degree + 1)];
            Array.Fill(_knots, first, 0, Degree + 1);
            for (int index = 1; index <= interiorCount; index++)
                _knots[Degree + index] = first + index * spacing;
            Array.Fill(_knots, last, ControlCount, Degree + 1);
        }

        internal int ControlCount { get; }
        internal int Spacing => _spacing;

        // 지지 창의 양 끝에서 이동·속도·가속이 0으로 돌아오도록 감쇠를 적용함.
        internal int Evaluate(double frame, Span<double> weights)
        {
            if (weights.Length != Degree + 1)
                throw new ArgumentException("곡선 가중치 네 개 필요", nameof(weights));

            weights.Clear();
            if (!(frame > _first && frame < _last))
                return 0;

            int span = Math.Min(Degree + (int)((frame - _first) / _spacing), ControlCount - 1);
            Span<double> left = stackalloc double[Degree + 1];
            Span<double> right = stackalloc double[Degree + 1];
            weights[0] = 1;
            for (int order = 1; order <= Degree; order++)
            {
                left[order] = frame - _knots[span + 1 - order];
                right[order] = _knots[span + order] - frame;
                double carried = 0;
                for (int index = 0; index < order; index++)
                {
                    double ratio = weights[index] / (right[index + 1] + left[order - index]);
                    weights[index] = carried + right[index + 1] * ratio;
                    carried = left[order - index] * ratio;
                }
                weights[order] = carried;
            }

            double envelope = Ease((frame - _first) / _fadeWidth) * Ease((_last - frame) / _fadeWidth);
            for (int index = 0; index <= Degree; index++)
                weights[index] *= envelope;
            return span - Degree;
        }

        private static double Ease(double value)
        {
            double t = Math.Min(value, 1);
            return t * t * t * (10 + t * (-15 + 6 * t));
        }
    }
}
