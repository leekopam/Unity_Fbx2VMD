using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 원본 FBX 궤적에서만 지지·자유발·불확실 구간과 접지 의도를 추정함.
    /// 분류 상수와 구간 병합 규칙은 오프라인 분석기 `analyze-contact-events.mjs`와 같게 유지함.
    /// </summary>
    internal static class HumanoidFootContactIntentEstimator
    {
        private enum FrameClass { Uncertain, Support, Airborne }

        private const float FloorPercentile = 0.1f;
        private const float SupportHeightPerHumanScale = 0.015f;
        // 지지 속도 비율은 런타임 앵커 방침의 추종 상한으로도 재사용함.
        internal const float SupportSpeedPerHumanScale = 0.05f;
        private const float AirborneHeightPerHumanScale = 0.05f;
        private const float AirborneSpeedPerHumanScale = 0.2f;
        private const float ClipMotionPerHumanScale = 0.05f;
        private const float MinimumSupportDurationSeconds = 0.1f;
        private const int NoiseGapFrames = 2;

        // 지지 구간 안에서 순수 수평 이동이 이 비율을 넘고 방향이 일치할 때만 미끄럼 의도로 봄.
        // 핀 해제 임계로도 재사용함.
        internal const float SlideDisplacementPerHumanScale = 0.02f;
        private const float SlideDirectionConsistency = 0.5f;

        internal static HumanoidFootContactIntentEstimate Estimate(
            IReadOnlyList<HumanoidFootContactSample> samples,
            float frameRate,
            float humanScale,
            HumanoidFootContactIntentLabelSet labels = null)
        {
            if (samples == null || samples.Count < 2 ||
                !IsFinite(frameRate) || frameRate <= 0f ||
                !IsFinite(humanScale) || humanScale <= 0f)
            {
                throw new ArgumentException("접지 의도 추정에는 2프레임 이상의 표본과 유효한 프레임률·키 비율이 필요합니다.");
            }

            var leftFeet = new Vector3[samples.Count];
            var leftToes = new Vector3[samples.Count];
            var rightFeet = new Vector3[samples.Count];
            var rightToes = new Vector3[samples.Count];
            float minLeftFootY = float.MaxValue;
            float maxLeftFootY = float.MinValue;
            float minRightFootY = float.MaxValue;
            float maxRightFootY = float.MinValue;
            for (int index = 0; index < samples.Count; index++)
            {
                HumanoidFootContactSample sample = samples[index];
                if (!IsFinite(sample.LeftFoot) || !IsFinite(sample.LeftToes) ||
                    !IsFinite(sample.RightFoot) || !IsFinite(sample.RightToes))
                {
                    throw new ArgumentException("접지 의도 표본은 유한한 발 위치만 허용합니다.");
                }

                leftFeet[index] = sample.LeftFoot;
                leftToes[index] = sample.LeftToes;
                rightFeet[index] = sample.RightFoot;
                rightToes[index] = sample.RightToes;
                minLeftFootY = Mathf.Min(minLeftFootY, sample.LeftFoot.y);
                maxLeftFootY = Mathf.Max(maxLeftFootY, sample.LeftFoot.y);
                minRightFootY = Mathf.Min(minRightFootY, sample.RightFoot.y);
                maxRightFootY = Mathf.Max(maxRightFootY, sample.RightFoot.y);
            }

            // 오프라인 분석기처럼 발별 움직임 범위로 판정함. 양발을 합치면
            // 정지한 두 발의 높이 차이를 동작으로 오인한다.
            bool hasClipMotion =
                maxLeftFootY - minLeftFootY >= humanScale * ClipMotionPerHumanScale ||
                maxRightFootY - minRightFootY >= humanScale * ClipMotionPerHumanScale;
            List<HumanoidFootContactIntent> left = EstimateFoot(
                leftFeet, leftToes, frameRate, humanScale, true, hasClipMotion,
                labels?.Left, out List<Vector2Int> leftUncertain);
            List<HumanoidFootContactIntent> right = EstimateFoot(
                rightFeet, rightToes, frameRate, humanScale, false, hasClipMotion,
                labels?.Right, out List<Vector2Int> rightUncertain);
            return new HumanoidFootContactIntentEstimate(
                left, right, leftUncertain, rightUncertain);
        }

        private static List<HumanoidFootContactIntent> EstimateFoot(
            Vector3[] feet,
            Vector3[] toes,
            float frameRate,
            float humanScale,
            bool isLeft,
            bool hasClipMotion,
            IReadOnlyList<HumanoidFootContactIntentLabel> labels,
            out List<Vector2Int> uncertainSpans)
        {
            int count = feet.Length;
            float floorFoot = Percentile(feet, FloorPercentile);
            float floorToes = Percentile(toes, FloorPercentile);
            float supportHeight = humanScale * SupportHeightPerHumanScale;
            float supportSpeed = humanScale * SupportSpeedPerHumanScale;
            float airborneHeight = humanScale * AirborneHeightPerHumanScale;
            float airborneSpeed = humanScale * AirborneSpeedPerHumanScale;
            int minimumFrames = Mathf.Max(2,
                Mathf.CeilToInt(frameRate * MinimumSupportDurationSeconds));

            var classes = new FrameClass[count];
            for (int index = 0; index < count; index++)
            {
                // 첫 프레임은 속도가 정의되지 않아 항상 불확실로 둠.
                if (index == 0)
                {
                    classes[index] = FrameClass.Uncertain;
                    continue;
                }

                float footHeight = feet[index].y - floorFoot;
                float toesHeight = toes[index].y - floorToes;
                float footSpeed = Vector3.Distance(feet[index - 1], feet[index]) * frameRate;
                float toesSpeed = Vector3.Distance(toes[index - 1], toes[index]) * frameRate;
                if (footHeight <= supportHeight && toesHeight <= supportHeight &&
                    footSpeed <= supportSpeed && toesSpeed <= supportSpeed)
                {
                    classes[index] = FrameClass.Support;
                }
                else if (footHeight >= airborneHeight && toesHeight >= airborneHeight &&
                         footSpeed >= airborneSpeed && toesSpeed >= airborneSpeed)
                {
                    classes[index] = FrameClass.Airborne;
                }
                else
                {
                    classes[index] = FrameClass.Uncertain;
                }
            }

            bool hasStableFloor = hasClipMotion;
            if (hasStableFloor)
            {
                hasStableFloor = false;
                foreach (IntRange span in Ranges(classes))
                {
                    if (span.Classification == FrameClass.Support &&
                        span.Length >= minimumFrames)
                    {
                        hasStableFloor = true;
                        break;
                    }
                }
            }

            if (!hasStableFloor)
            {
                for (int index = 0; index < count; index++)
                {
                    classes[index] = FrameClass.Uncertain;
                }
            }
            var preMerge = (FrameClass[])classes.Clone();
            if (hasStableFloor)
            {
                foreach (IntRange span in Ranges(classes))
                {
                    if (span.Classification != FrameClass.Uncertain &&
                        span.Length < minimumFrames)
                    {
                        Fill(classes, span.Start, span.End, FrameClass.Uncertain);
                    }
                }

                List<IntRange> spans = Ranges(classes);
                for (int index = 1; index + 1 < spans.Count; index++)
                {
                    IntRange gap = spans[index];
                    if (gap.Classification == FrameClass.Uncertain &&
                        gap.Length <= NoiseGapFrames &&
                        spans[index - 1].Classification == spans[index + 1].Classification &&
                        spans[index - 1].Classification != FrameClass.Uncertain)
                    {
                        Fill(classes, gap.Start, gap.End,
                            spans[index - 1].Classification);
                    }
                }
            }

            // 사람 확정 표식은 자동 정리를 모두 거친 뒤 마지막에 덮어씀.
            // 병합 판정(preMerge)에도 같은 표식을 입혀 표식 구간이 확신을 깎지 않게 함.
            if (labels != null)
            {
                ApplyHumanLabels(classes, labels);
                ApplyHumanLabels(preMerge, labels);
            }

            var intents = new List<HumanoidFootContactIntent>();
            uncertainSpans = new List<Vector2Int>();
            foreach (IntRange span in Ranges(classes))
            {
                if (span.Classification == FrameClass.Support)
                {
                    intents.Add(BuildIntent(feet, toes, preMerge, span,
                        humanScale, isLeft, labels));
                }
                else if (span.Classification == FrameClass.Uncertain)
                {
                    uncertainSpans.Add(new Vector2Int(span.Start, span.End + 1));
                }
            }

            return intents;
        }

        private static HumanoidFootContactIntent BuildIntent(
            Vector3[] feet,
            Vector3[] toes,
            FrameClass[] classes,
            IntRange span,
            float humanScale,
            bool isLeft,
            IReadOnlyList<HumanoidFootContactIntentLabel> labels)
        {
            Vector3 anchor = (feet[span.Start] + toes[span.Start]) * 0.5f;
            Vector2 net = HorizontalDelta(anchor,
                (feet[span.End] + toes[span.End]) * 0.5f);
            float netLength = net.magnitude;
            float consistency = 0f;
            if (netLength > 0f)
            {
                Vector2 netDirection = net / netLength;
                float weighted = 0f;
                float total = 0f;
                for (int index = span.Start + 1; index <= span.End; index++)
                {
                    Vector2 step = HorizontalDelta(
                        (feet[index - 1] + toes[index - 1]) * 0.5f,
                        (feet[index] + toes[index]) * 0.5f);
                    float stepLength = step.magnitude;
                    if (stepLength <= 0f) continue;
                    weighted += Vector2.Dot(step / stepLength, netDirection) * stepLength;
                    total += stepLength;
                }
                if (total > 0f) consistency = weighted / total;
            }

            HumanoidFootContactIntentMode mode =
                netLength >= humanScale * SlideDisplacementPerHumanScale &&
                consistency >= SlideDirectionConsistency
                    ? HumanoidFootContactIntentMode.Slide
                    : HumanoidFootContactIntentMode.Plant;
            // 겹치는 표식의 모드가 하나로 확정될 때만 자동 판정을 덮어씀.
            HumanoidFootContactIntentMode? labeled = ResolveLabeledMode(labels, span);
            if (labeled.HasValue) mode = labeled.Value;
            HumanoidFootContactIntentCertainty certainty = HumanoidFootContactIntentCertainty.Confident;
            for (int index = span.Start; index <= span.End; index++)
            {
                // 소음 틈새 병합으로 지지에 흡수된 프레임이 있으면 구간 전체를 불확실로 둠.
                if (classes[index] == FrameClass.Support) continue;
                certainty = HumanoidFootContactIntentCertainty.Uncertain;
                break;
            }

            return new HumanoidFootContactIntent(
                isLeft,
                span.Start,
                span.End + 1,
                anchor,
                mode,
                certainty,
                // 첫 프레임은 속도가 없어 항상 불확실이므로, 첫 관측 프레임부터
                // 이어진 지지는 touchdown이 관측되지 않은 클립 시작 지지로 표시함.
                span.Start <= 1);
        }

        // 지지·자유발 표식은 자동 정리 이후의 분류를 그대로 덮어씀.
        private static void ApplyHumanLabels(FrameClass[] classes,
            IReadOnlyList<HumanoidFootContactIntentLabel> labels)
        {
            foreach (HumanoidFootContactIntentLabel label in labels)
            {
                if (!label.IsSupport.HasValue) continue;
                int start = Mathf.Max(0, label.StartFrame);
                int end = Mathf.Min(classes.Length - 1, label.EndFrameInclusive);
                FrameClass value = label.IsSupport.Value
                    ? FrameClass.Support
                    : FrameClass.Airborne;
                for (int index = start; index <= end; index++)
                    classes[index] = value;
            }
        }

        private static HumanoidFootContactIntentMode? ResolveLabeledMode(
            IReadOnlyList<HumanoidFootContactIntentLabel> labels, IntRange span)
        {
            if (labels == null) return null;
            HumanoidFootContactIntentMode? selected = null;
            foreach (HumanoidFootContactIntentLabel label in labels)
            {
                if (!label.Mode.HasValue ||
                    label.EndFrameInclusive < span.Start ||
                    label.StartFrame > span.End) continue;
                // 서로 엇갈리는 모드 표식은 어느 쪽도 채택하지 않음.
                if (selected.HasValue && selected.Value != label.Mode.Value)
                    return null;
                selected = label.Mode;
            }
            return selected;
        }

        private readonly struct IntRange
        {
            internal IntRange(int start, int end, FrameClass classification)
            {
                Start = start;
                End = end;
                Classification = classification;
            }

            internal int Start { get; }
            internal int End { get; }
            internal int Length => End - Start + 1;
            internal FrameClass Classification { get; }
        }

        private static List<IntRange> Ranges(FrameClass[] classes)
        {
            var spans = new List<IntRange>();
            int start = 0;
            while (start < classes.Length)
            {
                int end = start;
                while (end + 1 < classes.Length &&
                       classes[end + 1] == classes[start])
                {
                    end++;
                }

                spans.Add(new IntRange(start, end, classes[start]));
                start = end + 1;
            }

            return spans;
        }

        private static void Fill(FrameClass[] classes, int start, int end,
            FrameClass value)
        {
            for (int index = start; index <= end; index++)
            {
                classes[index] = value;
            }
        }

        private static float Percentile(Vector3[] points, float fraction)
        {
            var heights = new float[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                heights[index] = points[index].y;
            }

            Array.Sort(heights);
            return heights[(int)((heights.Length - 1) * fraction)];
        }

        private static Vector2 HorizontalDelta(Vector3 from, Vector3 to)
        {
            return new Vector2(to.x - from.x, to.z - from.z);
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
