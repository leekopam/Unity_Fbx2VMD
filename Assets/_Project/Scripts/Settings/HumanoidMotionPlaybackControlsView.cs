using System;
using Fbx2Vmd.FBXImporter;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fbx2Vmd.Settings
{
    /// <summary>
    /// 기존 FBX 임포트 버튼과 같은 모양의 재생 제어를 Play Mode에 구성함.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class HumanoidMotionPlaybackControlsView : MonoBehaviour
    {
        private const string ImportButtonName = "FBX_Button";
        private const string LegacyRecordButtonName = "MMD_Record_Button";
        private const string RecordButtonName = "FBX_Record_Button";
        private const string PlayPauseButtonName = "FBX_PlayPause_Button";
        private const string StopButtonName = "FBX_Stop_Button";
        private const string TimelineSliderName = "FBX_Timeline_Slider";
        private const string TimelineLabelName = "FBX_Timeline_Label";
        private const float ButtonVerticalSpacing = 110f;
        private const float TimelineHeight = 36f;
        private const float MinimumTimelineFontSize = 12f;

        private FBXVmdPipeline _pipeline;
        private Button _legacyRecordButton;
        private bool _wasLegacyRecordButtonActive;
        private Button _recordButton;
        private Button _playPauseButton;
        private Button _stopButton;
        private Slider _timelineSlider;
        private TMP_Text _timelineLabel;
        private Text _legacyTimelineLabel;

        internal static HumanoidMotionPlaybackControlsView Ensure(
            FBXVmdPipeline pipeline,
            Button importButton = null)
        {
            if (pipeline == null)
            {
                return null;
            }

            HumanoidMotionPlaybackControlsView view =
                pipeline.GetComponent<HumanoidMotionPlaybackControlsView>();
            if (view != null)
            {
                view.Bind(pipeline);
                view.RebindCallbacks(importButton);
                return view;
            }

            Button template = importButton ?? ResolveImportButton();
            if (template == null)
            {
                Debug.LogWarning(
                    $"[FBXImport] 재생 제어를 만들 {ImportButtonName}을 찾지 못했습니다.");
                return null;
            }

            view = pipeline.gameObject.AddComponent<HumanoidMotionPlaybackControlsView>();
            view.Build(pipeline, template);
            return view;
        }

        private void Update()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            DestroyControlButton(_playPauseButton);
            DestroyControlButton(_recordButton);
            DestroyControlButton(_stopButton);
            DestroyControlObject(_timelineSlider);
            if (_legacyRecordButton != null)
            {
                _legacyRecordButton.gameObject.SetActive(_wasLegacyRecordButtonActive);
            }
        }

        private void Build(FBXVmdPipeline pipeline, Button template)
        {
            Bind(pipeline);
            _legacyRecordButton = FindButton(
                template.transform.parent,
                LegacyRecordButtonName);
            if (_legacyRecordButton != null)
            {
                _wasLegacyRecordButtonActive = _legacyRecordButton.gameObject.activeSelf;
                _legacyRecordButton.gameObject.SetActive(false);
            }

            _recordButton = CreateControlButton(
                template,
                RecordButtonName,
                "녹화",
                ButtonVerticalSpacing,
                HandleRecordClick);
            _playPauseButton = CreateControlButton(
                template,
                PlayPauseButtonName,
                "재생",
                ButtonVerticalSpacing * 2f,
                HandlePlayPauseClick);
            _stopButton = CreateControlButton(
                template,
                StopButtonName,
                "정지",
                ButtonVerticalSpacing * 3f,
                HandleStopClick);
            _timelineSlider = CreateTimelineSlider(
                template,
                ButtonVerticalSpacing * 4f,
                HandleTimelineValueChanged,
                out _timelineLabel,
                out _legacyTimelineLabel);
            Refresh();
        }

        private void Bind(FBXVmdPipeline pipeline)
        {
            _pipeline = pipeline;
        }

        private void RebindCallbacks(Button importButton)
        {
            Transform parent = importButton != null
                ? importButton.transform.parent
                : _playPauseButton != null
                    ? _playPauseButton.transform.parent
                    : ResolveImportButton()?.transform.parent;
            _playPauseButton ??= FindButton(parent, PlayPauseButtonName);
            _recordButton ??= FindButton(parent, RecordButtonName);
            _stopButton ??= FindButton(parent, StopButtonName);
            _timelineSlider ??= FindSlider(parent, TimelineSliderName);
            ResolveTimelineLabel();

            if (_playPauseButton != null)
            {
                _playPauseButton.onClick = new Button.ButtonClickedEvent();
                _playPauseButton.onClick.AddListener(HandlePlayPauseClick);
            }

            if (_stopButton != null)
            {
                _stopButton.onClick = new Button.ButtonClickedEvent();
                _stopButton.onClick.AddListener(HandleStopClick);
            }

            if (_recordButton != null)
            {
                _recordButton.onClick = new Button.ButtonClickedEvent();
                _recordButton.onClick.AddListener(HandleRecordClick);
            }

            if (_timelineSlider != null)
            {
                _timelineSlider.onValueChanged = new Slider.SliderEvent();
                _timelineSlider.onValueChanged.AddListener(HandleTimelineValueChanged);
            }

            Refresh();
        }

        private void HandlePlayPauseClick()
        {
            if (_pipeline == null)
            {
                return;
            }

            if (_pipeline.IsImportedMotionPlaying)
            {
                _pipeline.TryPauseImportedMotion();
            }
            else
            {
                _pipeline.TryPlayImportedMotion();
            }

            Refresh();
        }

        private void HandleStopClick()
        {
            _pipeline?.TryStopImportedMotion();
            Refresh();
        }

        private void HandleRecordClick()
        {
            if (_pipeline == null)
            {
                return;
            }

            if (_pipeline.IsImportedMotionRecording)
            {
                _pipeline.TryStopImportedMotionRecording();
            }
            else
            {
                _pipeline.TryStartImportedMotionRecording();
            }

            Refresh();
        }

        private void HandleTimelineValueChanged(float frameValue)
        {
            if (_pipeline == null || !_pipeline.HasPreparedImportedMotion)
            {
                return;
            }

            _pipeline.TrySeekImportedMotionFrame(Mathf.RoundToInt(frameValue));
            Refresh();
        }

        private void Refresh()
        {
            bool hasPreparedMotion =
                _pipeline != null && _pipeline.HasPreparedImportedMotion;
            bool isRecording =
                _pipeline != null && _pipeline.IsImportedMotionRecording;
            if (_playPauseButton != null)
            {
                _playPauseButton.interactable = hasPreparedMotion && !isRecording;
                SetLabel(
                    _playPauseButton,
                    _pipeline != null && _pipeline.IsImportedMotionPlaying
                        ? "일시정지"
                        : "재생");
            }

            if (_stopButton != null)
            {
                _stopButton.interactable = hasPreparedMotion;
            }
            if (_recordButton != null)
            {
                _recordButton.interactable = hasPreparedMotion;
                SetLabel(_recordButton, isRecording ? "녹화 중지" : "녹화");
            }
            if (_timelineSlider != null)
            {
                _timelineSlider.interactable = hasPreparedMotion && !isRecording;
                _timelineSlider.minValue = 0f;
                _timelineSlider.maxValue = hasPreparedMotion
                    ? _pipeline.ImportedMotionLastFrameIndex
                    : 0f;
                _timelineSlider.SetValueWithoutNotify(
                    hasPreparedMotion
                        ? _pipeline.ImportedMotionCurrentFrameIndex
                        : 0f);
            }

            SetTimelineLabel(hasPreparedMotion);
        }

        private void SetTimelineLabel(bool hasPreparedMotion)
        {
            string label = hasPreparedMotion
                ? $"프레임 {_pipeline.ImportedMotionCurrentFrameIndex} / " +
                  $"{_pipeline.ImportedMotionLastFrameIndex} · " +
                  $"{FormatTime(_pipeline.ImportedMotionCurrentTimeSeconds)} / " +
                  FormatTime(_pipeline.ImportedMotionClipLengthSeconds)
                : "프레임 - / -";

            if (_timelineLabel != null)
            {
                _timelineLabel.text = label;
                if (_timelineLabel is TextMeshProUGUI timelineLabel)
                {
                    KoreanUiTextFallback.Apply(timelineLabel);
                }
            }
            else if (_legacyTimelineLabel != null)
            {
                _legacyTimelineLabel.text = label;
                ApplyLegacyKoreanFont(_legacyTimelineLabel);
            }
        }

        private static Button CreateControlButton(
            Button template,
            string objectName,
            string label,
            float verticalOffset,
            UnityEngine.Events.UnityAction onClick)
        {
            Button button = Instantiate(template, template.transform.parent);
            button.name = objectName;
            button.gameObject.hideFlags = HideFlags.DontSave;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);

            if (button.transform is RectTransform rectTransform &&
                template.transform is RectTransform templateRectTransform)
            {
                rectTransform.anchoredPosition =
                    templateRectTransform.anchoredPosition + Vector2.down * verticalOffset;
                rectTransform.SetAsLastSibling();
            }

            SetLabel(button, label);
            return button;
        }

        private static Slider CreateTimelineSlider(
            Button template,
            float verticalOffset,
            UnityEngine.Events.UnityAction<float> onValueChanged,
            out TMP_Text timelineLabel,
            out Text legacyTimelineLabel)
        {
            var sliderObject = new GameObject(
                TimelineSliderName,
                typeof(RectTransform),
                typeof(Slider));
            sliderObject.hideFlags = HideFlags.DontSave;
            sliderObject.transform.SetParent(template.transform.parent, false);

            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            if (template.transform is RectTransform templateRect)
            {
                sliderRect.anchorMin = templateRect.anchorMin;
                sliderRect.anchorMax = templateRect.anchorMax;
                sliderRect.pivot = templateRect.pivot;
                sliderRect.anchoredPosition =
                    templateRect.anchoredPosition + Vector2.down * verticalOffset;
                sliderRect.sizeDelta = new Vector2(
                    Mathf.Max(240f, templateRect.rect.width),
                    TimelineHeight);
                sliderRect.SetAsLastSibling();
            }

            Image background = CreateSliderImage(
                sliderRect,
                "Background",
                new Color32(40, 43, 50, 230));
            SetStretch(background.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform fillArea = CreateSliderRect(sliderRect, "Fill Area");
            SetStretch(fillArea, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            Image fill = CreateSliderImage(
                fillArea,
                "Fill",
                new Color32(56, 204, 190, 255));
            SetStretch(fill.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform handleArea = CreateSliderRect(sliderRect, "Handle Slide Area");
            SetStretch(handleArea, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            Image handle = CreateSliderImage(
                handleArea,
                "Handle",
                new Color32(240, 240, 240, 255));
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(18f, TimelineHeight + 4f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 0f;
            slider.wholeNumbers = true;
            slider.onValueChanged.AddListener(onValueChanged);

            timelineLabel = null;
            legacyTimelineLabel = null;
            TMP_Text tmpTemplate = template.GetComponentInChildren<TMP_Text>(true);
            if (tmpTemplate != null)
            {
                timelineLabel = Instantiate(tmpTemplate, sliderRect);
                ConfigureTimelineLabel(timelineLabel.rectTransform);
                timelineLabel.name = TimelineLabelName;
                timelineLabel.raycastTarget = false;
            }
            else
            {
                Text legacyTemplate = template.GetComponentInChildren<Text>(true);
                if (legacyTemplate != null)
                {
                    legacyTimelineLabel = Instantiate(legacyTemplate, sliderRect);
                    ConfigureTimelineLabel(
                        legacyTimelineLabel.GetComponent<RectTransform>());
                    legacyTimelineLabel.name = TimelineLabelName;
                    legacyTimelineLabel.raycastTarget = false;
                }
            }

            return slider;
        }

        private static RectTransform CreateSliderRect(RectTransform parent, string name)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.hideFlags = HideFlags.DontSave;
            rectObject.transform.SetParent(parent, false);
            return rectObject.GetComponent<RectTransform>();
        }

        private static Image CreateSliderImage(
            RectTransform parent,
            string name,
            Color color)
        {
            var imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.hideFlags = HideFlags.DontSave;
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void SetStretch(
            RectTransform rectTransform,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static void ConfigureTimelineLabel(RectTransform labelRect)
        {
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 8f);
            labelRect.sizeDelta = new Vector2(0f, TimelineHeight);

            TMP_Text tmpLabel = labelRect.GetComponent<TMP_Text>();
            if (tmpLabel != null)
            {
                tmpLabel.enableAutoSizing = true;
                tmpLabel.fontSizeMin = MinimumTimelineFontSize;
                tmpLabel.fontSizeMax = Mathf.Max(
                    MinimumTimelineFontSize,
                    tmpLabel.fontSize);
            }

            Text legacyLabel = labelRect.GetComponent<Text>();
            if (legacyLabel != null)
            {
                legacyLabel.resizeTextForBestFit = true;
                legacyLabel.resizeTextMinSize = Mathf.RoundToInt(MinimumTimelineFontSize);
                legacyLabel.resizeTextMaxSize = Mathf.Max(
                    legacyLabel.resizeTextMinSize,
                    legacyLabel.fontSize);
            }
        }

        private static void SetLabel(Button button, string label)
        {
            TMP_Text tmpLabel = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                tmpLabel.text = label;
                if (tmpLabel is TextMeshProUGUI tmpLabelUi)
                {
                    KoreanUiTextFallback.Apply(tmpLabelUi);
                }
                return;
            }

            Text legacyLabel = button.GetComponentInChildren<Text>(true);
            if (legacyLabel != null)
            {
                legacyLabel.text = label;
                ApplyLegacyKoreanFont(legacyLabel);
            }
        }

        private static void ApplyLegacyKoreanFont(Text label)
        {
            if (label != null &&
                KoreanUiFontResolver.ContainsKorean(label.text) &&
                KoreanUiFontResolver.TryGetLegacyFont(out Font koreanFont))
            {
                label.font = koreanFont;
            }
        }

        private static Button ResolveImportButton()
        {
            GameObject buttonObject = GameObject.Find(ImportButtonName);
            return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
        }

        private static Button ResolveButton(string objectName)
        {
            GameObject buttonObject = GameObject.Find(objectName);
            return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
        }

        private static Button FindButton(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == objectName)
                {
                    return child.GetComponent<Button>();
                }
            }

            return null;
        }

        private static Slider FindSlider(Transform parent, string objectName)
        {
            if (parent == null)
            {
                return null;
            }

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == objectName)
                {
                    return child.GetComponent<Slider>();
                }
            }

            return null;
        }

        private void ResolveTimelineLabel()
        {
            if (_timelineSlider == null)
            {
                return;
            }

            _timelineLabel ??= _timelineSlider.GetComponentInChildren<TMP_Text>(true);
            _legacyTimelineLabel ??=
                _timelineSlider.GetComponentInChildren<Text>(true);
        }

        private static string FormatTime(float timeSeconds)
        {
            double safeTime = float.IsNaN(timeSeconds) ||
                              float.IsInfinity(timeSeconds) ||
                              timeSeconds < 0f
                ? 0d
                : timeSeconds;
            TimeSpan time = TimeSpan.FromSeconds(safeTime);
            return time.TotalHours >= 1d
                ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}"
                : $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
        }

        private static void DestroyControlButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(button.gameObject);
            }
            else
            {
                DestroyImmediate(button.gameObject);
            }
        }

        private static void DestroyControlObject(Component component)
        {
            if (component == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(component.gameObject);
            }
            else
            {
                DestroyImmediate(component.gameObject);
            }
        }
    }
}
