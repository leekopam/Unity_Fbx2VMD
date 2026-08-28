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
    }
}
