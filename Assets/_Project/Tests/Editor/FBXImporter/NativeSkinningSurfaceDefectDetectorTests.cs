using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningSurfaceDefectDetectorTests
    {
        [Test]
        public void Given_SurfacePairs_When_CheckingCosineThresholds_Then_MatchesCorrectionContract()
        {
            Type detectorType = RequireProductType(
                "NativeSkinningSurfaceDefectDetector");
            MethodInfo method = detectorType.GetMethod(
                "IsCorrectable",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            object pair = CreateFacePair();
            Vector3[] sharpFold = CreateHingedPair(160f);
            Vector3[] collapsed = CreateHingedPair(110f);
            object sharpRestLengths = MeasureRestLengths(sharpFold, pair);
            object collapsedRestLengths = CreateRestLengths(10f);

            Assert.That(
                Invoke(method, sharpFold, pair, sharpRestLengths, 10f),
                Is.True);
            Assert.That(
                Invoke(method, collapsed, pair, collapsedRestLengths, 10f),
                Is.True);
            Assert.That(
                Invoke(
                    method,
                    collapsed,
                    pair,
                    MeasureRestLengths(collapsed, pair),
                    10f),
                Is.False);
            Assert.That(
                Invoke(method, sharpFold, pair, sharpRestLengths, 40f),
                Is.False,
                "휴식 자세부터 접힌 면은 보정 대상으로 확대하면 안 됩니다.");
        }

        private static bool Invoke(
            MethodInfo method,
            Vector3[] vertices,
            object pair,
            object restLengths,
            float restAngleDegrees)
        {
            float maximumNonSevereCosine = Mathf.Cos(
                (restAngleDegrees + 90f) * Mathf.Deg2Rad);
            return (bool)method.Invoke(
                null,
                new[]
                {
                    vertices,
                    pair,
                    restLengths,
                    restAngleDegrees,
                    (object)maximumNonSevereCosine
                });
        }

        private static Vector3[] CreateHingedPair(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                new Vector3(0f, -Mathf.Cos(radians), Mathf.Sin(radians))
            };
        }

        private static object CreateFacePair()
        {
            Type edgeType = RequireProductType("NativeSkinningEdgeKey");
            object edge = Create(edgeType, 0, 1);
            Type pairType = RequireProductType("NativeSkinningFacePair");
            return Create(pairType, edge, 0, 1, 2, 3, 0, 1);
        }

        private static object MeasureRestLengths(Vector3[] vertices, object pair)
        {
            Type detectorType = RequireProductType(
                "NativeSkinningRestShapeCollapseDetector");
            MethodInfo method = detectorType.GetMethod(
                "MeasureRestLengths",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new[] { vertices, pair });
        }

        private static object CreateRestLengths(float length)
        {
            Type type = RequireProductType("NativeSkinningFacePairRestLengths");
            return Create(type, length, length, length, length, length);
        }

        private static object Create(Type type, params object[] arguments)
        {
            return Activator.CreateInstance(
                type,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                arguments,
                null);
        }

        private static Type RequireProductType(string shortName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                $"Fbx2Vmd.FBXImporter.{shortName}",
                false);
            Assert.That(type, Is.Not.Null, $"{shortName} 타입이 필요합니다.");
            return type;
        }
    }
}
