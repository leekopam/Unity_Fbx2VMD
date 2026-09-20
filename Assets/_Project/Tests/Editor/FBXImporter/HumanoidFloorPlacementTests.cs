using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFloorPlacementTests
    {
        private GameObject _target;
        private GameObject _floor;
        private GameObject _guardObject;

        [SetUp]
        public void CreateTarget()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx");
            if (asset == null)
                Assert.Ignore("로컬 캐릭터 에셋이 없는 환경에서는 실제 밑창 검증을 생략함");
            _target = Object.Instantiate(asset);
            _target.transform.position = new Vector3(1000f, 3f, 1000f);
            _target.GetComponent<Animator>().runtimeAnimatorController = null;
            _guardObject = new GameObject("초기 배치 검증");
        }

        [TearDown]
        public void DisposeObjects()
        {
            Object.DestroyImmediate(_guardObject);
            Object.DestroyImmediate(_target);
            if (_floor != null)
                Object.DestroyImmediate(_floor);
        }

        [TestCase(1f)]
        [TestCase(2f)]
        public void Given_RaisedSoles_When_InitializingIdle_Then_AlignsWithoutBoneChangesOrAccumulation(float scale)
        {
            _target.transform.localScale = Vector3.one * scale;
            _floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _floor.transform.position = new Vector3(1000f, 3f, 1000f);
            Transform[] bones = _target.GetComponentsInChildren<Transform>().Where(t => t != _target.transform).ToArray();
            Vector3[] positions = bones.Select(t => t.localPosition).ToArray();
            Quaternion[] rotations = bones.Select(t => t.localRotation).ToArray();
            Vector3[] scales = bones.Select(t => t.localScale).ToArray();
            Assert.That(MeasureMinimumY(), Is.GreaterThan(3.004f));
            var guard = _guardObject.AddComponent<TargetIdlePoseGuard>();
            typeof(TargetIdlePoseGuard).GetField("_faceTargetToCameraOnIdle",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(guard, false);
            guard.SetTargetCharacter(_target);

            guard.Initialize();
            Vector3 placed = _target.transform.position;
            Assert.That(MeasureMinimumY(), Is.EqualTo(3f).Within(0.00001f));
            guard.Initialize();
            Assert.That(Vector3.Distance(_target.transform.position, placed), Is.LessThan(0.00001f));
            Assert.That(bones.Select(t => t.localPosition), Is.EqualTo(positions));
            Assert.That(bones.Select(t => t.localRotation), Is.EqualTo(rotations));
            Assert.That(bones.Select(t => t.localScale), Is.EqualTo(scales));

            _target.transform.position += Vector3.up;
            guard.TryApply(false, true);
            Assert.That(_target.transform.position.y, Is.EqualTo(placed.y + 1f).Within(0.00001f),
                "모션 재생 중에는 초기 배치가 점프 높이를 덮어쓰면 안 됩니다.");
            guard.TryApply(false, false);
            Assert.That(Vector3.Distance(_target.transform.position, placed), Is.LessThan(0.00001f),
                "대기로 돌아오면 정렬된 시작 위치를 복원해야 합니다.");
        }

        [Test]
        public void Given_NoFloor_When_Placing_Then_LeavesTargetUnchanged()
        {
            Vector3 before = _target.transform.position;
            Type type = typeof(FBXVmdPipeline).Assembly.GetType("Fbx2Vmd.FBXImporter.HumanoidFloorPlacement", true);
            bool placed = (bool)type.GetMethod("TryPlace", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { _target.GetComponent<Animator>() });
            Assert.That(placed, Is.False);
            Assert.That(_target.transform.position, Is.EqualTo(before));
        }

        private float MeasureMinimumY()
        {
            var mesh = new Mesh();
            try
            {
                float minimum = float.PositiveInfinity;
                foreach (var renderer in _target.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.BakeMesh(mesh);
                    foreach (Vector3 vertex in mesh.vertices)
                        minimum = Mathf.Min(minimum, renderer.transform.TransformPoint(vertex).y);
                }
                return minimum;
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
