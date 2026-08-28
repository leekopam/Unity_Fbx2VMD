using Assimp;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RuntimeMeshGeometryCalculatorTests
    {
        private const string CalculatorTypeName =
            "Fbx2Vmd.FBXImporter.RuntimeMeshGeometryCalculator";

        [Test]
        public void Given_RuntimeMeshImport_When_CheckingOwnership_Then_DelegatesGeometryCalculation()
        {
            Type calculatorType = typeof(AssimpFBXImporter).Assembly.GetType(CalculatorTypeName);
            string importerSource = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpFBXImporter.cs"));

            Assert.That(calculatorType, Is.Not.Null);
            Assert.That(importerSource, Does.Contain("RuntimeMeshGeometryCalculator.ConvertVertices("));
            Assert.That(importerSource, Does.Contain("RuntimeMeshGeometryCalculator.ConvertNormals("));
            Assert.That(importerSource, Does.Contain("RuntimeMeshGeometryCalculator.ConvertTextureCoordinates("));
            Assert.That(importerSource, Does.Contain("RuntimeMeshGeometryCalculator.BuildTriangleIndices("));
            Assert.That(importerSource, Does.Contain("RuntimeMeshGeometryCalculator.ConvertBindPoseMatrix("));
            Assert.That(importerSource, Does.Not.Contain("foreach (var v in asmMesh.Vertices)"));
            Assert.That(importerSource, Does.Not.Contain("foreach (var n in asmMesh.Normals)"));
            Assert.That(importerSource, Does.Not.Contain("foreach (var uv in asmMesh.TextureCoordinateChannels[0])"));
            Assert.That(importerSource, Does.Not.Contain("foreach (var face in asmMesh.Faces)"));
            Assert.That(importerSource, Does.Not.Contain("private UnityEngine.Matrix4x4 ToUnityMatrix("));
        }

        [Test]
        public void Given_AssimpGeometry_When_Converting_Then_PreservesValuesAndAppliesVertexScaleOnly()
        {
            MethodInfo convertVerticesMethod = FindCalculatorMethod(
                "ConvertVertices",
                typeof(IEnumerable<Vector3D>),
                typeof(float));
            MethodInfo convertNormalsMethod = FindCalculatorMethod(
                "ConvertNormals",
                typeof(IEnumerable<Vector3D>));
            MethodInfo convertTextureCoordinatesMethod = FindCalculatorMethod(
                "ConvertTextureCoordinates",
                typeof(IEnumerable<Vector3D>));
            var source = new List<Vector3D>
            {
                new Vector3D(100f, -200f, 50f),
                new Vector3D(0.25f, 0.75f, 9f)
            };

            var vertices = (List<Vector3>)convertVerticesMethod.Invoke(
                null,
                new object[] { source, 0.01f });
            var normals = (List<Vector3>)convertNormalsMethod.Invoke(
                null,
                new object[] { source });
            var textureCoordinates = (List<Vector2>)convertTextureCoordinatesMethod.Invoke(
                null,
                new object[] { source });

            Assert.That(vertices, Has.Count.EqualTo(2));
            Assert.That(vertices[0], Is.EqualTo(new Vector3(1f, -2f, 0.5f)));
            Assert.That(
                Vector3.Distance(vertices[1], new Vector3(0.0025f, 0.0075f, 0.09f)),
                Is.LessThan(0.000001f));
            Assert.That(normals, Has.Count.EqualTo(2));
            Assert.That(normals[0], Is.EqualTo(new Vector3(100f, -200f, 50f)));
            Assert.That(normals[1], Is.EqualTo(new Vector3(0.25f, 0.75f, 9f)));
            Assert.That(textureCoordinates, Has.Count.EqualTo(2));
            Assert.That(textureCoordinates[0], Is.EqualTo(new Vector2(100f, -200f)));
            Assert.That(textureCoordinates[1], Is.EqualTo(new Vector2(0.25f, 0.75f)));
        }

        [Test]
        public void Given_MixedPolygonFaces_When_BuildingTriangleIndices_Then_FiltersNonTrianglesAndPreservesOrder()
        {
            MethodInfo buildTriangleIndicesMethod = FindCalculatorMethod(
                "BuildTriangleIndices",
                typeof(IEnumerable<Face>));
            var faces = new List<Face>
            {
                new Face(new[] { 0, 1, 2 }),
                new Face(new[] { 3, 4, 5, 6 }),
                new Face(new[] { 7, 8, 9 })
            };

            var indices = (List<int>)buildTriangleIndicesMethod.Invoke(
                null,
                new object[] { faces });

            Assert.That(indices, Is.EqualTo(new[] { 0, 1, 2, 7, 8, 9 }));
        }

        [Test]
        public void Given_AssimpBindPoseMatrix_When_Converting_Then_CopiesElementsAndScalesTranslationOnly()
        {
            MethodInfo convertMethod = FindCalculatorMethod(
                "ConvertBindPoseMatrix",
                typeof(Assimp.Matrix4x4),
                typeof(float));
            var source = new Assimp.Matrix4x4
            {
                A1 = 1f,
                A2 = 2f,
                A3 = 3f,
                A4 = 4f,
                B1 = 5f,
                B2 = 6f,
                B3 = 7f,
                B4 = 8f,
                C1 = 9f,
                C2 = 10f,
                C3 = 11f,
                C4 = 12f,
                D1 = 13f,
                D2 = 14f,
                D3 = 15f,
                D4 = 16f
            };

            var converted = (UnityEngine.Matrix4x4)convertMethod.Invoke(
                null,
                new object[] { source, 0.01f });

            Assert.That(converted.m00, Is.EqualTo(1f));
            Assert.That(converted.m01, Is.EqualTo(2f));
            Assert.That(converted.m02, Is.EqualTo(3f));
            Assert.That(converted.m03, Is.EqualTo(0.04f).Within(0.000001f));
            Assert.That(converted.m10, Is.EqualTo(5f));
            Assert.That(converted.m11, Is.EqualTo(6f));
            Assert.That(converted.m12, Is.EqualTo(7f));
            Assert.That(converted.m13, Is.EqualTo(0.08f).Within(0.000001f));
            Assert.That(converted.m20, Is.EqualTo(9f));
            Assert.That(converted.m21, Is.EqualTo(10f));
            Assert.That(converted.m22, Is.EqualTo(11f));
            Assert.That(converted.m23, Is.EqualTo(0.12f).Within(0.000001f));
            Assert.That(converted.m30, Is.EqualTo(13f));
            Assert.That(converted.m31, Is.EqualTo(14f));
            Assert.That(converted.m32, Is.EqualTo(15f));
            Assert.That(converted.m33, Is.EqualTo(16f));
        }

        private static MethodInfo FindCalculatorMethod(string methodName, params Type[] parameterTypes)
        {
            Type calculatorType = typeof(AssimpFBXImporter).Assembly.GetType(CalculatorTypeName);
            Assert.That(calculatorType, Is.Not.Null);

            MethodInfo method = calculatorType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
