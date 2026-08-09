using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

using BoneNames = UnityHumanoidVMDRecorder.BoneNames;

namespace Tests.Editor.VMDRecorderSample
{
    /// <summary>
    /// SaveVMDAsync 파이프라인의 가드, 검증, end-to-end 출력을 검증한다.
    /// 편집 모드에서 최소 합성 데이터를 사용해 실제 VMD 파일 출력까지 확인한다.
    /// </summary>
    public class SaveVmdAsyncCharacterizationTests
    {
        private const string FieldName_frameNumberSaved = "frameNumberSaved";
        private const string FieldName_positionDictionarySaved = "positionDictionarySaved";
        private const string FieldName_rotationDictionarySaved = "rotationDictionarySaved";
        private const string FieldName_BoneDictionary = "BoneDictionary";

        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "fbx2vmd-char-tests");
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
            }
        }

        // --- Validation guard tests ---

        [Test]
        public void SaveVmdAsync_WhenRecording_ReturnsFail()
        {
            var go = new GameObject("test-recorder-guard");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                recorder.StartRecording(); // sets IsRecording=true

                string path = Path.Combine(_tempDir, "recording_guard.vmd");
                var task = recorder.SaveVMDAsync("testModel", path);
                task.Wait();

                Assert.That(task.Result.Success, Is.False,
                    "레코딩 중이면 VMD 저장을 거부해야 한다.");
                Assert.That(task.Result.ErrorMessage, Does.Contain("stopped"),
                    "레코딩 중지 안내 메시지가 포함되어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WhenPathIsEmpty_ReturnsFail()
        {
            var go = new GameObject("test-empty-path");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);

                var task = recorder.SaveVMDAsync("testModel", "");
                task.Wait();

                Assert.That(task.Result.Success, Is.False,
                    "빈 경로로 저장을 거부해야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WhenPathIsWhitespace_ReturnsFail()
        {
            var go = new GameObject("test-whitespace");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);

                var task = recorder.SaveVMDAsync("testModel", "   ");
                task.Wait();

                Assert.That(task.Result.Success, Is.False,
                    "공백만 있는 경로로 저장을 거부해야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WhenNoFramesRecorded_ReturnsFail()
        {
            var go = new GameObject("test-no-frames");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);

                // recorder 시작 직후 frameNumberSaved=0
                string path = Path.Combine(_tempDir, "no_frames.vmd");
                var task = recorder.SaveVMDAsync("testModel", path);
                task.Wait();

                Assert.That(task.Result.Success, Is.False,
                    "녹화 프레임이 없으면 저장을 거부해야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WhenDictionariesAreNull_ReturnsFail()
        {
            var go = new GameObject("test-null-dict");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                // Start를 호출하지 않고 바로 저장 시도 → BoneDictionary 등이 null
                string path = Path.Combine(_tempDir, "null_dict.vmd");
                var task = recorder.SaveVMDAsync("testModel", path);
                task.Wait();

                Assert.That(task.Result.Success, Is.False,
                    "초기화되지 않은 레코더는 저장을 거부해야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WhenDirectoryDoesNotExist_CreatesDirectoryAndSucceeds()
        {
            var go = new GameObject("test-mkdir");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);
                PopulateMinimalRecordingData(recorder);

                string nestedDir = Path.Combine(_tempDir, "nested", "sub");
                string path = Path.Combine(nestedDir, "mkdir_test.vmd");

                var task = recorder.SaveVMDAsync("testModel", path);
                task.Wait();

                Assert.That(task.Result.Success, Is.True,
                    "존재하지 않는 디렉터리는 생성 후 저장에 성공해야 한다.");
                Assert.That(File.Exists(path), Is.True,
                    "VMD 파일이 실제로 생성되어야 한다.");
                Assert.That(new FileInfo(path).Length, Is.GreaterThan(0),
                    "생성된 VMD 파일은 비어 있지 않아야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // --- End-to-end output tests ---

        [Test]
        public void SaveVmdAsync_WithSingleBoneSingleFrame_ProducesValidVmd()
        {
            var go = new GameObject("test-e2e-single");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);
                PopulateMinimalRecordingData(recorder, frameCount: 1);

                string path = Path.Combine(_tempDir, "single_bone.vmd");
                var task = recorder.SaveVMDAsync("testModel", path);
                task.Wait();
                var result = task.Result;

                Assert.That(result.Success, Is.True,
                    "최소 본+프레임으로 저장에 성공해야 한다.");
                Assert.That(result.FrameCount, Is.GreaterThan(0),
                    "프레임 수가 0보다 커야 한다.");
                Assert.That(result.FileSizeBytes, Is.GreaterThan(0),
                    "파일 크기가 0보다 커야 한다.");
                Assert.That(result.FilePath, Is.EqualTo(path));
                Assert.That(File.Exists(path), Is.True);

                byte[] bytes = File.ReadAllBytes(path);
                string signature = System.Text.Encoding.ASCII.GetString(bytes, 0, 30);
                Assert.That(signature, Does.Contain("Vocaloid Motion Data"),
                    "VMD 시그니처가 포함되어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WithMultipleBonesMultipleFrames_ProducesValidVmd()
        {
            var go = new GameObject("test-e2e-multi");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);
                PopulateMinimalRecordingData(recorder, frameCount: 5);

                string path = Path.Combine(_tempDir, "multi_bone.vmd");
                var task = recorder.SaveVMDAsync("testModel", path);
                task.Wait();
                var result = task.Result;

                Assert.That(result.Success, Is.True,
                    "여러 본+프레임으로 저장에 성공해야 한다.");
                Assert.That(result.FrameCount, Is.GreaterThan(0));
                Assert.That(File.Exists(path), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WithCenterBone_ResultsAreDeterministic()
        {
            var go = new GameObject("test-deterministic");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);
                PopulateMinimalRecordingData(recorder, frameCount: 2);

                string path1 = Path.Combine(_tempDir, "deterministic_a.vmd");
                string path2 = Path.Combine(_tempDir, "deterministic_b.vmd");

                var task1 = recorder.SaveVMDAsync("testModel", path1);
                task1.Wait();
                var task2 = recorder.SaveVMDAsync("testModel", path2);
                task2.Wait();

                Assert.That(task1.Result.Success, Is.True);
                Assert.That(task2.Result.Success, Is.True);

                byte[] bytes1 = File.ReadAllBytes(path1);
                byte[] bytes2 = File.ReadAllBytes(path2);

                Assert.That(bytes1, Is.EqualTo(bytes2),
                    "같은 입력으로 두 번 저장한 VMD는 바이트 단위로 동일해야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SaveVmdAsync_WithNullModelName_UsesDefaultNameAndSucceeds()
        {
            var go = new GameObject("test-default-name");
            try
            {
                var recorder = go.AddComponent<UnityHumanoidVMDRecorder>();
                WaitForRecorderStart(recorder);
                PopulateMinimalRecordingData(recorder, frameCount: 1);

                string path = Path.Combine(_tempDir, "null_name.vmd");
                var task = recorder.SaveVMDAsync(null, path);
                task.Wait();
                var result = task.Result;

                Assert.That(result.Success, Is.True,
                    "모델명이 null이어도 기본값으로 저장에 성공해야 한다.");
                Assert.That(File.Exists(path), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // --- Helpers ---

        /// <summary>
        /// Recorder의 Start()가 완료될 때까지 대기한다.
        /// Start()는 Awake 직후 호출되므로 추가 대기가 필요하지 않지만,
        /// Animator/Humanoid가 없는 환경에서는 BoneDictionary가 null로 남을 수 있다.
        /// </summary>
        private static void WaitForRecorderStart(UnityHumanoidVMDRecorder recorder)
        {
            // Start() is called by Unity after Awake during the frame lifecycle.
            // In EditMode tests, we may need to invoke it manually through reflection
            // if the recorder has an Animator with a humanoid avatar.
            // Without an Avatar, Start() sets BoneDictionary=null, which is fine for guard tests.
            var startMethod = typeof(UnityHumanoidVMDRecorder).GetMethod(
                "Start", BindingFlags.Instance | BindingFlags.NonPublic);
            if (startMethod != null)
            {
                try { startMethod.Invoke(recorder, null); } catch { /* best-effort */ }
            }
        }

        /// <summary>
        /// Reflection으로 레코더의 내부 상태를 StopRecording 직후 상태로 설정한다.
        /// BoneDictionary에 センター 본 하나만 등록하고,
        /// positionDictionarySaved, rotationDictionarySaved에 합성 데이터를 넣는다.
        /// </summary>
        private static void PopulateMinimalRecordingData(
            UnityHumanoidVMDRecorder recorder,
            int frameCount = 1)
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

            // BoneDictionary 설정
            var boneDict = new Dictionary<BoneNames, Transform>
            {
                [BoneNames.センター] = recorder.transform
            };
            SetField(recorder, FieldName_BoneDictionary, boneDict);

            // 합성 프레임 데이터
            var positions = new Dictionary<BoneNames, List<Vector3>>();
            var rotations = new Dictionary<BoneNames, List<Quaternion>>();
            var posList = new List<Vector3>(frameCount);
            var rotList = new List<Quaternion>(frameCount);
            for (int i = 0; i < frameCount; i++)
            {
                posList.Add(new Vector3(0f, i * 0.1f, 0f));
                rotList.Add(Quaternion.identity);
            }
            positions[BoneNames.センター] = posList;
            rotations[BoneNames.センター] = rotList;

            SetField(recorder, FieldName_positionDictionarySaved, positions);
            SetField(recorder, FieldName_rotationDictionarySaved, rotations);
            SetField(recorder, FieldName_frameNumberSaved, frameCount);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = typeof(UnityHumanoidVMDRecorder).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"필드 {fieldName}가 존재해야 한다.");
            field.SetValue(target, value);
        }
    }
}
