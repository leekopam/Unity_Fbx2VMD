using System;
using RootMotion.FinalIK;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 추가 굽힘의 방향을 계산하고 기존 다리 IK의 근평행 미세 회전 누락을 보완함.
    /// 반환값은 계산 수행 여부이며 도달 오차 판정과 발·발가락 회전 복원은 호출자가 담당함.
    /// </summary>
    internal static class HumanoidLegPoseSolver
    {
        // 원본 벡터는 골반·IK 보정 전, 목표 벡터는 골반 보정 후의 허벅지에서 발목으로 향함.
        // 실패 시 방향을 임의로 뒤집지 않으며 호출자가 원본 방향 유지 여부를 결정함.
        internal static bool TryCalculateBendNormal(Vector3 originalUpperToKnee,
            Vector3 originalKneeToFoot, Vector3 upperToTarget, Vector3 referenceNormal,
            out Vector3 bendNormal, out float referenceWeight)
        {
            bendNormal = Vector3.zero;
            referenceWeight = 0f;
            float upperLength = originalUpperToKnee.magnitude;
            float lowerLength = originalKneeToFoot.magnitude;
            float distance = upperToTarget.magnitude;
            Vector3 originalUpperToFoot = originalUpperToKnee + originalKneeToFoot;
            if (!IsPositive(upperLength) || !IsPositive(lowerLength) || !IsPositive(distance) ||
                !IsPositive(originalUpperToFoot.magnitude))
                return false;

            double upper = upperLength;
            double lower = lowerLength;
            double reach = upper + lower;
            if (distance > reach * 1.000001d || distance < Math.Abs(upper - lower) - reach * 0.000001d)
                return false;

            double along = (distance * (double)distance + upper * upper - lower * lower) / (2d * distance);
            double targetRadius = Math.Sqrt(Math.Max(0d, upper * upper - along * along));
            Vector3 originalCross = Vector3.Cross(originalUpperToKnee, originalKneeToFoot);
            double originalRadius = Vector3.Cross(originalUpperToKnee, originalUpperToFoot).magnitude /
                originalUpperToFoot.magnitude;
            float weight = 1f - (float)Math.Min(1d, targetRadius > reach * 0.0000000001d ? originalRadius / targetRadius : 1d);
            Vector3 axis = upperToTarget / distance;
            Vector3 current = Vector3.ProjectOnPlane(originalCross, axis);
            float currentLength = current.magnitude;
            if (currentLength == 0f && IsPositive(originalCross.sqrMagnitude))
                return false;
            if (weight == 0f)
            {
                if (!IsPositive(currentLength))
                    return false;
                bendNormal = current / currentLength;
                return true;
            }

            if (!IsFinite(referenceNormal.sqrMagnitude))
                return false;
            Vector3 reference = Vector3.ProjectOnPlane(referenceNormal, axis);
            float referenceLength = reference.magnitude;
            if (!IsPositive(referenceLength))
                return false;
            reference /= referenceLength;
            if (IsPositive(currentLength))
            {
                current /= currentLength;
                if (Vector3.Dot(current, reference) <= -0.99f)
                    return false;
                reference = Vector3.Slerp(current, reference, weight).normalized;
            }

            bendNormal = reference;
            referenceWeight = weight;
            return true;
        }

        internal static bool TrySolve(Transform upperLeg, Transform lowerLeg, Transform foot,
            Vector3 target, Vector3 bendNormal, out float targetError)
        {
            targetError = float.PositiveInfinity;
            if (upperLeg == null || lowerLeg == null || foot == null ||
                upperLeg == lowerLeg || lowerLeg == foot ||
                !lowerLeg.IsChildOf(upperLeg) || !foot.IsChildOf(lowerLeg) ||
                !IsFinite(target.sqrMagnitude) || !IsPositive(bendNormal.sqrMagnitude))
                return false;

            float upperLength = Vector3.Distance(upperLeg.position, lowerLeg.position);
            float lowerLength = Vector3.Distance(lowerLeg.position, foot.position);
            if (!IsPositive(upperLength) || !IsPositive(lowerLength) ||
                !IsPositive((target - upperLeg.position).sqrMagnitude))
                return false;

            Vector3 unitBendNormal = bendNormal.normalized;
            IKSolverTrigonometric.Solve(upperLeg, lowerLeg, foot, target, unitBendNormal, 1f);

            // 허벅지의 미세 회전도 누락되면 무릎 위치가 틀어져 종아리 보완의 도달 반경 검사를 통과하지 못함.
            Vector3 direction = target - upperLeg.position;
            double distance = direction.magnitude;
            double along = (distance * distance + upperLength * (double)upperLength -
                lowerLength * (double)lowerLength) / (2d * distance);
            double radius = Math.Sqrt(Math.Max(0d, upperLength * (double)upperLength - along * along));
            Vector3 desiredKnee = direction.normalized * (float)along +
                Vector3.Cross(direction, unitBendNormal).normalized * (float)radius;
            ApplyResidualRotation(upperLeg, lowerLeg.position - upperLeg.position, desiredKnee, upperLength);
            // 무릎 위치가 바뀌면 종아리의 필요 회전은 미세각 범위를 넘을 수 있으므로 목표 방향부터 다시 맞춤.
            lowerLeg.rotation = Quaternion.FromToRotation(foot.position - lowerLeg.position,
                target - lowerLeg.position) * lowerLeg.rotation;
            ApplyResidualRotation(lowerLeg, foot.position - lowerLeg.position, target - lowerLeg.position, lowerLength);

            targetError = Vector3.Distance(foot.position, target);
            return IsFinite(targetError);
        }

        private static void ApplyResidualRotation(Transform bone, Vector3 current, Vector3 desired, float boneLength)
        {
            float currentLength = current.magnitude;
            float desiredLength = desired.magnitude;
            if (IsPositive(currentLength) && IsPositive(desiredLength) &&
                Mathf.Abs(desiredLength - currentLength) <= boneLength * 0.000001f)
            {
                current /= currentLength;
                desired /= desiredLength;
                Vector3 cross = Vector3.Cross(current, desired);
                float crossLength = cross.magnitude;
                float angle = Mathf.Atan2(crossLength, Vector3.Dot(current, desired)) * Mathf.Rad2Deg;
                // 도달 반경이 맞는 근평행 잔여에만 작은 보완각을 허용하여 다른 IK 실패를 덮지 않음.
                if (crossLength > 0f && angle > 0f && angle <= 0.1f)
                    bone.rotation = Quaternion.AngleAxis(angle, cross / crossLength) * bone.rotation;
            }
        }

        private static bool IsPositive(float value) => IsFinite(value) && value > 0f;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
