using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootSupportWeightCalculatorTests
    {
        [Test]
        public void Given_AirborneOrBriefContact_When_Building_Then_RejectsSupport()
        {
            var rear = Enumerable.Repeat(Vector3.up, 40).ToArray();
            rear[15] = rear[16] = Vector3.zero;
            Vector3[] front = Enumerable.Range(0, 40)
                .Select(i => new Vector3(0f, i * 0.01f, 0f)).ToArray();
            Vector2[] weights = Build(rear, front);
            Assert.That(weights.All(w => w == Vector2.zero), Is.True);
            var sixSamples = new Vector3[6];
            Assert.That(Build(sixSamples, sixSamples).All(w => w == Vector2.zero), Is.True);
            var sevenSamples = new Vector3[7];
            Assert.That(Build(sevenSamples, sevenSamples).All(w => w == Vector2.one), Is.True);
        }

        [Test]
        public void Given_RearToFrontSupport_When_Building_Then_BlendsAndReleasesDeterministically()
        {
            var rear = Enumerable.Range(0, 100).Select(i => i < 60 ? Vector3.zero : Vector3.up).ToArray();
            var front = Enumerable.Range(0, 100).Select(i => i >= 30 && i < 90 ? Vector3.zero : Vector3.up).ToArray();
            Vector2[] weights = Build(rear, front);
            Assert.That(weights[20], Is.EqualTo(Vector2.right));
            Assert.That(weights[45], Is.EqualTo(Vector2.one));
            Assert.That(weights[75], Is.EqualTo(Vector2.up));
            Assert.That(weights[95], Is.EqualTo(Vector2.zero));
            for (int i = 1; i < weights.Length; i++)
                Assert.That(Vector2.Distance(weights[i], weights[i - 1]), Is.LessThan(0.26f));
            CollectionAssert.AreEqual(weights, Build(rear, front));
        }

        [Test]
        public void Given_SlowSlideAndRapidReversal_When_Building_Then_PreservesInputAndRejectsReversal()
        {
            var rear = Enumerable.Range(0, 40).Select(i => Vector3.right * (i * 0.001f)).ToArray();
            var front = Enumerable.Range(0, 40).Select(i => Vector3.right * (i % 2 == 0 ? 0f : 0.05f)).ToArray();
            Vector3[] original = rear.ToArray();
            Vector2[] weights = Build(rear, front);
            Assert.That(weights[20].x, Is.EqualTo(1f));
            Assert.That(weights.All(w => w.y == 0f), Is.True);
            CollectionAssert.AreEqual(original, rear);
        }

        [Test]
        public void Given_InvalidInput_When_Building_Then_RejectsWithoutPartialWeights()
        {
            var points = Enumerable.Repeat(Vector3.zero, 10).ToArray();
            object[] args = CreateArguments(points, points);
            args[2] = 0f;
            Assert.That((bool)GetMethod().Invoke(null, args), Is.False);
            Assert.That((Vector2[])args[8], Is.Empty);
            points[4] = new Vector3(float.NaN, 0f, 0f);
            args = CreateArguments(points, points);
            Assert.That((bool)GetMethod().Invoke(null, args), Is.False);
            Assert.That((Vector2[])args[8], Is.Empty);
        }

        private static Vector2[] Build(Vector3[] rear, Vector3[] front)
        {
            object[] args = CreateArguments(rear, front);
            Assert.That((bool)GetMethod().Invoke(null, args), Is.True);
            return (Vector2[])args[8];
        }

        private static object[] CreateArguments(Vector3[] rear, Vector3[] front)
        {
            return new object[] { rear, front, 60f, Vector2.zero, 0.03f, 0.15f, 0.1f, 0.1f, null };
        }

        private static MethodInfo GetMethod()
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidFootSupportWeightCalculator");
            Assert.That(type, Is.Not.Null, "원본 궤적의 지지 가중치 계산이 필요합니다.");
            return type.GetMethod("TryBuild", BindingFlags.Static | BindingFlags.NonPublic);
        }
    }
}
