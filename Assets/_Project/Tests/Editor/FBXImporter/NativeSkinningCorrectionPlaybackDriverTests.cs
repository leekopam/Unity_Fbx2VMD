using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Editor.FBXImporter
{
    public class NativeSkinningCorrectionPlaybackDriverTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath =
            "Assets/Resources/Import_FBX/satisfaction_2.fbx";

        [OneTimeSetUp]
        public void EnsureHumanoidClipImport()
        {
            MethodInfo method = RequireProductType(
                    "EditorHumanoidClipImportConfigurator")
                .GetMethod(
                    "EnsureHumanoid",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { ClipAssetPath });
        }

        [Test]
        public void Given_ConfiguredDriver_When_CancelingPreparation_Then_RestoresRendererStates()
        {
            GameObject target = InstantiateTarget();
            GameObject host = new GameObject("Native Skinning Driver Test Host")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            object controller = CreateController();

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                Invoke(
                    controller,
                    "PrepareWithArmDirectionReference",
                    animator,
                    LoadHumanoidClip(),
                    AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath));
                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
                bool[] enabledStates = renderers
                    .Select(renderer => renderer.enabled)
                    .ToArray();
                Type driverType = RequireProductType(
                    "NativeSkinningCorrectionPlaybackDriver");
                MethodInfo attachMethod = driverType.GetMethod(
                    "TryAttach",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(attachMethod, Is.Not.Null);
                object[] attachArguments =
                {
                    host,
                    animator,
                    controller,
                    null,
                    string.Empty
                };

                Assert.That((bool)attachMethod.Invoke(null, attachArguments), Is.True);
                object driver = attachArguments[3];
                Assert.That(driver, Is.Not.Null);
                Assert.That(ReadProperty<bool>(driver, "IsReady"), Is.False);
                Assert.That((bool)Invoke(driver, "BeginPreparation"), Is.True);
                Assert.That(ReadProperty<bool>(driver, "IsPreparing"), Is.True);
                Assert.That(renderers.All(renderer => !renderer.enabled), Is.True,
                    "사전 계산 중 빠르게 탐색하는 자세가 화면에 노출되면 안 됩니다.");

                Invoke(driver, "CancelPreparation");

                Assert.That(ReadProperty<bool>(driver, "IsPreparing"), Is.False);
                Assert.That(ReadProperty<bool>(driver, "IsReady"), Is.False);
                Assert.That(
                    renderers.Select(renderer => renderer.enabled),
                    Is.EqualTo(enabledStates),
                    "취소 시 모든 Renderer의 기존 enabled 상태를 복원해야 합니다.");
                Assert.That(ReadProperty<int>(controller, "CurrentFrameIndex"), Is.Zero);
            }
            finally
            {
                DisposeController(controller);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Given_PreparedMotion_When_PlayIsRequested_Then_PreparesCorrectionBeforePlayback()
        {
            GameObject target = InstantiateTarget();
            GameObject host = new GameObject("Native Skinning Pipeline Test Host")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Fbx2Vmd.FBXImporter.FBXVmdPipeline pipeline = null;

            try
            {
                Animator animator = RequireHumanoidAnimator(target);
                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
                bool[] enabledStates = renderers
                    .Select(renderer => renderer.enabled)
                    .ToArray();
                pipeline = host.AddComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                Invoke(
                    pipeline,
                    "PrepareEditorHumanoidPlayback",
                    animator,
                    LoadHumanoidClip(),
                    "satisfaction_2",
                    AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath));

                Assert.That(pipeline.TryPlayImportedMotion(), Is.True);
                Assert.That(
                    ReadProperty<bool>(pipeline, "IsPreparingImportedMotionCorrection"),
                    Is.True);
                Assert.That(pipeline.IsImportedMotionPlaying, Is.False,
                    "보정 준비가 끝나기 전에 재생을 시작하면 탐색 프레임이 노출됩니다.");
                Assert.That(renderers.All(renderer => !renderer.enabled), Is.True);

                Assert.That(pipeline.TryStopImportedMotion(), Is.True);
                Assert.That(
                    ReadProperty<bool>(pipeline, "IsPreparingImportedMotionCorrection"),
                    Is.False);
                Assert.That(
                    renderers.Select(renderer => renderer.enabled),
                    Is.EqualTo(enabledStates));
            }
            finally
            {
                pipeline?.TryStopImportedMotion();
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [Test]
        [Explicit("전체 FBX 프레임과 자동 선택된 모든 팔 표면을 계산하는 장시간 통합 검증입니다.")]
        [Timeout(1800000)]
        public void Given_FullMotion_When_PreparationCompletes_Then_StartsCorrectedPlayback()
        {
            GameObject target = InstantiateTarget();
            GameObject host = new GameObject("Native Skinning Full Playback Test Host")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Fbx2Vmd.FBXImporter.FBXVmdPipeline pipeline = null;

            try
            {
                pipeline = host.AddComponent<Fbx2Vmd.FBXImporter.FBXVmdPipeline>();
                Invoke(
                    pipeline,
                    "PrepareEditorHumanoidPlayback",
                    RequireHumanoidAnimator(target),
                    LoadHumanoidClip(),
                    "satisfaction_2",
                    AssetDatabase.LoadAssetAtPath<GameObject>(ClipAssetPath));
                Assert.That(pipeline.TryPlayImportedMotion(), Is.True);

                Type driverType = RequireProductType(
                    "NativeSkinningCorrectionPlaybackDriver");
                object driver = host.GetComponent(driverType);
                Assert.That(driver, Is.Not.Null);
                Array contracts = ReadField<Array>(driver, "_contracts");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                int maximumStepCount = pipeline.ImportedMotionLastFrameIndex + 2;
                for (int step = 0;
                     step < maximumStepCount &&
                     ReadProperty<bool>(driver, "IsPreparing");
                     step++)
                {
                    Invoke(driver, "LateUpdate");
                }
                stopwatch.Stop();
                string performanceMessage =
                    $"contracts={contracts.Length}, " +
                    $"frames={pipeline.ImportedMotionLastFrameIndex + 1}, " +
                    $"seconds={stopwatch.Elapsed.TotalSeconds:F3}";
                TestContext.Progress.WriteLine(performanceMessage);

                Assert.That(ReadProperty<bool>(driver, "IsPreparing"), Is.False);
                Assert.That(ReadProperty<bool>(driver, "IsFaulted"), Is.False,
                    ReadProperty<string>(driver, "FailureMessage"));
                Assert.That(ReadProperty<bool>(driver, "IsReady"), Is.True);
                object result = ReadProperty<object>(driver, "Result");
                Assert.That(result, Is.Not.Null);
                Assert.That(
                    ReadProperty<int>(result, "FrameCount"),
                    Is.EqualTo(pipeline.ImportedMotionLastFrameIndex + 1));
                Assert.That(
                    ReadProperty<int>(result, "CorrectedFrameCount"),
                    Is.GreaterThan(0));
                Assert.That(
                    ReadProperty<int>(result, "CorrectionEntryCount"),
                    Is.GreaterThan(0));
                Assert.That(
                    stopwatch.Elapsed.TotalSeconds,
                    Is.LessThan(600d),
                    performanceMessage);

                Invoke(pipeline, "LateUpdate");

                Assert.That(pipeline.IsImportedMotionPlaying, Is.True,
                    "모든 보정 cache가 완성된 뒤 대기 중인 재생 요청을 시작해야 합니다.");
                Assert.That(
                    ReadProperty<bool>(pipeline, "IsPreparingImportedMotionCorrection"),
                    Is.False);
            }
            finally
            {
                pipeline?.TryStopImportedMotion();
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static Type RequireProductType(string shortName)
        {
            Type type = typeof(Fbx2Vmd.FBXImporter.FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.FBXImporter." + shortName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null, $"{shortName} 제품 타입이 필요합니다.");
            return type;
        }

        private static object CreateController()
        {
            return Activator.CreateInstance(
                RequireProductType("HumanoidMotionPlaybackController"),
                nonPublic: true);
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"{methodName} 메서드가 필요합니다.");
            return method.Invoke(target, arguments);
        }

        private static T ReadProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, $"{propertyName} 속성이 필요합니다.");
            return (T)property.GetValue(target);
        }

        private static T ReadField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{fieldName} 필드가 필요합니다.");
            return (T)field.GetValue(target);
        }

        private static void DisposeController(object controller)
        {
            if (controller != null)
            {
                Invoke(controller, "Dispose");
            }
        }

        private static GameObject InstantiateTarget()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(TargetAssetPath);
            Assert.That(source, Is.Not.Null);
            GameObject target = UnityEngine.Object.Instantiate(source);
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate =>
                    !candidate.name.StartsWith("__", StringComparison.Ordinal) &&
                    candidate.humanMotion);
            Assert.That(clip, Is.Not.Null);
            return clip;
        }
    }
}
