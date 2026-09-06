using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmSupportPoseApplierTests
    {
        private GameObject _root;
        private Transform _driver;
        private Transform _support;
        private Mesh _mesh;
        private SkinnedMeshRenderer _skin;
        private object _applier;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("상완 보조 본 테스트");
            _root.hideFlags = HideFlags.HideAndDontSave;
            _driver = new GameObject("임의 리그 구동 본").transform;
            _support = new GameObject("임의 리그 의상 본").transform;
            _driver.SetParent(_root.transform, false);
            _support.SetParent(_root.transform, false);
            _support.localRotation = Quaternion.Euler(5f, 8f, 13f);
            _skin = _root.AddComponent<SkinnedMeshRenderer>();
            _skin.bones = new[] { _driver, _support };
            _mesh = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
                triangles = new[] { 0, 1, 2 },
                bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity },
                boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 1, weight0 = 1f }, 3).ToArray()
            };
            _skin.sharedMesh = _mesh;
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.HumanoidArmSupportPoseApplier", true);
            _applier = Activator.CreateInstance(type, true);
        }

        [TearDown]
        public void TearDown()
        {
            (_applier as IDisposable)?.Dispose();
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void Given_ExplicitPairWithDifferentNames_When_ApplyingRepeatedly_Then_UsesBindOffsetAndRestores()
        {
            Quaternion offset = Quaternion.Euler(0f, 0f, 20f);
            Quaternion original = _support.localRotation;
            _mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.Rotate(Quaternion.Inverse(offset)) };
            Assert.That(AddBinding(_skin), Is.True);
            Assert.That(AddBinding(_skin), Is.False, "동일 보조 본을 중복 소유하면 안 됩니다.");
            _driver.localRotation = Quaternion.Euler(40f, -25f, 15f);
            for (int repeat = 0; repeat < 5; repeat++)
            {
                Invoke("Apply");
                Assert.That(Quaternion.Angle(_support.localRotation, _driver.localRotation * offset),
                    Is.LessThan(0.05f));
                Assert.That(_support.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(_support.localScale, Is.EqualTo(Vector3.one));
            }
            ((IDisposable)_applier).Dispose();
            Assert.That(Quaternion.Angle(_support.localRotation, original), Is.LessThan(0.05f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void Given_InvalidRelativeBind_When_Adding_Then_RejectsWithoutMutation(int invalidKind)
        {
            Matrix4x4 invalid = Matrix4x4.identity;
            switch (invalidKind)
            {
                case 0: invalid.m00 = -1f; break;
                case 1: invalid.m11 = 2f; break;
                case 2: invalid.m01 = 0.2f; break;
                case 3: invalid.m03 = 0.1f; break;
                case 4: invalid = Matrix4x4.zero; break;
            }
            _mesh.bindposes = new[] { invalid, Matrix4x4.identity };
            Quaternion original = _support.localRotation;
            Assert.That(AddBinding(_skin), Is.False);
            Invoke("Apply");
            Assert.That(_support.localRotation, Is.EqualTo(original));
        }

        [Test]
        public void Given_ConflictingRendererBindings_When_Adding_Then_RejectsBoth()
        {
            Mesh otherMesh = UnityEngine.Object.Instantiate(_mesh);
            try
            {
                SkinnedMeshRenderer other = new GameObject("두 번째 renderer").AddComponent<SkinnedMeshRenderer>();
                other.transform.SetParent(_root.transform, false);
                other.bones = _skin.bones;
                other.sharedMesh = otherMesh;
                otherMesh.bindposes = new[] { Matrix4x4.Rotate(Quaternion.Euler(0f, 10f, 0f)), Matrix4x4.identity };
                Assert.That(AddBinding(_skin, other), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherMesh);
            }
        }

        [Test]
        public void Given_UnweightedSupportOrDifferentParent_When_Adding_Then_Rejects()
        {
            _mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1f }, 3).ToArray();
            Assert.That(AddBinding(_skin), Is.False);
            _support.SetParent(_driver, false);
            Assert.That(AddBinding(_skin), Is.False);
        }

        [TestCase("unknown")]
        [TestCase("authored")]
        [TestCase("guard")]
        [TestCase("ambiguous")]
        public void Given_UnsupportedOrOwnedRig_When_Initializing_Then_DoesNotOverrideSupport(string reason)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx");
            Assert.That(asset, Is.Not.Null);
            GameObject target = UnityEngine.Object.Instantiate(asset);
            AnimationClip clip = new AnimationClip();
            try
            {
                Animator animator = target.GetComponent<Animator>();
                Transform[] supports = target.GetComponentsInChildren<Transform>(true)
                    .Where(bone => bone.name.EndsWith(".joint_LeftArmM", StringComparison.Ordinal) ||
                        bone.name.EndsWith(".joint_RightArmM", StringComparison.Ordinal)).ToArray();
                Assert.That(supports.Length, Is.EqualTo(2));
                foreach (Transform support in supports)
                {
                    if (reason == "unknown") support.name = "미지원 의상 본";
                    if (reason == "authored")
                        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(
                            AnimationUtility.CalculateTransformPath(support, animator.transform),
                            typeof(Transform), "m_LocalRotation.x"), AnimationCurve.Constant(0f, 1f, 0f));
                    if (reason == "ambiguous")
                        new GameObject(support.name).transform.SetParent(support.parent, false);
                }
                if (reason == "guard")
                    target.AddComponent<Fbx2Vmd.FBXImporter.HumanoidArmSleeveAnchorGuard>()
                        .Configure(animator, 1f, 0f, 120f, false);
                Quaternion[] original = supports.Select(bone => bone.localRotation).ToArray();
                Invoke("Initialize", animator, clip);
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation = Quaternion.Euler(0f, 0f, 60f);
                animator.GetBoneTransform(HumanBodyBones.RightUpperArm).localRotation = Quaternion.Euler(0f, 0f, -60f);
                Invoke("Apply");
                for (int index = 0; index < supports.Length; index++)
                    Assert.That(supports[index].localRotation, Is.EqualTo(original[index]));
            }
            finally
            {
                ((IDisposable)_applier).Dispose();
                UnityEngine.Object.DestroyImmediate(clip);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private bool AddBinding(params SkinnedMeshRenderer[] skins)
        {
            return (bool)Invoke("TryAddBinding", _driver, _support, skins);
        }

        private object Invoke(string name, params object[] arguments)
        {
            return _applier.GetType().GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(_applier, arguments);
        }
    }
}
