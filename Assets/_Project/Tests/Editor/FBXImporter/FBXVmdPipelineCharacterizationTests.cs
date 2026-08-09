using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// FBXVmdPipeline 파이프라인 진입점의 설정, 상수, 상태 전이를 검증한다.
    /// 씬 로딩 없이 편집 모드에서 검증 가능한 항목에 집중한다.
    /// </summary>
    public class FBXVmdPipelineCharacterizationTests
    {
        private const string SceneMainAuto = "Assets/_Project/Scene/Main_Auto.unity";
        private const string SceneFbxImportCapture = "Assets/_Project/Scene/FbxImport_Capture.unity";

        // --- Pipeline constants ---

        [Test]
        public void ImportFolderName_IsImportFbx()
        {
            Assert.That(FBXVmdPipeline.IMPORT_FBX_FOLDER, Is.EqualTo("Import_FBX"),
                "임포트 폴더명은 'Import_FBX'로 고정되어야 한다.");
        }

        [Test]
        public void FbxExtension_IsLowercaseFbx()
        {
            Assert.That(FBXVmdPipeline.FBX_EXTENSION, Is.EqualTo("fbx"),
                "FBX 확장자는 소문자 'fbx'로 고정되어야 한다.");
        }

        [Test]
        public void MmdReferenceFrameRate_Is30()
        {
            Assert.That(FBXVmdPipeline.MMD_REFERENCE_FRAME_RATE, Is.EqualTo(30f).Within(0.001f),
                "MMD 기준 프레임레이트는 30fps로 고정되어야 한다.");
        }

        [Test]
        public void MaxRetargetPrewarmFrameCount_Is120()
        {
            Assert.That(FBXVmdPipeline.MAX_RETARGET_PREWARM_FRAME_COUNT, Is.EqualTo(120),
                "리타겟 예열 프레임 수는 120으로 고정되어야 한다.");
        }

        // --- Default settings verification ---

        [Test]
        public void NewPipeline_DefaultsToRecordAfterImport()
        {
            var go = new GameObject("pipeline-defaults-test");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();

                Assert.That(pipeline.ShouldRecordVmdAfterImport, Is.True,
                    "새 파이프라인은 VMD 자동 녹화가 기본 활성화되어야 한다.");
                Assert.That(pipeline.ShouldSaveToImportFolder, Is.False,
                    "새 파이프라인은 Import_FBX 복사가 기본 비활성화되어야 한다.");
                Assert.That(pipeline.ShouldPreserveRetargetBodyPosition, Is.True,
                    "타겟 body position 보존이 기본 활성화되어야 한다.");
                Assert.That(pipeline.ShouldUseEditorHumanoidClipMuscleReference, Is.True,
                    "Editor Humanoid clip muscle reference가 기본 활성화되어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NewPipeline_ExperimentalFlagsDefaultOff()
        {
            var go = new GameObject("pipeline-experimental-test");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();

                // 실험적/진단 기능들은 기본 비활성화
                Assert.That(pipeline.ShouldUseLegacyPoseSpaceFacingCorrection, Is.False,
                    "Legacy PoseSpace 방향 보정은 기본 비활성화되어야 한다.");
                Assert.That(pipeline.ShouldPreserveFbxRootRotation, Is.False,
                    "FBX root rotation 보존은 기본 비활성화되어야 한다.");
                Assert.That(pipeline.ShouldUseRetargetBodyPositionXZRootMotion, Is.False,
                    "BodyPosition X/Z root motion은 기본 비활성화되어야 한다.");
                Assert.That(pipeline.ShouldStabilizeGroundedFootXZ, Is.False,
                    "Grounded foot X/Z 안정화는 기본 비활성화되어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NewPipeline_ArmGuardDefaults()
        {
            var go = new GameObject("pipeline-armguard-test");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();

                Assert.That(pipeline.ClampRetargetMusclesToHumanRange, Is.True,
                    "Humanoid muscle 범위 clamp가 기본 활성화되어야 한다.");
                Assert.That(pipeline.EnableAnatomicalArmGuard, Is.True,
                    "해부학적 팔 가드가 기본 활성화되어야 한다.");
                Assert.That(pipeline.ArmStretchMuscleLimit, Is.EqualTo(0f).Within(0.001f),
                    "Arm Stretch muscle 제한이 기본 0이어야 한다.");
                Assert.That(pipeline.UpperArmTwistMuscleLimit, Is.EqualTo(0.75f).Within(0.001f),
                    "상완 Twist muscle 제한이 0.75여야 한다.");
                Assert.That(pipeline.LowerArmTwistMuscleLimit, Is.EqualTo(0.65f).Within(0.001f),
                    "전완 Twist muscle 제한이 0.65여야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // --- Entry-point guard: IsProcessing와 입력 검증 ---

        [Test]
        public void TrySubmitImportSource_WhenSourcePathNull_ReturnsFalse()
        {
            var go = new GameObject("pipeline-null-path");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();

                bool result = pipeline.TrySubmitImportSource(null, "원본 파일을 찾을 수 없습니다.");

                Assert.That(result, Is.False,
                    "null 소스 경로는 거부되어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TrySubmitImportSource_WhenSourcePathEmpty_ReturnsFalse()
        {
            var go = new GameObject("pipeline-empty-path");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();

                bool result = pipeline.TrySubmitImportSource("", "파일 경로가 비어 있습니다.");

                Assert.That(result, Is.False,
                    "빈 소스 경로는 거부되어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryStartFbxImportFromSharedSettings_DelegatesToTrySubmitImportSource()
        {
            var go = new GameObject("pipeline-shared-settings");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();

                // 같은 경계 조건을 공유하는지 검증
                bool result = pipeline.TryStartFbxImportFromSharedSettings(null);

                Assert.That(result, Is.False,
                    "TryStartFbxImportFromSharedSettings도 null 경로를 거부해야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // --- IsProcessing 상태 보호 ---

        [Test]
        public void IsProcessing_WhenTrue_ShouldRejectNewImport()
        {
            var go = new GameObject("pipeline-processing-guard");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();
                // 내부 _isProcessing 필드를 reflection으로 설정
                var isProcessingField = typeof(FBXVmdPipeline).GetField(
                    "_isProcessing",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assume.That(isProcessingField, Is.Not.Null,
                    "_isProcessing 필드가 존재해야 한다.");

                isProcessingField.SetValue(pipeline, true);

                // 비어 있지 않은 유효한 경로도 처리 중이면 거부
                bool result = pipeline.TrySubmitImportSource("Assets/SomeModel.fbx");
                Assert.That(result, Is.False,
                    "처리 중이면 새 임포트 요청을 거부해야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsProcessing_UpdatesViaField()
        {
            var go = new GameObject("pipeline-isprocessing");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();
                var isProcessingField = typeof(FBXVmdPipeline).GetField(
                    "_isProcessing",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assume.That(isProcessingField, Is.Not.Null);

                // 초기 상태: false
                Assert.That((bool)isProcessingField.GetValue(pipeline), Is.False,
                    "초기 IsProcessing은 false여야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // --- TargetCharacter 설정 ---

        [Test]
        public void TargetCharacter_CanBeSet()
        {
            var go = new GameObject("pipeline-target-test");
            var target = new GameObject("target-character");
            try
            {
                var pipeline = go.AddComponent<FBXVmdPipeline>();

                pipeline.targetCharacter = target;
                Assert.That(pipeline.targetCharacter, Is.SameAs(target),
                    "targetCharacter가 설정한 값으로 반환되어야 한다.");

                pipeline.targetCharacter = null;
                Assert.That(pipeline.targetCharacter, Is.Null,
                    "targetCharacter를 null로 초기화할 수 있어야 한다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // --- Scene file existence ---

        [Test]
        public void MainAutoScene_ExistsOnDisk()
        {
            Assert.That(System.IO.File.Exists(SceneMainAuto), Is.True,
                "Main_Auto.unity 씬 파일이 존재해야 한다.");
        }

        [Test]
        public void FbxImportCaptureScene_ExistsOnDisk()
        {
            Assert.That(System.IO.File.Exists(SceneFbxImportCapture), Is.True,
                "FbxImport_Capture.unity 씬 파일이 존재해야 한다.");
        }
    }
}
