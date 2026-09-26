using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class FbxPlaybackSmokeAutomationStoreTests
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private string _temporaryProjectRoot;

        [SetUp]
        public void SetUp()
        {
            _temporaryProjectRoot = Path.Combine(
                Path.GetTempPath(),
                nameof(FbxPlaybackSmokeAutomationStoreTests),
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryProjectRoot))
            {
                Directory.Delete(_temporaryProjectRoot, true);
            }
        }

        [Test]
        public void Given_TemporaryProjectRoot_When_CreatingStore_Then_UsesRuntimeArtifactPaths()
        {
            object store = CreateStore();
            string runtimeDirectory = Path.Combine(
                _temporaryProjectRoot,
                "Docs",
                "Workflow",
                "Local",
                "runtime");

            Assert.That(Directory.Exists(runtimeDirectory), Is.True);
            Assert.That(ReadProperty<string>(store, "RequestPath"),
                Is.EqualTo(Path.Combine(runtimeDirectory, "fbx_smoke_request.json")));
            Assert.That(ReadProperty<string>(store, "StatusPath"),
                Is.EqualTo(Path.Combine(runtimeDirectory, "fbx_smoke_status.json")));
            Assert.That(ReadProperty<string>(store, "TracePath"),
                Is.EqualTo(Path.Combine(runtimeDirectory, "fbx_smoke_trace.log")));
        }

        [Test]
        public void Given_Request_When_SavingReadingAndDeleting_Then_PreservesEnvelopeAndFileState()
        {
            object store = CreateStore();
            object request = CreateEnvelope(
                "FbxPlaybackSmokeAutomationRequest",
                ("request_id", "request-1"),
                ("command", "capture"),
                ("requested_command", "capture-clean"));

            Invoke(store, "SaveRequest", request);

            Assert.That(ReadProperty<bool>(store, "HasPendingRequest"), Is.True);
            object loadedRequest = Invoke(store, "ReadRequest");
            Assert.That(ReadField<string>(loadedRequest, "request_id"), Is.EqualTo("request-1"));
            Assert.That(ReadField<string>(loadedRequest, "command"), Is.EqualTo("capture"));
            Assert.That(ReadField<string>(loadedRequest, "requested_command"), Is.EqualTo("capture-clean"));

            Invoke(store, "DeleteRequest");

            Assert.That(ReadProperty<bool>(store, "HasPendingRequest"), Is.False);
            Assert.DoesNotThrow(() => Invoke(store, "DeleteRequest"));
        }

        [Test]
        public void Given_StatusAndTrace_When_WritingArtifacts_Then_PreservesJsonAndMessage()
        {
            object store = CreateStore();
            object status = CreateEnvelope(
                "FbxPlaybackSmokeAutomationStatus",
                ("request_id", "request-2"),
                ("status", "completed"),
                ("updated_at", "2026-08-28T12:00:00.0000000+09:00"),
                ("command", "capture"),
                ("message", "완료"),
                ("passed", true),
                ("manifest_path", "manifest.json"),
                ("failure_stage", ""),
                ("output_path", "smoke_satisfaction_2_2s.vmd"),
                ("frame_count", 60),
                ("file_size_bytes", 360000L),
                ("capture_path", "game-view.png"),
                ("preselection_state_path", "state.json"),
                ("total_jobs", 2),
                ("success_jobs", 1),
                ("failures", new[] { "failure-1" }));

            Invoke(store, "SaveStatus", status);
            Invoke(store, "AppendTrace", "smoke trace");

            string statusJson = File.ReadAllText(ReadProperty<string>(store, "StatusPath"));
            string trace = File.ReadAllText(ReadProperty<string>(store, "TracePath"));
            Assert.That(statusJson, Does.Contain("\"request_id\": \"request-2\""));
            Assert.That(statusJson, Does.Contain("\"status\": \"completed\""));
            Assert.That(statusJson, Does.Contain("\"updated_at\": \"2026-08-28T12:00:00.0000000+09:00\""));
            Assert.That(statusJson, Does.Contain("\"command\": \"capture\""));
            Assert.That(statusJson, Does.Contain("\"message\": \"완료\""));
            Assert.That(statusJson, Does.Contain("\"passed\": true"));
            Assert.That(statusJson, Does.Contain("\"output_path\": \"smoke_satisfaction_2_2s.vmd\""));
            Assert.That(statusJson, Does.Contain("\"frame_count\": 60"));
            Assert.That(statusJson, Does.Contain("\"file_size_bytes\": 360000"));
            Assert.That(statusJson, Does.Contain("\"capture_path\": \"game-view.png\""));
            Assert.That(statusJson, Does.Contain("\"preselection_state_path\": \"state.json\""));
            Assert.That(statusJson, Does.Contain("\"manifest_path\": \"manifest.json\""));
            Assert.That(statusJson, Does.Contain("\"total_jobs\": 2"));
            Assert.That(statusJson, Does.Contain("\"success_jobs\": 1"));
            Assert.That(statusJson, Does.Contain("\"failure-1\""));
            Assert.That(trace, Does.Contain("smoke trace"));
        }

        [Test]
        public void Given_E2eEnvironmentEvidence_When_Serializing_Then_IncludesPlaybackRateFields()
        {
            Type type = typeof(FbxPlaybackSmokeRunner).Assembly.GetType(
                "Fbx2Vmd.FBXImporter.FbxPlaybackSmokeRunner+E2eEnvironmentEvidence",
                throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "E2E 환경 증거 타입이 필요합니다.");
            object evidence = Activator.CreateInstance(type, nonPublic: true);

            string json = JsonUtility.ToJson(evidence);
            // 재생 속도 검증에는 두 시점 샘플의 모션 위치 차이가 필요함.
            Assert.That(json, Does.Contain("\"motion_time_seconds\""),
                "모션 재생 위치 필드가 E2E 증거에 포함되어야 합니다.");
            Assert.That(json, Does.Contain("\"motion_record_framerate\""),
                "기대 화면 진행률 비교용 녹화 FPS 필드가 E2E 증거에 포함되어야 합니다.");
        }

        private object CreateStore()
        {
            Type storeType = FindRuntimeType("FbxPlaybackSmokeAutomationStore");
            return Activator.CreateInstance(
                storeType,
                InstanceMembers,
                binder: null,
                args: new object[] { _temporaryProjectRoot },
                culture: null);
        }

        private static object CreateEnvelope(string typeName, params (string FieldName, object Value)[] values)
        {
            object envelope = Activator.CreateInstance(FindRuntimeType(typeName), nonPublic: true);
            foreach ((string fieldName, object value) in values)
            {
                FieldInfo field = envelope.GetType().GetField(fieldName, InstanceMembers);
                Assert.That(field, Is.Not.Null, fieldName);
                field.SetValue(envelope, value);
            }

            return envelope;
        }

        private static Type FindRuntimeType(string typeName)
        {
            Type type = typeof(FbxPlaybackSmokeRunner).Assembly.GetType(
                $"Fbx2Vmd.FBXImporter.{typeName}",
                throwOnError: false);
            Assert.That(type, Is.Not.Null, typeName);
            return type;
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, InstanceMembers);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, InstanceMembers);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(target);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceMembers);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }
    }
}
