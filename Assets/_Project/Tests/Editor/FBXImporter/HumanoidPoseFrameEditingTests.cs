using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class HumanoidPoseFrameEditingTests
    {
        private const float ValueTolerance = 0.0001f;

        [Test]
        public void Given_ClipTiming_When_ConvertingFrameAndTime_Then_UsesStableRoundedFrame()
        {
            Type calculatorType = RequireType(
                "Fbx2Vmd.FBXImporter.HumanoidMotionFrameCalculator");

            int lastFrameIndex = (int)InvokeStatic(
                calculatorType,
                "CalculateLastFrameIndex",
                2.05f,
                30f);
            int currentFrameIndex = (int)InvokeStatic(
                calculatorType,
                "CalculateFrameIndex",
                1.02f,
                2.05f,
                30f);
            float timeSeconds = (float)InvokeStatic(
                calculatorType,
                "CalculateTimeSeconds",
                currentFrameIndex,
                2.05f,
                30f);

            Assert.That(lastFrameIndex, Is.EqualTo(62));
            Assert.That(currentFrameIndex, Is.EqualTo(31));
            Assert.That(timeSeconds, Is.EqualTo(31f / 30f).Within(ValueTolerance));
        }

        [Test]
        public void Given_MuscleDelta_When_SerializingAndRestoring_Then_PreservesCanonicalData()
        {
            Type documentType = RequireType(
                "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionDocument");
            object document = Activator.CreateInstance(
                documentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { "motion", 30f },
                culture: null);
            string muscleName = HumanTrait.MuscleName[0];

            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", 45, muscleName, 0.2f),
                Is.True);

            string json = JsonUtility.ToJson(document, prettyPrint: true);
            object restored = JsonUtility.FromJson(json, documentType);
            object[] arguments = { 45, muscleName, 0f };

            Assert.That((int)ReadProperty(restored, "SchemaVersion"), Is.EqualTo(1));
            Assert.That((string)ReadProperty(restored, "MotionName"), Is.EqualTo("motion"));
            Assert.That((float)ReadProperty(restored, "SourceFrameRate"),
                Is.EqualTo(30f).Within(ValueTolerance));
            Assert.That((int)ReadProperty(restored, "FrameCount"), Is.EqualTo(1));
            Assert.That((bool)Invoke(restored, "TryGetMuscleDelta", arguments), Is.True);
            Assert.That((float)arguments[2], Is.EqualTo(0.2f).Within(ValueTolerance));
            Assert.That(json, Does.Contain(muscleName),
                "보정 데이터는 모델 본 이름이 아니라 Unity Humanoid muscle 이름을 저장해야 합니다.");
        }

        [Test]
        public void Given_InvalidPoseCorrection_When_Storing_Then_RejectsWithoutMutation()
        {
            Type documentType = RequireType(
                "Fbx2Vmd.FBXImporter.HumanoidPoseCorrectionDocument");
            object document = Activator.CreateInstance(
                documentType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { "motion", 60f },
                culture: null);

            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", -1, HumanTrait.MuscleName[0], 0.1f),
                Is.False);
            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", 1, "Unknown Bone Name", 0.1f),
                Is.False);
            Assert.That(
                (bool)Invoke(document, "TrySetMuscleDelta", 1, HumanTrait.MuscleName[0], float.NaN),
                Is.False);
            Assert.That((int)ReadProperty(document, "FrameCount"), Is.Zero);
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                fullName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{fullName} 타입이 필요합니다.");
            return type;
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(null, arguments);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static object ReadProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return property.GetValue(target);
        }
    }
}
