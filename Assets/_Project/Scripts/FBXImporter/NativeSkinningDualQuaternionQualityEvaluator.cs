using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    /// <summary>
    /// 이중 쿼터니언 후보가 바꾸는 국소 면과 간선만 품질 검사함.
    /// </summary>
    internal sealed class NativeSkinningDualQuaternionQualityEvaluator
    {
        private const float MinimumSignedAreaRatio = 0.05f;
        private const float MinimumNormalSquaredMagnitude = 0.0000000000000001f;
        private readonly FaceReference[] _faces;
        private readonly EdgeReference[] _edges;

        private NativeSkinningDualQuaternionQualityEvaluator(
            FaceReference[] faces,
            EdgeReference[] edges)
        {
            _faces = faces;
            _edges = edges;
        }

        internal static bool TryCreate(
            IReadOnlyList<Vector3> baselineVertices,
            IReadOnlyList<Vector3> restVertices,
            NativeSkinningDualQuaternionBlendContract blendContract,
            out NativeSkinningDualQuaternionQualityEvaluator evaluator)
        {
            evaluator = null;
            NativeSkinningSurfaceContract contract = blendContract?.SurfaceContract;
            if (baselineVertices == null ||
                restVertices == null ||
                contract == null ||
                blendContract.QualityFaceIndices.Length == 0 ||
                blendContract.QualityEdges.Length == 0 ||
                baselineVertices.Count != contract.VertexCount ||
                restVertices.Count != contract.VertexCount)
            {
                return false;
            }

            var faces = new List<FaceReference>();
            foreach (int faceIndex in blendContract.QualityFaceIndices)
            {
                int offset = faceIndex * 3;
                int first = contract.Triangles[offset];
                int second = contract.Triangles[offset + 1];
                int third = contract.Triangles[offset + 2];
                Vector3 baselineCross = Vector3.Cross(
                    baselineVertices[second] - baselineVertices[first],
                    baselineVertices[third] - baselineVertices[first]);
                float baselineArea = baselineCross.magnitude;
                if (baselineCross.sqrMagnitude < MinimumNormalSquaredMagnitude)
                {
                    continue;
                }

                faces.Add(new FaceReference(
                    first,
                    second,
                    third,
                    baselineCross / baselineArea,
                    baselineArea * MinimumSignedAreaRatio));
            }

            EdgeReference[] edges = blendContract.QualityEdges
                .Select(edge => new EdgeReference(
                    edge.First,
                    edge.Second,
                    Vector3.Distance(
                        baselineVertices[edge.First],
                        baselineVertices[edge.Second]),
                    Vector3.Distance(
                        restVertices[edge.First],
                        restVertices[edge.Second])))
                .ToArray();
            evaluator = new NativeSkinningDualQuaternionQualityEvaluator(
                faces.ToArray(),
                edges);
            return true;
        }

        internal void Evaluate(
            IReadOnlyList<Vector3> candidateVertices,
            out float maximumRestEdgeStrainIncrease,
            out int newDegenerateFaceCount,
            out int reversedFaceCount)
        {
            maximumRestEdgeStrainIncrease = 0f;
            newDegenerateFaceCount = 0;
            reversedFaceCount = 0;
            foreach (FaceReference face in _faces)
            {
                Vector3 candidateCross = Vector3.Cross(
                    candidateVertices[face.Second] - candidateVertices[face.First],
                    candidateVertices[face.Third] - candidateVertices[face.First]);
                float signedArea = Vector3.Dot(
                    candidateCross,
                    face.BaselineNormal);
                if (float.IsNaN(signedArea) ||
                    float.IsInfinity(signedArea) ||
                    signedArea < face.MinimumSignedArea)
                {
                    if (signedArea < 0f)
                    {
                        reversedFaceCount++;
                    }
                    else
                    {
                        newDegenerateFaceCount++;
                    }
                }
            }

            foreach (EdgeReference edge in _edges)
            {
                float increase = NativeSkinningRestShapeRecoveryCalculator
                    .CalculateStrainIncrease(
                        edge.BaselineLength,
                        Vector3.Distance(
                            candidateVertices[edge.First],
                            candidateVertices[edge.Second]),
                        edge.RestLength);
                maximumRestEdgeStrainIncrease = Mathf.Max(
                    maximumRestEdgeStrainIncrease,
                    increase);
            }
        }

        private readonly struct FaceReference
        {
            internal FaceReference(
                int first,
                int second,
                int third,
                Vector3 baselineNormal,
                float minimumSignedArea)
            {
                First = first;
                Second = second;
                Third = third;
                BaselineNormal = baselineNormal;
                MinimumSignedArea = minimumSignedArea;
            }

            internal int First { get; }
            internal int Second { get; }
            internal int Third { get; }
            internal Vector3 BaselineNormal { get; }
            internal float MinimumSignedArea { get; }
        }

        private readonly struct EdgeReference
        {
            internal EdgeReference(
                int first,
                int second,
                float baselineLength,
                float restLength)
            {
                First = first;
                Second = second;
                BaselineLength = baselineLength;
                RestLength = restLength;
            }

            internal int First { get; }
            internal int Second { get; }
            internal float BaselineLength { get; }
            internal float RestLength { get; }
        }
    }
}
