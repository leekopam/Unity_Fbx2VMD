using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 매끄러운 휴식 표면의 급접힘과 심한 압축을 역삼각함수 없이 판정함.
    /// </summary>
    internal static class NativeSkinningSurfaceDefectDetector
    {
        internal const float MaximumCorrectableRestAngleDegrees = 30f;
        internal const float MinimumSharpFoldAngleDegrees = 150f;
        private const float SharpFoldMaximumCosine = -0.8660254f;
        private const float MinimumNormalSquaredMagnitude =
            0.0000000000000001f;

        internal static bool IsCorrectable(
            IReadOnlyList<Vector3> vertices,
            NativeSkinningFacePair pair,
            NativeSkinningFacePairRestLengths restLengths,
            float restAngleDegrees,
            float maximumNonSevereCosine)
        {
            if (vertices == null ||
                restAngleDegrees > MaximumCorrectableRestAngleDegrees ||
                !TryCalculateNormalCosine(vertices, pair, out float cosine))
            {
                return false;
            }

            if (cosine < SharpFoldMaximumCosine)
            {
                return true;
            }

            return cosine <= maximumNonSevereCosine &&
                   NativeSkinningRestShapeCollapseDetector.HasSevereCompression(
                       vertices,
                       pair,
                       restLengths);
        }

        private static bool TryCalculateNormalCosine(
            IReadOnlyList<Vector3> vertices,
            NativeSkinningFacePair pair,
            out float cosine)
        {
            cosine = 0f;
            Vector3 firstNormal = Vector3.Cross(
                vertices[pair.FirstEdge] - vertices[pair.FirstOpposite],
                vertices[pair.SecondEdge] - vertices[pair.FirstOpposite]);
            Vector3 secondNormal = Vector3.Cross(
                vertices[pair.SecondEdge] - vertices[pair.SecondOpposite],
                vertices[pair.FirstEdge] - vertices[pair.SecondOpposite]);
            float firstSquaredMagnitude = firstNormal.sqrMagnitude;
            float secondSquaredMagnitude = secondNormal.sqrMagnitude;
            if (firstSquaredMagnitude < MinimumNormalSquaredMagnitude ||
                secondSquaredMagnitude < MinimumNormalSquaredMagnitude)
            {
                return false;
            }

            cosine = Mathf.Clamp(
                Vector3.Dot(firstNormal, secondNormal) /
                Mathf.Sqrt(firstSquaredMagnitude * secondSquaredMagnitude),
                -1f,
                1f);
            return !float.IsNaN(cosine) && !float.IsInfinity(cosine);
        }
    }
}
