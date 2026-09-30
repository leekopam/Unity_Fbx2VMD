using System.Collections.Generic;
using Fbx2Vmd.ClothPhysics;
using NUnit.Framework;

namespace Tests.Editor.ClothPhysics
{
    public class HairPartColliderMapperTests
    {
        const float Torso = 0.5f; // 상반신 0.5m 기준

        [Test]
        public void Given_FrontHair_When_Mapping_Then_OnlyHeadAndNeck()
        {
            var cats = HairPartColliderMapper.Map(HairPart.Front, 0.1f, Torso);
            Assert.That(cats, Is.EqualTo(ColliderCategory.Head | ColliderCategory.Neck));
        }

        [Test]
        public void Given_ShortBackHair_When_Mapping_Then_UpperBodyWithoutHands()
        {
            var cats = HairPartColliderMapper.Map(HairPart.Back, 0.2f, Torso);
            Assert.That(cats.HasFlag(ColliderCategory.Head), Is.False);
            Assert.That(cats.HasFlag(ColliderCategory.Hands), Is.False);
            Assert.That(cats.HasFlag(ColliderCategory.Waist), Is.True);
        }

        [Test]
        public void Given_LongBackHair_When_Mapping_Then_IncludesLegsAndHands()
        {
            // 상반신 0.5 × 0.8 = 0.4m 이상 = 장발
            var cats = HairPartColliderMapper.Map(HairPart.Back, 0.6f, Torso);
            Assert.That(cats.HasFlag(ColliderCategory.Legs), Is.True);
            Assert.That(cats.HasFlag(ColliderCategory.Hands), Is.True);
            Assert.That(cats.HasFlag(ColliderCategory.Feet), Is.True);
        }

        [Test]
        public void Given_TwinTail_When_Mapping_Then_IncludesHands()
        {
            var cats = HairPartColliderMapper.Map(HairPart.Tail, 0.3f, Torso);
            Assert.That(cats.HasFlag(ColliderCategory.Hands), Is.True,
                "트윈테일은 짧아도 손·팔과 접촉하므로 Hands 포함");
        }

        [Test]
        public void Given_Selection_When_OverCap_Then_TruncatesAt32()
        {
            var byCat = new Dictionary<ColliderCategory, List<int>>();
            foreach (ColliderCategory c in System.Enum.GetValues(typeof(ColliderCategory)))
            {
                if (c == ColliderCategory.None || c == ColliderCategory.UpperBody || c == ColliderCategory.AllBody)
                    continue;
                var list = new List<int>();
                for (int i = 0; i < 8; i++)
                    list.Add((int)c * 100 + i);
                byCat[c] = list;
            }

            var selected = HairPartColliderMapper.Select(ColliderCategory.AllBody, byCat, 32);
            Assert.That(selected.Count, Is.EqualTo(32), "32개 제한을 넘으면 안 됩니다.");
            // 머리쪽이 우선 채워져야 함
            Assert.That(selected[0], Is.EqualTo((int)ColliderCategory.Head * 100));
        }

        [Test]
        public void Given_PartialCategories_When_Selecting_Then_OnlyThoseReturned()
        {
            var byCat = new Dictionary<ColliderCategory, List<int>>
            {
                { ColliderCategory.Head, new List<int> { 1 } },
                { ColliderCategory.Neck, new List<int> { 2 } },
                { ColliderCategory.Legs, new List<int> { 3 } },
            };
            var selected = HairPartColliderMapper.Select(
                ColliderCategory.Head | ColliderCategory.Neck, byCat);
            Assert.That(selected, Is.EquivalentTo(new[] { 1, 2 }));
        }
    }
}
