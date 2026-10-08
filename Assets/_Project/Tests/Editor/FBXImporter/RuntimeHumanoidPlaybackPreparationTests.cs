using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Editor.FBXImporter
{
    /// <summary>
    /// 빌드 재생 경로(PrepareRuntimeHumanoidPlayback)가 에디터 의존 없이
    /// 재생 컨트롤러와 하체 보정 스택을 준비하는지 검증한다.
    /// </summary>
    public class RuntimeHumanoidPlaybackPreparationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private const string ClipPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";
        private const string ModelPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";

        [Test]
        public void Given_RuntimeImport_When_PreparingPlayback_Then_ReachesReadyWithCorrectionStack()
        {
            GameObject clipModelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath);
            GameObject modelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (clipModelAsset == null || modelAsset == null)
            {
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 런타임 준비 검증을 생략함");
            }
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath)
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__",
                    StringComparison.Ordinal));

            var pipelineObject = new GameObject("런타임 재생 경로 검증")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
            // 런타임 임포트 결과물은 Assimp 인스턴스지만 계약은 Animator+렌더러뿐이라
            // 에셋 인스턴스로 동일 경로를 검증한다.
            GameObject target = Object.Instantiate(modelAsset);
            GameObject imported = Object.Instantiate(clipModelAsset);
            target.hideFlags = HideFlags.HideAndDontSave;
            imported.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Animator animator = target.GetComponentInChildren<Animator>(true);
                pipeline.targetCharacter = target;

                Invoke(pipeline, "PrepareRuntimeHumanoidPlayback",
                    animator, clip, "runtime-motion", imported, ClipPath);

                Assert.That(Property(pipeline, "SessionState").ToString(),
                    Is.EqualTo("Ready"),
                    $"준비 실패 메시지: {Property(pipeline, "LastSessionMessage")}");
                Assert.That(pipeline.ImportedMotionClipLengthSeconds,
                    Is.GreaterThan(0f));

                object controller = Field(pipeline,
                    "_humanoidMotionPlaybackController");
                Assert.That(controller, Is.Not.Null);
                Assert.That((bool)Property(controller, "IsPrepared"), Is.True);
                // 하체 보정 스택이 런타임 준비에서도 생성됐는지 확인한다.
                Assert.That(Field(controller, "_footContactStabilizer"),
                    Is.Not.Null);
                Assert.That(Field(controller, "_groundResponse"), Is.Not.Null);
                Assert.That(Field(controller, "_footGrounding"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(imported);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pipelineObject);
            }
        }

        [Test]
        public void Given_RuntimePlayback_When_SeekingGroundedFrame_Then_GroundingStateIsMeasured()
        {
            GameObject clipModelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath);
            GameObject modelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (clipModelAsset == null || modelAsset == null)
            {
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 런타임 접지 검증을 생략함");
            }
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath)
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__",
                    StringComparison.Ordinal));

            var floor = new GameObject("런타임 접지 검증 바닥")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            floor.transform.position = new Vector3(0f, 2.95f, 0f);
            floor.AddComponent<BoxCollider>().size =
                new Vector3(40f, 0.1f, 40f);

            var pipelineObject = new GameObject("런타임 접지 경로 검증")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
            GameObject target = Object.Instantiate(modelAsset);
            GameObject imported = Object.Instantiate(clipModelAsset);
            target.hideFlags = HideFlags.HideAndDontSave;
            imported.hideFlags = HideFlags.HideAndDontSave;
            target.transform.position = new Vector3(0f, 3f, 0f);
            foreach (MonoBehaviour script in
                     target.GetComponentsInChildren<MonoBehaviour>(true))
            {
                script.enabled = false;
            }
            try
            {
                Animator animator = target.GetComponentInChildren<Animator>(true);
                animator.enabled = true;
                pipeline.targetCharacter = target;

                Invoke(pipeline, "PrepareRuntimeHumanoidPlayback",
                    animator, clip, "runtime-motion", imported, ClipPath);
                object controller = Field(pipeline,
                    "_humanoidMotionPlaybackController");

                // 지면 반응 없이도 접지 측정 자체는 동작해야 한다 — 접촉 게이트가
                // 실제 바닥과 밑창 오차를 측정했는지 확인한다.
                Invoke(controller, "Seek", 1073f / clip.frameRate);
                object gate = Property(controller, "LastGroundingGate");
                Assert.That(gate, Is.Not.Null,
                    "런타임 경로에서 접지 게이트가 초기화되지 않음");
                Assert.That((bool)Field(gate, "measured"), Is.True,
                    "런타임 경로에서 접촉 게이트가 측정되지 않음");
                Assert.That(Property(controller, "LastGroundingStatus")
                        .ToString(), Is.Not.EqualTo("Skipped"),
                    "접지 보정이 건너뛰어지면 빌드 접지가 무보정과 같아짐");
            }
            finally
            {
                Object.DestroyImmediate(imported);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pipelineObject);
                Object.DestroyImmediate(floor);
            }
        }

        [Test]
        public void Given_SourceModel_When_ConfigureRuntimePoseReference_Then_PlayerIsReady()
        {
            GameObject clipModelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath);
            if (clipModelAsset == null)
            {
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 런타임 참조 검증을 생략함");
            }
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath)
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__",
                    StringComparison.Ordinal));

            var host = new GameObject("런타임 포즈 참조 검증")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var retargeter = host.AddComponent<PoseSpaceRetargeter>();
            try
            {
                // 빌드에서 호출되는 것과 동일한 진입점 — 에셋 인스턴스로 계약을 검증한다.
                Invoke(retargeter, "ConfigureRuntimeHumanoidPoseReference",
                    clipModelAsset, clip);

                object player = Field(retargeter,
                    "_editorHumanoidPoseReferencePlayer");
                Assert.That(player, Is.Not.Null);
                Assert.That((bool)Property(player, "IsInitialized"), Is.True);
                Assert.That((bool)Field(retargeter,
                        "_useCompleteEditorHumanoidMuscleReference"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static object Invoke(object target, string name,
            params object[] args) =>
            target.GetType().GetMethod(name, Flags).Invoke(target, args);

        private static object Property(object target, string name) =>
            target.GetType().GetProperty(name, Flags).GetValue(target);

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Flags).GetValue(target);
    }
}
