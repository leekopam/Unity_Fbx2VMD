using System.Collections.Generic;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 사람이 human-labels.csv에 기록한 한 발의 확정 표식임.
    /// 프레임 범위는 템플릿 서식과 같이 양끝 포함으로 해석함.
    /// </summary>
    internal readonly struct HumanoidFootContactIntentLabel
    {
        internal HumanoidFootContactIntentLabel(
            int startFrame,
            int endFrameInclusive,
            bool? isSupport,
            HumanoidFootContactIntentMode? mode)
        {
            StartFrame = startFrame;
            EndFrameInclusive = endFrameInclusive;
            IsSupport = isSupport;
            Mode = mode;
        }

        internal int StartFrame { get; }

        internal int EndFrameInclusive { get; }

        // null은 미기록으로 지지 여부 판정을 바꾸지 않음.
        internal bool? IsSupport { get; }

        // null이 아니면 겹치는 지지 구간의 Plant/Slide 의도를 덮어씀.
        internal HumanoidFootContactIntentMode? Mode { get; }
    }

    /// <summary>
    /// 같은 입력 FBX에 누적된 사람 표식 묶음임.
    /// </summary>
    internal sealed class HumanoidFootContactIntentLabelSet
    {
        internal static readonly HumanoidFootContactIntentLabelSet Empty =
            new HumanoidFootContactIntentLabelSet(
                new List<HumanoidFootContactIntentLabel>(),
                new List<HumanoidFootContactIntentLabel>(), 0, 0);

        internal HumanoidFootContactIntentLabelSet(
            IReadOnlyList<HumanoidFootContactIntentLabel> left,
            IReadOnlyList<HumanoidFootContactIntentLabel> right,
            int fileCount,
            int rowCount)
        {
            Left = left;
            Right = right;
            FileCount = fileCount;
            RowCount = rowCount;
        }

        internal IReadOnlyList<HumanoidFootContactIntentLabel> Left { get; }

        internal IReadOnlyList<HumanoidFootContactIntentLabel> Right { get; }

        internal int FileCount { get; }

        internal int RowCount { get; }
    }
}
