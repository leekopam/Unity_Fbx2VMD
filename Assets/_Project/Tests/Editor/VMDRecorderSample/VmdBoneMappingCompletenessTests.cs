using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

namespace Tests.Editor.VMDRecorderSample
{
    /// <summary>
    /// VmdHumanoidBoneMap의 완전성을 검증한다.
    /// BoneNames enum의 모든 값이 RecorderBonesByWriterName에 매핑되고
    /// HumanBonesByRecorderBone에도 포함되는지 확인한다.
    /// </summary>
    public class VmdBoneMappingCompletenessTests
    {
        // VmdHumanoidBoneMap의 정적 필드/메서드 접근을 위한 reflection
        private static readonly Type BoneMapType = typeof(VmdHumanoidBoneMap);

        /// <summary>
        /// BoneNames enum 값 중 None을 제외한 모든 값이 writer name → recorder bone 매핑에 존재해야 한다.
        /// </summary>
        [Test]
        public void EveryBoneNamesValue_HasWriterNameMapping()
        {
            BoneNames[] allBones = Enum.GetValues(typeof(BoneNames))
                .Cast<BoneNames>()
                .Where(b => b != BoneNames.None)
                .ToArray();

            // RecorderBonesByWriterName은 internal static IReadOnlyDictionary
            var recorderBonesField = BoneMapType.GetField(
                "RecorderBonesByWriterName",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(recorderBonesField, Is.Not.Null,
                "RecorderBonesByWriterName 필드가 존재해야 한다.");

            var recorderBones = recorderBonesField.GetValue(null)
                as IReadOnlyDictionary<string, BoneNames>;
            Assert.That(recorderBones, Is.Not.Null,
                "RecorderBonesByWriterName이 null이 아니어야 한다.");

            var mappedValues = new HashSet<BoneNames>(recorderBones.Values);

            foreach (BoneNames bone in allBones)
            {
                Assert.That(mappedValues.Contains(bone), Is.True,
                    $"BoneNames.{bone}이 writer name 매핑에 포함되어야 한다.");
            }
        }

        /// <summary>
        /// RecorderBonesByWriterName의 모든 값이 HumanBonesByRecorderBone에 매핑되는지 교차 검증한다.
        /// 일부 본(全ての親 등)은 HumanBodyBones에 해당하지 않을 수 있지만,
        /// 나머지는 명시적 매핑이 필요하다.
        /// </summary>
        [Test]
        public void RecorderBonesByWriterName_CrossReferencesHumanBonesByRecorderBone()
        {
            var recorderBonesField = BoneMapType.GetField(
                "RecorderBonesByWriterName",
                BindingFlags.NonPublic | BindingFlags.Static);
            var humanBonesField = BoneMapType.GetField(
                "HumanBonesByRecorderBone",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assume.That(recorderBonesField, Is.Not.Null);
            Assume.That(humanBonesField, Is.Not.Null);

            var recorderBones = recorderBonesField.GetValue(null)
                as IReadOnlyDictionary<string, BoneNames>;
            var humanBones = humanBonesField.GetValue(null)
                as IReadOnlyDictionary<BoneNames, HumanBodyBones>;

            Assume.That(recorderBones, Is.Not.Null);
            Assume.That(humanBones, Is.Not.Null);

            // HumanBonesByRecorderBone에 없는 값이 있으면 상세 출력
            var unmappedInHuman = new List<BoneNames>();
            foreach (var kvp in recorderBones)
            {
                if (humanBones.ContainsKey(kvp.Value) || kvp.Value == BoneNames.全ての親)
                {
                    continue;
                }

                unmappedInHuman.Add(kvp.Value);
            }

            Assert.That(unmappedInHuman, Is.Empty,
                "Writer name에 매핑된 모든 RecorderBone은 HumanBonesByRecorderBone에도 있거나 " +
                "全ての親처럼 명시적 제외 대상이어야 한다. 누락: " +
                string.Join(", ", unmappedInHuman));
        }

        /// <summary>
        /// HumanBodyBones enum과 BoneNames 간 cardinality 비교.
        /// HumanBodyBones는 Unity 휴머노이드 본 구조를 반영하므로
        /// BoneNames 매핑이 HumanBodyBones의 주요 본들을 포함하는지 확인한다.
        /// </summary>
        [Test]
        public void EssentialHumanBodyBones_HaveBoneNamesMapping()
        {
            var humanBonesField = BoneMapType.GetField(
                "HumanBonesByRecorderBone",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(humanBonesField, Is.Not.Null);

            var humanBones = humanBonesField.GetValue(null)
                as IReadOnlyDictionary<BoneNames, HumanBodyBones>;
            Assume.That(humanBones, Is.Not.Null);

            // MMD에서 사용하는 필수 HumanBodyBones 목록
            HumanBodyBones[] essentialBones =
            {
                HumanBodyBones.Hips,
                HumanBodyBones.Spine,
                HumanBodyBones.Chest,
                HumanBodyBones.Neck,
                HumanBodyBones.Head,
                HumanBodyBones.LeftShoulder,
                HumanBodyBones.RightShoulder,
                HumanBodyBones.LeftUpperArm,
                HumanBodyBones.RightUpperArm,
                HumanBodyBones.LeftLowerArm,
                HumanBodyBones.RightLowerArm,
                HumanBodyBones.LeftHand,
                HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg,
                HumanBodyBones.RightUpperLeg,
                HumanBodyBones.LeftLowerLeg,
                HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftFoot,
                HumanBodyBones.RightFoot,
                HumanBodyBones.LeftToes,
                HumanBodyBones.RightToes,
            };

            var mappedBones = new HashSet<HumanBodyBones>(humanBones.Values);

            foreach (HumanBodyBones essential in essentialBones)
            {
                Assert.That(mappedBones.Contains(essential), Is.True,
                    $"HumanBodyBones.{essential}가 BoneNames 매핑에 포함되어야 한다.");
            }
        }

        /// <summary>
        /// TryResolveWriterBoneName이 non-None BoneNames에 대해
        /// 기본 매개변수로 resolve 되었을 때 HumanBodyBones 매핑도 함께 반환하는지 확인한다.
        /// </summary>
        [Test]
        public void TryResolveWriterBoneName_ForMainBones_ReturnsHumanBodyBone()
        {
            var tryResolve = BoneMapType.GetMethod(
                "TryResolveWriterBoneName",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string), typeof(VmdHumanoidBoneBinding).MakeByRefType() },
                null);
            Assume.That(tryResolve, Is.Not.Null,
                "TryResolveWriterBoneName(string, out VmdHumanoidBoneBinding)이 존재해야 한다.");

            // 대표 본 5개를 검증: HasHumanBodyBone이 true임을 확인
            string[] bonesToCheck = { "上半身", "左腕", "右足首", "左足ＩＫ", "右つま先ＩＫ" };
            foreach (string boneName in bonesToCheck)
            {
                object[] args = new object[] { boneName, null };
                bool resolved = (bool)tryResolve.Invoke(null, args);
                var binding = (VmdHumanoidBoneBinding)args[1];

                Assert.That(resolved, Is.True,
                    $"'{boneName}'이 resolve 되어야 한다.");

                if (boneName != "左足ＩＫ" && boneName != "右つま先ＩＫ")
                {
                    Assert.That(binding.HasHumanBodyBone, Is.True,
                        $"'{boneName}'에 HumanBodyBone 매핑이 있어야 한다.");
                }
            }
        }

        /// <summary>
        /// センター에 groove routing이 활성화되었을 때
        /// センター → 全ての親, グルーブ → センター로 분리되는지 확인한다.
        /// </summary>
        [Test]
        public void CenterToGrooveRouting_SeparatesParentAndCenter()
        {
            // 기본 resolve: "センター" → BoneNames.センター
            bool basicResolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                "センター",
                out VmdHumanoidBoneBinding basicBinding);

            Assert.That(basicResolved, Is.True);
            Assert.That(basicBinding.RecorderBoneName, Is.EqualTo(BoneNames.センター),
                "기본 설정에서 センター는 BoneNames.センター에 매핑되어야 한다.");

            // groove routing 활성화: "センター" → 全ての親, "グルーブ" → センター
            bool parentResolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                "センター",
                useCenterAsParentOfAll: true,
                routeCenterBoneToGroove: true,
                centerNameString: "センター",
                grooveNameString: "グルーブ",
                out VmdHumanoidBoneBinding parentBinding);

            bool grooveResolved = VmdHumanoidBoneMap.TryResolveWriterBoneName(
                "グルーブ",
                useCenterAsParentOfAll: true,
                routeCenterBoneToGroove: true,
                centerNameString: "センター",
                grooveNameString: "グルーブ",
                out VmdHumanoidBoneBinding grooveBinding);

            Assert.That(parentResolved, Is.True);
            Assert.That(grooveResolved, Is.True);
            Assert.That(parentBinding.RecorderBoneName, Is.EqualTo(BoneNames.全ての親),
                "groove routing 시 センター는 全ての親에 매핑되어야 한다.");
            Assert.That(grooveBinding.RecorderBoneName, Is.EqualTo(BoneNames.センター),
                "groove routing 시 グルーブ는 センター에 매핑되어야 한다.");
        }

        /// <summary>
        /// RecorderBonesByWriterName과 HumanBonesByRecorderBone 두 맵의
        /// 키 cardinality가 일치하는지 확인한다 (全ての親 제외).
        /// </summary>
        [Test]
        public void BoneMapDictionaries_HaveMatchingCardinality()
        {
            var recorderBonesField = BoneMapType.GetField(
                "RecorderBonesByWriterName",
                BindingFlags.NonPublic | BindingFlags.Static);
            var humanBonesField = BoneMapType.GetField(
                "HumanBonesByRecorderBone",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assume.That(recorderBonesField, Is.Not.Null);
            Assume.That(humanBonesField, Is.Not.Null);

            var recorderBones = recorderBonesField.GetValue(null)
                as IReadOnlyDictionary<string, BoneNames>;
            var humanBones = humanBonesField.GetValue(null)
                as IReadOnlyDictionary<BoneNames, HumanBodyBones>;

            Assume.That(recorderBones, Is.Not.Null);
            Assume.That(humanBones, Is.Not.Null);

            // 全ての親을 제외한 recorderBones count가 humanBones count와 일치해야 함
            int recorderCount = recorderBones.Values.Count(b => b != BoneNames.全ての親);
            int humanCount = humanBones.Count;

            Assert.That(recorderCount, Is.EqualTo(humanCount),
                $"Writer name 매핑된 본 수({recorderCount}, 全ての親 제외)와 " +
                $"HumanBodyBone 매핑 수({humanCount})가 일치해야 한다.");
        }
    }
}
