using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Tests.Editor.FBXImporter
{
    public class RuntimeFbxMaterialResolverTests
    {
        [Test]
        public void Given_TexturePathIsRelative_When_ResolveTextureCandidate_Then_UsesFbxDirectoryOnly()
        {
            string root = CreateTempRoot();
            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                Directory.CreateDirectory(Path.Combine(fbxDirectory, "tex"));
                string expected = Path.Combine(fbxDirectory, "tex", "body.png");
                File.WriteAllBytes(expected, new byte[] { 1, 2, 3 });

                string result = InvokeResolve(Path.Combine(fbxDirectory, "motion.fbx"), "tex/body.png");

                Assert.That(result, Is.EqualTo(expected));
                Assert.That(result, Does.StartWith(fbxDirectory));
                Assert.That(result, Does.Not.Contain(".."));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void Given_TexturePathNamesSubfolder_When_RootHasSameFileName_Then_UsesReferencedSubfolder()
        {
            string root = CreateTempRoot();
            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                Directory.CreateDirectory(Path.Combine(fbxDirectory, "tex"));
                string rootTexture = Path.Combine(fbxDirectory, "body.png");
                string expected = Path.Combine(fbxDirectory, "tex", "body.png");
                File.WriteAllBytes(rootTexture, new byte[] { 4, 5, 6 });
                File.WriteAllBytes(expected, new byte[] { 1, 2, 3 });

                string result = InvokeResolve(Path.Combine(fbxDirectory, "motion.fbx"), "tex/body.png");

                Assert.That(result, Is.EqualTo(expected));
                Assert.That(result, Is.Not.EqualTo(rootTexture));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void Given_TextureNameMatchesKnownFile_When_ResolveTextureCandidate_Then_ReturnsExistingTexturePath()
        {
            string root = CreateTempRoot();
            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                Directory.CreateDirectory(Path.Combine(fbxDirectory, "Texture2D"));
                string expected = Path.Combine(fbxDirectory, "Texture2D", "Body_D.PNG");
                File.WriteAllBytes(expected, new byte[] { 1, 2, 3 });

                string result = InvokeResolve(Path.Combine(fbxDirectory, "motion.fbx"), "body_d.png");

                Assert.That(result, Is.EqualTo(expected));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void Given_MaterialNameHasOrdinalPrefix_When_TextureNameMatchesMaterialToken_Then_ReturnsTexturePath()
        {
            string root = CreateTempRoot();
            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                Directory.CreateDirectory(Path.Combine(fbxDirectory, "tex"));
                string expected = Path.Combine(fbxDirectory, "tex", "F00_001_01_Body_00_SKIN.png");
                File.WriteAllBytes(expected, new byte[] { 1, 2, 3 });

                string result = InvokeResolveForMaterial(
                    fbxDirectory,
                    "5.2450_F00_001_01_Body_00_SKIN");

                Assert.That(result, Is.EqualTo(expected));
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        [Test]
        public void Given_TexturePathEscapesProject_When_ResolveTextureCandidate_Then_ReturnsEmpty()
        {
            string root = CreateTempRoot();
            try
            {
                string fbxDirectory = Path.Combine(root, "Import_FBX");
                Directory.CreateDirectory(fbxDirectory);
                string outsidePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "body.png");

                string relativeEscape = InvokeResolve(Path.Combine(fbxDirectory, "motion.fbx"), "../body.png");
                string absoluteEscape = InvokeResolve(Path.Combine(fbxDirectory, "motion.fbx"), outsidePath);

                Assert.That(relativeEscape, Is.Empty);
                Assert.That(absoluteEscape, Is.Empty);
            }
            finally
            {
                DeleteTempRoot(root);
            }
        }

        private static string InvokeResolve(string fbxPath, string textureReference)
        {
            Type resolverType = Type.GetType(
                "Fbx2Vmd.Modules.FBXImporter.RuntimeFbxMaterialResolver, Assembly-CSharp");
            Assert.That(resolverType, Is.Not.Null, "RuntimeFbxMaterialResolver must expose a testable pure texture candidate resolver.");

            MethodInfo method = resolverType.GetMethod(
                "ResolveTextureCandidate",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);

            return (string)method.Invoke(null, new object[] { fbxPath, textureReference });
        }

        private static string InvokeResolveForMaterial(string fbxDirectory, string materialName)
        {
            Type resolverType = Type.GetType(
                "Fbx2Vmd.Modules.FBXImporter.RuntimeFbxMaterialResolver, Assembly-CSharp");
            Assert.That(resolverType, Is.Not.Null);

            MethodInfo method = resolverType.GetMethod(
                "ResolveTextureCandidateFromMaterialName",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);

            return (string)method.Invoke(null, new object[] { fbxDirectory, materialName });
        }

        private static string CreateTempRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "UnityFbx2VmdMaterialResolverTests", Guid.NewGuid().ToString("N"));
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
