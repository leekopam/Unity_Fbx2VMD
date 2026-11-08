using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Unity.Mathematics;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed partial class HumanoidMotionPlaybackController
    {
        private const int SupportCurvePreRollFrames = 24;
        private const int SupportCurveSamplesPerFrame = 4;
        private const int MaximumHeldSupportFrames = 144;
        private readonly List<HumanoidSupportCurveProblem.Curve> _supportCurves = new();
        private HumanoidSupportCurvePose _supportCurvePose;
        private bool _isPreparingSupportCurves;

        private bool CanUseSupportCurves => !_isPreparingSupportCurves &&
            _isGroundResponseEnabled && _useFootStepDiffusion && IsIkGoalFootDriveActive && _footGoalAlpha == 1f &&
            (_poseCorrectionDocument == null || _poseCorrectionDocument.FrameCount == 0);

        internal string GetSupportCurveCacheIdentity()
        {
            if (!CanUseSupportCurves || _supportCurves.Count == 0) return string.Empty;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(_supportCurves.Count);
            foreach (var curve in _supportCurves) curve.WriteCacheIdentity(writer);
            writer.Flush();
            stream.Position = 0;
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream));
        }

        internal void PrepareSupportCurves(Animator animator)
        {
            if (State != HumanoidMotionPlaybackState.Ready || animator == null || !CanUseSupportCurves ||
                _footGrounding == null || !_footGrounding.IsPrepared)
                return;

            float savedTime = CurrentTimeSeconds;
            _supportCurves.Clear();
            _supportCurvePose = new HumanoidSupportCurvePose(animator);
            _isPreparingSupportCurves = true;
            try
            {
                foreach (var window in _footGrounding.GetSupportCurveWindows().OrderBy(window => window.Start))
                {
                    if (_supportCurves.Count > 0 && _supportCurves[^1].End > window.Start ||
                        !IsExtensionLimitedAt(window.Full, window.Side) || !IsExtensionLimitedAt(window.Full + 1, window.Side))
                        continue;

                    int first = Math.Max(0, window.Start - 1);
                    TryPrepareSupportCurve(first, window.Start, window.FadeEnd, 4, window.Boundaries);

                    int heldEnd = window.FadeEnd;
                    int limit = Math.Min(window.SupportEnd, window.FadeEnd + MaximumHeldSupportFrames);
                    for (int frame = window.FadeEnd; frame <= limit && IsExtensionLimitedAt(frame, window.Side); frame++)
                        heldEnd = frame;
                    if (heldEnd - window.FadeEnd >= 12)
                        TryPrepareSupportCurve(first, window.FadeEnd, heldEnd, 12, window.Boundaries);
                }
            }
            finally
            {
                _supportCurvePose.Restore();
                _isPreparingSupportCurves = false;
                Seek(savedTime);
            }
        }

        private bool IsExtensionLimitedAt(int frame, int side)
        {
            if (!SeekFrame(frame) || LastGroundingStatus != HumanoidFootGroundingStatus.Applied ||
                !TryReadSupportCurveSource(out double2 angles))
                return false;
            return _supportCurvePose.IsExtensionLimited(side, side == 0 ? angles.x : angles.y);
        }

        private bool TryReadSupportCurveSource(out double2 source)
        {
            double2 angles = default;
            bool captured = TryCapturePoseBeforeFootStabilization(() => angles = _supportCurvePose.ReadKneeAngles());
            source = angles;
            return captured && math.all(math.isfinite(angles));
        }

        private void TryPrepareSupportCurve(int first, int start, int end, int spacing, int[] boundaries)
        {
            int last = Math.Min(LastFrameIndex, end + 3);
            int count = (last - first) * SupportCurveSamplesPerFrame + 1;
            var samples = new HumanoidSupportCurveProblem.Sample[count];
            PreRollSupportCurve(first);
            for (int index = 0; index < count; index++)
            {
                float frame = first + index / (float)SupportCurveSamplesPerFrame;
                if (!Seek(frame / ClipFrameRate) || !TryReadSupportCurveSource(out double2 source)) return;
                samples[index] = _supportCurvePose.Capture(source);
                if (!samples[index].Left.IsValid || !samples[index].Right.IsValid) return;
            }

            var problem = new HumanoidSupportCurveProblem(samples, first, start, end, spacing, boundaries);
            if (problem.TryGenerate(out var curve) && PreservesSupportCurve(curve, first, last, samples, boundaries))
                _supportCurves.Add(curve);
        }

        private void PreRollSupportCurve(int frame)
        {
            for (int before = Math.Max(0, frame - SupportCurvePreRollFrames); before < frame; before++) SeekFrame(before);
        }

        private bool PreservesSupportCurve(HumanoidSupportCurveProblem.Curve curve, int first, int last,
            HumanoidSupportCurveProblem.Sample[] samples, int[] boundaries)
        {
            var baseline = new double2[samples.Length];
            var corrected = new double2[samples.Length];
            var baselineHips = new double3[samples.Length];
            var correctedHips = new double3[samples.Length];
            double2 originalError = double2.zero;
            double2 correctedError = double2.zero;
            PreRollSupportCurve(first);
            for (int index = 0; index < samples.Length; index++)
            {
                float frame = first + index / (float)SupportCurveSamplesPerFrame;
                if (!Seek(frame / ClipFrameRate)) return false;
                baseline[index] = _supportCurvePose.ReadKneeAngles();
                baselineHips[index] = _supportCurvePose.ReadHipMm();
                if (!TryApplySupportCurve(curve.GetShiftMm(frame))) return false;
                corrected[index] = _supportCurvePose.ReadKneeAngles();
                correctedHips[index] = _supportCurvePose.ReadHipMm();
                _supportCurvePose.Restore();
                if (frame < curve.Start || frame > curve.End) continue;
                var source = new double2(samples[index].Left.ReferenceDegrees, samples[index].Right.ReferenceDegrees);
                originalError += (baseline[index] - source) * (baseline[index] - source);
                correctedError += (corrected[index] - source) * (corrected[index] - source);
            }
            if (!math.all(correctedError < originalError) || !PreservesMotion(0, samples.Length)) return false;
            foreach (int boundary in boundaries)
            {
                if (boundary - 1 < first || boundary + 1 > last) continue;
                if (!PreservesMotion((boundary - 1 - first) * SupportCurveSamplesPerFrame,
                    (boundary + 1 - first) * SupportCurveSamplesPerFrame + 1)) return false;
            }

            PreRollSupportCurve(curve.Start);
            for (int frame = curve.Start + 1; frame <= curve.End; frame++)
            {
                if (!ReadBoundary(frame - .001f, out double2 beforeBase, out double2 beforeCurve) ||
                    !ReadBoundary(frame, out double2 atBase, out double2 atCurve) ||
                    !math.all(math.abs(atCurve - beforeCurve) <= math.abs(atBase - beforeBase) + .01)) return false;
            }
            return true;

            bool ReadBoundary(float frame, out double2 old, out double2 next)
            {
                old = next = default;
                if (!Seek(frame / ClipFrameRate)) return false;
                old = _supportCurvePose.ReadKneeAngles();
                if (!TryApplySupportCurve(curve.GetShiftMm(frame))) return false;
                next = _supportCurvePose.ReadKneeAngles();
                _supportCurvePose.Restore();
                return true;
            }

            bool PreservesMotion(int from, int to)
            {
                double2 oldStep = double2.zero, newStep = double2.zero;
                double2 oldAcceleration = double2.zero, newAcceleration = double2.zero;
                double oldHipAcceleration = 0, newHipAcceleration = 0;
                for (int index = from + 1; index < to; index++)
                {
                    oldStep = math.max(oldStep, math.abs(baseline[index] - baseline[index - 1]));
                    newStep = math.max(newStep, math.abs(corrected[index] - corrected[index - 1]));
                    if (index <= from + 1) continue;
                    oldAcceleration = math.max(oldAcceleration, math.abs(baseline[index] - 2 * baseline[index - 1] + baseline[index - 2]));
                    newAcceleration = math.max(newAcceleration, math.abs(corrected[index] - 2 * corrected[index - 1] + corrected[index - 2]));
                    oldHipAcceleration = Math.Max(oldHipAcceleration, math.length(baselineHips[index] - 2 * baselineHips[index - 1] + baselineHips[index - 2]));
                    newHipAcceleration = Math.Max(newHipAcceleration, math.length(correctedHips[index] - 2 * correctedHips[index - 1] + correctedHips[index - 2]));
                }
                return math.all(newStep <= oldStep + .0025) && math.all(newAcceleration <= oldAcceleration + .000625) &&
                    newHipAcceleration <= oldHipAcceleration + .001;
            }
        }

        private void ApplyPreparedSupportCurve(float frame)
        {
            foreach (var curve in _supportCurves)
            {
                if (frame <= curve.Start) return;
                if (frame >= curve.End) continue;
                TryApplySupportCurve(curve.GetShiftMm(frame));
                return;
            }
        }

        private bool TryApplySupportCurve(double3 shiftMm)
        {
            if (math.lengthsq(shiftMm) <= 1e-10) return true;
            if (LastGroundingStatus != HumanoidFootGroundingStatus.Applied ||
                !TryCaptureCurrentFootSurface(out var left, out var right)) return false;

            var shift = new Vector3((float)shiftMm.x, (float)shiftMm.y, (float)shiftMm.z) * .001f;
            float contactLimit = _footGrounding.SupportCurveContactLimitMm;
            return _supportCurvePose.TryApply(shift, () =>
                TryCaptureCurrentFootSurface(out var afterLeft, out var afterRight) &&
                PreservesSupportSurface(left, afterLeft, contactLimit) && PreservesSupportSurface(right, afterRight, contactLimit));
        }

        private static bool PreservesSupportSurface(HumanoidFootGroundingSnapshot before, HumanoidFootGroundingSnapshot after,
            float contactLimitMm)
        {
            const float AllowedSoleDeltaMm = .01f;
            if (before.has_ground != after.has_ground || before.rear_weight != after.rear_weight ||
                before.front_weight != after.front_weight) return false;
            return !before.has_ground ||
                KeepsContact(before.rear_distance_mm, after.rear_distance_mm) &&
                KeepsContact(before.front_distance_mm, after.front_distance_mm) &&
                after.minimum_distance_mm >= Mathf.Min(-contactLimitMm, before.minimum_distance_mm) - .001f;

            bool KeepsContact(float original, float current) => (original <= contactLimitMm) == (current <= contactLimitMm) &&
                Mathf.Abs(original - current) <= AllowedSoleDeltaMm;
        }
    }
}
