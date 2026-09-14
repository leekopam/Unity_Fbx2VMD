using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootSupportRoleCalculatorTests
    {
        [Test]
        public void Given_AbruptRoleReversal_When_Building_Then_LimitsTransitionWithoutRemovingSupport()
        {
            var source = Enumerable.Repeat(Vector2.right, 32).ToArray();
            var heights = Enumerable.Range(0, 32).Select(i => i < 8 ? -0.02f : 0.02f).ToArray();
            Vector2[] original = source.ToArray();
            float[] originalHeights = heights.ToArray();
            Vector2[] weights = Build(source, heights);
            Assert.That(weights[0], Is.EqualTo(Vector2.up), "기존 본 채널과 달라도 낮은 앞쪽에 지지를 배분해야 합니다.");
            Assert.That(weights[8].x, Is.GreaterThan(0f).And.LessThan(0.2f));
            Assert.That(weights[25], Is.EqualTo(Vector2.right));
            for (int i = 0; i < weights.Length; i++)
            {
                Assert.That(Mathf.Max(weights[i].x, weights[i].y), Is.EqualTo(1f));
                if (i > 0)
                    Assert.That(Mathf.Abs((weights[i].x - weights[i].y) -
                        (weights[i - 1].x - weights[i - 1].y)), Is.LessThanOrEqualTo(1f / 6f + 0.000001f));
            }
            CollectionAssert.AreEqual(original, source);
            CollectionAssert.AreEqual(originalHeights, heights);
            CollectionAssert.AreEqual(weights, Build(source, heights));
        }

        [Test]
        public void Given_ReleaseAndReentry_When_Building_Then_ReleasesImmediatelyAndStartsAtCurrentRole()
        {
            Vector2[] source = { Vector2.up, Vector2.up * 0.4f, Vector2.zero, Vector2.right * 0.7f };
            float[] heights = { -0.02f, 0.02f, 0.02f, 0.02f };
            Vector2[] weights = Build(source, heights);
            for (int i = 0; i < weights.Length; i++)
                Assert.That(Mathf.Max(weights[i].x, weights[i].y),
                    Is.EqualTo(Mathf.Max(source[i].x, source[i].y)));
            Assert.That(weights[2], Is.EqualTo(Vector2.zero));
            Assert.That(weights[3], Is.EqualTo(Vector2.right * 0.7f));
        }

        [Test]
        public void Given_InvalidInput_When_Building_Then_ReturnsNoPartialWeights()
        {
            object[][] cases = {
                new object[] { null, new float[1], 60f, 0.003f, 0.1f, null },
                new object[] { new Vector2[1], new float[2], 60f, 0.003f, 0.1f, null },
                new object[] { new[] { Vector2.one }, new[] { float.NaN }, 60f, 0.003f, 0.1f, null },
                new object[] { new[] { Vector2.one * 2f }, new float[1], 60f, 0.003f, 0.1f, null },
                new object[] { new Vector2[1], new float[1], 0f, 0.003f, 0.1f, null },
                new object[] { new Vector2[1], new float[1], 60f, float.PositiveInfinity, 0.1f, null },
                new object[] { new Vector2[1], new float[1], float.MaxValue, 0.003f, float.MaxValue, null }
            };
            foreach (object[] args in cases)
            {
                Assert.That((bool)GetMethod().Invoke(null, args), Is.False);
                Assert.That((Vector2[])args[args.Length - 1], Is.Empty);
            }
        }

        [Test]
        public void Given_FractionalRoleCrossing_When_Interpolating_Then_PreservesWholeSupportAndRelease()
        {
            MethodInfo method = GetMethod().DeclaringType.GetMethod("Interpolate", BindingFlags.Static | BindingFlags.NonPublic);
            Vector2 value = (Vector2)method.Invoke(null, new object[] { new Vector2(1f, 0.9f), new Vector2(0.9f, 1f), 0.5f });
            Assert.That(value, Is.EqualTo(Vector2.one), "소수 시각의 역할 교차가 전체 지지를 줄이면 안 됩니다.");
            value = (Vector2)method.Invoke(null, new object[] { new Vector2(0.4f, 0.8f), Vector2.zero, 0.5f });
            Assert.That(value, Is.EqualTo(new Vector2(0.2f, 0.4f)));
            value = (Vector2)method.Invoke(null, new object[] { Vector2.right, Vector2.zero, 1f });
            Assert.That(value, Is.EqualTo(Vector2.zero));
        }

        private static Vector2[] Build(Vector2[] source, float[] heights)
        {
            object[] args = { source, heights, 60f, 0.003f, 0.1f, null };
            Assert.That((bool)GetMethod().Invoke(null, args), Is.True);
            return (Vector2[])args[args.Length - 1];
        }

        private static MethodInfo GetMethod()
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidFootSupportRoleCalculator");
            Assert.That(type, Is.Not.Null);
            return type.GetMethod("TryBuild", BindingFlags.Static | BindingFlags.NonPublic);
        }
    }
}
