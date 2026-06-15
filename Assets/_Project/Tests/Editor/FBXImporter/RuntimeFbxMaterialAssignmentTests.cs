using System.Reflection;
using Assimp;
using Member_Han.Modules.FBXImporter;
using NUnit.Framework;
using UnityEngine;
using System;
using System.IO;

namespace Tests.Editor.FBXImporter
{
    public class RuntimeFbxMaterialAssignmentTests
    {
        [Test]
        public void Given_FbxMeshHasMaterialIndex_When_RuntimeImportCreatesRenderer_Then_AssignsNamedMaterial()
        {
            var importer = new RuntimeFBXImporter();
            var target = new GameObject("material-target");

            try
            {
                var scene = new Scene();
                scene.Materials.Add(new Assimp.Material { Name = "expected_body_material" });

                var mesh = new Assimp.Mesh("body_mesh", Assimp.PrimitiveType.Triangle)
                {
                    MaterialIndex = 0
                };
                mesh.Vertices.Add(new Vector3D(0f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(1f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(0f, 1f, 0f));
                mesh.Faces.Add(new Face(new[] { 0, 1, 2 }));

                MethodInfo createMesh = typeof(RuntimeFBXImporter).GetMethod(
                    "CreateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(createMesh, Is.Not.Null);

                createMesh.Invoke(importer, new object[] { target, mesh, scene });

                Renderer renderer = RequireComponent<Renderer>(target);
                Assert.That(renderer.sharedMaterials, Has.Length.GreaterThan(0));
                Assert.That(renderer.sharedMaterials[0], Is.Not.Null);
                Assert.That(renderer.sharedMaterials[0].name, Does.Contain("expected_body_material"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_FbxMaterialHasDiffuseTexture_When_RuntimeImportCreatesRenderer_Then_AssignsMainTexture()
        {
            var importer = new RuntimeFBXImporter();
            var target = new GameObject("texture-target");
            string root = CreateTempRoot();

            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                Directory.CreateDirectory(fbxDirectory);
                string texturePath = Path.Combine(fbxDirectory, "body_diffuse.png");
                WritePng(texturePath, Color.magenta);

                SetSourceDirectory(importer, fbxDirectory);

                var sourceMaterial = new Assimp.Material { Name = "expected_textured_material" };
                var textureSlot = new TextureSlot(
                    "body_diffuse.png",
                    TextureType.Diffuse,
                    0,
                    TextureMapping.FromUV,
                    0,
                    1f,
                    TextureOperation.Add,
                    Assimp.TextureWrapMode.Wrap,
                    Assimp.TextureWrapMode.Wrap,
                    0);
                sourceMaterial.TextureDiffuse = textureSlot;

                var scene = new Scene();
                scene.Materials.Add(sourceMaterial);

                var mesh = new Assimp.Mesh("body_mesh", Assimp.PrimitiveType.Triangle)
                {
                    MaterialIndex = 0
                };
                mesh.Vertices.Add(new Vector3D(0f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(1f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(0f, 1f, 0f));
                mesh.Faces.Add(new Face(new[] { 0, 1, 2 }));

                MethodInfo createMesh = typeof(RuntimeFBXImporter).GetMethod(
                    "CreateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(createMesh, Is.Not.Null);

                createMesh.Invoke(importer, new object[] { target, mesh, scene });

                Renderer renderer = RequireComponent<Renderer>(target);
                Assert.That(renderer.sharedMaterials[0], Is.Not.Null);
                Assert.That(renderer.sharedMaterials[0].mainTexture, Is.Not.Null);
                Assert.That(renderer.sharedMaterials[0].mainTexture.name, Is.EqualTo("body_diffuse.png"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void Given_DiffuseTextureHasTransparentPixels_When_RuntimeImportCreatesRenderer_Then_UsesAlphaCutoutMaterial()
        {
            var importer = new RuntimeFBXImporter();
            var target = new GameObject("alpha-texture-target");
            string root = CreateTempRoot();

            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                Directory.CreateDirectory(fbxDirectory);
                string texturePath = Path.Combine(fbxDirectory, "hair_alpha.png");
                WritePng(
                    texturePath,
                    new[]
                    {
                        new Color(1f, 1f, 1f, 1f),
                        new Color(1f, 1f, 1f, 0f),
                        new Color(1f, 1f, 1f, 1f),
                        new Color(1f, 1f, 1f, 0f)
                    });

                SetSourceDirectory(importer, fbxDirectory);

                var sourceMaterial = new Assimp.Material { Name = "hair_alpha_material" };
                var textureSlot = new TextureSlot(
                    "hair_alpha.png",
                    TextureType.Diffuse,
                    0,
                    TextureMapping.FromUV,
                    0,
                    1f,
                    TextureOperation.Add,
                    Assimp.TextureWrapMode.Wrap,
                    Assimp.TextureWrapMode.Wrap,
                    0);
                sourceMaterial.TextureDiffuse = textureSlot;

                var scene = new Scene();
                scene.Materials.Add(sourceMaterial);

                var mesh = new Assimp.Mesh("hair_mesh", Assimp.PrimitiveType.Triangle)
                {
                    MaterialIndex = 0
                };
                mesh.Vertices.Add(new Vector3D(0f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(1f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(0f, 1f, 0f));
                mesh.Faces.Add(new Face(new[] { 0, 1, 2 }));

                MethodInfo createMesh = typeof(RuntimeFBXImporter).GetMethod(
                    "CreateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(createMesh, Is.Not.Null);

                createMesh.Invoke(importer, new object[] { target, mesh, scene });

                Renderer renderer = RequireComponent<Renderer>(target);
                UnityEngine.Material material = renderer.sharedMaterials[0];
                Assert.That(material, Is.Not.Null);
                Assert.That(material.mainTexture, Is.Not.Null);
                bool usesCutoutShader = material.shader != null
                    && material.shader.name == "Unlit/Transparent Cutout";
                if (material.HasProperty("_Mode"))
                {
                    Assert.That(material.GetFloat("_Mode"), Is.EqualTo(1f).Within(0.0001f));
                }

                Assert.That(
                    usesCutoutShader || material.IsKeywordEnabled("_ALPHATEST_ON"),
                    Is.True);
                Assert.That(material.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.AlphaTest));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void Given_RuntimeMaterialUsesStandardShader_When_RuntimeImportCreatesRenderer_Then_UsesMatteReferenceGlossiness()
        {
            var importer = new RuntimeFBXImporter();
            var target = new GameObject("matte-material-target");

            try
            {
                var scene = new Scene();
                scene.Materials.Add(new Assimp.Material { Name = "matte_reference_material" });

                var mesh = new Assimp.Mesh("body_mesh", Assimp.PrimitiveType.Triangle)
                {
                    MaterialIndex = 0
                };
                mesh.Vertices.Add(new Vector3D(0f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(1f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(0f, 1f, 0f));
                mesh.Faces.Add(new Face(new[] { 0, 1, 2 }));

                MethodInfo createMesh = typeof(RuntimeFBXImporter).GetMethod(
                    "CreateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(createMesh, Is.Not.Null);

                createMesh.Invoke(importer, new object[] { target, mesh, scene });

                Renderer renderer = RequireComponent<Renderer>(target);
                Assert.That(renderer.sharedMaterials[0], Is.Not.Null);
                Assert.That(renderer.sharedMaterials[0].HasProperty("_Glossiness"), Is.True);
                Assert.That(renderer.sharedMaterials[0].GetFloat("_Glossiness"), Is.EqualTo(0f).Within(0.0001f));
                Assert.That(renderer.sharedMaterials[0].HasProperty("_Metallic"), Is.True);
                Assert.That(renderer.sharedMaterials[0].GetFloat("_Metallic"), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_NodeHasMultipleSkinnedMeshes_When_RuntimeImportCreatesRenderers_Then_UsesSeparateMeshObjects()
        {
            var importer = new RuntimeFBXImporter();
            var target = new GameObject("multi-skinned-mesh-target");
            var bone = new GameObject("Bone");
            bone.transform.SetParent(target.transform, false);

            try
            {
                SetNodeMap(importer, bone.transform);

                var scene = new Scene();
                scene.Materials.Add(new Assimp.Material { Name = "first_material" });
                scene.Materials.Add(new Assimp.Material { Name = "second_material" });

                Assimp.Mesh firstMesh = CreateSkinnedTriangleMesh("first_mesh", 0);
                Assimp.Mesh secondMesh = CreateSkinnedTriangleMesh("second_mesh", 1);

                MethodInfo createMesh = typeof(RuntimeFBXImporter).GetMethod(
                    "CreateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(createMesh, Is.Not.Null);

                Assert.DoesNotThrow(() => createMesh.Invoke(importer, new object[] { target, firstMesh, scene }));
                Assert.DoesNotThrow(() => createMesh.Invoke(importer, new object[] { target, secondMesh, scene }));

                var renderers = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderers, Has.Length.EqualTo(2));
                Assert.That(target.GetComponents<SkinnedMeshRenderer>(), Has.Length.EqualTo(1));
                Assert.That(target.transform.Find("second_mesh"), Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_FbxMaterialHasNoDiffuseSlotButNameMatchesTexture_When_RuntimeImportCreatesRenderer_Then_AssignsMainTexture()
        {
            var importer = new RuntimeFBXImporter();
            var target = new GameObject("material-name-texture-target");
            string root = CreateTempRoot();

            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                string textureDirectory = Path.Combine(fbxDirectory, "tex");
                Directory.CreateDirectory(textureDirectory);
                string texturePath = Path.Combine(textureDirectory, "F00_001_01_Body_00_SKIN.png");
                WritePng(texturePath, Color.cyan);

                SetSourceDirectory(importer, fbxDirectory);

                var scene = new Scene();
                scene.Materials.Add(new Assimp.Material { Name = "5.2450_F00_001_01_Body_00_SKIN" });

                var mesh = new Assimp.Mesh("body_mesh", Assimp.PrimitiveType.Triangle)
                {
                    MaterialIndex = 0
                };
                mesh.Vertices.Add(new Vector3D(0f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(1f, 0f, 0f));
                mesh.Vertices.Add(new Vector3D(0f, 1f, 0f));
                mesh.Faces.Add(new Face(new[] { 0, 1, 2 }));

                MethodInfo createMesh = typeof(RuntimeFBXImporter).GetMethod(
                    "CreateMesh",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(createMesh, Is.Not.Null);

                createMesh.Invoke(importer, new object[] { target, mesh, scene });

                Renderer renderer = RequireComponent<Renderer>(target);
                Assert.That(renderer.sharedMaterials[0], Is.Not.Null);
                Assert.That(renderer.sharedMaterials[0].mainTexture, Is.Not.Null);
                Assert.That(renderer.sharedMaterials[0].mainTexture.name, Is.EqualTo("F00_001_01_Body_00_SKIN.png"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                DeleteTempRoot(root);
            }
        }

        private static T RequireComponent<T>(GameObject gameObject) where T : Component
        {
            T[] components = gameObject.GetComponents<T>();
            Assert.That(components, Has.Length.GreaterThan(0), $"{gameObject.name} must have {typeof(T).Name}.");
            return components[0];
        }

        private static void SetSourceDirectory(RuntimeFBXImporter importer, string fbxDirectory)
        {
            FieldInfo sourceDirectory = typeof(RuntimeFBXImporter).GetField(
                "_sourceDirectory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(sourceDirectory, Is.Not.Null, "RuntimeFBXImporter must keep the FBX source directory for texture resolving.");
            sourceDirectory.SetValue(importer, fbxDirectory);
        }

        private static void SetNodeMap(RuntimeFBXImporter importer, Transform bone)
        {
            FieldInfo nodeMapField = typeof(RuntimeFBXImporter).GetField(
                "_nodeMap",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(nodeMapField, Is.Not.Null);

            var nodeMap = new System.Collections.Generic.Dictionary<string, Transform>
            {
                { bone.name, bone }
            };
            nodeMapField.SetValue(importer, nodeMap);
        }

        private static Assimp.Mesh CreateSkinnedTriangleMesh(string name, int materialIndex)
        {
            var mesh = new Assimp.Mesh(name, Assimp.PrimitiveType.Triangle)
            {
                MaterialIndex = materialIndex
            };
            mesh.Vertices.Add(new Vector3D(0f, 0f, 0f));
            mesh.Vertices.Add(new Vector3D(1f, 0f, 0f));
            mesh.Vertices.Add(new Vector3D(0f, 1f, 0f));
            mesh.Faces.Add(new Face(new[] { 0, 1, 2 }));

            var bone = new Bone { Name = "Bone" };
            bone.VertexWeights.Add(new VertexWeight(0, 1f));
            bone.VertexWeights.Add(new VertexWeight(1, 1f));
            bone.VertexWeights.Add(new VertexWeight(2, 1f));
            mesh.Bones.Add(bone);
            return mesh;
        }

        private static void WritePng(string path, Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void WritePng(string path, Color[] colors)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(colors);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "UnityFbx2VmdMaterialAssignmentTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }

        private static void DeleteTempRoot(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            Directory.Delete(root, true);
        }
    }
}
