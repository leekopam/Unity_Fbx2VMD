using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootContactPlannerTests
    {
        private const float FrameRate = 60f;
        private const float HumanScale = 1f;

        [Test]
        public void Given_SourceContact_When_TargetSlides_Then_PreservesSourceRelativeMotion()
        {
            Vector3[] source = CreatePoints(8, index =>
                new Vector3(index * 0.001f, 0f, 0f));
            Vector3[] target = CreatePoints(8, index =>
                new Vector3(index * 0.01f, 0f, 0f));

            object plan = BuildPlan(source, target, Quaternion.identity);

            Assert.That(TryEvaluate(plan, 5f / FrameRate, out Vector3 correction), Is.True);
            Assert.That(correction.x, Is.EqualTo(-0.045f).Within(0.000001f));
            Assert.That(correction.y, Is.EqualTo(0f).Within(0.000001f));
            Assert.That(correction.z, Is.EqualTo(0f).Within(0.000001f));
            Assert.That(GetIntProperty(plan, "LeftContactRunCount"), Is.EqualTo(1));
        }

        [Test]
        public void Given_ContactShorterThanMinimum_When_Building_Then_DoesNotCorrect()
        {
            Vector3[] source = CreatePoints(10, index =>
                new Vector3(0f, index < 2 ? 0f : 1f, 0f));
            Vector3[] target = CreatePoints(10, index =>
                new Vector3(index * 0.01f, 0f, 0f));

            object plan = BuildPlan(source, target, Quaternion.identity);

            Assert.That(TryEvaluate(plan, 1f / FrameRate, out Vector3 correction), Is.True);
            Assert.That(correction, Is.EqualTo(Vector3.zero));
            Assert.That(GetIntProperty(plan, "LeftContactRunCount"), Is.Zero);
        }

        [Test]
        public void Given_LowFootMovingVertically_When_Building_Then_DoesNotTreatAsContact()
        {
            Vector3[] source = CreatePoints(8, index =>
                new Vector3(0f, index * 0.003f, 0f));
            Vector3[] target = CreatePoints(8, index =>
                new Vector3(index * 0.01f, 0f, 0f));

            object plan = BuildPlan(source, target, Quaternion.identity);

            Assert.That(TryEvaluate(plan, 5f / FrameRate, out Vector3 correction), Is.True);
            Assert.That(correction.x, Is.EqualTo(0f).Within(0.000001f));
            Assert.That(correction.z, Is.EqualTo(0f).Within(0.000001f));
            Assert.That(GetIntProperty(plan, "LeftContactRunCount"), Is.Zero);
        }

        [Test]
        public void Given_StableContactWithVerticalRoll_When_Building_Then_KeepsContactRun()
        {
            Vector3[] source = CreatePoints(8, index =>
                new Vector3(0f, index >= 3 ? 0.004f : 0f, 0f));
            Vector3[] target = CreatePoints(8, index =>
                new Vector3(index * 0.01f, 0f, 0f));

            object plan = BuildPlan(source, target, Quaternion.identity);

            Assert.That(TryEvaluate(plan, 7f / FrameRate, out Vector3 correction), Is.True);
            Assert.That(correction.x, Is.EqualTo(-0.07f).Within(0.0001f));
            Assert.That(GetIntProperty(plan, "LeftContactRunCount"), Is.EqualTo(1));
        }

        [Test]
        public void Given_ContactEnds_When_EvaluatingRelease_Then_FadesWithoutSnap()
        {
            Vector3[] source = CreatePoints(14, index =>
                new Vector3(0f, index < 6 ? 0f : 1f, 0f));
            Vector3[] target = CreatePoints(14, index =>
                new Vector3(index * 0.01f, 0f, 0f));

            object plan = BuildPlan(source, target, Quaternion.identity);

            TryEvaluate(plan, 5f / FrameRate, out Vector3 contactCorrection);
            TryEvaluate(plan, 6f / FrameRate, out Vector3 firstReleaseCorrection);
            TryEvaluate(plan, 12f / FrameRate, out Vector3 releasedCorrection);
            Assert.That(Mathf.Abs(firstReleaseCorrection.x),
                Is.LessThan(Mathf.Abs(contactCorrection.x)));
            Assert.That(Mathf.Abs(firstReleaseCorrection.x), Is.GreaterThan(0f));
            Assert.That(releasedCorrection.x, Is.EqualTo(0f).Within(0.000001f));
            Assert.That(releasedCorrection.z, Is.EqualTo(0f).Within(0.000001f));
        }

        [Test]
        public void Given_RotatedTarget_When_Building_Then_StoresCorrectionInTargetRootSpace()
        {
            Vector3[] source = CreatePoints(8, index =>
                new Vector3(index * 0.001f, 0f, 0f));
            Vector3[] target = CreatePoints(8, _ => Vector3.zero);
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

            object plan = BuildPlan(source, target, rotation);

            TryEvaluate(plan, 5f / FrameRate, out Vector3 rootSpaceCorrection);
            Vector3 worldCorrection = rotation * rootSpaceCorrection;
            Assert.That(rootSpaceCorrection.x, Is.EqualTo(0.005f).Within(0.000001f));
            Assert.That(worldCorrection.z, Is.EqualTo(-0.005f).Within(0.000001f));
        }

        [Test]
        public void Given_SubFrameTime_When_Evaluating_Then_InterpolatesCorrections()
        {
            Vector3[] source = CreatePoints(8, _ => Vector3.zero);
            Vector3[] target = CreatePoints(8, index =>
                new Vector3(index * 0.01f, 0f, 0f));
            object plan = BuildPlan(source, target, Quaternion.identity);

            TryEvaluate(plan, 2.5f / FrameRate, out Vector3 correction);

            Assert.That(correction.x, Is.EqualTo(-0.025f).Within(0.000001f));
        }

        [Test]
        public void Given_SourceFootHeightChange_When_Building_Then_TransfersVerticalMotion()
        {
            Vector3[] source = CreatePoints(8, index =>
                new Vector3(0f, index < 4 ? 0f : 0.02f, 0f));
            Vector3[] target = CreatePoints(8, _ => Vector3.zero);

            object plan = BuildPlan(source, target, Quaternion.identity);

            Assert.That(TryEvaluate(plan, 7f / FrameRate, out Vector3 correction), Is.True);
            Assert.That(correction.y, Is.EqualTo(0.02f).Within(0.000001f));
        }

        private static object BuildPlan(
            Vector3[] source,
            Vector3[] target,
            Quaternion rotation)
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type sampleType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactSample",
                throwOnError: true);
            Type plannerType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactPlanner",
                throwOnError: true);
            Array sourceSamples = CreateSamples(sampleType, source);
            Array targetSamples = CreateSamples(sampleType, target);
            MethodInfo build = plannerType.GetMethod(
                "Build",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    sourceSamples.GetType(),
                    targetSamples.GetType(),
                    typeof(float),
                    typeof(float),
                    typeof(Quaternion),
                    typeof(float)
                },
                modifiers: null);
            Assert.That(build, Is.Not.Null);
            return build.Invoke(
                null,
                new object[]
                {
                    sourceSamples,
                    targetSamples,
                    FrameRate,
                    HumanScale,
                    rotation,
                    HumanScale
                });
        }

        private static Array CreateSamples(Type sampleType, Vector3[] points)
        {
            ConstructorInfo constructor = sampleType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Vector3), typeof(Vector3) },
                modifiers: null);
            Assert.That(constructor, Is.Not.Null);
            Array samples = Array.CreateInstance(sampleType, points.Length);
            for (int index = 0; index < points.Length; index++)
            {
                samples.SetValue(
                    constructor.Invoke(new object[] { points[index], points[index] }),
                    index);
            }

            return samples;
        }

        private static bool TryEvaluate(
            object plan,
            float timeSeconds,
            out Vector3 leftCorrection)
        {
            MethodInfo evaluate = plan.GetType().GetMethod(
                "TryEvaluate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(evaluate, Is.Not.Null);
            object[] arguments = { timeSeconds, Vector3.zero, Vector3.zero };
            bool result = (bool)evaluate.Invoke(plan, arguments);
            leftCorrection = (Vector3)arguments[1];
            return result;
        }

        private static int GetIntProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return (int)property.GetValue(target);
        }

        private static Vector3[] CreatePoints(int count, Func<int, Vector3> create)
        {
            var result = new Vector3[count];
            for (int index = 0; index < count; index++)
            {
                result[index] = create(index);
            }

            return result;
        }
    }
}
