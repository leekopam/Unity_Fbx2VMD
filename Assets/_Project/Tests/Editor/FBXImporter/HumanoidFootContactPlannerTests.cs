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

        [Test]
        public void Given_ConfidentPlantIntent_When_SourceDriftsDuringSupport_Then_AnchorStaysPinned()
        {
            // 지지 구간 안에서 원본 발이 미세하게 이동해도 확정된 Plant 의도는 앵커를 고정함.
            Vector3[] source = CreatePoints(40, index => new Vector3(
                index >= 10 && index < 20 ? (index - 9) * 0.0005f :
                index >= 20 ? 0.0055f : 0f, 0f, 0f));
            Vector3[] target = CreatePoints(40, _ => Vector3.zero);

            object plan = BuildPlan(source, target, Quaternion.identity,
                CreateEstimate(10, 20));

            TryEvaluate(plan, 11f / FrameRate, out Vector3 pinnedFirst);
            TryEvaluate(plan, 18f / FrameRate, out Vector3 pinnedLast);
            Assert.That(Vector3.Distance(pinnedFirst, pinnedLast),
                Is.LessThan(0.000001f));

            // 의도가 없으면 같은 구간의 보정이 원본 이동을 그대로 따라감.
            object freePlan = BuildPlan(source, target, Quaternion.identity);
            TryEvaluate(freePlan, 11f / FrameRate, out Vector3 freeFirst);
            TryEvaluate(freePlan, 18f / FrameRate, out Vector3 freeLast);
            Assert.That(Vector3.Distance(freeFirst, freeLast),
                Is.GreaterThan(0.0005f));

            // 핀 구간을 빠져나가면 접힌 앵커에서 원본 추종을 재개해 위치가 연속됨.
            TryEvaluate(plan, 19f / FrameRate, out Vector3 pinnedEdge);
            TryEvaluate(plan, 21f / FrameRate, out Vector3 resumed);
            Assert.That(Vector3.Distance(pinnedEdge, resumed),
                Is.LessThan(0.002f));
        }

        [Test]
        public void Given_ToePivotRoll_When_Pinned_Then_ToeStaysAnchored()
        {
            // 롤 구간: 발끝은 바닥에 고정된 채 발목만 들림.
            // 핀이 발끝을 기준으로 체결되면 보정 후 발끝(발목+보정+발끝 방향)이 고정됨.
            Vector3[] sourceFeet = CreatePoints(70, index =>
                new Vector3(0f, index >= 25 && index < 50 ? 0.09f : 0.04f, 0f));
            Vector3[] sourceToes = CreatePoints(70, _ =>
                new Vector3(0f, 0f, 0.12f));
            Vector3[] targetFeet = CreatePoints(70, index =>
                new Vector3(index * 0.002f, 0.04f, 0f));
            Vector3[] targetToes = CreatePoints(70, index =>
                new Vector3(index * 0.002f, 0f, 0.12f));

            object plan = BuildPlanParts(sourceFeet, sourceToes,
                targetFeet, targetToes, CreateEstimate(5, 60));

            TryEvaluatePose(plan, 35f / FrameRate,
                out Vector3 correctionA, out Vector3 directionA);
            TryEvaluatePose(plan, 45f / FrameRate,
                out Vector3 correctionB, out Vector3 directionB);
            Vector3 toeA = targetFeet[35] + correctionA + directionA;
            Vector3 toeB = targetFeet[45] + correctionB + directionB;
            Assert.That(Vector3.Distance(toeA, toeB), Is.LessThan(0.001f));
        }

        private static object BuildPlan(
            Vector3[] source,
            Vector3[] target,
            Quaternion rotation,
            object intents = null)
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type sampleType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactSample",
                throwOnError: true);
            Array sourceSamples = CreateSamples(sampleType, source);
            Array targetSamples = CreateSamples(sampleType, target);
            return InvokeBuild(sourceSamples, targetSamples, intents, rotation);
        }

        private static object BuildPlanParts(
            Vector3[] sourceFeet,
            Vector3[] sourceToes,
            Vector3[] targetFeet,
            Vector3[] targetToes,
            object intents)
        {
            Type sampleType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactSample",
                throwOnError: true);
            Array sourceSamples = CreateSamplesParts(
                sampleType, sourceFeet, sourceToes);
            Array targetSamples = CreateSamplesParts(
                sampleType, targetFeet, targetToes);
            return InvokeBuild(
                sourceSamples, targetSamples, intents, Quaternion.identity);
        }

        private static Array CreateSamplesParts(
            Type sampleType, Vector3[] feet, Vector3[] toes)
        {
            ConstructorInfo constructor = sampleType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(Vector3), typeof(Vector3),
                    typeof(Vector3), typeof(Vector3)
                },
                modifiers: null);
            Assert.That(constructor, Is.Not.Null);
            Array samples = Array.CreateInstance(sampleType, feet.Length);
            for (int index = 0; index < feet.Length; index++)
            {
                samples.SetValue(
                    constructor.Invoke(new object[]
                    {
                        feet[index], toes[index], feet[index], toes[index]
                    }),
                    index);
            }

            return samples;
        }

        private static object InvokeBuild(
            Array sourceSamples, Array targetSamples, object intents,
            Quaternion rotation)
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type plannerType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactPlanner",
                throwOnError: true);
            Type estimateType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentEstimate",
                throwOnError: true);
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
                    typeof(float),
                    estimateType
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
                    HumanScale,
                    intents
                });
        }

        private static object CreateEstimate(int startFrame, int endFrameExclusive)
        {
            Assembly assembly = typeof(FBXVmdPipeline).Assembly;
            Type estimateType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentEstimate",
                throwOnError: true);
            Type intentType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntent",
                throwOnError: true);
            Type modeType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentMode",
                throwOnError: true);
            Type certaintyType = assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidFootContactIntentCertainty",
                throwOnError: true);
            Array left = Array.CreateInstance(intentType, 1);
            left.SetValue(
                Activator.CreateInstance(
                    intentType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[]
                    {
                        true, startFrame, endFrameExclusive, Vector3.zero,
                        Enum.ToObject(modeType, 0), Enum.ToObject(certaintyType, 0), false
                    },
                    culture: null),
                0);
            return Activator.CreateInstance(
                estimateType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[]
                {
                    left, Array.CreateInstance(intentType, 0),
                    new Vector2Int[0], new Vector2Int[0]
                },
                culture: null);
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

        private static void TryEvaluatePose(
            object plan,
            float timeSeconds,
            out Vector3 leftCorrection,
            out Vector3 leftToeDirection)
        {
            MethodInfo evaluate = plan.GetType().GetMethod(
                "TryEvaluateSupportPose",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(evaluate, Is.Not.Null);
            object[] arguments =
            {
                timeSeconds, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero
            };
            Assert.That((bool)evaluate.Invoke(plan, arguments), Is.True);
            leftCorrection = (Vector3)arguments[1];
            leftToeDirection = (Vector3)arguments[3];
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
