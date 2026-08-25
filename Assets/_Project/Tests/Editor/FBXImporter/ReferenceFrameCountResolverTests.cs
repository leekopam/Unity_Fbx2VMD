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
            Type resolverType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.ReferenceFrameCountResolver",
                throwOnError: false);
            Assert.That(resolverType, Is.Not.Null, "모델 중립 참조 프레임 수 결정기가 필요합니다.");

            MethodInfo resolveMethod = resolverType.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null);

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
    }
}
