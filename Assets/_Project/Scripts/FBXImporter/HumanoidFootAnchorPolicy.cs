using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 프레임별 지지점 앵커 처리 방침임. 확정된 의도 구간에서만 핀·미끄럼을 부여함.
    /// </summary>
    internal enum HumanoidFootAnchorPolicy
    {
        // 의도 구간 밖 — 원본 수평 추종을 유지하되 프레임 이동 속도를 제한함.
        Free,
        // 확정된 Plant 구간 — 앵커를 구간 진입 위치에 고정함.
        Pinned,
        // 확정된 Slide 구간 — 앵커가 원본 수평 이동을 그대로 따라감.
        Sliding
    }

    /// <summary>
    /// 발별 접지 의도 구간을 프레임 단위 앵커 방침과 보정 가중치로 변환함.
    /// </summary>
    internal static class HumanoidFootAnchorPolicyResolver
    {
        // 진입은 이 프레임 수 안에 완전 잠금하고 이탈은 기존 해제 곡선만 사용해 비대칭 전이를 구현함.
        internal const int TouchdownLockFrames = 2;

        internal static HumanoidFootAnchorPolicy[] Rasterize(
            IReadOnlyList<HumanoidFootContactIntent> intents, int frameCount)
        {
            var policies = new HumanoidFootAnchorPolicy[frameCount];
            if (intents == null) return policies;
            foreach (HumanoidFootContactIntent intent in intents)
            {
                if (intent == null ||
                    intent.Certainty != HumanoidFootContactIntentCertainty.Confident)
                    continue;
                int start = Mathf.Max(0, intent.StartFrame);
                int end = Mathf.Min(frameCount, intent.EndFrameExclusive);
                HumanoidFootAnchorPolicy policy =
                    intent.Mode == HumanoidFootContactIntentMode.Slide
                        ? HumanoidFootAnchorPolicy.Sliding
                        : HumanoidFootAnchorPolicy.Pinned;
                for (int index = start; index < end; index++)
                    policies[index] = policy;
            }
            return policies;
        }

        // 확정 지지 구간 안에서 이미 감지된 접촉 가중치만 빠르게 잠금함.
        // 감지되지 않은 프레임을 의도만으로 접촉시키지 않음.
        internal static void ApplyTouchdownLock(
            Vector2[] weights, IReadOnlyList<HumanoidFootContactIntent> intents)
        {
            if (weights == null || intents == null) return;
            foreach (HumanoidFootContactIntent intent in intents)
            {
                if (intent == null ||
                    intent.Certainty != HumanoidFootContactIntentCertainty.Confident)
                    continue;
                int start = Mathf.Max(0, intent.StartFrame);
                int end = Mathf.Min(weights.Length, intent.EndFrameExclusive);
                for (int index = start; index < end; index++)
                {
                    float lockWeight = Mathf.Clamp01(
                        (index - start + 1f) / TouchdownLockFrames);
                    Vector2 weight = weights[index];
                    if (weight.x > 0f && lockWeight > weight.x) weight.x = lockWeight;
                    if (weight.y > 0f && lockWeight > weight.y) weight.y = lockWeight;
                    weights[index] = weight;
                }
            }
        }
    }
}
