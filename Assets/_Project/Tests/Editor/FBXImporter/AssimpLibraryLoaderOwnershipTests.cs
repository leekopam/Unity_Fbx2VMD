using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Tests.Editor.FBXImporter
{
    public class AssimpLibraryLoaderOwnershipTests
    {
        [Test]
        public void Given_LibraryLoader_When_CheckingFileOwnership_Then_TypeHasDedicatedFile()
        {
            string importerPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpFBXImporter.cs");
            string loaderPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                "AssimpLibraryLoader.cs");

            Assert.That(File.Exists(loaderPath), Is.True);
            Assert.That(File.ReadAllText(importerPath), Does.Not.Contain("class AssimpLibraryLoader"));
            Assert.That(File.ReadAllText(loaderPath), Does.Contain("public static class AssimpLibraryLoader"));
        }

        [Test]
        public void Given_LibraryLoader_When_InspectingPublicMembers_Then_PreservesApi()
        {
            FieldInfo isLoadedField = typeof(AssimpLibraryLoader).GetField(
                "IsLoaded",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo loadMethod = typeof(AssimpLibraryLoader).GetMethod(
                "LoadLibrary",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);

            Assert.That(isLoadedField, Is.Not.Null);
            Assert.That(isLoadedField.FieldType, Is.EqualTo(typeof(bool)));
            Assert.That(isLoadedField.IsInitOnly, Is.False);
            Assert.That(loadMethod, Is.Not.Null);
            Assert.That(loadMethod.ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void Given_NativeLoadMethod_When_InspectingInteropContract_Then_PreservesWindowsSignature()
        {
            MethodInfo nativeLoadMethod = typeof(AssimpLibraryLoader).GetMethod(
                "LoadLibrary",
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);
            DllImportAttribute attribute = nativeLoadMethod?.GetCustomAttribute<DllImportAttribute>();

            Assert.That(nativeLoadMethod, Is.Not.Null);
            Assert.That(nativeLoadMethod.ReturnType, Is.EqualTo(typeof(IntPtr)));
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.Value, Is.EqualTo("kernel32"));
            Assert.That(attribute.SetLastError, Is.True);
            Assert.That(attribute.CharSet, Is.EqualTo(CharSet.Unicode));
        }

        [Test]
        public void Given_LibraryLoader_When_InspectingCallSites_Then_PreservesPreloadGuards()
        {
            string importerSource = ReadFbxImporterSource("AssimpFBXImporter.cs");
            string inspectorSource = ReadFbxImporterSource("AssimpAnimationInspector.cs");
            string comparisonRunnerSource = ReadFbxImporterSource("FbxRuntimePoseClipCompareRunner.cs");

            Assert.That(Regex.Matches(importerSource, @"AssimpLibraryLoader\.IsLoaded").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(importerSource, @"AssimpLibraryLoader\.LoadLibrary\(\)").Count, Is.EqualTo(2));
            Assert.That(Regex.Matches(inspectorSource, @"AssimpLibraryLoader\.IsLoaded").Count, Is.EqualTo(1));
            Assert.That(Regex.Matches(inspectorSource, @"AssimpLibraryLoader\.LoadLibrary\(\)").Count, Is.EqualTo(1));
            Assert.That(Regex.Matches(comparisonRunnerSource, @"AssimpLibraryLoader\.IsLoaded").Count, Is.EqualTo(1));
            Assert.That(Regex.Matches(comparisonRunnerSource, @"AssimpLibraryLoader\.LoadLibrary\(\)").Count, Is.EqualTo(1));
        }

        [Test]
        public void Given_LibraryLoader_When_InspectingCandidatePaths_Then_PreservesSearchOrder()
        {
            string loaderSource = ReadFbxImporterSource("AssimpLibraryLoader.cs");
            const string pluginPath =
                "Path.Combine(Application.dataPath, \"Plugins\", ASSIMP_PLUGIN_FOLDER, ASSIMP_DLL_NAME)";
            const string directPluginPath =
                "Path.Combine(Application.dataPath, \"Plugins\", ASSIMP_DLL_NAME)";
            const string architecturePath =
                "Path.Combine(Application.dataPath, \"Plugins\", \"x86_64\", ASSIMP_DLL_NAME)";
            int firstPluginPathIndex = loaderSource.IndexOf(pluginPath, StringComparison.Ordinal);
            int directPluginPathIndex = loaderSource.IndexOf(directPluginPath, StringComparison.Ordinal);
            int architecturePathIndex = loaderSource.IndexOf(architecturePath, StringComparison.Ordinal);
            int lastPluginPathIndex = loaderSource.LastIndexOf(pluginPath, StringComparison.Ordinal);

            Assert.That(firstPluginPathIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(firstPluginPathIndex, Is.LessThan(directPluginPathIndex));
            Assert.That(directPluginPathIndex, Is.LessThan(architecturePathIndex));
            Assert.That(architecturePathIndex, Is.LessThan(lastPluginPathIndex));
        }

        private static string ReadFbxImporterSource(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets",
                "_Project",
                "Scripts",
                "FBXImporter",
                fileName));
        }
    }
}
