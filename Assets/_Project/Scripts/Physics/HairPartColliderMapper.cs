using System;
using System.Collections.Generic;

namespace Fbx2Vmd.ClothPhysics
{
    /// <summary>
    /// 자동 생성되는 신체 콜라이더의 부위 카테고리.
    /// </summary>
    [Flags]
    public enum ColliderCategory
    {
        None = 0,
        Head = 1 << 0,
        Neck = 1 << 1,
        Chest = 1 << 2,
        Waist = 1 << 3,
        Hips = 1 << 4,
        Shoulders = 1 << 5,
        Arms = 1 << 6,
        Hands = 1 << 7,
        Legs = 1 << 8,
        Feet = 1 << 9,

        UpperBody = Head | Neck | Chest | Waist | Shoulders,
        AllBody = Head | Neck | Chest | Waist | Hips | Shoulders | Arms | Hands | Legs | Feet,
    }

    /// <summary>
    /// 머리카락 부위와 체인 길이에 따라 관련 콜라이더만 선별한다.
    /// MC2의 Cloth당 32개 콜라이더 제한과 손·발 걸림 제어를 위한 순수 매핑.
    /// </summary>
    public static class HairPartColliderMapper
    {
        // 체인 길이가 이 비율 × 상반신 길이를 넘으면 "장발"로 간주
        const float LongChainRatio = 0.8f;

        /// <summary>
        /// 부위와 체인 길이로 필요한 콜라이더 카테고리를 반환한다.
        /// </summary>
        /// <param name="part">분류된 머리카락 부위</param>
        /// <param name="chainLength">체인 월드 길이(미터)</param>
        /// <param name="torsoLength">기준이 되는 상반신 길이(머리~골반, 미터). 0 이하면 길이 무시</param>
        public static ColliderCategory Map(HairPart part, float chainLength, float torsoLength)
        {
            bool isLong = torsoLength > 0f && chainLength >= torsoLength * LongChainRatio;

            switch (part)
            {
                case HairPart.Front:
                case HairPart.Ahoge:
                    // 앞머리는 머리·목만. 팔 콜라이더와의 불필요 상호작용 방지
                    return ColliderCategory.Head | ColliderCategory.Neck;

                case HairPart.Side:
                    return isLong
                        ? ColliderCategory.UpperBody | ColliderCategory.Arms | ColliderCategory.Hands | ColliderCategory.Hips
                        : ColliderCategory.Head | ColliderCategory.Neck | ColliderCategory.Chest | ColliderCategory.Shoulders | ColliderCategory.Arms;

                case HairPart.Back:
                    return isLong
                        ? ColliderCategory.AllBody & ~ColliderCategory.Head
                        : ColliderCategory.Neck | ColliderCategory.Chest | ColliderCategory.Waist | ColliderCategory.Shoulders | ColliderCategory.Arms;

                case HairPart.Tail:
                    // 트윈테일은 옆을 크게 흔들며 손·팔과 접촉 빈도가 높음
                    return isLong
                        ? ColliderCategory.AllBody
                        : ColliderCategory.Head | ColliderCategory.Neck | ColliderCategory.Chest | ColliderCategory.Shoulders | ColliderCategory.Arms | ColliderCategory.Hands;

                case HairPart.Accessory:
                    // 장식물은 장착 부위 근처만 — 호출자가 구체 범위 조정
                    return ColliderCategory.Head | ColliderCategory.Neck | ColliderCategory.Chest;

                default:
                    return ColliderCategory.UpperBody;
            }
        }

        /// <summary>
        /// 카테고리 목록에서 콜라이더를 선별한다. 32개 제한을 넘으면 상반신 우선으로 자른다.
        /// </summary>
        public static List<T> Select<T>(
            ColliderCategory categories,
            IReadOnlyDictionary<ColliderCategory, List<T>> collidersByCategory,
            int maxCount = 32)
        {
            var selected = new List<T>();
            // 우선순위 순으로 순회 (머리→발)
            var order = new[]
            {
                ColliderCategory.Head, ColliderCategory.Neck, ColliderCategory.Chest,
                ColliderCategory.Waist, ColliderCategory.Hips, ColliderCategory.Shoulders,
                ColliderCategory.Arms, ColliderCategory.Hands, ColliderCategory.Legs,
                ColliderCategory.Feet,
            };
            foreach (var cat in order)
            {
                if (!categories.HasFlag(cat))
                    continue;
                if (!collidersByCategory.TryGetValue(cat, out var list))
                    continue;
                foreach (var c in list)
                {
                    if (selected.Count >= maxCount)
                        return selected;
                    selected.Add(c);
                }
            }
            return selected;
        }
    }
}
