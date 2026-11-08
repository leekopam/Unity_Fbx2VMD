using System;
using Unity.Mathematics;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal sealed class HumanoidSupportCurvePose
    {
        private const float AnkleToleranceMeters = .000002f;
        private const float LengthToleranceMeters = .000001f;
        private readonly Transform _hips;
        private readonly Leg _left;
        private readonly Leg _right;
        private LegPose _savedLeft;
        private LegPose _savedRight;
        private Vector3 _savedHipPosition;
        private bool _hasSavedPose;

        internal HumanoidSupportCurvePose(Animator animator)
        {
            _hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            _left = new Leg(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            _right = new Leg(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot));
        }

        internal double2 ReadKneeAngles() => new double2(_left.ReadKneeAngle(), _right.ReadKneeAngle());

        internal double3 ReadHipMm() => Millimeters(_hips.position);

        internal HumanoidSupportCurveProblem.Sample Capture(double2 sourceAngles) =>
            new HumanoidSupportCurveProblem.Sample(Millimeters(_hips.position),
                _left.Capture(sourceAngles.x), _right.Capture(sourceAngles.y));

        internal bool IsExtensionLimited(int side, double sourceAngle) =>
            (side == 0 ? _left : _right).IsExtensionLimited(sourceAngle);

        internal void Restore()
        {
            if (!_hasSavedPose) return;
            _hips.localPosition = _savedHipPosition;
            _savedLeft.Restore();
            _savedRight.Restore();
            _hasSavedPose = false;
        }

        private static double3 Millimeters(Vector3 position) =>
            new double3(position.x * 1000d, position.y * 1000d, position.z * 1000d);

        internal bool TryApply(Vector3 pelvisShift, Func<bool> preservesContact)
        {
            if (_hips == null || !_left.IsValid || !_right.IsValid || !float.IsFinite(pelvisShift.sqrMagnitude)) return false;
            if (pelvisShift.sqrMagnitude <= 1e-16f) return true;
            if (_hasSavedPose) return false;

            _savedHipPosition = _hips.localPosition;
            _savedLeft = new LegPose(_left);
            _savedRight = new LegPose(_right);
            _hasSavedPose = true;
            bool applied = false;
            try
            {
                _hips.position += pelvisShift;
                applied = _savedLeft.TryReapply() && _savedRight.TryReapply() && preservesContact();
                return applied;
            }
            finally
            {
                if (!applied) Restore();
            }
        }

        private readonly struct Leg
        {
            internal Leg(Transform upper, Transform lower, Transform foot)
            {
                Upper = upper;
                Lower = lower;
                Foot = foot;
            }

            internal Transform Upper { get; }
            internal Transform Lower { get; }
            internal Transform Foot { get; }
            internal bool IsValid => Upper != null && Lower != null && Foot != null;

            internal HumanoidSupportCurveProblem.LegGeometry Capture(double sourceAngle) =>
                new HumanoidSupportCurveProblem.LegGeometry(Millimeters(Upper.position), Millimeters(Lower.position),
                    Millimeters(Foot.position), sourceAngle);

            internal double ReadKneeAngle()
            {
                var upper = Millimeters(Lower.position) - Millimeters(Upper.position);
                var lower = Millimeters(Foot.position) - Millimeters(Lower.position);
                double length = math.length(upper) * math.length(lower);
                if (!(length > 0)) return double.NaN;
                return Math.Acos(Math.Clamp(math.dot(upper, lower) / length, -1, 1)) * 180 / Math.PI;
            }

            internal bool IsExtensionLimited(double sourceAngle)
            {
                double angle = ReadKneeAngle();
                if (!(sourceAngle - angle >= 1)) return false;
                float upper = Vector3.Distance(Upper.position, Lower.position);
                float lower = Vector3.Distance(Lower.position, Foot.position);
                if (!HumanoidPelvisReachCalculator.TryCalculateExtensionReach(upper, lower,
                    Mathf.PI - (float)sourceAngle * Mathf.Deg2Rad, upper + lower, 1f,
                    HumanoidPelvisReachCalculator.DefaultDampingThresholdRadians, out float distance))
                    return false;
                double reach = distance;
                double cosine = (reach * reach - (double)upper * upper - (double)lower * lower) / (2d * upper * lower);
                double limit = Math.Acos(Math.Clamp(cosine, -1, 1)) * 180 / Math.PI;
                return Math.Abs(limit - angle) <= .05;
            }
        }

        private readonly struct LegPose
        {
            private readonly Leg _leg;
            private readonly Vector3 _ankle;
            private readonly Vector3 _upperToKnee;
            private readonly Vector3 _kneeToFoot;
            private readonly Vector3 _bend;
            private readonly Quaternion _footWorld;
            private readonly Quaternion _upperLocal;
            private readonly Quaternion _lowerLocal;
            private readonly Quaternion _footLocal;

            internal LegPose(Leg leg)
            {
                _leg = leg;
                _ankle = leg.Foot.position;
                _upperToKnee = leg.Lower.position - leg.Upper.position;
                _kneeToFoot = _ankle - leg.Lower.position;
                _bend = Vector3.Cross(_upperToKnee, _kneeToFoot).normalized;
                _footWorld = leg.Foot.rotation;
                _upperLocal = leg.Upper.localRotation;
                _lowerLocal = leg.Lower.localRotation;
                _footLocal = leg.Foot.localRotation;
            }

            internal bool TryReapply()
            {
                if (!HumanoidLegPoseSolver.TryCalculateBendNormal(_upperToKnee, _kneeToFoot, _ankle - _leg.Upper.position,
                        _bend, out Vector3 bend, out _) ||
                    !HumanoidLegPoseSolver.TrySolve(_leg.Upper, _leg.Lower, _leg.Foot, _ankle, bend, out float error) ||
                    error > AnkleToleranceMeters)
                    return false;

                _leg.Foot.rotation = _footWorld;
                Vector3 upper = _leg.Lower.position - _leg.Upper.position;
                Vector3 lower = _leg.Foot.position - _leg.Lower.position;
                return Mathf.Abs(upper.magnitude - _upperToKnee.magnitude) <= LengthToleranceMeters &&
                    Mathf.Abs(lower.magnitude - _kneeToFoot.magnitude) <= LengthToleranceMeters &&
                    Vector3.Dot(_bend, Vector3.Cross(upper, lower)) > 0f;
            }

            internal void Restore()
            {
                _leg.Upper.localRotation = _upperLocal;
                _leg.Lower.localRotation = _lowerLocal;
                _leg.Foot.localRotation = _footLocal;
            }
        }
    }
}
