#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// Humanoid 클립 시작부의 저활동(준비 자세) 구간 끝 시각을 찾음.
    /// 모션캡처 선두 구간은 수치상 완전 정지가 아니라 미세 요동이 지속되므로
    /// 키 편차 대신 0.1초 단위 전 곡선 변화량 합계로 안무 시작을 감지함.
    /// </summary>
    internal static class HumanoidMotionLeadingStillLocator
    {
        private const float SampleStepSeconds = 0.1f;
        // satisfaction_2 실측: 정지 구간 최대 0.75, 안무 시작 시 1.3 이상으로 단계 상승함.
        private const float ActiveSumDeltaThreshold = 1.0f;
        // 초기 포즈 정착 트랜지언트가 먼저 지나갔는지 확인하는 선행 정지 샘플 수.
        private const int RequiredQuietSamples = 5;
        // 단발 스파이크를 안무 시작으로 오인하지 않기 위한 연속 활동 샘플 수.
        private const int RequiredActiveSamples = 2;
        private const float MaxScanSeconds = 60f;

        internal static bool TryFindMotionStartTime(
            AnimationClip clip,
            out float motionStartTimeSeconds)
        {
            motionStartTimeSeconds = 0f;
            if (clip == null || clip.length <= SampleStepSeconds)
            {
                return false;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings == null || bindings.Length == 0)
            {
                return false;
            }

            var curves = new List<AnimationCurve>(bindings.Length);
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve != null && curve.length >= 2)
                {
                    curves.Add(curve);
                }
            }

            if (curves.Count == 0)
            {
                return false;
            }

            float scanEnd = Mathf.Min(clip.length, MaxScanSeconds);
            int consecutiveQuiet = 0;
            int consecutiveActive = 0;
            float previousTime = 0f;
            for (float t = SampleStepSeconds; t < scanEnd; t += SampleStepSeconds)
            {
                float sumDelta = 0f;
                foreach (AnimationCurve curve in curves)
                {
                    sumDelta += Mathf.Abs(
                        curve.Evaluate(t) - curve.Evaluate(previousTime));
                }

                previousTime = t;
                if (sumDelta >= ActiveSumDeltaThreshold)
                {
                    consecutiveActive++;
                    if (consecutiveQuiet >= RequiredQuietSamples &&
                        consecutiveActive >= RequiredActiveSamples)
                    {
                        // 상승 구간 첫 샘플 직전까지를 정지 끝으로 잡아
                        // 움직임 램프 시작이 잘리지 않게 함.
                        motionStartTimeSeconds =
                            t - RequiredActiveSamples * SampleStepSeconds;
                        return motionStartTimeSeconds > 0f;
                    }
                }
                else
                {
                    consecutiveActive = 0;
                    consecutiveQuiet++;
                }
            }

            return false;
        }
    }
}

#endif
