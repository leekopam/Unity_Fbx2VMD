using System.IO;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootGoalStreamTests
    {
        private GameObject _root;
        private Transform _foot;
        private Transform _toes;
        private HumanoidFootGoalStream _stream;

        [SetUp]
        public void CreateRig()
        {
            _root = new GameObject("goal 스트림 루트");
            _foot = new GameObject("발목").transform;
            _foot.SetParent(_root.transform, false);
            _foot.localPosition = new Vector3(0f, 0.08f, 0f);
            _toes = new GameObject("발끝").transform;
            _toes.SetParent(_foot, false);
            _toes.localPosition = new Vector3(0f, -0.05f, 0.18f);
            _stream = new HumanoidFootGoalStream();
        }

        [TearDown]
        public void DisposeRig()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Given_Recording_When_RootMoves_Then_GoalStaysRootRelative()
        {
            _stream.Record(3, true, _root.transform, _foot, _toes);
            Vector3 first = _stream.Samples[0].RootSpaceFootPosition;
            _root.transform.position += new Vector3(5f, 0f, -2f);
            _root.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            _stream.Record(4, true, _root.transform, _foot, _toes);
            Vector3 moved = _stream.Samples[1].RootSpaceFootPosition;
            Assert.That(Vector3.Distance(first, moved), Is.LessThan(0.0001f),
                "루트가 움직여도 goal은 루트 상대 공간에서 불변이어야 함");
        }

        [Test]
        public void Given_WorldValues_When_RootMoves_Then_GoalStaysRootRelative()
        {
            // 안정화기의 골반 포즈 경로와 같은 입력 형태 — 루트가 움직여도 결과가 같아야 함.
            _stream.Record(0, true, new Vector3(0f, 1f, 0f), Quaternion.identity,
                new Vector3(0f, 0.1f, 0.2f), Quaternion.identity,
                new Vector3(0f, 0.02f, 0.3f));
            _stream.Record(1, true, new Vector3(3f, 1f, -2f), Quaternion.identity,
                new Vector3(3f, 0.1f, -1.8f), Quaternion.identity,
                new Vector3(3f, 0.02f, -1.7f));
            Assert.That(Vector3.Distance(
                    _stream.Samples[0].RootSpaceFootPosition,
                    _stream.Samples[1].RootSpaceFootPosition), Is.LessThan(0.0001f),
                "월드 입력도 루트 상대 공간에서 불변이어야 함");
        }

        [Test]
        public void Given_MissingToes_When_Recording_Then_ToeFallsBackToFoot()
        {
            _stream.Record(0, false, _root.transform, _foot, null);
            HumanoidFootGoalStream.GoalSample sample = _stream.Samples[0];
            Assert.That(sample.RootSpaceToesPosition,
                Is.EqualTo(sample.RootSpaceFootPosition));
            Assert.That(sample.SoleOffset, Is.EqualTo(0f));
            Assert.That(sample.IsLeft, Is.False);
        }

        [Test]
        public void Given_NullBones_When_Recording_Then_Throws()
        {
            Assert.That(() => _stream.Record(0, true, null, _foot, _toes),
                Throws.ArgumentNullException);
            Assert.That(() => _stream.Record(0, true, _root.transform, null, _toes),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Given_GoalBelowGround_When_Snapped_Then_ToeStaysAboveGround()
        {
            _stream.Record(0, true, _root.transform, _foot, _toes);
            HumanoidFootGoalStream.GoalSample sample = _stream.Samples[0];
            float below = sample.RootSpaceToesPosition.y - 0.02f;
            HumanoidFootGoalStream.GoalSample snapped =
                HumanoidFootGoalStream.SnapAboveGround(sample, below + 0.04f);
            Assert.That(snapped.RootSpaceToesPosition.y,
                Is.GreaterThanOrEqualTo(below + 0.04f));
            float unchanged = HumanoidFootGoalStream
                .SnapAboveGround(sample, below - 1f).RootSpaceToesPosition.y;
            Assert.That(unchanged,
                Is.EqualTo(sample.RootSpaceToesPosition.y).Within(0.0001f));
        }

        [Test]
        public void Given_Samples_When_WritingJsonl_Then_JoinScriptCanRead()
        {
            _stream.Record(7, false, _root.transform, _foot, _toes);
            using (var writer = new StringWriter())
            {
                _stream.WriteJsonl(writer);
                string line = writer.ToString().Trim();
                Assert.That(line, Does.StartWith("{\"frame\":7,\"side\":\"right\""));
                Assert.That(line, Does.Contain("\"sole_offset\":"));
            }
        }
    }
}
