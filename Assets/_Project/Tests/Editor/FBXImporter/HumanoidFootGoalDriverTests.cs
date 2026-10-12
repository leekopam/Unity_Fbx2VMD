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
