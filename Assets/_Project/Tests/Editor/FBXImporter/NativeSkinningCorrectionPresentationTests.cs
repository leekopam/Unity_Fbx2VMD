using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningCorrectionPresentationTests
    {
        private const string NamespacePrefix = "Fbx2Vmd.FBXImporter.";

        [Test]
        public void Given_SparseFrame_When_ApplyingCache_Then_OnlySelectedVerticesMove()
        {
            object cache = CreateCache(
                3,
                CreateFrame(
                    7,
                    CreateCorrection(0, new Vector3(0.1f, 0f, 0f)),
                    CreateCorrection(2, new Vector3(0f, -0.2f, 0f))));
            var vertices = new List<Vector3>
            {
                Vector3.zero,
                Vector3.one,
                new Vector3(2f, 2f, 2f)
            };

            object[] arguments = { 7, vertices, 0 };
            Assert.That((bool)Invoke(cache, "TryApply", arguments), Is.True);
            Assert.That(arguments[2], Is.EqualTo(2));
            Assert.That(vertices[0], Is.EqualTo(new Vector3(0.1f, 0f, 0f)));
            Assert.That(vertices[1], Is.EqualTo(Vector3.one));
            Assert.That(vertices[2], Is.EqualTo(new Vector3(2f, 1.8f, 2f)));
        }

        [Test]
        public void Given_UncachedFrame_When_ApplyingCache_Then_SucceedsWithoutChanges()
        {
            object cache = CreateCache(
                2,
                CreateFrame(1, CreateCorrection(0, Vector3.right)));
            var vertices = new List<Vector3> { Vector3.zero, Vector3.one };

            object[] arguments = { 2, vertices, -1 };
            Assert.That((bool)Invoke(cache, "TryApply", arguments), Is.True);
            Assert.That(arguments[2], Is.Zero);
            Assert.That(vertices, Is.EqualTo(new[] { Vector3.zero, Vector3.one }));
        }

        [Test]
        public void Given_WrongVertexCount_When_ApplyingCache_Then_FailsWithoutPartialMutation()
        {
            object cache = CreateCache(
                3,
                CreateFrame(1, CreateCorrection(0, Vector3.right)));
            var vertices = new List<Vector3> { Vector3.zero, Vector3.one };
            Vector3[] baseline = vertices.ToArray();

            object[] arguments = { 1, vertices, -1 };
            Assert.That((bool)Invoke(cache, "TryApply", arguments), Is.False);
            Assert.That(arguments[2], Is.Zero);
            Assert.That(vertices, Is.EqualTo(baseline));
        }

        [Test]
        public void Given_BakedRenderer_When_PresentingCorrection_Then_RestoresSourceOnDispose()
        {
            GameObject root = null;
            Mesh sourceMesh = null;
            Mesh baselineMesh = null;
            object presenter = null;

            try
            {
                CreateSkinnedTriangle(out root, out SkinnedMeshRenderer renderer, out sourceMesh);
                baselineMesh = new Mesh();
                renderer.BakeMesh(baselineMesh, false);
                Vector3 baselineVertex = baselineMesh.vertices[0];
                Vector3 correction = new Vector3(0.05f, 0f, 0f);
                object cache = CreateCache(
                    sourceMesh.vertexCount,
                    CreateFrame(4, CreateCorrection(0, correction)));
                presenter = CreatePresenter(renderer, cache);

                object[] arguments = { 4, 0 };
                Assert.That((bool)Invoke(presenter, "TryPresentFrame", arguments), Is.True);
                Assert.That(arguments[1], Is.EqualTo(1));
                Assert.That(renderer.enabled, Is.False,
                    "보정 메시와 원본 메시가 동시에 표시되면 안 됩니다.");

                Mesh previewMesh = ReadProperty<Mesh>(presenter, "PreviewMesh");
                Assert.That(previewMesh, Is.Not.Null);
                Assert.That(
                    Vector3.Distance(previewMesh.vertices[0], baselineVertex + correction),
                    Is.LessThan(0.000001f));
                Assert.That(previewMesh.vertexCount, Is.EqualTo(sourceMesh.vertexCount));
                Assert.That(previewMesh.subMeshCount, Is.EqualTo(sourceMesh.subMeshCount));

                Invoke(presenter, "Dispose");
                presenter = null;
                Assert.That(renderer.enabled, Is.True,
                    "표시기 종료 시 원본 Renderer 상태를 복원해야 합니다.");
            }
            finally
            {
                if (presenter != null)
                {
                    Invoke(presenter, "Dispose");
                }
                if (baselineMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(baselineMesh);
                }
                if (sourceMesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceMesh);
                }
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static object CreateCorrection(int vertexIndex, Vector3 delta)
        {
            return Activator.CreateInstance(
                RequireType("NativeSkinningVertexCorrection"),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { vertexIndex, delta },
                null);
        }

        private static object CreateFrame(int frameIndex, params object[] corrections)
        {
            Type correctionType = RequireType("NativeSkinningVertexCorrection");
            Array typedCorrections = Array.CreateInstance(correctionType, corrections.Length);
            for (int index = 0; index < corrections.Length; index++)
            {
                typedCorrections.SetValue(corrections[index], index);
            }

            return Activator.CreateInstance(
                RequireType("NativeSkinningCorrectionFrame"),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { frameIndex, typedCorrections },
                null);
        }

        private static object CreateCache(int vertexCount, params object[] frames)
        {
            Type frameType = RequireType("NativeSkinningCorrectionFrame");
            Array typedFrames = Array.CreateInstance(frameType, frames.Length);
            for (int index = 0; index < frames.Length; index++)
            {
                typedFrames.SetValue(frames[index], index);
            }

            return Activator.CreateInstance(
                RequireType("NativeSkinningCorrectionCache"),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { vertexCount, typedFrames },
                null);
        }

        private static object CreatePresenter(
            SkinnedMeshRenderer renderer,
            object cache)
        {
            return Activator.CreateInstance(
                RequireType("NativeSkinningCorrectionPresenter"),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { renderer, cache },
                null);
        }

        private static void CreateSkinnedTriangle(
            out GameObject root,
            out SkinnedMeshRenderer renderer,
            out Mesh mesh)
        {
            root = new GameObject("Native Skinning Presenter Test Root");
            root.hideFlags = HideFlags.HideAndDontSave;
            GameObject boneObject = new GameObject("Bone");
            boneObject.transform.SetParent(root.transform, false);
            GameObject rendererObject = new GameObject("Renderer");
            rendererObject.transform.SetParent(root.transform, false);
            renderer = rendererObject.AddComponent<SkinnedMeshRenderer>();

            mesh = new Mesh { name = "Native Skinning Presenter Test Mesh" };
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up
            };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bindposes = new[]
            {
                boneObject.transform.worldToLocalMatrix * rendererObject.transform.localToWorldMatrix
            };
            mesh.boneWeights = new[]
            {
                CreateBoneWeight(),
                CreateBoneWeight(),
                CreateBoneWeight()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            renderer.sharedMesh = mesh;
            renderer.bones = new[] { boneObject.transform };
            renderer.rootBone = boneObject.transform;
        }

        private static BoneWeight CreateBoneWeight()
        {
            return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        }

        private static Type RequireType(string shortName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                NamespacePrefix + shortName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{shortName} 제품 타입이 필요합니다.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }
    }
}
