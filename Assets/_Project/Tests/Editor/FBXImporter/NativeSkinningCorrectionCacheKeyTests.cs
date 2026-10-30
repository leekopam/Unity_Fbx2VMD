using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningCorrectionCacheKeyTests
    {
        [Test]
        public void Given_MetaOnlyImporterChange_When_ComputingAssetIdentity_Then_InvalidatesPreviousIdentity()
        {
            string path = "Assets/NativeSkinningCacheIdentity_" + Guid.NewGuid().ToString("N") + ".anim";
            var clip = new AnimationClip();
            try
            {
                AssetDatabase.CreateAsset(clip, path);
                byte[] source = File.ReadAllBytes(path);
                string guid = AssetDatabase.AssetPathToGUID(path);
                Hash128 dependencyHash = AssetDatabase.GetAssetDependencyHash(path);
                string before = GetIdentity(clip);
                Assert.That(GetIdentity(clip), Is.EqualTo(before));

                AssetImporter importer = AssetImporter.GetAtPath(path);
                importer.userData = "임포트 설정 변경 검증";
                importer.SaveAndReimport();
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

                Assert.That(File.ReadAllBytes(path), Is.EqualTo(source), "원본 내용 변경과 메타 변경을 구분해야 함");
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.Not.EqualTo(dependencyHash));
                string after = GetIdentity(clip);
                Assert.That(after, Is.Not.EqualTo(before), "같은 원본이라도 임포트 설정 변경 시 캐시를 분리해야 함");
                Assert.That(GetIdentity(clip), Is.EqualTo(after));
            }
            finally
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                    Assert.That(AssetDatabase.DeleteAsset(path), Is.True, "검증용 에셋 정리 실패");
                else if (clip != null)
                    UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        private static string GetIdentity(UnityEngine.Object asset)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Fbx2Vmd.FBXImporter.NativeSkinningCorrectionCacheKey"))
                .First(candidate => candidate != null);
            MethodInfo method = type.GetMethod("GetAssetIdentity", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { asset });
        }
    }
}
