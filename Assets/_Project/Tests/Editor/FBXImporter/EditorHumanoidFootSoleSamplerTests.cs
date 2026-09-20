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

        [TestCase(false)]
        [TestCase(true)]
        public void Given_LowerMiddleSole_When_SelectingContact_Then_IncludesWholeSupportRegion(bool isFront)
        {
            _mesh.vertices = new[]
            {
                new Vector3(-0.05f, 0f, -0.1f), new Vector3(0.05f, 0f, -0.1f),
                new Vector3(-0.05f, 0f, 0.3f), new Vector3(0.05f, 0f, 0.3f),
                new Vector3(0f, -0.01f, 0.08f), new Vector3(0f, -0.01f, 0.12f)
            };
            var weights = new BoneWeight[6];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            _mesh.boneWeights = weights;
            CreateSampler();

            object[] select = { isFront, Vector3.up, -1, Vector3.zero };
            Assert.That(Call("TrySelectContact", select), Is.True);
            Assert.That(((Vector3)select[3]).y, Is.EqualTo(-0.01f).Within(0.000001f),
                "관통 검사에 포함된 중앙 밑창을 앞·뒤 접촉 후보에서 빠뜨리면 안 됨");
        }

        [TestCase(0f)]
        [TestCase(5f)]
        public void Given_AlignedHeading_When_FindingSupportPose_Then_PreservesReferenceYaw(float pitch)
        {
            CreateSampler();
            Quaternion heading = Quaternion.AngleAxis(30f, Vector3.up);
            Quaternion reference = heading * Quaternion.AngleAxis(pitch, Vector3.right);
            Type plan = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootContactPlan", true);
            object[] args = { _foot, _sampler, heading * Vector3.right, reference, Vector3.up,
                Quaternion.identity, -1, -1 };
            Assert.That(plan.GetMethod("TryFindSupportPose", Flags).Invoke(null, args), Is.True);
            Assert.That(Quaternion.Angle((Quaternion)args[5], heading), Is.LessThan(0.05f),
                "현재 정렬한 방향을 샘플링 당시 원본 방향으로 되돌리면 안 됨");
        }

        [TestCase(1f, 0.001f, 0f)]
        [TestCase(1f, 0.0001f, 0f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 1f, 20f)]
        public void Given_LowerContactRelease_When_AligningSupport_Then_PreservesRemainingContact(
            float rearWeight, float frontWeight, float expectedPitch)
        {
            CreateSampler();
            Type grounding = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.EditorHumanoidFootGrounding", true);
            Type legType = grounding.GetNestedType("Leg", BindingFlags.NonPublic);
            object leg = Activator.CreateInstance(legType, Flags, null,
                new object[] { _root.transform, _root.transform, _foot, _toes, _sampler,
                    Vector3.forward, Quaternion.identity }, null);
            legType.GetField("_ground", Flags).SetValue(leg, new RaycastHit { normal = Vector3.up });
            legType.GetField("_originalFootRotation", Flags).SetValue(leg, Quaternion.identity);
            legType.GetField("_targetFootRotation", Flags).SetValue(leg, Quaternion.AngleAxis(20f, Vector3.right));
            legType.GetField("_activeWeights", Flags).SetValue(leg, new Vector2(rearWeight, frontWeight));

            Assert.That(legType.GetMethod("TryAlignSupportSurface", Flags).Invoke(leg, null), Is.True);
            var result = (Quaternion)legType.GetField("_targetFootRotation", Flags).GetValue(leg);
            Assert.That(Quaternion.Angle(result, Quaternion.AngleAxis(expectedPitch, Vector3.right)), Is.LessThan(0.05f),
                "반대쪽 지지 해제 여부로 남은 지지점 보정이 끊기거나 정상 발 구르기가 사라지면 안 됨");
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
