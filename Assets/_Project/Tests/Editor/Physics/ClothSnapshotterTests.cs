using Fbx2Vmd.ClothPhysics;
using MagicaCloth2;
using NUnit.Framework;

namespace Tests.Editor.ClothPhysics
{
    /// <summary>Phase 5 — 클로스 설정 스냅샷의 결정성과 diff 검증.</summary>
    public class ClothSnapshotterTests
    {
        [Test]
        public void Given_SerializeData_When_Capture_Then_ValueFieldsFlattened()
        {
            var s = new ClothSerializeData();
            s.gravity = 7.5f;
            s.inertiaConstraint.depthInertia = 0.33f;
            var snap = ClothSnapshotter.Capture(s);
            Assert.AreEqual("7.5", snap["gravity"]);
            Assert.AreEqual("0.33", snap["inertiaConstraint.depthInertia"]);
            // 참조 필드(콜라이더/본 목록)는 스냅샷에 포함되지 않는다
            Assert.IsFalse(snap.ContainsKey("colliderCollisionConstraint.colliderList"));
            Assert.IsFalse(snap.ContainsKey("rootBones"));
        }

        [Test]
        public void Given_SameData_When_CaptureTwice_Then_IdenticalSnapshot()
        {
            var s = new ClothSerializeData();
            HairPhysicsParameters.Apply(HairPart.Tail, s, 0.6f, null, true,
                HairTuning.Default, false);
            var a = ClothSnapshotter.Serialize(ClothSnapshotter.Capture(s));
            var b = ClothSnapshotter.Serialize(ClothSnapshotter.Capture(s));
            Assert.AreEqual(a, b, "동일 입력은 바이트 동일 스냅샷이어야 한다");
        }

        [Test]
        public void Given_ChangedValue_When_Diff_Then_ReportsField()
        {
            var s = new ClothSerializeData();
            var before = ClothSnapshotter.Capture(s);
            s.damping.value = 0.77f;
            var after = ClothSnapshotter.Capture(s);
            var diffs = ClothSnapshotter.Diff(before, after);
            Assert.IsTrue(diffs.Exists(d => d.StartsWith("damping.value")),
                "damping.value 변경이 diff에 잡혀야 한다: " + string.Join(", ", diffs));
        }

        [Test]
        public void Given_Serialized_When_Deserialize_Then_RoundTrip()
        {
            var s = new ClothSerializeData();
            s.gravity = 3.3f;
            var text = ClothSnapshotter.Serialize(ClothSnapshotter.Capture(s));
            var map = ClothSnapshotter.Deserialize(text);
            Assert.AreEqual("3.3", map["gravity"]);
            Assert.IsTrue(map.ContainsKey("animationPoseRatio"));
        }

        [Test]
        public void Given_CurveData_When_Capture_Then_CurveKeysRecorded()
        {
            var s = new ClothSerializeData();
            s.damping.SetValue(0.5f, 1.0f, 0.3f, true);
            var snap = ClothSnapshotter.Capture(s);
            // 커브는 튜닝 값이므로 키·탄젠트까지 스냅샷에 남아야 한다
            Assert.IsTrue(snap.TryGetValue("damping.curve", out var v),
                "damping.curve 키가 스냅샷에 있어야 한다");
            Assert.IsTrue(v.Contains("0.3"),
                "커브 끝단 값(0.3)이 직렬화에 포함돼야 한다: " + v);
        }

        [Test]
        public void Given_ChangedCurveKey_When_Diff_Then_ReportsCurveChange()
        {
            var s = new ClothSerializeData();
            s.angleRestorationConstraint.stiffness.SetValue(0.2f, 1.0f, 0.5f, true);
            var before = ClothSnapshotter.Capture(s);
            var keys = s.angleRestorationConstraint.stiffness.curve.keys;
            keys[keys.Length - 1].value = 0.1f; // 끝단 강성만 변경
            s.angleRestorationConstraint.stiffness.curve.keys = keys;

            var diffs = ClothSnapshotter.Diff(before, ClothSnapshotter.Capture(s));
            Assert.IsTrue(
                diffs.Exists(d => d.StartsWith("angleRestorationConstraint.stiffness.curve")),
                "커브 키 변경이 diff에 잡혀야 한다: " + string.Join(", ", diffs));
        }
    }
}
