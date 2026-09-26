using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootAnchorPolicyTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly Type PolicyType = typeof(FBXVmdPipeline).Assembly
            .GetType("Fbx2Vmd.FBXImporter.HumanoidFootAnchorPolicy", true);
        private static readonly Type ResolverType = typeof(FBXVmdPipeline).Assembly
            .GetType("Fbx2Vmd.FBXImporter.HumanoidFootAnchorPolicyResolver", true);

        [Test]
        public void Given_ConfidentPlantIntent_When_Rasterizing_Then_MarksPinnedSpan()
        {
            Array policies = Rasterize(new[] { CreateIntent(10, 20, 0, 0) }, 30);
            for (int index = 0; index < policies.Length; index++)
            {
                Assert.That(policies.GetValue(index).ToString(),
                    Is.EqualTo(index >= 10 && index < 20 ? "Pinned" : "Free"),
                    $"frame {index}");
            }
        }

        [Test]
        public void Given_ConfidentSlideIntent_When_Rasterizing_Then_MarksSlidingSpan()
        {
            Array policies = Rasterize(new[] { CreateIntent(5, 8, 1, 0) }, 10);
            Assert.That(policies.GetValue(6).ToString(), Is.EqualTo("Sliding"));
            Assert.That(policies.GetValue(4).ToString(), Is.EqualTo("Free"));
        }

        [Test]
        public void Given_UncertainOrClippedIntent_When_Rasterizing_Then_StaysFree()
        {
            // 불확실 구간은 앵커 방침을 바꾸지 않고, 범위를 벗어난 구간은 잘림.
            Array policies = Rasterize(new[]
            {
                CreateIntent(2, 6, 0, 1),
                CreateIntent(-5, 4, 0, 0),
                CreateIntent(8, 99, 0, 0)
            }, 10);
            Assert.That(policies.GetValue(2).ToString(), Is.EqualTo("Pinned"));
            Assert.That(policies.GetValue(5).ToString(), Is.EqualTo("Free"));
            Assert.That(policies.GetValue(9).ToString(), Is.EqualTo("Pinned"));
        }

        [Test]
        public void Given_NullIntents_When_Rasterizing_Then_AllFree()
        {
            Array policies = Rasterize(null, 4);
            for (int index = 0; index < policies.Length; index++)
                Assert.That(policies.GetValue(index).ToString(), Is.EqualTo("Free"));
        }

        [Test]
        public void Given_PartialWeightInsideIntent_When_TouchdownLock_Then_RaisesWithinLockFrames()
        {
            // 진입은 TouchdownLockFrames 안에 완전 잠금됨. 감지되지 않은 채널은 만들지 않음.
            var weights = new[]
            {
                new Vector2(0.2f, 0f), new Vector2(0.2f, 0f), new Vector2(0.2f, 0f),
                new Vector2(0f, 0f), new Vector2(1f, 0f)
            };
            ApplyTouchdownLock(weights, new[] { CreateIntent(0, 3, 0, 0) });
            Assert.That(weights[0].x, Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(weights[1].x, Is.EqualTo(1f));
            Assert.That(weights[2].x, Is.EqualTo(1f));
            Assert.That(weights[0].y, Is.EqualTo(0f));
            Assert.That(weights[3].x, Is.EqualTo(0f));
            Assert.That(weights[4].x, Is.EqualTo(1f));
        }

        [Test]
        public void Given_UncertainIntent_When_TouchdownLock_Then_LeavesWeights()
        {
            var weights = new[] { new Vector2(0.2f, 0f), new Vector2(0.2f, 0f) };
            ApplyTouchdownLock(weights, new[] { CreateIntent(0, 1, 0, 1) });
            Assert.That(weights[0].x, Is.EqualTo(0.2f));
            Assert.That(weights[1].x, Is.EqualTo(0.2f));
        }

        private static Array Rasterize(object[] intents, int frameCount)
        {
            Type intentType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntent", true);
            Array array = null;
            if (intents != null)
            {
                array = Array.CreateInstance(intentType, intents.Length);
                for (int index = 0; index < intents.Length; index++)
                    array.SetValue(intents[index], index);
            }
            return (Array)ResolverType.GetMethod("Rasterize", Flags)
                .Invoke(null, new object[] { array, frameCount });
        }

        private static void ApplyTouchdownLock(Vector2[] weights, object[] intents)
        {
            Type intentType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntent", true);
            Array array = Array.CreateInstance(intentType, intents.Length);
            for (int index = 0; index < intents.Length; index++)
                array.SetValue(intents[index], index);
            ResolverType.GetMethod("ApplyTouchdownLock", Flags)
                .Invoke(null, new object[] { weights, array });
        }

        private static object CreateIntent(int startFrame, int endFrameExclusive,
            int mode, int certainty)
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type intentType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntent", true);
            Type modeType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentMode", true);
            Type certaintyType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentCertainty", true);
            return Activator.CreateInstance(intentType, Flags, null,
                new object[] { true, startFrame, endFrameExclusive, Vector3.zero,
                    Enum.ToObject(modeType, mode), Enum.ToObject(certaintyType, certainty),
                    false },
                null);
        }
    }
}
