using System;
using System.Linq;
using System.Reflection;
using Fbx2Vmd.FBXImporter;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Editor.Settings
{
    public class HumanoidMotionPlaybackControlsViewTests
    {
        private const string TargetAssetPath =
            "Assets/_Project/Model/YYB Hatsune Miku_default/YYB Hatsune Miku_default_1.0ver.fbx";
        private const string ClipAssetPath = "Assets/Resources/Import_FBX/satisfaction_2.fbx";

        [Test]
        public void Given_ImportButtonTemplate_When_BuildingPlaybackControls_Then_CreatesIsolatedKoreanButtons()
        {
            var canvasObject = new GameObject(
                "Playback Controls Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            var pipelineObject = new GameObject("Playback Controls Pipeline");
            pipelineObject.SetActive(false);

            try
            {
                Button template = CreateButtonTemplate(canvasObject.transform);
                int importInvocationCount = 0;
                template.onClick.AddListener(() => importInvocationCount++);
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();

                Type viewType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.Settings.HumanoidMotionPlaybackControlsView",
                    throwOnError: false);
                Assert.That(viewType, Is.Not.Null,
                    "씬 저장 없이 재생 버튼을 만드는 전용 View가 필요합니다.");
                MethodInfo ensureMethod = viewType.GetMethod(
                    "Ensure",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(ensureMethod, Is.Not.Null, "Ensure 메서드가 필요합니다.");

                object view = ensureMethod.Invoke(null, new object[] { pipeline, template });

                Assert.That(view, Is.Not.Null);
                Button playPauseButton = FindButton(
                    canvasObject,
                    "FBX_PlayPause_Button");
                Button stopButton = FindButton(canvasObject, "FBX_Stop_Button");
                Assert.That(ReadLabel(playPauseButton), Is.EqualTo("재생"));
                Assert.That(ReadLabel(stopButton), Is.EqualTo("정지"));

                playPauseButton.onClick.Invoke();
                stopButton.onClick.Invoke();
                Assert.That(importInvocationCount, Is.Zero,
                    "복제한 재생 제어가 FBX 임포트 콜백을 함께 실행하면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        [Test]
        public void Given_ImportRefreshClearedCallbacks_When_EnsuringAgain_Then_PlayButtonStartsPreparedMotion()
        {
            var canvasObject = new GameObject(
                "Playback Callback Recovery Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            var pipelineObject = new GameObject("Playback Callback Recovery Pipeline");
            pipelineObject.SetActive(false);
            GameObject target = InstantiateTarget();

            try
            {
                Button template = CreateButtonTemplate(canvasObject.transform);
                FBXVmdPipeline pipeline = pipelineObject.AddComponent<FBXVmdPipeline>();
                pipeline.targetCharacter = target;
                Type viewType = typeof(FBXVmdPipeline).Assembly.GetType(
                    "Fbx2Vmd.Settings.HumanoidMotionPlaybackControlsView",
                    throwOnError: true);
                MethodInfo ensureMethod = viewType.GetMethod(
                    "Ensure",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                ensureMethod.Invoke(null, new object[] { pipeline, template });

                MethodInfo prepareMethod = typeof(FBXVmdPipeline).GetMethod(
                    "PrepareEditorHumanoidPlayback",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(prepareMethod, Is.Not.Null);
                prepareMethod.Invoke(
                    pipeline,
                    new object[] { RequireHumanoidAnimator(target), LoadHumanoidClip(), "satisfaction_2" });

                Button playPauseButton = FindButton(
                    canvasObject,
                    "FBX_PlayPause_Button");
                playPauseButton.onClick = new Button.ButtonClickedEvent();

                ensureMethod.Invoke(null, new object[] { pipeline, template });
                playPauseButton.onClick.Invoke();

                Assert.That(pipeline.IsImportedMotionPlaying, Is.True,
                    "FBX 재임포트로 런타임 콜백이 사라져도 재생 버튼 연결을 복구해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pipelineObject);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(canvasObject);
            }
        }

        private static Button CreateButtonTemplate(Transform parent)
        {
            var buttonObject = new GameObject(
                "FBX_Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.GetComponent<TextMeshProUGUI>().text = "FBX 임포트";
            return buttonObject.GetComponent<Button>();
        }

        private static Button FindButton(GameObject root, string name)
        {
            Button button = root
                .GetComponentsInChildren<Button>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.That(button, Is.Not.Null, $"{name} 버튼이 필요합니다.");
            return button;
        }

        private static string ReadLabel(Button button)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label, Is.Not.Null, $"{button.name}에 TMP 라벨이 필요합니다.");
            return label.text;
        }

        private static GameObject InstantiateTarget()
        {
            GameObject source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                TargetAssetPath);
            Assert.That(source, Is.Not.Null, $"기준 모델을 찾을 수 없습니다: {TargetAssetPath}");
            GameObject target = UnityEngine.Object.Instantiate(source);
            target.hideFlags = HideFlags.HideAndDontSave;
            target.SetActive(true);
            return target;
        }

        private static Animator RequireHumanoidAnimator(GameObject target)
        {
            Animator animator = target.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman,
                Is.True);
            return animator;
        }

        private static AnimationClip LoadHumanoidClip()
        {
            AnimationClip clip = UnityEditor.AssetDatabase
                .LoadAllAssetsAtPath(ClipAssetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(candidate => candidate.humanMotion);
            Assert.That(clip, Is.Not.Null, $"Humanoid 클립을 찾을 수 없습니다: {ClipAssetPath}");
            return clip;
        }
    }
}
