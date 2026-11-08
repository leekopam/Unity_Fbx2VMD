using System.IO;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using Unity.Mathematics;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidSupportCurveTests
    {
        [Test]
        public void Given_PreparedCurve_When_SeekingFractionalBoundaries_Then_ReturnsToZero()
        {
            var basis = new HumanoidSupportCurveBasis(10, 22, 4);
            var controls = new double[basis.ControlCount * 3];
            for (int index = 0; index < controls.Length; index += 3) controls[index] = 10;
            var curve = new HumanoidSupportCurveProblem.Curve(10, 22, basis, controls);

            Assert.That(curve.GetShiftMm(9.999), Is.EqualTo(double3.zero));
            Assert.That(curve.GetShiftMm(10), Is.EqualTo(double3.zero));
            Assert.That(curve.GetShiftMm(10.001).x, Is.GreaterThan(0).And.LessThan(1e-7));
            Assert.That(curve.GetShiftMm(16).x, Is.EqualTo(10).Within(1e-10));
            Assert.That(curve.GetShiftMm(21.999).x, Is.GreaterThan(0).And.LessThan(1e-7));
            Assert.That(curve.GetShiftMm(22), Is.EqualTo(double3.zero));
        }

        [Test]
        public void Given_SameFrameRange_When_ControlChanges_Then_CacheIdentityChanges()
        {
            var basis = new HumanoidSupportCurveBasis(10, 22, 4);
            var controls = new double[basis.ControlCount * 3];
            byte[] first = Identity(controls);
            Assert.That(Identity(controls), Is.EqualTo(first));
            controls[1] = .01;
            Assert.That(Identity(controls), Is.Not.EqualTo(first));

            byte[] Identity(double[] values)
            {
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream);
                new HumanoidSupportCurveProblem.Curve(10, 22, basis, values).WriteCacheIdentity(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        [Test]
        public void Given_TwoBindingConstraints_When_Fitting_Then_ReturnsFeasibleImprovedCurveControls()
        {
            bool solved = HumanoidSupportCurveOptimizer.TrySolve(2, Evaluate, out double[] solution);
            Assert.That(solved, Is.True);
            Assert.That(solution[0], Is.EqualTo(System.Math.Sqrt(.99)).Within(1e-4));
            Assert.That(solution[1], Is.EqualTo(.1).Within(1e-4));

            static HumanoidSupportCurveOptimizer.Evaluation Evaluate(double[] value, bool derivatives)
            {
                double x = value[0], y = value[1];
                double dx = x - 1, dy = y - 1;
                double cost = dx * dx + dy * dy;
                var slack = new[] { 1 - x * x - y * y, .1 - y };
                return derivatives ? new HumanoidSupportCurveOptimizer.Evaluation(cost, slack,
                    new[] { 2 * dx, 2 * dy }, new double[,] { { 2, 0 }, { 0, 2 } },
                    new[] { new[] { -2 * x, -2 * y }, new[] { 0d, -1d } }) :
                    new HumanoidSupportCurveOptimizer.Evaluation(cost, slack);
            }
        }
    }
}
