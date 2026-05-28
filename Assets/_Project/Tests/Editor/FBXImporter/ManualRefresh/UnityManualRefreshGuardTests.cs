using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Tests.Editor.FBXImporter.ManualRefresh
{
    public class UnityManualRefreshGuardTests
    {
        [Test]
        public void Given_MixedPaths_When_GetExistingAssetPaths_Then_NormalizesUnityAssetPaths()
        {
            var paths = new[]
            {
                "Assets\\Plugins\\VMDRecorderSample\\SampleScript\\MotionComparisonProbe.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/FileManager.cs",
                string.Empty,
                "Packages/com.example/package.json",
                "C:/outside/file.cs"
            };

            Type guardType = Type.GetType(
                "Member_Han.Modules.FBXImporter.EditorTools.UnityManualRefreshGuard, Assembly-CSharp-Editor",
                throwOnError: true);
            MethodInfo method = guardType.GetMethod(
                "GetRefreshableAssetPaths",
                BindingFlags.Public | BindingFlags.Static);

            var assetPaths = new List<string>();
            foreach (object value in (IEnumerable)method.Invoke(null, new object[] { paths }))
            {
                assetPaths.Add((string)value);
            }

            Assert.That(assetPaths, Is.EqualTo(new[]
            {
                "Assets/Plugins/VMDRecorderSample/SampleScript/MotionComparisonProbe.cs",
                "Assets/_Project/Scripts/Member_Han/Modules/FBXImporter/FileManager.cs"
            }));
        }
    }
}
