using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class FbxReferenceClipPathResolverTests
    {
        [Test]
        public void Given_ProjectCandidateExists_When_Resolving_Then_PrefersProjectPath()
        {
            Type resolverType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FbxReferenceClipPathResolver",
                throwOnError: false);
            Assert.That(resolverType, Is.Not.Null, "모델 중립 FBX 참조 클립 경로 결정기가 필요합니다.");

            MethodInfo resolveMethod = resolverType.GetMethod(
                "Resolve",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(resolveMethod, Is.Not.Null);

            string resolved = (string)resolveMethod.Invoke(
                null,
                new object[]
                {
                    "walk",
                    "default.fbx",
                    "Assets/ProjectFbx",
                    "Assets/ImportFbx",
                    (Func<string, bool>)(path => path == "Assets/ProjectFbx/walk.fbx")
                });

            Assert.That(resolved, Is.EqualTo("Assets/ProjectFbx/walk.fbx"));
        }
    }
}
