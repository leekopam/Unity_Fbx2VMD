#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class NativeHumanoidBaselinePoseMetrics
    {
        internal bool HasBoundPlayables { get; set; }
        internal bool IsFootIkEnabled { get; set; }
        internal bool IsPlayableIkEnabled { get; set; }
        internal int MappedBoneCount { get; set; }
        internal int MovingBoneCount { get; set; }
        internal float RootScaleDelta { get; set; }
        internal float MaxBoneScaleDelta { get; set; }
        internal float MaxBoneLengthDelta { get; set; }
        internal float MaxBoneRotationDeltaDegrees { get; set; }
        internal float BodyMotionScoreDegrees { get; set; }
        internal int ChangedPixelCount { get; set; }
        internal float PixelChangeRatio { get; set; }
    }

    internal static class NativeHumanoidBaselinePoseAnalyzer
    {
        private const int SampleCandidateCount = 20;
        private static readonly HumanBodyBones[] MotionScoreBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.UpperChest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftShoulder,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightShoulder,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
        };

        internal static Dictionary<HumanBodyBones, NativeHumanoidBoneState> Capture(
            Animator animator)
        {
            var states = new Dictionary<HumanBodyBones, NativeHumanoidBoneState>();
            for (HumanBodyBones bone = HumanBodyBones.Hips;
                bone < HumanBodyBones.LastBone;
                bone++)
            {
                Transform transform = animator.GetBoneTransform(bone);
                if (transform == null)
                {
                    continue;
                }

                states[bone] = new NativeHumanoidBoneState(
                    transform.localPosition.magnitude,
                    transform.localRotation,
                    transform.localScale);
            }

            return states;
        }

        internal static float SelectComparisonTime(
            NativeHumanoidAnimationPlayer player,
            Animator animator,
            IReadOnlyDictionary<HumanBodyBones, NativeHumanoidBoneState> firstPose,
            float clipLength)
        {
            float bestTime = 0f;
            float bestScore = float.MinValue;
            for (int i = 1; i <= SampleCandidateCount; i++)
            {
                float candidateTime = clipLength * i / SampleCandidateCount;
                player.EvaluateAt(candidateTime);
                Dictionary<HumanBodyBones, NativeHumanoidBoneState> candidatePose =
                    Capture(animator);
                float score = CalculateBodyMotionScore(firstPose, candidatePose);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTime = candidateTime;
                }
            }

            return bestTime;
        }

        internal static NativeHumanoidBaselinePoseMetrics Calculate(
            IReadOnlyDictionary<HumanBodyBones, NativeHumanoidBoneState> firstPose,
            IReadOnlyDictionary<HumanBodyBones, NativeHumanoidBoneState> samplePose)
        {
            var metrics = new NativeHumanoidBaselinePoseMetrics
            {
                MappedBoneCount = firstPose.Count,
                BodyMotionScoreDegrees = CalculateBodyMotionScore(firstPose, samplePose),
            };

            foreach (KeyValuePair<HumanBodyBones, NativeHumanoidBoneState> entry in firstPose)
            {
                if (!samplePose.TryGetValue(entry.Key, out NativeHumanoidBoneState sample))
                {
                    continue;
                }

                float rotationDelta = Quaternion.Angle(
                    entry.Value.LocalRotation,
                    sample.LocalRotation);
                if (rotationDelta > 0.5f)
                {
                    metrics.MovingBoneCount++;
                }

                metrics.MaxBoneRotationDeltaDegrees = Mathf.Max(
                    metrics.MaxBoneRotationDeltaDegrees,
                    rotationDelta);
                metrics.MaxBoneScaleDelta = Mathf.Max(
                    metrics.MaxBoneScaleDelta,
                    Vector3.Distance(entry.Value.LocalScale, sample.LocalScale));

                if (entry.Key != HumanBodyBones.Hips)
                {
                    metrics.MaxBoneLengthDelta = Mathf.Max(
                        metrics.MaxBoneLengthDelta,
                        Mathf.Abs(
                            entry.Value.LocalPositionMagnitude -
                            sample.LocalPositionMagnitude));
                }
            }

            return metrics;
        }

        private static float CalculateBodyMotionScore(
            IReadOnlyDictionary<HumanBodyBones, NativeHumanoidBoneState> firstPose,
            IReadOnlyDictionary<HumanBodyBones, NativeHumanoidBoneState> samplePose)
        {
            float score = 0f;
            foreach (HumanBodyBones bone in MotionScoreBones)
            {
                if (firstPose.TryGetValue(bone, out NativeHumanoidBoneState first) &&
                    samplePose.TryGetValue(bone, out NativeHumanoidBoneState sample))
                {
                    score += Quaternion.Angle(first.LocalRotation, sample.LocalRotation);
                }
            }

            return score;
        }
    }

    internal readonly struct NativeHumanoidBoneState
    {
        internal NativeHumanoidBoneState(
            float localPositionMagnitude,
            Quaternion localRotation,
            Vector3 localScale)
        {
            LocalPositionMagnitude = localPositionMagnitude;
            LocalRotation = localRotation;
            LocalScale = localScale;
        }

        internal float LocalPositionMagnitude { get; }
        internal Quaternion LocalRotation { get; }
        internal Vector3 LocalScale { get; }
    }
}
#endif
