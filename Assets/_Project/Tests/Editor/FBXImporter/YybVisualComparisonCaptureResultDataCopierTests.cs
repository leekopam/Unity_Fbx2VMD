using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonCaptureResultDataCopierTests
    {
        [Test]
        public void Given_ResultData_When_Copying_Then_CopiesEveryPublicField()
        {
            Assembly runtimeAssembly = typeof(FBXVmdPipeline).Assembly;
            Type dataType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonCaptureResultData",
                throwOnError: true);
            Type copierType = runtimeAssembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonCaptureResultDataCopier",
                throwOnError: false);
            Assert.That(copierType, Is.Not.Null, "YYB 캡처 결과 데이터 복사 책임을 분리해야 합니다.");

            object source = Activator.CreateInstance(dataType, nonPublic: true);
            object destination = Activator.CreateInstance(dataType, nonPublic: true);
            FieldInfo[] fields = dataType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo field in fields)
            {
                field.SetValue(source, CreateSampleValue(field.FieldType, field.Name));
            }

            MethodInfo copyMethod = copierType.GetMethod(
                "Copy",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(copyMethod, Is.Not.Null);
            copyMethod.Invoke(null, new[] { source, destination });

            foreach (FieldInfo field in fields)
            {
                Assert.That(
                    field.GetValue(destination),
                    Is.EqualTo(field.GetValue(source)),
                    $"복사 누락 필드: {field.Name}");
            }
        }

        private static object CreateSampleValue(Type type, string fieldName)
        {
            if (type == typeof(string))
            {
                return fieldName;
            }

            if (type == typeof(bool))
            {
                return true;
            }

            if (type == typeof(int))
            {
                return 17;
            }

            if (type == typeof(long))
            {
                return 29L;
            }

            if (type == typeof(float))
            {
                return fieldName.Length + 0.5f;
            }

            Assert.Fail($"지원하지 않는 필드 타입: {type.FullName}");
            return null;
        }
    }
}
