using System.Collections.Generic;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidFootGoalDriverTests
    {
        private const string ModelPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const float FrameRate = 30f;
        private const int FrameCount = 60;
        private const int PinStart = 5;
        private const int PinEnd = 40;

        [Test]
        public void Given_UninitializedOrZeroAlpha_When_Applying_Then_PreservesPose()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 footPosition = foot.position;
                Quaternion footRotation = foot.rotation;
                Vector3 hipsPosition = hips.position;
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryApply(0f, 1f, Vector3.right, Vector3.right), Is.True);
                Assert.That(foot.position, Is.EqualTo(footPosition));

                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                Assert.That(driver.TryApply(PinStart * 2f / FrameRate, 0f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(foot.position, footPosition),
                    Is.LessThan(0.000001f), "alpha=0은 아무 본도 움직이면 안 됨");
                Assert.That(foot.rotation, Is.EqualTo(footRotation));
                Assert.That(hips.position, Is.EqualTo(hipsPosition));
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_ConfidentPlantIntent_When_ApplyingPinnedFrame_Then_DrivesFootToGoal()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                Vector3 goalWorld = ExpectedLeftGoalWorld(animator);
                Assert.That(Vector3.Distance(foot.position, goalWorld), Is.GreaterThan(0.03f),
                    "검증 전제 — 목표가 현재 발 위치와 달라야 함");
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(foot.position, goalWorld), Is.LessThan(0.002f),
                    "pin 확정 프레임에서 발이 목표로 이동해야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_UnpinnedFrames_When_ApplyingWithAlpha_Then_PreservesPose()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Vector3 footPosition = foot.position;
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                // pin이 없는 프레임 0은 alpha=1이어도 현행 출력을 그대로 유지함.
                Assert.That(driver.TryApply(0f, 1f, Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(foot.position, footPosition),
                    Is.LessThan(0.000001f));
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_UncertainOnlyIntent_When_Applying_Then_StaysPassThrough()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Vector3 footPosition = foot.position;
                var intents = new HumanoidFootContactIntentEstimate(
                    new List<HumanoidFootContactIntent>(),
                    new List<HumanoidFootContactIntent>(),
                    new List<Vector2Int> { new Vector2Int(PinStart, PinEnd) },
                    new List<Vector2Int>());
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), intents,
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(foot.position, footPosition),
                    Is.LessThan(0.000001f), "불확실 구간은 사람 표식 없이 pin을 열면 안 됨");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_SupportLabelOverUncertainSpan_When_Applying_Then_Pins()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var intents = new HumanoidFootContactIntentEstimate(
                    new List<HumanoidFootContactIntent>(),
                    new List<HumanoidFootContactIntent>(),
                    new List<Vector2Int> { new Vector2Int(PinStart, PinEnd) },
                    new List<Vector2Int>());
                var labels = new HumanoidFootContactIntentLabelSet(
                    new List<HumanoidFootContactIntentLabel>
                    {
                        new HumanoidFootContactIntentLabel(PinStart, PinEnd - 1, true, null),
                    },
                    new List<HumanoidFootContactIntentLabel>(), 1, 1);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), intents, labels,
                    animator.humanScale, FrameRate), Is.True);
                Vector3 goalWorld = ExpectedLeftGoalWorld(animator);
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(foot.position, goalWorld), Is.LessThan(0.002f),
                    "사람 지지 표식은 불확실 구간의 pin을 강제할 수 있어야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_NonSupportLabelOverPlant_When_Applying_Then_SuppressesPin()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Vector3 footPosition = foot.position;
                var labels = new HumanoidFootContactIntentLabelSet(
                    new List<HumanoidFootContactIntentLabel>
                    {
                        new HumanoidFootContactIntentLabel(PinStart, PinEnd - 1, false, null),
                    },
                    new List<HumanoidFootContactIntentLabel>(), 1, 1);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator), labels,
                    animator.humanScale, FrameRate), Is.True);
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(foot.position, footPosition),
                    Is.LessThan(0.000001f), "사람 비지지 표식은 confident 의도도 억제해야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_SlideOnlyIntent_When_Applying_Then_DoesNotPin()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Vector3 footPosition = foot.position;
                var slide = new HumanoidFootContactIntent(true, PinStart, PinEnd,
                    animator.GetBoneTransform(HumanBodyBones.LeftFoot).position +
                    LeftGoalOffset,
                    HumanoidFootContactIntentMode.Slide,
                    HumanoidFootContactIntentCertainty.Confident, false);
                var intents = new HumanoidFootContactIntentEstimate(
                    new List<HumanoidFootContactIntent> { slide },
                    new List<HumanoidFootContactIntent>(),
                    new List<Vector2Int>(), new List<Vector2Int>());
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), intents,
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(foot.position, footPosition),
                    Is.LessThan(0.000001f), "slide 의도는 접지만 표시하고 pin은 열지 않아야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_PinnedFrame_When_SeekingBackAndForth_Then_ReproducesSamePose()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                Vector3 original = foot.position;
                driver.TryApply(20f / FrameRate, 1f, Vector3.right, Vector3.right);
                Vector3 first = foot.position;
                foot.position = original;
                driver.TryApply(50f / FrameRate, 1f, Vector3.right, Vector3.right);
                foot.position = original;
                driver.TryApply(20f / FrameRate, 1f, Vector3.right, Vector3.right);
                Assert.That(Vector3.Distance(foot.position, first), Is.LessThan(0.000001f),
                    "탐색 점프 후 같은 프레임은 같은 결과를 재현해야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_MismatchedArrays_When_Initializing_Then_ReturnsFalseSafely()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    new Quaternion[1], BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.False,
                    "골반 궤적과 샘플 수가 다르면 비활성으로 남아야 함");
                Assert.That(driver.IsInitialized, Is.False);
                Assert.That(driver.TryApply(0f, 1f, Vector3.right, Vector3.right), Is.True);
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_GoalSample_When_ConvertingSpaces_Then_MatchesHipsBasis()
        {
            var goal = new HumanoidFootGoalStream.GoalSample
            {
                RootSpaceFootPosition = new Vector3(0.1f, -0.7f, 0.2f),
                RootSpaceFootRotation = Quaternion.identity,
                RootSpaceToesPosition = new Vector3(0.1f, -0.78f, 0.32f),
            };
            var hips = new GameObject("골반").transform;
            try
            {
                hips.SetPositionAndRotation(new Vector3(3f, 1f, -2f),
                    Quaternion.Euler(10f, 80f, -5f));
                Vector3 world = HumanoidFootGoalDriver.GoalToWorld(goal, hips, 2f);
                Assert.That(Vector3.Distance(world,
                    hips.position + hips.rotation * goal.RootSpaceFootPosition * 2f),
                    Is.LessThan(0.000001f));
                var rotated = new HumanoidFootGoalStream.GoalSample
                {
                    RootSpaceFootRotation = Quaternion.Euler(15f, -30f, 45f),
                };
                Quaternion frame = HumanoidFootGoalDriver.GoalFrameToWorld(rotated, hips);
                Assert.That(Quaternion.Angle(frame,
                    hips.rotation * Quaternion.Euler(15f, -30f, 45f)),
                    Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(hips.gameObject); }
        }

        [Test]
        public void Given_ExcessiveToePitch_When_SoftCapping_Then_CompressesSmoothly()
        {
            Vector3 normal = Vector3.up;
            Quaternion mild = Quaternion.AngleAxis(
                HumanoidFootGoalDriver.ToePitchSoftKneeDeg * 0.5f, Vector3.right);
            Assert.That(Quaternion.Angle(
                HumanoidFootGoalDriver.ApplyToePitchSoftCap(mild, normal, 1f), mild),
                Is.LessThan(0.001f), "무릎 각도 이내는 건드리지 않아야 함");

            Quaternion steep = Quaternion.AngleAxis(-80f, Vector3.right);
            Quaternion capped = HumanoidFootGoalDriver.ApplyToePitchSoftCap(
                steep, normal, 1f);
            float pitch = Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(capped * Vector3.forward, normal), -1f, 1f)) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.GreaterThanOrEqualTo(
                -HumanoidFootGoalDriver.MaximumSupportedToePitch - 0.01f),
                "하향 발끝 각도가 지지 한계를 넘으면 안 됨");
            Assert.That(pitch, Is.LessThan(-HumanoidFootGoalDriver.ToePitchSoftKneeDeg),
                "무릎 각도 초과분은 완전히 자르지 않고 압축해야 함");
        }

        [Test]
        public void Given_Smoothing_When_Stepping_Then_ExponentialApproach()
        {
            Assert.That(HumanoidFootGoalDriver.Smooth(1f, 5f, 12f, 0f), Is.EqualTo(1f),
                "dt=0은 현재값을 유지해야 함");
            Assert.That(HumanoidFootGoalDriver.Smooth(1f, 5f, 12f, 10f),
                Is.EqualTo(5f).Within(0.0001f), "큰 dt는 목표에 수렴해야 함");
            float step = HumanoidFootGoalDriver.Smooth(0f, 1f, 12f, 1f / 30f);
            Assert.That(step, Is.GreaterThan(0.2f).And.LessThan(0.5f));
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

        // 검증 궤적 — 왼발만 목표 위치로 옮기고 나머지는 현재 자세를 유지함.
        private static Vector3 LeftGoalOffset => new Vector3(0.05f, 0f, 0.03f);

        private static HumanoidFootContactSample[] BuildSamples(Animator animator)
        {
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            var samples = new HumanoidFootContactSample[FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                samples[frame] = new HumanoidFootContactSample(
                    leftFoot.position + LeftGoalOffset,
                    leftToes.position + LeftGoalOffset,
                    rightFoot.position, rightToes.position,
                    null, leftFoot.rotation, rightFoot.rotation);
            }
            return samples;
        }

        private static Vector3[] BuildHipsPositions(Animator animator)
        {
            var positions = new Vector3[FrameCount];
            Vector3 hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            for (int frame = 0; frame < FrameCount; frame++) positions[frame] = hips;
            return positions;
        }

        private static Quaternion[] BuildHipsRotations(Animator animator)
        {
            var rotations = new Quaternion[FrameCount];
            Quaternion hips = animator.GetBoneTransform(HumanBodyBones.Hips).rotation;
            for (int frame = 0; frame < FrameCount; frame++) rotations[frame] = hips;
            return rotations;
        }

        private static HumanoidFootContactIntentEstimate BuildPlantEstimate(Animator animator)
        {
            Vector3 anchor = (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position +
                animator.GetBoneTransform(HumanBodyBones.LeftToes).position) * 0.5f +
                LeftGoalOffset;
            return new HumanoidFootContactIntentEstimate(
                new List<HumanoidFootContactIntent>
                {
                    new HumanoidFootContactIntent(true, PinStart, PinEnd,
                        anchor,
                        HumanoidFootContactIntentMode.Plant,
                        HumanoidFootContactIntentCertainty.Confident, false),
                },
                new List<HumanoidFootContactIntent>(),
                new List<Vector2Int>(), new List<Vector2Int>());
        }

        private static Vector3 ExpectedLeftGoalWorld(Animator animator)
        {
            return animator.GetBoneTransform(HumanBodyBones.LeftFoot).position +
                LeftGoalOffset;
        }
    }
}
