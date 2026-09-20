using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorHumanoidClipImportConfiguratorTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Given_CompatibleMapping_When_ResolvingNames_Then_PreservesLimitsAndExactEntries(bool useAlias)
        {
            var root = new GameObject("매핑 검증");
            try
            {
                AddBone(root, "mixamorig:Hips");
                AddBone(root, "수동 척추");
                var limit = new HumanLimit { useDefaultValues = false, min = new Vector3(-10f, -20f, -30f),
                    max = new Vector3(10f, 20f, 30f), center = Vector3.one, axisLength = 0.25f };
                var source = new[] {
                    new HumanBone { humanName = "Hips", boneName = useAlias ? "Skeleton_Hips" : "mixamorig:Hips", limit = limit },
                    new HumanBone { humanName = "Spine", boneName = "수동 척추", limit = limit }
                };
                HumanBone[] original = (HumanBone[])source.Clone();
                Assert.That(Resolve(root, source, out HumanBone[] result), Is.True);
                Assert.That(result[0].boneName, Is.EqualTo("mixamorig:Hips"));
                Assert.That(result[1], Is.EqualTo(original[1]));
                Assert.That(result[0].limit, Is.EqualTo(limit));
                Assert.That(source, Is.EqualTo(original));
                Assert.That(ReferenceEquals(source, result), Is.EqualTo(!useAlias));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase("missing")]
        [TestCase("duplicateHuman")]
        [TestCase("duplicateTransform")]
        [TestCase("reusedTransform")]
        [TestCase("ambiguousAlias")]
        [TestCase("ambiguousNormalized")]
        public void Given_UnresolvableMapping_When_ResolvingNames_Then_RejectsWithoutPartialChanges(string failure)
        {
            var root = new GameObject("매핑 실패 검증");
            try
            {
                AddBone(root, "mixamorig:Hips");
                AddBone(root, "수동 척추");
                var source = new[] {
                    new HumanBone { humanName = "Hips", boneName = "Skeleton_Hips" },
                    new HumanBone { humanName = "Spine", boneName = "수동 척추" }
                };
                if (failure == "missing") source[1].boneName = "없는 척추";
                if (failure == "duplicateHuman") source[1].humanName = "Hips";
                if (failure == "duplicateTransform") AddBone(root, "mixamorig:Hips");
                if (failure == "reusedTransform") source[1].boneName = "mixamorig:Hips";
                if (failure == "ambiguousAlias") AddBone(root, "otherRig:Hips");
                if (failure == "ambiguousNormalized")
                {
                    AddBone(root, "mixamorig_Hips");
                    source[0].boneName = "MIXAMORIG HIPS";
                }
                HumanBone[] original = (HumanBone[])source.Clone();
                Assert.That(Resolve(root, source, out HumanBone[] result), Is.False);
                Assert.That(result, Is.SameAs(source));
                Assert.That(source, Is.EqualTo(original));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Given_ExactNameWithSharedAlias_When_ResolvingNames_Then_PreservesExactMapping()
        {
            var root = new GameObject("정확 매핑 우선 검증");
            try
            {
                AddBone(root, "rigA:Hips");
                AddBone(root, "rigB:Hips");
                var source = new[] { new HumanBone { humanName = "Hips", boneName = "rigB:Hips" } };
                Assert.That(Resolve(root, source, out HumanBone[] result), Is.True);
                Assert.That(result, Is.SameAs(source));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Given_LocalSnakeMapping_When_ResolvingNames_Then_MatchesActualBonesWithoutChangingImporter()
        {
            const string path = "Assets/Resources/Import_FBX/Snake Hip Hop Dance.fbx";
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) Assert.Ignore("로컬 입력 FBX가 없는 환경에서는 실제 매핑 검증을 생략함");
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            HumanDescription original = importer.humanDescription;
            HumanBone[] source = original.human;
            var dependencyHash = AssetDatabase.GetAssetDependencyHash(path);
            Assert.That(Resolve(root, source, out HumanBone[] result), Is.True);
            Assert.That(result.Length, Is.EqualTo(source.Length));
            var names = root.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            for (int i = 0; i < source.Length; i++)
            {
                Assert.That(names, Does.Contain(result[i].boneName));
                Assert.That(result[i].humanName, Is.EqualTo(source[i].humanName));
                Assert.That(result[i].limit, Is.EqualTo(source[i].limit));
            }
            Assert.That(importer.humanDescription.human, Is.EqualTo(source));
            Assert.That(importer.humanDescription.skeleton, Is.EqualTo(original.skeleton));
            Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.EqualTo(dependencyHash));
        }

        private static bool Resolve(GameObject root, HumanBone[] source, out HumanBone[] result)
        {
            Type type = typeof(FBXImportController).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidClipImportConfigurator");
            MethodInfo method = type.GetMethod("TryResolveHumanBoneNames", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            object[] args = { root, source, null };
            bool succeeded = (bool)method.Invoke(null, args);
            result = (HumanBone[])args[2];
            return succeeded;
        }

        private static void AddBone(GameObject root, string name)
        {
            new GameObject(name).transform.SetParent(root.transform, false);
        }
    }
}
