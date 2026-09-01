using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class ReferenceFrameCountResolverTests
    {
        [Test]
        public void Given_KnownReferenceProfile_When_ClipAndRequestCoverDuration_Then_UsesKnownFrameCount()
        {
            MethodInfo resolveMethod = FindResolverMethod(
                "Resolve",
                typeof(string),
                typeof(float),
                typeof(int),
                typeof(float),
                typeof(float),
                typeof(string),
                typeof(int));

            int resolved = (int)resolveMethod.Invoke(
                null,
                new object[]
                {
                    "motion.fbx",
                    10.1f,
                    400,
                    10.1f,
                    30f,
                    "motion",
                    300
                });

            Assert.That(resolved, Is.EqualTo(301));
        }

        [Test]
        public void Given_FullSatisfactionReferenceTiming_When_ResolvingReferenceMmdTargetFrameCount_Then_Uses6001FrameReference()
        {
            int resolved = ResolveReferenceTarget(
                "satisfaction_2.fbx",
                requestedDurationSeconds: 207.7833f,
                configuredTargetFrameCount: 6234,
                referenceClipLengthSeconds: 207.7833f,
                recordingFrameRate: 30f,
                knownReferenceBaseName: "satisfaction_2",
                knownReferenceMaxFrameIndex: 6000);

            Assert.That(resolved, Is.EqualTo(6001));
        }

        [Test]
        public void Given_ShortSatisfactionSmoke_When_ResolvingReferenceMmdTargetFrameCount_Then_KeepsConfiguredSmokeTarget()
        {
            int resolved = ResolveReferenceTarget(
                "satisfaction_2.fbx",
                requestedDurationSeconds: 31f,
                configuredTargetFrameCount: 930,
                referenceClipLengthSeconds: 207.7833f,
                recordingFrameRate: 30f,
                knownReferenceBaseName: "satisfaction_2",
                knownReferenceMaxFrameIndex: 6000);

            Assert.That(resolved, Is.EqualTo(930));
        }

        [Test]
        public void Given_CandidateFrameCountDiffersFromReference_When_ResolvingSummaryTarget_Then_KeepsReferenceTarget()
        {
            int resolved = ResolveSummaryTarget(
                referenceTargetFrameCount: 6001,
                candidateFrameCount: 5900);

            Assert.That(resolved, Is.EqualTo(6001));
        }

        [Test]
        public void Given_CandidateFrameCountIsUnavailable_When_ResolvingSummaryTarget_Then_KeepsReferenceTarget()
        {
            int resolved = ResolveSummaryTarget(
                referenceTargetFrameCount: 6234,
                candidateFrameCount: 0);

            Assert.That(resolved, Is.EqualTo(6234));
        }

        [Test]
        public void Given_SummaryTargetPolicy_When_InspectingRunner_Then_PureCalculationOverloadIsAbsent()
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null, "시각 비교 배치 실행기 타입이 필요합니다.");

            MethodInfo staleOverload = runnerType.GetMethod(
                "ResolveSummaryTargetFrameCount",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int), typeof(int) },
                modifiers: null);

            Assert.That(
                staleOverload,
                Is.Null,
                "순수 요약 프레임 수 판정은 ReferenceFrameCountResolver만 소유해야 합니다.");
        }

        [Test]
        public void Given_ReferenceFrameCountResolver_When_InspectingRunner_Then_ReferenceCalculationWrapperIsAbsent()
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null, "시각 비교 배치 실행기 타입이 필요합니다.");

            MethodInfo staleWrapper = runnerType.GetMethod(
                "ResolveReferenceMmdTargetFrameCount",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(
                staleWrapper,
                Is.Null,
                "참조 프레임 수 계산은 ReferenceFrameCountResolver만 소유해야 합니다.");
        }

        private static int ResolveSummaryTarget(int referenceTargetFrameCount, int candidateFrameCount)
        {
            MethodInfo method = FindResolverMethod(
                "ResolveSummaryTarget",
                typeof(int),
                typeof(int));

            return (int)method.Invoke(
                null,
                new object[] { referenceTargetFrameCount, candidateFrameCount });
        }

        private static int ResolveReferenceTarget(
            string sourceFileName,
            float requestedDurationSeconds,
            int configuredTargetFrameCount,
            float referenceClipLengthSeconds,
            float recordingFrameRate,
            string knownReferenceBaseName,
            int knownReferenceMaxFrameIndex)
        {
            MethodInfo method = FindResolverMethod(
                "Resolve",
                typeof(string),
                typeof(float),
                typeof(int),
                typeof(float),
                typeof(float),
                typeof(string),
                typeof(int));

            return (int)method.Invoke(
                null,
                new object[]
                {
                    sourceFileName,
                    requestedDurationSeconds,
                    configuredTargetFrameCount,
                    referenceClipLengthSeconds,
                    recordingFrameRate,
                    knownReferenceBaseName,
                    knownReferenceMaxFrameIndex
                });
        }

        private static MethodInfo FindResolverMethod(string methodName, params Type[] parameterTypes)
        {
            Type resolverType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceFrameCountResolver",
                throwOnError: false);
            Assert.That(resolverType, Is.Not.Null, "모델 중립 참조 프레임 수 결정기가 필요합니다.");

            MethodInfo method = resolverType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method;
        }
    }
}
