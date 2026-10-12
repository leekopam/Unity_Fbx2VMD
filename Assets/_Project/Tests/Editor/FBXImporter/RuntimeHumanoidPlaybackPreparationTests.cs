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
        public void Given_GoalDrive_When_SeekingGroundedFrame_Then_SurfaceCaptureIsMeasured()
        {
            GameObject clipModelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath);
            GameObject modelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (clipModelAsset == null || modelAsset == null)
            {
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 goal 경로 계측 검증을 생략함");
            }
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath)
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__",
                    StringComparison.Ordinal));

            var floor = new GameObject("goal 경로 접지 검증 바닥")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            floor.transform.position = new Vector3(0f, 2.95f, 0f);
            floor.AddComponent<BoxCollider>().size =
                new Vector3(40f, 0.1f, 40f);

            var pipelineObject = new GameObject("goal 경로 표면 계측 검증")
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

                Invoke(controller, "SetIkGoalFootDrive", true, 1f);
                Assert.That((bool)Property(controller, "IsIkGoalFootDriveActive"),
                    Is.True, "런타임 준비 경로에서 goal 드라이버가 초기화되어야 함");

                Invoke(controller, "Seek", 1073f / clip.frameRate);
                // goal 경로는 gate 평가를 생략하지만 표면 계측은 측정형 상태가 필요함 —
                // Unavailable이면 풀클립 캡처가 첫 프레임에서 실패함.
                string status = Property(controller, "LastGroundingStatus").ToString();
                Assert.That(status, Is.Not.EqualTo("Unavailable")
                    .And.Not.EqualTo("Disabled"),
                    $"goal 경로 접지 상태가 측정형이 아님: {status}");
                // 드라이버 주장 프레임은 ik_goal_drive, 기권 프레임은 alpha=0과 같은
                // gate 경로가 실행됨 — 어느 쪽이든 표면 계측이 프레임별로 갱신되어야 함.
                string stage = (string)Field(Property(controller, "LastGroundingGate"),
                    "evaluation_stage");
                Assert.That(stage, Is.Not.Null.And.Not.EqualTo("unavailable"),
                    $"goal 경로 평가 단계가 알 수 없는 값: {stage}");
                // gate 진단 열은 기권 프레임의 gate 적용에서만 채워져야 함 —
                // 주장 프레임(ik_goal_*)에서 measured가 켜지면 게이트가 다시 켜진 회귀임.
                Assert.That(Field(Property(controller, "LastGroundingGate"),
                        "measured"), Is.EqualTo(stage == "gate"));
                object[] surface = { null, null };
                Assert.That((bool)Invoke(controller, "TryCaptureCurrentFootSurface",
                        surface), Is.True,
                    "goal 경로에서 접지 표면을 측정하지 못함");
                // 스냅샷이 마지막 gate 평가(프레임 0)나 초기값을 재사용하지 않고
                // 현재 프레임의 지면·지지 가중치를 담아야 함 — 1073은 왼발 지지 구간.
                object leftSurface = surface[0];
                Assert.That((bool)Field(leftSurface, "has_ground"), Is.True,
                    "현재 자세 아래의 지면을 프레임별로 다시 평가해야 함");
                Assert.That((string)Field(leftSurface, "support_role"),
                    Is.Not.EqualTo("released"),
                    "지지 가중치가 프레임 0의 초기값으로 고정되어서는 안 됨");
                // 발별 진단도 goal 평가 결과를 담아야 함 — 이전에는 null로 남았음.
                Assert.That(Field(Property(controller, "LastGroundingGate"),
                        "left"), Is.Not.Null,
                    "goal 경로 표면 진단이 비어 있음");
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
        public void Given_GoalDrive_When_SeekingAcrossFrames_Then_SurfaceSnapshotFollowsCurrentFrame()
        {
            GameObject clipModelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath);
            GameObject modelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (clipModelAsset == null || modelAsset == null)
            {
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 goal 경로 스냅샷 갱신 검증을 생략함");
            }
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath)
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__",
                    StringComparison.Ordinal));

            var floor = new GameObject("goal 스냅샷 갱신 검증 바닥")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            floor.transform.position = new Vector3(0f, 2.95f, 0f);
            floor.AddComponent<BoxCollider>().size =
                new Vector3(40f, 0.1f, 40f);

            var pipelineObject = new GameObject("goal 스냅샷 갱신 검증")
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
                Invoke(controller, "SetIkGoalFootDrive", true, 1f);

                object[] surface = { null, null };
                Invoke(controller, "Seek", 1073f / clip.frameRate);
                Assert.That((bool)Invoke(controller, "TryCaptureCurrentFootSurface",
                    surface), Is.True);
                string pinnedRole = (string)Field(surface[0], "support_role");
                float pinnedRear = (float)Field(surface[0], "rear_weight");
                float pinnedFront = (float)Field(surface[0], "front_weight");
                Assert.That(pinnedRole, Is.Not.EqualTo("released"),
                    "지지 구간 스냅샷이 프레임별 평가를 반영해야 함");

                // 프레임 0은 지지 가중치가 없어 released여야 함 — alpha=1 풀클립 런에서
                // 관측된 상수 released는 이 프레임의 gate 평가 잔여였음.
                Invoke(controller, "Seek", 0f);
                Assert.That((bool)Invoke(controller, "TryCaptureCurrentFootSurface",
                    surface), Is.True);
                Assert.That((string)Field(surface[0], "support_role"),
                    Is.EqualTo("released"),
                    "스냅샷이 직전 프레임 값을 재사용하면 역할이 갱신되지 않음");

                // 같은 프레임의 지지 계획은 경로와 무관하게 동일해야 함 — goal 경로도
                // 프레임별 평가를 수행함을 gate 결과와 교차 검증.
                Invoke(controller, "SetIkGoalFootDrive", false, 0f);
                Invoke(controller, "Seek", 1073f / clip.frameRate);
                Assert.That((bool)Invoke(controller, "TryCaptureCurrentFootSurface",
                    surface), Is.True);
                Assert.That((string)Field(surface[0], "support_role"),
                    Is.EqualTo(pinnedRole));
                Assert.That((float)Field(surface[0], "rear_weight"),
                    Is.EqualTo(pinnedRear).Within(0.0001f));
                Assert.That((float)Field(surface[0], "front_weight"),
                    Is.EqualTo(pinnedFront).Within(0.0001f));
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

        [Test]
        public void Given_GoalDrive_When_SeekingAcrossBendMarginSpan_Then_RightKneeKeepsForwardSide()
        {
            // Phase 6 회귀 — 원시 자세의 부호반전 굽힘 법선이 goal 경로의 지면
            // 상승 IK에 전달되면 발 하단 상승 시 무릎이 반대편에 배치된다.
            // f878-894에서 +25→-25로 미러됐던 구간을 연속 Seek해 검증한다.
            GameObject clipModelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ClipPath);
            GameObject modelAsset =
                AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (clipModelAsset == null || modelAsset == null)
            {
                Assert.Ignore("로컬 FBX 입력이 없는 환경에서는 회귀 검증을 생략함");
            }
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipPath)
                .OfType<AnimationClip>()
                .First(c => !c.name.StartsWith("__preview__",
                    StringComparison.Ordinal));

            var floor = new GameObject("무릎 반전 회귀 검증 바닥")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.AddComponent<BoxCollider>().size =
                new Vector3(40f, 0.1f, 40f);

            var pipelineObject = new GameObject("무릎 반전 회귀 검증")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            var pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
            GameObject target = Object.Instantiate(modelAsset);
            GameObject imported = Object.Instantiate(clipModelAsset);
            target.hideFlags = HideFlags.HideAndDontSave;
            imported.hideFlags = HideFlags.HideAndDontSave;
            target.transform.position = Vector3.zero;
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
                Invoke(controller, "SetGroundResponseEnabled", true);
                Invoke(controller, "SetIkGoalFootDrive", true, 1f);

                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform rightUpper =
                    animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                Transform rightKnee =
                    animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                Transform rightFoot =
                    animator.GetBoneTransform(HumanBodyBones.RightFoot);

                // weight 감쇠 이력이 반전을 유발하므로 pin 해제 전부터 연속 Seek.
                float worstKnee = float.MaxValue;
                int worstFrame = -1;
                for (int frame = 830; frame <= 905; frame++)
                {
                    Invoke(controller, "Seek", frame / clip.frameRate);
                    if (frame < 870) continue;
                    float knee = Vector3.SignedAngle(
                        (rightKnee.position - rightUpper.position).normalized,
                        (rightFoot.position - rightKnee.position).normalized,
                        hips.right);
                    if (knee < worstKnee)
                    {
                        worstKnee = knee;
                        worstFrame = frame;
                    }
                }
                // 회귀 시 -25~-32로 붕괴 — alpha=0은 같은 구간에서 +15~+41임.
                Assert.That(worstKnee, Is.GreaterThan(-1f),
                    $"f{worstFrame}에서 우측 무릎이 해부학 방향을 잃음 " +
                    $"(knee={worstKnee:F1}°) — 지면 상승 IK에 부호반전된 " +
                    $"굽힘 법선이 전달된 회귀");
            }
            finally
            {
                Object.DestroyImmediate(imported);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pipelineObject);
                Object.DestroyImmediate(floor);
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
