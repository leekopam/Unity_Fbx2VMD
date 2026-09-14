using System;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class EditorHumanoidFootSoleSamplerTests
    {
        private GameObject _root;
        private Transform _foot;
        private Transform _toes;
        private Mesh _mesh;
        private SkinnedMeshRenderer _skin;
        private object _sampler;

        [SetUp]
        public void CreateSurface()
        {
            _root = new GameObject("밑창 샘플링 테스트");
            _foot = new GameObject("발").transform;
            _foot.SetParent(_root.transform, false);
            _toes = new GameObject("발가락").transform;
            _toes.SetParent(_foot, false);
            _toes.localPosition = Vector3.forward * 0.2f;
            _skin = _root.AddComponent<SkinnedMeshRenderer>();
            _mesh = new Mesh();
            _mesh.vertices = new[]
            {
                new Vector3(-0.05f, 0f, -0.1f), new Vector3(0.05f, 0f, -0.1f),
                new Vector3(-0.05f, 0f, 0.3f), new Vector3(0.05f, 0f, 0.3f)
            };
            _mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            _mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 1, weight0 = 1f },
                new BoneWeight { boneIndex0 = 1, weight0 = 1f }
            };
            _mesh.bindposes = new[] { _foot.worldToLocalMatrix, _toes.worldToLocalMatrix };
            _skin.sharedMesh = _mesh;
            _skin.bones = new[] { _foot, _toes };
        }

        [TearDown]
        public void DisposeSurface()
        {
            (_sampler as IDisposable)?.Dispose();
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void Given_DeformingToes_When_Sampling_Then_UpdatesSamePointAndReusesBuffer()
        {
            CreateSampler();
            Assert.That(Call("TrySample"), Is.EqualTo(true));
            object[] select = { true, Vector3.up, -1, Vector3.zero };
            Assert.That(Call("TrySelectContact", select), Is.EqualTo(true));
            int index = (int)select[2];
            var buffer = (Vector3[])GetTypeUnderTest().GetProperty("LocalSolePoints", Flags).GetValue(_sampler);
            Vector3 before = buffer[index];
            _toes.localRotation = Quaternion.Euler(30f, 0f, 0f);
            Assert.That(Call("TrySample"), Is.EqualTo(true));
            Assert.That(GetTypeUnderTest().GetProperty("LocalSolePoints", Flags).GetValue(_sampler), Is.SameAs(buffer));
            Assert.That(Vector3.Distance(before, buffer[index]), Is.GreaterThan(0.01f));
            object[] point = { index, Vector3.zero };
            Assert.That(Call("TryGetLocalPoint", point), Is.EqualTo(true));
            Assert.That((Vector3)point[1], Is.EqualTo(buffer[index]));
        }

        [Test]
        public void Given_ChangedBindingOrDisposedSampler_When_Sampling_Then_RejectsStalePoints()
        {
            CreateSampler();
            Assert.That(Call("TrySample"), Is.EqualTo(true));
            _skin.bones = new[] { _toes, _foot };
            Assert.That(Call("TrySample"), Is.EqualTo(false));
            Assert.That(Call("TryGetLocalPoint", new object[] { 0, Vector3.zero }), Is.EqualTo(false));
            ((IDisposable)_sampler).Dispose();
            ((IDisposable)_sampler).Dispose();
            Assert.That(Call("TrySample"), Is.EqualTo(false));
        }

        [Test]
        public void Given_InvalidScaleOrMissingToes_When_Creating_Then_Rejects()
        {
            _foot.localScale = new Vector3(1f, 2f, 1f);
            object[] args = { _foot, _toes, new[] { _skin }, Vector3.up, null };
            Assert.That(GetTypeUnderTest().GetMethod("TryCreate", Flags).Invoke(null, args), Is.EqualTo(false));
            Assert.That(args[4], Is.Null);
            _foot.localScale = Vector3.one;
            args[1] = null;
            Assert.That(GetTypeUnderTest().GetMethod("TryCreate", Flags).Invoke(null, args), Is.EqualTo(false));
        }

        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private void CreateSampler()
        {
            object[] args = { _foot, _toes, new[] { _skin }, Vector3.up, null };
            Assert.That(GetTypeUnderTest().GetMethod("TryCreate", Flags).Invoke(null, args), Is.EqualTo(true));
            _sampler = args[4];
        }

        private object Call(string name, params object[] args) => GetTypeUnderTest().GetMethod(name, Flags).Invoke(_sampler, args);

        private static Type GetTypeUnderTest()
        {
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootSoleSampler");
            Assert.That(type, Is.Not.Null, "실제 밑창 샘플러가 필요합니다.");
            return type;
        }
    }
}
