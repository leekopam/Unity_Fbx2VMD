using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal readonly struct NativeSkinningDualQuaternionTransform
    {
        internal NativeSkinningDualQuaternionTransform(Vector4 real, Vector4 dual)
        {
            Real = real;
            Dual = dual;
        }

        internal Vector4 Real { get; }

        internal Vector4 Dual { get; }
    }

    /// <summary>
    /// 스키닝 행렬을 이중 쿼터니언으로 변환하고 단일 정점 자세를 계산함.
    /// </summary>
    internal static class NativeSkinningDualQuaternionVertexCalculator
    {
        private const float MinimumMagnitude = 0.000001f;

        internal static bool TryBuildTransforms(
            IReadOnlyList<Matrix4x4> skinningMatrices,
            out NativeSkinningDualQuaternionTransform[] transforms)
        {
            transforms = null;
            if (skinningMatrices == null || skinningMatrices.Count == 0)
            {
                return false;
            }

            var result = new NativeSkinningDualQuaternionTransform[
                skinningMatrices.Count];
            for (int boneIndex = 0; boneIndex < skinningMatrices.Count; boneIndex++)
            {
                Matrix4x4 matrix = skinningMatrices[boneIndex];
                Quaternion rotation = matrix.rotation;
                float magnitude = Mathf.Sqrt(
                    rotation.x * rotation.x +
                    rotation.y * rotation.y +
                    rotation.z * rotation.z +
                    rotation.w * rotation.w);
                if (magnitude <= MinimumMagnitude || !IsFinite(magnitude))
                {
                    return false;
                }
                rotation = new Quaternion(
                    rotation.x / magnitude,
                    rotation.y / magnitude,
                    rotation.z / magnitude,
                    rotation.w / magnitude);
                Vector3 translation = matrix.GetColumn(3);
                if (!IsFinite(translation))
                {
                    return false;
                }
                Quaternion dual = Multiply(
                    new Quaternion(
                        translation.x,
                        translation.y,
                        translation.z,
                        0f),
                    rotation);
                result[boneIndex] = new NativeSkinningDualQuaternionTransform(
                    ToVector4(rotation),
                    ToVector4(dual) * 0.5f);
            }

            transforms = result;
            return true;
        }

        internal static bool TryTransformVertex(
            Vector3 restVertex,
            BoneWeight weights,
            IReadOnlyList<NativeSkinningDualQuaternionTransform> transforms,
            out Vector3 transformedVertex)
        {
            Vector4 referenceReal = Vector4.zero;
            Vector4 realSum = Vector4.zero;
            Vector4 dualSum = Vector4.zero;
            bool hasReference = false;
            Accumulate(
                weights.boneIndex0,
                weights.weight0,
                transforms,
                ref referenceReal,
                ref hasReference,
                ref realSum,
                ref dualSum);
            Accumulate(
                weights.boneIndex1,
                weights.weight1,
                transforms,
                ref referenceReal,
                ref hasReference,
                ref realSum,
                ref dualSum);
            Accumulate(
                weights.boneIndex2,
                weights.weight2,
                transforms,
                ref referenceReal,
                ref hasReference,
                ref realSum,
                ref dualSum);
            Accumulate(
                weights.boneIndex3,
                weights.weight3,
                transforms,
                ref referenceReal,
                ref hasReference,
                ref realSum,
                ref dualSum);

            float magnitude = Mathf.Sqrt(Vector4.Dot(realSum, realSum));
            if (!hasReference ||
                magnitude <= MinimumMagnitude ||
                !IsFinite(magnitude))
            {
                transformedVertex = restVertex;
                return false;
            }

            Vector4 real = realSum / magnitude;
            Vector4 dual = dualSum / magnitude;
            dual -= real * Vector4.Dot(real, dual);
            Quaternion rotation = ToQuaternion(real);
            Quaternion translationQuaternion = Multiply(
                ToQuaternion(dual),
                new Quaternion(-rotation.x, -rotation.y, -rotation.z, rotation.w));
            Vector3 translation = new Vector3(
                translationQuaternion.x,
                translationQuaternion.y,
                translationQuaternion.z) * 2f;
            transformedVertex = rotation * restVertex + translation;
            return IsFinite(transformedVertex);
        }

        private static void Accumulate(
            int boneIndex,
            float weight,
            IReadOnlyList<NativeSkinningDualQuaternionTransform> transforms,
            ref Vector4 referenceReal,
            ref bool hasReference,
            ref Vector4 realSum,
            ref Vector4 dualSum)
        {
            if (transforms == null ||
                weight <= 0f ||
                boneIndex < 0 ||
                boneIndex >= transforms.Count)
            {
                return;
            }

            NativeSkinningDualQuaternionTransform transform = transforms[boneIndex];
            if (!hasReference)
            {
                referenceReal = transform.Real;
                hasReference = true;
            }
            float sign = Vector4.Dot(referenceReal, transform.Real) < 0f
                ? -1f
                : 1f;
            realSum += transform.Real * (weight * sign);
            dualSum += transform.Dual * (weight * sign);
        }

        private static Quaternion Multiply(Quaternion first, Quaternion second)
        {
            return new Quaternion(
                first.w * second.x + first.x * second.w +
                    first.y * second.z - first.z * second.y,
                first.w * second.y - first.x * second.z +
                    first.y * second.w + first.z * second.x,
                first.w * second.z + first.x * second.y -
                    first.y * second.x + first.z * second.w,
                first.w * second.w - first.x * second.x -
                    first.y * second.y - first.z * second.z);
        }

        private static Vector4 ToVector4(Quaternion value)
        {
            return new Vector4(value.x, value.y, value.z, value.w);
        }

        private static Quaternion ToQuaternion(Vector4 value)
        {
            return new Quaternion(value.x, value.y, value.z, value.w);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
