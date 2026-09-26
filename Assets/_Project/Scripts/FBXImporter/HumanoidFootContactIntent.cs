using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 원본 궤적만으로 추정한 한 발의 접지 의도 구간임. 제품 판정과 독립된 정답 후보로만 씀.
    /// </summary>
    internal enum HumanoidFootContactIntentMode
    {
        Plant,
        Slide
    }

    /// <summary>
    /// 구간 안에 원본 분류가 불확실했던 프레임이 흡수됐는지 표시함.
    /// </summary>
    internal enum HumanoidFootContactIntentCertainty
    {
        Confident,
        Uncertain
    }

    internal sealed class HumanoidFootContactIntent
    {
        internal HumanoidFootContactIntent(
            bool isLeft,
            int startFrame,
            int endFrameExclusive,
            Vector3 anchor,
            HumanoidFootContactIntentMode mode,
            HumanoidFootContactIntentCertainty certainty,
            bool startsAtClipStart)
        {
            IsLeft = isLeft;
            StartFrame = startFrame;
            EndFrameExclusive = endFrameExclusive;
            Anchor = anchor;
            Mode = mode;
            Certainty = certainty;
            StartsAtClipStart = startsAtClipStart;
        }

        internal const string Source = "source_fbx_trajectory";

        internal bool IsLeft { get; }

        internal int StartFrame { get; }

        internal int EndFrameExclusive { get; }

        /// <summary>
        /// 지지 시작 프레임의 발목·발끝 중점 월드 위치임.
        /// </summary>
        internal Vector3 Anchor { get; }

        internal HumanoidFootContactIntentMode Mode { get; }

        internal HumanoidFootContactIntentCertainty Certainty { get; }

        internal bool StartsAtClipStart { get; }
    }

    /// <summary>
    /// 한 발 또는 양발의 의도 구간과 사람 표식이 필요한 불확실 범위를 담음.
    /// x는 시작 프레임, y는 끝 배타 프레임임.
    /// </summary>
    internal sealed class HumanoidFootContactIntentEstimate
    {
        internal HumanoidFootContactIntentEstimate(
            IReadOnlyList<HumanoidFootContactIntent> left,
            IReadOnlyList<HumanoidFootContactIntent> right,
            IReadOnlyList<Vector2Int> leftUncertainSpans,
            IReadOnlyList<Vector2Int> rightUncertainSpans)
        {
            Left = left;
            Right = right;
            LeftUncertainSpans = leftUncertainSpans;
            RightUncertainSpans = rightUncertainSpans;
        }

        internal IReadOnlyList<HumanoidFootContactIntent> Left { get; }

        internal IReadOnlyList<HumanoidFootContactIntent> Right { get; }

        internal IReadOnlyList<Vector2Int> LeftUncertainSpans { get; }

        internal IReadOnlyList<Vector2Int> RightUncertainSpans { get; }
    }
}
