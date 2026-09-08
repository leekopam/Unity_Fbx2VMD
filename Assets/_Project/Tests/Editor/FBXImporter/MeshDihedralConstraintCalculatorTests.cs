using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class MeshDihedralConstraintCalculatorTests
    {
        private const string CalculatorTypeName =
            "Fbx2Vmd.FBXImporter.MeshDihedralConstraintCalculator";

        [Test]
        public void Given_FoldedSurface_When_CalculatingAngle_Then_ReturnsExpectedDihedralAngle()
        {
            MethodInfo method = FindCalculatorMethod("TryCalculateAngleDegrees");
            CreateFoldedSurface(
                170f,
                out Vector3 opposite0,
                out Vector3 opposite1,
                out Vector3 edge0,
                out Vector3 edge1);
            object[] arguments =
            {
                opposite0,
                opposite1,
                edge0,
                edge1,
                0f
            };

            bool succeeded = (bool)method.Invoke(null, arguments);

            Assert.That(succeeded, Is.True);
            Assert.That((float)arguments[4], Is.EqualTo(170f).Within(0.001f));
        }

        [Test]
        public void Given_SmallScaleFoldedSurface_When_CalculatingAngle_Then_PreservesAngle()
        {
            MethodInfo method = FindCalculatorMethod("TryCalculateAngleDegrees");
            CreateFoldedSurface(
                170f,
                0.001f,
                out Vector3 opposite0,
                out Vector3 opposite1,
                out Vector3 edge0,
                out Vector3 edge1);
            object[] arguments =
            {
                opposite0,
                opposite1,
                edge0,
                edge1,
                0f
            };

            bool succeeded = (bool)method.Invoke(null, arguments);

            Assert.That(succeeded, Is.True);
            Assert.That((float)arguments[4], Is.EqualTo(170f).Within(0.001f));
        }

        [Test]
        public void Given_ExcessiveFold_When_ProjectingConstraint_Then_AngleMovesTowardLimit()
        {
            MethodInfo projectionMethod = FindCalculatorMethod("TryCalculateCorrections");
            MethodInfo angleMethod = FindCalculatorMethod("TryCalculateAngleDegrees");
            CreateFoldedSurface(
                170f,
                out Vector3 opposite0,
                out Vector3 opposite1,
                out Vector3 edge0,
                out Vector3 edge1);

            object[] projectionArguments =
            {
                opposite0,
                opposite1,
                edge0,
                edge1,
                149f,
                0.8f,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero
            };
            bool succeeded = (bool)projectionMethod.Invoke(null, projectionArguments);

            Assert.That(succeeded, Is.True);
            Vector3 correctedOpposite0 = opposite0 + (Vector3)projectionArguments[6];
            Vector3 correctedOpposite1 = opposite1 + (Vector3)projectionArguments[7];
            Vector3 correctedEdge0 = edge0 + (Vector3)projectionArguments[8];
            Vector3 correctedEdge1 = edge1 + (Vector3)projectionArguments[9];
            object[] angleArguments =
            {
                correctedOpposite0,
                correctedOpposite1,
                correctedEdge0,
                correctedEdge1,
                0f
            };
            bool angleSucceeded = (bool)angleMethod.Invoke(null, angleArguments);

            Assert.That(angleSucceeded, Is.True);
            Assert.That((float)angleArguments[4], Is.LessThan(170f));
            Assert.That((float)angleArguments[4], Is.GreaterThan(140f));
            AssertFinite((Vector3)projectionArguments[6]);
            AssertFinite((Vector3)projectionArguments[7]);
            AssertFinite((Vector3)projectionArguments[8]);
            AssertFinite((Vector3)projectionArguments[9]);
        }

        [Test]
        public void Given_TranslatedSurface_When_ProjectingConstraint_Then_CorrectionsRemainEquivalent()
        {
            MethodInfo method = FindCalculatorMethod("TryCalculateCorrections");
            CreateFoldedSurface(
                165f,
                out Vector3 opposite0,
                out Vector3 opposite1,
                out Vector3 edge0,
                out Vector3 edge1);
            object[] originArguments = CreateProjectionArguments(
                opposite0,
                opposite1,
                edge0,
                edge1);
            Vector3 translation = new Vector3(13f, -7f, 5f);
            object[] translatedArguments = CreateProjectionArguments(
                opposite0 + translation,
                opposite1 + translation,
                edge0 + translation,
                edge1 + translation);

            bool originSucceeded = (bool)method.Invoke(null, originArguments);
            bool translatedSucceeded = (bool)method.Invoke(null, translatedArguments);

            Assert.That(originSucceeded, Is.True);
            Assert.That(translatedSucceeded, Is.True);
            for (int i = 6; i <= 9; i++)
            {
                Assert.That(
                    Vector3.Distance(
                        (Vector3)originArguments[i],
                        (Vector3)translatedArguments[i]),
                    Is.LessThan(0.00001f));
            }
        }

        [Test]
        public void Given_DegenerateSharedEdge_When_ProjectingConstraint_Then_FailsWithoutCorrection()
        {
            MethodInfo method = FindCalculatorMethod("TryCalculateCorrections");
            Vector3 sharedPoint = new Vector3(1f, 2f, 3f);
            object[] arguments =
            {
                Vector3.up,
                Vector3.down,
                sharedPoint,
                sharedPoint,
                149f,
                0.8f,
                Vector3.one,
                Vector3.one,
                Vector3.one,
                Vector3.one
            };

            bool succeeded = (bool)method.Invoke(null, arguments);

            Assert.That(succeeded, Is.False);
            for (int i = 6; i <= 9; i++)
            {
                Assert.That((Vector3)arguments[i], Is.EqualTo(Vector3.zero));
            }
        }

        private static object[] CreateProjectionArguments(
            Vector3 opposite0,
            Vector3 opposite1,
            Vector3 edge0,
            Vector3 edge1)
        {
            return new object[]
            {
                opposite0,
                opposite1,
                edge0,
                edge1,
                149f,
                0.8f,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero
            };
        }

        private static void CreateFoldedSurface(
            float angleDegrees,
            out Vector3 opposite0,
            out Vector3 opposite1,
            out Vector3 edge0,
            out Vector3 edge1)
        {
            CreateFoldedSurface(
                angleDegrees,
                1f,
                out opposite0,
                out opposite1,
                out edge0,
                out edge1);
        }

        private static void CreateFoldedSurface(
            float angleDegrees,
            float scale,
            out Vector3 opposite0,
            out Vector3 opposite1,
            out Vector3 edge0,
            out Vector3 edge1)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            opposite0 = new Vector3(0f, 1f, 0f) * scale;
            opposite1 = new Vector3(
                0f,
                -Mathf.Cos(radians),
                Mathf.Sin(radians)) * scale;
            edge0 = Vector3.zero;
            edge1 = Vector3.right * scale;
        }

        private static MethodInfo FindCalculatorMethod(string methodName)
        {
            Type calculatorType = typeof(AssimpFBXImporter).Assembly.GetType(CalculatorTypeName);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(float.IsNaN(value.x) || float.IsInfinity(value.x), Is.False);
            Assert.That(float.IsNaN(value.y) || float.IsInfinity(value.y), Is.False);
            Assert.That(float.IsNaN(value.z) || float.IsInfinity(value.z), Is.False);
        }
    }
}
