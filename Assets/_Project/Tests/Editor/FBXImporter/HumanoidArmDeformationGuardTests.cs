using NUnit.Framework;
using UnityEngine;
using Fbx2Vmd.Modules.FBXImporter;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidArmDeformationGuardTests
    {
        // 손가락 muscle 이름 목록 (IsFingerMuscle과 동일 기준)
        private static readonly string[] FingerKeywords = { "thumb", "index", "middle", "ring", "little" };

        private static bool IsFingerMuscleName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Replace(" ", "").ToLowerInvariant();
            foreach (string kw in FingerKeywords)
                if (n.Contains(kw)) return true;
            return false;
        }

        private static int CountNonFingerMuscles()
        {
            int count = 0;
            int max = Mathf.Min(HumanTrait.MuscleCount, HumanTrait.MuscleName.Length);
            for (int i = 0; i < max; i++)
                if (!IsFingerMuscleName(HumanTrait.MuscleName[i])) count++;
            return count;
        }

        [Test]
        public void Given_MuscleValuesExceedRange_When_ClampMusclesToHumanRange_Then_AllNonFingerMusclesWithinNeg1To1()
        {
            var pose = new HumanPose();
            pose.muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < pose.muscles.Length; i++)
                pose.muscles[i] = 2.0f;

            HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);

            int max = Mathf.Min(pose.muscles.Length, HumanTrait.MuscleCount);
            for (int i = 0; i < max; i++)
            {
                if (IsFingerMuscleName(HumanTrait.MuscleName[i])) continue;
                Assert.LessOrEqual(pose.muscles[i], 1f,
                    $"muscle[{i}] '{HumanTrait.MuscleName[i]}' must be <= 1");
                Assert.GreaterOrEqual(pose.muscles[i], -1f,
                    $"muscle[{i}] '{HumanTrait.MuscleName[i]}' must be >= -1");
            }
        }

        [Test]
        public void Given_MuscleValuesExceedRange_When_ClampMusclesToHumanRange_Then_ReturnCountEqualsClampedNonFingerMuscles()
        {
            var pose = new HumanPose();
            pose.muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < pose.muscles.Length; i++)
                pose.muscles[i] = 2.0f;

            int expectedCount = CountNonFingerMuscles();
            int actualCount = HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);

            Assert.AreEqual(expectedCount, actualCount,
                "클램핑된 muscle 수가 비손가락 muscle 수와 일치해야 한다");
        }

        [Test]
        public void Given_AllMusclesInRange_When_ClampMusclesToHumanRange_Then_NoneModifiedAndReturnsZero()
        {
            var pose = new HumanPose();
            pose.muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < pose.muscles.Length; i++)
                pose.muscles[i] = 0.5f;

            int clampedCount = HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);

            Assert.AreEqual(0, clampedCount, "범위 내 muscle은 수정되지 않아야 한다");
            for (int i = 0; i < pose.muscles.Length; i++)
                Assert.AreEqual(0.5f, pose.muscles[i], 1e-6f,
                    $"muscle[{i}] 값이 변경되지 않아야 한다");
        }

        [Test]
        public void Given_NullMuscles_When_ClampMusclesToHumanRange_Then_ReturnsZeroWithoutException()
        {
            var pose = new HumanPose();
            pose.muscles = null;

            int result = 0;
            Assert.DoesNotThrow(() =>
            {
                result = HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);
            });
            Assert.AreEqual(0, result, "muscles가 null이면 0을 반환해야 한다");
        }

        [Test]
        public void Given_BoundaryMuscleValues_When_ClampMusclesToHumanRange_Then_BoundaryValuesUnchanged()
        {
            var pose = new HumanPose();
            pose.muscles = new float[HumanTrait.MuscleCount];
            for (int i = 0; i < pose.muscles.Length; i++)
                pose.muscles[i] = (i % 2 == 0) ? 1.0f : -1.0f;

            int clampedCount = HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);

            Assert.AreEqual(0, clampedCount, "정확히 ±1.0인 경계값은 클램핑 대상이 아니다");
        }

        [Test]
        public void Given_MixedMuscleValues_When_ClampMusclesToHumanRange_Then_OnlyOutOfRangeMusclesModified()
        {
            var pose = new HumanPose();
            pose.muscles = new float[HumanTrait.MuscleCount];
            // 모두 범위 내로 초기화
            for (int i = 0; i < pose.muscles.Length; i++)
                pose.muscles[i] = 0.5f;

            // 손가락이 아닌 첫 번째 muscle을 범위 초과로 설정
            int targetIdx = -1;
            for (int i = 0; i < Mathf.Min(pose.muscles.Length, HumanTrait.MuscleCount); i++)
            {
                if (!IsFingerMuscleName(HumanTrait.MuscleName[i]))
                {
                    targetIdx = i;
                    break;
                }
            }
            Assume.That(targetIdx >= 0, "테스트 가능한 비손가락 muscle이 없다");
            pose.muscles[targetIdx] = 1.5f;

            int clampedCount = HumanoidArmDeformationGuard.ClampMusclesToHumanRange(ref pose);

            Assert.AreEqual(1, clampedCount, "범위를 벗어난 muscle 1개만 클램핑되어야 한다");
            Assert.AreEqual(1.0f, pose.muscles[targetIdx], 1e-6f,
                "클램핑된 muscle은 1.0으로 고정되어야 한다");
        }
    }
}
