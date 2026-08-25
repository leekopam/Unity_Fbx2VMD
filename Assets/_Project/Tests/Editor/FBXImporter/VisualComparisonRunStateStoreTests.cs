using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class VisualComparisonRunStateStoreTests
    {
        [Test]
        public void Given_RunStateJson_When_SavingAndClearing_Then_RoundTripsByKey()
        {
            Type storeType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonRunStateStore",
                throwOnError: false);
            Assert.That(storeType, Is.Not.Null, "모델 중립 비교 실행 상태 저장 경계가 필요합니다.");

            MethodInfo saveMethod = FindMethod(storeType, "SaveJson");
            MethodInfo readMethod = FindMethod(storeType, "ReadJson");
            MethodInfo clearMethod = FindMethod(storeType, "Clear");
            string key = "visual-comparison-state-test-" + Guid.NewGuid().ToString("N");

            try
            {
                saveMethod.Invoke(null, new object[] { key, "{\"running\":true}" });

                Assert.That(readMethod.Invoke(null, new object[] { key }), Is.EqualTo("{\"running\":true}"));
            }
            finally
            {
                clearMethod.Invoke(null, new object[] { key });
            }

            Assert.That(readMethod.Invoke(null, new object[] { key }), Is.EqualTo(string.Empty));
        }

        private static MethodInfo FindMethod(Type storeType, string methodName)
        {
            MethodInfo method = storeType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method;
        }
    }
}
