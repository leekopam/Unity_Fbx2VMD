using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class RuntimeFbxImportGeometryTests
    {
        private const string SampleFbxPath = "Assets/Plugins/VMDRecorderSample/Models/TestModel/test.fbx";
        private const float BoundsSizeToleranceRatio = 0.05f;

        [Test]
        public void Given_RuntimeGeometryImplementation_When_CheckingDeadCode_Then_UsesOnlyRequiredValues()
        {
            MethodInfo hierarchyMethod = typeof(AssimpFBXImporter).GetMethod(
                "BuildHierarchy",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Assimp.Node), typeof(Transform) },
                modifiers: null);
            MethodInfo legacyHierarchyMethod = typeof(AssimpFBXImporter).GetMethod(
                "BuildHierarchy",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Assimp.Node), typeof(Transform), typeof(Assimp.Scene) },
                modifiers: null);
            MethodInfo setupStaticMeshMethod = typeof(AssimpFBXImporter).GetMethod(
                "SetupStaticMesh",
                BindingFlags.Instance | BindingFlags.NonPublic);
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpFBXImporter.cs"));
            var meshObject = new GameObject("StaticMesh");
            var mesh = new UnityEngine.Mesh();

            try
            {
                Assert.That(hierarchyMethod, Is.Not.Null);
                Assert.That(legacyHierarchyMethod, Is.Null);
                Assert.That(setupStaticMeshMethod, Is.Not.Null);
                Assert.That(source, Does.Not.Contain("MeshRenderer mr = go.AddComponent<MeshRenderer>();"));

                setupStaticMeshMethod.Invoke(
                    new AssimpFBXImporter(),
                    new object[] { meshObject, mesh });

                Assert.That(meshObject.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(meshObject.GetComponent<MeshRenderer>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(meshObject);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Given_TextureSampleFbx_When_RuntimeImportCreatesSkinnedMeshes_Then_UsesUnityImporterWorldScale()
        {
            GameObject referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SampleFbxPath);
            Assert.That(referencePrefab, Is.Not.Null, "Sample FBX prefab must be available for Unity importer scale comparison.");

            GameObject reference = (GameObject)PrefabUtility.InstantiatePrefab(referencePrefab);
            GameObject runtime = null;

            try
            {
                runtime = new AssimpFBXImporter().ImportSynchronouslyForEditorDiagnostics(SampleFbxPath);
                Assert.That(runtime, Is.Not.Null);

                Bounds referenceBounds = CalculateCombinedRendererBounds(reference);
                Bounds runtimeBounds = CalculateCombinedRendererBounds(runtime);

                Assert.That(referenceBounds.size.y, Is.GreaterThan(0f));
                Assert.That(runtimeBounds.size.x, Is.EqualTo(referenceBounds.size.x).Within(referenceBounds.size.x * BoundsSizeToleranceRatio));
                Assert.That(runtimeBounds.size.y, Is.EqualTo(referenceBounds.size.y).Within(referenceBounds.size.y * BoundsSizeToleranceRatio));
                Assert.That(runtimeBounds.size.z, Is.EqualTo(referenceBounds.size.z).Within(referenceBounds.size.z * BoundsSizeToleranceRatio));
            }
            finally
            {
                if (reference != null)
                {
                    Object.DestroyImmediate(reference);
                }

                if (runtime != null)
                {
                    Object.DestroyImmediate(runtime);
                }
            }
        }

        [Test]
        public void Given_TextureSampleFbx_When_RuntimeImportBakesSkinnedMeshes_Then_BakedVertexBoundsMatchUnityImporter()
        {
            GameObject referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SampleFbxPath);
            Assert.That(referencePrefab, Is.Not.Null, "Sample FBX prefab must be available for Unity importer baked bounds comparison.");

            GameObject reference = (GameObject)PrefabUtility.InstantiatePrefab(referencePrefab);
            GameObject runtime = null;

            try
            {
                runtime = new AssimpFBXImporter().ImportSynchronouslyForEditorDiagnostics(SampleFbxPath);
                Assert.That(runtime, Is.Not.Null);

                Bounds referenceBounds = CalculateCombinedBakedVertexBounds(reference);
                Bounds runtimeBounds = CalculateCombinedBakedVertexBounds(runtime);

                Assert.That(referenceBounds.size.y, Is.GreaterThan(0f));
                Assert.That(runtimeBounds.size.x, Is.EqualTo(referenceBounds.size.x).Within(referenceBounds.size.x * BoundsSizeToleranceRatio));
                Assert.That(runtimeBounds.size.y, Is.EqualTo(referenceBounds.size.y).Within(referenceBounds.size.y * BoundsSizeToleranceRatio));
                Assert.That(runtimeBounds.size.z, Is.EqualTo(referenceBounds.size.z).Within(referenceBounds.size.z * BoundsSizeToleranceRatio));
            }
            finally
            {
                if (reference != null)
                {
                    Object.DestroyImmediate(reference);
                }

                if (runtime != null)
                {
                    Object.DestroyImmediate(runtime);
                }
            }
        }

        [Test]
        public void Given_TextureSampleFbx_When_RuntimeImportCreatesMaterials_Then_MatchesUnityImporterCutoutShader()
        {
            GameObject referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SampleFbxPath);
            Assert.That(referencePrefab, Is.Not.Null, "Sample FBX prefab must be available for Unity importer material comparison.");

            GameObject reference = (GameObject)PrefabUtility.InstantiatePrefab(referencePrefab);
            GameObject runtime = null;

            try
            {
                runtime = new AssimpFBXImporter().ImportSynchronouslyForEditorDiagnostics(SampleFbxPath);
                Assert.That(runtime, Is.Not.Null);

                Material[] referenceMaterials = GetRendererMaterials(reference);
                Material[] runtimeMaterials = GetRendererMaterials(runtime);

                Assert.That(referenceMaterials, Has.Length.EqualTo(15));
                Assert.That(runtimeMaterials, Has.Length.EqualTo(referenceMaterials.Length));

                string expectedShader = referenceMaterials[0].shader.name;
                int expectedRenderQueue = referenceMaterials[0].renderQueue;
                Assert.That(expectedShader, Is.EqualTo("Unlit/Transparent Cutout"));
                Assert.That(expectedRenderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.AlphaTest));

                foreach (Material material in runtimeMaterials)
                {
                    Assert.That(material.mainTexture, Is.Not.Null, $"{material.name} must keep the restored runtime texture.");
                    Assert.That(material.shader.name, Is.EqualTo(expectedShader), $"{material.name} must match Unity importer shader parity.");
                    Assert.That(material.renderQueue, Is.EqualTo(expectedRenderQueue), $"{material.name} must match Unity importer cutout queue.");
                }
            }
            finally
            {
                if (reference != null)
                {
                    Object.DestroyImmediate(reference);
                }

                if (runtime != null)
                {
                    Object.DestroyImmediate(runtime);
                }
            }
        }

        private static Bounds CalculateCombinedRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }

            return combined;
        }

        private static Bounds CalculateCombinedBakedVertexBounds(GameObject root)
        {
            SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(renderers, Is.Not.Empty);

            bool hasBounds = false;
            Bounds combined = default;
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                renderer.updateWhenOffscreen = true;
                renderer.forceMatrixRecalculationPerRender = true;

                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked, true);
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        Vector3 worldVertex = renderer.transform.TransformPoint(vertex);
                        if (!hasBounds)
                        {
                            combined = new Bounds(worldVertex, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            combined.Encapsulate(worldVertex);
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }

            Assert.That(hasBounds, Is.True);
            return combined;
        }

        private static Material[] GetRendererMaterials(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);

            var materials = new System.Collections.Generic.List<Material>();
            foreach (Renderer renderer in renderers)
            {
                materials.AddRange(renderer.sharedMaterials);
            }

            return materials.FindAll(material => material != null).ToArray();
        }
    }
}
