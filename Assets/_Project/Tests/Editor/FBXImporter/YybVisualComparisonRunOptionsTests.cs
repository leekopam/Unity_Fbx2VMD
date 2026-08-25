using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class YybVisualComparisonRunOptionsTests
    {
        [Test]
        public void Given_RunState_When_Serializing_Then_PreservesInheritedRunOptions()
        {
            Type runtimeType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: false);
            Assert.That(runtimeType, Is.Not.Null, "YYB 비교 실행 옵션 경계가 필요합니다.");

            Type stateType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunStateData",
                throwOnError: true);
            Assert.That(stateType.BaseType, Is.EqualTo(runtimeType));

            object state = Activator.CreateInstance(stateType, nonPublic: true);
            runtimeType.GetField("fbxFileName").SetValue(state, "future-model-motion.fbx");
            runtimeType.GetField("enableYybArmSwingLimitRuntimeOverride").SetValue(state, true);

            string json = JsonUtility.ToJson(state);

            Assert.That(json, Does.Contain("\"fbxFileName\":\"future-model-motion.fbx\""));
            Assert.That(json, Does.Contain("\"enableYybArmSwingLimitRuntimeOverride\":true"));
        }

        [Test]
        public void Given_StartRun_When_CheckingSignature_Then_AcceptsSingleRunOptionsObject()
        {
            Type runtimeType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            MethodInfo startRun = typeof(YybVisualComparisonBatchRunner)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method => method.Name == "StartRun");

            ParameterInfo[] parameters = startRun.GetParameters();

            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(runtimeType));
        }
    }
}
