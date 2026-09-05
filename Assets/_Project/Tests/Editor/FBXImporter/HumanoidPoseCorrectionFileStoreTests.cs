using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidPoseCorrectionFileStoreTests
    {
        private const string DocumentTypeName =
            "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionDocument";
        private const string StoreTypeName =
            "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionFileStore";
        private const float ValueTolerance = 0.0001f;

        [Test]
        public void Given_ValidDocument_When_SavingAndLoading_Then_PreservesCanonicalData()
        {
            string directoryPath = CreateTemporaryDirectory();

            try
            {
                string filePath = Path.Combine(directoryPath, "motion.pose-corrections.json");
                string muscleName = HumanTrait.MuscleName[0];
                object document = CreateDocument("motion", 60f);
                Assert.That(
                    (bool)Invoke(document, "TrySetMuscleDelta", 120, muscleName, 0.125f),
                    Is.True);
                Type storeType = RequireType(StoreTypeName);

                object[] saveArguments = { filePath, document, null };
                Assert.That((bool)InvokeStatic(storeType, "TrySave", saveArguments), Is.True);
                Assert.That(saveArguments[2], Is.EqualTo(string.Empty));
                Assert.That(File.Exists(filePath), Is.True);
                byte[] bytes = File.ReadAllBytes(filePath);
                Assert.That(HasUtf8Bom(bytes), Is.False,
                    "보정 문서는 BOM 없는 UTF-8로 저장해야 합니다.");

                object[] loadArguments = { filePath, null, null };
                Assert.That((bool)InvokeStatic(storeType, "TryLoad", loadArguments), Is.True);
                Assert.That(loadArguments[2], Is.EqualTo(string.Empty));
                object loadedDocument = loadArguments[1];
                Assert.That(loadedDocument, Is.Not.Null);
                Assert.That((int)ReadProperty(loadedDocument, "SchemaVersion"), Is.EqualTo(1));
                Assert.That((string)ReadProperty(loadedDocument, "MotionName"), Is.EqualTo("motion"));
                Assert.That((float)ReadProperty(loadedDocument, "SourceFrameRate"),
                    Is.EqualTo(60f).Within(ValueTolerance));
                Assert.That((int)ReadProperty(loadedDocument, "FrameCount"), Is.EqualTo(1));

                object[] deltaArguments = { 120, muscleName, 0f };
                Assert.That(
                    (bool)Invoke(loadedDocument, "TryGetMuscleDelta", deltaArguments),
                    Is.True);
                Assert.That((float)deltaArguments[2], Is.EqualTo(0.125f).Within(ValueTolerance));
            }
            finally
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }

        [Test]
        public void Given_InvalidDocuments_When_Loading_Then_ReturnsActionableFailure()
        {
            string directoryPath = CreateTemporaryDirectory();

            try
            {
                Type storeType = RequireType(StoreTypeName);
                string filePath = Path.Combine(directoryPath, "invalid.pose-corrections.json");
                File.WriteAllText(filePath, "{ invalid json", new UTF8Encoding(false));

                object[] malformedArguments = { filePath, null, null };
                Assert.That(
                    (bool)InvokeStatic(storeType, "TryLoad", malformedArguments),
                    Is.False);
                Assert.That(malformedArguments[1], Is.Null);
                Assert.That((string)malformedArguments[2], Is.Not.Empty);

                File.WriteAllText(
                    filePath,
                    "{\"_schemaVersion\":999,\"_motionName\":\"motion\"," +
                    "\"_sourceFrameRate\":60,\"_frames\":[]}",
                    new UTF8Encoding(false));
                object[] schemaArguments = { filePath, null, null };
                Assert.That(
                    (bool)InvokeStatic(storeType, "TryLoad", schemaArguments),
                    Is.False);
                Assert.That(schemaArguments[1], Is.Null);
                Assert.That((string)schemaArguments[2], Does.Contain("버전"));

                File.WriteAllText(
                    filePath,
                    "{\"_schemaVersion\":1,\"_motionName\":\"motion\"," +
                    "\"_sourceFrameRate\":60,\"_frames\":[{" +
                    "\"_frameIndex\":1,\"_muscleCorrections\":[{" +
                    "\"_muscleName\":\"Unknown Muscle\",\"_delta\":0.1}]}]}",
                    new UTF8Encoding(false));
                object[] muscleArguments = { filePath, null, null };
                Assert.That(
                    (bool)InvokeStatic(storeType, "TryLoad", muscleArguments),
                    Is.False);
                Assert.That(muscleArguments[1], Is.Null);
                Assert.That((string)muscleArguments[2], Does.Contain("muscle"));
            }
            finally
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(
                Path.GetTempPath(),
                "Fbx2VmdPoseCorrectionTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static object CreateDocument(string motionName, float frameRate)
        {
            Type documentType = RequireType(DocumentTypeName);
            return Activator.CreateInstance(
                documentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { motionName, frameRate },
                culture: null);
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                fullName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{fullName} 타입이 필요합니다.");
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, arguments.Length);
            return method.Invoke(target, arguments);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = FindMethod(type, methodName, arguments.Length);
            return method.Invoke(null, arguments);
        }

        private static MethodInfo FindMethod(Type type, string methodName, int parameterCount)
        {
            MethodInfo method = type
                .GetMethods(BindingFlags.Instance | BindingFlags.Static |
                            BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(candidate =>
                    candidate.Name == methodName &&
                    candidate.GetParameters().Length == parameterCount);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method;
        }

        private static object ReadProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return property.GetValue(target);
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3 &&
                   bytes[0] == 0xEF &&
                   bytes[1] == 0xBB &&
                   bytes[2] == 0xBF;
        }
    }
}
