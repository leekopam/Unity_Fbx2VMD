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
                Button legacyRecordButton = CreateLegacyRecordButton(canvasObject.transform);
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
                Button recordButton = FindButton(canvasObject, "FBX_Record_Button");
                Button stopButton = FindButton(canvasObject, "FBX_Stop_Button");
                Slider timelineSlider = FindSlider(canvasObject, "FBX_Timeline_Slider");
                Assert.That(ReadLabel(playPauseButton), Is.EqualTo("재생"));
                Assert.That(ReadLabel(recordButton), Is.EqualTo("녹화"));
                Assert.That(ReadLabel(stopButton), Is.EqualTo("정지"));
                AssertReadableKoreanLabel(playPauseButton.GetComponentInChildren<TextMeshProUGUI>(true));
                AssertReadableKoreanLabel(recordButton.GetComponentInChildren<TextMeshProUGUI>(true));
                AssertReadableKoreanLabel(stopButton.GetComponentInChildren<TextMeshProUGUI>(true));
                AssertReadableKoreanLabel(
                    timelineSlider.GetComponentInChildren<TextMeshProUGUI>(true));
                Assert.That(recordButton.interactable, Is.False);
                Assert.That(timelineSlider.wholeNumbers, Is.True);
                Assert.That(timelineSlider.interactable, Is.False);
                Assert.That(legacyRecordButton.gameObject.activeSelf, Is.False,
                    "에디터 직접 재생에서는 기존 VMD 녹화 버튼이 중복 노출되면 안 됩니다.");

                playPauseButton.onClick.Invoke();
                recordButton.onClick.Invoke();
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
                    new object[]
                    {
                        RequireHumanoidAnimator(target),
                        LoadHumanoidClip(),
                        "satisfaction_2",
                        null
                    });

                Button playPauseButton = FindButton(
                    canvasObject,
                    "FBX_PlayPause_Button");
                Slider timelineSlider = FindSlider(canvasObject, "FBX_Timeline_Slider");
                playPauseButton.onClick = new Button.ButtonClickedEvent();

                ensureMethod.Invoke(null, new object[] { pipeline, template });
                Assert.That(timelineSlider.interactable, Is.True);
                AssertTimelineLabelSupportsLongText(
                    timelineSlider.GetComponentInChildren<TextMeshProUGUI>(true));
                int requestedFrameIndex = Math.Min(30, pipeline.ImportedMotionLastFrameIndex);
                timelineSlider.value = requestedFrameIndex;
                Assert.That(pipeline.ImportedMotionCurrentFrameIndex,
                    Is.EqualTo(requestedFrameIndex));
                Assert.That(pipeline.IsImportedMotionPlaying, Is.False,
                    "프레임 탐색은 자세 검토를 위해 정지 상태를 유지해야 합니다.");
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

        private static Button CreateLegacyRecordButton(Transform parent)
        {
            var buttonObject = new GameObject(
                "MMD_Record_Button",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var labelObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            labelObject.GetComponent<TextMeshProUGUI>().text = "MMD_Record";
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

        private static Slider FindSlider(GameObject root, string name)
        {
            Slider slider = root
                .GetComponentsInChildren<Slider>(true)
                .FirstOrDefault(candidate => candidate.name == name);
            Assert.That(slider, Is.Not.Null, $"{name} 슬라이더가 필요합니다.");
            return slider;
        }

        private static string ReadLabel(Button button)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            Assert.That(label, Is.Not.Null, $"{button.name}에 TMP 라벨이 필요합니다.");
            return label.text;
        }

        private static void AssertReadableKoreanLabel(TextMeshProUGUI label)
        {
            Assert.That(label, Is.Not.Null, "한글 표시를 검증할 TMP 라벨이 필요합니다.");
            Type fallbackType = typeof(FBXVmdPipeline).Assembly.GetType(
                "Fbx2Vmd.Settings.KoreanUiTextFallback",
                throwOnError: true);
            MethodInfo isReadableMethod = fallbackType.GetMethod(
                "IsReadable",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(isReadableMethod, Is.Not.Null);
            Assert.That(
                (bool)isReadableMethod.Invoke(null, new object[] { label }),
                Is.True,
                $"{label.transform.parent.name}의 한글 라벨이 실제 글리프로 표시되어야 합니다.");
        }

        private static void AssertTimelineLabelSupportsLongText(TextMeshProUGUI label)
        {
            Assert.That(label, Is.Not.Null, "타임라인 TMP 라벨이 필요합니다.");
            if (label.enabled)
            {
                Assert.That(label.enableAutoSizing, Is.True,
                    "긴 타임라인 문구는 표시 영역에 맞춰 자동 축소되어야 합니다.");
                return;
            }

            Text fallbackText = label.GetComponentInChildren<Text>(true);
            Assert.That(fallbackText, Is.Not.Null, "한글 대체 라벨이 필요합니다.");
            Assert.That(fallbackText.resizeTextForBestFit, Is.True,
                "한글 대체 타임라인 문구도 표시 영역에 맞춰 자동 축소되어야 합니다.");
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
