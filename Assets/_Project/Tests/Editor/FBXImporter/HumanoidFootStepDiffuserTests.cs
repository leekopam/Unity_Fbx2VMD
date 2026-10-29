using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootStepDiffuserTests
    {
        private const string ModelPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const float FrameRate = 60f;

        [Test]
        public void Given_Uninitialized_When_Applying_Then_ReturnsFalse()
        {
            var diffuser = new HumanoidFootStepDiffuser();
            Assert.That(diffuser.TryApply(0f, FrameRate), Is.False);
        }

        [Test]
        public void Given_PlantedFootTeleport_When_Applying_Then_SpreadsMoveOverFrames()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var diffuser = new HumanoidFootStepDiffuser();
                diffuser.Initialize(animator);

                // 정지 상태 확립 — 두 프레임 연속 평가로 prev 상태를 채움.
                Assert.That(diffuser.TryApply(0f, FrameRate), Is.False);
                Assert.That(diffuser.TryApply(1f / FrameRate, FrameRate), Is.False);
                Vector3 planted = foot.position;

                // 정지 발이 한 프레임에 150mm 순간이동하는 소스 불연속을 시뮬레이트.
                Vector3 teleport = planted + new Vector3(0f, 0f, 0.15f);
                foot.position = teleport;
                diffuser.TryApply(2f / FrameRate, FrameRate);
                float firstStep = Vector3.Distance(foot.position, planted);
                Assert.That(firstStep, Is.LessThan(0.06f),
                    "순간이동 첫 프레임의 출력 이동은 확산으로 제한되어야 함");
                Assert.That(firstStep, Is.GreaterThan(0.001f),
                    "잔여가 상쇄되는 동안 발은 목표를 향해 미끄러져야 함");

                // 확산 중 원본이 같은 위치를 유지하면 출력은 목표로 수렴함.
                for (int f = 3; f < 20; f++)
                {
                    foot.position = teleport;
                    diffuser.TryApply(f / FrameRate, FrameRate);
                }
                Assert.That(Vector3.Distance(foot.position, teleport), Is.LessThan(0.002f),
                    "잔여 소화 후 발은 원본 목표에 도달해야 함");
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_SustainedFastMotion_When_Applying_Then_PreservesMotion()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var diffuser = new HumanoidFootStepDiffuser();
                diffuser.Initialize(animator);

                Vector3 origin = foot.position;
                Assert.That(diffuser.TryApply(0f, FrameRate), Is.False);

                // 연속 고속 스윙 — 킥 개시 수준(매 프레임 70mm)의 이동은 발화 임계
                // 미만이라 정지 발 판정에 걸리지 않고 원본 궤적을 그대로 따라가야 함.
                for (int f = 1; f <= 4; f++)
                {
                    foot.position = origin + new Vector3(0f, 0f, 0.07f * f);
                    Assert.That(diffuser.TryApply(f / FrameRate, FrameRate), Is.False,
                        $"f{f} 발차기 수준 이동은 확산하지 않아야 함");
                }
                Assert.That(Vector3.Distance(foot.position, origin + new Vector3(0f, 0f, 0.28f)),
                    Is.LessThan(0.001f), "연속 고속 동작은 확산 없이 원본을 따라가야 함");
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_SeekJump_When_Applying_Then_ResetsTrackWithoutDiffusion()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var diffuser = new HumanoidFootStepDiffuser();
                diffuser.Initialize(animator);

                Assert.That(diffuser.TryApply(0f, FrameRate), Is.False);
                Assert.That(diffuser.TryApply(1f / FrameRate, FrameRate), Is.False);

                // 탐색 — 시간이 크게 건너뛰면 정지 발 판정 없이 상태를 리셋함.
                Vector3 seekPosition = foot.position + new Vector3(0f, 0f, 0.5f);
                foot.position = seekPosition;
                diffuser.TryApply(10f, FrameRate);
                Assert.That(Vector3.Distance(foot.position, seekPosition), Is.LessThan(0.001f),
                    "탐색 직후 프레임은 확산 없이 원본 위치를 유지해야 함");
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Given_UnconstrainedAirborneStep_When_Applying_Then_PreservesNegativeDebt(
            bool useGroundResponse, bool hasGround)
        {
            Animator animator = LoadTargetAnimator();
            Assert.That(animator, Is.Not.Null, "분산 회귀 모델이 필요합니다.");
            GameObject floor = null;
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform[] bones = animator.GetComponentsInChildren<Transform>(true);
                Vector3[] positions = bones.Select(b => b.localPosition).ToArray();
                Quaternion[] rotations = bones.Select(b => b.localRotation).ToArray();
                Vector3 planted = foot.position;
                Vector3 raw = planted + new Vector3(0f, 0.15f, 0.15f);
                if (hasGround)
                {
                    floor = new GameObject("공중 분산 회귀 바닥");
                    floor.transform.position = new Vector3(planted.x,
                        planted.y - animator.leftFeetBottomHeight - 0.15f, planted.z);
                    floor.AddComponent<BoxCollider>().size = new Vector3(2f, 0.1f, 2f);
                }
                var response = new HumanoidGroundResponse();
                response.Initialize(animator);
                Physics.SyncTransforms();
                Assert.That(response.TryFindGround(raw, out _), Is.EqualTo(hasGround));
                Vector3 baseline = default;
                for (int pass = 0; pass < 2; pass++)
                {
                    RestorePose();
                    var diffuser = new HumanoidFootStepDiffuser();
                    diffuser.Initialize(animator);
                    diffuser.TryApply(0f, FrameRate);
                    diffuser.TryApply(1f / FrameRate, FrameRate);
                    foot.position = raw;
                    Quaternion rotation = foot.rotation;
                    HumanoidGroundResponse constraint = pass == 1 && useGroundResponse ? response : null;
                    Assert.That(diffuser.TryApply(2f / FrameRate, FrameRate, constraint), Is.True);
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                    object track = typeof(HumanoidFootStepDiffuser).GetField("_left", flags).GetValue(diffuser);
                    Vector3 debt = (Vector3)track.GetType().GetField("Debt", flags).GetValue(track);
                    Assert.That(debt.y, Is.LessThan(0f),
                        "공중에서 음수 수직 잔여를 임의로 제거하면 안 됩니다.");
                    Assert.That(Quaternion.Angle(foot.rotation, rotation), Is.LessThan(0.01f));
                    if (pass == 0) baseline = foot.position;
                    else Assert.That(Vector3.Distance(foot.position, baseline), Is.LessThan(0.00001f));

                    for (int repeat = 0; repeat < 3; repeat++)
                    {
                        RestorePose();
                        foot.position = raw;
                        diffuser.TryApply(2f / FrameRate, FrameRate, constraint);
                        track = typeof(HumanoidFootStepDiffuser).GetField("_left", flags).GetValue(diffuser);
                        Vector3 repeatedDebt = (Vector3)track.GetType().GetField("Debt", flags).GetValue(track);
                        Assert.That(Vector3.Distance(repeatedDebt, debt), Is.LessThan(0.00001f));
                        Assert.That(Vector3.Distance(foot.position, baseline), Is.LessThan(0.00001f),
                            "같은 원본 자세와 시각의 재평가에서 잔여를 소진하면 안 됩니다.");
                    }
                }

                void RestorePose()
                {
                    for (int index = 0; index < bones.Length; index++)
                    {
                        bones[index].localPosition = positions[index];
                        bones[index].localRotation = rotations[index];
                    }
                }
            }
            finally
            {
                if (floor != null) Object.DestroyImmediate(floor);
                Object.DestroyImmediate(animator.transform.root.gameObject);
            }
        }

        private static Animator LoadTargetAnimator()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null)
            {
                return null;
            }
            GameObject instance = Object.Instantiate(asset);
            instance.hideFlags = HideFlags.HideAndDontSave;
            // 열린 씬의 물체와 겹치지 않게 멀리 배치함.
            instance.transform.SetPositionAndRotation(new Vector3(40f, 5f, 40f),
                Quaternion.identity);
            foreach (MonoBehaviour script in
                     instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                script.enabled = false;
            }
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            animator.enabled = true;
            return animator;
        }
    }
}
