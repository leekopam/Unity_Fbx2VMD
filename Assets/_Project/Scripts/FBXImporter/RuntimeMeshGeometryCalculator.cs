using Assimp;
using System.Collections.Generic;
using UnityEngine;

namespace Fbx2Vmd.FBXImporter
{
    internal static class RuntimeMeshGeometryCalculator
    {
        internal static List<Vector3> ConvertVertices(
            IEnumerable<Vector3D> vertices,
            float unitScale)
        {
            var convertedVertices = new List<Vector3>();
            foreach (Vector3D vertex in vertices)
            {
                convertedVertices.Add(
                    new Vector3(vertex.X, vertex.Y, vertex.Z) * unitScale);
            }

            return convertedVertices;
        }

        internal static List<Vector3> ConvertNormals(IEnumerable<Vector3D> normals)
        {
            var convertedNormals = new List<Vector3>();
            foreach (Vector3D normal in normals)
            {
                convertedNormals.Add(new Vector3(normal.X, normal.Y, normal.Z));
            }

            return convertedNormals;
        }

        internal static List<Vector2> ConvertTextureCoordinates(
            IEnumerable<Vector3D> textureCoordinates)
        {
            var convertedTextureCoordinates = new List<Vector2>();
            foreach (Vector3D textureCoordinate in textureCoordinates)
            {
                convertedTextureCoordinates.Add(
                    new Vector2(textureCoordinate.X, textureCoordinate.Y));
            }

            return convertedTextureCoordinates;
        }

        internal static List<int> BuildTriangleIndices(IEnumerable<Face> faces)
        {
            var triangleIndices = new List<int>();
            foreach (Face face in faces)
            {
                if (face.IndexCount != 3)
                {
                    continue;
                }

                triangleIndices.Add(face.Indices[0]);
                triangleIndices.Add(face.Indices[1]);
                triangleIndices.Add(face.Indices[2]);
            }

            return triangleIndices;
        }

        internal static UnityEngine.Matrix4x4 ConvertBindPoseMatrix(
            Assimp.Matrix4x4 source,
            float unitScale)
        {
            var converted = new UnityEngine.Matrix4x4
            {
                m00 = source.A1,
                m01 = source.A2,
                m02 = source.A3,
                m03 = source.A4 * unitScale,
                m10 = source.B1,
                m11 = source.B2,
                m12 = source.B3,
                m13 = source.B4 * unitScale,
                m20 = source.C1,
                m21 = source.C2,
                m22 = source.C3,
                m23 = source.C4 * unitScale,
                m30 = source.D1,
                m31 = source.D2,
                m32 = source.D3,
                m33 = source.D4
            };

            return converted;
        }
    }
}
