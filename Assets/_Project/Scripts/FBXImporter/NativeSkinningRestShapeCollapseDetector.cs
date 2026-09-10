using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 기준 자세 대비 국소 면 쌍의 심한 엣지 압축을 크기 독립적으로 판정함.
    /// </summary>
    internal static class NativeSkinningRestShapeCollapseDetector
    {
        private const float MinimumRestEdgeLength = 0.00000001f;
        private const float MinimumSevereAngleIncreaseDegrees = 90f;
        private const float MinimumSevereCompressionRatio = 0.9f;

        internal static NativeSkinningFacePairRestLengths MeasureRestLengths(
            IReadOnlyList<Vector3> restVertices,
            NativeSkinningFacePair pair)
        {
            return new NativeSkinningFacePairRestLengths(
                MeasureLength(restVertices, pair.FirstEdge, pair.SecondEdge),
                MeasureLength(restVertices, pair.FirstOpposite, pair.FirstEdge),
                MeasureLength(restVertices, pair.FirstOpposite, pair.SecondEdge),
                MeasureLength(restVertices, pair.SecondOpposite, pair.FirstEdge),
                MeasureLength(restVertices, pair.SecondOpposite, pair.SecondEdge));
        }

        internal static float CalculateMinimumNonSevereRestLengthRatio()
        {
            return 1f - MinimumSevereCompressionRatio;
        }

        internal static bool IsSeverelyCollapsed(
            IReadOnlyList<Vector3> posedVertices,
            NativeSkinningFacePair pair,
            NativeSkinningFacePairRestLengths restLengths,
            float restAngleDegrees,
            float posedAngleDegrees)
        {
            return posedAngleDegrees - restAngleDegrees >=
                       MinimumSevereAngleIncreaseDegrees &&
                   HasSevereCompression(
                       posedVertices,
                       pair,
                       restLengths);
        }

        internal static bool HasSevereCompression(
            IReadOnlyList<Vector3> posedVertices,
            NativeSkinningFacePair pair,
            NativeSkinningFacePairRestLengths restLengths)
        {
            return CalculateMaximumCompressionRatio(
                       posedVertices,
                       pair,
                       restLengths) >= MinimumSevereCompressionRatio;
        }

        private static float CalculateMaximumCompressionRatio(
            IReadOnlyList<Vector3> posedVertices,
            NativeSkinningFacePair pair,
            NativeSkinningFacePairRestLengths restLengths)
        {
            float maximumCompression = 0f;
            maximumCompression = Mathf.Max(
                maximumCompression,
                CalculateCompressionRatio(
                    posedVertices,
                    pair.FirstEdge,
                    pair.SecondEdge,
                    restLengths.SharedEdge));
            maximumCompression = Mathf.Max(
                maximumCompression,
                CalculateCompressionRatio(
                    posedVertices,
                    pair.FirstOpposite,
                    pair.FirstEdge,
                    restLengths.FirstOppositeToFirstEdge));
            maximumCompression = Mathf.Max(
                maximumCompression,
                CalculateCompressionRatio(
                    posedVertices,
                    pair.FirstOpposite,
                    pair.SecondEdge,
                    restLengths.FirstOppositeToSecondEdge));
            maximumCompression = Mathf.Max(
                maximumCompression,
                CalculateCompressionRatio(
                    posedVertices,
                    pair.SecondOpposite,
                    pair.FirstEdge,
                    restLengths.SecondOppositeToFirstEdge));
            maximumCompression = Mathf.Max(
                maximumCompression,
                CalculateCompressionRatio(
                    posedVertices,
                    pair.SecondOpposite,
                    pair.SecondEdge,
                    restLengths.SecondOppositeToSecondEdge));
            return maximumCompression;
        }

        private static float MeasureLength(
            IReadOnlyList<Vector3> vertices,
            int firstVertex,
            int secondVertex)
        {
            return Vector3.Distance(vertices[firstVertex], vertices[secondVertex]);
        }

        private static float CalculateCompressionRatio(
            IReadOnlyList<Vector3> posedVertices,
            int firstVertex,
            int secondVertex,
            float restLength)
        {
            if (restLength <= MinimumRestEdgeLength)
            {
                return 0f;
            }

            float posedLength = Vector3.Distance(
                posedVertices[firstVertex],
                posedVertices[secondVertex]);
            return Mathf.Max(0f, 1f - posedLength / restLength);
        }
    }
}
