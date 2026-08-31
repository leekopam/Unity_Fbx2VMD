using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public sealed class YybVisualComparisonRequestOptionsMapperTests
    {
        private const string MapperTypeName =
            "Fbx2Vmd.FBXImporter.YybVisualComparisonRequestOptionsMapper";

        [Test]
        public void Given_RequestMapping_When_CheckingContract_Then_UsesSingleRunOptionsBoundary()
        {
            Type mapperType = FindMapperType();
            Type requestType = FindRequestType(mapperType);
            Type optionsType = FindRunOptionsType();
            MethodInfo mapMethod = FindMapMethod(mapperType);

            Assert.That(mapMethod.GetParameters(), Has.Length.EqualTo(1));
            Assert.That(mapMethod.GetParameters()[0].ParameterType, Is.EqualTo(requestType));
            Assert.That(mapMethod.ReturnType, Is.EqualTo(optionsType));

            MethodInfo optionsOverload = typeof(YybVisualComparisonBatchRunner)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .SingleOrDefault(method =>
                    method.Name == "RunWithOptions" &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType == optionsType);

            Assert.That(optionsOverload, Is.Not.Null, "실행 옵션 객체 하나만 받는 runner 경계가 필요합니다.");
        }

        [Test]
        public void Given_RequestValues_When_Mapping_Then_PreservesNormalizationAndClamps()
        {
            Type mapperType = FindMapperType();
            Type requestType = FindRequestType(mapperType);
            object request = Activator.CreateInstance(requestType, nonPublic: true);

            SetField(request, "fbx_file", "future-model-motion.fbx");
            SetField(request, "duration_seconds", 12.5f);
            SetField(request, "finger_closeups", true);
            SetField(request, "manual_animator_full_body_pose_weight", float.PositiveInfinity);
            SetField(request, "retarget_pose_visual_spike_current_weight", 3f);
            SetField(request, "yyb_arm_swing_min_hand_horizontal_ratio", 2f);
            SetField(request, "yyb_arm_direction_left_side_weight_scale", -0.25f);
            SetField(request, "manual_animator_body_position_xz_enabled", true);
            SetField(request, "segment", "middle");
            SetField(request, "diagnostic_capture_width_override", -10);

            object options = FindMapMethod(mapperType).Invoke(null, new[] { request });

            Assert.That(ReadField<string>(options, "fbxFileName"), Is.EqualTo("future-model-motion.fbx"));
            Assert.That(ReadField<float>(options, "durationSeconds"), Is.EqualTo(12.5f));
            Assert.That(ReadField<bool>(options, "enableFingerCloseups"), Is.True);
            Assert.That(ReadField<float>(options, "manualAnimatorFullBodyPoseReferenceWeight"), Is.EqualTo(1f));
            Assert.That(ReadField<float>(options, "retargetPoseVisualSpikeCurrentWeight"), Is.EqualTo(1f));
            Assert.That(ReadField<float>(options, "yybArmSwingMinHandHorizontalRatio"), Is.EqualTo(1.5f));
            Assert.That(ReadField<float>(options, "yybArmDirectionLeftSideWeightScale"), Is.EqualTo(0f));
            Assert.That(ReadField<bool>(options, "enableManualAnimatorBodyPositionXzRuntimeOverride"), Is.True);
            Assert.That(ReadField<string>(options, "editorDiagnosticSmokeSegment"), Is.EqualTo("middle"));
            Assert.That(ReadField<int>(options, "diagnosticCaptureWidthOverride"), Is.EqualTo(0));
        }

        [Test]
        public void Given_WatcherPoll_When_CheckingSource_Then_DelegatesRequestMapping()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null);
            string sourcePath = Path.Combine(
                projectRoot,
                "Assets/_Project/Scripts/FBXImporter/YybVisualComparisonRequestWatcher.cs");
            string source = File.ReadAllText(sourcePath).Replace("\r\n", "\n");

            Assert.That(source, Does.Contain("YybVisualComparisonRequestOptionsMapper.Map(request)"));
            Assert.That(
                source,
                Does.Not.Contain("mmdIkDeltaGuardLimitOverrideVmd: request.mmd_ik_delta_guard_limit_vmd"),
                "Poll이 139개 실행 인자 매핑을 직접 소유하면 안 됩니다.");
        }

        private static Type FindMapperType()
        {
            Type mapperType = typeof(FBXVmdPipeline).Assembly.GetType(MapperTypeName, throwOnError: false);
            Assert.That(mapperType, Is.Not.Null, "요청 값을 실행 옵션으로 변환하는 pure mapper가 필요합니다.");
            return mapperType;
        }

        private static Type FindRequestType(Type mapperType)
        {
            Type requestType = mapperType.GetNestedType(
                "RequestData",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(requestType, Is.Not.Null, "요청 JSON 데이터 경계가 mapper에 필요합니다.");
            return requestType;
        }

        private static Type FindRunOptionsType()
        {
            return typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.YybVisualComparisonRunOptions",
                throwOnError: true);
        }

        private static MethodInfo FindMapMethod(Type mapperType)
        {
            MethodInfo method = mapperType.GetMethod(
                "Map",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "request → options 변환 메서드가 필요합니다.");
            return method;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"요청 필드가 없습니다: {fieldName}");
            field.SetValue(target, value);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"실행 옵션 필드가 없습니다: {fieldName}");
            return (T)field.GetValue(target);
        }
    }
}
