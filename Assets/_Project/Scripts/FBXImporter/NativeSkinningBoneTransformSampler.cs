using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 현재 본 자세를 SkinnedMeshRenderer 로컬 공간의 스키닝 행렬로 표본화함.
    /// </summary>
    internal static class NativeSkinningBoneTransformSampler
    {
        internal static bool TrySample(
            SkinnedMeshRenderer renderer,
            out Matrix4x4[] skinningMatrices)
        {
            skinningMatrices = null;
            if (renderer == null || renderer.sharedMesh == null)
            {
                return false;
            }

            Transform[] bones = renderer.bones;
            Matrix4x4[] bindPoses = renderer.sharedMesh.bindposes;
            if (bones == null ||
                bindPoses == null ||
                bones.Length == 0 ||
                bones.Length != bindPoses.Length)
            {
                return false;
            }

            Matrix4x4 rendererWorldToLocal = renderer.transform.worldToLocalMatrix;
            var sampledMatrices = new Matrix4x4[bones.Length];
            for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
            {
                if (bones[boneIndex] == null)
                {
                    return false;
                }

                Matrix4x4 matrix = rendererWorldToLocal *
                    bones[boneIndex].localToWorldMatrix *
                    bindPoses[boneIndex];
                if (!IsFinite(matrix))
                {
                    return false;
                }
                sampledMatrices[boneIndex] = matrix;
            }

            skinningMatrices = sampledMatrices;
            return true;
        }

        private static bool IsFinite(Matrix4x4 matrix)
        {
            for (int columnIndex = 0; columnIndex < 4; columnIndex++)
            {
                Vector4 column = matrix.GetColumn(columnIndex);
                if (!IsFinite(column.x) ||
                    !IsFinite(column.y) ||
                    !IsFinite(column.z) ||
                    !IsFinite(column.w))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
