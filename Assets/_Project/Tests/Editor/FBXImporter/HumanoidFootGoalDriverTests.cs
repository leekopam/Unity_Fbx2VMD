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

        [TestCase(0f, 1f, false, 0f)]
        [TestCase(40f, 1f, false, 0f)]
        [TestCase(40f, 0.25f, false, 0f)]
        [TestCase(40f, 1f, true, 0f)]
        [TestCase(40f, 1f, true, 25f)]
        public void Given_UnpinnedSupportRelease_When_SourceFootDiffers_Then_DoesNotActivateSourceGoal(
            float sourceYaw, float alpha, bool liftFoot, float upstreamBendAngle)
        {
            Animator animator = LoadTargetAnimator();
            Assert.That(animator, Is.Not.Null);
            try
            {
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Vector3 supportedPosition = right.position;
                Quaternion supportedRotation = right.rotation;
                var samples = BuildSamples(animator, Vector3.zero);
                for (int frame = 0; frame < samples.Length; frame++)
                {
                    HumanoidFootContactSample sample = samples[frame];
                    samples[frame] = new HumanoidFootContactSample(
                        sample.LeftFoot, sample.LeftToes,
                        sample.RightFoot + Vector3.right * 0.15f,
                        sample.RightToes + Vector3.right * 0.15f,
                        sample.RelativeFootHeights, sample.LeftFootFrame,
                        Quaternion.AngleAxis(sourceYaw, Vector3.up) * sample.RightFootFrame.Value);
                }
                var driver = new HumanoidFootGoalDriver();
                var noPlant = new HumanoidFootContactIntentEstimate(
                    new List<HumanoidFootContactIntent>(), new List<HumanoidFootContactIntent>(),
                    new List<Vector2Int>(), new List<Vector2Int>());
                Assert.That(driver.TryInitializeCore(animator, samples,
                    BuildHipsPositions(animator), BuildHipsRotations(animator), noPlant,
                    HumanoidFootContactIntentLabelSet.Empty, animator.humanScale, FrameRate), Is.True);
                driver.CaptureValidatedSupport(false, true);
                Assert.That(driver.TryApply(20f / FrameRate, alpha, Vector3.right, Vector3.right), Is.True);
                Assert.That(driver.DebugRightWeight, Is.Zero);
                if (liftFoot)
                {
                    Transform upper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    Transform lower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                    Assert.That(HumanoidLegPoseSolver.TrySolve(upper, lower, right,
                        right.position + Vector3.up * 0.06f, Vector3.right, out float error), Is.True);
                    Assert.That(error, Is.LessThan(0.001f));
                    if (upstreamBendAngle > 0f)
                    {
                        Vector3 originalKnee = lower.position;
                        Vector3 planeNormal = Vector3.Cross(lower.position - upper.position,
                            right.position - lower.position).normalized;
                        Vector3 rotatedNormal = Quaternion.AngleAxis(upstreamBendAngle,
                            (right.position - upper.position).normalized) * planeNormal;
                        Assert.That(HumanoidLegPoseSolver.TrySolve(upper, lower, right,
                            right.position, rotatedNormal, out error), Is.True);
                        Assert.That(error, Is.LessThan(0.001f));
                        Assert.That(Vector3.Distance(lower.position, originalKnee), Is.GreaterThan(0.005f));
                    }
                    right.rotation = Quaternion.AngleAxis(20f, Vector3.up) * right.rotation;
                    supportedPosition = right.position;
                    supportedRotation = right.rotation;
                }
                Transform upperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                Transform lowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                Vector3 upstreamKnee = lowerLeg.position;
                Quaternion upstreamUpperRotation = upperLeg.rotation;
                Quaternion upstreamLowerRotation = lowerLeg.rotation;
                driver.CaptureValidatedSupport(false, false);
                Assert.That(driver.TryApply(21f / FrameRate, alpha, Vector3.right, Vector3.right), Is.True);
                Assert.That(driver.DebugRightWeight, Is.Zero);
                Assert.That(driver.DebugSeparationWeight, Is.Zero);
                Assert.That(driver.LastAppliedPelvisOffset, Is.EqualTo(0f).Within(0.000001f));
                Assert.That(driver.LastUsedRightBendNormal, Is.EqualTo(Vector3.right),
                    "기권 뒤 지면 보정에는 이전 IK 법선 대신 이번 프레임의 입력 법선을 전달해야 합니다.");
                Assert.That(Vector3.Distance(right.position, supportedPosition), Is.LessThan(0.001f),
                    "지지 해제 감쇠가 기권 중인 원본 발 목표를 새로 활성화하면 안 됩니다.");
                Assert.That(Quaternion.Angle(right.rotation, supportedRotation), Is.LessThan(0.05f));
                Assert.That(Vector3.Distance(lowerLeg.position, upstreamKnee), Is.LessThan(0.001f),
                    "기권 중인 지지 해제는 앞 단계에서 만든 무릎 자세를 바꾸면 안 됩니다.");
                Assert.That(Quaternion.Angle(upperLeg.rotation, upstreamUpperRotation), Is.LessThan(0.05f));
                Assert.That(Quaternion.Angle(lowerLeg.rotation, upstreamLowerRotation), Is.LessThan(0.05f));
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_ValidatedSupport_When_SeparationBegins_Then_MovesTowardSourceGoal()
        {
            Animator animator = LoadTargetAnimator();
            Assert.That(animator, Is.Not.Null);
            try
            {
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform upper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                Transform lower = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                Vector3 support = left.position;
                Vector3 sourceGoal = support + LeftGoalOffset;
                Vector3 lowerLocal = lower.localPosition;
                Vector3 footLocal = right.localPosition;
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty, animator.humanScale, FrameRate), Is.True);
                driver.CaptureValidatedSupport(true, false);
                Assert.That(driver.TryApply(20f / FrameRate, 1f, Vector3.right, Vector3.right), Is.True);
                Assert.That(driver.DebugSeparationWeight, Is.Zero);
                Assert.That(driver.DebugLeftWeight, Is.EqualTo(1f));
                Assert.That(Vector3.Distance(driver.DebugLeftSolveGoal, support), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(sourceGoal, support), Is.GreaterThan(0.03f));

                // 본 길이를 바꾸지 않고 오른다리 IK로 발 모임을 구성함.
                Vector3 closeTarget = left.position + Vector3.right * 0.01f + Vector3.up * 0.03f;
                Assert.That(HumanoidLegPoseSolver.TrySolve(upper, lower, right,
                    closeTarget, Vector3.right, out float error), Is.True);
                Assert.That(error, Is.LessThan(0.001f));
                float before = Vector3.ProjectOnPlane(left.position - right.position, Vector3.up).magnitude;
                Assert.That(before, Is.LessThan(HumanoidFootGoalDriver.MinimumFootSeparationMeters));
                driver.CaptureValidatedSupport(true, false);
                Assert.That(driver.TryApply(21f / FrameRate, 1f, Vector3.right, Vector3.right), Is.True);
                Assert.That(driver.DebugSeparationWeight, Is.GreaterThan(0f).And.LessThan(0.2f));
                Assert.That(Vector3.ProjectOnPlane(driver.DebugLeftSolveGoal - sourceGoal, Vector3.up).magnitude,
                    Is.LessThan(Vector3.ProjectOnPlane(support - sourceGoal, Vector3.up).magnitude),
                    "이전 지지 앵커에 멈추지 않고 원본 분리 목표로 이동해야 합니다.");
                Assert.That(Vector3.Distance(driver.DebugLeftSolveGoal, support), Is.GreaterThan(0.001f));
                Assert.That(Vector3.Distance(driver.DebugLeftSolveGoal, sourceGoal), Is.GreaterThan(0.001f),
                    "복구 첫 프레임에 반대 목표로 단발 교체하면 안 됩니다.");
                Assert.That(Vector3.ProjectOnPlane(left.position - right.position, Vector3.up).magnitude,
                    Is.GreaterThan(before), "실제 발 간격도 회복되어야 합니다.");
                Assert.That(lower.localPosition, Is.EqualTo(lowerLocal));
                Assert.That(right.localPosition, Is.EqualTo(footLocal));
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [TestCase(1f)]
        [TestCase(0.25f)]
        public void Given_UnpinnedSupport_When_OtherLegLowersPelvis_Then_PreservesSupportedFoot(float alpha)
        {
            Animator animator = LoadTargetAnimator();
            Assert.That(animator, Is.Not.Null);
            try
            {
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Vector3 support = right.position;
                Quaternion supportRotation = right.rotation;
                Vector3 sourceGoal = left.position + Vector3.down * 0.03f;
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator, Vector3.down * 0.03f), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty, animator.humanScale, FrameRate), Is.True);
                driver.CaptureValidatedSupport(false, true);
                Assert.That(driver.TryApply(20f / FrameRate, alpha, Vector3.right, Vector3.right), Is.True);
                Assert.That(driver.DebugLeftWeight, Is.EqualTo(alpha));
                Assert.That(driver.DebugRightWeight, Is.Zero);
                Assert.That(driver.DebugSeparationWeight, Is.Zero);
                Assert.That(driver.LastAppliedPelvisOffset, Is.LessThan(0f));
                if (alpha == 1f)
                {
                    Assert.That(driver.LastAppliedPelvisOffset, Is.LessThan(-0.02f));
                    Assert.That(Vector3.Distance(left.position, sourceGoal), Is.LessThan(0.002f));
                }
                Assert.That(Vector3.Distance(right.position, support), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(right.rotation, supportRotation), Is.LessThan(0.05f));
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        public void Given_PartialGoalWeight_When_BlendingLeg_Then_PreservesSolvedLowerRotationTarget(float alpha)
        {
            Animator fullAnimator = LoadTargetAnimator();
            Animator partialAnimator = LoadTargetAnimator();
            try
            {
                if (fullAnimator == null || partialAnimator == null)
                    Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");

                Transform lower = partialAnimator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform foot = partialAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Quaternion initialLowerRotation = lower.rotation;
                Vector3 lowerPosition = lower.localPosition;
                Vector3 footPosition = foot.localPosition;
                Vector3 offset = new Vector3(0.05f, 0.08f, 0.03f);
                var fullDriver = new HumanoidFootGoalDriver();
                var partialDriver = new HumanoidFootGoalDriver();
                Assert.That(fullDriver.TryInitializeCore(fullAnimator,
                    BuildSamples(fullAnimator, offset), BuildHipsPositions(fullAnimator),
                    BuildHipsRotations(fullAnimator), BuildPlantEstimate(fullAnimator),
                    HumanoidFootContactIntentLabelSet.Empty, fullAnimator.humanScale, FrameRate), Is.True);
                Assert.That(partialDriver.TryInitializeCore(partialAnimator,
                    BuildSamples(partialAnimator, offset), BuildHipsPositions(partialAnimator),
                    BuildHipsRotations(partialAnimator), BuildPlantEstimate(partialAnimator),
                    HumanoidFootContactIntentLabelSet.Empty, partialAnimator.humanScale, FrameRate), Is.True);

                Assert.That(fullDriver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(fullDriver.DebugLeftSolveSucceeded, Is.True);
                Quaternion solvedLowerRotation = fullAnimator
                    .GetBoneTransform(HumanBodyBones.LeftLowerLeg).rotation;
                Assert.That(fullDriver.LastAppliedPelvisOffset, Is.EqualTo(0f).Within(0.000001f),
                    "골반 이동이 없는 도달 가능한 목표로 회전 혼합만 분리함");
                Assert.That(Quaternion.Angle(initialLowerRotation, solvedLowerRotation), Is.GreaterThan(5f));

                Assert.That(partialDriver.TryApply(20f / FrameRate, alpha,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(partialDriver.DebugLeftWeight, Is.EqualTo(alpha).Within(0.000001f));
                Assert.That(partialDriver.DebugLeftSolveSucceeded, Is.True);
                Assert.That(partialDriver.LastAppliedPelvisOffset, Is.EqualTo(0f).Within(0.000001f));
                Assert.That(Vector3.Distance(partialDriver.DebugLeftSolveGoal, fullDriver.DebugLeftSolveGoal),
                    Is.LessThan(0.000001f), "두 경로의 IK 목표가 같아야 함");
                Quaternion expected = Quaternion.Slerp(initialLowerRotation, solvedLowerRotation, alpha);
                Assert.That(Quaternion.Angle(lower.rotation, expected), Is.LessThan(0.05f),
                    "허벅지 혼합이 종아리의 완전 IK 목표 회전을 바꾸면 안 됨");
                Assert.That(lower.localPosition, Is.EqualTo(lowerPosition));
                Assert.That(foot.localPosition, Is.EqualTo(footPosition));
            }
            finally
            {
                if (fullAnimator != null) Object.DestroyImmediate(fullAnimator.transform.root.gameObject);
                if (partialAnimator != null) Object.DestroyImmediate(partialAnimator.transform.root.gameObject);
            }
        }

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
        public void Given_GoalBeyondLegReach_When_ApplyingPinnedFrame_Then_DropsPelvisToReach()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                // 기본 자세의 거의 펴진 다리보다 3cm 아래 목표 — 골반 드롭 없이는 도달 불가함.
                Vector3 downOffset = new Vector3(0f, -0.03f, 0f);
                Vector3 goalWorld = foot.position + downOffset;
                float hipsHeight = hips.position.y;
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator, downOffset), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(hips.position.y, Is.LessThan(hipsHeight - 0.02f),
                    "도달 거리를 넘는 목표는 골반을 내려 흡수해야 함");
                Assert.That(Vector3.Distance(foot.position, goalWorld), Is.LessThan(0.002f),
                    "골반 드롭 뒤 발이 목표에 도달해야 함");
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
        public void Given_MergedFeetOnUnpinnedFrame_When_Applying_Then_SeparatesTowardGoals()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                // 근육 리타겟이 두 발을 한 점에 모은 출력을 재현 — pin 없는 프레임 0.
                Vector3 rightGoalWorld = rightFoot.position;
                rightFoot.position = leftFoot.position;
                Assert.That(driver.TryApply(0f, 1f, Vector3.right, Vector3.right), Is.True);
                float separation = Mathf.Sqrt(
                    (leftFoot.position.x - rightFoot.position.x) *
                    (leftFoot.position.x - rightFoot.position.x) +
                    (leftFoot.position.z - rightFoot.position.z) *
                    (leftFoot.position.z - rightFoot.position.z));
                Assert.That(separation,
                    Is.GreaterThan(HumanoidFootGoalDriver.MinimumFootSeparationMeters),
                    $"발 모임 출력은 소스 목표로 분리돼야 함 sep={separation:F4}");
                Assert.That(Vector3.Distance(rightFoot.position, rightGoalWorld),
                    Is.LessThan(0.01f),
                    "오른발은 원래 지지 목표로 돌아가야 함");
                Assert.That(driver.DebugSeparationWeight, Is.GreaterThan(0f));
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [TestCase(1f)]
        [TestCase(0.5f)]
        [TestCase(0.25f)]
        public void Given_ValidatedSupportWithMergedFeet_When_Separating_Then_PreservesSupportTargetAndRotation(
            float alpha)
        {
            Animator animator = LoadTargetAnimator();
            Assert.That(animator, Is.Not.Null);
            try
            {
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator, Vector3.zero), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                // 상위 접지 단계가 원본 목표와 다른 높이·회전으로 발을 지지한 상태를 재현함.
                animator.GetBoneTransform(HumanBodyBones.Hips).position += Vector3.up * 0.04f;
                right.position = left.position;
                left.rotation = Quaternion.AngleAxis(15f, left.right) * left.rotation;
                right.rotation = Quaternion.AngleAxis(15f, right.right) * right.rotation;
                float leftHeight = left.position.y;
                float rightHeight = right.position.y;
                Quaternion leftRotation = left.rotation;
                Quaternion rightRotation = right.rotation;
                driver.CaptureValidatedSupport(true, true);

                Assert.That(driver.TryApply(0f, alpha, Vector3.right, Vector3.right), Is.True);
                Assert.That(driver.DebugLeftWeight, Is.Zero);
                Assert.That(driver.DebugRightWeight, Is.Zero);
                Assert.That(driver.DebugSeparationWeight, Is.EqualTo(alpha).Within(0.000001f));
                Assert.That(driver.DebugLeftSolveGoal.y, Is.EqualTo(leftHeight).Within(0.00001f),
                    "핀 없는 발 모임 복구가 검증된 지지 높이를 원본 높이로 바꾸면 안 됩니다.");
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                object rightDrive = typeof(HumanoidFootGoalDriver).GetField("_right", flags).GetValue(driver);
                Vector3 rightSolveGoal = (Vector3)rightDrive.GetType().GetProperty(
                    "LastSolveGoal", flags).GetValue(rightDrive);
                Assert.That(rightSolveGoal.y, Is.EqualTo(rightHeight).Within(0.00001f));
                float supportedHeightTolerance = alpha == 1f ? 0.0001f : animator.humanScale *
                    HumanoidFootGrounding.SupportedContactErrorPerHumanScale;
                Assert.That(left.position.y, Is.EqualTo(leftHeight).Within(supportedHeightTolerance));
                Assert.That(right.position.y, Is.EqualTo(rightHeight).Within(supportedHeightTolerance),
                    "부분 IK의 발목 오차도 기존 접지 계수로 환산한 국소 한도를 넘어서는 안 됩니다.");
                Assert.That(Quaternion.Angle(left.rotation, leftRotation), Is.LessThan(0.05f));
                Assert.That(Quaternion.Angle(right.rotation, rightRotation), Is.LessThan(0.05f),
                    "부분 가중치에서도 IK 부모 회전이 검증된 발 회전에 남으면 안 됩니다.");
                float separation = Vector2.Distance(new Vector2(left.position.x, left.position.z),
                    new Vector2(right.position.x, right.position.z));
                Assert.That(separation, Is.GreaterThan(alpha == 1f
                    ? HumanoidFootGoalDriver.MinimumFootSeparationMeters : 0.001f),
                    "높이·회전 보존 중에도 기존 수평 분리를 유지해야 합니다.");
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_ValidatedSeparation_When_PinEntersAndReleases_Then_TransitionsContinuously()
        {
            Animator animator = LoadTargetAnimator();
            Assert.That(animator, Is.Not.Null);
            try
            {
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator, Vector3.zero), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                Vector3 leftSupport = left.position + Vector3.up * 0.04f;
                Assert.That(HumanoidLegPoseSolver.TrySolve(
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), left,
                    leftSupport, Vector3.right, out float leftError), Is.True);
                Assert.That(leftError, Is.LessThan(0.0001f));
                Assert.That(HumanoidLegPoseSolver.TrySolve(
                    animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                    animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), right,
                    leftSupport + Vector3.right * 0.01f, Vector3.right, out float rightError), Is.True);
                Assert.That(rightError, Is.LessThan(0.0001f));
                left.rotation = Quaternion.AngleAxis(15f, left.right) * left.rotation;
                right.rotation = Quaternion.AngleAxis(15f, right.right) * right.rotation;
                float supportHeight = left.position.y;
                Quaternion supportRotation = left.rotation;
                Transform[] transforms = animator.transform.root.GetComponentsInChildren<Transform>(true);
                var positions = new Vector3[transforms.Length];
                var rotations = new Quaternion[transforms.Length];
                for (int index = 0; index < transforms.Length; index++)
                {
                    positions[index] = transforms[index].localPosition;
                    rotations[index] = transforms[index].localRotation;
                }
                int lastPin = -1;
                for (int frame = 0; frame < FrameCount; frame++)
                    if (driver.DebugLeftPinArray[frame]) lastPin = frame;
                Assert.That(lastPin, Is.GreaterThan(0));

                var weights = new List<float>();
                var goalHeights = new List<float>();
                var effectiveHeights = new List<float>();
                var footHeights = new List<float>();
                var footRotations = new List<Quaternion>();
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                object leftDrive = typeof(HumanoidFootGoalDriver).GetField("_left", flags).GetValue(driver);
                System.Reflection.PropertyInfo effectiveGoalProperty = leftDrive.GetType().GetProperty(
                    "EffectiveGoal", flags);
                float fullHeightRange = 0f, fullRotationRange = 0f;
                bool hasPartial = false, hasFull = false, hasReleasedZero = false;
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    // 매 프레임 같은 상위 지지 자세를 입력해 출력 누적으로 분리가 사라지지 않게 함.
                    for (int index = 0; index < transforms.Length; index++)
                    {
                        transforms[index].localPosition = positions[index];
                        transforms[index].localRotation = rotations[index];
                    }
                    driver.CaptureValidatedSupport(true, true);
                    Assert.That(driver.TryApply(frame / FrameRate, 1f,
                        Vector3.right, Vector3.right), Is.True);
                    Assert.That(driver.DebugSeparationWeight, Is.EqualTo(1f));
                    float weight = driver.DebugLeftWeight;
                    hasPartial |= weight > 0f && weight < 1f;
                    hasFull |= weight == 1f;
                    hasReleasedZero |= frame > lastPin && weight == 0f;
                    if (weight == 1f)
                    {
                        fullHeightRange = Mathf.Max(fullHeightRange,
                            Mathf.Abs(driver.DebugLeftSolveGoal.y - supportHeight));
                        fullRotationRange = Mathf.Max(fullRotationRange,
                            Quaternion.Angle(left.rotation, supportRotation));
                    }
                    weights.Add(weight);
                    goalHeights.Add(driver.DebugLeftSolveGoal.y);
                    effectiveHeights.Add(((Vector3)effectiveGoalProperty.GetValue(leftDrive)).y);
                    footHeights.Add(left.position.y);
                    footRotations.Add(left.rotation);
                }
                Assert.That(hasPartial && hasFull && hasReleasedZero, Is.True,
                    "실제 핀 진입·완전 적용·해제 경계가 모두 있어야 합니다.");
                Assert.That(fullHeightRange, Is.GreaterThan(0.03f));
                Assert.That(fullRotationRange, Is.GreaterThan(1f));
                for (int frame = 1; frame < FrameCount; frame++)
                {
                    float weightStep = Mathf.Abs(weights[frame] - weights[frame - 1]);
                    Assert.That(Mathf.Abs(goalHeights[frame] - goalHeights[frame - 1]),
                        Is.LessThanOrEqualTo(fullHeightRange * weightStep + 0.00001f),
                        $"프레임{frame}: 작은 핀 가중치가 전체 수직 영향을 활성화하면 안 됩니다.");
                    Assert.That(Mathf.Abs(effectiveHeights[frame] - effectiveHeights[frame - 1]),
                        Is.LessThanOrEqualTo(fullHeightRange * weightStep + 0.00001f));
                    // 실제 발 한도는 이 고정 입력의 회귀 기준이며 일반 IK의 선형성을 가정하지 않음.
                    Assert.That(Mathf.Abs(footHeights[frame] - footHeights[frame - 1]),
                        Is.LessThanOrEqualTo(fullHeightRange * weightStep + 0.0001f));
                    Assert.That(Quaternion.Angle(footRotations[frame], footRotations[frame - 1]),
                        Is.LessThanOrEqualTo(fullRotationRange * weightStep + 0.05f),
                        $"프레임{frame}: 고정 입력의 발 회전이 핀 전환에서 급변하면 안 됩니다.");
                }
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
        }


        [Test]
        public void Given_SeparatedFeetOnUnpinnedFrame_When_Applying_Then_NoSeparationClaim()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                // 정상 간격의 기본 자세는 복구를 주장하지 않아야 함 — 오탐 방지.
                Assert.That(driver.TryApply(0f, 1f, Vector3.right, Vector3.right), Is.True);
                Assert.That(driver.DebugSeparationWeight, Is.EqualTo(0f));
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
        public void Given_ReleasingSupport_When_ReevaluatingAndSeeking_Then_ReproducesSameHistory()
        {
            Animator animator = LoadTargetAnimator();
            Assert.That(animator, Is.Not.Null);
            try
            {
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                Transform[] bones = animator.GetComponentsInChildren<Transform>(true);
                Vector3[] positions = System.Array.ConvertAll(bones, bone => bone.localPosition);
                Quaternion[] rotations = System.Array.ConvertAll(bones, bone => bone.localRotation);
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                object leg = typeof(HumanoidFootGoalDriver).GetField("_left", flags).GetValue(driver);
                float ReadSupport() => (float)leg.GetType().GetProperty("SupportWeight", flags).GetValue(leg);
                Vector3 ReadGoal() => (Vector3)leg.GetType().GetProperty("EffectiveGoal", flags).GetValue(leg);
                void Apply(int frame, bool supported)
                {
                    for (int index = 0; index < bones.Length; index++)
                    {
                        bones[index].localPosition = positions[index];
                        bones[index].localRotation = rotations[index];
                    }
                    driver.CaptureValidatedSupport(supported, false);
                    Assert.That(driver.TryApply(frame / FrameRate, 1f, Vector3.right, Vector3.right), Is.True);
                }
                Apply(20, true);
                Assert.That(ReadSupport(), Is.EqualTo(1f));
                Apply(21, false);
                float firstSupport = ReadSupport();
                Vector3 firstGoal = ReadGoal();
                Assert.That(firstSupport, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(driver.DebugLeftWeight, Is.GreaterThan(0f));
                Assert.That(driver.DebugSeparationWeight, Is.Zero);
                Vector3[] firstPositions = System.Array.ConvertAll(bones, bone => bone.position);
                Quaternion[] firstRotations = System.Array.ConvertAll(bones, bone => bone.rotation);
                void AssertSameHistory()
                {
                    Assert.That(ReadSupport(), Is.EqualTo(firstSupport));
                    Assert.That(Vector3.Distance(ReadGoal(), firstGoal), Is.LessThan(0.000001f));
                    for (int index = 0; index < bones.Length; index++)
                    {
                        Assert.That(Vector3.Distance(bones[index].position, firstPositions[index]),
                            Is.LessThan(0.000001f), bones[index].name);
                        Assert.That(Quaternion.Angle(bones[index].rotation, firstRotations[index]),
                            Is.LessThan(0.05f), bones[index].name);
                    }
                }
                Apply(21, false);
                AssertSameHistory();
                Apply(50, false);
                Assert.That(ReadSupport(), Is.Zero);
                Apply(20, true);
                Assert.That(ReadSupport(), Is.EqualTo(1f));
                Apply(21, false);
                AssertSameHistory();
            }
            finally { Object.DestroyImmediate(animator.transform.root.gameObject); }
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
                // 생산 경로는 매 프레임 원본 자세를 다시 평가하므로 전체 본 자세를 복원함 —
                // 발 위치만 되돌리면 이전 해의 허벅지·종아리 회전이 남아 재현 조건이 달라짐.
                Transform[] bones = animator.GetComponentsInChildren<Transform>(true);
                Vector3[] positions = System.Array.ConvertAll(bones, bone => bone.localPosition);
                Quaternion[] rotations = System.Array.ConvertAll(bones, bone => bone.localRotation);
                void RestorePose()
                {
                    for (int index = 0; index < bones.Length; index++)
                    {
                        bones[index].localPosition = positions[index];
                        bones[index].localRotation = rotations[index];
                    }
                }
                driver.TryApply(20f / FrameRate, 1f, Vector3.right, Vector3.right);
                Vector3 first = foot.position;
                RestorePose();
                driver.TryApply(50f / FrameRate, 1f, Vector3.right, Vector3.right);
                RestorePose();
                driver.TryApply(20f / FrameRate, 1f, Vector3.right, Vector3.right);
                Assert.That(Vector3.Distance(foot.position, first), Is.LessThan(0.000001f),
                    "탐색 점프 후 같은 프레임은 같은 결과를 재현해야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_DriftingGoalDuringPin_When_SeekingForward_Then_FootStaysAtPinEntryAnchor()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildDriftingGoalSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                // pin 구간을 순차 진행해 weight가 수렴한 뒤 프레임 간 발 수평 이동이
                // 없어야 함 — 목표는 프레임당 5mm씩 계속 밀리는 입력임.
                Transform[] bones = animator.GetComponentsInChildren<Transform>(true);
                Vector3[] positions = System.Array.ConvertAll(bones, bone => bone.localPosition);
                Quaternion[] rotations = System.Array.ConvertAll(bones, bone => bone.localRotation);
                void RestorePose()
                {
                    for (int index = 0; index < bones.Length; index++)
                    {
                        bones[index].localPosition = positions[index];
                        bones[index].localRotation = rotations[index];
                    }
                }
                Vector3 late = Vector3.zero, last = Vector3.zero;
                for (int frame = PinStart; frame < PinEnd; frame++)
                {
                    RestorePose();
                    driver.TryApply(frame / FrameRate, 1f, Vector3.right, Vector3.right);
                    if (frame == PinEnd - 10) late = foot.position;
                    if (frame == PinEnd - 1) last = foot.position;
                }
                Vector2 lateXZ = new Vector2(late.x, late.z);
                Vector2 lastXZ = new Vector2(last.x, last.z);
                Assert.That(Vector2.Distance(lateXZ, lastXZ), Is.LessThan(0.001f),
                    "핀 구간의 발은 목표 드리프트를 따라가지 않고 진입 앵커에 정박해야 함");
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
            var hips = new GameObject("골반").transform;
            try
            {
                hips.SetPositionAndRotation(new Vector3(3f, 1f, -2f),
                    Quaternion.Euler(10f, 80f, -5f));
                var rotated = new HumanoidFootGoalStream.GoalSample
                {
                    RootSpaceFootRotation = Quaternion.Euler(15f, -30f, 45f),
                };
                Quaternion frame = HumanoidFootGoalDriver.GoalFrameToWorld(
                    rotated, hips.rotation);
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

            // Unity Euler 관례에서 +x 회전이 발끝을 아래로 기울임 — 과도한 하향 피치는 +80°.
            Quaternion steep = Quaternion.AngleAxis(80f, Vector3.right);
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
        public void Given_VerticalDownToe_When_SoftCapping_Then_StillCapsDegenerateAxis()
        {
            // 발끝이 지면 법선과 평행하면 회전축 cross가 0이 됨 — 그래도 cap이 적용돼야 함.
            Vector3 normal = Vector3.up;
            Quaternion vertical = Quaternion.AngleAxis(90f, Vector3.right);
            Quaternion capped = HumanoidFootGoalDriver.ApplyToePitchSoftCap(
                vertical, normal, 1f);
            float pitch = Mathf.Asin(Mathf.Clamp(
                Vector3.Dot(capped * Vector3.forward, normal), -1f, 1f)) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.GreaterThanOrEqualTo(
                -HumanoidFootGoalDriver.MaximumSupportedToePitch - 0.01f),
                "수직 하향 발끝도 지지 한계 안으로 압축돼야 함");
            Assert.That(pitch, Is.LessThan(-HumanoidFootGoalDriver.ToePitchSoftKneeDeg));
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

        [Test]
        public void Given_StraightenedLeg_When_ReapplyingPinnedFrame_Then_UsesStableBendNormal()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                Transform[] bones = animator.GetComponentsInChildren<Transform>(true);
                Vector3[] positions = System.Array.ConvertAll(bones, bone => bone.localPosition);
                Quaternion[] rotations = System.Array.ConvertAll(bones, bone => bone.localRotation);
                for (int index = 0; index < bones.Length; index++)
                {
                    bones[index].localPosition = positions[index];
                    bones[index].localRotation = rotations[index];
                }

                // 다리를 거의 펴서 현재 자세 cross를 퇴화시킴 — 퇴화 입력은
                // 잡음성 소벡터라 그대로 쓰면 무릎이 반대쪽 원 위에 배치됨.
                Vector3 upperDirection =
                    (lower.position - upper.position).normalized;
                Quaternion straighten = Quaternion.FromToRotation(
                    (foot.position - lower.position).normalized, upperDirection);
                lower.rotation = straighten * lower.rotation;
                Assert.That(Vector3.Cross(lower.position - upper.position,
                        foot.position - lower.position).sqrMagnitude,
                    Is.LessThan(0.0001f), "검증 전제 — 다리가 거의 펴져야 함");

                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.zero, Vector3.zero), Is.True);
                Vector3 usedNormal = driver.LastUsedLeftBendNormal;
                // 퇴화 입력 대신 안정된 법선(해부학 기준 정합 또는 캐시)으로 풀어야 함.
                Assert.That(usedNormal.sqrMagnitude, Is.GreaterThan(0.25f),
                    $"사용된 굽힘 법선이 퇴화하면 안 됨 usedN={usedNormal}");
                Assert.That(driver.DebugLeftSolveSucceeded, Is.True);
                Assert.That(driver.DebugLeftTargetError, Is.LessThan(0.005f),
                    $"발이 목표에 도달해야 함 err={driver.DebugLeftTargetError}");
                // 무릎이 사용된 법선과 같은 쪽에 배치돼야 함 — 반대쪽이면 과신전.
                Vector3 postCross = Vector3.Cross(
                    lower.position - upper.position,
                    foot.position - lower.position);
                Assert.That(Vector3.Dot(postCross.normalized, usedNormal.normalized),
                    Is.GreaterThan(0.5f),
                    $"무릎이 법선 반대쪽에 배치되면 안 됨\n" +
                    $"  usedN={usedNormal} postCross={postCross}\n" +
                    $"  knee={lower.position} inputs[{driver.DebugLeftSolveInputs}]");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_HyperextendedPose_When_ApplyingPinnedFrame_Then_KneeStaysOnAnatomicalSide()
        {
            // Phase 6c 회귀 — 자세 cross가 반대쪽을 가리키는(과신전) 입력에서도
            // solve가 무릎을 해부학 기준 쪽에 배치해야 함. 캡처에서 -79.7°로 관측됨.
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                // IK solve로 무릎을 해부학 기준과 반대쪽에 배치해 과신전 자세를 만듦 —
                // 회전만으로는 발만 움직여 무릎 자체는 반대쪽으로 가지 않음.
                Vector3 anatomicalCross = Vector3.Cross(
                    lower.position - upper.position,
                    foot.position - lower.position);
                Assert.That(anatomicalCross.sqrMagnitude, Is.GreaterThan(0.0001f),
                    "검증 전제 — 초기 자세는 무릎이 구부러져 있어야 함");
                Assert.That(HumanoidLegPoseSolver.TrySolve(upper, lower, foot,
                    foot.position, -anatomicalCross, out _), Is.True,
                    "과신전 자세 구성은 성공해야 함");
                Vector3 flippedCross = Vector3.Cross(
                    lower.position - upper.position,
                    foot.position - lower.position);
                Assert.That(Vector3.Dot(flippedCross, anatomicalCross), Is.LessThan(0f),
                    "검증 전제 — 무릎이 반대쪽으로 넘어가야 함");

                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    flippedCross, flippedCross), Is.True);
                Assert.That(driver.DebugLeftSolveSucceeded, Is.True);
                Vector3 postCross = Vector3.Cross(
                    lower.position - upper.position,
                    foot.position - lower.position);
                // 뒤집힌 입력은 사용 법선과 결과 무릎 모두에서 반대로 되돌려져야 함 —
                // 이전 코드는 뒤집힌 법선을 그대로 캐시해 무릎이 과신전을 유지했음.
                Assert.That(Vector3.Dot(postCross, flippedCross), Is.LessThan(0f),
                    $"solve가 뒤집힌 무릎을 되돌려야 함\n" +
                    $"  usedN={driver.LastUsedLeftBendNormal} postCross={postCross}\n" +
                    $"  flipped={flippedCross} knee={lower.position}");
                Assert.That(Vector3.Dot(driver.LastUsedLeftBendNormal, flippedCross),
                    Is.LessThan(0f),
                    $"사용 법선이 뒤집힌 입력을 따르면 안 됨 " +
                    $"usedN={driver.LastUsedLeftBendNormal} flipped={flippedCross}");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_SoleProbeBelowFloor_When_ApplyingPinnedFrame_Then_LiftsAnkleToKeepSoleAbove()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                // 발끝 본보다 5cm 아래 밑창 정점 — 발끝 스냅만으로는 이 정점이 지면 아래로 감.
                Vector3 probeLocal = leftFoot.InverseTransformPoint(
                    leftToes.position + new Vector3(0f, -0.05f, 0f));
                driver.SetSoleProbes(true, new[] { probeLocal });
                // 지면 추정값은 적용 전 발끝 높이 — 적용 후 발끝은 lift만큼 올라가 있음.
                float floorY = leftToes.position.y;

                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                float probeY = leftFoot.TransformPoint(probeLocal).y;
                Assert.That(probeY, Is.GreaterThanOrEqualTo(floorY - 0.005f),
                    "밑창 정점이 지면 아래로 파고들면 안 됨");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void Given_SourceToeFloorAboveCollider_When_ApplyingPinnedFrame_Then_UsesActualSoleFloor(
            bool useGround, bool hasGround)
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            GameObject floor = null;
            try
            {
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform toes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                float floorY = toes.position.y - 0.02f;
                floor = new GameObject("실제 지면 높이 검증");
                floor.transform.position = new Vector3(foot.position.x, floorY - 0.05f, foot.position.z);
                BoxCollider collider = floor.AddComponent<BoxCollider>();
                collider.size = new Vector3(4f, 0.1f, 4f);
                Physics.SyncTransforms();
                var ground = new HumanoidGroundResponse();
                ground.Initialize(animator);
                Assert.That(ground.TryFindGround(foot.position, out RaycastHit hit), Is.True);
                Assert.That(hit.collider, Is.SameAs(collider), "검증용 실제 지면을 선택해야 함");
                collider.enabled = hasGround;
                Physics.SyncTransforms();
                Assert.That(ground.TryFindGround(foot.position, out _), Is.EqualTo(hasGround));

                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);
                Vector3 probeLocal = foot.InverseTransformPoint(toes.position - Vector3.up * 0.05f);
                driver.SetSoleProbes(true, new[] { probeLocal });
                float expectedFloorY = useGround && hasGround ? floorY : toes.position.y;
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right, useGround ? ground : null), Is.True);
                Assert.That(foot.TransformPoint(probeLocal).y,
                    Is.EqualTo(expectedFloorY).Within(0.005f),
                    "실제 지면을 선택하고 비활성·미검출은 기존 높이를 유지해야 함");
            }
            finally
            {
                if (floor != null) Object.DestroyImmediate(floor);
                if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject);
            }
        }

        [Test]
        public void Given_PitchedGoalOnGroundedFrame_When_Applying_Then_AlignsSoleTowardGround()
        {
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);

                // pin 구간 목표 발 회전을 20° 기울임 — 평지 정렬이 동작하면
                // 적용 회전은 목표보다 수평에 가까워져야 함.
                Quaternion pitched = Quaternion.AngleAxis(20f, leftFoot.right) *
                    leftFoot.rotation;
                var samples = new HumanoidFootContactSample[FrameCount];
                for (int frame = 0; frame < FrameCount; frame++)
                {
                    samples[frame] = new HumanoidFootContactSample(
                        leftFoot.position + LeftGoalOffset,
                        leftToes.position + LeftGoalOffset,
                        rightFoot.position, rightToes.position,
                        null, pitched, rightFoot.rotation);
                }
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator, samples,
                    BuildHipsPositions(animator), BuildHipsRotations(animator),
                    BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Vector3 forward = leftFoot.rotation * Vector3.forward;
                float vertical = Vector3.Dot(forward, Vector3.up);
                float horizontal = (forward - Vector3.up * vertical).magnitude;
                float appliedPitch = Mathf.Abs(
                    Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg);
                Assert.That(appliedPitch, Is.LessThan(15f),
                    "지지 구간의 기운 목표 회전은 밑창 정렬로 수평에 가까워져야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_PinReleased_When_WeightDecaysOut_Then_ReachAnchorInvalidated()
        {
            // Phase 6c 회귀 — 핀 해제 감쇠 중·weight 소실 뒤에도 reach 계산용 앵커가
            // 유효한 채 남으면, 수백 프레임 뒤 핀 재진입 시 골반 reach 목표가 오래된
            // 위치로 오염돼 단발 골반 드롭(hip jitter·침투)이 생김.
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildDriftingGoalSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                bool[] pins = driver.DebugLeftPinArray;
                int pinCount = 0;
                foreach (bool p in pins) { if (p) pinCount++; }
                Assert.That(pinCount, Is.GreaterThan(0),
                    "드리프트 샘플에서 핀 구간이 확보돼야 회귀 검증이 성립함");

                // 불변식 — 적용 직후 reach 앵커 유효성은 그 프레임의 pin과 정확히
                // 일치해야 함. 해제 감쇠·weight 소실·재진입 어디서도 stale 앵커가
                // reach 계산으로 새면 안 됨(단발 골반 드롭 회귀).
                bool sawReleaseDecay = false;
                for (int frame = 0; frame < pins.Length; frame++)
                {
                    driver.TryApply(frame / FrameRate, 1f, Vector3.right, Vector3.right);
                    Assert.That(driver.DebugLeftPinAnchorInReach,
                        Is.EqualTo(pins[frame]),
                        $"reach 앵커 유효성은 pin과 일치해야 함 " +
                        $"f={frame} pin={pins[frame]} w={driver.DebugLeftWeight:F3}");
                    if (!pins[frame] && driver.DebugLeftWeight > 0f)
                    {
                        sawReleaseDecay = true;
                    }
                }
                Assert.That(sawReleaseDecay, Is.True, "해제 감쇠 구간 표본이 필요함");
                Assert.That(driver.DebugLeftWeight, Is.EqualTo(0f),
                    "핀 종료 뒤 weight는 0으로 수렴해야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_PinReleaseWithDriftedGoal_When_ApplyingDecay_Then_GoalDoesNotJump()
        {
            // Phase 6c 회귀 — 핀 해제 시 앵커(핀 시작 목표)와 원본 목표의 갭이 한
            // 프레임에 노출되면 solve 목표가 수십mm 점프해 잔차 슬라이드 위반이 됨.
            // 감쇠 중 목표는 weight 비례로 앵커에서 원본으로 이행해야 함.
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildDriftingGoalSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                bool[] pins = driver.DebugLeftPinArray;
                int lastPin = -1;
                for (int index = 0; index < pins.Length; index++)
                {
                    if (pins[index]) lastPin = index;
                }
                Assert.That(lastPin, Is.GreaterThan(0),
                    "드리프트 샘플에서 핀 구간이 확보돼야 회귀 검증이 성립함");

                // 핀 해제 직후 감쇠 구간 — solve 목표의 프레임간 수평 스텝은 갭
                // (~30mm)의 일부분이어야 하며 블렌드 없이는 갭 전체가 한 번에 점프함.
                Vector3 previous = driver.DebugLeftSolveGoal;
                for (int frame = 0; frame < pins.Length; frame++)
                {
                    driver.TryApply(frame / FrameRate, 1f, Vector3.right, Vector3.right);
                    if (frame > lastPin && driver.DebugLeftWeight > 0f)
                    {
                        Vector3 goal = driver.DebugLeftSolveGoal;
                        float step = Vector2.Distance(
                            new Vector2(goal.x, goal.z),
                            new Vector2(previous.x, previous.z));
                        Assert.That(step, Is.LessThan(0.015f),
                            $"해제 감쇠 중 solve 목표가 갭 전체로 점프하면 안 됨 " +
                            $"f={frame} step={step * 1000f:F1}mm");
                        previous = goal;
                    }
                    else if (frame <= lastPin && driver.DebugLeftWeight > 0f)
                    {
                        previous = driver.DebugLeftSolveGoal;
                    }
                }
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_PinnedAndReleasing_When_ReapplyingSameTime_Then_StateIsIdempotent()
        {
            // 격리 리뷰 회귀 — 같은 시각 재평가(Seek 동일 시각·자세 복원 재적용)에서
            // weight 스냅+핀 상태 파괴를 하면 재평가마다 앵커가 라이브 목표로
            // 리베이스되거나 감쇠가 영구 중단돼 발이 점프함. 경과 시간이 없는
            // 재평가는 상태를 유지해 멱등 재적용이 되어야 함.
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildDriftingGoalSamples(animator), BuildHipsPositions(animator),
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                bool[] pins = driver.DebugLeftPinArray;
                int lastPin = -1;
                for (int index = 0; index < pins.Length; index++)
                {
                    if (pins[index]) lastPin = index;
                }
                Assert.That(lastPin, Is.GreaterThan(0),
                    "드리프트 샘플에서 핀 구간이 확보돼야 회귀 검증이 성립함");

                // 핀 프레임의 같은 시각 재평가 — weight·앵커·solve 목표가 그대로여야 함.
                int pinFrame = lastPin;
                for (int frame = 0; frame <= pinFrame; frame++)
                {
                    driver.TryApply(frame / FrameRate, 1f, Vector3.right, Vector3.right);
                }
                float weightBefore = driver.DebugLeftWeight;
                Vector3 goalBefore = driver.DebugLeftSolveGoal;
                driver.TryApply(pinFrame / FrameRate, 1f, Vector3.right, Vector3.right);
                Assert.That(driver.DebugLeftWeight, Is.EqualTo(weightBefore),
                    "같은 시각 재평가에서 weight가 스냅되면 안 됨");
                Assert.That(driver.DebugLeftPinAnchorInReach, Is.True,
                    "같은 시각 재평가에서 핀 앵커가 파괴되면 안 됨");
                Assert.That(Vector3.Distance(driver.DebugLeftSolveGoal, goalBefore),
                    Is.LessThan(0.001f), "같은 시각 재평가는 solve 목표도 동일해야 함");

                // 해제 감쇠 중 재평가 — weight가 0으로 스냅돼 블렌드가 중단되면 안 됨.
                int decayFrame = -1;
                for (int frame = pinFrame + 1; frame < pins.Length; frame++)
                {
                    driver.TryApply(frame / FrameRate, 1f, Vector3.right, Vector3.right);
                    if (!pins[frame] && driver.DebugLeftWeight > 0.05f &&
                        driver.DebugLeftWeight < 0.95f)
                    {
                        decayFrame = frame;
                        break;
                    }
                }
                Assert.That(decayFrame, Is.GreaterThan(0), "감쇠 중간 표본이 필요함");
                float decayWeight = driver.DebugLeftWeight;
                Vector3 decayGoal = driver.DebugLeftSolveGoal;
                driver.TryApply(decayFrame / FrameRate, 1f, Vector3.right, Vector3.right);
                Assert.That(driver.DebugLeftWeight, Is.EqualTo(decayWeight),
                    "감쇠 중 같은 시각 재평가에서 weight가 0으로 스냅되면 안 됨");
                Assert.That(Vector3.Distance(driver.DebugLeftSolveGoal, decayGoal),
                    Is.LessThan(0.001f), "감쇠 중 재평가의 solve 목표도 동일해야 함");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
        }

        [Test]
        public void Given_SourceHipsBelowTarget_When_Applying_Then_GoalMatchesSourceFootWorld()
        {
            // Phase 6c 회귀 — 소스/타겟 골반 높이가 다른 모델에서 타겟 골반으로
            // 목표를 복원하면 목표가 그 차이만큼 뜸(실측 +20cm). 소스 골반으로
            // 복원해 원본 월드 발 위치가 정확히 나와야 함.
            Animator animator = LoadTargetAnimator();
            if (animator == null) Assert.Ignore("로컬 검증 모델이 없는 환경에서는 생략함");
            try
            {
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                // 소스 골반이 타겟보다 0.25 낮은 상태 — 실측 비율(1.03 vs 0.77)과 같은 규모.
                Vector3[] hipsPositions = BuildHipsPositions(animator);
                for (int index = 0; index < hipsPositions.Length; index++)
                {
                    hipsPositions[index] += Vector3.down * 0.25f;
                }
                var driver = new HumanoidFootGoalDriver();
                Assert.That(driver.TryInitializeCore(animator,
                    BuildSamples(animator), hipsPositions,
                    BuildHipsRotations(animator), BuildPlantEstimate(animator),
                    HumanoidFootContactIntentLabelSet.Empty,
                    animator.humanScale, FrameRate), Is.True);

                // 적용 전에 기대 목표를 고정함 — solve 후 발은 이미 목표 위치에 있음.
                Vector3 expectedGoal = leftFoot.position + LeftGoalOffset;
                Assert.That(driver.TryApply(20f / FrameRate, 1f,
                    Vector3.right, Vector3.right), Is.True);
                Assert.That(Vector3.Distance(leftFoot.position, expectedGoal),
                    Is.LessThan(0.02f),
                    $"목표는 원본 월드 발 위치로 복원돼야 함 " +
                    $"foot={leftFoot.position} expected={expectedGoal} " +
                    $"err={driver.DebugLeftTargetError}");
            }
            finally { if (animator != null) Object.DestroyImmediate(animator.transform.root.gameObject); }
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
            return BuildSamples(animator, LeftGoalOffset);
        }

        private static HumanoidFootContactSample[] BuildSamples(Animator animator,
            Vector3 leftGoalOffset)
        {
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            var samples = new HumanoidFootContactSample[FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                samples[frame] = new HumanoidFootContactSample(
                    leftFoot.position + leftGoalOffset,
                    leftToes.position + leftGoalOffset,
                    rightFoot.position, rightToes.position,
                    null, leftFoot.rotation, rightFoot.rotation);
            }
            return samples;
        }

        // 핀 구간 동안 목표가 프레임당 1mm씩 수평으로 미끄러지는 궤적 — 정박 동작
        // 검증용. 발목 속도(=드리프트×프레임률)가 게이트 진입 한계(0.05×humanScale
        // ≈0.049m/s)를 넘으면 핀이 열리지 않으므로 드리프트는 1mm/프레임이어야 함.
        private static HumanoidFootContactSample[] BuildDriftingGoalSamples(Animator animator)
        {
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
            var samples = new HumanoidFootContactSample[FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                Vector3 offset = LeftGoalOffset + new Vector3(0.001f * frame, 0f, 0f);
                samples[frame] = new HumanoidFootContactSample(
                    leftFoot.position + offset,
                    leftToes.position + offset,
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
