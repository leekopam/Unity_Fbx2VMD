using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 원본 모션과 분리해 저장하는 모델 중립 Humanoid pose 수정 데이터임.
    /// </summary>
    [Serializable]
    internal sealed class HumanoidPoseCorrectionDocument
    {
        internal const int CurrentSchemaVersion = 1;

        [SerializeField] private int _schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string _motionName = string.Empty;
        [SerializeField] private float _sourceFrameRate =
            HumanoidMotionFrameCalculator.DefaultFrameRate;
        [SerializeField] private List<HumanoidPoseCorrectionFrame> _frames = new();

        internal HumanoidPoseCorrectionDocument()
        {
        }

        internal HumanoidPoseCorrectionDocument(string motionName, float sourceFrameRate)
        {
            _motionName = motionName?.Trim() ?? string.Empty;
            _sourceFrameRate = HumanoidMotionFrameCalculator.NormalizeFrameRate(
                sourceFrameRate);
        }

        internal int SchemaVersion => _schemaVersion;

        internal string MotionName => _motionName ?? string.Empty;

        internal float SourceFrameRate =>
            HumanoidMotionFrameCalculator.NormalizeFrameRate(_sourceFrameRate);

        internal int FrameCount => Frames.Count;

        internal int LastCorrectionFrameIndex
        {
            get
            {
                int lastFrameIndex = -1;
                foreach (HumanoidPoseCorrectionFrame frame in Frames)
                {
                    if (frame != null)
                    {
                        lastFrameIndex = Mathf.Max(lastFrameIndex, frame.FrameIndex);
                    }
                }

                return lastFrameIndex;
            }
        }

        internal bool HasFrameCorrection(int frameIndex)
        {
            return frameIndex >= 0 && Frames.Exists(
                candidate => candidate.FrameIndex == frameIndex);
        }

        internal bool TrySetMuscleDelta(
            int frameIndex,
            string muscleName,
            float delta)
        {
            if (frameIndex < 0 || !IsFinite(delta))
            {
                return false;
            }

            int muscleIndex = RetargetingMuscleReferencePolicy.FindHumanMuscleIndex(
                muscleName);
            if (muscleIndex < 0 || muscleIndex >= HumanTrait.MuscleCount)
            {
                return false;
            }

            HumanoidPoseCorrectionFrame frame = Frames.Find(
                candidate => candidate.FrameIndex == frameIndex);
            if (frame == null)
            {
                frame = new HumanoidPoseCorrectionFrame(frameIndex);
                Frames.Add(frame);
                Frames.Sort((left, right) => left.FrameIndex.CompareTo(right.FrameIndex));
            }

            frame.SetMuscleDelta(HumanTrait.MuscleName[muscleIndex], delta);
            return true;
        }

        internal bool TryGetMuscleDelta(
            int frameIndex,
            string muscleName,
            out float delta)
        {
            delta = 0f;
            int muscleIndex = RetargetingMuscleReferencePolicy.FindHumanMuscleIndex(
                muscleName);
            if (frameIndex < 0 || muscleIndex < 0)
            {
                return false;
            }

            HumanoidPoseCorrectionFrame frame = Frames.Find(
                candidate => candidate.FrameIndex == frameIndex);
            return frame != null && frame.TryGetMuscleDelta(
                HumanTrait.MuscleName[muscleIndex],
                out delta);
        }

        internal bool TryApplyMuscleDeltas(int frameIndex, float[] muscles)
        {
            if (frameIndex < 0 ||
                muscles == null ||
                muscles.Length < HumanTrait.MuscleCount)
            {
                return false;
            }

            HumanoidPoseCorrectionFrame frame = Frames.Find(
                candidate => candidate.FrameIndex == frameIndex);
            return frame != null && frame.TryApplyMuscleDeltas(muscles);
        }

        internal bool TryRemoveFrame(int frameIndex)
        {
            if (frameIndex < 0)
            {
                return false;
            }

            int frameListIndex = Frames.FindIndex(
                candidate => candidate.FrameIndex == frameIndex);
            if (frameListIndex < 0)
            {
                return false;
            }

            Frames.RemoveAt(frameListIndex);
            return true;
        }

        internal bool TryValidate(out string errorMessage)
        {
            if (_schemaVersion != CurrentSchemaVersion)
            {
                errorMessage =
                    $"지원하지 않는 보정 문서 버전입니다: {_schemaVersion}";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_motionName) ||
                !string.Equals(_motionName, _motionName.Trim(), StringComparison.Ordinal))
            {
                errorMessage = "보정 문서의 모션 이름이 올바르지 않습니다.";
                return false;
            }

            if (!IsFinite(_sourceFrameRate) || _sourceFrameRate <= 0f)
            {
                errorMessage = "보정 문서의 원본 프레임률이 올바르지 않습니다.";
                return false;
            }

            var frameIndices = new HashSet<int>();
            foreach (HumanoidPoseCorrectionFrame frame in Frames)
            {
                if (frame == null)
                {
                    errorMessage = "보정 문서에 비어 있는 프레임이 있습니다.";
                    return false;
                }

                if (!frameIndices.Add(frame.FrameIndex))
                {
                    errorMessage = $"중복 보정 프레임이 있습니다: {frame.FrameIndex}";
                    return false;
                }

                if (!frame.TryValidate(out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private List<HumanoidPoseCorrectionFrame> Frames =>
            _frames ??= new List<HumanoidPoseCorrectionFrame>();

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    internal sealed class HumanoidPoseCorrectionFrame
    {
        [SerializeField] private int _frameIndex;
        [SerializeField] private List<HumanoidMuscleCorrection> _muscleCorrections = new();

        internal HumanoidPoseCorrectionFrame()
        {
        }

        internal HumanoidPoseCorrectionFrame(int frameIndex)
        {
            _frameIndex = frameIndex;
        }

        internal int FrameIndex => _frameIndex;

        internal void SetMuscleDelta(string muscleName, float delta)
        {
            for (int index = 0; index < MuscleCorrections.Count; index++)
            {
                if (!string.Equals(
                        MuscleCorrections[index].MuscleName,
                        muscleName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                MuscleCorrections[index] = new HumanoidMuscleCorrection(
                    muscleName,
                    delta);
                return;
            }

            MuscleCorrections.Add(new HumanoidMuscleCorrection(muscleName, delta));
        }

        internal bool TryGetMuscleDelta(string muscleName, out float delta)
        {
            foreach (HumanoidMuscleCorrection correction in MuscleCorrections)
            {
                if (string.Equals(
                        correction.MuscleName,
                        muscleName,
                        StringComparison.Ordinal))
                {
                    delta = correction.Delta;
                    return true;
                }
            }

            delta = 0f;
            return false;
        }

        internal bool TryApplyMuscleDeltas(float[] muscles)
        {
            if (muscles == null ||
                muscles.Length < HumanTrait.MuscleCount ||
                MuscleCorrections.Count == 0)
            {
                return false;
            }

            var muscleIndices = new int[MuscleCorrections.Count];
            for (int index = 0; index < MuscleCorrections.Count; index++)
            {
                HumanoidMuscleCorrection correction = MuscleCorrections[index];
                int muscleIndex = RetargetingMuscleReferencePolicy.FindHumanMuscleIndex(
                    correction.MuscleName);
                if (muscleIndex < 0 ||
                    muscleIndex >= HumanTrait.MuscleCount ||
                    !IsFinite(correction.Delta) ||
                    !IsFinite(muscles[muscleIndex]))
                {
                    return false;
                }

                muscleIndices[index] = muscleIndex;
            }

            for (int index = 0; index < MuscleCorrections.Count; index++)
            {
                int muscleIndex = muscleIndices[index];
                muscles[muscleIndex] = Mathf.Clamp(
                    muscles[muscleIndex] + MuscleCorrections[index].Delta,
                    -1f,
                    1f);
            }

            return true;
        }

        internal bool TryValidate(out string errorMessage)
        {
            if (_frameIndex < 0)
            {
                errorMessage = $"보정 프레임 번호가 올바르지 않습니다: {_frameIndex}";
                return false;
            }

            if (MuscleCorrections.Count == 0)
            {
                errorMessage = $"{_frameIndex} 프레임에 muscle 보정값이 없습니다.";
                return false;
            }

            var muscleNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (HumanoidMuscleCorrection correction in MuscleCorrections)
            {
                if (!correction.TryValidate(out errorMessage))
                {
                    return false;
                }

                if (!muscleNames.Add(correction.MuscleName))
                {
                    errorMessage =
                        $"{_frameIndex} 프레임에 중복 muscle이 있습니다: " +
                        correction.MuscleName;
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private List<HumanoidMuscleCorrection> MuscleCorrections =>
            _muscleCorrections ??= new List<HumanoidMuscleCorrection>();

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    [Serializable]
    internal struct HumanoidMuscleCorrection
    {
        [SerializeField] private string _muscleName;
        [SerializeField] private float _delta;

        internal HumanoidMuscleCorrection(string muscleName, float delta)
        {
            _muscleName = muscleName;
            _delta = delta;
        }

        internal string MuscleName => _muscleName ?? string.Empty;

        internal float Delta => _delta;

        internal bool TryValidate(out string errorMessage)
        {
            int muscleIndex = RetargetingMuscleReferencePolicy.FindHumanMuscleIndex(
                MuscleName);
            if (muscleIndex < 0 ||
                muscleIndex >= HumanTrait.MuscleCount ||
                !string.Equals(
                    HumanTrait.MuscleName[muscleIndex],
                    MuscleName,
                    StringComparison.Ordinal))
            {
                errorMessage = $"표준 Humanoid muscle 이름이 아닙니다: {MuscleName}";
                return false;
            }

            if (float.IsNaN(_delta) || float.IsInfinity(_delta))
            {
                errorMessage = $"muscle 보정값이 유한수가 아닙니다: {MuscleName}";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }
    }
}
