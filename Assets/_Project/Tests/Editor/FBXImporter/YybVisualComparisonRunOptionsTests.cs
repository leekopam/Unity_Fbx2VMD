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

        [Test]
        public void Given_YybRunOptions_When_CheckingOwnership_Then_GenericFieldsBelongToBaseOptions()
        {
            Type assemblyMarker = typeof(FBXVmdPipeline);
            Type genericType = assemblyMarker.Assembly.GetType(
                "Fbx2Vmd.FBXImporter.VisualComparisonRunOptions",
                throwOnError: false);
            Type yybType = assemblyMarker.Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            Assert.That(genericType, Is.Not.Null, "모델 중립 비교 실행 옵션 경계가 필요합니다.");
            Assert.That(yybType.BaseType, Is.EqualTo(genericType));

            BindingFlags declaredFields =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly;
            Assert.That(genericType.GetField("fbxFileName", declaredFields), Is.Not.Null);
            Assert.That(
                genericType.GetField("enableYybArmSwingLimitRuntimeOverride", declaredFields),
                Is.Null);
            Assert.That(
                yybType.GetField("enableYybArmSwingLimitRuntimeOverride", declaredFields),
                Is.Not.Null);
        }

        [Test]
        public void Given_NormalizedOptions_When_CheckingRunner_Then_UsesSingleApplyBoundary()
        {
            Type optionsType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            MethodInfo applyMethod = typeof(YybVisualComparisonBatchRunner).GetMethod(
                "ApplyRunOptions",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(applyMethod, Is.Not.Null, "정규화된 실행 옵션 적용 경계가 필요합니다.");

            ParameterInfo[] parameters = applyMethod.GetParameters();

            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(optionsType));
        }

        [Test]
        public void Given_RunOptions_When_CheckingRunnerState_Then_DoesNotMirrorOptionFieldsAsStatics()
        {
            Type optionsType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
            string[] mirroredFieldNames = optionsType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => field.Name)
                .Where(fieldName => fieldName != "editorDiagnosticSmokeSegment")
                .Select(fieldName => "_" + fieldName)
                .Concat(new[]
                {
                    "_postSetHumanPoseEndpointPositionUseLeftSide",
                    "_preSetHumanPoseEndpointPositionUseLeftSide",
                    "_preSetHumanPoseEndpointPositionInvertBodyPositionX",
                    "_preSetHumanPoseEndpointPositionInvertBodyPositionZ"
                })
                .ToArray();
            string[] runnerStaticFields = typeof(YybVisualComparisonBatchRunner)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                .Select(field => field.Name)
                .ToArray();

            Assert.That(runnerStaticFields.Intersect(mirroredFieldNames), Is.Empty);
        }
    }
}
