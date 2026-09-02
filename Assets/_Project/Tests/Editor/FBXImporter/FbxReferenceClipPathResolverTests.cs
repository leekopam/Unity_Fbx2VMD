using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class FbxReferenceClipPathResolverTests
    {
        private static readonly Type[] YybReferenceClipResolverParameterTypes =
        {
            typeof(string),
            typeof(Func<string, bool>)
        };

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

        [Test]
        public void Given_ProjectFbxExists_When_ResolvingYybReferenceClipPath_Then_UsesProjectReferenceBeforeControlledImport()
        {
            string controlledPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";
            string projectPath = "Assets/_Project/FBX/satisfaction_2.fbx";

            string resolved = ResolveYybReferenceClipAssetPath(
                "satisfaction_2",
                controlledPath,
                projectPath);

            Assert.That(resolved, Is.EqualTo(projectPath));
        }

        private static string ResolveYybReferenceClipAssetPath(
            string fbxFileName,
            params string[] existingAssetPaths)
        {
            Type runnerType = Type.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonBatchRunner, Assembly-CSharp");
            Assert.That(runnerType, Is.Not.Null, "YYB visual comparison runner type must be available in editor tests.");

            MethodInfo method = runnerType.GetMethod(
                "ResolveReferenceClipAssetPath",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: YybReferenceClipResolverParameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, "YYB runner must expose a fakeable resolver so manual reference and Main_Auto smoke use the same FBX source priority.");

            var existing = new HashSet<string>(existingAssetPaths, StringComparer.OrdinalIgnoreCase);
            Func<string, bool> assetExists = existing.Contains;
            return (string)method.Invoke(null, new object[] { fbxFileName, assetExists });
        }
    }
}
