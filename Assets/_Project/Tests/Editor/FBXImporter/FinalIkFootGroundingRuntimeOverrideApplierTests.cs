using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Tests.Editor.FBXImporter
{
    public class FinalIkFootGroundingRuntimeOverrideApplierTests
    {
        [Test]
        public void Given_MissingPipeline_When_DisablingGrounding_Then_ReturnsFalse()
        {
            Type applierType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FinalIkFootGroundingRuntimeOverrideApplier",
                throwOnError: false);
            Assert.That(applierType, Is.Not.Null, "모델 중립 FinalIK 접지 override 적용기가 필요합니다.");

            MethodInfo applyMethod = applierType.GetMethod(
                "Apply",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(applyMethod, Is.Not.Null);

            bool applied = (bool)applyMethod.Invoke(null, new object[] { null, false });

            Assert.That(applied, Is.False);
        }
    }
}
